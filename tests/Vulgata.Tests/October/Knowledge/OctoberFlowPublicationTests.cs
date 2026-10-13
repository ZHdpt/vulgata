using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Actual SQLite B0/B1 acceptance, owner loading and publication; proposals and source/Scan authority remain controlled.</summary>
public sealed class OctoberFlowPublicationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("success")]
    [InlineData("denied")]
    [InlineData("tampered-retained-request")]
    public async Task ContextOverride_PublicationRevalidatesEffectiveDependencies_AndKeepsOldPublicationReadableAsync(string scenario)
    {
        await using var fixture = await SynthesisAdvisoryPersistenceCase.CreateAsync(true, exactRepositoryIdentity: true);
        var store = new OctoberFlowPublicationStore(fixture.Acceptance.Factory, fixture.Acceptance.Authorization, fixture.Acceptance.Guard, fixture.Acceptance.Log);
        var input = (await new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, fixture.Acceptance.Authorization)
            .LoadAsync(fixture.Corpus, [fixture.Root])).Input!;
        var previous = await store.PublishAsync(fixture.Request(input));
        Assert.Equal(BusinessFinalizationStatus.Published, previous.Status);
        var source = new OctoberKnowledgePublishedSource(fixture.Acceptance.Factory);
        string original = OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(previous.Publication!));
        var contextOverride = new BusinessAdvisoryContextOverride([new(fixture.Acceptance.Origin.RepositoryId,
            Vulgata.Core.DomainServices.OctoberContextScope.System, "Run-specific project terminology.")]);
        var effective = OctoberBusinessContextOverridePolicy.Apply(input, contextOverride);
        var added = Assert.Single(effective.AdditionalAdvisoryContributors);
        var union = OctoberBusinessContextOverridePolicy.EffectiveContributors(effective);
        var request = fixture.Request(effective, contextOverride);
        if (scenario == "denied")
        {
            var authority = new FlowPublicationAuthority { DeniedContributor = added };
            var denied = await new OctoberFlowPublicationStore(fixture.Acceptance.Factory, authority, fixture.Acceptance.Guard, fixture.Acceptance.Log).PublishAsync(request);
            Assert.Null(denied.Publication);
            Assert.Equal(KnowledgeWriteStatus.Denied, denied.PersistenceStatus);
            Assert.Contains(authority.Seen, seen => seen.Contains(added));
            await using var deniedContext = fixture.Acceptance.Factory.CreateDbContext();
            Assert.Equal(1, await deniedContext.BusinessFinalizations.CountAsync());
            return;
        }
        var result = await store.PublishAsync(request);
        Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
        Assert.NotEqual(previous.Publication, result.Publication);
        var published = (await source.LoadAsync(result.Publication!))!;
        var business = Assert.Single(published.Documents.Where(value => value.Kind == DocumentKind.BusinessLogic));
        Assert.True(union.ToHashSet().SetEquals(business.Contributors));
        var selections = new OctoberKnowledgeSelectionAuthority(fixture.Acceptance.Factory);
        var selected = (await selections.LoadAsync(result.Publication!))!;
        Assert.Contains(selected.Advisory, entry => entry.Kind == added.Kind && entry.RecordId == added.AdvisoryRecordId && entry.ContentDigest == added.ContentDigest);
        Assert.DoesNotContain((await selections.LoadAsync(previous.Publication!))!.Advisory, entry => entry.ContentDigest == added.ContentDigest);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        var stored = await context.BusinessFinalizations.AsNoTracking().SingleAsync(value => value.OperationId == request.Operation.OperationId);
        var retained = JsonSerializer.Deserialize<FlowFinalizationPublicationRequest>(stored.RequestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(contextOverride), OctoberKnowledgePolicy.SerializeCanonical(retained.Selection.ContextOverride));
        if (scenario == "tampered-retained-request")
        {
            var tampered = retained with { Selection = retained.Selection with { ContextOverride = new([contextOverride.Entries[0] with { Text = "Unbound substitute" }]) } };
            string json = OctoberKnowledgePolicy.SerializeCanonical(tampered);
            Assert.Equal(1, await context.BusinessFinalizations.Where(value => value.OperationId == request.Operation.OperationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.RequestJson, json)));
            Assert.Null(await selections.LoadAsync(result.Publication!));
        }
        else
        {
            var live = new OverrideLiveAuthority();
            var policy = new OctoberKnowledgeDisclosurePolicy(live, selections);
            var readSelection = new ReadSelectionRequest(result.Publication!, new("ordinary", "stamp"), KnowledgeMode.Business,
                KnowledgePurpose.Reader, [fixture.Acceptance.Origin.RepositoryId]);
            Assert.Equal(DisclosureStatus.Allowed, await policy.CheckAsync(new(readSelection, business.Contributors, DocumentKind.BusinessLogic)));
            Assert.Equal(DisclosureStatus.Denied, await policy.CheckAsync(new(readSelection,
                business.Contributors.Add(added with { ContentDigest = OctoberKnowledgePolicy.Hash("unselected version") }), DocumentKind.BusinessLogic)));
            var replay = await store.LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
            Assert.Equal(result.Publication, replay.Publication);
        }
        Assert.Equal(original, OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(previous.Publication!)));
        var old = (await source.LoadAsync(previous.Publication!))!;
        var oldBusiness = Assert.Single(old.Documents.Where(value => value.Kind == DocumentKind.BusinessLogic));
        var oldPolicy = new OctoberKnowledgeDisclosurePolicy(new OverrideLiveAuthority(), selections);
        var reader = new OctoberKnowledgeReader(source, previous.Publication!, oldPolicy,
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
        var opened = await reader.OpenReadSelectionAsync(new(previous.Publication!, new("ordinary", "stamp"), KnowledgeMode.Business,
            KnowledgePurpose.Reader, [fixture.Acceptance.Origin.RepositoryId]));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var read = await reader.ReadExactAsync(new(opened.Selection!, new(oldBusiness.Reference.BatchId, previous.Publication!.PublicationId,
            oldBusiness.Reference.LogicalDocumentId, oldBusiness.Reference.DocumentVersionId)));
        Assert.Equal(KnowledgeReadStatus.Success, read.Status);
    }

    private sealed class OverrideLiveAuthority : IOctoberKnowledgeLiveReadAuthority
    {
        public Task<DisclosureStatus> CheckAsync(KnowledgeSubject subject, KnowledgeSelectionAuthoritySnapshot snapshot,
            ImmutableArray<Guid> requestedScope, ImmutableArray<Contributor> allContributors, CancellationToken cancellationToken = default) =>
            Task.FromResult(DisclosureStatus.Allowed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadAsync_SelectedSystemAndRepositoryContext_RequiresExistingCompleteLedgerAsync(bool includeSystemContributor)
    {
        await using var fixture = await SynthesisAdvisoryPersistenceCase.CreateAsync(includeSystemContributor);
        var loader = new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, fixture.Acceptance.Authorization);
        var loaded = await loader.LoadAsync(fixture.Corpus, [fixture.Root]);
        if (!includeSystemContributor)
        {
            Assert.NotEqual(KnowledgeReadStatus.Success, loaded.Status);
            Assert.Null(loaded.Input);
            return;
        }
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var input = Assert.IsType<FlowPlanningInput>(loaded.Input);
        Assert.Equal(2, input.AdvisoryContext.Length);
        var system = Assert.Single(input.AdvisoryContext.Where(value => value.Scope == Vulgata.Core.DomainServices.OctoberContextScope.System));
        var repository = Assert.Single(input.AdvisoryContext.Where(value => value.Scope == Vulgata.Core.DomainServices.OctoberContextScope.Repository));
        Assert.Equal(SynthesisAdvisoryPersistenceCase.SystemText, system.Text);
        Assert.Equal(fixture.SystemContributor, system.Contributor);
        Assert.Equal(OctoberKnowledgeAcceptanceFixture.AdvisorySnapshot, repository.Text);
        Assert.Equal(fixture.Acceptance.AdvisoryContributor, repository.Contributor);
        Assert.True(fixture.Contributors.ToHashSet().SetEquals(input.Contributors));
        var group = Assert.Single(OctoberFlowPlanner.Plan(input).Groups);
        var packet = OctoberBusinessSynthesisPolicy.CreateModelPacket(input, group.GroupVersionId);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(input.AdvisoryContext),
            OctoberKnowledgePolicy.SerializeCanonical(packet.AdvisoryContext));
        var reloaded = await loader.LoadAsync(fixture.Corpus, [fixture.Root]);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(input.AdvisoryContext),
            OctoberKnowledgePolicy.SerializeCanonical(reloaded.Input!.AdvisoryContext));
        var request = fixture.Request(input);
        var result = await new OctoberFlowPublicationStore(fixture.Acceptance.Factory, fixture.Acceptance.Authorization,
            fixture.Acceptance.Guard, fixture.Acceptance.Log).PublishAsync(request);
        Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
        var published = await new OctoberKnowledgePublishedSource(fixture.Acceptance.Factory).LoadAsync(result.Publication!);
        var business = Assert.Single(published!.Documents.Where(value => value.Kind == DocumentKind.BusinessLogic));
        Assert.True(fixture.Contributors.ToHashSet().SetEquals(business.Contributors));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("text")]
    [InlineData("origin-record")]
    [InlineData("evidence-kind")]
    [InlineData("system-binding")]
    [InlineData("deleted-row")]
    [InlineData("applicability")]
    public async Task PublishAsync_AdvisorySnapshotCorruptedAfterPlanning_FreshLoadRejectsWithoutBusinessWritesAsync(string mutation)
    {
        await using var fixture = await SynthesisAdvisoryPersistenceCase.CreateAsync(true);
        var loader = new OctoberFlowPlanningInputLoader(fixture.Acceptance.Factory, fixture.Acceptance.Authorization);
        var loaded = await loader.LoadAsync(fixture.Corpus, [fixture.Root]);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var request = fixture.Request(loaded.Input!);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        string clBefore = OctoberKnowledgePolicy.SerializeCanonical(await context.DocumentVersions.AsNoTracking().OrderBy(value => value.VersionId).ToListAsync());
        // Deliberate corruption in this test-owned SQLite database bypasses the normal immutable setup API.
        var selected = context.AdvisoryEntries.Where(value => value.Scope == 1);
        if (mutation == "text") Assert.Equal(1, await selected.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Value, "Altered retained context")));
        if (mutation == "origin-record") Assert.Equal(1, await selected.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.OriginRecordId, Guid.NewGuid())));
        if (mutation == "evidence-kind") Assert.Equal(1, await selected.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.EvidenceKind, 1)));
        if (mutation == "system-binding") Assert.Equal(1, await context.RepositorySelections.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.SystemId, Guid.NewGuid())));
        if (mutation == "deleted-row") Assert.Equal(1, await selected.ExecuteDeleteAsync());
        if (mutation == "applicability") Assert.Equal(1, await selected.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Applicability, 1)));
        var fresh = await loader.LoadAsync(fixture.Corpus, [fixture.Root]);
        Assert.NotEqual(KnowledgeReadStatus.Success, fresh.Status);
        Assert.Null(fresh.Input);
        var result = await new OctoberFlowPublicationStore(fixture.Acceptance.Factory, fixture.Acceptance.Authorization,
            fixture.Acceptance.Guard, fixture.Acceptance.Log).PublishAsync(request);
        Assert.Null(result.Publication);
        Assert.Equal(0, await context.BusinessFinalizations.CountAsync());
        Assert.Equal(0, await context.KnowledgeOperations.CountAsync(value => value.Kind == OperationKind.FinalizeCorpus));
        Assert.Equal(0, await context.LogicalDocuments.CountAsync(value => value.Kind == DocumentKind.BusinessLogic));
        Assert.Equal(clBefore, OctoberKnowledgePolicy.SerializeCanonical(await context.DocumentVersions.AsNoTracking().OrderBy(value => value.VersionId).ToListAsync()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_TwentyTwoExactRoots_RevalidatesAtomicallyAndReplaysWithoutChangingClAsync(bool invalidateLastRoot)
    {
        const int rootCount = 22;
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var template = fixture.Proposal.Units[0];
        var sourceProposal = fixture.Proposal with
        {
            Units = Enumerable.Range(0, rootCount).Select(index => OctoberKnowledgeAcceptanceFixture.Unit("batch-root-" + index,
                null, "Controlled root " + index, template.Anchors, template.Sections)).ToImmutableArray(),
            Observations = [], Uncertainties = []
        };
        Assert.True(OctoberKnowledgePolicy.Validate(sourceProposal, new(fixture.Origin, 100)));
        var accepted = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(sourceProposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var freeze = new CorpusFreezeRequest(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()),
            fixture.Binding, "", [accepted.AcceptedFile!], [sourceProposal.Coverage]);
        freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        fixture.FreezeAuthority.Approve(freeze);
        var frozen = await fixture.Writer().FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        await using var context = fixture.Factory.CreateDbContext();
        var units = await context.CodeUnits.AsNoTracking().ToListAsync();
        var logicals = await context.LogicalDocuments.AsNoTracking().ToListAsync();
        var versions = await context.DocumentVersions.AsNoTracking().OrderBy(version => version.VersionId).ToListAsync();
        string clBefore = OctoberKnowledgePolicy.SerializeCanonical(versions);
        var roots = units.Select(unit =>
        {
            var logical = logicals.Single(value => value.UnitId == unit.UnitId);
            var version = versions.Single(value => value.LogicalDocumentId == logical.LogicalDocumentId);
            return new FlowRootSelection(unit.UnitId, new(fixture.Binding.BatchId, logical.LogicalDocumentId, version.VersionId),
                FlowRootKind.SelectedCodeUnit, unit.SemanticSelector);
        }).ToImmutableArray();
        var loaded = await new OctoberFlowPlanningInputLoader(fixture.Factory, fixture.Authorization).LoadAsync(frozen.Corpus!, roots);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var input = loaded.Input!;
        var groups = OctoberFlowPlanner.Plan(input).Groups;
        Assert.Equal(rootCount, groups.Length);
        var completed = roots.Select((root, index) => new FlowSynthesisSubmission(root,
            groups.Single(group => group.RootVersion == root.Document).GroupVersionId,
            new("受控业务说明 " + index, [new("main", 0, "业务操作", [new("action", "依据已接受的证据说明本地操作。",
                ClaimQualification.SourceSupported, [new(root.Document, "overview", "action")], [])])]))).ToImmutableArray();
        if (invalidateLastRoot)
        {
            var last = completed[^1];
            var section = last.Proposal.Sections[0];
            // Keep a well-shaped proposal but substitute a nonexistent accepted claim in the final root.
            var claim = section.Claims[0] with { Inputs = [section.Claims[0].Inputs[0] with { ClaimGroupId = "not-accepted" }] };
            completed = completed.SetItem(rootCount - 1, last with { Proposal = last.Proposal with
                { Sections = [section with { Claims = [claim] }] } });
        }
        var operation = new OperationKey(fixture.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        var selection = new BusinessFinalizationSelection(roots, new(), Guid.NewGuid(), "controlled-profile",
            OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(new("controlled-host", 0, "controlled-profile", "openai",
                "https://unused.invalid", "controlled-model", "controlled-reference")),
            OctoberKnowledgePolicy.Hash("Controlled batch synthesis instructions."), 40);
        string digest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(new(operation, frozen.Corpus!, "", new FinalizationForbiddenScope()) { Selection = selection });
        var request = new FlowFinalizationPublicationRequest(operation, frozen.Corpus!, digest, selection, completed, [], "");
        request = request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };
        var store = new OctoberFlowPublicationStore(fixture.Factory, fixture.Authorization, fixture.Guard, fixture.Log);
        var result = await store.PublishAsync(request);
        Assert.Equal(invalidateLastRoot ? BusinessFinalizationStatus.Failed : BusinessFinalizationStatus.Published, result.Status);
        Assert.Equal(invalidateLastRoot ? KnowledgeWriteStatus.InvalidProposal : KnowledgeWriteStatus.Committed, result.PersistenceStatus);
        Assert.Equal(invalidateLastRoot ? 0 : rootCount, await context.LogicalDocuments.CountAsync(document => document.Kind == DocumentKind.BusinessLogic));
        Assert.Equal(invalidateLastRoot ? 0 : 1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(invalidateLastRoot ? 0 : 1, await context.BusinessFinalizations.CountAsync());
        Assert.Equal(invalidateLastRoot ? 0 : 1, await context.KnowledgeOperations.CountAsync(operation => operation.Kind == OperationKind.FinalizeCorpus));
        Assert.Equal(invalidateLastRoot ? 0 : rootCount * rootCount, await context.FlowDocumentDependencies.CountAsync());
        if (invalidateLastRoot)
        {
            Assert.Null(result.Publication);
            foreach (string table in _flowTables)
                Assert.Equal(0, await context.Database.SqlQueryRaw<long>($"SELECT COUNT(*) AS \"Value\" FROM \"{table}\"").SingleAsync());
        }
        else
        {
            int submissions = fixture.Guard.Submissions;
            var replay = await store.LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
            Assert.Equal(result.Publication, replay.Publication);
            Assert.True(replay.IsReplay);
            Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.PersistenceStatus);
            Assert.Equal(submissions, fixture.Guard.Submissions);
            var published = await new OctoberKnowledgePublishedSource(fixture.Factory).LoadAsync(result.Publication!);
            Assert.Equal(rootCount, published!.Documents.Count(document => document.Kind == DocumentKind.BusinessLogic));
            Assert.All(published.Documents.Where(document => document.Kind == DocumentKind.BusinessLogic),
                document => Assert.True(input.Contributors.ToHashSet().SetEquals(document.Contributors)));
        }
        var retainedCl = await context.DocumentVersions.AsNoTracking().Where(version => versions.Select(original => original.VersionId).Contains(version.VersionId))
            .OrderBy(version => version.VersionId).ToListAsync();
        Assert.Equal(clBefore, OctoberKnowledgePolicy.SerializeCanonical(retainedCl));
    }

    private static readonly string[] _flowTables = ["OctoberBusinessFinalizations", "OctoberLogicalFlows", "OctoberFlowGroupVersions",
        "OctoberFlowDocumentDependencies", "OctoberFlowRelationshipDependencies", "OctoberFlowResolutionUncertaintyDependencies", "OctoberFlowFileUncertaintyDependencies"];
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_OriginalProposal_ProducesExactReadableBlWithoutChangingClOrB0Async()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        string clBefore = await fixture.ClSnapshotAsync();
        var source = new OctoberKnowledgePublishedSource(fixture.Basis.Acceptance.Factory);
        string b0Before = OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(fixture.Basis.B0Publication));
        var request = fixture.Request();
        var expected = OctoberBusinessSynthesisPolicy.Validate(fixture.Input, fixture.Group.GroupVersionId,
            request.Completed[0].Proposal, request.Selection.Limits);
        Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, expected.Status);

        var result = await fixture.Store().PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.PersistenceStatus);
        Assert.False(result.IsReplay);
        var publication = Assert.IsType<PublicationBinding>(result.Publication);
        Assert.NotEqual(fixture.Basis.B0Publication.PublicationId, publication.PublicationId);
        var retained = Assert.IsType<OctoberPublishedKnowledge>(await source.LoadAsync(publication));
        var bl = Assert.Single(retained.Documents.Where(document => document.Kind == DocumentKind.BusinessLogic));
        Assert.Equal(expected.Content!.ContentJson, bl.ContentJson);
        Assert.True(fixture.Input.Contributors.ToHashSet().SetEquals(bl.Contributors));
        Assert.Contains(fixture.Basis.Acceptance.UncitedContributor, bl.Contributors);
        Assert.Contains(bl.Contributors, contributor => contributor.RepositoryId == fixture.Basis.ThirdRepository);
        var reader = fixture.Reader(publication);
        var opened = await reader.OpenReadSelectionAsync(fixture.ReadRequest(publication));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var page = await reader.ReadExactAsync(new(opened.Selection!, new(bl.Reference.BatchId, publication.PublicationId,
            bl.Reference.LogicalDocumentId, bl.Reference.DocumentVersionId)));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Contains(request.Completed[0].Proposal.Sections[0].Claims[0].Text, page.Content, StringComparison.Ordinal);
        Assert.Equal(DocumentKind.BusinessLogic, page.Document!.Kind);
        Assert.Equal(new DocumentHandle(bl.Reference.BatchId, publication.PublicationId,
            bl.Reference.LogicalDocumentId, bl.Reference.DocumentVersionId), page.Document.Handle);
        Assert.Contains(page.Claims, claim => claim.Qualification == ClaimQualification.Uncertain);
        Assert.Equal(clBefore, await fixture.ClSnapshotAsync());
        Assert.Equal(b0Before, OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(fixture.Basis.B0Publication)));
        await using var context = fixture.Basis.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.LogicalDocuments.CountAsync(document => document.Kind == DocumentKind.BusinessLogic));
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(operation => operation.Kind == OperationKind.FinalizeCorpus));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAndLookupAsync_IdenticalOutcome_ReplaysExactPublicationWithoutAnotherSubmissionAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        var first = await fixture.Store().PublishAsync(request);
        Assert.Equal(BusinessFinalizationStatus.Published, first.Status);
        int submitted = fixture.Basis.Acceptance.Guard.Submissions;
        // The prior finalization selection remains pinned to its original prompt and small
        // output allowance; adopting a new synthesis packet must not rewrite its receipt.
        Assert.Equal(40, request.Selection.MaximumTokens);
        string clBefore = await fixture.ClSnapshotAsync();
        var source = new OctoberKnowledgePublishedSource(fixture.Basis.Acceptance.Factory);
        string publicationBefore = OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(first.Publication!));
        string requestJson;
        string resultJson;
        await using (var before = fixture.Basis.Acceptance.Factory.CreateDbContext())
        {
            var receipt = await before.BusinessFinalizations.AsNoTracking().SingleAsync();
            requestJson = receipt.RequestJson;
            resultJson = receipt.ResultJson;
        }

        var replay = await fixture.Store().PublishAsync(request);
        var lookup = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);

        foreach (var result in new[] { replay, lookup })
        {
            Assert.Equal(first.Status, result.Status);
            Assert.Equal(first.Publication, result.Publication);
            Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(first.Coverage), OctoberKnowledgePolicy.SerializeCanonical(result.Coverage));
            Assert.True(result.IsReplay);
            Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, result.PersistenceStatus);
        }
        Assert.Equal(submitted, fixture.Basis.Acceptance.Guard.Submissions);
        await using var context = fixture.Basis.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.LogicalDocuments.CountAsync(document => document.Kind == DocumentKind.BusinessLogic));
        Assert.Equal(2, await context.KnowledgePublications.CountAsync());
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(operation => operation.Kind == OperationKind.FinalizeCorpus));
        var after = await context.BusinessFinalizations.AsNoTracking().SingleAsync();
        Assert.Equal(requestJson, after.RequestJson);
        Assert.Equal(resultJson, after.ResultJson);
        Assert.Equal(clBefore, await fixture.ClSnapshotAsync());
        Assert.Equal(publicationBefore, OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(first.Publication!)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_ChangedOriginalOutcomeUnderSameRequest_ConflictsWithoutReplacingHistoryAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var original = fixture.Request();
        var published = await fixture.Store().PublishAsync(original);
        Assert.Equal(BusinessFinalizationStatus.Published, published.Status);
        var source = new OctoberKnowledgePublishedSource(fixture.Basis.Acceptance.Factory);
        string before = OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(published.Publication!));
        int submissions = fixture.Basis.Acceptance.Guard.Submissions;
        var changed = original with
        {
            Completed = [original.Completed[0] with { Proposal = original.Completed[0].Proposal with { Title = "另一份业务说明" } }]
        };
        changed = changed with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(changed) };
        Assert.Equal(original.CanonicalPayloadDigest, changed.CanonicalPayloadDigest);
        Assert.NotEqual(original.CanonicalOutcomeDigest, changed.CanonicalOutcomeDigest);

        var conflict = await fixture.Store().PublishAsync(changed);

        Assert.Equal(BusinessFinalizationStatus.Failed, conflict.Status);
        Assert.Equal(KnowledgeWriteStatus.Conflict, conflict.PersistenceStatus);
        Assert.Null(conflict.Publication);
        Assert.Equal(submissions, fixture.Basis.Acceptance.Guard.Submissions);
        var exact = await fixture.Store().LookupAsync(original.Corpus, original.Operation, original.CanonicalPayloadDigest);
        Assert.Equal(published.Publication, exact.Publication);
        Assert.Equal(published.Status, exact.Status);
        Assert.Equal(before, OctoberKnowledgePolicy.SerializeCanonical(await source.LoadAsync(published.Publication!)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_ExplicitEmptyRoots_DurablyReplaysNoContentWithoutBlOrPublicationAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request(emptyRoots: true);
        int submissions = fixture.Basis.Acceptance.Guard.Submissions;
        var missing = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, missing.Status);
        Assert.Null(missing.Publication);

        var result = await fixture.Store().PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.NoContent, result.Status);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.PersistenceStatus);
        Assert.Null(result.Publication);
        Assert.False(result.IsReplay);
        Assert.Equal(submissions + 1, fixture.Basis.Acceptance.Guard.Submissions);
        var lookup = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(BusinessFinalizationStatus.NoContent, lookup.Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, lookup.PersistenceStatus);
        Assert.True(lookup.IsReplay);
        Assert.Null(lookup.Publication);
        Assert.Equal(submissions + 1, fixture.Basis.Acceptance.Guard.Submissions);
        await using var context = fixture.Basis.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.LogicalDocuments.CountAsync(document => document.Kind == DocumentKind.BusinessLogic));
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(operation => operation.Kind == OperationKind.FinalizeCorpus));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("claim-version")]
    [InlineData("qualification")]
    [InlineData("group")]
    public async Task PublishAsync_RevalidatesOriginalProposalAgainstExactStoredInputsAsync(string invalid)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        var submission = request.Completed[0];
        var section = submission.Proposal.Sections[0];
        var claim = section.Claims[0];
        if (invalid == "group") submission = submission with { GroupVersionId = Guid.NewGuid() };
        else
        {
            claim = invalid == "qualification" ? claim with { Qualification = ClaimQualification.Advisory }
                : claim with { Inputs = [claim.Inputs[0] with { Document = claim.Inputs[0].Document with { DocumentVersionId = Guid.NewGuid() } }] };
            submission = submission with { Proposal = submission.Proposal with { Sections = [section with { Claims = [claim] }] } };
        }
        request = request with { Completed = [submission] };
        request = request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };
        int submitted = fixture.Basis.Acceptance.Guard.Submissions;

        var result = await fixture.Store().PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.Failed, result.Status);
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.PersistenceStatus);
        Assert.Null(result.Publication);
        Assert.Equal(submitted, fixture.Basis.Acceptance.Guard.Submissions);
        await AssertNoFlowMutationAsync(fixture);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_TypedDependenciesPinAllInputsSeparatelyFromSliceMembershipAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        Assert.Equal(BusinessFinalizationStatus.Published, (await fixture.Store().PublishAsync(fixture.Request())).Status);
        await using var context = fixture.Basis.Acceptance.Factory.CreateDbContext();
        var documents = await context.Database.SqlQueryRaw<FlowDocumentDependencyRow>(
            "SELECT \"LogicalDocumentId\", \"VersionId\", \"IsMember\" FROM \"OctoberFlowDocumentDependencies\"").ToListAsync();
        Assert.Equal(3, documents.Count);
        Assert.True(fixture.Input.Corpus.Documents.Select(value => (value.Version.LogicalDocumentId, value.Version.DocumentVersionId)).ToHashSet()
            .SetEquals(documents.Select(value => (value.LogicalDocumentId, value.VersionId))));
        Assert.True(fixture.Group.Members.Select(value => value.DocumentVersionId).ToHashSet()
            .SetEquals(documents.Where(value => value.IsMember).Select(value => value.VersionId)));
        Assert.Contains(documents, value => value.VersionId == fixture.Basis.Corpus.Documents[2].Version.DocumentVersionId && !value.IsMember);
        var edgeIds = await context.Database.SqlQueryRaw<Guid>("SELECT \"RelationshipVersionId\" AS \"Value\" FROM \"OctoberFlowRelationshipDependencies\"").ToListAsync();
        Assert.True(fixture.Group.Dependencies.Relationships.Select(value => value.RelationshipVersionId).ToHashSet().SetEquals(edgeIds));
        var unknownIds = await context.Database.SqlQueryRaw<Guid>("SELECT \"UncertaintyId\" AS \"Value\" FROM \"OctoberFlowResolutionUncertaintyDependencies\"").ToListAsync();
        Assert.True(fixture.Group.Dependencies.Uncertainties.Select(value => value.UncertaintyVersionId).ToHashSet().SetEquals(unknownIds));
        string ledgerJson = await context.Database.SqlQueryRaw<string>("SELECT \"ContributorsJson\" AS \"Value\" FROM \"OctoberFlowGroupVersions\"").SingleAsync();
        var ledger = JsonSerializer.Deserialize<ImmutableArray<Contributor>>(ledgerJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.True(fixture.Input.Contributors.ToHashSet().SetEquals(ledger));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_SameUnknownKeyInRootAndZeroUnitFiles_PreservesBothCompositeDependenciesAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var rootProposal = fixture.Proposal with
        {
            Observations = [],
            Uncertainties = [fixture.Proposal.Uncertainties[0] with { Key = "same-file-key" }]
        };
        var acceptedRoot = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(rootProposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, acceptedRoot.Status);
        var zeroOrigin = fixture.Origin with { RelativePath = "src/NoUnits.cs", SourceDigest = new string('d', 64) };
        var zeroProposal = rootProposal with
        {
            AdmissionId = OctoberKnowledgeAcceptanceFixture.Id(99500), AttemptId = OctoberKnowledgeAcceptanceFixture.Id(99501), Origin = zeroOrigin,
            Units = [], Observations = [],
            Uncertainties = [new("same-file-key", "The zero-unit file retains a separate source uncertainty.",
                [new(null, null, new(zeroOrigin, 1, 2), null, zeroOrigin.SourceDigest)], ["No semantic unit was accepted from this file."])],
            Contributors = [new(ContributorKind.Source, zeroOrigin.RepositoryId, null, zeroOrigin.RootBindingIdentity, null, zeroOrigin.SourceDigest), fixture.UncitedContributor],
            Coverage = new(OctoberKnowledgeAcceptanceFixture.Id(99500), zeroOrigin, CoverageDisposition.CompletedZeroUnit, "The completed file contains no semantic units.")
        };
        fixture.Source.Result = new(zeroOrigin, 100);
        Assert.True(OctoberKnowledgePolicy.Validate(zeroProposal, new(zeroOrigin, 100)));
        var acceptedZero = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(zeroProposal, OctoberKnowledgeAcceptanceFixture.Id(99502)));
        Assert.Equal(KnowledgeWriteStatus.Committed, acceptedZero.Status);
        var freeze = new CorpusFreezeRequest(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()), fixture.Binding, "",
            [acceptedRoot.AcceptedFile!, acceptedZero.AcceptedFile!], [rootProposal.Coverage, zeroProposal.Coverage]);
        freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        fixture.FreezeAuthority.Approve(freeze);
        var frozen = await fixture.Writer().FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        await using var context = fixture.Factory.CreateDbContext();
        var unit = await context.CodeUnits.SingleAsync(value => value.SemanticSelector == "operation:order");
        var logical = await context.LogicalDocuments.SingleAsync(value => value.UnitId == unit.UnitId);
        var version = await context.DocumentVersions.SingleAsync(value => value.LogicalDocumentId == logical.LogicalDocumentId);
        var root = new FlowRootSelection(unit.UnitId, new(fixture.Binding.BatchId, logical.LogicalDocumentId, version.VersionId), FlowRootKind.SelectedCodeUnit, unit.SemanticSelector);
        var loaded = await new OctoberFlowPlanningInputLoader(fixture.Factory, fixture.Authorization).LoadAsync(frozen.Corpus!, [root]);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var input = loaded.Input!;
        Assert.Equal(2, input.FileUncertainties.Length);
        var group = Assert.Single(OctoberFlowPlanner.Plan(input).Groups);
        var proposal = new BusinessSynthesisProposal("业务处理说明",
            [new("business-main", 0, "处理过程", [new("business-action", "执行已确认的业务操作。", ClaimQualification.SourceSupported,
                [new(root.Document, "overview", "action")], [])])]);
        var operation = new OperationKey(fixture.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        var selection = new BusinessFinalizationSelection([root], new(), Guid.NewGuid(), "controlled-profile",
            OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(new("controlled-host", 0, "controlled-profile", "openai", "https://unused.invalid", "controlled-model", "controlled-reference")),
            OctoberKnowledgePolicy.Hash("Controlled synthesis instructions."), 40);
        string digest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(new(operation, frozen.Corpus!, "", new FinalizationForbiddenScope()) { Selection = selection });
        var request = new FlowFinalizationPublicationRequest(operation, frozen.Corpus!, digest, selection, [new(root, group.GroupVersionId, proposal)], [], "");
        request = request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };

        var result = await new OctoberFlowPublicationStore(fixture.Factory, fixture.Authorization, fixture.Guard, fixture.Log).PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
        var rows = await context.Database.SqlQueryRaw<FlowFileDependencyRow>(
            "SELECT \"BatchId\", \"ManifestId\", \"Key\" FROM \"OctoberFlowFileUncertaintyDependencies\"").ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, value => Assert.Equal("same-file-key", value.Key));
        Assert.Equal(2, rows.Select(value => value.ManifestId).Distinct().Count());
        Assert.True(input.FileUncertainties.Select(value => (value.BatchId, value.ManifestId, value.Key)).ToHashSet()
            .SetEquals(rows.Select(value => (value.BatchId, value.ManifestId, value.Key))));
        var published = await new OctoberKnowledgePublishedSource(fixture.Factory).LoadAsync(result.Publication!);
        var bl = Assert.Single(published!.Documents.Where(value => value.Kind == DocumentKind.BusinessLogic));
        Assert.True(input.Contributors.ToHashSet().SetEquals(bl.Contributors));
        Assert.All(input.FileUncertainties, unknown => Assert.Contains(bl.Sections.SelectMany(section => section.Claims)
            .SelectMany(claim => claim.Limitations), text => text == unknown.Content.Explanation));
        Assert.Equal(2, await context.KnowledgeUncertainties.CountAsync());
        Assert.Equal(2, await context.CodeUnits.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("third-cl")]
    [InlineData("uncited-advisory")]
    [InlineData("unavailable")]
    public async Task PublishAndLookupAsync_LiveUncitedContributorAuthorityFails_RevealsNoArtifactAsync(string deniedInput)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var authority = new FlowPublicationAuthority
        {
            DeniedContributor = deniedInput == "third-cl"
                ? fixture.Input.Contributors.First(value => value.RepositoryId == fixture.Basis.ThirdRepository)
                : fixture.Basis.Acceptance.UncitedContributor,
            Unavailable = deniedInput == "unavailable"
        };
        var store = fixture.Store(authority);
        var request = fixture.Request();
        var expected = deniedInput == "unavailable" ? BusinessFinalizationStatus.AuthorityUnavailable : BusinessFinalizationStatus.Denied;
        var rejected = await store.PublishAsync(request);
        Assert.Equal(expected, rejected.Status);
        Assert.Null(rejected.Publication);
        await AssertNoFlowMutationAsync(fixture);
        authority.Blocked = false;
        var accepted = await store.PublishAsync(request);
        Assert.Equal(BusinessFinalizationStatus.Published, accepted.Status);
        authority.Blocked = true;

        var hidden = await store.LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);

        Assert.Equal(expected, hidden.Status);
        Assert.Null(hidden.Publication);
        Assert.Empty(hidden.Coverage.PermittedGaps);
        Assert.NotEmpty(authority.Seen);
        Assert.All(authority.Seen, contributors => Assert.True(fixture.Input.Contributors.ToHashSet().SetEquals(contributors)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_CompletedAndFailedRoots_PublishesOnlyCompletedFlowWithDurableGapAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync(twoRoots: true);
        var request = fixture.Request(partial: true);
        var missing = request with { Dispositions = [] };
        var rejected = await fixture.Store().PublishAsync(missing);
        Assert.Equal(BusinessFinalizationStatus.Failed, rejected.Status);
        Assert.Null(rejected.Publication);
        await AssertNoFlowMutationAsync(fixture);

        var result = await fixture.Store().PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.PublishedPartial, result.Status);
        Assert.True(result.Coverage.IsPartial);
        Assert.NotEmpty(result.Coverage.PermittedGaps);
        var retained = await new OctoberKnowledgePublishedSource(fixture.Basis.Acceptance.Factory).LoadAsync(result.Publication!);
        var bl = Assert.Single(retained!.Documents.Where(value => value.Kind == DocumentKind.BusinessLogic));
        var reader = fixture.Reader(result.Publication!);
        var opened = await reader.OpenReadSelectionAsync(fixture.ReadRequest(result.Publication!));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var page = await reader.ReadExactAsync(new(opened.Selection!, new(bl.Reference.BatchId, result.Publication!.PublicationId,
            bl.Reference.LogicalDocumentId, bl.Reference.DocumentVersionId)));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        var coverage = Assert.IsType<CoverageProjection>(page.Coverage);
        Assert.True(coverage.IsPartial);
        Assert.NotEmpty(coverage.PermittedGaps);
        Assert.All(coverage.PermittedGaps, gap => Assert.False(string.IsNullOrWhiteSpace(gap)));
        string visibleGaps = string.Join("\n", coverage.PermittedGaps);
        Assert.DoesNotContain(request.Dispositions[0].Reason, visibleGaps, StringComparison.Ordinal);
        Assert.DoesNotContain(request.Dispositions[0].Root.UnitId.ToString(), visibleGaps, StringComparison.OrdinalIgnoreCase);
        Assert.All(fixture.Input.Units, unit => Assert.DoesNotContain(unit.PrimaryOrigin.RelativePath, visibleGaps, StringComparison.Ordinal));
        var replay = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(result.Status, replay.Status);
        Assert.Equal(result.Publication, replay.Publication);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(result.Coverage), OctoberKnowledgePolicy.SerializeCanonical(replay.Coverage));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_F1FenceBeforeEntryOrAtSubmission_LeavesNoFlowRowsAsync(bool duringStaging)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var guard = fixture.Basis.Acceptance.Guard;
        int submissions = guard.Submissions;
        if (duringStaging) guard.BeforeSubmission = () => guard.Fenced = true;
        else guard.Fenced = true;

        var result = await fixture.Store().PublishAsync(fixture.Request());

        Assert.Equal(BusinessFinalizationStatus.HardStopped, result.Status);
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, result.PersistenceStatus);
        Assert.Null(result.Publication);
        Assert.Equal(submissions, guard.Submissions);
        await AssertNoFlowMutationAsync(fixture);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_LateBlMembershipSqlFault_RollsBackContentLineageTerminalAndPublicationAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        string before = await fixture.ClSnapshotAsync();
        await using var context = fixture.Basis.Acceptance.Factory.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER "RejectControlledBlMember" BEFORE INSERT ON "OctoberPublicationMembers"
            WHEN EXISTS (SELECT 1 FROM "OctoberLogicalDocuments" d
                WHERE d."BatchId" = NEW."BatchId" AND d."LogicalDocumentId" = NEW."LogicalDocumentId" AND d."Kind" = 1)
            BEGIN SELECT RAISE(ABORT, 'controlled late BL membership failure'); END;
            """);
        var request = fixture.Request();

        var result = await fixture.Store().PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.Failed, result.Status);
        Assert.Null(result.Publication);
        await AssertNoFlowMutationAsync(fixture);
        Assert.Equal(before, await fixture.ClSnapshotAsync());
        var absent = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, absent.Status);
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER \"RejectControlledBlMember\"");
        Assert.Equal(BusinessFinalizationStatus.Published, (await fixture.Store().PublishAsync(request)).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_SubmittedCommitAcknowledgementLost_ReconcilesExactResultWithoutAnotherSubmissionAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        fixture.Basis.Acceptance.Guard.LoseAcknowledgement = true;
        int before = fixture.Basis.Acceptance.Guard.Submissions;

        var result = await fixture.Store().PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.Published, result.Status);
        Assert.NotNull(result.Publication);
        Assert.Equal(before + 1, fixture.Basis.Acceptance.Guard.Submissions);
        var exact = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(result.Publication, exact.Publication);
        Assert.Equal(result.Status, exact.Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, exact.PersistenceStatus);
        Assert.Equal(before + 1, fixture.Basis.Acceptance.Guard.Submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAndLookupAsync_LogUnavailable_RetainsKnownDurableLogicalOutcomeAsync(bool noContent)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request(emptyRoots: noContent);
        fixture.Basis.Acceptance.Log.Succeeds = false;

        var result = await fixture.Store().PublishAsync(request);
        var lookup = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);

        Assert.Equal(noContent ? BusinessFinalizationStatus.NoContent : BusinessFinalizationStatus.Published, result.Status);
        Assert.Equal(KnowledgeWriteStatus.LogUnavailable, result.PersistenceStatus);
        Assert.Equal(result.Status, lookup.Status);
        Assert.Equal(result.Publication, lookup.Publication);
        Assert.Equal(KnowledgeWriteStatus.LogUnavailable, lookup.PersistenceStatus);
        Assert.True(lookup.IsReplay);
        if (noContent) Assert.Null(result.Publication);
        else Assert.NotNull(result.Publication);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RawInsertAsync_BlVersionWithNullFinalizationKind_CannotBypassOwnerCheckAndForeignKeyAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        Assert.Equal(BusinessFinalizationStatus.Published, (await fixture.Store().PublishAsync(fixture.Request())).Status);
        await using var context = fixture.Basis.Acceptance.Factory.CreateDbContext();
        var original = await context.DocumentVersions.SingleAsync(value => value.Kind == DocumentKind.BusinessLogic);
        Assert.NotNull(original.FlowGroupVersionId);
        Assert.NotNull(original.FinalizationOperationId);
        Guid invalidVersion = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "OctoberDocumentVersions"
                ("BatchId", "VersionId", "LogicalDocumentId", "Kind", "ManifestId", "FlowGroupVersionId",
                 "FinalizationOperationKind", "FinalizationOperationId", "SchemaVersion", "Title", "ContentJson",
                 "ContentDigest", "AnchorsJson", "CreatedAtUtc")
            SELECT "BatchId", {invalidVersion}, "LogicalDocumentId", "Kind", NULL, "FlowGroupVersionId",
                   NULL, "FinalizationOperationId", "SchemaVersion", "Title", "ContentJson",
                   "ContentDigest", "AnchorsJson", "CreatedAtUtc"
            FROM "OctoberDocumentVersions" WHERE "BatchId" = {original.BatchId} AND "VersionId" = {original.VersionId}
            """));

        Assert.Equal(19, error.SqliteErrorCode);
        Assert.Equal(275, error.SqliteExtendedErrorCode); // CHECK constraint, not an unrelated unique/FK failure.
        Assert.False(await context.DocumentVersions.AnyAsync(value => value.VersionId == invalidVersion));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadForOriginalDispatchAsync_AuthorizedExactAbsence_IsDistinctFromPublicIndeterminateAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        var store = fixture.Store();
        int submissions = fixture.Basis.Acceptance.Guard.Submissions;

        var initial = await store.ReadForOriginalDispatchAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);

        Assert.Equal(BusinessFinalizationInitialReadKind.ObservedAbsent, initial.Kind);
        Assert.Null(initial.Result);
        Assert.True(fixture.Input.Contributors.ToHashSet().SetEquals(fixture.Basis.Authorization.LastContributors));
        var ordinary = await store.LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, ordinary.Status);
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, ordinary.PersistenceStatus);
        Assert.Null(ordinary.Publication);
        Assert.Equal(submissions, fixture.Basis.Acceptance.Guard.Submissions);
        await AssertNoFlowMutationAsync(fixture);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadForOriginalDispatchAsync_ExistingOutcome_ReturnsAuthorizedNoDispatchReplayAsync(bool noContent)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request(emptyRoots: noContent);
        var stored = await fixture.Store().PublishAsync(request);
        Assert.Equal(noContent ? BusinessFinalizationStatus.NoContent : BusinessFinalizationStatus.Published, stored.Status);

        var initial = await fixture.Store().ReadForOriginalDispatchAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);

        Assert.Equal(BusinessFinalizationInitialReadKind.NoDispatchResult, initial.Kind);
        var result = Assert.IsType<BusinessFinalizationResult>(initial.Result);
        Assert.Equal(stored.Status, result.Status);
        Assert.Equal(stored.Publication, result.Publication);
        Assert.True(result.IsReplay);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, result.PersistenceStatus);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadForOriginalDispatchAsync_ConflictingDigest_ReturnsNoDispatchConflictWithoutArtifactAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        Assert.Equal(BusinessFinalizationStatus.Published, (await fixture.Store().PublishAsync(request)).Status);
        string wrongDigest = new('f', 64);
        Assert.NotEqual(request.CanonicalPayloadDigest, wrongDigest);

        var initial = await fixture.Store().ReadForOriginalDispatchAsync(request.Corpus, request.Operation, wrongDigest);

        Assert.Equal(BusinessFinalizationInitialReadKind.NoDispatchResult, initial.Kind);
        var result = Assert.IsType<BusinessFinalizationResult>(initial.Result);
        Assert.Equal(BusinessFinalizationStatus.Failed, result.Status);
        Assert.Equal(KnowledgeWriteStatus.Conflict, result.PersistenceStatus);
        Assert.Null(result.Publication);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadForOriginalDispatchAsync_CompleteContributorDenialOrOutage_NeverReportsAbsentAsync(bool outage)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        var authority = new FlowPublicationAuthority { DeniedContributor = fixture.Basis.Acceptance.UncitedContributor, Unavailable = outage };

        var initial = await fixture.Store(authority).ReadForOriginalDispatchAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);

        if (outage) AssertUnavailableInitial(initial);
        else
        {
            Assert.Equal(BusinessFinalizationInitialReadKind.NoDispatchResult, initial.Kind);
            var result = Assert.IsType<BusinessFinalizationResult>(initial.Result);
            Assert.Equal(BusinessFinalizationStatus.Denied, result.Status);
            Assert.Null(result.Publication);
        }
        Assert.NotEmpty(authority.Seen);
        Assert.All(authority.Seen, ledger => Assert.True(fixture.Input.Contributors.ToHashSet().SetEquals(ledger)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadForOriginalDispatchAsync_StorageFailure_IsUnavailableNotObservedAbsentAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        fixture.Basis.Acceptance.Factory.Available = false;
        try
        {
            AssertUnavailableInitial(await fixture.Store().ReadForOriginalDispatchAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest));
        }
        finally { fixture.Basis.Acceptance.Factory.Available = true; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadForOriginalDispatchAsync_OperationExistsButNoContentReceiptChildIsMissing_IsUnavailableAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request(emptyRoots: true);
        var stored = await fixture.Store().PublishAsync(request);
        Assert.Equal(BusinessFinalizationStatus.NoContent, stored.Status);
        await using (var context = fixture.Basis.Acceptance.Factory.CreateDbContext())
        {
            var operation = await context.KnowledgeOperations.SingleAsync(value => value.Kind == OperationKind.FinalizeCorpus && value.OperationId == request.Operation.OperationId);
            Assert.Equal(request.CanonicalPayloadDigest, operation.PayloadDigest);
            // Deliberate owned-SQLite corruption, not a reachable atomic publication path:
            // remove only the receipt child while retaining its durable parent operation.
            Assert.Equal(1, await context.BusinessFinalizations.Where(value => value.OperationId == request.Operation.OperationId).ExecuteDeleteAsync());
            Assert.True(await context.KnowledgeOperations.AnyAsync(value => value.Kind == OperationKind.FinalizeCorpus && value.OperationId == request.Operation.OperationId));
        }

        var initial = await fixture.Store().ReadForOriginalDispatchAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        var ordinary = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);

        AssertUnavailableInitial(initial);
        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, ordinary.Status);
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, ordinary.PersistenceStatus);
        Assert.Null(ordinary.Publication);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadForOriginalDispatchAsync_InvalidDigestOrForeignBinding_CannotEstablishAbsentAsync(bool foreignBinding)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        var corpus = foreignBinding ? request.Corpus with { Binding = request.Corpus.Binding with { CanonicalDatabaseIdentity = "foreign-database" } } : request.Corpus;

        var initial = await fixture.Store().ReadForOriginalDispatchAsync(corpus, request.Operation, foreignBinding ? request.CanonicalPayloadDigest : "");

        Assert.NotEqual(BusinessFinalizationInitialReadKind.ObservedAbsent, initial.Kind);
        if (initial.Kind == BusinessFinalizationInitialReadKind.NoDispatchResult)
        {
            var result = Assert.IsType<BusinessFinalizationResult>(initial.Result);
            Assert.Contains(result.Status, new[] { BusinessFinalizationStatus.Denied, BusinessFinalizationStatus.Failed });
            Assert.Null(result.Publication);
        }
        else AssertUnavailableInitial(initial);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadForOriginalDispatchAsync_CancelledDelayedFactory_ReturnsBoundedAndDisposesLateContextAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        await using var reference = fixture.Basis.Acceptance.Factory.CreateDbContext();
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString()).Options;
        var factory = new AdmissionLifetimeFactory(options) { Delay = true };
        var store = new OctoberFlowPublicationStore(factory, fixture.Basis.Authorization, fixture.Basis.Acceptance.Guard, fixture.Basis.Acceptance.Log);
        using var cancellation = new CancellationTokenSource();
        var pending = store.ReadForOriginalDispatchAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest, cancellation.Token);
        try
        {
            await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            AssertUnavailableInitial(await pending);
        }
        finally
        {
            factory.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (factory.DelayedContext is { } late)
            {
                await factory.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
                for (int attempt = 0; attempt < 100 && Record.Exception(() => late.ChangeTracker.Entries().ToArray()) is not ObjectDisposedException; attempt++)
                    await Task.Delay(20);
                Assert.IsType<ObjectDisposedException>(Record.Exception(() => late.ChangeTracker.Entries().ToArray()));
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LookupAsync_CorruptDurableStatusArtifactMatrix_NeitherReadPortExposesItAsync(bool unknownStatus)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var request = fixture.Request();
        var published = await fixture.Store().PublishAsync(request);
        Assert.Equal(BusinessFinalizationStatus.Published, published.Status);
        await using (var context = fixture.Basis.Acceptance.Factory.CreateDbContext())
        {
            var stored = await context.BusinessFinalizations.AsNoTracking().SingleAsync();
            var retained = JsonSerializer.Deserialize<BusinessFinalizationResult>(stored.ResultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.Equal(BusinessFinalizationStatus.Published, retained.Status);
            Assert.Equal(published.Publication, retained.Publication);
            Assert.Equal(stored.PublicationId, retained.Publication!.PublicationId);
            Assert.Equal(request.Operation, retained.Operation);
            Assert.Equal(request.CanonicalPayloadDigest, stored.RequestDigest);
            Assert.Equal(request.CanonicalOutcomeDigest, stored.OutcomeDigest);
            Assert.Null(retained.PersistenceStatus);
            Assert.False(retained.IsReplay);
            // Change only Status in the original persisted representation. Copying the public
            // response would introduce PersistenceStatus and fail an unrelated existing check.
            var invalid = retained with { Status = unknownStatus ? (BusinessFinalizationStatus)999 : BusinessFinalizationStatus.NoContent };
            string invalidJson = OctoberKnowledgePolicy.SerializeCanonical(invalid);
            Assert.Equal(invalid.Status, JsonSerializer.Deserialize<BusinessFinalizationResult>(invalidJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Status);
            // Owned SQLite corruption probe; intentionally bypasses EF append-only validation.
            Assert.Equal(1, await context.BusinessFinalizations.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.ResultJson, invalidJson)));
        }

        var ordinary = await fixture.Store().LookupAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        var initial = await fixture.Store().ReadForOriginalDispatchAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);

        Assert.Equal(BusinessFinalizationStatus.CommitIndeterminate, ordinary.Status);
        Assert.Null(ordinary.Publication);
        AssertUnavailableInitial(initial);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishAsync_IndividuallyValidProposalsExceedAggregateReplayBudget_RejectsBeforeCommitAsync()
    {
        const int rootCount = 20;
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var template = fixture.Proposal.Units[0];
        var sourceProposal = fixture.Proposal with
        {
            Units = Enumerable.Range(0, rootCount).Select(index => OctoberKnowledgeAcceptanceFixture.Unit("bounded-unit-" + index,
                null, "Controlled unit " + index, template.Anchors, template.Sections)).ToImmutableArray(),
            Observations = [], Uncertainties = []
        };
        Assert.True(OctoberKnowledgePolicy.Validate(sourceProposal, new(fixture.Origin, 100)));
        var accepted = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(sourceProposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var freeze = new CorpusFreezeRequest(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()),
            fixture.Binding, "", [accepted.AcceptedFile!], [sourceProposal.Coverage]);
        freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        fixture.FreezeAuthority.Approve(freeze);
        var frozen = await fixture.Writer().FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        await using var context = fixture.Factory.CreateDbContext();
        var units = await context.CodeUnits.AsNoTracking().ToListAsync();
        var logicals = await context.LogicalDocuments.AsNoTracking().ToListAsync();
        var versions = await context.DocumentVersions.AsNoTracking().ToListAsync();
        var roots = units.Select(unit =>
        {
            var logical = logicals.Single(value => value.UnitId == unit.UnitId);
            var version = versions.Single(value => value.LogicalDocumentId == logical.LogicalDocumentId);
            return new FlowRootSelection(unit.UnitId, new(fixture.Binding.BatchId, logical.LogicalDocumentId, version.VersionId), FlowRootKind.SelectedCodeUnit, unit.SemanticSelector);
        }).ToImmutableArray();
        var loaded = await new OctoberFlowPlanningInputLoader(fixture.Factory, fixture.Authorization).LoadAsync(frozen.Corpus!, roots);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        var input = loaded.Input!;
        var groups = OctoberFlowPlanner.Plan(input).Groups;
        Assert.Equal(rootCount, groups.Length);
        // This is intentionally amplified but individually policy-valid provider-shaped output.
        // It is not source/provider usefulness qualification.
        // Derived validation results contain both ContentJson and qualified section text.
        // Keep each doubled representation under 1 MiB while twenty proposals exceed replay's aggregate bound.
        string amplifiedText = new('x', 450_000);
        var completed = roots.Select(root =>
        {
            var proposal = new BusinessSynthesisProposal("受控业务说明",
                [new("main", 0, "业务操作", [new("action", amplifiedText, ClaimQualification.SourceSupported,
                    [new(root.Document, "overview", "action")], [])])]);
            var group = groups.Single(value => value.RootVersion == root.Document);
            Assert.Equal(BusinessSynthesisValidationStatus.Succeeded, OctoberBusinessSynthesisPolicy.Validate(input, group.GroupVersionId, proposal).Status);
            return new FlowSynthesisSubmission(root, group.GroupVersionId, proposal);
        }).ToImmutableArray();
        var operation = new OperationKey(fixture.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        var selection = new BusinessFinalizationSelection(roots, new(), Guid.NewGuid(), "controlled-profile",
            OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(new("controlled-host", 0, "controlled-profile", "openai", "https://unused.invalid", "controlled-model", "controlled-reference")),
            OctoberKnowledgePolicy.Hash("Controlled bounded synthesis instructions."), 40);
        string payloadDigest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(new(operation, frozen.Corpus!, "", new FinalizationForbiddenScope()) { Selection = selection });
        var request = new FlowFinalizationPublicationRequest(operation, frozen.Corpus!, payloadDigest, selection, completed, [], "");
        request = request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };
        // SQLite's shared stored-read budget conservatively reserves four bytes per character.
        Assert.True(4L * OctoberKnowledgePolicy.SerializeCanonical(request).Length > 32L * 1024 * 1024);
        int submissions = fixture.Guard.Submissions;

        var result = await new OctoberFlowPublicationStore(fixture.Factory, fixture.Authorization, fixture.Guard, fixture.Log).PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.Failed, result.Status);
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.PersistenceStatus);
        Assert.Null(result.Publication);
        Assert.Equal(submissions, fixture.Guard.Submissions);
        Assert.Equal(rootCount, await context.DocumentVersions.CountAsync());
        Assert.Equal(0, await context.LogicalDocuments.CountAsync(value => value.Kind == DocumentKind.BusinessLogic));
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
        Assert.Equal(0, await context.KnowledgeOperations.CountAsync(value => value.Kind == OperationKind.FinalizeCorpus));
        foreach (string table in _flowTables)
            Assert.Equal(0, await context.Database.SqlQueryRaw<long>($"SELECT COUNT(*) AS \"Value\" FROM \"{table}\"").SingleAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FinalizeAsync_RealOwnerWithValidAndUnsupportedRoot_PublishesPartialWithExactNoGroupDispositionAsync()
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var original = fixture.Request();
        var unplanned = fixture.Root with { Kind = FlowRootKind.Handler };
        var selection = original.Selection with { Roots = [fixture.Root, unplanned] };
        var planned = OctoberFlowPlanner.Plan(fixture.Input with { Roots = selection.Roots }, selection.Limits);
        Assert.Equal(FlowPlanningStatus.Succeeded, planned.Status);
        var group = Assert.Single(planned.Groups);
        Assert.Equal(fixture.Group.GroupVersionId, group.GroupVersionId);
        Assert.Contains(planned.RootDispositions, disposition => disposition.Root == unplanned && disposition.Kind == FlowRootDispositionKind.UnsupportedKind);
        await using var host = new OctoberExecutionScope(selection.RunId, selection.ProviderProfileId, new OctoberScanPolicy
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 1_000_000,
            SourceRules = [new(fixture.Input.Corpus.Documents[0].RepositoryId, "src", "selected", "Controlled component qualification")]
        }, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), original.Operation);
        var request = new FinalizationRequest(original.Operation, original.Corpus, "", host) { Selection = selection };
        request = request with { CanonicalPayloadDigest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(request) };
        // The slot models original-run single flight; it does not claim actual Scan slot issuance.
        using var slot = new FinalizerControlledSlot(request);
        ResolutionClient? retainedClient = null;
        using var client = retainedClient = new ResolutionClient(selection.ProviderProfileId, (call, cancellation) =>
        {
            if (call == 1) return Task.FromResult(SynthesisCase.JsonResponse(JsonSerializer.Serialize(original.Completed[0].Proposal)));
            Assert.Equal(2, call);
            var messages = retainedClient!.Requests[^1].Messages;
            Assert.Equal(new[] { ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User }, messages.Select(message => message.Role));
            using var review = JsonDocument.Parse(messages.Last(message => message.Role == ChatRole.User).Text);
            Assert.Equal("semantic-review", review.RootElement.GetProperty("phase").GetString());
            Assert.False(review.RootElement.TryGetProperty("candidate", out _));
            Assert.False(review.RootElement.TryGetProperty("packet", out _));
            var candidate = JsonSerializer.Deserialize<BusinessSynthesisProposal>(messages[^2].Text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(candidate), messages[^2].Text);
            Assert.Equal(OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(candidate)), review.RootElement.GetProperty("proposalDigest").GetString());
            return Task.FromResult(OctoberBusinessSynthesisTests.SemanticReviewForCandidate(fixture.Input, candidate));
        });
        var store = fixture.Store();
        var finalizer = new OctoberBusinessFinalizer(slot, store,
            new OctoberFlowPlanningInputLoader(fixture.Basis.Acceptance.Factory, fixture.Basis.Authorization),
            client, selection.ProviderProfileId,
            new("controlled-host", 0, "controlled-profile", "openai", "https://unused.invalid", "controlled-model", "controlled-reference"),
            "Controlled synthesis instructions.", new ResolutionDisclosure());

        var result = await finalizer.FinalizeAsync(request);

        Assert.Equal(BusinessFinalizationStatus.PublishedPartial, result.Status);
        Assert.NotNull(result.Publication);
        Assert.Equal(2, client.Calls);
        Assert.Equal(1, slot.Invocations);
        await using var context = fixture.Basis.Acceptance.Factory.CreateDbContext();
        var receipt = await context.BusinessFinalizations.AsNoTracking().SingleAsync();
        var submitted = JsonSerializer.Deserialize<FlowFinalizationPublicationRequest>(receipt.RequestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var completed = Assert.Single(submitted.Completed);
        var omitted = Assert.Single(submitted.Dispositions);
        Assert.Equal(fixture.Root, completed.Root);
        Assert.Equal(group.GroupVersionId, completed.GroupVersionId);
        Assert.Equal(unplanned, omitted.Root);
        Assert.Equal(FlowFinalizationDispositionKind.NotEvaluated, omitted.Kind);
        Assert.Null(omitted.GroupVersionId);
        Assert.False(string.IsNullOrWhiteSpace(omitted.Reason));
        Assert.True(selection.Roots.ToHashSet().SetEquals(new[] { completed.Root, omitted.Root }));
        Assert.Equal(OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(submitted), submitted.CanonicalOutcomeDigest);
        var retained = await new OctoberKnowledgePublishedSource(fixture.Basis.Acceptance.Factory).LoadAsync(result.Publication!);
        var bl = Assert.Single(retained!.Documents.Where(document => document.Kind == DocumentKind.BusinessLogic));
        var reader = fixture.Reader(result.Publication!);
        var opened = await reader.OpenReadSelectionAsync(fixture.ReadRequest(result.Publication!));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var page = await reader.ReadExactAsync(new(opened.Selection!, new(bl.Reference.BatchId, result.Publication!.PublicationId,
            bl.Reference.LogicalDocumentId, bl.Reference.DocumentVersionId)));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.True(page.Coverage!.IsPartial);
        Assert.NotEmpty(page.Coverage.PermittedGaps);
        Assert.DoesNotContain(omitted.Reason, string.Join("\n", page.Coverage.PermittedGaps), StringComparison.Ordinal);
        var replay = await finalizer.FinalizeAsync(request);
        Assert.Equal(result.Publication, replay.Publication);
        Assert.Equal(result.Status, replay.Status);
        Assert.Equal(2, client.Calls);
        Assert.Equal(1, slot.Invocations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("fake-group")]
    [InlineData("failed")]
    [InlineData("completed")]
    public async Task PublishAsync_UnplannedRootCannotAcquireFabricatedGroupOrAttemptOutcomeAsync(string fabricated)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var original = fixture.Request();
        var unplanned = fixture.Root with { Kind = FlowRootKind.Handler };
        var selection = original.Selection with { Roots = [fixture.Root, unplanned] };
        string payload = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(new(original.Operation, original.Corpus, "", new FinalizationForbiddenScope()) { Selection = selection });
        var disposition = new FlowFinalizationDisposition(unplanned, null, FlowFinalizationDispositionKind.NotEvaluated, "Unsupported bounded root.");
        var completed = original.Completed;
        if (fabricated == "fake-group") disposition = disposition with { GroupVersionId = fixture.Group.GroupVersionId };
        if (fabricated == "failed") disposition = disposition with { Kind = FlowFinalizationDispositionKind.Failed };
        if (fabricated == "completed") completed = completed.Add(new(unplanned, fixture.Group.GroupVersionId, original.Completed[0].Proposal));
        var request = new FlowFinalizationPublicationRequest(original.Operation, original.Corpus, payload, selection,
            completed, fabricated == "completed" ? [] : [disposition], "");
        request = request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };
        int submissions = fixture.Basis.Acceptance.Guard.Submissions;

        var result = await fixture.Store().PublishAsync(request);

        Assert.Equal(BusinessFinalizationStatus.Failed, result.Status);
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.PersistenceStatus);
        Assert.Null(result.Publication);
        Assert.Equal(submissions, fixture.Basis.Acceptance.Guard.Submissions);
        await AssertNoFlowMutationAsync(fixture);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unit")]
    [InlineData("version")]
    [InlineData("selector")]
    public async Task LoadAsync_UnsupportedKindCannotHideInvalidExactRootIdentityAsync(string invalid)
    {
        await using var fixture = await FlowPublicationCase.CreateAsync();
        var root = fixture.Root with { Kind = FlowRootKind.Handler };
        root = invalid switch
        {
            "unit" => root with { UnitId = Guid.NewGuid() },
            "version" => root with { Document = root.Document with { DocumentVersionId = Guid.NewGuid() } },
            _ => root with { Selector = "not-an-accepted-unit-selector" }
        };

        var loaded = await new OctoberFlowPlanningInputLoader(fixture.Basis.Acceptance.Factory, fixture.Basis.Authorization)
            .LoadAsync(fixture.Input.Corpus.Handle, [root]);

        Assert.Equal(KnowledgeReadStatus.Unavailable, loaded.Status);
        Assert.Null(loaded.Input);
        await AssertNoFlowMutationAsync(fixture);
    }

    private static void AssertUnavailableInitial(BusinessFinalizationInitialRead result)
    {
        Assert.Equal(BusinessFinalizationInitialReadKind.Unavailable, result.Kind);
        Assert.Null(result.Result);
    }

    private static async Task AssertNoFlowMutationAsync(FlowPublicationCase fixture)
    {
        await using var context = fixture.Basis.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.LogicalDocuments.CountAsync(document => document.Kind == DocumentKind.BusinessLogic));
        Assert.Equal(3, await context.DocumentVersions.CountAsync());
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(0, await context.KnowledgeOperations.CountAsync(operation => operation.Kind == OperationKind.FinalizeCorpus));
        foreach (string table in _flowTables)
            Assert.Equal(0, await context.Database.SqlQueryRaw<long>($"SELECT COUNT(*) AS \"Value\" FROM \"{table}\"").SingleAsync());
    }
}

/// <summary>Uses existing real writers/loader and controlled proposals. It grants no replacement acceptance or provider capability.</summary>
internal sealed class FlowPublicationCase : IAsyncDisposable
{
    private FlowPublicationCase(ResolutionPersistenceFixture basis, FlowPlanningInput input, FlowRootSelection root)
    {
        Basis = basis;
        Input = input;
        Root = root;
        Group = OctoberFlowPlanner.Plan(input).Groups.Single(group => group.RootVersion == root.Document);
        Operation = new(input.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
    }
    public ResolutionPersistenceFixture Basis { get; }
    public FlowPlanningInput Input { get; }
    public FlowRootSelection Root { get; }
    public FlowGroupVersion Group { get; }
    public OperationKey Operation { get; }
    public Guid RunId { get; } = Guid.NewGuid();
    public OctoberFlowPublicationStore Store(IOctoberKnowledgeProducerAuthorization? authorization = null) =>
        new(Basis.Acceptance.Factory, authorization ?? Basis.Authorization, Basis.Acceptance.Guard, Basis.Acceptance.Log);

    public static async Task<FlowPublicationCase> CreateAsync(bool twoRoots = false)
    {
        var basis = await ResolutionPersistenceFixture.CreateAsync();
        try
        {
            Assert.Equal(KnowledgeWriteStatus.Committed, (await basis.Store().AcceptOutcomeAsync(basis.Request())).Status);
            await using var context = basis.Acceptance.Factory.CreateDbContext();
            var document = basis.Corpus.Documents[0].Version;
            var logical = await context.LogicalDocuments.SingleAsync(value => value.LogicalDocumentId == document.LogicalDocumentId);
            var unit = await context.CodeUnits.SingleAsync(value => value.UnitId == logical.UnitId);
            var root = new FlowRootSelection(unit.UnitId, document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector);
            ImmutableArray<FlowRootSelection> roots = [root];
            if (twoRoots)
            {
                var otherDocument = basis.Corpus.Documents[1].Version;
                var otherLogical = await context.LogicalDocuments.SingleAsync(value => value.LogicalDocumentId == otherDocument.LogicalDocumentId);
                var otherUnit = await context.CodeUnits.SingleAsync(value => value.UnitId == otherLogical.UnitId);
                roots = roots.Add(new(otherUnit.UnitId, otherDocument, FlowRootKind.SelectedCodeUnit, otherUnit.SemanticSelector));
            }
            var loaded = await new OctoberFlowPlanningInputLoader(basis.Acceptance.Factory, basis.Authorization).LoadAsync(basis.Corpus.Handle, roots);
            Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
            return new(basis, Assert.IsType<FlowPlanningInput>(loaded.Input), root);
        }
        catch { await basis.DisposeAsync(); throw; }
    }

    public FlowFinalizationPublicationRequest Request(bool emptyRoots = false, bool partial = false)
    {
        var selection = new BusinessFinalizationSelection(emptyRoots ? [] : Input.Roots, new(), RunId, "controlled-profile",
            OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(new("controlled-host", 0, "controlled-profile", "openai", "https://unused.invalid", "controlled-model", "controlled-reference")),
            OctoberKnowledgePolicy.Hash("Controlled synthesis instructions."), 40);
        var finalization = new FinalizationRequest(Operation, Input.Corpus.Handle, "", new FinalizationForbiddenScope()) { Selection = selection };
        var groups = OctoberFlowPlanner.Plan(Input).Groups;
        BusinessSynthesisProposal Proposal(FlowRootSelection root) => new("业务处理说明",
            [new("business-main", 0, "处理过程", [new("business-action", "满足处理条件后，请求会转交给后续处理环节。", ClaimQualification.SourceSupported,
                [new(root.Document, "operation", "action")], [])])]);
        var completed = selection.Roots.Where(root => !partial || root == Root)
            .Select(root => new FlowSynthesisSubmission(root, groups.Single(group => group.RootVersion == root.Document).GroupVersionId, Proposal(root))).ToImmutableArray();
        ImmutableArray<FlowFinalizationDisposition> dispositions = partial ? selection.Roots.Where(root => root != Root)
            .Select(root => new FlowFinalizationDisposition(root, groups.Single(group => group.RootVersion == root.Document).GroupVersionId,
                FlowFinalizationDispositionKind.Failed, "Controlled synthesis attempt failed.")).ToImmutableArray() : [];
        var request = new FlowFinalizationPublicationRequest(Operation, Input.Corpus.Handle,
            OctoberBusinessFinalizationPolicy.ComputeRequestDigest(finalization), selection,
            completed, dispositions, "");
        return request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };
    }

    public OctoberKnowledgeReader Reader(PublicationBinding publication)
    {
        var protector = new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        return new(new OctoberKnowledgePublishedSource(Basis.Acceptance.Factory), publication, new B0bReaderDisclosure(),
            new OctoberKnowledgeCursorProtector(protector, new B0bReadClock()), new B0bReadLog());
    }
    public ReadSelectionRequest ReadRequest(PublicationBinding publication) =>
        new(publication, new("ordinary-user", "controlled-stamp"), KnowledgeMode.Business, KnowledgePurpose.Reader,
            Input.Corpus.Documents.Select(document => document.RepositoryId).Distinct().ToImmutableArray());
    public async Task<string> ClSnapshotAsync()
    {
        await using var context = Basis.Acceptance.Factory.CreateDbContext();
        var versions = await (from version in context.DocumentVersions.AsNoTracking()
                              join logical in context.LogicalDocuments.AsNoTracking()
                                  on new { version.BatchId, version.LogicalDocumentId } equals new { logical.BatchId, logical.LogicalDocumentId }
                              where logical.Kind == DocumentKind.CodeLogic
                              orderby version.VersionId
                              select version).ToListAsync();
        return OctoberKnowledgePolicy.SerializeCanonical(versions);
    }
    public ValueTask DisposeAsync() => Basis.DisposeAsync();
}

internal sealed class FlowPublicationAuthority : IOctoberKnowledgeProducerAuthorization
{
    public Contributor? DeniedContributor { get; init; }
    public bool Unavailable { get; init; }
    public bool Blocked { get; set; } = true;
    public List<ImmutableArray<Contributor>> Seen { get; } = [];
    public Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Seen.Add(contributors);
        return Task.FromResult(Blocked && Unavailable ? DisclosureStatus.AuthorityUnavailable :
            Blocked && DeniedContributor is { } denied && contributors.Contains(denied) ? DisclosureStatus.Denied : DisclosureStatus.Allowed);
    }
}

internal sealed record FlowDocumentDependencyRow(Guid LogicalDocumentId, Guid VersionId, bool IsMember);
internal sealed record FlowFileDependencyRow(Guid BatchId, Guid ManifestId, string Key);

/// <summary>Owned SQLite setup snapshot and real acceptance/freeze; no live provider or management context.</summary>
internal sealed class SynthesisAdvisoryPersistenceCase : IAsyncDisposable
{
    public const string SystemText = "项目背景：订单服务为受理与后续核验提供协调。This is advisory, not source proof.\n";
    private SynthesisAdvisoryPersistenceCase(OctoberKnowledgeAcceptanceFixture acceptance, CorpusHandle corpus,
        FlowRootSelection root, Contributor systemContributor, ImmutableArray<Contributor> contributors)
    {
        Acceptance = acceptance;
        Corpus = corpus;
        Root = root;
        SystemContributor = systemContributor;
        Contributors = contributors;
    }
    public OctoberKnowledgeAcceptanceFixture Acceptance { get; }
    public CorpusHandle Corpus { get; }
    public FlowRootSelection Root { get; }
    public Contributor SystemContributor { get; }
    public ImmutableArray<Contributor> Contributors { get; }
    public FlowFinalizationPublicationRequest Request(FlowPlanningInput input, BusinessAdvisoryContextOverride? contextOverride = null)
    {
        var operation = new OperationKey(Acceptance.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
        var selection = new BusinessFinalizationSelection([Root], new(), Guid.NewGuid(), "controlled-profile",
            OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(new("controlled-host", 0, "controlled-profile", "openai",
                "https://unused.invalid", "controlled-model", "controlled-reference")),
            OctoberKnowledgePolicy.Hash("Controlled synthesis instructions."), 40) { ContextOverride = contextOverride };
        var proposal = new BusinessSynthesisProposal("受理过程", [new("main", 0, "操作说明",
            [new("action", "根据已接受的证据获取订单锁。", ClaimQualification.SourceSupported, [new(Root.Document, "overview", "action")], [])])]);
        var group = Assert.Single(OctoberFlowPlanner.Plan(input).Groups);
        var request = new FlowFinalizationPublicationRequest(operation, Corpus,
            OctoberBusinessFinalizationPolicy.ComputeRequestDigest(new(operation, Corpus, "", new FinalizationForbiddenScope()) { Selection = selection }),
            selection, [new(Root, group.GroupVersionId, proposal)], [], "");
        return request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };
    }
    public static async Task<SynthesisAdvisoryPersistenceCase> CreateAsync(bool includeSystemContributor,
        bool exactRepositoryIdentity = false, DbContextOptions<OctoberDbContext>? options = null)
    {
        var acceptance = options is null ? await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync()
            : await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
        try
        {
            var systemId = OctoberKnowledgeAcceptanceFixture.Id(97101);
            var system = new Contributor(ContributorKind.SystemAdvisory, null, systemId, null, systemId,
                OctoberKnowledgePolicy.Hash(SystemText));
            await using (var setup = acceptance.Factory.CreateDbContext())
            {
                (await setup.RepositorySelections.SingleAsync()).SystemId = systemId;
                setup.ContextReferences.Add(new() { BatchId = acceptance.Binding.BatchId, Scope = 1, RecordId = systemId });
                setup.AdvisoryEntries.Add(new() { BatchId = acceptance.Binding.BatchId, RepositoryId = acceptance.Origin.RepositoryId,
                    Scope = 1, OriginRecordId = systemId, Value = SystemText, Applicability = 0, EvidenceKind = 0 });
                if (exactRepositoryIdentity)
                {
                    setup.ContextReferences.Remove(await setup.ContextReferences.SingleAsync(value => value.Scope == 2));
                    setup.ContextReferences.Add(new() { BatchId = acceptance.Binding.BatchId, Scope = 2, RecordId = acceptance.Origin.RepositoryId });
                    (await setup.AdvisoryEntries.SingleAsync(value => value.Scope == 2)).OriginRecordId = acceptance.Origin.RepositoryId;
                }
                await setup.SaveChangesAsync();
            }
            var proposal = acceptance.Proposal with { Observations = [], Uncertainties = [] };
            if (exactRepositoryIdentity)
            {
                var exact = acceptance.AdvisoryContributor with { AdvisoryRecordId = acceptance.Origin.RepositoryId };
                proposal = proposal with
                {
                    Contributors = proposal.Contributors.Replace(acceptance.AdvisoryContributor, exact),
                    Units = proposal.Units.Select(unit => OctoberKnowledgeAcceptanceFixture.Unit(unit.SemanticSelector,
                        unit.ParentSelector, unit.Title, unit.Anchors, unit.Sections.Select(section => section with
                        {
                            Claims = section.Claims.Select(claim => claim with
                            {
                                Evidence = claim.Evidence.Select(evidence => evidence.AdvisoryRecordId == acceptance.AdvisoryContributor.AdvisoryRecordId
                                    ? evidence with { AdvisoryRecordId = acceptance.Origin.RepositoryId } : evidence).ToImmutableArray()
                            }).ToImmutableArray()
                        }).ToImmutableArray())).ToImmutableArray()
                };
            }
            if (includeSystemContributor) proposal = proposal with { Contributors = proposal.Contributors.Add(system) };
            var accepted = await acceptance.Writer().AcceptCompletedFileAsync(acceptance.Seal(proposal));
            Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
            var freeze = new CorpusFreezeRequest(new(acceptance.Binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()),
                acceptance.Binding, "", [accepted.AcceptedFile!], [proposal.Coverage]);
            freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
            acceptance.FreezeAuthority.Approve(freeze);
            var frozen = await acceptance.Writer().FreezeCorpusAsync(freeze);
            Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
            await using var context = acceptance.Factory.CreateDbContext();
            var unit = await context.CodeUnits.SingleAsync(value => value.SemanticSelector == "operation:order");
            var logical = await context.LogicalDocuments.SingleAsync(value => value.UnitId == unit.UnitId);
            var version = await context.DocumentVersions.SingleAsync(value => value.LogicalDocumentId == logical.LogicalDocumentId);
            var root = new FlowRootSelection(unit.UnitId, new(acceptance.Binding.BatchId, logical.LogicalDocumentId, version.VersionId),
                FlowRootKind.SelectedCodeUnit, unit.SemanticSelector);
            return new(acceptance, frozen.Corpus!, root, system, proposal.Contributors);
        }
        catch { await acceptance.DisposeAsync(); throw; }
    }
    public ValueTask DisposeAsync() => Acceptance.DisposeAsync();
}
