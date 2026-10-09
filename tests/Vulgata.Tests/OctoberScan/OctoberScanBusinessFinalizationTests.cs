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
        using var transport = new NaturalTransport { IncludeCallerObservation = boundary is "nonempty-b1" or "nonempty-b1-direct" or "unresolved-b1" or "budget-b1-direct" or "budget-b1-ack", ReturnUnresolved = boundary == "unresolved-b1" };
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
        }        else if (boundary is "publication-ack" or "publication-first-lookup-unknown")
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
        internal int ExpectedWorkers = 1;
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
            Assert.Equal(IncludeCallerObservation ? 2 : 1, BusinessRequests);
            var root = Assert.IsType<DocumentVersionReference>(Root);
            var proposal = new BusinessSynthesisProposal("条件处理", [new("business", 0, "处理规则", [new("rule", "满足条件时执行处理。", ClaimQualification.SourceSupported, [new(root, "logic", "rule")], [])])]);
            string json = JsonSerializer.Serialize(new { id = "controlled-business", @object = "chat.completion", created = 1, model,
                choices = new[] { new { index = 0, message = new { role = "assistant", content = JsonSerializer.Serialize(proposal) }, finish_reason = "stop" } }, usage = new { prompt_tokens = 80, completion_tokens = 25, total_tokens = 105 } });
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
