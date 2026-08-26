using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Web.Data;

public sealed class PreparedPass2JobRunner(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PreparedPass2JobRunner> logger)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(30);

    internal async Task RunExecutionAsync(
        PreparedPass2ExecutionActivator.ActivationSlot slot,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            PreparedPass2ClaimResult claim;
            await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
            {
                claim = await scope.ServiceProvider.GetRequiredService<PreparedPass2Store>()
                    .ClaimNextAsync(
                        slot.Pass2ExecutionId,
                        timeProvider.GetUtcNow(),
                        LeaseDuration,
                        cancellationToken).ConfigureAwait(false);
            }

            if (claim.Outcome == PreparedPass2ClaimOutcome.Claimed)
            {
                await RunClaimAsync(claim, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (claim.Outcome == PreparedPass2ClaimOutcome.Active)
            {
                DateTimeOffset now = timeProvider.GetUtcNow();
                if (claim.NextEligibleAt is { } due && due > now)
                {
                    await WaitForWakeOrDueAsync(slot, due - now, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }
            }
            else if (claim.Outcome == PreparedPass2ClaimOutcome.Ineligible &&
                claim.NextEligibleAt is { } nextEligibleAt)
            {
                TimeSpan delay = nextEligibleAt - timeProvider.GetUtcNow();
                await WaitForWakeOrDueAsync(
                    slot,
                    delay > TimeSpan.Zero ? delay : TimeSpan.Zero,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            return;
        }
    }

    private async Task RunClaimAsync(
        PreparedPass2ClaimResult claim,
        CancellationToken cancellationToken)
    {
        if (claim.JobId is not Guid jobId || claim.LeaseToken is not Guid leaseToken)
        {
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        PreparedPass2DispatchHydrationResult hydration = await services
            .GetRequiredService<IPreparedPass2DispatchRequestSource>()
            .HydrateAsync(
                claim.ExecutionId,
                jobId,
                claim.JobStateVersion,
                leaseToken,
                cancellationToken).ConfigureAwait(false);
        if (hydration.Outcome != PreparedPass2DispatchHydrationOutcome.Hydrated ||
            hydration.Request is not { } request)
        {
            bool securityRefused =
                hydration.Outcome == PreparedPass2DispatchHydrationOutcome.SecurityRefused;
            await RecordFailureAsync(
                services,
                claim,
                securityRefused ? ScanJobFailureCategory.Security : ScanJobFailureCategory.Integrity,
                securityRefused ? "authority-revoked" : "invalid-binding",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        PreparedPass2DispatchResult result;
        try
        {
            result = await services.GetRequiredService<IPreparedPass2WorkerDispatcher>()
                .DispatchAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Pass 2 job dispatch failed for {ExecutionId}/{JobId} with {FailureCategory}/{FailureCode}.",
                claim.ExecutionId,
                jobId,
                ScanJobFailureCategory.Unclassified,
                "worker-failed");
            await RecordFailureAsync(
                services, claim, ScanJobFailureCategory.Unclassified,
                "worker-failed", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!result.Accepted)
        {
            logger.LogWarning(
                "Pass 2 job dispatch refused {ExecutionId}/{JobId} with {FailureCode}.",
                claim.ExecutionId,
                jobId,
                result.ReasonCode ?? "worker-failed");
            bool providerUnavailable = string.Equals(
                result.ReasonCode, "provider-unavailable", StringComparison.Ordinal) ||
                string.Equals(result.ReasonCode, "provider-timeout", StringComparison.Ordinal);
            await RecordFailureAsync(
                services,
                claim,
                providerUnavailable
                    ? ScanJobFailureCategory.ProviderTransient
                    : ScanJobFailureCategory.JobPermanent,
                result.ReasonCode ?? "worker-failed",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RecordFailureAsync(
        IServiceProvider services,
        PreparedPass2ClaimResult claim,
        ScanJobFailureCategory category,
        string code,
        CancellationToken cancellationToken)
    {
        if (claim.JobId is Guid jobId && claim.LeaseToken is Guid leaseToken)
        {
            _ = await services.GetRequiredService<PreparedPass2Store>()
                .RecordDispatchFailureAsync(
                    claim.ExecutionId,
                    jobId,
                    claim.JobStateVersion,
                    leaseToken,
                    category,
                    code,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WaitForWakeOrDueAsync(
        PreparedPass2ExecutionActivator.ActivationSlot slot,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task wake = slot.WaitAsync(linked.Token);
        Task due = Task.Delay(delay, linked.Token);
        Task completed = await Task.WhenAny(wake, due).ConfigureAwait(false);
        linked.Cancel();
        try
        {
            await completed.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
        }
    }
}
