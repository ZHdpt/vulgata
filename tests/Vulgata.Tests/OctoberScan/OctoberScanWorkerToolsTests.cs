using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Real runtime and closed facades over inert source/native adapters; no processes or providers.</summary>
public sealed class OctoberScanWorkerToolsTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("list_prepared_source", SourceMetadataOperation.List)]
    [InlineData("read_prepared_source", SourceMetadataOperation.ReadTextRange)]
    [InlineData("search_prepared_source", SourceMetadataOperation.SearchText)]
    [InlineData("navigate_java_definition", SourceMetadataOperation.JavaDefinition)]
    [InlineData("navigate_java_references", SourceMetadataOperation.JavaReferences)]
    [InlineData("codegraph_explore", SourceMetadataOperation.CodeGraphExplore)]
    public async Task ClosedFacade_ActualInvocationRecordsItsInputBeforeReturningAsync(string name, SourceMetadataOperation operation)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await OctoberScanToolRuntimeTests.RuntimeHarness.CreateAsync();
        var source = new Frame(host.Source.Origin with { RepositoryId = controlled.RepositoryId, RootBindingIdentity = controlled.Approval.RootBindingIdentity, PreparedVersion = controlled.Approval.ApprovalIdentity, RelativePath = "a.java", SourceDigest = OctoberKnowledgePolicy.Hash("source") });
        var request = host.Request with { Source = source, Contributors = [new(ContributorKind.Source, source.Origin.RepositoryId, null, source.Origin.RootBindingIdentity, null, source.Origin.SourceDigest)], Advisory = [] };
        var ledger = new OctoberWorkerInputLedger(request, (_, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(source), (_, _) => Task.FromResult(DisclosureStatus.Allowed));
        if (operation == SourceMetadataOperation.SearchText)
        {
            var context = controlled.Context(_ => new() { RepositoryId = controlled.RepositoryId, Operation = PreparedSourceOperation.List, Outcome = PreparedSourceOutcome.CompleteEmpty });
            context.Search = _ => new() { RepositoryId = controlled.RepositoryId, Operation = PreparedSourceOperation.SearchText, Outcome = PreparedSourceOutcome.CompleteEmpty };
            controlled.Sources.Enqueue(context);
        }
        await using var runtime = controlled.CreateRuntime();
        var tools = await new OctoberWorkerTools(runtime, ledger).CreateAsync();
        Assert.Equal(new[] { "codegraph_explore", "list_prepared_source", "navigate_java_definition", "navigate_java_references", "read_prepared_source", "search_prepared_source" }, tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray());
        var arguments = new AIFunctionArguments();
        if (operation == SourceMetadataOperation.List || operation == SourceMetadataOperation.SearchText)
        {
            arguments["relativeDirectory"] = ""; arguments["pageSize"] = 10;
            if (operation == SourceMetadataOperation.SearchText) arguments["literal"] = "source";
        }
        else if (operation == SourceMetadataOperation.CodeGraphExplore) { arguments["query"] = "source"; arguments["maxFiles"] = 4; }
        else
        {
            arguments["relativePath"] = "a.java";
            if (operation == SourceMetadataOperation.ReadTextRange) { arguments["byteOffset"] = 0L; arguments["byteCount"] = 6; }
            else { arguments["line"] = 1; arguments["character"] = 1; arguments["maximumLocations"] = 10; }
        }
        var result = await tools.Single(tool => tool.Name == name).InvokeAsync(arguments);
        Assert.NotNull(result);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Equal(operation, Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.SourceMetadata)).MetadataOrigin!.Operation);
        if (operation == SourceMetadataOperation.ReadTextRange) Assert.Equal("source", Assert.IsType<PreparedSourceResult>(result).Text);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameRepositoryDifferentRootOrApproval_RejectsBeforeRuntimeUseAsync(bool wrongApproval)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await OctoberScanToolRuntimeTests.RuntimeHarness.CreateAsync();
        var source = new Frame(host.Source.Origin with
        {
            RepositoryId = controlled.RepositoryId,
            RootBindingIdentity = wrongApproval ? controlled.Approval.RootBindingIdentity : "foreign-root-binding",
            PreparedVersion = wrongApproval ? "foreign-approval-identity" : controlled.Approval.ApprovalIdentity,
            RelativePath = "a.java", SourceDigest = OctoberKnowledgePolicy.Hash("source"),
        });
        var request = host.Request with { Source = source, Contributors = [new(ContributorKind.Source, source.Origin.RepositoryId, null, source.Origin.RootBindingIdentity, null, source.Origin.SourceDigest)], Advisory = [] };
        var ledger = new OctoberWorkerInputLedger(request, (_, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(source), (_, _) => Task.FromResult(DisclosureStatus.Allowed));
        await using var runtime = controlled.CreateRuntime();
        await Assert.ThrowsAnyAsync<ArgumentException>(async () => await new OctoberWorkerTools(runtime, ledger).CreateAsync());
        Assert.Equal(0, controlled.Preparations);
        Assert.Equal(0, controlled.SourceIssues);
        Assert.Empty(controlled.CodeGraphSessions);
    }
    // Current behavior characterization: the facade denies malformed arguments, while
    // the composed ledger wrapper reads the required path before reaching that facade.
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("list_prepared_source", "missing")]
    [InlineData("list_prepared_source", "null")]
    [InlineData("list_prepared_source", "number")]
    [InlineData("read_prepared_source", "missing")]
    [InlineData("read_prepared_source", "null")]
    [InlineData("read_prepared_source", "number")]
    [InlineData("search_prepared_source", "missing")]
    [InlineData("search_prepared_source", "null")]
    [InlineData("search_prepared_source", "number")]
    public async Task CurrentBehavior_MalformedPath_FacadeDeniesButWrapperThrowsBeforeSourceAsync(string name, string defect)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await OctoberScanToolRuntimeTests.RuntimeHarness.CreateAsync();
        var ledger = Ledger(host, controlled);
        var arguments = SourceArguments(name);
        string key = name == "read_prepared_source" ? "relativePath" : "relativeDirectory";
        if (defect == "missing") arguments.Remove(key);
        else arguments[key] = defect == "null" ? null : 42;

        await using var direct = new CountingSource(controlled.Context(_ => throw new InvalidOperationException("Unexpected source call")));
        AIFunction facade = new PreparedSourceFunctions(direct).Create().Single(tool => tool.Name == name);
        var denied = Assert.IsType<PreparedSourceResult>(await facade.InvokeAsync(arguments));
        Assert.Equal(PreparedSourceOutcome.Denied, denied.Outcome);
        Assert.Null(denied.Text);
        Assert.Empty(denied.Entries);
        Assert.Empty(denied.Matches);
        Assert.Equal(0, direct.Calls);

        await using var runtime = controlled.CreateRuntime();
        var tools = await new OctoberWorkerTools(runtime, ledger).CreateAsync();
        Exception? error = await Record.ExceptionAsync(async () => await tools.Single(tool => tool.Name == name).InvokeAsync(arguments));
        Assert.NotNull(error);
        Assert.Equal(defect switch
        {
            "missing" => typeof(KeyNotFoundException),
            "null" => typeof(NullReferenceException),
            _ => typeof(InvalidOperationException)
        }, error.GetType());
        Assert.Equal(0, controlled.SourceIssues);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.DoesNotContain(snapshot.Contributors, contributor => contributor.Kind == ContributorKind.SourceMetadata);
    }

    // Current behavior characterization: direct facade cursors belong to its held
    // context. OctoberToolRuntime drains those pages itself and forbids caller cursors.
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("list_prepared_source")]
    [InlineData("search_prepared_source")]
    public async Task CurrentBehavior_Pagination_DirectFacadeContinuesButComposedRuntimeDrainsAndWithholdsCallerCursorAsync(string name)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await OctoberScanToolRuntimeTests.RuntimeHarness.CreateAsync();
        PreparedSourceOperation operation = name == "list_prepared_source" ? PreparedSourceOperation.List : PreparedSourceOperation.SearchText;
        int directPages = 0;
        PreparedSourceResult DirectPage(string? cursor)
        {
            Assert.Equal(directPages++ == 0 ? null : "controlled-cursor", cursor);
            return Page(controlled.RepositoryId, operation, cursor is null);
        }
        var directContext = controlled.Context(request => DirectPage(request.Continuation));
        directContext.Search = request => DirectPage(request.Continuation);
        await using var direct = new CountingSource(directContext);
        var facade = new PreparedSourceFunctions(direct).Create().Single(tool => tool.Name == name);
        var arguments = SourceArguments(name);
        var first = Assert.IsType<PreparedSourceResult>(await facade.InvokeAsync(arguments));
        Assert.Equal("controlled-cursor", first.Continuation);
        arguments["continuation"] = first.Continuation;
        var next = Assert.IsType<PreparedSourceResult>(await facade.InvokeAsync(arguments));
        Assert.Null(next.Continuation);
        Assert.Equal(2, direct.Calls);
        Assert.Equal(2, directPages);

        int runtimePages = 0;
        PreparedSourceResult RuntimePage(string? cursor)
        {
            Assert.Equal(runtimePages++ == 0 ? null : "controlled-cursor", cursor);
            return Page(controlled.RepositoryId, operation, cursor is null);
        }
        var context = controlled.Context(request => RuntimePage(request.Continuation));
        context.Search = request => RuntimePage(request.Continuation);
        controlled.Sources.Enqueue(context);
        var ledger = Ledger(host, controlled);
        await using var runtime = controlled.CreateRuntime();
        var wrapped = (await new OctoberWorkerTools(runtime, ledger).CreateAsync()).Single(tool => tool.Name == name);
        Assert.Contains("string", wrapped.JsonSchema.GetProperty("properties").GetProperty("continuation")
            .GetProperty("type").EnumerateArray().Select(value => value.GetString()));
        var drained = Assert.IsType<PreparedSourceResult>(await wrapped.InvokeAsync(SourceArguments(name)));
        Assert.Equal(PreparedSourceOutcome.CompleteEmpty, drained.Outcome);
        Assert.Null(drained.Continuation);
        Assert.Equal(2, runtimePages);
        Assert.Equal(1, controlled.SourceIssues);
        Assert.True(await ledger.VerifyCurrentAsync());

        // This advertised string cursor still invokes the wrapper/facade, but the runtime
        // denies it before another source grant or page. The ledger withholds that denial.
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await wrapped.InvokeAsync(arguments));
        Assert.Equal(2, runtimePages);
        Assert.Equal(1, controlled.SourceIssues);
        Assert.Null(await ledger.FreezeForCompletionAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentBehavior_CodeGraphOptionalMaximum_OmittedIsAcceptedButExplicitNullIsWithheldAsync(bool explicitNull)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await OctoberScanToolRuntimeTests.RuntimeHarness.CreateAsync();
        var ledger = Ledger(host, controlled);
        await using var runtime = controlled.CreateRuntime();
        var function = (await new OctoberWorkerTools(runtime, ledger).CreateAsync()).Single(tool => tool.Name == "codegraph_explore");
        var arguments = new AIFunctionArguments { ["query"] = "source" };
        if (explicitNull) arguments["maxFiles"] = null;
        if (explicitNull)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await function.InvokeAsync(arguments));
            Assert.Null(await ledger.FreezeForCompletionAsync());
        }
        else
        {
            Assert.Equal(CodeGraphToolInvocationStatus.Complete,
                Assert.IsType<CodeGraphToolInvocationResult>(await function.InvokeAsync(arguments)).Status);
            var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
            Assert.Equal(SourceMetadataOperation.CodeGraphExplore,
                Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.SourceMetadata)).MetadataOrigin!.Operation);
        }
        Assert.Equal(0, controlled.SourceIssues);
    }

    private static AIFunctionArguments SourceArguments(string name) => name == "read_prepared_source"
        ? new() { ["relativePath"] = "a.java", ["byteOffset"] = 0L, ["byteCount"] = 6 }
        : name == "search_prepared_source"
            ? new() { ["relativeDirectory"] = "", ["pageSize"] = 10, ["literal"] = "source" }
            : new() { ["relativeDirectory"] = "", ["pageSize"] = 10 };

    private static PreparedSourceResult Page(Guid repository, PreparedSourceOperation operation, bool first) => new()
    {
        RepositoryId = repository, Operation = operation,
        Outcome = first ? PreparedSourceOutcome.Limited : PreparedSourceOutcome.CompleteEmpty,
        Continuation = first ? "controlled-cursor" : null,
        StoppingLimit = first ? PreparedSourceStoppingLimit.PageSize : null
    };

    private static OctoberWorkerInputLedger Ledger(OctoberScanAcceptanceHarness host, OctoberScanToolRuntimeTests.RuntimeHarness controlled)
    {
        var source = new Frame(host.Source.Origin with
        {
            RepositoryId = controlled.RepositoryId, RootBindingIdentity = controlled.Approval.RootBindingIdentity,
            PreparedVersion = controlled.Approval.ApprovalIdentity, RelativePath = "a.java", SourceDigest = OctoberKnowledgePolicy.Hash("source")
        });
        var request = host.Request with { Source = source, Contributors = [new(ContributorKind.Source, source.Origin.RepositoryId,
            null, source.Origin.RootBindingIdentity, null, source.Origin.SourceDigest)], Advisory = [] };
        return new(request, (_, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(source), (_, _) => Task.FromResult(DisclosureStatus.Allowed));
    }

    private sealed class CountingSource(IPreparedSourceReadContext inner) : IPreparedSourceReadContext
    {
        internal int Calls;
        public Guid RepositoryId => inner.RepositoryId;
        public PreparedSourceContextLifetime Lifetime => inner.Lifetime;
        public Task<PreparedSourceResult> ListAsync(PreparedSourceListRequest request, CancellationToken cancellationToken = default)
        { Calls++; return inner.ListAsync(request, cancellationToken); }
        public Task<PreparedSourceResult> ReadTextRangeAsync(PreparedSourceReadTextRangeRequest request, CancellationToken cancellationToken = default)
        { Calls++; return inner.ReadTextRangeAsync(request, cancellationToken); }
        public Task<PreparedSourceResult> SearchTextAsync(PreparedSourceSearchTextRequest request, CancellationToken cancellationToken = default)
        { Calls++; return inner.SearchTextAsync(request, cancellationToken); }
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class Frame(PreparedOrigin origin) : IOctoberVerifiedFileFrame
    {
        public PreparedOrigin Origin => origin;
        public string Text => "source";
        public long ByteLength => 6;
        public int LineCount => 1;
        public string FileIdentity => "controlled-wrapper-source";
        public Task<bool> VerifyCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
