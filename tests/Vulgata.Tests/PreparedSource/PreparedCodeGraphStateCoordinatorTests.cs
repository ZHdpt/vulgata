using Microsoft.Extensions.Configuration;
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
    public async Task ProvisionNewAsync_InitializedStoreLoadsAsVerifiedAbsent()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceStorage:RootPath"] = sourceStorageRoot,
                ["SourceStorage:MaximumEntries"] = "10000",
                ["SourceStorage:MaximumBlobBytes"] = "8388608",
                ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
                ["SourceStorage:CaptureDeadlineSeconds"] = "600",
                ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
                ["SourceStorage:VerificationBufferBytes"] = "65536",
                ["SourceStorage:Read:DeadlineSeconds"] = "60",
            })
            .Build();
        RetainedSourceCapturePolicy policy = RetainedSourceCapturePolicy.Create(
            configuration,
            fixture.SandboxPath,
            fixture.PreparedRootPath);
        var expectedBinding = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);

        PreparedCodeGraphStoreProvisionStatus provisionStatus =
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(policy);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            provisionStatus);

        var store = new FilePreparedCodeGraphOwnershipStore(policy);
        PreparedCodeGraphOwnershipLoadResult load = await store.LoadAsync(expectedBinding);

        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, load.Status);
        Assert.Null(load.Record);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BeginCreateAsync_PersistsCreatingRecordVisibleFromFreshStore()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceStorage:RootPath"] = sourceStorageRoot,
                ["SourceStorage:MaximumEntries"] = "10000",
                ["SourceStorage:MaximumBlobBytes"] = "8388608",
                ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
                ["SourceStorage:CaptureDeadlineSeconds"] = "600",
                ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
                ["SourceStorage:VerificationBufferBytes"] = "65536",
                ["SourceStorage:Read:DeadlineSeconds"] = "60",
            })
            .Build();
        RetainedSourceCapturePolicy policy = RetainedSourceCapturePolicy.Create(
            configuration,
            fixture.SandboxPath,
            fixture.PreparedRootPath);
        var absent = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);

        PreparedCodeGraphStoreProvisionStatus provisionStatus =
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(policy);
        Assert.Equal(PreparedCodeGraphStoreProvisionStatus.Provisioned, provisionStatus);

        var store = new FilePreparedCodeGraphOwnershipStore(policy);
        PreparedCodeGraphOwnershipLoadResult initialLoad = await store.LoadAsync(absent);
        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, initialLoad.Status);
        Assert.Null(initialLoad.Record);

        PreparedCodeGraphOwnershipTransitionResult transition = await store.BeginCreateAsync(absent);

        Assert.Equal(PreparedCodeGraphOwnershipTransitionStatus.Committed, transition.Status);
        PreparedCodeGraphWorkspaceOwnership creating = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
            transition.Record);
        Assert.Equal(PreparedCodeGraphOwnershipPhase.Creating, creating.Phase);
        Assert.True(
            creating.Version > absent.Version,
            "A committed Creating transition must advance the ownership version.");
        Assert.Equal(absent.OwnershipId, creating.OwnershipId);
        Assert.Equal(absent.Approval, creating.Approval);
        Assert.Equal(absent.PhysicalRootPath, creating.PhysicalRootPath);
        Assert.Equal(absent.RootObjectIdentity, creating.RootObjectIdentity);
        Assert.Equal(absent.WorkspaceOwnerIdentity, creating.WorkspaceOwnerIdentity);
        Assert.Null(creating.StateEntryObjectIdentity);

        var freshStore = new FilePreparedCodeGraphOwnershipStore(policy);
        PreparedCodeGraphOwnershipLoadResult freshLoad = await freshStore.LoadAsync(absent);

        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, freshLoad.Status);
        Assert.Equal(creating, freshLoad.Record);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseOwnedStateAsync_RequiresLeaseAndOwnershipApprovalToMatchExactly()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceApproval leaseApproval = fixture.Approval;
        PreparedSourceApproval ownershipApproval = leaseApproval with
        {
            BatchId = Guid.NewGuid(),
            SubjectId = "another-current-subject",
            ApprovalIdentity = "another-current-approval",
            RootBindingIdentity = "another-current-root-binding",
        };
        var sourceAuthority = new CurrentPreparedSourceApprovals(leaseApproval, ownershipApproval);
        var toolAuthorization = new CurrentPreparedToolGrants(
            (leaseApproval, PreparedToolPurpose.CodeGraphPreparation),
            (ownershipApproval, PreparedToolPurpose.CodeGraphPreparation));
        var issuer = new PreparedToolWorkspaceIssuer(sourceAuthority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                leaseApproval,
                fixture.PreparedRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var ownership = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            ownershipApproval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);
        var store = new RecordingOwnershipStore();
        var coordinator = new PreparedCodeGraphStateCoordinator(
            sourceAuthority,
            toolAuthorization,
            new ExactWorkspaceOwnershipVerifier(ownership),
            store);
        int callbackCount = 0;

        try
        {
            PreparedToolWorkspaceCheckStatus result = await coordinator.UseOwnedStateAsync(
                lease,
                ownership,
                (_, _) =>
                {
                    callbackCount++;
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, result);
            Assert.Equal(0, callbackCount);
            Assert.Empty(store.CommittedPhases);
            Assert.Null(store.CurrentRecord);
            Assert.False(Directory.Exists(Path.Combine(fixture.PreparedRootPath, ".codegraph")));
            Assert.Contains(leaseApproval, sourceAuthority.VerifiedApprovals);
            Assert.Contains(ownershipApproval, sourceAuthority.VerifiedApprovals);
            Assert.Contains(
                (leaseApproval, PreparedToolPurpose.CodeGraphPreparation),
                toolAuthorization.VerifiedGrants);
            Assert.Contains(
                (ownershipApproval, PreparedToolPurpose.CodeGraphPreparation),
                toolAuthorization.VerifiedGrants);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

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
            new RecordingWorkspaceOwnershipVerifier(),
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
            new RecordingWorkspaceOwnershipVerifier(),
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseOwnedStateAsync_LostBeginCreateAcknowledgementQuarantinesPersistedRecord()
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

        var absent = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);
        var store = new RecordingOwnershipStore
        {
            LoseBeginCreateAcknowledgement = true,
        };
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            authorization,
            new RecordingWorkspaceOwnershipVerifier(),
            store);
        int callbackCount = 0;

        try
        {
            PreparedToolWorkspaceCheckStatus result = await coordinator.UseOwnedStateAsync(
                lease,
                absent,
                (_, _) =>
                {
                    callbackCount++;
                    return ValueTask.CompletedTask;
                });
            PreparedCodeGraphOwnershipViewResult laterView = await coordinator.GetOwnershipViewAsync(
                absent,
                fixture.Approval);

            Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, result);
            Assert.Equal(0, callbackCount);
            Assert.False(Directory.Exists(Path.Combine(fixture.PreparedRootPath, ".codegraph")));
            Assert.True(store.QuarantineAttempted,
                "An uncertain Creating commit must quarantine its exact persisted record when the store is reachable.");
            Assert.Equal(absent.OwnershipId, store.CurrentRecord?.OwnershipId);
            Assert.Equal(absent.Version + 2, store.CurrentRecord?.Version);
            Assert.Equal(PreparedCodeGraphOwnershipPhase.Quarantined, store.CurrentRecord?.Phase);
            Assert.Equal(1, store.QuarantineReadbackCount);
            Assert.Equal(PreparedCodeGraphOwnershipViewStatus.Unavailable, laterView.Status);
            Assert.Null(laterView.Record);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseOwnedStateAsync_ConflictingCreateDoesNotQuarantineOtherOwner()
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

        var absent = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);
        var competingCreating = absent with
        {
            OwnershipId = Guid.NewGuid(),
            Version = 1,
            Phase = PreparedCodeGraphOwnershipPhase.Creating,
            WorkspaceOwnerIdentity = "competing-workspace-owner",
        };
        var store = new RecordingOwnershipStore
        {
            CompetingCreateRecord = competingCreating,
        };
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            authorization,
            new RecordingWorkspaceOwnershipVerifier(),
            store);
        int callbackCount = 0;
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);

        try
        {
            PreparedToolWorkspaceCheckStatus result = await coordinator.UseOwnedStateAsync(
                lease,
                absent,
                (_, _) =>
                {
                    callbackCount++;
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, result);
            Assert.Equal(0, callbackCount);
            Assert.False(Directory.Exists(Path.Combine(fixture.PreparedRootPath, ".codegraph")));
            Assert.False(store.QuarantineAttempted,
                "A create conflict must not let this request quarantine a different owner’s record.");
            Assert.Equal(competingCreating, store.CurrentRecord);
            Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
            Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeOwnedStateAsync_CommitsDisposingBeforeRemovalAndReadsBackRemoved()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string statePath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        string cachePath = Path.Combine(statePath, "cache", "index.db");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllTextAsync(cachePath, "owned state fixture");
        Assert.True(File.Exists(fixture.UnfamiliarTextPath), "The fixture source must exist before disposition.");

        var ready = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 2,
            PreparedCodeGraphOwnershipPhase.Ready,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(statePath));
        var store = new RecordingOwnershipStore();
        store.Seed(ready);
        var authorization = new TestToolUseAuthorizer();
        authorization.Allow(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        var ownerVerifier = new RecordingWorkspaceOwnershipVerifier();
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            authorization,
            ownerVerifier,
            store);

        PreparedToolWorkspaceCheckStatus result = await coordinator.DisposeOwnedStateAsync(
            fixture.Approval,
            ready);

        Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, result);
        Assert.Equal(
            new[]
            {
                PreparedCodeGraphOwnershipPhase.Ready,
                PreparedCodeGraphOwnershipPhase.Disposing,
                PreparedCodeGraphOwnershipPhase.Removed,
            },
            store.CommittedPhases);
        Assert.Equal(PreparedCodeGraphOwnershipPhase.Removed, store.CurrentRecord?.Phase);
        Assert.Equal(4, store.CurrentRecord?.Version);
        Assert.Null(store.CurrentRecord?.StateEntryObjectIdentity);
        Assert.Equal(1, store.TerminalReadbackCount);
        Assert.True(ownerVerifier.DispositionReadyChecks >= 2,
            "Trusted owner readiness must be rechecked before and after removing the entry.");
        Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        Assert.Equal(fixture.Approval, authorization.LastApproval);
        Assert.Equal(PreparedToolPurpose.CodeGraphPreparation, authorization.LastPurpose);
        Assert.False(Directory.Exists(statePath), "Disposition must remove only the recorded CodeGraph state tree.");
        Assert.True(File.Exists(fixture.UnfamiliarTextPath), "Prepared source must remain untouched by state disposition.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetOwnershipViewAsync_ReplacesStaleReadyCopyWithCurrentExactRecord()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string statePath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(statePath);
        await File.WriteAllTextAsync(Path.Combine(statePath, "index.bin"), "owned fixture state");
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);
        PreparedToolDirectoryObjectIdentity stateIdentity = ObserveDirectoryIdentity(statePath);
        var currentReady = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 8,
            PreparedCodeGraphOwnershipPhase.Ready,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: stateIdentity);
        PreparedToolDirectoryObjectIdentity staleEntryIdentity = stateIdentity with
        {
            FileIndexHigh = stateIdentity.FileIndexHigh ^ 1u,
        };
        PreparedCodeGraphWorkspaceOwnership staleReadyCopy = currentReady with
        {
            Version = currentReady.Version - 1,
            StateEntryObjectIdentity = staleEntryIdentity,
        };

        var store = new RecordingOwnershipStore();
        store.Seed(currentReady);
        // Ownership classification is a source/workspace view; it must not require
        // a CodeGraph-purpose grant that authorizes preparation or mutation.
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            new TestToolUseAuthorizer(),
            new RecordingWorkspaceOwnershipVerifier(),
            store);

        PreparedCodeGraphOwnershipViewResult result = await coordinator.GetOwnershipViewAsync(
            staleReadyCopy,
            fixture.Approval);

        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, result.Status);
        Assert.Equal(currentReady, result.Record);
        Assert.NotEqual(staleReadyCopy, result.Record);
        Assert.Equal(stateIdentity, result.Record?.StateEntryObjectIdentity);
        Assert.Equal(fixture.Approval, fixture.Authority.LastRequestedApproval);
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
        Assert.True(File.Exists(Path.Combine(statePath, "index.bin")));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetOwnershipViewAsync_DistinguishesUnavailableAbsenceAndForeignOwnership()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string statePath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(statePath);
        string unownedContentPath = Path.Combine(statePath, "source-owned-directory.txt");
        await File.WriteAllTextAsync(unownedContentPath, "same-name unowned content");
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);
        var absentBinding = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);

        var unavailableStore = new RecordingOwnershipStore
        {
            EmptyLoadStatus = PreparedCodeGraphOwnershipLoadStatus.Unavailable,
        };
        var unavailableCoordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            new TestToolUseAuthorizer(),
            new RecordingWorkspaceOwnershipVerifier(),
            unavailableStore);
        PreparedCodeGraphOwnershipViewResult unavailable = await unavailableCoordinator.GetOwnershipViewAsync(
            absentBinding,
            fixture.Approval);

        var absentStore = new RecordingOwnershipStore();
        var absentCoordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            new TestToolUseAuthorizer(),
            new RecordingWorkspaceOwnershipVerifier(),
            absentStore);
        PreparedCodeGraphOwnershipViewResult verifiedAbsent = await absentCoordinator.GetOwnershipViewAsync(
            absentBinding,
            fixture.Approval);

        var foreignOwner = absentBinding with
        {
            OwnershipId = Guid.NewGuid(),
            Version = 4,
            Phase = PreparedCodeGraphOwnershipPhase.Ready,
            WorkspaceOwnerIdentity = "another-workspace-owner",
            StateEntryObjectIdentity = ObserveDirectoryIdentity(statePath),
        };
        var foreignStore = new RecordingOwnershipStore();
        foreignStore.Seed(foreignOwner);
        var foreignCoordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            new TestToolUseAuthorizer(),
            new RecordingWorkspaceOwnershipVerifier(),
            foreignStore);
        PreparedCodeGraphOwnershipViewResult foreign = await foreignCoordinator.GetOwnershipViewAsync(
            absentBinding,
            fixture.Approval);

        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.Unavailable, unavailable.Status);
        Assert.Null(unavailable.Record);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedUnowned, verifiedAbsent.Status);
        Assert.Null(verifiedAbsent.Record);
        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.Unavailable, foreign.Status);
        Assert.Null(foreign.Record);
        Assert.Equal("same-name unowned content", await File.ReadAllTextAsync(unownedContentPath));
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetOwnershipViewAsync_RejectsPersistedTransitionalAndInconsistentRecords()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string statePath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(statePath);
        string stateContentPath = Path.Combine(statePath, "index.bin");
        await File.WriteAllTextAsync(stateContentPath, "state retained while classification is unavailable");
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);
        PreparedToolDirectoryObjectIdentity entryIdentity = ObserveDirectoryIdentity(statePath);
        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(fixture.PreparedRootPath);

        foreach (PreparedCodeGraphOwnershipPhase phase in new[]
        {
            PreparedCodeGraphOwnershipPhase.Creating,
            PreparedCodeGraphOwnershipPhase.Disposing,
            PreparedCodeGraphOwnershipPhase.Quarantined,
            PreparedCodeGraphOwnershipPhase.Removed,
        })
        {
            var persistedRecord = new PreparedCodeGraphWorkspaceOwnership(
                Guid.NewGuid(),
                Version: 3,
                phase,
                fixture.Approval,
                Path.GetFullPath(fixture.PreparedRootPath),
                rootIdentity,
                WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
                StateEntryObjectIdentity: phase == PreparedCodeGraphOwnershipPhase.Creating
                    ? null
                    : entryIdentity);
            var store = new RecordingOwnershipStore();
            store.Seed(persistedRecord);
            var coordinator = new PreparedCodeGraphStateCoordinator(
                fixture.Authority,
                new TestToolUseAuthorizer(),
                new RecordingWorkspaceOwnershipVerifier(),
                store);

            PreparedCodeGraphOwnershipViewResult view = await coordinator.GetOwnershipViewAsync(
                persistedRecord,
                fixture.Approval);

            Assert.True(
                view.Status == PreparedCodeGraphOwnershipViewStatus.Unavailable,
                $"A loaded {phase} record must not establish a positive ownership view.");
            Assert.Null(view.Record);
        }

        Assert.Equal(
            "state retained while classification is unavailable",
            await File.ReadAllTextAsync(stateContentPath));
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task UseOwnedStateAsync_DoesNotAdoptPreexistingSameNameEntry()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string statePath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(statePath);
        string preexistingContentPath = Path.Combine(statePath, "unowned.txt");
        await File.WriteAllTextAsync(preexistingContentPath, "preexisting same-name entry");
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);

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

        var absentBinding = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);
        var store = new RecordingOwnershipStore();
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            authorization,
            new RecordingWorkspaceOwnershipVerifier(),
            store);
        int callbackCount = 0;

        try
        {
            PreparedToolWorkspaceCheckStatus result = await coordinator.UseOwnedStateAsync(
                lease,
                absentBinding,
                (_, _) =>
                {
                    callbackCount++;
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(PreparedToolWorkspaceCheckStatus.Denied, result);
            Assert.Equal(0, callbackCount);
            Assert.Empty(store.CommittedPhases);
            Assert.Null(store.CurrentRecord);
            Assert.Equal("preexisting same-name entry", await File.ReadAllTextAsync(preexistingContentPath));
            Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
            Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeOwnedStateAsync_LeavesSubstitutedEntryAndSourceUntouched()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string statePath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        string originalStatePath = Path.Combine(fixture.PreparedRootPath, ".codegraph-original");
        string originalStateFile = Path.Combine(statePath, "cache", "index.db");
        Directory.CreateDirectory(Path.GetDirectoryName(originalStateFile)!);
        await File.WriteAllTextAsync(originalStateFile, "owned state fixture");
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);

        var ready = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 2,
            PreparedCodeGraphOwnershipPhase.Ready,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(statePath));
        var store = new RecordingOwnershipStore(() =>
        {
            Directory.Move(statePath, originalStatePath);
            Directory.CreateDirectory(statePath);
            File.WriteAllText(Path.Combine(statePath, "replacement.marker"), "foreign replacement");
        });
        store.Seed(ready);

        var authorization = new TestToolUseAuthorizer();
        authorization.Allow(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            authorization,
            new RecordingWorkspaceOwnershipVerifier(),
            store);

        PreparedToolWorkspaceCheckStatus result = await coordinator.DisposeOwnedStateAsync(
            fixture.Approval,
            ready);

        Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, result);
        Assert.Equal(PreparedCodeGraphOwnershipPhase.Quarantined, store.CurrentRecord?.Phase);
        Assert.True(store.QuarantineAttempted,
            "A substituted entry must leave ownership unavailable after disposition cannot verify the recorded identity.");
        Assert.Equal("owned state fixture", await File.ReadAllTextAsync(Path.Combine(originalStatePath, "cache", "index.db")));
        Assert.Equal("foreign replacement", await File.ReadAllTextAsync(Path.Combine(statePath, "replacement.marker")));
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeOwnedStateAsync_QuarantinesWhenReadinessIsLostDuringRemoval()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string statePath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        string cachePath = Path.Combine(statePath, "cache", "index.db");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllTextAsync(cachePath, "owned state fixture");
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);
        var ready = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 2,
            PreparedCodeGraphOwnershipPhase.Ready,
            fixture.Approval,
            Path.GetFullPath(fixture.PreparedRootPath),
            ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(statePath));
        var store = new RecordingOwnershipStore();
        store.Seed(ready);
        bool readinessLostAfterDeletion = false;
        var ownerVerifier = new RecordingWorkspaceOwnershipVerifier
        {
            DispositionReadiness = () =>
            {
                if (File.Exists(cachePath))
                {
                    return true;
                }

                readinessLostAfterDeletion = true;
                return false;
            },
        };
        var authorization = new TestToolUseAuthorizer();
        authorization.Allow(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            authorization,
            ownerVerifier,
            store);

        PreparedToolWorkspaceCheckStatus result = await coordinator.DisposeOwnedStateAsync(
            fixture.Approval,
            ready);

        Assert.Equal(PreparedToolWorkspaceCheckStatus.RootUnavailable, result);
        Assert.True(readinessLostAfterDeletion,
            "Disposition must recheck workspace readiness after each completed removal before continuing.");
        Assert.Equal(PreparedCodeGraphOwnershipPhase.Quarantined, store.CurrentRecord?.Phase);
        Assert.True(store.QuarantineAttempted,
            "A partially removed state tree must not remain classified as reusable after readiness is lost.");
        Assert.False(File.Exists(cachePath), "The controlled revocation occurs after the owned file is removed.");
        Assert.True(Directory.Exists(statePath), "Cleanup must stop while owned state remains after authority loss.");
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
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

    private sealed class CurrentPreparedSourceApprovals(params PreparedSourceApproval[] approvals)
        : IPreparedSourceCurrentAuthorityVerifier
    {
        private readonly HashSet<PreparedSourceApproval> _approvals = new(approvals);
        private readonly List<PreparedSourceApproval> _verifiedApprovals = [];

        internal IReadOnlyList<PreparedSourceApproval> VerifiedApprovals => _verifiedApprovals;

        public Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _verifiedApprovals.Add(requestedApproval);
            return Task.FromResult(_approvals.Contains(requestedApproval)
                ? new PreparedSourceAuthorityResult(
                    PreparedSourceAuthorityStatus.Current,
                    requestedApproval)
                : new PreparedSourceAuthorityResult(
                    PreparedSourceAuthorityStatus.Revoked,
                    null));
        }
    }

    private sealed class CurrentPreparedToolGrants(
        params (PreparedSourceApproval Approval, PreparedToolPurpose Purpose)[] grants)
        : IPreparedToolUseAuthorizer
    {
        private readonly HashSet<(PreparedSourceApproval Approval, PreparedToolPurpose Purpose)> _grants =
            new(grants);
        private readonly List<(PreparedSourceApproval Approval, PreparedToolPurpose Purpose)> _verifiedGrants = [];

        internal IReadOnlyList<(PreparedSourceApproval Approval, PreparedToolPurpose Purpose)> VerifiedGrants =>
            _verifiedGrants;

        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var grant = (approval, toolPurpose);
            _verifiedGrants.Add(grant);
            return Task.FromResult(new PreparedToolUseAuthorityResult(
                _grants.Contains(grant)
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied));
        }
    }

    private sealed class ExactWorkspaceOwnershipVerifier(
        PreparedCodeGraphWorkspaceOwnership expected) : IPreparedCodeGraphWorkspaceOwnershipVerifier
    {
        public ValueTask<bool> VerifyCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                ownership.Approval == expected.Approval &&
                ownership.PhysicalRootPath == expected.PhysicalRootPath &&
                ownership.RootObjectIdentity == expected.RootObjectIdentity &&
                ownership.WorkspaceOwnerIdentity == expected.WorkspaceOwnerIdentity);

        public ValueTask<bool> VerifyDispositionReadyAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class RecordingOwnershipStore : IPreparedCodeGraphOwnershipStore
    {
        private readonly List<PreparedCodeGraphOwnershipPhase> _committedPhases = [];
        private readonly bool _loseCreatedCommitAcknowledgement;
        private readonly Action? _beforeDispositionCommit;

        internal RecordingOwnershipStore(
            bool loseCreatedCommitAcknowledgement = false,
            Action? beforeDispositionCommit = null)
        {
            _loseCreatedCommitAcknowledgement = loseCreatedCommitAcknowledgement;
            _beforeDispositionCommit = beforeDispositionCommit;
        }

        internal RecordingOwnershipStore(Action beforeDispositionCommit)
            : this(loseCreatedCommitAcknowledgement: false, beforeDispositionCommit)
        {
        }

        internal IReadOnlyList<PreparedCodeGraphOwnershipPhase> CommittedPhases => _committedPhases;

        internal PreparedCodeGraphWorkspaceOwnership? CurrentRecord { get; private set; }

        internal int TerminalReadbackCount { get; private set; }

        internal PreparedCodeGraphOwnershipLoadStatus EmptyLoadStatus { get; init; } =
            PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent;

        internal bool LoseBeginCreateAcknowledgement { get; init; }

        internal PreparedCodeGraphWorkspaceOwnership? CompetingCreateRecord { get; init; }

        internal int QuarantineReadbackCount { get; private set; }

        internal void Seed(PreparedCodeGraphWorkspaceOwnership ready)
        {
            CurrentRecord = ready;
            _committedPhases.Add(ready.Phase);
        }

        public Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
            PreparedCodeGraphWorkspaceOwnership expectedBinding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedCodeGraphWorkspaceOwnership? record = CurrentRecord;
            if (expectedBinding.Phase == PreparedCodeGraphOwnershipPhase.Quarantined &&
                record?.Phase == PreparedCodeGraphOwnershipPhase.Quarantined &&
                record.OwnershipId == expectedBinding.OwnershipId &&
                record.Version == expectedBinding.Version)
            {
                QuarantineReadbackCount++;
            }

            if (expectedBinding.Phase == PreparedCodeGraphOwnershipPhase.Removed &&
                record?.Phase == PreparedCodeGraphOwnershipPhase.Removed &&
                record.OwnershipId == expectedBinding.OwnershipId &&
                record.Version == expectedBinding.Version)
            {
                TerminalReadbackCount++;
            }

            return Task.FromResult(record is not null
                ? new PreparedCodeGraphOwnershipLoadResult(PreparedCodeGraphOwnershipLoadStatus.Found, record)
                : new PreparedCodeGraphOwnershipLoadResult(EmptyLoadStatus, null));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
            PreparedCodeGraphWorkspaceOwnership absent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string statePath = Path.Combine(absent.PhysicalRootPath, ".codegraph");
            Assert.False(Directory.Exists(statePath), "Creating must be durably committed before the state entry appears.");
            if (CompetingCreateRecord is { } competing)
            {
                CurrentRecord = competing;
                _committedPhases.Add(competing.Phase);
                return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                    PreparedCodeGraphOwnershipTransitionStatus.Conflict,
                    competing));
            }

            CurrentRecord = absent with { Version = absent.Version + 1, Phase = PreparedCodeGraphOwnershipPhase.Creating };
            _committedPhases.Add(CurrentRecord.Phase);
            if (LoseBeginCreateAcknowledgement)
            {
                return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                    PreparedCodeGraphOwnershipTransitionStatus.Unavailable,
                    null));
            }

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
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AssertRecordMatches(ready, PreparedCodeGraphOwnershipPhase.Ready);
            Assert.True(Directory.Exists(Path.Combine(ready.PhysicalRootPath, ".codegraph")),
                "Disposing must commit before any physical removal.");
            CurrentRecord = ready with
            {
                Version = ready.Version + 1,
                Phase = PreparedCodeGraphOwnershipPhase.Disposing,
            };
            _committedPhases.Add(CurrentRecord.Phase);
            _beforeDispositionCommit?.Invoke();
            return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                PreparedCodeGraphOwnershipTransitionStatus.Committed,
                CurrentRecord));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
            PreparedCodeGraphWorkspaceOwnership disposing,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AssertRecordMatches(disposing, PreparedCodeGraphOwnershipPhase.Disposing);
            Assert.False(Directory.Exists(Path.Combine(disposing.PhysicalRootPath, ".codegraph")),
                "Removed may commit only after the owned entry is absent.");
            CurrentRecord = disposing with
            {
                Version = disposing.Version + 1,
                Phase = PreparedCodeGraphOwnershipPhase.Removed,
                StateEntryObjectIdentity = null,
            };
            _committedPhases.Add(CurrentRecord.Phase);
            return Task.FromResult(new PreparedCodeGraphOwnershipTransitionResult(
                PreparedCodeGraphOwnershipTransitionStatus.Committed,
                CurrentRecord));
        }

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

        private void AssertRecordMatches(
            PreparedCodeGraphWorkspaceOwnership expected,
            PreparedCodeGraphOwnershipPhase phase)
        {
            Assert.NotNull(CurrentRecord);
            Assert.Equal(expected.OwnershipId, CurrentRecord!.OwnershipId);
            Assert.Equal(expected.Version, CurrentRecord.Version);
            Assert.Equal(phase, CurrentRecord.Phase);
        }
    }

    private sealed class RecordingWorkspaceOwnershipVerifier : IPreparedCodeGraphWorkspaceOwnershipVerifier
    {
        internal int DispositionReadyChecks { get; private set; }

        internal Func<bool>? DispositionReadiness { get; init; }

        public ValueTask<bool> VerifyCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);

        public ValueTask<bool> VerifyDispositionReadyAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default)
        {
            DispositionReadyChecks++;
            return ValueTask.FromResult(DispositionReadiness?.Invoke() ?? true);
        }
    }

    private sealed class TestToolUseAuthorizer : IPreparedToolUseAuthorizer
    {
        private PreparedSourceApproval? _approval;
        private PreparedToolPurpose _purpose;

        internal PreparedSourceApproval? LastApproval { get; private set; }

        internal PreparedToolPurpose? LastPurpose { get; private set; }

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
            LastApproval = approval;
            LastPurpose = toolPurpose;
            PreparedToolUseAuthorityStatus status = approval == _approval && toolPurpose == _purpose
                ? PreparedToolUseAuthorityStatus.Current
                : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }
}
