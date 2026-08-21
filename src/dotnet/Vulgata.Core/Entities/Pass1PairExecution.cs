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

    public static Pass1PairExecution Create(
        Guid id,
        Guid coordinationId,
        Guid systemId,
        string pairPackageFingerprint,
        string pairSelectionFingerprint,
        Guid manifestOneId,
        Guid manifestTwoId,
        string pass1PairFingerprint,
        string promptIdentityOne,
        string promptIdentityTwo,
        string documentSchemaIdentity,
        string analysisRuntimeIdentityHashOne,
        string analysisRuntimeIdentityHashTwo,
        int concurrencyLimit,
        int dispatchedFileCount,
        int highValueSeedCount,
        DateTimeOffset startedAt)
    {
        if (id == Guid.Empty || coordinationId == Guid.Empty || systemId == Guid.Empty ||
            manifestOneId == Guid.Empty || manifestTwoId == Guid.Empty || manifestOneId == manifestTwoId)
        {
            throw new ArgumentException("Complete and distinct pair identities are required.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(concurrencyLimit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(concurrencyLimit, 4);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dispatchedFileCount);
        ArgumentOutOfRangeException.ThrowIfNegative(highValueSeedCount);
        if (highValueSeedCount > dispatchedFileCount || startedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The pair count or timestamp binding is invalid.");
        }

        return new Pass1PairExecution
        {
            Id = id,
            CoordinationId = coordinationId,
            SystemId = systemId,
            PairPackageFingerprint = RequireHash(pairPackageFingerprint),
            PairSelectionFingerprint = RequireHash(pairSelectionFingerprint),
            ManifestOneId = manifestOneId,
            ManifestTwoId = manifestTwoId,
            Pass1PairFingerprint = RequireHash(pass1PairFingerprint),
            PromptIdentityOne = RequireText(promptIdentityOne),
            PromptIdentityTwo = RequireText(promptIdentityTwo),
            DocumentSchemaIdentity = RequireText(documentSchemaIdentity),
            AnalysisRuntimeIdentityHashOne = RequireHash(analysisRuntimeIdentityHashOne),
            AnalysisRuntimeIdentityHashTwo = RequireHash(analysisRuntimeIdentityHashTwo),
            InitialConcurrencyLimit = concurrencyLimit,
            ConcurrencyLimit = concurrencyLimit,
            ConcurrencyUpdatedAt = startedAt,
            DispatchedFileCount = dispatchedFileCount,
            HighValueSeedCount = highValueSeedCount,
            Phase = Pass1PairExecutionPhase.Running,
            CircuitState = Pass1CircuitState.Closed,
            StartedAt = startedAt,
            UpdatedAt = startedAt,
        };
    }

    public void UpdateConcurrency(
        long expectedStateVersion,
        int requestedLimit,
        DateTimeOffset changedAt)
    {
        if (StateVersion != expectedStateVersion)
        {
            throw new InvalidOperationException("Concurrency update requires the current pair state version.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(requestedLimit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(requestedLimit, 4);
        if (requestedLimit == ConcurrencyLimit)
        {
            throw new InvalidOperationException("An effective concurrency change is required.");
        }

        if (Phase is Pass1PairExecutionPhase.Reconciling or
            Pass1PairExecutionPhase.ReadyForResolution or
            Pass1PairExecutionPhase.FailedPass1 or
            Pass1PairExecutionPhase.FailedIntegrity or
            Pass1PairExecutionPhase.FailedSecurity or
            Pass1PairExecutionPhase.Cancelled)
        {
            throw new InvalidOperationException("Concurrency cannot change after Pass 1 enters reconciliation or a terminal phase.");
        }

        if (changedAt.Offset != TimeSpan.Zero || changedAt < UpdatedAt)
        {
            throw new ArgumentException("The concurrency timestamp must be a current UTC value.", nameof(changedAt));
        }

        ConcurrencyLimit = requestedLimit;
        ConcurrencyRevision++;
        StateVersion++;
        ConcurrencyUpdatedAt = changedAt;
        UpdatedAt = changedAt;
    }

    private static string RequireHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character)))
        {
            throw new ArgumentException("A lower-case SHA-256 fingerprint is required.", nameof(value));
        }

        return value;
    }

    private static string RequireText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}
