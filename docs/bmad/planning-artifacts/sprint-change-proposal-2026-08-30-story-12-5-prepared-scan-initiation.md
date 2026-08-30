# Sprint Change Proposal: Story 12.5 Prepared Scan Initiation

**Date:** 2026-08-30

**Story:** 12.5 — Grounded LLM-Wiki Chat & August 30 Release Evidence

**Mode:** Expedited batch

**Classification:** Moderate, bounded in-place prerequisite correction

**Approval:** Explicitly approved in the user instruction selecting the bounded
in-place correct-course/specification strategy. This approval permits authority,
specification, planning, and independent plan review only. It does not satisfy
the post-review Scan planned-change authorization gate.

## 1. Issue Summary

The first clean Track C-MVP host run proved that the production Prepared Scan
endpoint assumes durable initiation state that ordinary production users cannot
create. `PreparedScanPairBuilder` correctly owns the accepted CodeGraph 1.5,
JDT-LS, safe XML, deterministic selection, and atomic Prepared Scan Manifest
pipeline, but it requires two matching Scan Runs and one Preparing
Two-Repository coordination. Existing tests commonly seed those prerequisites.

The failed `/scan-runs` workaround is not the solution. It resolves the
historical `RepositoryProfilingCoordinator`, which requires the retired
`ICodeGraphProvider`/`CodeGraphContextProvider` contract. Story 12.1 AD-10 and
the August 30 architecture correctly exclude that path from production.

## 2. Checklist and Impact Analysis

### Trigger and evidence

- [x] Story 12.5 Track C-MVP T010 exposed the missing entrance on a clean
  isolated PostgreSQL database.
- [x] The authenticated Prepared Scan endpoint exists and validates exactly two
  Repository revision bindings.
- [x] The existing builder rejects the request when its Scan Runs or Preparing
  coordination are absent.
- [x] Real CodeGraph 1.5 qualification remains passing evidence; this is not a
  CodeGraph runtime failure.

### Epic and story impact

- [x] Epic 12 remains achievable without a new story or story reorder.
- [x] Story 12.5 gains one narrow production prerequisite: initiate or replay
  the exact two-Repository Prepared Scan boundary before invoking its existing
  preparation pipeline.
- [x] Stories 12.1 through 12.4 remain accepted dependencies and are not
  redefined.
- [x] General Scan entry consolidation remains post-competition backlog.

### Artifact impact

- [x] The PRD August 30 overlay must name the production initiation entrance.
- [x] Epic 12 Story 12.5 must accept creation/replay, mismatch rejection,
  authorization, and retry behavior from ordinary System/Repository records.
- [x] The August 30 architecture must define ordering and transaction bounds.
- [x] Story 12.5 specification and plan return to their earliest owners.
- [N/A] UI/UX: the existing authenticated endpoint and Chinese product flow are
  reused; no new screen or interaction pattern is required.
- [N/A] Persistence shape: existing tables, factories, constraints, and stores
  are sufficient; no EF migration or model change is permitted.

### Path evaluation

- [x] Direct adjustment inside Story 12.5: **selected**. Medium effort and
  medium integration risk; it restores the already-required production
  entrance without changing later Scan behavior.
- [x] Rollback/legacy profiling restoration: **rejected**. It conflicts with
  Story 12.1 AD-10 and the current CodeGraph 1.5 runtime.
- [x] Separate corrective story: **rejected by explicit user direction**.
- [x] MVP reduction: **rejected**. Real clean-database Scan remains mandatory
  release value.

## 3. Corrected Production Boundary

```text
authenticated Prepared Scan request
-> validate one visible System and exactly two visible distinct Repositories
-> validate safe Git refs and exact expected Profiled Revisions
-> short atomic initiation transaction: create or replay two bound Scan Runs,
   two new-path Scan Executions, and one Preparing Two-Repository coordination
-> commit and close the transaction
-> invoke the existing PreparedScanCoordinator and PreparedScanPairBuilder
-> acquire/verify source and run CodeGraph 1.5, JDT-LS, safe XML, selection,
   and atomic Prepared Scan Manifest publication
-> recheck authorization immediately before publication
-> return the existing prepared response/handoff
```

The initiation transaction performs no Git, CodeGraph, JDT-LS, XML, Worker,
LLM, Pass 1, Cross-Repository Resolution, Pass 2, Document, Edge, Uncertainty,
Chat, or evidence publication operation.

## 4. Required Initiation Semantics

1. The request identifies exactly one System and exactly two distinct
   Repositories with safe full Git refs and exact lowercase Profiled Revisions.
2. The server checks authenticated ownership/visibility before durable
   creation. The established authorization recheck immediately before
   Prepared Scan publication remains mandatory.
3. A short Infrastructure-owned transaction uses existing domain factories and
   tables to create the complete initiation tuple or expose none of it. It never
   spans external analysis.
4. The two Scan Runs bind exact Profiled Revisions. The two new-path Scan
   Executions bind the existing current prompt, Document schema, and Analysis
   Runtime identities. The coordination binds the canonical pair in Preparing.
5. An identical authorized request returns the same durable tuple. A request
   that changes System, Repository pair, Profiled Revision, runtime identity,
   prompt identity, Document schema identity, or authorization scope conflicts
   or fails closed and cannot attach to the tuple.
6. If initiation commits and later preparation fails, an exact retry reuses the
   same tuple and resumes/replays existing Prepared Scan behavior. No old v1
   profiling result can satisfy the prepared handoff.
7. Concurrency must not create cross-paired or partially visible tuples. The
   plan must select an existing-schema locking/isolation strategy and test it
   against PostgreSQL without adding a migration.

## 5. Detailed Authority Edits

### PRD August 30 overlay

**Old:** The overlay requires a real two-Repository Scan but does not state how
ordinary authenticated System and Repository records enter Prepared Scan.

**New:** The authenticated exactly-two-Repository Prepared Scan request creates
or replays the complete bound initiation tuple before the existing PreScan
pipeline, with atomic visibility, exact identity matching, and no external work
inside the transaction.

### Epic 12 Story 12.5

**Old:** Acceptance begins after prepared prerequisites already exist.

**New:** Acceptance begins with one visible System, exactly two visible ordinary
Repositories, and exact Git/ref revision bindings, then proves atomic initiation,
idempotent replay, mismatch rejection, authorization denial with zero writes,
retry after preparation failure, and handoff to the existing Prepared Scan path.

### August 30 architecture

**Old:** Sections 4 and 10 describe PreScan and two-Repository coordination but
leave initiation ownership implicit.

**New:** A narrow initiation coordinator/store owns only the short durable
entrance transaction. `PreparedScanCoordinator` continues to own external
preparation and final publication.

### Story 12.5 feature

The specification and plan must define exact outcomes, identities, transaction
and replay behavior, production DI ownership, vertical integration tests, and
the later fresh-database release rerun. Tasks and production work remain closed
until independent plan review and the repository Scan planned-change decision.

## 6. Verification Strategy

The definition and plan require one production-composition vertical test that
starts with only an authenticated authorized user, one ordinary System, exactly
two ordinary Repository records, exact Git/ref revision bindings, and ready
CodeGraph 1.5/JDT-LS runtime. It must not pre-seed a Scan Run, Pre-Scan Result,
Scan Execution, coordination, Prepared Scan Manifest, File Code Unit, or
Document.

The test proves creation/replay, handoff to the existing builder, Prepared Scan
Manifest publication, identical-request idempotency, revision/pair mismatch
rejection, unauthorized zero-write behavior, safe retry after post-initiation
preparation failure, CodeGraph 1.5 production composition with no CGC, and zero
Worker/Pass 1/Pass 2/Document/Edge/Uncertainty/Chat/LLM activity during the
initiation/preparation boundary.

Real CodeGraph CLI qualification remains supporting evidence rather than a
substitute for this vertical test.

## 7. Explicit Non-Goals and Post-Competition Backlog

This correction does not register or modify `CodeGraphContextProvider`, register
the historical `RepositoryProfilingCoordinator`, redesign the general Scan
lifecycle, restore single/standalone profiling, support arbitrary N-Repository
initiation, remove compatibility code/tables, add a migration, change
`VulgataDbContext` or its snapshot, modify Story 12.4, weaken security or Chat,
alter Track A/B, use fake Backup B evidence, or delete the retained blocked
database.

Complete legacy Scan-entry consolidation is post-competition backlog.

## 8. Handoff, Estimate, and Gates

Classification is **moderate**. Route specification to the spec author, the
transaction/architecture plan to the high-reasoning plan architect, and the
accepted plan to an independent plan-only reviewer.

Estimated implementation after all approvals: 6–10 hours for TDD, production
composition, PostgreSQL concurrency/replay verification, surrounding regression,
convergence, and quality review. Estimated fresh real run: 2–5 hours depending
on CodeGraph/JDT-LS/LLM latency and remediation-free completion.

After independent plan-review `ACCEPT`, pause for explicit Scan planned-change
authorization. That later approval permits task generation only; definition
review and implementation approval still follow before the first production
write.

## 9. Approval Record

The user explicitly selected this bounded in-place correct-course/specification
strategy on 2026-08-30. The prior overnight standing authorization is expired
and is not used. The user instruction authorizes correction through independent
plan review and requires a pause at the post-review planned-change gate.
