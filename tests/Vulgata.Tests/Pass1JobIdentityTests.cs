using Vulgata.Tests.TestInfrastructure;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

public sealed class Pass1JobIdentityTests
{
    private const string _goldenEnvelopeHex =
        "56554c474154412d4f5244494e4152592d50415353312d4a4f4200010001000000101111111122223333844455555555555500020000001066666666777748889999aaaaaaaaaaaa000300000010bbbbbbbbcccc4ddd8eeeffffffffffff0004000000100123456789ab4cde8f0123456789abcd0005000000140123456789abcdef0123456789abcdef01234567";
    private const string _goldenDigest =
        "6b9362f7193b38d94d9cb2a21ecfe2fd5ffe6a3aaaa00bd8c787e8efccdc71da";
    private static readonly Guid _scanExecutionId =
        Guid.Parse("11111111-2222-3333-8444-555555555555");
    private static readonly Guid _scanRunId =
        Guid.Parse("66666666-7777-4888-9999-aaaaaaaaaaaa");
    private static readonly Guid _repositoryId =
        Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");
    private static readonly Guid _codeUnitId =
        Guid.Parse("01234567-89ab-4cde-8f01-23456789abcd");
    private const string _profiledRevision =
        "0123456789abcdef0123456789abcdef01234567";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_GoldenBoundary_ProducesCanonicalEnvelopeDigestAndActualJobId()
    {
        OrdinaryPass1JobIdentity identity = OrdinaryPass1JobIdentity.Create(
            _scanExecutionId,
            _scanRunId,
            _repositoryId,
            _codeUnitId,
            _profiledRevision);

        byte[] expectedEnvelope = Convert.FromHexString(_goldenEnvelopeHex);
        Assert.Equal(142, expectedEnvelope.Length);
        Assert.Equal(expectedEnvelope, identity.CanonicalEnvelope.ToArray());
        Assert.Equal(_goldenDigest, Convert.ToHexStringLower(identity.Digest.Span));
        Assert.Equal(
            Guid.Parse("6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd"),
            identity.JobId);
        Assert.Equal(
            _goldenDigest,
            Convert.ToHexStringLower(SHA256.HashData(expectedEnvelope)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_SameBoundaryRepeatsAndEveryCanonicalInputMutationChangesIdentity()
    {
        OrdinaryPass1JobIdentity baseline = CreateIdentity(
            _scanExecutionId,
            _scanRunId,
            _repositoryId,
            _codeUnitId,
            _profiledRevision);
        Assert.Equal(
            baseline.JobId,
            CreateIdentity(
                _scanExecutionId,
                _scanRunId,
                _repositoryId,
                _codeUnitId,
                _profiledRevision).JobId);

        OrdinaryPass1JobIdentity[] mutations =
        [
            CreateIdentity(Guid.Parse("11111111-2222-3333-8444-555555555556"),
                _scanRunId, _repositoryId, _codeUnitId, _profiledRevision),
            CreateIdentity(_scanExecutionId,
                Guid.Parse("66666666-7777-4888-9999-aaaaaaaaaaab"),
                _repositoryId, _codeUnitId, _profiledRevision),
            CreateIdentity(_scanExecutionId, _scanRunId,
                Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-fffffffffffe"),
                _codeUnitId, _profiledRevision),
            CreateIdentity(_scanExecutionId, _scanRunId, _repositoryId,
                Guid.Parse("01234567-89ab-4cde-8f01-23456789abce"),
                _profiledRevision),
            CreateIdentity(_scanExecutionId, _scanRunId, _repositoryId,
                _codeUnitId,
                "1123456789abcdef0123456789abcdef01234567"),
        ];

        Assert.All(mutations, mutation => Assert.NotEqual(baseline.JobId, mutation.JobId));
        Assert.Equal(mutations.Length, mutations.Select(value => value.JobId).Distinct().Count());
        Assert.DoesNotContain(
            Convert.FromHexString("56554c474154412d50524550415245442d5052455041535332"),
            baseline.CanonicalEnvelope.ToArray());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("")]
    [InlineData("0123456")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567")]
    [InlineData(" 0123456789abcdef0123456789abcdef01234567")]
    [InlineData("0123456789abcdef0123456789abcdef0123456g")]
    public void Create_NoncanonicalRevision_RejectsWithoutRepair(string revision)
    {
        Assert.Throws<ArgumentException>(() => CreateIdentity(
            _scanExecutionId,
            _scanRunId,
            _repositoryId,
            _codeUnitId,
            revision));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CreateDocumentVersionId_DerivedJob_UsesExistingForwardProjection()
    {
        Guid jobId = CreateIdentity(
            _scanExecutionId,
            _scanRunId,
            _repositoryId,
            _codeUnitId,
            _profiledRevision).JobId;
        byte[] independent = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"pass1-document-version\n{jobId:D}"));

        Assert.Equal(
            new Guid(independent.AsSpan(0, 16)),
            OrdinaryPass1JobIdentity.CreateDocumentVersionId(jobId));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_OrdinaryCodeUnit_DispatchesCanonicalActualJobIdAsync()
    {
        Guid repositoryId = Guid.NewGuid();
        Guid scanRunId = Guid.NewGuid();
        Guid systemId = Guid.NewGuid();
        var unit = new CodeUnit(
            scanRunId,
            repositoryId,
            "src/Demo.cs",
            new string('a', 64),
            new string('b', 64),
            1,
            2,
            "Execute",
            CodeUnitType.Method,
            RangeConfidence.Exact,
            "csharp");
        var store = new RecordingExecutionStore();
        var dispatcher = new RecordingDispatcher();
        var runner = new SequentialScanRunner(
            new ApprovedHandoffSource(repositoryId, scanRunId),
            store,
            new SingleCodeUnitSource(unit),
            dispatcher,
            TimeProvider.System);
        Guid executionId = await runner.StartAsync(repositoryId, scanRunId, systemId);

        await runner.RunPass1Async(executionId);

        WorkerAgentJobPacket packet = Assert.Single(dispatcher.Packets);
        Assert.Equal(
            OrdinaryPass1JobIdentity.Create(
                executionId,
                scanRunId,
                repositoryId,
                unit.Id,
                _profiledRevision).JobId,
            packet.JobId);
    }

    private static OrdinaryPass1JobIdentity CreateIdentity(
        Guid scanExecutionId,
        Guid scanRunId,
        Guid repositoryId,
        Guid codeUnitId,
        string profiledRevision) =>
        OrdinaryPass1JobIdentity.Create(
            scanExecutionId,
            scanRunId,
            repositoryId,
            codeUnitId,
            profiledRevision);

    private sealed class ApprovedHandoffSource(Guid repositoryId, Guid scanRunId)
        : IApprovedPreScanHandoffSource
    {
        public Task<ApprovedPreScanHandoff?> GetAsync(
            Guid requestedRepositoryId,
            Guid requestedScanRunId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovedPreScanHandoff?>(
                requestedRepositoryId == repositoryId && requestedScanRunId == scanRunId
                    ? ApprovedPreScanHandoff.Create(
                        scanRunId,
                        repositoryId,
                        scanRunId,
                        _profiledRevision,
                        PreScanApprovalState.PreScanApproved,
                        PreScanResult.CurrentSchemaVersion,
                        DateTimeOffset.UtcNow,
                        "已批准前置扫描。",
                        ConfirmedCommunicationKinds.None,
                        0,
                        0,
                        0,
                        false,
                        [])
                    : null);
    }

    private sealed class SingleCodeUnitSource(CodeUnit unit) : ICodeUnitSource
    {
        public Task<IReadOnlyList<CodeUnit>> GetPreparedAsync(
            Guid scanRunId,
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CodeUnit>>([unit]);
    }

    private sealed class RecordingExecutionStore : IScanExecutionStore
    {
        private readonly Dictionary<Guid, ScanExecution> _executions = [];

        public Task<ScanExecution?> GetAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_executions.GetValueOrDefault(scanExecutionId));

        public Task<ScanExecution?> FindActiveAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanExecution?>(null);

        public Task PersistAsync(
            ScanExecution execution,
            CancellationToken cancellationToken = default)
        {
            _executions[execution.Id] = execution;
            return Task.CompletedTask;
        }

        public Task<ScanExecutionStateView> ReadStateAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDispatcher : IWorkerAgentDispatcher
    {
        public List<WorkerAgentJobPacket> Packets { get; } = [];

        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet,
            CancellationToken cancellationToken = default)
        {
            Packets.Add(packet);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return Task.FromResult(WorkerAgentResultEnvelope.Create(
                packet.JobId,
                packet.ScanExecutionId,
                packet.Kind,
                WorkerAgentResultOutcome.Valid,
                null,
                now,
                now));
        }
    }
}
