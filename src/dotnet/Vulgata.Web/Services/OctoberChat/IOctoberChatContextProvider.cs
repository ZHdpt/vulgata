using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>
/// Integration-owned, server-bound Chat selection with live identity. This is not
/// durable admission, provider readiness or authorization for individual Documents.
/// </summary>
public interface IOctoberChatContextProvider
{
    event Action? ContextChanged;
    Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
