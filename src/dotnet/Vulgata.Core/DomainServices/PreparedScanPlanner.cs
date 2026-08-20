using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public enum PreparedBusinessEvidence
{
    ProductionFallback,
    CommunicationHint,
    ResolvedImplementation,
    EndpointOrApplicationEntry,
    ExactXmlCommunicationDeclaration,
    ConfirmedCommunication,
}

public sealed record PreparedSelectionPlan(
    IReadOnlyList<PreparedFileCodeUnitDraft> Selected,
    IReadOnlyList<PreparedFileCodeUnitDraft> Excluded);

/// <summary>Pure, input-order-independent Story 12.2 pair selection.</summary>
public static class PreparedScanPlanner
{
    public const int MaximumHighValueSeedsPerRepository = 20;
    public const int MinimumPairSelection = 50;
    public const int MaximumPairSelection = 150;

    public static int Score(PreparedBusinessEvidence evidence) => evidence switch
    {
        PreparedBusinessEvidence.ConfirmedCommunication => 1_000,
        PreparedBusinessEvidence.ExactXmlCommunicationDeclaration => 900,
        PreparedBusinessEvidence.EndpointOrApplicationEntry => 800,
        PreparedBusinessEvidence.ResolvedImplementation => 700,
        PreparedBusinessEvidence.CommunicationHint => 400,
        PreparedBusinessEvidence.ProductionFallback => 100,
        _ => throw new ArgumentOutOfRangeException(nameof(evidence)),
    };

    public static PreparedFileCodeUnitDraft CreateFileCodeUnit(PreparedFileCodeUnitInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Evidence is null || input.Evidence.Count == 0 ||
            input.ByteStart < 0 || input.ByteEndExclusive <= input.ByteStart ||
            input.ByteEndExclusive - input.ByteStart > PreparedSourceRangePlanner.MaximumRawBytes ||
            input.LineStart < 1 || input.LineEnd < input.LineStart ||
            input.LineEnd - input.LineStart + 1 > PreparedSourceRangePlanner.MaximumLogicalLines ||
            input.SegmentOrdinal < 1 || input.SegmentCount < input.SegmentOrdinal)
        {
            throw new ArgumentException("A File Code Unit requires exact bounded ranges and evidence.", nameof(input));
        }

        int directScore = input.Evidence.Max(Score);
        PreparedSelectionReason reason = directScore switch
        {
            1_000 or 900 or 800 or 700 or 400 => PreparedSelectionReason.DirectBusinessEvidence,
            100 => PreparedSelectionReason.ProductionFallback,
            _ => throw new InvalidOperationException("The direct score is outside the closed table."),
        };
        PreparedCanonicalArtifact identity = PreparedScanIdentityBuilders.CreateFileCodeUnitIdentity(
            input.RepositoryId,
            input.ScanRunId,
            input.ProfiledRevision,
            input.RelativePath,
            RequireExactSha256(input.FileHash, nameof(input.FileHash)),
            input.ByteStart,
            input.ByteEndExclusive,
            input.LineStart,
            input.LineEnd,
            input.SegmentOrdinal,
            input.SegmentCount,
            RequireExactSha256(input.SourceHash, nameof(input.SourceHash)));
        Guid id = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
            "FileCodeUnit",
            identity.Fingerprint);
        return new PreparedFileCodeUnitDraft(
            id,
            input.RepositoryId,
            input.ScanRunId,
            input.ProfiledRevision,
            AnalysisContractGuard.RequireRelativePath(input.RelativePath),
            input.FileHash,
            input.ByteStart,
            input.ByteEndExclusive,
            input.LineStart,
            input.LineEnd,
            input.SegmentOrdinal,
            input.SegmentCount,
            input.SourceHash,
            identity.Fingerprint,
            directScore,
            directScore,
            reason,
            0,
            0);
    }

    public static PreparedFileCodeUnitConstruction BuildFileCodeUnit(
        PreparedFileCodeUnitInput input,
        string language)
    {
        if (!CodeUnit.IsNormalizedLanguage(language) || language is not ("java" or "xml"))
        {
            throw new ArgumentException("Prepared File Code Units require the bounded java/xml language.", nameof(language));
        }

        PreparedFileCodeUnitDraft draft = CreateFileCodeUnit(input);
        CodeUnit entity = CodeUnit.CreatePreparedFileCodeUnit(
            draft.Id,
            draft.ScanRunId,
            draft.RepositoryId,
            draft.RelativePath,
            draft.FileHash,
            draft.SourceHash,
            draft.ByteStart,
            draft.ByteEndExclusive,
            draft.LineStart,
            draft.LineEnd,
            draft.SegmentOrdinal,
            draft.SegmentCount,
            draft.IdentityFingerprint,
            language);
        return new PreparedFileCodeUnitConstruction(draft, entity);
    }

    public static IReadOnlyList<PreparedFileCodeUnitDraft> SelectSeeds(
        IEnumerable<PreparedFileCodeUnitDraft> candidates)
    {
        PreparedFileCodeUnitDraft[] copy = RequireCandidates(candidates);
        return copy
            .Where(candidate => candidate.DirectBusinessScore >= 400)
            .GroupBy(candidate => candidate.RepositoryId)
            .OrderBy(group => RepositoryText(group.Key), StringComparer.Ordinal)
            .SelectMany(group => group
                .OrderByDescending(candidate => candidate.DirectBusinessScore)
                .ThenBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.SegmentOrdinal)
                .Take(MaximumHighValueSeedsPerRepository))
            .ToArray();
    }

    public static PreparedSelectionPlan Plan(
        IEnumerable<PreparedFileCodeUnitDraft> candidates,
        int minimumSelection = MinimumPairSelection,
        int maximumSelection = MaximumPairSelection)
    {
        if (minimumSelection < 0 || maximumSelection > MaximumPairSelection || minimumSelection > maximumSelection)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSelection));
        }

        PreparedFileCodeUnitDraft[] copy = RequireCandidates(candidates);
        HashSet<string> mandatory = SelectSeeds(copy)
            .Select(candidate => candidate.IdentityFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        if (mandatory.Count > maximumSelection)
        {
            throw new InvalidOperationException("Mandatory high-value seeds exceed the selection bound.");
        }

        PreparedFileCodeUnitDraft[] retained = OrderPair(copy.Where(candidate => mandatory.Contains(candidate.IdentityFingerprint)))
            .Concat(OrderPair(copy.Where(candidate => !mandatory.Contains(candidate.IdentityFingerprint))))
            .Take(maximumSelection)
            .ToArray();
        retained = OrderPair(retained).ToArray();
        if (retained.Length < minimumSelection)
        {
            throw new InvalidOperationException("The pair selection is below the approved lower bound.");
        }

        var pairOrdinals = retained
            .Select((candidate, index) => candidate with { PairSelectionOrdinal = index + 1 })
            .ToArray();
        PreparedFileCodeUnitDraft[] selected = pairOrdinals
            .GroupBy(candidate => candidate.RepositoryId)
            .SelectMany(group => OrderManifest(group)
                .Select((candidate, index) => candidate with { ManifestOrdinal = index + 1 }))
            .OrderBy(candidate => candidate.PairSelectionOrdinal)
            .ToArray();
        HashSet<string> selectedIdentities = selected
            .Select(candidate => candidate.IdentityFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        PreparedFileCodeUnitDraft[] excluded = OrderPair(copy
            .Where(candidate => !selectedIdentities.Contains(candidate.IdentityFingerprint)))
            .ToArray();
        return new PreparedSelectionPlan(selected, excluded);
    }

    public static IReadOnlyList<PreparedScanAuditDraft> CreateSelectionAuditLinks(
        PreparedSelectionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        PreparedScanAuditDraft[] selected = plan.Selected
            .OrderBy(candidate => candidate.PairSelectionOrdinal)
            .Select(candidate => PreparedScanAuditFactory.Create(
                candidate.RepositoryId,
                candidate.ScanRunId,
                candidate.RelativePath,
                PreparedScanAuditOutcome.Selected,
                PreparedScanAuditReason.SelectedForPreparation,
                candidate.FileHash,
                candidate.ByteEndExclusive - candidate.ByteStart,
                candidate.LineEnd - candidate.LineStart + 1,
                candidate.IdentityFingerprint,
                candidate.PairSelectionOrdinal,
                0,
                candidate.Id))
            .ToArray();
        PreparedScanAuditDraft[] excluded = plan.Excluded
            .Select(candidate => PreparedScanAuditFactory.Create(
                candidate.RepositoryId,
                candidate.ScanRunId,
                candidate.RelativePath,
                PreparedScanAuditOutcome.Eligible,
                PreparedScanAuditReason.SelectionCapExceeded,
                candidate.FileHash,
                candidate.ByteEndExclusive - candidate.ByteStart,
                candidate.LineEnd - candidate.LineStart + 1,
                candidate.IdentityFingerprint,
                null,
                0))
            .ToArray();
        return selected.Concat(excluded)
            .OrderBy(audit => audit.RelativePath, StringComparer.Ordinal)
            .ThenBy(audit => AuditOutcomeRank(audit.Outcome))
            .ThenBy(audit => audit.Reason.ToString(), StringComparer.Ordinal)
            .ThenBy(audit => audit.DetailFingerprint, StringComparer.Ordinal)
            .Select((audit, index) => audit with { Ordinal = index + 1 })
            .ToArray();
    }

    public static IEnumerable<PreparedFileCodeUnitDraft> OrderManifest(
        IEnumerable<PreparedFileCodeUnitDraft> candidates) => candidates
        .OrderByDescending(candidate => candidate.FinalBusinessScore)
        .ThenBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
        .ThenBy(candidate => candidate.SegmentOrdinal);

    public static IEnumerable<PreparedFileCodeUnitDraft> OrderPair(
        IEnumerable<PreparedFileCodeUnitDraft> candidates) => candidates
        .OrderByDescending(candidate => candidate.FinalBusinessScore)
        .ThenBy(candidate => RepositoryText(candidate.RepositoryId), StringComparer.Ordinal)
        .ThenBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
        .ThenBy(candidate => candidate.SegmentOrdinal);

    public static string FingerprintIdentity(PreparedFileCodeUnitDraft candidate) =>
        PreparedScanIdentityBuilders.CreateFileCodeUnitIdentity(
            (candidate ?? throw new ArgumentNullException(nameof(candidate))).RepositoryId,
            candidate.ScanRunId,
            candidate.ProfiledRevision,
            candidate.RelativePath,
            candidate.FileHash,
            candidate.ByteStart,
            candidate.ByteEndExclusive,
            candidate.LineStart,
            candidate.LineEnd,
            candidate.SegmentOrdinal,
            candidate.SegmentCount,
            candidate.SourceHash).Fingerprint;

    private static PreparedFileCodeUnitDraft[] RequireCandidates(
        IEnumerable<PreparedFileCodeUnitDraft> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        PreparedFileCodeUnitDraft[] copy = candidates.ToArray();
        if (copy.Any(candidate => candidate is null) ||
            copy.GroupBy(candidate => candidate.IdentityFingerprint, StringComparer.Ordinal).Any(group => group.Count() > 1) ||
            copy.Any(candidate => candidate.DirectBusinessScore is not (100 or 400 or 700 or 800 or 900 or 1_000) ||
                candidate.FinalBusinessScore < candidate.DirectBusinessScore))
        {
            throw new ArgumentException("Candidates must have unique identities and closed valid scores.", nameof(candidates));
        }

        return copy;
    }

    private static string RepositoryText(Guid repositoryId) =>
        repositoryId == Guid.Empty
            ? throw new ArgumentException("Repository identity is required.", nameof(repositoryId))
            : repositoryId.ToString("D").ToLowerInvariant();

    private static int AuditOutcomeRank(PreparedScanAuditOutcome outcome) => outcome switch
    {
        PreparedScanAuditOutcome.Eligible => 0,
        PreparedScanAuditOutcome.Filtered => 1,
        PreparedScanAuditOutcome.Skipped => 2,
        PreparedScanAuditOutcome.Selected => 3,
        PreparedScanAuditOutcome.Warning => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static string RequireExactSha256(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length != 64 ||
            value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("An exact lowercase SHA-256 is required.", parameterName);
        }

        return value;
    }
}
