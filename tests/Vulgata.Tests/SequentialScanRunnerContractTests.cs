using Vulgata.Tests.TestInfrastructure;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

/// <summary>
/// G1 contract tests for the closed typed Worker Agent job packet, result
/// envelope, and result payload. Validates: closed kind allowlist; finite
/// UTF-8/count limits; sensitive-content rejection (absolute host path,
/// secret, raw-source carrier, unbounded diagnostic); deterministic ordering;
/// packet/result identity match; outcome closed enum.
/// </summary>
public sealed class SequentialScanRunnerContractTests
{
    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string SymbolName = "CalculateTotal";
    private const string FilePath = "src/Service.cs";

    private static Guid NonEmptyGuid(int n) =>
        Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static (Guid JobId, Guid ScanExecutionId, Guid RepositoryId, Guid SystemId, Guid ScanRunId, Guid CodeUnitId) Ids() =>
        (NonEmptyGuid(1), NonEmptyGuid(2), NonEmptyGuid(3), NonEmptyGuid(4), NonEmptyGuid(5), NonEmptyGuid(6));

    private static WorkerAgentJobPacket CreatePacket(
        string? filePath = FilePath,
        string? symbolName = SymbolName,
        string? profiledRevision = ProfiledRevision,
        string? sourceHash = Sha256,
        string? fileIdentityHash = Sha256,
        string? language = "csharp") =>
        WorkerAgentJobPacket.Create(
            jobId: NonEmptyGuid(1),
            kind: WorkerAgentJobKind.Pass1CodeLogicDocument,
            scanExecutionId: NonEmptyGuid(2),
            repositoryId: NonEmptyGuid(3),
            systemId: NonEmptyGuid(4),
            scanRunId: NonEmptyGuid(5),
            codeUnitId: NonEmptyGuid(6),
            profiledRevision: profiledRevision,
            filePath: filePath,
            lineStart: 1,
            lineEnd: 10,
            symbolName: symbolName,
            unitType: CodeUnitType.Method,
            language: language,
            sourceHash: sourceHash,
            fileIdentityHash: fileIdentityHash);

    // --- T003: closed kind allowlist ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void WorkerAgentJobKind_DefinesOnlyPass1AndPass2DocumentKinds()
    {
        WorkerAgentJobKind[] values = Enum.GetValues<WorkerAgentJobKind>();
        Assert.Equal(
            [
                WorkerAgentJobKind.Pass1CodeLogicDocument,
                WorkerAgentJobKind.Pass2BusinessLogicDocument,
            ],
            values);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void WorkerAgentJobPacket_RejectsUndefinedKind()
    {
        var (jobId, scanExecId, repoId, sysId, scanRunId, cuId) = Ids();
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentJobPacket.Create(
                jobId, (WorkerAgentJobKind)999, scanExecId, repoId, sysId, scanRunId, cuId,
                ProfiledRevision, FilePath, 1, 10, SymbolName, CodeUnitType.Method, "csharp", Sha256, Sha256));
    }

    // --- T004: WorkerAgentJobPacket factory validation ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_Create_ValidInput_ReturnsBoundedPacket()
    {
        WorkerAgentJobPacket packet = CreatePacket();
        Assert.Equal(NonEmptyGuid(1), packet.JobId);
        Assert.Equal(WorkerAgentJobKind.Pass1CodeLogicDocument, packet.Kind);
        Assert.Equal(NonEmptyGuid(3), packet.RepositoryId);
        Assert.Equal(ProfiledRevision, packet.ProfiledRevision);
        Assert.Equal(FilePath, packet.SourceSlice.FilePath);
        Assert.Equal("csharp", packet.SourceSlice.Language);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsEmptyJobId()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentJobPacket.Create(
                Guid.Empty, WorkerAgentJobKind.Pass1CodeLogicDocument,
                NonEmptyGuid(2), NonEmptyGuid(3), NonEmptyGuid(4), NonEmptyGuid(5), NonEmptyGuid(6),
                ProfiledRevision, FilePath, 1, 10, SymbolName, CodeUnitType.Method, "csharp", Sha256, Sha256));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("uppercase", "ABCDEF0123456789abcdef0123456789abcdef01")]
    [InlineData("too_long", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")]
    [InlineData("non_hex", "g123456789abcdef0123456789abcdef01234567")]
    public void JobPacket_RejectsInvalidProfiledRevision(string _, string revision) =>
        Assert.Throws<ArgumentException>(() => CreatePacket(profiledRevision: revision));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsAbsoluteHostPath() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(filePath: "/home/user/src/Service.cs"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsWindowsDrivePath() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(filePath: "C:/source/Service.cs"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsParentTraversal() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(filePath: "../escape/Service.cs"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsSecretMarker() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(symbolName: "password=secret"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsPrivateKeyMarker() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(symbolName: "-----BEGIN PRIVATE KEY-----"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsStackTraceDiagnostic() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(symbolName: "stack trace: at System.Method()"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsOversizedFilePath() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(filePath: new string('a', 2_049)));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsOversizedSymbolName() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(symbolName: new string('a', 513)));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsLineStartBelowOne() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkerAgentJobPacket.Create(
                NonEmptyGuid(1), WorkerAgentJobKind.Pass1CodeLogicDocument,
                NonEmptyGuid(2), NonEmptyGuid(3), NonEmptyGuid(4), NonEmptyGuid(5), NonEmptyGuid(6),
                ProfiledRevision, FilePath, 0, 10, SymbolName, CodeUnitType.Method, "csharp", Sha256, Sha256));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsLineEndBelowLineStart() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkerAgentJobPacket.Create(
                NonEmptyGuid(1), WorkerAgentJobKind.Pass1CodeLogicDocument,
                NonEmptyGuid(2), NonEmptyGuid(3), NonEmptyGuid(4), NonEmptyGuid(5), NonEmptyGuid(6),
                ProfiledRevision, FilePath, 10, 5, SymbolName, CodeUnitType.Method, "csharp", Sha256, Sha256));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsInvalidSourceHash() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(sourceHash: "not-a-hash"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_RejectsInvalidFileIdentityHash() =>
        Assert.Throws<ArgumentException>(() => CreatePacket(fileIdentityHash: "short"));

    // --- T005: WorkerAgentResultEnvelope factory validation ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultEnvelope_Create_ValidOutcome_ReturnsEnvelope()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var envelope = WorkerAgentResultEnvelope.Create(
            jobId: NonEmptyGuid(1),
            scanExecutionId: NonEmptyGuid(2),
            kind: WorkerAgentJobKind.Pass1CodeLogicDocument,
            outcome: WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            dispatchedAt: now,
            receivedAt: now.AddSeconds(1));
        Assert.Equal(WorkerAgentResultOutcome.Valid, envelope.Outcome);
        Assert.NotNull(envelope.Payload);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultEnvelope_RejectsReceivedBeforeDispatched() =>
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.Create(
                NonEmptyGuid(1), NonEmptyGuid(2), WorkerAgentJobKind.Pass1CodeLogicDocument,
                WorkerAgentResultOutcome.Valid, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(-1)));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultEnvelope_RequiresReasonOnNonValidOutcome()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.ThrowsAny<ArgumentException>(() =>
            WorkerAgentResultEnvelope.Create(
                NonEmptyGuid(1), NonEmptyGuid(2), WorkerAgentJobKind.Pass1CodeLogicDocument,
                WorkerAgentResultOutcome.SchemaInvalid, null, now, now));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultEnvelope_RejectsPayloadOnNonValidOutcome()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.Create(
                NonEmptyGuid(1), NonEmptyGuid(2), WorkerAgentJobKind.Pass1CodeLogicDocument,
                WorkerAgentResultOutcome.Error, "发生错误。", now, now,
                payload: WorkerAgentResultPayload.Reserved));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultEnvelope_RejectsUndefinedOutcome() =>
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.Create(
                NonEmptyGuid(1), NonEmptyGuid(2), WorkerAgentJobKind.Pass1CodeLogicDocument,
                (WorkerAgentResultOutcome)999, "未知结果。", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultEnvelope_RejectsSecretInReason() =>
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.Create(
                NonEmptyGuid(1), NonEmptyGuid(2), WorkerAgentJobKind.Pass1CodeLogicDocument,
                WorkerAgentResultOutcome.Error, "password=secret leaked", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultEnvelope_RejectsOversizedReason() =>
        Assert.Throws<ArgumentException>(() =>
            WorkerAgentResultEnvelope.Create(
                NonEmptyGuid(1), NonEmptyGuid(2), WorkerAgentJobKind.Pass1CodeLogicDocument,
                WorkerAgentResultOutcome.Error, new string('错', 200), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultEnvelope_OutcomeEnum_IsClosedAllowlist()
    {
        WorkerAgentResultOutcome[] values = Enum.GetValues<WorkerAgentResultOutcome>();
        Assert.Equal(4, values.Length);
        Assert.Contains(WorkerAgentResultOutcome.Valid, values);
        Assert.Contains(WorkerAgentResultOutcome.SchemaInvalid, values);
        Assert.Contains(WorkerAgentResultOutcome.Error, values);
        Assert.Contains(WorkerAgentResultOutcome.Unbounded, values);
    }

    // --- T006: WorkerAgentResultPayload closed allowlist ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResultPayload_Reserved_IsSingleton()
    {
        WorkerAgentResultPayload a = WorkerAgentResultPayload.Reserved;
        WorkerAgentResultPayload b = WorkerAgentResultPayload.Reserved;
        Assert.Same(a, b);
    }

    // --- T007: deterministic ordering (validated via dispatch tests, but
    // the ordering rule is documented in the packet contract) ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JobPacket_FilePath_IsRepositoryRelativeAndNormalized()
    {
        WorkerAgentJobPacket packet = CreatePacket(filePath: "src\\Service.cs");
        Assert.Equal("src/Service.cs", packet.SourceSlice.FilePath);
    }
}
