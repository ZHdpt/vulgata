using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.Infrastructure.Analysis.CodeGraph;

namespace Vulgata.OctoberGenerator;

public sealed record PreparedSourceRootBinding(
    string TargetId,
    Guid BatchId,
    Guid RepositoryId,
    string RootBindingIdentity,
    string PhysicalRootPath);

public sealed class PreparedSourceBinding
{
    private readonly PreparedSourceContextIssuer _issuer;
    private readonly string _targetId;
    private readonly IReadOnlyDictionary<(string TargetId, Guid BatchId, Guid RepositoryId,
        string RootBindingIdentity), string> _trustedRoots;

    public PreparedSourceBinding(
        PreparedSourceContextIssuer issuer,
        string targetId,
        IReadOnlyCollection<PreparedSourceRootBinding> trustedRoots)
    {
        _issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("An exact October target is required.", nameof(targetId));
        }

        ArgumentNullException.ThrowIfNull(trustedRoots);
        _targetId = targetId;
        Dictionary<(string TargetId, Guid BatchId, Guid RepositoryId,
            string RootBindingIdentity), string> roots = new();
        foreach (PreparedSourceRootBinding binding in trustedRoots)
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.TargetId) ||
                binding.BatchId == Guid.Empty || binding.RepositoryId == Guid.Empty ||
                string.IsNullOrWhiteSpace(binding.RootBindingIdentity) ||
                string.IsNullOrWhiteSpace(binding.PhysicalRootPath) ||
                !Path.IsPathFullyQualified(binding.PhysicalRootPath) ||
                !roots.TryAdd((binding.TargetId, binding.BatchId, binding.RepositoryId,
                    binding.RootBindingIdentity), binding.PhysicalRootPath))
            {
                throw new ArgumentException("Invalid or duplicate trusted prepared-source mapping.",
                    nameof(trustedRoots));
            }
        }
        _trustedRoots = roots;
    }

    public bool HasCurrentRootBinding(PreparedSourceApproval approval)
    {
        return approval is not null &&
            _trustedRoots.ContainsKey((_targetId, approval.BatchId, approval.RepositoryId,
                approval.RootBindingIdentity));
    }

    internal string GetTrustedRoot(PreparedSourceApproval approval) =>
        _trustedRoots.TryGetValue((_targetId, approval.BatchId, approval.RepositoryId,
            approval.RootBindingIdentity), out string? root)
            ? root : throw new InvalidOperationException("The exact prepared-source binding is unavailable.");

    internal Task<string?> ObserveFileIdentityAsync(IPreparedSourceReadContext context,
        string relativePath, CancellationToken cancellationToken)
        => _issuer.ObserveFileIdentityAsync(context, relativePath, cancellationToken);

    public Task<PreparedSourceIssueResult> IssueAsync(
        PreparedSourceApproval approval, PreparedSourceGrantLimits limits,
        PreparedCodeGraphConnection ownedState, CancellationToken cancellationToken = default)
    {
        if (!HasCurrentRootBinding(approval))
        {
            return Task.FromResult(new PreparedSourceIssueResult(PreparedSourceIssueStatus.Denied, null));
        }
        return _issuer.IssueAsync(new(approval, GetTrustedRoot(approval), limits), ownedState, cancellationToken);
    }

    public Task<PreparedSourceIssueResult> IssueAsync(
        PreparedSourceApproval approval,
        PreparedSourceGrantLimits limits,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new PreparedSourceIssueResult(
                PreparedSourceIssueStatus.Cancelled, null));
        }

        if (approval is null || limits is null ||
            !_trustedRoots.TryGetValue((_targetId, approval.BatchId, approval.RepositoryId,
                approval.RootBindingIdentity), out string? physicalRootPath))
        {
            return Task.FromResult(new PreparedSourceIssueResult(
                PreparedSourceIssueStatus.Denied, null));
        }

        return _issuer.IssueAsync(
            new PreparedSourceGrantRequest(approval, physicalRootPath, limits),
            cancellationToken);
    }
}
