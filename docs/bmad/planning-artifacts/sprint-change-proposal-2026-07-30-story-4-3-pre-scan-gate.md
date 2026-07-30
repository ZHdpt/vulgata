# Sprint Change Proposal: Align Story 4.3 with the Canonical Pre-Scan Gate

**Date:** 2026-07-30
**Trigger:** Story 4.3 preparation exposed contradictory timeout and ownership rules.
**Mode:** Batch
**Approval:** Approved by the user on 2026-07-30 before baseline edits.
**Scope classification:** Moderate direct backlog adjustment.

## 1. Issue Summary

Story 4.3 required every low-confidence communication detection to wait for
human confirmation and required an unanswered review to expire after seven
days. The canonical July scanning architecture instead requires V1 to:

- avoid blocking on ordinary low-confidence hints;
- require human review only for ambiguity that could omit important source,
  exclude large areas, fail framework detection, or require unsupported
  resource interpretation; and
- continue with conservative scope and record a warning when review exceeds
  the V1 time limit.

The story also described immediate Worker Agent dispatch even though Epic 5
owns the Scan Coordinator queue, deterministic Orchestrator, leases, retry,
and dispatch. Its communication-type seed and notification behavior similarly
overlapped later Epic 7 and Epic 8 ownership.

These rules could not all be implemented truthfully: the same unanswered gate
could not both become expired and continue as approved.

## 2. Impact Analysis

### Checklist results

| Item | Result |
| --- | --- |
| 1.1-1.3 trigger, problem, and evidence | Done — Story 4.3 and the July pre-scan rules are the conflicting evidence. |
| 2.1-2.5 epic impact and sequencing | Done — Epic 4 remains viable; no epic is added, removed, or reordered. |
| 3.1 PRD | Done — no PRD change is required. Detailed provider/consumer/endpoint detection remains Worker-embedded under FR-5.x. |
| 3.2 architecture | Done — the July architecture remains canonical and needs no edit. |
| 3.3 UX | Done — the Repository review and bell badge remain; the full notification center and SignalR lifecycle remain Epic 8. |
| 3.4 other artifacts | Done — the Story 4.3 implementation artifact must be regenerated from the corrected epic before activation. |
| 4.1 direct adjustment | Viable — medium effort, low baseline risk. |
| 4.2 rollback | Not viable — completed Stories 4.1 and 4.2 remain valid prerequisites. |
| 4.3 MVP review | Not required — the V1 objective remains achievable. |
| 4.4 selected path | Done — direct adjustment to Epic 4 and Story 4.3. |
| 5.1-5.5 proposal and handoff | Done — captured below. |
| 6.1-6.5 final review | Done — approval was explicit; sprint status remains `backlog`; handoff is intentionally paused. |

### Epic and story impact

- **Epic 4:** Clarifies that deterministic pre-scan produces communication-kind
  hypotheses, not the full Cross-Repository detection model.
- **Story 4.3:** Blocks only on material ambiguity, continues conservatively
  after seven days, and publishes a durable `pre_scan_approved` handoff.
- **Epic 5:** Still owns Scan Coordinator queueing and actual Worker Agent
  dispatch.
- **Epic 7:** Still owns detailed provider/consumer/endpoint detection,
  Communication Pattern Catalog consumption and management, and final
  Cross-Repository detection/resolution.
- **Epic 8:** Still owns the complete notification center, SignalR delivery,
  replay, and real-time dashboard behavior.

### PRD, architecture, and UX impact

- The PRD is unchanged. FR-4.1 remains pre-scan profiling; FR-4.2 remains the
  deterministic Orchestrator and dispatch boundary; FR-5.x remains detailed
  Worker-embedded communication detection; FR-7.x remains general HITL.
- The canonical July architecture is unchanged. Its pre-scan auto-approval
  rule is now reflected in the epic.
- UX intent is unchanged. Story 4.3 retains the Chinese Repository review and
  bell badge; it does not claim completion of Epic 8's notification center.

## 3. Recommended Approach

Use a direct backlog adjustment:

1. retain deterministic, auditable communication-kind hypotheses;
2. preserve ordinary low-confidence hints as conservative metadata without
   blocking;
3. invoke a bounded LLM question generator only for blocking ambiguities;
4. cap the generated questions at five and prohibit the LLM from changing
   deterministic decisions;
5. after seven days, approve conservative scope, retain unresolved items as
   warnings, and notify the SystemOwner;
6. publish a typed `pre_scan_approved` result for Epic 5 without implementing
   dispatch in Story 4.3; and
7. keep detailed Cross-Repository detection and full notification delivery in
   their existing later epics.

This is the smallest sustainable correction. It removes an impossible state
transition while preserving the V1 scan objective and all completed Epic 4
work.

## 4. Detailed Change Proposals

### Epic 4 summary

**Old:** All agent questions pause the scan until answers and approval.

**New:** Ordinary low-confidence hints continue conservatively. Only blocking
ambiguities enter human review; a seven-day timeout continues with warnings.

### Story 4.3 confidence and question gate

**Old:** Every low-confidence or ambiguous communication detection requires
human confirmation.

**New:** Ordinary low-confidence hints are retained without blocking. Human
review is required only when ambiguity could omit important source, exclude
large areas, fail framework detection, or require unsupported interpretation.

### Story 4.3 timeout

**Old:** An unanswered gate becomes `已过期` and requires a restarted Scan.

**New:** After seven days, the gate uses conservative scope, retains unresolved
items as warnings, transitions to `pre_scan_approved`, and notifies the
SystemOwner on the next visit.

### Story 4.3 downstream ownership

**Old:** Story 4.3 directly proceeds to Worker Agent dispatch.

**New:** Story 4.3 persists and exposes an approved handoff. Epic 5 consumes
that handoff and owns actual dispatch.

## 5. Implementation Handoff

The correction is complete when:

- `docs/bmad/epics.md` contains the revised Epic 4 and Story 4.3 rules;
- Stories 4.1 and 4.2 remain unchanged and `done`;
- Story 4.3 remains `backlog`;
- no SpecKit feature, task list, implementation, migration, or activation
  record exists for Story 4.3; and
- a later sprint-loop run regenerates the Story 4.3 implementation artifact
  from this corrected authority before activation.

The intended handoff recipient is the root BMad sprint-loop orchestrator, but
handoff is deliberately paused at the user's request.

## 6. Approval Record

The user approved the recommended correct-course remediation on 2026-07-30
with the condition: apply the correction, then pause before handing on Story
4.3.
