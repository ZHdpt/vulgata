using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Shared.Repositories;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Web.Data;

public interface IRepositoryManagementCoordinator
{
    Task<IReadOnlyList<RepositorySummaryDto>> ListVisibleAsync(Guid systemId, IUserContext user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepositorySummaryDto>> ListStandaloneVisibleAsync(IUserContext user, CancellationToken cancellationToken = default);

    Task<RepositoryMutationResult<RepositoryDetailDto>> CreateAsync(
        Guid systemId,
        CreateRepositoryRequest request,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<RepositoryMutationResult<RepositoryDetailDto>> CreateStandaloneAsync(
        CreateRepositoryRequest request,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<RepositoryMutationResult<bool>> DeleteAsync(
        Guid systemId,
        Guid repositoryId,
        IUserContext user,
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
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, user, cancellationToken) is null)
        {
            return [];
        }

        var repositories = await repositoryRepository.ListBySystemAsync(systemId, cancellationToken);

        return repositories.Select(MapToSummary).ToList();
    }

    public async Task<IReadOnlyList<RepositorySummaryDto>> ListStandaloneVisibleAsync(
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        var repositories = await repositoryRepository.ListStandaloneAsync(cancellationToken);
        return repositories.Select(MapToSummary).ToList();
    }

    public async Task<RepositoryMutationResult<RepositoryDetailDto>> CreateAsync(
        Guid systemId,
        CreateRepositoryRequest request,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        var system = await GetVisibleSystemAsync(systemId, user, cancellationToken);
        if (system is null)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.SystemNotFound;
        }

        bool exists = await repositoryRepository.NameExistsAsync(systemId, request.Name, cancellationToken: cancellationToken);
        if (exists)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.DuplicateName("该系统下的仓库名称已存在。");
        }

        var validationResult = await gitRemoteValidationService.ValidateAsync(request.GitUrl, cancellationToken);
        if (validationResult.Status == GitRemoteValidationStatus.AuthenticationRequired)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.GitAuthenticationRequired(validationResult.Message);
        }

        if (validationResult.Status == GitRemoteValidationStatus.Unreachable)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.GitUnreachable(validationResult.Message);
        }

        var repository = system.AddRepository(
            request.Name,
            request.GitUrl,
            request.Description,
            request.Context,
            DateTimeOffset.UtcNow);

        await repositoryRepository.AddAsync(repository, cancellationToken);
        await repositoryRepository.SaveChangesAsync(cancellationToken);

        return RepositoryMutationResult<RepositoryDetailDto>.Success(MapToDetail(repository));
    }

    public async Task<RepositoryMutationResult<RepositoryDetailDto>> CreateStandaloneAsync(
        CreateRepositoryRequest request,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        bool exists = await repositoryRepository.StandaloneNameExistsAsync(
            request.Name,
            cancellationToken: cancellationToken);
        if (exists)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.DuplicateName("独立仓库名称已存在。");
        }

        var validationResult = await gitRemoteValidationService.ValidateAsync(request.GitUrl, cancellationToken);
        if (validationResult.Status == GitRemoteValidationStatus.AuthenticationRequired)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.GitAuthenticationRequired(validationResult.Message);
        }

        if (validationResult.Status == GitRemoteValidationStatus.Unreachable)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.GitUnreachable(validationResult.Message);
        }

        var repository = RepositoryEntity.CreateStandalone(
            request.Name,
            request.GitUrl,
            request.Description,
            request.Context,
            DateTimeOffset.UtcNow);

        await repositoryRepository.AddAsync(repository, cancellationToken);

        try
        {
            await repositoryRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return RepositoryMutationResult<RepositoryDetailDto>.DuplicateName("独立仓库名称已存在。");
        }

        return RepositoryMutationResult<RepositoryDetailDto>.Success(MapToDetail(repository));
    }

    public async Task<RepositoryMutationResult<bool>> DeleteAsync(
        Guid systemId,
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (await GetVisibleSystemAsync(systemId, user, cancellationToken) is null)
        {
            return RepositoryMutationResult<bool>.SystemNotFound;
        }

        var repository = await repositoryRepository.GetBySystemAndIdAsync(systemId, repositoryId, cancellationToken);
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
        IUserContext user,
        CancellationToken cancellationToken)
    {
        return await systemRepository.GetVisibleByIdAsync(systemId, user.ToSystemOwnerFilter(), cancellationToken);
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
        var databaseConnectionSummary = repository.DatabaseConnection is null
            ? null
            : new RepositoryDatabaseConnectionSummaryDto
            {
                RepositoryId = repository.DatabaseConnection.RepositoryId,
                DatabaseType = (int)repository.DatabaseConnection.DatabaseType,
                IsConfigured = true,
                HasConnectionString = !string.IsNullOrWhiteSpace(repository.DatabaseConnection.EncryptedConnectionString),
                HasUsername = !string.IsNullOrWhiteSpace(repository.DatabaseConnection.EncryptedUsername),
                HasPassword = !string.IsNullOrWhiteSpace(repository.DatabaseConnection.EncryptedPassword),
                UpdatedAt = repository.DatabaseConnection.UpdatedAt,
            };

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
            DatabaseConnection = databaseConnectionSummary,
        };
    }
}
