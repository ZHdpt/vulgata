namespace Vulgata.Core.DomainServices;

/// <summary>
/// Write/read port for the durable <see cref="Entities.ScanExecution"/>
/// record. Implemented by Infrastructure using EF Core against
/// <c>VulgataDbContext</c>. Every phase transition is persisted atomically.
/// </summary>
public interface IScanExecutionStore
{
    /// <summary>
    /// Load a <see cref="Entities.ScanExecution"/> by its identifier, or
    /// <c>null</c> if not found.
    /// </summary>
    Task<Entities.ScanExecution?> GetAsync(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find an active (non-terminal) <see cref="Entities.ScanExecution"/> for
    /// the given Repository and ScanRun, or <c>null</c> if none. Used by the
    /// runner to refuse duplicate active Scans.
    /// </summary>
    Task<Entities.ScanExecution?> FindActiveAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist a <see cref="Entities.ScanExecution"/> phase transition
    /// atomically. The row is locked inside the transaction before mutation.
    /// </summary>
    Task PersistAsync(
        Entities.ScanExecution execution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read the bounded <see cref="ScanExecutionStateView"/> for a Scan
    /// execution. On a restart re-read of a <c>Pass1Running</c> row with a
    /// non-null <c>InFlightJobId</c>, the bounded restart indicator is
    /// incremented and persisted, but the LLM job is not resumed and no
    /// result is fabricated.
    /// </summary>
    Task<ScanExecutionStateView> ReadStateAsync(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default);
}
