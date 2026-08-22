using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedScanning;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T009 RED slice.  These tests enter through the prepared Pass 1
/// store seam and prove that start is a pair-wide operation: the store must
/// reconstruct exactly two current, authorized Story 12.2 manifests before
/// exposing any deterministic Story 12.1 jobs or navigation budgets.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1PairStartTests
{
    [Fact]
    public async Task StartAsync_ExactApprovedPair_CreatesCompletePendingJobSetWithReusedStory12Point1IdentityAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureExecutionsToStory12Point3Async(fixture);
        IPreparedPass1Store store = CreateStore(fixture.Context);

        dynamic result = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            concurrencyLimit: 2,
            CancellationToken.None);

        Assert.Equal("Created", result.Outcome.ToString());
        Assert.Equal(1, await fixture.Context.Pass1PairExecutions.CountAsync());
        Assert.Equal(fixture.Package.SelectedCount, await fixture.Context.ScanJobExecutions.CountAsync());
        Assert.Equal(fixture.Package.SelectedCount, await fixture.Context.Pass1NavigationBudgets.CountAsync());

        Pass1PairExecution pair = await fixture.Context.Pass1PairExecutions.SingleAsync();
        Assert.Equal(fixture.Package.CoordinationId, pair.CoordinationId);
        Assert.Equal(2, pair.InitialConcurrencyLimit);
        Assert.Equal(2, pair.ConcurrencyLimit);
        Assert.Equal(fixture.Package.SelectedCount, pair.DispatchedFileCount);
        Assert.Equal(Pass1PairExecutionPhase.Running, pair.Phase);

        Guid[] expectedJobIds = [
            .. fixture.Package.Repositories
                .SelectMany(repository => repository.FileCodeUnits)
                .Select(unit => ScanJobExecution.Create(
                    FindScanExecutionId(fixture, unit.RepositoryId),
                    unit.RepositoryId,
                    unit.ScanRunId,
                    unit.Id,
                    unit.ManifestOrdinal,
                    PreparedScanTestHost.FixedUtcNow).Id)
                .OrderBy(id => id),
        ];
        Assert.Equal(
            expectedJobIds,
            (await fixture.Context.ScanJobExecutions
                .Select(job => job.Id)
                .ToListAsync())
                .OrderBy(id => id));

        Assert.All(
            await fixture.Context.ScanJobExecutions.ToListAsync(),
            job => Assert.Equal(ScanJobExecutionState.Pending, job.State));
        Assert.Equal(
            ScanExecutionPhase.Running,
            (await fixture.Context.ScanExecutions
                .OrderBy(execution => execution.RepositoryId)
                .ToListAsync())
                .First().Phase);
        Assert.Equal(
            TwoRepositoryScanCoordinationPhase.Pass1,
            (await fixture.Context.TwoRepositoryScanCoordinations.SingleAsync()).Phase);
    }

    [Fact]
    public async Task StartAsync_MissingOrMismatchedPair_IsRejectedBeforeAnyPass1RowsAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureExecutionsToStory12Point3Async(fixture);
        IPreparedPass1Store store = CreateStore(fixture.Context);
        PreparedScanHandoff exact = CreateHandoff(fixture.Package);

        PreparedScanHandoff oneManifest = exact with
        {
            Manifests = [exact.Manifests[0]],
        };
        dynamic missing = await store.StartAsync(
            fixture.Authorization,
            oneManifest,
            concurrencyLimit: 2,
            CancellationToken.None);

        Assert.NotEqual("Created", missing.Outcome.ToString());
        Assert.Equal(0, await fixture.Context.Pass1PairExecutions.CountAsync());
        Assert.Equal(0, await fixture.Context.ScanJobExecutions.CountAsync());
        Assert.Equal(0, await fixture.Context.Pass1NavigationBudgets.CountAsync());

        PreparedScanHandoff mismatched = exact with
        {
            PairPackageFingerprint = new string('f', 64),
        };
        dynamic conflict = await store.StartAsync(
            fixture.Authorization,
            mismatched,
            concurrencyLimit: 2,
            CancellationToken.None);

        Assert.NotEqual("Created", conflict.Outcome.ToString());
        Assert.Equal(0, await fixture.Context.Pass1PairExecutions.CountAsync());
        Assert.Equal(0, await fixture.Context.ScanJobExecutions.CountAsync());
        Assert.Equal(0, await fixture.Context.Pass1NavigationBudgets.CountAsync());
    }

    [Fact]
    public async Task StartAsync_OutsideCurrentAuthorization_IsIndistinguishableAndMutationFreeAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureExecutionsToStory12Point3Async(fixture);
        IPreparedPass1Store store = CreateStore(fixture.Context);
        PreparedScanAuthorizationScope outsideScope = fixture.Authorization with
        {
            RepositoryIds = [fixture.Package.Repositories[0].RepositoryId],
        };

        dynamic result = await store.StartAsync(
            outsideScope,
            CreateHandoff(fixture.Package),
            concurrencyLimit: 2,
            CancellationToken.None);

        Assert.NotEqual("Created", result.Outcome.ToString());
        Assert.Equal(0, await fixture.Context.Pass1PairExecutions.CountAsync());
        Assert.Equal(0, await fixture.Context.ScanJobExecutions.CountAsync());
        Assert.Equal(0, await fixture.Context.Pass1NavigationBudgets.CountAsync());
        Assert.Equal(
            TwoRepositoryScanCoordinationPhase.Preparing,
            (await fixture.Context.TwoRepositoryScanCoordinations.SingleAsync()).Phase);
    }

    [Fact]
    public async Task StartAsync_ExactReplayIsZeroWriteAndChangedInitialLimitIsConflictAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureExecutionsToStory12Point3Async(fixture);
        IPreparedPass1Store store = CreateStore(fixture.Context);
        PreparedScanHandoff handoff = CreateHandoff(fixture.Package);

        dynamic created = await store.StartAsync(
            fixture.Authorization,
            handoff,
            concurrencyLimit: 2,
            CancellationToken.None);
        int pairCount = await fixture.Context.Pass1PairExecutions.CountAsync();
        int jobCount = await fixture.Context.ScanJobExecutions.CountAsync();
        int budgetCount = await fixture.Context.Pass1NavigationBudgets.CountAsync();
        long pairVersion = (await fixture.Context.Pass1PairExecutions.SingleAsync()).StateVersion;

        dynamic replay = await store.StartAsync(
            fixture.Authorization,
            handoff,
            concurrencyLimit: 2,
            CancellationToken.None);
        dynamic conflict = await store.StartAsync(
            fixture.Authorization,
            handoff,
            concurrencyLimit: 3,
            CancellationToken.None);

        Assert.Equal("Created", created.Outcome.ToString());
        Assert.Equal("Replay", replay.Outcome.ToString());
        Assert.Equal("Conflict", conflict.Outcome.ToString());
        Assert.Equal(pairCount, await fixture.Context.Pass1PairExecutions.CountAsync());
        Assert.Equal(jobCount, await fixture.Context.ScanJobExecutions.CountAsync());
        Assert.Equal(budgetCount, await fixture.Context.Pass1NavigationBudgets.CountAsync());
        Assert.Equal(pairVersion, (await fixture.Context.Pass1PairExecutions.SingleAsync()).StateVersion);
        Assert.Equal(2, (await fixture.Context.Pass1PairExecutions.SingleAsync()).InitialConcurrencyLimit);
    }

    private static async Task CommitPreparedPairAsync(PreparedScanPostgreSqlFixture fixture)
    {
        PreparedScanCommitResult result = await PreparedScanPostgreSqlFixture
            .CreateStore(fixture.Context)
            .CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, result.Outcome);
    }

    private static async Task PromoteFixtureExecutionsToStory12Point3Async(
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

        for (int index = 0; index < repositoryIds.Length; index++)
        {
            Guid repositoryId = repositoryIds[index];
            await fixture.Context.ScanExecutions
                .Where(execution => execution.RepositoryId == repositoryId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(execution => execution.PreScanResultId, Guid.Empty)
                    .SetProperty(execution => execution.Phase, ScanExecutionPhase.Prepared)
                    .SetProperty(execution => execution.PromptIdentity, $"story-12-3-test-prompt-{index + 1}")
                    .SetProperty(execution => execution.DocumentSchemaIdentity, documentSchemaIdentity)
                    .SetProperty(execution => execution.AnalysisRuntimeIdentityJson, runtimeIdentityJson)
                    .SetProperty(execution => execution.AnalysisRuntimeIdentityHash, runtimeIdentityHash)
                    .SetProperty(execution => execution.UpdatedAt, PreparedScanTestHost.FixedUtcNow));
        }

        fixture.Context.ChangeTracker.Clear();
    }

    private static IPreparedPass1Store CreateStore(VulgataDbContext context)
    {
        Type storeType = typeof(PreparedPass1Store);
        Assert.True(typeof(IPreparedPass1Store).IsAssignableFrom(storeType));
        return Assert.IsAssignableFrom<IPreparedPass1Store>(
            Activator.CreateInstance(storeType, context));
    }

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
                        "PreparedScanManifest",
                        repository.ManifestFingerprint),
                    repository.ManifestFingerprint,
                    PreparedScanIdentityBuilders.CreateCodeGraphProof(repository.CodeGraphProof).Fingerprint,
                    PreparedScanIdentityBuilders.CreateJavaReadiness(repository.JavaReadiness).Fingerprint,
                    repository.PreparedScanContextIdentityHash))
                .ToArray(),
            package.Repositories.SelectMany(repository => repository.FileCodeUnits).ToArray(),
            package.SelectedCount);

    private static Guid FindScanExecutionId(
        PreparedScanPostgreSqlFixture fixture,
        Guid repositoryId) =>
        fixture.Context.ScanExecutions
            .AsNoTracking()
            .Single(execution => execution.RepositoryId == repositoryId)
            .Id;

    private static async Task<PreparedScanPostgreSqlFixture> CreateFixtureAsync()
    {
        string administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "T009 requires VULGATA_TEST_POSTGRES_ADMIN for an isolated prepared pair database.");
        return await PreparedScanPostgreSqlFixture.CreateAsync(administrator);
    }
}
