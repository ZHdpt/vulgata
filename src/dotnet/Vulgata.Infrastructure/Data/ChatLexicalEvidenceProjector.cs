using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Data;

public sealed class ChatLexicalEvidenceProjector : IChatLexicalEvidenceProjector
{
    public Task<ChatRetrievalStageResult<ChatLexicalPrimaryProjection>> ProjectAsync(
        AuthorizedChatSnapshot snapshot,
        string question,
        CancellationToken cancellationToken) => Task.FromResult(
            ChatEvidenceRetriever.RetrieveLexicalPrimaryAsync(
                snapshot,
                question,
                cancellationToken));
}
