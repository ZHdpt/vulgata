using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Production acceptance/freeze/publication on owned SQLite with explicit controlled Scan inventory authority.</summary>
internal sealed class OctoberKnowledgePublicationFixture : IAsyncDisposable
{
    private OctoberKnowledgePublicationFixture(OctoberKnowledgeAcceptanceFixture acceptance, AcceptedFile accepted)
    {
        Acceptance = acceptance;
        Accepted = accepted;
    }
    public OctoberKnowledgeAcceptanceFixture Acceptance { get; }
    public AcceptedFile Accepted { get; }
    public CorpusHandle? Corpus { get; private set; }
    public PublicationBinding? Publication { get; private set; }
    public static async Task<OctoberKnowledgePublicationFixture> CreateAsync(bool zeroUnit = false, bool includeUncitedContributor = false)
    {
        var acceptance = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        try
        {
            var proposal = acceptance.Proposal;
            if (includeUncitedContributor) proposal = proposal with { Contributors = proposal.Contributors.Add(acceptance.UncitedContributor) };
            if (zeroUnit) proposal = proposal with { Units = [], Observations = [], Uncertainties = [], Coverage = proposal.Coverage with { Disposition = CoverageDisposition.CompletedZeroUnit, Reason = "Complete file has no semantic units" } };
            var accepted = await acceptance.Writer().AcceptCompletedFileAsync(acceptance.Seal(proposal));
            Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
            return new(acceptance, accepted.AcceptedFile!);
        }
        catch { await acceptance.DisposeAsync(); throw; }
    }
    public CorpusFreezeRequest FreezeRequest(bool includeGap = true)
    {
        ImmutableArray<FileCoverage> coverage = includeGap ? [Accepted.Coverage, new(OctoberKnowledgeAcceptanceFixture.Id(410), Acceptance.Origin with { RelativePath = "src/unprocessed.cs" }, CoverageDisposition.Unprocessed, "Stopped before dispatch")] : [Accepted.Coverage];
        var request = new CorpusFreezeRequest(new(Acceptance.Binding.BatchId, OperationKind.FreezeCorpus, OctoberKnowledgeAcceptanceFixture.Id(411)), Acceptance.Binding, "", [Accepted], coverage);
        return request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(request) };
    }
    public async Task<KnowledgeWriteResult> FreezeAsync(bool includeGap = true)
    {
        var request = FreezeRequest(includeGap);
        Acceptance.FreezeAuthority.Approve(request);
        var result = await Acceptance.Writer().FreezeCorpusAsync(request);
        Corpus = result.Corpus;
        return result;
    }
    public PublicationRequest PublicationRequest(bool includeUnknown = true)
    {
        Assert.NotNull(Corpus);
        var request = new PublicationRequest(new(Acceptance.Binding.BatchId, OperationKind.PublishSelection, OctoberKnowledgeAcceptanceFixture.Id(412)), Corpus!, "", Accepted.Documents, [], includeUnknown && !Accepted.Documents.IsEmpty ? Acceptance.Proposal.Uncertainties : [], [], FreezeRequest().TerminalCoverage);
        return request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(request) };
    }
    public async Task<KnowledgeWriteResult> PublishAsync(bool includeUnknown = true)
    {
        var result = await Acceptance.Writer().PublishSelectionAsync(PublicationRequest(includeUnknown));
        Publication = result.Publication;
        return result;
    }
    public ValueTask DisposeAsync() => Acceptance.DisposeAsync();
}
