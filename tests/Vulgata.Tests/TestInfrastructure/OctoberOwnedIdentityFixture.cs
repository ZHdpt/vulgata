using System.Globalization;
using Npgsql;

namespace Vulgata.Tests.TestInfrastructure;

internal sealed class OctoberOwnedIdentityFixture : IAsyncDisposable
{
    private const string DatabasePrefix = "vulgata_story184_identity_";
    private readonly string _administratorConnectionString;
    private bool _disposed;

    private OctoberOwnedIdentityFixture(
        string runId,
        string databaseName,
        string administratorConnectionString,
        string connectionString,
        string canonicalDatabaseIdentity)
    {
        RunId = runId;
        DatabaseName = databaseName;
        _administratorConnectionString = administratorConnectionString;
        ConnectionString = connectionString;
        CanonicalDatabaseIdentity = canonicalDatabaseIdentity;
    }

    internal string RunId { get; }
    internal string DatabaseName { get; }
    internal string ConnectionString { get; }
    internal string CanonicalDatabaseIdentity { get; }

    internal static async Task<OctoberOwnedIdentityFixture> CreateAsync()
    {
        string runId = Environment.GetEnvironmentVariable("VULGATA_TEST_RESOURCE_RUN") ?? string.Empty;
        if (!Guid.TryParseExact(runId, "N", out _))
            throw new InvalidOperationException("The PostgreSQL suite must supply its owned run identifier.");

        string administrator = TestResourceScope.RequireAdministrator();
        TestResourceScope.ValidateAdministrator(administrator);
        NpgsqlConnectionStringBuilder administratorBuilder = new(administrator)
        {
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 15,
        };
        string databaseName = DatabasePrefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        bool databaseCreated = false;
        string? targetConnectionString = null;

        try
        {
            await using (NpgsqlConnection connection = new(administratorBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                int majorVersion = Convert.ToInt32(await new NpgsqlCommand(
                    "SELECT current_setting('server_version_num')::integer;", connection)
                    .ExecuteScalarAsync(), CultureInfo.InvariantCulture) / 10000;
                if (majorVersion != 17)
                    throw new InvalidOperationException("The owned PostgreSQL fixture must be version 17.");

                await using NpgsqlCommand exists = new(
                    "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
                exists.Parameters.AddWithValue("name", databaseName);
                if ((bool)(await exists.ExecuteScalarAsync() ?? true))
                    throw new InvalidOperationException("The generated owned database name already exists.");

                await using NpgsqlCommand create = new($"CREATE DATABASE \"{databaseName}\";", connection);
                await create.ExecuteNonQueryAsync();
                databaseCreated = true;
                TestResourceScope.RecordCreated(administratorBuilder.ConnectionString, databaseName);
            }

            NpgsqlConnectionStringBuilder targetBuilder = new(administratorBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false,
                Timeout = 5,
                CommandTimeout = 30,
            };
            targetConnectionString = targetBuilder.ConnectionString;
            TestResourceScope.ValidateOwnedTarget(targetConnectionString);
            string canonicalDatabaseIdentity = string.Create(CultureInfo.InvariantCulture,
                $"postgres://{targetBuilder.Host}:{targetBuilder.Port}/{databaseName}");

            return new OctoberOwnedIdentityFixture(
                runId,
                databaseName,
                administratorBuilder.ConnectionString,
                targetConnectionString,
                canonicalDatabaseIdentity);
        }
        catch
        {
            if (databaseCreated)
                await DropOwnedDatabaseAsync(administratorBuilder.ConnectionString, databaseName,
                    targetConnectionString ?? new NpgsqlConnectionStringBuilder(administratorBuilder.ConnectionString)
                    {
                        Database = databaseName,
                        Pooling = false,
                    }.ConnectionString);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await DropOwnedDatabaseAsync(_administratorConnectionString, DatabaseName, ConnectionString);
    }

    private static async Task DropOwnedDatabaseAsync(
        string administrator,
        string databaseName,
        string targetConnectionString)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                databaseName, "^vulgata_story184_identity_[0-9a-f]{32}$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Refusing to drop a database outside the owned identity fixture name.");

        TestResourceScope.ValidateOwnedTarget(targetConnectionString);
        NpgsqlConnectionStringBuilder administratorBuilder = new(administrator)
        {
            Pooling = false,
            Timeout = 5,
        };
        TestResourceScope.ValidateAdministrator(administratorBuilder.ConnectionString);
        await using NpgsqlConnection connection = new(administratorBuilder.ConnectionString);
        await connection.OpenAsync();
        await using (NpgsqlCommand terminate = new(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid();",
            connection))
        {
            terminate.Parameters.AddWithValue("name", databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using (NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS \"{databaseName}\";", connection))
            await drop.ExecuteNonQueryAsync();

        await using NpgsqlCommand exists = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
        exists.Parameters.AddWithValue("name", databaseName);
        if ((bool)(await exists.ExecuteScalarAsync() ?? true))
            throw new InvalidOperationException("The owned identity database remained after cleanup.");
    }
}
