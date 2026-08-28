# Data Model: Grounded LLM-Wiki Chat & Release Evidence

This feature adds no production persistence entity. It reads existing immutable/current graph and durable Scan entities and introduces closed provider-independent contract, transient, view, fixture, and evidence-record models.

## 1. Existing persistent authorities (unchanged)

| Authority | Required fields/invariant used by Story 12.5 |
| --- | --- |
| `LogicalDocument` / `DocumentVersion` | selected current pointer; immutable version; `SupersededById == null`; System/Repository/Profiled Revision match |
| `IndexEntry` / `DocumentSection` | current Document Version ownership; canonical entry/section order; bounded safe text; section Source Anchor and supporting-Document references |
| `SourceAnchor` | same current Document Version/Repository; safe relative path; positive line range; current source hash |
| `DocumentEdge` | current directed source/target; `References` or `GeneratedFrom`; both endpoints authorized and selected-scope eligible |
| `CrossRepositoryResolutionDecision` | accepted exact-match outcome; non-null exact `DocumentEdgeId`; current consumer/provider/system/repository/revision/evidence binding |
| `FlowGroup` / `FlowTrace` / `FlowTraceStep` | `State == Published`; non-null current Business Logic Document Version; complete exact decision/Edge binding; current ordered trace <=12 steps |
| `ScanExecution` / `ScanJobExecution` / `FileCodeUnit` | persisted production phase, counts, pause/failure codes, retry timestamp, safe failed-file summary source |
| `TwoRepositoryScanCoordination` / Pass 2 execution state | final cross-Repository warning/integrity/security context when present in accepted integration |

`ExactCrossRepositoryEdgeEvidence` remains a legacy persistent entity but is never sufficient for Story 12.5 exact expansion or currentness.

## 2. `ChatRetrievalRequest`

Server-owned Core contract extending the normalized question with transient context.

| Field | Rule |
| --- | --- |
| `RequestId` | nonempty opaque request identity |
| `Mode` | Business or IT |
| `SystemId` | nonempty, current authorized System |
| `RepositoryIds` | exactly two distinct canonical IDs in the System |
| `Question` | NFC, controls rejected, 1-2,000 runes |
| `Conversation` | valid `ChatConversationState`, <=4,096 UTF-8 bytes |
| `AuthorizedScope` | server-created caller/System/exact-two-Repository binding; caller cannot construct it |
| `AdvisoryContext` | server-loaded `ChatAdvisoryContextSnapshot`; caller DTO has no text/source field |
| `DeadlineUtc` / monotonic remaining budget | one server-created absolute deadline, exactly the same binding across retrieval/primary/repair/validation/reveal; implementation maximum 29,000 ms after accepted submission |

Caller DTOs never supply `Conversation`, prior handles, deadline, Document IDs, or relationship identities.

## 3. `ChatConversationState` (transient)

| Field | Bound/invariant |
| --- | --- |
| `ScopeBinding` | exact mode/System/canonical two Repositories |
| `Turns` | 0-2 validated turns, canonical oldest-to-newest |
| `EncodedUtf8Bytes` | <=4,096 for complete canonical representation |

### `ValidatedConversationTurn`

| Field | Rule |
| --- | --- |
| `Question` | normalized original question |
| `AnswerText` | complete validated rendered answer only; no partial/provider diagnostic |
| `PacketFingerprint` | lower SHA-256 |
| `Citations` | bounded prior opaque handles; context only, never current claim authority |
| `PriorLogicalDocumentIds` | 0-5 stable IDs used only as current re-fetch/ranking hints; serialized in the conversation packet and canonical fingerprint |

State transitions:

```text
Empty
  -> AcceptedAnswer(turn 1)
  -> AcceptedAnswer(turn 1, turn 2)
  -> AcceptedAnswer(turn 2, turn 3)

Any state
  -> Empty on new conversation, mode/scope change, authorization loss,
     coordinator disposal, or explicit reset

Failed/unsupported/cancelled/timed-out attempts do not append a turn.
```

Prior answer text and handles are context only. A stable ID may add a small ranking boost only after the current row is independently eligible.

## 4. Typed retrieval pipeline

```text
AuthorizedChatSnapshot
  -> ChatLexicalPrimaryProjection
  -> ChatRelationshipExpansionProjection
  -> ChatEvidenceAssembly
  -> EvidencePacket v2
```

Every arrow accepts the immutable successful value from the prior stage. The closed result union is `Succeeded`, `InsufficientEvidence`, `NotFoundOrUnauthorized`, or `InconsistentCurrentEvidence`. No stage may return a final packet except `ChatEvidenceAssembly`; no relationship stage may alter `PrimaryDocumentIds`, `PrimarySectionIds`, their ranks, or their Source Anchors.

### `AuthorizedChatSnapshot`

| Field | Rule |
| --- | --- |
| current scope | one System, exactly two authorized Repositories |
| candidate rows | stream all current eligible rows in stable-ID order; deduplicate/score before retaining the highest-ranked 256 or exhaustion |
| duplicate stable ID | all ranking/currentness fields byte-equivalent or snapshot inconsistent |
| source rows | current Logical Document/Document Version/Scan Run, Index Entry, Document Section, section Source Anchors, applied Global/System/two-Repository Context only |
| pending Context | excluded until the existing lifecycle applies it |

### `ChatAdvisoryContextSnapshot`

| Field | Rule |
| --- | --- |
| `SourceUnits` | applied nonblank Global, selected System, and two selected Repository Context values; Repository units ordered by RFC 4122 ID bytes |
| source currentness | server identity + `UpdatedAt` + normalized complete text; exact scope membership rechecked |
| `ComposedText` | NFC; CRLF/CR -> LF; outer Unicode whitespace trimmed; canonical Global -> System -> Repository order |
| `Terms` | distinct ordinal-sorted ASCII identifiers length >=2 plus CJK bigrams after the closed ignored-term set |
| `EncodedUtf8Bytes` | 0-8,192 inclusive; 8,193+ is `InconsistentCurrentEvidence`, never truncated |
| `AuthorityFingerprint` | lower SHA-256 over source kinds/identities/order/`UpdatedAt`/normalized complete text and empty/applied disposition |
| empty disposition | missing/all-blank applied values produce canonical empty text/terms and a deterministic empty-authority fingerprint |

`IChatAdvisoryContextReader` accepts only `AuthorizedChatScope`. Hidden/missing/mismatched scope is `NotFoundOrUnauthorized`. Advisory terms are used only after the direct question/conversation pass yields zero positive candidates; fallback is restricted to current Business Logic summary sections and applies search/summary 20, heading 5, title 3 and prose 1 at most once. Currentness reloads and recomputes the exact authority before reveal.

### `ChatLexicalPrimaryProjection`

| Field | Rule |
| --- | --- |
| `PrimaryDocuments` | 1-5 in lexical rank order |
| `PrimarySections` | 1-12; strongest-per-Document first, then extras, <=4 per Document |
| score vector | Repository 50, symbol/endpoint 40, path 30, active-mode term 25 (Business field only in Business; Technical field only in IT), search 20, heading 5, title 3, prose 1, one prior-current-Document boost 1 |
| tie key | descending score, mode preference, Repository ID, Logical Document ID, section order, stable section ID |
| `ExpansionSeeds` | first `min(3, PrimaryDocuments.Count)` primary Documents |

### `ChatRelationshipExpansionProjection`

| Field | Rule |
| --- | --- |
| `PrimaryBinding` | exact immutable identities/fingerprint from lexical projection |
| `Units` | ordered complete optional relationship/Flow Group units |
| additions | <=2 new Documents per seed; <=5 Documents overall; one hop |
| bounds | <=32 relationships, <=12 flow steps |
| invariant | cannot reorder, replace, or remove primary identities |

## 5. `RankedEvidenceSection`

Internal Infrastructure value returned by deterministic lexical scoring.

| Field | Rule |
| --- | --- |
| current Document/section IDs | current selected scope only |
| `DocumentType`, title, heading, content | safe current projection |
| Source Anchor/support IDs | complete and parseable |
| `MetadataScore` | sum of Repository/symbol/path/mode-term/search components, each applied at most once |
| `ProseScore` | sum of heading/title/content components, each applied at most once |
| `Score` | exact integer components from `ChatLexicalPrimaryProjection` |
| `PriorCitationBoost` | exactly 0 or 1 total, applied only to a current eligible Logical Document |
| canonical tie key | descending score, mode preference, Repository ID, Logical Document ID, section order, stable section ID |

Structured symbol and mode-term arrays are parsed and matched by normalized token equality; malformed arrays make the row inconsistent. Every positive-score candidate competes in one bounded top-256 rank, so metadata presence does not pre-filter prose candidates. If no positive-score candidate exists, the same bounded ranker evaluates current Business Logic summaries plus advisory terms. Selection takes the strongest section from each chosen Document before extras, <=4 sections per Document, and stops at exactly 12 primary sections or exhaustion. Relationship packing may add support up to 20 total.

## 6. `ProjectedChatRelationship` and closed authority states

Closed complete support unit; partial units are invalid.

| Field | Rule |
| --- | --- |
| `RelationshipKind` | References or GeneratedFrom |
| `DocumentEdgeId` | current exact persisted Edge |
| source/target current Document Version + Repository | both inside selected authorized scope, directed and distinct |
| `SelectedExactDecisionId` | Cross-Repository References only: first decision in canonical current valid set; relationship proof witness, not uniqueness claim |
| `ExactDecisionCount` | Cross-Repository References only: count of all independently valid current decisions bound to the Edge, >=1 |
| `ExactDecisionSetFingerprint` | SHA-256 over the complete canonical current valid decision/contribution authority set |
| `FlowGroupBinding` | optional only for current published flow evidence; includes group/trace identities and fingerprints |
| `Depth` | exactly 1 for expanded relation |

Cross-Repository REFERENCES validation:

```text
one or more decisions exact-match
AND every included decision.DocumentEdgeId == edge.Id
AND edge.EdgeType == References
AND every decision consumer == edge source
AND every decision provider == edge target
AND all System/Repository/Profiled Revision/current-pointer/contribution bindings agree
AND canonical selected decision == ordered valid set[0]
AND count/fingerprint == recomputed complete valid set
```

Legacy profile fingerprints are not a v2 binding.

### Relationship state table

| State | Exact meaning | Projection result | Packet/claim effect |
| --- | --- | --- | --- |
| `absent` | no allowlisted current Edge exists for this seed/candidate identity | no optional unit | none; lexical primaries remain valid; if an already selected unit requires it, assembly refuses |
| `present-valid` | one current directed allowlisted Edge/current endpoints; Cross-Repository REFERENCES has >=1 independently valid current decision, canonical first decision, count and full-set fingerprint | eligible and canonically ranked; multiple distinct decision slots remain valid | may become a complete optional unit |
| `present-inconsistent` | stale/wrong-scope/wrong-direction Edge, conflicting duplicate decision slot, zero valid decisions where required, decision/Edge/contribution/set-fingerprint mismatch, or decision authority attached where forbidden | closed exclusion with reason | cannot support expansion or claim; unrelated lexical evidence is not poisoned |
| `selected-required` | a `present-valid` relationship unit remains after deterministic complete-unit packing and all optional drops | all Documents/sections/anchors/Edge plus selected-decision/count/full-set fingerprint become required | any later valid-set membership/order/fingerprint or other required-field change refuses the whole assembly; no lower-ranked substitution |

For Cross-Repository REFERENCES, a decision slot is `(Pass1PairExecutionId, ConsumerCandidateId, ConsumerEntryStableKey, Protocol, NormalizedEndpointIdentifier)`. Valid decisions order by pair RFC 4122 bytes, protocol, canonical stable-key UTF-8 bytes, canonical endpoint UTF-8 bytes, candidate RFC 4122 bytes, then decision RFC 4122 bytes. The set fingerprint binds schema + Edge and the complete ordered persisted authority: decision/schema/pair/coordination/System; consumer candidate/entry/stable-key/Document/Repository/Scan/revision; protocol/endpoint/normalization; outcome/kind/reason; nullable provider identity/Document/Repository/Scan/revision and Edge/Uncertainty links; and all four decision/evidence fingerprints after complete contribution-set recomputation. Encodings use length prefixes, RFC 4122 GUID bytes, fixed big-endian enum values and explicit nullable markers. The first decision is only the canonical packet witness; it does not suppress later valid decisions or their Flow Groups.

### Flow Group state table

| State | Exact meaning | Projection result | Packet/claim effect |
| --- | --- | --- | --- |
| `absent` | no Flow Group is associated with a current included Business Logic Document/eligible exact relationship | no optional flow unit | none; a plain valid Edge remains usable without a flow claim |
| `present-valid` | one applicable `Published` group with current Business Logic Document, its exact decision present in the owning Edge's valid set, exact Edge, fingerprints, Flow Trace and contiguous current steps within scope/revisions | eligible and ranked by Edge rank, decision rank, group, trace, step identity; distinct decision-owned groups may share one Edge | may become one complete flow unit |
| `present-inconsistent` | `Incomplete`, `Complete`, `Failed`, `BoundExcluded`, unpublished, stale, conflicting duplicate for one decision, missing Business Logic Document, decision absent from owning Edge set, wrong Edge, missing/duplicate/gapped trace step, wrong revision/scope/fingerprint | closed exclusion with reason | cannot support a flow claim; unrelated lexical evidence is not poisoned |
| `selected-required` | a `present-valid` flow unit remains after deterministic complete-unit packing and all optional drops | group, Business Logic Document, exact relationship, trace, every step Document/Edge/description and required Source Anchor are required | any later defect refuses the whole assembly; no partial steps or substitute group |

Selection states are transient values, not persistence states. Persisted Story 12.4 `Complete` means dispatchable but unpublished and is therefore `present-inconsistent` for Chat expansion; only persisted `Published` can become `present-valid`.

## 7. `EvidencePacket` v2

Schema identity: `grounded-chat-evidence/2.2`. The incompatible addition of complete many-decision authority and applied advisory-context authority is an explicit schema cutover, not an in-place reinterpretation of 2.1.

| Collection/value | Bound |
| --- | --- |
| Documents | 1-5 |
| Sections | 1-20; <=4 per Document |
| Source Anchors | 1-32 |
| Relationships | 0-32 |
| Cross-Repository decision authority | each relationship binds one canonical selected decision plus complete current valid decision count/set fingerprint; multiplicity is not a relationship-count multiplier |
| Flow steps | 0-12; canonical contiguous order |
| relationship depth | exactly 1 |
| selection policy | `lexical-current-v2/1.0`; binds primary Document/section and expansion-seed handles |
| Flow Group authorities | 0-10; each binds Published group, Business Logic Document, decision, Edge, fingerprints and trace |
| evidence body | <=65,536 UTF-8 bytes |
| advisory context | <=8,192 UTF-8 bytes |
| advisory authority | lower SHA-256 over the exact server-owned applied Context snapshot used |
| conversation | <=4,096 UTF-8 bytes |
| fingerprint | lower SHA-256 over complete canonical packet |

Each item has an opaque packet-local handle. A Cross-Repository relationship binds the persisted Edge, canonical selected exact decision, full current decision-set count/fingerprint, and any independently selected decision-owned Flow Groups. Packing drops lowest-ranked optional relationship/Flow Group units first, then lowest-ranked non-strongest lexical extras; it removes newly unreachable support after each whole-unit drop. Strongest-per-Document units and selected-required support are never truncated or partially retained. Required-only overflow refuses assembly. The packet fingerprint includes the exact advisory text and advisory authority fingerprint.

## 8. `ChatSynthesisAttempt`

| Field | Rule |
| --- | --- |
| `AttemptOrdinal` | 1 or 2 |
| `AttemptKind` | Primary or SyntaxRepair |
| request/packet fingerprint | exact same binding on both attempts |
| absolute deadline | same coordinator-created 29-second maximum deadline on both attempts |
| `RuntimeObservationId` | identifies only immutable raw-adapter transport telemetry: request/header/read/EOF/counters/transport outcome/rejection; no validation field |
| `CompletionSummaryId` | identifies the immutable coordinator summary that orders runtime observations and owns repair/final validation/duration |
| `ValidationOutcome` | coordinator-owned closed value copied from the completion summary; never stored in `ChatStreamRuntimeObservation` |
| remaining deadline | positive and >=3,000 ms for ordinal 2 |
| syntax category | closed parse category on ordinal 2; no database/currentness/authorization detail |

State transitions:

```text
Ready -> PrimaryStreaming -> PrimaryAssembled
PrimaryAssembled -> Parsed -> EvidenceValidated -> CurrentnessValidated -> AcceptedForReveal -> Rendered
PrimaryAssembled -> SyntaxRejected -> RepairStreaming (budget permits)
RepairStreaming -> RepairAssembled -> Parsed -> EvidenceValidated -> CurrentnessValidated -> AcceptedForReveal -> Rendered
Any streaming/assembled/validation state -> Discarded | TimedOut | Cancelled | ProviderFailed
SyntaxRejected without budget, second syntax rejection, or any semantic rejection -> Rejected
```

Semantic rejection is terminal and cannot transition to repair.

## 9. Provider stream pipeline, runtime telemetry, and Track C attestation

Agents-internal transport model; it never crosses the client boundary.

| Model/field | Rule |
| --- | --- |
| raw read/body | arbitrary response-body octets; requested read <=16,384 and raw body <=1,048,576 implementation bytes |
| wire decoder | strict incremental UTF-8 across physical reads; no replacement; owns malformed/truncated wire failure |
| SSE line/event | parsed across CR/LF/read splits; each <=131,072 raw wire bytes as an implementation safety cap |
| received logical Provider Chunk | one complete nonterminal SSE `data` event, including empty/metadata/finish/error/answer JSON; exact received `data`-value UTF-8 octets include JSON syntax/escapes/metadata and exclude SSE field syntax, terminators, inserted multi-line LF and `[DONE]`; count before JSON; <=16,384 normative bytes |
| assembled received provider response | monotonic sum of every received logical Provider Chunk payload in stream order, before JSON; <=65,536 normative bytes |
| JSON event | one already received-bounded Provider Chunk decoded into delta/finish/error; JSON escapes and surrogate pairs validated |
| decoded answer delta | one non-empty decoded content delta re-encoded strict UTF-8, <=16,384 additional bytes after JSON unescape |
| final decoded answer | concatenated decoded answer-delta bytes, <=65,536 additional bytes; strict final decode after terminal/EOF |
| data inactivity | ten seconds since last non-empty decoded answer delta unless valid `[DONE]` arrives; raw reads/keepalive/empty/metadata/finish do not reset |
| terminal/EOF | one consistent `[DONE]`, then physical EOF with no later content/error; every other disposition rejects |

`ChatStreamRuntimeObservation` is created only by the production raw response-header adapter and is Track B's neutral telemetry. It binds an opaque runtime observation ID, monotonic sequence, request/attempt, provider/config and canonical `stream=true` request fingerprints, response status/media/protocol, first physical read/EOF disposition, UTC/monotonic transport timings, stage counters, terminal outcome and rejection category. It has no post-transport validation result, candidate, run, corpus case, public key, signature, artifact/export path, prompt, response, evidence, claim, citation, handle, relationship, Source Anchor, secret, connection, or host path.

`ChatStreamCompletionSummary` is created by the coordinator after all attempted transport and validation work. It binds the ordered runtime observation IDs, whether syntax repair was used, the validation outcome for each attempt, the final validation outcome, and total accepted-answer duration. It is independently content-dark and release-neutral; it cannot alter transport counters or outcomes.

Track C optionally composes `Story125StreamAttestationSink`. Only the production raw adapter can invoke its sealed internal admission method. The sink binds a server-owned candidate/run/case/process context, exact adapter type/version/assembly SHA-256, and ephemeral ECDSA P-256 public-key fingerprint to the immutable runtime observation ID/sequence and fields. It emits one signed content-dark `Story125StreamAttemptAttestation`; the private key/admission capability never leaves process memory. Aggregate/SDK fake-chunk observations cannot construct accepted provenance even when their counters match.

Release schema 2.2 uses one exact vocabulary. `transportOutcome` is `completed` or one of the rejection categories below; `rejectionCategory` is null only for `completed` and otherwise exactly matches the transport outcome:

| Pipeline owner/domain | Exact rejection category |
| --- | --- |
| caller/transport supervision | `cancelled`, `disconnect`, `inactivity`, `overall-deadline` |
| raw response-body safety cap | `raw-body-overflow` |
| SSE safety caps/grammar | `sse-line-overflow`, `sse-event-overflow`, `invalid-sse` |
| strict wire decoder | `invalid-wire-utf8` |
| JSON event/escape decoder | `invalid-json-event`, `invalid-json-escape` |
| decoded answer scalar encoding | `invalid-answer-utf8` |
| received logical Provider Chunk / assembled received response | `received-provider-chunk-overflow`, `received-provider-response-overflow` |
| decoded answer delta / final decoded answer | `decoded-delta-overflow`, `final-answer-overflow` |
| terminal and post-terminal assembly | `missing-terminal`, `duplicate-terminal`, `contradictory-terminal`, `post-terminal-data` |
| provider error response | `provider-failure` |

Coordinator-owned `validationOutcome` is a separate exact vocabulary: `syntax-repair-required`, `accepted`, `rejected-before-parse`, `rejected-syntax`, `rejected-semantic`, or `cancelled`. It never substitutes for a transport rejection. Every rejected transport or validation state discards all untrusted buffers and produces no partial answer.

The runtime model carries independent `providerChunkCount`, `maximumReceivedProviderChunkByteCount`, `assembledReceivedProviderResponseByteCount`, `answerDeltaCount`, `maximumAnswerDeltaByteCount`, and `assembledAnswerByteCount`. The cross-artifact consistency vector compares those literal fields/categories and the normative pre-JSON received 16,384/65,536 domains against `plan.md`, the streaming contract, release contract, and release schema 2.2. Any counter substitution, post-decode reinterpretation of FR-054, generic overflow synonym, missing category, or release-field leakage into runtime telemetry fails the vector.

## 10. `ValidatedRevealState`

Transient Web presentation model distinct from provider streaming.

| Field/state | Rule |
| --- | --- |
| `Pending` | generic activity/cancel state only; no provider fragment or evidence detail |
| `AcceptedAnswer` | complete immutable answer after all syntax/evidence/citation/currentness/authorization validation |
| `RevealMode` | Unicode text-element progressive reveal or immediate complete render |
| immediate condition | reduced-motion requested or remaining shared deadline cannot safely complete reveal |
| terminal | complete answer, bounded Chinese outcome, cancelled, or failure; no partial-answer terminal |

## 11. `ProductionScanStatusView`

Server-owned Core read model.

| Field | Rule |
| --- | --- |
| execution/system/repository IDs | nonempty; returned only after ownership filtering |
| `DisplayState` | Preparing, Running, Retrying, Paused, Resumed, Completed, CompletedWithWarnings, FailedIntegrity, FailedSecurity, Failed, Cancelled |
| completed/total/pending/running/retry/failed counts | nonnegative, relationally consistent |
| `NextRetryAt` | earliest current retry timestamp or null |
| `ReasonCode` / `ReasonTextZh` | closed machine code and bounded non-sensitive Chinese mapping |
| `CanResume` | true only for currently recoverable paused state and authorized SystemOwner |
| `FailedFiles` | 0-20 safe Repository-relative path summaries; total failed count remains visible |
| `ObservedAt` / `StateVersion` | current read timestamp and optimistic state version |
| `EvidenceEligibility` | always `ProductionPersisted`; no fake-state implementation can construct an accepted view |

Refresh is read-only. Resume re-reads authorization/current state, calls durable recovery, then re-reads the view. A stale or hidden execution never carries protected detail.

## 12. `ChatEvaluationCase`

Schema: [story125-evaluation-case.schema.json](contracts/story125-evaluation-case.schema.json).

| Field group | Required content |
| --- | --- |
| identity | stable case ID, source authority references |
| scope | release System alias and exactly two Repository aliases; never credentials/paths |
| question | mode, question, optional prior-case ID for follow-up |
| expectations | evidence themes, relationship kinds, useful-answer rubric, citation expectation, authorization expectation |
| observation | status, duration, answer/citation fingerprints, usefulness/citation/leakage result, limitation |

The corpus has 12-20 entries. `expectedAnswer` is forbidden. Follow-up references must be acyclic and form chains no longer than three questions.

## 13. `ReleaseEvidenceRunEnvelope`

Schema: [story125-release-evidence.schema.json](contracts/story125-release-evidence.schema.json).

| Group | Required content |
| --- | --- |
| identity | immutable `runId`; optional `supersedesRunId` points to the prior attempt without replacing it |
| status/result | matching planned/attempted/passed/failed/blocked/not-run discriminator; terminal non-pass reason/evidence and attempted observations where applicable |
| candidate | for executed runs, Story 12.5 commit, integrated accepted Story 12.4 commit, dirty-state assertion |
| environment | database name, PostgreSQL major, host topology, Repository revisions, prompt/packet/stream/tool versions, provider API/model/config, observation process/public-key fingerprint, exact raw-adapter type/version/assembly hash |
| commands | stable ID, redacted exact command, command SHA-256, exit code, UTC timing, bounded hashed stdout/stderr artifacts |
| checks | non-passed runs use conditioned check states; passed runs contain exactly the fixed ten-check inventory with ID/tier/status consistency, command IDs, attempted-observation IDs and result artifacts |
| attemptedObservations | what was actually observed, UTC time, related command IDs and hashed artifacts; never inferred from absent files |
| passed-only release receipt | Scan, Chat, restart, database-ownership and screenshot receipts plus bounded limitations; null for every non-passed status |
| Scan receipts | File Code Unit/seed/failure counts and separate exact Edge, Uncertainty, Business Logic Document, and Cross-Repository-flow query receipts |
| Chat receipt | corpus and quality aggregates plus one case receipt per case; invoked attempts include immutable runtime observation and coordinator completion-summary artifacts plus signed Track C candidate/run/case/request/process/adapter/provider/request/header/read/EOF/validation attestation, per-domain counters, result/packet/stream artifacts, and zero non-streaming accepted answers |
| restart/database receipts | pre/post restart artifacts, duplicate-query receipt, database preflight/ownership/cleanup artifacts and zero forbidden-database access |
| screenshots | at least one each for real Scan state, grounded Business answer, grounded IT answer, restart, and release summary; candidate-bound, production-state, hashed and redaction-passed |
| limitations | explicit bounded list inside a passed receipt; never hidden by aggregate pass |

The passed check inventory is fixed: Tier 1 `tier1-contract-ranking-relationship`, `tier1-conversation-synthesis-security`, `tier1-production-ui-corpus-evidence`, `tier1-build-full-tests`; Tier 2 `tier2-postgresql-restart-resume`; Tier 3 `tier3-candidate-tool-database-preflight`, `tier3-real-scan`, `tier3-grounded-chat-corpus`, `tier3-restart-continuity`, `tier3-screenshot-release-integrity`.

Each case receipt declares `providerDisposition`. `invoked` requires a packet/fingerprint and exactly one primary stream plus, only when declared, one ordered syntax-repair stream. `not-invoked-insufficient-evidence` and `not-invoked-authorization-boundary` require null packet fields and zero stream attempts. An accepted case requires `invoked`, one completed final attempt, and total accepted-answer duration 0-29,999 ms. Operational deterministic rescue is a bounded outcome, not a streamed accepted provider answer.

JSON Schema enforces the closed shape, status-conditioned required fields, Track C attestation provenance, stream-attempt cardinality/order/domain bounds, and exact mandatory-check inventory. The semantic validator authenticates the same-process signature and sealed adapter admission, then resolves runtime observation/candidate/run/case/request/adapter/provider/stream-request/header/read/EOF plus every command, observation, and artifact against the same envelope. It rejects a perfectly consistent SDK aggregate fake-chunk receipt solely when authorized adapter-to-sink provenance is absent, then retains all prior Scan/Chat/restart/database recomputation, corpus coverage, zero non-streaming acceptance, exact two-decimal half-up usefulness, and immutable supersession rules.

Database names must begin `vulgata_story125_`, contain only lowercase ASCII/digits/underscore after the prefix, be unique for the run, and differ from configured development and Story 12.4 qualification database names. Validation occurs before a product connection.

## 14. Relationship overview

```text
ChatConversationState (transient)
  -> ChatRetrievalRequest
  -> AuthorizedChatSnapshot
  -> ChatLexicalPrimaryProjection (sole primary authority)
  -> ChatRelationshipExpansionProjection (optional complete units only)
       -> current DocumentEdge
       -> ordered CrossRepositoryResolutionDecision set (required for cross-Repository REFERENCES)
       -> decision-owned published FlowGroup[] -> FlowTrace -> FlowTraceStep
  -> ChatEvidenceAssembly (sole packet builder)
  -> EvidencePacket v2.2
  -> ChatSynthesisAttempt primary stream (optionally syntax-repair stream)
  -> strict ChatProviderStreamFrame assembly
  -> ChatStreamRuntimeObservation (Track B neutral transport)
  -> validated GroundedAnswer + ChatStreamCompletionSummary (Track B neutral coordinator result)
  -> ValidatedRevealState
  -> append ValidatedConversationTurn

ScanExecution -> ScanJobExecution -> FileCodeUnit
  -> ProductionScanStatusView -> production management UI
  -> existing durable manual resume -> refreshed ProductionScanStatusView

ChatEvaluationCase[] -> Story125StreamAttestationSink (Track C sealed/signing)
  -> Tier 3 attempted observations -> ReleaseEvidenceRunEnvelope 2.2
  -> passed-only ReleaseReceipt (only when every final check passes)
```

## 15. Migration disposition

No model above requires EF persistence. No migration, configuration, `VulgataDbContext`, or model snapshot change is planned. Discovery of an unavailable durable invariant blocks the affected track and requires revised authority/ownership; it does not authorize an opportunistic migration.
