using System.Globalization;
using System.Text.RegularExpressions;
using Npgsql;

namespace Vulgata.Tests.Migrations;

internal sealed partial class DisposablePostgreSqlDatabase : IAsyncDisposable
{
    internal const string DatabaseNamePrefix = "vulgata_story121_";

    private static readonly HashSet<string> _protectedDatabaseNames = new(
        ["postgres", "template0", "template1", "vulgata"],
        StringComparer.OrdinalIgnoreCase);

    private readonly string _administratorConnectionString;
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _cleanupComplete;

    private DisposablePostgreSqlDatabase(
        string administratorConnectionString,
        string databaseName,
        string connectionString)
    {
        _administratorConnectionString = administratorConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    internal string DatabaseName { get; }

    internal string ConnectionString { get; }

    internal static string CreateUniqueDatabaseName() =>
        DatabaseNamePrefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    internal static void ValidateOwnedDatabaseName(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName) ||
            _protectedDatabaseNames.Contains(databaseName) ||
            !OwnedDatabaseNameRegex().IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                "Refusing to operate on a database not uniquely owned by the Story 12.1 test harness.");
        }
    }

    internal static async Task<DisposablePostgreSqlDatabase> CreateAsync(
        string administratorConnectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(administratorConnectionString);
        string databaseName = CreateUniqueDatabaseName();
        ValidateOwnedDatabaseName(databaseName);

        var administrator = new NpgsqlConnectionStringBuilder(administratorConnectionString)
        {
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 15,
        };
        ValidateAdministratorConnection(administrator, databaseName);
        Vulgata.Tests.TestInfrastructure.TestResourceScope.ValidateAdministrator(administratorConnectionString);

        await using var connection = new NpgsqlConnection(administrator.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await RequirePostgreSql17Async(connection, cancellationToken);
        if (await DatabaseExistsAsync(connection, databaseName, cancellationToken))
        {
            throw new InvalidOperationException(
                "The uniquely named Story 12.1 test database already exists.");
        }

        await ExecuteOwnedDatabaseCommandAsync(
            connection, "CREATE DATABASE ", databaseName, cancellationToken);

        Vulgata.Tests.TestInfrastructure.TestResourceScope.RecordCreated(administrator.ConnectionString, databaseName);
        var target = new NpgsqlConnectionStringBuilder(administrator.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        };
        EnsureExactTarget(target, databaseName);
        return new DisposablePostgreSqlDatabase(
            administrator.ConnectionString,
            databaseName,
            target.ConnectionString);
    }

    internal async Task<NpgsqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureExactTarget(new NpgsqlConnectionStringBuilder(ConnectionString), DatabaseName);
        var connection = new NpgsqlConnection(ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            if (!StringComparer.Ordinal.Equals(connection.Database, DatabaseName))
            {
                throw new InvalidOperationException(
                    "The PostgreSQL connection opened a database other than the owned test database.");
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    internal async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        ValidateOwnedDatabaseName(DatabaseName);
        await using var connection = new NpgsqlConnection(_administratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        return await DatabaseExistsAsync(connection, DatabaseName, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _disposeLock.WaitAsync(CancellationToken.None);
        try
        {
            if (_cleanupComplete)
            {
                return;
            }

            await CleanupDatabaseAsync(
                _administratorConnectionString,
                DatabaseName,
                CancellationToken.None);
            _cleanupComplete = true;
        }
        finally
        {
            _disposeLock.Release();
        }
    }

    private static void ValidateAdministratorConnection(
        NpgsqlConnectionStringBuilder administrator,
        string databaseName)
    {
        if (string.IsNullOrWhiteSpace(administrator.Host) ||
            string.IsNullOrWhiteSpace(administrator.Database))
        {
            throw new InvalidOperationException(
                "The PostgreSQL administrator connection must identify a host and administration database.");
        }

        if (StringComparer.Ordinal.Equals(administrator.Database, databaseName) ||
            OwnedDatabaseNameRegex().IsMatch(administrator.Database))
        {
            throw new InvalidOperationException(
                "The PostgreSQL administrator database must be distinct from every harness-owned database.");
        }
    }

    private static void EnsureExactTarget(
        NpgsqlConnectionStringBuilder target,
        string databaseName)
    {
        ValidateOwnedDatabaseName(databaseName);
        if (!StringComparer.Ordinal.Equals(target.Database, databaseName))
        {
            throw new InvalidOperationException(
                "The target connection does not identify the exact harness-owned database.");
        }
    }

    private static async Task RequirePostgreSql17Async(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT current_setting('server_version_num')::integer;",
            connection);
        int serverVersionNumber = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (serverVersionNumber / 10000 != 17)
        {
            throw new InvalidOperationException(
                "The disposable database harness requires PostgreSQL major version 17.");
        }
    }

    private static async Task<bool> DatabaseExistsAsync(
        NpgsqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        ValidateOwnedDatabaseName(databaseName);
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", databaseName);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task CleanupDatabaseAsync(
        string administratorConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        ValidateOwnedDatabaseName(databaseName);
        await using var connection = new NpgsqlConnection(administratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
            "WHERE datname = @name AND pid <> pg_backend_pid();",
            connection))
        {
            terminate.Parameters.AddWithValue("name", databaseName);
            await terminate.ExecuteNonQueryAsync(cancellationToken);
        }

        await ExecuteOwnedDatabaseCommandAsync(
            connection,
            "DROP DATABASE IF EXISTS ",
            databaseName,
            cancellationToken);
        if (await DatabaseExistsAsync(connection, databaseName, cancellationToken))
        {
            throw new InvalidOperationException(
                "The uniquely named Story 12.1 test database still exists after cleanup.");
        }
    }

    private static async Task ExecuteOwnedDatabaseCommandAsync(
        NpgsqlConnection connection,
        string commandPrefix,
        string databaseName,
        CancellationToken cancellationToken)
    {
        ValidateOwnedDatabaseName(databaseName);
        await using var command = new NpgsqlCommand(
            commandPrefix + QuoteIdentifier(databaseName) + ';',
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string QuoteIdentifier(string value) =>
        '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    [GeneratedRegex(
        "^vulgata_story121_[0-9a-f]{32}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex OwnedDatabaseNameRegex();
}
