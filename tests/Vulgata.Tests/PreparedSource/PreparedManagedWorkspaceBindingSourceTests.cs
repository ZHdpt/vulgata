using Microsoft.Extensions.Configuration;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedManagedWorkspaceBindingSourceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_InitializedLedgerAndExactCurrentAbsentWorkspace_ReturnsBoundAfterHeldAdmission()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        PreparedCodeGraphStoreProvisionStatus provision =
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy);
        Assert.Equal(PreparedCodeGraphStoreProvisionStatus.Provisioned, provision);

        string preparedRoot = Path.GetFullPath(fixture.PreparedRootPath);
        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(preparedRoot);
        string derivedOwnerIdentity = $"fixture-owner-{fixture.SandboxId:N}";
        var grant = new PreparedSourceGrantRequest(
            fixture.Approval,
            preparedRoot,
            new PreparedSourceGrantLimits(
                MaximumOperations: 4,
                MaximumMetadataEntries: 16,
                MaximumExaminedBytes: 4_096,
                MaximumReturnedBytes: 4_096,
                Deadline: TimeSpan.FromSeconds(5)));
        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var hostProtection = new ControlledHostVerifier(
            fixture.Approval,
            preparedRoot,
            sourceStoragePolicy.RootPath,
            rootIdentity,
            derivedOwnerIdentity,
            ownershipStore);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            hostProtection,
            new SettledPreviousGenerationVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        var bindingSource = new PreparedManagedWorkspaceBindingSource(
            fixture.Authority,
            toolAuthorization,
            sourceStoragePolicy,
            ownershipStore,
            hostProtection,
            registry);

        PreparedManagedWorkspaceBindingResult result;
        try
        {
            result = await bindingSource.BindAsync(grant, PreparedToolPurpose.JdtNavigation)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedManagedWorkspaceBindingStatus.Bound, result.Status);
            PreparedCodeGraphWorkspaceOwnership binding = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
                result.Binding);
            Assert.Equal(PreparedCodeGraphOwnershipPhase.Absent, binding.Phase);
            Assert.Equal(0, binding.Version);
            Assert.Equal(fixture.Approval, binding.Approval);
            Assert.Equal(preparedRoot, binding.PhysicalRootPath);
            Assert.Equal(rootIdentity, binding.RootObjectIdentity);
            Assert.Equal(derivedOwnerIdentity, binding.WorkspaceOwnerIdentity);
            Assert.Null(binding.StateEntryObjectIdentity);

            Assert.Equal(grant, hostProtection.LastObservedGrant);
            Assert.True(hostProtection.ObservationCount > 0);
            Assert.True(hostProtection.VerifyAndHoldCount > 0);
            Assert.True(hostProtection.Hold.CheckCurrentCount > 0);

            PreparedCodeGraphOwnershipLoadResult current = await ownershipStore.LoadAsync(binding);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, current.Status);
            Assert.Null(current.Record);
            Assert.False(Directory.Exists(Path.Combine(preparedRoot, ".codegraph")));
        }
        finally
        {
            await registry.DisposeAsync();
        }

        Assert.True(hostProtection.Hold.IsDisposed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_WhenFinalHostObservationFailsAfterAdmission_ClosesRegistryToFurtherBorrowing()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        PreparedCodeGraphStoreProvisionStatus provision =
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy);
        Assert.Equal(PreparedCodeGraphStoreProvisionStatus.Provisioned, provision);

        string preparedRoot = Path.GetFullPath(fixture.PreparedRootPath);
        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(preparedRoot);
        string derivedOwnerIdentity = $"fixture-owner-{fixture.SandboxId:N}";
        var grant = new PreparedSourceGrantRequest(
            fixture.Approval,
            preparedRoot,
            new PreparedSourceGrantLimits(
                MaximumOperations: 4,
                MaximumMetadataEntries: 16,
                MaximumExaminedBytes: 4_096,
                MaximumReturnedBytes: 4_096,
                Deadline: TimeSpan.FromSeconds(5)));
        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var hostProtection = new ControlledHostVerifier(
            fixture.Approval,
            preparedRoot,
            sourceStoragePolicy.RootPath,
            rootIdentity,
            derivedOwnerIdentity,
            ownershipStore,
            unavailableObservationCall: 2);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            hostProtection,
            new SettledPreviousGenerationVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        var bindingSource = new PreparedManagedWorkspaceBindingSource(
            fixture.Authority,
            toolAuthorization,
            sourceStoragePolicy,
            ownershipStore,
            hostProtection,
            registry);

        PreparedWorkspaceBorrow? unexpectedBorrow = null;
        try
        {
            PreparedManagedWorkspaceBindingResult bindingResult = await bindingSource.BindAsync(
                    grant,
                    PreparedToolPurpose.JdtNavigation)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(PreparedManagedWorkspaceBindingStatus.Unavailable, bindingResult.Status);
            Assert.Null(bindingResult.Binding);
            Assert.Equal(2, hostProtection.ObservationCount);

            PreparedCodeGraphWorkspaceOwnership admittedBinding = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
                hostProtection.LastVerifiedBinding);
            PreparedCodeGraphOwnershipLoadResult current = await ownershipStore.LoadAsync(admittedBinding);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent, current.Status);
            Assert.Null(current.Record);

            PreparedWorkspaceBorrowResult borrow = await registry.BeginBorrowAsync(
                admittedBinding,
                PreparedToolPurpose.JdtNavigation,
                new SettledResourceProbe());
            unexpectedBorrow = borrow.Borrow;
            Assert.Equal(PreparedWorkspaceSettlementStatus.Unknown, borrow.Status);
            Assert.Null(borrow.Borrow);
        }
        finally
        {
            try
            {
                if (unexpectedBorrow is not null)
                {
                    await unexpectedBorrow.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            finally
            {
                await registry.DisposeAsync();
            }
        }

        Assert.True(hostProtection.Hold.IsDisposed);
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
                ["SourceStorage:MaximumEntries"] = "16",
                ["SourceStorage:MaximumBlobBytes"] = "4096",
                ["SourceStorage:MaximumAggregateBlobBytes"] = "8192",
                ["SourceStorage:CaptureDeadlineSeconds"] = "5",
                ["SourceStorage:OperationStaleAfterMinutes"] = "5",
                ["SourceStorage:VerificationBufferBytes"] = "4096",
                ["SourceStorage:Read:DeadlineSeconds"] = "5",
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

    private sealed class CurrentToolAuthorization(
        PreparedSourceApproval approval,
        PreparedToolPurpose purpose) : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            PreparedToolPurpose requestedPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedToolUseAuthorityResult(
                requestedApproval == approval && requestedPurpose == purpose
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied));
        }
    }

    private sealed class SettledPreviousGenerationVerifier : IPreparedWorkspacePreviousGenerationVerifier
    {
        public ValueTask<PreparedWorkspaceSettlementStatus> VerifySettledAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(PreparedWorkspaceSettlementStatus.Settled);
        }
    }

    private sealed class SettledResourceProbe : IPreparedWorkspaceResourceSettlementProbe
    {
        public ValueTask<PreparedWorkspaceSettlementStatus> InspectOwnedResourcesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(PreparedWorkspaceSettlementStatus.Settled);
        }
    }

    private sealed class ControlledHostVerifier(
        PreparedSourceApproval approval,
        string preparedRoot,
        string sourceStorageRoot,
        PreparedToolDirectoryObjectIdentity rootIdentity,
        string derivedOwnerIdentity,
        IPreparedCodeGraphOwnershipStore ownershipStore,
        int? unavailableObservationCall = null) : IPreparedManagedWorkspaceHostVerifier
    {
        private bool _holdIssued;

        internal int ObservationCount { get; private set; }

        internal int VerifyAndHoldCount { get; private set; }

        internal PreparedSourceGrantRequest? LastObservedGrant { get; private set; }

        internal PreparedCodeGraphWorkspaceOwnership? LastVerifiedBinding { get; private set; }

        internal ControlledHostHold Hold { get; } = new(ownershipStore: ownershipStore);

        public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
            PreparedSourceGrantRequest applicationHeldSourceGrant,
            RetainedSourceCapturePolicy policy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObservationCount++;
            LastObservedGrant = applicationHeldSourceGrant;
            bool exactGrant = applicationHeldSourceGrant.Approval == approval &&
                string.Equals(applicationHeldSourceGrant.PhysicalRootPath, preparedRoot,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(policy.RootPath, sourceStorageRoot, StringComparison.OrdinalIgnoreCase);
            bool unavailable = !exactGrant || ObservationCount == unavailableObservationCall;
            return Task.FromResult(!unavailable
                ? new PreparedManagedWorkspaceHostObservation(
                    PreparedManagedWorkspaceHostStatus.Observed,
                    rootIdentity,
                    derivedOwnerIdentity)
                : new PreparedManagedWorkspaceHostObservation(
                    PreparedManagedWorkspaceHostStatus.Unavailable,
                    null,
                    null));
        }

        public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            RetainedSourceCapturePolicy policy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifyAndHoldCount++;
            bool exactBinding = binding.Approval == approval &&
                string.Equals(binding.PhysicalRootPath, preparedRoot, StringComparison.OrdinalIgnoreCase) &&
                binding.RootObjectIdentity == rootIdentity &&
                string.Equals(binding.WorkspaceOwnerIdentity, derivedOwnerIdentity, StringComparison.Ordinal) &&
                string.Equals(policy.RootPath, sourceStorageRoot, StringComparison.OrdinalIgnoreCase);
            if (!exactBinding || _holdIssued)
            {
                return Task.FromResult(new PreparedManagedWorkspaceHostHoldResult(
                    PreparedManagedWorkspaceHostStatus.Unavailable,
                    null));
            }

            _holdIssued = true;
            LastVerifiedBinding = binding;
            Hold.Bind(binding);
            return Task.FromResult(new PreparedManagedWorkspaceHostHoldResult(
                PreparedManagedWorkspaceHostStatus.Protected,
                Hold));
        }
    }

    private sealed class ControlledHostHold(IPreparedCodeGraphOwnershipStore ownershipStore)
        : IPreparedManagedWorkspaceHostHold
    {
        private PreparedCodeGraphWorkspaceOwnership? _binding;
        private bool _disposed;

        internal int CheckCurrentCount { get; private set; }

        internal bool IsDisposed => _disposed;

        internal void Bind(PreparedCodeGraphWorkspaceOwnership binding) => _binding = binding;

        public async ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckCurrentCount++;
            if (_disposed || _binding is null || !SameImmutableKey(_binding, binding) ||
                binding.Phase != PreparedCodeGraphOwnershipPhase.Absent || binding.Version != 0 ||
                binding.StateEntryObjectIdentity is not null)
            {
                return PreparedManagedWorkspaceHostStatus.Unavailable;
            }

            PreparedCodeGraphOwnershipLoadResult current = await ownershipStore.LoadAsync(
                binding,
                cancellationToken);
            return current.Status == PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent &&
                current.Record is null
                    ? PreparedManagedWorkspaceHostStatus.Protected
                    : PreparedManagedWorkspaceHostStatus.Unavailable;
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        private static bool SameImmutableKey(
            PreparedCodeGraphWorkspaceOwnership left,
            PreparedCodeGraphWorkspaceOwnership right) =>
            left.OwnershipId == right.OwnershipId &&
            left.Approval == right.Approval &&
            string.Equals(left.PhysicalRootPath, right.PhysicalRootPath, StringComparison.OrdinalIgnoreCase) &&
            left.RootObjectIdentity == right.RootObjectIdentity &&
            string.Equals(left.WorkspaceOwnerIdentity, right.WorkspaceOwnerIdentity, StringComparison.Ordinal);
    }
}
