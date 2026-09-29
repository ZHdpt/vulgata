using Npgsql;
using Vulgata.Tests.PreparedPass2;

namespace Vulgata.Tests.TestInfrastructure;

/// <summary>Controlled imported marker data only; no model/provider execution or invented manifests.</summary>
public sealed class SeededTargetTests
{
    [Trait("Suite", "External")]
    [SuiteFact("External")]
    public async Task ImportedTarget_AndT124Clone_StayInOwnedInstance()
    {
        string source = Environment.GetEnvironmentVariable("VULGATA_T124_SOURCE_CONNECTION") ??
            throw new InvalidOperationException("This import probe requires its explicit local seed dump.");
        Assert.True(StringComparer.Ordinal.Equals(source, Environment.GetEnvironmentVariable("VULGATA_T062_QUALIFICATION_CONNECTION")),
            "The runner must wire identical T062 and T124 seed targets.");
        TestResourceScope.ValidateOwnedTarget(source);
        await using (var connection = new NpgsqlConnection(source))
        {
            await connection.OpenAsync();
            await using var marker = new NpgsqlCommand("SELECT value FROM public.isolation_seed_probe;", connection);
            Assert.Equal("controlled-local-seed", await marker.ExecuteScalarAsync());
        }
        string admin = Environment.GetEnvironmentVariable("VULGATA_T124_POSTGRES_ADMIN")!;
        await using var clone = await Pass2ProductionQualificationTests.QualificationDatabase.CloneAsync(admin, source);
        Assert.False(StringComparer.Ordinal.Equals(source, clone.ConnectionString), "The clone must use its own database.");
        TestResourceScope.ValidateOwnedTarget(clone.ConnectionString);
        await using var cloned = new NpgsqlConnection(clone.ConnectionString);
        await cloned.OpenAsync();
        await using var clonedMarker = new NpgsqlCommand("SELECT value FROM public.isolation_seed_probe;", cloned);
        Assert.Equal("controlled-local-seed", await clonedMarker.ExecuteScalarAsync());
    }

    [Trait("Suite", "External")]
    [SuiteFact("External")]
    public async Task Clone_RejectsAmbientAdministratorAndUnrecordedSourceBeforeConnecting()
    {
        string source = Environment.GetEnvironmentVariable("VULGATA_T124_SOURCE_CONNECTION") ??
            throw new InvalidOperationException("This clone probe requires its explicit local seed dump.");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Pass2ProductionQualificationTests.QualificationDatabase.CloneAsync("Host=example.invalid;Database=postgres;Username=app;Password=unused", source));
        var unrecorded = new NpgsqlConnectionStringBuilder(source) { Database = "vulgata_story124_unrecorded" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Pass2ProductionQualificationTests.QualificationDatabase.CloneAsync(TestResourceScope.RequireAdministrator(), unrecorded.ConnectionString));
    }
}
