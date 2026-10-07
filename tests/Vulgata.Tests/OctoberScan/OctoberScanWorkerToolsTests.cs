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
