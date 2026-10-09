using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Tests.PreparedSource;
using Vulgata.Infrastructure.Source;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Analysis.CodeGraph;

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

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("configuration", PreparedJavaNavigationOutcome.ConfigurationUnavailable)]
    [InlineData("readiness", PreparedJavaNavigationOutcome.ReadinessFailed)]
    public async Task SettledJavaLimitation_RealRuntimeFrameAndLedgerPermitOrdinaryFileFallbackAsync(string failure, PreparedJavaNavigationOutcome expected)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await ContainedToolRuntime.CreateAsync(failure);
        var acquired = await controlled.Runtime.AcquireFileFrameAsync("a.java");
        var frame = Assert.IsType<OctoberPreparedSourceFrame>(acquired.Frame);
        var ledger = RuntimeLedger(host, controlled, frame);
        var owner = new OctoberWorkerTools(controlled.Runtime, ledger);
        var tools = await owner.CreateAsync();
        var result = Assert.IsType<PreparedJavaNavigationResult>(await tools.Single(tool => tool.Name == "navigate_java_definition")
            .InvokeAsync(ToolArguments("navigate_java_definition")));
        Assert.Equal(expected, result.Outcome);
        Assert.Empty(result.Locations);
        PreparedJavaLimitation limitation = Assert.Single(result.Limitations);
        Assert.Equal(failure == "configuration" ? "jdt_configuration_unavailable" : "jdt_readiness_failed", limitation.Code);
        Assert.Null(limitation.Source);
        Assert.Equal(1, controlled.JavaOpens); // No automatic retry.
        Assert.True(await frame.VerifyCurrentAsync());
        var read = Assert.IsType<PreparedSourceResult>(await tools.Single(tool => tool.Name == "read_prepared_source")
            .InvokeAsync(SourceArguments("read_prepared_source")));
        Assert.Equal(PreparedSourceOutcome.Complete, read.Outcome);
        Assert.Equal("source", read.Text);
        Assert.Equal(frame.Origin.RepositoryId, read.RepositoryId);
        Assert.Equal("a.java", read.ReadLocation!.RelativePath);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Single(snapshot.VerifiedSources);
        Assert.Equal(frame.Origin, snapshot.VerifiedSources[0].Origin);
        Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.Source));
        Assert.Equal(new[] { SourceMetadataOperation.ReadTextRange, SourceMetadataOperation.JavaDefinition }.Order(),
            snapshot.Contributors.Where(item => item.Kind == ContributorKind.SourceMetadata)
                .Select(item => item.MetadataOrigin!.Operation).Order());
        Assert.Equal(0, controlled.GraphCalls);
        Assert.Null(owner.FirstFailure);
        Assert.Null(new OctoberWorkerTools(controlled.Runtime, ledger).FirstFailure);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("denied")]
    [InlineData("authority-unavailable")]
    [InlineData("root-unavailable")]
    [InlineData("revoked")]
    [InlineData("root-binding")]
    [InlineData("source-content-change")]
    [InlineData("unknown")]
    [InlineData("cleanup-unconfirmed")]
    [InlineData("cancelled")]
    [InlineData("deadline")]
    [InlineData("required-graph")]
    [InlineData("graph-unavailable")]
    [InlineData("graph-failed")]
    [InlineData("graph-timedout")]
    [InlineData("graph-foreign")]
    [InlineData("graph-missing")]
    [InlineData("java-foreign")]
    public async Task FatalToolFailure_RealRuntimeFrameCannotAuthorizeFileFallbackAsync(string failure)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await ContainedToolRuntime.CreateAsync(failure);
        var frame = Assert.IsType<OctoberPreparedSourceFrame>((await controlled.Runtime.AcquireFileFrameAsync("a.java")).Frame);
        var ledger = RuntimeLedger(host, controlled, frame);
        var owner = new OctoberWorkerTools(controlled.Runtime, ledger);
        var tools = await owner.CreateAsync();
        bool graphFailure = failure == "required-graph" || failure.StartsWith("graph-", StringComparison.Ordinal);
        string name = graphFailure ? "codegraph_explore" : "navigate_java_definition";
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await tools.Single(tool => tool.Name == name).InvokeAsync(ToolArguments(name)));
        var diagnostic = Assert.IsType<OctoberToolDiagnosticSnapshot>(owner.FirstFailure);
        Assert.Equal(graphFailure ? SourceMetadataOperation.CodeGraphExplore : SourceMetadataOperation.JavaDefinition,
            diagnostic.Operation);
        OctoberToolDiagnosticSnapshot expectedDiagnostic = failure switch
        {
            "required-graph" => new(SourceMetadataOperation.CodeGraphExplore, OctoberToolFailureBoundary.RuntimeInvoke),
            "graph-unavailable" => new(SourceMetadataOperation.CodeGraphExplore, OctoberToolFailureBoundary.RuntimeInvoke,
                CodeGraphStatus: CodeGraphToolInvocationStatus.Unavailable),
            "graph-failed" => new(SourceMetadataOperation.CodeGraphExplore, OctoberToolFailureBoundary.RuntimeInvoke,
                CodeGraphStatus: CodeGraphToolInvocationStatus.Failed),
            "graph-timedout" => new(SourceMetadataOperation.CodeGraphExplore, OctoberToolFailureBoundary.RuntimeInvoke,
                CodeGraphStatus: CodeGraphToolInvocationStatus.TimedOut),
            "graph-foreign" or "graph-missing" => new(SourceMetadataOperation.CodeGraphExplore, OctoberToolFailureBoundary.SourceCurrentness,
                CodeGraphStatus: CodeGraphToolInvocationStatus.Complete),
            "java-foreign" => new(SourceMetadataOperation.JavaDefinition, OctoberToolFailureBoundary.SourceCurrentness,
                JavaOutcome: PreparedJavaNavigationOutcome.Observed, CleanupConfirmed: true),
            "unknown" => new(SourceMetadataOperation.JavaDefinition, OctoberToolFailureBoundary.RuntimeOpen),
            "source-content-change" => new(SourceMetadataOperation.JavaDefinition, OctoberToolFailureBoundary.ResultAdmission,
                JavaOutcome: PreparedJavaNavigationOutcome.ConfigurationUnavailable),
            "revoked" or "root-binding" => new(SourceMetadataOperation.JavaDefinition,
                OctoberToolFailureBoundary.SourceCurrentness, CleanupConfirmed: true),
            _ => new(SourceMetadataOperation.JavaDefinition, OctoberToolFailureBoundary.RuntimeOpen,
                JavaOpenStatus: failure switch
                {
                    "denied" => PreparedJavaNavigationOpenStatus.Denied,
                    "authority-unavailable" => PreparedJavaNavigationOpenStatus.AuthorityUnavailable,
                    "root-unavailable" => PreparedJavaNavigationOpenStatus.RootUnavailable,
                    "cancelled" => PreparedJavaNavigationOpenStatus.Cancelled,
                    "deadline" => PreparedJavaNavigationOpenStatus.DeadlineExceeded,
                    _ => PreparedJavaNavigationOpenStatus.ConfigurationUnavailable
                }, CleanupConfirmed: failure != "cleanup-unconfirmed")
        };
        Assert.Equal(expectedDiagnostic, diagnostic);
        Assert.Equal(graphFailure ? 0 : 1, controlled.JavaOpens);
        Assert.Equal(graphFailure ? 1 : 0, controlled.GraphCalls);
        Assert.Equal(failure == "java-foreign" ? 1 : 0, controlled.JavaCloses);
        Assert.False(await frame.VerifyCurrentAsync());
        int issuesAfterFailure = controlled.SourceIssues;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await tools.Single(tool => tool.Name == "read_prepared_source")
            .InvokeAsync(SourceArguments("read_prepared_source")));
        Assert.Equal(issuesAfterFailure, controlled.SourceIssues);
        Assert.Null(await ledger.FreezeForCompletionAsync());
        Assert.Same(diagnostic, owner.FirstFailure);
        Assert.Null(new OctoberWorkerTools(controlled.Runtime, ledger).FirstFailure);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SourceRuntimeException_RecordsUnknownFirstCauseWithoutSynthesizedAuthorityStatusAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        await using var controlled = await OctoberScanToolRuntimeTests.RuntimeHarness.CreateAsync();
        int sourceCalls = 0;
        controlled.Sources.Enqueue(controlled.Context(_ =>
        {
            sourceCalls++;
            throw new IOException("PRIVATE_CONTROLLED_SOURCE_EXCEPTION");
        }));
        var ledger = Ledger(host, controlled);
        await using var runtime = controlled.CreateRuntime();
        var owner = new OctoberWorkerTools(runtime, ledger);
        var tools = await owner.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await tools.Single(tool => tool.Name == "list_prepared_source")
            .InvokeAsync(SourceArguments("list_prepared_source")));
        var diagnostic = Assert.IsType<OctoberToolDiagnosticSnapshot>(owner.FirstFailure);
        Assert.Equal(new OctoberToolDiagnosticSnapshot(SourceMetadataOperation.List, OctoberToolFailureBoundary.RuntimeInvoke), diagnostic);
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(diagnostic), StringComparison.Ordinal);
        Assert.Equal(1, sourceCalls);
        Assert.Equal(1, controlled.SourceIssues);
        var frame = await runtime.AcquireFileFrameAsync("a.java");
        Assert.Equal(OctoberSourceFrameStatus.Unavailable, frame.Status);
        Assert.Null(frame.Frame);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await tools.Single(tool => tool.Name == "read_prepared_source")
            .InvokeAsync(SourceArguments("read_prepared_source")));
        Assert.Equal(1, controlled.SourceIssues);
        Assert.Equal(1, sourceCalls);
        Assert.Null(await ledger.FreezeForCompletionAsync());
        Assert.Same(diagnostic, owner.FirstFailure);
    }

    private static OctoberWorkerInputLedger RuntimeLedger(OctoberScanAcceptanceHarness host, ContainedToolRuntime controlled,
        OctoberPreparedSourceFrame frame)
    {
        var request = host.Request with { Source = frame, Contributors = [new(ContributorKind.Source, frame.Origin.RepositoryId,
            null, frame.Origin.RootBindingIdentity, null, frame.Origin.SourceDigest)], Advisory = [] };
        return new(request, async (path, token) => (await controlled.Runtime.AcquireFileFrameAsync(path, cancellationToken: token)).Frame,
            (_, _) => Task.FromResult(DisclosureStatus.Allowed));
    }

    // Existing inert runtime dependency seam, but real contained source grants and
    // a runtime-acquired primary frame. Public factory lease proof is tested separately.
    private sealed class ContainedToolRuntime : IAsyncDisposable
    {
        private readonly PreparedSourceFixture _fixture;
        private readonly PreparedCodeGraphConnection _descriptor;
        private readonly bool _expectCleanupFailure;
        internal OctoberToolRuntime Runtime { get; }
        internal int SourceIssues;
        internal int JavaOpens;
        internal int JavaCloses;
        internal int GraphCalls;
        private ContainedToolRuntime(PreparedSourceFixture fixture, string failure)
        {
            _fixture = fixture;
            _expectCleanupFailure = failure == "cleanup-unconfirmed";
            fixture.Authority.Approve(fixture.Approval);
            var issuer = new PreparedSourceContextIssuer(fixture.Authority);
            _descriptor = new(fixture.Approval.RepositoryId, Path.Combine(fixture.SandboxPath, "never-executed.ps1"),
                "never-executed", [], fixture.PreparedRootPath, new Dictionary<string, string>(), static () => { },
                _ => new InertGraph(this, failure));
            Runtime = new(fixture.Approval, TimeSpan.FromMinutes(5), new OctoberToolRuntimeDependencies(
                (_, _) => Task.FromResult(_descriptor),
                (_, remaining, token) =>
                {
                    SourceIssues++;
                    return issuer.IssueAsync(new(fixture.Approval, fixture.PreparedRootPath,
                        PreparedSourceGrantLimits.Default with { Deadline = remaining }), _descriptor, token);
                },
                issuer.ObserveFileIdentityAsync,
                (_, _) =>
                {
                    JavaOpens++;
                    if (failure == "java-foreign")
                        return Task.FromResult<IPreparedJavaNavigationContext>(new ForeignResultJava(this));
                    if (failure == "revoked") fixture.Authority.Revoke();
                    if (failure == "root-binding") fixture.Authority.Approve(fixture.Approval with { RootBindingIdentity = "changed-controlled-binding" });
                    if (failure == "source-content-change")
                        File.WriteAllText(Path.Combine(fixture.PreparedRootPath, "a.java"), "mutate", new UTF8Encoding(false));
                    Exception error = failure == "unknown" ? new IOException("Controlled unknown open failure") :
                        new PreparedJdtLsOpenFailureException(failure switch
                        {
                            "readiness" => PreparedJavaNavigationOpenStatus.ReadinessFailed,
                            "denied" => PreparedJavaNavigationOpenStatus.Denied,
                            "authority-unavailable" => PreparedJavaNavigationOpenStatus.AuthorityUnavailable,
                            "root-unavailable" => PreparedJavaNavigationOpenStatus.RootUnavailable,
                            "cancelled" => PreparedJavaNavigationOpenStatus.Cancelled,
                            "deadline" => PreparedJavaNavigationOpenStatus.DeadlineExceeded,
                            _ => PreparedJavaNavigationOpenStatus.ConfigurationUnavailable
                        }, cleanupConfirmed: failure != "cleanup-unconfirmed");
                    return Task.FromException<IPreparedJavaNavigationContext>(error);
                },
                (descriptor, _) => descriptor.OpenSession()));
        }
        internal static async Task<ContainedToolRuntime> CreateAsync(string failure)
        {
            var fixture = await PreparedSourceFixture.CreateAsync();
            Directory.CreateDirectory(Path.Combine(fixture.PreparedRootPath, ".codegraph"));
            await File.WriteAllTextAsync(Path.Combine(fixture.PreparedRootPath, "a.java"), "source", new UTF8Encoding(false));
            return new(fixture, failure);
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_expectCleanupFailure)
                {
                    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Runtime.DisposeAsync().AsTask());
                    Assert.Equal("Tool cleanup remains unconfirmed.", error.Message);
                }
                else await Runtime.DisposeAsync();
            }
            // This test adapter started no native resource. Release its explicitly owned
            // descriptor after asserting quarantine; this is not production settlement.
            finally { await _descriptor.DisposeAsync(); await _fixture.DisposeAsync(); }
        }
        private sealed class InertGraph(ContainedToolRuntime owner, string failure) : ICodeGraphWorkerToolSession
        {
            public Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken)
                => Task.FromResult(new CodeGraphToolReadiness(CodeGraphToolReadinessStatus.Ready, owner._fixture.Approval.RepositoryId));
            public Task<CodeGraphToolInvocationResult> ExploreAsync(string query, int maxFiles, CancellationToken cancellationToken)
            {
                owner.GraphCalls++;
                return failure == "required-graph" ? Task.FromException<CodeGraphToolInvocationResult>(new IOException("Controlled required graph failure")) :
                    Task.FromResult(new CodeGraphToolInvocationResult
                    {
                        RepositoryId = failure == "graph-missing" ? null :
                            failure == "graph-foreign" ? Guid.NewGuid() : owner._fixture.Approval.RepositoryId,
                        Status = failure switch
                        {
                            "graph-unavailable" => CodeGraphToolInvocationStatus.Unavailable,
                            "graph-failed" => CodeGraphToolInvocationStatus.Failed,
                            "graph-timedout" => CodeGraphToolInvocationStatus.TimedOut,
                            _ => CodeGraphToolInvocationStatus.Complete
                        }
                    });
            }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        private sealed class ForeignResultJava(ContainedToolRuntime owner) : IPreparedJavaNavigationContext
        {
            public Guid RepositoryId => owner._fixture.Approval.RepositoryId;
            public Task<PreparedJavaNavigationResult> GetDefinitionAsync(PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default)
                => Task.FromResult(new PreparedJavaNavigationResult(Guid.NewGuid(), PreparedJavaNavigationOperation.Definition,
                    PreparedJavaNavigationOutcome.Observed, [], [], null));
            public Task<PreparedJavaNavigationResult> GetReferencesAsync(PreparedJavaNavigationRequest request, CancellationToken cancellationToken = default)
                => Task.FromResult(new PreparedJavaNavigationResult(Guid.NewGuid(), PreparedJavaNavigationOperation.References,
                    PreparedJavaNavigationOutcome.Observed, [], [], null));
            public ValueTask DisposeAsync() { owner.JavaCloses++; return ValueTask.CompletedTask; }
        }
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
