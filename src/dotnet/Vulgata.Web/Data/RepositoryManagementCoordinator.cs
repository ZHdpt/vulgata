using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Git;
using Vulgata.Shared.Repositories;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Web.Data;

public interface IRepositoryManagementCoordinator
{
    Task<IReadOnlyList<RepositorySummaryDto>> ListVisibleAsync(Guid systemId, string userId, bool isAdministrator, CancellationToken cancellationToken = default);

    Task<RepositoryMutationResult<RepositoryDetailDto>> CreateAsync(
        Guid systemId,
        CreateRepositoryRequest request,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<RepositoryMutationResult<bool>> DeleteAsync(
        Guid systemId,
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}

public enum RepositoryMutationOutcome
{
    Success,
    SystemNotFound,
    RepositoryNotFound,
    DuplicateName,
    GitAuthenticationRequired,
    GitUnreachable,
}

public sealed record RepositoryMutationResult<T>(RepositoryMutationOutcome Outcome, T? Value = default, string? Message = null)
{
    public static RepositoryMutationResult<T> Success(T value) => new(RepositoryMutationOutcome.Success, value);

    public static RepositoryMutationResult<T> SystemNotFound { get; } = new(RepositoryMutationOutcome.SystemNotFound);

    public static RepositoryMutationResult<T> RepositoryNotFound { get; } = new(RepositoryMutationOutcome.RepositoryNotFound);

    public static RepositoryMutationResult<T> DuplicateName(string message) => new(RepositoryMutationOutcome.DuplicateName, default, message);

    public static RepositoryMutationResult<T> GitAuthenticationRequired(string message) =>
        new(RepositoryMutationOutcome.GitAuthenticationRequired, default, message);

    public static RepositoryMutationResult<T> GitUnreachable(string message) =>
        new(RepositoryMutationOutcome.GitUnreachable, default, message);
}

public sealed class RepositoryManagementCoordinator(
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IGitRemoteValidationService gitRemoteValidationService)
    : IRepositoryManagementCoordinator
{
    public async Task<IReadOnlyList<RepositorySummaryDto>> ListVisibleAsync(
        Guid systemId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, userId, isAdministrator, cancellationToken) is null)
        {
            return [];
        }

        IReadOnlyList<RepositoryEntity> repositories = await repositoryRepository.ListBySystemAsync(systemId, cancellationToken);

        return repositories.Select(MapToSummary).ToList();
    }

    public async Task<RepositoryMutationResult<RepositoryDetailDto>> CreateAsync(
        Guid systemId,
        CreateRepositoryRequest request,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        SystemEntity? system = await GetVisibleSystemAsync(systemId, userId, isAdministrator, cancellationToken);
        if (system is null)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.SystemNotFound;
        }

        bool exists = await repositoryRepository.NameExistsAsync(systemId, request.Name, cancellationToken: cancellationToken);
        if (exists)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.DuplicateName("该系统下的仓库名称已存在。");
        }

        GitRemoteValidationResult validationResult = await gitRemoteValidationService.ValidateAsync(request.GitUrl, cancellationToken);
        if (validationResult.Status == GitRemoteValidationStatus.AuthenticationRequired)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.GitAuthenticationRequired(validationResult.Message);
        }

        if (validationResult.Status == GitRemoteValidationStatus.Unreachable)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.GitUnreachable(validationResult.Message);
        }

        RepositoryEntity repository = system.AddRepository(
            request.Name,
            request.GitUrl,
            request.Description,
            request.Context,
            DateTimeOffset.UtcNow);

        await repositoryRepository.AddAsync(repository, cancellationToken);
        await repositoryRepository.SaveChangesAsync(cancellationToken);

        return RepositoryMutationResult<RepositoryDetailDto>.Success(MapToDetail(repository));
    }

    public async Task<RepositoryMutationResult<bool>> DeleteAsync(
        Guid systemId,
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, userId, isAdministrator, cancellationToken) is null)
        {
            return RepositoryMutationResult<bool>.SystemNotFound;
        }

        RepositoryEntity? repository = await repositoryRepository.GetBySystemAndIdAsync(systemId, repositoryId, cancellationToken);
        if (repository is null)
        {
            return RepositoryMutationResult<bool>.RepositoryNotFound;
        }

        repositoryRepository.Remove(repository);
        await repositoryRepository.SaveChangesAsync(cancellationToken);

        return RepositoryMutationResult<bool>.Success(true);
    }

    private async Task<SystemEntity?> GetVisibleSystemAsync(
        Guid systemId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken)
    {
        return await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken);
    }

    private static RepositorySummaryDto MapToSummary(RepositoryEntity repository)
    {
        return new RepositorySummaryDto
        {
            Id = repository.Id,
            SystemId = repository.SystemId,
            Name = repository.Name,
            ScanStatus = "未扫描",
            LastScannedAt = null,
            DocumentCount = 0,
        };
    }

    private static RepositoryDetailDto MapToDetail(RepositoryEntity repository)
    {
        return new RepositoryDetailDto
        {
            Id = repository.Id,
            SystemId = repository.SystemId,
            Name = repository.Name,
            GitUrl = repository.GitUrl,
            Description = repository.Description,
            Context = repository.Context,
            ScanStatus = "未扫描",
            LastScannedAt = null,
            DocumentCount = 0,
        };
    }
}
