namespace Vulgata.Core.Entities;

public enum StructuralRelationshipKind
{
    Calls,
    Imports,
    Inherits,
    Contains,
}

public enum ScanCandidateOutcomeKind
{
    Filtered,
    Parsed,
    Skipped,
    Errored,
}

public sealed class ScanCandidateOutcome
{
    private ScanCandidateOutcome()
    {
    }

    public ScanCandidateOutcome(
        Guid scanRunId,
        string filePath,
        ScanCandidateOutcomeKind outcome,
        string reasonOrCategory,
        int auditGroupOrdinal,
        DateTimeOffset recordedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(auditGroupOrdinal);
        Id = Guid.NewGuid();
        ScanRunId = scanRunId;
        FilePath = CodeUnit.NormalizePath(filePath);
        Outcome = outcome;
        ReasonOrCategory = Normalize(reasonOrCategory, 512);
        AuditGroupOrdinal = auditGroupOrdinal;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid ScanRunId { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public ScanCandidateOutcomeKind Outcome { get; private set; }
    public string ReasonOrCategory { get; private set; } = string.Empty;
    public int AuditGroupOrdinal { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public ScanRun? ScanRun { get; private set; }

    private static string Normalize(string value, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }
}

public sealed class CodeUnitEdge
{
    private CodeUnitEdge()
    {
    }

    public CodeUnitEdge(
        Guid scanRunId,
        Guid repositoryId,
        Guid sourceCodeUnitId,
        Guid targetCodeUnitId,
        StructuralRelationshipKind edgeType)
    {
        if (scanRunId == Guid.Empty || repositoryId == Guid.Empty ||
            sourceCodeUnitId == Guid.Empty || targetCodeUnitId == Guid.Empty)
        {
            throw new ArgumentException("结构关系标识不能为空。");
        }

        Id = Guid.NewGuid();
        ScanRunId = scanRunId;
        RepositoryId = repositoryId;
        SourceCodeUnitId = sourceCodeUnitId;
        TargetCodeUnitId = targetCodeUnitId;
        EdgeType = edgeType;
        IsResolved = true;
    }

    public Guid Id { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid SourceCodeUnitId { get; private set; }
    public Guid TargetCodeUnitId { get; private set; }
    public StructuralRelationshipKind EdgeType { get; private set; }
    public bool IsResolved { get; private set; }
    public ScanRun? ScanRun { get; private set; }
    public CodeUnit? SourceCodeUnit { get; private set; }
    public CodeUnit? TargetCodeUnit { get; private set; }
}

public sealed class UnresolvedStructuralRelationshipCandidate
{
    private UnresolvedStructuralRelationshipCandidate()
    {
    }

    public UnresolvedStructuralRelationshipCandidate(
        Guid scanRunId,
        Guid repositoryId,
        Guid sourceCodeUnitId,
        StructuralRelationshipKind relationshipKind,
        string providerTargetEndpointIdentity,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerTargetEndpointIdentity);
        Id = Guid.NewGuid();
        ScanRunId = scanRunId;
        RepositoryId = repositoryId;
        SourceCodeUnitId = sourceCodeUnitId;
        RelationshipKind = relationshipKind;
        ProviderTargetEndpointIdentity = providerTargetEndpointIdentity.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid SourceCodeUnitId { get; private set; }
    public StructuralRelationshipKind RelationshipKind { get; private set; }
    public string ProviderTargetEndpointIdentity { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public ScanRun? ScanRun { get; private set; }
    public CodeUnit? SourceCodeUnit { get; private set; }
}
