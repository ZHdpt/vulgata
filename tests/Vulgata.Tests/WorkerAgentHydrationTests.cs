using System.Security.Cryptography;
using System.Text;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

public sealed class WorkerAgentHydrationTests
{
    [Fact]
    public async Task HydrateAsync_AuthorizedExactRevision_ReturnsOnlyExactCommittedSliceAsync()
    {
        using var files = new HydrationFiles();
        Guid systemId = WorkerAgentTestFixtures.Id(4);
        Repository repository = Repository.Create(
            systemId,
            "Orders",
            "https://example.invalid/orders.git",
            description: null,
            context: null,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        string committedSource = "first\ncommitted-second\ncommitted-third\nlast";
        string expectedSlice = "committed-second\ncommitted-third";
        files.WriteCommitted(WorkerAgentTestFixtures.RepositoryRelativePath, committedSource);
        files.WriteMutable(
            WorkerAgentTestFixtures.RepositoryRelativePath,
            "first\nDIRTY-MOVED-WORKING-COPY\nDIRTY-MOVED-WORKING-COPY\nlast");

        byte[] committedBytes = Encoding.UTF8.GetBytes(committedSource);
        string fileIdentityHash = Hash(
            committedBytes.AsSpan(0, Math.Min(4_096, committedBytes.Length)));
        string sourceHash = Hash(Encoding.UTF8.GetBytes(expectedSlice));
        Pass1CodeLogicJobPayload payload = Pass1CodeLogicJobPayload.Create(
            codeUnitId: WorkerAgentTestFixtures.Id(6),
            repositoryRelativePath: WorkerAgentTestFixtures.RepositoryRelativePath,
            lineStart: 2,
            lineEnd: 3,
            symbolName: "CalculateTotal",
            unitType: CodeUnitType.Method,
            language: "csharp",
            sourceHash,
            fileIdentityHash,
            neighboringStructuralContext:
            [
                WorkerAgentStructuralContext.Create(
                    WorkerAgentTestFixtures.Id(7),
                    "src/Orders/Order.cs",
                    "Order"),
            ],
            approvedCommunicationEvidence:
            [
                WorkerAgentCommunicationEvidence.Create(
                    WorkerAgentTestFixtures.Id(8),
                    "HTTP",
                    "订单服务调用库存接口。"),
            ],
            allowedReferenceDocumentVersionIds: [WorkerAgentTestFixtures.Id(9)]);
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            jobId: WorkerAgentTestFixtures.Id(1),
            kind: WorkerAgentJobKind.Pass1CodeLogicDocument,
            scanExecutionId: WorkerAgentTestFixtures.Id(2),
            repositoryId: repository.Id,
            systemId,
            scanRunId: WorkerAgentTestFixtures.Id(5),
            profiledRevision: WorkerAgentTestFixtures.ProfiledRevision,
            schemaVersion: WorkerAgentContractLimits.SupportedSchemaVersion,
            payload);
        var lease = new RecordingSnapshotLease(
            repository.Id,
            packet.ScanRunId,
            packet.ProfiledRevision,
            files.CommittedRoot);
        var snapshotService = new RecordingSnapshotService(lease);
        var hydrator = new RevisionBoundWorkerContextHydrator(snapshotService);

        WorkerAgentExecutionContext context = await hydrator.HydrateAsync(
            packet,
            repository,
            GitWorkingCopyResult.Success(files.MutableMovedRoot, packet.ProfiledRevision),
            new RepositoryAccessScope([repository.Id]));

        Assert.Same(packet, context.Packet);
        Assert.Equal(expectedSlice, context.SourceText);
        Assert.DoesNotContain("DIRTY-MOVED-WORKING-COPY", context.SourceText, StringComparison.Ordinal);
        Assert.Equal(2, context.SourceLineCount);
        Assert.Equal(Encoding.UTF8.GetByteCount(expectedSlice), context.SourceUtf8ByteCount);
        Assert.InRange(
            context.RequestUtf8ByteCount,
            context.SourceUtf8ByteCount,
            WorkerAgentContractLimits.MaximumProviderRequestUtf8Bytes);
        Assert.Equal(payload.NeighboringStructuralContext, context.NeighboringStructuralContext);
        Assert.Equal(payload.ApprovedCommunicationEvidence, context.ApprovedCommunicationEvidence);
        Assert.Equal(
            payload.AllowedReferenceDocumentVersionIds,
            context.AllowedReferenceDocumentVersionIds);
        Assert.Equal(1, snapshotService.AcquireCount);
        Assert.Same(repository, snapshotService.Repository);
        Assert.Equal(packet.ScanRunId, snapshotService.ScanRunId);
        Assert.Equal(files.MutableMovedRoot, snapshotService.WorkingCopy?.WorkingCopyPath);
        Assert.Equal(1, lease.VerifyIdentityCount);
        Assert.Equal(1, lease.DisposeCount);
        Assert.DoesNotContain(expectedSlice, context.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HydrateAsync_ExactPositiveBounds_ReturnsBoundedTransientContextAsync()
    {
        using var files = new HydrationFiles();
        Guid systemId = WorkerAgentTestFixtures.Id(4);
        Repository repository = Repository.Create(
            systemId,
            "Orders",
            "https://example.invalid/orders.git",
            description: null,
            context: null,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        string source = string.Join(
            '\n',
            Enumerable.Range(0, WorkerAgentContractLimits.MaximumSourceSliceLines)
                .Select(index => index == 0 ? new string('x', 61_538) : "x"));
        Assert.Equal(
            WorkerAgentContractLimits.MaximumSourceSliceUtf8Bytes,
            Encoding.UTF8.GetByteCount(source));
        files.WriteCommitted(WorkerAgentTestFixtures.RepositoryRelativePath, source);
        files.WriteMutable(WorkerAgentTestFixtures.RepositoryRelativePath, "dirty");

        byte[] sourceBytes = Encoding.UTF8.GetBytes(source);
        WorkerAgentCommunicationEvidence[] communicationContext =
        [
            .. Enumerable.Range(0, 14).Select(index =>
                WorkerAgentCommunicationEvidence.Create(
                    WorkerAgentTestFixtures.Id(100 + index),
                    "HTTP",
                    new string('e', 4_000))),
        ];
        Pass1CodeLogicJobPayload payload = Pass1CodeLogicJobPayload.Create(
            codeUnitId: WorkerAgentTestFixtures.Id(6),
            repositoryRelativePath: WorkerAgentTestFixtures.RepositoryRelativePath,
            lineStart: 1,
            lineEnd: WorkerAgentContractLimits.MaximumSourceSliceLines,
            symbolName: "CalculateTotal",
            unitType: CodeUnitType.Method,
            language: "csharp",
            sourceHash: Hash(sourceBytes),
            fileIdentityHash: Hash(sourceBytes.AsSpan(0, 4_096)),
            neighboringStructuralContext: [],
            approvedCommunicationEvidence: communicationContext,
            allowedReferenceDocumentVersionIds: []);
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            jobId: WorkerAgentTestFixtures.Id(1),
            kind: WorkerAgentJobKind.Pass1CodeLogicDocument,
            scanExecutionId: WorkerAgentTestFixtures.Id(2),
            repositoryId: repository.Id,
            systemId,
            scanRunId: WorkerAgentTestFixtures.Id(5),
            profiledRevision: WorkerAgentTestFixtures.ProfiledRevision,
            schemaVersion: WorkerAgentContractLimits.SupportedSchemaVersion,
            payload);
        var lease = new RecordingSnapshotLease(
            repository.Id,
            packet.ScanRunId,
            packet.ProfiledRevision,
            files.CommittedRoot);
        var hydrator = new RevisionBoundWorkerContextHydrator(
            new RecordingSnapshotService(lease));

        WorkerAgentExecutionContext context = await hydrator.HydrateAsync(
            packet,
            repository,
            GitWorkingCopyResult.Success(files.MutableMovedRoot, packet.ProfiledRevision),
            new RepositoryAccessScope([repository.Id]));

        Assert.Equal(source, context.SourceText);
        Assert.Equal(
            WorkerAgentContractLimits.MaximumSourceSliceLines,
            context.SourceLineCount);
        Assert.Equal(
            WorkerAgentContractLimits.MaximumSourceSliceUtf8Bytes,
            context.SourceUtf8ByteCount);
        Assert.InRange(
            context.RequestUtf8ByteCount,
            120 * 1_024,
            WorkerAgentContractLimits.MaximumProviderRequestUtf8Bytes);
        Assert.Equal(communicationContext, context.ApprovedCommunicationEvidence);
        Assert.DoesNotContain(source, context.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HydrateAsync_WrongSystem_FailsClosedBeforeSnapshotAccessAsync()
    {
        using var files = new HydrationFiles();
        Guid systemId = WorkerAgentTestFixtures.Id(4);
        Repository repository = Repository.Create(
            systemId,
            "Orders",
            "https://example.invalid/orders.git",
            description: null,
            context: null,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        const string source = "return total;";
        files.WriteCommitted(WorkerAgentTestFixtures.RepositoryRelativePath, source);
        byte[] sourceBytes = Encoding.UTF8.GetBytes(source);
        Pass1CodeLogicJobPayload payload = Pass1CodeLogicJobPayload.Create(
            WorkerAgentTestFixtures.Id(6),
            WorkerAgentTestFixtures.RepositoryRelativePath,
            1,
            1,
            "CalculateTotal",
            CodeUnitType.Method,
            "csharp",
            Hash(sourceBytes),
            Hash(sourceBytes),
            [],
            [],
            []);
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            WorkerAgentTestFixtures.Id(1),
            WorkerAgentJobKind.Pass1CodeLogicDocument,
            WorkerAgentTestFixtures.Id(2),
            repository.Id,
            WorkerAgentTestFixtures.Id(404),
            WorkerAgentTestFixtures.Id(5),
            WorkerAgentTestFixtures.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            payload);
        var snapshotService = new RecordingSnapshotService(
            new RecordingSnapshotLease(
                repository.Id,
                packet.ScanRunId,
                packet.ProfiledRevision,
                files.CommittedRoot));
        var hydrator = new RevisionBoundWorkerContextHydrator(snapshotService);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            hydrator.HydrateAsync(
                packet,
                repository,
                GitWorkingCopyResult.Success(files.MutableMovedRoot, packet.ProfiledRevision),
                new RepositoryAccessScope([repository.Id])));

        Assert.Equal("Worker Agent 源上下文校验失败。", exception.Message);
        Assert.Equal(0, snapshotService.AcquireCount);
        Assert.DoesNotContain(source, exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HydrationFailure.UnauthorizedScope)]
    [InlineData(HydrationFailure.WrongRepository)]
    [InlineData(HydrationFailure.WorkingRevisionDrift)]
    [InlineData(HydrationFailure.LeaseRevisionDrift)]
    [InlineData(HydrationFailure.WrongLeaseScanRun)]
    [InlineData(HydrationFailure.MissingPath)]
    [InlineData(HydrationFailure.OutOfRange)]
    [InlineData(HydrationFailure.SourceHashMismatch)]
    [InlineData(HydrationFailure.FileIdentityHashMismatch)]
    [InlineData(HydrationFailure.OversizedSource)]
    [InlineData(HydrationFailure.OversizedRequest)]
    public async Task HydrateAsync_InvalidIdentitySourceOrBound_FailsWithOneClosedMessageAsync(
        HydrationFailure failure)
    {
        using var files = new HydrationFiles();
        Guid systemId = WorkerAgentTestFixtures.Id(4);
        Repository repository = Repository.Create(
            systemId,
            "Orders",
            "https://example.invalid/orders.git",
            description: null,
            context: null,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        string source = failure == HydrationFailure.OversizedSource
            ? new string('x', WorkerAgentContractLimits.MaximumSourceSliceUtf8Bytes + 1)
            : "return total;";
        files.WriteCommitted(WorkerAgentTestFixtures.RepositoryRelativePath, source);
        byte[] sourceBytes = Encoding.UTF8.GetBytes(source);
        string repositoryRelativePath = failure == HydrationFailure.MissingPath
            ? "src/Orders/Missing.cs"
            : WorkerAgentTestFixtures.RepositoryRelativePath;
        WorkerAgentCommunicationEvidence[] evidence = failure == HydrationFailure.OversizedRequest
            ? [
                .. Enumerable.Range(0, 33).Select(index =>
                    WorkerAgentCommunicationEvidence.Create(
                        WorkerAgentTestFixtures.Id(500 + index),
                        "HTTP",
                        new string('e', 4_000))),
            ]
            : [];
        Pass1CodeLogicJobPayload payload = Pass1CodeLogicJobPayload.Create(
            WorkerAgentTestFixtures.Id(6),
            repositoryRelativePath,
            1,
            failure == HydrationFailure.OutOfRange ? 2 : 1,
            "CalculateTotal",
            CodeUnitType.Method,
            "csharp",
            failure == HydrationFailure.SourceHashMismatch
                ? new string('0', 64)
                : Hash(sourceBytes),
            failure == HydrationFailure.FileIdentityHashMismatch
                ? new string('1', 64)
                : Hash(sourceBytes.AsSpan(0, Math.Min(4_096, sourceBytes.Length))),
            [],
            evidence,
            []);
        Guid packetRepositoryId = failure == HydrationFailure.WrongRepository
            ? WorkerAgentTestFixtures.Id(404)
            : repository.Id;
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            WorkerAgentTestFixtures.Id(1),
            WorkerAgentJobKind.Pass1CodeLogicDocument,
            WorkerAgentTestFixtures.Id(2),
            packetRepositoryId,
            systemId,
            WorkerAgentTestFixtures.Id(5),
            WorkerAgentTestFixtures.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            payload);
        string leaseRevision = failure == HydrationFailure.LeaseRevisionDrift
            ? "1123456789abcdef0123456789abcdef01234567"
            : packet.ProfiledRevision;
        Guid leaseScanRunId = failure == HydrationFailure.WrongLeaseScanRun
            ? WorkerAgentTestFixtures.Id(405)
            : packet.ScanRunId;
        var snapshotService = new RecordingSnapshotService(
            new RecordingSnapshotLease(
                packet.RepositoryId,
                leaseScanRunId,
                leaseRevision,
                files.CommittedRoot));
        var hydrator = new RevisionBoundWorkerContextHydrator(snapshotService);
        string workingRevision = failure == HydrationFailure.WorkingRevisionDrift
            ? "2123456789abcdef0123456789abcdef01234567"
            : packet.ProfiledRevision;
        RepositoryAccessScope scope = failure == HydrationFailure.UnauthorizedScope
            ? new RepositoryAccessScope([WorkerAgentTestFixtures.Id(999)])
            : new RepositoryAccessScope([packet.RepositoryId]);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            hydrator.HydrateAsync(
                packet,
                repository,
                GitWorkingCopyResult.Success(files.MutableMovedRoot, workingRevision),
                scope));

        Assert.Equal("Worker Agent 源上下文校验失败。", exception.Message);
        Assert.DoesNotContain(source, exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../outside.cs", 1, 1, WorkerAgentTestFixtures.SourceHash)]
    [InlineData("C:/host/source.cs", 1, 1, WorkerAgentTestFixtures.SourceHash)]
    [InlineData("src/api_key=secret.cs", 1, 1, WorkerAgentTestFixtures.SourceHash)]
    [InlineData(WorkerAgentTestFixtures.RepositoryRelativePath, 1, 2001, WorkerAgentTestFixtures.SourceHash)]
    [InlineData(WorkerAgentTestFixtures.RepositoryRelativePath, 1, 1, "not-a-hash")]
    public void Pass1Payload_TraversalAbsoluteSecretRangeOrHash_IsRejectedBeforeHydration(
        string path,
        int lineStart,
        int lineEnd,
        string sourceHash)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() => Pass1CodeLogicJobPayload.Create(
            WorkerAgentTestFixtures.Id(6),
            path,
            lineStart,
            lineEnd,
            "CalculateTotal",
            CodeUnitType.Method,
            "csharp",
            sourceHash,
            WorkerAgentTestFixtures.FileIdentityHash,
            [],
            [],
            []));
    }

    [Fact]
    public async Task HydrateAsync_Cancellation_StopsBeforeSnapshotAccessAsync()
    {
        WorkerAgentJobPacket packet = WorkerAgentTestFixtures.CreatePass1Packet();
        Guid systemId = packet.SystemId;
        Repository repository = Repository.Create(
            systemId,
            "Orders",
            "https://example.invalid/orders.git",
            null,
            null,
            DateTimeOffset.UnixEpoch);
        var snapshotService = new RecordingSnapshotService(
            new RecordingSnapshotLease(
                repository.Id,
                packet.ScanRunId,
                packet.ProfiledRevision,
                Path.GetTempPath()));
        var hydrator = new RevisionBoundWorkerContextHydrator(snapshotService);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hydrator.HydrateAsync(
            packet,
            repository,
            GitWorkingCopyResult.Success(Path.GetTempPath(), packet.ProfiledRevision),
            new RepositoryAccessScope([packet.RepositoryId]),
            cancellation.Token));

        Assert.Equal(0, snapshotService.AcquireCount);
    }

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    public enum HydrationFailure
    {
        UnauthorizedScope,
        WrongRepository,
        WorkingRevisionDrift,
        LeaseRevisionDrift,
        WrongLeaseScanRun,
        MissingPath,
        OutOfRange,
        SourceHashMismatch,
        FileIdentityHashMismatch,
        OversizedSource,
        OversizedRequest,
    }

    private sealed class RecordingSnapshotService(RecordingSnapshotLease lease)
        : ICommittedRepositorySnapshotService
    {
        public int AcquireCount { get; private set; }
        public Repository? Repository { get; private set; }
        public Guid ScanRunId { get; private set; }
        public GitWorkingCopyResult? WorkingCopy { get; private set; }

        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            Repository repository,
            Guid scanRunId,
            GitWorkingCopyResult workingCopy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AcquireCount++;
            Repository = repository;
            ScanRunId = scanRunId;
            WorkingCopy = workingCopy;
            return Task.FromResult<ICommittedRepositorySnapshotLease>(lease);
        }
    }

    private sealed class RecordingSnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId { get; } = repositoryId;
        public Guid ScanRunId { get; } = scanRunId;
        public string ProfiledRevision { get; } = profiledRevision;
        public string RootPath { get; } = rootPath;
        public int VerifyIdentityCount { get; private set; }
        public int DisposeCount { get; private set; }

        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifyIdentityCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HydrationFiles : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"vulgata-worker-hydration-{Guid.NewGuid():N}");

        public HydrationFiles()
        {
            CommittedRoot = Path.Combine(_root, "committed");
            MutableMovedRoot = Path.Combine(_root, "mutable-moved-dirty");
            Directory.CreateDirectory(CommittedRoot);
            Directory.CreateDirectory(MutableMovedRoot);
        }

        public string CommittedRoot { get; }
        public string MutableMovedRoot { get; }

        public void WriteCommitted(string relativePath, string content) =>
            Write(CommittedRoot, relativePath, content);

        public void WriteMutable(string relativePath, string content) =>
            Write(MutableMovedRoot, relativePath, content);

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static void Write(string root, string relativePath, string content)
        {
            string path = Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
