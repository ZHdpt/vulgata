namespace Vulgata.Core.Entities;

public sealed class Uncertainty
{
    private static readonly HashSet<string> _types =
        ["cross_repository", "unsupported_communication"];
    private static readonly HashSet<string> _subStatuses =
        ["cross_repo", "unknown_target", "ambiguous"];
    private static readonly HashSet<string> _reasonCodes =
    [
        "variable_target",
        "disallowed_invocation",
        "malformed_provider",
        "provider_out_of_root",
        "unmatched",
        "multiple_providers",
        "normalization_mismatch",
        "unsupported_kind",
    ];

    private Uncertainty()
    {
    }

    public Uncertainty(
        Guid id,
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid? sourceAnchorId,
        string type,
        string subStatus,
        string reasonCode,
        string? observationKind,
        string? endpointIdentifier,
        string? endpointIdentifierNormalized,
        Guid? targetRepositoryId,
        string profileSchemaVersion,
        string profileId,
        string profileVersion,
        string profileFingerprint,
        string occurrenceId,
        string evidenceLocator,
        string evidenceFingerprint,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty ||
            documentVersionId == Guid.Empty ||
            repositoryId == Guid.Empty ||
            scanRunId == Guid.Empty ||
            sourceAnchorId == Guid.Empty ||
            targetRepositoryId == Guid.Empty ||
            !_types.Contains(type) ||
            !_subStatuses.Contains(subStatus) ||
            !_reasonCodes.Contains(reasonCode) ||
            (endpointIdentifier is null) != (endpointIdentifierNormalized is null) ||
            (reasonCode == "unsupported_kind") !=
                (observationKind == "file_storage" && type == "unsupported_communication") ||
            profileSchemaVersion != "1.0")
        {
            throw new ArgumentException("The automatic Uncertainty is outside the closed contract.");
        }

        Id = id;
        DocumentVersionId = documentVersionId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        SourceAnchorId = sourceAnchorId;
        Type = type;
        Status = "unresolved";
        SubStatus = subStatus;
        ReasonCode = reasonCode;
        ObservationKind = observationKind;
        EndpointIdentifier = endpointIdentifier;
        EndpointIdentifierNormalized = endpointIdentifierNormalized;
        TargetRepositoryId = targetRepositoryId;
        ProfileSchemaVersion = profileSchemaVersion;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        ProfileFingerprint = profileFingerprint;
        OccurrenceId = occurrenceId;
        EvidenceLocator = evidenceLocator;
        EvidenceFingerprint = evidenceFingerprint;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid? SourceAnchorId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string SubStatus { get; private set; } = string.Empty;
    public string ReasonCode { get; private set; } = string.Empty;
    public string? ObservationKind { get; private set; }
    public string? EndpointIdentifier { get; private set; }
    public string? EndpointIdentifierNormalized { get; private set; }
    public Guid? TargetRepositoryId { get; private set; }
    public string ProfileSchemaVersion { get; private set; } = string.Empty;
    public string ProfileId { get; private set; } = string.Empty;
    public string ProfileVersion { get; private set; } = string.Empty;
    public string ProfileFingerprint { get; private set; } = string.Empty;
    public string OccurrenceId { get; private set; } = string.Empty;
    public string EvidenceLocator { get; private set; } = string.Empty;
    public string EvidenceFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedByDocumentVersionId { get; private set; }
    public DocumentVersion? DocumentVersion { get; private set; }
    public SourceAnchor? SourceAnchor { get; private set; }
    public Repository? TargetRepository { get; private set; }
    public DocumentVersion? ResolvedByDocumentVersion { get; private set; }
}
