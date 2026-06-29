using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.LlmProviders;
using Vulgata.Web.Validators;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Web.Data;

public interface ISystemLlmProviderOverrideCoordinator
{
    Task<SystemLlmProviderOverrideMutationResult<IReadOnlyList<SystemLlmProviderOverrideSummaryDto>>> ListAsync(
        Guid systemId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<SystemLlmProviderOverrideMutationResult<IReadOnlyList<LlmProviderSummaryDto>>> ListProvidersAsync(
        Guid systemId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>> UpsertAsync(
        Guid systemId,
        Guid? overrideId,
        UpsertSystemLlmProviderOverrideRequest request,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<SystemLlmProviderOverrideMutationResult<bool>> DeleteAsync(
        Guid systemId,
        Guid overrideId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<SystemLlmProviderEffectiveProviderResult> GetEffectiveProviderAsync(
        Guid systemId,
        AgentType agentType,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}

public enum SystemLlmProviderOverrideMutationOutcome
{
    Success,
    SystemNotFound,
    OverrideNotFound,
    ProviderNotFound,
    AgentTypeMismatch,
    DuplicateAgentType,
}

public sealed record SystemLlmProviderOverrideMutationResult<T>(
    SystemLlmProviderOverrideMutationOutcome Outcome,
    T? Value = default,
    bool IsCreated = false,
    string? Message = null)
{
    public static SystemLlmProviderOverrideMutationResult<T> Success(T value, bool isCreated = false) =>
        new(SystemLlmProviderOverrideMutationOutcome.Success, value, isCreated);

    public static SystemLlmProviderOverrideMutationResult<T> SystemNotFound { get; } =
        new(SystemLlmProviderOverrideMutationOutcome.SystemNotFound);

    public static SystemLlmProviderOverrideMutationResult<T> OverrideNotFound { get; } =
        new(SystemLlmProviderOverrideMutationOutcome.OverrideNotFound);

    public static SystemLlmProviderOverrideMutationResult<T> ProviderNotFound { get; } =
        new(SystemLlmProviderOverrideMutationOutcome.ProviderNotFound);

    public static SystemLlmProviderOverrideMutationResult<T> AgentTypeMismatch(string message) =>
        new(SystemLlmProviderOverrideMutationOutcome.AgentTypeMismatch, default, false, message);

    public static SystemLlmProviderOverrideMutationResult<T> DuplicateAgentType(string message) =>
        new(SystemLlmProviderOverrideMutationOutcome.DuplicateAgentType, default, false, message);
}

public sealed record SystemLlmProviderEffectiveProviderResult(Guid ProviderId, bool IsOverride);

public sealed class SystemLlmProviderOverrideSummaryDto
{
    public Guid Id { get; set; }

    public Guid SystemId { get; set; }

    public Guid LlmProviderId { get; set; }

    public int AgentType { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public bool UsesGlobalDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SystemLlmProviderOverrideCoordinator(
    ISystemRepository systemRepository,
    ISystemLlmProviderOverrideRepository overrideRepository,
    ILlmProviderRepository llmProviderRepository)
    : ISystemLlmProviderOverrideCoordinator
{
    public async Task<SystemLlmProviderOverrideMutationResult<IReadOnlyList<SystemLlmProviderOverrideSummaryDto>>> ListAsync(
        Guid systemId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, userId, isAdministrator, cancellationToken) is null)
        {
            return SystemLlmProviderOverrideMutationResult<IReadOnlyList<SystemLlmProviderOverrideSummaryDto>>.SystemNotFound;
        }

        IReadOnlyList<SystemLlmProviderOverride> overrides = await overrideRepository.ListBySystemAsync(systemId, cancellationToken);
        IReadOnlyList<SystemLlmProviderOverrideSummaryDto> mapped = overrides
            .Select(o => MapToSummary(o))
            .ToList();

        return SystemLlmProviderOverrideMutationResult<IReadOnlyList<SystemLlmProviderOverrideSummaryDto>>.Success(mapped);
    }

    public async Task<SystemLlmProviderOverrideMutationResult<IReadOnlyList<LlmProviderSummaryDto>>> ListProvidersAsync(
        Guid systemId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, userId, isAdministrator, cancellationToken) is null)
        {
            return SystemLlmProviderOverrideMutationResult<IReadOnlyList<LlmProviderSummaryDto>>.SystemNotFound;
        }

        IReadOnlyList<LlmProvider> providers = await llmProviderRepository.ListAllAsync(cancellationToken);
        IReadOnlyList<LlmProviderSummaryDto> mapped = providers
            .Select(MapToProviderSummary)
            .ToList();

        return SystemLlmProviderOverrideMutationResult<IReadOnlyList<LlmProviderSummaryDto>>.Success(mapped);
    }

    public async Task<SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>> UpsertAsync(
        Guid systemId,
        Guid? overrideId,
        UpsertSystemLlmProviderOverrideRequest request,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, userId, isAdministrator, cancellationToken) is null)
        {
            return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.SystemNotFound;
        }

        LlmProvider? provider = await llmProviderRepository.GetByIdAsync(request.LlmProviderId, cancellationToken);
        if (provider is null)
        {
            return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.ProviderNotFound;
        }

        if (provider.DefaultAgentType != request.AgentType)
        {
            return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.AgentTypeMismatch(
                "所选 LLM 提供商的默认代理角色与目标代理角色不匹配。");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (overrideId.HasValue)
        {
            SystemLlmProviderOverride? existingById = await overrideRepository.GetBySystemAndIdAsync(
                systemId,
                overrideId.Value,
                cancellationToken);
            if (existingById is null)
            {
                return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.OverrideNotFound;
            }

            if (existingById.AgentType != request.AgentType)
            {
                SystemLlmProviderOverride? duplicate = await overrideRepository.GetBySystemAndAgentTypeAsync(
                    systemId,
                    request.AgentType,
                    cancellationToken);

                if (duplicate is not null && duplicate.Id != existingById.Id)
                {
                    return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.DuplicateAgentType(
                        "同一系统下该代理角色已存在覆盖配置。");
                }
            }

            existingById.Update(request.LlmProviderId, request.AgentType, now);

            try
            {
                await overrideRepository.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.DuplicateAgentType(
                    "同一系统下该代理角色已存在覆盖配置。");
            }

            return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.Success(
                MapToSummary(existingById, provider.Name));
        }

        SystemLlmProviderOverride? existing = await overrideRepository.GetBySystemAndAgentTypeAsync(
            systemId,
            request.AgentType,
            cancellationToken);

        bool created = false;
        if (existing is null)
        {
            existing = SystemLlmProviderOverride.Create(systemId, request.LlmProviderId, request.AgentType, now);
            await overrideRepository.AddAsync(existing, cancellationToken);
            created = true;
        }
        else
        {
            existing.Update(request.LlmProviderId, request.AgentType, now);
        }

        try
        {
            await overrideRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.DuplicateAgentType(
                "同一系统下该代理角色已存在覆盖配置。");
        }

        return SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto>.Success(
            MapToSummary(existing, provider.Name),
            created);
    }

    public async Task<SystemLlmProviderOverrideMutationResult<bool>> DeleteAsync(
        Guid systemId,
        Guid overrideId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, userId, isAdministrator, cancellationToken) is null)
        {
            return SystemLlmProviderOverrideMutationResult<bool>.SystemNotFound;
        }

        bool removed = await overrideRepository.RemoveAsync(systemId, overrideId, cancellationToken);
        if (!removed)
        {
            return SystemLlmProviderOverrideMutationResult<bool>.OverrideNotFound;
        }

        await overrideRepository.SaveChangesAsync(cancellationToken);
        return SystemLlmProviderOverrideMutationResult<bool>.Success(true);
    }

    public async Task<SystemLlmProviderEffectiveProviderResult> GetEffectiveProviderAsync(
        Guid systemId,
        AgentType agentType,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, userId, isAdministrator, cancellationToken) is null)
        {
            return new SystemLlmProviderEffectiveProviderResult(Guid.Empty, false);
        }

        SystemLlmProviderOverride? overrideEntry = await overrideRepository.GetBySystemAndAgentTypeAsync(
            systemId,
            agentType,
            cancellationToken);

        if (overrideEntry is not null)
        {
            return new SystemLlmProviderEffectiveProviderResult(overrideEntry.LlmProviderId, true);
        }

        LlmProvider? globalDefault = (await llmProviderRepository.ListAllAsync(cancellationToken))
            .FirstOrDefault(provider => provider.DefaultAgentType == agentType);

        return globalDefault is null
            ? new SystemLlmProviderEffectiveProviderResult(Guid.Empty, false)
            : new SystemLlmProviderEffectiveProviderResult(globalDefault.Id, false);
    }

    private async Task<SystemEntity?> GetVisibleSystemAsync(
        Guid systemId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken)
    {
        return await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken);
    }

    private static SystemLlmProviderOverrideSummaryDto MapToSummary(SystemLlmProviderOverride overrideEntry, string? providerName = null)
    {
        return new SystemLlmProviderOverrideSummaryDto
        {
            Id = overrideEntry.Id,
            SystemId = overrideEntry.SystemId,
            LlmProviderId = overrideEntry.LlmProviderId,
            AgentType = (int)overrideEntry.AgentType,
            ProviderName = providerName ?? overrideEntry.LlmProvider?.Name ?? string.Empty,
            UsesGlobalDefault = false,
            CreatedAt = overrideEntry.CreatedAt,
            UpdatedAt = overrideEntry.UpdatedAt,
        };
    }

    private static LlmProviderSummaryDto MapToProviderSummary(LlmProvider provider)
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
