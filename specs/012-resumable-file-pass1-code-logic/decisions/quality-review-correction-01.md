# Story 12.3 Quality-Review Correction Authorization 01

Status: pending explicit user authorization

Source verdict: `reviews/quality-review/quality-review-01.json`

Reviewed commit: `03080c6963b4cbe2a6c952459e33d044f1146466`

## Trigger

Quality review attempt 1 returned `REVISE` with four High findings. `QR-001` is
owned first by the plan because the accepted design names a prepared dispatcher
but does not define the production execution coordinator that owns claim,
server-built dispatch packet, Worker invocation, atomic result/failure handling,
shared-capacity continuation, and terminal reconciliation. The correction also
materially changes Worker dispatch, provider-probe scheduling, navigation
authorization, replay validation, and production-composed verification. These
are planned-change authorization triggers under `AGENTS.md`.

## Proposed approach and effects

1. Define one host-run production Pass 1 execution coordinator over the accepted
   durable pair/lease seams. It will claim only authorized prepared jobs, build
   the server-owned Worker packet, invoke the closed dispatcher, commit or
   classify the result, preserve the shared one-through-four concurrency limit,
   and invoke deterministic reconciliation only after terminal jobs.
2. Replace configuration-resolution health with the accepted fixed source-free
   provider request and define the durable probe driver for the exact
   10/30/60/120/300-second windows. Claims remain stopped while the circuit is
   unavailable; no automatic resume is added after the fifth failure.
3. Bind every `navigate_code` call and receipt to the current lease token and
   expected job version, revalidating pair/job/circuit/expiry authorization
   before evidence disclosure or durable receipt mutation.
4. Make completed replay reconstruct and compare the entire persisted canonical
   Document graph: Source Anchors, Document Sections, Index Entry, and direct
   REFERENCES Edges, including exact order and cardinality.
5. Add production-composed Start-to-terminal and provider-outage integration
   evidence. Existing runtime/version checks remain supporting qualification.

Effects: authenticated Start becomes capable of executing the prepared pair to
a truthful terminal result; stale Workers cannot navigate; provider failure
transitions become reachable; corrupted graph replay fails closed. Story 12.1
and 12.2 identities and atomicity stay authoritative. Story 12.4/12.5,
Cross-Repository Resolution, Pass 2, Business Logic Documents,
CodeGraphContext, raw LSP/CodeGraph/shell/network/filesystem access, and
automatic background resume remain excluded.

## Implementation intent

If authorized, the root will return to plan correction, obtain an independent
plan-review ACCEPT, regenerate dependency-ordered tasks, rerun definition review,
then execute the correction test-first. The missing production coordinator will
be a bounded host service over existing ports, not an unrestricted Agent loop or
a Docker-hosted Vulgata runtime.

## Affected plan sections

- `Planned production ownership`
- `AD-2`, `AD-3`, `AD-5`, `AD-9`, `AD-10`, and `AD-11`
- `Failure, Recovery, and Security Invariants`
- `Public-Seam Test-First Slices`
- `Verification Strategy`, including PostgreSQL and real provider qualification
- `Requirement and Authority Traceability`
- `Planned-Change Authorization Disclosure`

## Affected requirements and PRD clauses

- Story requirements: FR-004, FR-006, FR-007, FR-011, FR-014, FR-017,
  FR-018, FR-032 through FR-044, and FR-040 through FR-052 where production
  execution reaches retry, completion, and reconciliation.
- Success criteria: SC-001, SC-003, SC-004, SC-006 through SC-008, and
  SC-010 through SC-012.
- PRD traceability already recorded by the plan: FR-4.2, FR-4.3, FR-4.4,
  FR-4.6, FR-4.7, FR-4.9, FR-4.10, FR-4.11, FR-4.12, FR-11.7,
  NFR-2.3, NFR-3.1 through NFR-3.3, and the dated NFR-4.1 host-runtime
  replacement.

## Remaining gates

Authorization does not accept a corrected plan or waive any gate. Remaining:
corrected plan, independent plan-review ACCEPT, task regeneration, definition
review ACCEPT, honest TDD RED/GREEN, convergence, fresh quality review,
conditional E2E, final verification, and a bound `review -> done` checkpoint.
