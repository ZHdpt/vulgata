using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;

namespace Vulgata.Web.Explore;

public sealed partial class ExplorePresentationService
{
    public async Task<ExploreGraphResult> LoadGraphAsync(CancellationToken cancellationToken = default)
    {
        // Traverse the publication in bounded requests; the caller owns the overall lifetime.
        var token = cancellationToken;
        var generation = Volatile.Read(ref _contextGeneration);
        try
        {
            token.ThrowIfCancellationRequested();
            if (_reader is null || _disclosedReader is null || _dependencyAuthority is null || _disposed)
            {
                return GraphFailure(KnowledgeReadStatus.UnavailableService);
            }

            var context = await ObserveContextAsync(token).WaitAsync(token);
            if (context is null || generation != Volatile.Read(ref _contextGeneration))
            {
                return GraphFailure(KnowledgeReadStatus.Unavailable);
            }

            // Host-controlled derivation creates a NEW Graph-purpose owner selection, never reuses a Reader token.
            var graphSelection = context.Selection with { Purpose = KnowledgePurpose.Graph };
            var opened = await _reader.OpenReadSelectionAsync(graphSelection, token).WaitAsync(token);
            if (opened.Status != KnowledgeReadStatus.Success || string.IsNullOrWhiteSpace(opened.Selection?.Token) ||
                !await IsCurrentAsync(context, generation, token).WaitAsync(token))
            {
                return GraphFailure(KnowledgeReadStatus.Unavailable);
            }

            var encoder = new ExploreGraphOutputEncoder();
            const long remainingBytes = KnowledgeContract.MaximumContentBytes;
            var capsules = ImmutableArray.CreateBuilder<KnowledgeDependencyHandle>();
            var seedPages = ImmutableArray.CreateBuilder<SearchPage>();
            var relatedPages = ImmutableArray.CreateBuilder<RelatedPage>();
            const int pageLimit = KnowledgeContract.MaximumPageSize;
            ContinuationToken? cursor = null;
            var cursors = new HashSet<string>(StringComparer.Ordinal);
            do
            {
                if (!await IsCurrentAsync(context, generation, token).WaitAsync(token))
                    return GraphFailure(KnowledgeReadStatus.Unavailable);
                var result = await _disclosedReader.SearchAsync(new(opened.Selection, "", pageLimit, cursor),
                    new(KnowledgeContract.MaximumContentBytes, remainingBytes, encoder), token).WaitAsync(token);
                if (result.Status != KnowledgeReadStatus.Success || result.Disclosure is not { } disclosure ||
                    !ValidGraphSearch(disclosure.Page, graphSelection, pageLimit) ||
                    !ValidGraphEnvelope(disclosure, encoder.EncodeSearch(disclosure.Page), encoder.ProfileId, remainingBytes))
                    return GraphFailure(KnowledgeReadStatus.Unavailable);
                capsules.Add(disclosure.Dependencies);
                seedPages.Add(disclosure.Page with { Next = null });
                cursor = disclosure.Page.Next;
                if (cursor is not null && !cursors.Add(cursor.Value))
                    return GraphFailure(KnowledgeReadStatus.Unavailable);
            } while (cursor is not null);

            foreach (var seed in seedPages.SelectMany(page => page.Documents).DistinctBy(document => document.Handle))
            {
                cursor = null;
                cursors.Clear();
                do
                {
                    if (!await IsCurrentAsync(context, generation, token).WaitAsync(token))
                        return GraphFailure(KnowledgeReadStatus.Unavailable);
                    var result = await _disclosedReader.GetRelatedAsync(new(opened.Selection, seed.Handle, pageLimit, cursor),
                        new(KnowledgeContract.MaximumContentBytes, remainingBytes, encoder), token).WaitAsync(token);
                    if (result.Status != KnowledgeReadStatus.Success || result.Disclosure is not { } disclosure ||
                        !ValidGraphRelated(disclosure.Page, graphSelection, pageLimit) ||
                        !ValidGraphEnvelope(disclosure, encoder.EncodeRelated(disclosure.Page), encoder.ProfileId, remainingBytes))
                        return GraphFailure(KnowledgeReadStatus.Unavailable);
                    capsules.Add(disclosure.Dependencies);
                    relatedPages.Add(disclosure.Page with { Next = null });
                    cursor = disclosure.Page.Next;
                    if (cursor is not null && !cursors.Add(cursor.Value))
                        return GraphFailure(KnowledgeReadStatus.Unavailable);
                } while (cursor is not null);
            }

            var graph = KnowledgeGraphProjector.Project(seedPages.ToImmutable(), relatedPages.ToImmutable(), false);
            if (!await IsCurrentAsync(context, generation, token).WaitAsync(token))
            {
                return GraphFailure(KnowledgeReadStatus.Unavailable);
            }

            // Recheck every acquired proof without imposing an aggregate graph-size limit.
            foreach (var batch in capsules.Chunk(OctoberKnowledgeDisclosureLimits.MaximumOriginGroups))
            {
                var authorized = await ReauthorizeGraphBatchAsync(graphSelection, batch.ToImmutableArray(), token);
                token.ThrowIfCancellationRequested();
                if (authorized != KnowledgeDependencyStatus.Succeeded ||
                    !await IsCurrentAsync(context, generation, token).WaitAsync(token))
                    return GraphFailure(KnowledgeReadStatus.Unavailable);
            }

            return new(KnowledgeReadStatus.Success, graph, context.Selection.Mode, context.IsControlledFixture);
        }
        catch (OperationCanceledException)
        {
            return GraphFailure(cancellationToken.IsCancellationRequested ? KnowledgeReadStatus.Cancelled : KnowledgeReadStatus.ReadBudgetExceeded);
        }
        catch (Exception)
        {
            return GraphFailure(KnowledgeReadStatus.UnavailableService);
        }
    }

    private static ExploreGraphResult GraphFailure(KnowledgeReadStatus status) => new(status, null);

    private async Task<KnowledgeDependencyStatus> ReauthorizeGraphBatchAsync(ReadSelectionRequest selection,
        ImmutableArray<KnowledgeDependencyHandle> batch, CancellationToken token)
    {
        var status = await _dependencyAuthority!.ReauthorizeAsync(selection, batch, token).WaitAsync(token);
        if (status != KnowledgeDependencyStatus.BudgetExceeded || batch.Length <= 1) return status;
        // Proofs may reach the owner's byte limit before its count limit. Split rather than drop any proof.
        var middle = batch.Length / 2;
        var left = await ReauthorizeGraphBatchAsync(selection, batch[..middle], token);
        return left == KnowledgeDependencyStatus.Succeeded
            ? await ReauthorizeGraphBatchAsync(selection, batch[middle..], token)
            : left;
    }

    private static bool ValidGraphEnvelope<T>(KnowledgeDisclosedPage<T> disclosure, ImmutableArray<byte> expectedBytes,
        string profile, long remainingBytes) where T : class =>
        disclosure.Dependencies is { SetId: var id } dependency && id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(dependency.BindingDigest) && !string.IsNullOrWhiteSpace(dependency.ProtectedPayload) &&
        ExplorePresentationText.IsWellFormed(dependency.ProtectedPayload) &&
        Encoding.UTF8.GetByteCount(dependency.ProtectedPayload) <= OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes &&
        !disclosure.OutputUtf8.IsDefaultOrEmpty && disclosure.OutputUtf8.Length <= KnowledgeContract.MaximumContentBytes &&
        disclosure.OutputUtf8.Length <= remainingBytes && disclosure.OutputProfileId == profile &&
        disclosure.OutputUtf8.AsSpan().SequenceEqual(expectedBytes.AsSpan()) &&
        disclosure.OutputDigest == Convert.ToHexString(SHA256.HashData(disclosure.OutputUtf8.AsSpan())) &&
        disclosure.PageDigest == ExploreGraphOutputEncoder.ComputePageDigest(disclosure.Page);

    private static bool ValidGraphSearch(SearchPage page, ReadSelectionRequest selection, int pageLimit) =>
        page is not null && page.Status == KnowledgeReadStatus.Success && !page.Documents.IsDefault && page.Documents.Length <= pageLimit &&
        page.Documents.All(document => ValidSummary(document, selection) &&
                                       (selection.Mode != KnowledgeMode.Business || document.QualifiedExcerpt.All(claim => claim.Evidence.IsEmpty))) &&
        ValidNext(page.Next, null) && ValidCoverage(page.Coverage);

    private static bool ValidGraphRelated(RelatedPage page, ReadSelectionRequest selection, int pageLimit) =>
        page is not null && page.Status == KnowledgeReadStatus.Success && !page.Relationships.IsDefault && !page.PermittedUncertainties.IsDefault &&
        page.Relationships.Length + page.PermittedUncertainties.Length <= pageLimit &&
        page.Relationships.All(edge => edge is not null && edge.RelationshipVersionId is { } id && id != Guid.Empty &&
                                       Enum.IsDefined(edge.Kind) && new ExploreDocumentHandleValidator().Validate(edge.From).IsValid &&
                                       new ExploreDocumentHandleValidator().Validate(edge.To).IsValid && Matches(edge.From, selection) && Matches(edge.To, selection) &&
                                       ExplorePresentationText.IsWellFormed(edge.Meaning)) &&
        page.PermittedUncertainties.All(uncertainty => uncertainty is not null && !string.IsNullOrWhiteSpace(uncertainty.Key) &&
                                                       ExplorePresentationText.IsWellFormed(uncertainty.Key) && ExplorePresentationText.IsWellFormed(uncertainty.Explanation) &&
                                                       !uncertainty.Limitations.IsDefault && uncertainty.Limitations.All(ExplorePresentationText.IsWellFormed) &&
                                                       !uncertainty.Evidence.IsDefault && uncertainty.Evidence.All(evidence => evidence is not null &&
                                                           (evidence.Source is null || ValidSource(evidence.Source))) &&
                                                       (selection.Mode != KnowledgeMode.Business || uncertainty.Evidence.IsEmpty)) &&
        ValidNext(page.Next, null) && ValidCoverage(page.Coverage);
}
