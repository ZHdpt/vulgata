using Vulgata.Core.Entities;

namespace Vulgata.Tests.DurableScanning;

public sealed class ScanExecutionFoundationTests
{
    private static readonly Guid _executionId =
        Guid.Parse("11000000-0000-4000-8000-000000000011");

    private static readonly Guid _repositoryId =
        Guid.Parse("22000000-0000-4000-8000-000000000022");

    private static readonly Guid _systemId =
        Guid.Parse("33000000-0000-4000-8000-000000000033");

    private static readonly Guid _scanRunId =
        Guid.Parse("44000000-0000-4000-8000-000000000044");

    private static readonly Guid _preScanResultId =
        Guid.Parse("55000000-0000-4000-8000-000000000055");

    private static readonly DateTimeOffset _startedAt =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateNewPath_NormalizesTrustedBindingsRejectsHostPathsAndRetainsLegacyColumns()
    {
        ScanExecution execution = ScanExecution.CreateNewPath(
            _executionId,
            _repositoryId,
            _systemId,
            _scanRunId,
            profiledRevision: " ABCDEF0123456789ABCDEF0123456789ABCDEF01 ",
            promptIdentity: " prompt/1.0 ",
            documentSchemaIdentity: " code-logic/file-v2 ",
            analysisRuntimeIdentityJson: "{\"codeGraph\":\"1.5.0\",\"java\":\"21\"}",
            analysisRuntimeIdentityHash:
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            startedAt: _startedAt);

        Assert.Equal(ScanExecutionPhase.Prepared, execution.Phase);
        Assert.Equal(0, execution.StateVersion);
        Assert.Equal(_startedAt, execution.UpdatedAt);
        Assert.Equal("abcdef0123456789abcdef0123456789abcdef01", execution.ProfiledRevision);
        Assert.Equal("prompt/1.0", execution.PromptIdentity);
        Assert.Equal("code-logic/file-v2", execution.DocumentSchemaIdentity);
        Assert.Equal("{\"codeGraph\":\"1.5.0\",\"java\":\"21\"}", execution.AnalysisRuntimeIdentityJson);
        Assert.Equal(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            execution.AnalysisRuntimeIdentityHash);
        Assert.Null(execution.InFlightJobId);
        Assert.Null(execution.PauseCategory);
        Assert.Null(execution.PauseCode);

        Assert.Throws<ArgumentException>(() => ScanExecution.CreateNewPath(
            _executionId,
            _repositoryId,
            _systemId,
            _scanRunId,
            profiledRevision: "abcdef0123456789abcdef0123456789abcdef01",
            promptIdentity: "prompt/1.0",
            documentSchemaIdentity: "code-logic/file-v2",
            analysisRuntimeIdentityJson: "{\"root\":\"C:\\\\Users\\\\13728\\\\repo\"}",
            analysisRuntimeIdentityHash:
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            startedAt: _startedAt));

        ScanExecution legacy = ScanExecution.Start(
            _executionId,
            _repositoryId,
            _systemId,
            _scanRunId,
            _preScanResultId,
            "abcdef0123456789abcdef0123456789abcdef01",
            _startedAt);

        Assert.Equal(ScanExecutionPhase.Pass1Planning, legacy.Phase);
        Assert.Equal(_preScanResultId, legacy.PreScanResultId);
        Assert.Null(legacy.PromptIdentity);
        Assert.Null(legacy.DocumentSchemaIdentity);
        Assert.Null(legacy.AnalysisRuntimeIdentityJson);
        Assert.Null(legacy.UpdatedAt);
    }

    [Fact]
    public void NewPathTransitions_AdvanceVersionAndUpdatedAtThroughPausesAndWarningsWithoutAutomaticResume()
    {
        ScanExecution execution = CreateNewPath();
        DateTimeOffset runningAt = _startedAt.AddSeconds(1);
        DateTimeOffset providerPausedAt = _startedAt.AddSeconds(2);
        DateTimeOffset preparedAgainAt = _startedAt.AddSeconds(3);
        DateTimeOffset operatorPausedAt = _startedAt.AddSeconds(4);
        DateTimeOffset completedWithWarningsAt = _startedAt.AddSeconds(7);

        execution.BeginRunning(expectedStateVersion: 0, updatedAt: runningAt);
        Assert.Equal(ScanExecutionPhase.Running, execution.Phase);
        Assert.Equal(1, execution.StateVersion);
        Assert.Equal(runningAt, execution.UpdatedAt);

        execution.PauseProviderUnavailable(
            expectedStateVersion: 1,
            pauseCode: " provider-timeout ",
            updatedAt: providerPausedAt);
        Assert.Equal(ScanExecutionPhase.PausedProviderUnavailable, execution.Phase);
        Assert.Equal(2, execution.StateVersion);
        Assert.Equal(providerPausedAt, execution.UpdatedAt);
        Assert.Equal(ScanExecutionPauseCategory.ProviderUnavailable, execution.PauseCategory);
        Assert.Equal("provider-timeout", execution.PauseCode);

        execution.ResumeToPrepared(expectedStateVersion: 2, updatedAt: preparedAgainAt);
        Assert.Equal(ScanExecutionPhase.Prepared, execution.Phase);
        Assert.Equal(3, execution.StateVersion);
        Assert.Equal(preparedAgainAt, execution.UpdatedAt);
        Assert.Null(execution.PauseCategory);
        Assert.Null(execution.PauseCode);

        execution.BeginRunning(expectedStateVersion: 3, updatedAt: preparedAgainAt.AddSeconds(1));
        execution.PauseOperatorCorrection(
            expectedStateVersion: 4,
            pauseCode: " runtime-mismatch ",
            updatedAt: operatorPausedAt);
        Assert.Equal(ScanExecutionPhase.PausedOperatorCorrection, execution.Phase);
        Assert.Equal(5, execution.StateVersion);
        Assert.Equal(ScanExecutionPauseCategory.OperatorCorrection, execution.PauseCategory);
        Assert.Equal("runtime-mismatch", execution.PauseCode);

        execution.ResumeToPrepared(expectedStateVersion: 5, updatedAt: operatorPausedAt.AddSeconds(1));
        execution.BeginRunning(expectedStateVersion: 6, updatedAt: operatorPausedAt.AddSeconds(2));
        execution.CompleteWithWarnings(
            expectedStateVersion: 7,
            terminalMessage: " bounded warning ",
            updatedAt: completedWithWarningsAt);

        Assert.Equal(ScanExecutionPhase.CompletedWithWarnings, execution.Phase);
        Assert.Equal(8, execution.StateVersion);
        Assert.Equal(completedWithWarningsAt, execution.UpdatedAt);
        Assert.Equal("bounded warning", execution.TerminalMessage);
        Assert.Null(execution.PauseCategory);
        Assert.Null(execution.PauseCode);

        Assert.Throws<InvalidOperationException>(() => execution.BeginRunning(
            expectedStateVersion: 8,
            updatedAt: completedWithWarningsAt.AddSeconds(1)));
        Assert.Equal(ScanExecutionPhase.CompletedWithWarnings, execution.Phase);
        Assert.Equal(8, execution.StateVersion);
    }

    [Fact]
    public void NewPathFailuresAndCancellation_UseClosedReasonsAndKeepTerminalStatesImmutable()
    {
        ScanExecution integrityFailure = CreateNewPath();
        integrityFailure.FailIntegrity(
            expectedStateVersion: 0,
            failureCode: " revision-mismatch ",
            updatedAt: _startedAt.AddSeconds(1));
        Assert.Equal(ScanExecutionPhase.FailedIntegrity, integrityFailure.Phase);
        Assert.Equal(1, integrityFailure.StateVersion);
        Assert.Equal(ScanExecutionFailureCategory.Integrity, integrityFailure.FailureCategory);
        Assert.Equal("revision-mismatch", integrityFailure.FailureCode);
        AssertTerminalAndImmutable(integrityFailure);

        ScanExecution securityFailure = CreateNewPath();
        securityFailure.FailSecurity(
            expectedStateVersion: 0,
            failureCode: " forbidden-content ",
            updatedAt: _startedAt.AddSeconds(1));
        Assert.Equal(ScanExecutionPhase.FailedSecurity, securityFailure.Phase);
        Assert.Equal(1, securityFailure.StateVersion);
        Assert.Equal(ScanExecutionFailureCategory.Security, securityFailure.FailureCategory);
        Assert.Equal("forbidden-content", securityFailure.FailureCode);
        AssertTerminalAndImmutable(securityFailure);

        ScanExecution cancelled = CreateNewPath();
        cancelled.Cancel(
            expectedStateVersion: 0,
            cancelCode: " operator-cancelled ",
            updatedAt: _startedAt.AddSeconds(1));
        Assert.Equal(ScanExecutionPhase.Cancelled, cancelled.Phase);
        Assert.Equal(1, cancelled.StateVersion);
        Assert.Equal(ScanExecutionFailureCategory.Cancellation, cancelled.FailureCategory);
        Assert.Equal("operator-cancelled", cancelled.FailureCode);
        AssertTerminalAndImmutable(cancelled);

        Assert.Throws<ArgumentException>(() => CreateNewPath().FailIntegrity(
            expectedStateVersion: 0,
            failureCode: "invalid code!",
            updatedAt: _startedAt.AddSeconds(1)));
        Assert.Equal(
            new[]
            {
                ScanExecutionPhase.Prepared,
                ScanExecutionPhase.Running,
                ScanExecutionPhase.PausedProviderUnavailable,
                ScanExecutionPhase.PausedOperatorCorrection,
                ScanExecutionPhase.CompletedWithWarnings,
                ScanExecutionPhase.FailedIntegrity,
                ScanExecutionPhase.FailedSecurity,
            },
            Enum.GetValues<ScanExecutionPhase>()
                .Where(phase => phase is
                    ScanExecutionPhase.Prepared or
                    ScanExecutionPhase.Running or
                    ScanExecutionPhase.PausedProviderUnavailable or
                    ScanExecutionPhase.PausedOperatorCorrection or
                    ScanExecutionPhase.CompletedWithWarnings or
                    ScanExecutionPhase.FailedIntegrity or
                    ScanExecutionPhase.FailedSecurity)
                .ToArray());
    }

    private static ScanExecution CreateNewPath() => ScanExecution.CreateNewPath(
        _executionId,
        _repositoryId,
        _systemId,
        _scanRunId,
        profiledRevision: "abcdef0123456789abcdef0123456789abcdef01",
        promptIdentity: "prompt/1.0",
        documentSchemaIdentity: "code-logic/file-v2",
        analysisRuntimeIdentityJson: "{\"codeGraph\":\"1.5.0\",\"java\":\"21\"}",
        analysisRuntimeIdentityHash:
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        startedAt: _startedAt);

    private static void AssertTerminalAndImmutable(ScanExecution execution)
    {
        Assert.Null(execution.InFlightJobId);
        Assert.Null(execution.PauseCategory);
        Assert.Null(execution.PauseCode);
        Assert.Throws<InvalidOperationException>(() => execution.BeginRunning(
            expectedStateVersion: execution.StateVersion,
            updatedAt: _startedAt.AddSeconds(2)));
        Assert.Equal(1, execution.StateVersion);
    }
}
