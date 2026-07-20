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
    RangeConfidence RangeConfidence,
    string? Language = null);

public sealed record ProviderStructuralRelationship(
    string SourceEndpointIdentity,
    string TargetEndpointIdentity,
    StructuralRelationshipKind RelationshipKind);

public sealed record ProviderCandidateOutcome(
    string RelativePath,
    ScanCandidateOutcomeKind Outcome,
    string Reason);

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
