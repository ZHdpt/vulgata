using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Internal natural-completion composition only: real owner stores/reader and real SDK factory, controlled HTTP, no CLI or live provider.</summary>
public sealed class OctoberScanBusinessFinalizationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContextOverride_ExistingClRoute_CapturesEffectivePacketAndRetainsCanonicalSelectionAsync(bool useOverride)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var cl = await host.Runner.ExecuteAsync(host.Execution);
        var original = Assert.IsType<PublicationBinding>(cl.Report!.Publication);
        var execution = ExistingClExecution(host.Execution, original);
        using var transport = new NaturalTransport();
        var dependencies = ExistingClDependencies(host, execution, transport);
        const string text = "Run-specific vocabulary supplied by the authorized host.";
        BusinessAdvisoryContextOverride? supplied = useOverride
            ? new([new(host.Database.Origin.RepositoryId, OctoberContextScope.Repository, text)]) : null;
        dependencies = dependencies with { Business = dependencies.Business! with { ContextOverride = supplied } };
        await using var runner = new OctoberGenerateRunner(dependencies);
        var result = await runner.ExecuteAsync(execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(0, transport.WorkerRequests);
        Assert.Equal(1, transport.BusinessRequests);
        using var packet = JsonDocument.Parse(Assert.Single(transport.SynthesisPackets));
        var advisory = Assert.Single(packet.RootElement.GetProperty("AdvisoryContext").EnumerateArray());
        Assert.Equal(useOverride ? text : OctoberKnowledgeAcceptanceFixture.AdvisorySnapshot, advisory.GetProperty("Text").GetString());
        await using var context = host.Database.Factory.CreateDbContext();
        var retained = await context.BusinessFinalizations.AsNoTracking().SingleAsync();
        var request = JsonSerializer.Deserialize<FlowFinalizationPublicationRequest>(retained.RequestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(supplied), OctoberKnowledgePolicy.SerializeCanonical(request.Selection.ContextOverride));
        if (!useOverride) Assert.DoesNotContain("ContextOverride", retained.RequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(await context.KnowledgePublications.AnyAsync(value => value.PublicationId == original.PublicationId));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ContextOverride_FreshScanRoute_RejectsBeforeAdmissionOrClientCreationAsync()
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        using var transport = new NaturalTransport();
        var execution = host.Execution with { Policy = host.Execution.Policy with { OutputStage = "cl-and-bl" } };
        var dependencies = ExistingClDependencies(host, execution, transport);
        dependencies = dependencies with { Business = dependencies.Business! with
        { ContextOverride = new([new(host.Database.Origin.RepositoryId, OctoberContextScope.Repository, "Existing CL route only")]) } };
        await using var runner = new OctoberGenerateRunner(dependencies);
        var result = await runner.ExecuteAsync(execution);
        Assert.NotEqual(0, Assert.IsType<OctoberScanReport>(result.Report).ExitCode);
        Assert.Equal(0, host.BeginCalls);
        Assert.Empty(transport.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ContextOverride_GeneratorSettings_RejectMalformedOverride()
    {
        var settings = new OctoberBusinessSettings([], new(), 40);
        Assert.True(settings.IsValid());
        Assert.False((settings with { ContextOverride = new([new(Guid.NewGuid(), OctoberContextScope.Global, "Forbidden global scope")]) }).IsValid());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("initial-overflow")]
    [InlineData("later-overflow")]
    [InlineData("minimum-overflow")]
    [InlineData("denied")]
    [InlineData("aggregate-limit")]
    public async Task ExistingClContinuation_PublicationCatalogBudget_RestartsOnlyBoundedReadsAsync(string scenario)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var cl = await host.Runner.ExecuteAsync(host.Execution);
        var original = Assert.IsType<PublicationBinding>(cl.Report!.Publication);
        var execution = ExistingClExecution(host.Execution, original);
        using var transport = new NaturalTransport();
        var dependencies = ExistingClDependencies(host, execution, transport);
        CatalogBudgetReader? observed = null;
        dependencies = dependencies with { Business = dependencies.Business! with
        {
            BindReader = (session, publication, _) =>
            {
                var bound = ControlledBinding(host, publication, session, null);
                observed = new CatalogBudgetReader(bound.Reader, scenario);
                return Task.FromResult<OctoberVerifiedReaderBinding?>(bound with { Reader = observed });
            }
        } };
        await using var runner = new OctoberGenerateRunner(dependencies);
        var result = await runner.ExecuteAsync(execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        Assert.NotNull(observed);
        Assert.Equal(1, transport.BusinessRequests);
        Assert.Equal(0, transport.WorkerRequests);
        Assert.Equal(100, observed.Requests[0].PageSize);
        if (scenario is "initial-overflow" or "later-overflow")
        {
            Assert.Equal(0, report.ExitCode);
            Assert.NotEmpty(report.Documents);
            Assert.Equal(report.Documents.Length, report.Documents.Distinct().Count());
            Assert.True(observed.Documents.Select(document => document.Handle).ToHashSet().SetEquals(report.Documents));
            int restart = scenario == "initial-overflow" ? 1 : 2;
            Assert.Equal(50, observed.Requests[restart].PageSize);
            Assert.Null(observed.Requests[restart].Continuation);
            Assert.Equal(scenario == "initial-overflow" ? 2 : 4, observed.Requests.Count);
            if (scenario == "later-overflow")
            {
                Assert.Equal(100, observed.Requests[1].PageSize);
                Assert.NotNull(observed.Requests[1].Continuation);
                Assert.Equal(50, observed.Requests[3].PageSize);
                Assert.NotNull(observed.Requests[3].Continuation);
            }
        }
        else
        {
            Assert.NotEqual(0, report.ExitCode);
            Assert.Empty(report.Documents);
            if (scenario == "aggregate-limit")
            {
                Assert.Equal(256, observed.Requests.Count);
                Assert.Equal(131, observed.Requests.Count(request => request.PageSize == 100));
                Assert.Equal(125, observed.Requests.Count(request => request.PageSize == 50));
                Assert.Null(observed.Requests[131].Continuation);
            }
            else
            {
                Assert.Equal(scenario == "denied" ? new[] { 100 } : new[] { 100, 50, 25, 12, 6, 3, 1 },
                    observed.Requests.Select(request => request.PageSize).ToArray());
                Assert.All(observed.Requests, request => Assert.Null(request.Continuation));
            }
        }
        await using var context = host.Database.Factory.CreateDbContext();
        Assert.Equal(2, await context.KnowledgePublications.CountAsync());
        Assert.True(await context.KnowledgePublications.AnyAsync(publication => publication.PublicationId == original.PublicationId));
    }

    private sealed class CatalogBudgetReader(IOctoberKnowledgeReader inner, string scenario) : IOctoberKnowledgeReader
    {
        public List<KnowledgeSearchRequest> Requests { get; } = [];
        public ImmutableArray<DocumentSummary> Documents { get; private set; } = [];
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken token = default) => inner.OpenReadSelectionAsync(request, token);
        public async Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken token = default)
        {
            Requests.Add(request);
            if (scenario == "denied") return new(KnowledgeReadStatus.Unavailable, [], null, null);
            if (scenario == "aggregate-limit" && Requests.Count == 131)
                return new(KnowledgeReadStatus.ReadBudgetExceeded, [], null, null);
            if (scenario == "minimum-overflow" || scenario == "initial-overflow" && request.PageSize == 100 ||
                scenario == "later-overflow" && request.PageSize == 100 && request.Continuation is not null)
                return new(KnowledgeReadStatus.ReadBudgetExceeded, [], null, null);
            if (Documents.IsEmpty)
            {
                var actual = await inner.SearchAsync(request with { Continuation = null }, token);
                Assert.Equal(KnowledgeReadStatus.Success, actual.Status);
                Assert.Null(actual.Next);
                Documents = actual.Documents;
                Assert.True(Documents.Length >= 2);
            }
            if (scenario == "aggregate-limit")
            {
                // Controlled endless catalog envelopes use distinct validly bound identities.
                // They never claim these synthetic handles were persisted or readable.
                var document = Documents[0] with { Handle = Documents[0].Handle with { DocumentVersionId = Guid.NewGuid() } };
                return new(KnowledgeReadStatus.Success, [document], new("controlled-next-" + Requests.Count), null);
            }
            if (scenario == "later-overflow")
            {
                if (request.Continuation is null)
                    return new(KnowledgeReadStatus.Success, [Documents[0]], new("controlled-page-size-" + request.PageSize), null);
                Assert.Equal("controlled-page-size-" + request.PageSize, request.Continuation.Value);
                return new(KnowledgeReadStatus.Success, Documents.RemoveAt(0), null, null);
            }
            return new(KnowledgeReadStatus.Success, Documents, null, null);
        }
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken token = default) => inner.ReadExactAsync(request, token);
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken token = default) => inner.GetRelatedAsync(request, token);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BusinessFromClStore_BeginsOnlyAgainstAnExactPublishedClPublicationAsync()
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var clResult = await host.Runner.ExecuteAsync(host.Execution);
        Assert.Equal(0, clResult.Report!.ExitCode);
        var publication = Assert.IsType<PublicationBinding>(clResult.Report.Publication);
        Assert.NotEmpty(clResult.Report.Documents);
        ExistingClCounts before = await CaptureExistingClCountsAsync(host);

        var unsigned = new BusinessFromClRequest(publication, host.Execution.Session.SubjectId, Guid.NewGuid(), string.Empty);
        var request = unsigned with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(unsigned) };
        var authority = new ControlledBusinessFromClAuthority();
        var settlement = new ControlledBusinessFromClSettlementAuthority();
        var store = new OctoberBusinessFromClStore(host.Database.Factory, authority, host.Database.Guard,
            host.Database.Guard, settlement, host.Database.Log);

        var result = await store.BeginAsync(request);

        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(OctoberBusinessFromClPolicy.BeginOperation(request), result.Operation);
        Assert.NotNull(result.Admission);
        var snapshot = Assert.IsType<BusinessFromClSnapshot>(result.Snapshot);
        Assert.Equal(publication, snapshot.Publication);
        Assert.Single(snapshot.AcceptedFiles);
        Assert.NotEmpty(snapshot.AcceptedFiles[0].Documents);
        Assert.NotNull(snapshot.Corpus);
        Assert.True(authority.Calls >= 1);
        Assert.All(authority.ContributorChecks, checkedContributors => Assert.False(checkedContributors.IsDefault));
        Assert.Equal(before, await CaptureExistingClCountsAsync(host));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BusinessFromClStore_UnacknowledgedBeginBlocksConcurrentFreshRunUntilSettledAsync()
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var cl = await host.Runner.ExecuteAsync(host.Execution);
        var publication = Assert.IsType<PublicationBinding>(cl.Report!.Publication);
        var beginGuard = new HeldCommitAcknowledgementGuard(host.Database.Guard);
        var store = CreateBusinessFromClStore(host, new ControlledBusinessFromClAuthority(),
            new ControlledBusinessFromClSettlementAuthority(), beginGuard);
        var firstRequest = BusinessFromClRequestFor(publication, host.Execution.Session.SubjectId);

        Task<BusinessFromClResult> firstPending = store.BeginAsync(firstRequest);
        await beginGuard.CommitSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var competing = await store.BeginAsync(BusinessFromClRequestFor(publication, host.Execution.Session.SubjectId));

        Assert.NotEqual(KnowledgeWriteStatus.Committed, competing.Status);
        Assert.Null(competing.Admission);
        beginGuard.ReleaseAcknowledgement.TrySetResult();
        var first = await firstPending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(KnowledgeWriteStatus.Committed, first.Status);
        Assert.NotNull(first.Admission);
        Assert.Equal(1, beginGuard.Submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BusinessFromClStore_OnlyOriginalCapabilityAndSettledCompletionReleaseNextAttemptAsync()
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var cl = await host.Runner.ExecuteAsync(host.Execution);
        var publication = Assert.IsType<PublicationBinding>(cl.Report!.Publication);
        ExistingClCounts before = await CaptureExistingClCountsAsync(host);
        var authority = new ControlledBusinessFromClAuthority();
        var settlement = new ControlledBusinessFromClSettlementAuthority();
        var store = CreateBusinessFromClStore(host, authority, settlement);
        var request = BusinessFromClRequestFor(publication, host.Execution.Session.SubjectId);
        var begin = await store.BeginAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, begin.Status);
        var admission = Assert.IsAssignableFrom<BusinessFromClAdmission>(begin.Admission);
        var completionBase = new BusinessFromClCompletionRequest(request, string.Empty);
        var completion = completionBase with
        { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeCompletionDigest(completionBase) };

        var freshStore = CreateBusinessFromClStore(host, authority, settlement);
        var freshIssuerAttempt = await freshStore.CompleteAsync(completion, admission);
        Assert.Equal(KnowledgeWriteStatus.Denied, freshIssuerAttempt.Status);
        var forgedAttempt = await store.CompleteAsync(completion, new ForgedBusinessFromClAdmission());
        Assert.Equal(KnowledgeWriteStatus.Denied, forgedAttempt.Status);
        Assert.Equal(0, await CountBusinessFromClCompletionsAsync(host));

        settlement.Status = BusinessFromClSettlement.Unsettled;
        var unsettled = await store.CompleteAsync(completion, admission);
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, unsettled.Status);
        Assert.Equal(0, await CountBusinessFromClCompletionsAsync(host));
        var blocked = await store.BeginAsync(BusinessFromClRequestFor(publication, host.Execution.Session.SubjectId));
        Assert.NotEqual(KnowledgeWriteStatus.Committed, blocked.Status);
        Assert.Null(blocked.Admission);

        // Cleanup remains possible after the user loses disclosure authority, but returns no corpus metadata.
        authority.Status = DisclosureStatus.Denied;
        settlement.Status = BusinessFromClSettlement.Settled;
        var closed = await store.CompleteAsync(completion, admission);
        Assert.Equal(KnowledgeWriteStatus.Committed, closed.Status);
        Assert.Null(closed.AcceptedFile);
        Assert.Null(closed.Corpus);
        Assert.Null(closed.Publication);
        var undisclosedReplay = await store.LookupAsync(request);
        Assert.Null(undisclosedReplay.Snapshot);

        authority.Status = DisclosureStatus.Allowed;
        var next = await store.BeginAsync(BusinessFromClRequestFor(publication, host.Execution.Session.SubjectId));
        Assert.Equal(KnowledgeWriteStatus.Committed, next.Status);
        Assert.NotNull(next.Admission);
        Assert.Equal(1, await CountBusinessFromClCompletionsAsync(host));
        Assert.Equal(before, await CaptureExistingClCountsAsync(host));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BusinessFromClStore_NonExactPublicationCannotIssueAdmissionAsync()
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var cl = await host.Runner.ExecuteAsync(host.Execution);
        var publication = Assert.IsType<PublicationBinding>(cl.Report!.Publication);
        var wrong = publication with { PublicationId = Guid.NewGuid() };
        var store = CreateBusinessFromClStore(host, new ControlledBusinessFromClAuthority(),
            new ControlledBusinessFromClSettlementAuthority());

        var result = await store.BeginAsync(BusinessFromClRequestFor(wrong, host.Execution.Session.SubjectId));

        Assert.NotEqual(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Null(result.Snapshot);
        Assert.Null(result.Admission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExistingClBusiness_FailedAttemptPreservesOriginalClAndFreshSettledAttemptCanSucceedAsync()
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        var original = await host.Runner.ExecuteAsync(host.Execution);
        Assert.Equal(0, original.Report!.ExitCode);
        var originalPublication = Assert.IsType<PublicationBinding>(original.Report.Publication);
        DocumentHandle[] originalHandles = original.Report.Documents.OrderBy(handle => handle.LogicalDocumentId).ToArray();
        ExistingClCounts before = await CaptureExistingClCountsAsync(host);
        var originalMembers = await CapturePublicationMembersAsync(host, originalPublication.PublicationId);
        var originalContent = await CaptureOriginalClContentAsync(host, originalPublication, originalHandles);
        int originalSourceOpens = host.SourceOpens;
        int originalAdmissions = host.BeginCalls;
        int originalFreezes = host.FreezeCalls;

        using (var failedTransport = new NaturalTransport { InvalidSynthesisProposal = true })
        {
            var failedExecution = ExistingClExecution(host.Execution, originalPublication);
            await using var failedRunner = new OctoberGenerateRunner(ExistingClDependencies(host, failedExecution, failedTransport));
            var failed = await failedRunner.ExecuteAsync(failedExecution);

            Assert.NotEqual(0, failed.Report!.ExitCode);
            Assert.Equal(0, failedTransport.WorkerRequests);
            Assert.Equal(1, failedTransport.BusinessRequests);
        }

        Assert.Equal(originalSourceOpens, host.SourceOpens);
        Assert.Equal(originalAdmissions, host.BeginCalls);
        Assert.Equal(originalFreezes, host.FreezeCalls);
        Assert.Equal(before, await CaptureExistingClCountsAsync(host));
        Assert.True(originalMembers.SequenceEqual(await CapturePublicationMembersAsync(host, originalPublication.PublicationId)));
        Assert.True(originalContent.SequenceEqual(await CaptureOriginalClContentAsync(host, originalPublication, originalHandles)));

        using (var successfulTransport = new NaturalTransport())
        {
            var successfulExecution = ExistingClExecution(host.Execution, originalPublication);
            await using var successfulRunner = new OctoberGenerateRunner(ExistingClDependencies(host, successfulExecution, successfulTransport));
            var successful = await successfulRunner.ExecuteAsync(successfulExecution);

            Assert.Equal(0, successful.Report!.ExitCode);
            var successfulPublication = Assert.IsType<PublicationBinding>(successful.Report.Publication);
            Assert.All(successful.Report.Documents, handle => Assert.Equal(successfulPublication.PublicationId, handle.PublicationId));
            Assert.All(originalHandles, originalHandle => Assert.Contains(successful.Report.Documents, handle =>
                handle.BatchId == originalHandle.BatchId &&
                handle.LogicalDocumentId == originalHandle.LogicalDocumentId &&
                handle.DocumentVersionId == originalHandle.DocumentVersionId));
            Assert.Equal(0, successfulTransport.WorkerRequests);
            Assert.Equal(1, successfulTransport.BusinessRequests);
        }

        Assert.Equal(originalSourceOpens, host.SourceOpens);
        Assert.Equal(originalAdmissions, host.BeginCalls);
        Assert.Equal(originalFreezes, host.FreezeCalls);
        Assert.True(originalMembers.SequenceEqual(await CapturePublicationMembersAsync(host, originalPublication.PublicationId)));
        Assert.True(originalContent.SequenceEqual(await CaptureOriginalClContentAsync(host, originalPublication, originalHandles)));

        using (var repeatedTransport = new NaturalTransport())
        {
            var repeatedExecution = ExistingClExecution(host.Execution, originalPublication);
            await using var repeatedRunner = new OctoberGenerateRunner(ExistingClDependencies(host, repeatedExecution, repeatedTransport));
            var repeated = await repeatedRunner.ExecuteAsync(repeatedExecution);

            Assert.Equal(0, repeated.Report!.ExitCode);
            var repeatedPublication = Assert.IsType<PublicationBinding>(repeated.Report.Publication);
            Assert.NotEqual(originalPublication.PublicationId, repeatedPublication.PublicationId);
            Assert.All(originalHandles, originalHandle => Assert.Contains(repeated.Report.Documents, handle =>
                handle.BatchId == originalHandle.BatchId &&
                handle.LogicalDocumentId == originalHandle.LogicalDocumentId &&
                handle.DocumentVersionId == originalHandle.DocumentVersionId));
            Assert.Equal(0, repeatedTransport.WorkerRequests);
            Assert.Equal(1, repeatedTransport.BusinessRequests);
        }

        Assert.Equal(originalSourceOpens, host.SourceOpens);
        Assert.Equal(originalAdmissions, host.BeginCalls);
        Assert.Equal(originalFreezes, host.FreezeCalls);
        Assert.True(originalMembers.SequenceEqual(await CapturePublicationMembersAsync(host, originalPublication.PublicationId)));
        Assert.True(originalContent.SequenceEqual(await CaptureOriginalClContentAsync(host, originalPublication, originalHandles)));
        await using var context = host.Database.Factory.CreateDbContext();
        Assert.Contains(await context.KnowledgePublications.ToArrayAsync(), item =>
            item.PublicationId == originalPublication.PublicationId && item.CorpusId != Guid.Empty);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExistingClBusiness_DoesNotPersistFailedResolutionThenReusesUnresolvedOutcomeAfterSynthesisFailureAsync()
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        string callerObservation = """{"observationKey":"local-call","ownerSelector":"file","role":"caller","mechanism":"call","operation":"Run","logicalNamespace":null,"literalsOrExpressions":["Run()"],"qualification":"SourceSupported","evidence":[{"startLine":1,"endLine":1}]}""";
        host.Client.TerminalTextOverride = OctoberScanGenerateRunnerTests.Harness.Model.ValidProposalJson.Replace(
            "\"observations\":[]", "\"observations\":[" + callerObservation + "]", StringComparison.Ordinal);
        var original = await host.Runner.ExecuteAsync(host.Execution);
        Assert.Equal(0, original.Report!.ExitCode);
        var originalPublication = Assert.IsType<PublicationBinding>(original.Report.Publication);
        Assert.Contains(original.Report.Documents, handle => handle.PublicationId == originalPublication.PublicationId);

        using (var invalidResolutionTransport = new NaturalTransport { IncludeCallerObservation = true })
        {
            var execution = ExistingClExecution(host.Execution, originalPublication);
            await using var runner = new OctoberGenerateRunner(ExistingClDependencies(host, execution, invalidResolutionTransport));
            var failed = await runner.ExecuteAsync(execution);
            Assert.NotEqual(0, failed.Report!.ExitCode);
            Assert.Equal(1, invalidResolutionTransport.ResolutionRequests);
            Assert.Equal(0, invalidResolutionTransport.WorkerRequests);
        }

        await using (var context = host.Database.Factory.CreateDbContext())
            Assert.Empty(await context.ResolutionItems.ToArrayAsync());

        using (var validUnresolvedTransport = new NaturalTransport
        {
            IncludeCallerObservation = true,
            ReturnUnresolved = true,
            InvalidSynthesisProposal = true
        })
        {
            var execution = ExistingClExecution(host.Execution, originalPublication);
            await using var runner = new OctoberGenerateRunner(ExistingClDependencies(host, execution, validUnresolvedTransport));
            var failedAfterProgress = await runner.ExecuteAsync(execution);
            Assert.NotEqual(0, failedAfterProgress.Report!.ExitCode);
            Assert.Equal(1, validUnresolvedTransport.ResolutionRequests);
            Assert.Equal(2, validUnresolvedTransport.BusinessRequests);
        }

        string persistedOutcomeJson;
        await using (var context = host.Database.Factory.CreateDbContext())
        {
            var item = Assert.Single(await context.ResolutionItems.AsNoTracking().ToArrayAsync());
            persistedOutcomeJson = item.OutcomeJson;
            var persisted = JsonSerializer.Deserialize<ResolutionOutcome>(persistedOutcomeJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.Equal(ResolutionOutcomeKind.Unresolved, persisted.Kind);
            Assert.Null(persisted.Relationship);
        }

        using (var retryTransport = new NaturalTransport
        {
            IncludeCallerObservation = true,
            ReturnUnresolved = true,
            ExpectedBusinessRequests = 1
        })
        {
            var execution = ExistingClExecution(host.Execution, originalPublication);
            await using var runner = new OctoberGenerateRunner(ExistingClDependencies(host, execution, retryTransport));
            var recovered = await runner.ExecuteAsync(execution);
            Assert.Equal(0, recovered.Report!.ExitCode);
            Assert.Equal(0, retryTransport.ResolutionRequests);
            Assert.Equal(1, retryTransport.BusinessRequests);
            Assert.IsType<PublicationBinding>(recovered.Report.Publication);
        }

        await using (var context = host.Database.Factory.CreateDbContext())
        {
            var rows = await context.ResolutionItems.AsNoTracking().ToArrayAsync();
            var onlyRow = Assert.Single(rows);
            var finalOutcome = JsonSerializer.Deserialize<ResolutionOutcome>(onlyRow.OutcomeJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal(persistedOutcomeJson, onlyRow.OutcomeJson);
            Assert.Equal(ResolutionOutcomeKind.Unresolved, finalOutcome!.Kind);
            Assert.DoesNotContain(rows, item =>
            {
                var outcome = JsonSerializer.Deserialize<ResolutionOutcome>(item.OutcomeJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return outcome?.Kind is ResolutionOutcomeKind.Failed or ResolutionOutcomeKind.NotEvaluated;
            });
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task InternalNaturalCompletion_UsesActualOwnerRootSelectionAndSameRealProviderClientAsync(bool mismatchedConfiguration, bool twoRepositories)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync(twoRepositories: twoRepositories);
        using var transport = new NaturalTransport { ExpectedWorkers = twoRepositories ? 2 : 1 };
        var execution = host.Execution with { Policy = host.Execution.Policy with { OutputStage = "cl-and-bl" } };
        var selected = execution.Provider;
        var profile = selected.Profile;
        var projection = new BusinessProviderConfigurationProjection(selected.SourceIdentity, 0, profile.Name, "chat-completions", profile.Endpoint,
            mismatchedConfiguration ? "foreign-business-model" : profile.Model, profile.CredentialReference);
        int clientCreations = 0, rootSelections = 0;
        var composition = new OctoberBusinessRunComposition(
            (authority, guard) => new OctoberResolutionStore(host.Database.Factory, authority, guard, host.Database.Log),
            authority => new OctoberFlowPlanningInputLoader(host.Database.Factory, authority),
            (authority, guard) => new OctoberFlowPublicationStore(host.Database.Factory, authority, guard, host.Database.Log),
            input =>
            {
                rootSelections++;
                // Explicit trusted selector over the actual owner-loaded identities, never inferred roots.
                var unit = Assert.Single(input.Units.Where(unit => unit.SemanticSelector == "file" && unit.PrimaryOrigin.RepositoryId == host.Database.Origin.RepositoryId));
                transport.Root = unit.Document;
                return [new FlowRootSelection(unit.UnitId, unit.Document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector)];
            }, projection, "Controlled resolution: use accepted evidence only.", "Controlled synthesis: preserve accepted evidence and qualifications.", new(), 32768);
        composition = composition with { BindReader = (session, publication, _) =>
        {
            Assert.Same(execution.Session, session);
            return Task.FromResult<OctoberVerifiedReaderBinding?>(ControlledBinding(host, publication, session, null));
        } };
        var dependencies = host.Dependencies with
        {
            Business = composition,
            CreateWorkerClient = async (provider, token) =>
            {
                clientCreations++;
                Assert.Same(selected, provider);
                return await new OctoberWorkerClientFactory(new SyntheticSecret(), transport).CreateAsync(provider, token);
            },
        };
        await using var runner = new OctoberGenerateRunner(dependencies);
        var result = await runner.ExecuteAsync(execution);
        var report = Assert.IsType<OctoberScanReport>(result.Report);
        if (mismatchedConfiguration)
        {
            Assert.NotEqual(0, report.ExitCode);
            Assert.Equal(0, host.BeginCalls);
            Assert.Equal(0, clientCreations);
            Assert.Empty(transport.Requests);
            return;
        }
        Assert.True(report.ExitCode == 0, $"Expected complete natural route; exit={report.ExitCode}; gaps={string.Join(",", report.Gaps.Select(gap => gap.Code))}");
        Assert.Equal("cl-and-bl", report.CompletedStage);
        Assert.Equal(OctoberPhaseStatus.Completed, report.Matching);
        Assert.Equal(OctoberPhaseStatus.Completed, report.BusinessLogic);
        Assert.Equal(1, clientCreations);
        Assert.Equal(1, rootSelections);
        Assert.Equal(twoRepositories ? 2 : 1, transport.WorkerRequests);
        Assert.Equal(1, transport.BusinessRequests);
        Assert.Equal(twoRepositories ? 3 : 2, report.Usage.Requests);
        Assert.False(report.Usage.HasUnknownUsage);
        Assert.Equal(1, host.BeginCalls);
        Assert.Equal(1, host.FreezeCalls);
        Assert.Equal(twoRepositories ? 3 : 2, report.Documents.Length);
        Assert.All(transport.Requests, request =>
        {
            Assert.Equal("https://never-contacted.invalid/v1/chat/completions", request.Endpoint);
            Assert.Equal("Bearer synthetic-approved-token", request.Authorization);
            Assert.Equal(profile.Model, request.Model);
        });
        await using var context = host.Database.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        var publication = Assert.IsType<PublicationBinding>(report.Publication);
        var reader = Reader(host, publication, "ordinary-reader");
        var opened = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-reader", "separate-authentication"), KnowledgeMode.IT, KnowledgePurpose.Reader, [.. execution.Authority.RepositoryIds]));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        foreach (var handle in report.Documents)
        {
            var read = await reader.ReadExactAsync(new(opened.Selection!, handle));
            Assert.Equal(KnowledgeReadStatus.Success, read.Status);
            Assert.NotEmpty(read.Content!);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("freeze-unknown")]
    [InlineData("publication-ack")]
    [InlineData("reason-observer")]
    [InlineData("reason-invalid-proposal")]
    [InlineData("publication-first-lookup-unknown")]
    [InlineData("publication-failed")]
    [InlineData("publication-committed-failed")]
    [InlineData("publication-undefined-status")]
    [InlineData("publication-foreign-operation")]
    [InlineData("publication-uncommitted")]
    [InlineData("empty-roots")]
    [InlineData("default-roots")]
    [InlineData("revoked")]
    [InlineData("session-expired")]
    [InlineData("nonempty-b1")]
    [InlineData("nonempty-b1-direct")]
    [InlineData("unresolved-b1")]
    [InlineData("budget-b1-direct")]
    [InlineData("budget-b1-ack")]
    [InlineData("publication-held-log")]
    [InlineData("publication-held-revoked")]
    [InlineData("publication-held-scope-stop")]
    [InlineData("replay-authority-held")]
    [InlineData("binding-valid")]
    [InlineData("operator-valid")]
    [InlineData("operator-empty")]
    [InlineData("operator-absent")]
    [InlineData("operator-stale")]
    [InlineData("operator-foreign")]
    [InlineData("operator-duplicate")]
    [InlineData("operator-revoked-after")]
    [InlineData("operator-held-cancel")]
    [InlineData("operator-cleanup-foreign")]
    [InlineData("operator-broker-no-response")]
    [InlineData("operator-broker-valid")]
    [InlineData("operator-scope-before")]
    [InlineData("operator-scope-after")]
    [InlineData("cl-only-binding-unused")]
    [InlineData("binding-null")]
    [InlineData("binding-subject")]
    [InlineData("binding-publication")]
    [InlineData("binding-mode")]
    [InlineData("binding-purpose")]
    [InlineData("binding-scope")]
    [InlineData("binding-held-acquire")]
    [InlineData("binding-held-read")]
    [InlineData("scope-stop-before-commit")]
    [InlineData("freeze-log")]
    [InlineData("publication-log")]
    [InlineData("reader-nonprogress")]
    [InlineData("reader-diagnostic-only-replay")]
    [InlineData("reader-empty-fresh-cursor")]
    [InlineData("scope-stop-before-freeze")]
    public async Task InternalNaturalCompletion_RespectsOwnedPhaseBoundariesAsync(string boundary)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        using var transport = new NaturalTransport { IncludeCallerObservation = boundary is "nonempty-b1" or "nonempty-b1-direct" or "unresolved-b1" or "budget-b1-direct" or "budget-b1-ack", ReturnUnresolved = boundary == "unresolved-b1", InvalidSynthesisProposal = boundary == "reason-invalid-proposal" };
        var state = new BoundaryState { Boundary = boundary };
        if (boundary == "freeze-log") state.BeforeFreeze = () => host.Database.Log.Succeeds = false;
        var clock = boundary is "session-expired" or "replay-authority-held" ? new OctoberScanAcceptanceHarness.ManualClock() : null;
        var execution = host.Execution with { Policy = host.Execution.Policy with { OutputStage = "cl-and-bl" } };
        if (boundary is "operator-broker-no-response" or "operator-broker-valid") execution = execution with { Policy = execution.Policy with { RunDeadlineSeconds = 900, FileDeadlineSeconds = 480 } };
        if (boundary == "session-expired") execution = execution with { Session = new FixedSession(execution.Session.SubjectId, execution.Session.ManagementAuthorityId, clock!.GetUtcNow().AddSeconds(5)) };
        if (boundary == "cl-only-binding-unused") execution = execution with { Policy = execution.Policy with { OutputStage = "cl-only" } };
        var profile = execution.Provider.Profile;
        int clients = 0;
        var composition = new OctoberBusinessRunComposition(
            (authority, guard) => new ResolutionBoundary(state, new OctoberResolutionStore(host.Database.Factory, authority, guard, host.Database.Log)),
            authority => new ObservedPlanningLoader(state, new OctoberFlowPlanningInputLoader(host.Database.Factory, authority)),
            (authority, guard) => new PublicationBoundary(state, new OctoberFlowPublicationStore(host.Database.Factory, authority, guard, host.Database.Log)),
            input =>
            {
                if (boundary.StartsWith("operator-", StringComparison.Ordinal)) throw new InvalidOperationException("Operator mode must not fall back to exact settings");
                state.RootSelections++;
                if (boundary == "empty-roots") return [];
                if (boundary == "default-roots") return default;
                var unit = Assert.Single(input.Units.Where(unit => unit.SemanticSelector == "file"));
                transport.Root = unit.Document;
                if (boundary == "revoked") host.AuthorityAllowed = false;
                if (boundary == "session-expired") clock!.Advance(TimeSpan.FromSeconds(10));
                if (boundary == "replay-authority-held") clock!.Advance(TimeSpan.FromSeconds(1));
                return [new FlowRootSelection(unit.UnitId, unit.Document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector)];
            }, new(execution.Provider.SourceIdentity, 0, profile.Name, "chat-completions", profile.Endpoint, profile.Model, profile.CredentialReference),
            "Controlled accepted resolution only.", "Controlled accepted synthesis only.", new(),
            boundary is "budget-b1-direct" or "budget-b1-ack" ? execution.Policy.MaximumTotalTokens!.Value : 32768);
        composition = composition with { BindReader = async (session, publication, token) =>
        {
            Assert.Same(execution.Session, session);
            if (boundary == "cl-only-binding-unused") throw new InvalidOperationException("CL-only must not bind Business readers");
            state.ReaderBindings++;
            var binding = state.Boundary == "binding-null" ? null : ControlledBinding(host, publication, session, state);
            if (state.Boundary == "binding-held-acquire")
            {
                using var registration = token.Register(() => state.ReaderCancelled.TrySetResult());
                state.ReaderHeld.TrySetResult();
                await state.ReleaseReader.Task; // Deliberately late physical binding acquisition.
            }
            return binding;
        } };
        if (boundary.StartsWith("operator-", StringComparison.Ordinal))
        {
            composition = composition with
            {
                OperatorSelection = new("operator", Path.Combine(Path.GetTempPath(), "unopened-operator-exchange-" + Guid.NewGuid().ToString("N")), boundary is "operator-broker-no-response" or "operator-broker-valid" ? 1 : 30),
                InteractAsync = async (request, token) =>
                {
                    state.Interactions++;
                    Assert.Equal(1, request.Version); Assert.NotEqual(Guid.Empty, request.RequestId); Assert.NotEqual(Guid.Empty, request.RunId);
                    Assert.Equal(state.FrozenCorpus!.CorpusId, request.CorpusId);
                    Assert.True(request.ExpiresAtUtc > DateTimeOffset.UtcNow);
                    var candidate = Assert.Single(request.Candidates);
                    Assert.Equal(host.Database.Origin.RepositoryId, candidate.RepositoryId);
                    Assert.Equal("src/Order.cs", candidate.RelativePath); Assert.Equal("file", candidate.SemanticSelector);
                    Assert.Equal(execution.Setup.BatchId, candidate.Document.BatchId);
                    state.BeforeInteraction?.Invoke();
                    if (boundary == "operator-cleanup-foreign") return await InteractWithForeignEntryAsync(state, request, token);
                    if (boundary is "operator-broker-no-response" or "operator-broker-valid")
                    {
                        transport.Root = candidate.Document;
                        return await InteractWithForeignEntryAsync(state, request, token);
                    }
                    if (boundary == "operator-held-cancel")
                    {
                        using var registration = token.Register(() => state.OperatorCancelled.TrySetResult());
                        state.OperatorEntered.TrySetResult(); await state.ReleaseOperator.Task;
                    }
                    if (boundary == "operator-revoked-after") host.AuthorityAllowed = false;
                    if (boundary == "operator-scope-after") state.StopScope!();
                    if (boundary == "operator-absent") return null;
                    transport.Root = candidate.Document;
                    return new(1, boundary == "operator-stale" ? Guid.NewGuid() : request.RequestId, request.RunId,
                        boundary == "operator-empty" ? [] : boundary == "operator-foreign" ? [Guid.NewGuid()] : boundary == "operator-duplicate" ? [candidate.UnitId, candidate.UnitId] : [candidate.UnitId]);
                },
            };
        }
        var replayAuthority = new HeldReplayAuthority(host.Dependencies.Authority);
        var runner = new OctoberGenerateRunner(host.Dependencies with
        {
            Authority = replayAuthority,
            Business = composition,
            CreateWorkerClient = async (provider, token) => { clients++; return await new OctoberWorkerClientFactory(new SyntheticSecret(), transport).CreateAsync(provider, token); },
            CreateKnowledgeWriter = ports => new FreezeBoundary(state, host.Dependencies.CreateKnowledgeWriter(ports)),
        }, clock);
        await using var runnerLifetime = new RunnerTestLifetime(runner, state);
        if (boundary is "publication-log" or "publication-held-log") state.BeforePublication = () => host.Database.Log.Succeeds = false;
        if (boundary == "scope-stop-before-freeze") state.BeforeFreeze = () => ((OctoberExecutionScope)typeof(OctoberGenerateRunner).GetField("_scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!).HardStop();
        if (boundary == "scope-stop-before-commit") state.BeforePublication = () => ((OctoberExecutionScope)typeof(OctoberGenerateRunner).GetField("_scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!).HardStop();
        state.StopScope = () => ((OctoberExecutionScope)typeof(OctoberGenerateRunner).GetField("_scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!).HardStop();
        if (boundary == "operator-scope-before") state.AfterCatalog = state.StopScope;
        state.BeforeInteraction = () =>
        {
            var activeScope = (OctoberExecutionScope)typeof(OctoberGenerateRunner).GetField("_scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!;
            Assert.Equal(0, activeScope.GetSnapshot().OutstandingAttempts);
            Assert.Equal(1, host.BeginCalls);
        };
        var runElapsed = System.Diagnostics.Stopwatch.StartNew();
        using var callerCancellation = new CancellationTokenSource();
        var pending = runner.ExecuteAsync(execution, callerCancellation.Token);
        if (boundary == "operator-held-cancel")
        {
            try
            {
                await state.OperatorEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                callerCancellation.Cancel();
                await state.OperatorCancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(1))); Assert.False(pending.IsCompleted);
            }
            finally { state.ReleaseOperator.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
        if (boundary is "binding-held-acquire" or "binding-held-read")
        {
            try
            {
                await state.ReaderHeld.Task.WaitAsync(TimeSpan.FromSeconds(5));
                callerCancellation.Cancel();
                await state.ReaderCancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(1)));
                Assert.False(pending.IsCompleted);
                Assert.Equal(0, state.ReaderDisposals);
            }
            finally { state.ReleaseReader.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
        if (boundary is "publication-held-log" or "publication-held-revoked" or "publication-held-scope-stop")
        {
            try
            {
                await state.PublicationHeld.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await state.PublicationBoundExpired.Task.WaitAsync(TimeSpan.FromSeconds(12));
                // The bound-token callback runs before finalizer continuations. Observe the actual
                // post-timeout lookup/runner completion before deciding whether ownership was retained.
                await state.PublicationLookupCompleted.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(1)));
                Assert.False(pending.IsCompleted);
                if (boundary == "publication-held-revoked") host.AuthorityAllowed = false;
                if (boundary == "publication-held-scope-stop")
                    ((OctoberExecutionScope)typeof(OctoberGenerateRunner).GetField("_scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!).HardStop();
            }
            finally { state.ReleasePublication.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
        var report = Assert.IsType<OctoberScanReport>((await pending).Report);
        if (boundary == "reason-observer")
        {
            // Controlled consumer conformance through the existing private observer; this does not
            // claim the owner produced any synthetic result below. The request is from this real run.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var request = Assert.IsType<FinalizationRequest>(typeof(OctoberGenerateRunner).GetField("_originalBusinessRequest", flags)!.GetValue(runner));
            var field = typeof(OctoberGenerateRunner).GetField("_businessDiagnostics", flags)!;
            var observe = typeof(OctoberGenerateRunner).GetMethod("ObserveBusinessOutcome", flags)!;
            var original = Assert.IsType<OctoberBusinessDiagnosticSnapshot>(field.GetValue(runner));
            foreach (var reason in new BusinessSynthesisFailureReason?[] { BusinessSynthesisFailureReason.ProposalShape, BusinessSynthesisFailureReason.None, null, (BusinessSynthesisFailureReason)99999 })
            {
                field.SetValue(runner, original with { OriginalSynthesisFailureReason = null });
                var immediate = new BusinessFinalizationResult(BusinessFinalizationStatus.Failed, request.Operation, null, new(true, []))
                    { OriginalSynthesisFailureReason = reason };
                observe.Invoke(runner, [request, immediate, true]);
                var expected = reason == BusinessSynthesisFailureReason.ProposalShape ? reason : null;
                Assert.Equal(expected, Assert.IsType<OctoberBusinessDiagnosticSnapshot>(field.GetValue(runner)).OriginalSynthesisFailureReason);
                observe.Invoke(runner, [request, immediate with { OriginalSynthesisFailureReason = BusinessSynthesisFailureReason.DocumentReference }, false]);
                var afterLookup = Assert.IsType<OctoberBusinessDiagnosticSnapshot>(field.GetValue(runner));
                Assert.Equal(expected, afterLookup.OriginalSynthesisFailureReason);
                Assert.Equal(BusinessFinalizationStatus.Failed, afterLookup.FinalizerStatus);
                Assert.Equal(BusinessFinalizationStatus.Failed, afterLookup.LookupStatus);
            }
            field.SetValue(runner, original with { OriginalSynthesisFailureReason = null });
            var foreign = new BusinessFinalizationResult(BusinessFinalizationStatus.Failed, request.Operation with { OperationId = Guid.NewGuid() }, null, new(true, []))
                { OriginalSynthesisFailureReason = BusinessSynthesisFailureReason.ProposalShape };
            observe.Invoke(runner, [request, foreign, true]);
            var rejected = Assert.IsType<OctoberBusinessDiagnosticSnapshot>(field.GetValue(runner));
            Assert.Null(rejected.OriginalSynthesisFailureReason); Assert.True(rejected.OperationMismatch); Assert.Null(rejected.FinalizerStatus);
            field.SetValue(runner, original);
        }
        if (boundary is "publication-ack" or "publication-first-lookup-unknown" or "publication-failed" or "publication-committed-failed" or "publication-foreign-operation" or "publication-uncommitted" or "publication-undefined-status" or "reader-nonprogress" or "reader-diagnostic-only-replay" or "reader-empty-fresh-cursor" or "binding-null")
        {
            var diagnostic = Assert.IsType<OctoberBusinessDiagnosticSnapshot>(report.BusinessDiagnostics);
            Assert.Equal(boundary == "publication-foreign-operation", diagnostic.OperationMismatch);
            if (boundary == "publication-first-lookup-unknown")
            {
                Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, diagnostic.FinalizerStatus); Assert.Null(diagnostic.FinalizerPersistenceStatus);
                Assert.Equal(BusinessFinalizationStatus.Published, diagnostic.LookupStatus); Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, diagnostic.LookupPersistenceStatus);
            }
            else if (boundary == "publication-foreign-operation") { Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, diagnostic.FinalizerStatus); Assert.Null(diagnostic.FinalizerPersistenceStatus); Assert.Null(diagnostic.LookupStatus); Assert.Null(diagnostic.LookupPersistenceStatus); }
            else if (boundary == "publication-undefined-status") { Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, diagnostic.FinalizerStatus); Assert.Null(diagnostic.FinalizerPersistenceStatus); Assert.Null(diagnostic.LookupStatus); Assert.Null(diagnostic.LookupPersistenceStatus); }
            else if (boundary == "publication-committed-failed")
            {
                // Transparent controlled status projection over the same real committed operation;
                // this does not claim the owner naturally returned Failed for the exercised synthesis.
                Assert.Equal(BusinessFinalizationStatus.Failed, diagnostic.FinalizerStatus); Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, diagnostic.FinalizerPersistenceStatus);
                Assert.Equal(BusinessFinalizationStatus.Failed, diagnostic.LookupStatus); Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, diagnostic.LookupPersistenceStatus);
            }
            else if (boundary == "publication-failed")
            {
                Assert.Equal(BusinessFinalizationStatus.Failed, diagnostic.FinalizerStatus); Assert.Equal(KnowledgeWriteStatus.StorageUnavailable, diagnostic.FinalizerPersistenceStatus);
                Assert.Equal(BusinessFinalizationStatus.Failed, diagnostic.LookupStatus); Assert.Equal(KnowledgeWriteStatus.StorageUnavailable, diagnostic.LookupPersistenceStatus);
            }
            else
            {
                Assert.Equal(boundary == "publication-uncommitted" ? BusinessFinalizationStatus.CommitIndeterminate : BusinessFinalizationStatus.Published, diagnostic.FinalizerStatus);
                Assert.Equal(BusinessFinalizationStatus.Published, diagnostic.LookupStatus);
                Assert.Equal(boundary == "publication-uncommitted" ? KnowledgeWriteStatus.StorageUnavailable : KnowledgeWriteStatus.AlreadyCommitted, diagnostic.LookupPersistenceStatus);
            }
            Assert.Equal(boundary is "publication-ack" or "publication-first-lookup-unknown" ? OctoberBusinessDiagnosticBoundary.Completed : boundary == "binding-null" ? OctoberBusinessDiagnosticBoundary.ReaderBinding : boundary is "reader-nonprogress" or "reader-diagnostic-only-replay" or "reader-empty-fresh-cursor" ? OctoberBusinessDiagnosticBoundary.ExactReader : OctoberBusinessDiagnosticBoundary.ExactLookup, diagnostic.Boundary);
        }
        if (boundary is "revoked" or "publication-held-revoked" or "operator-revoked-after" or "cl-only-binding-unused") Assert.Null(report.BusinessDiagnostics);
        Assert.Equal(1, clients);
        Assert.Equal(1, transport.WorkerRequests);
        if (boundary.StartsWith("operator-", StringComparison.Ordinal))
        {
            Assert.Equal(boundary == "operator-scope-before" ? 0 : 1, state.Interactions); Assert.Equal(0, state.RootSelections);
            if (boundary is "operator-valid" or "operator-broker-valid") { Assert.Equal(0, report.ExitCode); Assert.NotNull(report.Publication); Assert.Equal(1, transport.BusinessRequests); }
            else if (boundary == "operator-empty") { Assert.Equal(5, report.ExitCode); Assert.Equal(0, transport.BusinessRequests); }
            else { Assert.NotEqual(0, report.ExitCode); Assert.NotEqual(5, report.ExitCode); Assert.Null(report.Publication); Assert.Equal(0, transport.BusinessRequests); }
            var replay = await runner.ExecuteAsync(execution);
            Assert.Equal(report.ExitCode, replay.Report!.ExitCode); Assert.Equal(boundary == "operator-scope-before" ? 0 : 1, state.Interactions);
            if (boundary == "operator-revoked-after") { Assert.Empty(report.Documents); Assert.Empty(report.Inventory); Assert.Empty(report.AcceptedFiles); }
            if (boundary is "operator-broker-no-response" or "operator-broker-valid")
            {
                Assert.True(state.BrokerRequestObserved);
                Assert.False(callerCancellation.IsCancellationRequested);
                Assert.True(execution.Session.ExpiresAtUtc > DateTimeOffset.UtcNow);
                Assert.Equal(900, execution.Policy.RunDeadlineSeconds); Assert.Equal(480, execution.Policy.FileDeadlineSeconds);
                Assert.InRange(execution.Session.ExpiresAtUtc - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));
                Assert.True(runElapsed.Elapsed < TimeSpan.FromSeconds(480));
                Assert.False(Directory.Exists(state.ExchangeDirectory));
                if (boundary == "operator-broker-no-response")
                {
                    Assert.Equal("controlled-selector-rejected", state.SelectorRejection);
                    Assert.False(state.BrokerResponseWritten);
                    // Diagnostic current behavior: operator timeout is reported as run_interrupted and hides
                    // durable progress. This is an observed misclassification, not the desired future contract.
                    Assert.Equal(3, report.ExitCode); Assert.Equal(OctoberExecutionStatus.Interrupted, report.Execution);
                    Assert.Contains(report.Gaps, gap => gap.Code == "run_interrupted");
                    Assert.Null(report.Publication); Assert.Empty(report.Documents); Assert.Empty(report.AcceptedFiles);
                    Assert.Empty(report.Inventory); Assert.Empty(report.TerminalCoverage);
                    Assert.Equal(1, report.Usage.Requests); Assert.Equal(100, report.Usage.InputTokens); Assert.Equal(40, report.Usage.OutputTokens);
                    Assert.False(report.Usage.HasUnknownUsage); Assert.Equal(0, transport.BusinessRequests);
                    await using var context = host.Database.Factory.CreateDbContext();
                    Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
                    Assert.Equal(1, await context.CorpusSelections.CountAsync());
                    Assert.Equal(0, await context.KnowledgePublications.CountAsync());
                }
                else { Assert.True(state.BrokerResponseWritten); Assert.Null(state.SelectorRejection); }
            }
            if (boundary == "operator-cleanup-foreign")
            {
                Assert.True(state.BrokerResponseWritten, "The real request and atomic response must complete before cleanup classification is tested.");
                Assert.Equal("preserve caller-owned marker", await File.ReadAllTextAsync(Path.Combine(state.ExchangeDirectory!, "foreign.txt")));
                Assert.False(File.Exists(Path.Combine(state.ExchangeDirectory!, "root-selection.request.json")));
                Assert.False(File.Exists(Path.Combine(state.ExchangeDirectory!, "root-selection.response.json")));
                Assert.Equal(4, report.ExitCode);
                Assert.Contains(report.Gaps, gap => gap.Code == "owned_cleanup_unconfirmed");
                await Assert.ThrowsAnyAsync<Exception>(() => runner.DisposeAsync().AsTask());
                state.ExpectedCleanupRefusalObserved = true;
            }
        }
        else        if (boundary == "cl-only-binding-unused")
        {
            Assert.Equal(0, report.ExitCode); Assert.Equal("cl-only", report.CompletedStage);
            Assert.Equal(0, state.ReaderBindings); Assert.Equal(0, state.RootSelections); Assert.Equal(0, transport.BusinessRequests);
            Assert.Equal(OctoberPhaseStatus.NotRequested, report.Matching); Assert.Equal(OctoberPhaseStatus.NotRequested, report.BusinessLogic);
        }
        else if (boundary.StartsWith("binding-", StringComparison.Ordinal))
        {
            Assert.Equal(1, state.ReaderBindings);
            Assert.Equal(boundary == "binding-null" ? 0 : 1, state.ReaderDisposals);
            Assert.Equal(1, state.Publications);
            if (boundary == "binding-valid") { Assert.Equal(0, report.ExitCode); Assert.Equal(1, state.ReaderOpens); Assert.Equal(2, report.Documents.Length); }
            else { Assert.NotEqual(0, report.ExitCode); Assert.Null(report.Publication); Assert.Empty(report.Documents); Assert.Equal(boundary == "binding-held-read" ? 1 : 0, state.ReaderOpens); }
        }
        else if (boundary is "publication-foreign-operation" or "publication-uncommitted" or "publication-failed" or "publication-committed-failed" or "publication-undefined-status")
        {
            Assert.NotEqual(0, report.ExitCode); Assert.Null(report.Publication); Assert.Empty(report.Documents);
            Assert.Equal(OctoberPhaseStatus.Unavailable, report.Matching);
            Assert.Equal(OctoberPhaseStatus.Unavailable, report.BusinessLogic);
            Assert.Equal(1, state.Publications);
        }
        else if (boundary == "replay-authority-held")
        {
            Assert.Equal(0, report.ExitCode);
            replayAuthority.Armed = true;
            var replay = runner.ExecuteAsync(execution);
            try
            {
                await replayAuthority.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
                clock!.Advance(TimeSpan.FromSeconds(6));
                await replayAuthority.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Task.WhenAny(replay, Task.Delay(TimeSpan.FromSeconds(1)));
                Assert.False(replay.IsCompleted);
            }
            finally { replayAuthority.Release.TrySetResult(); await replay.WaitAsync(TimeSpan.FromSeconds(5)); }
            var hidden = Assert.IsType<OctoberScanReport>((await replay).Report);
            Assert.Null(hidden.Publication); Assert.Empty(hidden.Documents); Assert.Empty(hidden.Inventory);
            Assert.Empty(hidden.AcceptedFiles); Assert.Empty(hidden.TerminalCoverage);
            Assert.Null(hidden.BusinessDiagnostics);
            Assert.Equal(OctoberPhaseStatus.Unavailable, hidden.Matching); Assert.Equal(OctoberPhaseStatus.Unavailable, hidden.BusinessLogic);
            Assert.Equal(1, state.Publications); Assert.Equal(1, transport.BusinessRequests);
            await using var context = host.Database.Factory.CreateDbContext();
            Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        }
        else if (boundary == "freeze-unknown")
        {
            Assert.Equal(4, report.ExitCode); Assert.Equal(0, state.RootSelections); Assert.Equal(0, transport.BusinessRequests);
            Assert.Null(report.Publication); Assert.Equal(0, state.Publications);
        }
        else if (boundary is "budget-b1-direct" or "budget-b1-ack")
        {
            Assert.Equal(0, transport.ResolutionRequests);
            Assert.Equal(0, transport.BusinessRequests);
            Assert.Equal(1, state.ResolutionAccepts);
            if (boundary == "budget-b1-ack") Assert.True(state.ResolutionLookups > 0);
            await using var context = host.Database.Factory.CreateDbContext();
            var item = Assert.Single(await context.ResolutionItems.ToArrayAsync());
            var outcome = JsonSerializer.Deserialize<ResolutionOutcome>(item.OutcomeJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal(ResolutionOutcomeKind.NotEvaluated, outcome!.Kind);
            Assert.Null(outcome.Relationship);
            Assert.NotEqual(0, report.ExitCode);
            Assert.Contains(report.Gaps, gap => gap.Code.Contains("budget", StringComparison.Ordinal) || gap.Code.Contains("limit", StringComparison.Ordinal));
            var replay = await runner.ExecuteAsync(execution);
            Assert.Equal(report.Publication, replay.Report!.Publication);
            Assert.Equal(1, state.ResolutionAccepts);
            Assert.Equal(0, transport.BusinessRequests);
        }
        else if (boundary is "publication-held-revoked" or "publication-held-scope-stop")
        {
            Assert.NotEqual(0, report.ExitCode);
            Assert.Null(report.Publication);
            Assert.Empty(report.Documents);
            Assert.Equal(OctoberPhaseStatus.Unavailable, report.Matching); Assert.Equal(OctoberPhaseStatus.Unavailable, report.BusinessLogic);
            if (boundary == "publication-held-revoked")
            {
                Assert.Empty(report.Inventory);
                Assert.Empty(report.AcceptedFiles);
                Assert.Empty(report.TerminalCoverage);
            }
            Assert.Equal(1, state.Publications);
            Assert.Equal(1, transport.BusinessRequests);
            var replay = await runner.ExecuteAsync(execution);
            Assert.Null(replay.Report!.Publication);
            Assert.Equal(1, state.Publications);
            Assert.Equal(1, transport.BusinessRequests);
            await using var context = host.Database.Factory.CreateDbContext();
            Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        }
        else if (boundary == "reader-empty-fresh-cursor")
        {
            Assert.NotEqual(0, report.ExitCode); Assert.NotEqual(5, report.ExitCode);
            Assert.InRange(state.ReaderCalls, 1, 2);
            Assert.Equal(1, state.Publications);
        }
        else if (boundary == "scope-stop-before-freeze")
        {
            Assert.NotEqual(0, report.ExitCode); Assert.Null(report.Publication);
            Assert.Equal(0, transport.BusinessRequests);
            await using var context = host.Database.Factory.CreateDbContext();
            Assert.Equal(0, await context.CorpusSelections.CountAsync());
        }        else if (boundary is "reader-nonprogress" or "reader-diagnostic-only-replay")
        {
            Assert.NotEqual(0, report.ExitCode); Assert.NotEqual(5, report.ExitCode);
            Assert.InRange(state.ReaderCalls, 1, 3);
            var originalDiagnostic = Assert.IsType<OctoberBusinessDiagnosticSnapshot>(report.BusinessDiagnostics);
            if (boundary == "reader-diagnostic-only-replay")
            {
                // Isolate the cached metadata predicate using the actual observed snapshot. No new
                // receipt or diagnostic is fabricated; every other metadata field is removed.
                var onlyDiagnostic = report with { Publication = null, Documents = [], AcceptedFiles = [], TerminalCoverage = [], Inventory = [] };
                typeof(OctoberGenerateRunner).GetField("_executionTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(runner, Task.FromResult(new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.GenerateFinished, Report: onlyDiagnostic)));
                Assert.Same(originalDiagnostic, onlyDiagnostic.BusinessDiagnostics);
            }
            host.AuthorityAllowed = false;
            var hiddenReplay = await runner.ExecuteAsync(execution);
            Assert.Null(hiddenReplay.Report!.BusinessDiagnostics); Assert.Null(hiddenReplay.Report.Publication); Assert.Empty(hiddenReplay.Report.Documents);
            Assert.Same(originalDiagnostic, report.BusinessDiagnostics); // Previously observed immutable snapshot is not mutated or reattached.
            Assert.Equal(1, state.Publications);
        }        else if (boundary == "freeze-log")
        {
            Assert.Equal(1, report.ExitCode); Assert.Equal(0, transport.BusinessRequests);
            Assert.Equal(0, state.Publications); Assert.Null(report.Publication);
        }
        else if (boundary == "scope-stop-before-commit")
        {
            Assert.NotEqual(0, report.ExitCode); Assert.Null(report.Publication);
            await using var context = host.Database.Factory.CreateDbContext();
            Assert.Equal(0, await context.KnowledgePublications.CountAsync());
        }
        else if (boundary is "publication-log" or "publication-held-log")
        {
            Assert.Equal(1, report.ExitCode); Assert.Equal(OctoberExecutionStatus.Failed, report.Execution);
            Assert.Equal(OctoberPublicationStatus.Confirmed, report.PublicationStatus); Assert.Equal("cl-and-bl", report.CompletedStage);
            Assert.NotNull(report.Publication); Assert.Equal(2, report.Documents.Length);
            Assert.Contains(report.Gaps, gap => gap.Code == "operation_log_unavailable");
            Assert.Equal(1, state.Publications); Assert.Equal(1, transport.BusinessRequests);
            var repeated = await runner.ExecuteAsync(execution);
            Assert.Equal(report.Publication, repeated.Report!.Publication); Assert.Equal(1, state.Publications);
        }        else if (boundary is "publication-ack" or "publication-first-lookup-unknown" or "reason-observer")
        {
            Assert.Equal(0, report.ExitCode); Assert.Equal("cl-and-bl", report.CompletedStage);
            Assert.NotNull(report.Publication); Assert.Equal(1, state.Publications); Assert.True(state.PublicationLookups > 0);
            Assert.Equal(1, transport.BusinessRequests);
            var replay = await runner.ExecuteAsync(execution);
            Assert.Equal(report.Publication, replay.Report!.Publication); Assert.Equal(1, state.Publications); Assert.Equal(1, transport.BusinessRequests);
        }
        else if (boundary == "reason-invalid-proposal")
        {
            // Real owner validation and finalization, using a parsed but structurally invalid proposal.
            var diagnostic = Assert.IsType<OctoberBusinessDiagnosticSnapshot>(report.BusinessDiagnostics);
            Assert.Equal(BusinessSynthesisFailureReason.ProposalShape, diagnostic.OriginalSynthesisFailureReason);
            Assert.Equal(BusinessFinalizationStatus.Failed, diagnostic.FinalizerStatus);
            Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, diagnostic.FinalizerPersistenceStatus);
            Assert.Equal(BusinessFinalizationStatus.Failed, diagnostic.LookupStatus);
            Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, diagnostic.LookupPersistenceStatus);
            Assert.NotEqual(0, report.ExitCode); Assert.Null(report.Publication); Assert.Empty(report.Documents);
            Assert.Equal(1, state.Publications); Assert.Equal(1, transport.BusinessRequests);
            var replay = await runner.ExecuteAsync(execution);
            Assert.Equal(BusinessSynthesisFailureReason.ProposalShape, replay.Report!.BusinessDiagnostics!.OriginalSynthesisFailureReason);
            host.AuthorityAllowed = false;
            var hidden = await runner.ExecuteAsync(execution);
            Assert.Null(hidden.Report!.BusinessDiagnostics); Assert.Null(hidden.Report.Publication); Assert.Empty(hidden.Report.Documents);
            Assert.Equal(1, state.Publications); Assert.Equal(1, transport.BusinessRequests);
        }
        else if (boundary == "empty-roots")
        {
            Assert.Equal(5, report.ExitCode); Assert.Equal(1, state.RootSelections); Assert.Equal(0, transport.BusinessRequests);
        }
        else if (boundary is "default-roots" or "revoked" or "session-expired")
        {
            Assert.NotEqual(0, report.ExitCode); Assert.NotEqual(5, report.ExitCode);
            Assert.Equal(0, transport.BusinessRequests); Assert.Equal(0, state.Publications); Assert.Null(report.Publication);
            if (boundary == "revoked") { Assert.Empty(report.Documents); Assert.Empty(report.Inventory); Assert.Empty(report.AcceptedFiles); }
        }
        else
        {
            Assert.Equal(1, transport.ResolutionRequests);
            Assert.Equal(1, state.ResolutionAccepts);
            if (boundary == "nonempty-b1") Assert.True(state.ResolutionLookups > 0);
            await using var context = host.Database.Factory.CreateDbContext();
            var item = Assert.Single(await context.ResolutionItems.ToArrayAsync());
            var outcome = JsonSerializer.Deserialize<ResolutionOutcome>(item.OutcomeJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal(boundary == "unresolved-b1" ? ResolutionOutcomeKind.Unresolved : ResolutionOutcomeKind.Failed, outcome!.Kind);
            Assert.Null(outcome.Relationship);
            Assert.Equal(boundary == "unresolved-b1" ? 0 : 1, report.ExitCode);
            Assert.Equal(boundary == "unresolved-b1" ? OctoberExecutionStatus.Completed : OctoberExecutionStatus.Failed, report.Execution);
            if (boundary == "unresolved-b1") Assert.NotNull(outcome.Uncertainty);
            Assert.Equal(boundary == "unresolved-b1" ? OctoberPhaseStatus.Completed : OctoberPhaseStatus.Failed, report.Matching);
            Assert.Equal(OctoberPhaseStatus.Completed, report.BusinessLogic);
            Assert.Equal(OctoberPublicationStatus.Confirmed, report.PublicationStatus);
            Assert.NotNull(report.Publication);
        }
    }

    // Explicit controlled binding: verifies Scan consumption only, not real Identity authentication.
    private static OctoberVerifiedReaderBinding ControlledBinding(OctoberScanGenerateRunnerTests.Harness host, PublicationBinding publication, IOctoberOperatorSession session, BoundaryState? state)
    {
        var subject = new KnowledgeSubject(session.SubjectId, "controlled-verified-reader-stamp");
        var selection = new ReadSelectionRequest(publication, subject, KnowledgeMode.IT, KnowledgePurpose.Reader, [.. host.Execution.Authority.RepositoryIds]);
        selection = state?.Boundary switch
        {
            "binding-subject" => selection with { Subject = new("foreign-subject", subject.AuthenticationBinding) },
            "binding-publication" => selection with { Publication = publication with { PublicationId = Guid.NewGuid() } },
            "binding-mode" => selection with { Mode = KnowledgeMode.Business },
            "binding-purpose" => selection with { Purpose = KnowledgePurpose.Synthesis },
            "binding-scope" => selection with { RepositoryScope = [Guid.NewGuid()] },
            _ => selection,
        };
        var lifetime = new ReaderLifetime(state);
        IOctoberKnowledgeReader reader = Reader(host, publication, session.SubjectId);
        if (state?.Boundary is "reader-nonprogress" or "reader-diagnostic-only-replay" or "reader-empty-fresh-cursor") reader = new NonprogressReader(state, reader);
        return new(subject, selection, new BoundReader(reader, selection, lifetime, state), lifetime);
    }
    private sealed class ReaderLifetime(BoundaryState? state) : IAsyncDisposable
    {
        internal bool Disposed;
        public ValueTask DisposeAsync() { Assert.False(Disposed); Disposed = true; if (state is not null) state.ReaderDisposals++; return ValueTask.CompletedTask; }
    }
    private sealed class BoundReader(IOctoberKnowledgeReader inner, ReadSelectionRequest expected, ReaderLifetime lifetime, BoundaryState? state) : IOctoberKnowledgeReader
    {
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken token = default)
        {
            Assert.False(lifetime.Disposed); Assert.Equal(expected, request);
            Assert.Equal("controlled-verified-reader-stamp", request.Subject.AuthenticationBinding);
            if (state is not null) state.ReaderOpens++;
            return inner.OpenReadSelectionAsync(request, token);
        }
        public async Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken token = default)
        {
            Assert.False(lifetime.Disposed);
            var result = await inner.SearchAsync(request, token);
            if (state?.Boundary == "binding-held-read")
            {
                using var registration = token.Register(() => state.ReaderCancelled.TrySetResult());
                state.ReaderHeld.TrySetResult();
                await state.ReleaseReader.Task; // Actual owner read finished; its physical return is still owned.
            }
            Assert.False(lifetime.Disposed);
            return result;
        }
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken token = default) => inner.ReadExactAsync(request, token);
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken token = default) => inner.GetRelatedAsync(request, token);
    }
    private static async Task<OctoberOperatorRootSelectionResponse?> InteractWithForeignEntryAsync(BoundaryState state, OctoberOperatorRootSelectionRequest request, CancellationToken token)
    {
        state.ExchangeDirectory = Path.Combine(Path.GetTempPath(), "october-host-operator-cleanup-" + Guid.NewGuid().ToString("N"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var pending = new OctoberOperatorRootSelection(new("operator", state.ExchangeDirectory, state.Boundary is "operator-broker-no-response" or "operator-broker-valid" ? 1 : 5)).InteractAsync(request, cancellation.Token);
        try
        {
        string requestPath = Path.Combine(state.ExchangeDirectory, "root-selection.request.json");
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (!File.Exists(requestPath))
        {
            if (pending.IsCompleted) return await pending;
            if (elapsed.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("Owned broker request did not appear");
            await Task.Delay(10, token);
        }
        var observed = JsonSerializer.Deserialize<OctoberOperatorRootSelectionRequest>(await File.ReadAllTextAsync(requestPath, token), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(request.RequestId, observed.RequestId); Assert.Equal(request.RunId, observed.RunId);
        Assert.Equal(request.Candidates.ToArray(), observed.Candidates.ToArray());
        state.BrokerRequestObserved = true;
        if (state.Boundary == "operator-broker-no-response")
        {
            state.SelectorRejection = "controlled-selector-rejected"; // Deliberate rejection: no response file is written.
            return await pending;
        }
        if (state.Boundary == "operator-cleanup-foreign")
            await File.WriteAllTextAsync(Path.Combine(state.ExchangeDirectory, "foreign.txt"), "preserve caller-owned marker", token);
        string temporary = Path.Combine(state.ExchangeDirectory, "response.tmp");
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new OctoberOperatorRootSelectionResponse(1, request.RequestId, request.RunId, [request.Candidates[0].UnitId]), new JsonSerializerOptions(JsonSerializerDefaults.Web)), token);
        File.Move(temporary, Path.Combine(state.ExchangeDirectory, "root-selection.response.json"));
        state.BrokerResponseWritten = true;
        return await pending;
        }
        finally { cancellation.Cancel(); try { await pending.WaitAsync(TimeSpan.FromSeconds(6)); } catch (Exception) { } }
    }
    private sealed class RunnerTestLifetime(OctoberGenerateRunner runner, BoundaryState state) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await runner.DisposeAsync(); }
            catch (Exception) when (state.ExpectedCleanupRefusalObserved) { /* Explicitly asserted unconfirmed cleanup; never relabelled settled. */ }
            finally
            {
                if (state.ExchangeDirectory is { } directory && Directory.Exists(directory))
                {
                    foreach (string name in new[] { "foreign.txt", "response.tmp", "root-selection.request.json", "root-selection.response.json" })
                        File.Delete(Path.Combine(directory, name));
                    Directory.Delete(directory, recursive: false);
                }
            }
        }
    }
    private sealed class HeldReplayAuthority(IOctoberLocalAuthority inner) : IOctoberLocalAuthority
    {
        internal bool Armed;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken token) => inner.AuthenticateAsync(token);
        public async Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(IOctoberOperatorSession session, OctoberAuthoritySelectionRequest request, CancellationToken token)
        {
            if (Armed)
            {
                using var registration = token.Register(() => Cancelled.TrySetResult());
                Entered.TrySetResult();
                await Release.Task; // Physical authority work intentionally ignores its cancelled wait bound.
            }
            return await inner.AuthorizeSelectionAsync(session, request, token);
        }
        public Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(IOctoberOperatorSession session, IOctoberHistoricalBoundSetupData setup, Guid repositoryId, CancellationToken token) => inner.IssueSourceApprovalAsync(session, setup, repositoryId, token);
        public Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(IOctoberOperatorSession session, string targetId, PreparedSourceApproval approval, CancellationToken token) => inner.VerifySourceApprovalAsync(session, targetId, approval, token);
        public Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(IOctoberOperatorSession session, string targetId, string approvalIdentity, CancellationToken token) => inner.RevokeSourceApprovalAsync(session, targetId, approvalIdentity, token);
    }
    private sealed class NonprogressReader(BoundaryState state, IOctoberKnowledgeReader inner) : IOctoberKnowledgeReader
    {
        private SearchPage? _observed;
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken token = default) => inner.OpenReadSelectionAsync(request, token);
        public async Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken token = default)
        {
            state.ReaderCalls++;
            if (state.ReaderCalls > 3) throw new InvalidOperationException("Controlled reader nonprogress guard");
            // Actual owner-loaded page, then controlled malformed continuation replay; not a second storage reader.
            _observed ??= (await inner.SearchAsync(request, token)) with { Next = new ContinuationToken("controlled-nonprogress-cursor") };
            return state.Boundary == "reader-empty-fresh-cursor" ? _observed with { Documents = [], Next = new ContinuationToken("controlled-fresh-cursor-" + state.ReaderCalls) } : _observed;
        }
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken token = default) => inner.ReadExactAsync(request, token);
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken token = default) => inner.GetRelatedAsync(request, token);
    }
    private sealed class BoundaryState
    {
        internal string Boundary = "";
        internal CorpusHandle? FrozenCorpus;
        internal int Interactions;
        internal string? ExchangeDirectory;
        internal bool ExpectedCleanupRefusalObserved;
        internal bool BrokerResponseWritten, BrokerRequestObserved;
        internal string? SelectorRejection;
        internal Action? BeforeInteraction, StopScope, AfterCatalog;
        internal TaskCompletionSource OperatorEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource OperatorCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseOperator { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReaderHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReaderCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseReader { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PublicationLookupCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PublicationHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PublicationBoundExpired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleasePublication { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Action? BeforeFreeze, BeforePublication;
        internal int RootSelections, Publications, PublicationLookups, ResolutionAccepts, ResolutionLookups, ReaderCalls, ReaderBindings, ReaderDisposals, ReaderOpens;
    }
    private sealed record FixedSession(string SubjectId, string ManagementAuthorityId, DateTimeOffset ExpiresAtUtc) : IOctoberOperatorSession
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FreezeBoundary(BoundaryState state, IOctoberKnowledgeWriter inner) : IOctoberKnowledgeWriter
    {
        public Task<KnowledgeWriteResult> AcceptCompletedFileAsync(CompletedFileSubmission submission, CancellationToken token = default) => inner.AcceptCompletedFileAsync(submission, token);
        public async Task<KnowledgeWriteResult> FreezeCorpusAsync(CorpusFreezeRequest request, CancellationToken token = default)
        {
            state.BeforeFreeze?.Invoke();
            var result = await inner.FreezeCorpusAsync(request, token);
            state.FrozenCorpus = result.Corpus;
            return state.Boundary == "freeze-unknown" ? result with { Status = KnowledgeWriteStatus.CommitIndeterminate, Corpus = null } : result;
        }
        public Task<KnowledgeWriteResult> LookupOperationAsync(KnowledgeBinding binding, OperationKey operation, string digest, CancellationToken token = default)
            => state.Boundary == "freeze-unknown" ? Task.FromResult(new KnowledgeWriteResult(KnowledgeWriteStatus.CommitIndeterminate, operation)) : inner.LookupOperationAsync(binding, operation, digest, token);
        public Task<KnowledgeWriteResult> PublishSelectionAsync(PublicationRequest request, CancellationToken token = default) => inner.PublishSelectionAsync(request, token);
    }
    private sealed class PublicationBoundary(BoundaryState state, OctoberFlowPublicationStore inner) : IOctoberFlowPublicationStore, IOctoberFlowPublicationInitialRead
    {
        public Task<BusinessFinalizationInitialRead> ReadForOriginalDispatchAsync(CorpusHandle corpus, OperationKey operation, string digest, CancellationToken token = default) => inner.ReadForOriginalDispatchAsync(corpus, operation, digest, token);
        public async Task<BusinessFinalizationResult> PublishAsync(FlowFinalizationPublicationRequest request, CancellationToken token = default)
        {
            state.Publications++; state.BeforePublication?.Invoke();
            var result = await inner.PublishAsync(request, token);
            if (state.Boundary is "publication-held-log" or "publication-held-revoked" or "publication-held-scope-stop")
            {
                using var registration = token.Register(() => state.PublicationBoundExpired.TrySetResult());
                state.PublicationHeld.TrySetResult();
                await state.ReleasePublication.Task; // Retain actual completed-owner result until controlled physical return.
            }
            return state.Boundary == "publication-ack" ? result with { Status = BusinessFinalizationStatus.CommitIndeterminate, Publication = null } : Result(result);
        }
        public async Task<BusinessFinalizationResult> LookupAsync(CorpusHandle corpus, OperationKey operation, string digest, CancellationToken token = default) { state.PublicationLookups++; var result = await inner.LookupAsync(corpus, operation, digest, token); state.PublicationLookupCompleted.TrySetResult(); return state.Boundary == "publication-first-lookup-unknown" && state.PublicationLookups == 1 ? new(BusinessFinalizationStatus.CommitIndeterminate, operation, null, new(true, [])) : Result(result); }
        private BusinessFinalizationResult Result(BusinessFinalizationResult result) => state.Boundary switch
        {
            "publication-foreign-operation" => result with { Operation = result.Operation with { OperationId = Guid.NewGuid() } },
            "publication-uncommitted" => result with { PersistenceStatus = KnowledgeWriteStatus.StorageUnavailable },
            "publication-committed-failed" => result with { Status = BusinessFinalizationStatus.Failed, Publication = null },
            "publication-failed" => result with { Status = BusinessFinalizationStatus.Failed, PersistenceStatus = KnowledgeWriteStatus.StorageUnavailable, Publication = null },
            "publication-undefined-status" => result with { Status = (BusinessFinalizationStatus)99999, PersistenceStatus = (KnowledgeWriteStatus)99999, Publication = null },
            _ => result,
        };
    }
    private sealed class ObservedPlanningLoader(BoundaryState state, IOctoberFlowPlanningInputLoader inner) : IOctoberFlowPlanningInputLoader
    {
        public async Task<FlowPlanningInputResult> LoadAsync(CorpusHandle corpus, ImmutableArray<FlowRootSelection> roots, CancellationToken token = default)
        {
            var result = await inner.LoadAsync(corpus, roots, token);
            state.AfterCatalog?.Invoke();
            return result;
        }
    }
    private sealed class ResolutionBoundary(BoundaryState state, IOctoberResolutionStore inner) : IOctoberResolutionStore
    {
        public Task<ResolutionCorpusResult> LoadCorpusAsync(CorpusHandle corpus, CancellationToken token = default) => inner.LoadCorpusAsync(corpus, token);
        public async Task<ResolutionAcceptanceResult> AcceptOutcomeAsync(ResolutionAcceptanceRequest request, CancellationToken token = default)
        {
            state.ResolutionAccepts++;
            var result = await inner.AcceptOutcomeAsync(request, token);
            return state.Boundary is "nonempty-b1" or "budget-b1-ack" ? result with { Status = KnowledgeWriteStatus.CommitIndeterminate, Outcome = null } : result;
        }
        public Task<ResolutionAcceptanceResult> LookupOutcomeAsync(CorpusHandle corpus, OperationKey operation, string digest, CancellationToken token = default) { state.ResolutionLookups++; return inner.LookupOutcomeAsync(corpus, operation, digest, token); }
    }
    private static OctoberKnowledgeReader Reader(OctoberScanGenerateRunnerTests.Harness host, PublicationBinding publication, string subject) => new(
        new OctoberKnowledgePublishedSource(host.Database.Factory), publication, new B0bReaderDisclosure { CurrentSubject = subject },
        new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());

    private static async Task<ImmutableArray<OriginalClContentSnapshot>> CaptureOriginalClContentAsync(OctoberScanGenerateRunnerTests.Harness host,
        PublicationBinding publication, IReadOnlyCollection<DocumentHandle> handles)
    {
        var reader = Reader(host, publication, host.Execution.Session.SubjectId);
        var subject = new KnowledgeSubject(host.Execution.Session.SubjectId, "existing-cl-preservation-check");
        var opened = await reader.OpenReadSelectionAsync(new(publication, subject, KnowledgeMode.IT,
            KnowledgePurpose.Reader, [.. host.Execution.Authority.RepositoryIds]));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var snapshots = ImmutableArray.CreateBuilder<OriginalClContentSnapshot>(handles.Count);
        foreach (DocumentHandle handle in handles)
        {
            var read = await reader.ReadExactAsync(new(opened.Selection!, handle));
            Assert.Equal(KnowledgeReadStatus.Success, read.Status);
            Assert.Equal(handle, read.Document!.Handle);
            Assert.NotEmpty(read.Content!);
            snapshots.Add(new(handle, read.Content!));
        }
        return snapshots.ToImmutable();
    }

    private sealed record OriginalClContentSnapshot(DocumentHandle Handle, string Content);

    private static OctoberGenerateExecution ExistingClExecution(OctoberGenerateExecution execution,
        PublicationBinding publication) => execution with
    {
        Policy = execution.Policy with { OutputStage = "cl-and-bl", ExistingClPublicationId = publication.PublicationId }
    };

    private static OctoberGenerateRunnerDependencies ExistingClDependencies(
        OctoberScanGenerateRunnerTests.Harness host, OctoberGenerateExecution execution, NaturalTransport transport)
    {
        var selected = execution.Provider;
        var profile = selected.Profile;
        var projection = new BusinessProviderConfigurationProjection(selected.SourceIdentity, 0, profile.Name,
            "chat-completions", profile.Endpoint, profile.Model, profile.CredentialReference);
        var business = new OctoberBusinessRunComposition(
            (authority, guard) => new OctoberResolutionStore(host.Database.Factory, authority, guard, host.Database.Log),
            authority => new OctoberFlowPlanningInputLoader(host.Database.Factory, authority),
            (authority, guard) => new OctoberFlowPublicationStore(host.Database.Factory, authority, guard, host.Database.Log),
            input =>
            {
                var unit = Assert.Single(input.Units.Where(item => item.SemanticSelector == "file" &&
                    item.PrimaryOrigin.RepositoryId == host.Database.Origin.RepositoryId));
                transport.Root = unit.Document;
                return [new FlowRootSelection(unit.UnitId, unit.Document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector)];
            }, projection, "Use accepted source evidence only.", "Preserve existing CL and qualifications.", new(), 32768)
        {
            BindReader = (session, publication, _) =>
            {
                Assert.Same(execution.Session, session);
                return Task.FromResult<OctoberVerifiedReaderBinding?>(ControlledBinding(host, publication, session, null));
            }
        };
        return host.Dependencies with
        {
            ValidateSourceConfiguration = _ => throw new InvalidOperationException("Existing CL route must not validate new source scopes."),
            CreateAdmissionStore = (_, _) => throw new InvalidOperationException("Existing CL route must not begin a fresh CL scan."),
            OpenSource = (_, _, _) => throw new InvalidOperationException("Existing CL route must not open source tools."),
            CreateKnowledgeWriter = _ => throw new InvalidOperationException("Existing CL route must not accept or freeze CL content."),
            CreateBusinessFromClStore = (authority, begin, completion, settlement) =>
                new OctoberBusinessFromClStore(host.Database.Factory, authority, begin, completion, settlement, host.Database.Log),
            Business = business,
            CreateWorkerClient = async (provider, token) =>
            {
                Assert.Same(selected, provider);
                return await new OctoberWorkerClientFactory(new SyntheticSecret(), transport).CreateAsync(provider, token);
            }
        };
    }

    private static async Task<ImmutableArray<PublicationMemberSnapshot>> CapturePublicationMembersAsync(
        OctoberScanGenerateRunnerTests.Harness host, Guid publicationId)
    {
        await using var context = host.Database.Factory.CreateDbContext();
        return (await context.PublicationMembers.AsNoTracking().Where(item => item.PublicationId == publicationId)
            .OrderBy(item => item.Ordinal).Select(item => new PublicationMemberSnapshot(item.BatchId, item.PublicationId,
                item.CorpusId, item.LogicalDocumentId, item.VersionId, item.Ordinal)).ToArrayAsync()).ToImmutableArray();
    }

    private sealed record PublicationMemberSnapshot(Guid BatchId, Guid PublicationId, Guid CorpusId,
        Guid LogicalDocumentId, Guid VersionId, int Ordinal);

    private static OctoberBusinessFromClStore CreateBusinessFromClStore(
        OctoberScanGenerateRunnerTests.Harness host, IOctoberBusinessFromClAuthority authority,
        IOctoberBusinessFromClSettlementAuthority settlement, IOctoberCommitSubmissionGuard? beginGuard = null) =>
        new(host.Database.Factory, authority, beginGuard ?? host.Database.Guard, host.Database.Guard,
            settlement, host.Database.Log);

    private static BusinessFromClRequest BusinessFromClRequestFor(PublicationBinding publication, string subject)
    {
        var unsigned = new BusinessFromClRequest(publication, subject, Guid.NewGuid(), string.Empty);
        return unsigned with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(unsigned) };
    }

    private static async Task<int> CountBusinessFromClCompletionsAsync(OctoberScanGenerateRunnerTests.Harness host)
    {
        await using var context = host.Database.Factory.CreateDbContext();
        return await context.KnowledgeOperations.CountAsync(item => item.Kind == OperationKind.CompleteBusinessFromCl);
    }

    private static async Task<ExistingClCounts> CaptureExistingClCountsAsync(OctoberScanGenerateRunnerTests.Harness host)
    {
        await using var context = host.Database.Factory.CreateDbContext();
        return new(await context.DocumentVersions.CountAsync(), await context.KnowledgePublications.CountAsync(),
            await context.PublicationMembers.CountAsync());
    }

    private sealed record ExistingClCounts(int DocumentVersions, int Publications, int PublicationMembers);

    private sealed class SyntheticSecret : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(string purpose, string plaintext, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DecryptAsync(string purpose, string ciphertext, CancellationToken cancellationToken = default)
        {
            Assert.Equal("Vulgata.LlmProviders.ApiKey", purpose);
            Assert.Equal("unused-protected-test-secret", ciphertext);
            return Task.FromResult("synthetic-approved-token");
        }
    }
    private sealed class ControlledBusinessFromClAuthority : IOctoberBusinessFromClAuthority
    {
        internal int Calls { get; private set; }
        internal DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
        internal List<ImmutableArray<Contributor>> ContributorChecks { get; } = [];
        public Task<DisclosureStatus> CheckAsync(BusinessFromClRequest request,
            ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
        {
            Calls++;
            ContributorChecks.Add(contributors);
            return Task.FromResult(Status);
        }
    }
    private sealed class ControlledBusinessFromClSettlementAuthority : IOctoberBusinessFromClSettlementAuthority
    {
        internal BusinessFromClSettlement Status { get; set; } = BusinessFromClSettlement.Settled;
        internal int Calls { get; private set; }
        public Task<BusinessFromClSettlement> VerifyAsync(BusinessFromClCompletionRequest request,
            BusinessFromClAdmission admission, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Status);
        }
    }
    private sealed class ForgedBusinessFromClAdmission : BusinessFromClAdmission { }
    private sealed class HeldCommitAcknowledgementGuard(IOctoberCommitSubmissionGuard inner) : IOctoberCommitSubmissionGuard
    {
        internal int Submissions { get; private set; }
        internal TaskCompletionSource<CommitSettlement> CommitSettled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseAcknowledgement { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt,
            CancellationToken cancellationToken = default) => inner.CheckBeforeTransactionAsync(attempt, cancellationToken);

        public async Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt,
            Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
        {
            if (await inner.CheckBeforeTransactionAsync(attempt, cancellationToken) != CommitEligibilityStatus.Eligible)
                return new(CommitSubmissionStatus.StaleOrFenced, null);
            SubmittedCommit submitted = submitAsync(cancellationToken);
            Submissions++;
            CommitSettlement settlement = await submitted.Settlement.WaitAsync(cancellationToken);
            CommitSettled.TrySetResult(settlement);
            await ReleaseAcknowledgement.Task.WaitAsync(cancellationToken);
            return new(CommitSubmissionStatus.Submitted, settlement);
        }
    }
    private sealed class NaturalTransport : HttpMessageHandler
    {
        internal DocumentVersionReference? Root;
        internal int WorkerRequests, BusinessRequests, ResolutionRequests;
        internal int ExpectedWorkers = 1;
        internal int? ExpectedBusinessRequests;
        internal bool IncludeCallerObservation, ReturnUnresolved, InvalidSynthesisProposal;
        internal List<(string Endpoint, string? Authorization, string Model)> Requests { get; } = [];
        internal List<string> SynthesisPackets { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string model = body.RootElement.GetProperty("model").GetString()!;
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), model));
            bool streaming = body.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean();
            if (streaming)
            {
                WorkerRequests++;
                Assert.InRange(WorkerRequests, 1, ExpectedWorkers);
                const string worker = """{"units":[{"semanticSelector":"file","parentSelector":null,"title":"Conditional execution","anchors":[{"startLine":1,"endLine":2}],"sections":[{"sectionId":"logic","ordinal":0,"title":"Behavior","claims":[{"claimGroupId":"rule","text":"Run executes when enabled.","qualification":"SourceSupported","evidence":[{"startLine":1,"endLine":1}],"limitations":[]}]}]}],"observations":[],"uncertainties":[]}""";
                string payload = IncludeCallerObservation ? worker.Replace("\"observations\":[]", "\"observations\":[{\"observationKey\":\"local-call\",\"ownerSelector\":\"file\",\"role\":\"caller\",\"mechanism\":\"call\",\"operation\":\"Run\",\"logicalNamespace\":null,\"literalsOrExpressions\":[\"Run()\"],\"qualification\":\"SourceSupported\",\"evidence\":[{\"startLine\":1,\"endLine\":1}]}]") : worker;
                string chunk = JsonSerializer.Serialize(new { id = "controlled-worker", @object = "chat.completion.chunk", created = 1, model,
                    choices = new[] { new { index = 0, delta = new { role = "assistant", content = payload }, finish_reason = "stop" } }, usage = new { prompt_tokens = 100, completion_tokens = 40, total_tokens = 140 } });
                return new(HttpStatusCode.OK) { Content = new StringContent("data: " + chunk + "\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream") };
            }
            BusinessRequests++;
            if (IncludeCallerObservation && Root is null)
            {
                ResolutionRequests++; Assert.Equal(1, ResolutionRequests);
                string resolution = "null";
                if (ReturnUnresolved)
                {
                    var message = body.RootElement.GetProperty("messages").EnumerateArray().Single(value => value.GetProperty("role").GetString() == "user");
                    using var prompt = JsonDocument.Parse(message.GetProperty("content").GetString()!);
                    var caller = prompt.RootElement.GetProperty("inquiry").GetProperty("Caller").Deserialize<ResolutionObservationRef>()!;
                    resolution = JsonSerializer.Serialize(new ResolutionWitness(caller, caller, "Run action", "No accepted provider", "Run", "call", "Unknown namespace", "No proven binding", null, [], [], "No candidate provider evidence", [], false, false));
                }
                string failed = JsonSerializer.Serialize(new { id = "controlled-resolution", @object = "chat.completion", created = 1, model, choices = new[] { new { index = 0, message = new { role = "assistant", content = resolution }, finish_reason = "stop" } }, usage = new { prompt_tokens = 30, completion_tokens = 10, total_tokens = 40 } });
                return new(HttpStatusCode.OK) { Content = new StringContent(failed, Encoding.UTF8, "application/json") };
            }
            Assert.Equal(ExpectedBusinessRequests ?? (IncludeCallerObservation ? 2 : 1), BusinessRequests);
            var root = Assert.IsType<DocumentVersionReference>(Root);
            SynthesisPackets.Add(body.RootElement.GetProperty("messages").EnumerateArray()
                .Single(value => value.GetProperty("role").GetString() == "user").GetProperty("content").GetString()!);
            var proposal = new BusinessSynthesisProposal("条件处理", [new("business", 0, "处理规则", [new("rule", "满足条件时执行处理。", ClaimQualification.SourceSupported, [new(root, "logic", "rule")], [])])]);
            string json = JsonSerializer.Serialize(new { id = "controlled-business", @object = "chat.completion", created = 1, model,
                choices = new[] { new { index = 0, message = new { role = "assistant", content = JsonSerializer.Serialize(InvalidSynthesisProposal ? new BusinessSynthesisProposal("", []) : proposal) }, finish_reason = "stop" } }, usage = new { prompt_tokens = 80, completion_tokens = 25, total_tokens = 105 } });
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
