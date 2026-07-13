using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface IDatabaseConnectionTestService
{
    Task<DatabaseConnectionTestAttemptResult> TestAsync(
        DatabaseType databaseType,
        string connectionString,
        string? username,
        string? password,
        CancellationToken cancellationToken = default);
}

public sealed record DatabaseConnectionTestAttemptResult(bool Success, string Message);
