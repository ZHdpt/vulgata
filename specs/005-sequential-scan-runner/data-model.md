# Data Model: Sequential Scan Runner & Typed Agent Dispatch

**Date**: 2026-07-31 | **Plan**: [plan.md](plan.md)

## Core entities and value types

### `ScanExecution` (entity, schema `vulgata`, table `ScanExecutions`)

One durable record per deadline Scan run. Created by
`SequentialScanRunner.StartAsync`; mutated only through atomic phase
transitions inside `ScanExecutionRepository`.

| Field | Type | Constraints |
| --- | --- | --- |
| `Id` | `Guid` | non-empty, runner-assigned; PK |
| `RepositoryId` | `Guid` | non-empty; FK to `Repositories`; part of alternate key `(Id, RepositoryId)` and `(ScanRunId, RepositoryId)` |
| `SystemId` | `Guid` | non-empty; from approved handoff |
| `ScanRunId` | `Guid` | non-empty; FK composite `(ScanRunId, RepositoryId)` to `ScanRun` alternate key |
| `PreScanResultId` | `Guid` | non-empty; from approved handoff |
| `ProfiledRevision` | `string` | bounded lowercase Git object ID (≤64 chars, `[0-9a-f]`); from handoff |
| `Phase` | `ScanExecutionPhase` | closed enum; relational `CK_ScanExecutions_Phase_Closed` |
| `InFlightJobId` | `Guid?` | null when idle; non-null while one typed job is dispatched |
| `CompletedJobCount` | `int` | non-negative; count of jobs that returned `Valid` |
| `TotalJobCount` | `int` | non-negative; set at `Plan` time from prepared Code Units |
| `RestartIndicator` | `int` | `CHECK (0 <= "RestartIndicator" AND "RestartIndicator" <= 32)` |
| `TerminalMessage` | `string?` | bounded Chinese (≤512 chars); non-sensitive; set on terminal phases |
| `TerminalReasonKind` | `ScanExecutionFailureKind?` | closed enum; relational `CK_ScanExecutions_TerminalReasonKind_Closed` |
| `StartedAt` | `DateTimeOffset` | `TerminalAt >= StartedAt` when terminal |
| `TerminalAt` | `DateTimeOffset?` | set on terminal phases |

Indexes: by `RepositoryId`; by `ScanRunId`; by `Phase`. All FKs
`Restrict`/`NoAction`; no domain delete method.

### `ScanExecutionPhase` (closed enum)

`Pass1Planning`, `Pass1Running`, `Pass1Completed`, `Completed`, `Failed`,
`Cancelled`. Relational check constraint permits only these six values.

### `ScanExecutionFailureKind` (closed enum)

`SchemaInvalid`, `Error`, `Unbounded`, `MissingPrerequisite`,
`DuplicateActiveScan`, `MissingCodeUnit`, `ForbiddenContent`,
`OverLimitPacket`. Relational check constraint permits only these values.

### `WorkerAgentJobKind` (closed enum)

`Pass1CodeLogicDocument`. (Story 11.3 may add `Pass2BusinessLogicDocument`.)
Unknown kinds are rejected before dispatch.

### `WorkerAgentJobPacket` (immutable Core type)

| Field | Type | Constraints |
| --- | --- | --- |
| `JobId` | `Guid` | non-empty, runner-assigned |
| `Kind` | `WorkerAgentJobKind` | closed |
| `ScanExecutionId` | `Guid` | non-empty |
| `RepositoryId` | `SystemId` | `Guid` | non-empty, exact from handoff |
| `ScanRunId` | `Guid` | non-empty |
| `CodeUnitId` | `Guid` | non-empty, exact Code Unit identity |
| `ProfiledRevision` | `string` | bounded lowercase Git object ID |
| `BoundedSourceSlice.FilePath` | `string` | repository-relative, ≤2048 UTF-8 bytes |
| `BoundedSourceSlice.LineStart` | `int` | ≥1 |
| `BoundedSourceSlice.LineEnd` | `int` | ≥`LineStart` |
| `BoundedSourceSlice.SymbolName` | `string` | ≤512 UTF-8 bytes |
| `BoundedSourceSlice.UnitType` | `CodeUnitType` | closed |
| `BoundedSourceSlice.Language` | `string?` | normalized or null |
| `BoundedSourceSlice.SourceHash` | `string` | lowercase SHA-256 |
| `BoundedSourceSlice.FileIdentityHash` | `string` | lowercase SHA-256 |

Every string is Form-C normalized, rejects forbidden controls, and is checked
against the closed credential/host-path/raw-source-carrier/raw-diagnostic
rules (Story 11.1 sensitive-content guard) before construction. No source
text is carried — only identity and hashes.

### `WorkerAgentResultEnvelope` (immutable Core type)

| Field | Type | Constraints |
| --- | --- | --- |
| `JobId` | `Guid` | must match the dispatched packet |
| `ScanExecutionId` | `Guid` | must match the dispatched packet |
| `Kind` | `WorkerAgentJobKind` | must match the dispatched packet |
| `Outcome` | `WorkerAgentResultOutcome` | closed: `Valid`, `SchemaInvalid`, `Error`, `Unbounded` |
| `BoundedReason` | `string?` | Chinese, ≤512 UTF-8 bytes, non-sensitive; required on non-`Valid` |
| `DispatchedAt` | `DateTimeOffset` | from packet |
| `ReceivedAt` | `DateTimeOffset` | `>= DispatchedAt` |
| `Payload` | `WorkerAgentResultPayload?` | closed allowlist, present only on `Valid`; reserved for Story 11.3 |

Every string is subject to the Story 11.1 sensitive-content guard.

### `WorkerAgentResultPayload` (closed allowlist, reserved)

A closed allowlist reserved for Story 11.3. Story 11.2 validates shape only
and does not consume any payload field. Adding fields in Story 11.3 must
reopen the allowlist deliberately; ad-hoc fields are rejected.

### `ScanExecutionStateView` (Core read model)

Returned by `SequentialScanRunner.ReadStateAsync`. Carries: `Phase`,
`InFlightJobId`, `CompletedJobCount`, `TotalJobCount`, `RestartIndicator`,
`TerminalMessage`, `TerminalReasonKind`, `StartedAt`, `TerminalAt`. Enough
to distinguish `Pass1Running` from `Completed`/`Failed`/`Cancelled` using
persisted state alone.

## State transitions

```text
[Start: approved handoff] --> Pass1Planning
Pass1Planning --[Plan: set TotalJobCount]--> Pass1Running
Pass1Running --[Advance: dispatch one job, set InFlightJobId]--> Pass1Running
Pass1Running --[RecordValid: clear InFlightJobId, ++CompletedJobCount]--> Pass1Running
Pass1Running --[RecordFailure]--> Failed
Pass1Running --[CompletedJobCount == TotalJobCount]--> Pass1Completed --[Finalize]--> Completed
Pass1Planning|Pass1Running --[Cancel]--> Cancelled
```

Terminal phases (`Completed`, `Failed`, `Cancelled`) are immutable. A restart
re-reads a `Pass1Running` row with non-null `InFlightJobId`, increments the
bounded `RestartIndicator`, and surfaces the in-flight phase; it does not
resume the LLM job or fabricate a result.
