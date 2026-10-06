# OCT-CHAT logical data requirements

These are Chat domain requirements supplied to OCT-BUSINESS, the sole October persistence/migration owner. They are not SQL, a second DbContext, or final shared Knowledge type definitions. Exact B0 types and storage mapping are accepted at checkpoint K0 in [plan.md](plan.md).

## Conversation

Fields: server-issued ConversationId; authenticated SubjectId; immutable Mode; canonical authorized Repository selection (one/several/standalone allowed); exact BatchId and PublicationId from Knowledge; context identity; created UTC time; next ordered turn sequence; optional active turn identity/fence.

Rules: identifiers never supplied as model authority. Ownership and current disclosure authority checked on access. Scope/mode/publication changes require a new empty conversation. Existing authorized dialogue remains retained. Unique active turn is enforced in storage, across tabs/request races, not only an in-memory lock.

## Chat Turn

Fields: server-issued TurnId; ConversationId; monotonically ordered sequence; idempotent client request key and canonical request digest; user question; fixed Knowledge Read Context identity; provider profile identity (no credentials); owner execution/fence token; original deadline/budgets; execution status; terminal outcome operation key/digest; timestamps.

Begin semantics: same key and same digest returns the existing turn/status without redispatch. Same key/different request conflicts. Another request while active or saving-indeterminate is rejected as busy. Only successful durable begin/fence ownership can dispatch. A restarted host never dispatches an existing running record.

## Inference input lineage and exact-read ledger

- Knowledge-owned handles identify exact Document Version/optional section in one publication; no mutable current-version substitution.
- Each successful authorized content/section read is associated with TurnId and recorded as eligible reference evidence. Search titles, related metadata and prior-turn references alone do not qualify as content reads.
- Track actual successfully returned chunk coverage separately from its exact Document Version/section identity, using Business's existing coverage/continuation semantics. A prefix is not the whole Document/section. Owner-issued opaque continuation remains bound to the original selection/target and is not a Chat-generated offset or durable authority; only complete successful pages extend coverage. Preserve material claim-group qualification/attribution across chunks and retain partial coverage with terminal references as needed for truthful history/footer disclosure. No competing Knowledge type or schema is defined here.
- Track all contributor dependencies of everything actually sent to the provider: admitted question/context, reused completed history, search/related metadata and content. This may conservatively exceed the reported footer.
- Authorize that union before subsequent disclosure. The storage owner persists dependencies needed to recheck saved prose, question and footer; use B0's canonical contributor representation, not a Chat-owned access schema.
- Runtime ledger is bounded and need not be a durable tool transcript/token journal. Terminal references and complete inference lineage are durable.

## Terminal outcome

Fields: TurnId/fence; operation key and canonical payload digest; authorized answer text; Completed/Interrupted/Failed; sanitized reason; ordered exact references; all inference contributor dependencies; provider finish classification; outcome timestamp. Persist question and answer in durable order; references bind to exact original versions.

Atomicity: content, references, dependencies, status, sequence outcome and active-turn release commit together. Identical terminal retries return the same saved result; conflicting payload/fence is rejected. Lookup distinguishes committed, definitely absent, conflict and indeterminate. Timeout/lost acknowledgement is not proof of absence. Keep saving-indeterminate turn active until lookup or safe identical save determines its result; do not allow an overlapping new generation.

## Lifecycle

`Admitted -> Running -> Saving -> Completed | Interrupted | Failed`

`Saving -> SaveIndeterminate -> lookup/identical-save -> terminal or still indeterminate`

- Completed: provider has valid final stop/end, no truncation, no unfinished tools, live disclosure authorization remains valid, and atomic save succeeded.
- Interrupted: explicit cancellation, original deadline/budget exhaustion, host orphaning or interrupted transport after useful text; retain only currently authorized useful text.
- Failed: invalid configuration/protocol or other terminal failure with sanitized reason; useful permitted partial text can be retained but never becomes normal completed history.
- Revocation: suppress affected text/references and cancel work; terminal record can retain a safe generic status. Previously committed content is not disclosed again without live access checks; previously delivered text cannot be recalled.
- Browser disconnect: no new dispatch and no automatic cancellation. Host-owned work continues within original limits and saves; there is no live reattachment. Dropped UI subscribers cannot block execution indefinitely.
- Host failure: Business-owned storage operation fences orphaned active records and records interruption when the execution owner is confirmed gone. Shared composition must choose a single-host owner generation or equivalent safe owner-death proof; age alone cannot steal a live turn. No automatic regeneration, durable per-token recovery or cross-host scheduler.

## History projection and validation

Select ordered completed turns only for model history; preserve last contiguous permitted suffix fitting the configured message/character budget and indicate omitted context without generated summaries. Never truncate a message into an apparently complete fact. Recheck all contributors before reuse; discard the affected whole turn, not only its reference. Display interrupted/failed saved outcomes only when permitted and explicitly labelled.

Saved answer prose and reused history preserve Business's material SourceSupported/Advisory/Hypothesis/Uncertain/Mixed qualifications and explicit mixed-clause attribution. Preserve owner-issued claim-group provenance in supplied inference context; source/advisory contributor identity alone is not visible attribution. Never strip a motive's advisory qualifier while keeping the motive, or silently promote a hypothesis through follow-up. If bounded history cannot retain qualification with its claim, omit the affected group/turn. This constrains the existing dialogue contribution; it does not expand or implement the separately deferred persistence contract.

Validate request length, IDs, mode, scope and bounded tool arguments with FluentValidation and domain checks. Persistence mappings belong in Infrastructure, with no EF/IQueryable exposed in Core. No schema changes are made by these artifacts.
