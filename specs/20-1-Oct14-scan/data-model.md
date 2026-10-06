# OCT-SCAN data model and lifecycle

Planning model only. Names below describe roles; they are not proposed competing Knowledge DTOs or tables. OCT-BUSINESS selects all shared persistent shapes at C0 and owns OctoberDbContext/migrations/snapshot.

The initial owner candidate is now imported at integration `3bcd815b09cfbac7418d6644a2a75816e0788ec0`; actual symbols are mapped in `contracts/scan-boundaries.md`. `CompletedFileProof` seals analysis; `CommitAttempt` and `IOctoberCommitSubmissionGuard` carry eligibility/submission; `SubmittedCommit` wraps settlement only after synchronous commit invocation. `IOctoberProviderAttemptLease` owns pre-send recheck/accounting/settled release; `BusinessFinalizationResult.Status` explicitly distinguishes published/partial/no-content/failure/denied/authority-unavailable/hard-stop/indeterminate. These refine names without changing the accepted lifecycle or introducing lane-owned shared records. Only controlled conformance is authorized now.

F1/F2/F5 semantics are adopted from the verified Business canonical contract SHA-256 `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36`. This is a planning model referencing Business-owned types, not compiled conformance or a schema change.

## Scan-owned process-local state

| Record | Required fields/relationships | Validation |
| --- | --- | --- |
| Generate execution | Exact target/batch, operator session, selected context/provider fingerprints, policy including pinned outputStage, start/deadline, stop state | outputStage explicitly cl-only or cl-and-bl; no default/change/downgrade; trusted setup and finite budgets |
| Inventory entry | Repository/root binding, relative path and source identity, classification/reason, priority, coverage disposition | Contained normalized path; positive tool-state exclusion; no filename/index-only eligibility |
| File workstream | Execution + inventory identity, attempt token, draft/window coverage, elapsed/request/usage budgets, terminal outcome | One valid writer; windows do not create semantic units; changed source invalidates attempt |
| Frozen submission | Operation key, immutable proposal/fingerprint, application completion capability, acceptance result/lookup state | Provider finish and tools settled; capability cannot be deserialized from model JSON; same key cannot carry changed payload |
| Attempt eligibility / submission | Separate from immutable analysis seal: irreversible fence, monotonic drain/run deadline, hard stop, shared per-attempt guard, exact CommitInFlight operation | Business checks entry and final guarded submission; seal cannot override current ineligibility; track until exact settlement |
| Host execution scope / provider attempt | Run/phase/provider/finalization binding, one cap and rate admission, request/usage reservations, monotonic deadline, cancellation, settlement/accounting status | Every Business attempt/retry acquires; no outer orchestration permit; exact-once accounting; transport must settle or confirm disposal before permit release |
| Repository tool owner | Approval/root object identity, tool-state identity, returned native descriptor, native session count, dedicated JDT lease, lifecycle gate | Same process only; finite deadlines; one incompatible tool phase at a time; verified disposal before reuse |

Drafts, queues and tool capabilities are not persistent Knowledge. After host failure they are not resumed/adopted. An explicit fresh batch is required; accepted history remains in the owner store.

## Business-owned persistent state required by Scan

- Exact accepted file manifest, semantic Code Units, immutable structured CL Document Versions/sections, observations, source anchors and advisory qualifications.
- Operation identity/fingerprint, atomic committed outcome and exact lookup (including explicit not-committed/indeterminate distinction), with trusted Scan ownership validation.
- Terminal file dispositions and frozen exact corpus membership/coverage needed for idempotent finalization; no general durable job/restart machinery is requested.
- Finalization operation/result, exact publication membership and BL/relationship/Uncertainty dependencies. Read authorization remains live and all-contributor, separate from operator selection.

All prepared-origin fields derive from trusted setup. A root binding is not a Git Profiled Revision. Business chooses the shared representation; Scan never fills a Git column with a fake value.

## State transitions

File: inventoried → excluded/supporting/unsupported **or** admitted → queued → running → continued draft → frozen proposal → acceptance pending → accepted/accepted-zero-unit. Retry may return running work to queued only within budgets after prior ownership settles. Invalid/failed/stopped work becomes an explicit terminal gap. An ambiguous store reply enters indeterminate; no new generation or corpus freeze until exact reconciliation resolves it.

Run common path: validated → tools ready → inventory/admission → running → draining → settled → corpus frozen. Pinned cl-only then goes to Business B0 CL publication → CL-published (complete or partial), with matching/BL not requested. Pinned cl-and-bl goes to Business finalization → CL+BL-published (complete or partial). A publication-pending result is not success; reconcile its original operation. Empty accepted corpus or zero CL Documents ends no-content. Hard cancellation/revocation/integrity failure can transition to interrupted/failed; unresolved store state transitions to indeterminate. None of these auto-resumes.

F1 commit lifecycle: immutable Completed(digest) analysis evidence + separate current eligibility → checked transaction entry/staging → shared guarded fresh authority/fence/monotonic deadline/hard-stop check → CommitInFlight at serialized submission → committed, confirmed rolled-back/never-submitted, or CommitIndeterminate. Hold the guard through submission without an unguarded gap. Seal→expiry→entry rejects entry; expiry during staging rejects submission/rolls back. Delayed timers cannot bypass deadline checks. Already-submitted commits may settle after fence/cancellation: cancellation and temporary absence are not rollback proof. Exact lookup with fresh disclosure authority settles them; freeze waits for all possibly committing operations. Accepted-file state and outcome commit atomically; stopped-gap requires confirmed noncommit. Publication uses identical phase-eligible submission rules with its own operation identity. No new local schema.

First graceful stop closes new file admission, allowing eligible bounded drain; it is not itself a fence. Drain expiry irreversibly fences affected file attempts, not a separately permitted finalization phase. Hard stop or observed authority failure fences/cancels affected work and bars new calls/publication; already-submitted operations remain tracked until settlement and committed history survives. Indeterminate blocks freeze; hard stop never resumes finalization merely because a commit later succeeds.

Finalization uses one fixed exact corpus and idempotent operation. A partial publication preserves gaps. No late writer can alter it; no hidden previous-file selection fills a failed file. Publication identity and domain semantics belong to Business.

CL-only publication uses the same owner identity/replay rules without relationship/BL generation. Host output records requested/completed stage, coverage and exact returned handles; these reference Business's types rather than duplicate them. Later CL+BL publication has a separate identity; earlier membership/handles remain immutable.

F2 provider attempt: queued within original deadline → granted shared cap/rate/budget reservation → eligibility/rate recheck before send → dispatched → settled actual/unknown usage and outcome exactly once → permit released only on confirmed transport settlement/disposal. Denied/expired/queued-cancelled attempts send nothing. Unknown usage keeps a conservative reservation or blocks further usage-constrained work; unconfirmed transport cancellation remains outstanding and blocks dispatch. Retry reacquires without resetting scope/counters/deadline. Parent finalization holds no permit while child attempts wait, so cap=1 progresses. The host scope is process-local and shared with file Workers, not with Web processes. Concrete shared type definitions and compile conformance remain CP-K1 work after authorization.
