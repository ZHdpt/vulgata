using Microsoft.Data.Sqlite;
using Vulgata.Core.Entities;

namespace Vulgata.Web.Data;

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

public sealed class DatabaseConnectionTestService(ILogger<DatabaseConnectionTestService> logger) : IDatabaseConnectionTestService
{
    public async Task<DatabaseConnectionTestAttemptResult> TestAsync(
        DatabaseType databaseType,
        string connectionString,
        string? username,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (databaseType != DatabaseType.Sqlite)
        {
            logger.LogInformation("Database connection test for type {DatabaseType} is not supported in this version.", databaseType);
            return new DatabaseConnectionTestAttemptResult(
                false,
                "当前版本仅支持 SQLite 自动测试。请在只读环境中手动验证其他数据库类型连接。");
        }

        return await TestSqliteAsync(connectionString, cancellationToken);
    }

    private async Task<DatabaseConnectionTestAttemptResult> TestSqliteAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DatabaseConnectionTestAttemptResult(false, "连接字符串不能为空。无法执行连接测试。");
        }

        try
        {
            await using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            _ = await command.ExecuteScalarAsync(cancellationToken);

            logger.LogInformation("SQLite database connection test succeeded.");
            return new DatabaseConnectionTestAttemptResult(true, "连接测试成功。");
        }
        catch (SqliteException)
        {
            logger.LogWarning("SQLite database connection test failed.");
            return new DatabaseConnectionTestAttemptResult(false, "SQLite 连接失败。请检查连接字符串是否正确，并确认目标库可访问。");
        }
        catch (Exception)
        {
            logger.LogWarning("Unexpected database connection test failure.");
            return new DatabaseConnectionTestAttemptResult(false, "连接测试失败。请检查数据库地址与网络连通性。");
        }
    }
}
