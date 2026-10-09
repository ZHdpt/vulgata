using Microsoft.Extensions.AI;
using System.Text.Json;
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
    // Raw facade denial is preserved; the composed Worker boundary returns a fixed
    // non-data refusal and allows a corrected call without admitting invented input.
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
    public async Task InvalidPath_FacadeDeniesAndWorkerRefusesBeforeSource_ThenCorrectedCallSucceedsAsync(string name, string defect)
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
        var wrapped = tools.Single(tool => tool.Name == name);
        AssertRefusal(await wrapped.InvokeAsync(arguments));
        Assert.Equal(0, controlled.SourceIssues);
        Assert.True(await ledger.VerifyCurrentAsync());
        QueueEmptySearch(controlled, name);
        Assert.IsType<PreparedSourceResult>(await wrapped.InvokeAsync(SourceArguments(name)));
        Assert.Equal(1, controlled.SourceIssues);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Single(snapshot.Contributors.Where(contributor => contributor.Kind == ContributorKind.SourceMetadata));
    }

    // Current behavior characterization: direct facade cursors belong to its held
    // context. OctoberToolRuntime drains those pages itself and forbids caller cursors.
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("list_prepared_source")]
    [InlineData("search_prepared_source")]
    public async Task Pagination_RawSchemaPreserved_WorkerHidesCursorAndRefusesWithoutLosingLedgerAsync(string name)
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
        Assert.Contains("string", facade.JsonSchema.GetProperty("properties").GetProperty("continuation")
            .GetProperty("type").EnumerateArray().Select(value => value.GetString()));
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
        Assert.False(wrapped.JsonSchema.GetProperty("properties").TryGetProperty("continuation", out _));
        Assert.DoesNotContain("continuation", wrapped.JsonSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var drained = Assert.IsType<PreparedSourceResult>(await wrapped.InvokeAsync(SourceArguments(name)));
        Assert.Equal(PreparedSourceOutcome.CompleteEmpty, drained.Outcome);
        Assert.Null(drained.Continuation);
        Assert.Equal(2, runtimePages);
        Assert.Equal(1, controlled.SourceIssues);
        Assert.True(await ledger.VerifyCurrentAsync());

        AssertRefusal(await wrapped.InvokeAsync(arguments));
        arguments["continuation"] = null;
        AssertRefusal(await wrapped.InvokeAsync(arguments));
        Assert.Equal(2, runtimePages);
        Assert.Equal(1, controlled.SourceIssues);
        Assert.True(await ledger.VerifyCurrentAsync());
        QueueEmptySearch(controlled, name);
        Assert.IsType<PreparedSourceResult>(await wrapped.InvokeAsync(SourceArguments(name)));
        Assert.Equal(2, controlled.SourceIssues);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.SourceMetadata));

        // Explicit null remains a supported optional cursor on the raw facade itself.
        int nullCalls = 0;
        var nullContext = controlled.Context(request => { Assert.Null(request.Continuation); nullCalls++; return Page(controlled.RepositoryId, operation, false); });
        nullContext.Search = request => { Assert.Null(request.Continuation); nullCalls++; return Page(controlled.RepositoryId, operation, false); };
        await using var rawNull = new CountingSource(nullContext);
        Assert.IsType<PreparedSourceResult>(await new PreparedSourceFunctions(rawNull).Create().Single(tool => tool.Name == name).InvokeAsync(arguments));
        Assert.Equal(1, nullCalls);
        Assert.Equal(1, rawNull.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CodeGraphOptionalMaximum_OmittedIsAccepted_ExplicitNullRefusesWithoutPoisoningLedgerAsync(bool explicitNull)
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
            AssertRefusal(await function.InvokeAsync(arguments));
            Assert.True(await ledger.VerifyCurrentAsync());
            arguments.Remove("maxFiles");
            Assert.IsType<CodeGraphToolInvocationResult>(await function.InvokeAsync(arguments));
            var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
            Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.SourceMetadata));
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

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("navigate_java_definition", "relativePath", "missing")]
    [InlineData("navigate_java_definition", "relativePath", "null")]
    [InlineData("navigate_java_definition", "relativePath", "number")]
    [InlineData("navigate_java_references", "relativePath", "missing")]
    [InlineData("navigate_java_references", "relativePath", "null")]
    [InlineData("navigate_java_references", "relativePath", "number")]
    [InlineData("codegraph_explore", "query", "missing")]
    [InlineData("codegraph_explore", "query", "null")]
    [InlineData("codegraph_explore", "query", "number")]
    [InlineData("list_prepared_source", "pageSize", "zero")]
    [InlineData("read_prepared_source", "byteCount", "null")]
    [InlineData("search_prepared_source", "literal", "null")]
    [InlineData("navigate_java_definition", "line", "zero")]
    [InlineData("navigate_java_references", "maximumLocations", "null")]
    [InlineData("codegraph_explore", "query", "oversize")]
    [InlineData("read_prepared_source", "unexpected", "number")]
    public async Task InvalidClosedScalars_RefuseWithoutInputContribution_ThenCorrectedCallSucceedsAsync(string name, string key, string defect)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await OctoberScanToolRuntimeTests.RuntimeHarness.CreateAsync();
        controlled.Java.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var ledger = Ledger(host, controlled);
        await using var runtime = controlled.CreateRuntime();
        var function = (await new OctoberWorkerTools(runtime, ledger).CreateAsync()).Single(tool => tool.Name == name);
        var arguments = ToolArguments(name);
        if (defect == "missing") arguments.Remove(key);
        else arguments[key] = defect switch { "null" => null, "zero" => 0, "oversize" => new string('x', 513), _ => 42 };
        AssertRefusal(await function.InvokeAsync(arguments));
        Assert.Equal(0, controlled.SourceIssues);
        Assert.False(controlled.Java.Started.Task.IsCompleted);
        Assert.True(await ledger.VerifyCurrentAsync());
        QueueEmptySearch(controlled, name);
        object? result = await function.InvokeAsync(ToolArguments(name));
        Assert.NotNull(result);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.Source));
        Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.SourceMetadata));
        Assert.Equal(2, snapshot.Contributors.Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("authority")]
    [InlineData("source-denial")]
    [InlineData("foreign-repository")]
    public async Task GenuineAuthorityOrSourceFailure_RemainsFatalAndCannotBeRepairedAsArgumentsAsync(string failure)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await OctoberScanToolRuntimeTests.RuntimeHarness.CreateAsync();
        bool allowed = true;
        var ledger = Ledger(host, controlled, () => allowed ? DisclosureStatus.Allowed : DisclosureStatus.Denied);
        int sourceCalls = 0;
        controlled.Sources.Enqueue(controlled.Context(_ =>
        {
            sourceCalls++;
            return new PreparedSourceResult
            {
                RepositoryId = failure == "foreign-repository" ? Guid.NewGuid() : controlled.RepositoryId,
                Operation = PreparedSourceOperation.List,
                Outcome = failure == "source-denial" ? PreparedSourceOutcome.Denied : PreparedSourceOutcome.CompleteEmpty
            };
        }));
        await using var runtime = controlled.CreateRuntime();
        var function = (await new OctoberWorkerTools(runtime, ledger).CreateAsync()).Single(tool => tool.Name == "list_prepared_source");
        if (failure == "authority") allowed = false;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await function.InvokeAsync(SourceArguments(function.Name)));
        Assert.Equal(failure == "authority" ? 0 : 1, sourceCalls);
        Assert.Equal(sourceCalls, controlled.SourceIssues);
        allowed = true;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await function.InvokeAsync(SourceArguments(function.Name)));
        Assert.Equal(failure == "authority" ? 0 : 1, sourceCalls);
        Assert.Null(await ledger.FreezeForCompletionAsync());
    }

    private static void AssertRefusal(object? result)
    {
        Assert.NotNull(result);
        JsonElement json = JsonSerializer.SerializeToElement(result);
        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        JsonProperty property = Assert.Single(json.EnumerateObject());
        Assert.Equal("status", property.Name);
        Assert.Equal("InvalidArguments", property.Value.GetString());
    }

    private static void QueueEmptySearch(OctoberScanToolRuntimeTests.RuntimeHarness controlled, string name)
    {
        if (name != "search_prepared_source") return;
        var context = controlled.Context(_ => throw new InvalidOperationException("Unexpected listing"));
        context.Search = _ => new PreparedSourceResult { RepositoryId = controlled.RepositoryId,
            Operation = PreparedSourceOperation.SearchText, Outcome = PreparedSourceOutcome.CompleteEmpty };
        controlled.Sources.Enqueue(context);
    }

    private static AIFunctionArguments ToolArguments(string name) => name switch
    {
        "codegraph_explore" => new() { ["query"] = "source" },
        "navigate_java_definition" or "navigate_java_references" => new()
        { ["relativePath"] = "a.java", ["line"] = 1, ["character"] = 1, ["maximumLocations"] = 10 },
        _ => SourceArguments(name)
    };

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

    private static OctoberWorkerInputLedger Ledger(OctoberScanAcceptanceHarness host, OctoberScanToolRuntimeTests.RuntimeHarness controlled,
        Func<DisclosureStatus>? authority = null)
    {
        var source = new Frame(host.Source.Origin with
        {
            RepositoryId = controlled.RepositoryId, RootBindingIdentity = controlled.Approval.RootBindingIdentity,
            PreparedVersion = controlled.Approval.ApprovalIdentity, RelativePath = "a.java", SourceDigest = OctoberKnowledgePolicy.Hash("source")
        });
        var request = host.Request with { Source = source, Contributors = [new(ContributorKind.Source, source.Origin.RepositoryId,
            null, source.Origin.RootBindingIdentity, null, source.Origin.SourceDigest)], Advisory = [] };
        return new(request, (_, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(source), (_, _) => Task.FromResult(authority?.Invoke() ?? DisclosureStatus.Allowed));
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
