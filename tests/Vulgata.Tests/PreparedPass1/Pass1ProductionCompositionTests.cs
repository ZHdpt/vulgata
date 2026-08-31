using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedPass1.Fixtures;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T054 RED slice. These tests exercise the approved runtime seams:
/// a claimed File Code Unit enters the real bounded dispatcher, and a rejected
/// Worker result is durably classified without entering the atomic committer.
/// Host registration and static forbidden-surface checks belong to the
/// qualification test, not this behavioral slice.
/// </summary>
public sealed class Pass1ProductionCompositionTests
{
    [Fact]
    public void ProductionComposition_ResolvesReservationPortToTheSolePreparedStore()
    {
        Type reservationPort = typeof(IPass1NavigationAttemptReservationStore);
        Assert.True(
            reservationPort.IsAssignableFrom(typeof(PreparedPass1Store)),
            "T109 RED: PreparedPass1Store must be the sole implementation of the durable navigation reservation port.");

        string program = ReadRepositorySource("src/dotnet/Vulgata.Web/Program.cs");
        Assert.Contains(
            "IPass1NavigationAttemptReservationStore",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "PreparedPass1Store",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HighWaterEight_RepairsCallsMarkerBeforeProviderOrTerminalVisibility()
    {
        string runner = ReadRepositorySource(
            "src/dotnet/Vulgata.Web/Data/PreparedPass1JobRunner.cs");
        int marker = runner.IndexOf(
            "EnsureNavigationCallsMarkerAsync",
            StringComparison.Ordinal);
        int dispatch = runner.IndexOf(
            "GetRequiredService<PreparedPass1DispatchWorker>()",
            StringComparison.Ordinal);
        Assert.True(
            marker >= 0,
            "T109 RED: durable claim execution must expose the marker-repair transition before provider execution.");
        Assert.True(
            dispatch < 0 || marker < dispatch,
            "T109 RED: Calls marker repair must precede provider execution visibility.");
    }

    [Fact]
    public void JobRunner_RequiresDurableCallsMarkerRepairStore()
    {
        string runner = ReadRepositorySource(
            "src/dotnet/Vulgata.Web/Data/PreparedPass1JobRunner.cs");

        Assert.Contains(
            "GetRequiredService<IPass1NavigationCallsMarkerRepairStore>()",
            runner,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetService<IPass1NavigationCallsMarkerRepairStore>()",
            runner,
            StringComparison.Ordinal);
    }

    [Fact]
    public void JobRunnerDispatchBridge_ForwardsServerOwnedSymbolNameWithoutDefaulting()
    {
        // T045 RED: the sole hydrated-packet-to-assignment bridge must
        // preserve path-v1 and semantic-v2 SymbolName without inference.
        Assert.NotNull(typeof(PreparedPass1FileCodeUnitAssignment).GetProperty(
            "SymbolName", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(typeof(PreparedPass1WorkerPacket).GetProperty(
            "SymbolName", BindingFlags.Public | BindingFlags.Instance));

        MethodInfo bridge = typeof(PreparedPass1JobRunner)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => method.Name == "CreateDispatchRequest");
        Assert.Equal(typeof(PreparedPass1WorkerPacket), bridge.GetParameters()[0].ParameterType);
    }

    [Fact]
    public async Task ClaimedFileCodeUnit_ReachesRealDispatcherAndRecordingExecutorAsync()
    {
        PreparedPass1FileCodeUnitAssignment assignment = CreateAssignment();
        RecordingExecutor executor = new();
        IPreparedPass1WorkerDispatcher dispatcher = new PreparedPass1WorkerDispatcher(executor);

        PreparedPass1DispatchResult result = await dispatcher.DispatchAsync(
            new([assignment], [CreateContext(assignment)], [], 2));

        Assert.Single(executor.Packets);
        Assert.Equal(assignment.JobId, executor.Packets.Single().JobId);
        Assert.Single(result.Results);
    }

    [Fact]
    public async Task OrdinaryRejectedWorkerResult_IsRetryClassifiedAndCannotReachAtomicCommitAsync()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        PreparedPass1WorkerPacket packet = CreatePacket();
        Guid leaseToken = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        ScanJobExecution job = ScanJobExecution.Create(
            packet.ScanExecutionId, packet.RepositoryId, packet.ScanRunId,
            packet.FileCodeUnitId, packet.ManifestOrdinal, now);
        job.AcquireLease(leaseToken, now.AddMinutes(30), now);

        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using VulgataDbContext context = new(new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        await using (DbCommand pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            await pragma.ExecuteNonQueryAsync();
        }
        context.ScanJobExecutions.Add(job);
        await context.SaveChangesAsync();

        RecordingStore store = new();
        RecordingCommitter committer = new();
        PreparedPass1ClaimResult claim = new(
            PreparedPass1ClaimOutcome.Claimed, packet.JobId, leaseToken,
            now.AddMinutes(30), packet.PairSelectionOrdinal, packet.ManifestOrdinal,
            1, 2, job.StateVersion, 1);
        var services = new ServiceCollection();
        services.AddScoped(_ => context);
        services.AddScoped<IPreparedPass1Store>(_ => store);
        services.AddScoped<IPreparedPass1ClaimedPacketSource>(_ => new FixedPacketSource(packet));
        services.AddScoped<IPreparedPass1WorkerDispatcher>(_ => new RejectedDispatcher(packet));
        services.AddScoped<PreparedPass1DispatchWorker>();
        services.AddScoped<IPass1NavigationCallsMarkerRepairStore>(
            _ => new RecordingMarkerRepairStore());
        services.AddScoped<IPass1CodeLogicDocumentCommitter>(_ => committer);
        await using ServiceProvider provider = services.BuildServiceProvider();
        var runner = new PreparedPass1JobRunner(
            provider.GetRequiredService<IServiceScopeFactory>(), new FixedTimeProvider(now));
        PreparedScanAuthorizationScope authorization = new(
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), packet.SystemId, [packet.RepositoryId]);
        PreparedPass1Activation activation = new(packet.PairExecutionId, authorization, null);
        MethodInfo method = typeof(PreparedPass1JobRunner).GetMethod(
            "RunClaimAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)method.Invoke(runner, [activation, claim, CancellationToken.None])!;

        Assert.Equal(ScanJobFailureCategory.JobTransient, store.RecordedCategory);
        Assert.Equal(0, committer.CallCount);
    }

    private static PreparedPass1FileCodeUnitAssignment CreateAssignment()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        return new(
            packet.PairExecutionId, packet.JobId, packet.ScanExecutionId, packet.SystemId,
            packet.RepositoryId, packet.ScanRunId, packet.ProfiledRevision, packet.FileCodeUnitId,
            packet.FileCodeUnitIdentityFingerprint, packet.PreparedScanManifestId,
            packet.PreparedScanManifestFingerprint, packet.PairPackageFingerprint,
            packet.RepositoryRelativePath, packet.ByteStart, packet.ByteEnd, packet.LineStart,
            packet.LineEnd, packet.SegmentOrdinal, packet.SegmentCount, packet.FileHash,
            packet.SourceHash, packet.PromptIdentity, packet.DocumentSchemaIdentity,
            packet.AnalysisRuntimeIdentityHash, packet.ContextSnapshot.ContextSnapshotId,
            packet.ManifestOrdinal, packet.PairSelectionOrdinal, packet.SourceUtf8);
    }

    private static PreparedPass1WorkerPacket CreatePacket()
    {
        ApprovedPreparedManifestPairSeed pair = PreparedPass1ManifestPairFixtures.CreateApprovedPair();
        PreparedManifestSeed manifest = pair.Manifests[0];
        PreparedFileCodeUnitSeed file = manifest.FileCodeUnits[0];
        Guid systemId = Guid.Parse("50000000-0000-4000-8000-000000000005");
        Guid pairExecutionId = Guid.Parse("60000000-0000-4000-8000-000000000006");
        Guid scanExecutionId = GuidFromHash($"execution:{manifest.RepositoryId:D}");
        Guid manifestId = GuidFromHash($"manifest:{manifest.RepositoryId:D}");
        Guid contextId = GuidFromHash($"context:{manifest.RepositoryId:D}");
        Guid fileCodeUnitId = GuidFromHash($"unit:{manifest.RepositoryId:D}:{file.RelativePath}");
        Guid jobId = ScanJobExecution.Create(
            scanExecutionId, manifest.RepositoryId, manifest.ScanRunId, fileCodeUnitId,
            file.ManifestOrdinal, DateTimeOffset.UnixEpoch).Id;
        byte[] source = Encoding.UTF8.GetBytes(file.Source);
        string contextHash = Hash($"context:{manifest.RepositoryId:D}");
        return new(
            pairExecutionId, jobId, scanExecutionId, systemId, manifest.RepositoryId,
            manifest.ScanRunId, manifest.ProfiledRevision, fileCodeUnitId, file.IdentityFingerprint,
            manifestId, manifest.ManifestFingerprint, pair.PairFingerprint, file.RelativePath,
            0, source.Length, 1, Math.Max(1, file.Source.Count(value => value == '\n') + 1),
            1, 1, file.FileHash, file.SourceHash, "story-12-3-composition-prompt",
            "code-logic/file-v2", Hash("runtime:story-12-3"), file.ManifestOrdinal,
            file.PairSelectionOrdinal, source,
            new(contextId, manifest.RepositoryId, manifest.ScanRunId, manifest.ProfiledRevision,
                contextHash, contextHash, null, null, null), []);
    }

    private static PreparedPass1ContextSnapshotProjection CreateContext(
        PreparedPass1FileCodeUnitAssignment assignment) =>
        new(assignment.ContextSnapshotId, assignment.RepositoryId, assignment.ScanRunId,
            assignment.ProfiledRevision, Hash("context"), Hash("context"), null, null, null);

    private static Guid GuidFromHash(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ReadRepositorySource(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        string path = Path.Combine(
            directory!.FullName,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"T109 source seam is missing: {relativePath}");
        return File.ReadAllText(path);
    }

    private sealed class RecordingExecutor : IPreparedPass1JobExecutor
    {
        public ConcurrentQueue<PreparedPass1WorkerPacket> Packets { get; } = new();

        public Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Packets.Enqueue(packet);
            return Task.FromResult(new PreparedPass1RawWorkerResult(
                PreparedPass1RawWorkerOutcome.Rejected, ReadOnlyMemory<byte>.Empty,
                "worker-result-rejected"));
        }
    }

    private sealed class RejectedDispatcher(PreparedPass1WorkerPacket packet)
        : IPreparedPass1WorkerDispatcher
    {
        public Task<PreparedPass1DispatchResult> DispatchAsync(
            PreparedPass1DispatchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedPass1DispatchResult(
                [packet],
                [new(packet.JobId, packet.PairSelectionOrdinal,
                    PreparedPass1RawWorkerOutcome.Rejected, null, "worker-result-rejected")]));
    }

    private sealed class FixedPacketSource(PreparedPass1WorkerPacket packet)
        : IPreparedPass1ClaimedPacketSource
    {
        public Task<PreparedPass1WorkerPacket?> HydrateAsync(
            Guid pairExecutionId, PreparedPass1ClaimResult claim, DateTimeOffset observedAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PreparedPass1WorkerPacket?>(packet);
    }

    private sealed class RecordingCommitter : IPass1CodeLogicDocumentCommitter
    {
        public int CallCount { get; private set; }

        public Task<Pass1DocumentCommitResult> CommitAsync(
            Pass1DocumentCommitRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new Pass1DocumentCommitResult(
                Pass1DocumentCommitStatus.Committed, Guid.NewGuid(), Guid.NewGuid(),
                Hash("unexpected-commit"), null));
        }
    }

    private sealed class RecordingMarkerRepairStore : IPass1NavigationCallsMarkerRepairStore
    {
        public Task<bool> EnsureNavigationCallsMarkerAsync(
            PreparedPass1NavigationCallsMarkerRepairRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class RecordingStore : IPreparedPass1Store
    {
        public ScanJobFailureCategory? RecordedCategory { get; private set; }

        public Task<PreparedPass1FailureResult> RecordFailureAsync(
            Guid pairExecutionId, Guid jobId, long expectedStateVersion, Guid expectedLeaseToken,
            ScanJobFailureCategory failureCategory, DateTimeOffset observedAt,
            CancellationToken cancellationToken = default)
        {
            RecordedCategory = failureCategory;
            return Task.FromResult(new PreparedPass1FailureResult(
                PreparedPass1FailureOutcome.Recorded, failureCategory, "worker-result-rejected",
                "failed", false, 1, 1, false, 1, Pass1PairExecutionPhase.Running, null,
                false, false, false, false, false));
        }

        public Task<PreparedPass1StartResult> StartAsync(PreparedScanAuthorizationScope a, PreparedScanHandoff h, int c = 2, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PreparedPass1ClaimResult> ClaimNextAsync(Guid p, DateTimeOffset n, TimeSpan l, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PreparedPass1ConcurrencyUpdateResult> UpdateConcurrencyAsync(PreparedScanAuthorizationScope a, Guid p, int r, long v, string s, DateTimeOffset c, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PreparedPass1RecoveryResult> RecoverAsync(Guid p, PreparedScanAuthorizationScope a, bool v, DateTimeOffset o, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PreparedPass1ProviderCircuitResult> RecordProviderTransientAsync(Guid p, DateTimeOffset o, DateTimeOffset? r = null, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PreparedPass1ProviderCircuitResult> ProbeProviderAsync(Guid p, long v, int o, bool a, DateTimeOffset n, DateTimeOffset? r = null, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PreparedPass1ReconciliationResult> ReconcileAsync(Guid p, PreparedPass1ReconciliationRequest r, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PreparedPass1CompletionResult> CompletePass1Async(Guid p, PreparedScanAuthorizationScope a, DateTimeOffset c, CancellationToken t = default) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
