using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedCodeGraphStateCoordinatorTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseOwnedStateAsync_PersistsCreatingAndReadyIdentityBeforeCallback()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        var authorization = new TestToolUseAuthorizer();
        authorization.Allow(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, authorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(fixture.PreparedRootPath);
        var draft = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            rootIdentity,
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);
        var store = new RecordingOwnershipStore();
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            authorization,
            new CurrentWorkspaceOwnershipVerifier(),
            store);

        int callbackCount = 0;
        try
        {
            PreparedToolWorkspaceCheckStatus result = await coordinator.UseOwnedStateAsync(
                lease,
                draft,
                (scope, _) =>
                {
                    callbackCount++;
                    string statePath = scope.GetPhysicalStatePathForTrustedToolBinding();
                    Assert.True(Directory.Exists(statePath), "The state entry must exist before trusted use begins.");
                    Assert.Equal(Path.Combine(fixture.PreparedRootPath, ".codegraph"), statePath);
                    Assert.Equal(
                        new[] { PreparedCodeGraphOwnershipPhase.Creating, PreparedCodeGraphOwnershipPhase.Ready },
                        store.CommittedPhases);

                    PreparedCodeGraphWorkspaceOwnership ready = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(store.CurrentRecord);
                    Assert.Equal(PreparedCodeGraphOwnershipPhase.Ready, ready.Phase);
                    Assert.NotNull(ready.StateEntryObjectIdentity);
                    Assert.Equal(
                        ObserveDirectoryIdentity(statePath),
                        ready.StateEntryObjectIdentity);
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, result);
            Assert.Equal(1, callbackCount);
            Assert.Equal(
                new[] { PreparedCodeGraphOwnershipPhase.Creating, PreparedCodeGraphOwnershipPhase.Ready },
                store.CommittedPhases);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseOwnedStateAsync_LostReadyCommitAcknowledgementNeverPublishesOwnership()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        var authorization = new TestToolUseAuthorizer();
        authorization.Allow(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, authorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var draft = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);
        var store = new RecordingOwnershipStore(loseCreatedCommitAcknowledgement: true);
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            authorization,
            new CurrentWorkspaceOwnershipVerifier(),
            store);

        int callbackCount = 0;
        try
        {
            PreparedToolWorkspaceCheckStatus useResult = await coordinator.UseOwnedStateAsync(
                lease,
                draft,
                (_, _) =>
                {
                    callbackCount++;
                    return ValueTask.CompletedTask;
                });
            PreparedCodeGraphOwnershipViewResult laterView = await coordinator.GetOwnershipViewAsync(
                draft,
                fixture.Approval);

            Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, useResult);
            Assert.Equal(0, callbackCount);
            Assert.NotEqual(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, laterView.Status);
            Assert.Null(laterView.Record);
            Assert.True(store.QuarantineAttempted, "Uncertain Ready persistence must trigger a compare-and-set quarantine attempt.");
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    private static PreparedToolDirectoryObjectIdentity ObserveDirectoryIdentity(string path)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? handle) =
            PreparedSourceTraversal.OpenRoot(path);
        using (handle)
        {
            Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, status);
            PreparedSourceTraversal.DirectoryIdentity observed = Assert.IsType<PreparedSourceTraversal.DirectoryIdentity>(
                handle is null ? null : PreparedSourceTraversal.ObserveDirectory(handle));
            return new PreparedToolDirectoryObjectIdentity(
                observed.VolumeSerialNumber,
                observed.FileIndexHigh,
                observed.FileIndexLow);
        }
    }

    private sealed class RecordingOwnershipStore : IPreparedCodeGraphOwnershipStore
    {
        private readonly List<PreparedCodeGraphOwnershipPhase> _committedPhases = [];
        private readonly bool _loseCreatedCommitAcknowledgement;

        internal RecordingOwnershipStore(bool loseCreatedCommitAcknowledgement = false) =>
            _loseCreatedCommitAcknowledgement = loseCreatedCommitAcknowledgement;

        internal IReadOnlyList<PreparedCodeGraphOwnershipPhase> CommittedPhases => _committedPhases;

        internal PreparedCodeGraphWorkspaceOwnership? CurrentRecord { get; private set; }

        public Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
            PreparedCodeGraphWorkspaceOwnership expectedBinding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedCodeGraphWorkspaceOwnership? record = CurrentRecord;
            return Task.FromResult(record is not null && record.OwnershipId == expectedBinding.OwnershipId
                ? new PreparedCodeGraphOwnershipLoadResult(PreparedCodeGraphOwnershipLoadStatus.Found, record)
                : new PreparedCodeGraphOwnershipLoadResult(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, null));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
            PreparedCodeGraphWorkspaceOwnership absent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string statePath = Path.Combine(absent.PhysicalRootPath, ".codegraph");
            Assert.False(Directory.Exists(statePath), "Creating must be durably committed before the state entry appears.");
            CurrentRecord = absent with { Version = absent.Version + 1, Phase = PreparedCodeGraphOwnershipPhase.Creating };
            _committedPhases.Add(CurrentRecord.Phase);
            return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                PreparedCodeGraphOwnershipTransitionStatus.Committed,
                CurrentRecord));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitCreatedAsync(
            PreparedCodeGraphWorkspaceOwnership creating,
            PreparedToolDirectoryObjectIdentity createdEntry,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(PreparedCodeGraphOwnershipPhase.Creating, creating.Phase);
            Assert.True(Directory.Exists(Path.Combine(creating.PhysicalRootPath, ".codegraph")));
            CurrentRecord = creating with
            {
                Version = creating.Version + 1,
                Phase = PreparedCodeGraphOwnershipPhase.Ready,
                StateEntryObjectIdentity = createdEntry,
            };
            _committedPhases.Add(CurrentRecord.Phase);
            if (_loseCreatedCommitAcknowledgement)
            {
                // Model a durable Ready write whose acknowledgement is lost.
                return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                    PreparedCodeGraphOwnershipTransitionStatus.Unavailable,
                    null));
            }

            return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                PreparedCodeGraphOwnershipTransitionStatus.Committed,
                CurrentRecord));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginDispositionAsync(
            PreparedCodeGraphWorkspaceOwnership ready,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This tracer-bullet scenario does not dispose the workspace.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
            PreparedCodeGraphWorkspaceOwnership disposing,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This tracer-bullet scenario does not dispose the workspace.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> QuarantineAsync(
            PreparedCodeGraphWorkspaceOwnership current,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QuarantineAttempted = true;
            if (CurrentRecord is null ||
                CurrentRecord.OwnershipId != current.OwnershipId ||
                CurrentRecord.Version != current.Version ||
                CurrentRecord.Phase != current.Phase)
            {
                return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                    PreparedCodeGraphOwnershipTransitionStatus.Conflict,
                    CurrentRecord));
            }

            CurrentRecord = CurrentRecord with
            {
                Version = CurrentRecord.Version + 1,
                Phase = PreparedCodeGraphOwnershipPhase.Quarantined,
            };
            _committedPhases.Add(CurrentRecord.Phase);
            return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                PreparedCodeGraphOwnershipTransitionStatus.Committed,
                CurrentRecord));
        }

        internal bool QuarantineAttempted { get; private set; }
    }

    private sealed class CurrentWorkspaceOwnershipVerifier : IPreparedCodeGraphWorkspaceOwnershipVerifier
    {
        public ValueTask<bool> VerifyCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);

        public ValueTask<bool> VerifyDispositionReadyAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }

    private sealed class TestToolUseAuthorizer : IPreparedToolUseAuthorizer
    {
        private PreparedSourceApproval? _approval;
        private PreparedToolPurpose _purpose;

        internal void Allow(PreparedSourceApproval approval, PreparedToolPurpose purpose)
        {
            _approval = approval;
            _purpose = purpose;
        }

        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedToolUseAuthorityStatus status = approval == _approval && toolPurpose == _purpose
                ? PreparedToolUseAuthorityStatus.Current
                : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }
}
