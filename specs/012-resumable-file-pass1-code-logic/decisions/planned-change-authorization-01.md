# Story 12.3 Planned-Change Authorization

**Decision ID:** `story-12-3-planned-change-authorization-01`
**Status:** Accepted
**Requested at:** 2026-08-21T08:14:55Z
**Decided at:** 2026-08-21T08:25:57Z
**Accepted plan scope SHA-256:** `b422424248a47e1bfda677ca7362dd12addc4861873f173edd4579ecb3d3199a`
**Scope construction:** SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
accepted spec, complete plan set, and plan-review-02 verdict.
**Independent plan review:** `ACCEPT`, iteration 2,
`reviews/plan-review/plan-review-02.json`

## Decision requested

Authorize the active root to proceed from the independently accepted Story
12.3 plan to `speckit-tasks`. This authorization covers only the immutable
scope hash above. It does not authorize production implementation and does not
waive any later analysis, TDD, review, E2E, security, migration, verification,
checkpoint, or commit gate.

## Repository-policy triggers, effects, and implementation intent

| Trigger | Proposed effect and implementation intent | Affected plan sections | Affected PRD clauses |
| --- | --- | --- | --- |
| Worker Agent tool | Add one server-owned closed `navigate_code` facade for definition, references, implementations, callers, callees, symbol search, related files, and exact source evidence. Enforce authorization/Profiled Revision/current-hash binding, eight calls, 20 results/call, 64 KiB, depth three, and metadata-only bound receipts. Never expose raw LSP, CodeGraph storage/query, shell, network, filesystem, or scheduling access. | AD-3 through AD-5; `contracts/navigate-code-contract.md`; navigation portions of runtime/data model/quickstart | FR-4.3, FR-4.4, FR-4.9, FR-4.12, FR-11.6 dated replacement; NFR-2.3 |
| Scan dispatch and scheduling | Atomically create exactly one durable job per prepared File Code Unit across the exact two-manifest pair. Enforce pair-wide concurrency default two/range one-four, canonical pair/manifest ordering, versioned authorized updates for new claims, and active-lease preservation. Completion order never changes identity or display. | AD-1, AD-2; Start/Claim/Update Concurrency API and runtime contracts; concurrency data model/envelope | FR-4.2; dated NFR-1.1 replacement; NFR-3.3 |
| Retry, pause, resume, and failure classification | Add a pair-wide provider circuit with 10s/30s/1m/2m/5m probes, dispatch stop while unavailable, durable `PausedProviderUnavailable`, Chinese `继续扫描`, one isolated job retry, fail-closed integrity/security, and operator pause for provider-configuration/unclassified systemic failures. Reuse Story 12.1 lease/recovery/manual-resume invariants. | AD-10 through AD-12; failure/state tables; runtime/API/data-model recovery sections | FR-4.2; NFR-3.1 through NFR-3.3 |
| Document creation and projections | Accept only strict authoritative `code-logic/file-v2` JSON with exact identity/hash/range/classification/Source Anchor validation. Deterministically derive Markdown, Document Sections, and Index Entries; perform atomic Document/job commit and exact replay. Add schema allowlist and v2-only byte anchors without rewriting historical rows. | AD-6 through AD-9 and AD-12; canonical envelope, JSON Schema, projection contract, persistence/migration | FR-4.3, FR-4.6, FR-4.7, FR-4.10, FR-11.7 |
| Handoff and reconciliation behavior | Consume rather than recompute Story 12.2 manifest/order/hash/readiness/classification authority. After terminal Pass 1, reconcile only exact current same-Repository references from direct/deferred observations as separate immutable REFERENCES Edges; never rewrite Documents or perform Cross-Repository Resolution. | AD-11, AD-13; reconciliation contract; handoff/runtime identity sections | FR-4.4 and FR-4.12 dated replacement; FR-6.1/FR-6.2 remain deferred to Story 12.4 |
| Analysis-tool/runtime use | Route `navigate_code` only through the accepted closed CodeGraph, JDT LS, and revision-bound source ports and qualify the real host tools. Preserve CodeGraph `@colbymchenry/codegraph@1.5.0`, Java 21, JDT LS `jdt-language-server-1.61.0-202608141332` over stdio/StreamJsonRpc `2.25.29`, Windows-host Vulgata/tools, and PostgreSQL 17-only Docker. | AD-4; runtime composition and real-tool qualification; security tests | FR-4.9; NFR-2.3; dated NFR-4.1 replacement |

## Planned additive compatibility corrections

- Permit warning-free new-path `Running -> Completed` without changing legacy
  phase meanings or historical rows.
- Add closed `FailedPass1` / `Pass1Policy` terminal atoms for honest ordinary
  Pass 1 policy failure.
- Broaden the Document Version schema allowlist from only
  `vulgata.knowledge-document/1.0` to the exact v1/v2 allowlist.
- Add nullable Source Anchor byte bounds that are mandatory only for new
  `code-logic/file-v2` anchors.
- Add Story 12.3-specific queue, navigation budget/receipt, provider circuit,
  failure summary, and reconciliation persistence through one additive
  PostgreSQL migration. Story 12.1 and Story 12.2 accepted rows and replay
  semantics remain readable and unchanged.

## Preserved boundary

- Exactly Story 12.3; no Story 12.4 Cross-Repository Resolution,
  endpoint-centered grouping, `retrieve_code_logic`, Pass 2, or Business Logic
  Documents.
- No Story 12.5 Chat, final release UI, evaluation questions, or release run.
- No CodeGraphContext, unrestricted Agent loop, raw protocol/storage, shell,
  network, or unrestricted filesystem access.
- No silent change to accepted Story 12.1 or Story 12.2 behavior.
- The active root remains the sole lifecycle-state writer and committer;
  workers never stage, commit, delegate, or alter Git history.

## Options and recommendation

1. **Authorize (recommended):** proceed to dependency-ordered task generation
   against the exact accepted scope hash. No production implementation starts.
2. **Defer:** keep Story 12.3 paused with accepted plan artifacts intact.
3. **Request revision:** state the required change; this invalidates the scope
   hash and returns to the earliest affected definition owner.

## Remaining gates after authorization

Task authoring/self-review; fresh combined definition review with read-only
SpecKit analysis; separate explicit implementation approval; public-seam TDD
RED/GREEN; convergence and any traceable correction loop; independent quality
review; conditional E2E; PostgreSQL migration/atomicity, security, host-runtime
and real CodeGraph/JDT-LS qualification; pinned final verification; and
root-owned checkpoint/state/commit binding.

## Decision

The user explicitly approved the accepted scope on 2026-08-21. The active
root may proceed to dependency-ordered task generation. This decision does not
authorize production implementation and remains valid only while the accepted
scope SHA-256 is unchanged.
