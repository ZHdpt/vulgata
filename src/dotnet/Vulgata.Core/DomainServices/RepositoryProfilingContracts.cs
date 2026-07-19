using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface IGitWorkingCopyService
{
    Task<GitWorkingCopyResult> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        CancellationToken cancellationToken = default);
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

public sealed record GitWorkingCopyResult(bool Succeeded, string? WorkingCopyPath, string? Error)
{
    public static GitWorkingCopyResult Success(string path) => new(true, path, null);
    public static GitWorkingCopyResult Failure(string error) => new(false, null, error);
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
    RangeConfidence RangeConfidence);

public sealed record ProviderStructuralRelationship(
    string SourceEndpointIdentity,
    string TargetEndpointIdentity,
    StructuralRelationshipKind RelationshipKind);

public sealed record ProviderCandidateOutcome(
    string RelativePath,
    ScanCandidateOutcomeKind Outcome,
    string Reason);
