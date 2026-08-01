using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Deterministic application-code Sequential Scan Runner. Consumes one
/// <see cref="ApprovedPreScanHandoff"/> for a prepared Repository and drives
/// one Scan execution from <c>pre_scan_approved</c> through typed Worker
/// Agent job dispatch, durable phase transitions, and terminal success or
/// failure. The runner is application code, not an LLM agent; all dispatch
/// goes through <see cref="IWorkerAgentDispatcher"/>. No concurrency, lease,
/// replay, failover, retry, or multi-Repository scheduling is introduced.
/// </summary>
public sealed class SequentialScanRunner
{
    private readonly IApprovedPreScanHandoffSource _handoffSource;
    private readonly IScanExecutionStore _executionStore;
    private readonly ICodeUnitSource _codeUnitSource;
    private readonly IWorkerAgentDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;

    public SequentialScanRunner(
        IApprovedPreScanHandoffSource handoffSource,
        IScanExecutionStore executionStore,
        ICodeUnitSource codeUnitSource,
        IWorkerAgentDispatcher dispatcher,
        TimeProvider timeProvider)
    {
        _handoffSource = handoffSource ?? throw new ArgumentNullException(nameof(handoffSource));
        _executionStore = executionStore ?? throw new ArgumentNullException(nameof(executionStore));
        _codeUnitSource = codeUnitSource ?? throw new ArgumentNullException(nameof(codeUnitSource));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Start a deadline Scan from an approved Pre-Scan Handoff. Refuses with
    /// <see cref="ScanExecutionFailureKind.MissingPrerequisite"/> if the
    /// handoff is missing, not approved, or missing a Profiled Revision.
    /// Refuses with
    /// <see cref="ScanExecutionFailureKind.DuplicateActiveScan"/> if an active
    /// Scan execution already exists. On success, creates exactly one
    /// <see cref="ScanExecution"/> in
    /// <see cref="ScanExecutionPhase.Pass1Planning"/> and persists it
    /// atomically. Returns the new execution identifier.
    /// </summary>
    public async Task<Guid> StartAsync(
        Guid repositoryId,
        Guid scanRunId,
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        ApprovedPreScanHandoff? handoff = await _handoffSource.GetAsync(
            repositoryId,
            scanRunId,
            cancellationToken).ConfigureAwait(false);

        if (handoff is null ||
            handoff.State != PreScanApprovalState.PreScanApproved ||
            string.IsNullOrWhiteSpace(handoff.ProfiledRevision))
        {
            throw new InvalidOperationException(
                BuildRefusalMessage(ScanExecutionFailureKind.MissingPrerequisite, repositoryId));
        }

        ScanExecution? active = await _executionStore.FindActiveAsync(
            repositoryId,
            scanRunId,
            cancellationToken).ConfigureAwait(false);
        if (active is not null)
        {
            throw new InvalidOperationException(
                BuildRefusalMessage(ScanExecutionFailureKind.DuplicateActiveScan, repositoryId));
        }

        Guid executionId = Guid.NewGuid();
        ScanExecution execution = ScanExecution.Start(
            executionId,
            repositoryId,
            systemId,
            scanRunId,
            handoff.ResultId,
            handoff.ProfiledRevision,
            _timeProvider.GetUtcNow());

        await _executionStore.PersistAsync(execution, cancellationToken).ConfigureAwait(false);
        return executionId;
    }

    /// <summary>
    /// Read the bounded <see cref="ScanExecutionStateView"/> for a Scan
    /// execution. On a restart re-read of a <c>Pass1Running</c> row with a
    /// non-null in-flight job, the bounded restart indicator is incremented
    /// and persisted, but the LLM job is not resumed and no result is
    /// fabricated.
    /// </summary>
    public async Task<ScanExecutionStateView> ReadStateAsync(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default)
    {
        return await _executionStore.ReadStateAsync(scanExecutionId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildRefusalMessage(ScanExecutionFailureKind kind, Guid repositoryId) =>
        kind switch
        {
            ScanExecutionFailureKind.MissingPrerequisite =>
                $"仓库 {repositoryId} 的前置条件不满足,无法启动扫描。",
            ScanExecutionFailureKind.DuplicateActiveScan =>
                $"仓库 {repositoryId} 已有进行中的扫描,无法重复启动。",
            _ =>
                $"仓库 {repositoryId} 的扫描启动被拒绝。",
        };
}
