using Vulgata.Core.Entities;

namespace Vulgata.Tests.DurableScanning;

public sealed class TwoRepositoryScanCoordinationTests
{
    private static readonly Guid _systemId =
        Guid.Parse("31000000-0000-4000-8000-000000000031");

    private static readonly Guid _repositoryOneId =
        Guid.Parse("32000000-0000-4000-8000-000000000032");

    private static readonly Guid _repositoryTwoId =
        Guid.Parse("33000000-0000-4000-8000-000000000033");

    private static readonly Guid _scanRunOneId =
        Guid.Parse("34000000-0000-4000-8000-000000000034");

    private static readonly Guid _scanRunTwoId =
        Guid.Parse("35000000-0000-4000-8000-000000000035");

    private static readonly Guid _scanExecutionOneId =
        Guid.Parse("36000000-0000-4000-8000-000000000036");

    private static readonly Guid _scanExecutionTwoId =
        Guid.Parse("37000000-0000-4000-8000-000000000037");

    private static readonly DateTimeOffset _createdAt =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithExactlyTwoSameSystemScans_RetainsCanonicalIndependentBindingsAndIdentity()
    {
        ScanExecution repositoryOne = CreateNewPath(
            _repositoryOneId,
            _scanRunOneId,
            _scanExecutionOneId,
            " ABCDEF0123456789ABCDEF0123456789ABCDEF01 ");
        ScanExecution repositoryTwo = CreateNewPath(
            _repositoryTwoId,
            _scanRunTwoId,
            _scanExecutionTwoId,
            " 0123456789ABCDEF0123456789ABCDEF01234567 ");

        TwoRepositoryScanCoordination coordination =
            TwoRepositoryScanCoordination.Create(repositoryOne, repositoryTwo, _createdAt);
        TwoRepositoryScanCoordination reversed =
            TwoRepositoryScanCoordination.Create(repositoryTwo, repositoryOne, _createdAt);

        Assert.Equal(coordination.Id, reversed.Id);
        Assert.Equal(_systemId, coordination.SystemId);
        Assert.Equal(_repositoryOneId, coordination.RepositoryOneId);
        Assert.Equal(_repositoryTwoId, coordination.RepositoryTwoId);
        Assert.Equal(_scanRunOneId, coordination.ScanRunOneId);
        Assert.Equal(_scanRunTwoId, coordination.ScanRunTwoId);
        Assert.Equal(_scanExecutionOneId, coordination.ScanExecutionOneId);
        Assert.Equal(_scanExecutionTwoId, coordination.ScanExecutionTwoId);
        Assert.Equal(
            "abcdef0123456789abcdef0123456789abcdef01",
            coordination.ProfiledRevisionOne);
        Assert.Equal(
            "0123456789abcdef0123456789abcdef01234567",
            coordination.ProfiledRevisionTwo);
        Assert.Equal(TwoRepositoryScanCoordinationPhase.Preparing, coordination.Phase);
        Assert.Equal(0, coordination.StateVersion);
        Assert.Equal(_createdAt, coordination.CreatedAt);
        Assert.Equal(_createdAt, coordination.UpdatedAt);
        Assert.Null(coordination.ReasonCategory);
        Assert.Null(coordination.ReasonCode);
        Assert.Null(coordination.TerminalAt);

        ScanExecution wrongSystem = ScanExecution.CreateNewPath(
            Guid.Parse("38000000-0000-4000-8000-000000000038"),
            Guid.Parse("39000000-0000-4000-8000-000000000039"),
            Guid.Parse("3a000000-0000-4000-8000-00000000003a"),
            Guid.Parse("3b000000-0000-4000-8000-00000000003b"),
            "0123456789abcdef0123456789abcdef01234567",
            "prompt/1.0",
            "code-logic/file-v2",
            "{\"provider\":\"deepseek-v4\"}",
            new string('b', 64),
            _createdAt);

        Assert.Throws<ArgumentException>(() =>
            TwoRepositoryScanCoordination.Create(repositoryOne, wrongSystem, _createdAt));

        ScanExecution duplicateRepository = CreateNewPath(
            _repositoryOneId,
            Guid.Parse("3c000000-0000-4000-8000-00000000003c"),
            Guid.Parse("3d000000-0000-4000-8000-00000000003d"),
            "0123456789abcdef0123456789abcdef01234567");

        Assert.Throws<ArgumentException>(() =>
            TwoRepositoryScanCoordination.Create(repositoryOne, duplicateRepository, _createdAt));
    }

    [Fact]
    public void Lifecycle_UsesClosedCombinedPhasesReasonCodesAndMonotonicExpectedVersions()
    {
        TwoRepositoryScanCoordination coordination = CreateCoordination();

        coordination.BeginPass1(expectedStateVersion: 0, updatedAt: _createdAt.AddSeconds(1));
        Assert.Equal(TwoRepositoryScanCoordinationPhase.Pass1, coordination.Phase);
        Assert.Equal(1, coordination.StateVersion);
        Assert.Equal(_createdAt.AddSeconds(1), coordination.UpdatedAt);

        Assert.Throws<InvalidOperationException>(() => coordination.BeginResolving(
            expectedStateVersion: 0,
            updatedAt: _createdAt.AddSeconds(2)));
        Assert.Equal(TwoRepositoryScanCoordinationPhase.Pass1, coordination.Phase);
        Assert.Equal(1, coordination.StateVersion);

        coordination.BeginResolving(
            expectedStateVersion: 1,
            updatedAt: _createdAt.AddSeconds(2));
        coordination.BeginPass2(
            expectedStateVersion: 2,
            updatedAt: _createdAt.AddSeconds(3));
        Assert.Equal(TwoRepositoryScanCoordinationPhase.Pass2, coordination.Phase);
        Assert.Equal(3, coordination.StateVersion);

        coordination.Pause(
            expectedStateVersion: 3,
            reasonCategory: TwoRepositoryScanReasonCategory.ProviderUnavailable,
            reasonCode: " provider-timeout ",
            updatedAt: _createdAt.AddSeconds(4));

        Assert.Equal(TwoRepositoryScanCoordinationPhase.Paused, coordination.Phase);
        Assert.Equal(4, coordination.StateVersion);
        Assert.Equal(
            TwoRepositoryScanReasonCategory.ProviderUnavailable,
            coordination.ReasonCategory);
        Assert.Equal("provider-timeout", coordination.ReasonCode);
        Assert.Equal(_createdAt.AddSeconds(4), coordination.UpdatedAt);

        Assert.Equal(
            new[]
            {
                TwoRepositoryScanCoordinationPhase.Preparing,
                TwoRepositoryScanCoordinationPhase.Pass1,
                TwoRepositoryScanCoordinationPhase.Resolving,
                TwoRepositoryScanCoordinationPhase.Pass2,
                TwoRepositoryScanCoordinationPhase.Paused,
                TwoRepositoryScanCoordinationPhase.Completed,
                TwoRepositoryScanCoordinationPhase.CompletedWithWarnings,
                TwoRepositoryScanCoordinationPhase.FailedIntegrity,
                TwoRepositoryScanCoordinationPhase.FailedSecurity,
                TwoRepositoryScanCoordinationPhase.Cancelled,
            },
            Enum.GetValues<TwoRepositoryScanCoordinationPhase>());

        Assert.Equal(
            new[]
            {
                TwoRepositoryScanReasonCategory.ProviderUnavailable,
                TwoRepositoryScanReasonCategory.OperatorCorrection,
                TwoRepositoryScanReasonCategory.Integrity,
                TwoRepositoryScanReasonCategory.Security,
                TwoRepositoryScanReasonCategory.Cancellation,
                TwoRepositoryScanReasonCategory.Unclassified,
            },
            Enum.GetValues<TwoRepositoryScanReasonCategory>());
    }

    [Fact]
    public void CompleteFromChildren_RequiresTwoValidTerminalScansPropagatesWarningsAndPreservesOtherOutcome()
    {
        ScanExecution completedOne = CreateLegacyCompleted(
            _repositoryOneId,
            _scanRunOneId,
            _scanExecutionOneId,
            "abcdef0123456789abcdef0123456789abcdef01");
        ScanExecution completedTwo = CreateLegacyCompleted(
            _repositoryTwoId,
            _scanRunTwoId,
            _scanExecutionTwoId,
            "0123456789abcdef0123456789abcdef01234567");
        TwoRepositoryScanCoordination completed =
            TwoRepositoryScanCoordination.Create(completedOne, completedTwo, _createdAt);

        Assert.Throws<InvalidOperationException>(() => completed.CompleteFromChildren(
            expectedStateVersion: 0,
            repositoryOne: CreateNewPath(
                _repositoryOneId,
                _scanRunOneId,
                _scanExecutionOneId,
                "abcdef0123456789abcdef0123456789abcdef01"),
            repositoryTwo: completedTwo,
            updatedAt: _createdAt.AddSeconds(1)));
        Assert.Equal(TwoRepositoryScanCoordinationPhase.Preparing, completed.Phase);
        Assert.Equal(0, completed.StateVersion);

        completed.BeginPass1(expectedStateVersion: 0, updatedAt: _createdAt.AddSeconds(1));
        completed.BeginResolving(expectedStateVersion: 1, updatedAt: _createdAt.AddSeconds(2));
        completed.BeginPass2(expectedStateVersion: 2, updatedAt: _createdAt.AddSeconds(3));
        completed.CompleteFromChildren(
            expectedStateVersion: 3,
            repositoryOne: completedOne,
            repositoryTwo: completedTwo,
            updatedAt: _createdAt.AddSeconds(4));

        Assert.Equal(TwoRepositoryScanCoordinationPhase.Completed, completed.Phase);
        Assert.Equal(4, completed.StateVersion);
        Assert.Equal(_createdAt.AddSeconds(4), completed.TerminalAt);
        Assert.Null(completed.ReasonCategory);
        Assert.Null(completed.ReasonCode);
        AssertTerminalAndImmutable(completed);

        ScanExecution warningOne = CreateWarning(
            _repositoryOneId,
            _scanRunOneId,
            _scanExecutionOneId,
            "abcdef0123456789abcdef0123456789abcdef01");
        TwoRepositoryScanCoordination warningCoordination =
            TwoRepositoryScanCoordination.Create(warningOne, completedTwo, _createdAt);
        warningCoordination.BeginPass1(expectedStateVersion: 0, updatedAt: _createdAt.AddSeconds(1));
        warningCoordination.BeginResolving(
            expectedStateVersion: 1,
            updatedAt: _createdAt.AddSeconds(2));
        warningCoordination.BeginPass2(
            expectedStateVersion: 2,
            updatedAt: _createdAt.AddSeconds(3));
        warningCoordination.CompleteFromChildren(
            expectedStateVersion: 3,
            repositoryOne: warningOne,
            repositoryTwo: completedTwo,
            updatedAt: _createdAt.AddSeconds(4));

        Assert.Equal(
            TwoRepositoryScanCoordinationPhase.CompletedWithWarnings,
            warningCoordination.Phase);
        Assert.Equal(4, warningCoordination.StateVersion);
        AssertTerminalAndImmutable(warningCoordination);

        foreach (ScanExecution invalidChild in new[]
        {
            CreateFailure(
                _repositoryOneId,
                _scanRunOneId,
                _scanExecutionOneId,
                "abcdef0123456789abcdef0123456789abcdef01",
                security: false),
            CreateFailure(
                _repositoryOneId,
                _scanRunOneId,
                _scanExecutionOneId,
                "abcdef0123456789abcdef0123456789abcdef01",
                security: true),
            CreateCancelled(
                _repositoryOneId,
                _scanRunOneId,
                _scanExecutionOneId,
                "abcdef0123456789abcdef0123456789abcdef01"),
        })
        {
            TwoRepositoryScanCoordination blocked =
                TwoRepositoryScanCoordination.Create(invalidChild, completedTwo, _createdAt);

            blocked.BeginPass1(
                expectedStateVersion: 0,
                updatedAt: _createdAt.AddSeconds(1));
            blocked.BeginResolving(
                expectedStateVersion: 1,
                updatedAt: _createdAt.AddSeconds(2));
            blocked.BeginPass2(
                expectedStateVersion: 2,
                updatedAt: _createdAt.AddSeconds(3));

            long invalidStateVersion = invalidChild.StateVersion;
            DateTimeOffset? invalidUpdatedAt = invalidChild.UpdatedAt;
            ScanExecutionPhase invalidPhase = invalidChild.Phase;
            long validStateVersion = completedTwo.StateVersion;
            DateTimeOffset? validUpdatedAt = completedTwo.UpdatedAt;
            ScanExecutionPhase validPhase = completedTwo.Phase;
            Guid coordinationId = blocked.Id;
            Guid repositoryOneId = blocked.RepositoryOneId;
            Guid repositoryTwoId = blocked.RepositoryTwoId;
            Guid scanRunOneId = blocked.ScanRunOneId;
            Guid scanRunTwoId = blocked.ScanRunTwoId;
            Guid scanExecutionOneId = blocked.ScanExecutionOneId;
            Guid scanExecutionTwoId = blocked.ScanExecutionTwoId;
            string profiledRevisionOne = blocked.ProfiledRevisionOne;
            string profiledRevisionTwo = blocked.ProfiledRevisionTwo;

            InvalidOperationException rejection = Assert.Throws<InvalidOperationException>(() => blocked.CompleteFromChildren(
                expectedStateVersion: 3,
                repositoryOne: invalidChild,
                repositoryTwo: completedTwo,
                updatedAt: _createdAt.AddSeconds(4)));
            Assert.Contains("valid terminal success outcomes", rejection.Message, StringComparison.Ordinal);
            Assert.Equal(TwoRepositoryScanCoordinationPhase.Pass2, blocked.Phase);
            Assert.Equal(3, blocked.StateVersion);
            Assert.Equal(invalidPhase, invalidChild.Phase);
            Assert.Equal(invalidStateVersion, invalidChild.StateVersion);
            Assert.Equal(invalidUpdatedAt, invalidChild.UpdatedAt);
            Assert.Equal(validPhase, completedTwo.Phase);
            Assert.Equal(validStateVersion, completedTwo.StateVersion);
            Assert.Equal(validUpdatedAt, completedTwo.UpdatedAt);
            Assert.Equal(coordinationId, blocked.Id);
            Assert.Equal(repositoryOneId, blocked.RepositoryOneId);
            Assert.Equal(repositoryTwoId, blocked.RepositoryTwoId);
            Assert.Equal(scanRunOneId, blocked.ScanRunOneId);
            Assert.Equal(scanRunTwoId, blocked.ScanRunTwoId);
            Assert.Equal(scanExecutionOneId, blocked.ScanExecutionOneId);
            Assert.Equal(scanExecutionTwoId, blocked.ScanExecutionTwoId);
            Assert.Equal(profiledRevisionOne, blocked.ProfiledRevisionOne);
            Assert.Equal(profiledRevisionTwo, blocked.ProfiledRevisionTwo);
        }
    }

    [Fact]
    public void FailureAndCancellation_UseClosedReasonsAndRemainTerminalAndImmutable()
    {
        TwoRepositoryScanCoordination integrity = CreateCoordination();
        integrity.FailIntegrity(
            expectedStateVersion: 0,
            reasonCode: " revision-mismatch ",
            updatedAt: _createdAt.AddSeconds(1));
        Assert.Equal(TwoRepositoryScanCoordinationPhase.FailedIntegrity, integrity.Phase);
        Assert.Equal(TwoRepositoryScanReasonCategory.Integrity, integrity.ReasonCategory);
        Assert.Equal("revision-mismatch", integrity.ReasonCode);
        AssertTerminalAndImmutable(integrity);

        TwoRepositoryScanCoordination security = CreateCoordination();
        security.FailSecurity(
            expectedStateVersion: 0,
            reasonCode: " forbidden-content ",
            updatedAt: _createdAt.AddSeconds(1));
        Assert.Equal(TwoRepositoryScanCoordinationPhase.FailedSecurity, security.Phase);
        Assert.Equal(TwoRepositoryScanReasonCategory.Security, security.ReasonCategory);
        Assert.Equal("forbidden-content", security.ReasonCode);
        AssertTerminalAndImmutable(security);

        TwoRepositoryScanCoordination cancelled = CreateCoordination();
        cancelled.Cancel(
            expectedStateVersion: 0,
            reasonCode: " operator-cancelled ",
            updatedAt: _createdAt.AddSeconds(1));
        Assert.Equal(TwoRepositoryScanCoordinationPhase.Cancelled, cancelled.Phase);
        Assert.Equal(TwoRepositoryScanReasonCategory.Cancellation, cancelled.ReasonCategory);
        Assert.Equal("operator-cancelled", cancelled.ReasonCode);
        AssertTerminalAndImmutable(cancelled);

        Assert.Throws<ArgumentException>(() => CreateCoordination().FailIntegrity(
            expectedStateVersion: 0,
            reasonCode: "invalid code!",
            updatedAt: _createdAt.AddSeconds(1)));
    }

    private static TwoRepositoryScanCoordination CreateCoordination() =>
        TwoRepositoryScanCoordination.Create(
            CreateNewPath(
                _repositoryOneId,
                _scanRunOneId,
                _scanExecutionOneId,
                "abcdef0123456789abcdef0123456789abcdef01"),
            CreateNewPath(
                _repositoryTwoId,
                _scanRunTwoId,
                _scanExecutionTwoId,
                "0123456789abcdef0123456789abcdef01234567"),
            _createdAt);

    private static ScanExecution CreateNewPath(
        Guid repositoryId,
        Guid scanRunId,
        Guid scanExecutionId,
        string profiledRevision) => ScanExecution.CreateNewPath(
        scanExecutionId,
        repositoryId,
        _systemId,
        scanRunId,
        profiledRevision,
        promptIdentity: "prompt/1.0",
        documentSchemaIdentity: "code-logic/file-v2",
        analysisRuntimeIdentityJson: "{\"provider\":\"deepseek-v4\"}",
        analysisRuntimeIdentityHash: new string('a', 64),
        startedAt: _createdAt);

    private static ScanExecution CreateLegacyCompleted(
        Guid repositoryId,
        Guid scanRunId,
        Guid scanExecutionId,
        string profiledRevision)
    {
        ScanExecution execution = ScanExecution.Start(
            scanExecutionId,
            repositoryId,
            _systemId,
            scanRunId,
            Guid.Parse("3e000000-0000-4000-8000-00000000003e"),
            profiledRevision,
            _createdAt);
        Guid jobId = Guid.Parse("3f000000-0000-4000-8000-00000000003f");
        execution.Plan(1, _createdAt.AddSeconds(1));
        execution.Advance(jobId);
        execution.RecordValid(jobId);
        execution.Finalize("complete", _createdAt.AddSeconds(2));
        return execution;
    }

    private static ScanExecution CreateWarning(
        Guid repositoryId,
        Guid scanRunId,
        Guid scanExecutionId,
        string profiledRevision)
    {
        ScanExecution execution = CreateNewPath(
            repositoryId,
            scanRunId,
            scanExecutionId,
            profiledRevision);
        execution.BeginRunning(expectedStateVersion: 0, updatedAt: _createdAt.AddSeconds(1));
        execution.CompleteWithWarnings(
            expectedStateVersion: 1,
            terminalMessage: "bounded warning",
            updatedAt: _createdAt.AddSeconds(2));
        return execution;
    }

    private static ScanExecution CreateFailure(
        Guid repositoryId,
        Guid scanRunId,
        Guid scanExecutionId,
        string profiledRevision,
        bool security)
    {
        ScanExecution execution = CreateNewPath(
            repositoryId,
            scanRunId,
            scanExecutionId,
            profiledRevision);
        if (security)
        {
            execution.FailSecurity(
                expectedStateVersion: 0,
                failureCode: "forbidden-content",
                updatedAt: _createdAt.AddSeconds(1));
        }
        else
        {
            execution.FailIntegrity(
                expectedStateVersion: 0,
                failureCode: "revision-mismatch",
                updatedAt: _createdAt.AddSeconds(1));
        }

        return execution;
    }

    private static ScanExecution CreateCancelled(
        Guid repositoryId,
        Guid scanRunId,
        Guid scanExecutionId,
        string profiledRevision)
    {
        ScanExecution execution = CreateNewPath(
            repositoryId,
            scanRunId,
            scanExecutionId,
            profiledRevision);
        execution.Cancel(
            expectedStateVersion: 0,
            cancelCode: "operator-cancelled",
            updatedAt: _createdAt.AddSeconds(1));
        return execution;
    }

    private static void AssertTerminalAndImmutable(
        TwoRepositoryScanCoordination coordination)
    {
        long version = coordination.StateVersion;
        Assert.NotNull(coordination.TerminalAt);
        Assert.Throws<InvalidOperationException>(() => coordination.BeginPass1(
            expectedStateVersion: version,
            updatedAt: _createdAt.AddSeconds(10)));
        Assert.Throws<InvalidOperationException>(() => coordination.FailIntegrity(
            expectedStateVersion: version,
            reasonCode: "late-failure",
            updatedAt: _createdAt.AddSeconds(10)));
        Assert.Equal(version, coordination.StateVersion);
    }
}
