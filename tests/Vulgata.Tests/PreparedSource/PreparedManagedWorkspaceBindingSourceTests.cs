using System.Text;
using System.Text.Json;
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

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BindAsync_FoundReadyRecordRequiresExactHostKeyAndPriorGenerationSettlement(
        bool previousGenerationSettled)
    {
        PreparedWorkspaceSettlementStatus previousGenerationStatus = previousGenerationSettled
            ? PreparedWorkspaceSettlementStatus.Settled
            : PreparedWorkspaceSettlementStatus.Unknown;
        PreparedManagedWorkspaceBindingStatus expectedStatus = previousGenerationSettled
            ? PreparedManagedWorkspaceBindingStatus.Bound
            : PreparedManagedWorkspaceBindingStatus.Unavailable;
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));

        string preparedRoot = Path.GetFullPath(fixture.PreparedRootPath);
        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(preparedRoot);
        string derivedOwnerIdentity = $"fixture-owner-{fixture.SandboxId:N}";
        var absent = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            preparedRoot,
            rootIdentity,
            derivedOwnerIdentity,
            StateEntryObjectIdentity: null);
        PreparedCodeGraphWorkspaceOwnership ready = await CreateReadyWorkspaceAsync(
            fixture,
            absent,
            ownershipStore,
            sourceStoragePolicy);
        Assert.Equal(PreparedCodeGraphOwnershipPhase.Ready, ready.Phase);
        Assert.Equal(2, ready.Version);
        Assert.NotNull(ready.StateEntryObjectIdentity);

        string originalCodeGraph = Path.Combine(preparedRoot, ".codegraph");
        Assert.Equal(ready.StateEntryObjectIdentity, ObserveDirectoryIdentity(originalCodeGraph));

        var grant = new PreparedSourceGrantRequest(
            fixture.Approval,
            preparedRoot,
            new PreparedSourceGrantLimits(
                MaximumOperations: 4,
                MaximumMetadataEntries: 16,
                MaximumExaminedBytes: 4_096,
                MaximumReturnedBytes: 4_096,
                Deadline: TimeSpan.FromSeconds(5)));
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
        var previousGeneration = new ControlledPreviousGenerationVerifier(previousGenerationStatus);
        var registry = new PreparedWorkspaceUseRegistry(
            fixture.Authority,
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            hostProtection,
            previousGeneration,
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
        }
        finally
        {
            await registry.DisposeAsync();
        }

        Assert.Equal(expectedStatus, result.Status);
        if (expectedStatus == PreparedManagedWorkspaceBindingStatus.Bound)
        {
            PreparedCodeGraphWorkspaceOwnership bound = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
                result.Binding);
            Assert.Equal(ready, bound);
            Assert.Equal(ready.OwnershipId, bound.OwnershipId);
            Assert.Equal(ready.Version, bound.Version);
            Assert.Equal(ready, Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
                (await ownershipStore.LoadAsync(ready)).Record));
            Assert.Equal(1, hostProtection.VerifyAndHoldCount);
            Assert.Equal(2, hostProtection.Hold.CheckCurrentCount);
            Assert.True(hostProtection.Hold.IsDisposed);
        }
        else
        {
            Assert.Null(result.Binding);
            Assert.Equal(0, hostProtection.VerifyAndHoldCount);
            Assert.False(hostProtection.Hold.IsDisposed);
        }

        Assert.Equal(1, previousGeneration.VerificationCount);
        Assert.Equal(ready, previousGeneration.LastBinding);

        Assert.Equal(expectedStatus == PreparedManagedWorkspaceBindingStatus.Bound ? 2 : 1,
            hostProtection.ObservationCount);
        Assert.Equal(ready.StateEntryObjectIdentity, ObserveDirectoryIdentity(originalCodeGraph));
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("source")]
    [InlineData("tool-purpose")]
    public async Task BindAsync_WhenSourceOrToolPurposeIsDenied_ReturnsDeniedWithoutBinding(
        string deniedAuthority)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));

        string preparedRoot = Path.GetFullPath(fixture.PreparedRootPath);
        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(preparedRoot);
        string ownerIdentity = $"fixture-owner-{fixture.SandboxId:N}";
        PreparedToolPurpose authorizedPurpose = deniedAuthority == "tool-purpose"
            ? PreparedToolPurpose.CodeGraphPreparation
            : PreparedToolPurpose.JdtNavigation;
        if (deniedAuthority == "source")
        {
            fixture.Authority.Revoke();
        }

        var toolAuthorization = new CurrentToolAuthorization(fixture.Approval, authorizedPurpose);
        var hostProtection = new ControlledHostVerifier(
            fixture.Approval,
            preparedRoot,
            sourceStoragePolicy.RootPath,
            rootIdentity,
            ownerIdentity,
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
            result = await bindingSource.BindAsync(
                    new PreparedSourceGrantRequest(
                        fixture.Approval,
                        preparedRoot,
                        new PreparedSourceGrantLimits(
                            MaximumOperations: 4,
                            MaximumMetadataEntries: 16,
                            MaximumExaminedBytes: 4_096,
                            MaximumReturnedBytes: 4_096,
                            Deadline: TimeSpan.FromSeconds(5))),
                    PreparedToolPurpose.JdtNavigation)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await registry.DisposeAsync();
        }

        Assert.Equal(PreparedManagedWorkspaceBindingStatus.Denied, result.Status);
        Assert.Null(result.Binding);
        Assert.Null(hostProtection.LastVerifiedBinding);
        Assert.False(Directory.Exists(Path.Combine(preparedRoot, ".codegraph")));
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_WhenObservedRootDoesNotMatchGrantedRoot_ReturnsUnavailableWithoutBinding()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));

        string preparedRoot = Path.GetFullPath(fixture.PreparedRootPath);
        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(preparedRoot);
        PreparedToolDirectoryObjectIdentity otherRootIdentity = ObserveDirectoryIdentity(sourceStorageRoot);
        Assert.True(rootIdentity != otherRootIdentity,
            "The controlled observed root must be a different existing directory object.");
        string ownerIdentity = $"fixture-owner-{fixture.SandboxId:N}";
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var hostProtection = new ControlledHostVerifier(
            fixture.Approval,
            preparedRoot,
            sourceStoragePolicy.RootPath,
            rootIdentity,
            ownerIdentity,
            ownershipStore,
            observedRootIdentity: otherRootIdentity);
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
            result = await bindingSource.BindAsync(
                    new PreparedSourceGrantRequest(
                        fixture.Approval,
                        preparedRoot,
                        new PreparedSourceGrantLimits(
                            MaximumOperations: 4,
                            MaximumMetadataEntries: 16,
                            MaximumExaminedBytes: 4_096,
                            MaximumReturnedBytes: 4_096,
                            Deadline: TimeSpan.FromSeconds(5))),
                    PreparedToolPurpose.JdtNavigation)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await registry.DisposeAsync();
        }

        Assert.Equal(PreparedManagedWorkspaceBindingStatus.Unavailable, result.Status);
        Assert.Null(result.Binding);
        Assert.Null(hostProtection.LastVerifiedBinding);
        Assert.False(Directory.Exists(Path.Combine(preparedRoot, ".codegraph")));
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_WhenFoundRecordUsesDifferentDerivedOwner_ReturnsUnavailableWithoutBinding()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));

        string preparedRoot = Path.GetFullPath(fixture.PreparedRootPath);
        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(preparedRoot);
        string ownerIdentity = $"fixture-owner-{fixture.SandboxId:N}";
        var absent = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            preparedRoot,
            rootIdentity,
            ownerIdentity,
            StateEntryObjectIdentity: null);
        PreparedCodeGraphWorkspaceOwnership ready = await CreateReadyWorkspaceAsync(
            fixture,
            absent,
            ownershipStore,
            sourceStoragePolicy);

        var grant = new PreparedSourceGrantRequest(
            fixture.Approval,
            preparedRoot,
            new PreparedSourceGrantLimits(
                MaximumOperations: 4,
                MaximumMetadataEntries: 16,
                MaximumExaminedBytes: 4_096,
                MaximumReturnedBytes: 4_096,
                Deadline: TimeSpan.FromSeconds(5)));
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var hostProtection = new ControlledHostVerifier(
            fixture.Approval,
            preparedRoot,
            sourceStoragePolicy.RootPath,
            rootIdentity,
            ownerIdentity,
            ownershipStore,
            observedWorkspaceOwnerIdentity: "foreign-owner");
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
        }
        finally
        {
            await registry.DisposeAsync();
        }

        Assert.Equal(PreparedManagedWorkspaceBindingStatus.Unavailable, result.Status);
        Assert.Null(result.Binding);
        Assert.Null(hostProtection.LastVerifiedBinding);
        Assert.Equal(ready.StateEntryObjectIdentity, ObserveDirectoryIdentity(
            Path.Combine(preparedRoot, ".codegraph")));
        Assert.Equal(ready, Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
            (await ownershipStore.LoadAsync(ready)).Record));
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindAsync_WhenLedgerContainsDuplicateReadyRecords_ReturnsUnavailableBeforeHostAdmission()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);
        string originalSource = await File.ReadAllTextAsync(fixture.UnfamiliarTextPath);
        string originalSibling = await File.ReadAllTextAsync(fixture.SentinelPath);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        Assert.Equal(
            PreparedCodeGraphStoreProvisionStatus.Provisioned,
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(sourceStoragePolicy));
        var ownershipStore = new FilePreparedCodeGraphOwnershipStore(sourceStoragePolicy);

        string preparedRoot = Path.GetFullPath(fixture.PreparedRootPath);
        PreparedToolDirectoryObjectIdentity rootIdentity = ObserveDirectoryIdentity(preparedRoot);
        string ownerIdentity = $"fixture-owner-{fixture.SandboxId:N}";
        var absent = new PreparedCodeGraphWorkspaceOwnership(
            Guid.NewGuid(),
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            fixture.Approval,
            preparedRoot,
            rootIdentity,
            ownerIdentity,
            StateEntryObjectIdentity: null);
        PreparedCodeGraphWorkspaceOwnership ready = await CreateReadyWorkspaceAsync(
            fixture,
            absent,
            ownershipStore,
            sourceStoragePolicy);
        Assert.Equal(PreparedCodeGraphOwnershipPhase.Ready, ready.Phase);

        string ledgerPath = Path.Combine(
            sourceStoragePolicy.RootPath,
            ".prepared-codegraph-ownership",
            "ledger.json");
        using JsonDocument validLedger = JsonDocument.Parse(await File.ReadAllTextAsync(ledgerPath));
        JsonElement records = validLedger.RootElement.GetProperty("records");
        Assert.Equal(1, records.GetArrayLength());
        string actualReadyRecord = records[0].GetRawText();
        string ambiguousLedger =
            "{\"format\":\"vulgata-codegraph-ownership-v1\",\"records\":[" +
            actualReadyRecord + "," + actualReadyRecord + "]}";
        await File.WriteAllTextAsync(ledgerPath, ambiguousLedger, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var grant = new PreparedSourceGrantRequest(
            fixture.Approval,
            preparedRoot,
            new PreparedSourceGrantLimits(
                MaximumOperations: 4,
                MaximumMetadataEntries: 16,
                MaximumExaminedBytes: 4_096,
                MaximumReturnedBytes: 4_096,
                Deadline: TimeSpan.FromSeconds(5)));
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.JdtNavigation);
        var hostProtection = new ControlledHostVerifier(
            fixture.Approval,
            preparedRoot,
            sourceStoragePolicy.RootPath,
            rootIdentity,
            ownerIdentity,
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
        }
        finally
        {
            await registry.DisposeAsync();
        }

        Assert.Equal(PreparedManagedWorkspaceBindingStatus.Unavailable, result.Status);
        Assert.Null(result.Binding);
        Assert.Equal(grant, hostProtection.LastObservedGrant);
        Assert.Null(hostProtection.LastVerifiedBinding);
        Assert.Equal(0, hostProtection.VerifyAndHoldCount);
        Assert.Equal(ready.StateEntryObjectIdentity, ObserveDirectoryIdentity(
            Path.Combine(preparedRoot, ".codegraph")));
        Assert.Equal(originalSource, await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllTextAsync(fixture.SentinelPath));
    }

    private static async Task<PreparedCodeGraphWorkspaceOwnership> CreateReadyWorkspaceAsync(
        PreparedSourceFixture fixture,
        PreparedCodeGraphWorkspaceOwnership absent,
        FilePreparedCodeGraphOwnershipStore ownershipStore,
        RetainedSourceCapturePolicy sourceStoragePolicy)
    {
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.CodeGraphPreparation);
        var issuer = new PreparedToolWorkspaceIssuer(fixture.Authority, toolAuthorization);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                fixture.Approval,
                absent.PhysicalRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(5)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);
        var coordinator = new PreparedCodeGraphStateCoordinator(
            fixture.Authority,
            toolAuthorization,
            new ExactWorkspaceOwnershipVerifier(absent),
            ownershipStore);

        try
        {
            PreparedToolWorkspaceCheckStatus use = await coordinator.UseOwnedStateAsync(
                lease,
                absent,
                (_, _) => ValueTask.CompletedTask);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, use);
        }
        finally
        {
            await lease.DisposeAsync();
        }

        PreparedCodeGraphOwnershipLoadResult loaded = await new FilePreparedCodeGraphOwnershipStore(
            sourceStoragePolicy).LoadAsync(absent);
        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, loaded.Status);
        PreparedCodeGraphWorkspaceOwnership ready = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
            loaded.Record);
        Assert.Equal(PreparedCodeGraphOwnershipPhase.Ready, ready.Phase);
        Assert.Equal(absent.Version + 2, ready.Version);
        Assert.True(SameImmutableKey(absent, ready));
        Assert.NotNull(ready.StateEntryObjectIdentity);
        return ready;
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

    private static bool SameImmutableKey(
        PreparedCodeGraphWorkspaceOwnership left,
        PreparedCodeGraphWorkspaceOwnership right) =>
        left.OwnershipId == right.OwnershipId &&
        left.Approval == right.Approval &&
        string.Equals(left.PhysicalRootPath, right.PhysicalRootPath, StringComparison.OrdinalIgnoreCase) &&
        left.RootObjectIdentity == right.RootObjectIdentity &&
        string.Equals(left.WorkspaceOwnerIdentity, right.WorkspaceOwnerIdentity, StringComparison.Ordinal);

    private sealed class ExactWorkspaceOwnershipVerifier(
        PreparedCodeGraphWorkspaceOwnership expected) : IPreparedCodeGraphWorkspaceOwnershipVerifier
    {
        public ValueTask<bool> VerifyCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(SameImmutableKey(expected, ownership));
        }

        public ValueTask<bool> VerifyDispositionReadyAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(false);
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

    private sealed class ControlledPreviousGenerationVerifier(
        PreparedWorkspaceSettlementStatus status) : IPreparedWorkspacePreviousGenerationVerifier
    {
        internal int VerificationCount { get; private set; }

        internal PreparedCodeGraphWorkspaceOwnership? LastBinding { get; private set; }

        public ValueTask<PreparedWorkspaceSettlementStatus> VerifySettledAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerificationCount++;
            LastBinding = binding;
            return ValueTask.FromResult(status);
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
        int? unavailableObservationCall = null,
        PreparedToolDirectoryObjectIdentity? observedRootIdentity = null,
        string? observedWorkspaceOwnerIdentity = null) : IPreparedManagedWorkspaceHostVerifier
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
                    observedRootIdentity ?? rootIdentity,
                    observedWorkspaceOwnerIdentity ?? derivedOwnerIdentity)
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
            bool absentDraft = binding.Phase == PreparedCodeGraphOwnershipPhase.Absent &&
                binding.Version == 0 && binding.StateEntryObjectIdentity is null;
            bool currentReady = binding.Phase == PreparedCodeGraphOwnershipPhase.Ready &&
                binding.Version >= 2 && binding.StateEntryObjectIdentity is not null;
            if (_disposed || _binding is null || !SameImmutableKey(_binding, binding) ||
                (!absentDraft && !currentReady))
            {
                return PreparedManagedWorkspaceHostStatus.Unavailable;
            }

            PreparedCodeGraphOwnershipLoadResult current = await ownershipStore.LoadAsync(
                binding,
                cancellationToken);
            bool exactCurrentStoreReadback = absentDraft
                ? current.Status == PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent && current.Record is null
                : current.Status == PreparedCodeGraphOwnershipLoadStatus.Found && current.Record == binding;
            return exactCurrentStoreReadback
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
