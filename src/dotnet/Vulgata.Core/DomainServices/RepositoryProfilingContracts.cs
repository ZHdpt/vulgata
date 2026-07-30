using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface IGitWorkingCopyService
{
    Task<GitWorkingCopyResult> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        CancellationToken cancellationToken = default);
}

public interface ICommittedRepositorySnapshotService
{
    Task<ICommittedRepositorySnapshotLease> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        GitWorkingCopyResult workingCopy,
        CancellationToken cancellationToken = default);
}

public interface ICommittedRepositorySnapshotLease : IAsyncDisposable
{
    Guid RepositoryId { get; }
    Guid ScanRunId { get; }
    string ProfiledRevision { get; }
    string RootPath { get; }
    Task VerifyIdentityAsync(CancellationToken cancellationToken = default);
}

public interface IScanCandidateEnumerator
{
    Task<IReadOnlyList<ScanSourceCandidate>> EnumerateAsync(
        string workingCopyPath,
        CancellationToken cancellationToken = default);
}

public interface ICodeGraphProvider
{
    Task<CodeGraphProviderResult> ProfileAsync(
        string workingCopyPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public interface IFrameworkEvidenceDetector
{
    Task<IReadOnlyList<PackageEvidence>> DetectAsync(
        string workingCopyPath,
        CancellationToken cancellationToken = default);

    Task<FrameworkEvidenceDetectionResult> DetectDetailedAsync(
        string workingCopyPath,
        CancellationToken cancellationToken = default);

    FrameworkEvidenceDetectionResult SelectApplicable(
        FrameworkEvidenceDetectionResult detection,
        string codeUnitPath,
        string parserLanguage);
}

public enum PackageEvidenceRelationship
{
    Direct,
    Transitive,
}

public sealed record PackageEvidence(
    string ArtifactPath,
    string ScopeRoot,
    string Ecosystem,
    string PackageCoordinate,
    PackageEvidenceRelationship Relationship,
    string DetectionSource,
    int ScopeDepth);

public enum DirectManifestOwnerReadStatus
{
    SafeAndBounded,
    UnsafePath,
    ReparsePoint,
    BoundExceeded,
    Unreadable,
}

public enum DirectManifestOwnerParseStatus
{
    ParsedSupported,
    Malformed,
    Unsupported,
    ConditionalOrDynamic,
}

public sealed record DirectManifestOwnerCandidate(
    string ArtifactPath,
    string ScopeRoot,
    string Ecosystem,
    DirectManifestOwnerReadStatus ReadStatus,
    DirectManifestOwnerParseStatus ParseStatus,
    bool Applicable);

public sealed record ValidatedDirectManifestOwner(
    string ArtifactPath,
    string ScopeRoot,
    string Ecosystem);

public enum FrameworkEvidenceDetectionFailure
{
    None,
    RepositoryBoundExceeded,
    DetectorFailed,
}

public sealed record FrameworkEvidenceAuditOutcome(
    string ArtifactPath,
    ScanCandidateOutcomeKind Outcome,
    string Reason,
    string Message);

public sealed record FrameworkEvidenceAuditIdentity(
    string ArtifactPath,
    ScanCandidateOutcomeKind Outcome,
    string Reason);

public sealed class FrameworkEvidenceDetectionResult
{
    private FrameworkEvidenceDetectionResult(
        bool succeeded,
        IReadOnlyList<PackageEvidence> evidence,
        IReadOnlyList<FrameworkEvidenceAuditOutcome> auditOutcomes,
        IReadOnlyList<FrameworkEvidenceAuditIdentity> auditOutcomeIdentities,
        IReadOnlyList<ValidatedDirectManifestOwner> directManifestScopes,
        int totalAuditOutcomeCount,
        FrameworkEvidenceDetectionFailure failure,
        string? error)
    {
        Succeeded = succeeded;
        Evidence = evidence;
        AuditOutcomes = auditOutcomes;
        AuditOutcomeIdentities = auditOutcomeIdentities;
        DirectManifestScopes = directManifestScopes;
        TotalAuditOutcomeCount = totalAuditOutcomeCount;
        Failure = failure;
        Error = error;
    }

    public bool Succeeded { get; }
    public IReadOnlyList<PackageEvidence> Evidence { get; }
    public IReadOnlyList<FrameworkEvidenceAuditOutcome> AuditOutcomes { get; }
    public IReadOnlyList<FrameworkEvidenceAuditIdentity> AuditOutcomeIdentities { get; }
    public IReadOnlyList<ValidatedDirectManifestOwner> DirectManifestScopes { get; }
    public int TotalAuditOutcomeCount { get; }
    public FrameworkEvidenceDetectionFailure Failure { get; }
    public string? Error { get; }

    public static FrameworkEvidenceDetectionResult Success(
        IEnumerable<PackageEvidence> evidence,
        IEnumerable<FrameworkEvidenceAuditOutcome> auditOutcomes,
        int totalAuditOutcomeCount,
        IEnumerable<FrameworkEvidenceAuditIdentity>? auditOutcomeIdentities = null,
        IEnumerable<ValidatedDirectManifestOwner>? directManifestScopes = null)
    {
        PackageEvidence[] retainedEvidence = [.. evidence];
        FrameworkEvidenceAuditOutcome[] retainedOutcomes = [.. auditOutcomes];
        FrameworkEvidenceAuditIdentity[] completeIdentities =
        [
            .. (auditOutcomeIdentities ??
                retainedOutcomes.Select(outcome => new FrameworkEvidenceAuditIdentity(
                    outcome.ArtifactPath,
                    outcome.Outcome,
                    outcome.Reason)))
                .Distinct(),
        ];
        if (totalAuditOutcomeCount < retainedOutcomes.Length ||
            totalAuditOutcomeCount < completeIdentities.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalAuditOutcomeCount),
                "The complete audit count cannot be smaller than the retained sample or identity set.");
        }

        return new(
            succeeded: true,
            Array.AsReadOnly(retainedEvidence),
            Array.AsReadOnly(retainedOutcomes),
            Array.AsReadOnly(completeIdentities),
            Array.AsReadOnly([.. directManifestScopes ?? []]),
            totalAuditOutcomeCount,
            FrameworkEvidenceDetectionFailure.None,
            error: null);
    }

    public static FrameworkEvidenceDetectionResult Failed(
        FrameworkEvidenceDetectionFailure failure,
        string error)
    {
        if (failure == FrameworkEvidenceDetectionFailure.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                "A failed framework detection result requires a closed failure reason.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(
            succeeded: false,
            [],
            [],
            [],
            [],
            totalAuditOutcomeCount: 0,
            failure,
            error);
    }
}

public sealed record GitWorkingCopyResult(
    bool Succeeded,
    string? WorkingCopyPath,
    string? ProfiledRevision,
    string? Error)
{
    public static GitWorkingCopyResult Success(string path, string profiledRevision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(true, path, RequireProfiledRevision(profiledRevision), null);
    }

    public static GitWorkingCopyResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(false, null, null, error);
    }

    private static string RequireProfiledRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 64 ||
            value.Length == 0 ||
            value.Any(static character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Profiled Revision must be a bounded lowercase Git object ID.",
                nameof(value));
        }

        return value;
    }
}

public sealed record ScanSourceCandidate(
    string RelativePath,
    bool IsFiltered,
    string Category,
    int AuditGroupOrdinal);

public sealed record CodeGraphProviderResult(
    bool Succeeded,
    IReadOnlyList<ProviderCodeUnit> CodeUnits,
    IReadOnlyList<ProviderStructuralRelationship> Relationships,
    IReadOnlyList<ProviderCandidateOutcome> CandidateOutcomes,
    int ParsedFileCount,
    TimeSpan Duration,
    string? Error)
{
    public static CodeGraphProviderResult Failure(string error, TimeSpan duration) =>
        new(false, [], [], [], 0, duration, error);
}

public sealed record ProviderCodeUnit(
    string RelativePath,
    string SymbolName,
    CodeUnitType UnitType,
    int LineStart,
    int LineEnd,
    string EndpointIdentity,
    RangeConfidence RangeConfidence,
    string? Language = null);

public sealed record ProviderStructuralRelationship(
    string SourceEndpointIdentity,
    string TargetEndpointIdentity,
    StructuralRelationshipKind RelationshipKind);

public sealed record SafeProviderPath
{
    public const string UnknownValue = "(unknown)";

    private SafeProviderPath(string value, bool isKnown)
    {
        Value = value;
        IsKnown = isKnown;
    }

    public string Value { get; }
    public bool IsKnown { get; }

    public static SafeProviderPath FromProviderScalar(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.Length > 2_048 ||
            path.Length != path.Trim().Length ||
            path.Any(char.IsControl) ||
            (path[0] is '/' or '\\') ||
            Path.IsPathRooted(path) ||
            (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':'))
        {
            return Unknown();
        }

        string normalized = path.Trim().Replace('\\', '/');
        string[] segments = normalized.Split('/');
        if (StringComparer.Ordinal.Equals(normalized, UnknownValue) ||
            normalized.Length is 0 or > 2_048 ||
            segments.Any(segment => segment is "" or "." or ".."))
        {
            return Unknown();
        }

        return new(normalized, isKnown: true);
    }

    public static SafeProviderPath Revalidate(SafeProviderPath? path) =>
        FromProviderScalar(path?.Value);

    public override string ToString() => Value;

    private static SafeProviderPath Unknown() => new(UnknownValue, isKnown: false);
}

public sealed record ProviderCandidateOutcome(
    SafeProviderPath RelativePath,
    ScanCandidateOutcomeKind Outcome,
    string Reason)
{
    public ProviderCandidateOutcome(
        string? relativePath,
        ScanCandidateOutcomeKind outcome,
        string reason)
        : this(SafeProviderPath.FromProviderScalar(relativePath), outcome, reason)
    {
    }
}

public static class ScanCandidateOutcomeReasons
{
    public const string UnknownLanguage = nameof(UnknownLanguage);
    public const string FrameworkEvidenceMalformed = nameof(FrameworkEvidenceMalformed);
    public const string FrameworkEvidenceUnsafePath = nameof(FrameworkEvidenceUnsafePath);
    public const string FrameworkEvidenceReparsePoint = nameof(FrameworkEvidenceReparsePoint);
    public const string FrameworkEvidenceUnsupported = nameof(FrameworkEvidenceUnsupported);
    public const string FrameworkEvidenceAmbiguousScope = nameof(FrameworkEvidenceAmbiguousScope);
    public const string FrameworkEvidenceBoundExceeded = nameof(FrameworkEvidenceBoundExceeded);
}

public sealed class RepositoryProfileDraft
{
    private RepositoryProfileDraft(
        ScanRun scanRun,
        IReadOnlyList<CodeUnit> codeUnits,
        IReadOnlyList<CodeUnitEdge> codeUnitEdges,
        IReadOnlyList<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships,
        IReadOnlyList<CodeUnitFramework> codeUnitFrameworks,
        IReadOnlyList<ScanRunLanguageSummary> languageSummaries,
        IReadOnlyList<ScanCandidateOutcome> candidateOutcomes)
    {
        ScanRun = scanRun;
        CodeUnits = codeUnits;
        CodeUnitEdges = codeUnitEdges;
        UnresolvedRelationships = unresolvedRelationships;
        CodeUnitFrameworks = codeUnitFrameworks;
        LanguageSummaries = languageSummaries;
        CandidateOutcomes = candidateOutcomes;
    }

    public ScanRun ScanRun { get; }
    public IReadOnlyList<CodeUnit> CodeUnits { get; }
    public IReadOnlyList<CodeUnitEdge> CodeUnitEdges { get; }
    public IReadOnlyList<UnresolvedStructuralRelationshipCandidate> UnresolvedRelationships { get; }
    public IReadOnlyList<CodeUnitFramework> CodeUnitFrameworks { get; }
    public IReadOnlyList<ScanRunLanguageSummary> LanguageSummaries { get; }
    public IReadOnlyList<ScanCandidateOutcome> CandidateOutcomes { get; }

    public static RepositoryProfileDraft Create(
        ScanRun scanRun,
        IEnumerable<CodeUnit> codeUnits,
        IEnumerable<CodeUnitEdge> codeUnitEdges,
        IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships,
        IEnumerable<CodeUnitFramework> codeUnitFrameworks,
        IEnumerable<ScanRunLanguageSummary> languageSummaries,
        IEnumerable<ScanCandidateOutcome> candidateOutcomes)
    {
        ArgumentNullException.ThrowIfNull(scanRun);
        var draft = new RepositoryProfileDraft(
            scanRun,
            codeUnits.ToArray(),
            codeUnitEdges.ToArray(),
            unresolvedRelationships.ToArray(),
            codeUnitFrameworks.ToArray(),
            languageSummaries.ToArray(),
            candidateOutcomes.ToArray());
        draft.Validate();
        return draft;
    }

    private void Validate()
    {
        if (ScanRun.Status != ScanRunStatus.Completed)
        {
            throw new InvalidOperationException("A Repository Profile Draft requires a completed ScanRun.");
        }

        if (CodeUnits.Any(unit =>
                unit.ScanRunId != ScanRun.Id ||
                unit.RepositoryId != ScanRun.RepositoryId ||
                string.IsNullOrWhiteSpace(unit.Language)))
        {
            throw new InvalidOperationException(
                "Every Code Unit in a completed Repository Profile Draft requires one normalized language.");
        }

        var unitsById = CodeUnits.ToDictionary(unit => unit.Id);
        if (CodeUnitFrameworks.Any(framework =>
                framework.ScanRunId != ScanRun.Id ||
                framework.RepositoryId != ScanRun.RepositoryId ||
                !unitsById.TryGetValue(framework.CodeUnitId, out CodeUnit? owner) ||
                owner.ScanRunId != framework.ScanRunId ||
                owner.RepositoryId != framework.RepositoryId) ||
            CodeUnitFrameworks.GroupBy(framework => (framework.CodeUnitId, framework.FrameworkName))
                .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Framework facts must belong uniquely to this Repository Profile Draft.");
        }

        Dictionary<string, int> expectedSummaries = CodeUnits
            .GroupBy(unit => unit.Language!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(unit => unit.FilePath).Distinct(StringComparer.Ordinal).Count(),
                StringComparer.Ordinal);
        Dictionary<string, int> actualSummaries = LanguageSummaries
            .Where(summary =>
                summary.ScanRunId == ScanRun.Id &&
                summary.RepositoryId == ScanRun.RepositoryId)
            .ToDictionary(
                summary => summary.Language,
                summary => summary.DistinctFileCount,
                StringComparer.Ordinal);
        if (expectedSummaries.Count != actualSummaries.Count ||
            expectedSummaries.Any(expected =>
                !actualSummaries.TryGetValue(expected.Key, out int actual) ||
                actual != expected.Value))
        {
            throw new InvalidOperationException(
                "The Repository Profile Draft language summary must exactly match distinct retained Code Unit paths.");
        }

        if (CodeUnitEdges.Any(edge =>
                edge.ScanRunId != ScanRun.Id ||
                edge.RepositoryId != ScanRun.RepositoryId) ||
            UnresolvedRelationships.Any(relationship =>
                relationship.ScanRunId != ScanRun.Id ||
                relationship.RepositoryId != ScanRun.RepositoryId) ||
            CandidateOutcomes.Any(outcome => outcome.ScanRunId != ScanRun.Id))
        {
            throw new InvalidOperationException("Repository Profile Draft records must share one ScanRun identity.");
        }
    }
}

public sealed class PreScanResultDraft
{
    private PreScanResultDraft(PreScanResult result)
    {
        Result = result;
    }

    public PreScanResult Result { get; }
    public Guid RepositoryId => Result.RepositoryId;
    public Guid ScanRunId => Result.ScanRunId;
    public string ProfiledRevision => Result.ProfiledRevision;
    public PreScanApprovalState State => Result.State;
    public string SchemaVersion => Result.SchemaVersion;
    public DateTimeOffset ApprovedAt => Result.ApprovedAt;
    public string Fingerprint => Result.Fingerprint;
    public string ApprovalMessage => Result.ApprovalMessage;
    public ConfirmedCommunicationKinds ConfirmedCommunicationKinds =>
        Result.ConfirmedCommunicationKinds;
    public int TotalConfirmedEvidenceCount => Result.TotalConfirmedEvidenceCount;
    public int TotalHintCount => Result.TotalHintCount;
    public int TotalWarningCount => Result.TotalWarningCount;
    public bool EvidenceTruncated => Result.EvidenceTruncated;
    public IReadOnlyList<CommunicationKindClassification> Classifications =>
        Result.Classifications;

    public static PreScanResultDraft Create(PreScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(result);
    }
}

public sealed class RepositoryProfileCommitDraft
{
    private RepositoryProfileCommitDraft(
        RepositoryProfileDraft profile,
        PreScanResultDraft preScanResult)
    {
        Profile = profile;
        PreScanResult = preScanResult;
    }

    public RepositoryProfileDraft Profile { get; }
    public PreScanResultDraft PreScanResult { get; }

    public static RepositoryProfileCommitDraft Create(
        RepositoryProfileDraft profile,
        PreScanResultDraft preScanResult)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(preScanResult);
        if (profile.ScanRun.ProfiledRevision is null)
        {
            profile.ScanRun.BindProfiledRevision(preScanResult.ProfiledRevision);
        }

        if (profile.ScanRun.Status != ScanRunStatus.Completed ||
            profile.ScanRun.RepositoryId != preScanResult.RepositoryId ||
            profile.ScanRun.Id != preScanResult.ScanRunId ||
            !StringComparer.Ordinal.Equals(
                profile.ScanRun.ProfiledRevision,
                preScanResult.ProfiledRevision) ||
            preScanResult.ApprovedAt < profile.ScanRun.StartedAt)
        {
            throw new InvalidOperationException(
                "The profile and approved Pre-Scan Result must share one completed Repository and ScanRun identity.");
        }

        return new(profile, preScanResult);
    }
}
