# Data Model: Durable Scan Foundation & Analysis Runtime

All timestamps are UTC `DateTimeOffset` / PostgreSQL `timestamp with time zone`. SHA-256 values are 64 lowercase hexadecimal characters. Identifiers are application-assigned and never database-generated. Machine-specific absolute paths are not persisted.

## 1. Scan Context Snapshot

Immutable insert-only context captured once for one Repository Scan Run.

| Field | Shape | Invariant |
| --- | --- | --- |
| `Id` | `Guid` | Deterministic, non-empty primary key |
| `RepositoryId` | `Guid` | Bound Repository |
| `ScanRunId` | `Guid` | Composite FK with Repository to `ScanRuns` |
| `ProfiledRevision` | string, max 64 | Lowercase Git object ID; equals bound Scan Run |
| `GlobalContext` | nullable string, max 10,000 chars | Normalized advisory text |
| `SystemContext` | nullable string, max 10,000 chars | Normalized advisory text |
| `RepositoryContext` | nullable string, max 10,000 chars | Normalized advisory text |
| `SchemaIdentity` | string, max 128 | Versioned closed identity, initially `scan-context-snapshot/1.0` |
| `ContentHash` | fixed string 64 | SHA-256 of canonical schema + three context components |
| `CapturedAt` | timestamp | On/after bound `ScanRun.StartedAt`; on/before associated new-path `ScanExecution.StartedAt` so capture precedes dispatch execution |

Keys/relationships: unique `(ScanRunId, RepositoryId)` enforces exactly one snapshot. Delete behavior is Restrict. There is no update method; EF configuration treats all fields as required/immutable according to nullability.

Temporal store validation is symmetric for creation order. Snapshot insertion loads the bound Scan Run and rejects `CapturedAt < ScanRun.StartedAt`; when the associated new-path Scan Execution already exists for the same `(ScanRunId, RepositoryId)`, it also rejects `CapturedAt > ScanExecution.StartedAt`. If the snapshot is inserted first, later new-path Scan Execution creation loads that snapshot and rejects an execution `StartedAt` earlier than `CapturedAt`. The second write performs the cross-record check inside its transaction, so neither insertion order can persist an invalid temporal binding.

## 2. Scan Execution (evolved)

The existing per-Repository aggregate remains the durable operational root. Existing fields and legacy phase atoms remain readable.

### Added fields

| Field | Shape | Invariant |
| --- | --- | --- |
| `PromptIdentity` | nullable string, max 128 | Required for new Epic 12 phases; null only for legacy rows |
| `DocumentSchemaIdentity` | nullable string, max 128 | Required for new Epic 12 phases |
| `AnalysisRuntimeIdentityJson` | nullable `jsonb` | Canonical bounded identity, no absolute host root |
| `AnalysisRuntimeIdentityHash` | nullable fixed string 64 | Hash of canonical identity JSON |
| `PauseCategory` | nullable enum string, max 32 | Present only in paused/closed failure shapes |
| `PauseCode` | nullable normalized string, max 64 | Bounded machine-readable code; no raw diagnostic |
| `UpdatedAt` | nullable timestamp | Required for new-path rows; nullable for historical compatibility |
| `StateVersion` | `long`, default 0 | Non-negative EF concurrency token; increment once per accepted transition |

### Phase allowlist

Legacy atoms retained: `Pass1Planning`, `Pass1Running`, `Pass1Completed`, `Completed`, `Failed`, `Cancelled`.

New-path atoms: `Prepared`, `Running`, `PausedProviderUnavailable`, `PausedOperatorCorrection`, `CompletedWithWarnings`, `FailedIntegrity`, `FailedSecurity`. `Completed` and `Cancelled` are shared terminal atoms. New-path `Running` does not use `InFlightJobId`; per-file state lives only in Scan Job Execution.

### Transition rules

```text
Prepared -> Running | Cancelled | FailedIntegrity | FailedSecurity
Running -> PausedProviderUnavailable | PausedOperatorCorrection |
           Completed | CompletedWithWarnings | FailedIntegrity |
           FailedSecurity | Cancelled
PausedProviderUnavailable -> Prepared | FailedIntegrity | FailedSecurity | Cancelled
PausedOperatorCorrection -> Prepared | FailedIntegrity | FailedSecurity | Cancelled
terminal -> no transition
```

Manual resume moves an eligible paused phase to `Prepared`; it does not claim or dispatch jobs. A completion transition is legal only when the store verifies every bound job is terminal and completion semantics match the phase.

## 3. Scan Job Execution

Durable lifecycle of one prepared File Code Unit job.

| Field | Shape | Invariant |
| --- | --- | --- |
| `Id` | `Guid` | Deterministic from schema namespace + Scan Execution + File Code Unit + ordinal |
| `ScanExecutionId` | `Guid` | Composite FK with Repository to `ScanExecutions` |
| `RepositoryId` | `Guid` | Must match execution, Code Unit, Scan Run, and Document |
| `ScanRunId` | `Guid` | Exact bound Scan Run |
| `FileCodeUnitId` | `Guid` | Composite FK `(Id, ScanRunId, RepositoryId)`; UnitType must be File in application invariant |
| `ManifestOrdinal` | int | Positive, deterministic, unique per execution |
| `State` | enum string, max 32 | `Pending`, `Running`, `RetryScheduled`, `Completed`, `Failed`, `Cancelled` |
| `AttemptCount` | int | Non-negative; incremented when a lease is acquired, not during recovery |
| `NextAttemptAt` | nullable timestamp | Required only for `RetryScheduled` |
| `FailureCategory` | nullable enum string, max 32 | `ProviderTransient`, `ProviderConfiguration`, `JobTransient`, `JobPermanent`, `Integrity`, `Security`, `Cancellation`, `Unclassified` |
| `FailureCode` | nullable string, max 64 | Normalized machine-readable code; no provider/source secret |
| `LeaseToken` | nullable `Guid` | Required only for `Running` |
| `LeaseExpiresAt` | nullable timestamp | Required only for `Running`; later than `StateChangedAt` |
| `CommittedDocumentVersionId` | nullable `Guid` | Required exactly when `Completed`; composite FK to same Repository/Scan Run |
| `StateChangedAt` | timestamp | Monotonic within aggregate |
| `StateVersion` | `long` | Non-negative concurrency token, incremented per transition |

Unique indexes: `(ScanExecutionId, FileCodeUnitId)`, `(ScanExecutionId, ManifestOrdinal)`, and filtered unique `CommittedDocumentVersionId IS NOT NULL`.

Cross-column checks:

- `Running` iff lease token and expiry are present; committed Document and next attempt are null.
- `RetryScheduled` iff next attempt is present; lease and committed Document are null.
- `Completed` iff committed Document is present; lease and next attempt are null.
- `Pending`, `Failed`, and `Cancelled` have no lease, next attempt, or committed Document.
- A completed Document reference cannot be cleared or changed.

### Job transitions

```text
Pending -> Running | Cancelled
Running -> Completed | RetryScheduled | Failed | Cancelled | Pending (expired lease recovery only)
RetryScheduled -> Pending (due manual recovery) | Failed | Cancelled
Completed | Failed | Cancelled -> no transition
```

A new Running-to-Completed commit requires matching `LeaseToken`, expected `StateVersion`, deterministic Document Version identity, and a single shared transaction. A retry after a lost response follows a separate already-Completed replay branch: it verifies authorization plus the full canonical job and persisted Document identity, returns mutation-free `Committed` only for an exact match, and returns `Conflict` for every non-identical completed replay.

## 4. Two-Repository Scan Coordination

Lightweight persisted operational record; not a Scan, Scan Coordinator, or job ledger.

| Field | Shape | Invariant |
| --- | --- | --- |
| `Id` | `Guid` | Deterministic from System and canonical pair of bound executions |
| `SystemId` | `Guid` | Both Repositories must currently belong to this System when created/resumed |
| `RepositoryOneId`, `RepositoryTwoId` | `Guid` | Non-empty and distinct; canonical ordering for deterministic identity |
| `ScanRunOneId`, `ScanRunTwoId` | `Guid` | Each bound to its respective Repository |
| `ScanExecutionOneId`, `ScanExecutionTwoId` | `Guid` | Each bound to its respective Repository/Scan Run |
| `ProfiledRevisionOne`, `ProfiledRevisionTwo` | string, max 64 | Equal each bound Scan Execution/Scan Run revision |
| `Phase` | enum string, max 32 | Closed combined phase |
| `ReasonCategory` | nullable enum string, max 32 | Bounded pause/failure category |
| `ReasonCode` | nullable string, max 64 | Sanitized machine-readable code |
| `CreatedAt`, `UpdatedAt`, `TerminalAt` | timestamps | TerminalAt only in terminal phases |
| `StateVersion` | `long` | Concurrency token |

Combined phases: `Preparing`, `Pass1`, `Resolving`, `Pass2`, `Paused`, `Completed`, `CompletedWithWarnings`, `FailedIntegrity`, `FailedSecurity`, `Cancelled`. Story 12.1 supplies representation/invariants; Stories 12.2-12.4 own phase-driving policies.

The store locks or version-checks both Scan Executions before a combined terminal transition. It cannot report success if either child is nonterminal, failed, cancelled, revision-drifted, or otherwise invalid. A failure in one child does not delete valid Documents from the other.

## 5. Analysis Runtime Identity (value object)

Canonical versioned JSON, embedded/bound by Scan Execution rather than a machine-installation entity.

```text
schemaIdentity
codeGraph.packageName/packageVersion/repository/command/platformPackage
codeGraph.files[{relativeComponent, sha256}]
jdtLs.releaseIdentity/distributionArchiveName/distributionArchiveSha256
java.majorVersion
client.name/clientVersion/transport
workspace.repositoryId/profiledRevision
```

Fixed CodeGraph values:

- package `@colbymchenry/codegraph`, version `1.5.0`, command `codegraph`
- platform `@colbymchenry/codegraph-win32-x64@1.5.0`
- `npm-shim.js`: `80419260F06862D7A422D13EF69E3B24C7A21DBEF4309BCE41DB02AD642B1877`
- `node.exe`: `B3094D0B49F9AD602262A9921551737BB97637C05DD357A06AE98188D7290AA3`
- `lib/dist/bin/codegraph.js`: `03E4C791CC0DD91ED264278461BF9A56C0278AA0670D5942FC4732311C66DE03`

Fixed Java values: JDT LS `jdt-language-server-1.61.0-202608141332`, Java major 21, StreamJsonRpc `2.25.29`, transport `stdio`. The distribution archive SHA-256 has no approved value yet; identity construction/readiness fails until it is supplied and verified. The core JAR hash is not an identity substitute.

## 6. Analysis Runtime Health (non-persisted result)

Fields: overall `Ready|NotReady`, canonical identity/hash when complete, ordered component checks, bounded failure category/code, observation time. It contains no raw stderr, absolute path, environment, source text, or process handle. Ordinary startup can report NotReady while keeping management/reporting surfaces available; Worker dispatch must reject NotReady.

## 7. Recovery read model

`ScanRecoverySnapshot` is a materialized immutable view containing execution identity/bindings/version, context hash, ordered jobs with versions/state/hashes, coordination binding if any, and expected committed Document links. It is never persisted as a second authority. The conditional recovery write accepts the snapshot versions; any drift produces `Conflict` and no partial mutation.

## 8. Protected compatibility data

The migration must not change meaning or readability of `LogicalDocuments`, `DocumentVersions`, `SourceAnchors`, `DocumentEdges`, `ExactCrossRepositoryEdgeEvidence`, `Uncertainties`, prepared-demo flow/projection rows, or existing `ScanExecutions`. New foreign keys use Restrict/NoAction. Rollback may remove new Story 12.1 records but cannot cascade into protected historical tables.
