using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Actual executor, Scan scope and SQLite Knowledge handoff; provider, source and commit authority remain controlled.</summary>
public sealed class OctoberResolutionHandoffTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAcceptPublishAsync_OriginalWitnessCrossesHandoffWithoutReconstructionAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        string clBefore = await fixture.ClSnapshotAsync();
        var store = fixture.Store();
        var loaded = await store.LoadCorpusAsync(fixture.Corpus.Handle);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var corpus = Assert.IsType<ResolutionCorpus>(loaded.Corpus);
        var inquiry = Assert.Single(OctoberResolutionPolicy.Plan(corpus).Inquiries);
        var request = fixture.Request();
        var original = request.Witness! with
        {
            ConnectionExplanation = "Original provider interpretation: the exact accepted configuration connects these two versions.",
            Limitations = ["Original provider qualifier: this static connection does not establish deployed availability."]
        };
        Guid run = Guid.NewGuid();
        var finalization = new OperationKey(request.Operation.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        await using var scope = Scope(fixture, run, finalization);
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(JsonSerializer.Serialize(original))));
        var executor = new OctoberResolutionExecutor(client, "Controlled inquiry: retain accepted facts, exact evidence and qualifications.");

        var execution = await executor.ExecuteAsync(corpus, inquiry, finalization, run, "controlled-profile", 40,
            scope, new ResolutionDisclosure());

        Assert.Null(execution.AdmissionFailure);
        Assert.Equal(ResolutionOutcomeKind.Supported, execution.Outcome!.Kind);
        Assert.Equal(1, client.Calls);
        var handedOff = Assert.IsType<ResolutionWitness>(execution.Witness);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(original), OctoberKnowledgePolicy.SerializeCanonical(handedOff));
        Assert.All(handedOff.SupportingAssertions, assertion =>
        {
            Assert.Null(assertion.Evidence.Document);
            Assert.Null(assertion.Evidence.ClaimGroupId);
            Assert.NotNull(assertion.Evidence.Source);
            var document = corpus.Documents.Single(value => value.Version == assertion.Document);
            var claim = document.Sections.Single(value => value.SectionId == assertion.SectionId)
                .Claims.Single(value => value.ClaimGroupId == assertion.ClaimGroupId);
            Assert.Equal(claim.Text, assertion.ExactAcceptedText);
            Assert.Contains(assertion.Evidence, claim.Evidence);
        });

        // Pass the returned original witness directly; the accepted outcome is recomputed by Knowledge.
        request = ResolutionPersistenceFixture.Digest(request with { Witness = handedOff });
        var accepted = await store.AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var outcome = Assert.IsType<ResolutionOutcome>(accepted.Outcome);
        Assert.Equal(ResolutionOutcomeKind.Supported, outcome.Kind);
        Assert.Equal(inquiry.Caller.Document, outcome.Relationship!.From);
        Assert.Equal(handedOff.Provider!.Document, outcome.Relationship.To);
        Assert.Contains(original.Limitations[0], outcome.Uncertainty!.Limitations);
        Assert.Contains(ResolutionPersistenceFixture.SourceLimit, outcome.Uncertainty.Limitations);
        await using (var context = fixture.Acceptance.Factory.CreateDbContext())
        {
            var stored = await context.ResolutionItems.AsNoTracking().SingleAsync();
            var retained = JsonSerializer.Deserialize<ResolutionWitness>(stored.WitnessJson!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(original), OctoberKnowledgePolicy.SerializeCanonical(retained));
        }
        var publication = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([outcome]));
        Assert.Equal(KnowledgeWriteStatus.Committed, publication.Status);
        Assert.NotEqual(fixture.B0Publication.PublicationId, publication.Publication!.PublicationId);
        var related = await fixture.RelatedAsync(publication.Publication, inquiry.Caller.Document);
        Assert.Equal(KnowledgeReadStatus.Success, related.Status);
        Assert.Equal(outcome.Relationship.RelationshipVersionId, Assert.Single(related.Relationships).RelationshipVersionId);
        Assert.Contains(related.PermittedUncertainties, value => value.Key == outcome.Uncertainty.Key);
        Assert.Empty((await fixture.RelatedAsync(fixture.B0Publication, inquiry.Caller.Document)).Relationships);
        Assert.Equal(clBefore, await fixture.ClSnapshotAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("malformed")]
    [InlineData("final-denial")]
    [InlineData("hard-stop")]
    public async Task ExecuteAsync_UnsuccessfulResponse_DoesNotHandOffWitnessAsync(string failure)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var original = fixture.Request().Witness!;
        Assert.Equal(ResolutionOutcomeKind.Supported, OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry, original).Kind);
        Guid run = Guid.NewGuid();
        var finalization = new OperationKey(fixture.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        await using var scope = Scope(fixture, run, finalization);
        var disclosure = new ResolutionDisclosure();
        using var client = new ResolutionClient("controlled-profile", (_, _) =>
        {
            if (failure == "final-denial") disclosure.Status = DisclosureStatus.Denied;
            if (failure == "hard-stop") scope.HardStop();
            return Task.FromResult(Response(failure == "malformed" ? "not-json" : JsonSerializer.Serialize(original)));
        });

        var result = await new OctoberResolutionExecutor(client, "Controlled accepted-evidence inquiry.")
            .ExecuteAsync(fixture.Corpus, fixture.Inquiry, finalization, run, "controlled-profile", 40, scope, disclosure);

        Assert.Equal(1, client.Calls);
        Assert.Single(result.Attempts);
        Assert.Null(result.Outcome);
        Assert.Null(result.Witness);
        Assert.Equal(failure switch
        {
            "final-denial" => ProviderAdmissionStatus.Denied,
            "hard-stop" => ProviderAdmissionStatus.HardStopped,
            _ => ProviderAdmissionStatus.AuthorityUnavailable
        }, result.AdmissionFailure);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied, ProviderAdmissionStatus.Denied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable, ProviderAdmissionStatus.AuthorityUnavailable)]
    public async Task ExecuteAsync_AuthorityChangesDuringActualLeaseSettlement_ExposesNoOutcomeOrWitnessAsync(
        DisclosureStatus changedAuthority, ProviderAdmissionStatus expectedFailure)
    {
        var fixture = new ResolutionCase();
        fixture.Corpus = fixture.Corpus with { Documents = fixture.Corpus.Documents.Add(ResolutionCase.Document(5, "provider")) };
        var inquiry = fixture.Inquiry();
        var original = fixture.Witness();
        Assert.Equal(ResolutionOutcomeKind.Supported, OctoberResolutionPolicy.Validate(fixture.Corpus, inquiry, original).Kind);
        Guid run = Guid.NewGuid();
        var finalization = new OperationKey(fixture.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        await using var host = new OctoberExecutionScope(run, "controlled-profile", new OctoberScanPolicy
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
            SourceRules = [new(fixture.Corpus.Documents[0].RepositoryId, "src", "selected", "Controlled component qualification")]
        }, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), finalization);
        var disclosure = new ResolutionDisclosure();
        var scope = new ResolutionSettlementRevocationScope(host, disclosure, changedAuthority);
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(JsonSerializer.Serialize(original))));

        var result = await new OctoberResolutionExecutor(client, "Controlled accepted-evidence inquiry.")
            .ExecuteAsync(fixture.Corpus, inquiry, finalization, run, "controlled-profile", 40, scope, disclosure);

        Assert.Equal(1, scope.Acquisitions);
        Assert.Equal(1, scope.Settlements);
        Assert.Equal(1, scope.Disposals);
        Assert.False(scope.CancellationAtTransition);
        Assert.Equal(1, client.Calls);
        Assert.Equal(expectedFailure, result.AdmissionFailure);
        Assert.Null(result.Outcome);
        Assert.Null(result.Witness);
        var usage = Assert.Single(result.Attempts);
        Assert.Equal(10, usage.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
        Assert.True(disclosure.Contributors.Count >= 3, "Disclosure must be checked again after settlement and release complete.");
        var completeContributors = fixture.Corpus.Documents.SelectMany(document => document.Contributors).ToHashSet();
        Assert.All(disclosure.Contributors, supplied => Assert.True(completeContributors.SetEquals(supplied)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, ProviderAdmissionStatus.HardStopped)]
    [InlineData(true, ProviderAdmissionStatus.DeadlineExceeded)]
    public async Task ExecuteAsync_ActualRunEndsWhileFinalAuthorityWaits_RejectsLateAllowedWitnessAsync(
        bool runDeadline, ProviderAdmissionStatus expectedFailure)
    {
        var fixture = new ResolutionCase();
        fixture.Corpus = fixture.Corpus with { Documents = fixture.Corpus.Documents.Add(ResolutionCase.Document(5, "provider")) };
        Guid run = Guid.NewGuid();
        var finalization = new OperationKey(fixture.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        var clock = new SynthesisClock();
        await using var host = ActualScope(fixture, run, finalization, clock, runDeadline ? 180 : 1800);
        // The original run has 60 seconds left, less than this executor's 120-second allowance.
        if (runDeadline) clock.Advance(TimeSpan.FromSeconds(120));
        var fullLedger = fixture.Corpus.Documents.SelectMany(document => document.Contributors).Distinct().ToImmutableArray();
        var authority = new SynthesisHandoffDisclosure(fullLedger);
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(JsonSerializer.Serialize(fixture.Witness()))));
        var active = new OctoberResolutionExecutor(client, "Controlled accepted-evidence inquiry.", clock)
            .ExecuteAsync(fixture.Corpus, fixture.Inquiry(), finalization, run, "controlled-profile", 40, host, authority);
        OctoberWorkerProviderAttemptRequest Worker() => new(run, ResolutionCase.Id(98500), Guid.NewGuid(),
            "controlled-profile", TimeSpan.FromSeconds(10), 1);
        try
        {
            await authority.HandoffEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, client.Calls);
            Assert.False(active.IsCompleted);
            clock.Advance(TimeSpan.FromSeconds(1));
            // Actual cap-one capacity must have been returned before the final authority wait.
            var capacity = await host.AcquireWorkerAttemptAsync(Worker()).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ProviderAdmissionStatus.Granted, capacity.Status);
            Assert.NotNull(capacity.Lease);
            await capacity.Lease.DisposeAsync();
            if (runDeadline) clock.Advance(TimeSpan.FromSeconds(60));
            else host.HardStop();
            Assert.Equal(expectedFailure, (await host.AcquireWorkerAttemptAsync(Worker())).Status);
        }
        finally
        {
            authority.Release.TrySetResult();
            await active.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var result = await active;
        Assert.Equal(expectedFailure, result.AdmissionFailure);
        Assert.Null(result.Outcome);
        Assert.Null(result.Witness);
        Assert.Equal(1, client.Calls);
        Assert.Single(result.Attempts);
        Assert.Equal(3, authority.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_HostDoesNotExposeHandoffLifetime_RefusesBeforeProviderAdmissionAsync()
    {
        var fixture = new ResolutionCase();
        Guid run = Guid.NewGuid();
        var finalization = new OperationKey(fixture.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        await using var host = ActualScope(fixture, run, finalization);
        var legacy = new ResolutionHandoffLegacyScope(host);
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(JsonSerializer.Serialize(fixture.Witness()))));

        var result = await new OctoberResolutionExecutor(client, "Controlled accepted-evidence inquiry.")
            .ExecuteAsync(fixture.Corpus, fixture.Inquiry(), finalization, run, "controlled-profile", 40, legacy, new ResolutionDisclosure());

        Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, result.AdmissionFailure);
        Assert.Null(result.Outcome);
        Assert.Null(result.Witness);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, legacy.Acquisitions);
        Assert.Equal(0, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ActualHostUnknownUsage_RetainsCompletedWitnessAndBlocksFutureWorkerAndResolutionAsync()
    {
        var fixture = new ResolutionCase();
        Guid run = Guid.NewGuid();
        var finalization = new OperationKey(fixture.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        var clock = new SynthesisClock();
        await using var host = ActualScope(fixture, run, finalization, clock);
        var original = fixture.Witness();
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(original)))
        {
            FinishReason = ChatFinishReason.Stop, Usage = null
        }));
        var authority = new ResolutionDisclosure();
        var executor = new OctoberResolutionExecutor(client, "Controlled accepted-evidence inquiry.", clock);

        var completed = await executor.ExecuteAsync(fixture.Corpus, fixture.Inquiry(), finalization, run,
            "controlled-profile", 40, host, authority);

        Assert.Null(completed.AdmissionFailure);
        Assert.Equal(ResolutionOutcomeKind.Supported, completed.Outcome!.Kind);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(original),
            OctoberKnowledgePolicy.SerializeCanonical(Assert.IsType<ResolutionWitness>(completed.Witness)));
        var usage = Assert.Single(completed.Attempts);
        Assert.Null(usage.InputTokens);
        Assert.Null(usage.OutputTokens);
        Assert.Null(usage.Cost);
        Assert.Equal(ProviderAttemptOutcome.Completed, usage.Outcome);
        Assert.Equal(ProviderAdmissionStatus.Granted,
            host.CheckHandoffEligibility(run, finalization, "controlled-profile"));
        var worker = await host.AcquireWorkerAttemptAsync(new(run, ResolutionCase.Id(98501), Guid.NewGuid(),
            "controlled-profile", TimeSpan.FromSeconds(10), 1));
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, worker.Status);
        Assert.Null(worker.Lease);
        var next = await executor.ExecuteAsync(fixture.Corpus, fixture.Inquiry(), finalization, run,
            "controlled-profile", 40, host, authority);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, next.AdmissionFailure);
        Assert.Null(next.Outcome);
        Assert.Null(next.Witness);
        Assert.Empty(next.Attempts);
        Assert.Equal(1, client.Calls);
        Assert.Equal(3, authority.Contributors.Count);
        Assert.All(authority.Contributors, contributors => Assert.True(fixture.Corpus.Documents
            .SelectMany(document => document.Contributors).ToHashSet().SetEquals(contributors)));
    }

    private static OctoberExecutionScope ActualScope(ResolutionCase fixture, Guid run, OperationKey finalization,
        TimeProvider? clock = null, int runDeadlineSeconds = 1800) =>
        new(run, "controlled-profile", new OctoberScanPolicy
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
            MaximumRequestsPerMinute = 60, RunDeadlineSeconds = runDeadlineSeconds,
            FileDeadlineSeconds = Math.Min(120, runDeadlineSeconds),
            SourceRules = [new(fixture.Corpus.Documents[0].RepositoryId, "src", "selected", "Controlled component qualification")]
        }, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), finalization, clock);

    private static OctoberExecutionScope Scope(ResolutionPersistenceFixture fixture, Guid run, OperationKey finalization) =>
        new(run, "controlled-profile", new OctoberScanPolicy
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
            SourceRules = [new(fixture.Corpus.Documents[0].RepositoryId, "src", "selected", "Controlled component qualification")]
        }, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), finalization);

    private static ChatResponse Response(string text) => new(new ChatMessage(ChatRole.Assistant, text))
    {
        FinishReason = ChatFinishReason.Stop,
        Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 }
    };
}

/// <summary>Delegates admission and lease ownership to the actual Scan scope; only the live-authority transition is controlled.</summary>
internal sealed class ResolutionSettlementRevocationScope(IOctoberExecutionScope host, ResolutionDisclosure disclosure,
    DisclosureStatus changedAuthority) : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime
{
    public int Acquisitions { get; private set; }
    public int Settlements { get; private set; }
    public int Disposals { get; private set; }
    public bool CancellationAtTransition { get; private set; }
    public CancellationToken HandoffCancellation => host is IOctoberBusinessHandoffLifetime lifetime
        ? lifetime.HandoffCancellation : throw new InvalidOperationException("The actual host has no Business handoff lifetime.");
    public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey finalizationOperation, string providerProfileId) =>
        host is IOctoberBusinessHandoffLifetime lifetime
            ? lifetime.CheckHandoffEligibility(runId, finalizationOperation, providerProfileId)
            : ProviderAdmissionStatus.AuthorityUnavailable;
    public async Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        Acquisitions++;
        var admission = await host.AcquireProviderAttemptAsync(request, cancellationToken);
        return admission.Lease is { } lease ? admission with { Lease = new Lease(this, lease) } : admission;
    }
    private void ChangeAuthority(CancellationToken cancellationToken)
    {
        CancellationAtTransition = cancellationToken.IsCancellationRequested;
        disclosure.Status = changedAuthority;
    }
    private sealed class Lease(ResolutionSettlementRevocationScope owner, IOctoberProviderAttemptLease inner) : IOctoberProviderAttemptLease, IOctoberWorkerDispatchObserver
    {
        private IOctoberWorkerDispatchObserver Observer => inner as IOctoberWorkerDispatchObserver
            ?? throw new InvalidOperationException("Missing inner lease dispatch observer.");
        public void RecordDispatch() => Observer.RecordDispatch();
        public void CompleteDispatchObservation() => Observer.CompleteDispatchObservation();
        public Guid AttemptId => inner.AttemptId;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default) => inner.CheckBeforeSendAsync(cancellationToken);
        public async Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default)
        {
            owner.Settlements++;
            await inner.SettleAsync(usage, cancellationToken);
            owner.ChangeAuthority(inner.CancellationToken);
        }
        public async ValueTask DisposeAsync()
        {
            owner.Disposals++;
            await inner.DisposeAsync();
        }
    }
}

/// <summary>Deliberately presents only the older admission port, without inventing any handoff eligibility.</summary>
internal sealed class ResolutionHandoffLegacyScope(IOctoberExecutionScope host) : IOctoberExecutionScope
{
    public int Acquisitions { get; private set; }
    public Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        Acquisitions++;
        return host.AcquireProviderAttemptAsync(request, cancellationToken);
    }
}
