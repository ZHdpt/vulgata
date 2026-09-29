using Vulgata.Tests.TestInfrastructure;
using System.Data.Common;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T015 RED contract for the Infrastructure-owned PostgreSQL initiation store.
/// Every test owns a new T011 database and starts with only ordinary
/// System/Repository rows. No lifecycle row is pre-created by this fixture.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class PreparedScanInitiationPostgreSqlTests
{
    private static readonly DateTimeOffset InitiatedAt =
        new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_AbsentState_CreatesOneCompleteTupleAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();

        PreparedScanInitiationResult result = await CreateStore(database).CreateOrReplayAsync(
            CreateCommand(database), CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Created, result.Outcome);
        PreparedScanInitiationTuple tuple = Assert.IsType<PreparedScanInitiationTuple>(result.Tuple);
        Assert.Equal(database.System.Id, tuple.SystemId);
        Assert.Equal(TwoRepositoryScanCoordinationPhase.Preparing, tuple.CoordinationPhase);
        Assert.Equal(PreparedScanArtifactPresence.None, tuple.PreparedArtifactPresence);
        Assert.Equal(2, tuple.Repositories.Count);
        Assert.All(tuple.Repositories, repository =>
        {
            Assert.Equal(ScanExecutionPhase.Prepared, repository.ScanExecutionPhase);
            Assert.Equal("code-logic/file-v2", repository.DocumentSchemaIdentity);
            Assert.Equal(Guid.Empty, repository.PreScanResultId);
            Assert.NotEqual(Guid.Empty, repository.ScanRunId);
        });

        Assert.Equal(new InitiationCounts(2, 2, 1, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_ExactRequestAndReversedOrder_ReplaysSameCanonicalTupleAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        PreparedScanInitiationCommand command = CreateCommand(database);
        PreparedScanInitiationStore firstStore = CreateStore(database);

        PreparedScanInitiationResult first = await firstStore.CreateOrReplayAsync(
            command, CancellationToken.None);
        PreparedScanInitiationResult replay = await CreateStore(database).CreateOrReplayAsync(
            command with { Repositories = command.Repositories.Reverse().ToArray() },
            CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Created, first.Outcome);
        Assert.Equal(PreparedScanInitiationOutcome.Replay, replay.Outcome);
        AssertEquivalentTuples(
            Assert.IsType<PreparedScanInitiationTuple>(first.Tuple),
            Assert.IsType<PreparedScanInitiationTuple>(replay.Tuple));
        Assert.Equal(new InitiationCounts(2, 2, 1, 0), await ReadCountsAsync(database));
    }

    public static TheoryData<string> ReplayOnlyDimensions => new()
    {
        "ref", "revision", "runtime", "prompt", "schema", "authorization",
    };

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [MemberData(nameof(ReplayOnlyDimensions))]
    public async Task CreateOrReplayAsync_ChangedReplayDimension_ReturnsConflictWithoutWritesAsync(
        string dimension)
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        PreparedScanInitiationCommand command = CreateCommand(database);
        Assert.Equal(PreparedScanInitiationOutcome.Created,
            (await CreateStore(database).CreateOrReplayAsync(command, CancellationToken.None)).Outcome);

        PreparedScanInitiationCommand changed = Mutate(command, dimension);
        PreparedScanInitiationResult result = await CreateStore(database).CreateOrReplayAsync(
            changed, CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Conflict, result.Outcome);
        Assert.Null(result.Tuple);
        Assert.Equal(new InitiationCounts(2, 2, 1, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_HistoricalUncoordinatedRuns_AreIgnoredAndNewPathIsCreatedAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        ScanRun historicalOne = ScanRun.Start(database.First.Id, 900, InitiatedAt.AddDays(-1));
        historicalOne.BindProfiledRevision(RevisionOne);
        ScanRun historicalTwo = ScanRun.Start(database.Second.Id, 900, InitiatedAt.AddDays(-1));
        historicalTwo.BindProfiledRevision(RevisionTwo);
        await using (VulgataDbContext context = database.Fixture.CreateContext())
        {
            context.ScanRuns.AddRange(historicalOne, historicalTwo);
            await context.SaveChangesAsync();
        }

        PreparedScanInitiationResult result = await CreateStore(database).CreateOrReplayAsync(
            CreateCommand(database), CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Created, result.Outcome);
        Assert.Equal(new InitiationCounts(4, 2, 1, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("legacy")]
    [InlineData("partial")]
    [InlineData("extra")]
    [InlineData("ambiguous")]
    [InlineData("cross-paired")]
    public async Task CreateOrReplayAsync_LegacyPartialExtraAmbiguousOrCrossPairedState_IsRejectedAsync(
        string state)
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        if (state == "cross-paired")
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => SeedInvalidStateAsync(database, state));
            return;
        }

        await SeedInvalidStateAsync(database, state);

        PreparedScanInitiationResult result = await CreateStore(database).CreateOrReplayAsync(
            CreateCommand(database), CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Rejected, result.Outcome);
        Assert.Null(result.Tuple);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_CurrentPhaseWithoutPreparedArtifact_ProjectsReplayMetadataAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        PreparedScanInitiationCommand command = CreateCommand(database);
        PreparedScanInitiationResult created = await CreateStore(database).CreateOrReplayAsync(
            command, CancellationToken.None);
        Assert.Equal(PreparedScanInitiationOutcome.Created, created.Outcome);

        await using (VulgataDbContext context = database.Fixture.CreateContext())
        {
            TwoRepositoryScanCoordination coordination = await context.TwoRepositoryScanCoordinations
                .SingleAsync();
            coordination.BeginPass1(0, InitiatedAt.AddMinutes(1));
            await context.SaveChangesAsync();
        }

        PreparedScanInitiationResult replay = await CreateStore(database).CreateOrReplayAsync(
            command, CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Replay, replay.Outcome);
        Assert.Equal(TwoRepositoryScanCoordinationPhase.Pass1,
            Assert.IsType<PreparedScanInitiationTuple>(replay.Tuple).CoordinationPhase);
        Assert.Equal(PreparedScanArtifactPresence.None,
            Assert.IsType<PreparedScanInitiationTuple>(replay.Tuple).PreparedArtifactPresence);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_ExactPreparedContextSnapshot_ProjectsArtifactPresenceAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        PreparedScanInitiationCommand command = CreateCommand(database);
        PreparedScanInitiationResult created = await CreateStore(database).CreateOrReplayAsync(
            command, CancellationToken.None);
        PreparedScanInitiationTuple createdTuple =
            Assert.IsType<PreparedScanInitiationTuple>(created.Tuple);
        PreparedScanInitiationRepositoryTuple firstRepository = createdTuple.Repositories
            .Single(repository => repository.RepositoryId == database.First.Id);

        await using (VulgataDbContext context = database.Fixture.CreateContext())
        {
            ScanRun run = await context.ScanRuns.SingleAsync(value => value.Id == firstRepository.ScanRunId);
            context.ScanContextSnapshots.Add(ScanContextSnapshot.CapturePrepared(
                run,
                "global context",
                database.System.Context,
                database.First.Context,
                InitiatedAt));
            await context.SaveChangesAsync();
        }

        PreparedScanInitiationResult replay = await CreateStore(database).CreateOrReplayAsync(
            command, CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Replay, replay.Outcome);
        Assert.Equal(PreparedScanArtifactPresence.Present,
            Assert.IsType<PreparedScanInitiationTuple>(replay.Tuple).PreparedArtifactPresence);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_IdenticalIndependentConnections_ConvergeToOneTupleAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        PreparedScanInitiationCommand command = CreateCommand(database);
        await using VulgataDbContext firstContext = database.Fixture.CreateContext();
        await using VulgataDbContext secondContext = database.Fixture.CreateContext();

        PreparedScanInitiationResult[] results = await Task.WhenAll(
            new PreparedScanInitiationStore(firstContext).CreateOrReplayAsync(command, CancellationToken.None),
            new PreparedScanInitiationStore(secondContext).CreateOrReplayAsync(command, CancellationToken.None));

        Assert.Equal(1, results.Count(result => result.Outcome == PreparedScanInitiationOutcome.Created));
        Assert.Equal(1, results.Count(result => result.Outcome == PreparedScanInitiationOutcome.Replay));
        AssertEquivalentTuples(
            Assert.IsType<PreparedScanInitiationTuple>(results[0].Tuple),
            Assert.IsType<PreparedScanInitiationTuple>(results[1].Tuple));
        Assert.Equal(new InitiationCounts(2, 2, 1, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_CompetingPairOrRevisionConnections_HaveAtMostOneWinnerAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        PreparedScanInitiationCommand first = CreateCommand(database);
        PreparedScanInitiationCommand competing = first with
        {
            Repositories = first.Repositories.Select(repository => repository with
            {
                RequestedRef = repository.RequestedRef + "-competing",
            }).ToArray(),
        };
        await using VulgataDbContext firstContext = database.Fixture.CreateContext();
        await using VulgataDbContext secondContext = database.Fixture.CreateContext();

        PreparedScanInitiationResult[] results = await Task.WhenAll(
            new PreparedScanInitiationStore(firstContext).CreateOrReplayAsync(first, CancellationToken.None),
            new PreparedScanInitiationStore(secondContext).CreateOrReplayAsync(competing, CancellationToken.None));

        Assert.Equal(1, results.Count(result => result.Outcome == PreparedScanInitiationOutcome.Created));
        Assert.Equal(1, results.Count(result => result.Outcome == PreparedScanInitiationOutcome.Conflict));
        Assert.Equal(new InitiationCounts(2, 2, 1, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_AdvisoryLockTimeout_ReturnsUnavailableAndLeavesZeroRowsAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        long lockKey = CreateSystemLockKey(database.System.Id);
        await using NpgsqlConnection connection = new(database.Fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await using NpgsqlCommand lockCommand = new(
            "SELECT pg_advisory_xact_lock(@key);", connection, transaction);
        lockCommand.Parameters.AddWithValue("key", lockKey);
        await lockCommand.ExecuteNonQueryAsync();

        PreparedScanInitiationResult result = await CreateStore(database).CreateOrReplayAsync(
            CreateCommand(database), CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Unavailable, result.Outcome);
        Assert.Equal(new InitiationCounts(0, 0, 0, 0), await ReadCountsAsync(database));
        await transaction.RollbackAsync();
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_CancellationBeforeCommit_ReturnsCancelledAndLeavesZeroRowsAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        PreparedScanInitiationResult result = await CreateStore(database).CreateOrReplayAsync(
            CreateCommand(database), cancellation.Token);

        Assert.Equal(PreparedScanInitiationOutcome.Cancelled, result.Outcome);
        Assert.Equal(new InitiationCounts(0, 0, 0, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_InjectedSaveFailure_RollsBackAllRowsAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        ThrowingSaveChangesInterceptor interceptor = new();
        await using VulgataDbContext context = database.CreateContext(interceptor);
        interceptor.Arm();

        PreparedScanInitiationResult result = await new PreparedScanInitiationStore(context)
            .CreateOrReplayAsync(CreateCommand(database), CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Unavailable, result.Outcome);
        Assert.Equal(new InitiationCounts(0, 0, 0, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_InjectedPreCommitTransactionFailure_RollsBackAllRowsAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        ThrowingTransactionInterceptor interceptor = new();
        await using VulgataDbContext context = database.CreateContext(interceptor);
        interceptor.Arm();

        PreparedScanInitiationResult result = await new PreparedScanInitiationStore(context)
            .CreateOrReplayAsync(CreateCommand(database), CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Unavailable, result.Outcome);
        Assert.Equal(new InitiationCounts(0, 0, 0, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_OneSaveAndCommit_ObserverSeesCompleteTupleOnlyAfterCommitAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        SaveChangesCounter saveCounter = new();
        CommitCounter commitCounter = new();
        await using VulgataDbContext context = database.CreateContext(saveCounter, commitCounter);
        PreparedScanInitiationResult result = await new PreparedScanInitiationStore(context)
            .CreateOrReplayAsync(CreateCommand(database), CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Created, result.Outcome);
        Assert.Equal(1, saveCounter.SaveChangesCalls);
        Assert.Equal(1, commitCounter.Commits);
        Assert.Equal(new InitiationCounts(2, 2, 1, 0), await ReadCountsAsync(database));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CreateOrReplayAsync_AmbiguousPostCommitAcknowledgement_ReturnsUnavailableAndExactRetryReplaysAsync()
    {
        await using InitiationDatabase database = await CreateDatabaseAsync();
        AcknowledgementLostAfterCommitInterceptor interceptor = new();
        await using VulgataDbContext context = database.CreateContext(interceptor);
        interceptor.Arm();
        PreparedScanInitiationCommand command = CreateCommand(database);

        PreparedScanInitiationResult uncertain = await new PreparedScanInitiationStore(context)
            .CreateOrReplayAsync(command, CancellationToken.None);
        PreparedScanInitiationResult retry = await CreateStore(database).CreateOrReplayAsync(
            command, CancellationToken.None);

        Assert.Equal(PreparedScanInitiationOutcome.Unavailable, uncertain.Outcome);
        Assert.Equal(PreparedScanInitiationOutcome.Replay, retry.Outcome);
        Assert.Equal(new InitiationCounts(2, 2, 1, 0), await ReadCountsAsync(database));
    }

    private static async Task<InitiationDatabase> CreateDatabaseAsync()
    {
        PreparedScanInitiationPostgreSqlFixture fixture =
            await PreparedScanInitiationPostgreSqlFixture.CreateAsync();
        try
        {
            await using VulgataDbContext context = fixture.CreateContext();
            await context.Database.MigrateAsync();
            DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
            SystemEntity system = new("Story 12.5 T015 system", "ordinary system", "T015", now);
            Repository first = system.AddRepository(
                "story125-t015-one", "https://example.invalid/story125-t015-one.git", null, "one", now);
            Repository second = system.AddRepository(
                "story125-t015-two", "https://example.invalid/story125-t015-two.git", null, "two", now);
            context.Systems.Add(system);
            await context.SaveChangesAsync();
            return new InitiationDatabase(fixture, system, first, second);
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private static PreparedScanInitiationStore CreateStore(InitiationDatabase database) =>
        new(database.Fixture.CreateContext());

    private static PreparedScanInitiationCommand CreateCommand(InitiationDatabase database) => new(
        Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
        "Administrator",
        database.System.Id,
        [
            new(database.First.Id, "refs/tags/story125-one", RevisionOne, RuntimeOne, new('a', 64)),
            new(database.Second.Id, "refs/tags/story125-two", RevisionTwo, RuntimeTwo, new('b', 64)),
        ],
        "prepared-scan-prompt/story125-v1",
        "code-logic/file-v2",
        1_800,
        InitiatedAt);

    private static PreparedScanInitiationCommand Mutate(
        PreparedScanInitiationCommand command,
        string dimension) => dimension switch
        {
            "ref" => command with { Repositories = command.Repositories.Select(value =>
                value with { RequestedRef = value.RequestedRef + "-changed" }).ToArray() },
            "revision" => command with { Repositories = command.Repositories.Select(value =>
                value with { ProfiledRevision = new string('c', 40) }).ToArray() },
            "runtime" => command with { Repositories = command.Repositories.Select(value =>
                value with { RuntimeIdentityJson = "{\"analysisRuntimeVersion\":\"changed\"}" }).ToArray() },
            "prompt" => command with { PromptIdentity = "prepared-scan-prompt/story125-v2" },
            "schema" => command with { DocumentSchemaIdentity = "code-logic/file-v3" },
            "authorization" => command with { EffectiveAuthorizationMode = "SystemOwner" },
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        };

    private static async Task SeedInvalidStateAsync(InitiationDatabase database, string state)
    {
        await using VulgataDbContext context = database.Fixture.CreateContext();
        ScanRun firstRun = ScanRun.Start(database.First.Id, 1_800, InitiatedAt);
        firstRun.BindProfiledRevision(RevisionOne);
        context.ScanRuns.Add(firstRun);
        if (state == "legacy")
        {
            firstRun.Complete(0, 0, InitiatedAt);
            PreScanResult result = PreScanResult.CreateApproved(
                database.First.Id, firstRun.Id, RevisionOne, InitiatedAt, new('d', 64),
                "预扫描已批准——继续进入顺序演示运行器", ConfirmedCommunicationKinds.None,
                0, 0, 0, false, []);
            context.PreScanResults.Add(result);
            context.ScanExecutions.Add(ScanExecution.Start(
                Guid.NewGuid(), database.First.Id, database.System.Id, firstRun.Id, result.Id,
                RevisionOne, InitiatedAt));
        }
        else if (state == "partial")
        {
            // A new-path run without its paired execution/coordination.
        }
        else
        {
            ScanRun secondRun = ScanRun.Start(database.Second.Id, 1_800, InitiatedAt);
            secondRun.BindProfiledRevision(RevisionTwo);
            context.ScanRuns.Add(secondRun);
            ScanExecution firstExecution = ScanExecution.CreateNewPath(
                Guid.NewGuid(), database.First.Id, database.System.Id, firstRun.Id,
                RevisionOne, "prepared-scan-prompt/story125-v1", "code-logic/file-v2",
                RuntimeOne, new('a', 64), InitiatedAt);
            ScanExecution secondExecution = ScanExecution.CreateNewPath(
                Guid.NewGuid(), database.Second.Id, database.System.Id, secondRun.Id,
                RevisionTwo, "prepared-scan-prompt/story125-v1", "code-logic/file-v2",
                RuntimeTwo, new('b', 64), InitiatedAt);
            context.ScanExecutions.AddRange(firstExecution, secondExecution);
            if (state == "extra")
            {
                ScanRun extraRun = ScanRun.Start(database.First.Id, 1_800, InitiatedAt.AddMinutes(1));
                extraRun.BindProfiledRevision(RevisionOne);
                context.ScanRuns.Add(extraRun);
                context.ScanExecutions.Add(ScanExecution.CreateNewPath(
                    Guid.NewGuid(), database.First.Id, database.System.Id, extraRun.Id,
                    RevisionOne, "prepared-scan-prompt/story125-v1", "code-logic/file-v2",
                    RuntimeOne, new('a', 64), InitiatedAt));
            }
            else if (state == "ambiguous")
            {
                Repository third = database.System.AddRepository(
                    "story125-t015-three", "https://example.invalid/story125-t015-three.git", null, "three", InitiatedAt);
                context.Repositories.Add(third);
                ScanRun thirdRun = ScanRun.Start(third.Id, 1_800, InitiatedAt);
                thirdRun.BindProfiledRevision(RevisionOne);
                context.ScanRuns.Add(thirdRun);
                context.ScanExecutions.Add(ScanExecution.CreateNewPath(
                    Guid.NewGuid(), third.Id, database.System.Id, thirdRun.Id,
                    RevisionOne, "prepared-scan-prompt/story125-v1", "code-logic/file-v2",
                    RuntimeOne, new('a', 64), InitiatedAt));
            }
            else if (state == "cross-paired")
            {
                context.ScanExecutions.Add(ScanExecution.CreateNewPath(
                    Guid.NewGuid(), database.First.Id, database.System.Id, secondRun.Id,
                    RevisionOne, "prepared-scan-prompt/story125-v1", "code-logic/file-v2",
                    RuntimeOne, new('a', 64), InitiatedAt));
            }
        }

        await context.SaveChangesAsync();
    }

    private static void AssertEquivalentTuples(
        PreparedScanInitiationTuple expected,
        PreparedScanInitiationTuple actual)
    {
        Assert.Equal(expected.SystemId, actual.SystemId);
        Assert.Equal(expected.EffectiveAuthorizationScopeFingerprint,
            actual.EffectiveAuthorizationScopeFingerprint);
        Assert.Equal(expected.InitiatedAt, actual.InitiatedAt);
        Assert.Equal(expected.CoordinationId, actual.CoordinationId);
        Assert.Equal(expected.CoordinationStateVersion, actual.CoordinationStateVersion);
        Assert.Equal(expected.CoordinationPhase, actual.CoordinationPhase);
        Assert.Equal(expected.PreparedArtifactPresence, actual.PreparedArtifactPresence);

        PreparedScanInitiationRepositoryTuple[] expectedRepositories = expected.Repositories
            .OrderBy(repository => repository.RepositoryId).ToArray();
        PreparedScanInitiationRepositoryTuple[] actualRepositories = actual.Repositories
            .OrderBy(repository => repository.RepositoryId).ToArray();
        Assert.Equal(expectedRepositories.Length, actualRepositories.Length);
        for (int index = 0; index < expectedRepositories.Length; index++)
        {
            PreparedScanInitiationRepositoryTuple left = expectedRepositories[index];
            PreparedScanInitiationRepositoryTuple right = actualRepositories[index];
            Assert.Equal(left.RepositoryId, right.RepositoryId);
            Assert.Equal(left.RequestedRef, right.RequestedRef);
            Assert.Equal(left.ProfiledRevision, right.ProfiledRevision);
            Assert.Equal(left.ScanRunId, right.ScanRunId);
            Assert.Equal(left.ScanExecutionId, right.ScanExecutionId);
            Assert.Equal(left.ScanExecutionPhase, right.ScanExecutionPhase);
            Assert.Equal(left.PromptIdentity, right.PromptIdentity);
            Assert.Equal(left.DocumentSchemaIdentity, right.DocumentSchemaIdentity);
            Assert.Equal(left.RuntimeIdentityJson, right.RuntimeIdentityJson);
            Assert.Equal(left.RuntimeIdentityHash, right.RuntimeIdentityHash);
            Assert.Equal(left.PreScanResultId, right.PreScanResultId);
        }
    }

    private static async Task<InitiationCounts> ReadCountsAsync(InitiationDatabase database)
    {
        await using VulgataDbContext context = database.Fixture.CreateContext();
        return new(
            await context.ScanRuns.CountAsync(),
            await context.ScanExecutions.CountAsync(),
            await context.TwoRepositoryScanCoordinations.CountAsync(),
            await context.PreScanResults.CountAsync());
    }

    private static long CreateSystemLockKey(Guid systemId)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"vulgata/story-12.5/prepared-scan-initiation-system-lock/1\n{systemId:D}"));
        return BinaryPrimitives.ReadInt64BigEndian(digest);
    }

    private const string RevisionOne = "0123456789abcdef0123456789abcdef01234567";
    private const string RevisionTwo = "fedcba9876543210fedcba9876543210fedcba98";
    private const string RuntimeOne = "{\"analysisRuntimeVersion\":\"1\"}";
    private const string RuntimeTwo = "{\"analysisRuntimeVersion\":\"1\"}";

    private sealed class InitiationDatabase(
        PreparedScanInitiationPostgreSqlFixture fixture,
        SystemEntity system,
        Repository first,
        Repository second) : IAsyncDisposable
    {
        internal PreparedScanInitiationPostgreSqlFixture Fixture { get; } = fixture;
        internal SystemEntity System { get; } = system;
        internal Repository First { get; } = first;
        internal Repository Second { get; } = second;

        internal VulgataDbContext CreateContext(params IInterceptor[] interceptors)
        {
            DbContextOptionsBuilder<VulgataDbContext> builder = new();
            builder.UseNpgsql(Fixture.ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"));
            builder.AddInterceptors(interceptors);
            return new VulgataDbContext(builder.Options);
        }

        public async ValueTask DisposeAsync() => await Fixture.DisposeAsync();
    }

    private readonly record struct InitiationCounts(
        int ScanRuns,
        int ScanExecutions,
        int Coordinations,
        int PreScanResults);

    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        private bool _armed;

        internal void Arm() => _armed = true;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_armed)
            {
                _armed = false;
                throw new InvalidOperationException("T015 injected SaveChanges failure.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingTransactionInterceptor : DbTransactionInterceptor
    {
        private bool _armed;

        internal void Arm() => _armed = true;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (_armed)
            {
                _armed = false;
                throw new InvalidOperationException("T015 injected pre-commit failure.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class SaveChangesCounter : SaveChangesInterceptor
    {
        internal int SaveChangesCalls { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return ValueTask.FromResult(result);
        }

    }

    private sealed class CommitCounter : DbTransactionInterceptor
    {
        internal int Commits { get; private set; }

        public override void TransactionCommitted(
            DbTransaction transaction,
            TransactionEndEventData eventData) => Commits++;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Commits++;
            return Task.CompletedTask;
        }
    }

    private sealed class AcknowledgementLostAfterCommitInterceptor : DbTransactionInterceptor
    {
        private bool _armed;

        internal void Arm() => _armed = true;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (_armed)
            {
                _armed = false;
                throw new PostgresException(
                    "T015 injected acknowledgement loss after commit.", "ERROR", "ERROR", "40001");
            }

            return Task.CompletedTask;
        }
    }
}
