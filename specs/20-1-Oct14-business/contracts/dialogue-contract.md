# Canonical October dialogue contract v1

Business-owned agreement candidate, 2026-10-07. The corresponding declarations are
`src/dotnet/Vulgata.Core/DomainServices/OctoberDialogueContracts.cs`, namespace
`Vulgata.Core.DomainServices.OctoberDialogue`. This evaluates and supersedes the proposed durable
shapes in Chat contribution `f3db38dbd467de4c1ea2bfb89b8480f601f51291`; it is a declaration
checkpoint, not a claim of implementation, migration, runtime or consumer conformance.
Business remains the sole October schema writer. Chat owns dialogue execution/UI. Integration
owns live execution identity, provider capture, owner-death evidence and shared composition.

## Accepted shape and explicit counterproposals

Reuse existing `OctoberChatLimits`, `OctoberChatReference`, `KnowledgeSubject`, exact Knowledge
bindings/handles and the Business-owned dependency/consumed-content capsules. There is no
Chat-specific contributor schema, generic operation framework, token journal or reconnect engine.
The conversation has one active turn slot across **Admitted, Running, Saving and SaveIndeterminate**.
A Running-only index does not satisfy the invariant. Its owner, context, publication, mode and
scope never change; purpose is always `KnowledgePurpose.Chat`. A different selection starts a
new empty conversation and cannot import an IT transcript into Business.

`OpenConversationAsync` combines creation and reopening. Null `CreateIfMissing` is open-only;
creation requires host-issued conversation/context IDs and takes the current authenticated subject
as owner. Existing identity must exactly match any supplied creation selection. Timestamps, turn
IDs, sequence and fence are store-issued. Checked monotonic counters never wrap. Failed admission
consumes no committed sequence. The complete original `PublicationBinding` is retained; no latest
selection, alternate database or stale authentication-binding fallback exists.

`ProviderProfileId` identifies the selected existing provider row. `ProviderProfileRevision` is a
nonsecret, host-issued immutable **admission snapshot ID**, optionally associated with UpdatedAt;
it is not a new versioned credential store or promise of replaying old credentials. Integration
privately captures existing provider fields/protected credential value and checks current
eligibility plus equality before each outbound request. Persist only the row identity and opaque
snapshot revision, never credentials, their digest, endpoint secrets or a public credential identity.
Restart does not resolve that snapshot into a new generation. Historical traceability is limited
to those stored identifiers unless the host separately retains an authorized configuration snapshot.

## Exact bounds

All strings are well-formed Unicode, measured with strict UTF-8. Preserve text exactly: no trimming,
replacement, normalization or silent clipping. All IDs are nonempty Guids; UTC timestamps have
zero offset. Collections are non-default immutable arrays. Both individual and whole-envelope
limits apply; satisfying individual maxima does not promise their combination fits.

| Item | Canonical maximum / constraint |
| --- | --- |
| Question | Nonblank; 8,000 Unicode scalars and 32,000 UTF-8 bytes |
| Answer | 65,536 UTF-8 bytes; Completed is nonblank; interrupted/failed may be empty |
| OwnerSubjectId | Nonblank, 1,024 bytes |
| CanonicalDatabaseIdentity / TargetId | Nonblank, 4,096 / 256 bytes; dialogue-entry limits only, not a rewrite of Knowledge identities |
| ProviderProfileId / ProviderProfileRevision | Nonblank, 256 bytes each |
| SelectionDigest | Existing canonical Knowledge SHA-256, exactly 64 hex characters; preserve owner casing |
| RequestDigest / OutcomeDigest / PromptDigest / HistoryProjectionDigest | Exactly 64 uppercase ASCII hex characters |
| RepositoryScope | 1..256 distinct Guids, sorted by lowercase Guid-D ordinal order |
| SectionId | Null or nonblank, at most 1,024 UTF-8 bytes; exact existing section identity |
| References / ConsumedReads | At most 256 distinct exact targets each; each complete array at most 524,288 serialized bytes; reference order is first report order |
| SuccessfulChunks / ContentBytes | Successful target requires chunks >=1 and bytes >=0; checked int/long, exact owner proof facts, within actual admitted counts/accounting |
| History | At most 12 distinct prior turns, in admitted sequence order; at most 49,152 bytes of exact model projection |
| Begin / terminal envelope | At most 1,048,576 / 2,097,152 UTF-8 bytes, including actual protected capsules |
| Dependency capsule | At most 4,096 contributors, 128 origins, 512 KiB canonical plaintext and 1 MiB protected payload; all bounds apply |
| Diagnostics | Enumerated reason and provider finish only; no exception/provider diagnostic prose |
| Sequence / fence / next sequence | Positive signed long, checked increment; refuse on exhaustion |
| Persistence attempt | At most ten seconds; timeout is not rollback proof |

The capsule limits counterpropose Chat's two-MiB plaintext union. Use the exact values in
`OctoberKnowledgeDisclosureLimits`; do not silently discard old origins. Per-target consumed
proofs contain their own bounded stream facts, not repeated full inference capsules. Their actual
bytes still count in the consumed array and terminal envelope. Protected-key retention must cover
the lifetime of saved dialogue; unavailable keys fail closed and never create empty lineage.

Keep all existing `OctoberChatLimits` fields positive. Question/output/history/page/content bounds
cannot exceed their corresponding canonical ceilings (page 100; content-result 65,536). Other
positive execution fields remain host deployment budgets within their numeric types; the defaults
are not newly invented schema limits. The original duration is fixed at admission; calculate UTC
deadline with checked arithmetic. Host monotonic enforcement owns actual elapsed execution.
Larger limits never authorize exceeding capsule/envelope/target caps: reject invalid configuration
before admission and reject an oversized dependency extension before model input. No budget resets
on retry, continuation, disconnect or lookup. Generation can fail or be cancelled and still drain
successfully; **settled means physical request termination and permit/resource release**, not a
successful provider outcome. Only unproven cleanup blocks terminal release.

## Admission, fencing and commit semantics

- `BeginTurnAsync` validates the current owner/exact selection, initial dependency capsule, exact
  history projection and immutable limits/profile snapshot, then occupies the slot and allocates
  sequence/turn/fence in one short transaction. Unique `(ConversationId, ClientRequestKey)` gives
  exact replay; changed logical request conflicts. Another key while any nonterminal state owns
  the slot is Busy. Only a freshly confirmed admission for this live registered execution returns
  `MayDispatch=true`. Lookup/replay/restart always return false. Unknown admission never dispatches.
- `LookupAdmissionAsync` resolves the original key/digest before rebuilding history or provider
  metadata. It never grants a second execution, updated limits or takeover. Lack of a visible row
  while an original submission can finish is Indeterminate, not ConfirmedAbsent.
- `TransitionTurnAsync` is a fenced compare-and-set, not ownership transfer. Forward phases are
  Admitted -> Running -> Saving and Saving -> SaveIndeterminate; identical recovery may move
  SaveIndeterminate -> Saving, never back to generation. Replayed transitions grant no dispatch.
  Admission may terminate without Running when dispatch failed or cancellation occurred.
- `SaveTerminalAsync` verifies the original owner/fence, actual generation finish and settled
  owned work through mandatory `IOctoberDialogueExecutionAuthority`. Completed requires ValidStop,
  no pending tools, None reason, nonblank answer and admitted execution bounds. Failed/cancelled
  but physically settled work can save its truthful non-Completed outcome. No long transaction
  spans inference or tool calls.
- One transaction writes immutable outcome/text, complete inference capsule, verified consumed
  targets/references, receipt and terminal state, then clears the active slot. Partial reference
  rows, early release and TurnSaved-before-confirmation are forbidden. Same operation/digest/owner
  fence returns the same receipt; another terminal operation or changed outcome cannot overwrite.
- `IOctoberDialogueCommitGuard` uses the existing F1 `SubmittedCommit`/settlement mechanics but no
  Scan `BusinessWorkKind`, finalization operation or generator execution scope. Recheck owner,
  fence, phase and applicable live authority immediately before actual provider CommitAsync under
  the same guard as fencing. The callback invokes that API synchronously, then returns its already
  submitted settlement task. Cancellation after submission requires bounded exact reconciliation.
- SaveIndeterminate is a consumer/active-state observation, **not an instruction to overwrite an
  already committed terminal row**. A terminal may already have committed and released its slot;
  its durable truth wins. Where the outcome is still unknown, no new generation is authorized.
  ConfirmedAbsent is permitted only when store-owned serialization proves the earlier submission
  cannot still commit; ordinary absence is never enough. A safe identical save retries storage only.

Mutation IDs are ConversationId for creation, ClientRequestKey for admission, SaveOperationId for
terminal save and OrphanOperationId for orphan cleanup. Transition mutation identity is derived
by Business from the exact ownership/expected/next-state tuple; consumers do not invent it.
Only the current registered host execution can act on its ownership tuple. Authentication stamps,
cookies and read tokens are ephemeral and never persisted as grants.

### Revocation and orphan cleanup

Normal content save and all content disclosure require fresh owner and full-input authorization.
If authority is denied/unavailable after generation, the original fenced, **settled** host may close
with Interrupted or Failed, reason AuthorizationDenied or AuthorityUnavailable, empty AnswerText,
empty References/ConsumedReads and null InferenceDependencies. This is a narrowly sanitized closure,
not successful content save or an authorized empty lineage. It stores no newly supplied unverified
capsule, never creates inference history and returns no content projection. Admitted immutable
question/dependencies remain historical facts; they are not disclosed through this exception.
The host may retain the terminal receipt for bookkeeping; forwarding even metadata to a user still
requires current conversation-owner access. The commit guard distinguishes this no-content cleanup
from permission to save or disclose prose.

`InterruptOrphanAsync` is host-only and has no user content projection. The mandatory execution
adapter must establish the exact owner generation/attempt is dead and its owned provider/tool work
is settled from authoritative host facts, never a caller Guid, age, deadline or missing heartbeat
alone. In a short serialized transaction, a racing committed terminal wins. Otherwise invalidate
the old fence and atomically record Interrupted/OwnerLost with no invented partial text and release
the slot. Unknown provider finish is Unknown, not fabricated NotStarted/ValidStop. A database commit
that may still settle must first be reconciled under the conversation/turn lock; timeout remains
Indeterminate. Restart recovery never regenerates and cannot resume an old admission. A runtime
adapter unable to prove death/settlement blocks only that cleanup; no new proof-ID framework is added.

## Dependencies, references and history

All lineage uses the canonical owner-protected `KnowledgeDependencyHandle`; a SetId is not a
lookup-row prerequisite or access grant. Initial lineage covers the exact question/static context
plus every reused history/context input. Question-only initialization requires an explicit owner
no-Knowledge attestation. Missing lineage is unavailable, never an empty authorized set.
The final capsule must contain every admitted input across rounds, including uncited search,
related, uncertainty, coverage and advisory inputs. Use owner Merge/Reauthorize on the supplied
capsule plus required admitted/history capsules; the merged semantic BindingDigest must remain
identical to the claimed complete aggregate. Dialogue never reconstructs hidden contributor unions.

For each `DialogueConsumedTarget`, call owner `VerifyConsumedContentAsync` with the fresh exact
Chat selection, consumed proof and full inference capsule. It verifies origin containment and
returns authoritative document, partial flag, unique chunk count, raw content bytes and separate
source coverage. Every claimed field must match. References must be a deduplicated ordered subset
of verified consumed targets. Search/history/related data earn no exact-content credit. Empty footer
is valid but does not remove dependencies. A complete chunk chain never upgrades partial source
coverage to complete analysis; null owner coverage remains unknown. Persist the verified coverage
facts with the terminal contribution rather than relying on expired cursor strings.

`ReadHistoryAsync` supplies only the newest contiguous eligible Completed suffix. A non-Completed,
denied, unverifiable or oversized whole turn is a boundary; do not skip it to stitch older facts.
Return chronological order within the suffix, whole turns only, and a generic ContextOmitted flag.
Reauthorize full original capsules before projection and again before model submission. Begin checks
its exact IDs/projection digest against the admissible current suffix; an arbitrary old paging bound
cannot bypass a newer boundary. Empty history has no dependency grant; create/merge the explicit
question root before model dispatch. No summaries, rewritten qualifications or cross-mode import.

`ReadTranscriptAsync` is a separate bounded UI display path for authorized Completed, Interrupted
and Failed outcomes with explicit state/reason. It does not require the browser to know save operation
IDs/digests. It uses the same fixed owner/selection, byte/count bounds and live checks; denied content
is omitted without hidden titles/counts and only a generic omission signal. Sanitized closures can
project only generic state/reason with empty question/answer/references and null dependencies after
current owner access; they cannot expose admitted content through the cleanup exception. Never feed
this display transcript directly to inference. `LookupTerminalAsync` separately reconciles known
operation/digest/fence and can return a receipt without a currently authorized content projection.

Dependency/proof fields and the nested admitted request are JsonIgnore in outward store results.
These host-only records are not API/model DTOs. Durable codec envelope projections explicitly include
the actual protected capsules despite JsonIgnore; those attributes are not a size-accounting bypass.
Persist each final inference capsule once; consumed proofs do not embed it again. Data Protection
keys and protected bytes must survive with saved history, without turning protection into permission.

## One codec and exact canonical bytes

`IOctoberDialoguePayloadCodec` is a declaration for the sole Business implementation. It reuses
`OctoberKnowledgePolicy.SerializeCanonical` and `.Hash` over explicitly projected logical objects:
UTF-8, no BOM/trailing newline, property keys ordinally sorted, numeric enum values, lowercase Guid-D
strings, exact strings without Unicode normalization, and only integer numeric values. UTC time
strings use `yyyy-MM-ddTHH:mm:ss.fffffff+00:00`; durations use integer `durationTicks`. This deliberately
counterproposes Chat's separate Guid-N/string-enum digest format. Scope sets sort ordinally by Guid-D;
reference, consumed-target and history orders are retained. Capsule semantic identity is BindingDigest,
not randomized protected ciphertext, issuance Guid or issuance time. Authenticate the complete protected
handle before trusting that digest. Request/OutcomeDigest itself and transport authentication are excluded.

The logical objects have these exact camelCase keys (nested binding/reference fields use their existing
record camelCase names; publication includes its full Knowledge binding):

- Begin: `{domain:"october/dialogue-begin/1",schemaVersion:1,conversation,request}`. `conversation`
  contains every DialogueConversationIdentity field plus `purpose` = numeric Chat. `request` contains
  clientRequestKey, question, providerProfileId, providerProfileRevision, promptDigest, historyTurnIds,
  historyProjectionDigest, initialDependencyDigest and limits. `limits` contains every OctoberChatLimits
  property except duration, replaced by durationTicks. Exclude Execution, requestDigest and store-issued
  timestamps/sequence/turn/fence; ConversationId/ContextId in the request must equal the conversation.
- Terminal: `{domain:"october/dialogue-terminal/1",schemaVersion:1,owner,saveOperationId,terminalState,
  reason,providerFinish,answerText,references,consumedReads,inferenceDependencyDigest,historyTurnIds,
  generationEndedAtUtc}`. Owner includes the full execution identity/fence. Each consumed entry is
  `{reference,proofBindingDigest}`. The nullable inference digest is legal only in the sanitized closure.
  GenerationEndedAtUtc is the original frozen host observation, not a new retry timestamp. Receipt time
  is excluded. Identical retry reuses the entire original request and save operation.
- History digest: `{domain:"october/dialogue-history/1",schemaVersion:1,turns}`. Each turn contains
  turnId, sequence, question, answerText, references and inferenceDependencyDigest.
- Model history bytes: the compact JSON array of `{question,answerText,references}` entries in that
  same order, camelCase, numeric enums, default System.Text.Json escaping, explicit nulls, no indentation.
  EncodeHistoryProjectionUtf8 is the exact projection handed to Chat; it excludes all host capsules.
  MeasureHistoryProjectionUtf8Bytes equals the encoded length; wrappers added by Chat must be separately
  accounted inside the original execution context rather than presumed free.

Durable begin/terminal envelope measurement uses an explicit complete host serialization of each request:
compact System.Text.Json, camelCase, numeric enums, default escaping, explicit nulls, no indentation,
including all capsule fields and ownership/digest fields. Duration uses standard invariant TimeSpan
`c` text in this envelope only; canonical logical limits use durationTicks. JsonIgnore does not exclude
capsules from this explicit persistence measurement. References/consumed-array caps use the same host
encoding; no capsule stripping or alternate UI serialization may satisfy these bounds.

A static canonical history vector (no application codec/test executed at this declaration stage):

```json
{"domain":"october/dialogue-history/1","schemaVersion":1,"turns":[]}
```

UTF-8 length 68; SHA-256 `2D6A9251579D2AB4E8B5C0C28897CE4A9905ABA98E504EB9C273F1A62960D0D8`.
The empty model-history encoding is `[]` (two bytes), not a no-Knowledge attestation. Future codec
implementation must supply full begin/terminal Unicode/boundary/retry vectors and consumer conformance
before runtime use; this static vector does not claim that implementation or actual byte compatibility.

## Checkpoint and remaining dependencies

These declarations supersede the former four-state/Running-only task shorthand, not compatible TDD,
authorization, immutable history or verification requirements. Root updates tasks/data-model and commits
this canonical candidate for consumer review. No store, EF mapping, migration, test, runtime or automatic
approval is created here. Implementation requires the agreed consumer field mapping plus authoritative
host execution/settlement/owner-death and provider-snapshot adapters. Missing adapters never justify
permissive defaults. T036–T039 will prove actual admission/save/replay/history and owned PostgreSQL races;
Chat/integration retain streaming, safe output release, UI and real runtime qualification.
