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
    public async Task InternalNaturalCompletion_UsesActualOwnerRootSelectionAndSameRealProviderClientAsync(bool mismatchedConfiguration)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        using var transport = new NaturalTransport();
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
            publication => Reader(host, publication, execution.Session.SubjectId),
            input =>
            {
                rootSelections++;
                // Explicit trusted selector over the actual owner-loaded identities, never inferred roots.
                var unit = Assert.Single(input.Units.Where(unit => unit.SemanticSelector == "file"));
                transport.Root = unit.Document;
                return [new FlowRootSelection(unit.UnitId, unit.Document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector)];
            }, projection, "Controlled resolution: use accepted evidence only.", "Controlled synthesis: preserve accepted evidence and qualifications.", new(), 32768);
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
        Assert.Equal(0, report.ExitCode);
        Assert.Equal("cl-and-bl", report.CompletedStage);
        Assert.Equal(1, clientCreations);
        Assert.Equal(1, rootSelections);
        Assert.Equal(1, transport.WorkerRequests);
        Assert.Equal(1, transport.BusinessRequests);
        Assert.Equal(2, report.Usage.Requests);
        Assert.False(report.Usage.HasUnknownUsage);
        Assert.Equal(1, host.BeginCalls);
        Assert.Equal(1, host.FreezeCalls);
        Assert.Equal(2, report.Documents.Length);
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
        var opened = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-reader", "separate-authentication"), KnowledgeMode.IT, KnowledgePurpose.Reader, [host.Database.Origin.RepositoryId]));
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
    [InlineData("scope-stop-before-commit")]
    [InlineData("freeze-log")]
    [InlineData("publication-log")]
    [InlineData("reader-nonprogress")]
    [InlineData("reader-empty-fresh-cursor")]
    [InlineData("scope-stop-before-freeze")]
    public async Task InternalNaturalCompletion_RespectsOwnedPhaseBoundariesAsync(string boundary)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        using var transport = new NaturalTransport { IncludeCallerObservation = boundary is "nonempty-b1" or "nonempty-b1-direct" or "unresolved-b1" or "budget-b1-direct" or "budget-b1-ack", ReturnUnresolved = boundary == "unresolved-b1" };
        var state = new BoundaryState { Boundary = boundary };
        if (boundary == "freeze-log") state.BeforeFreeze = () => host.Database.Log.Succeeds = false;
        var clock = boundary is "session-expired" or "replay-authority-held" ? new OctoberScanAcceptanceHarness.ManualClock() : null;
        var execution = host.Execution with { Policy = host.Execution.Policy with { OutputStage = "cl-and-bl" } };
        if (boundary == "session-expired") execution = execution with { Session = new FixedSession(execution.Session.SubjectId, execution.Session.ManagementAuthorityId, clock!.GetUtcNow().AddSeconds(5)) };
        var profile = execution.Provider.Profile;
        int clients = 0;
        var composition = new OctoberBusinessRunComposition(
            (authority, guard) => new ResolutionBoundary(state, new OctoberResolutionStore(host.Database.Factory, authority, guard, host.Database.Log)),
            authority => new OctoberFlowPlanningInputLoader(host.Database.Factory, authority),
            (authority, guard) => new PublicationBoundary(state, new OctoberFlowPublicationStore(host.Database.Factory, authority, guard, host.Database.Log)),
            publication => boundary is "reader-nonprogress" or "reader-empty-fresh-cursor" ? new NonprogressReader(state, Reader(host, publication, execution.Session.SubjectId)) : Reader(host, publication, execution.Session.SubjectId),
            input =>
            {
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
        var replayAuthority = new HeldReplayAuthority(host.Dependencies.Authority);
        await using var runner = new OctoberGenerateRunner(host.Dependencies with
        {
            Authority = replayAuthority,
            Business = composition,
            CreateWorkerClient = async (provider, token) => { clients++; return await new OctoberWorkerClientFactory(new SyntheticSecret(), transport).CreateAsync(provider, token); },
            CreateKnowledgeWriter = ports => new FreezeBoundary(state, host.Dependencies.CreateKnowledgeWriter(ports)),
        }, clock);
        if (boundary is "publication-log" or "publication-held-log") state.BeforePublication = () => host.Database.Log.Succeeds = false;
        if (boundary == "scope-stop-before-freeze") state.BeforeFreeze = () => ((OctoberExecutionScope)typeof(OctoberGenerateRunner).GetField("_scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!).HardStop();
        if (boundary == "scope-stop-before-commit") state.BeforePublication = () => ((OctoberExecutionScope)typeof(OctoberGenerateRunner).GetField("_scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!).HardStop();
        var pending = runner.ExecuteAsync(execution);
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
        Assert.Equal(1, clients);
        Assert.Equal(1, transport.WorkerRequests);
        if (boundary == "replay-authority-held")
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
        }        else if (boundary == "reader-nonprogress")
        {
            Assert.NotEqual(0, report.ExitCode); Assert.NotEqual(5, report.ExitCode);
            Assert.InRange(state.ReaderCalls, 1, 3);
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
        }        else if (boundary == "publication-ack")
        {
            Assert.Equal(0, report.ExitCode); Assert.Equal("cl-and-bl", report.CompletedStage);
            Assert.NotNull(report.Publication); Assert.Equal(1, state.Publications); Assert.True(state.PublicationLookups > 0);
            Assert.Equal(1, transport.BusinessRequests);
            var replay = await runner.ExecuteAsync(execution);
            Assert.Equal(report.Publication, replay.Report!.Publication); Assert.Equal(1, state.Publications); Assert.Equal(1, transport.BusinessRequests);
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
            Assert.Equal(OctoberPublicationStatus.Confirmed, report.PublicationStatus);
            Assert.NotNull(report.Publication);
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
        internal TaskCompletionSource PublicationLookupCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PublicationHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PublicationBoundExpired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleasePublication { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Action? BeforeFreeze, BeforePublication;
        internal int RootSelections, Publications, PublicationLookups, ResolutionAccepts, ResolutionLookups, ReaderCalls;
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
            return state.Boundary == "publication-ack" ? result with { Status = BusinessFinalizationStatus.CommitIndeterminate, Publication = null } : result;
        }
        public async Task<BusinessFinalizationResult> LookupAsync(CorpusHandle corpus, OperationKey operation, string digest, CancellationToken token = default) { state.PublicationLookups++; var result = await inner.LookupAsync(corpus, operation, digest, token); state.PublicationLookupCompleted.TrySetResult(); return result; }
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
    private sealed class NaturalTransport : HttpMessageHandler
    {
        internal DocumentVersionReference? Root;
        internal int WorkerRequests, BusinessRequests, ResolutionRequests;
        internal bool IncludeCallerObservation, ReturnUnresolved;
        internal List<(string Endpoint, string? Authorization, string Model)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            string model = body.RootElement.GetProperty("model").GetString()!;
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), model));
            bool streaming = body.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean();
            if (streaming)
            {
                WorkerRequests++;
                Assert.Equal(1, WorkerRequests);
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
            Assert.Equal(IncludeCallerObservation ? 2 : 1, BusinessRequests);
            var root = Assert.IsType<DocumentVersionReference>(Root);
            var proposal = new BusinessSynthesisProposal("条件处理", [new("business", 0, "处理规则", [new("rule", "满足条件时执行处理。", ClaimQualification.SourceSupported, [new(root, "logic", "rule")], [])])]);
            string json = JsonSerializer.Serialize(new { id = "controlled-business", @object = "chat.completion", created = 1, model,
                choices = new[] { new { index = 0, message = new { role = "assistant", content = JsonSerializer.Serialize(proposal) }, finish_reason = "stop" } }, usage = new { prompt_tokens = 80, completion_tokens = 25, total_tokens = 105 } });
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
