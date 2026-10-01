using Microsoft.Extensions.Configuration;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedWorkspaceUseRegistryTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AdmitAsync_CurrentAbsentBindingWithProtectedHostHold_OpensRegistry()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);

        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 0,
            Phase: PreparedCodeGraphOwnershipPhase.Absent,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);
        var sourceAuthority = new CurrentSourceAuthority(fixture.Approval);
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.CodeGraphPreparation);
        var ownershipStore = new VerifiedAbsentOwnershipStore(binding);
        var hostProtection = new ProtectedHostVerifier(binding);
        var previousGeneration = new PreviousGenerationSettledVerifier();
        var resourceProbe = new ControlledResourceProbe();

        var registry = new PreparedWorkspaceUseRegistry(
            sourceAuthority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            hostProtection,
            previousGeneration,
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));

        try
        {
            PreparedWorkspaceBorrowResult beforeAdmission = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                resourceProbe);
            Assert.True(beforeAdmission.Borrow is null,
                "A newly constructed registry must not grant a borrow before admission succeeds.");

            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation);

            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);
            Assert.True(sourceAuthority.VerificationCount > 0,
                "Admission must verify the exact current source approval.");
            Assert.True(toolAuthorization.VerificationCount > 0,
                "Admission must verify the selected CodeGraph purpose.");
            Assert.Equal(binding, ownershipStore.LastLoadedBinding);
            Assert.True(hostProtection.VerifyAndHoldCount > 0,
                "Admission must obtain the protected host hold, not rely on observation alone.");
            Assert.Equal(binding, hostProtection.LastBinding);
            Assert.True(hostProtection.Hold.CheckCurrentCount > 0,
                "Admission must check the protected hold before opening the registry.");
        }
        finally
        {
            await registry.DisposeAsync();
        }

        Assert.True(hostProtection.Hold.IsDisposed,
            "The registry owns the protected root, metadata, and mutex hold after admission.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AdmitAsync_WhenPartialHostHoldCleanupFails_RemainsUnavailableAndReleasesGate()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 0,
            Phase: PreparedCodeGraphOwnershipPhase.Absent,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: null);
        var sourceAuthority = new CurrentSourceAuthority(fixture.Approval);
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.CodeGraphPreparation);
        var ownershipStore = new VerifiedAbsentOwnershipStore(binding);
        var failingHold = new ProtectedHostHold(
            binding,
            currentStatus: PreparedManagedWorkspaceHostStatus.Unavailable,
            throwOnDispose: true);
        var hostProtection = new ProtectedHostVerifier(binding, failingHold);
        var registry = new PreparedWorkspaceUseRegistry(
            sourceAuthority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            hostProtection,
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));

        PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
            binding,
            PreparedToolPurpose.CodeGraphPreparation);

        Assert.Equal(PreparedWorkspaceAdmissionStatus.Unavailable, admission.Status);
        Assert.True(failingHold.DisposeAttemptCount == 1,
            "A failed currentness check must attempt to release the partial host hold.");

        PreparedWorkspaceAdmissionResult retry = await registry.AdmitAsync(
            binding,
            PreparedToolPurpose.CodeGraphPreparation);
        Assert.Equal(PreparedWorkspaceAdmissionStatus.Unavailable, retry.Status);
        Assert.True(hostProtection.VerifyAndHoldCount == 1,
            "Uncertain partial-hold cleanup must not acquire a replacement hold or reopen admission.");

        Task disposal = registry.DisposeAsync().AsTask();
        Task completed = await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.True(ReferenceEquals(disposal, completed),
            "Failed partial-hold cleanup must release the admission gate so registry disposal can finish.");
        await disposal;
    }

    private static RetainedSourceCapturePolicy CreatePolicy(
        string sandboxPath,
        string preparedRootPath,
        string sourceStorageRoot)
    {
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

        return RetainedSourceCapturePolicy.Create(configuration, sandboxPath, preparedRootPath);
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

    private sealed class CurrentSourceAuthority(PreparedSourceApproval expected)
        : IPreparedSourceCurrentAuthorityVerifier
    {
        internal int VerificationCount { get; private set; }

        public Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerificationCount++;
            return Task.FromResult(requestedApproval == expected
                ? new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current, expected)
                : new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null));
        }
    }

    private sealed class CurrentToolAuthorization(
        PreparedSourceApproval expectedApproval,
        PreparedToolPurpose expectedPurpose) : IPreparedToolUseAuthorizer
    {
        internal int VerificationCount { get; private set; }

        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerificationCount++;
            return Task.FromResult(new PreparedToolUseAuthorityResult(
                approval == expectedApproval && toolPurpose == expectedPurpose
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied));
        }
    }

    private sealed class VerifiedAbsentOwnershipStore(PreparedCodeGraphWorkspaceOwnership expectedBinding)
        : IPreparedCodeGraphOwnershipStore
    {
        internal PreparedCodeGraphWorkspaceOwnership? LastLoadedBinding { get; private set; }

        public Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
            PreparedCodeGraphWorkspaceOwnership expected,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastLoadedBinding = expected;
            return Task.FromResult(expected == expectedBinding
                ? new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent,
                    null)
                : new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.Unavailable,
                    null));
        }

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
            PreparedCodeGraphWorkspaceOwnership absent,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Admission must not mutate an absent ownership record.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitCreatedAsync(
            PreparedCodeGraphWorkspaceOwnership creating,
            PreparedToolDirectoryObjectIdentity createdEntry,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Admission must not commit a created entry.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginDispositionAsync(
            PreparedCodeGraphWorkspaceOwnership ready,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Admission must not begin disposition.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
            PreparedCodeGraphWorkspaceOwnership disposing,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Admission must not commit removal.");

        public Task<PreparedCodeGraphOwnershipTransitionResult> QuarantineAsync(
            PreparedCodeGraphWorkspaceOwnership current,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Admission must not quarantine an unchanged absent record.");
    }

    private sealed class ProtectedHostVerifier(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        ProtectedHostHold? hold = null)
        : IPreparedManagedWorkspaceHostVerifier
    {
        internal int VerifyAndHoldCount { get; private set; }

        internal PreparedCodeGraphWorkspaceOwnership? LastBinding { get; private set; }

        internal ProtectedHostHold Hold { get; } = hold ?? new(expectedBinding);

        public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
            PreparedSourceGrantRequest applicationHeldSourceGrant,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedManagedWorkspaceHostObservation(
                PreparedManagedWorkspaceHostStatus.Observed,
                expectedBinding.RootObjectIdentity,
                expectedBinding.WorkspaceOwnerIdentity));
        }

        public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifyAndHoldCount++;
            LastBinding = binding;
            return Task.FromResult(binding == expectedBinding
                ? new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Protected, Hold)
                : new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Denied, null));
        }
    }

    private sealed class ProtectedHostHold(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        PreparedManagedWorkspaceHostStatus currentStatus = PreparedManagedWorkspaceHostStatus.Protected,
        bool throwOnDispose = false)
        : IPreparedManagedWorkspaceHostHold
    {
        internal int CheckCurrentCount { get; private set; }

        internal int DisposeAttemptCount { get; private set; }

        internal bool IsDisposed { get; private set; }

        public ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckCurrentCount++;
            return ValueTask.FromResult(!IsDisposed && binding == expectedBinding
                ? currentStatus
                : PreparedManagedWorkspaceHostStatus.Unavailable);
        }

        public ValueTask DisposeAsync()
        {
            DisposeAttemptCount++;
            if (throwOnDispose)
            {
                throw new InvalidOperationException("The controlled host hold cannot confirm cleanup.");
            }

            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PreviousGenerationSettledVerifier : IPreparedWorkspacePreviousGenerationVerifier
    {
        internal int VerificationCount { get; private set; }

        public ValueTask<PreparedWorkspaceSettlementStatus> VerifySettledAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerificationCount++;
            return ValueTask.FromResult(PreparedWorkspaceSettlementStatus.Settled);
        }
    }

    private sealed class ControlledResourceProbe : IPreparedWorkspaceResourceSettlementProbe
    {
        public ValueTask<PreparedWorkspaceSettlementStatus> InspectOwnedResourcesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(PreparedWorkspaceSettlementStatus.Active);
        }
    }
}
