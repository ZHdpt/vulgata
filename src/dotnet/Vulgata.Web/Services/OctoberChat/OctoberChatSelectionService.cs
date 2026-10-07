using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;

namespace Vulgata.Web.Services.OctoberChat;

public sealed class OctoberChatSelectionService(
    IOctoberKnowledgeReader reader,
    IOctoberKnowledgeDisclosurePolicy disclosurePolicy) : IOctoberChatSelectionService
{
    private readonly OctoberChatRequestValidator _validator = new();

    public async Task<OctoberChatSelectionResult> OpenAsync(ReadSelectionRequest? trustedSelection,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (trustedSelection is null)
        {
            return Unavailable();
        }

        if (!(await _validator.ValidateAsync(trustedSelection, cancellationToken)).IsValid)
        {
            return new(OctoberChatEntryStatus.InvalidSelection, null, null);
        }

        try
        {
            // This check validates the live selection. The owner reader checks each returned
            // Document's complete contributor lineage; an empty list is not a grant for it.
            var disclosure = new DisclosureRequest(trustedSelection, [], null);
            if (await disclosurePolicy.CheckAsync(disclosure, cancellationToken) != DisclosureStatus.Allowed)
            {
                return Unavailable();
            }

            ReadSelectionResult opened = await reader.OpenReadSelectionAsync(trustedSelection, cancellationToken);
            if (opened.Status != KnowledgeReadStatus.Success || opened.Selection is null)
            {
                return Unavailable();
            }

            SearchPage catalog = await reader.SearchAsync(
                new(opened.Selection, string.Empty, KnowledgeContract.DefaultPageSize), cancellationToken);
            if (catalog.Status != KnowledgeReadStatus.Success ||
                await disclosurePolicy.CheckAsync(disclosure, cancellationToken) != DisclosureStatus.Allowed)
            {
                return Unavailable();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var conversation = new OctoberChatConversation(Guid.NewGuid(), Guid.NewGuid(),
                trustedSelection, opened.Selection, DateTimeOffset.UtcNow);
            return new(catalog.Documents.IsEmpty ? OctoberChatEntryStatus.Empty : OctoberChatEntryStatus.Ready,
                conversation, catalog);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // No raw service error, prior catalog, or legacy reader crosses this boundary.
            return Unavailable();
        }
    }

    private static OctoberChatSelectionResult Unavailable() => new(OctoberChatEntryStatus.Unavailable, null, null);
}
