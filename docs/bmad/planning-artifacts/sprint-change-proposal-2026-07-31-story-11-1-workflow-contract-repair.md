# Sprint Change Proposal: Story 11.1 Workflow Contract Repair

**Date:** 2026-07-31
**Trigger:** The Story 11.1 combined quality review accepted the product
implementation but blocked lifecycle completion because the persisted workflow
record does not satisfy the workflow schema and cannot represent the checkout's
canonical Git/worktree identities.
**Mode:** Batch
**Approval:** Approved by the user on 2026-07-31.
**Scope classification:** Minor workflow-only scope change with moderate
implementation risk.

## 1. Issue Summary

Story 11.1's knowledge-store implementation, migration, security behavior, and
tests are not the problem. The blocking defect is in the orchestration record:
`workflow-state.json` contains noncanonical stages, statuses, evidence kinds,
identifiers, dispatch outputs, and a stale preflight/source boundary. The
activation record also drifted from its historical activation checkpoint.

The executable workflow contract rejects absolute paths everywhere, while a
linked worktree can only identify its Git common directory and worktree
unambiguously with canonical absolute identity paths. Durable artifact paths
must remain repository-relative.

The review also requested historical raw outputs that were not durably saved.
Those outputs must not be reconstructed. The expedited exception expressly
omits the routine analysis pass and independent evidence ledger, so recovery
will persist fresh gate-bearing raw results and concise verification evidence
without fabricating or expanding the approved workflow.

## 2. Evidence and Impact Analysis

### Concrete evidence

- The workflow-owned validator reports 58 errors in the current Story 11.1
  state: invalid scalar/history vocabulary, invalid preflight identities, null
  dispatch outputs, and invalid verification/checkpoint records.
- The activation checkpoint
  `bmad-11-1-tasks-20260731T010200Z-a4d1c7` still resolves uniquely at the
  accepted delivery-definition boundary, but the activation record was
  overwritten with current review progress.
- The current quality gate is blocked; the two prior `REVISE` results consume
  revision attempts, while `BLOCKED` results do not.
- The recorded preflight and E2E boundary predate the current implementation
  and do not cover the implemented persistence, migration, retrieval, or
  authorization behavior.

### Checklist results

| Item | Result |
| --- | --- |
| Trigger and evidence | Done — Story 11.1 quality findings and the 58 validator errors are concrete. |
| Current epic impact | Done — Epic 11 remains viable; only Story 11.1 lifecycle completion is blocked. |
| Future epic impact | Done — Stories 11.2–11.5 benefit from the repaired reusable workflow contract; their scope and order do not change. |
| PRD impact | None — no requirement, MVP boundary, or acceptance criterion changes. |
| Architecture impact | Workflow-only — distinguish repository-relative artifact paths from canonical checkout identity paths. |
| UX impact | None. |
| Deployment/production impact | None — no runtime, container, database, EF model, or migration change. |
| Direct adjustment | Viable — low product risk and moderate workflow effort. |
| Rollback | Not viable — it would discard accepted Story 11.1 product work without correcting the contract defect. |
| PRD MVP review | Not needed — the approved August 15 demonstration slice is unchanged. |
| Handoff | Root orchestrator owns lifecycle state and commits; a workflow implementer owns schema/validator/tests/docs; independent reviewers remain read-only. |

## 3. Recommended Approach

Use a direct, test-first workflow repair:

1. Introduce an explicit checkout-identity path contract for
   `git_common_directory` and `worktree_path`. It accepts canonical absolute
   identities (and the existing repository-relative fixture form) while all
   durable artifact paths remain repository-relative and traversal-safe.
2. Clarify that the canonical workflow state and gate-bearing raw verdicts are
   not an additional independent evidence ledger. Under `expedited-mvp`, omit
   Full-only analysis records and never reconstruct unavailable historical raw
   output.
3. Restore the activation record to its immutable `tasks` activation
   checkpoint. Rebuild Story 11.1 state using exact stage/status/evidence
   vocabularies, fresh preflight and source pins, and a current nonrecursive
   E2E boundary.
4. Persist fresh raw outputs for the gates rerun during recovery, validate the
   rebuilt state, and obtain a fresh combined quality-review verdict on the
   pinned boundary.
5. Complete the conditional E2E and verification gates required for Story 11.1
   without claiming the Story 11.5 final two-Repository demonstration or final
   Docker release check early.

## 4. Scope and Guardrails

### Included

- `docs/agent-workflow/` contract clarification.
- Run BMad Sprint Loop schema, validator, and validator tests.
- This approved Sprint Change Proposal.
- Story 11.1 activation, development record, workflow state, and fresh
  gate/evidence artifacts.
- Story 11.1 lifecycle status after all gates pass.

### Excluded

- Story 11.1 product behavior, tests, domain model, persistence implementation,
  EF migration, and model snapshot.
- Every Story 4.3 file.
- PRD, epic scope, UX, production deployment, and the August 15 exception's
  quality floor.
- Fabricated historical verdicts or an independent evidence ledger.
- Story 11.5's final clean-database two-Repository demonstration and final
  Docker release verification.

## 5. Handoff and Sequence

| Step | Owner | Outcome |
| --- | --- | --- |
| Contract RED/GREEN | Workflow implementer | Focused tests prove identity-path and expedited recovery semantics. |
| Contract integration | Root orchestrator | Docs, schema, validator, and tests agree. |
| State recovery | Root orchestrator | Historical activation restored; canonical state validates with fresh pins. |
| Combined review | Fresh quality reviewer | Raw verdict is persisted unchanged against the pinned current boundary. |
| Conditional E2E | E2E runner when required | Story 11.1 predicates pass or a complete current-boundary coverage override is recorded. |
| Verification and completion | Root orchestrator | Required tests/build/migration checks pass; Story and sprint state become `done`. |

The integration owner remains the sole authority for EF migration and model
snapshot changes. This proposal requires none.

## 6. Success Criteria

- Workflow contract tests and repository workflow validation pass.
- Story 11.1 `workflow-state.json` passes `validate_workflow_state` with zero
  errors.
- The activation record points to the historical `tasks` checkpoint.
- The current combined quality-review raw verdict is durable and bound to the
  reviewed commit.
- The Story 11.1 E2E decision uses the current nonrecursive product boundary
  and real predicate evidence.
- Required Story 11.1 tests, PostgreSQL/migration checks, and Web build pass.
- Story 11.1 reaches `done` without modifying Story 4.3, EF migration, or model
  snapshot files.

## 7. Approval and Handoff

The user explicitly approved this bounded workflow-contract repair on
2026-07-31. The next action is the test-first contract implementation, followed
by canonical Story 11.1 state recovery and fresh independent review.
