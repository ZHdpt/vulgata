using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Owns one lowercase Story 12.5 PostgreSQL database for the public
/// no-preseed initiation slice. The fixture deliberately does not seed any
/// scan lifecycle records; the production entrance must create that boundary.
/// </summary>
internal sealed partial class PreparedScanInitiationPostgreSqlFixture : IAsyncDisposable
{
    private const string DatabasePrefix = "vulgata_story125_";
    private readonly string _administratorConnectionString;
    private bool _disposed;

    private PreparedScanInitiationPostgreSqlFixture(
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

    internal static async Task<PreparedScanInitiationPostgreSqlFixture> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        string administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            throw new InvalidOperationException(
                "T011 requires VULGATA_TEST_POSTGRES_ADMIN for an isolated PostgreSQL 17 database.");
        }

        string databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
        ValidateOwnedDatabaseName(databaseName);

        NpgsqlConnectionStringBuilder administrator =
            new(administratorConnectionString)
            {
                Pooling = false,
                Timeout = 5,
                CommandTimeout = 15,
            };
        if (string.IsNullOrWhiteSpace(administrator.Host) ||
            string.IsNullOrWhiteSpace(administrator.Database) ||
            OwnedDatabaseNameRegex().IsMatch(administrator.Database))
        {
            throw new InvalidOperationException(
                "The PostgreSQL administrator connection must target a non-owned administration database.");
        }

        await using NpgsqlConnection connection = new(administrator.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        int version = Convert.ToInt32(
            await new NpgsqlCommand(
                "SELECT current_setting('server_version_num')::integer;",
                connection).ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (version / 10000 != 17)
        {
            throw new InvalidOperationException("T011 requires PostgreSQL major version 17.");
        }

        await using (NpgsqlCommand exists = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection))
        {
            exists.Parameters.AddWithValue("name", databaseName);
            if ((bool)(await exists.ExecuteScalarAsync(cancellationToken) ?? true))
            {
                throw new InvalidOperationException("The generated Story 12.5 database name already exists.");
            }
        }

        await using (NpgsqlCommand create = new(
            $"CREATE DATABASE \"{databaseName}\";",
            connection))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        NpgsqlConnectionStringBuilder target = new(administrator.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        };
        return new(administrator.ConnectionString, databaseName, target.ConnectionString);
    }

    internal VulgataDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(ConnectionString, options =>
                options.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options);

    internal static void ValidateOwnedDatabaseName(string? databaseName)
    {
        if (databaseName is null || !OwnedDatabaseNameRegex().IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                "Refusing to operate on a PostgreSQL database outside the Story 12.5 disposable boundary.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ValidateOwnedDatabaseName(DatabaseName);
        await using NpgsqlConnection connection = new(_administratorConnectionString);
        await connection.OpenAsync();
        await using (NpgsqlCommand terminate = new(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
            "WHERE datname = @name AND pid <> pg_backend_pid();",
            connection))
        {
            terminate.Parameters.AddWithValue("name", DatabaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS \"{DatabaseName}\";", connection);
        await drop.ExecuteNonQueryAsync();
    }

    [GeneratedRegex("^vulgata_story125_[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnedDatabaseNameRegex();
}

/// <summary>
/// PostgreSQL-backed WebApplicationFactory used only by T011. It keeps the
/// normal production endpoint and authentication pipeline while redirecting
/// both EF contexts to the fixture-owned database.
/// </summary>
internal sealed class PreparedScanInitiationPostgreSqlWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    private readonly string _keyRingDirectory = Path.Combine(
        Path.GetTempPath(),
        "vulgata-tests",
        "story125-t011-keys",
        Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
            }));

        builder.ConfigureServices(services =>
        {
            Directory.CreateDirectory(_keyRingDirectory);
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(_keyRingDirectory))
                .SetApplicationName("Vulgata.Tests.Story125.T011");

            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<VulgataDbContext>>();
            services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
            services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
            services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<VulgataDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options
                    .UseNpgsql(connectionString, npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable(
                            "__IdentityMigrationsHistory",
                            "identity");
                        npgsql.EnableRetryOnFailure(3);
                    })
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
            services.AddDbContext<VulgataDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        npgsql.EnableRetryOnFailure(3);
                    })
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning)),
                contextLifetime: ServiceLifetime.Scoped,
                optionsLifetime: ServiceLifetime.Singleton);
            services.AddDbContextFactory<VulgataDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        npgsql.EnableRetryOnFailure(3);
                    })
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_keyRingDirectory))
        {
            Directory.Delete(_keyRingDirectory, recursive: true);
        }
    }
}
