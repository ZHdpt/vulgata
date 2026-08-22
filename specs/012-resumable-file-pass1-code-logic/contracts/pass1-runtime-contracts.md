# Contract: Prepared Pass 1 Dispatch, Recovery, and Completion

## Start preconditions

Start consumes only the authorized Story 12.2 prepared handoff for one coordination. The store independently reconstructs both complete `prepared-scan-manifest/1.0` packages and verifies the pair/package/selection fingerprints, distinct Repository bindings, System, Scan Runs, Profiled Revisions, File Code Units, exact file/source hashes and ranges, readiness proof hashes, classifications, context snapshots, both ordinal domains, approved states, and current row versions. Both bound Scan Executions must be new-path executions with a nonempty persisted prompt identity, exact document schema identity `code-logic/file-v2`, and their accepted runtime identities; start never fills in or rewrites those bindings. A Pre-Scan Handoff is evidence, not dispatch authority.

The caller must have current SystemOwner scope over both Repositories. Not found and outside ownership are indistinguishable after policy authorization. One, three, legacy-only, partial, stale, independently combined, or mismatched manifests are rejected before mutation.

## Atomic start

One serializable PostgreSQL transaction:

1. locks coordination, both manifests, both child Scan Executions, and existing pair-execution key;
2. revalidates every start precondition;
3. computes the complete job set by Story 12.2 `PairSelectionOrdinal`;
4. calls accepted Story 12.1 `ScanJobExecution.Create` once per manifest entry;
5. creates one zeroed navigation budget per job;
6. persists one pair execution binding and the exact denominator/high-value-seed set;
7. transitions both child Scans `Prepared -> Running` and coordination `Preparing -> Pass1`;
8. commits before a claim is observable.

Outcomes are `Created`, `Replay`, `Conflict`, `Rejected`, `NotFound`, `Cancelled`, or `Failed`. Replay requires a complete exact relational reconstruction and performs zero writes. Any injected failure exposes either the entire set or no new set.

Start persists both `InitialConcurrencyLimit` and current `ConcurrencyLimit`. Exact start replay compares the immutable initial value and returns the current effective limit/state; a different start limit is conflict and must use the active update contract below.

## Production activation and execution ownership

After a `Created` Start, an exact replay whose durable pair is still `Running` with a closed circuit, a successful manual Resume, or an eligible concurrency update, the Web coordinator signals one host-run execution service with the pair ID and the already ownership-filtered `PreparedScanAuthorizationScope`. The signal is a bounded in-process wake-up only. PostgreSQL pair/job/lease/circuit/outcome rows remain the sole authority.

The execution service is one single-flight coordinator per pair and resolves every launched job runner in an independent scope through `IServiceScopeFactory`. It never enumerates a Repository or creates a job. One coordinator iteration is the following normative fill/observe/drain algorithm; awaiting one runner before attempting the next claim is nonconforming:

1. **Fill**: while dispatch is durably open, call `ClaimNextAsync` serially and, for every `Claimed` result, immediately launch one independently scoped claimed-packet -> Worker -> atomic commit/closed-failure runner, record only its local completion handle keyed by job/lease, and attempt the next claim without awaiting that runner.
2. **Stop filling**: stop launching immediately when claim returns `AtCapacity`, future/ineligible work, terminal exhaustion, or any durable circuit/pause/failure/cancellation state that closes dispatch. No Worker may enter provider execution after closure is durable: a claim racing with closure is rejected by the claimed-packet runner's required pair/lease revalidation. Local handle count never authorizes another launch; every slot decision comes from the pair-row-locked claim.
3. **Observe**: when one or more runners are in flight, await any completion or an explicit activation/state-change signal. Completion is only a wake-up: remove that local handle, observe/classify its closed durable result, reload durable pair state, and return to Fill if dispatch remains open. Which runner finishes first never selects the next job or changes identity/order.
4. **Drain**: after dispatch closes, launch nothing and observe all already-launched runners to a closed durable result. A 4-to-1 decrease does not revoke any of four valid leases and permits no replacement while durable active leases are at or above one. With at least three additional eligible jobs and dispatch still open, a committed 1-to-4 increase wakes Fill and must claim three additional jobs while the original lease may still be running.
5. **Reconcile**: call `ReconcileAsync` only after the local runner set is empty and a fresh durable claim proves terminal exhaustion with zero active leases, no Pending job, and no due/future retry. A provider circuit, pause, failure, cancellation, or nonterminal future eligibility stops or waits instead of reconciling.

An `AtCapacity` or future-eligibility result uses runner completion/state-change notification or `TimeProvider` delay; it must not busy-poll. Multiple activations coalesce into the same pair coordinator and can only wake the algorithm.

The service does not discover dormant pairs at process startup. Process loss preserves durable progress and requires authenticated manual Resume to revalidate and signal remaining work. The fifth failed provider probe removes the activation and persists the paused/manual-action state; no timer, startup scan, or provider callback may reactivate it. An explicit `继续扫描` request is the only reactivation path after that pause.

The production DI graph must resolve the execution activator/hosted service, claimed-packet source, `IPreparedPass1JobExecutor`, prepared dispatcher, lease-bound `navigate_code`, provider health request, probe driver, `IPass1CodeLogicDocumentCommitter`, and store/reconciler. A static registration without an authenticated Start-to-terminal call path is nonconforming.

## Active concurrency update

A currently authorized SystemOwner may request one effective integer limit 1-4 with the expected pair StateVersion. A transaction locks the pair, revalidates ownership over both Repositories and pair currentness, and permits changes only before `Reconciling`/terminal state. Exact equality at the current version is a zero-write no-op. Otherwise it writes one deterministic `Pass1ConcurrencyChange`, updates the effective limit/revision/timestamp, and advances pair StateVersion atomically.

Already-valid leases survive a decrease unchanged. Claims count them against the new limit and issue no new lease while the count is at or above it; transient active leases may therefore exceed a reduced limit only until those preserved leases close. A committed decrease wakes Observe/Drain but never cancels a runner. A committed increase wakes Fill immediately, and Fill launches newly available eligible slots without waiting for an existing runner to complete. Exact lost-ack replay reconstructs the change record and is zero-write. Same expected version with different target/actor, stale version without that exact record, or changed authorization is conflict/rejection. Limit, version, and audit change never alter job, Document, projection, reconciliation, warning, or display identity.

## Claim

Claims are internal. A transaction locks the pair execution, rejects any non-`Running`/closed-circuit state, returns strictly expired leases to Pending, releases only due nonexhausted Story 12.3 retries, counts valid unexpired Running leases, and if capacity remains selects the smallest eligible Pair Selection Ordinal among Pending jobs. It applies `AcquireLease` using a server UUID and bounded lease expiration. The result carries the current job `StateVersion`, lease token/expiry, pair `StateVersion`, Pair Selection Ordinal, Manifest Ordinal, and nullable next durable eligibility as a server-only claim authorization.

A specific Infrastructure packet source accepts only that claim authorization. It reconstructs the exact manifest entry, File Code Unit, Scan Context Snapshot, contained Communication Kind Classifications, prompt/schema/runtime bindings, and revision-bound source bytes. After source I/O it rechecks the current job version/unexpired lease and pair Running/closed state before returning one packet. A mismatch returns no packet and no source/provider disclosure. The returned model-visible packet contains no lease token/version/expiry. The dispatcher never accepts a caller-built multi-job batch on the production path.

The Worker packet includes only the assigned exact source range, untrusted bounded context, and same-manifest classifications whose canonical path and Evidence Locator are contained by the assigned File Code Unit. Each supplied classification binding carries its exact persisted ID, fingerprint, nullable closed kind, level, confidence, rule, provenance, Catalog Rule Identity, and Evidence Locator so a result can echo and be compared field-for-field. It excludes unrelated classifications and never converts a classification into endpoint/provider ownership. A null-kind Warning cannot support an authored `Rpc`/`Http` communication observation.

`AtCapacity` returns the current effective limit plus the earliest known lease eligibility. It is the expected Fill result after simultaneous valid leases reach the limit, not a reason to await jobs sequentially. `Exhausted` with a future retry/lease eligibility is not terminal. `Exhausted` with zero active leases, no Pending job, and no future retry is the only claim result that lets the drained execution service attempt reconciliation. Completion order never changes the next claim or any identity.

## Worker attempt and result classification

The prepared dispatcher has one normal attempt and no in-memory retry decorator. Durable `AttemptCount` decides whether the isolated `JobTransient` retry remains legal.

| Observation | Durable classification | Effect |
| --- | --- | --- |
| timeout/transport/408/429/5xx | ProviderTransient | open pair circuit; no new claims |
| missing/invalid provider assignment, endpoint, model or credential; 401/403 | ProviderConfiguration | pause pair immediately for correction |
| malformed/unknown/duplicate JSON, ordinary schema/semantic defect | JobTransient on first attempt | schedule the same job once |
| second JobTransient or permanent file-specific refusal | JobPermanent/ordinary file failure | terminal failed job, counted once |
| current manifest/revision/hash/job/receipt relational contradiction | Integrity | fail affected Scan and pair closed |
| forbidden tool/scope/disclosure/secret/host path | Security | fail affected Scan and pair closed |
| explicit operator cancellation | Cancellation | cancel remaining work; no auto-resume |
| systemic condition with no safe class | Unclassified | pause for inspection |

A bad Worker result never commits a Document. Integrity/security classification is reserved for a violated trusted boundary; ordinary provider-authored mistakes remain job-scoped so peripheral files can use the approved budget. A metadata-only navigation BoundExceeded receipt is neither provider failure nor accepted evidence: it truthfully supports only a deferred bound-exhaustion observation.

The production job runner maps every dispatcher/commit outcome before continuing: valid canonical result -> atomic commit; ordinary malformed/rejected result -> `JobTransient` then `JobPermanent` according to the durable attempt policy; provider transient -> pair circuit/probe driver while retaining the same current claim when still valid; provider configuration -> operator-correction pause; binding/graph contradiction -> Integrity; forbidden disclosure/tool access -> Security; unknown systemic result -> inspection pause. No caught exception may be treated as successful completion or silently abandon a Running lease.

## Provider circuit

Circuit states are `Closed`, `Probing`, and `Paused`. On ProviderTransient:

1. stop new claims pair-wide;
2. allow already-running jobs to finish and classify;
3. make probe 1-5 eligible after nominal 10, 30, 60, 120, and 300 second windows respectively from the preceding observed failure;
4. add deterministic nonnegative jitter below 10% from pair ID and probe ordinal;
5. let a valid Retry-After delay eligibility, but no more than ten minutes from receipt;
6. use one fixed source-free bounded provider request per probe;
7. on success close the circuit and continue pending work;
8. after probe five fails, atomically pause both nonterminal child Scans as `PausedProviderUnavailable`, pause the coordination, and retain jobs/Documents.

Probe state/version, ordinal, eligibility, bounded retry-after and sanitized reason are durable. Every open/scheduled result returns a server-only probe ticket containing expected pair version, expected probe ordinal, and `NextProbeAt`. One per-pair probe driver waits until that exact durable eligibility, sends one active health request, and conditionally calls `ProbeProviderAsync` with the ticket. Conflict reloads durable state; it never counts an extra probe. No probe occurs before eligibility and no sixth ticket exists.

The health request is a fixed source-free OpenAI-compatible Chat Completions request against the resolved Worker assignment: no tools, caller text, Repository/System/Scan identity, source, context, classification, Document content, or navigation. SDK retries/logging/message logging are disabled; timeout and request/response bytes are bounded; response content is discarded. The closed observation is only `Available`, `TransientUnavailable` with a bounded Retry-After, or `ProviderConfiguration`. Only availability/classification and a sanitized code cross into the durable store. Raw headers/body/credential/model response are never persisted or returned.

## Retry, restart, and manual resume

An isolated JobTransient may use one `Running -> RetryScheduled -> Pending` cycle. The same deterministic job and remaining navigation budget are reused. AttemptCount can never exceed two for this policy.

Restart/manual resume reuses Story 12.1 revalidation for both child Scans and additionally verifies the pair execution, both exact manifests and pair fingerprint. It preserves/skips Completed jobs, returns only strictly expired leases to Pending, releases only due nonexhausted retries, retains future/terminal jobs, performs the same fixed active source-free provider request rather than configuration resolution, and moves the pair/child Scans to their eligible prepared/running state in one version-conditional operation. `Continued` and an eligible Running no-op signal the execution service; paused/ineligible results do not. Repeated resume is a durable no-op. Automatic background resume is excluded.

The visible manual action text is exactly `继续扫描`. No response includes lease tokens, source/provider content, host paths, raw diagnostics, or hidden-resource distinction.

## Atomic Document commit

The commit port accepts one validated canonical result plus the expected job StateVersion/lease token. It rechecks all trusted bindings and calls one transaction-aware knowledge-store primitive. Document graph insertion/supersession and `ScanJobExecution.Complete` are one transaction. Already-Completed replay is evaluated before the Running lease branch.

Replay independently reconstructs and compares the complete canonical commit graph from scalar rows: Document Version fields and canonical ContentJson/Markdown, every Source Anchor field/order/cardinality, every Document Section field/order/cardinality, exactly one Index Entry with all projected fields, and direct REFERENCES Edge identity/source/target/type/order/cardinality. Duplicate source/target relations with a different row identity reject. When a durable post-Pass1 outcome exists, the pure reconciliation projection must independently reconstruct the exact additional Edge set; those edges cannot mask a missing or mutated direct Edge. Only the exact graph returns the existing Document Version without mutation. Missing, extra, changed, reordered, renormalized, or duplicate graph state conflicts or fails integrity with zero writes.

## Terminal accounting

The denominator is the number of distinct jobs committed by atomic start. Each File Code Unit counts once. `ordinaryBudget = min(5, floor(dispatchedCount / 10))`.

High-value seeds are reconstructed from accepted manifest entries only: per Repository, descending Direct Business Score, exact path, segment ordinal; take the first at most 20 with direct score at least 400. No rescore or expansion is allowed.

When all jobs are terminal:

- any integrity/security outcome retains its stronger terminal state;
- any failed seed or ordinary count over budget produces `FailedPass1`, no reconciliation handoff, and no later phase;
- otherwise run deterministic local reconciliation;
- reconciliation success with every job Completed produces warning-free child `Completed` states;
- reconciliation success within budget gives each child containing an ordinary failure `CompletedWithWarnings`, leaves a clean child `Completed`, writes the canonical pair warning inventory, and advances coordination to `Resolving` as the Story 12.4 handoff only.

No Story 12.4 job is created by this transition.

The production execution service calls `ReconcileAsync` as the single normal terminal-accounting/reconciliation/handoff transaction only after the capacity-filling coordinator has drained every locally launched runner and a fresh claim proves the durable terminal predicate. It does not separately create an outcome and then attempt reconciliation. Conflict reloads current state and retries only while the same exact drained terminal pair remains eligible; failed/integrity/security/paused outcomes stop the coordinator after in-flight results close and never enter reconciliation. `ReadyForResolution` ends Story 12.3 activation and does not execute the next phase.
