namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>Producer-only initial observation; none of these states grants execution authority.</summary>
public enum BusinessFinalizationInitialReadKind
{
    /// <summary>Return Result, including an authenticated durable replay or explicit conflict/denial; do not generate.</summary>
    NoDispatchResult,
    /// <summary>Exact binding, current authority and storage read succeeded, but no operation row was observed.</summary>
    ObservedAbsent,
    /// <summary>The exact observation could not be established; never generate.</summary>
    Unavailable
}

/// <summary>
/// NoDispatchResult requires a nonnull authorized replay/conflict/denial Result and never permits generation.
/// ObservedAbsent and Unavailable require a null Result. Consumers reject inconsistent combinations.
/// Absence does not prove no earlier write can submit.
/// This internal-purpose result is not an ordinary Knowledge read or UI transport contract.
/// </summary>
public sealed record BusinessFinalizationInitialRead(BusinessFinalizationInitialReadKind Kind,
    BusinessFinalizationResult? Result);

/// <summary>
/// Implemented by the same owner store as IOctoberFlowPublicationStore, with the same exact binding
/// and live all-contributor authority. Used only inside an independently authorized original-run
/// callback. A successful absent observation can precede first generation only under that already
/// issued, consumed original slot; it never grants a slot, takeover, retry or restart.
/// Public LookupAsync continues to return CommitIndeterminate for missing or unavailable storage.
/// </summary>
public interface IOctoberFlowPublicationInitialRead
{
    Task<BusinessFinalizationInitialRead> ReadForOriginalDispatchAsync(CorpusHandle corpus,
        OperationKey operation, string canonicalPayloadDigest, CancellationToken cancellationToken = default);
}

/// <summary>
/// Scan privately issues and retains this slot only for its original live BeginGenerate admission
/// (including reconciliation of that same original submitted attempt), never from a receipt alone.
/// The slot captures the exact immutable run/operation/corpus/request digest and selection plus the
/// same execution-scope instance and host run token. Wrong bindings cannot invoke or stop the slot.
/// Validate binding/current host eligibility, then atomically install the retained task and consume
/// the slot before invoking originalWork outside the lock. Every outcome, exception or cancellation
/// consumes it; no replacement process/instance or absent lookup can obtain another first callback.
/// Exact duplicates join the retained task and do not replace its callback. ALL callers, including
/// the first, use waitCancellation only to cancel their wait; originalWork receives only the host
/// run token. Keep owned work/lifetimes alive when no waiters remain. No provider permit is held here.
/// Returned cached results are private producer data: the Business wrapper must perform fresh owner
/// lookup/disclosure authorization before every external return, including the first and duplicates.
/// The slot grants neither durable-write authority nor a provider budget; F1/F2 remain mandatory.
/// </summary>
public interface IOctoberOriginalBusinessFinalizationSlot
{
    Task<BusinessFinalizationResult> RunOnceAsync(FinalizationRequest request,
        Func<CancellationToken, Task<BusinessFinalizationResult>> originalWork,
        CancellationToken waitCancellation = default);
}
