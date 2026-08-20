using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

public sealed class PreparedRelatedFileExpansion(IPreparedCodeGraphAnalysisPort analysisPort)
{
    public const int MaximumHops = 2;
    public const int MaximumFiles = 200;
    public static TimeSpan Timeout => TimeSpan.FromSeconds(5);
    public static string WarningCode => "RelatedFileExpansionFailed";

    public async Task<PreparedRelatedFileExpansionResult> ExpandAsync(
        PreparedRelatedFilesRequest request,
        IReadOnlyList<PreparedFileCodeUnitDraft> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);
        ValidateRequest(request, candidates);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        PreparedRelatedFilesResult result;
        try
        {
            result = await analysisPort.GetPreparedRelatedFilesAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WarningFallback(candidates, "RelatedFileExpansionTimedOut");
        }

        if (result.Status != AnalysisOperationStatus.Succeeded || result.Failure is not null)
        {
            return WarningFallback(candidates, WarningCode);
        }

        PreparedRelatedFile[] files = result.Files.ToArray();
        HashSet<string> eligible = request.EligibleRelativePaths.ToHashSet(StringComparer.Ordinal);
        HashSet<string> seeds = request.SeedRelativePaths.ToHashSet(StringComparer.Ordinal);
        if (files.Length > MaximumFiles || files.Any(file =>
            file.Hop is < 1 or > MaximumHops ||
            !eligible.Contains(file.RelativePath) ||
            !seeds.Contains(file.SeedRelativePath) ||
            !request.RelationshipKinds.Contains(file.RelationshipKind)))
        {
            return WarningFallback(candidates, "RelatedFileExpansionMalformed");
        }

        Dictionary<string, PreparedFileCodeUnitDraft> seedByPath = candidates
            .Where(candidate => seeds.Contains(candidate.RelativePath))
            .GroupBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(candidate => candidate.DirectBusinessScore)
                    .ThenBy(candidate => candidate.SegmentOrdinal)
                    .First(),
                StringComparer.Ordinal);
        var best = new Dictionary<string, (int Score, PreparedSelectionReason Reason)>(StringComparer.Ordinal);
        foreach (PreparedRelatedFile file in files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ThenBy(file => file.Hop)
            .ThenBy(file => file.RelationshipKind))
        {
            if (!seedByPath.TryGetValue(file.SeedRelativePath, out PreparedFileCodeUnitDraft? seed))
            {
                return WarningFallback(candidates, "RelatedFileExpansionMalformed");
            }

            int score = Math.Max(100, seed.DirectBusinessScore - (100 * file.Hop));
            PreparedSelectionReason reason = file.RelationshipKind switch
            {
                PreparedRelationshipKind.Calls => PreparedSelectionReason.RelatedCall,
                PreparedRelationshipKind.Imports => PreparedSelectionReason.RelatedImport,
                PreparedRelationshipKind.Inherits => PreparedSelectionReason.RelatedInheritance,
                _ => throw new InvalidOperationException("Unknown prepared relationship kind."),
            };
            if (!best.TryGetValue(file.RelativePath, out var current) || score > current.Score ||
                (score == current.Score && reason < current.Reason))
            {
                best[file.RelativePath] = (score, reason);
            }
        }

        PreparedFileCodeUnitDraft[] expanded = candidates.Select(candidate =>
        {
            if (!best.TryGetValue(candidate.RelativePath, out var related) ||
                related.Score <= candidate.DirectBusinessScore)
            {
                return candidate with { FinalBusinessScore = candidate.DirectBusinessScore };
            }

            return candidate with
            {
                FinalBusinessScore = related.Score,
                SelectionReason = related.Reason,
            };
        }).ToArray();
        return new PreparedRelatedFileExpansionResult(
            PreparedRelatedFileExpansionOutcome.Expanded,
            null,
            expanded);
    }

    private static void ValidateRequest(
        PreparedRelatedFilesRequest request,
        IReadOnlyList<PreparedFileCodeUnitDraft> candidates)
    {
        PreparedRelationshipKind[] expected =
        [
            PreparedRelationshipKind.Calls,
            PreparedRelationshipKind.Imports,
            PreparedRelationshipKind.Inherits,
        ];
        string[] canonicalSeeds = PreparedScanPlanner.SelectSeeds(candidates)
            .Select(candidate => candidate.RelativePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string[] requestedSeeds = request.SeedRelativePaths
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (request.MaximumHops != MaximumHops || request.MaximumFiles != MaximumFiles ||
            request.SeedRelativePaths.Count is < 1 or > PreparedScanPlanner.MaximumHighValueSeedsPerRepository ||
            request.SeedRelativePaths.Distinct(StringComparer.Ordinal).Count() != request.SeedRelativePaths.Count ||
            request.EligibleRelativePaths.Distinct(StringComparer.Ordinal).Count() != request.EligibleRelativePaths.Count ||
            !request.RelationshipKinds.Order().SequenceEqual(expected) ||
            !requestedSeeds.SequenceEqual(canonicalSeeds, StringComparer.Ordinal) ||
            candidates.Any(candidate => candidate.RepositoryId != request.Scope.RepositoryId ||
                candidate.ScanRunId != request.Scope.ScanRunId ||
                !StringComparer.Ordinal.Equals(candidate.ProfiledRevision, request.Scope.ProfiledRevision)))
        {
            throw new ArgumentException("Related-file expansion request is outside the closed Story 12.2 bounds.", nameof(request));
        }
    }

    private static PreparedRelatedFileExpansionResult WarningFallback(
        IReadOnlyList<PreparedFileCodeUnitDraft> candidates,
        string warningCode) =>
        new(
            PreparedRelatedFileExpansionOutcome.WarningFallback,
            warningCode,
            candidates.Select(candidate => candidate with
            {
                FinalBusinessScore = candidate.DirectBusinessScore,
                SelectionReason = candidate.DirectBusinessScore == 100
                    ? PreparedSelectionReason.ProductionFallback
                    : PreparedSelectionReason.DirectBusinessEvidence,
            }).ToArray());
}
