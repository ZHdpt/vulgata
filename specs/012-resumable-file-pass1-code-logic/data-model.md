# Data Model: Resumable File Pass 1 & Code Logic Documents

All durable changes are additive and Story 12.3-specific unless a compatibility correction is explicitly named. The DeepSeek strict-result-submission correction adds only execution-local/provider-transport projections and zero durable schema or migration delta. Existing Story 11 rows, Story 12.1 durable state, Story 12.2 prepared packages, and `vulgata.knowledge-document/1.0` Documents remain readable and semantically unchanged.

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
| `ToolAttemptCount` | int 0-8 | Contiguous durable reservation high-water; ordinals 1 through this value are consumed invocation attempts whether or not a receipt exists |
| `AcceptedCallCount` | int 0-8 | Well-formed Document/Source/Unavailable calls; BoundExceeded refusals excluded |
| `EvidenceBytes` | int 0-65,536 | Exact returned canonical evidence bytes |
| `MaximumDepthObserved` | int 0-3 | Largest accepted explicit depth |
| `StateVersion` | non-negative long | Conditional budget commit |
| `UpdatedAt` | UTC | Audit only |

Check constraints enforce `AcceptedCallCount <= ToolAttemptCount`, all maxima, and nonnegative counters. `ReserveNextAttempt` requires the expected state version/high-water, current lease authorization, and capacity; it increments only `ToolAttemptCount` and `StateVersion` before a callback and returns that committed cursor with the ordinal. `ApplyReservedReceipt` requires an already-reserved ordinal and updates accepted/evidence/depth plus `StateVersion` without incrementing `ToolAttemptCount`, returning the replacement cursor. A no-receipt path continues from the reservation cursor; no caller derives or predicts either value. Retry and restart reuse this row. A job without this row is not claimable on the prepared path, and hydration exposes exactly `8 - ToolAttemptCount` remaining calls.

Every runtime navigation scope additionally carries the current server-only claim authorization. The authorization is not duplicated into this row. Scope resolution and receipt commit both require the job to remain `Running` at that version with that unexpired lease and require the owning pair to remain `Running` with a closed circuit.

## 5. Pass1NavigationReceipt

Immutable metadata for one reserved attempt that produced a durable outcome, plus at most one Calls marker; a reserved no-receipt attempt has no row.

| Field | Type/constraint | Meaning |
| --- | --- | --- |
| `Id` | deterministic UUIDv8 PK | Receipt identity |
| `ScanJobExecutionId` | Guid FK | Same job as budget |
| `PairExecutionId` | Guid FK | Same pair |
| `ReceiptOrdinal` | int 1-9 | Ordinary rows equal tool-attempt ordinal; gaps are consumed no-receipt attempts; nine is the Calls marker |
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

Unique `(ScanJobExecutionId, ReceiptOrdinal)`, filtered unique non-null `(ScanJobExecutionId, ToolAttemptOrdinal)` and request fingerprint, and unique receipt fingerprint remain unchanged. An ordinary receipt has `ReceiptOrdinal = ToolAttemptOrdinal` and can exist only for a reserved ordinal. Missing ordinary receipt ordinals are legal consumed no-evidence attempts after `Guid.Empty`, exception, cancellation, lease loss, or crash; they cannot be cited. Results/bytes/depth `BoundExceeded` has zero descriptors/evidence and updates no attempt counter because reservation already consumed it. When reservation eight reaches a closed callback outcome, the idempotent Calls marker uses `ReceiptOrdinal=9`, null tool ordinal, and the final cumulative accepted/evidence snapshot. Hydration/lease recovery and terminal/reconciliation guards repair the marker if a crash leaves high-water eight without it. Same-ordinal/same-fingerprint replay is zero-write; a conflicting second receipt rejects. Descriptor JSON has one server codec and is reconstructed during replay; stored hashes are checked outputs, not trust inputs.

### Runtime-only strict submission projections

These projections exist only within one Worker attempt or callback and are never persisted:

| Projection | Closed state/fields | Authority |
| --- | --- | --- |
| `PreparedPass1SubmissionSession` | `SubmissionCount` 0-2; `NavigationEnabled`; `None|RejectedOnce|Accepted|Rejected`; nullable captured canonical UTF-8 bytes <=65,536 | Serializes submission state and atomically captures only full-local-parser success. |
| `PreparedPass1ValidationResult` | success with canonical bytes, or typed `Shape|Binding|Evidence|Semantics` | Sole exhaustive mapping to `InvalidShape|InvalidBinding|InvalidEvidence|InvalidSemantics`; exception-message classification forbidden. No exception text crosses this seam. |
| `PreparedPass1TerminalContextCorrection` | exact `attempt=1`; closed rejection category; category-derived `UseMinimalValidDocument|CopyPacketBinding|UseAuthorizedFullRangeAnchor` repair code | Supplies bounded actionable correction without candidate, validator, path, schema-fragment, source, persistence, or model authority. Only the fixed category/code mapping is legal. |
| `PreparedPass1FunctionLoopState` | provider turn 1-10 derived as zero-based SDK `FunctionInvocationContext.Iteration + 1`; exact hydrated remaining budget 0-8; submission count 0-2; navigation-capable versus finalization-only selection; absolute start/deadline | Vulgata authority above SDK maximum eleven. Provider turns one through eight use generic required selection with both strict declarations unless submission begins earlier. Provider turn nine/SDK iteration 8 forces a sole first `submit_code_logic` when no earlier submission ended execution; a locally invalid turn-nine first candidate alone authorizes provider turn ten/SDK iteration 9 as the sole correction. Every finalization-only request names `submit_code_logic` and advertises only its unchanged strict declaration. Turn ten cannot be a first-submission fallback and always terminates after its eligible correction; mapped post-ten invocation is unreachable/fail-closed. The state may screen batch count and project admitted capacity but cannot consume or restore a durable attempt; only a committed budget reservation changes durable remaining capacity. Exact initial zero enters the normal strict conversation rather than a pre-provider fallback. |
| `DeepSeekToolCallBatchPreflight` | provider turn; complete ordered call count/names/nonempty unique IDs; eligibility decision; rejection subtype; current admitted navigation turn/count/prefix length | Story-only inner-client gate. It buffers one bounded response and rejects a complete illegal call set before FICC sees any call. For a legal one-through-eight all-navigation response it transiently admits only the provider-order prefix fitting current remaining capacity; excess siblings stay eligible only for a constant no-effect result. It retains no call identity or payload. Eligibility creates no reservation; admitted calls remain subject to one durable reservation immediately before each callback. |
| `PreparedPass1NavigationReservationTicket` | job/pair/lease/version authorization; reserved ordinal 1-8; budget state version/high-water after reservation | Server-only proof returned by the store after one committed reservation. It binds exactly one fresh function invocation and is discarded after the callback; it is not a new durable row or provider field. |
| `PreparedPass1RawActivityClock` | last successful nonzero raw-read timestamp; monotonic generation; inactivity deadline; absolute deadline | One pending parsed `MoveNextAsync` races the deadline derived from the latest raw generation. Nonzero data, including SSE comments, advances inactivity only; zero-byte reads, headers, parsed replay, and tool work do not. Absolute deadline never resets. |
| `DeepSeekStrictSchemaProjection` | authoritative schema hash/bytes; algorithm identity; projected schema/tool hashes/bytes; closed omission/transform ledger | Deterministically derived from the one linked embedded authoritative schema; no second schema or filesystem dependency. |
| `DeepSeekStrictToolDeclarations` | ordered `navigate_code` then `submit_code_logic`; immutable public `AsOpenAIChatTool()` names/descriptions/`strict:true`/closed raw parameter bytes and per-function/combined hashes; deterministic projection algorithm identity; sorted transform ledger of pointer/kind/input-subtree-hash/output-subtree-hash; exact projected per-function/combined bytes and hashes | One transient declaration authority shared by the source-free handshake and every production Worker turn. Raw public schemas are the only inputs; the accepted wire is their fail-closed AD-20 projection. SDK-emitted declaration drift is replaced before transport and production/handshake projected bytes must match exactly. |
| `PreparedPass1WireBudget` | per response raw SSE 0-16,777,216 (Vulgata policy: 32,768 output tokens x conservative 512 serialized SSE bytes/token); assembled 0-16,777,216; each prior assistant contribution 0-16,777,216; prior assistant count 0-9; aggregate prior assistant contributions 0-16,777,216; tool-result contributions 0-393,216 (six-times nested escaping of 65,536 evidence); outgoing 0-17,416,192 | Neither the byte caps nor 512 ratio is a provider/SDK guarantee. Raw is counted incrementally/discarded without whole-response or cumulative buffering. Assembled is current-execution only. Assistant per-message and aggregate values are measured after final UTF-8 serialization; the aggregate, not count times per-message maximum, closes replay growth. A deterministic checked accounting seam proves exact inclusive/+1 arithmetic; public execution proves reachable precedence/cancellation/no-send. Formula: 131,072 prepared initial + 81,920 declarations + 16,777,216 assistant aggregate + 393,216 tools + 32,768 envelope. |
| `DeepSeekStrictWireCompatibility` | provisional captured JSON bytes 0-17,416,192; duplicate-aware parsed source shape; token rename count 0-1; disabled-thinking insertion count 0-1; ordinary required-tool-choice insert/replace count 0-1 or terminal exact named-submit preservation; exact removal counts 0-1 for SDK `parallel_tool_calls:false` and JSON `response_format`; forbidden `reasoning_effort`, `temperature`, `top_p`, `presence_penalty`, `frequency_penalty`; accepted projected-declaration substitution; absent/null assistant tool-call content completion count; deterministic final JSON bytes 0-17,416,192; exact final `max_tokens=32,768`; forbidden final `max_completion_tokens`; exact final `thinking.type=disabled`; exact turn-appropriate `tool_choice`; `stream=true`; no forbidden compatibility/sampling field; exact projected declaration bytes/hash; ordered assistant string-content/tool-call and matching tool-result IDs | Transient pre-transport authority for only the Story 12.3 DeepSeek strict Worker. One valid SDK `max_completion_tokens` may be renamed once; absent thinking may independently receive exact disabled thinking once; absent or one exact SDK `tool_choice:auto` may become ordinary required once; one exact SDK named `submit_code_logic` object may be preserved only for the terminal request. Alternate/duplicate shapes, enabled thinking, named navigation/unknown/malformed object, `none`/other tool choice, reasoning effort, and any ignored sampling control reject. The production tools value is replaced by the projected wire from `DeepSeekStrictToolDeclarations`; every later tool-call assistant message has complete string content and every original call has a matching result. Contributions/body are measured after all operations. No captured bytes or values persist or enter diagnostics. |
| `DeepSeekStrictCapabilityObservation` | sanitized route/model classification; raw public `navigate_code`, `submit_code_logic`, and combined hashes/bytes; declaration-projection algorithm identity; complete canonical transform-ledger hash and row count; projected `navigate_code`, `submit_code_logic`, and combined hashes/bytes; ordinary-required and terminal-named-submit source-free request hashes/bytes; strict/stream/thinking/token-field/forbidden-field-absence/call/`[DONE]` flags for both shapes; timestamps | Qualification evidence only. `DeepSeekStrictToolDeclarations` supplies every raw/algorithm/ledger/projected scalar; the test-only handshake harness copies them without inspecting or reconstructing schemas/projection and computes only each request identity after inserting the helper's projected bytes. It is never pair/provider-circuit/probe/scheduling state and contains no schema, ledger, request, source, prompt, tool arguments/result, response, credential, or host-path bytes. The named response is inspected only transiently for the fixed selected name and records a Boolean. Missing/inconsistent helper evidence yields `ProviderConfiguration`, empty declaration identities, and zero send. The projected combined identity must equal both production fake-wire shapes before source-bearing qualification; the failure-25 diagnostic output hash is not an implementation identity. |

Exactly two functions remain local to the FICC execution. Provider-visible navigation-capable requests on turns one through eight expose both strict declarations; every finalization-only request--forced turn nine, the optional correction-only turn ten, or an earlier correction after one rejected candidate--exposes only the unchanged strict `submit_code_logic` declaration and names it. `navigate_code` is the only projection that can reserve durable attempt capacity or touch receipt rows. A multi-call response is eligible only when every call is declared `navigate_code`, IDs are nonempty/unique, its count is at most eight, and the provider turn is one through eight. Calls execute serially: only the provider-order prefix fitting hydrated remaining capacity may request a committed reservation and callback; every excess sibling receives constant `navigation-budget-exhausted` with no durable or evidence effect. A response above eight calls or any other illegal preflight changes no durable state. Receipt gaps remain consumed attempts; receipts never allocate capacity. `submit_code_logic` remains sole-call and non-evidence: turn nine admits the first candidate, and turn ten admits only the second candidate after one locally rejected turn-nine submission. A valid turn-nine candidate terminates without request ten; an eligible turn-ten correction always terminates, and a first submission on turn ten is rejected before callback. Complete content/tool calls/results remain bounded/current-attempt only. Preflight admission integers, reservation tickets, activity clock, schema/wire projections, and capability observation are transient; only the existing budget high-water and receipts survive retry/restart. This correction changes no persisted field, table, constraint, migration, or EF model.

At commit, the prepared packet's Communication Kind Classification projections form the complete supplied authorization set, while `communicationEvidence.classificationEvidence` forms the candidate's cited subset. The unchanged local validator first rejects any cited-unsupplied ID. The committer then loads and revalidates every distinct supplied ID against the current Pre-Scan Result and exact durable `CommunicationKindClassification` row, and retains cited-set membership as defense in depth. The candidate need not cite unrelated supplied classifications; omission creates no classification or Edge. This is a validation relation only and adds no entity, field, constraint, migration, or EF-model delta.

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

The in-process activation channel, pair coordinator, runner handles, claimed packet, batch preflight, reservation tickets, submission/turn/transcript/raw-activity/wire projections, capability observation, provider health observation, and probe ticket are transient controls. They cannot restore or consume navigation capacity independently: only the persisted budget reservation transition does so. A process restart discards transient state; claim hydration reconstructs exact remaining capacity and repairs marker eight/nine state from PostgreSQL before provider reactivation.

## 12. Migration disposition

One cohesive additive EF migration under Infrastructure will:

1. create `Pass1PairExecutions`, `Pass1ConcurrencyChanges`, `Pass1NavigationBudgets`, `Pass1NavigationReceipts`, `Pass1Outcomes`, and `Pass1FailureInventoryEntries` with all FKs/indexes/checks;
2. add nullable Source Anchor byte columns and their shape check;
3. broaden the exact Document schema CHECK;
4. extend Scan Execution and coordination closed phase/reason checks for `FailedPass1`/`Pass1Policy`;
5. update `VulgataDbContext` and model snapshot.

There is no data backfill, content rewrite, new required value on historical rows, or destructive column change. Clean PostgreSQL 17 apply/rollback/reapply, legacy row read, v1/v2 coexistence, and no-pending-model-change evidence are mandatory.

The Quality Review 01 and PR14 corrections add no table, column, row kind, provider-circuit field, migration, or snapshot change. PR14 changes the existing budget aggregate/store transitions: reservation advances `ToolAttemptCount`; receipt commit does not; ordinary receipt ordinal equals its reserved tool ordinal; marker nine is idempotently repaired. PostgreSQL tests must prove those semantics, historical-row readability, and zero pending model changes. Any EF model delta is a design violation and must return to plan review.
