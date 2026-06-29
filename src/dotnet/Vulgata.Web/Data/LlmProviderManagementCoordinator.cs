using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.LlmProviders;
using Vulgata.Web.Validators;

namespace Vulgata.Web.Data;

public interface ILlmProviderManagementCoordinator
{
    Task<IReadOnlyList<LlmProviderSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<LlmProviderSummaryDto?> GetDefaultCandidateAsync(AgentType agentType, CancellationToken cancellationToken = default);

    Task<LlmProviderMutationResult<LlmProviderSummaryDto>> CreateAsync(
        CreateLlmProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<LlmProviderMutationResult<LlmProviderSummaryDto>> UpdateAsync(
        Guid providerId,
        UpdateLlmProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<LlmProviderMutationResult<bool>> DeleteAsync(Guid providerId, CancellationToken cancellationToken = default);

    Task<LlmProviderConnectionTestResult> TestConnectionAsync(Guid providerId, CancellationToken cancellationToken = default);
}

public enum LlmProviderMutationOutcome
{
    Success,
    NotFound,
    DuplicateName,
}

public sealed record LlmProviderMutationResult<T>(LlmProviderMutationOutcome Outcome, T? Value = default, string? Message = null)
{
    public static LlmProviderMutationResult<T> Success(T value) => new(LlmProviderMutationOutcome.Success, value);

    public static LlmProviderMutationResult<T> NotFound { get; } = new(LlmProviderMutationOutcome.NotFound);

    public static LlmProviderMutationResult<T> DuplicateName(string message) =>
        new(LlmProviderMutationOutcome.DuplicateName, default, message);
}

public enum LlmProviderConnectionTestOutcome
{
    Success,
    NotFound,
    MissingApiKey,
    Failed,
}

public sealed record LlmProviderConnectionTestResult(LlmProviderConnectionTestOutcome Outcome, string Message)
{
    public static LlmProviderConnectionTestResult Success(string message) =>
        new(LlmProviderConnectionTestOutcome.Success, message);

    public static LlmProviderConnectionTestResult NotFound { get; } =
        new(LlmProviderConnectionTestOutcome.NotFound, "LLM 提供商不存在。");

    public static LlmProviderConnectionTestResult MissingApiKey { get; } =
        new(LlmProviderConnectionTestOutcome.MissingApiKey, "当前提供商未配置可用的 API 密钥。");

    public static LlmProviderConnectionTestResult Failed(string message) =>
        new(LlmProviderConnectionTestOutcome.Failed, message);
}

public sealed class LlmProviderManagementCoordinator(
    ILlmProviderRepository repository,
    IApiKeyEncryptionService encryptionService,
    ILlmProviderConnectionTestService connectionTestService)
    : ILlmProviderManagementCoordinator
{
    public async Task<IReadOnlyList<LlmProviderSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LlmProvider> providers = await repository.ListAllAsync(cancellationToken);
        return providers.Select(MapToSummary).ToList();
    }

    public async Task<LlmProviderSummaryDto?> GetDefaultCandidateAsync(
        AgentType agentType,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LlmProvider> providers = await repository.ListAllAsync(cancellationToken);
        LlmProvider? provider = providers.FirstOrDefault(candidate => candidate.DefaultAgentType == agentType);
        return provider is null ? null : MapToSummary(provider);
    }

    public async Task<LlmProviderMutationResult<LlmProviderSummaryDto>> CreateAsync(
        CreateLlmProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await repository.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
        {
            return LlmProviderMutationResult<LlmProviderSummaryDto>.DuplicateName("提供商名称已存在。");
        }

        string encryptedApiKey = await encryptionService.EncryptAsync(request.ApiKey, cancellationToken);
        LlmProvider provider = new(
            request.Name,
            request.BaseEndpointUrl,
            encryptedApiKey,
            request.SupportedApiTypes,
            request.DefaultAgentType,
            DateTimeOffset.UtcNow);

        await repository.AddAsync(provider, cancellationToken);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return LlmProviderMutationResult<LlmProviderSummaryDto>.DuplicateName("提供商名称已存在。");
        }

        return LlmProviderMutationResult<LlmProviderSummaryDto>.Success(MapToSummary(provider));
    }

    public async Task<LlmProviderMutationResult<LlmProviderSummaryDto>> UpdateAsync(
        Guid providerId,
        UpdateLlmProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        LlmProvider? provider = await repository.GetByIdAsync(providerId, cancellationToken);
        if (provider is null)
        {
            return LlmProviderMutationResult<LlmProviderSummaryDto>.NotFound;
        }

        if (await repository.NameExistsAsync(request.Name, providerId, cancellationToken))
        {
            return LlmProviderMutationResult<LlmProviderSummaryDto>.DuplicateName("提供商名称已存在。");
        }

        string encryptedApiKey = provider.EncryptedApiKey;
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            encryptedApiKey = await encryptionService.EncryptAsync(request.ApiKey, cancellationToken);
        }

        provider.UpdateDetails(
            request.Name,
            request.BaseEndpointUrl,
            encryptedApiKey,
            request.SupportedApiTypes,
            request.DefaultAgentType,
            DateTimeOffset.UtcNow);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return LlmProviderMutationResult<LlmProviderSummaryDto>.DuplicateName("提供商名称已存在。");
        }

        return LlmProviderMutationResult<LlmProviderSummaryDto>.Success(MapToSummary(provider));
    }

    public async Task<LlmProviderMutationResult<bool>> DeleteAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        LlmProvider? provider = await repository.GetByIdAsync(providerId, cancellationToken);
        if (provider is null)
        {
            return LlmProviderMutationResult<bool>.NotFound;
        }

        repository.Remove(provider);
        await repository.SaveChangesAsync(cancellationToken);
        return LlmProviderMutationResult<bool>.Success(true);
    }

    public async Task<LlmProviderConnectionTestResult> TestConnectionAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        LlmProvider? provider = await repository.GetByIdAsync(providerId, cancellationToken);
        if (provider is null)
        {
            return LlmProviderConnectionTestResult.NotFound;
        }

        if (string.IsNullOrWhiteSpace(provider.EncryptedApiKey))
        {
            return LlmProviderConnectionTestResult.MissingApiKey;
        }

        string apiKey = await encryptionService.DecryptAsync(provider.EncryptedApiKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return LlmProviderConnectionTestResult.MissingApiKey;
        }

        LlmProviderConnectionAttemptResult result = await connectionTestService.TestAsync(provider, apiKey, cancellationToken);
        return result.Success
            ? LlmProviderConnectionTestResult.Success(result.Message)
            : LlmProviderConnectionTestResult.Failed($"连接测试失败：{result.Message}");
    }

    private static LlmProviderSummaryDto MapToSummary(LlmProvider provider)
    {
        return new LlmProviderSummaryDto
        {
            Id = provider.Id,
            Name = provider.Name,
            BaseEndpointUrl = provider.BaseEndpointUrl,
            SupportedApiTypes = (int)provider.SupportedApiTypes,
            DefaultAgentType = (int)provider.DefaultAgentType,
            HasApiKey = !string.IsNullOrWhiteSpace(provider.EncryptedApiKey),
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt,
        };
    }
}
