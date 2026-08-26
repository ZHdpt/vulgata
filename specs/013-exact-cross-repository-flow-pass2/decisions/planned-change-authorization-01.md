# Story 12.4 planned-change authorization 01

- Decision ID: `story-12-4-planned-change-authorization-01`
- Status: pending
- Scope SHA-256: `683F10B848D93A843EF61F0CAC11518F9048E4FC7F5E23ACDAE116C3C8CF92E0`
- Accepted plan review: `reviews/plan-review/plan-review-03.json`
- Accepted plan SHA-256: `77CBCE41E3C4CAB9B6ECE0220A2EE29A64BECD9D0E6872499EA1877C78840F94`
- Specification SHA-256: `50327089B48F6ACCFBA1267E762CD2AF29E9F33A53E249B7FB8590C06D671517`

## Decision requested

Authorize the accepted Story 12.4 plan to advance to task generation. This is
required because the plan:

1. materially adds Scan strategy for deterministic Cross-Repository
   Resolution, endpoint-centered Flow Group planning, one Pass 2 job per
   complete group, omission accounting, and Pass 2 lifecycle transitions;
2. materially adds the Worker Agent tools `retrieve_code_logic` and
   `submit_business_logic` in a strict bounded two-function loop; and
3. implements the approved dated exact Custom RPC/safely normalizable HTTP
   narrowing of the broader PRD FR-4.5, FR-6.1, and FR-6.2 behavior.

The approach preserves unique-current-provider-only resolution, honest
Uncertainties, deterministic bounded membership, immutable Business Logic
Document Versions, and complete all-and-only-used `GENERATED_FROM` provenance.
It uses one additive compatibility migration and deepens the accepted Story
12.1–12.3 execution and persistence seams.

## Effect of authorization

Approval permits only dependency-ordered task generation from the accepted
definition. It does not authorize production implementation and does not waive
the later independent definition review, explicit implementation approval,
TDD, convergence, quality review, conditional E2E, final verification, or
completion checkpoint gates. Declining keeps Story 12.4 at plan review so the
scope can be revised or deferred.

## Authorities

- `specs/013-exact-cross-repository-flow-pass2/plan.md`
- `specs/013-exact-cross-repository-flow-pass2/contracts/`
- `docs/bmad/epics.md`, Story 12.4
- `docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md`, sections 11–12
- `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md`
- `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md`, FR-4.5, FR-6.1, FR-6.2
- `AGENTS.md`, Planned-change authorization
