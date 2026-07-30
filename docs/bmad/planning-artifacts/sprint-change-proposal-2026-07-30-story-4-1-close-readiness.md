# Sprint Change Proposal: Restore Story 4.1 Full Close Readiness

**Date:** 2026-07-30  
**Trigger:** Epic 4 closure found missing durable Full-level review and E2E evidence for Story 4.1.  
**Mode:** Batch  
**Approval:** The user approved the narrow remediation plan on 2026-07-30.  
**Scope classification:** Minor evidence and lifecycle correction.

## 1. Issue Summary

Story 4.1 implementation and verification were completed, and its completion
checkpoint is an ancestor of the current Epic 4 integration boundary. The
remaining BMad ceremony was explicitly waived at the time to recover schedule.
The durable workflow record therefore contains:

- `execution_level: "full"` and `stage: "complete"`;
- both independent implementation-review gates as `waived`, iteration zero;
- an E2E decision whose result is still `pending`; and
- a BMad story status of `done`.

Those records truthfully describe the earlier waiver, but a waiver is not an
independent `ACCEPT` verdict and a pending E2E decision is not closure evidence.
Epic 4 cannot pass its close-readiness gate until Story 4.1 has the Full-level
evidence required by the current workflow.

## 2. Impact Analysis

### Checklist results

| Item | Result |
| --- | --- |
| Trigger and evidence | Confirmed in the Story 4.1 workflow state and implementation record. |
| Epic impact | Epic 4 remains viable; closure is paused only for this evidence repair. |
| Story impact | Story 4.1 temporarily transitions `done -> review`; Stories 4.2 and 4.3 remain `done`. |
| PRD impact | None. FR-4.1 and FR-4.9 are unchanged. |
| Architecture impact | None. The accepted CodeGraphContext and pre-scan architecture are unchanged. |
| UX impact | None. No user-visible behavior is added or changed. |
| Source impact | No production-code change is planned. |
| Selected path | Direct lifecycle and evidence correction. |

This proposal does not add or change an agent tool, Scan strategy, PRD
requirement, persistence model, migration, or product behavior. Planned-change
authorization is therefore not triggered.

## 3. Recommended Approach

1. Preserve all previous implementation, qualification, verification, and
   waiver records as history.
2. Temporarily move Story 4.1 from `done` to `review` under this approved
   Correct Course authority.
3. Pin the current integrated Epic 4 source boundary at
   `b851ba165d6ebe560227bfdc3b206a814280e9b2`, while using the Story 4.1 owned
   path set and its raw BMad/SpecKit authorities.
4. Run fresh, independent Full-level acceptance and quality reviews in
   parallel against that identical immutable boundary.
5. Apply the E2E decision matrix. Story 4.1 triggers user-visible,
   authorization, API/persistence, and migration predicates, so an
   `e2e-runner` must either pass the required current-boundary scenarios or
   establish a complete, cited current-boundary coverage override.
6. If a reviewer or E2E execution identifies a genuine product defect, route
   only that raw finding through fresh TDD correction and rerun every
   invalidated gate. Do not make speculative or opportunistic product changes.
7. After both reviews accept, E2E is passed or validly covered, and final
   verification is green, restore `review -> done`, bind a new Story 4.1
   completion checkpoint, and resume Epic 4 closure.

## 4. Detailed State Changes

### Story and sprint lifecycle

- Story 4.1: `done -> review` for close-readiness remediation.
- Epic 4: remains `in-progress`.
- Stories 4.2 and 4.3: remain `done`.

### Workflow state

- Keep `execution_level: "full"`.
- Preserve the former waived gates and completion record in history.
- Reset the active acceptance and quality gates to pending iteration zero.
- Replace the unevaluated E2E placeholder with the current required predicates
  and a pending `e2e-runner` decision.
- Record the correction proposal, approval, source boundary, and resolved
  historical completion checkpoint SHA.

### Review boundary

- Base commit:
  `9b7f3ec56cd343de012620345f4297e003fdef75`
- Integrated candidate commit:
  `b851ba165d6ebe560227bfdc3b206a814280e9b2`
- Story-owned boundary SHA-256:
  `983c79d647401b0a18bcafdee201f2660592891d82ab5210fb7429f47ff8df9f`

The candidate includes downstream Epic 4 integration so the reviews judge the
behavior that will actually be closed, while the owned-path boundary keeps the
review focused on Story 4.1.

## 5. Implementation Handoff

The root sprint-loop orchestrator owns lifecycle/state edits and commits.
Fresh direct-child `acceptance-reviewer` and `quality-reviewer` agents own
read-only independent verdicts. A fresh `e2e-runner` owns required E2E
execution and may edit tests only if existing coverage is defective or
incomplete. No worker may edit production code, lifecycle state, or Git
history.

The remediation is complete only when:

- both Full review gates contain schema-valid `ACCEPT` verdicts over the same
  pinned boundary;
- the E2E decision is no longer pending and satisfies every triggered
  predicate;
- final Story 4.1 verification passes on the accepted boundary;
- Story 4.1 returns to `done` through a new uniquely bound checkpoint; and
- Epic 4 closure is retried from its eligibility gate.

## 6. Approval Record

The user approved this exact narrow remediation on 2026-07-30. The approval
does not authorize a requirement, Scan-strategy, agent-tool, migration, or
product-code change. Any such newly discovered need must follow its ordinary
authorization and review path.
