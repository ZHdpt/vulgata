# Data Model: Resumable File Pass 1 & Code Logic Documents

All changes are additive and Story 12.3-specific unless a compatibility correction is explicitly named. Existing Story 11 rows, Story 12.1 durable state, Story 12.2 prepared packages, and `vulgata.knowledge-document/1.0` Documents remain readable and semantically unchanged.

## 1. Pass1PairExecution

One durable operational aggregate for one exact approved prepared pair. It does not duplicate manifests, File Code Units, source, classifications, or child job state.

| Field | Type/constraint | Meaning |
| --- | --- | --- |
| `Id` | deterministic UUIDv8 PK | Canonical pair-execution identity |
| `CoordinationId` | non-null Guid, unique FK | Accepted two-Repository coordination |
| `SystemId` | non-null Guid FK | Same System as coordination |
| `PairPackageFingerprint` | fixed 64 lower hex, unique | Exact Story 12.2 approved pair package |
| `PairSelectionFingerprint` | fixed 64 lower hex | Exact Story 12.2 ordering package |
| `ManifestOneId`, `ManifestTwoId` | non-null Guid FKs | Canonically ordered approved manifests |
| `Pass1PairFingerprint` | fixed 64 lower hex | Reconstructed Story 12.3 identity digest |
| `PromptIdentityOne`, `PromptIdentityTwo` | each max 128 | Exact persisted prompt identity for each canonically ordered child Scan Execution |
| `DocumentSchemaIdentity` | exactly `code-logic/file-v2` | Authoritative result grammar |
| `AnalysisRuntimeIdentityHashOne`, `AnalysisRuntimeIdentityHashTwo` | each fixed 64 lower hex | Exact accepted Story 12.1 runtime binding for each canonically ordered child |
| `InitialConcurrencyLimit` | smallint, 1-4 | Start request/default retained for exact start replay |
| `ConcurrencyLimit` | smallint, 1-4 | Current effective pair-wide claim cap |
| `ConcurrencyRevision` | non-negative long | Increments only for an effective limit change |
| `ConcurrencyUpdatedAt` | UTC | Last effective change; audit only |
| `DispatchedFileCount` | int, 50-150 | Exact job-set denominator |
| `HighValueSeedCount` | int, bounded by job count | Reconstructed accepted seed set |
| `Phase` | closed enum | `Running`, `ProbingProvider`, `PausedProviderUnavailable`, `PausedOperatorCorrection`, `PausedInspection`, `Reconciling`, `ReadyForResolution`, `FailedPass1`, `FailedIntegrity`, `FailedSecurity`, `Cancelled` |
| `CircuitState` | closed enum | `Closed`, `Probing`, `Paused` |
| `ProbeOrdinal` | smallint 0-5 | Number of probes already attempted |
| `NextProbeAt` | nullable UTC | Required only while Probing |
| `RetryAfterAt` | nullable UTC | Bounded accepted provider hint |
| `ReasonCategory`, `ReasonCode` | nullable closed enum + max 64 | Sanitized pause/failure reason |
| `StateVersion` | non-negative long concurrency token | Version-conditional transitions |
| `StartedAt`, `UpdatedAt`, `TerminalAt` | UTC | Audit only; excluded from identity |

Unique/relational constraints prove the coordination/System/manifests bind the same prepared pair. Deletes are Restrict/NoAction. Phase/circuit/reason/probe check constraints reject impossible combinations. No raw provider/tool/source value is stored.

### Transitions

```text
Running -> ProbingProvider | PausedOperatorCorrection | PausedInspection |
           Reconciling | FailedPass1 | FailedIntegrity | FailedSecurity | Cancelled
ProbingProvider -> Running | PausedProviderUnavailable |
                   FailedIntegrity | FailedSecurity | Cancelled
PausedProviderUnavailable -> Running | FailedIntegrity | FailedSecurity | Cancelled
PausedOperatorCorrection -> Running | FailedIntegrity | FailedSecurity | Cancelled
PausedInspection -> Running | FailedIntegrity | FailedSecurity | Cancelled
Reconciling -> ReadyForResolution | FailedPass1 | FailedIntegrity | FailedSecurity
terminal/ReadyForResolution -> immutable for Story 12.3
```

Manual resume performs revalidation before a paused-to-Running transition. `ReadyForResolution` means Story 12.3 is complete and coordination has entered its accepted `Resolving` handoff; it does not mean Story 12.4 ran.

### Active concurrency update

An authorized version-conditional command may change `ConcurrencyLimit` among 1-4 while the pair is `Running`, `ProbingProvider`, `PausedProviderUnavailable`, `PausedOperatorCorrection`, or `PausedInspection`. It locks the pair row, requires the expected pair `StateVersion`, increments both `StateVersion` and `ConcurrencyRevision`, and appends one immutable `Pass1ConcurrencyChange`. Existing valid leases are never shortened, revoked, or reissued. If their count is at or above the lower effective limit, claims remain closed until normal completion/expiry reduces the count below the limit. Reconciling and terminal pairs reject updates.

`InitialConcurrencyLimit` never changes. An exact replay of the original start request compares it, not the current effective limit, and returns current state without mutation. A different start limit remains a conflict and cannot masquerade as an update.

## 2. Pass1ConcurrencyChange

Immutable audit/replay record for one effective active-pair limit transition.

| Field | Type/constraint | Meaning |
| --- | --- | --- |
| `Id` | deterministic UUIDv8 PK | Canonical update command identity |
| `PairExecutionId` | Guid FK | Updated pair |
| `ExpectedStateVersion`, `ResultingStateVersion` | non-negative long | Exact version transition; resulting = expected + 1 |
| `PreviousLimit`, `EffectiveLimit` | smallint 1-4, unequal | Exact old/new values |
| `ConcurrencyRevision` | positive long | Pair revision after update |
| `ChangedByPrincipalId` | bounded identity | Authorized SystemOwner audit identity; excluded from scan/content identity |
| `ChangeFingerprint` | fixed SHA-256 | Reconstructable command/result envelope |
| `ChangedAt` | UTC | Audit only |

Unique `(PairExecutionId, ExpectedStateVersion)` and `(PairExecutionId, ConcurrencyRevision)`. Exact lost-acknowledgement replay reconstructs the row and returns the current pair state with zero writes. Same expected version with different target/actor or any stale version without an exact record is conflict. A request equal to the current limit at the current version is a zero-write no-op and creates no change row.

## 3. Existing ScanJobExecution (reused unchanged)

Story 12.3 creates the accepted entity with:

- deterministic ID from Scan Execution + File Code Unit + Manifest Ordinal;
- Repository/Scan Run/File Code Unit bindings;
- `Pending|Running|RetryScheduled|Completed|Failed|Cancelled`;
- AttemptCount, StateVersion, lease, retry time, closed failure category/code;
- one `CommittedDocumentVersionId` only for Completed.

No new pair ordinal or manifest field is added. Exact manifest membership and `PairSelectionOrdinal` are joined through `PreparedScanManifestEntry` by the pair's manifest IDs and File Code Unit ID. Existing unique constraints `(ScanExecutionId, FileCodeUnitId)` and `(ScanExecutionId, ManifestOrdinal)` remain the identity guards. `AttemptCount <= 2` is a Story 12.3 policy enforced by its store, not a global schema change that could reinterpret other users.

### Runtime-only claim authorization

The production claim result carries a server-only authorization projection, not a new row: pair/job IDs, current job `StateVersion`, lease token/expiry, pair `StateVersion`, Pair Selection Ordinal, Manifest Ordinal, and nullable next durable eligibility. The packet source must consume that exact projection and revalidate it after revision-bound source hydration. It never enters a model-visible packet, API/status response, navigation receipt, provider message, Document, or log.

Before choosing a Pending job, the Story 12.3 claim transaction releases only due `RetryScheduled` jobs using their current versions and recovers only strictly expired leases. If no claim is possible, `NextEligibleAt` is the minimum durable future retry/lease/probe eligibility; null with zero active leases means the complete job set is terminal and eligible for terminal reconciliation. This projection drives `TimeProvider` delays without a polling or in-memory retry authority.

### Runtime-only pair coordinator projection

One in-process single-flight coordinator exists per explicitly activated pair. Its runner map contains only local completion handles keyed by claimed job/lease authorization; it is not persisted, cannot select work, and is discarded on process loss. Fill serially obtains durable claims but launches each claimed job in an independent DI scope without awaiting a prior runner. It stops at `AtCapacity`/ineligible/closed state, observes completions only as wake-ups, and refills from the durable store. A lower limit preserves existing leases and blocks replacements while active count is at or above the limit; an increase wakes Fill immediately. Reconciliation requires both an empty local runner map and a fresh durable terminal-exhaustion result. No table, column, migration, content identity, or display order derives from this projection.

## 4. Pass1NavigationBudget

Exactly one row per Story 12.3 job.

| Field | Type/constraint | Meaning |
| --- | --- | --- |
| `ScanJobExecutionId` | Guid PK/FK | Owned job |
| `PairExecutionId` | Guid FK | Pair aggregate |
| `ToolAttemptCount` | int 0-8 | Accepted plus rejected invocation attempts |
| `AcceptedCallCount` | int 0-8 | Well-formed Document/Source/Unavailable calls; BoundExceeded refusals excluded |
| `EvidenceBytes` | int 0-65,536 | Exact returned canonical evidence bytes |
| `MaximumDepthObserved` | int 0-3 | Largest accepted explicit depth |
| `StateVersion` | non-negative long | Conditional budget commit |
| `UpdatedAt` | UTC | Audit only |

Check constraints enforce `AcceptedCallCount <= ToolAttemptCount`, all maxima, and nonnegative counters. Retry and restart reuse this row. A job without this row is not claimable on the prepared path.

Every runtime navigation scope additionally carries the current server-only claim authorization. The authorization is not duplicated into this row. Scope resolution and receipt commit both require the job to remain `Running` at that version with that unexpired lease and require the owning pair to remain `Running` with a closed circuit.

## 5. Pass1NavigationReceipt

Immutable metadata for one accepted call; raw excerpt/provider content is not stored.

| Field | Type/constraint | Meaning |
| --- | --- | --- |
| `Id` | deterministic UUIDv8 PK | Receipt identity |
| `ScanJobExecutionId` | Guid FK | Same job as budget |
| `PairExecutionId` | Guid FK | Same pair |
| `ReceiptOrdinal` | int 1-9 | Contiguous durable receipt order, including at most one call-limit marker |
| `ToolAttemptOrdinal` | nullable int 1-8 | Provider function attempt; null only for the server-created call-limit marker |
| `Operation` | nullable closed enum | Eight allowed operations; null only for the server-created Calls marker |
| `Depth` | nullable int 1-4 | Requested chain depth; four is legal only on a metadata-only Depth refusal, null only for the Calls marker |
| `Outcome` | closed enum | `DocumentEvidence`, `SourceEvidence`, `Unavailable`, `BoundExceeded` |
| `BoundKind` | nullable closed enum | `Calls`, `Results`, `EvidenceBytes`, or `Depth`; required only for BoundExceeded |
| `RequestFingerprint` | nullable SHA-256 | Null only for the server-created Calls marker |
| `ReceiptFingerprint` | fixed SHA-256 | Independently reconstructable envelope |
| `ResultCount` | int 0-20 | Canonical distinct descriptors |
| `EvidenceBytes` | int 0-65,536 | Exact response evidence bytes |
| `DescriptorJson` | bounded jsonb | IDs/path/range/hash metadata only; no excerpt text |
| `CreatedAt` | UTC | Audit only |

Unique `(ScanJobExecutionId, ReceiptOrdinal)`, filtered unique non-null `(ScanJobExecutionId, ToolAttemptOrdinal)` and request fingerprint, and unique receipt fingerprint. `BoundExceeded` has zero descriptors/results/evidence bytes and releases no evidence. Results/bytes/depth refusal consumes the already-bounded tool attempt but not `AcceptedCallCount`; the Calls marker is created atomically when either accepted-call or tool-attempt count first reaches eight and disables further calls, and consumes no ninth tool attempt. Descriptor JSON has one server codec and is reconstructed during replay; stored hashes are checked outputs, not trust inputs.

## 6. Pass1Outcome and warning entries

One immutable outcome is written after terminal accounting. A failed seed or over-budget result writes `FailedPass1` without reconciliation; a permitted successful/warning result inserts its outcome atomically with local reconciliation and the coordination handoff.

### Pass1Outcome

| Field | Type/constraint | Meaning |
| --- | --- | --- |
| `PairExecutionId` | Guid PK/FK | Outcome owner |
| `Status` | `Succeeded|CompletedWithWarnings|FailedPass1` | Honest Pass 1 result |
| `DispatchedCount`, `CompletedCount`, `OrdinaryFailureCount` | bounded ints | Distinct jobs only |
| `OrdinaryFailureBudget` | int 0-5 | `min(5,floor(dispatched/10))` |
| `HighValueSeedCount`, `SucceededHighValueSeedCount` | bounded ints | Accepted seed rule |
| `ReconciliationFingerprint` | nullable SHA-256 | Required for successful status |
| `ReconciledEdgeCount` | non-negative int | Exact final same-Repository Edge count |
| `OutcomeFingerprint` | SHA-256 | Canonical counts/status/warnings/reconciliation |
| `CompletedAt` | UTC | Audit only |

### Pass1FailureInventoryEntry

Immutable ordered child for each ordinary terminal failed file.

| Field | Type/constraint | Meaning |
| --- | --- | --- |
| `Id` | deterministic UUIDv8 PK | Derived from outcome + File Code Unit |
| `PairExecutionId` | Guid FK | Outcome owner |
| `FileCodeUnitId`, `RepositoryId` | Guid FKs | Exact prepared unit |
| `PairSelectionOrdinal`, `ManifestOrdinal`, `SegmentOrdinal` | positive ints | Canonical presentation |
| `RepositoryRelativePath` | contained bounded path | No host path |
| `FailureCategory`, `FailureCode` | closed ordinary category + max 64 | Sanitized result |

Unique `(PairExecutionId, FileCodeUnitId)` and `(PairExecutionId, PairSelectionOrdinal)`. It contains no source/provider text.

## 7. Code Logic Document Version

Reuse `LogicalDocument`, immutable `DocumentVersion`, `DocumentSection`, `IndexEntry`, `SourceAnchor`, and `DocumentEdge`.

- Logical Document identity: Repository + path + segment ordinal.
- Document Version identity: deterministic job + File Code Unit fingerprint + v2 schema.
- `DocumentVersion.SchemaIdentity`: `code-logic/file-v2` for this path.
- `ContentJson`: sole authoritative canonical v2 JSON.
- `ContentMarkdown`, sections, index: pure projections.
- `ContentFingerprint` and `CommitFingerprint`: Story 12.3 canonical digests.
- Version Chain behavior remains immutable; previous current version is only superseded in the atomic new-version transaction.

No Flow Group, Flow Trace, Uncertainty, Cross-Repository exact evidence, Business Logic Document, or Pass 2 row is created.

Completed replay reconstructs the complete canonical commit graph from scalar rows: the Document Version, exact ordered Source Anchors, exact ordered Document Sections, exactly one Index Entry, and exact direct REFERENCES Edges. A durable successful `Pass1Outcome` additionally authorizes only the Edge set independently reconstructed by the pure reconciliation projection. Missing, extra, changed, reordered, or duplicate relationships are never hidden by stored fingerprints.

## 8. Source Anchor compatibility correction

Add nullable `ByteStart` and `ByteEnd` to existing Source Anchor rows:

- historical/v1: both null remains legal and unchanged;
- v2: both non-null, zero-based half-open, `0 <= ByteStart < ByteEnd`, contained by its File Code Unit;
- all rows retain one-based inclusive `LineStart <= LineEnd`.

The new v2 factory and commit port require byte bounds. A relational both-null-or-valid CHECK prevents half-populated rows. Cross-row File Code Unit containment is revalidated in the committer and PostgreSQL mutation tests.

## 9. New-path Scan/coordination compatibility correction

### ScanExecution

The accepted Story 12.1 phase set already declares new-path `Running -> Completed`, but the current entity lacks that method. Add a warning-free `Complete` new-path transition. Add `FailedPass1` plus failure category `Pass1Policy` because ordinary seed/budget failure cannot truthfully use Integrity, Security, Cancelled, Paused, or CompletedWithWarnings.

Historical phases/constructors/methods/rows remain unchanged. `FailedPass1` requires prompt/schema/runtime bindings, non-null machine failure code, no pause, and terminal time.

### TwoRepositoryScanCoordination

Add terminal `FailedPass1` and reason category `Pass1Policy`, plus a version-conditional transition. Existing phase values and transitions remain unchanged. Successful Story 12.3 uses existing `Pass1 -> Resolving` as handoff and does not add a redundant coordination phase.

## 10. Document schema compatibility correction

Broaden `CK_DocumentVersions_SchemaIdentity_Closed` from one exact value to:

```text
vulgata.knowledge-document/1.0
code-logic/file-v2
```

No v1 row is updated. The legacy materializer continues accepting/producing only v1; the new prepared committer accepts/produces only v2. Read paths branch on exact schema identity and fail closed on unknown values.

## 11. Relationships and visibility

```text
TwoRepositoryScanCoordination 1 -- 0..1 Pass1PairExecution
Pass1PairExecution 1 -- 2 PreparedScanManifest
Pass1PairExecution 1 -- 0..N Pass1ConcurrencyChange
Pass1PairExecution 1 -- N ScanJobExecution (through two bound Scan Executions)
ScanJobExecution 1 -- 1 Pass1NavigationBudget
ScanJobExecution 1 -- 0..9 Pass1NavigationReceipt
Pass1PairExecution 1 -- 0..1 Pass1Outcome
Pass1Outcome 1 -- 0..N Pass1FailureInventoryEntry
Completed ScanJobExecution 1 -- 1 DocumentVersion
DocumentVersion 1 -- N SourceAnchor/DocumentSection
DocumentVersion 1 -- 1 IndexEntry
DocumentVersion N -- N DocumentVersion through DocumentEdge REFERENCES
```

The pair transaction is the only start visibility boundary. The Document/job transaction is the only per-file success boundary. The reconciliation transaction is the only post-Pass1 Edge/outcome/handoff boundary.

The in-process activation channel, single-flight capacity-filling pair coordinator, local runner handles, claimed packet, provider probe observation, and probe ticket are transient control projections. They create no durable identity and cannot make a claim, retry, probe, pause, completion, capacity, order, or reconciliation decision without the persisted rows above. A process restart discards them; authenticated manual Resume reconstructs authority from PostgreSQL before reactivation.

## 12. Migration disposition

One cohesive additive EF migration under Infrastructure will:

1. create `Pass1PairExecutions`, `Pass1ConcurrencyChanges`, `Pass1NavigationBudgets`, `Pass1NavigationReceipts`, `Pass1Outcomes`, and `Pass1FailureInventoryEntries` with all FKs/indexes/checks;
2. add nullable Source Anchor byte columns and their shape check;
3. broaden the exact Document schema CHECK;
4. extend Scan Execution and coordination closed phase/reason checks for `FailedPass1`/`Pass1Policy`;
5. update `VulgataDbContext` and model snapshot.

There is no data backfill, content rewrite, new required value on historical rows, or destructive column change. Clean PostgreSQL 17 apply/rollback/reapply, legacy row read, v1/v2 coexistence, and no-pending-model-change evidence are mandatory.

The Quality Review 01 correction adds no table or column: production execution uses existing jobs/pair/probe state, lease-bound navigation uses existing job/lease/version fields, and complete replay reads the existing Document graph. If implementation produces an EF model delta for these corrections, it is a design violation and must return to plan review rather than silently adding persistence.
