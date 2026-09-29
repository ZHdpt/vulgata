using Npgsql;

namespace Vulgata.Tests.TestInfrastructure;

/// <summary>Only the runner's disposable loopback container may supply database administration.</summary>
internal static class TestResourceScope
{
    private static readonly object OwnershipLock = new();

    internal static void RecordCreated(string administrator, string databaseName)
    {
        ValidateAdministrator(administrator);
        lock (OwnershipLock)
        {
            string current = Environment.GetEnvironmentVariable("VULGATA_TEST_OWNED_DATABASES") ?? "";
            Environment.SetEnvironmentVariable("VULGATA_TEST_OWNED_DATABASES", current + ";" + databaseName);
        }
    }

    internal static void ValidateOwnedTarget(string connectionString)
        => ValidateOwnedTarget(connectionString,
            Environment.GetEnvironmentVariable("VULGATA_TEST_SUITE"),
            Environment.GetEnvironmentVariable("VULGATA_TEST_RESOURCE_RUN"),
            Environment.GetEnvironmentVariable("VULGATA_TEST_OWNED_ADMIN"),
            (Environment.GetEnvironmentVariable("VULGATA_TEST_OWNED_DATABASES") ?? "").Split(';'));

    internal static void ValidateOwnedTarget(string connectionString, string? suite, string? run,
        string? administrator, IReadOnlyCollection<string> created)
    {
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        string databaseName = target.Database ?? "";
        target.Database = "postgres";
        ValidateAdministrator(target.ConnectionString, suite, run, administrator);
        if (databaseName.Length == 0 || !created.Contains(databaseName, StringComparer.Ordinal))
            throw new InvalidOperationException("The database was not created and recorded by this test run.");
    }

    internal static string RequireAdministrator()
    {
        string value = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN") ?? "";
        ValidateAdministrator(value);
        return value;
    }

    internal static void ValidateAdministrator(string connectionString)
    {
        string? suite = Environment.GetEnvironmentVariable("VULGATA_TEST_SUITE");
        string? run = Environment.GetEnvironmentVariable("VULGATA_TEST_RESOURCE_RUN");
        string? expected = Environment.GetEnvironmentVariable("VULGATA_TEST_OWNED_ADMIN");
        ValidateAdministrator(connectionString, suite, run, expected);
    }

    internal static void ValidateAdministrator(string connectionString, string? suite, string? run, string? expected)
    {
        if (suite is not ("PostgreSql" or "External" or "E2E") ||
            !Guid.TryParseExact(run, "N", out _) || string.IsNullOrWhiteSpace(expected))
        {
            throw new InvalidOperationException("PostgreSQL requires a runner-owned disposable resource scope.");
        }

        var supplied = new NpgsqlConnectionStringBuilder(connectionString);
        var owned = new NpgsqlConnectionStringBuilder(expected);
        if (supplied.Host != "127.0.0.1" || supplied.Port != owned.Port ||
            supplied.Host != owned.Host || supplied.Username != owned.Username ||
            supplied.Password != owned.Password || supplied.Database != "postgres")
        {
            throw new InvalidOperationException("Refusing a PostgreSQL endpoint outside this run's owned container.");
        }
    }
}
