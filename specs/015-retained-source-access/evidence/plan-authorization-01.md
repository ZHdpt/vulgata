# Story 13.1 plan authorization

Decision ID: 13-1/plan-authorization/1
Status: accepted
Requested at: 2026-09-14T09:54:06.960600Z
Scope SHA-256: `0dd320e5015dae3efecb7721c862b4c9a479c320c770b5f925904cbb97955e4b`
Accepted boundary: [plan-review-boundary-02.json](plan-review-boundary-02.json)
Independent verdict: [plan-review-02.json](plan-review-02.json) — ACCEPT, no findings.

## Decision requested

Authorize progression from the accepted Story 13.1 plan to task generation only. The root recommends proceeding with this reviewed design; the alternative is to request specific plan revisions and repeat affected review. No production implementation is authorized by this decision.

## Trigger and controlling authorities

The plan makes architecture decisions for the shared retained-source owner and materially changes generic source acquisition/retry used by Scan inputs. Root AGENTS.md, Planned-change authorization, requires a pause after independent plan acceptance and before tasks for a materially changed Scan strategy. The current run-bmad-sprint-loop skill also requires explicit authorization for architecture decisions before tasks.

Affected plan: [plan.md](../plan.md), Architecture Decisions and Planned-Change and Approval Gates. Product references: docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md FR-2.4, FR-2.5, FR-4.9; NFR-2.3, NFR-3.1, NFR-3.3 and source portions of NFR-4.1. Architecture references: AD-2/3/30/31, with the other constraints traced by the plan.

## Proposed approach and effects

- Read raw committed Git tree/blob bytes; retain a verified flat blob/manifest object under configured durable source storage. Public identity stays Repository plus full Profiled Revision.
- Add one source-owned immutable SourceSnapshots record. PostgreSQL commit makes a fully verified object visible; a cross-process identity lock coordinates publication, retry adoption and stale cleanup.
- Issue source-specific live-authorized read contexts for eligible System and standalone Repositories. Ordinary access has relative locations, exact bytes, explicit unsupported outcomes and cumulative server-owned budgets/deadlines. Capture caps are also server-owned and cannot be raised by callers.
- Keep physical workspace materialization internal to trusted Infrastructure adapters; preserve existing pair consumers. No Agent tool or full Scan/Knowledge/Chat route is added.
- Add only source-owned schema/configuration/composition and associated qualification. The restrictive Repository foreign key preserves retained source; the existing Repository delete path returns a sanitized Chinese 409 when retained-source records prevent deletion.
- Require isolated Git fixtures, owned PostgreSQL migration/publication checks and a true separate OS-process reopen with the remote unavailable. Full Docker analysis-stack, tool/provider, backup and restore qualification remains outside this story.

## Root implementation intent and remaining gates

After approval, dispatch task-author at configured Luna/medium, then a fresh combined definition-reviewer at Sol/medium. After accepted complete definition, request the mandatory separate implementation approval bound to those artifact hashes. Only then dispatch the configured separate TDD test/production roles. Convergence, independent quality review, E2E decision, security/migration checks, build/full tests and pinned completion verification remain mandatory.

Historical plans and competition waivers remain inactive. This request cannot authorize Story 13.2 or any other story. Activation checkpoint: cd82c8994063fa522253264f59c13b1cbc08a3f2. Plan artifacts remain in the working tree until the accepted-definition checkpoint.

## Response

User response: "Approval authorizes only progression to tasks"

Recorded at: 2026-09-14T10:18:58.368839Z

Accepted solely for progression to task generation on the bound plan scope. Production implementation remains unauthorized.
