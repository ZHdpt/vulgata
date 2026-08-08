namespace Vulgata.Core.DomainServices;

public interface IChatEvidenceRetriever
{
    Task<ChatEvidenceResult> RetrieveAsync(
        ChatQuestionRequest request,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default);
}
