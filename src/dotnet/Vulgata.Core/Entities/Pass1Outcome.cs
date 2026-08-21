namespace Vulgata.Core.Entities;

public enum Pass1OutcomeStatus
{
    Succeeded,
    CompletedWithWarnings,
    FailedPass1,
}

public sealed class Pass1Outcome
{
    private Pass1Outcome()
    {
    }

    public Guid PairExecutionId { get; private set; }
    public Pass1OutcomeStatus Status { get; private set; }
    public int DispatchedCount { get; private set; }
    public int CompletedCount { get; private set; }
    public int OrdinaryFailureCount { get; private set; }
    public int OrdinaryFailureBudget { get; private set; }
    public int HighValueSeedCount { get; private set; }
    public int SucceededHighValueSeedCount { get; private set; }
    public string? ReconciliationFingerprint { get; private set; }
    public int ReconciledEdgeCount { get; private set; }
    public string OutcomeFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; private set; }
}

public enum Pass1OrdinaryFailureCategory
{
    ProviderTransient,
    WorkerTransient,
    InvalidResult,
}

public sealed class Pass1FailureInventoryEntry
{
    private Pass1FailureInventoryEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid PairExecutionId { get; private set; }
    public Guid FileCodeUnitId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public int PairSelectionOrdinal { get; private set; }
    public int ManifestOrdinal { get; private set; }
    public int SegmentOrdinal { get; private set; }
    public string RepositoryRelativePath { get; private set; } = string.Empty;
    public Pass1OrdinaryFailureCategory FailureCategory { get; private set; }
    public string FailureCode { get; private set; } = string.Empty;
}
