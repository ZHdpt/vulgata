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
        var ownershipStore = new ControlledOwnershipStore(binding);
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
        var ownershipStore = new ControlledOwnershipStore(binding);
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BeginBorrowAsync_AdmittedReadyBindingTracksBorrowUntilProbeSettles()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var sourceAuthority = new CurrentSourceAuthority(fixture.Approval);
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.CodeGraphPreparation);
        var ownershipStore = new ControlledOwnershipStore(binding);
        var hostProtection = new ProtectedHostVerifier(binding);
        var previousGeneration = new PreviousGenerationSettledVerifier();
        var resources = new ControlledResourceProbe();
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
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);
            Assert.True(previousGeneration.VerificationCount > 0,
                "A persisted Ready binding must be reconciled before this process borrows it.");

            PreparedWorkspaceBorrowResult borrowResult = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                resources);
            Assert.True(borrowResult.Borrow is not null,
                "An admitted current Ready CodeGraph binding must register a ticket before its borrower starts.");

            PreparedWorkspaceBorrow borrow = borrowResult.Borrow!;
            resources.Report(PreparedWorkspaceSettlementStatus.Active);
            PreparedWorkspaceSettlementStatus whileActive = await registry.CloseForDispositionAsync(binding);
            Assert.True(whileActive != PreparedWorkspaceSettlementStatus.Settled,
                "Disposition cannot settle while the registered borrow's probe is Active.");

            resources.Report(PreparedWorkspaceSettlementStatus.Settled);
            int inspectionsBeforeTicketDisposal = resources.InspectionCount;
            await borrow.DisposeAsync();
            Assert.True(resources.InspectionCount > inspectionsBeforeTicketDisposal,
                "Ticket disposal must consult its resource probe after settlement is reported.");
            Assert.Equal(PreparedWorkspaceSettlementStatus.Settled, resources.LastReportedStatus);

            PreparedWorkspaceSettlementStatus afterTicketDisposal = await registry.CloseForDispositionAsync(binding);
            Assert.Equal(PreparedWorkspaceSettlementStatus.Settled, afterTicketDisposal);
        }
        finally
        {
            await registry.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AdmitAsync_WhenCurrentSourceProviderIgnoresCancellation_ReturnsUnavailableByDeadline()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var sourceAuthority = new UncooperativeCurrentSourceAuthority(fixture.Approval);
        var registry = new PreparedWorkspaceUseRegistry(
            sourceAuthority,
            new CurrentToolAuthorization(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation),
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            new ProtectedHostVerifier(binding),
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromMilliseconds(100),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        Task<PreparedWorkspaceAdmissionResult>? admissionTask = null;
        PreparedWorkspaceAdmissionResult? admissionResult = null;
        PreparedWorkspaceBorrowResult? lateBorrow = null;
        bool returnedBeforeProviderRelease = false;

        try
        {
            admissionTask = registry.AdmitAsync(binding, PreparedToolPurpose.CodeGraphPreparation);
            await sourceAuthority.VerificationStarted.WaitAsync(TimeSpan.FromSeconds(2));

            Task completed = await Task.WhenAny(admissionTask, Task.Delay(TimeSpan.FromSeconds(2)));
            returnedBeforeProviderRelease = ReferenceEquals(admissionTask, completed);
            if (returnedBeforeProviderRelease)
            {
                admissionResult = await admissionTask;
            }
        }
        finally
        {
            sourceAuthority.ReleaseCurrent();
            await sourceAuthority.PendingVerification.WaitAsync(TimeSpan.FromSeconds(2));

            if (admissionTask is not null)
            {
                Task observedAdmission = await Task.WhenAny(admissionTask, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(admissionTask, observedAdmission))
                {
                    try
                    {
                        admissionResult = await admissionTask;
                        lateBorrow = await registry.BeginBorrowAsync(
                            binding,
                            PreparedToolPurpose.CodeGraphPreparation,
                            new ControlledResourceProbe());
                    }
                    catch
                    {
                        // The test asserts public outcomes after cleanup; cleanup only observes the controlled admission.
                    }
                }
            }

            await registry.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.True(returnedBeforeProviderRelease,
            "Admission must finish within its trusted deadline even when the current-source provider ignores cancellation.");
        Assert.True(admissionResult is { Status: PreparedWorkspaceAdmissionStatus.Unavailable },
            "An expired source check must return Unavailable rather than publish Open after the provider eventually returns Current.");
        Assert.True(lateBorrow is { Borrow: null },
            "The late Current provider result cannot open the registry or issue a borrow after the admission deadline.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AdmitAsync_WhenHostAcquisitionIgnoresCancellation_DisposesLateHold()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var hostProtection = new UncooperativeProtectedHostVerifier(binding);
        var registry = new PreparedWorkspaceUseRegistry(
            new CurrentSourceAuthority(fixture.Approval),
            new CurrentToolAuthorization(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation),
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            hostProtection,
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromMilliseconds(100),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        Task<PreparedWorkspaceAdmissionResult>? admissionTask = null;
        PreparedWorkspaceAdmissionResult? admissionResult = null;
        PreparedWorkspaceBorrowResult? lateBorrow = null;
        bool returnedBeforeHostRelease = false;
        bool lateHoldDisposed = false;

        try
        {
            admissionTask = registry.AdmitAsync(binding, PreparedToolPurpose.CodeGraphPreparation);
            await hostProtection.AcquisitionStarted.WaitAsync(TimeSpan.FromSeconds(2));

            Task completed = await Task.WhenAny(admissionTask, Task.Delay(TimeSpan.FromSeconds(2)));
            returnedBeforeHostRelease = ReferenceEquals(admissionTask, completed);
            if (returnedBeforeHostRelease)
            {
                admissionResult = await admissionTask;
            }
        }
        finally
        {
            hostProtection.ReleaseProtectedHold();
            await hostProtection.ProviderRelease.WaitAsync(TimeSpan.FromSeconds(2));

            if (admissionTask is not null)
            {
                Task observedAdmission = await Task.WhenAny(admissionTask, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(admissionTask, observedAdmission))
                {
                    try
                    {
                        admissionResult = await admissionTask;
                        lateBorrow = await registry.BeginBorrowAsync(
                            binding,
                            PreparedToolPurpose.CodeGraphPreparation,
                            new ControlledResourceProbe());
                    }
                    catch
                    {
                        // The public outcome is asserted on the success path; cleanup only observes the controlled admission.
                    }
                }
            }

            Task holdDisposed = await Task.WhenAny(
                hostProtection.Hold.DisposalCompleted,
                Task.Delay(TimeSpan.FromSeconds(2)));
            lateHoldDisposed = ReferenceEquals(hostProtection.Hold.DisposalCompleted, holdDisposed);

            await registry.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.True(returnedBeforeHostRelease,
            "Admission must finish within its trusted deadline when host verification ignores cancellation.");
        Assert.True(admissionResult is { Status: PreparedWorkspaceAdmissionStatus.Unavailable },
            "A late protected host result cannot publish Open after the admission deadline.");
        Assert.True(lateHoldDisposed,
            "The registry must dispose a protected hold returned after its admission deadline.");
        Assert.True(lateBorrow is { Borrow: null },
            "A late host hold cannot authorize a borrow after admission timed out.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("tool-authorization")]
    [InlineData("ownership-store")]
    [InlineData("previous-generation")]
    [InlineData("held-current-check")]
    public async Task AdmitAsync_WhenMandatoryProofIgnoresCancellation_ReturnsUnavailableByDeadline(string proofStage)
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var proofWait = new ControlledAdmissionProofWait();
        IPreparedToolUseAuthorizer toolAuthorization = proofStage == "tool-authorization"
            ? new UncooperativeToolAuthorization(fixture.Approval, proofWait)
            : new CurrentToolAuthorization(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation);
        IPreparedCodeGraphOwnershipStore ownershipStore = proofStage == "ownership-store"
            ? new UncooperativeOwnershipStore(binding, proofWait)
            : new ControlledOwnershipStore(binding);
        IPreparedWorkspacePreviousGenerationVerifier previousGeneration = proofStage == "previous-generation"
            ? new UncooperativePreviousGenerationVerifier(proofWait)
            : new PreviousGenerationSettledVerifier();
        UncooperativeCurrentHoldVerifier? heldCheckHost = null;
        IPreparedManagedWorkspaceHostVerifier hostProtection;
        if (proofStage == "held-current-check")
        {
            heldCheckHost = new UncooperativeCurrentHoldVerifier(binding, proofWait);
            hostProtection = heldCheckHost;
        }
        else
        {
            hostProtection = new ProtectedHostVerifier(binding);
        }

        var registry = new PreparedWorkspaceUseRegistry(
            new CurrentSourceAuthority(fixture.Approval),
            toolAuthorization,
            ownershipStore,
            sourceStoragePolicy,
            hostProtection,
            previousGeneration,
            admissionDeadline: TimeSpan.FromMilliseconds(100),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        Task<PreparedWorkspaceAdmissionResult>? admissionTask = null;
        PreparedWorkspaceAdmissionResult? admissionResult = null;
        PreparedWorkspaceBorrowResult? laterBorrow = null;
        bool returnedBeforeProviderRelease = false;
        bool acquiredHoldDisposed = proofStage != "held-current-check";

        try
        {
            admissionTask = registry.AdmitAsync(binding, PreparedToolPurpose.CodeGraphPreparation);
            await proofWait.Started.WaitAsync(TimeSpan.FromSeconds(2));

            Task completed = await Task.WhenAny(admissionTask, Task.Delay(TimeSpan.FromSeconds(2)));
            returnedBeforeProviderRelease = ReferenceEquals(admissionTask, completed);
            if (returnedBeforeProviderRelease)
            {
                admissionResult = await admissionTask;
            }
        }
        finally
        {
            proofWait.Release();
            await proofWait.ProviderTask.WaitAsync(TimeSpan.FromSeconds(2));

            if (admissionTask is not null)
            {
                Task observedAdmission = await Task.WhenAny(admissionTask, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(admissionTask, observedAdmission))
                {
                    try
                    {
                        admissionResult = await admissionTask;
                        laterBorrow = await registry.BeginBorrowAsync(
                            binding,
                            PreparedToolPurpose.CodeGraphPreparation,
                            new ControlledResourceProbe());
                    }
                    catch
                    {
                        // The test asserts public outcomes after cleanup; cleanup only observes the controlled admission.
                    }
                }
            }

            if (heldCheckHost is not null)
            {
                Task disposed = await Task.WhenAny(
                    heldCheckHost.Hold.DisposalCompleted,
                    Task.Delay(TimeSpan.FromSeconds(2)));
                acquiredHoldDisposed = ReferenceEquals(heldCheckHost.Hold.DisposalCompleted, disposed);
            }

            await registry.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.True(returnedBeforeProviderRelease,
            $"Admission must bound the {proofStage} proof even when its provider ignores cancellation.");
        Assert.True(admissionResult is { Status: PreparedWorkspaceAdmissionStatus.Unavailable },
            $"A late positive {proofStage} proof cannot publish Open after the admission deadline.");
        Assert.True(laterBorrow is { Borrow: null },
            $"A timed-out {proofStage} proof cannot authorize a borrow after admission closes.");
        if (heldCheckHost is not null)
        {
            Assert.True(acquiredHoldDisposed,
                "A hold acquired before a timed-out currentness check must be disposed after the late check completes.");
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AdmitAsync_WhenHeldCheckCancelsSynchronously_DisposesAcquiredHold()
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
        using var callerCancellation = new CancellationTokenSource();
        var hold = new SynchronouslyCancelledHostHold(binding);
        var hostProtection = new CancellingProtectedHostVerifier(binding, hold, callerCancellation);
        var registry = new PreparedWorkspaceUseRegistry(
            new CurrentSourceAuthority(fixture.Approval),
            new CurrentToolAuthorization(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation),
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            hostProtection,
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));

        PreparedWorkspaceAdmissionResult? admission = null;
        PreparedWorkspaceBorrowResult? borrow = null;
        int disposeCountAfterAdmission = 0;
        bool disposalCompletedAfterAdmission = false;
        try
        {
            admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                callerCancellation.Token);
            disposeCountAfterAdmission = hold.DisposeCount;
            disposalCompletedAfterAdmission = hold.DisposalCompleted.IsCompleted;
            borrow = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                new ControlledResourceProbe());
        }
        finally
        {
            await registry.DisposeAsync();
        }

        Assert.Equal(PreparedWorkspaceAdmissionStatus.Cancelled, admission?.Status);
        Assert.True(hostProtection.VerifyAndHoldCount == 1,
            "The controlled host provider must return the acquired protected hold before cancellation is observed.");
        Assert.True(hold.CheckCurrentCount == 1,
            "The held currentness proof must be called and synchronously observe the caller cancellation.");
        Assert.True(disposeCountAfterAdmission == 1,
            "The registry must dispose the acquired hold when currentness throws synchronously on cancellation.");
        Assert.True(disposalCompletedAfterAdmission,
            "Cancellation cleanup must finish disposing the acquired hold before registry shutdown.");
        Assert.True(borrow?.Borrow is null,
            "A cancelled admission must not issue a borrow for its prepared binding.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BeginBorrowAsync_ReadyWorkspaceTracksJdtAndCodeGraphPurposesTogether()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var sourceAuthority = new CurrentSourceAuthority(fixture.Approval);
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.CodeGraphPreparation,
            PreparedToolPurpose.JdtNavigation);
        var registry = new PreparedWorkspaceUseRegistry(
            sourceAuthority,
            toolAuthorization,
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            new ProtectedHostVerifier(binding),
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromSeconds(2));
        var jdtResources = new ControlledResourceProbe();
        var codeGraphResources = new ControlledResourceProbe();

        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            int jdtChecksBeforeBorrow = toolAuthorization.CountFor(PreparedToolPurpose.JdtNavigation);
            PreparedWorkspaceBorrowResult jdtBorrowResult = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.JdtNavigation,
                jdtResources);
            Assert.True(jdtBorrowResult.Borrow is not null,
                "The same admitted Ready workspace can issue a separately authorized JDT borrow.");
            Assert.True(toolAuthorization.CountFor(PreparedToolPurpose.JdtNavigation) > jdtChecksBeforeBorrow,
                "The JDT borrow must verify its own current purpose after registry admission.");

            int codeGraphChecksBeforeBorrow = toolAuthorization.CountFor(PreparedToolPurpose.CodeGraphPreparation);
            PreparedWorkspaceBorrowResult codeGraphBorrowResult = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                codeGraphResources);
            Assert.True(codeGraphBorrowResult.Borrow is not null,
                "The same admitted Ready workspace can also issue a separately authorized CodeGraph borrow.");
            Assert.True(toolAuthorization.CountFor(PreparedToolPurpose.CodeGraphPreparation) > codeGraphChecksBeforeBorrow,
                "The CodeGraph borrow must verify its current purpose independently.");

            PreparedWorkspaceBorrow jdtBorrow = jdtBorrowResult.Borrow!;
            PreparedWorkspaceBorrow codeGraphBorrow = codeGraphBorrowResult.Borrow!;
            jdtResources.Report(PreparedWorkspaceSettlementStatus.Active);
            codeGraphResources.Report(PreparedWorkspaceSettlementStatus.Active);
            PreparedWorkspaceSettlementStatus bothActive = await registry.CloseForDispositionAsync(binding);
            Assert.True(bothActive != PreparedWorkspaceSettlementStatus.Settled,
                "Disposition cannot settle while either registered purpose remains Active.");

            jdtResources.Report(PreparedWorkspaceSettlementStatus.Settled);
            await jdtBorrow.DisposeAsync();
            PreparedWorkspaceSettlementStatus codeGraphStillActive = await registry.CloseForDispositionAsync(binding);
            Assert.True(codeGraphStillActive != PreparedWorkspaceSettlementStatus.Settled,
                "Disposing the settled JDT ticket cannot hide the still-active CodeGraph borrow.");

            codeGraphResources.Report(PreparedWorkspaceSettlementStatus.Settled);
            await codeGraphBorrow.DisposeAsync();
            PreparedWorkspaceSettlementStatus bothSettled = await registry.CloseForDispositionAsync(binding);
            Assert.Equal(PreparedWorkspaceSettlementStatus.Settled, bothSettled);
        }
        finally
        {
            await registry.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CloseForDispositionAsync_WhenProbeIgnoresCancellation_ReturnsUnknownWithoutReopening()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var sourceAuthority = new CurrentSourceAuthority(fixture.Approval);
        var toolAuthorization = new CurrentToolAuthorization(
            fixture.Approval,
            PreparedToolPurpose.CodeGraphPreparation);
        var hostProtection = new ProtectedHostVerifier(binding);
        var resources = new UncooperativeResourceProbe();
        var registry = new PreparedWorkspaceUseRegistry(
            sourceAuthority,
            toolAuthorization,
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            hostProtection,
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromMilliseconds(100));
        Task<PreparedWorkspaceSettlementStatus>? closeTask = null;

        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            PreparedWorkspaceBorrowResult borrow = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                resources);
            Assert.True(borrow.Borrow is not null,
                "Close must wait on an already registered borrow, not infer settlement from an empty registry.");

            closeTask = registry.CloseForDispositionAsync(binding);
            await resources.InspectionStarted.WaitAsync(TimeSpan.FromSeconds(2));
            Task completed = await Task.WhenAny(closeTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(ReferenceEquals(closeTask, completed),
                "Close must honor its disposition deadline when an owned-resource probe ignores cancellation.");

            PreparedWorkspaceSettlementStatus result = await closeTask;
            Assert.Equal(PreparedWorkspaceSettlementStatus.Unknown, result);
            Assert.True(resources.CapturedCancellationToken.IsCancellationRequested,
                "An Unknown closure result must follow the configured disposition deadline.");
            Assert.False(hostProtection.Hold.IsDisposed,
                "A timed-out live borrower must not release the registry's protected host hold.");

            PreparedWorkspaceBorrowResult laterBorrow = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                new ControlledResourceProbe());
            Assert.True(laterBorrow.Borrow is null,
                "A timed-out Unknown registry cannot reopen admission to another borrow.");
        }
        finally
        {
            resources.ReleaseUnknown();
            if (closeTask is not null)
            {
                Task observed = await Task.WhenAny(closeTask, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(closeTask, observed))
                {
                    try
                    {
                        await closeTask;
                    }
                    catch
                    {
                        // The test observes the close result on its success path; cleanup only drains it.
                    }
                }
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_WhenRegisteredProbeIgnoresCancellation_BoundsShutdownWithoutClaimingSettlement()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var hostProtection = new ProtectedHostVerifier(binding);
        var resources = new UncooperativeResourceProbe();
        var registry = new PreparedWorkspaceUseRegistry(
            new CurrentSourceAuthority(fixture.Approval),
            new CurrentToolAuthorization(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation),
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            hostProtection,
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromMilliseconds(100));
        Task? shutdown = null;

        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            PreparedWorkspaceBorrowResult borrow = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                resources);
            Assert.True(borrow.Borrow is not null,
                "The shutdown case requires an owned live probe registered before disposal.");

            shutdown = registry.DisposeAsync().AsTask();
            Task started = await Task.WhenAny(
                resources.InspectionStarted,
                shutdown,
                Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(ReferenceEquals(resources.InspectionStarted, started),
                "Bounded shutdown must inspect registered work before releasing its hold.");

            Task completed = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(ReferenceEquals(shutdown, completed),
                "Shutdown must finish within its trusted disposition deadline when a probe ignores cancellation.");
            await shutdown;

            Assert.True(resources.CapturedCancellationToken.IsCancellationRequested,
                "The configured shutdown deadline must cancel the bounded wait.");
            Assert.False(resources.InspectionCompleted,
                "Completing bounded shutdown cannot turn the still-pending owned probe into settlement.");
            Assert.True(hostProtection.Hold.IsDisposed,
                "Shutdown releases its host hold after the bounded wait without claiming resource settlement.");

            PreparedWorkspaceBorrowResult laterBorrow = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                new ControlledResourceProbe());
            Assert.Null(laterBorrow.Borrow);
            Assert.Equal(PreparedWorkspaceSettlementStatus.Unknown,
                await registry.CloseForDispositionAsync(binding));
        }
        finally
        {
            resources.ReleaseUnknown();
            await resources.PendingInspection.WaitAsync(TimeSpan.FromSeconds(2));
            if (shutdown is not null)
            {
                Task observed = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(shutdown, observed))
                {
                    try
                    {
                        await shutdown;
                    }
                    catch
                    {
                        // Shutdown is observed on the success path; cleanup only drains it.
                    }
                }
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_WhenTicketCompletionWaitsOnProbe_RemainsBoundedAndFailClosed()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var resources = new UncooperativeResourceProbe();
        var registry = new PreparedWorkspaceUseRegistry(
            new CurrentSourceAuthority(fixture.Approval),
            new CurrentToolAuthorization(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation),
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            new ProtectedHostVerifier(binding),
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(2),
            dispositionDeadline: TimeSpan.FromMilliseconds(100));
        Task? ticketDisposal = null;
        Task? shutdown = null;

        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            PreparedWorkspaceBorrowResult borrow = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                resources);
            Assert.True(borrow.Borrow is not null,
                "The concurrent shutdown case requires a registered owned-use ticket.");

            ticketDisposal = borrow.Borrow!.DisposeAsync().AsTask();
            await resources.InspectionStarted.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(resources.InspectionCompleted,
                "An unproved owned-resource inspection must remain pending while ticket disposal waits.");

            shutdown = registry.DisposeAsync().AsTask();
            Task completed = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(ReferenceEquals(shutdown, completed),
                "Registry shutdown must honor its disposition deadline while ticket completion is blocked by an unresponsive probe.");
            await shutdown;

            Assert.False(resources.InspectionCompleted,
                "Bounded shutdown cannot turn the still-pending owned-resource inspection into settlement.");
        }
        finally
        {
            resources.ReleaseUnknown();
            if (ticketDisposal is not null && shutdown is not null)
            {
                Task drain = Task.WhenAll(ticketDisposal, shutdown);
                Task observed = await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(drain, observed))
                {
                    try
                    {
                        await drain;
                    }
                    catch
                    {
                        // The public completion outcome is asserted on the success path; cleanup only observes the controlled tasks.
                    }
                }
            }
        }

        PreparedWorkspaceBorrowResult laterBorrow = await registry.BeginBorrowAsync(
            binding,
            PreparedToolPurpose.CodeGraphPreparation,
            new ControlledResourceProbe());
        Assert.True(laterBorrow.Borrow is null,
            "Unproved ticket completion and shutdown cannot reopen the disposed registry.");
        Assert.Equal(PreparedWorkspaceSettlementStatus.Unknown,
            await registry.CloseForDispositionAsync(binding));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_WhenAdmissionAuthorityIgnoresCancellation_RemainsBoundedAndPreventsLateOpen()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var sourceAuthority = new UncooperativeCurrentSourceAuthority(fixture.Approval);
        var hostProtection = new ProtectedHostVerifier(binding);
        var registry = new PreparedWorkspaceUseRegistry(
            sourceAuthority,
            new CurrentToolAuthorization(fixture.Approval, PreparedToolPurpose.CodeGraphPreparation),
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            hostProtection,
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(5),
            dispositionDeadline: TimeSpan.FromMilliseconds(100));
        Task<PreparedWorkspaceAdmissionResult>? admission = null;
        Task? shutdown = null;

        try
        {
            admission = registry.AdmitAsync(binding, PreparedToolPurpose.CodeGraphPreparation);
            await sourceAuthority.VerificationStarted.WaitAsync(TimeSpan.FromSeconds(2));

            shutdown = registry.DisposeAsync().AsTask();
            Task completed = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(ReferenceEquals(shutdown, completed),
                "Shutdown must honor its deadline while a current-authority provider is stalled inside admission.");
            await shutdown;
            Assert.False(admission.IsCompleted,
                "A blocked authority result cannot be treated as a completed admission during shutdown.");

            sourceAuthority.ReleaseCurrent();
            PreparedWorkspaceAdmissionResult lateAdmission = await admission.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(lateAdmission.Status != PreparedWorkspaceAdmissionStatus.Opened,
                "A provider result arriving after shutdown cannot publish a late Open admission.");

            PreparedWorkspaceBorrowResult laterBorrow = await registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                new ControlledResourceProbe());
            Assert.True(laterBorrow.Borrow is null,
                "A disposed registry cannot issue a borrow after the delayed authority provider returns.");
            Assert.Equal(PreparedWorkspaceSettlementStatus.Unknown,
                await registry.CloseForDispositionAsync(binding));
        }
        finally
        {
            sourceAuthority.ReleaseCurrent();
            await sourceAuthority.PendingVerification.WaitAsync(TimeSpan.FromSeconds(2));
            if (admission is not null && shutdown is not null)
            {
                Task drain = Task.WhenAll(admission, shutdown);
                Task observed = await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(drain, observed))
                {
                    try
                    {
                        await drain;
                    }
                    catch
                    {
                        // The public admission/shutdown outcomes are asserted on the success path; cleanup only observes both tasks.
                    }
                }
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisposeAsync_WhenBorrowAuthorityCheckIsStalled_DeniesLateTicketAndReleasesHold()
    {
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        fixture.Authority.Approve(fixture.Approval);

        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string codeGraphPath = Path.Combine(fixture.PreparedRootPath, ".codegraph");
        Directory.CreateDirectory(codeGraphPath);
        RetainedSourceCapturePolicy sourceStoragePolicy = CreatePolicy(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot);
        var binding = new PreparedCodeGraphWorkspaceOwnership(
            OwnershipId: Guid.NewGuid(),
            Version: 1,
            Phase: PreparedCodeGraphOwnershipPhase.Ready,
            Approval: fixture.Approval,
            PhysicalRootPath: Path.GetFullPath(fixture.PreparedRootPath),
            RootObjectIdentity: ObserveDirectoryIdentity(fixture.PreparedRootPath),
            WorkspaceOwnerIdentity: $"owner-{fixture.SandboxId:N}",
            StateEntryObjectIdentity: ObserveDirectoryIdentity(codeGraphPath));
        var toolAuthorization = new UncooperativeCurrentToolAuthorization(fixture.Approval);
        var hostProtection = new ProtectedHostVerifier(binding);
        var registry = new PreparedWorkspaceUseRegistry(
            new CurrentSourceAuthority(fixture.Approval),
            toolAuthorization,
            new ControlledOwnershipStore(binding),
            sourceStoragePolicy,
            hostProtection,
            new PreviousGenerationSettledVerifier(),
            admissionDeadline: TimeSpan.FromSeconds(5),
            dispositionDeadline: TimeSpan.FromMilliseconds(100));
        Task<PreparedWorkspaceBorrowResult>? borrowTask = null;
        Task? shutdown = null;
        PreparedWorkspaceBorrowResult? lateBorrow = null;
        bool shutdownCompletedBeforeProviderRelease = false;
        bool holdReleasedAfterLateOperation = false;

        try
        {
            PreparedWorkspaceAdmissionResult admission = await registry.AdmitAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation);
            Assert.Equal(PreparedWorkspaceAdmissionStatus.Opened, admission.Status);

            toolAuthorization.BlockNextVerification();
            borrowTask = registry.BeginBorrowAsync(
                binding,
                PreparedToolPurpose.CodeGraphPreparation,
                new ControlledResourceProbe());
            await toolAuthorization.VerificationStarted.WaitAsync(TimeSpan.FromSeconds(2));

            shutdown = registry.DisposeAsync().AsTask();
            Task completed = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(2)));
            shutdownCompletedBeforeProviderRelease = ReferenceEquals(shutdown, completed);
        }
        finally
        {
            toolAuthorization.ReleaseCurrent();
            await toolAuthorization.PendingVerification.WaitAsync(TimeSpan.FromSeconds(2));

            if (borrowTask is not null)
            {
                Task observedBorrow = await Task.WhenAny(borrowTask, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(borrowTask, observedBorrow))
                {
                    try
                    {
                        lateBorrow = await borrowTask;
                        if (lateBorrow.Borrow is not null)
                        {
                            await lateBorrow.Borrow.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
                        }
                    }
                    catch
                    {
                        // The test asserts public results after cleanup; cleanup only observes the controlled borrow task.
                    }
                }
            }

            if (shutdown is not null)
            {
                Task observedShutdown = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(2)));
                if (ReferenceEquals(shutdown, observedShutdown))
                {
                    try
                    {
                        await shutdown;
                    }
                    catch
                    {
                        // The shutdown outcome is asserted after both controlled operations are observed.
                    }
                }
            }

            Task observedHoldRelease = await Task.WhenAny(
                hostProtection.Hold.DisposalCompleted,
                Task.Delay(TimeSpan.FromSeconds(2)));
            holdReleasedAfterLateOperation = ReferenceEquals(hostProtection.Hold.DisposalCompleted, observedHoldRelease);
        }

        Assert.True(shutdownCompletedBeforeProviderRelease,
            "Shutdown must finish within its disposition deadline while a borrow authority provider is stalled.");
        Assert.True(lateBorrow is not null && lateBorrow.Borrow is null,
            "A borrow authority result returned after shutdown cannot grant a late ticket.");
        Assert.True(holdReleasedAfterLateOperation,
            "The registry must eventually release its owned host hold after the stalled borrow unwinds.");
        Assert.Equal(PreparedWorkspaceSettlementStatus.Unknown,
            await registry.CloseForDispositionAsync(binding));
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

    private sealed class UncooperativeCurrentSourceAuthority(PreparedSourceApproval expected)
        : IPreparedSourceCurrentAuthorityVerifier
    {
        private readonly TaskCompletionSource _verificationStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<PreparedSourceAuthorityResult> _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task VerificationStarted => _verificationStarted.Task;

        internal Task<PreparedSourceAuthorityResult> PendingVerification => _released.Task;

        internal void ReleaseCurrent() => _released.TrySetResult(
            new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current, expected));

        public async Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval requestedApproval,
            CancellationToken cancellationToken = default)
        {
            _verificationStarted.TrySetResult();
            PreparedSourceAuthorityResult result = await _released.Task;
            return requestedApproval == expected
                ? result
                : new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
        }
    }

    private sealed class CurrentToolAuthorization : IPreparedToolUseAuthorizer
    {
        private readonly PreparedSourceApproval _expectedApproval;
        private readonly HashSet<PreparedToolPurpose> _expectedPurposes;
        private readonly List<PreparedToolPurpose> _verifiedPurposes = [];

        internal CurrentToolAuthorization(
            PreparedSourceApproval expectedApproval,
            params PreparedToolPurpose[] expectedPurposes)
        {
            _expectedApproval = expectedApproval;
            _expectedPurposes = new HashSet<PreparedToolPurpose>(expectedPurposes);
        }

        internal int VerificationCount { get; private set; }

        internal int CountFor(PreparedToolPurpose purpose) =>
            _verifiedPurposes.Count(verifiedPurpose => verifiedPurpose == purpose);

        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerificationCount++;
            _verifiedPurposes.Add(toolPurpose);
            return Task.FromResult(new PreparedToolUseAuthorityResult(
                approval == _expectedApproval && _expectedPurposes.Contains(toolPurpose)
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied));
        }
    }

    private sealed class UncooperativeCurrentToolAuthorization(PreparedSourceApproval expectedApproval)
        : IPreparedToolUseAuthorizer
    {
        private readonly TaskCompletionSource _verificationStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _blockNextVerification;

        internal Task VerificationStarted => _verificationStarted.Task;

        internal Task PendingVerification => _released.Task;

        internal void BlockNextVerification() => Interlocked.Exchange(ref _blockNextVerification, 1);

        internal void ReleaseCurrent() => _released.TrySetResult();

        public async Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Exchange(ref _blockNextVerification, 0) == 1)
            {
                _verificationStarted.TrySetResult();
                await _released.Task;
            }

            return new PreparedToolUseAuthorityResult(
                approval == expectedApproval && toolPurpose == PreparedToolPurpose.CodeGraphPreparation
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied);
        }
    }

    private sealed class ControlledAdmissionProofWait
    {
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? _providerTask;

        internal Task Started => _started.Task;

        internal Task ProviderTask => _providerTask ?? Task.CompletedTask;

        internal void Release() => _released.TrySetResult();

        internal Task<TResult> ReturnAfterReleaseAsync<TResult>(TResult result)
        {
            Task<TResult> providerTask = CompleteAfterReleaseAsync(result);
            _providerTask = providerTask;
            return providerTask;
        }

        private async Task<TResult> CompleteAfterReleaseAsync<TResult>(TResult result)
        {
            _started.TrySetResult();
            await _released.Task;
            return result;
        }
    }

    private sealed class UncooperativeToolAuthorization(
        PreparedSourceApproval expectedApproval,
        ControlledAdmissionProofWait proofWait) : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default) =>
            proofWait.ReturnAfterReleaseAsync(new PreparedToolUseAuthorityResult(
                approval == expectedApproval && toolPurpose == PreparedToolPurpose.CodeGraphPreparation
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied));
    }

    private sealed class UncooperativeOwnershipStore(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        ControlledAdmissionProofWait proofWait) : IPreparedCodeGraphOwnershipStore
    {
        public Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
            PreparedCodeGraphWorkspaceOwnership expected,
            CancellationToken cancellationToken = default) =>
            proofWait.ReturnAfterReleaseAsync(expected == expectedBinding
                ? new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.Found,
                    expectedBinding)
                : new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.Unavailable,
                    null));

        public Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
            PreparedCodeGraphWorkspaceOwnership absent,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Admission must not mutate the Ready ownership record.");

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
            throw new InvalidOperationException("Admission must not quarantine an unchanged Ready record.");
    }

    private sealed class UncooperativePreviousGenerationVerifier(ControlledAdmissionProofWait proofWait)
        : IPreparedWorkspacePreviousGenerationVerifier
    {
        public ValueTask<PreparedWorkspaceSettlementStatus> VerifySettledAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default) =>
            new(proofWait.ReturnAfterReleaseAsync(PreparedWorkspaceSettlementStatus.Settled));
    }

    private sealed class ControlledOwnershipStore(PreparedCodeGraphWorkspaceOwnership expectedBinding)
        : IPreparedCodeGraphOwnershipStore
    {
        internal PreparedCodeGraphWorkspaceOwnership? LastLoadedBinding { get; private set; }

        public Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
            PreparedCodeGraphWorkspaceOwnership expected,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastLoadedBinding = expected;
            PreparedCodeGraphOwnershipLoadResult result = expected == expectedBinding
                ? expectedBinding.Phase switch
                {
                    PreparedCodeGraphOwnershipPhase.Absent => new PreparedCodeGraphOwnershipLoadResult(
                        PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent,
                        null),
                    PreparedCodeGraphOwnershipPhase.Ready => new PreparedCodeGraphOwnershipLoadResult(
                        PreparedCodeGraphOwnershipLoadStatus.Found,
                        expectedBinding),
                    _ => new PreparedCodeGraphOwnershipLoadResult(
                        PreparedCodeGraphOwnershipLoadStatus.Unavailable,
                        null),
                }
                : new PreparedCodeGraphOwnershipLoadResult(
                    PreparedCodeGraphOwnershipLoadStatus.Unavailable,
                    null);
            return Task.FromResult(result);
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

    private sealed class CancellingProtectedHostVerifier(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        SynchronouslyCancelledHostHold hold,
        CancellationTokenSource callerCancellation)
        : IPreparedManagedWorkspaceHostVerifier
    {
        internal int VerifyAndHoldCount { get; private set; }

        public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
            PreparedSourceGrantRequest applicationHeldSourceGrant,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedManagedWorkspaceHostObservation(
                PreparedManagedWorkspaceHostStatus.Observed,
                expectedBinding.RootObjectIdentity,
                expectedBinding.WorkspaceOwnerIdentity));

        public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            VerifyAndHoldCount++;
            Task<PreparedManagedWorkspaceHostHoldResult> completed = Task.FromResult(
                binding == expectedBinding
                    ? new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Protected, hold)
                    : new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Denied, null));
            callerCancellation.Cancel();
            return completed;
        }
    }

    private sealed class SynchronouslyCancelledHostHold(
        PreparedCodeGraphWorkspaceOwnership expectedBinding) : IPreparedManagedWorkspaceHostHold
    {
        private readonly TaskCompletionSource _disposalCompleted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal int CheckCurrentCount { get; private set; }

        internal int DisposeCount { get; private set; }

        internal Task DisposalCompleted => _disposalCompleted.Task;

        public ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default)
        {
            CheckCurrentCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(binding == expectedBinding
                ? PreparedManagedWorkspaceHostStatus.Protected
                : PreparedManagedWorkspaceHostStatus.Unavailable);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            _disposalCompleted.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UncooperativeProtectedHostVerifier(PreparedCodeGraphWorkspaceOwnership expectedBinding)
        : IPreparedManagedWorkspaceHostVerifier
    {
        private readonly TaskCompletionSource _acquisitionStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task AcquisitionStarted => _acquisitionStarted.Task;

        internal Task ProviderRelease => _released.Task;

        internal ProtectedHostHold Hold { get; } = new(expectedBinding);

        internal void ReleaseProtectedHold() => _released.TrySetResult();

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

        public async Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default)
        {
            _acquisitionStarted.TrySetResult();
            await _released.Task;
            return binding == expectedBinding
                ? new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Protected, Hold)
                : new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Denied, null);
        }
    }

    private sealed class UncooperativeCurrentHoldVerifier(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        ControlledAdmissionProofWait proofWait) : IPreparedManagedWorkspaceHostVerifier
    {
        internal UncooperativeCurrentHold Hold { get; } = new(expectedBinding, proofWait);

        public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
            PreparedSourceGrantRequest applicationHeldSourceGrant,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedManagedWorkspaceHostObservation(
                PreparedManagedWorkspaceHostStatus.Observed,
                expectedBinding.RootObjectIdentity,
                expectedBinding.WorkspaceOwnerIdentity));

        public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            RetainedSourceCapturePolicy sourceStoragePolicy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(binding == expectedBinding
                ? new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Protected, Hold)
                : new PreparedManagedWorkspaceHostHoldResult(PreparedManagedWorkspaceHostStatus.Denied, null));
    }

    private sealed class UncooperativeCurrentHold(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        ControlledAdmissionProofWait proofWait) : IPreparedManagedWorkspaceHostHold
    {
        private readonly TaskCompletionSource _disposalCompleted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task DisposalCompleted => _disposalCompleted.Task;

        internal bool IsDisposed { get; private set; }

        public ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership binding,
            CancellationToken cancellationToken = default) =>
            new(proofWait.ReturnAfterReleaseAsync(!IsDisposed && binding == expectedBinding
                ? PreparedManagedWorkspaceHostStatus.Protected
                : PreparedManagedWorkspaceHostStatus.Unavailable));

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            _disposalCompleted.TrySetResult();
            return ValueTask.CompletedTask;
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

        private readonly TaskCompletionSource _disposalCompleted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task DisposalCompleted => _disposalCompleted.Task;

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
            _disposalCompleted.TrySetResult();
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
        private PreparedWorkspaceSettlementStatus _status = PreparedWorkspaceSettlementStatus.Settled;

        internal int InspectionCount { get; private set; }

        internal PreparedWorkspaceSettlementStatus LastReportedStatus { get; private set; }

        internal void Report(PreparedWorkspaceSettlementStatus status) => _status = status;

        public ValueTask<PreparedWorkspaceSettlementStatus> InspectOwnedResourcesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectionCount++;
            LastReportedStatus = _status;
            return ValueTask.FromResult(_status);
        }
    }

    private sealed class UncooperativeResourceProbe : IPreparedWorkspaceResourceSettlementProbe
    {
        private readonly TaskCompletionSource _inspectionStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<PreparedWorkspaceSettlementStatus> _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task InspectionStarted => _inspectionStarted.Task;

        internal Task<PreparedWorkspaceSettlementStatus> PendingInspection => _released.Task;

        internal CancellationToken CapturedCancellationToken { get; private set; }

        internal bool InspectionCompleted => _released.Task.IsCompleted;

        internal void ReleaseUnknown() =>
            _released.TrySetResult(PreparedWorkspaceSettlementStatus.Unknown);

        public ValueTask<PreparedWorkspaceSettlementStatus> InspectOwnedResourcesAsync(
            CancellationToken cancellationToken = default)
        {
            CapturedCancellationToken = cancellationToken;
            _inspectionStarted.TrySetResult();
            return new ValueTask<PreparedWorkspaceSettlementStatus>(_released.Task);
        }
    }
}
