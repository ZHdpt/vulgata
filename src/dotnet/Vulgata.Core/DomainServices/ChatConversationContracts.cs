using System.Text;
using System.Text.Json;

namespace Vulgata.Core.DomainServices;

public sealed class ChatConversationScope : IEquatable<ChatConversationScope>
{
    private ChatConversationScope(
        ChatMode mode,
        Guid systemId,
        IReadOnlyList<Guid> repositoryIds)
    {
        Mode = mode;
        SystemId = systemId;
        RepositoryIds = repositoryIds;
    }

    public ChatMode Mode { get; }

    public Guid SystemId { get; }

    public IReadOnlyList<Guid> RepositoryIds { get; }

    public static ChatConversationScope Create(
        ChatMode mode,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds)
    {
        if (!Enum.IsDefined(mode) || systemId == Guid.Empty || repositoryIds is null)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
        }

        Guid[] canonicalRepositoryIds = repositoryIds.Order().ToArray();
        if (canonicalRepositoryIds.Length != 2 ||
            canonicalRepositoryIds.Any(id => id == Guid.Empty) ||
            canonicalRepositoryIds[0] == canonicalRepositoryIds[1])
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
        }

        return new ChatConversationScope(
            mode,
            systemId,
            Array.AsReadOnly(canonicalRepositoryIds));
    }

    public bool Equals(ChatConversationScope? other) =>
        other is not null &&
        Mode == other.Mode &&
        SystemId == other.SystemId &&
        RepositoryIds.SequenceEqual(other.RepositoryIds);

    public override bool Equals(object? obj) => Equals(obj as ChatConversationScope);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mode);
        hash.Add(SystemId);
        foreach (Guid repositoryId in RepositoryIds)
        {
            hash.Add(repositoryId);
        }

        return hash.ToHashCode();
    }
}

public sealed record ValidatedConversationTurn
{
    private ValidatedConversationTurn(
        string question,
        string answerText,
        string packetFingerprint,
        IReadOnlyList<string> citationHandles,
        IReadOnlyList<Guid> logicalDocumentIds)
    {
        Question = question;
        AnswerText = answerText;
        PacketFingerprint = packetFingerprint;
        CitationHandles = citationHandles;
        LogicalDocumentIds = logicalDocumentIds;
    }

    public string Question { get; }

    public string AnswerText { get; }

    public string PacketFingerprint { get; }

    public IReadOnlyList<string> CitationHandles { get; }

    public IReadOnlyList<Guid> LogicalDocumentIds { get; }

    public static ValidatedConversationTurn Create(
        string question,
        string answerText,
        string packetFingerprint,
        IReadOnlyCollection<string> citationHandles,
        IReadOnlyCollection<Guid> logicalDocumentIds)
    {
        string normalizedQuestion = NormalizeRequired(question);
        string normalizedAnswer = NormalizeRequired(answerText);
        string normalizedFingerprint = NormalizeRequired(packetFingerprint);
        if (citationHandles is null || logicalDocumentIds is null)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        string[] canonicalCitationHandles = citationHandles
            .Select(NormalizeRequired)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Guid[] canonicalDocumentIds = logicalDocumentIds
            .Distinct()
            .ToArray();
        if (canonicalCitationHandles.Length == 0 ||
            canonicalDocumentIds.Length == 0 ||
            canonicalDocumentIds.Any(id => id == Guid.Empty))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return new ValidatedConversationTurn(
            normalizedQuestion,
            normalizedAnswer,
            normalizedFingerprint,
            Array.AsReadOnly(canonicalCitationHandles),
            Array.AsReadOnly(canonicalDocumentIds));
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return value.Trim().Normalize(NormalizationForm.FormC);
    }
}

public sealed record ChatConversationRetrievalContext(
    IReadOnlyList<Guid> CurrentDocumentHints,
    IReadOnlyList<string> CitationHandles,
    bool IsMissingEquivalent);

public sealed record ChatRetrievalRequest
{
    private ChatRetrievalRequest(
        ChatQuestionRequest questionRequest,
        ChatConversationState conversation,
        DateTimeOffset deadlineUtc)
    {
        QuestionRequest = questionRequest;
        Conversation = conversation;
        DeadlineUtc = deadlineUtc;
    }

    internal ChatQuestionRequest QuestionRequest { get; }

    public string RequestId => QuestionRequest.RequestId;

    public ChatMode Mode => QuestionRequest.Mode;

    public Guid SystemId => QuestionRequest.SystemId;

    public IReadOnlyList<Guid> RepositoryIds => QuestionRequest.RepositoryIds;

    public string Question => QuestionRequest.Question;

    public ChatConversationState Conversation { get; }

    public DateTimeOffset DeadlineUtc { get; }

    public static ChatRetrievalRequest Create(
        ChatQuestionRequest questionRequest,
        ChatConversationState conversation,
        DateTimeOffset deadlineUtc)
    {
        ArgumentNullException.ThrowIfNull(questionRequest);
        ArgumentNullException.ThrowIfNull(conversation);
        ChatConversationScope expectedScope = ChatConversationScope.Create(
            questionRequest.Mode,
            questionRequest.SystemId,
            questionRequest.RepositoryIds);
        if (!conversation.Scope.Equals(expectedScope) ||
            conversation.EncodedUtf8Bytes > 4_096 ||
            deadlineUtc == default)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
        }

        return new ChatRetrievalRequest(questionRequest, conversation, deadlineUtc);
    }
}

public sealed class ChatConversationState
{
    private const int _maximumTurns = 2;
    private const int _maximumEncodedUtf8Bytes = 4_096;
    private IReadOnlyList<ValidatedConversationTurn> _turns;

    private ChatConversationState(
        ChatConversationScope scope,
        IReadOnlyList<ValidatedConversationTurn> turns)
    {
        Scope = scope;
        _turns = turns;
        EncodedUtf8Bytes = GetEncodedUtf8Bytes(scope, turns);
    }

    public ChatConversationScope Scope { get; }

    public IReadOnlyList<ValidatedConversationTurn> Turns => _turns;

    public int EncodedUtf8Bytes { get; private set; }

    public IReadOnlyList<Guid> PriorLogicalDocumentHints => Array.AsReadOnly(_turns
        .SelectMany(turn => turn.LogicalDocumentIds)
        .Distinct()
        .ToArray());

    public IReadOnlyList<string> PriorCitationHandles => Array.AsReadOnly(_turns
        .SelectMany(turn => turn.CitationHandles)
        .Distinct(StringComparer.Ordinal)
        .ToArray());

    public string RetrievalContext => JsonSerializer.Serialize(new
    {
        questions = _turns.Select(turn => turn.Question),
        citationHandles = PriorCitationHandles,
        logicalDocumentHints = PriorLogicalDocumentHints,
    });

    public static ChatConversationState Empty(ChatConversationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new ChatConversationState(scope, Array.Empty<ValidatedConversationTurn>());
    }

    public ChatConversationState AppendValidatedTurn(ValidatedConversationTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ValidatedConversationTurn[] retainedTurns = _turns
            .Append(turn)
            .TakeLast(_maximumTurns)
            .ToArray();
        var candidate = new ChatConversationState(
            Scope,
            Array.AsReadOnly(retainedTurns));
        if (candidate.EncodedUtf8Bytes > _maximumEncodedUtf8Bytes)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return candidate;
    }

    public ChatConversationState ResetForScopeChange(ChatConversationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return Scope.Equals(scope) ? this : Empty(scope);
    }

    public ChatConversationState Reset() => Empty(Scope);

    public ChatConversationRetrievalContext BuildRetrievalContext(
        IReadOnlyCollection<Guid> currentAuthorizedDocumentIds,
        IReadOnlyCollection<Guid> currentDocumentVersions)
    {
        ArgumentNullException.ThrowIfNull(currentAuthorizedDocumentIds);
        ArgumentNullException.ThrowIfNull(currentDocumentVersions);

        HashSet<Guid> authorized = currentAuthorizedDocumentIds.ToHashSet();
        HashSet<Guid> current = currentDocumentVersions.ToHashSet();
        Guid[] priorHints = PriorLogicalDocumentHints.ToArray();
        bool authorizationLost = priorHints.Length > 0 &&
            !priorHints.Any(authorized.Contains);
        if (authorizationLost)
        {
            Clear();
            return new ChatConversationRetrievalContext(
                Array.Empty<Guid>(),
                Array.Empty<string>(),
                true);
        }

        Guid[] currentHints = priorHints
            .Where(id => authorized.Contains(id) && current.Contains(id))
            .ToArray();
        HashSet<Guid> currentHintSet = currentHints.ToHashSet();
        string[] currentCitationHandles = _turns
            .Where(turn => turn.LogicalDocumentIds.Any(currentHintSet.Contains))
            .SelectMany(turn => turn.CitationHandles)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new ChatConversationRetrievalContext(
            Array.AsReadOnly(currentHints),
            Array.AsReadOnly(currentCitationHandles),
            false);
    }

    private void Clear()
    {
        _turns = Array.Empty<ValidatedConversationTurn>();
        EncodedUtf8Bytes = GetEncodedUtf8Bytes(Scope, _turns);
    }

    private static int GetEncodedUtf8Bytes(
        ChatConversationScope scope,
        IReadOnlyList<ValidatedConversationTurn> turns) =>
        Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new
        {
            mode = scope.Mode,
            systemId = scope.SystemId,
            repositoryIds = scope.RepositoryIds,
            turns,
        }));
}
