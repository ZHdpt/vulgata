using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

public interface IPreparedPass1ProviderAvailabilityProbe
{
    Task<bool> IsAvailableAsync(Guid pairExecutionId, CancellationToken cancellationToken);
}

/// <summary>
/// Owns pair-wide restart/manual-resume orchestration below the HTTP boundary.
/// </summary>
public sealed class PreparedPass1Coordinator(
    IPreparedPass1Store store,
    IPreparedPass1ProviderAvailabilityProbe providerProbe,
    TimeProvider timeProvider)
{
    public async Task<PreparedPass1RecoveryResult> ResumeAsync(
        Guid pairExecutionId,
        PreparedScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled(pairExecutionId);
        }

        bool providerAvailable;
        try
        {
            providerAvailable = await providerProbe.IsAvailableAsync(pairExecutionId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled(pairExecutionId);
        }

        return await store.RecoverAsync(
            pairExecutionId,
            authorizationScope,
            providerAvailable,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    internal static TimeSpan GetProviderProbeWindow(int zeroBasedOrdinal) =>
        WorkerAgentProviderHealthProbe.GetProbeDelay(checked(zeroBasedOrdinal + 1));

    private static PreparedPass1RecoveryResult Cancelled(Guid pairExecutionId) => new(
        PreparedPass1RecoveryOutcome.Cancelled,
        DurableScanReason.Cancelled,
        pairExecutionId == Guid.Empty ? null : pairExecutionId,
        null,
        null,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        null);
}
