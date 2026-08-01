using System.Globalization;
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
    private readonly IWorkerAgentResultCommitter? _resultCommitter;
    private readonly PreparedPass2Flow? _preparedPass2Flow;
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

    public SequentialScanRunner(
        IApprovedPreScanHandoffSource handoffSource,
        IScanExecutionStore executionStore,
        ICodeUnitSource codeUnitSource,
        IWorkerAgentDispatcher dispatcher,
        IWorkerAgentResultCommitter resultCommitter,
        TimeProvider timeProvider)
    {
        _handoffSource = handoffSource ?? throw new ArgumentNullException(nameof(handoffSource));
        _executionStore = executionStore ?? throw new ArgumentNullException(nameof(executionStore));
        _codeUnitSource = codeUnitSource ?? throw new ArgumentNullException(nameof(codeUnitSource));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _resultCommitter = resultCommitter ?? throw new ArgumentNullException(nameof(resultCommitter));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public SequentialScanRunner(
        IApprovedPreScanHandoffSource handoffSource,
        IScanExecutionStore executionStore,
        ICodeUnitSource codeUnitSource,
        IWorkerAgentDispatcher dispatcher,
        IWorkerAgentResultCommitter resultCommitter,
        PreparedPass2Flow preparedPass2Flow,
        TimeProvider timeProvider)
        : this(
            handoffSource,
            executionStore,
            codeUnitSource,
            dispatcher,
            resultCommitter,
            timeProvider)
    {
        _preparedPass2Flow = preparedPass2Flow ??
            throw new ArgumentNullException(nameof(preparedPass2Flow));
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
    /// fabricated. The runner orchestrates the restart-observe step in the
    /// domain layer; the store is a pure CRUD port.
    /// </summary>
    public async Task<ScanExecutionStateView> ReadStateAsync(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default)
    {
        ScanExecution? execution = await _executionStore.GetAsync(
            scanExecutionId, cancellationToken).ConfigureAwait(false);
        if (execution is null)
        {
            throw new InvalidOperationException(
                $"找不到标识为 {scanExecutionId} 的扫描执行记录。");
        }

        // Observe a restart re-read: if a job is in flight, increment the
        // bounded indicator and persist. The LLM job is NOT resumed and no
        // result is fabricated.
        int indicatorBefore = execution.RestartIndicator;
        execution.ObserveRestart();
        if (execution.RestartIndicator != indicatorBefore)
        {
            await _executionStore.PersistAsync(execution, cancellationToken)
                .ConfigureAwait(false);
        }

        return new ScanExecutionStateView(
            execution.Id,
            execution.RepositoryId,
            execution.ScanRunId,
            execution.Phase,
            execution.InFlightJobId,
            execution.CompletedJobCount,
            execution.TotalJobCount,
            execution.RestartIndicator,
            execution.TerminalMessage,
            execution.TerminalReasonKind,
            execution.StartedAt,
            execution.TerminalAt);
    }

    /// <summary>
    /// Advance a Scan execution from <see cref="ScanExecutionPhase.Pass1Planning"/>
    /// through <see cref="ScanExecutionPhase.Pass1Running"/>, dispatching one
    /// bounded typed job packet per prepared Code Unit strictly one at a time
    /// through <see cref="IWorkerAgentDispatcher"/>. On <c>Valid</c> results,
    /// records progress; on <c>SchemaInvalid</c>/<c>Error</c>/<c>Unbounded</c>,
    /// records failure and stops. When all jobs return <c>Valid</c>,
    /// finalizes through <see cref="ScanExecutionPhase.Pass1Completed"/> to
    /// <see cref="ScanExecutionPhase.Completed"/>. No concurrency, lease,
    /// replay, failover, or retry.
    /// </summary>
    public async Task RunPass1Async(
        Guid scanExecutionId,
        CancellationToken cancellationToken = default)
    {
        await RunPass1CoreAsync(scanExecutionId, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Run Pass 1 with caller authorization. Every valid result is committed
    /// through the knowledge store before durable progress is recorded, and
    /// the completed pass stops at the nonterminal Pass1Completed boundary.
    /// </summary>
    public async Task RunPass1Async(
        Guid scanExecutionId,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (_resultCommitter is null)
        {
            throw new InvalidOperationException(
                "The Sequential Scan Runner has no result committer configured.");
        }

        await RunPass1CoreAsync(scanExecutionId, scope, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Dispatch exactly one already-resolved prepared Pass 2 flow and expose
    /// terminal success only after its Business Logic Document commits.
    /// </summary>
    public async Task RunPreparedPass2Async(
        Guid scanExecutionId,
        ResolvedPreparedPass2Flow resolvedFlow,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolvedFlow);
        ArgumentNullException.ThrowIfNull(scope);
        if (_resultCommitter is null)
        {
            throw new InvalidOperationException(
                "The Sequential Scan Runner has no result committer configured.");
        }

        ScanExecution? execution = await _executionStore.GetAsync(
            scanExecutionId, cancellationToken).ConfigureAwait(false);
        if (execution is null)
        {
            throw new InvalidOperationException(
                $"找不到标识为 {scanExecutionId} 的扫描执行记录。");
        }

        Pass2BusinessLogicJobPayload payload = Pass2BusinessLogicJobPayload.Create(
            resolvedFlow.Flow,
            resolvedFlow.CodeLogicDocuments);
        WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
            Guid.NewGuid(),
            WorkerAgentJobKind.Pass2BusinessLogicDocument,
            execution.Id,
            execution.RepositoryId,
            execution.SystemId,
            execution.ScanRunId,
            execution.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            payload);

        execution.BeginPass2(packet.JobId);
        await _executionStore.PersistAsync(execution, cancellationToken).ConfigureAwait(false);

        WorkerAgentResultEnvelope result;
        try
        {
            result = await _dispatcher.DispatchAsync(packet, scope, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await FailPass2Async(
                execution,
                packet.JobId,
                ScanExecutionFailureKind.Error,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (result.Outcome != WorkerAgentResultOutcome.Valid)
        {
            ScanExecutionFailureKind kind = result.Outcome switch
            {
                WorkerAgentResultOutcome.SchemaInvalid => ScanExecutionFailureKind.SchemaInvalid,
                WorkerAgentResultOutcome.Unbounded => ScanExecutionFailureKind.Unbounded,
                _ => ScanExecutionFailureKind.Error,
            };
            await FailPass2Async(execution, packet.JobId, kind, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        WorkerAgentResultCommitOutcome commitOutcome;
        try
        {
            commitOutcome = await _resultCommitter.CommitAsync(
                packet,
                result,
                scope,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            commitOutcome = WorkerAgentResultCommitOutcome.Failed;
        }

        if (commitOutcome != WorkerAgentResultCommitOutcome.Committed)
        {
            await FailPass2Async(
                execution,
                packet.JobId,
                ScanExecutionFailureKind.Error,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        execution.CompletePass2(
            packet.JobId,
            BuildPass2SuccessMessage(execution.RepositoryId),
            _timeProvider.GetUtcNow());
        await _executionStore.PersistAsync(execution, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunPass1CoreAsync(
        Guid scanExecutionId,
        RepositoryAccessScope? scope,
        CancellationToken cancellationToken)
    {
        ScanExecution? execution = await _executionStore.GetAsync(
            scanExecutionId, cancellationToken).ConfigureAwait(false);
        if (execution is null)
        {
            throw new InvalidOperationException(
                $"找不到标识为 {scanExecutionId} 的扫描执行记录。");
        }

        IReadOnlyList<CodeUnit> codeUnits = await _codeUnitSource.GetPreparedAsync(
            execution.ScanRunId, execution.RepositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (codeUnits.Count == 0)
        {
            throw new InvalidOperationException(
                $"仓库 {execution.RepositoryId} 没有已准备好的代码单元,无法启动 Pass 1。");
        }

        List<CodeUnit> ordered = OrderCodeUnits(codeUnits);
        execution.Plan(ordered.Count, _timeProvider.GetUtcNow());
        await _executionStore.PersistAsync(execution, cancellationToken).ConfigureAwait(false);

        foreach (CodeUnit unit in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WorkerAgentJobPacket packet;
            try
            {
                packet = BuildPacket(execution, unit);
            }
            catch (ArgumentException)
            {
                ScanExecutionFailureKind kind = ClassifyPacketRejection(unit);
                execution.RecordBoundaryRejection(
                    kind,
                    BuildBoundaryRejectionMessage(kind, execution.RepositoryId),
                    _timeProvider.GetUtcNow());
                await _executionStore.PersistAsync(execution, cancellationToken).ConfigureAwait(false);
                return;
            }

            Guid jobId = packet.JobId;
            execution.Advance(jobId);
            await _executionStore.PersistAsync(execution, cancellationToken).ConfigureAwait(false);

            DateTimeOffset dispatchedAt = _timeProvider.GetUtcNow();
            WorkerAgentResultEnvelope result;
            try
            {
                result = scope is null
                    ? await _dispatcher.DispatchAsync(packet, cancellationToken)
                        .ConfigureAwait(false)
                    : await _dispatcher.DispatchAsync(packet, scope, cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                execution.RecordFailure(
                    jobId,
                    ScanExecutionFailureKind.Error,
                    BuildFailureMessage(
                        ScanExecutionFailureKind.Error,
                        execution.RepositoryId),
                    _timeProvider.GetUtcNow());
                await _executionStore.PersistAsync(execution, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            DateTimeOffset receivedAt = _timeProvider.GetUtcNow();
            switch (result.Outcome)
            {
                case WorkerAgentResultOutcome.Valid:
                    if (_resultCommitter is not null && scope is not null)
                    {
                        WorkerAgentResultCommitOutcome commitOutcome =
                            await _resultCommitter.CommitAsync(
                                packet,
                                result,
                                scope,
                                cancellationToken).ConfigureAwait(false);
                        if (commitOutcome != WorkerAgentResultCommitOutcome.Committed)
                        {
                            execution.RecordFailure(
                                jobId,
                                ScanExecutionFailureKind.Error,
                                BuildFailureMessage(
                                    ScanExecutionFailureKind.Error,
                                    execution.RepositoryId),
                                _timeProvider.GetUtcNow());
                            await _executionStore.PersistAsync(execution, cancellationToken)
                                .ConfigureAwait(false);
                            return;
                        }
                    }

                    execution.RecordValid(jobId);
                    await PersistValidProgressAsync(
                        execution,
                        packet,
                        result,
                        scope,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case WorkerAgentResultOutcome.SchemaInvalid:
                    execution.RecordFailure(
                        jobId,
                        ScanExecutionFailureKind.SchemaInvalid,
                        BuildFailureMessage(ScanExecutionFailureKind.SchemaInvalid, execution.RepositoryId),
                        _timeProvider.GetUtcNow());
                    await _executionStore.PersistAsync(execution, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                case WorkerAgentResultOutcome.Error:
                    execution.RecordFailure(
                        jobId,
                        ScanExecutionFailureKind.Error,
                        BuildFailureMessage(ScanExecutionFailureKind.Error, execution.RepositoryId),
                        _timeProvider.GetUtcNow());
                    await _executionStore.PersistAsync(execution, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                case WorkerAgentResultOutcome.Unbounded:
                    execution.RecordFailure(
                        jobId,
                        ScanExecutionFailureKind.Unbounded,
                        BuildFailureMessage(ScanExecutionFailureKind.Unbounded, execution.RepositoryId),
                        _timeProvider.GetUtcNow());
                    await _executionStore.PersistAsync(execution, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                default:
                    execution.RecordFailure(
                        jobId,
                        ScanExecutionFailureKind.Error,
                        BuildFailureMessage(ScanExecutionFailureKind.Error, execution.RepositoryId),
                        _timeProvider.GetUtcNow());
                    await _executionStore.PersistAsync(execution, cancellationToken)
                        .ConfigureAwait(false);
                    return;
            }
        }

        if (_resultCommitter is not null && scope is not null)
        {
            execution.CompletePass1();
        }
        else
        {
            execution.Finalize(
                BuildSuccessMessage(execution.RepositoryId, ordered.Count),
                _timeProvider.GetUtcNow());
        }

        await _executionStore.PersistAsync(execution, cancellationToken).ConfigureAwait(false);
    }

    private async Task PersistValidProgressAsync(
        ScanExecution execution,
        WorkerAgentJobPacket packet,
        WorkerAgentResultEnvelope result,
        RepositoryAccessScope? scope,
        CancellationToken cancellationToken)
    {
        try
        {
            await _executionStore.PersistAsync(execution, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch when (_resultCommitter is not null && scope is not null)
        {
            WorkerAgentResultCommitOutcome replay = await _resultCommitter.CommitAsync(
                packet,
                result,
                scope,
                cancellationToken).ConfigureAwait(false);
            if (replay != WorkerAgentResultCommitOutcome.Committed)
            {
                throw new InvalidOperationException(
                    "Knowledge replay did not commit before the progress retry.");
            }

            await _executionStore.PersistAsync(execution, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task FailPass2Async(
        ScanExecution execution,
        Guid jobId,
        ScanExecutionFailureKind kind,
        CancellationToken cancellationToken)
    {
        execution.FailPass2(
            jobId,
            kind,
            BuildFailureMessage(kind, execution.RepositoryId),
            _timeProvider.GetUtcNow());
        await _executionStore.PersistAsync(execution, cancellationToken).ConfigureAwait(false);
    }

    private static List<CodeUnit> OrderCodeUnits(IReadOnlyList<CodeUnit> codeUnits) =>
        codeUnits
            .OrderBy(u => u.FilePath, StringComparer.Ordinal)
            .ThenBy(u => u.LineStart)
            .ThenBy(u => u.Id)
            .ToList();

    private WorkerAgentJobPacket BuildPacket(ScanExecution execution, CodeUnit unit) =>
        WorkerAgentJobPacket.Create(
            jobId: ResolvePass1JobId(execution, unit),
            kind: WorkerAgentJobKind.Pass1CodeLogicDocument,
            scanExecutionId: execution.Id,
            repositoryId: execution.RepositoryId,
            systemId: execution.SystemId,
            scanRunId: execution.ScanRunId,
            codeUnitId: unit.Id,
            profiledRevision: execution.ProfiledRevision,
            filePath: unit.FilePath,
            lineStart: unit.LineStart,
            lineEnd: unit.LineEnd,
            symbolName: unit.SymbolName,
            unitType: unit.UnitType,
            language: unit.Language,
            sourceHash: unit.SourceHash,
            fileIdentityHash: unit.FileIdentityHash);

    private Guid ResolvePass1JobId(ScanExecution execution, CodeUnit unit)
    {
        if (_preparedPass2Flow is null)
        {
            return Guid.NewGuid();
        }

        PreparedPass2FlowMember? member = _preparedPass2Flow.Members
            .SingleOrDefault(candidate => candidate.CodeUnitId == unit.Id);
        if (_preparedPass2Flow.SystemId != execution.SystemId ||
            _preparedPass2Flow.RepositoryId != execution.RepositoryId ||
            member is null)
        {
            throw new ArgumentException(
                "The prepared Code Unit is not a member of the accepted Pass 2 flow.",
                nameof(unit));
        }

        return member.JobId;
    }

    private static ScanExecutionFailureKind ClassifyPacketRejection(CodeUnit unit)
    {
        // The packet factory rejects forbidden content and oversized packets.
        // We classify conservatively: a rejection during construction is
        // treated as ForbiddenContent unless the symbol/path clearly exceeds
        // limits. The factory enforces both; the runner records a bounded
        // reason without exposing the rejected value.
        if (unit.SymbolName.Length > 512 || unit.FilePath.Length > 2_048)
        {
            return ScanExecutionFailureKind.OverLimitPacket;
        }

        return ScanExecutionFailureKind.ForbiddenContent;
    }

    private static string BuildFailureMessage(ScanExecutionFailureKind kind, Guid repositoryId) =>
        kind switch
        {
            ScanExecutionFailureKind.SchemaInvalid =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "仓库 {0} 的 Worker Agent 作业结果未通过架构校验,扫描已失败。",
                    repositoryId),
            ScanExecutionFailureKind.Error =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "仓库 {0} 的 Worker Agent 作业返回未处理错误,扫描已失败。",
                    repositoryId),
            ScanExecutionFailureKind.Unbounded =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "仓库 {0} 的 Worker Agent 作业结果超出有限边界,扫描已失败。",
                    repositoryId),
            _ =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "仓库 {0} 的扫描已失败。",
                    repositoryId),
        };

    private static string BuildBoundaryRejectionMessage(ScanExecutionFailureKind kind, Guid repositoryId) =>
        kind switch
        {
            ScanExecutionFailureKind.OverLimitPacket =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "仓库 {0} 的类型化作业包超出有限 UTF-8/数量限制,扫描已失败。",
                    repositoryId),
            ScanExecutionFailureKind.ForbiddenContent =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "仓库 {0} 的类型化作业包包含禁用内容,扫描已失败。",
                    repositoryId),
            _ =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "仓库 {0} 的类型化作业包被拒绝,扫描已失败。",
                    repositoryId),
        };

    private static string BuildSuccessMessage(Guid repositoryId, int jobCount) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "仓库 {0} 的 Pass 1 扫描已完成,共调度 {1} 个 Worker Agent 作业。",
            repositoryId,
            jobCount);

    private static string BuildPass2SuccessMessage(Guid repositoryId) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "仓库 {0} 的准备业务流程已生成,扫描已完成。",
            repositoryId);

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
