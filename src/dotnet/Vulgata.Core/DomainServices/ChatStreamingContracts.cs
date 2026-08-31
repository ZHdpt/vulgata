namespace Vulgata.Core.DomainServices;

public enum ChatProviderStreamSignalKind
{
    AnswerDelta,
    Terminal,
    ProviderError,
}

public sealed record ChatProviderStreamRequest(
    string RequestId,
    string AttemptKind,
    string ProviderIdentity,
    string ProviderConfigurationFingerprint,
    string Prompt,
    string EvidencePacketFingerprint,
    string StreamingRequestFingerprint,
    long AbsoluteDeadlineUnixTimeMilliseconds,
    string? ReasoningEffort = null);

public sealed record ChatStreamCompletionSummary(
    string CompletionSummaryId,
    string RequestId,
    IReadOnlyList<string> RuntimeObservationIds,
    bool SyntaxRepairUsed,
    IReadOnlyList<string> AttemptValidationOutcomes,
    string FinalValidationOutcome,
    long? TotalAcceptedAnswerDurationMilliseconds);

public interface IChatStreamCompletionSummaryRecorder
{
    void RecordCompletion(ChatStreamCompletionSummary summary);

    IReadOnlyList<string> GetRuntimeObservationIds(string requestId);
}

public sealed record ChatProviderStreamSignal(
    ChatProviderStreamSignalKind Kind,
    ReadOnlyMemory<byte> AnswerDelta,
    string? FailureCategory = null,
    int ProviderChunkCount = 0,
    int MaximumReceivedProviderChunkByteCount = 0,
    int AssembledReceivedProviderResponseByteCount = 0);

public sealed class ChatProviderStreamAssemblyResult
{
    private ChatProviderStreamAssemblyResult(
        bool accepted,
        string? answer,
        string? rejectionCategory,
        int answerDeltaCount,
        int answerDeltaByteCount,
        int assembledAnswerByteCount,
        int terminalCount,
        int rawBodyByteCount)
    {
        Accepted = accepted;
        Answer = answer;
        RejectionCategory = rejectionCategory;
        AnswerDeltaCount = answerDeltaCount;
        AnswerDeltaByteCount = answerDeltaByteCount;
        AssembledAnswerByteCount = assembledAnswerByteCount;
        TerminalCount = terminalCount;
        RawBodyByteCount = rawBodyByteCount;
    }

    public bool Accepted { get; }

    public string? Answer { get; }

    public string? PartialAnswer => null;

    public string? RejectionCategory { get; }

    public int AnswerDeltaCount { get; }

    public int AnswerDeltaByteCount { get; }

    public int AssembledAnswerByteCount { get; }

    public int TerminalCount { get; }

    public int RawBodyByteCount { get; }

    public static ChatProviderStreamAssemblyResult AcceptedResult(
        string answer,
        int answerDeltaCount,
        int answerDeltaByteCount,
        int assembledAnswerByteCount,
        int terminalCount,
        int rawBodyByteCount) =>
        new(
            true,
            answer,
            null,
            answerDeltaCount,
            answerDeltaByteCount,
            assembledAnswerByteCount,
            terminalCount,
            rawBodyByteCount);

    public static ChatProviderStreamAssemblyResult RejectedResult(
        string rejectionCategory,
        int answerDeltaCount,
        int answerDeltaByteCount,
        int assembledAnswerByteCount,
        int terminalCount,
        int rawBodyByteCount) =>
        new(
            false,
            null,
            rejectionCategory,
            answerDeltaCount,
            answerDeltaByteCount,
            assembledAnswerByteCount,
            terminalCount,
            rawBodyByteCount);
}
