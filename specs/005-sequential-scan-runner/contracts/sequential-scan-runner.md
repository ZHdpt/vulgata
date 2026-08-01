# Contract: Sequential Scan Runner

**Date**: 2026-07-31 | **Plan**: [plan.md](../plan.md) | **Data model**: [../data-model.md](../data-model.md)

## Purpose

The deterministic application-code Sequential Scan Runner that consumes one
`ApprovedPreScanHandoff` for a prepared Repository and drives one Scan
execution from `pre_scan_approved` through typed Worker Agent job dispatch,
durable phase transitions, and terminal success or failure.

## Ports (Core)

```text
public interface IApprovedPreScanHandoffSource
{
    Task<ApprovedPreScanHandoff?> GetAsync(
        Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default);
}

public interface IScanExecutionStore
{
    Task<ScanExecution?> GetAsync(Guid scanExecutionId, CancellationToken cancellationToken = default);
    Task<ScanExecution?> FindActiveAsync(Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default);
    Task PersistAsync(ScanExecution execution, CancellationToken cancellationToken = default);
    Task<ScanExecutionStateView> ReadStateAsync(Guid scanExecutionId, CancellationToken cancellationToken = default);
}

public interface ICodeUnitSource
{
    Task<IReadOnlyList<CodeUnit>> GetPreparedAsync(Guid scanRunId, Guid repositoryId, CancellationToken cancellationToken = default);
}

public interface IWorkerAgentDispatcher
{
    Task<WorkerAgentResultEnvelope> DispatchAsync(WorkerAgentJobPacket packet, CancellationToken cancellationToken = default);
}
```

## Behavior

### `StartAsync(repositoryId, scanRunId, systemId, cancellationToken)`

1. Load the approved handoff via `IApprovedPreScanHandoffSource`.
2. Refuse (`ScanExecutionFailureKind.MissingPrerequisite`) if the handoff is
   missing, not approved, or missing a Profiled Revision.
3. Refuse (`DuplicateActiveScan`) if an active `ScanExecution` already exists
   for the Repository/ScanRun via `IScanExecutionStore.FindActiveAsync`.
4. On success, create exactly one `ScanExecution` in `Pass1Planning` with
   exact identity links, persist atomically, and return its `Id`.

### `RunPass1Async(scanExecutionId, cancellationToken)`

1. Load the `ScanExecution`; require `Phase == Pass1Planning`.
2. Transition to `Pass1Running`; load prepared Code Units via
   `ICodeUnitSource.GetPreparedAsync` in deterministic order (by `FilePath`,
   then `LineStart`, then stable `Id`); set `TotalJobCount`; persist.
3. For each Code Unit: construct one `WorkerAgentJobPacket`; set
   `InFlightJobId`; persist; dispatch through `IWorkerAgentDispatcher`;
   on `Valid` call `RecordValid` (clear `InFlightJobId`, increment
   `CompletedJobCount`, persist); on `SchemaInvalid`/`Error`/`Unbounded`
   call `RecordFailure` and stop. No concurrency, lease, replay, failover,
   or retry.
4. When `CompletedJobCount == TotalJobCount`, finalize through
   `Pass1Completed` to `Completed` with a bounded Chinese success message
   and the exact job count.

### `ReadStateAsync(scanExecutionId, cancellationToken)`

Return a `ScanExecutionStateView` carrying phase, in-flight job, completed
count, total count, restart indicator, and terminal message. On a restart
re-read of a `Pass1Running` row with non-null `InFlightJobId`, increment the
bounded `RestartIndicator` and persist, but do not resume the LLM job and do
not fabricate a result.

## What the runner MUST NOT do

- execute an LLM call (all dispatch goes through `IWorkerAgentDispatcher`);
- commit a Document Version (Story 11.3 does that);
- implement concurrency, leases, replay, failover, retry, or
  multi-Repository scheduling;
- add a public API, UI, Chat Agent tool, Scan parsing strategy, Pass 2 flow
  planning, flow-group clustering, candidate selection, dispatch-site/endpoint
  binding, general Cross-Repository Resolution, or Story 11.3
  document-generation behavior;
- mutate EF migrations, `VulgataDbContext.cs`, `Program.cs`, or
  `sprint-status.yaml` without integration-owner coordination;
- silently resume LLM work or fabricate a result after a restart;
- commit a failing test.

## Failure isolation

On `RecordFailure` the runner transitions to `Failed` inside one atomic
transaction, sets a bounded Chinese `TerminalMessage` naming the failing
Repository and a closed `TerminalReasonKind`, clears `InFlightJobId`, and
does not call `IKnowledgeStoreRepository`. The atomic transaction ensures no
partial `ScanExecution` state is visible. Story 11.1 knowledge-store rows are
untouched; no Story 11.1 invariant is weakened.
