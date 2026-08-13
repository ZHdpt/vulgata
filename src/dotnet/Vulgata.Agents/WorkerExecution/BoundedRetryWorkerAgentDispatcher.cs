using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Explicit opt-in decorator for bounded demonstration retries. Production
/// composition does not register this type; callers must choose it directly.
/// Canonical validation remains the responsibility of the inner dispatcher.
/// </summary>
public sealed class BoundedRetryWorkerAgentDispatcher : IWorkerAgentDispatcher
{
    public const int MaximumAllowedAttempts = 3;

    private readonly IWorkerAgentDispatcher _inner;
    private readonly int _maximumAttempts;

    public BoundedRetryWorkerAgentDispatcher(
        IWorkerAgentDispatcher inner,
        int maximumAttempts)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (maximumAttempts is < 1 or > MaximumAllowedAttempts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAttempts),
                maximumAttempts,
                $"Maximum attempts must be between 1 and {MaximumAllowedAttempts}.");
        }

        _maximumAttempts = maximumAttempts;
    }

    public Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        CancellationToken cancellationToken = default) =>
        DispatchWithRetryAsync(
            token => _inner.DispatchAsync(packet, token),
            cancellationToken);

    public Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(scope);
        return DispatchWithRetryAsync(
            token => _inner.DispatchAsync(packet, scope, token),
            cancellationToken);
    }

    private async Task<WorkerAgentResultEnvelope> DispatchWithRetryAsync(
        Func<CancellationToken, Task<WorkerAgentResultEnvelope>> dispatch,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= _maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkerAgentResultEnvelope result = await dispatch(cancellationToken)
                .ConfigureAwait(false);
            if (attempt == _maximumAttempts || !IsRetryable(result.Outcome))
            {
                return result;
            }
        }

        throw new InvalidOperationException("The bounded retry loop did not return a result.");
    }

    private static bool IsRetryable(WorkerAgentResultOutcome outcome) =>
        outcome is WorkerAgentResultOutcome.SchemaInvalid or WorkerAgentResultOutcome.Error;
}
