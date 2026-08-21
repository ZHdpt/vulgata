namespace Vulgata.Core.Entities;

public enum Pass1PairExecutionPhase
{
    Running,
    ProbingProvider,
    PausedProviderUnavailable,
    PausedOperatorCorrection,
    PausedInspection,
    Reconciling,
    ReadyForResolution,
    FailedPass1,
    FailedIntegrity,
    FailedSecurity,
    Cancelled,
}

public enum Pass1CircuitState
{
    Closed,
    Probing,
    Paused,
}

public enum Pass1PairReasonCategory
{
    ProviderUnavailable,
    OperatorCorrection,
    Inspection,
    Pass1Policy,
    Integrity,
    Security,
    Cancellation,
}

public sealed class Pass1PairExecution
{
    private Pass1PairExecution()
    {
    }

    public Guid Id { get; private set; }
    public Guid CoordinationId { get; private set; }
    public Guid SystemId { get; private set; }
    public string PairPackageFingerprint { get; private set; } = string.Empty;
    public string PairSelectionFingerprint { get; private set; } = string.Empty;
    public Guid ManifestOneId { get; private set; }
    public Guid ManifestTwoId { get; private set; }
    public string Pass1PairFingerprint { get; private set; } = string.Empty;
    public string PromptIdentityOne { get; private set; } = string.Empty;
    public string PromptIdentityTwo { get; private set; } = string.Empty;
    public string DocumentSchemaIdentity { get; private set; } = string.Empty;
    public string AnalysisRuntimeIdentityHashOne { get; private set; } = string.Empty;
    public string AnalysisRuntimeIdentityHashTwo { get; private set; } = string.Empty;
    public int InitialConcurrencyLimit { get; private set; }
    public int ConcurrencyLimit { get; private set; }
    public long ConcurrencyRevision { get; private set; }
    public DateTimeOffset ConcurrencyUpdatedAt { get; private set; }
    public int DispatchedFileCount { get; private set; }
    public int HighValueSeedCount { get; private set; }
    public Pass1PairExecutionPhase Phase { get; private set; }
    public Pass1CircuitState CircuitState { get; private set; }
    public int ProbeOrdinal { get; private set; }
    public DateTimeOffset? NextProbeAt { get; private set; }
    public DateTimeOffset? RetryAfterAt { get; private set; }
    public Pass1PairReasonCategory? ReasonCategory { get; private set; }
    public string? ReasonCode { get; private set; }
    public long StateVersion { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? TerminalAt { get; private set; }
}
