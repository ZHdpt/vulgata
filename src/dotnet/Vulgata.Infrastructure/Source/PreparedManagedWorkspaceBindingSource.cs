using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

internal enum PreparedManagedWorkspaceBindingStatus
{
    Bound,
    Denied,
    Unavailable,
}

internal sealed record PreparedManagedWorkspaceBindingResult(
    PreparedManagedWorkspaceBindingStatus Status,
    PreparedCodeGraphWorkspaceOwnership? Binding);

internal sealed class PreparedManagedWorkspaceBindingSource
{
    private readonly IPreparedSourceCurrentAuthorityVerifier _sourceAuthority;
    private readonly IPreparedToolUseAuthorizer _toolAuthorization;
    private readonly RetainedSourceCapturePolicy _sourceStoragePolicy;
    private readonly FilePreparedCodeGraphOwnershipStore _ownershipStore;
    private readonly IPreparedManagedWorkspaceHostVerifier _hostProtection;
    private readonly PreparedWorkspaceUseRegistry _uses;

    internal PreparedManagedWorkspaceBindingSource(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        FilePreparedCodeGraphOwnershipStore ownershipStore,
        IPreparedManagedWorkspaceHostVerifier hostProtection,
        PreparedWorkspaceUseRegistry uses)
    {
        _sourceAuthority = sourceAuthority ?? throw new ArgumentNullException(nameof(sourceAuthority));
        _toolAuthorization = toolAuthorization ?? throw new ArgumentNullException(nameof(toolAuthorization));
        _sourceStoragePolicy = sourceStoragePolicy ??
            throw new ArgumentNullException(nameof(sourceStoragePolicy));
        _ownershipStore = ownershipStore ?? throw new ArgumentNullException(nameof(ownershipStore));
        _hostProtection = hostProtection ?? throw new ArgumentNullException(nameof(hostProtection));
        _uses = uses ?? throw new ArgumentNullException(nameof(uses));
    }

    internal async Task<PreparedManagedWorkspaceBindingResult> BindAsync(
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        PreparedToolPurpose selectedPurpose,
        CancellationToken cancellationToken = default)
    {
        if (applicationHeldSourceGrant?.Approval is null ||
            !Path.IsPathFullyQualified(applicationHeldSourceGrant.PhysicalRootPath))
        {
            return Unavailable();
        }

        PreparedCodeGraphWorkspaceOwnership? admittedBinding = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedManagedWorkspaceBindingStatus authority = await CheckAuthorityAsync(
                applicationHeldSourceGrant.Approval, selectedPurpose, cancellationToken);
            if (authority != PreparedManagedWorkspaceBindingStatus.Bound)
            {
                return new PreparedManagedWorkspaceBindingResult(authority, null);
            }

            PreparedManagedWorkspaceHostObservation observed = await _hostProtection.ObserveAsync(
                applicationHeldSourceGrant, _sourceStoragePolicy, cancellationToken);
            if (observed.Status != PreparedManagedWorkspaceHostStatus.Observed ||
                observed.RootObjectIdentity is null ||
                string.IsNullOrWhiteSpace(observed.DerivedWorkspaceOwnerIdentity) ||
                !RootMatches(applicationHeldSourceGrant.PhysicalRootPath,
                    observed.RootObjectIdentity))
            {
                return Unavailable();
            }

            PreparedCodeGraphOwnershipLoadResult lookup = await _ownershipStore.FindExactWorkspaceAsync(
                applicationHeldSourceGrant.Approval,
                applicationHeldSourceGrant.PhysicalRootPath,
                observed.RootObjectIdentity,
                observed.DerivedWorkspaceOwnerIdentity,
                cancellationToken);
            PreparedCodeGraphWorkspaceOwnership binding;
            if (lookup.Status == PreparedCodeGraphOwnershipLoadStatus.Found &&
                lookup.Record is not null)
            {
                binding = lookup.Record;
            }
            else if (lookup.Status == PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent &&
                     lookup.Record is null)
            {
                binding = new PreparedCodeGraphWorkspaceOwnership(
                    Guid.NewGuid(), 0, PreparedCodeGraphOwnershipPhase.Absent,
                    applicationHeldSourceGrant.Approval,
                    applicationHeldSourceGrant.PhysicalRootPath,
                    observed.RootObjectIdentity,
                    observed.DerivedWorkspaceOwnerIdentity,
                    StateEntryObjectIdentity: null);
            }
            else
            {
                return Unavailable();
            }

            if (binding.Approval != applicationHeldSourceGrant.Approval ||
                !string.Equals(binding.PhysicalRootPath, applicationHeldSourceGrant.PhysicalRootPath,
                    StringComparison.OrdinalIgnoreCase) ||
                binding.RootObjectIdentity != observed.RootObjectIdentity ||
                binding.WorkspaceOwnerIdentity != observed.DerivedWorkspaceOwnerIdentity ||
                binding.Phase is not (PreparedCodeGraphOwnershipPhase.Absent or
                    PreparedCodeGraphOwnershipPhase.Ready))
            {
                return Unavailable();
            }

            PreparedCodeGraphOwnershipLoadResult loaded = await _ownershipStore.LoadAsync(
                binding, cancellationToken);
            if (!SameLoad(binding, loaded))
            {
                return Unavailable();
            }

            PreparedWorkspaceAdmissionResult admission = await _uses.AdmitAsync(
                binding, selectedPurpose, cancellationToken);
            if (admission.Status != PreparedWorkspaceAdmissionStatus.Opened)
            {
                return new PreparedManagedWorkspaceBindingResult(
                    admission.Status == PreparedWorkspaceAdmissionStatus.Denied
                        ? PreparedManagedWorkspaceBindingStatus.Denied
                        : PreparedManagedWorkspaceBindingStatus.Unavailable, null);
            }

            admittedBinding = binding;

            authority = await CheckAuthorityAsync(
                applicationHeldSourceGrant.Approval, selectedPurpose, cancellationToken);
            PreparedCodeGraphOwnershipLoadResult finalLoad = await _ownershipStore.LoadAsync(
                binding, cancellationToken);
            PreparedManagedWorkspaceHostObservation finalObservation = await _hostProtection.ObserveAsync(
                applicationHeldSourceGrant, _sourceStoragePolicy, cancellationToken);
            bool finalProof = authority == PreparedManagedWorkspaceBindingStatus.Bound &&
                SameLoad(binding, finalLoad) &&
                finalObservation.Status == PreparedManagedWorkspaceHostStatus.Observed &&
                finalObservation.RootObjectIdentity == binding.RootObjectIdentity &&
                finalObservation.DerivedWorkspaceOwnerIdentity == binding.WorkspaceOwnerIdentity &&
                RootMatches(binding.PhysicalRootPath, binding.RootObjectIdentity);
            if (!finalProof)
            {
                return Unavailable();
            }

            admittedBinding = null;
            return new PreparedManagedWorkspaceBindingResult(
                PreparedManagedWorkspaceBindingStatus.Bound, binding);
        }
        catch (Exception)
        {
            return Unavailable();
        }
        finally
        {
            if (admittedBinding is not null)
            {
                // The caller's token may already be cancelled; closure must still latch.
                await _uses.CloseForDispositionAsync(admittedBinding, CancellationToken.None);
            }
        }
    }

    private async Task<PreparedManagedWorkspaceBindingStatus> CheckAuthorityAsync(
        PreparedSourceApproval approval, PreparedToolPurpose selectedPurpose,
        CancellationToken cancellationToken)
    {
        PreparedSourceAuthorityResult source = await _sourceAuthority.VerifyCurrentAsync(
            approval, cancellationToken);
        if (source.Status != PreparedSourceAuthorityStatus.Current ||
            source.CurrentApproval != approval)
        {
            return source.Status == PreparedSourceAuthorityStatus.Revoked
                ? PreparedManagedWorkspaceBindingStatus.Denied
                : PreparedManagedWorkspaceBindingStatus.Unavailable;
        }

        PreparedToolUseAuthorityResult tool = await _toolAuthorization.VerifyCurrentAsync(
            approval, selectedPurpose, cancellationToken);
        return tool.Status switch
        {
            PreparedToolUseAuthorityStatus.Current => PreparedManagedWorkspaceBindingStatus.Bound,
            PreparedToolUseAuthorityStatus.Denied => PreparedManagedWorkspaceBindingStatus.Denied,
            _ => PreparedManagedWorkspaceBindingStatus.Unavailable,
        };
    }

    private static bool SameLoad(PreparedCodeGraphWorkspaceOwnership binding,
        PreparedCodeGraphOwnershipLoadResult loaded) =>
        binding.Phase == PreparedCodeGraphOwnershipPhase.Absent
            ? loaded.Status == PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent &&
              loaded.Record is null
            : loaded.Status == PreparedCodeGraphOwnershipLoadStatus.Found &&
              loaded.Record == binding;

    private static bool RootMatches(string path, PreparedToolDirectoryObjectIdentity expected)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? handle) =
            PreparedSourceTraversal.OpenRoot(path);
        using (handle)
        {
            PreparedSourceTraversal.DirectoryIdentity? actual =
                handle is null ? null : PreparedSourceTraversal.ObserveDirectory(handle);
            return status == PreparedSourceTraversal.OpenStatus.Opened &&
                actual is not null &&
                new PreparedToolDirectoryObjectIdentity(actual.VolumeSerialNumber,
                    actual.FileIndexHigh, actual.FileIndexLow) == expected;
        }
    }

    private static PreparedManagedWorkspaceBindingResult Unavailable() =>
        new(PreparedManagedWorkspaceBindingStatus.Unavailable, null);
}
