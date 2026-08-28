using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.ChatExecution;

internal interface IChatProviderStreamingTransport
{
    IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
        ChatProviderStreamRequest request,
        CancellationToken cancellationToken);
}
