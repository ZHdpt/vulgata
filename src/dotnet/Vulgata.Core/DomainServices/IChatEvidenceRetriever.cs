using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public enum ChatRetrievalStageResultKind
{
    Succeeded,
    InsufficientEvidence,
    NotFoundOrUnauthorized,
    InconsistentCurrentEvidence,
}

public sealed record ChatRetrievalStageResult<T>
    where T : class
{
    private ChatRetrievalStageResult(ChatRetrievalStageResultKind kind, T? value)
    {
        Kind = kind;
        Value = value;
    }

    public ChatRetrievalStageResultKind Kind { get; }

    public T? Value { get; }

    public static ChatRetrievalStageResult<T> Succeeded(T value) =>
        new(ChatRetrievalStageResultKind.Succeeded, value ?? throw new ArgumentNullException(nameof(value)));

    public static ChatRetrievalStageResult<T> InsufficientEvidence() =>
        new(ChatRetrievalStageResultKind.InsufficientEvidence, null);

    public static ChatRetrievalStageResult<T> NotFoundOrUnauthorized() =>
        new(ChatRetrievalStageResultKind.NotFoundOrUnauthorized, null);

    public static ChatRetrievalStageResult<T> InconsistentCurrentEvidence() =>
        new(ChatRetrievalStageResultKind.InconsistentCurrentEvidence, null);
}

public interface IChatEvidenceRetriever
{
    Task<ChatEvidenceResult> RetrieveAsync(
        ChatRetrievalRequest request,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default);
}

public interface IChatRelationshipProjector
{
    Task<ChatRelationshipProjection> ProjectAsync(
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyCollection<Guid> seedDocumentVersionIds,
        IReadOnlyCollection<Guid> includedDocumentVersionIds,
        IReadOnlyCollection<Guid> excludedEdgeIds,
        int relatedDocumentLimit,
        int relationshipLimit,
        CancellationToken cancellationToken);
}

public sealed record ChatRelationshipProjection(
    IReadOnlyList<ProjectedChatRelationship> Relationships,
    IReadOnlyList<DocumentSection> RelatedSections,
    IReadOnlyList<EvidenceFlowGroupAuthority> FlowGroups,
    bool IsInconsistent = false)
{
    public static ChatRelationshipProjection Empty { get; } = new([], [], []);
    public static ChatRelationshipProjection Invalid { get; } = new([], [], [], true);
}

public sealed record ProjectedChatRelationship(
    Guid EdgeId,
    Guid SourceDocumentVersionId,
    Guid TargetDocumentVersionId,
    DocumentEdgeType EdgeType,
    Guid? SelectedExactDecisionId,
    int ExactDecisionCount,
    string? ExactDecisionSetFingerprint,
    string? ExactProviderEvidenceSetFingerprint,
    Guid? FlowGroupId);
