using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Validators;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Web.Data;

public interface IRepositoryDatabaseConnectionCoordinator
{
    Task<RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>> GetAsync(
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>> UpsertAsync(
        Guid repositoryId,
        UpsertDatabaseConnectionRequest request,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<RepositoryDatabaseConnectionMutationResult<bool>> DeleteAsync(
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<RepositoryDatabaseConnectionTestResult> TestConnectionAsync(
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}

public enum RepositoryDatabaseConnectionMutationOutcome
{
    Success,
    RepositoryNotFound,
}

public sealed record RepositoryDatabaseConnectionMutationResult<T>(
    RepositoryDatabaseConnectionMutationOutcome Outcome,
    T? Value = default,
    string? Message = null)
{
    public static RepositoryDatabaseConnectionMutationResult<T> Success(T value) =>
        new(RepositoryDatabaseConnectionMutationOutcome.Success, value);

    public static RepositoryDatabaseConnectionMutationResult<T> RepositoryNotFound { get; } =
        new(RepositoryDatabaseConnectionMutationOutcome.RepositoryNotFound, default, "仓库不存在。");
}

public enum RepositoryDatabaseConnectionTestOutcome
{
    Success,
    RepositoryNotFound,
    NotConfigured,
    Failed,
}

public sealed record RepositoryDatabaseConnectionTestResult(
    RepositoryDatabaseConnectionTestOutcome Outcome,
    string Message)
{
    public static RepositoryDatabaseConnectionTestResult Success(string message) =>
        new(RepositoryDatabaseConnectionTestOutcome.Success, message);

    public static RepositoryDatabaseConnectionTestResult RepositoryNotFound { get; } =
        new(RepositoryDatabaseConnectionTestOutcome.RepositoryNotFound, "仓库不存在。");

    public static RepositoryDatabaseConnectionTestResult NotConfigured { get; } =
        new(RepositoryDatabaseConnectionTestOutcome.NotConfigured, "仓库尚未配置数据库连接。");

    public static RepositoryDatabaseConnectionTestResult Failed(string message) =>
        new(RepositoryDatabaseConnectionTestOutcome.Failed, message);
}

public sealed class RepositoryDatabaseConnectionCoordinator(
    IRepositoryRepository repositoryRepository,
    ISystemRepository systemRepository,
    IDatabaseConnectionRepository databaseConnectionRepository,
    IDatabaseConnectionEncryptionService encryptionService,
    IDatabaseConnectionTestService connectionTestService)
    : IRepositoryDatabaseConnectionCoordinator
{
    public async Task<RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>> GetAsync(
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        RepositoryEntity? repository = await GetVisibleRepositoryAsync(repositoryId, userId, isAdministrator, cancellationToken);
        if (repository is null)
        {
            return RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>.RepositoryNotFound;
        }

        DatabaseConnection? databaseConnection = await databaseConnectionRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
        if (databaseConnection is null)
        {
            return RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>.Success(
                new RepositoryDatabaseConnectionSummaryDto
                {
                    RepositoryId = repository.Id,
                    IsConfigured = false,
                    HasConnectionString = false,
                    HasUsername = false,
                    HasPassword = false,
                    DatabaseType = null,
                    UpdatedAt = null,
                });
        }

        return RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>.Success(MapToSummary(databaseConnection));
    }

    public async Task<RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>> UpsertAsync(
        Guid repositoryId,
        UpsertDatabaseConnectionRequest request,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        RepositoryEntity? repository = await GetVisibleRepositoryAsync(repositoryId, userId, isAdministrator, cancellationToken);
        if (repository is null)
        {
            return RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>.RepositoryNotFound;
        }

        DatabaseConnection? databaseConnection = await databaseConnectionRepository.GetByRepositoryAsync(repository.Id, cancellationToken);

        string encryptedConnectionString = await encryptionService.EncryptAsync(request.ConnectionString, cancellationToken);

        if (databaseConnection is null)
        {
            string? encryptedUsername = await EncryptOptionalAsync(request.Username, cancellationToken);
            string? encryptedPassword = await EncryptOptionalAsync(request.Password, cancellationToken);

            DatabaseConnection created = DatabaseConnection.Create(
                repository.Id,
                encryptedConnectionString,
                request.DatabaseType,
                encryptedUsername,
                encryptedPassword,
                DateTimeOffset.UtcNow);

            await databaseConnectionRepository.AddAsync(created, cancellationToken);
            await databaseConnectionRepository.SaveChangesAsync(cancellationToken);

            return RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>.Success(MapToSummary(created));
        }

        string? updatedEncryptedUsername = databaseConnection.EncryptedUsername;
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            updatedEncryptedUsername = await encryptionService.EncryptAsync(request.Username, cancellationToken);
        }

        string? updatedEncryptedPassword = databaseConnection.EncryptedPassword;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            updatedEncryptedPassword = await encryptionService.EncryptAsync(request.Password, cancellationToken);
        }

        databaseConnection.UpdateEncryptedDetails(
            encryptedConnectionString,
            request.DatabaseType,
            updatedEncryptedUsername,
            updatedEncryptedPassword,
            DateTimeOffset.UtcNow);

        await databaseConnectionRepository.UpdateAsync(databaseConnection, cancellationToken);
        await databaseConnectionRepository.SaveChangesAsync(cancellationToken);

        return RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>.Success(MapToSummary(databaseConnection));
    }

    public async Task<RepositoryDatabaseConnectionMutationResult<bool>> DeleteAsync(
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        RepositoryEntity? repository = await GetVisibleRepositoryAsync(repositoryId, userId, isAdministrator, cancellationToken);
        if (repository is null)
        {
            return RepositoryDatabaseConnectionMutationResult<bool>.RepositoryNotFound;
        }

        DatabaseConnection? databaseConnection = await databaseConnectionRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
        if (databaseConnection is null)
        {
            return RepositoryDatabaseConnectionMutationResult<bool>.Success(true);
        }

        await databaseConnectionRepository.DeleteAsync(databaseConnection, cancellationToken);
        await databaseConnectionRepository.SaveChangesAsync(cancellationToken);

        return RepositoryDatabaseConnectionMutationResult<bool>.Success(true);
    }

    public async Task<RepositoryDatabaseConnectionTestResult> TestConnectionAsync(
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        RepositoryEntity? repository = await GetVisibleRepositoryAsync(repositoryId, userId, isAdministrator, cancellationToken);
        if (repository is null)
        {
            return RepositoryDatabaseConnectionTestResult.RepositoryNotFound;
        }

        DatabaseConnection? databaseConnection = await databaseConnectionRepository.GetByRepositoryAsync(repository.Id, cancellationToken);
        if (databaseConnection is null)
        {
            return RepositoryDatabaseConnectionTestResult.NotConfigured;
        }

        string plainConnectionString = await encryptionService.DecryptAsync(databaseConnection.EncryptedConnectionString, cancellationToken);
        string plainUsername = await encryptionService.DecryptAsync(databaseConnection.EncryptedUsername ?? string.Empty, cancellationToken);
        string plainPassword = await encryptionService.DecryptAsync(databaseConnection.EncryptedPassword ?? string.Empty, cancellationToken);

        DatabaseConnectionTestAttemptResult result = await connectionTestService.TestAsync(
            databaseConnection.DatabaseType,
            plainConnectionString,
            string.IsNullOrWhiteSpace(plainUsername) ? null : plainUsername,
            string.IsNullOrWhiteSpace(plainPassword) ? null : plainPassword,
            cancellationToken);

        return result.Success
            ? RepositoryDatabaseConnectionTestResult.Success(result.Message)
            : RepositoryDatabaseConnectionTestResult.Failed($"连接测试失败：{result.Message}");
    }

    private async Task<string?> EncryptOptionalAsync(string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return await encryptionService.EncryptAsync(value, cancellationToken);
    }

    private async Task<RepositoryEntity?> GetVisibleRepositoryAsync(
        Guid repositoryId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken)
    {
        RepositoryEntity? repository = await repositoryRepository.GetByIdAsync(repositoryId, cancellationToken);
        if (repository is null)
        {
            return null;
        }

        if (!repository.SystemId.HasValue)
        {
            return repository;
        }

        Vulgata.Core.Entities.System? visibleSystem = await systemRepository.GetVisibleByIdAsync(
            repository.SystemId.Value,
            userId,
            isAdministrator,
            cancellationToken);

        return visibleSystem is null ? null : repository;
    }

    private static RepositoryDatabaseConnectionSummaryDto MapToSummary(DatabaseConnection databaseConnection)
    {
        return new RepositoryDatabaseConnectionSummaryDto
        {
            RepositoryId = databaseConnection.RepositoryId,
            DatabaseType = (int)databaseConnection.DatabaseType,
            IsConfigured = true,
            HasConnectionString = !string.IsNullOrWhiteSpace(databaseConnection.EncryptedConnectionString),
            HasUsername = !string.IsNullOrWhiteSpace(databaseConnection.EncryptedUsername),
            HasPassword = !string.IsNullOrWhiteSpace(databaseConnection.EncryptedPassword),
            UpdatedAt = databaseConnection.UpdatedAt,
        };
    }
}
