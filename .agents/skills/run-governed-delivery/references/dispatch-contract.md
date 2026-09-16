# Dispatch Contract

Every worker dispatch contains exactly these seven numbered slots:

1. `role_and_objective`: Name the role and one bounded objective.
2. `authoritative_inputs`: Provide repository-relative paths to raw authoritative inputs; do not replace sources with another agent's conclusions.
3. `workflow_boundary`: State the current stage, lifecycle story key, feature directory, source commit, and pinned diff boundary.
4. `constraints_and_skills`: Name applicable repository constraints and installed skills the worker must follow.
5. `write_boundary`: List exact permitted writes and prohibited adjacent work. Only the active root orchestrator may stage or commit.
6. `output_and_evidence`: Define the output schema, artifact destination, and required evidence.
7. `completion_and_failure`: Define done conditions and how to report conflicts, unavailable evidence, unsafe actions, or other failures without advancing state.

Judges and reviewers receive raw pinned artifacts, not author reasoning. A correction dispatch may additionally include the unchanged raw finding because correcting it is the bounded objective.

Definition follow-ups identify the active ordinary/recovery profile and stage-scoped write set. Include relevant lessons, current raw inputs, ordered prior raw verdict paths, and the recovery hypothesis/evidence when applicable. Reuse retained sessions; exclusive handover ends dispatches to both superseded authors. Reject results against superseded profile, input, or write boundaries. The root runs the targeted state guard immediately before corrective dispatch, escalation, and resume advancement; a previous allowed result is not authorization after boundary changes.

TDD follow-ups reuse the coherent group's available/current test and production owners. Keep tests and production in disjoint write sets; an unavailable owner is replaced from raw scope, artifacts, findings, and observed RED/GREEN evidence, not just a summary. Completion includes each author's final-shape assessment and applicable final checks. A brief no-refactor-needed disposition is valid; no additional report artifact or lifecycle stage is required.

During specify/plan follow-ups, the active author may send root an intermediate optional question with its provisional approach and affected topic while continuing authorized work. Root is the user-facing channel, selects nonblocking input or commentary, and relays actual responses with their original context. Reconcile replies before accepting returned artifacts; material late changes invalidate the earliest affected owner without resetting recovery. Do not store unanswered questions or runtime handles in workflow state, and do not treat a preference as gate approval. See `human-interaction.md`.

Convergence dispatch includes implementation-prerequisite evidence and the existing task boundary for reuse/append checks. Quality dispatch includes the validated lookback proof and affected project-document/lesson outputs. No worker acquires project-memory, state, approval, or Git ownership from these inputs.

## Approval and escalation

A schema-valid independent `ACCEPT` satisfies its gate without duplicate confirmation. Ordinary blocking analysis findings return to the earliest owner. Diagnose after two failed repairs (total failed result 3); pause before another repair on total result 4 or its accepted finite extension limit. Escalating the author preserves that allowance. Human authority remains required for planned-change triggers, initial implementation, unresolved intent/scope, new architecture or prior-authority contradictions, hard blockers, unsafe recovery/actions, and the reserved epic dispositions. Each actual request preserves the authority contract in `review-and-correction.md` and uses the appropriate user-visible briefing from `human-interaction.md`.
