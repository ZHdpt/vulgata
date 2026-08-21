namespace Vulgata.Core.Entities;

/// <summary>
/// Closed phase allowlist for a <see cref="ScanExecution"/>. Relational
/// check constraint <c>CK_ScanExecutions_Phase_Closed</c> permits only these
/// six values. Terminal phases (<see cref="Completed"/>,
/// <see cref="Failed"/>, <see cref="Cancelled"/>) are immutable.
/// </summary>
public enum ScanExecutionPhase
{
    /// <summary>Initial phase: the Scan execution record has been created
    /// from an approved Pre-Scan Handoff but Pass 1 dispatch has not yet
    /// started.</summary>
    Pass1Planning,

    /// <summary>Pass 1 dispatch is in progress: typed Worker Agent jobs are
    /// being dispatched one at a time.</summary>
    Pass1Running,

    /// <summary>All Pass 1 jobs have returned Valid results; the runner is
    /// finalizing.</summary>
    Pass1Completed,

    /// <summary>Terminal success: the Scan completed with all jobs
    /// Valid.</summary>
    Completed,

    /// <summary>Terminal failure: a Worker Agent job returned an invalid
    /// result or a boundary rejection occurred.</summary>
    Failed,

    /// <summary>Terminal cancellation: an operator cancelled the Scan. No
    /// LLM resume.</summary>
    Cancelled,

    /// <summary>New-path durable inputs are prepared; no Worker Agent job is
    /// claimed or dispatched.</summary>
    Prepared,

    /// <summary>New-path per-file work is represented by durable Scan Job
    /// Executions.</summary>
    Running,

    /// <summary>New-path progress is paused because its provider is
    /// unavailable.</summary>
    PausedProviderUnavailable,

    /// <summary>New-path progress requires operator correction before manual
    /// resume.</summary>
    PausedOperatorCorrection,

    /// <summary>Terminal new-path success with bounded warnings.</summary>
    CompletedWithWarnings,

    /// <summary>Terminal new-path integrity failure.</summary>
    FailedIntegrity,

    /// <summary>Terminal new-path security failure.</summary>
    FailedSecurity,

    /// <summary>Terminal new-path Pass 1 policy failure.</summary>
    FailedPass1,
}
