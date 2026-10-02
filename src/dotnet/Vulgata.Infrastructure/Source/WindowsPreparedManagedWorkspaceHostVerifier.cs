using System.Security.Principal;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

internal sealed class WindowsPreparedManagedWorkspaceHostVerifier : IPreparedManagedWorkspaceHostVerifier
{
    internal WindowsPreparedManagedWorkspaceHostVerifier(SecurityIdentifier approvedApplicationPrincipal)
    {
        ArgumentNullException.ThrowIfNull(approvedApplicationPrincipal);
    }

    public Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PreparedManagedWorkspaceHostObservation(
            PreparedManagedWorkspaceHostStatus.Unavailable, null, null));

    public Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PreparedManagedWorkspaceHostHoldResult(
            PreparedManagedWorkspaceHostStatus.Unavailable, null));
}
