using System.Collections.Concurrent;
using Vulgata.Core.Entities;

namespace Vulgata.Web.Data;

public sealed class PreparedPass2ExecutionHostedService(
    PreparedPass2ExecutionActivator activator,
    IServiceScopeFactory scopeFactory,
    ILogger<PreparedPass2ExecutionHostedService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Task> _pumps = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Guid executionId in activator.ReadAllAsync(stoppingToken))
        {
            if (_pumps.TryGetValue(executionId, out Task? current) && !current.IsCompleted)
            {
                continue;
            }

            Task pump = RunAsync(executionId, stoppingToken);
            _pumps[executionId] = pump;
            _ = pump.ContinueWith(
                (_, state) => ((ConcurrentDictionary<Guid, Task>)state!).TryRemove(
                    executionId, out _),
                _pumps,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task RunAsync(Guid executionId, CancellationToken cancellationToken)
    {
        try
        {
            PreparedPass2ExecutionActivator.ActivationSlot? slot = activator.GetSlot(executionId);
            if (slot is null)
            {
                return;
            }

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PreparedPass2JobRunner>()
                .RunExecutionAsync(slot, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            logger.LogError(
                "Pass 2 execution pump failed for {ExecutionId} with {FailureCategory}/{FailureCode}.",
                executionId,
                ScanJobFailureCategory.Unclassified,
                "worker-failed");
        }
    }
}
