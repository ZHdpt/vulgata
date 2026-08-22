using System.Collections.Concurrent;

namespace Vulgata.Web.Data;

public sealed class PreparedPass1ExecutionHostedService(
    PreparedPass1ExecutionActivator activator,
    IServiceScopeFactory scopeFactory,
    ILogger<PreparedPass1ExecutionHostedService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Task> _pumps = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Guid pairExecutionId in activator.ReadActivatedPairsAsync(stoppingToken))
        {
            if (_pumps.TryGetValue(pairExecutionId, out Task? existing) && !existing.IsCompleted)
            {
                continue;
            }

            Task pump = RunPairAsync(pairExecutionId, stoppingToken);
            _pumps[pairExecutionId] = pump;
            _ = pump.ContinueWith(
                (_, state) => ((ConcurrentDictionary<Guid, Task>)state!).TryRemove(
                    pairExecutionId, out _),
                _pumps,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task RunPairAsync(Guid pairExecutionId, CancellationToken cancellationToken)
    {
        try
        {
            PreparedPass1ExecutionActivator.ActivationSlot? slot = activator.GetSlot(pairExecutionId);
            if (slot is null)
            {
                return;
            }

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PreparedPass1JobRunner>()
                .RunPairAsync(slot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Prepared Pass 1 pair pump stopped unexpectedly for {PairExecutionId}.", pairExecutionId);
        }
    }
}
