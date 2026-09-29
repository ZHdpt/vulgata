using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Infrastructure.Analysis.Java;

/// <summary>
/// Owns the returned protocol session. The separately issued workspace lease is
/// borrowed and remains the trusted caller's disposal responsibility.
/// </summary>
public sealed class PreparedJdtLsNavigationContext : IPreparedJavaNavigationContext
{
    private readonly PreparedToolWorkspaceLease _workspaceLease;
    private readonly IPreparedJdtLsSession _session;
    private int _disposed;

    internal PreparedJdtLsNavigationContext(
        PreparedToolWorkspaceLease workspaceLease,
        IPreparedJdtLsSession session)
    {
        _workspaceLease = workspaceLease;
        _session = session;
    }

    public Guid RepositoryId => _workspaceLease.RepositoryId;

    public Task<PreparedJavaNavigationResult> GetDefinitionAsync(
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default) =>
        CheckUnavailableAsync(PreparedJavaNavigationOperation.Definition, cancellationToken);

    public Task<PreparedJavaNavigationResult> GetReferencesAsync(
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default) =>
        CheckUnavailableAsync(PreparedJavaNavigationOperation.References, cancellationToken);

    private async Task<PreparedJavaNavigationResult> CheckUnavailableAsync(
        PreparedJavaNavigationOperation operation,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Empty(operation, PreparedJavaNavigationOutcome.Denied);
        }

        PreparedToolWorkspaceCheckStatus status =
            await _workspaceLease.CheckCurrentAsync(cancellationToken);
        PreparedJavaNavigationOutcome outcome = status switch
        {
            PreparedToolWorkspaceCheckStatus.AuthorityUnavailable =>
                PreparedJavaNavigationOutcome.AuthorityUnavailable,
            PreparedToolWorkspaceCheckStatus.Cancelled =>
                PreparedJavaNavigationOutcome.Cancelled,
            PreparedToolWorkspaceCheckStatus.DeadlineExceeded =>
                PreparedJavaNavigationOutcome.DeadlineExceeded,
            PreparedToolWorkspaceCheckStatus.Current =>
                PreparedJavaNavigationOutcome.ConfigurationUnavailable,
            _ => PreparedJavaNavigationOutcome.Denied,
        };
        return Empty(operation, outcome);
    }

    private PreparedJavaNavigationResult Empty(
        PreparedJavaNavigationOperation operation,
        PreparedJavaNavigationOutcome outcome) =>
        new(RepositoryId, operation, outcome, [], [], null);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await _session.DisposeAsync();
        }
    }
}
