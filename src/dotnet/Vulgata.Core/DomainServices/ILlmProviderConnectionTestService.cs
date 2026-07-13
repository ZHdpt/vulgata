using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface ILlmProviderConnectionTestService
{
    Task<LlmProviderConnectionAttemptResult> TestAsync(
        LlmProvider provider,
        string apiKey,
        CancellationToken cancellationToken = default);
}

public sealed record LlmProviderConnectionAttemptResult(bool Success, string Message);
