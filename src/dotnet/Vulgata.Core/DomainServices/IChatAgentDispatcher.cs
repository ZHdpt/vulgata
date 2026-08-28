namespace Vulgata.Core.DomainServices;

public interface IChatAgentDispatcher
{
    Task<ChatSynthesisOutcome> GenerateAsync(
        ChatSynthesisRequest request,
        CancellationToken cancellationToken = default);
}

public enum ChatStreamValidationDisposition
{
    SyntaxRepairRequired,
    Accepted,
    RejectedSyntax,
    RejectedSemantic,
    Cancelled,
}

public interface IChatStreamValidationDispositionRecorder
{
    bool TryRecordFinalValidationDisposition(
        string requestId,
        ChatStreamValidationDisposition disposition);
}
