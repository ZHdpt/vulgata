using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.ChatExecution;

public sealed record ResolvedChatAgentProvider(
    Guid ProviderId,
    string Model,
    Uri Endpoint,
    string EncryptedApiKey,
    bool IsSystemOverride);

public sealed class ChatAgentProviderResolver(
    ISystemLlmProviderOverrideRepository overrideRepository,
    ILlmProviderRepository providerRepository)
{
    public async Task<ResolvedChatAgentProvider> ResolveAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        if (systemId == Guid.Empty)
        {
            throw new ArgumentException("The System identifier must be non-empty.", nameof(systemId));
        }

        SystemLlmProviderOverride? overrideEntry =
            await overrideRepository.GetBySystemAndAgentTypeAsync(
                systemId,
                AgentType.Chat,
                cancellationToken);
        LlmProvider? provider;
        bool isSystemOverride;
        if (overrideEntry is not null)
        {
            provider = await providerRepository.GetByIdAsync(
                overrideEntry.LlmProviderId,
                cancellationToken);
            isSystemOverride = true;
        }
        else
        {
            provider = (await providerRepository.ListAllAsync(cancellationToken))
                .FirstOrDefault(candidate => candidate.DefaultAgentType == AgentType.Chat);
            isSystemOverride = false;
        }

        if (provider is null ||
            (provider.SupportedApiTypes & ApiType.ChatCompletions) == 0 ||
            string.IsNullOrWhiteSpace(provider.Name) ||
            string.IsNullOrWhiteSpace(provider.EncryptedApiKey) ||
            !Uri.TryCreate(provider.BaseEndpointUrl, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "No configured OpenAI-compatible Chat Agent assignment is available.");
        }

        return new ResolvedChatAgentProvider(
            provider.Id,
            provider.Name,
            endpoint,
            provider.EncryptedApiKey,
            isSystemOverride);
    }
}
