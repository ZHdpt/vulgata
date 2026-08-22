# Research: Resumable File Pass 1 & Code Logic Documents

## Authority set

This research resolves the design choices left to Story 12.3 without changing its intent. The controlling sources are the Story 12.3 specification and BMad story, the accepted Story 12.1 and 12.2 contracts, the August 30 bounded-scanning design and approved recovery/host-runtime proposals, the PRD delivery overlay, the Constitution, `AGENTS.md`, and the current source seams named below. There is no repository ADR or context document in this checkout.

## R-1: Isolate the prepared Pass 1 path from the legacy sequential path

**Decision**: Add Story 12.3-specific ports and types (`IPreparedPass1Store`, `IPreparedPass1WorkerDispatcher`, `IPass1NavigationService`, and `IPass1CodeLogicDocumentCommitter`). Reuse the accepted durable entities and analysis ports, but do not register the Story 12.2 handoff with `SequentialScanRunner` and do not reinterpret legacy `WorkerAgentJobPacket`, `WorkerAgentResultPayload`, `WorkerAgentDispatcher`, or `KnowledgeDocumentMaterializer` behavior.

**Rationale**: Story 12.2 AD-8 explicitly grants no dispatch. The current sequential dispatcher is one-shot, uses `vulgata.knowledge-document/1.0`, and lets the Worker supply Markdown/search fields. Story 12.3 requires a bounded tool loop and server-derived projections. A separate prepared path preserves Story 11 and accepted Story 12.1/12.2 behavior while allowing the new contract to be strict.

**Alternatives considered**:

- Mutate the legacy packet/result DTOs in place: rejected because historical prepared-demo tests and Pass 2 bindings depend on their exact meaning.
- Route the handoff into `SequentialScanRunner`: rejected because it is single-flight, recomputes legacy planning, and cannot enforce pair-wide concurrency or provider circuit state.
- Replace the knowledge store: rejected because Logical Document, immutable Document Version, Source Anchor, Document Section, Index Entry, and Document Edge are reusable authorities.

## R-2: Pair-wide execution aggregate and atomic start

**Decision**: Persist one `Pass1PairExecution` per approved coordination and pair-package fingerprint. In one PostgreSQL transaction, lock and reconstruct both prepared packages, revalidate current authorization and both child Scan Executions, insert the pair execution and the complete deterministic `ScanJobExecution` set, create one navigation budget per job, move both child executions to `Running`, and move the coordination from `Preparing` to `Pass1`. Nothing is claimable before commit.

The pair stores each canonically ordered child Scan Execution's exact prompt and analysis-runtime identities separately and requires their schema identities to be `code-logic/file-v2`; it never assumes the two child bindings are otherwise equal. Start stores immutable initial and current effective concurrency, defaulting to two and accepting only 1-4. Exact start replay compares the initial value and returns current state; a different start value conflicts rather than silently updating. Active reconfiguration uses a separate authorized/versioned command and immutable audit record. Concurrency is deliberately excluded from job, Logical Document, Document Version, reconciliation, warning, and display identities.

**Rationale**: Story 12.1 already supplies deterministic jobs and version-conditional durable state. Story 12.2 supplies exact two-manifest identity and pair ordinals. A small aggregate binds them without duplicating manifest contents.

**Alternatives considered**:

- Create jobs lazily while dispatching: rejected because a failure would expose a partial queue.
- Add `PairSelectionOrdinal` to `ScanJobExecution`: rejected because it already exists authoritatively on `PreparedScanManifestEntry`; duplicating it creates drift.
- Use an in-memory semaphore only: rejected because it cannot enforce the bound across restart or competing hosts.
- Treat a changed Start replay as reconfiguration: rejected because it makes acknowledgement replay ambiguous and has no explicit expected-version/authorization transition.

## R-3: Claim order and shared concurrency

**Decision**: Claim under a transaction that locks `Pass1PairExecution`, recovers strictly expired leases and due nonexhausted Story 12.3 retries, counts unexpired `Running` leases across both bound Scan Executions, and selects the smallest eligible Story 12.2 `PairSelectionOrdinal`. The claim then applies the accepted `ScanJobExecution.AcquireLease` transition and returns a server-only current version/lease authorization plus earliest durable eligibility; a specific packet source double-checks it after revision-bound source hydration. An authorized `PATCH` command may version-conditionally change the persisted effective limit among 1-4 before reconciliation. A decrease preserves every valid lease and closes claims while active lease count is at or above the new limit; an increase applies only to claims after the update commits. Each effective change has a canonical immutable `Pass1ConcurrencyChange`; exact lost-ack replay is zero-write. Database row order, Repository order, completion order, and change timing are never identity/order selectors. Repository-local progress is projected by `ManifestOrdinal`; pair progress and warnings use `PairSelectionOrdinal`.

**Rationale**: Locking one bounded pair row is simple and makes the 1-4 shared limit observable. The accepted job unique constraints remain the authoritative per-file identity guards.

**Alternatives considered**:

- `SKIP LOCKED` without a pair lock: rejected because two claimers could both observe capacity and exceed the shared bound.
- Per-Repository semaphores: rejected because the requirement is pair-wide.
- Completion-driven next-file selection: rejected because timing would change presentation and potentially starvation behavior.
- Revoke or shorten leases on decrease: rejected because the specification preserves already-valid leases and Story 12.1 lease authority.

## R-4: Bounded `navigate_code` implementation

**Decision**: Expose exactly one Microsoft.Extensions.AI `AIFunction` named `navigate_code` through a new explicit prepared dispatcher. Its request has one closed operation enum: `Definition`, `References`, `Implementations`, `Callers`, `Callees`, `SymbolSearch`, `RelatedFiles`, or `ExactSourceEvidence`. The server supplies pair/job/source identity plus the current expected job `StateVersion`, lease token, and lease expiry; those values are never tool arguments, provider content, API output, receipt content, or Document content.

The facade composes only accepted typed seams:

- current Code Logic Document/section/anchor lookup through a specific read port;
- JDT LS definition/references/implementations for Java;
- CodeGraph symbol search/callers/callees/one-hop related files;
- Story 12.2 approved inventory plus the revision-bound source reader for exact source evidence.

Every target is intersected with the approved Repository/revision snapshot. Results outside the authorized scope become the same `Unavailable` outcome as absence. No raw diagnostic, command, path, protocol payload, or storage identity crosses the facade.

**Rationale**: The current packages (`Microsoft.Agents.AI.OpenAI` 1.13.0 and `Microsoft.Extensions.AI.Abstractions` 10.7.0) expose `AsAIAgent(... IList<AITool> ...)` and `AIFunction` support, but the existing dispatcher passes no tools. The new dispatcher can therefore expose one function without adding a framework dependency.

**Alternatives considered**:

- Raw LSP/CodeGraph commands in the prompt: rejected by security requirements and the accepted runtime contracts.
- One tool per operation: rejected because it widens the exposed surface and complicates a single shared budget.
- Await another file's job: rejected because navigation must return immediately and cannot schedule work.

## R-5: Bound the entire Agent loop, not only successful navigation

**Decision**: Use an explicit dispatcher loop with at most ten provider turns: one initial turn, at most eight tool-invocation turns, and one final `code-logic/file-v2` turn. There are at most eight tool invocation attempts; valid accepted calls also consume the durable call/evidence budget. Rejected calls consume a tool-attempt turn so repeated invalid calls cannot create an unrestricted loop, but add no evidence. A retry of the same deterministic job receives the remaining durable budget rather than a reset.

**Rationale**: The eight-call and 64 KiB limits alone do not stop a model from repeatedly issuing rejected calls. Persisted counters prevent retry/restart from evading the bound.

**Alternatives considered**:

- Rely on framework defaults: rejected because their iteration limit is not the Story 12.3 contract.
- Reset budgets per attempt: rejected because a retried job is still the same job.
- Count rejected evidence bytes: rejected because no evidence is returned, but the separate attempt cap still bounds behavior.

## R-6: Durable navigation receipts without raw source persistence

**Decision**: Each accepted navigation call conditionally advances `Pass1NavigationBudget` and inserts one immutable `Pass1NavigationReceipt`. The receipt stores receipt/tool-attempt ordinals, operation/depth, canonical request/result hashes, result count, UTF-8 evidence bytes, and bounded evidence descriptors (Document/section/anchor IDs or path/range/source-hash metadata), never source text. A results/bytes/depth refusal instead writes a metadata-only `BoundExceeded` receipt with zero descriptors/evidence; when either accepted-call or tool-attempt count reaches eight and disables further calls, the same transaction appends one Calls exhaustion marker without a ninth tool attempt. These refusal receipts authorize only a deferred bound-exhaustion statement. The result validator requires receipt identities for navigation-derived claims and independently revalidates their current descriptors or budget/refusal state before commit.

An analysis call runs outside the database lock. Before analysis, scope resolution verifies the job is `Running`, its expected version/current unexpired lease matches, and the pair is `Running` with a closed circuit. The complete response is then bounded and canonicalized; one short transaction locks the pair, job, and budget, repeats the same lease/version/expiry/phase/circuit checks, revalidates remaining capacity, writes the receipt/counters, and only afterward releases the response to the Worker. A stale/replaced/expired/paused/terminal binding returns the same empty `Unavailable` shape and commits no receipt. A failure before receipt commit returns no evidence. A lost response may consume capacity safely but cannot exceed it.

**Rationale**: Counts must survive retry and restart, while FR-044 forbids raw source/provider content as status evidence. Hash-and-descriptor receipts provide a testable authorization chain.

**Alternatives considered**:

- Persist the whole tool transcript: rejected because it duplicates source and provider content.
- Keep counters only in memory: rejected because retry/restart could exceed the bounds.
- Reserve bytes before analysis: rejected because exact response size is not yet known and partial truncation is forbidden.
- Return BoundExceeded without a durable reference: rejected because a first refusal would make the required truthful deferred observation schema-impossible.

## R-7: Authoritative JSON and deterministic projections

**Decision**: `code-logic/file-v2` is a strict closed JSON grammar. Duplicate/unknown properties, invalid UTF-8, BOM, JSON depth over 32, total bytes over 65,536, unsafe text, non-NFC strings, CR line endings, and noncanonical item ordinals are rejected. Communication kind is exactly `Rpc` or `Http`; each referenced Story 12.2 classification is echoed as one closed binding and must exactly match its persisted ID/fingerprint/kind/level/confidence/rule/provenance/Catalog Rule Identity/Evidence Locator, with every non-null kind equal to the enclosing authored kind. Bound-exhaustion deferred observations cite metadata-only refusal receipts and cannot turn them into source or relationship proof. The Worker supplies no server UUID, Markdown document, section Markdown, search text, Index Entry, Edge ID, or commit fingerprint.

After semantic validation, a server writer emits `ContentJson` in the exact property order and array order defined by the contract. It normalizes strings to NFC and newlines to LF but does not summarize, paraphrase, or reorder authored claims. A pure `CodeLogicFileV2Projector` derives:

- `ContentMarkdown` from fixed headings and the authoritative fields;
- nine or fewer ordered Document Sections by fixed group order;
- one Index Entry from title/purpose, symbols, rules, communication kinds, and bounded deterministic search concatenation;
- Source Anchor, direct Edge, section, and index identities from canonical envelopes.

**Rationale**: The existing legacy result asks the model to author section Markdown and search text independently. A new pure projector closes that inconsistency while reusing the knowledge-store entities.

**Alternatives considered**:

- JSON Canonicalization Scheme dependency: rejected because the repository already has an explicit canonical-envelope writer and the server can write the closed grammar directly.
- Preserve arbitrary input property order: rejected because JSONB and serializers do not preserve it as identity authority.
- Ask the LLM for Markdown and index terms: rejected by FR-029/030.
- Accept a free-form communication label or only a classification UUID: rejected because either can contradict or under-specify the authoritative Story 12.2 classification meaning.

## R-8: Document and observation identity

**Decision**: Use the Story 12.1 `ScanJobExecution.Id` unchanged. Derive the Logical Document UUIDv8 from `(RepositoryId, canonical path, segment ordinal)` so a File Code Unit has a stable version-chain owner across Scan Runs. Derive Document Version UUIDv8 from the job ID. Derive Source Anchor identity from the Document Version plus exact path/byte/line/hash/locator. Derive known/deferred observation identity from source Document/File Code Unit, relationship kind, normalized target locator, and current target source identity when known; it excludes evidence form and completion timing. Derive REFERENCES Edge identity from source Document Version, target Document Version, and `References`.

**Rationale**: These inputs are server-owned and make completion order irrelevant. Excluding whether evidence came from a current Document or source fallback ensures reconciliation is stable.

**Alternatives considered**:

- LLM-supplied stable keys: rejected because identity would be provider-controlled.
- Include concurrency/completion timestamp: rejected because SC-001/011 require invariance.
- Include source hash in Logical Document identity: rejected because that would prevent Version Chains.

## R-9: Atomic Document commit and exact replay

**Decision**: Add a specific `IPass1CodeLogicDocumentCommitter` over a shared internal transaction primitive in `KnowledgeStoreRepository`. It revalidates authorization, pair/manifest/job/lease/version, source/hashes/ranges, receipts, classifications, current referenced Documents, prompt/schema/runtime identity, canonical JSON and all projections. One transaction inserts or supersedes the Logical Document version, Document Version, Source Anchors, direct REFERENCES Edges, Document Sections, Index Entry, and advances the job to `Completed` with the deterministic Document Version ID.

Already-Completed replay is checked before lease validation, exactly as Story 12.1 requires. It independently reconstructs canonical JSON, Markdown, Source Anchors, Document Sections, the single Index Entry, direct REFERENCES Edges, identities, cardinalities, and commit fingerprint from scalar rows. The comparison uses canonical order and rejects duplicate source/target relationships even when their row IDs differ. When a durable post-Pass1 outcome exists, the same pure reconciliation projection reconstructs the separately added Edge set; only the exact union of direct and reconciled Edges is legal. Exact equality returns the existing version with zero writes; any missing, extra, reordered, renormalized, duplicated, or changed value conflicts or fails integrity with zero writes.

**Rationale**: This preserves the accepted lost-acknowledgement contract while avoiding a second transaction or nested unit of work.

**Alternatives considered**:

- Commit the job after the Document: rejected because a crash exposes partial success.
- Reuse `WorkerAgentResultCommitter` unchanged: rejected because its legacy materializer is not `code-logic/file-v2` and permits independently authored projections.
- Delete/rewrite a Document during reconciliation: rejected by immutable Document rules.

## R-10: Failure classification and provider circuit

**Decision**: Persist pair-wide circuit state on `Pass1PairExecution`: `Closed`, `Probing`, or `Paused`, probe ordinal 0-5, next eligibility, bounded retry-after, and a sanitized machine code. Provider-transient HTTP 408/429/5xx, timeout, or transport loss opens the circuit and stops new claims. Probe delays are 10s, 30s, 60s, 120s, and 300s after the preceding observed failure, plus deterministic nonnegative jitter below 10% derived from pair ID/probe ordinal. A valid `Retry-After` may push eligibility later, capped at ten minutes from receipt; invalid or larger values are ignored in favor of the nominal schedule. No sixth probe exists.

The active probe is a minimal, fixed, source-free Worker-provider Chat Completions request with no tools, Repository/System/Scan identity, source, context, classifications, Document text, or caller-controlled content. It uses the resolved Worker model and encrypted credential only inside the provider client, disables SDK retries, bounds timeout/request/response bytes, discards response text, and returns only `Available`, `TransientUnavailable` with bounded Retry-After, or `ProviderConfiguration`. Success closes the circuit; 401/403 or invalid assignment pauses for operator correction; other provider-transient observations advance the durable probe ticket. Fifth transient failure atomically pauses both nonterminal child Scans as `PausedProviderUnavailable` and the coordination as `Paused` while preserving completed Documents and remaining jobs.

Failure mapping:

- provider configuration (missing assignment, invalid endpoint/model/key, 401/403) pauses immediately for correction;
- isolated malformed/schema-invalid/semantically invalid Worker output is `JobTransient` once, then one ordinary file failure;
- a clearly permanent file-specific provider refusal is `JobPermanent` and counts once;
- current durable binding/hash/manifest/receipt contradiction is `Integrity` and fails closed;
- attempted unauthorized/cross-Repository disclosure, secret/host-path output, or forbidden tool access is `Security` and fails closed;
- cancellation cancels without automatic resume;
- uncategorized systemic failure pauses for inspection.

**Rationale**: Scope, not exception type alone, determines whether work may continue. Deterministic jitter prevents synchronized probes while remaining testable.

**Alternatives considered**:

- Retry provider failures per job: rejected because it amplifies an outage and violates pair-wide stop.
- Persist raw HTTP/provider diagnostics: rejected by security and FR-044.
- Unlimited `Retry-After`: rejected because recovery must remain bounded.

## R-11: Compose authenticated Start into bounded production execution

**Decision**: Add one host-run, explicitly activated `BackgroundService` and one single-flight capacity-filling coordinator per pair over the existing Story 12.3 ports. A successful authenticated Start, exact eligible Start replay, successful manual Resume, or concurrency update signals the pair ID plus its already ownership-filtered authorization scope through a bounded in-process wake-up channel. The service deduplicates pair activations in memory and the coordinator repeatedly:

1. claims only through `IPreparedPass1Store.ClaimNextAsync`;
2. receives the current job `StateVersion`, lease token/expiry, pair state version, ordinals, and next durable eligibility as a server-only claim authorization;
3. for every claim, launches an independent DI-scoped runner that asks the specific Infrastructure packet source to reconstruct exactly one claimed File Code Unit packet from the approved manifests, Scan Context Snapshot, classifications, and revision-bound source, with a second lease check before returning it;
4. immediately returns to claim/fill without awaiting that runner; each runner invokes one prepared Worker through `IPreparedPass1WorkerDispatcher`, whose production `IPreparedPass1JobExecutor` registers only the lease-bound `navigate_code` function, then atomically commits a valid canonical result or records one closed durable failure against that exact lease;
5. continues launching until the store reports `AtCapacity`, future/ineligible work, terminal exhaustion, or a circuit/pause/failure/cancellation state that closes dispatch;
6. observes any runner completion or explicit activation only as a wake-up, removes the completed local handle, reloads durable state, and fills newly available capacity without using completion order as claim/identity authority;
7. immediately stops launches when durable policy closes dispatch, drains already-launched runners to closed outcomes, and follows the single pair-wide durable provider probe ticket when the provider is transiently unavailable; and
8. only after the local runner set is empty and a fresh claim proves no Running, Pending, or due/future retry work remains, calls the existing deterministic `ReconcileAsync` terminal-accounting/handoff transaction.

`ClaimNextAsync` releases due Story 12.3 `RetryScheduled` jobs under the same pair lock before choosing the lowest Pair Selection Ordinal and reports the next retry/lease/probe eligibility rather than requiring polling. The coordinator uses `TimeProvider` delays, runner-completion notifications, and store outcomes; it never invents an in-memory retry, order, capacity, or terminal decision. A 4-to-1 change preserves all four leases and blocks replacements until durable active count falls below one; a 1-to-4 change wakes the coordinator and fills three newly available slots without waiting for the first runner. The channel and local runner handles are only wake-up/lifetime mechanisms: PostgreSQL jobs, leases, circuit state, and outcome remain authoritative.

The hosted service never scans the database for dormant work at process startup. Process loss preserves durable progress but requires the accepted authenticated manual Resume to revalidate and signal remaining work. After the fifth failed probe it removes the activation and performs no automatic background resume; only `继续扫描` can reactivate the pair. This distinguishes continuation of one explicitly activated Running/Probing execution from the excluded automatic recovery of a persisted paused execution.

**Rationale**: Quality Review 01 showed that the current Start path persists jobs but no production caller claims, dispatches, commits, probes, or reconciles them. Plan Review 03 then showed that a claim-to-completion loop could close that call graph while remaining strictly sequential and leaving FR-006's shared 1-4 capacity operationally unused. The current `PreparedPass1DispatchWorker` also accepts caller-supplied already-claimed batches, while no prepared job executor or commit port is registered. A capacity-filling single-flight host coordinator closes the production call graph and requires real bounded overlap without moving Vulgata into Docker, bypassing durable leases, or absorbing Story 12.4.

**Alternatives considered**:

- Execute all 50-150 jobs inside the Start HTTP request: rejected because request cancellation/timeouts would own Scan lifetime and provider recovery windows.
- Poll all persisted pairs or resume them automatically on host startup: rejected by the explicit manual-resume boundary.
- Let the dispatcher enumerate/claim work or use its own semaphore: rejected because the durable pair row is the sole capacity/order authority.
- Await each claimed runner before the next claim: rejected because it is conformingly durable but strictly sequential, cannot fill limits two through four, and cannot prove the required shared bounded concurrency.
- Queue one payload per File Code Unit in memory: rejected because it duplicates the complete durable job set and loses exact restart/replay authority.
- Keep a caller-built batch dispatcher as the production seam: rejected because it cannot prove the current lease/version or server-owned packet composition.

## R-12: Honest terminal accounting and Story 12.4 handoff

**Decision**: The pair denominator is the distinct complete start-time job set. High-value seeds are reconstructed, not rescored: within each Repository, take the first at most 20 manifest entries with `DirectBusinessScore >= 400` under descending direct score, exact path, segment ordinal. Attempts, probes, leases, and replays never add denominator/failure entries.

After every job is terminal, compute the pair-wide ordinary failure count and budget `min(5, floor(total/10))`. Then reconcile in canonical source/observation/target order. Successful Pass 1 persists one immutable `Pass1Outcome` and bounded failed-file entries. Each child with a failed file becomes `CompletedWithWarnings`; a child without one uses the new-path warning-free `Completed` transition. The coordination advances from `Pass1` to `Resolving` only as the approved handoff state; Story 12.3 performs no Cross-Repository Resolution. If a seed failed, the budget was exceeded, or local reconciliation could not commit deterministically, the Pass 1 outcome and coordination fail and later phases remain unauthorized.

**Rationale**: `Resolving` already exists as the accepted next phase. A separate Pass 1 outcome captures the exact warning inventory without claiming Story 12.4 work.

**Alternatives considered**:

- Mark every partial completion successful: rejected because high-value seeds and the budget are mandatory.
- Put warnings only in terminal prose: rejected because ordered replay and exact counts need durable structured rows.
- Start Story 12.4 automatically in the same worker: rejected by the explicit story boundary.

## R-13: Required compatibility corrections and migration

**Decision**: One additive migration, provisionally named `AddResumablePass1CodeLogic`, is required. It creates the Story 12.3 tables/indexes, allows `code-logic/file-v2` beside `vulgata.knowledge-document/1.0`, adds nullable exact byte bounds to Source Anchors, and adds only the missing new-path failure atoms/transitions needed for honest Pass 1 status.

Three source gaps are proven by the pinned checkout:

1. `ScanExecution` has `CompleteWithWarnings` but no warning-free new-path `Running -> Completed` method, although the accepted Story 12.1 data model allows that transition.
2. Neither child nor coordination has an ordinary Pass 1 policy-failure terminal atom; only integrity/security/cancellation are representable, so exceeding the failure budget cannot be reported honestly.
3. the Document Version CHECK accepts only `vulgata.knowledge-document/1.0`, and Source Anchor has line but no byte bounds, while Story 12.3 requires `code-logic/file-v2` and exact byte/line containment.

Add `FailedPass1`/`Pass1Policy` only to the new path, preserve every existing enum value and historical row, add nullable `ByteStart`/`ByteEnd` with a both-null-or-valid check, and broaden the schema CHECK to the two exact identities. No existing content is rewritten, backfilled, or reinterpreted.

**Rationale**: These are narrow representation corrections required to connect already-accepted seams. They do not change Story 12.1 identity, leases, replay, recovery, or Story 12.2 manifest behavior.

**Alternatives considered**:

- Mislabel budget failure as integrity, warning, cancellation, or pause: rejected because it is observably false.
- Store v2 content under the v1 schema identity: rejected because schema identity would lie.
- encode byte ranges only inside JSON: rejected because Source Anchor associations could not be independently validated from relational rows.

## R-14: Verification levels

**Decision**: Use public-seam TDD slices and five qualification layers:

1. pure canonical grammar/identity/projector/failure-budget tests with literal vectors and an independent reconstructor;
2. API, authorization, dispatcher/tool, recovery, lease-race, and SQLite integration tests;
3. authenticated `WebApplicationFactory<Program>` production composition with a controllable OpenAI-compatible transport and fake time, proving Start-to-terminal and the five-probe outage path without direct test orchestration of store methods;
4. disposable PostgreSQL 17 migration/atomic-start/claim/commit/full-graph-replay/reconciliation fault injection;
5. real Windows-host CodeGraph 1.5.0, Java 21, pinned JDT LS over stdio, and a controllable OpenAI-compatible provider fixture, followed by one authenticated approved-pair host qualification through the production DI graph.

Production composition uses controllable barriers inside independently scoped Workers: enough eligible jobs must reach simultaneous counts two and four before release, a counter must never exceed the durable limit, held runners must prove 4-to-1 preservation/no replacement and 1-to-4 immediate fill, and at least ten release schedules must retain identical deterministic results. This barrier fails a sequential coordinator by timeout. The host qualification also varies concurrency 1/2/4 and completion schedules, performs real typed Java navigation plus exact XML source evidence, commits/reconciles, and stops at the Story 12.4 handoff. Security qualification inspects the complete production registration/call graph and denies CodeGraphContext, raw protocol/storage, shell/network/filesystem tools, and source/provider/host-path persistence. Existing constructor/literal/runtime-version checks remain supporting evidence rather than execution proof.

**Rationale**: Pure tests prove determinism, production composition proves reachability, PostgreSQL proves relational atomicity/corruption handling, and real tools prove the accepted host boundary. Isolated store tests, static DI inspection, or a runtime-version-only claim are insufficient for SC-008/SC-012 and QR-005.

**Alternatives considered**:

- Full-suite-only testing: rejected because failures would not identify the invariant owner.
- SQLite-only atomicity: rejected because row locking, filtered indexes, JSONB, and transaction semantics are PostgreSQL-specific.
- Real LLM release/Chat validation: rejected as Story 12.5 scope.
