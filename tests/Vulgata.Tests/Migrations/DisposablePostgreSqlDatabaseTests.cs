using Vulgata.Tests.TestInfrastructure;
using Npgsql;

namespace Vulgata.Tests.Migrations;

public sealed class DisposablePostgreSqlDatabaseTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void CreateUniqueDatabaseName_RepeatedCalls_ReturnDistinctStronglyOwnedNames()
    {
        string first = DisposablePostgreSqlDatabase.CreateUniqueDatabaseName();
        string second = DisposablePostgreSqlDatabase.CreateUniqueDatabaseName();

        Assert.NotEqual(first, second);
        Assert.StartsWith(DisposablePostgreSqlDatabase.DatabaseNamePrefix, first);
        Assert.Equal(49, first.Length);
        DisposablePostgreSqlDatabase.ValidateOwnedDatabaseName(first);
        DisposablePostgreSqlDatabase.ValidateOwnedDatabaseName(second);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("postgres")]
    [InlineData("template0")]
    [InlineData("template1")]
    [InlineData("vulgata")]
    [InlineData("vulgata_story121_")]
    [InlineData("vulgata_story121_0000000000000000000000000000000")]
    [InlineData("vulgata_story121_000000000000000000000000000000000")]
    [InlineData("VULGATA_STORY121_00000000000000000000000000000000")]
    [InlineData("vulgata_story120_00000000000000000000000000000000")]
    [InlineData("vulgata_story121_0000000000000000000000000000000g")]
    [InlineData("vulgata_story121_00000000000000000000000000000000;drop database postgres")]
    public void ValidateOwnedDatabaseName_UnownedOrProtectedName_RefusesOperation(
        string? databaseName)
    {
        Assert.Throws<InvalidOperationException>(
            () => DisposablePostgreSqlDatabase.ValidateOwnedDatabaseName(databaseName));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateAsync_HarnessOwnedAdministratorDatabase_RefusesBeforeConnecting()
    {
        string administratorDatabaseName =
            DisposablePostgreSqlDatabase.CreateUniqueDatabaseName();
        string administratorConnectionString =
            $"Host=example.invalid;Database={administratorDatabaseName};Username=test;Password=test";

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DisposablePostgreSqlDatabase.CreateAsync(
                administratorConnectionString));

        Assert.Contains("administrator database", exception.Message, StringComparison.Ordinal);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateAsync_ConfiguredPostgreSql17_CreatesUsesAndDropsOnlyOwnedDatabase()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            throw new InvalidOperationException("Required PostgreSQL qualification prerequisite is unavailable; use eng/test.ps1 -Suite PostgreSql.");
        }

        var administrator = new NpgsqlConnectionStringBuilder(administratorConnectionString)
        {
            Pooling = false,
        };

        string databaseName;
        await using (var database = await DisposablePostgreSqlDatabase.CreateAsync(
            administrator.ConnectionString))
        {
            databaseName = database.DatabaseName;
            Assert.True(await database.ExistsAsync());
            await using NpgsqlConnection target = await database.OpenConnectionAsync();
            Assert.Equal(databaseName, target.Database);
        }

        await using var verification = new NpgsqlConnection(administrator.ConnectionString);
        await verification.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            verification);
        command.Parameters.AddWithValue("name", databaseName);
        Assert.False((bool)(await command.ExecuteScalarAsync() ?? true));
    }
}
