using System.Collections.Immutable;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Actual SQLite B0/B1 acceptance, owner loading and publication; proposals and source/Scan authority remain controlled.</summary>
public sealed class OctoberFlowPublicationTests
{
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
}

/// <summary>Uses existing real writers/loader and controlled proposals. It grants no replacement acceptance or provider capability.</summary>
internal sealed class FlowPublicationCase : IAsyncDisposable
{
    private FlowPublicationCase(ResolutionPersistenceFixture basis, FlowPlanningInput input, FlowRootSelection root)
    {
        Basis = basis;
        Input = input;
        Root = root;
        Group = Assert.Single(OctoberFlowPlanner.Plan(input).Groups);
        Operation = new(input.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
    }
    public ResolutionPersistenceFixture Basis { get; }
    public FlowPlanningInput Input { get; }
    public FlowRootSelection Root { get; }
    public FlowGroupVersion Group { get; }
    public OperationKey Operation { get; }
    public Guid RunId { get; } = Guid.NewGuid();
    public OctoberFlowPublicationStore Store() => new(Basis.Acceptance.Factory, Basis.Authorization, Basis.Acceptance.Guard, Basis.Acceptance.Log);

    public static async Task<FlowPublicationCase> CreateAsync()
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
            var loaded = await new OctoberFlowPlanningInputLoader(basis.Acceptance.Factory, basis.Authorization).LoadAsync(basis.Corpus.Handle, [root]);
            Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
            return new(basis, Assert.IsType<FlowPlanningInput>(loaded.Input), root);
        }
        catch { await basis.DisposeAsync(); throw; }
    }

    public FlowFinalizationPublicationRequest Request(bool emptyRoots = false)
    {
        var selection = new BusinessFinalizationSelection(emptyRoots ? [] : [Root], new(), RunId, "controlled-profile",
            OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(new("controlled-host", 0, "controlled-profile", "openai", "https://unused.invalid", "controlled-model", "controlled-reference")),
            OctoberKnowledgePolicy.Hash("Controlled synthesis instructions."), 40);
        var finalization = new FinalizationRequest(Operation, Input.Corpus.Handle, "", new FinalizationForbiddenScope()) { Selection = selection };
        var proposal = new BusinessSynthesisProposal("业务处理说明",
            [new("business-main", 0, "处理过程", [new("business-action", "满足处理条件后，请求会转交给后续处理环节。", ClaimQualification.SourceSupported,
                [new(Root.Document, "operation", "action")], [])])]);
        var request = new FlowFinalizationPublicationRequest(Operation, Input.Corpus.Handle,
            OctoberBusinessFinalizationPolicy.ComputeRequestDigest(finalization), selection,
            emptyRoots ? [] : [new(Root, Group.GroupVersionId, proposal)], [], "");
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
