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

## Active concurrency update

A currently authorized SystemOwner may request one effective integer limit 1-4 with the expected pair StateVersion. A transaction locks the pair, revalidates ownership over both Repositories and pair currentness, and permits changes only before `Reconciling`/terminal state. Exact equality at the current version is a zero-write no-op. Otherwise it writes one deterministic `Pass1ConcurrencyChange`, updates the effective limit/revision/timestamp, and advances pair StateVersion atomically.

Already-valid leases survive a decrease unchanged. Claims count them against the new limit and issue no new lease while the count is at or above it; increases affect only claims made after commit. Exact lost-ack replay reconstructs the change record and is zero-write. Same expected version with different target/actor, stale version without that exact record, or changed authorization is conflict/rejection. Limit, version, and audit change never alter job, Document, projection, reconciliation, warning, or display identity.

## Claim

Claims are internal. A transaction locks the pair execution, rejects any non-`Running`/closed-circuit state, counts valid unexpired Running leases, and if capacity remains selects the smallest eligible Pair Selection Ordinal among Pending jobs (or due retry jobs already recovered to Pending). It applies `AcquireLease` using a server UUID and bounded lease expiration. The returned packet is built from the persisted manifest/File Code Unit/context/classification bindings, never caller or database row order.

The Worker packet includes only the assigned exact source range, untrusted bounded context, and same-manifest classifications whose canonical path and Evidence Locator are contained by the assigned File Code Unit. Each supplied classification binding carries its exact persisted ID, fingerprint, nullable closed kind, level, confidence, rule, provenance, Catalog Rule Identity, and Evidence Locator so a result can echo and be compared field-for-field. It excludes unrelated classifications and never converts a classification into endpoint/provider ownership. A null-kind Warning cannot support an authored `Rpc`/`Http` communication observation.

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

Probe state/version, ordinal, eligibility, bounded retry-after and sanitized reason are durable. Probe races are version conflicts, not additional probes. Raw headers/body/credentials are never persisted.

## Retry, restart, and manual resume

An isolated JobTransient may use one `Running -> RetryScheduled -> Pending` cycle. The same deterministic job and remaining navigation budget are reused. AttemptCount can never exceed two for this policy.

Restart/manual resume reuses Story 12.1 revalidation for both child Scans and additionally verifies the pair execution, both exact manifests and pair fingerprint. It preserves/skips Completed jobs, returns only strictly expired leases to Pending, releases only due nonexhausted retries, retains future/terminal jobs, actively probes provider availability, and moves the pair/child Scans to their eligible prepared/running state in one version-conditional operation. Repeated resume is a no-op. Automatic background resume is excluded.

The visible manual action text is exactly `继续扫描`. No response includes lease tokens, source/provider content, host paths, raw diagnostics, or hidden-resource distinction.

## Atomic Document commit

The commit port accepts one validated canonical result plus the expected job StateVersion/lease token. It rechecks all trusted bindings and calls one transaction-aware knowledge-store primitive. Document graph insertion/supersession and `ScanJobExecution.Complete` are one transaction. Already-Completed exact replay is evaluated before the Running lease branch and returns the existing Document Version without mutation. Changed replay conflicts without mutation.

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
