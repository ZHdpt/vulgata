using System.Collections.Immutable;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberResolutionPersistenceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedV2_ConnectedGroupPublication_RevalidatesSelectedPolicyAndPreservesOldPublicationAsync(bool mismatchedSelection)
    {
        await using var fixture = await RetainedResolutionPersistenceCase.CreateAsync();
        var source = new OctoberKnowledgePublishedSource(fixture.Acceptance.Factory);
        string oldPublication = OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(fixture.Publication));
        var legacy = await fixture.RequestAsync(null);
        var current = await fixture.RequestAsync(OctoberResolutionPolicy.RetainedPolicyVersion);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store(null).AcceptOutcomeAsync(legacy)).Status);
        var accepted = await fixture.Store(OctoberResolutionPolicy.RetainedPolicyVersion).AcceptOutcomeAsync(current);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var root = await fixture.RootAsync(OctoberResolutionPolicy.RetainedPolicyVersion);
        var loaded = await new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, fixture.Acceptance.Authorization,
            OctoberResolutionPolicy.RetainedPolicyVersion).LoadAsync(fixture.Corpus, [root]);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var input = loaded.Input!;
        var group = Assert.Single(OctoberFlowPlanner.Plan(input).Groups);
        Assert.Equal(2, group.Members.Length);
        var caller = input.Corpus.Documents.Single(document => document.Version == root.Document);
        var claim = caller.Sections[0].Claims[0];
        var proposal = new BusinessSynthesisProposal("静态流程引用说明", [new("main", 0, "流程声明",
            [new("declared", "入口声明引用另一个流程资源；实际运行时分派仍未证实。", ClaimQualification.SourceSupported,
                [new(root.Document, caller.Sections[0].SectionId, claim.ClaimGroupId)], [])])]);
        var operation = new OperationKey(fixture.Corpus.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        var selection = new BusinessFinalizationSelection([root], new(), Guid.NewGuid(), "controlled-profile",
            OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(FinalizationContractCase.Provider),
            OctoberKnowledgePolicy.Hash("Controlled retained evidence synthesis."), 40)
        { ResolutionPolicyVersion = mismatchedSelection ? null : OctoberResolutionPolicy.RetainedPolicyVersion };
        var digest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(new(operation, fixture.Corpus, "", new FinalizationForbiddenScope())
        { Selection = selection });
        var request = new FlowFinalizationPublicationRequest(operation, fixture.Corpus, digest, selection,
            [new(root, group.GroupVersionId, proposal)], [], "");
        request = request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };
        var result = await new OctoberFlowPublicationStore(fixture.Acceptance.Factory, fixture.Acceptance.Authorization,
            fixture.Acceptance.Guard, fixture.Acceptance.Log).PublishAsync(request);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        if (mismatchedSelection)
        {
            Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.PersistenceStatus);
            Assert.Null(result.Publication);
            Assert.Equal(0, await context.BusinessFinalizations.CountAsync());
        }
        else
        {
            Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
            var publication = (await source.LoadAsync(result.Publication!))!;
            var business = Assert.Single(publication.Documents.Where(document => document.Kind == DocumentKind.BusinessLogic));
            Assert.True(input.Contributors.ToHashSet().SetEquals(business.Contributors));
            Assert.Contains(publication.Relationships, value => value.Relationship.RelationshipVersionId == accepted.Outcome!.Relationship!.RelationshipVersionId);
            Assert.DoesNotContain(publication.Relationships, value => value.Relationship.From == legacy.Witness!.Caller.Document);
            var retained = await context.BusinessFinalizations.AsNoTracking().SingleAsync();
            Assert.Contains(OctoberResolutionPolicy.RetainedPolicyVersion, retained.RequestJson, StringComparison.Ordinal);
            var reader = new OctoberKnowledgeReader(source, result.Publication!, new B0bReaderDisclosure(),
                new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
            var opened = await reader.OpenReadSelectionAsync(new(result.Publication!, new("ordinary-user", "controlled-stamp"), KnowledgeMode.Business,
                KnowledgePurpose.Reader, input.Corpus.Documents.Select(document => document.RepositoryId).Distinct().ToImmutableArray()));
            Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
            var read = await reader.ReadExactAsync(new(opened.Selection!, new(business.Reference.BatchId, result.Publication!.PublicationId,
                business.Reference.LogicalDocumentId, business.Reference.DocumentVersionId)));
            Assert.Equal(KnowledgeReadStatus.Success, read.Status);
        }
        Assert.Equal(oldPublication, OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(fixture.Publication)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RetainedV2_MixedProfileHistory_LoadReplayAndContinuationRemainSeparatedAsync()
    {
        await using var fixture = await RetainedResolutionPersistenceCase.CreateAsync();
        var oldSource = new OctoberKnowledgePublishedSource(fixture.Acceptance.Factory);
        string before = OctoberKnowledgePolicy.SerializeCanonical(await oldSource.LoadAsync(fixture.Publication));
        var legacy = await fixture.RequestAsync(null);
        var current = await fixture.RequestAsync(OctoberResolutionPolicy.RetainedPolicyVersion);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store(null).AcceptOutcomeAsync(legacy)).Status);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store(OctoberResolutionPolicy.RetainedPolicyVersion).AcceptOutcomeAsync(current)).Status);
        foreach (var profile in new string?[] { null, OctoberResolutionPolicy.RetainedPolicyVersion })
        {
            var request = profile is null ? legacy : current;
            var store = fixture.Store(profile);
            var loaded = await store.LoadCorpusAsync(fixture.Corpus);
            Assert.Equal(profile, loaded.Corpus!.ResolutionPolicyVersion);
            var replay = await store.LookupOutcomeAsync(fixture.Corpus, request.Operation, request.CanonicalPayloadDigest);
            Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
            Assert.Equal(request.InquiryFingerprint, replay.Outcome!.Fingerprint);
            var root = await fixture.RootAsync(profile);
            var input = await new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, fixture.Acceptance.Authorization, profile)
                .LoadAsync(fixture.Corpus, [root]);
            Assert.Equal(KnowledgeReadStatus.Success, input.Status);
            Assert.Equal(profile, input.Input!.ResolutionPolicyVersion);
            Assert.Equal(request.InquiryFingerprint, Assert.Single(input.Input.Resolutions).Outcome.Fingerprint);
            Assert.Equal(2, Assert.Single(OctoberFlowPlanner.Plan(input.Input).Groups).Members.Length);
            var begin = new BusinessFromClRequest(fixture.Publication, "controlled-operator", Guid.NewGuid(), "") { ResolutionPolicyVersion = profile };
            begin = begin with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeBeginDigest(begin) };
            var continuation = fixture.Continuation();
            var admitted = await continuation.BeginAsync(begin);
            Assert.Equal(KnowledgeWriteStatus.Committed, admitted.Status);
            Assert.Equal(profile, admitted.Snapshot!.ResolutionPolicyVersion);
            Assert.Equal(request.InquiryFingerprint, Assert.Single(admitted.Snapshot.ExistingResolutions).Fingerprint);
            var completion = new BusinessFromClCompletionRequest(begin, "");
            completion = completion with { CanonicalPayloadDigest = OctoberBusinessFromClPolicy.ComputeCompletionDigest(completion) };
            Assert.Equal(KnowledgeWriteStatus.Committed, (await continuation.CompleteAsync(completion, admitted.Admission!)).Status);
        }
        var wrongLookup = await fixture.Store(OctoberResolutionPolicy.RetainedPolicyVersion)
            .LookupOutcomeAsync(fixture.Corpus, legacy.Operation, legacy.CanonicalPayloadDigest);
        Assert.Null(wrongLookup.Outcome);
        Assert.Equal(before, OctoberKnowledgePolicy.SerializeCanonical(await oldSource.LoadAsync(fixture.Publication)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RetainedV2_AcceptanceRejectsWrongProfileBeforeSubmission_AndBindsSelectedDigestAsync()
    {
        await using var fixture = await RetainedResolutionPersistenceCase.CreateAsync();
        var request = await fixture.RequestAsync(OctoberResolutionPolicy.RetainedPolicyVersion);
        var wrong = request with { ResolutionPolicyVersion = null };
        wrong = wrong with { CanonicalPayloadDigest = OctoberResolutionStore.ComputeAcceptanceDigest(wrong) };
        Assert.NotEqual(request.CanonicalPayloadDigest, wrong.CanonicalPayloadDigest);
        int submissions = fixture.Acceptance.Guard.Submissions;
        var rejected = await fixture.Store(OctoberResolutionPolicy.RetainedPolicyVersion).AcceptOutcomeAsync(wrong);
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, rejected.Status);
        Assert.Null(rejected.Outcome);
        Assert.Equal(submissions, fixture.Acceptance.Guard.Submissions);
        var begin = new BusinessFromClRequest(fixture.Publication, "controlled-operator", Guid.NewGuid(), "");
        string legacy = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        { domain = "october/business-from-cl/begin/1", begin.Publication, begin.SubjectId, begin.RunId }));
        Assert.Equal(legacy, OctoberBusinessFromClPolicy.ComputeBeginDigest(begin));
        Assert.NotEqual(legacy, OctoberBusinessFromClPolicy.ComputeBeginDigest(begin with { ResolutionPolicyVersion = OctoberResolutionPolicy.RetainedPolicyVersion }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RetainedV2_OtherProfileOversizedHistory_DoesNotConsumeSelectedPreflightBudgetAsync()
    {
        await using var fixture = await RetainedResolutionPersistenceCase.CreateAsync();
        var legacy = await fixture.RequestAsync(null);
        var current = await fixture.RequestAsync(OctoberResolutionPolicy.RetainedPolicyVersion);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store(null).AcceptOutcomeAsync(legacy)).Status);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store(OctoberResolutionPolicy.RetainedPolicyVersion).AcceptOutcomeAsync(current)).Status);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        var stored = await context.ResolutionItems.AsNoTracking().SingleAsync(value => value.OperationId == legacy.Operation.OperationId);
        // Owned SQLite corruption: this foreign-profile row must be excluded before measuring/materializing its payload.
        string oversized = new string(' ', 33 * 1024 * 1024) + stored.WitnessJson;
        Assert.Equal(1, await context.ResolutionItems.Where(value => value.OperationId == legacy.Operation.OperationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.WitnessJson, oversized)));
        var selected = await new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, fixture.Acceptance.Authorization, OctoberResolutionPolicy.RetainedPolicyVersion)
            .LoadAsync(fixture.Corpus, [await fixture.RootAsync(OctoberResolutionPolicy.RetainedPolicyVersion)]);
        Assert.Equal(KnowledgeReadStatus.Success, selected.Status);
        Assert.Equal(current.InquiryFingerprint, Assert.Single(selected.Input!.Resolutions).Outcome.Fingerprint);
        var blocked = await new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, fixture.Acceptance.Authorization)
            .LoadAsync(fixture.Corpus, [await fixture.RootAsync(null)]);
        Assert.NotEqual(KnowledgeReadStatus.Success, blocked.Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadCorpusAsync_RealAcceptedB0_ReconstructsExactRawEvidenceAndAllInputContributorsAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var loaded = await fixture.Store().LoadCorpusAsync(fixture.Corpus.Handle);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var corpus = Assert.IsType<ResolutionCorpus>(loaded.Corpus);
        Assert.Equal(fixture.Corpus.Handle, corpus.Handle);
        Assert.Equal(3, corpus.Documents.Length);
        Assert.Equal(fixture.Corpus.Documents.Select(x => x.Version).OrderBy(x => x.LogicalDocumentId),
            corpus.Documents.Select(x => x.Version).OrderBy(x => x.LogicalDocumentId));
        Assert.Equal(fixture.Inquiry.Fingerprint, Assert.Single(OctoberResolutionPolicy.Plan(corpus).Inquiries).Fingerprint);
        Assert.All(corpus.Documents.SelectMany(x => x.Sections).SelectMany(x => x.Claims).SelectMany(x => x.Evidence), evidence =>
        {
            Assert.Null(evidence.Document);
            Assert.Null(evidence.ClaimGroupId);
            Assert.NotNull(evidence.Source);
        });
        Assert.True(fixture.AllContributors.ToHashSet().SetEquals(fixture.Authorization.LastContributors));
        Assert.Contains(fixture.Authorization.LastContributors, x => x.RepositoryId == fixture.ThirdRepository);
        Assert.Contains(fixture.Acceptance.UncitedContributor, fixture.Authorization.LastContributors);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComputeAcceptanceDigest_ExactProjection_BindsOperationCorpusAttemptAndOriginalWitnessAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var request = fixture.Request();
        Assert.Equal(request.CanonicalPayloadDigest, OctoberResolutionStore.ComputeAcceptanceDigest(request));
        Assert.Equal(request.CanonicalPayloadDigest, OctoberResolutionStore.ComputeAcceptanceDigest(request with { CanonicalPayloadDigest = "self-field-is-excluded" }));
        Assert.NotEqual(request.CanonicalPayloadDigest, OctoberResolutionStore.ComputeAcceptanceDigest(request with { AttemptId = Guid.NewGuid() }));
        Assert.NotEqual(request.CanonicalPayloadDigest, OctoberResolutionStore.ComputeAcceptanceDigest(request with { Corpus = request.Corpus with { CorpusDigest = new string('f', 64) } }));
        Assert.NotEqual(request.CanonicalPayloadDigest, OctoberResolutionStore.ComputeAcceptanceDigest(request with { Witness = request.Witness! with { ConnectionExplanation = "Changed proposed interpretation" } }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptOutcomeAsync_SupportedWitness_PersistsExactEdgeAndQualificationWithoutRewritingClAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        string before = await fixture.ClSnapshotAsync();
        var request = fixture.Request();
        var accepted = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var outcome = Assert.IsType<ResolutionOutcome>(accepted.Outcome);
        Assert.Equal(ResolutionOutcomeKind.Supported, outcome.Kind);
        var edge = Assert.IsType<AcceptedRelationship>(outcome.Relationship);
        Assert.Equal(fixture.Inquiry.Caller.Document, edge.From);
        Assert.Equal(fixture.Provider.Version, edge.To);
        Assert.Equal(RelationshipKind.References, edge.Kind);
        Assert.Contains(ResolutionPersistenceFixture.SourceLimit, Assert.IsType<Uncertainty>(outcome.Uncertainty).Limitations);
        Assert.Contains("Deployment remains unverified.", outcome.Uncertainty!.Limitations);
        Assert.True(fixture.AllContributors.ToHashSet().SetEquals(outcome.Contributors));
        Assert.Equal(before, await fixture.ClSnapshotAsync());
        Assert.Empty((await fixture.RelatedAsync(fixture.B0Publication, edge.From)).Relationships);
        var lookup = await fixture.Store().LookupOutcomeAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, lookup.Status);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(outcome), OctoberKnowledgePolicy.SerializeCanonical(lookup.Outcome));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptOutcomeAsync_IdenticalReplayChangedDigestAndDuplicateFingerprint_PreservesOneOutcomeAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var request = fixture.Request();
        var committed = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, committed.Status);
        var replay = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(committed.Outcome!.Relationship!.RelationshipVersionId, replay.Outcome!.Relationship!.RelationshipVersionId);
        var changed = ResolutionPersistenceFixture.Digest(request with { Witness = request.Witness! with { ConnectionExplanation = "Another connecting interpretation." } });
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await fixture.Store().AcceptOutcomeAsync(changed)).Status);
        var duplicate = ResolutionPersistenceFixture.Digest(request with { Operation = request.Operation with { OperationId = Guid.NewGuid() }, AttemptId = Guid.NewGuid() });
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await fixture.Store().AcceptOutcomeAsync(duplicate)).Status);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.AcceptResolution));
        Assert.Equal(3, await context.DocumentVersions.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LookupOutcomeAsync_DeniedUncitedInputContributor_FailsClosedBeforeReplayOrConflictDisclosureAsync(bool authorityUnavailable)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var request = fixture.Request();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(request)).Status);
        fixture.Authorization.DeniedRepository = fixture.ThirdRepository;
        fixture.Authorization.Unavailable = authorityUnavailable;
        var expected = authorityUnavailable ? KnowledgeWriteStatus.AuthorityUnavailable : KnowledgeWriteStatus.Denied;
        var lookup = await fixture.Store().LookupOutcomeAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(expected, lookup.Status);
        Assert.Null(lookup.Outcome);
        var replay = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(expected, replay.Status);
        Assert.Null(replay.Outcome);
        var conflict = await fixture.Store().LookupOutcomeAsync(request.Corpus, request.Operation, new string('f', 64));
        Assert.Equal(expected, conflict.Status);
        Assert.Null(conflict.Outcome);
        Assert.Contains(fixture.Authorization.LastContributors, x => x.RepositoryId == fixture.ThirdRepository);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptOutcomeAsync_FenceAfterStaging_RollsBackThenAllowsExactEligibleRetryAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var request = fixture.Request();
        int submitted = fixture.Acceptance.Guard.Submissions;
        fixture.Acceptance.Guard.BeforeSubmission = () => fixture.Acceptance.Guard.Fenced = true;
        var fenced = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, fenced.Status);
        Assert.Null(fenced.Outcome);
        Assert.Equal(submitted, fixture.Acceptance.Guard.Submissions);
        await using (var context = fixture.Acceptance.Factory.CreateDbContext())
            Assert.Equal(0, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.AcceptResolution));
        fixture.Acceptance.Guard.BeforeSubmission = null;
        fixture.Acceptance.Guard.Fenced = false;
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Store().AcceptOutcomeAsync(request)).Status);
        Assert.Equal(submitted + 1, fixture.Acceptance.Guard.Submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptOutcomeAsync_UncheckableWitness_PersistsUnresolvedDispositionWithoutInventedEdgeAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var request = fixture.Request();
        request = ResolutionPersistenceFixture.Digest(request with { Witness = request.Witness! with
        {
            SupportingAssertions = request.Witness.SupportingAssertions.Where(x => x.Kind != ResolutionAssertionKind.ConnectingBinding).ToImmutableArray(),
            Limitations = ["The caller target has no accepted binding evidence."]
        } });
        var result = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(ResolutionOutcomeKind.Unresolved, result.Outcome!.Kind);
        Assert.Null(result.Outcome.Relationship);
        Assert.NotEmpty(result.Outcome.Uncertainty!.Limitations);
        Assert.True(fixture.AllContributors.ToHashSet().SetEquals(result.Outcome.Contributors));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(ResolutionOutcomeKind.Failed)]
    [InlineData(ResolutionOutcomeKind.NotEvaluated)]
    public async Task AcceptOutcomeAsync_ExplicitHostDisposition_RetainsHonestGapAndNoRelationshipAsync(ResolutionOutcomeKind disposition)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var request = ResolutionPersistenceFixture.Digest(fixture.Request() with { Witness = null, ExecutionDisposition = disposition });
        var result = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(disposition, result.Outcome!.Kind);
        Assert.Null(result.Outcome.Relationship);
        Assert.NotEmpty(Assert.IsType<Uncertainty>(result.Outcome.Uncertainty).Limitations);
        Assert.True(fixture.AllContributors.ToHashSet().SetEquals(result.Outcome.Contributors));
        var published = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([result.Outcome]));
        Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
        var related = await fixture.RelatedAsync(published.Publication!, fixture.Inquiry.Caller.Document);
        Assert.Empty(related.Relationships);
        Assert.Contains(related.PermittedUncertainties, x => x.Key == result.Outcome.Uncertainty!.Key);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-disposition")]
    [InlineData("host-claims-supported")]
    [InlineData("invented-inquiry")]
    public async Task AcceptOutcomeAsync_MissingOrFabricatedDisposition_RejectsWithoutOperationAsync(string mutation)
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var request = fixture.Request() with { Witness = null };
        request = mutation switch
        {
            "host-claims-supported" => request with { ExecutionDisposition = ResolutionOutcomeKind.Supported },
            "invented-inquiry" => request with { ExecutionDisposition = ResolutionOutcomeKind.Failed, InquiryFingerprint = new string('f', 64) },
            _ => request
        };
        var result = await fixture.Store().AcceptOutcomeAsync(ResolutionPersistenceFixture.Digest(request));
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.Status);
        Assert.Null(result.Outcome);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.AcceptResolution));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishSelectionAsync_AcceptedResolution_CreatesExplicitB1AndLeavesB0AndClUnchangedAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        string clBefore = await fixture.ClSnapshotAsync();
        var accepted = await fixture.Store().AcceptOutcomeAsync(fixture.Request());
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var outcome = accepted.Outcome!;
        var request = fixture.Publication([outcome]);
        Assert.Empty(request.Flows);
        var published = await fixture.Acceptance.Writer().PublishSelectionAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
        Assert.NotEqual(fixture.B0Publication.PublicationId, published.Publication!.PublicationId);
        var b1 = await fixture.RelatedAsync(published.Publication, outcome.Relationship!.From);
        Assert.Equal(KnowledgeReadStatus.Success, b1.Status);
        var edge = Assert.Single(b1.Relationships);
        Assert.Equal(outcome.Relationship.RelationshipVersionId, edge.RelationshipVersionId);
        Assert.Equal(published.Publication.PublicationId, edge.From.PublicationId);
        Assert.Equal(outcome.Relationship.To.DocumentVersionId, edge.To.DocumentVersionId);
        Assert.Contains(b1.PermittedUncertainties, x => x.Key == outcome.Uncertainty!.Key);
        var b0 = await fixture.RelatedAsync(fixture.B0Publication, outcome.Relationship.From);
        Assert.Empty(b0.Relationships);
        Assert.Empty(b0.PermittedUncertainties);
        var narrowed = await fixture.RelatedAsync(published.Publication, outcome.Relationship.From,
            fixture.Corpus.Documents.Where(x => x.RepositoryId != fixture.ThirdRepository).Select(x => x.RepositoryId).ToImmutableArray());
        Assert.Empty(narrowed.Relationships);
        Assert.Empty(narrowed.PermittedUncertainties);
        Assert.Equal(clBefore, await fixture.ClSnapshotAsync());
        var replay = await fixture.Acceptance.Writer().PublishSelectionAsync(request);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(published.Publication, replay.Publication);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(3, await context.DocumentVersions.CountAsync());
        Assert.All(await context.LogicalDocuments.ToListAsync(), x => Assert.Equal(DocumentKind.CodeLogic, x.Kind));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetRelatedAsync_UnrelatedInquiryInput_IsNotAnUncertaintyOwnerButRemainsAnAuthorizationContributorAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync();
        var request = fixture.Request();
        var accepted = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var outcome = Assert.IsType<ResolutionOutcome>(accepted.Outcome);
        Assert.Contains(outcome.Contributors, contributor => contributor.RepositoryId == fixture.ThirdRepository);
        var published = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([outcome]));
        Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
        var publication = published.Publication!;
        var caller = outcome.Relationship!.From;
        string uncertaintyKey = outcome.Uncertainty!.Key;

        // Narrowing scope still denies the relationship/qualification because the omitted input
        // contributed to the inquiry, even though it is neither the caller nor the linked provider.
        var narrowed = await fixture.RelatedAsync(publication, caller,
            fixture.Corpus.Documents.Where(x => x.RepositoryId != fixture.ThirdRepository).Select(x => x.RepositoryId).ToImmutableArray());
        Assert.Empty(narrowed.Relationships);
        Assert.Empty(narrowed.PermittedUncertainties);
        fixture.Authorization.DeniedRepository = fixture.ThirdRepository;
        var denied = await fixture.Store().LookupOutcomeAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(KnowledgeWriteStatus.Denied, denied.Status);
        Assert.Null(denied.Outcome);
        fixture.Authorization.DeniedRepository = null;

        var ownerPage = await fixture.RelatedAsync(publication, caller);
        Assert.Contains(ownerPage.PermittedUncertainties, value => value.Key == uncertaintyKey);
        var unrelated = fixture.Corpus.Documents.Single(x => x.RepositoryId == fixture.ThirdRepository).Version;
        var unrelatedPage = await fixture.RelatedAsync(publication, unrelated);
        Assert.Equal(KnowledgeReadStatus.Success, unrelatedPage.Status);
        Assert.Empty(unrelatedPage.Relationships);
        Assert.DoesNotContain(unrelatedPage.PermittedUncertainties, value => value.Key == uncertaintyKey);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishSelectionAsync_AnotherChosenInquiryIsUndisposed_RejectsB1UntilExplicitDispositionAsync()
    {
        await using var fixture = await ResolutionPersistenceFixture.CreateAsync(secondCaller: true);
        var chosen = OctoberResolutionPolicy.Plan(fixture.Corpus).Inquiries;
        Assert.Equal(2, chosen.Length);
        var first = await fixture.Store().AcceptOutcomeAsync(fixture.Request());
        Assert.Equal(KnowledgeWriteStatus.Committed, first.Status);
        var incomplete = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([first.Outcome!]));
        Assert.Contains(incomplete.Status, new[] { KnowledgeWriteStatus.InvalidProposal, KnowledgeWriteStatus.NotCompleted });
        Assert.Null(incomplete.Publication);
        var other = chosen.Single(x => x.Fingerprint != fixture.Inquiry.Fingerprint);
        var request = ResolutionPersistenceFixture.Digest(fixture.Request() with
        {
            Operation = fixture.Request().Operation with { OperationId = Guid.NewGuid() }, AttemptId = Guid.NewGuid(),
            InquiryFingerprint = other.Fingerprint, Witness = null, ExecutionDisposition = ResolutionOutcomeKind.NotEvaluated
        });
        var disposed = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, disposed.Status);
        var complete = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([first.Outcome!, disposed.Outcome!]));
        Assert.Equal(KnowledgeWriteStatus.Committed, complete.Status);
        Assert.Empty((await fixture.RelatedAsync(fixture.B0Publication, fixture.Inquiry.Caller.Document)).Relationships);
    }
}

/// <summary>Real B0 acceptance/freeze/publication on owned SQLite; only source/Scan authority are controlled.</summary>
internal sealed class ResolutionPersistenceFixture : IAsyncDisposable
{
    public const string SourceLimit = "Static configuration may be replaced by runtime overrides.";
    private ResolutionPersistenceFixture(OctoberKnowledgeAcceptanceFixture acceptance) => Acceptance = acceptance;
    public OctoberKnowledgeAcceptanceFixture Acceptance { get; }
    public ResolutionPersistenceAuthorization Authorization { get; } = new();
    public ResolutionCorpus Corpus { get; private set; } = null!;
    public PublicationBinding B0Publication { get; private set; } = null!;
    public ImmutableArray<AcceptedFile> Files { get; private set; }
    public ResolutionDocument Provider => Corpus.Documents[1];
    public Guid ThirdRepository => Corpus.Documents[2].RepositoryId;
    public ImmutableArray<Contributor> AllContributors => Corpus.Documents.SelectMany(x => x.Contributors).Distinct().ToImmutableArray();
    public ResolutionInquiry Inquiry => OctoberResolutionPolicy.Plan(Corpus).Inquiries.Single(x => x.Caller.Document == Corpus.Documents[0].Version);
    public OctoberResolutionStore Store() => new(Acceptance.Factory, Authorization, Acceptance.Guard, Acceptance.Log);

    public static async Task<ResolutionPersistenceFixture> CreateAsync(bool secondCaller = false)
    {
        var acceptance = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var fixture = new ResolutionPersistenceFixture(acceptance);
        try { await fixture.InitializeAsync(secondCaller); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public static async Task<ResolutionPersistenceFixture> CreateWithOptionsAsync(DbContextOptions<OctoberDbContext> options, bool secondCaller = false)
    {
        // The calling suite owns provisioning. This helper neither opens ambient services nor migrates them.
        var acceptance = await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
        var fixture = new ResolutionPersistenceFixture(acceptance);
        try { await fixture.InitializeAsync(secondCaller); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task InitializeAsync(bool secondCaller)
    {
        var origins = new[]
        {
            Acceptance.Origin,
            new PreparedOrigin(OctoberKnowledgeAcceptanceFixture.Id(9302), "controlled-provider-root", "src/Provider.cs", new string('d', 64), "controlled-v1"),
            new PreparedOrigin(OctoberKnowledgeAcceptanceFixture.Id(9303), "controlled-third-root", "src/Other.cs", new string('e', 64), "controlled-v1")
        };
        await using (var setup = Acceptance.Factory.CreateDbContext())
        {
            foreach (var origin in origins.Skip(1)) setup.RepositorySelections.Add(new()
            {
                BatchId = Acceptance.Binding.BatchId, RepositoryId = origin.RepositoryId,
                RootBindingIdentity = origin.RootBindingIdentity, OperatorLabel = "controlled-selected-repository"
            });
            await setup.SaveChangesAsync();
        }
        var accepted = ImmutableArray.CreateBuilder<AcceptedFile>();
        var documents = ImmutableArray.CreateBuilder<ResolutionDocument>();
        for (int index = 0; index < origins.Length; index++)
        {
            var origin = origins[index];
            string role = index == 0 || index == 2 && secondCaller ? "caller" : "provider";
            var actionEvidence = new EvidenceReference(null, null, new(origin, 10, 15), null, origin.SourceDigest);
            var bindingEvidence = new EvidenceReference(null, null, new(origin, 20, 25), null, origin.SourceDigest);
            var action = new QualifiedClaimGroup("action", role == "caller" ? "Caller sends SubmitInvoice to tenant-a invoice-route." : "Provider invoice-handler receives SubmitInvoice in tenant-a.",
                ClaimQualification.SourceSupported, [actionEvidence], []);
            var binding = new QualifiedClaimGroup("binding", "The accepted configuration connects tenant-a invoice-route to provider invoice-handler for SubmitInvoice.",
                ClaimQualification.SourceSupported, [bindingEvidence], [SourceLimit]);
            ImmutableArray<DocumentSection> sections = [new("operation", 0, "Operation", [action, binding])];
            var unit = OctoberKnowledgeAcceptanceFixture.Unit("operation", null, "Controlled " + role + " " + index, [new(origin, 1, 30)], sections);
            ImmutableArray<CommunicationObservation> observations = [new("observation-" + index, "operation", role, "message", "SubmitInvoice", "tenant-a",
                ["invoice-route", "audit-payload"], ClaimQualification.SourceSupported, [actionEvidence, bindingEvidence])];
            ImmutableArray<Contributor> contributors = [new(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest), Acceptance.UncitedContributor];
            if (index == 0) contributors = contributors.Add(Acceptance.AdvisoryContributor);
            Guid admission = OctoberKnowledgeAcceptanceFixture.Id(9400 + index);
            var proposal = new CompletedFileProposal(Acceptance.Binding, admission, OctoberKnowledgeAcceptanceFixture.Id(9500 + index), origin, "",
                [unit], observations, [], contributors, new(admission, origin, CoverageDisposition.Accepted, null));
            Assert.True(OctoberKnowledgePolicy.Validate(proposal, new(origin, 100)));
            Acceptance.Source.Result = new(origin, 100);
            var result = await Acceptance.Writer().AcceptCompletedFileAsync(Acceptance.Seal(proposal, OctoberKnowledgeAcceptanceFixture.Id(9600 + index)));
            Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
            var file = Assert.IsType<AcceptedFile>(result.AcceptedFile);
            accepted.Add(file);
            documents.Add(new(Assert.Single(file.Documents), origin.RepositoryId, sections, observations, contributors));
        }
        Files = accepted.ToImmutable();
        var freeze = new CorpusFreezeRequest(new(Acceptance.Binding.BatchId, OperationKind.FreezeCorpus, OctoberKnowledgeAcceptanceFixture.Id(9700)),
            Acceptance.Binding, "", Files, Files.Select(x => x.Coverage).ToImmutableArray());
        freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        Acceptance.FreezeAuthority.Approve(freeze);
        var frozen = await Acceptance.Writer().FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        Corpus = new(Assert.IsType<CorpusHandle>(frozen.Corpus), documents.ToImmutable());
        var publication = await Acceptance.Writer().PublishSelectionAsync(Publication([], OctoberKnowledgeAcceptanceFixture.Id(9701)));
        Assert.Equal(KnowledgeWriteStatus.Committed, publication.Status);
        B0Publication = Assert.IsType<PublicationBinding>(publication.Publication);
    }

    public ResolutionAcceptanceRequest Request()
    {
        var caller = Corpus.Documents[0];
        var action = caller.Sections[0].Claims[0];
        var providerAction = Provider.Sections[0].Claims[0];
        var binding = caller.Sections[0].Claims[1];
        var witness = new ResolutionWitness(Inquiry.Caller, new(Provider.Version, Provider.Observations[0].ObservationKey),
            "SubmitInvoice", "provider", "SubmitInvoice", "message", "tenant-a", "Accepted configuration connects invoice-route to invoice-handler.", null,
            [new(ResolutionAssertionKind.CallerAction, caller.Version, "operation", action.ClaimGroupId, action.Evidence[0], action.Text, "Caller sends this operation."),
             new(ResolutionAssertionKind.ProviderRoleAndOperation, Provider.Version, "operation", providerAction.ClaimGroupId, providerAction.Evidence[0], providerAction.Text, "Provider receives this operation."),
             new(ResolutionAssertionKind.ConnectingBinding, caller.Version, "operation", binding.ClaimGroupId, binding.Evidence[0], binding.Text, "The selected configuration connects the named route to the provider.")],
            [], "Source-backed static configuration connects these exact caller and provider versions.", ["Deployment remains unverified."], false, false);
        return Digest(new(new(Acceptance.Binding.BatchId, OperationKind.AcceptResolution, OctoberKnowledgeAcceptanceFixture.Id(9800)), Corpus.Handle,
            OctoberKnowledgeAcceptanceFixture.Id(9801), "", Inquiry.Fingerprint, witness));
    }

    public static ResolutionAcceptanceRequest Digest(ResolutionAcceptanceRequest request) => request with
    {
        CanonicalPayloadDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/resolution-accept/1", schemaVersion = 1, request.Operation, request.Corpus, request.AttemptId,
            request.InquiryFingerprint, request.Witness, request.ExecutionDisposition
        }))
    };

    public PublicationRequest Publication(ImmutableArray<ResolutionOutcome> outcomes, Guid? operationId = null)
    {
        var request = new PublicationRequest(new(Acceptance.Binding.BatchId, OperationKind.PublishSelection, operationId ?? OctoberKnowledgeAcceptanceFixture.Id(9900)),
            Corpus.Handle, "", Corpus.Documents.Select(x => x.Version).ToImmutableArray(),
            outcomes.Where(x => x.Relationship is not null).Select(x => x.Relationship!).ToImmutableArray(),
            outcomes.Where(x => x.Uncertainty is not null).Select(x => x.Uncertainty!).ToImmutableArray(), [], Files.Select(x => x.Coverage).ToImmutableArray());
        return request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(request) };
    }

    public async Task<string> ClSnapshotAsync()
    {
        await using var context = Acceptance.Factory.CreateDbContext();
        var versions = await context.DocumentVersions.AsNoTracking().ToListAsync();
        return OctoberKnowledgePolicy.SerializeCanonical(versions.OrderBy(x => x.VersionId));
    }

    public async Task<RelatedPage> RelatedAsync(PublicationBinding publication, DocumentVersionReference document, ImmutableArray<Guid>? scope = null)
    {
        var protector = new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        var reader = new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(Acceptance.Factory), publication,
            new B0bReaderDisclosure(), new OctoberKnowledgeCursorProtector(protector, new B0bReadClock()), new B0bReadLog());
        var opened = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-user", "controlled-stamp"),
            KnowledgeMode.IT, KnowledgePurpose.Reader, scope ?? Corpus.Documents.Select(x => x.RepositoryId).ToImmutableArray()));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        return await reader.GetRelatedAsync(new(Assert.IsType<ReadSelection>(opened.Selection),
            new(document.BatchId, publication.PublicationId, document.LogicalDocumentId, document.DocumentVersionId)));
    }

    public ValueTask DisposeAsync() => Acceptance.DisposeAsync();
}

internal sealed class ResolutionPersistenceAuthorization : IOctoberKnowledgeProducerAuthorization
{
    public Guid? DeniedRepository { get; set; }
    public bool Unavailable { get; set; }
    public ImmutableArray<Contributor> LastContributors { get; private set; }
    public Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastContributors = contributors;
        return Task.FromResult(Unavailable ? DisclosureStatus.AuthorityUnavailable :
            DeniedRepository is { } denied && contributors.Any(x => x.RepositoryId == denied) ? DisclosureStatus.Denied : DisclosureStatus.Allowed);
    }
}

/// <summary>Real owned SQLite accepts separate v1 communication and v2 flow declarations into one immutable corpus.</summary>
internal sealed class RetainedResolutionPersistenceCase : IAsyncDisposable
{
    private RetainedResolutionPersistenceCase(OctoberKnowledgeAcceptanceFixture acceptance, CorpusHandle corpus, PublicationBinding publication)
    { Acceptance = acceptance; Corpus = corpus; Publication = publication; }
    public OctoberKnowledgeAcceptanceFixture Acceptance { get; }
    public CorpusHandle Corpus { get; }
    public PublicationBinding Publication { get; }
    public OctoberResolutionStore Store(string? profile) => new(Acceptance.Factory, Acceptance.Authorization, Acceptance.Guard, Acceptance.Log, profile);
    public OctoberBusinessFromClStore Continuation() => new(Acceptance.Factory, new Allowed(), Acceptance.Guard, Acceptance.Guard, new Settled(), Acceptance.Log);
    public static async Task<RetainedResolutionPersistenceCase> CreateAsync()
    {
        var acceptance = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        try
        {
            var retained = new RetainedResolutionCase();
            ImmutableArray<ResolutionDocument> templates = [ResolutionCase.Document(3, "caller"), ResolutionCase.Document(4, "provider"), .. retained.Corpus.Documents];
            await using (var setup = acceptance.Factory.CreateDbContext())
            {
                foreach (var repository in templates.GroupBy(document => document.RepositoryId))
                {
                    var origin = repository.First().Sections[0].Claims[0].Evidence[0].Source!.Origin;
                    setup.RepositorySelections.Add(new() { BatchId = acceptance.Binding.BatchId, RepositoryId = origin.RepositoryId,
                        RootBindingIdentity = origin.RootBindingIdentity, OperatorLabel = "controlled-mixed-profile" });
                }
                await setup.SaveChangesAsync();
            }
            var files = ImmutableArray.CreateBuilder<AcceptedFile>();
            for (int index = 0; index < templates.Length; index++)
            {
                var template = templates[index];
                var origin = template.Sections[0].Claims[0].Evidence[0].Source!.Origin;
                string selector = template.Observations.FirstOrDefault()?.OwnerSelector ?? "artifact-file";
                var unit = OctoberKnowledgeAcceptanceFixture.Unit(selector, null, "Controlled mixed profile " + index,
                    [new(origin, 1, 100)], template.Sections);
                Guid admission = ResolutionCase.Id(8900 + index);
                var proposal = new CompletedFileProposal(acceptance.Binding, admission, ResolutionCase.Id(8910 + index), origin, "",
                    [unit], template.Observations, [], template.Contributors.Add(acceptance.UncitedContributor),
                    new(admission, origin, CoverageDisposition.Accepted, null));
                acceptance.Source.Result = new(origin, 100);
                var accepted = await acceptance.Writer().AcceptCompletedFileAsync(acceptance.Seal(proposal, ResolutionCase.Id(8920 + index)));
                Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
                files.Add(accepted.AcceptedFile!);
            }
            var freeze = new CorpusFreezeRequest(new(acceptance.Binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()),
                acceptance.Binding, "", files.ToImmutable(), files.Select(file => file.Coverage).ToImmutableArray());
            freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
            acceptance.FreezeAuthority.Approve(freeze);
            var frozen = await acceptance.Writer().FreezeCorpusAsync(freeze);
            Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
            var publication = new PublicationRequest(new(acceptance.Binding.BatchId, OperationKind.PublishSelection, Guid.NewGuid()),
                frozen.Corpus!, "", files.SelectMany(file => file.Documents).ToImmutableArray(), [], [], [], freeze.TerminalCoverage);
            publication = publication with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(publication) };
            var published = await acceptance.Writer().PublishSelectionAsync(publication);
            Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
            return new(acceptance, frozen.Corpus!, published.Publication!);
        }
        catch { await acceptance.DisposeAsync(); throw; }
    }
    public async Task<ResolutionAcceptanceRequest> RequestAsync(string? profile)
    {
        var corpus = (await Store(profile).LoadCorpusAsync(Corpus)).Corpus!;
        Assert.NotNull(corpus);
        Assert.Equal(profile, corpus.ResolutionPolicyVersion);
        var inquiry = Assert.Single(OctoberResolutionPolicy.Plan(corpus).Inquiries);
        var caller = corpus.Documents.Single(document => document.Version == inquiry.Caller.Document);
        var providerRef = Assert.Single(inquiry.Candidates);
        var provider = corpus.Documents.Single(document => document.Version == providerRef.Document);
        var observation = OctoberResolutionPolicy.ResolveObservation(corpus, inquiry.Caller)!;
        var endpoint = OctoberResolutionPolicy.ResolveObservation(corpus, providerRef)!;
        ResolutionEvidenceAssertion Assertion(ResolutionAssertionKind kind, ResolutionDocument document, int ordinal)
        {
            var section = document.Sections[0];
            var claim = section.Claims[ordinal];
            return new(kind, document.Version, section.SectionId, claim.ClaimGroupId, claim.Evidence[0], claim.Text, "Controlled exact accepted assertion.");
        }
        var witness = new ResolutionWitness(inquiry.Caller, providerRef, observation.Operation, endpoint.Role, endpoint.Operation,
            observation.Mechanism, profile is null ? "tenant-a" : "Same retained repository/root", "Exact accepted connecting declaration.", null,
            [Assertion(ResolutionAssertionKind.CallerAction, caller, 0),
             Assertion(ResolutionAssertionKind.ProviderRoleAndOperation, provider, 0),
             Assertion(ResolutionAssertionKind.ConnectingBinding, caller, profile is null ? 1 : 0)],
            [], "Static accepted evidence links these exact retained versions.", [], false, false)
        { BindingToken = profile is null ? null : RetainedResolutionCase.Token };
        var request = new ResolutionAcceptanceRequest(new(Corpus.Binding.BatchId, OperationKind.AcceptResolution, Guid.NewGuid()),
            Corpus, Guid.NewGuid(), "", inquiry.Fingerprint, witness) { ResolutionPolicyVersion = profile };
        return request with { CanonicalPayloadDigest = OctoberResolutionStore.ComputeAcceptanceDigest(request) };
    }
    public async Task<FlowRootSelection> RootAsync(string? profile)
    {
        var corpus = (await Store(profile).LoadCorpusAsync(Corpus)).Corpus!;
        var caller = Assert.Single(OctoberResolutionPolicy.Plan(corpus).Inquiries).Caller.Document;
        await using var context = Acceptance.Factory.CreateDbContext();
        var logical = await context.LogicalDocuments.SingleAsync(value => value.LogicalDocumentId == caller.LogicalDocumentId);
        var unit = await context.CodeUnits.SingleAsync(value => value.UnitId == logical.UnitId);
        return new(unit.UnitId, caller, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector);
    }
    public ValueTask DisposeAsync() => Acceptance.DisposeAsync();
    private sealed class Allowed : IOctoberBusinessFromClAuthority
    {
        public Task<DisclosureStatus> CheckAsync(BusinessFromClRequest request, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default) =>
            Task.FromResult(DisclosureStatus.Allowed);
    }
    private sealed class Settled : IOctoberBusinessFromClSettlementAuthority
    {
        public Task<BusinessFromClSettlement> VerifyAsync(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission, CancellationToken cancellationToken = default) =>
            Task.FromResult(BusinessFromClSettlement.Settled);
    }
}
