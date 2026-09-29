using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T050 regression RED slices for replay identity integrity.
///
/// The first two tests use the isolated public-store seam so they remain
/// deterministic without PostgreSQL.  The concurrent-writer qualification is
/// intentionally retained by PreparedScanAtomicPostgreSqlTests: SQLite cannot
/// honestly model PostgreSQL serializable writer arbitration.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class PreparedScanStoreRegressionTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PreparedStore_StableProofAndSelectedChildMutation_ConflictsWithoutMutationAsync()
    {
        await using RegressionDatabase database = await RegressionDatabase.CreateAsync();
        PreparedScanStore store = new(database.Context);

        PreparedScanCommitResult committed = await store.CommitPairAsync(
            database.Authorization, database.Package, expectedCoordinationVersion: 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, committed.Outcome);

        PreparedRepositoryPackage first = database.Package.Repositories[0];
        PreparedScanPairPackage proofMutation = database.Package with
        {
            Repositories =
            [
                first with
                {
                    CodeGraphProof = first.CodeGraphProof with
                    {
                        LogicalIndexBinding = first.CodeGraphProof.LogicalIndexBinding + "-changed",
                    },
                },
                database.Package.Repositories[1],
            ],
        };
        PreparedScanCommitResult proofResult = await store.CommitPairAsync(
            database.Authorization, proofMutation, expectedCoordinationVersion: 0, CancellationToken.None);

        PreparedFileCodeUnitDraft unit = first.FileCodeUnits[0];
        PreparedScanPairPackage childMutation = database.Package with
        {
            Repositories =
            [
                first with
                {
                    FileCodeUnits =
                    [unit with { DirectBusinessScore = unit.DirectBusinessScore + 1 }, .. first.FileCodeUnits.Skip(1)],
                },
                database.Package.Repositories[1],
            ],
        };
        PreparedScanCommitResult childResult = await store.CommitPairAsync(
            database.Authorization, childMutation, expectedCoordinationVersion: 0, CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Conflict, proofResult.Outcome);
        Assert.Equal(PreparedScanCommitOutcome.Conflict, childResult.Outcome);
        Assert.Equal(2, await database.Context.PreparedScanManifests.CountAsync());
        Assert.Equal(database.Package.SelectedCount, await database.Context.CodeUnits.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PreparedStore_PersistedFileCodeUnitFingerprintTamper_CannotReplayAsync()
    {
        await using RegressionDatabase database = await RegressionDatabase.CreateAsync();
        PreparedScanStore store = new(database.Context);

        PreparedScanCommitResult committed = await store.CommitPairAsync(
            database.Authorization, database.Package, expectedCoordinationVersion: 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, committed.Outcome);

        CodeUnit stored = await database.Context.CodeUnits.OrderBy(unit => unit.Id).FirstAsync();
        database.Context.Entry(stored).Property(nameof(CodeUnit.FileCodeUnitIdentityFingerprint)).CurrentValue =
            new string('e', 64);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        PreparedScanCommitResult tampered = await store.CommitPairAsync(
            database.Authorization, database.Package, expectedCoordinationVersion: 0, CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Conflict, tampered.Outcome);
        Assert.Equal(database.Package.SelectedCount, await database.Context.CodeUnits.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PreparedStore_RetainedClassification_PersistsOneClassificationRowAsync()
    {
        await using RegressionDatabase database = await RegressionDatabase.CreateAsync();
        MethodInfo builder = typeof(PreparedScanAtomicPostgreSqlTests).GetMethod(
                "AddCanonicalClassification", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted T050 classification fixture is unavailable.");
        PreparedScanPairPackage classified = (PreparedScanPairPackage)(builder.Invoke(null, [database.Package])
            ?? throw new InvalidOperationException("The accepted T050 classification fixture returned no package."));
        PreparedScanStore store = new(database.Context);

        PreparedScanCommitResult result = await store.CommitPairAsync(
            database.Authorization, classified, expectedCoordinationVersion: 0, CancellationToken.None);

        Assert.Equal(PreparedScanCommitOutcome.Committed, result.Outcome);
        Assert.Equal(1, await database.Context.CommunicationKindClassifications.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql", Skip = "PostgreSQL-only serializable writer arbitration remains qualified by T050; SQLite cannot represent it honestly.")]
    public Task PreparedStore_ConcurrentIdenticalWriters_CommitOnceAndReplayOnceAsync() =>
        Task.CompletedTask;

    private sealed class RegressionDatabase : IAsyncDisposable
    {
        private readonly IAsyncDisposable _inner;
        private readonly Type _innerType;

        private RegressionDatabase(IAsyncDisposable inner, Type innerType)
        {
            _inner = inner;
            _innerType = innerType;
            Context = (VulgataDbContext)(innerType.GetProperty("Context")?.GetValue(inner)
                ?? throw new InvalidOperationException("The accepted isolated prepared database has no context."));
            Package = (PreparedScanPairPackage)(innerType.GetProperty("Package")?.GetValue(inner)
                ?? throw new InvalidOperationException("The accepted isolated prepared database has no package."));
            object principals = innerType.GetProperty("Principals")?.GetValue(inner)
                ?? throw new InvalidOperationException("The accepted isolated prepared database has no principals.");
            Guid systemId = (Guid)(principals.GetType().GetProperty("SystemId")?.GetValue(principals)
                ?? throw new InvalidOperationException("The accepted isolated prepared database has no system identity."));
            Authorization = new(
                Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
                systemId,
                Package.Repositories.Select(repository => repository.RepositoryId).ToArray());
        }

        internal VulgataDbContext Context { get; }
        internal PreparedScanPairPackage Package { get; }
        internal PreparedScanAuthorizationScope Authorization { get; }

        internal static async Task<RegressionDatabase> CreateAsync()
        {
            Type databaseType = typeof(PreparedScanAtomicReplayTests).GetNestedType(
                "IsolatedPreparedDatabase", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The accepted isolated prepared database fixture is unavailable.");
            MethodInfo create = databaseType.GetMethod(
                "CreateAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The accepted isolated prepared database factory is unavailable.");
            Task creation = (Task)(create.Invoke(null, null)
                ?? throw new InvalidOperationException("The accepted isolated prepared database factory returned no task."));
            await creation;
            IAsyncDisposable inner = (IAsyncDisposable)(creation.GetType().GetProperty("Result")?.GetValue(creation)
                ?? throw new InvalidOperationException("The accepted isolated prepared database factory returned no database."));
            return new(inner, databaseType);
        }

        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
        }
    }
}
