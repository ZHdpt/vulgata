namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>
/// Host-owned eligibility of an already completed Business result within the original run lifetime.
/// This is not provider admission and grants no request, token, capacity or deadline allowance.
/// </summary>
public interface IOctoberBusinessHandoffLifetime
{
    /// <summary>Cached original run stop/deadline signal; remains observable after attempt settlement/disposal.</summary>
    CancellationToken HandoffCancellation { get; }

    /// <summary>
    /// Synchronously checks the exact run, finalization operation, provider profile and eligible
    /// CL-and-BL Business phase. Stop, disposal, deadline or authority denial is monotonic.
    /// Unknown usage or lack of future provider budget/capacity alone does not invalidate an
    /// otherwise eligible completed result. F1 still independently guards durable acceptance.
    /// </summary>
    ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey finalizationOperation,
        string providerProfileId);
}
