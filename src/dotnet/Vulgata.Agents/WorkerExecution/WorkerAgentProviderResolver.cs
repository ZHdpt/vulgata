using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

public sealed record ResolvedWorkerAgentProvider(
    Guid ProviderId,
    string Model,
    Uri Endpoint,
    string EncryptedApiKey,
    bool IsSystemOverride);

/// <summary>
/// Resolves exactly one existing Worker Agent assignment. A System override
/// wins; otherwise the existing ordered global Worker Agent assignment is used.
/// </summary>
public sealed class WorkerAgentProviderResolver(
    ISystemLlmProviderOverrideRepository overrideRepository,
    ILlmProviderRepository providerRepository)
{
    public async Task<ResolvedWorkerAgentProvider> ResolveAsync(
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
                AgentType.Worker,
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
                .FirstOrDefault(candidate => candidate.DefaultAgentType == AgentType.Worker);
            isSystemOverride = false;
        }

        if (provider is null ||
            provider.DefaultAgentType != AgentType.Worker ||
            (provider.SupportedApiTypes & ApiType.ChatCompletions) == 0 ||
            string.IsNullOrWhiteSpace(provider.Name) ||
            string.IsNullOrWhiteSpace(provider.EncryptedApiKey) ||
            !Uri.TryCreate(provider.BaseEndpointUrl, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "No configured OpenAI-compatible Worker Agent assignment is available.");
        }

        return new(
            provider.Id,
            provider.Name,
            endpoint,
            provider.EncryptedApiKey,
            isSystemOverride);
    }
}
