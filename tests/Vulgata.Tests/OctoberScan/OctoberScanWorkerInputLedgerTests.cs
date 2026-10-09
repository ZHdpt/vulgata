using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanWorkerInputLedgerTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UncitedSupport_IsRetainedWithAdvisory_AndAcceptedByActualOwnerWriterAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Support.cs", SourceDigest = OctoberKnowledgePolicy.Hash("helper();") }, "helper();");
        var ledger = Ledger(host, support);
        Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, support)).Status);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Contains(host.Fixture.UncitedContributor, snapshot.Contributors);
        Assert.Contains(snapshot.Contributors, item => item.SourceOrigin == support.Origin);
        Assert.Contains(snapshot.Contributors, item => item.Kind == ContributorKind.SourceMetadata);
        Assert.True(await ledger.VerifySnapshotAsync(snapshot));
        var proposal = host.Proposal with { Contributors = snapshot.Contributors };
        proposal = proposal with { CanonicalProposalDigest = OctoberKnowledgePolicy.ComputeProposalDigest(proposal) };
        Assert.True(OctoberKnowledgePolicy.ValidateInputs(proposal, snapshot.VerifiedSources));
        // Controlled completion capability isolates ledger/owner conformance; full runner tests own terminal sealing.
        var writer = new OctoberKnowledgeWriter(host.Fixture.Factory, host.Fixture.Completion, ledger,
            host.Fixture.Authorization, host.Fixture.Guard, host.Fixture.Log, host.Fixture.FreezeAuthority);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await writer.AcceptCompletedFileAsync(host.Fixture.Seal(proposal))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task EqualBytesAtDifferentPaths_RemainDistinctInputsAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var first = new Frame(host.Source.Origin with { RelativePath = "src/A.cs", SourceDigest = OctoberKnowledgePolicy.Hash("same") }, "same");
        var second = new Frame(first.Origin with { RelativePath = "src/B.cs" }, "same");
        var ledger = Ledger(host, first, second);
        Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, first)).Status);
        Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, second)).Status);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Contains(snapshot.VerifiedSources, item => item.Origin == first.Origin);
        Assert.Contains(snapshot.VerifiedSources, item => item.Origin == second.Origin);
        Assert.Equal(3, snapshot.VerifiedSources.Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MetadataListing_DoesNotAcquireListedContent_AndRetainsMissingLimitationAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        int acquisitions = 0;
        var ledger = new OctoberWorkerInputLedger(host.Request, (_, _) => { acquisitions++; return Task.FromResult<IOctoberVerifiedFileFrame?>(null); }, (_, _) => Task.FromResult(DisclosureStatus.Allowed));
        var result = new PreparedSourceResult { RepositoryId = host.Source.Origin.RepositoryId, Operation = PreparedSourceOperation.List, Outcome = PreparedSourceOutcome.Complete,
            Entries = [new(new(host.Source.Origin.RepositoryId, "missing.cs"), PreparedSourceEntryKind.RegularFile, 9)],
            Limitations = [new(new(host.Source.Origin.RepositoryId, "missing.cs"), PreparedSourceLimitationReason.Missing)] };
        var admitted = await ledger.AdmitToolResultAsync(SourceMetadataOperation.List, ".", "{\"relativeDirectory\":\"\",\"pageSize\":20}", result);
        Assert.Equal(DisclosureStatus.Allowed, admitted.Status);
        Assert.Equal(result.Limitations, Assert.IsType<PreparedSourceResult>(admitted.Result).Limitations);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Equal(0, acquisitions);
        Assert.Single(snapshot.VerifiedSources);
        var metadata = Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.SourceMetadata));
        Assert.Equal(".", metadata.MetadataOrigin!.NormalizedScopePath);
        Assert.Equal(SourceMetadataOperation.List, metadata.MetadataOrigin.Operation);
        var proposal = host.Proposal with { Contributors = snapshot.Contributors };
        proposal = proposal with { CanonicalProposalDigest = OctoberKnowledgePolicy.ComputeProposalDigest(proposal) };
        Assert.True(OctoberKnowledgePolicy.ValidateInputs(proposal, snapshot.VerifiedSources));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("text")]
    [InlineData("repository")]
    [InlineData("range")]
    [InlineData("request")]
    public async Task ReadMismatch_WithholdsPayloadAsync(string corruption)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Support.cs", SourceDigest = OctoberKnowledgePolicy.Hash("support") }, "support");
        var ledger = Ledger(host, support);
        var result = Result(support);
        if (corruption == "text") result = result with { Text = "foreign" };
        if (corruption == "repository") result = result with { RepositoryId = Guid.NewGuid() };
        if (corruption == "range") result = result with { ReadLocation = new(support.Origin.RepositoryId, support.Origin.RelativePath, 1, support.ByteLength + 1) };
        string request = corruption == "request" ? "{\"relativePath\":\"other.cs\",\"byteOffset\":0,\"byteCount\":7}" : Request(support);
        var admitted = await ledger.AdmitToolResultAsync(SourceMetadataOperation.ReadTextRange, support.Origin.RelativePath, request, result);
        Assert.NotEqual(DisclosureStatus.Allowed, admitted.Status);
        Assert.Null(admitted.Result);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviouslyDisclosedSupport_LossBlocksFurtherDisclosureAndCompletionAsync(bool revoke)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Support.cs", SourceDigest = OctoberKnowledgePolicy.Hash("support") }, "support");
        bool allowed = true;
        var ledger = new OctoberWorkerInputLedger(host.Request, (_, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(support),
            (_, _) => Task.FromResult(allowed ? DisclosureStatus.Allowed : DisclosureStatus.Denied));
        Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, support)).Status);
        var before = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        if (revoke) allowed = false; else support.Current = false;
        Assert.False(await ledger.VerifyCurrentAsync());
        Assert.False(await ledger.VerifySnapshotAsync(before));
        Assert.Null(await ledger.FreezeForCompletionAsync());
        var after = await Read(ledger, support);
        Assert.NotEqual(DisclosureStatus.Allowed, after.Status);
        Assert.Null(after.Result);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Snapshot_FromAnotherLedger_IsNotACompletionCapabilityAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var first = Ledger(host);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await first.FreezeForCompletionAsync());
        Assert.True(await first.VerifySnapshotAsync(snapshot));
        Assert.False(await Ledger(host).VerifySnapshotAsync(snapshot));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FrozenLedger_WithholdsNewDisclosure_WithoutExpandingItsSnapshotAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Late.cs", SourceDigest = OctoberKnowledgePolicy.Hash("late") }, "late");
        var ledger = Ledger(host, support);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        var originalContributors = snapshot.Contributors;
        var late = await Read(ledger, support);
        Assert.NotEqual(DisclosureStatus.Allowed, late.Status);
        Assert.Null(late.Result);
        Assert.Equal(originalContributors, snapshot.Contributors);
        Assert.DoesNotContain(snapshot.Contributors, item => item.SourceOrigin == support.Origin);
        Assert.Single(snapshot.VerifiedSources);
        // Withheld input never crossed disclosure; the original immutable proof may remain valid.
        if (await ledger.FreezeForCompletionAsync() is { } repeated)
        {
            Assert.Equal(originalContributors, repeated.Contributors);
            Assert.Single(repeated.VerifiedSources);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolverForeignRootOrVersion_WithholdsReadAndCompletionAsync(bool foreignVersion)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var expected = new Frame(host.Source.Origin with { RelativePath = "src/Support.cs", SourceDigest = OctoberKnowledgePolicy.Hash("support") }, "support");
        var foreignOrigin = foreignVersion
            ? expected.Origin with { PreparedVersion = "another-prepared-session" }
            : expected.Origin with { RootBindingIdentity = "another-bound-root" };
        var resolverFrame = new Frame(foreignOrigin, expected.Text);
        var ledger = Ledger(host, resolverFrame);
        var admitted = await Read(ledger, expected);
        Assert.NotEqual(DisclosureStatus.Allowed, admitted.Status);
        Assert.Null(admitted.Result);
        Assert.Null(await ledger.FreezeForCompletionAsync());
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostSeal_AddsObservedUncitedInputOnlyAfterTerminalWorker_AndRejectsChangedSupportAsync(bool changeBeforeSeal)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Uncited.cs", SourceDigest = OctoberKnowledgePolicy.Hash("helper();") }, "helper();");
        var ledger = Ledger(host, support);
        int workerCalls = 0;
        var operation = new OperationKey(host.Request.Binding.BatchId, OperationKind.AcceptFile, Guid.NewGuid());
        await using var attempt = new OctoberAttemptState(operation, host.Request with { Source = ledger }, async _ =>
        {
            workerCalls++;
            Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, support)).Status);
            if (changeBeforeSeal) support.Current = false;
            return new OctoberFileWorkerResult(OctoberFileWorkerStatus.Completed, host.Proposal, (int)host.Source.ByteLength, 1, 0);
        }, _ => Task.FromResult(DisclosureStatus.Allowed), TimeSpan.FromMinutes(1), ledger);
        var submission = await attempt.ExecuteAndSealAsync();
        Assert.Equal(1, workerCalls);
        if (changeBeforeSeal)
        {
            Assert.Null(submission);
            Assert.False(attempt.HasSubmitted);
            await host.Fixture.AssertNoKnowledgeAsync();
            return;
        }
        Assert.NotNull(submission);
        Assert.Equal(host.Proposal.Units, submission.Proposal.Units);
        Assert.Equal(host.Proposal.Coverage, submission.Proposal.Coverage);
        Assert.Contains(submission.Proposal.Contributors, item => item.SourceOrigin == support.Origin);
        Assert.NotEqual(host.Proposal.CanonicalProposalDigest, submission.Proposal.CanonicalProposalDigest);
        Assert.Equal(OctoberKnowledgePolicy.ComputeProposalDigest(submission.Proposal), submission.Proposal.CanonicalProposalDigest);
        var writer = new OctoberKnowledgeWriter(host.Fixture.Factory, attempt, ledger, host.Fixture.Authorization,
            attempt, host.Fixture.Log, host.Fixture.FreezeAuthority);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await attempt.AcceptAsync(writer)).Status);
        Assert.Equal(1, workerCalls);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(SourceMetadataOperation.JavaDefinition)]
    [InlineData(SourceMetadataOperation.JavaReferences)]
    [InlineData(SourceMetadataOperation.CodeGraphExplore)]
    public async Task NavigationWithoutText_RecordsMetadataWithoutOpeningReferencedFilesAsync(SourceMetadataOperation operation)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        int acquisitions = 0;
        var ledger = new OctoberWorkerInputLedger(host.Request, (_, _) => { acquisitions++; return Task.FromResult<IOctoberVerifiedFileFrame?>(null); }, (_, _) => Task.FromResult(DisclosureStatus.Allowed));
        var location = new PreparedSourceLocation(host.Source.Origin.RepositoryId, "src/Referenced.java");
        object result = operation == SourceMetadataOperation.CodeGraphExplore
            ? new CodeGraphToolInvocationResult { Status = CodeGraphToolInvocationStatus.Complete, RepositoryId = location.RepositoryId, Locations = [location] }
            : new PreparedJavaNavigationResult(location.RepositoryId, operation == SourceMetadataOperation.JavaDefinition ? PreparedJavaNavigationOperation.Definition : PreparedJavaNavigationOperation.References,
                PreparedJavaNavigationOutcome.Observed, [new(location, 1, 0, 1, 3)], [], null);
        string scope = operation == SourceMetadataOperation.CodeGraphExplore ? "." : host.Source.Origin.RelativePath;
        string request = operation == SourceMetadataOperation.CodeGraphExplore ? "{\"query\":\"referenced\",\"maxFiles\":4}" : JsonSerializer.Serialize(new { relativePath = scope, line = 1, character = 1, maximumLocations = 10 });
        var admitted = await ledger.AdmitToolResultAsync(operation, scope, request, result);
        Assert.Equal(DisclosureStatus.Allowed, admitted.Status);
        Assert.NotNull(admitted.Result);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Equal(0, acquisitions);
        Assert.Single(snapshot.VerifiedSources);
        Assert.Equal(operation, Assert.Single(snapshot.Contributors.Where(item => item.Kind == ContributorKind.SourceMetadata)).MetadataOrigin!.Operation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SearchOrCodeGraphExcerpt_RecordsVerifiedUncitedContentAsync(bool codeGraph)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Support.cs", SourceDigest = OctoberKnowledgePolicy.Hash("helper();") }, "helper();");
        var ledger = Ledger(host, support);
        object result = codeGraph
            ? new CodeGraphToolInvocationResult { Status = CodeGraphToolInvocationStatus.Complete, RepositoryId = support.Origin.RepositoryId,
                Excerpts = [new(new(support.Origin.RepositoryId, support.Origin.RelativePath), support.Text, 1, 1)] }
            : new PreparedSourceResult { RepositoryId = support.Origin.RepositoryId, Operation = PreparedSourceOperation.SearchText, Outcome = PreparedSourceOutcome.Complete,
                Matches = [new(new(support.Origin.RepositoryId, support.Origin.RelativePath, 0, 6, 1, 0), support.Text)] };
        var admitted = await ledger.AdmitToolResultAsync(codeGraph ? SourceMetadataOperation.CodeGraphExplore : SourceMetadataOperation.SearchText,
            codeGraph ? "." : "src", codeGraph ? "{\"query\":\"helper\",\"maxFiles\":4}" : "{\"relativeDirectory\":\"src\",\"literal\":\"helper\",\"pageSize\":20}", result);
        Assert.Equal(DisclosureStatus.Allowed, admitted.Status);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Contains(snapshot.Contributors, item => item.SourceOrigin == support.Origin);
        Assert.Equal(2, snapshot.VerifiedSources.Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CodeGraphFreeText_IsNeverDisclosedAsMetadataAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var ledger = Ledger(host);
        var admitted = await ledger.AdmitToolResultAsync(SourceMetadataOperation.CodeGraphExplore, ".", "{\"query\":\"helper\",\"maxFiles\":4}",
            new CodeGraphToolInvocationResult { Status = CodeGraphToolInvocationStatus.Complete, RepositoryId = host.Source.Origin.RepositoryId, Text = "unverified source or instructions" });
        if (admitted.Status == DisclosureStatus.Allowed)
        {
            var safe = Assert.IsType<CodeGraphToolInvocationResult>(admitted.Result);
            Assert.Null(safe.Text);
            Assert.Contains(CodeGraphToolIssueCode.UncontainedSource, safe.Limitations);
        }
        else Assert.Null(admitted.Result);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ContentOriginLimit_IncludesPrimary_AndNeverTruncatesNewDisclosureAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var frames = Enumerable.Range(0, 128).Select(index => new Frame(host.Source.Origin with { RelativePath = $"src/support-{index}.cs", SourceDigest = OctoberKnowledgePolicy.Hash("x") }, "x")).ToArray();
        var ledger = Ledger(host, frames);
        foreach (var frame in frames.Take(127)) Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, frame)).Status);
        var over = await Read(ledger, frames[127]);
        Assert.NotEqual(DisclosureStatus.Allowed, over.Status);
        Assert.Null(over.Result);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MetadataObservationLimit_WithholdsTheEntireExcessObservationAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var ledger = Ledger(host);
        for (int index = 0; index < 129; index++)
        {
            string scope = $"scope-{index}";
            var result = new PreparedSourceResult { RepositoryId = host.Source.Origin.RepositoryId, Operation = PreparedSourceOperation.List, Outcome = PreparedSourceOutcome.CompleteEmpty };
            var admitted = await ledger.AdmitToolResultAsync(SourceMetadataOperation.List, scope, JsonSerializer.Serialize(new { relativeDirectory = scope, pageSize = 1 }), result);
            if (index < 128) Assert.Equal(DisclosureStatus.Allowed, admitted.Status);
            else { Assert.NotEqual(DisclosureStatus.Allowed, admitted.Status); Assert.Null(admitted.Result); }
        }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SourceChangesDuringAllowedAuthorityCallback_WithholdsDisclosureAndSealAsync()
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Support.cs", SourceDigest = OctoberKnowledgePolicy.Hash("support") }, "support");
        bool mutate = false;
        var ledger = new OctoberWorkerInputLedger(host.Request, (_, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(support), async (_, _) =>
        {
            await Task.Yield();
            if (mutate) support.Current = false;
            return DisclosureStatus.Allowed;
        });
        Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, support)).Status);
        mutate = true;
        var result = await Read(ledger, support);
        Assert.NotEqual(DisclosureStatus.Allowed, result.Status);
        Assert.Null(result.Result);
        Assert.Null(await ledger.FreezeForCompletionAsync());
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupportChangesDuringFinalAuthorization_WithholdsNewMetadataOrSnapshotAsync(bool freeze)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Support.cs", SourceDigest = OctoberKnowledgePolicy.Hash("support") }, "support");
        bool armed = false;
        var ledger = new OctoberWorkerInputLedger(host.Request, (_, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(support), async (contributors, _) =>
        {
            await Task.Yield();
            bool finalListAdmission = contributors.Any(item => item.MetadataOrigin?.Operation == SourceMetadataOperation.List);
            if (armed && (freeze || finalListAdmission)) support.Current = false;
            return DisclosureStatus.Allowed;
        });
        Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, support)).Status);
        armed = true;
        if (freeze) Assert.Null(await ledger.FreezeForCompletionAsync());
        else
        {
            var listed = new PreparedSourceResult { RepositoryId = host.Source.Origin.RepositoryId, Operation = PreparedSourceOperation.List, Outcome = PreparedSourceOutcome.CompleteEmpty };
            var admission = await ledger.AdmitToolResultAsync(SourceMetadataOperation.List, ".", "{\"relativeDirectory\":\"\",\"pageSize\":20}", listed);
            Assert.NotEqual(DisclosureStatus.Allowed, admission.Status);
            Assert.Null(admission.Result);
        }
        Assert.False(support.Current);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FinalSourceVerifier_CancelsOrChangesPinnedOrigin_WithoutReturningFalse_WithholdsAsync(bool freeze, bool mutateOrigin)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var support = new Frame(host.Source.Origin with { RelativePath = "src/Support.cs", SourceDigest = OctoberKnowledgePolicy.Hash("support") }, "support");
        bool armed = false, finalVerification = false;
        var ledger = new OctoberWorkerInputLedger(host.Request, (_, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(support), (contributors, _) =>
        {
            if (armed && (freeze || contributors.Any(item => item.MetadataOrigin?.Operation == SourceMetadataOperation.List))) finalVerification = true;
            return Task.FromResult(DisclosureStatus.Allowed);
        });
        support.Verify = async _ =>
        {
            await Task.Yield();
            if (finalVerification)
            {
                if (mutateOrigin) support.Origin = support.Origin with { PreparedVersion = "changed-during-verification" };
                else cancellation.Cancel();
            }
            return true;
        };
        Assert.Equal(DisclosureStatus.Allowed, (await Read(ledger, support)).Status);
        armed = true;
        if (freeze) Assert.Null(await ledger.FreezeForCompletionAsync(cancellation.Token));
        else
        {
            var listed = new PreparedSourceResult { RepositoryId = host.Source.Origin.RepositoryId, Operation = PreparedSourceOperation.List, Outcome = PreparedSourceOutcome.CompleteEmpty };
            var admitted = await ledger.AdmitToolResultAsync(SourceMetadataOperation.List, ".", "{\"relativeDirectory\":\"\",\"pageSize\":20}", listed, cancellation.Token);
            Assert.NotEqual(DisclosureStatus.Allowed, admitted.Status);
            Assert.Null(admitted.Result);
        }
        Assert.True(finalVerification);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("java-empty", true)]
    [InlineData("java-limited", true)]
    [InlineData("java-readiness", true)]
    [InlineData("java-configuration", true)]
    [InlineData("java-protocol", true)]
    [InlineData("java-authority", false)]
    [InlineData("java-missing-repository", false)]
    [InlineData("java-foreign-repository", false)]
    [InlineData("graph-no-match", true)]
    [InlineData("graph-unavailable", true)]
    [InlineData("graph-failed", true)]
    [InlineData("graph-timeout", true)]
    [InlineData("graph-limited", true)]
    [InlineData("graph-undefined", false)]
    [InlineData("graph-authority", false)]
    [InlineData("graph-missing-repository", false)]
    [InlineData("graph-foreign-repository", false)]
    public async Task CurrentContract_NonSuccessNavigationEnvelope_RequiresExactRepositoryAsync(string scenario, bool allowed)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        int acquisitions = 0;
        var ledger = new OctoberWorkerInputLedger(host.Request, (_, _) => { acquisitions++; return Task.FromResult<IOctoberVerifiedFileFrame?>(null); }, (_, _) => Task.FromResult(DisclosureStatus.Allowed));
        bool graph = scenario.StartsWith("graph-", StringComparison.Ordinal);
        Guid repository = scenario.EndsWith("missing-repository", StringComparison.Ordinal) ? Guid.Empty : scenario.EndsWith("foreign-repository", StringComparison.Ordinal) ? Guid.NewGuid() : host.Source.Origin.RepositoryId;
        SourceMetadataOperation operation = graph ? SourceMetadataOperation.CodeGraphExplore : SourceMetadataOperation.JavaDefinition;
        string path = graph ? "." : host.Source.Origin.RelativePath;
        string request = graph ? "{\"query\":\"controlled\",\"maxFiles\":4}" : JsonSerializer.Serialize(new { relativePath = path, line = 1, character = 1, maximumLocations = 10 });
        object result;
        if (graph)
        {
            var status = scenario switch
            {
                "graph-no-match" => CodeGraphToolInvocationStatus.NoMatch,
                "graph-failed" => CodeGraphToolInvocationStatus.Failed,
                "graph-timeout" => CodeGraphToolInvocationStatus.TimedOut,
                "graph-limited" => CodeGraphToolInvocationStatus.Limited,
                "graph-undefined" => (CodeGraphToolInvocationStatus)99999,
                "graph-authority" => CodeGraphToolInvocationStatus.AuthorityUnavailable,
                _ => CodeGraphToolInvocationStatus.Unavailable,
            };
            result = new CodeGraphToolInvocationResult { RepositoryId = scenario == "graph-missing-repository" ? null : repository, Status = status,
                IssueCode = status == CodeGraphToolInvocationStatus.NoMatch ? CodeGraphToolIssueCode.None : CodeGraphToolIssueCode.InvocationFailed };
        }
        else
        {
            var outcome = scenario switch
            {
                "java-empty" => PreparedJavaNavigationOutcome.NoResultObserved,
                "java-limited" => PreparedJavaNavigationOutcome.Limited,
                "java-configuration" => PreparedJavaNavigationOutcome.ConfigurationUnavailable,
                "java-protocol" => PreparedJavaNavigationOutcome.ProtocolFailed,
                "java-authority" => PreparedJavaNavigationOutcome.AuthorityUnavailable,
                _ => PreparedJavaNavigationOutcome.ReadinessFailed,
            };
            result = new PreparedJavaNavigationResult(repository, PreparedJavaNavigationOperation.Definition, outcome, [], [], null);
        }
        // Envelope-only characterization: a real runtime may separately invalidate primary currentness.
        var admitted = await ledger.AdmitToolResultAsync(operation, path, request, result);
        Assert.Equal(allowed ? DisclosureStatus.Allowed : DisclosureStatus.Denied, admitted.Status); Assert.Equal(0, acquisitions);
        if (!allowed) { Assert.Null(admitted.Result); Assert.Null(await ledger.FreezeForCompletionAsync()); return; }
        if (graph) Assert.Equal(((CodeGraphToolInvocationResult)result).Status, Assert.IsType<CodeGraphToolInvocationResult>(admitted.Result).Status);
        else Assert.Equal(((PreparedJavaNavigationResult)result).Outcome, Assert.IsType<PreparedJavaNavigationResult>(admitted.Result).Outcome);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Single(snapshot.VerifiedSources);
        var metadata = Assert.Single(snapshot.Contributors.Where(contributor => contributor.Kind == ContributorKind.SourceMetadata));
        Assert.Equal(operation, metadata.MetadataOrigin!.Operation); Assert.Equal(host.Source.Origin.RepositoryId, metadata.MetadataOrigin.RepositoryId);
        Assert.Equal(path, metadata.MetadataOrigin.NormalizedScopePath);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("incomplete", true)]
    [InlineData("missing", true)]
    [InlineData("invalid-range", true)]
    [InlineData("unsupported-text", true)]
    [InlineData("empty-without-envelope", false)]
    [InlineData("empty-at-eof", true)]
    [InlineData("missing-repository", false)]
    [InlineData("foreign-repository", false)]
    public async Task CurrentContract_NonSuccessReadEnvelope_DistinguishesAbsentPayloadFromVerifiedEofAsync(string scenario, bool allowed)
    {
        await using var host = await OctoberScanAcceptanceHarness.CreateAsync();
        int acquisitions = 0;
        var ledger = new OctoberWorkerInputLedger(host.Request, (_, _) => { acquisitions++; return Task.FromResult<IOctoberVerifiedFileFrame?>(null); }, (_, _) => Task.FromResult(DisclosureStatus.Allowed));
        var primary = host.Source;
        bool eof = scenario == "empty-at-eof";
        long offset = eof ? primary.ByteLength : 0;
        Guid repository = scenario == "missing-repository" ? Guid.Empty : scenario == "foreign-repository" ? Guid.NewGuid() : primary.Origin.RepositoryId;
        var outcome = scenario switch
        {
            "missing" => PreparedSourceOutcome.Missing,
            "invalid-range" => PreparedSourceOutcome.InvalidRange,
            "unsupported-text" => PreparedSourceOutcome.UnsupportedText,
            "empty-without-envelope" or "empty-at-eof" => PreparedSourceOutcome.CompleteEmpty,
            _ => PreparedSourceOutcome.Incomplete,
        };
        var result = new PreparedSourceResult { RepositoryId = repository, Operation = PreparedSourceOperation.ReadTextRange, Outcome = outcome,
            Text = eof ? string.Empty : null, ReadLocation = eof ? new(repository, primary.Origin.RelativePath, offset, offset) : null,
            ObservedFileByteLength = eof ? primary.ByteLength : null };
        var admitted = await ledger.AdmitToolResultAsync(SourceMetadataOperation.ReadTextRange, primary.Origin.RelativePath,
            JsonSerializer.Serialize(new { relativePath = primary.Origin.RelativePath, byteOffset = offset, byteCount = 1 }), result);
        Assert.Equal(allowed ? DisclosureStatus.Allowed : DisclosureStatus.Denied, admitted.Status); Assert.Equal(0, acquisitions);
        if (!allowed) { Assert.Null(admitted.Result); Assert.Null(await ledger.FreezeForCompletionAsync()); return; }
        var disclosed = Assert.IsType<PreparedSourceResult>(admitted.Result); Assert.Equal(outcome, disclosed.Outcome);
        Assert.Equal(result.Text, disclosed.Text); Assert.Equal(result.ReadLocation, disclosed.ReadLocation);
        var snapshot = Assert.IsType<OctoberWorkerInputSnapshot>(await ledger.FreezeForCompletionAsync());
        Assert.Single(snapshot.VerifiedSources); Assert.Equal(primary.Origin, snapshot.VerifiedSources[0].Origin);
        Assert.Single(snapshot.Contributors.Where(contributor => contributor.Kind == ContributorKind.SourceMetadata));
    }
    private static OctoberWorkerInputLedger Ledger(OctoberScanAcceptanceHarness host, params Frame[] frames) => new(host.Request,
        (path, _) => Task.FromResult<IOctoberVerifiedFileFrame?>(frames.SingleOrDefault(frame => frame.Origin.RelativePath == path)),
        (_, _) => Task.FromResult(DisclosureStatus.Allowed));
    private static string Request(Frame frame) => JsonSerializer.Serialize(new { relativePath = frame.Origin.RelativePath, byteOffset = 0, byteCount = frame.ByteLength });
    private static Task<OctoberWorkerInputAdmission> Read(OctoberWorkerInputLedger ledger, Frame frame) => ledger.AdmitToolResultAsync(SourceMetadataOperation.ReadTextRange, frame.Origin.RelativePath, Request(frame), Result(frame));
    private static PreparedSourceResult Result(Frame frame) => new() { RepositoryId = frame.Origin.RepositoryId, Operation = PreparedSourceOperation.ReadTextRange, Outcome = PreparedSourceOutcome.Complete,
        ReadLocation = new(frame.Origin.RepositoryId, frame.Origin.RelativePath, 0, frame.ByteLength), ObservedFileByteLength = frame.ByteLength, Text = frame.Text };
    private sealed class Frame(PreparedOrigin origin, string text) : IOctoberVerifiedFileFrame
    {
        public PreparedOrigin Origin { get; set; } = origin;
        public string Text => text;
        public long ByteLength => Encoding.UTF8.GetByteCount(text);
        public int LineCount => 1;
        public string FileIdentity => origin.RelativePath + ":controlled";
        public bool Current { get; set; } = true;
        internal Func<CancellationToken, Task<bool>>? Verify { get; set; }
        public Task<bool> VerifyCurrentAsync(CancellationToken cancellationToken = default) => Verify?.Invoke(cancellationToken) ?? Task.FromResult(Current);
    }
}
