using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;

namespace Vulgata.Web.Explore;

public sealed partial class ExplorePresentationService
{
    private const int MaximumGraphAcquisitions = 6;
    private static readonly TimeSpan _graphDeadline = TimeSpan.FromSeconds(10);

    public async Task<ExploreGraphResult> LoadGraphAsync(CancellationToken cancellationToken = default)
    {
        // This deadline includes trusted-context observations as well as all eight graph protocol calls.
        using var timeout = new CancellationTokenSource(_graphDeadline, _timeProvider);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var token = lifetime.Token;
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
            long remainingBytes = MaximumGraphAcquisitions * KnowledgeContract.MaximumContentBytes;
            var capsules = ImmutableArray.CreateBuilder<KnowledgeDependencyHandle>();
            var relatedPages = ImmutableArray.CreateBuilder<RelatedPage>();
            var seedLimit = Math.Min(KnowledgeContract.MaximumPageSize, KnowledgeGraphProjector.MaximumNodes + 1);
            var relatedLimit = Math.Min(KnowledgeContract.MaximumPageSize, KnowledgeGraphProjector.MaximumEdges + 1);
            var seedsResult = await _disclosedReader.SearchAsync(new(opened.Selection, "", seedLimit),
                new(KnowledgeContract.MaximumContentBytes, remainingBytes, encoder), token).WaitAsync(token);
            if (seedsResult.Status != KnowledgeReadStatus.Success || seedsResult.Disclosure is not { } seedDisclosure ||
                !ValidGraphSearch(seedDisclosure.Page, graphSelection, seedLimit) ||
                !ValidGraphEnvelope(seedDisclosure, encoder.EncodeSearch(seedDisclosure.Page), encoder.ProfileId, remainingBytes))
            {
                return GraphFailure(KnowledgeReadStatus.Unavailable);
            }

            // Retain the whole page's dependency even when the sentinel item is not displayed.
            capsules.Add(seedDisclosure.Dependencies);
            remainingBytes -= seedDisclosure.OutputUtf8.Length;
            var seeds = seedDisclosure.Page;
            var displayedSeeds = seeds.Documents.DistinctBy(document => document.Handle).Take(KnowledgeGraphProjector.MaximumNodes).ToArray();
            foreach (var seed in displayedSeeds.Take(MaximumGraphAcquisitions - 1))
            {
                if (!await IsCurrentAsync(context, generation, token).WaitAsync(token))
                {
                    return GraphFailure(KnowledgeReadStatus.Unavailable);
                }

                var result = await _disclosedReader.GetRelatedAsync(new(opened.Selection, seed.Handle, relatedLimit),
                    new(KnowledgeContract.MaximumContentBytes, remainingBytes, encoder), token).WaitAsync(token);
                if (result.Status != KnowledgeReadStatus.Success || result.Disclosure is not { } disclosure ||
                    !ValidGraphRelated(disclosure.Page, graphSelection, relatedLimit) ||
                    !ValidGraphEnvelope(disclosure, encoder.EncodeRelated(disclosure.Page), encoder.ProfileId, remainingBytes))
                {
                    return GraphFailure(KnowledgeReadStatus.Unavailable);
                }

                capsules.Add(disclosure.Dependencies);
                remainingBytes -= disclosure.OutputUtf8.Length;
                relatedPages.Add(disclosure.Page);
            }

            var graph = KnowledgeGraphProjector.Project([seeds], relatedPages.ToImmutable(), displayedSeeds.Length > relatedPages.Count);
            if (!await IsCurrentAsync(context, generation, token).WaitAsync(token))
            {
                return GraphFailure(KnowledgeReadStatus.Unavailable);
            }

            // This is the final Knowledge operation. No guessed contributor subset, root or merge call.
            var authorized = await _dependencyAuthority.ReauthorizeAsync(graphSelection, capsules.ToImmutable(), token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (authorized != KnowledgeDependencyStatus.Succeeded || _disposed || generation != Volatile.Read(ref _contextGeneration))
            {
                return GraphFailure(KnowledgeReadStatus.Unavailable);
            }

            return new(KnowledgeReadStatus.Success, graph, context.Selection.Mode, context.IsControlledFixture);
        }
        catch (OperationCanceledException)
        {
            return GraphFailure(cancellationToken.IsCancellationRequested ? KnowledgeReadStatus.Cancelled : KnowledgeReadStatus.ReadBudgetExceeded);
        }
        catch (Exception) { return GraphFailure(KnowledgeReadStatus.UnavailableService); }
    }

    private static ExploreGraphResult GraphFailure(KnowledgeReadStatus status) => new(status, null);

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
