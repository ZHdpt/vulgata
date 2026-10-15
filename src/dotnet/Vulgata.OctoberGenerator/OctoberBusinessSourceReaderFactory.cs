using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Source;

namespace Vulgata.OctoberGenerator;

internal static class OctoberBusinessSourceReaderFactory
{
    internal static OctoberBusinessSourceReader Create(OctoberRuntimeConfiguration runtime,
        IOctoberLocalAuthority authority, OctoberGenerateExecution execution, TimeProvider clock) => new(async (source, token) =>
    {
        var issued = await authority.IssueBusinessSourceApprovalAsync(execution.Session, execution.Setup,
            source.Origin.RepositoryId, token);
        if (issued.Status != OctoberSourceApprovalStatus.Issued || issued.Approval is not { } approval)
            return null;
        async ValueTask CloseAsync()
        {
            var revoked = await authority.RevokeSourceApprovalAsync(execution.Session, execution.Setup.TargetId,
                approval.ApprovalIdentity, CancellationToken.None);
            if (revoked is not (OctoberRevocationStatus.Revoked or OctoberRevocationStatus.AlreadyRevoked))
                throw new InvalidOperationException("Business source approval closure unconfirmed.");
        }
        bool transferred = false;
        IPreparedSourceReadContext? context = null;
        try
        {
            if (approval.Purpose != PreparedSourcePurpose.BusinessSynthesisRead || approval.BatchId != execution.Setup.BatchId ||
                approval.SubjectId != execution.Session.SubjectId || approval.RepositoryId != source.Origin.RepositoryId ||
                approval.RootBindingIdentity != source.Origin.RootBindingIdentity || issued.ExpiresAtUtc is not { } expires || expires <= clock.GetUtcNow())
                return null;
            async Task<PreparedSourceAuthorityResult> VerifyAsync(string target, PreparedSourceApproval candidate, CancellationToken ct)
            {
                if (candidate != approval || target != execution.Setup.TargetId || clock.GetUtcNow() >= expires)
                    return new(PreparedSourceAuthorityStatus.Revoked, null);
                return await authority.VerifySourceApprovalAsync(execution.Session, target, candidate, ct);
            }
            var verifier = new OctoberPreparedSourceAuthorityVerifier(execution.Setup.TargetId, VerifyAsync,
                candidate => candidate == approval && runtime.PreparedRoots.Any(root => root.TargetId == execution.Setup.TargetId &&
                    root.BatchId == approval.BatchId && root.RepositoryId == approval.RepositoryId && root.RootBindingIdentity == approval.RootBindingIdentity));
            var binding = new PreparedSourceBinding(new PreparedSourceContextIssuer(verifier), execution.Setup.TargetId, runtime.PreparedRoots);
            var remaining = expires - clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero) return null;
            var result = await binding.IssueAsync(approval, PreparedSourceGrantLimits.Default with { MaximumOperations = 512, MaximumMetadataEntries = 1, MaximumExaminedBytes = 16 * 1024 * 1024, MaximumReturnedBytes = 8 * 1024 * 1024,
                Deadline = remaining < TimeSpan.FromSeconds(30) ? remaining : TimeSpan.FromSeconds(30) }, token);
            context = result.Context;
            if (result.Status != PreparedSourceIssueStatus.Issued || context is null) return null;
            var owned = context;
            var grant = new OctoberBusinessSourceGrant(owned, async ct =>
            {
                var current = await VerifyAsync(execution.Setup.TargetId, approval, ct);
                return current.Status == PreparedSourceAuthorityStatus.Current && current.CurrentApproval == approval
                    ? DisclosureStatus.Allowed : current.Status == PreparedSourceAuthorityStatus.Revoked
                        ? DisclosureStatus.Denied : DisclosureStatus.AuthorityUnavailable;
            }, ct => ObserveIdentityAsync(binding, approval, source.Origin.RelativePath, ct), CloseAsync);
            transferred = true;
            return grant;
        }
        finally
        {
            if (!transferred)
            {
                try { if (context is not null) await context.DisposeAsync(); }
                finally { await CloseAsync(); }
            }
        }
    });

    internal static async Task<string?> ObserveIdentityAsync(PreparedSourceBinding binding,
        PreparedSourceApproval approval, string relativePath, CancellationToken token)
    {
        // Each identity observation consumes one metadata entry. Keep the shared cap intact
        // and the two full content passes in their original context; this context cannot read
        // useful content and is disposed before returning its live-authorized identity.
        var issued = await binding.IssueAsync(approval, new(1, 1, 1, 1, TimeSpan.FromSeconds(30)), token);
        await using var context = issued.Context;
        return issued.Status == PreparedSourceIssueStatus.Issued && context is not null
            ? await binding.ObserveFileIdentityAsync(context, relativePath, token) : null;
    }
}
