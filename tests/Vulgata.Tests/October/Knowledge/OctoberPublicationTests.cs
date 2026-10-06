using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberPublicationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishSelectionAsync_ExactClOnlyCorpus_PublishesAtomicMembershipUnknownAndHonestCoverageAsync()
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync();
        await using (var before = fixture.Acceptance.Factory.CreateDbContext()) Assert.Equal(0, await before.PublicationMembers.CountAsync());
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.FreezeAsync()).Status);
        var request = fixture.PublicationRequest();
        Assert.Empty(request.Relationships);
        Assert.Empty(request.Flows);
        var published = await fixture.PublishAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
        Assert.NotNull(published.Publication);
        Assert.Equal(fixture.Acceptance.Binding, published.Publication.Knowledge);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.CorpusSelections.CountAsync());
        Assert.Equal(1, await context.CorpusManifests.CountAsync());
        Assert.Equal(2, await context.CorpusDocuments.CountAsync());
        Assert.Equal(2, await context.CorpusCoverage.CountAsync());
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(2, await context.PublicationMembers.CountAsync());
        Assert.Equal(2, await context.PublicationCoverage.CountAsync());
        Assert.Equal(1, await context.PublicationUncertainties.CountAsync());
        Assert.Equal(fixture.Accepted.Documents.Select(document => document.DocumentVersionId).Order(), (await context.PublicationMembers.Select(member => member.VersionId).ToListAsync()).Order());
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(CorpusFreezeEligibility.AdmissionOpen, KnowledgeWriteStatus.NotCompleted)]
    [InlineData(CorpusFreezeEligibility.Unsettled, KnowledgeWriteStatus.CommitIndeterminate)]
    [InlineData(CorpusFreezeEligibility.Paused, KnowledgeWriteStatus.NotCompleted)]
    [InlineData(CorpusFreezeEligibility.HardStopped, KnowledgeWriteStatus.StaleOrFenced)]
    [InlineData(CorpusFreezeEligibility.Denied, KnowledgeWriteStatus.Denied)]
    [InlineData(CorpusFreezeEligibility.AuthorityUnavailable, KnowledgeWriteStatus.AuthorityUnavailable)]
    public async Task FreezeCorpusAsync_HostDoesNotAttestSettlement_RejectsEvenWithCompleteCallerInventoryAsync(CorpusFreezeEligibility eligibility, KnowledgeWriteStatus expected)
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync();
        var request = fixture.FreezeRequest();
        fixture.Acceptance.FreezeAuthority.Approve(request);
        fixture.Acceptance.FreezeAuthority.Eligibility = eligibility;
        var result = await fixture.Acceptance.Writer().FreezeCorpusAsync(request);
        Assert.Equal(expected, result.Status);
        Assert.Null(result.Corpus);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.CorpusSelections.CountAsync());
        Assert.Equal(1, fixture.Acceptance.Guard.Submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("omitted-manifest")]
    [InlineData("forged-version")]
    [InlineData("missing-coverage")]
    [InlineData("duplicate-coverage")]
    [InlineData("changed-accepted-coverage")]
    public async Task FreezeCorpusAsync_InexactAcceptedSetOrCoverage_RejectsAtomicallyAsync(string mutation)
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync();
        var request = fixture.FreezeRequest();
        request = mutation switch
        {
            "omitted-manifest" => request with { AcceptedFiles = [] },
            "forged-version" => request with { AcceptedFiles = [fixture.Accepted with { Documents = [fixture.Accepted.Documents[0] with { DocumentVersionId = Guid.NewGuid() }] }] },
            "missing-coverage" => request with { TerminalCoverage = request.TerminalCoverage.RemoveAt(0) },
            "duplicate-coverage" => request with { TerminalCoverage = request.TerminalCoverage.Add(request.TerminalCoverage[0]) },
            _ => request with { TerminalCoverage = request.TerminalCoverage.SetItem(0, request.TerminalCoverage[0] with { Origin = fixture.Acceptance.Origin with { SourceDigest = new string('f', 64) } }) }
        };
        request = request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(request) };
        fixture.Acceptance.FreezeAuthority.Approve(request);
        var result = await fixture.Acceptance.Writer().FreezeCorpusAsync(request);
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.Status);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.CorpusSelections.CountAsync());
        Assert.Equal(0, await context.CorpusCoverage.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FreezeCorpusAsync_CommittedCorpus_BlocksLaterFileAcceptanceAndPreservesExactReplayAsync()
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync();
        var frozen = await fixture.FreezeAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        var late = fixture.Acceptance.Proposal;
        Guid admission = Guid.NewGuid();
        late = late with { AdmissionId = admission, AttemptId = Guid.NewGuid(), Units = [], Observations = [], Uncertainties = [], Coverage = late.Coverage with { AdmissionId = admission, Disposition = CoverageDisposition.CompletedZeroUnit, Reason = "Late completed file" } };
        var rejected = await fixture.Acceptance.Writer().AcceptCompletedFileAsync(fixture.Acceptance.Seal(late, Guid.NewGuid()));
        Assert.DoesNotContain(rejected.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        fixture.Acceptance.Guard.Fenced = true;
        var replay = await fixture.Acceptance.Writer().FreezeCorpusAsync(fixture.FreezeRequest());
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(frozen.Corpus, replay.Corpus);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishSelectionAsync_OnlyZeroUnitCoverage_DoesNotClaimReadableClAsync()
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync(zeroUnit: true);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.FreezeAsync()).Status);
        var result = await fixture.PublishAsync(includeUnknown: false);
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, result.Status);
        Assert.Null(result.Publication);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
        Assert.Equal(1, await context.CorpusCoverage.CountAsync(coverage => coverage.AdmissionId == fixture.Accepted.AdmissionId));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("foreign-version")]
    [InlineData("duplicate-version")]
    [InlineData("changed-corpus-digest")]
    [InlineData("invented-unknown")]
    public async Task PublishSelectionAsync_ForeignOrIncompleteMembership_RejectsWithoutPublicationAsync(string mutation)
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.FreezeAsync()).Status);
        var request = fixture.PublicationRequest();
        request = mutation switch
        {
            "foreign-version" => request with { Documents = [request.Documents[0] with { DocumentVersionId = Guid.NewGuid() }] },
            "duplicate-version" => request with { Documents = request.Documents.Add(request.Documents[0]) },
            "changed-corpus-digest" => request with { Corpus = request.Corpus with { CorpusDigest = new string('f', 64) } },
            _ => request with { Uncertainties = [request.Uncertainties[0] with { Explanation = "Invented conclusion" }] }
        };
        request = request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(request) };
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, (await fixture.Acceptance.Writer().PublishSelectionAsync(request)).Status);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
        Assert.Equal(0, await context.PublicationMembers.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishSelectionAsync_ReplayConflictAndStagingFence_PreserveImmutableExactPublicationAsync()
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.FreezeAsync()).Status);
        var request = fixture.PublicationRequest();
        fixture.Acceptance.Guard.BeforeSubmission = () => fixture.Acceptance.Guard.Fenced = true;
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await fixture.Acceptance.Writer().PublishSelectionAsync(request)).Status);
        await using (var before = fixture.Acceptance.Factory.CreateDbContext()) Assert.Equal(0, await before.PublicationMembers.CountAsync());
        fixture.Acceptance.Guard.Fenced = false;
        fixture.Acceptance.Guard.BeforeSubmission = null;
        var committed = await fixture.Acceptance.Writer().PublishSelectionAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, committed.Status);
        var replay = await fixture.Acceptance.Writer().PublishSelectionAsync(request);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(committed.Publication, replay.Publication);
        var changed = request with { Uncertainties = [] };
        changed = changed with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(changed) };
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await fixture.Acceptance.Writer().PublishSelectionAsync(changed)).Status);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(1, await context.PublicationUncertainties.CountAsync());
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishSelectionAsync_ExplicitNonemptySubset_PreservesExactMembershipAndCompleteAnalysisCoverageAsync()
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.FreezeAsync()).Status);
        var request = fixture.PublicationRequest() with { Documents = [fixture.Accepted.Documents[0]], Uncertainties = [] };
        request = request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(request) };
        var result = await fixture.Acceptance.Writer().PublishSelectionAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        var member = await context.PublicationMembers.SingleAsync();
        Assert.Equal(fixture.Accepted.Documents[0].DocumentVersionId, member.VersionId);
        Assert.Equal(2, await context.PublicationCoverage.CountAsync());
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
    }
}
