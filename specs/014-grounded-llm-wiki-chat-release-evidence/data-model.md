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
| `Citations` | bounded prior opaque handles plus stable Logical Document IDs used only as current re-fetch hints |

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

## 4. `RankedEvidenceSection`

Internal Infrastructure value returned by deterministic lexical scoring.

| Field | Rule |
| --- | --- |
| current Document/section IDs | current selected scope only |
| `DocumentType`, title, heading, content | safe current projection |
| Source Anchor/support IDs | complete and parseable |
| `MatchClass` | closed precedence: exact symbol, endpoint, path, title, heading, mode term, prose, fallback |
| `Score` | deterministic integer components only |
| `PriorCitationBoost` | fixed bounded value, applied only to current eligible Document |
| canonical tie key | match class/score, mode preference, Repository ID, Logical Document ID, section order, stable section ID |

Selection takes the strongest section from each chosen Document before extras, <=4 sections per Document, approximately 12 primary and <=20 total.

## 5. `ProjectedChatRelationship`

Closed complete support unit; partial units are invalid.

| Field | Rule |
| --- | --- |
| `RelationshipKind` | References or GeneratedFrom |
| `DocumentEdgeId` | current exact persisted Edge |
| source/target current Document Version + Repository | both inside selected authorized scope, directed and distinct |
| `ExactDecisionBinding` | required for Cross-Repository References; accepted exact-match decision bound to same Edge and direction |
| `FlowGroupBinding` | optional only for current published flow evidence; includes group/trace identities and fingerprints |
| `Depth` | exactly 1 for expanded relation |

Cross-Repository REFERENCES validation:

```text
decision exact-match
AND decision.DocumentEdgeId == edge.Id
AND edge.EdgeType == References
AND decision consumer == edge source
AND decision provider == edge target
AND all System/Repository/Profiled Revision/current-pointer bindings agree
```

Legacy profile fingerprints are not a v2 binding.

## 6. `EvidencePacket` v2

Schema identity: `grounded-chat-evidence/2.0`.

| Collection/value | Bound |
| --- | --- |
| Documents | 1-5 |
| Sections | 1-20; <=4 per Document |
| Source Anchors | 1-32 |
| Relationships | 0-32 |
| Flow steps | 0-12; canonical contiguous order |
| relationship depth | 0 or 1 only |
| evidence body | <=65,536 UTF-8 bytes |
| advisory context | <=8,192 UTF-8 bytes |
| conversation | <=4,096 UTF-8 bytes |
| fingerprint | lower SHA-256 over complete canonical packet |

Each item has an opaque packet-local handle. Relationship handles bind the persisted Edge plus accepted v2 decision/Flow Group identities when applicable. Packing may remove only a whole optional low-ranked section and its now-unreferenced optional support. Required support, section text, Source Anchors, and relationships are never truncated.

## 7. `ChatSynthesisAttempt`

| Field | Rule |
| --- | --- |
| `AttemptOrdinal` | 1 or 2 |
| `AttemptKind` | Primary or SyntaxRepair |
| request/packet fingerprint | exact same binding on both attempts |
| absolute deadline | same coordinator-created 29-second maximum deadline on both attempts |
| stream observation | content-dark request/start/first-data/terminal/count/bytes/outcome/validation fields only |
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

## 8. Provider stream pipeline and observations

Agents-internal transport model; it never crosses the client boundary.

| Model/field | Rule |
| --- | --- |
| raw read/body | arbitrary response-body octets; requested read <=16,384 and raw body <=1,048,576 implementation bytes |
| wire decoder | strict incremental UTF-8 across physical reads; no replacement; owns malformed/truncated wire failure |
| SSE line/event | parsed across CR/LF/read splits; each <=131,072 raw wire bytes as an implementation safety cap |
| JSON event | complete SSE data event decoded into delta/finish/error/`[DONE]`; JSON escapes and surrogate pairs validated |
| Provider Chunk | one non-empty decoded content delta re-encoded strict UTF-8, <=16,384 acceptance bytes after JSON unescape |
| assembled response | concatenated Provider Chunk bytes, <=65,536 acceptance bytes; strict final decode after terminal/EOF |
| data inactivity | ten seconds since last Provider Chunk unless valid `[DONE]` arrives; raw reads/keepalive/metadata/finish do not reset |
| terminal/EOF | one consistent `[DONE]`, then physical EOF with no later content/error; every other disposition rejects |

`ChatProviderStreamObservation` is created only by the production raw response-header adapter. It binds candidate/case/request/attempt and process-observation identity; exact adapter type/version/assembly SHA-256; provider API/config fingerprint; canonical `stream=true` request fingerprint; response-header status/media/protocol; first physical read and physical EOF disposition; stage counters; terminal/rejection/validation/duration; provenance artifact; process public-key fingerprint; and ECDSA signature. It has no prompt, response, evidence, claim, citation, handle, relationship, Source Anchor, secret, connection, or path field.

`ChatStreamObservationAuthority` owns an ephemeral process ECDSA P-256 key. Preflight records its public-key fingerprint with candidate/process/adapter identity; the private key is never persisted. A same-process semantic verifier authenticates every canonical raw-adapter receipt before export. Aggregate/SDK fake-chunk observations cannot construct accepted provenance.

Schema 2.1 uses one exact vocabulary. `transportOutcome` is `completed` or one of the rejection categories below; `rejectionCategory` is null only for `completed` and otherwise exactly matches the transport outcome:

| Pipeline owner/domain | Exact rejection category |
| --- | --- |
| caller/transport supervision | `cancelled`, `disconnect`, `inactivity`, `overall-deadline` |
| raw response-body safety cap | `raw-body-overflow` |
| SSE safety caps/grammar | `sse-line-overflow`, `sse-event-overflow`, `invalid-sse` |
| strict wire decoder | `invalid-wire-utf8` |
| JSON event/escape decoder | `invalid-json-event`, `invalid-json-escape` |
| decoded answer scalar encoding | `invalid-answer-utf8` |
| post-unescape Provider Chunk / concatenated decoded bytes | `chunk-overflow`, `total-overflow` |
| terminal and post-terminal assembly | `missing-terminal`, `duplicate-terminal`, `contradictory-terminal`, `post-terminal-data` |
| provider error response | `provider-failure` |

`validationOutcome` is a separate exact vocabulary: `syntax-repair-required`, `accepted`, `rejected-before-parse`, `rejected-syntax`, `rejected-semantic`, or `cancelled`. It never substitutes for a transport rejection. Every rejected transport or validation state discards all untrusted buffers and produces no partial answer.

The cross-artifact consistency vector compares these literal terms and the post-JSON-unescape 16,384-byte Provider Chunk / concatenated decoded 65,536-byte response domains against `plan.md`, the streaming contract, release contract, and schema 2.1. Any generic UTF-8 rejection synonym, missing category, or before-decoding acceptance count fails the vector.

## 9. `ValidatedRevealState`

Transient Web presentation model distinct from provider streaming.

| Field/state | Rule |
| --- | --- |
| `Pending` | generic activity/cancel state only; no provider fragment or evidence detail |
| `AcceptedAnswer` | complete immutable answer after all syntax/evidence/citation/currentness/authorization validation |
| `RevealMode` | Unicode text-element progressive reveal or immediate complete render |
| immediate condition | reduced-motion requested or remaining shared deadline cannot safely complete reveal |
| terminal | complete answer, bounded Chinese outcome, cancelled, or failure; no partial-answer terminal |

## 10. `ProductionScanStatusView`

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

## 11. `ChatEvaluationCase`

Schema: [story125-evaluation-case.schema.json](contracts/story125-evaluation-case.schema.json).

| Field group | Required content |
| --- | --- |
| identity | stable case ID, source authority references |
| scope | release System alias and exactly two Repository aliases; never credentials/paths |
| question | mode, question, optional prior-case ID for follow-up |
| expectations | evidence themes, relationship kinds, useful-answer rubric, citation expectation, authorization expectation |
| observation | status, duration, answer/citation fingerprints, usefulness/citation/leakage result, limitation |

The corpus has 12-20 entries. `expectedAnswer` is forbidden. Follow-up references must be acyclic and form chains no longer than three questions.

## 12. `ReleaseEvidenceRunEnvelope`

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
| Chat receipt | corpus and quality aggregates plus one case receipt per case; invoked attempts include signed raw-adapter candidate/case/request/provider/request/header/read/EOF provenance, per-domain counters, result/packet/stream artifacts, and zero non-streaming accepted answers |
| restart/database receipts | pre/post restart artifacts, duplicate-query receipt, database preflight/ownership/cleanup artifacts and zero forbidden-database access |
| screenshots | at least one each for real Scan state, grounded Business answer, grounded IT answer, restart, and release summary; candidate-bound, production-state, hashed and redaction-passed |
| limitations | explicit bounded list inside a passed receipt; never hidden by aggregate pass |

The passed check inventory is fixed: Tier 1 `tier1-contract-ranking-relationship`, `tier1-conversation-synthesis-security`, `tier1-production-ui-corpus-evidence`, `tier1-build-full-tests`; Tier 2 `tier2-postgresql-restart-resume`; Tier 3 `tier3-candidate-tool-database-preflight`, `tier3-real-scan`, `tier3-grounded-chat-corpus`, `tier3-restart-continuity`, `tier3-screenshot-release-integrity`.

Each case receipt declares `providerDisposition`. `invoked` requires a packet/fingerprint and exactly one primary stream plus, only when declared, one ordered syntax-repair stream. `not-invoked-insufficient-evidence` and `not-invoked-authorization-boundary` require null packet fields and zero stream attempts. An accepted case requires `invoked`, one completed final attempt, and total accepted-answer duration 0-29,999 ms. Operational deterministic rescue is a bounded outcome, not a streamed accepted provider answer.

JSON Schema enforces the closed shape, status-conditioned required fields, raw-adapter provenance, stream-attempt cardinality/order/domain bounds, and exact mandatory-check inventory. The semantic validator authenticates the same-process signature and resolves candidate/case/request/adapter/provider/stream-request/header/read/EOF plus every command, observation, and artifact against the same envelope. It rejects a perfectly consistent SDK aggregate fake-chunk receipt solely when raw-adapter provenance is absent, then retains all prior Scan/Chat/restart/database recomputation, corpus coverage, zero non-streaming acceptance, exact two-decimal half-up usefulness, and immutable supersession rules.

Database names must begin `vulgata_story125_`, contain only lowercase ASCII/digits/underscore after the prefix, be unique for the run, and differ from configured development and Story 12.4 qualification database names. Validation occurs before a product connection.

## 13. Relationship overview

```text
ChatConversationState (transient)
  -> ChatRetrievalRequest
  -> current IndexEntry/DocumentSection
  -> RankedEvidenceSection
  -> current DocumentEdge
       -> CrossRepositoryResolutionDecision (required for cross-Repository REFERENCES)
       -> published FlowGroup -> FlowTrace -> FlowTraceStep
  -> EvidencePacket v2
  -> ChatSynthesisAttempt primary stream (optionally syntax-repair stream)
  -> strict ChatProviderStreamFrame assembly
  -> validated GroundedAnswer -> ValidatedRevealState
  -> append ValidatedConversationTurn

ScanExecution -> ScanJobExecution -> FileCodeUnit
  -> ProductionScanStatusView -> production management UI
  -> existing durable manual resume -> refreshed ProductionScanStatusView

ChatEvaluationCase[] -> Tier 3 attempted observations -> ReleaseEvidenceRunEnvelope
  -> passed-only ReleaseReceipt (only when every final check passes)
```

## 14. Migration disposition

No model above requires EF persistence. No migration, configuration, `VulgataDbContext`, or model snapshot change is planned. Discovery of an unavailable durable invariant blocks the affected track and requires revised authority/ownership; it does not authorize an opportunistic migration.
