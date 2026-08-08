namespace Vulgata.Core.DomainServices;

public interface IChatAgentDispatcher
{
    Task<ChatSynthesisOutcome> GenerateAsync(
        ChatSynthesisRequest request,
        CancellationToken cancellationToken = default);
}
