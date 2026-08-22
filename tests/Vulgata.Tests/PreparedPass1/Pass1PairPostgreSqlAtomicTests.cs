using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedScanning;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T015 RED slice.  PostgreSQL is the authority for the pair-wide
/// start transaction: an injected failure at every durable write boundary
/// must expose no partial queue, and competing claimers must observe only a
/// committed complete queue under the shared pair limit.
///
/// The store is intentionally discovered through reflection.  T016 owns the
/// production PostgreSQL start/claim seam; this test must fail honestly while
/// that seam is absent instead of introducing a test-only implementation.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1PairPostgreSqlAtomicTests
{
    public static TheoryData<string, string, string, string> StartWriteBoundaries => new()
    {
        { "before-pair", "vulgata.\"Pass1PairExecutions\"", "INSERT", "BEFORE" },
        { "after-pair", "vulgata.\"Pass1PairExecutions\"", "INSERT", "AFTER" },
        { "before-job", "vulgata.\"ScanJobExecutions\"", "INSERT", "BEFORE" },
        { "after-job", "vulgata.\"ScanJobExecutions\"", "INSERT", "AFTER" },
        { "before-budget", "vulgata.\"Pass1NavigationBudgets\"", "INSERT", "BEFORE" },
        { "after-budget", "vulgata.\"Pass1NavigationBudgets\"", "INSERT", "AFTER" },
        { "before-child-scan", "vulgata.\"ScanExecutions\"", "UPDATE", "BEFORE" },
        { "after-child-scan", "vulgata.\"ScanExecutions\"", "UPDATE", "AFTER" },
        { "before-coordination", "vulgata.\"TwoRepositoryScanCoordinations\"", "UPDATE", "BEFORE" },
        { "after-coordination", "vulgata.\"TwoRepositoryScanCoordinations\"", "UPDATE", "AFTER" },
    };

    [Theory]
    [MemberData(nameof(StartWriteBoundaries))]
    public async Task PostgreSql17_StartFailureAtEveryWriteBoundary_RollsBackPairQueueAndStateAsync(
        string boundary,
        string table,
        string operation,
        string timing)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        await InstallFailingTriggerAsync(fixture.ConnectionString, boundary, table, operation, timing);
        StartVisibility before = await ReadVisibilityAsync(fixture.ConnectionString);

        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult? result = null;
        Exception? failure = null;
        try
        {
            result = await store.StartAsync(
                fixture.Authorization,
                CreateHandoff(fixture.Package),
                2,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            Assert.True(
                IsInjectedTriggerException(failure),
                $"T015 injected {timing} failure at {boundary} produced an unexpected exception: {failure.GetType().Name}: {failure.Message}");
        }
        else
        {
            Assert.NotNull(result);
            Assert.Equal(PreparedPass1StartOutcome.Failed, result!.Outcome);
        }
        Assert.Equal(before, await ReadVisibilityAsync(fixture.ConnectionString));
    }

    [Fact]
    public async Task PostgreSql17_CompetingClaimers_SeeOnlyCommittedCompleteQueueAndRespectPairLimitAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);

        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);

        Guid pairExecutionId = await ScalarAsync<Guid>(
            fixture.ConnectionString,
            "SELECT \"Id\" FROM vulgata.\"Pass1PairExecutions\" LIMIT 1;");
        StartVisibility committed = await ReadVisibilityAsync(fixture.ConnectionString);
        Assert.True(committed.PairExecutions == 1 && committed.Jobs > 0 && committed.Budgets == committed.Jobs);

        await using VulgataDbContext secondContext =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        IPreparedPass1Store firstStore = CreatePass1Store(fixture.Context);
        IPreparedPass1Store secondStore = CreatePass1Store(secondContext);
        Task<PreparedPass1ClaimResult> firstClaim = firstStore.ClaimNextAsync(
            pairExecutionId,
            PreparedScanTestHost.FixedUtcNow,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        Task<PreparedPass1ClaimResult> secondClaim = secondStore.ClaimNextAsync(
            pairExecutionId,
            PreparedScanTestHost.FixedUtcNow,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        PreparedPass1ClaimResult[] claims = await Task.WhenAll(firstClaim, secondClaim);

        Assert.All(claims, claimed => Assert.Equal(PreparedPass1ClaimOutcome.Claimed, claimed.Outcome));
        Guid[] claimedJobIds = claims.Select(claimed => claimed.JobId).ToArray();
        Assert.Equal(2, claimedJobIds.Distinct().Count());
        Assert.Equal(
            2,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"ScanJobExecutions\" WHERE \"State\" = 'Running';"));
        Assert.Equal(committed, await ReadVisibilityAsync(fixture.ConnectionString));
    }

    private static IPreparedPass1Store CreatePass1Store(VulgataDbContext context) =>
        new PreparedPass1Store(context);

    private static PreparedScanHandoff CreateHandoff(PreparedScanPairPackage package) =>
        new(
            PreScanResult.PreparedHandoffSchemaIdentity,
            package.CoordinationId,
            package.PairSelectionFingerprint,
            package.PairPackageFingerprint,
            package.Repositories
                .OrderBy(repository => repository.RepositoryId)
                .Select(repository => new PreparedScanManifestBinding(
                    repository.RepositoryId,
                    repository.ScanRunId,
                    repository.ProfiledRevision,
                    PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                        "PreparedScanManifest", repository.ManifestFingerprint),
                    repository.ManifestFingerprint,
                    PreparedScanIdentityBuilders.CreateCodeGraphProof(repository.CodeGraphProof).Fingerprint,
                    PreparedScanIdentityBuilders.CreateJavaReadiness(repository.JavaReadiness).Fingerprint,
                    repository.PreparedScanContextIdentityHash))
                .ToArray(),
            package.Repositories.SelectMany(repository => repository.FileCodeUnits).ToArray(),
            package.SelectedCount);

    private static async Task CommitPreparedPairAsync(PreparedScanPostgreSqlFixture fixture)
    {
        PreparedScanCommitResult result = await PreparedScanPostgreSqlFixture
            .CreateStore(fixture.Context)
            .CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, result.Outcome);
    }

    private static async Task PromoteFixtureScanExecutionsAsync(
        PreparedScanPostgreSqlFixture fixture)
    {
        const string documentSchemaIdentity = "code-logic/file-v2";
        const string runtimeIdentityJson = "{\"runtime\":\"story-12-3-test\"}";
        string runtimeIdentityHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(runtimeIdentityJson)));
        Guid[] repositoryIds = fixture.Package.Repositories
            .Select(repository => repository.RepositoryId)
            .OrderBy(repositoryId => repositoryId)
            .ToArray();
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;

        for (int index = 0; index < repositoryIds.Length; index++)
        {
            int updated = await fixture.Context.ScanExecutions
                .Where(execution => execution.RepositoryId == repositoryIds[index])
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(execution => execution.PreScanResultId, Guid.Empty)
                    .SetProperty(execution => execution.Phase, ScanExecutionPhase.Prepared)
                    .SetProperty(execution => execution.PromptIdentity, $"story-12-3-test-prompt-{index + 1}")
                    .SetProperty(execution => execution.DocumentSchemaIdentity, documentSchemaIdentity)
                    .SetProperty(execution => execution.AnalysisRuntimeIdentityJson, runtimeIdentityJson)
                    .SetProperty(execution => execution.AnalysisRuntimeIdentityHash, runtimeIdentityHash)
                    .SetProperty(execution => execution.UpdatedAt, now));
            Assert.Equal(1, updated);
        }

        fixture.Context.ChangeTracker.Clear();
    }

    private static bool IsInjectedTriggerException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.MessageText.Contains("T015 injected start boundary", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task InstallFailingTriggerAsync(
        string connectionString,
        string boundary,
        string table,
        string operation,
        string timing)
    {
        string suffix = boundary.Replace('-', '_');
        await ExecuteSqlAsync(connectionString, $"""
            CREATE OR REPLACE FUNCTION t123_fail_{suffix}() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'T015 injected start boundary: {boundary}';
            END $$;
            CREATE TRIGGER t123_fail_{suffix}_trigger
            {timing} {operation} ON {table}
            FOR EACH ROW EXECUTE FUNCTION t123_fail_{suffix}();
            """);
    }

    private static async Task<StartVisibility> ReadVisibilityAsync(string connectionString) => new(
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"Pass1PairExecutions\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"ScanJobExecutions\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"Pass1NavigationBudgets\";"),
        await ScalarAsync<string>(connectionString, "SELECT coalesce(string_agg(\"Phase\", ',' ORDER BY \"Id\"), '') FROM vulgata.\"ScanExecutions\";"),
        await ScalarAsync<string>(connectionString, "SELECT coalesce(string_agg(\"Phase\", ',' ORDER BY \"Id\"), '') FROM vulgata.\"TwoRepositoryScanCoordinations\";"));

    private static async Task<PreparedScanPostgreSqlFixture> CreateFixtureAsync()
    {
        string administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "T015 requires VULGATA_TEST_POSTGRES_ADMIN for an isolated PostgreSQL 17 database; no integration prerequisite was contacted.");
        return await PreparedScanPostgreSqlFixture.CreateAsync(administrator);
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        object? value = await command.ExecuteScalarAsync();
        if (value is null or DBNull)
        {
            return default!;
        }

        return value is T typed
            ? typed
            : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record StartVisibility(
        int PairExecutions,
        int Jobs,
        int Budgets,
        string ScanPhases,
        string CoordinationPhases);
}
