using Vulgata.Tests.TestInfrastructure;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourcePostgreSqlTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task Migration_PostgreSql17_CreatesSourceSnapshotStore()
    {
        await using RetainedSourcePostgreSqlDatabase database =
            await RetainedSourcePostgreSqlDatabase.CreateAsync();
        await using VulgataDbContext context = CreateContext(database.ConnectionString);

        await context.Database.MigrateAsync();

        Assert.Equal(17, await ReadPostgreSqlMajorAsync(context));
        Assert.NotNull(context.Model.FindEntityType(typeof(SourceSnapshot)));
        Assert.True(await TableExistsAsync(context, "SourceSnapshots"));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task SourceSnapshot_PostgreSql17_DeclaresFixedShapeChecksAndRestrictiveRelationship()
    {
        await using RetainedSourcePostgreSqlDatabase database =
            await RetainedSourcePostgreSqlDatabase.CreateAsync();
        await using VulgataDbContext context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();

        Assert.Equal(17, await ReadPostgreSqlMajorAsync(context));
        Assert.Equal(
            "retained-source-manifest/1.0",
            SourceSnapshot.RetainedSourceManifestSchemaIdentity);

        string[] constraintDefinitions = await ReadConstraintDefinitionsAsync(context);
        Assert.Contains(
            constraintDefinitions,
            definition => definition.Contains("retained-source-manifest/1.0", StringComparison.Ordinal));
        Assert.Contains(
            constraintDefinitions,
            definition => definition.Contains("EntryCount", StringComparison.Ordinal) ||
                definition.Contains("entry_count", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            constraintDefinitions,
            definition => definition.Contains("TotalBlobBytes", StringComparison.Ordinal) ||
                definition.Contains("total_blob_bytes", StringComparison.OrdinalIgnoreCase));

        Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType =
            context.Model.FindEntityType(typeof(SourceSnapshot)) ??
            throw new InvalidOperationException("SourceSnapshot is not part of the PostgreSQL model.");
        Microsoft.EntityFrameworkCore.Metadata.IIndex[] indexes = entityType.GetIndexes().ToArray();
        Assert.Contains(
            indexes,
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(SourceSnapshot.RepositoryId), nameof(SourceSnapshot.ProfiledRevision)]));
        Assert.Contains(
            indexes,
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(SourceSnapshot.InternalStorageKey)]));

        Microsoft.EntityFrameworkCore.Metadata.IForeignKey foreignKey = Assert.Single(
            entityType.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType.ClrType == typeof(Repository));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task SourceSnapshot_PostgreSql17_RejectsDuplicateIdentityStorageAndNegativeCounts()
    {
        await using RetainedSourcePostgreSqlDatabase database =
            await RetainedSourcePostgreSqlDatabase.CreateAsync();
        await using VulgataDbContext context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();

        Guid repositoryId = Guid.Parse("13000000-0000-4000-8000-000000000001");
        Repository repository = CreateRepository(repositoryId);
        context.Repositories.Add(repository);
        SourceSnapshot first = CreateSnapshot(repositoryId, "a", "b", "identity-first");
        context.Set<SourceSnapshot>().Add(first);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        SourceSnapshot duplicateIdentity = CreateSnapshot(repositoryId, "a", "c", "identity-second");
        context.Set<SourceSnapshot>().Add(duplicateIdentity);
        DbUpdateException identityException = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        Assert.Equal(
            "23505",
            Assert.IsType<PostgresException>(identityException.InnerException).SqlState);

        context.ChangeTracker.Clear();
        SourceSnapshot duplicateStorage = CreateSnapshot(repositoryId, "c", "d", "identity-first");
        context.Set<SourceSnapshot>().Add(duplicateStorage);
        DbUpdateException storageException = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        Assert.Equal(
            "23505",
            Assert.IsType<PostgresException>(storageException.InnerException).SqlState);

        await using NpgsqlConnection connection = await database.OpenConnectionAsync();
        await using NpgsqlCommand negative = new(
            "INSERT INTO \"vulgata\".\"SourceSnapshots\" (\"Id\", \"RepositoryId\", \"ProfiledRevision\", " +
            "\"GitObjectFormat\", \"ManifestSchemaIdentity\", \"ManifestSha256\", " +
            "\"EntryCount\", \"TotalBlobBytes\", \"InternalStorageKey\", \"CompletedAt\") " +
            "VALUES (@id, @repositoryId, @revision, 'sha1', 'retained-source-manifest/1.0', " +
            "@manifest, -1, 0, @storage, @completedAt);",
            connection);
        negative.Parameters.AddWithValue("id", Guid.NewGuid());
        negative.Parameters.AddWithValue("repositoryId", repositoryId);
        negative.Parameters.AddWithValue("revision", new string('e', 40));
        negative.Parameters.AddWithValue("manifest", new string('f', 64));
        negative.Parameters.AddWithValue("storage", "snapshots/negative");
        negative.Parameters.AddWithValue("completedAt", DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<PostgresException>(() => negative.ExecuteNonQueryAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task SourceSnapshot_PostgreSql17_RejectsMutationAndRestrictsRepositoryDeletion()
    {
        await using RetainedSourcePostgreSqlDatabase database =
            await RetainedSourcePostgreSqlDatabase.CreateAsync();
        await using VulgataDbContext context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();

        Guid repositoryId = Guid.Parse("13000000-0000-4000-8000-000000000002");
        Repository repository = CreateRepository(repositoryId);
        context.Repositories.Add(repository);
        SourceSnapshot snapshot = CreateSnapshot(repositoryId, "a", "b", "mutation-test");
        context.Set<SourceSnapshot>().Add(snapshot);
        await context.SaveChangesAsync();

        await using NpgsqlConnection connection = await database.OpenConnectionAsync();
        await using NpgsqlCommand update = new(
            "UPDATE \"vulgata\".\"SourceSnapshots\" SET \"ManifestSha256\" = @manifest WHERE \"Id\" = @id;",
            connection);
        update.Parameters.AddWithValue("manifest", new string('c', 64));
        update.Parameters.AddWithValue("id", snapshot.Id);
        await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());

        context.ChangeTracker.Clear();
        Repository stored = await context.Repositories.SingleAsync(item => item.Id == repositoryId);
        context.Repositories.Remove(stored);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptionsBuilder<VulgataDbContext> options = new();
        options.UseNpgsql(connectionString);
        return new VulgataDbContext(options.Options);
    }

    private static Repository CreateRepository(Guid id)
    {
        Repository repository = Repository.CreateStandalone(
            "retained-source-postgres-fixture",
            "https://example.invalid/retained-source-fixture.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        typeof(Repository)
            .GetProperty(nameof(Repository.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(repository, id);
        return repository;
    }

    private static SourceSnapshot CreateSnapshot(
        Guid repositoryId,
        string revisionSeed,
        string digestSeed,
        string storageSeed) =>
        SourceSnapshot.Create(
            repositoryId,
            new string(revisionSeed[0], 40),
            "sha1",
            new string(digestSeed[0], 64),
            1,
            1,
            $"snapshots/{repositoryId:N}/{storageSeed}",
            DateTimeOffset.UtcNow);

    private static async Task<int> ReadPostgreSqlMajorAsync(VulgataDbContext context)
    {
        System.Data.Common.DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using System.Data.Common.DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT current_setting('server_version_num')::integer;";
        int version = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        return version / 10000;
    }

    private static async Task<bool> TableExistsAsync(
        VulgataDbContext context,
        string tableName)
    {
        System.Data.Common.DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using System.Data.Common.DbCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT to_regclass('vulgata.\"" + tableName + "\"') IS NOT NULL;";
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<string[]> ReadConstraintDefinitionsAsync(VulgataDbContext context)
    {
        System.Data.Common.DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using System.Data.Common.DbCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint " +
            "WHERE conrelid = 'vulgata.\"SourceSnapshots\"'::regclass;";
        var definitions = new List<string>();
        await using System.Data.Common.DbDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            definitions.Add(reader.GetString(0));
        }

        return [.. definitions];
    }
}

internal sealed class RetainedSourcePostgreSqlDatabase : IAsyncDisposable
{
    private const string DatabaseNamePrefix = "vulgata_story131_";
    private static readonly Regex OwnedNameRegex = new(
        "^vulgata_story131_[0-9a-f]{32}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly string _administratorConnectionString;
    private bool _disposed;

    private RetainedSourcePostgreSqlDatabase(
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

    internal static async Task<RetainedSourcePostgreSqlDatabase> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        string administratorConnectionString = RequireAdministratorConnection();
        Vulgata.Tests.TestInfrastructure.TestResourceScope.ValidateAdministrator(administratorConnectionString);
        var suppliedConnection = new DbConnectionStringBuilder
        {
            ConnectionString = administratorConnectionString,
        };
        if (!suppliedConnection.ContainsKey("Port"))
        {
            throw new InvalidOperationException(
                "Vulgata Story 13.1 PostgreSQL tests require an explicitly supplied administrator port.");
        }

        var administrator = new NpgsqlConnectionStringBuilder(administratorConnectionString)
        {
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 15,
        };
        if (!IPAddress.TryParse(administrator.Host, out IPAddress? address) ||
            !IPAddress.IsLoopback(address) || administrator.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "Vulgata Story 13.1 PostgreSQL tests require a numeric loopback administrator endpoint.");
        }

        string databaseName = DatabaseNamePrefix + Guid.NewGuid().ToString("N");
        ValidateOwnedDatabaseName(databaseName);
        await using var connection = new NpgsqlConnection(administrator.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var create = new NpgsqlCommand(
            "CREATE DATABASE \"" + databaseName + "\";",
            connection))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        Vulgata.Tests.TestInfrastructure.TestResourceScope.RecordCreated(administrator.ConnectionString, databaseName);
        var target = new NpgsqlConnectionStringBuilder(administrator.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        };
        return new(administrator.ConnectionString, databaseName, target.ConnectionString);
    }

    internal async Task<NpgsqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateOwnedDatabaseName(DatabaseName);
        var connection = new NpgsqlConnection(ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            if (!StringComparer.Ordinal.Equals(connection.Database, DatabaseName))
            {
                throw new InvalidOperationException(
                    "The retained-source PostgreSQL connection opened an unexpected database.");
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await using var connection = new NpgsqlConnection(_administratorConnectionString);
        await connection.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
            "WHERE datname = @name AND pid <> pg_backend_pid();",
            connection))
        {
            terminate.Parameters.AddWithValue("name", DatabaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using (var drop = new NpgsqlCommand(
            "DROP DATABASE IF EXISTS \"" + DatabaseName + "\";",
            connection))
        {
            await drop.ExecuteNonQueryAsync();
        }

        await using var verify = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        verify.Parameters.AddWithValue("name", DatabaseName);
        if ((bool)(await verify.ExecuteScalarAsync() ?? true))
        {
            throw new InvalidOperationException(
                "The owned Story 13.1 PostgreSQL database still exists after cleanup.");
        }
    }

    private static string RequireAdministratorConnection()
    {
        string? connection = Environment.GetEnvironmentVariable(
            "VULGATA_STORY131_POSTGRES_ADMIN");
        return string.IsNullOrWhiteSpace(connection)
            ? throw new InvalidOperationException(
                "VULGATA_STORY131_POSTGRES_ADMIN must identify the owned PostgreSQL 17 administrator.")
            : connection;
    }

    private static void ValidateOwnedDatabaseName(string databaseName)
    {
        if (!OwnedNameRegex.IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                "Retained-source tests refuse a database outside the Story 13.1 owned prefix.");
        }
    }
}
