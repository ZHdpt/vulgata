---
name: run-governed-delivery
description: Use when implementing or resuming governed stories or epics through an installed lifecycle adapter, SpecKit definition, TDD construction, and recoverable delivery controls.
---

# Run Governed Delivery

Deliver each lifecycle story as one recoverable SpecKit feature. The configured lifecycle adapter owns queue and status, SpecKit owns definition and convergence, `governed-tdd` owns behavior changes, repository authorities own invariants, and the active root owns state, evidence, gates, checkpoints, and Git. New installs use BMad 6.12 and SpecKit 1.0.6; 6.11/0.16 adapters remain supported for pinned projects, with 0.16 limited to existing feature mappings under the new naming policy.

Read only the branch references needed by the current stage:

- lifecycle and resume: `references/state-machine.md`;
- durable records and dispatch shapes: `references/artifact-contracts.md`;
- roles and model escalation: `references/agent-catalog.md`;
- plan-contained implementation design: `references/implementation-design.md`;
- correction routing: `references/review-and-correction.md`;
- optional drafting input, decision briefings, and recovery reports: `references/human-interaction.md`;
- relevant lessons at context/replan and completion learning: `references/story-lookback.md`;
- E2E predicates: `references/e2e-decision-matrix.md`;
- operator recovery: `references/operator-runbook.md`.

Before using lifecycle or definition tooling, require a readable `.governed-delivery/config.toml` and read the selected contracts under `.governed-delivery/adapters/`. Adapter contracts own version-specific skills, scripts, output fields, hooks, and implementation-provider equivalence. Commit the configuration before creating checkpoints; missing, mutable-only, unreadable, or unsupported adapter configuration blocks the affected stage.

Use the consuming repository's canonical glossary when present. Surface Constitution, ADR, context, product-requirement, or repository-policy conflicts instead of resolving authority by inference.

## Inputs and Queue

Resolve exactly one target: `next`, `resume`, one story, one epic, or a dependency-ordered list. `dry-run` reports resolution, ownership, stages, agents, gates, checkpoints, and E2E predicates without mutation. Reject ambiguous keys, duplicate activation, and new work for `done` stories. Never expand scope to an unmet prerequisite without user authorization.

New features use `specs/<canonical-bmad-story-key>` (for example, `specs/18-1-cancellation-boundary`), never an incrementing index. Run the read-only `scripts/feature_selection.py --repo <root> --story-key <full-key>` before activation; existing mappings always resume in place, including old numeric directories. Follow `references/operator-runbook.md` and the selected adapter for explicit-directory initialization; collisions stop rather than renumber. No historical rename or branch change is implied.

New activations use compact `schema_version: "3.1"`, capped at 12 KiB canonical JSON. Keep current artifact/gate/decision pointers, one nullable recovery, retained transition-head proof, active definition-author profile, lookback proof, checkpoint, and resume data. Detailed history stays in existing artifacts and Git. Historical completed 3.0 states retain their original contract; active 3.0 adoption is bounded and root-owned, as described in `references/state-machine.md`.

## Preflight and Ownership

Before mutation, observe branch, full `HEAD`, repository root, Git common directory, worktree path, exact porcelain/index state, owned paths, unrelated paths, and observation time. Store branch/HEAD plus hashes of Git identity and status in compact state; put any detailed ownership record needed for recovery in the development record. Load the selected lifecycle artifacts, Constitution, relevant ADR/context documents, repository instructions, and current mapped state. Require a non-detached branch unless the host provides an explicit native handoff. Preserve unrelated work and stop when owned and unrelated changes are inseparable.

The active root is the sole lifecycle/state writer and committer. Workers never stage or commit. The root stages explicit owned paths only after refreshing preflight, inspecting the staged diff, running `git diff --cached --check`, and completing checkpoint verification. No implicit branch switch, reset, rebase, merge, push, force-push, history rewrite, or pull request.

At context and replan, retrieve only applicable entries from `paths.project_lessons` (default `docs/project-lessons.md`). Supply the relevant source/precaution to the author and reviewer; missing default content means no recorded lessons. The root owns selective lesson maintenance and approved canonical-document synchronization.

## Unified Story Flow

```text
preflight/context
-> persistent definition-author: speckit-specify + self-review
-> same author follow-up: speckit-plan + implementation-design classification + self-review
-> conditional early plan-review + planned-change authorization
-> same author follow-up: speckit-tasks + self-review
-> independent definition-review
-> implementation approval
-> implementation: behavior-driven TDD or appropriate direct verification
-> convergence: appropriate code/test correction or earliest-owner definition recovery
-> root story lookback
-> independent quality-review
-> conditional-e2e
-> pinned verification
-> complete
```

### 1. Define

Spawn one `definition-author` once per story at `gpt-6-sol`, medium. Reuse that same author session through stage-scoped follow-ups for `speckit-specify`, `speckit-plan`, `speckit-tasks`, and later definition corrections; retain its handle through story completion unless an exclusive recovery takeover supersedes it. Each follow-up permits only the current phase; the root validates, maps, and hashes its result before downstream writing. Self-review intent/acceptance, feasibility/design, task coverage/order, and relevant lesson precautions. Before recovery takeover, `plan-architect-high` (Sol/high) retains its serialized architecture-plan exception. Evidence-backed difficult definition recovery may exclusively replace the author with `definition-author-recovery` (Astra/medium), then the high profile only for a recorded deeper-analysis trigger; use `references/agent-catalog.md`. Retain the selected recovery tier through completion, including tasks. The author never owns production, tests, project lessons, or canonical project authorities.

For SpecKit 1.0.6, `SPECIFY_FEATURE_DIRECTORY` plus `.specify/feature.json` owns the feature path, plan setup returns `FEATURE_DIR`, and the selected PowerShell/Bash/Python script set must match `.specify/init-options.json`. Preserve the plan-contained implementation-design section through template resolution. Treat unreadable `extensions.yml` as a blocker, not a silent skip.

Self-review is part of each authoring turn, not an independent gate or extra worker.

During specification/planning, the active author may send root a useful preference question with its provisional approach and continue authorized drafting. Root uses genuinely nonblocking input when available, otherwise commentary; silence creates no approval or state event. Follow `references/human-interaction.md` for question selection and late replies. Reconcile relevant actual user replies before accepting the author's return; material changes follow earliest-owner invalidation and existing review, never retroactive approval.

Before plan review or tasks, run deterministic plan-content validation from `references/implementation-design.md`. Missing governed front matter is implicit `none`; it is invalid when governed design content/IDs exist or an active plan-review trigger requires explicit classification. After tasks, validate plan/task paths, hashes, ID coverage, and unknown references before dispatching the independent reviewer.

When `AGENTS.md` identifies a planned-change trigger or the plan introduces an architecture decision/change, dispatch `definition-reviewer` in plan-only mode after the plan and retain its session handle. An accepted independent verdict is required before the root requests the named authorization (human by default, or the explicitly designated delegate under the User Approval Policy) and before task generation. The request identifies the repository-policy or architecture trigger and its consequences. Approval authorizes only progression to tasks. For untriggered work, continue directly to tasks.

After tasks, reuse the same `definition-reviewer` session when plan-only review already created it; otherwise spawn it once at `gpt-6-sol`, medium. Retain it through story completion and reuse it for later correction reviews. Every reviewer follow-up receives the current raw lifecycle story, specification, complete plan set, Constitution, ADRs, contexts, relevant source, pinned hashes/boundary, and ordered prior raw verdict paths; include tasks only for complete-definition or later reviews where a current tasks artifact exists. If that session is unavailable, a replacement receives the same mode-applicable current raw artifacts and complete ordered verdict history rather than an author summary or only the latest verdict. It runs the read-only `speckit-analyze` checks and judges lifecycle-intent alignment, implementation-design classification, feasibility, testability, terminology, coverage, task ordering when applicable, resolution of earlier findings, and unresolved placeholders in one verdict. Suppress SpecKit's optional remediation question: the reviewer never asks the user or edits; raw findings return through this workflow's correction policy.

Every finding names its earliest owner: `spec`, `plan`, or `tasks`. Send unchanged raw findings to the active persistent author for only that phase. A spec correction invalidates the prior classification; a finding that challenges the classification or its trigger also requires fresh classification. Before takeover, architecture content returns to retained `plan-architect-high` only while the accepted classification and trigger remain valid, then tasks return to the ordinary author. After takeover, the recovery author owns every classification and definition phase; neither former author can reclaim writes. Reconcile affected downstream tasks while preserving valid text, IDs, completed work, and evidence. Reuse the retained reviewer without asking the user to approve ordinary correction, and apply the recovery guard below.

After the complete definition is independently accepted, obtain the scoped human or permitted delegated decision at `implementation-approval` before the first production write. Present the decision using the format in `review-and-correction.md`. The approval binds the accepted artifact hashes; it covers all internal seams and architecture choices already disclosed by those artifacts. Do not ask again unless the approved scope changes or a new approval trigger appears.

### 2. Implement with appropriate verification

Use governed-tdd's verification routing before dispatch: choose by accepted behavior and risk, not file type. Record the route/reason in existing task and dispatch evidence; do not create a new gate. Partition accepted tasks into coherent dependency-ready slices. For every observable production-behavior change:

1. a `tdd-test-author` (`gpt-6-luna`, high) writes one public-seam behavioral test and observes the expected RED;
2. a `tdd-implementer` (`gpt-6-sol`, medium) makes the minimal production change to reach GREEN;
3. while GREEN, the implementer may make the necessary bounded behavior-preserving local refactor inside task-owned paths;
4. the test author verifies focused and surrounding tests after the final implementation shape;
5. the root records minimal-GREEN, any refactor, and final verification in existing evidence.

Retain the same test author and implementer through follow-up turns for a coherent task group; reuse its available/current owners for later corrections. If a session is unavailable, initialize its replacement from current raw artifacts, accepted scope, prior findings, and RED/GREEN evidence. Never store runtime handles in state. Keep production implementation at `gpt-6-sol`, medium; architecture decisions must already be accepted in the plan.

Minimal GREEN limits speculative behavior and scope, not code quality. Both authors make deliberate local design choices consistent with the accepted plan and applicable conventions. Before coherent-group completion, both assess their final affected methods/classes or test units in context and correct concrete local design problems. Record their brief dispositions and verify the final shape before the group checkpoint; do not add a whole-unit assessment/reporting ceremony to every slice. Assessment is required; refactoring is conditional, and a sound result may need none. Local refactoring is allowed only while GREEN inside owned paths, preserving behavior, public contracts, schema, migrations, dependencies, architecture, and authority. There is no arbitrary cleanup count or line limit. Routine private decomposition stays local; consequential discoveries outside the accepted design return to `plan` through `references/implementation-design.md`. Refactoring never substitutes for RED/GREEN evidence, and neither author may weaken assertions to fit a product defect.

Existing-correct behavior that only lacks a test is test-only work and must not fabricate RED. Nonbehavioral work uses bounded normal execution plus risk-appropriate direct verification under governed-tdd's routing rules; dependency/artifact/setup changes do not automatically require new tests or RED. Behavior-preserving refactors require characterization coverage when current tests do not protect the seam.

### 3. Converge

Verify current implementation prerequisites before dispatching `convergence-analyst` (Luna/high) for `speckit-converge`; known unfinished tasks return to their existing owner. Behavior changes supply accepted-task and governed-TDD RED/minimal-GREEN/final-GREEN evidence; test-only work supplies passing tests/coverage reason; nonbehavioral work supplies risk-appropriate direct verification; behavior-preserving refactors supply before/after regression or characterization checks. Apply governed-tdd's routing rules, including to dependency, artifact, packaging, and setup work. Never also invoke `speckit-implement`. The analyst matches gaps by obligation, behavior, and defect evidence against unfinished tasks, and may append only genuinely new in-scope Convergence tasks while preserving every prior byte/ID. It never edits intent artifacts or application code. Preserve raw upstream output separately from the governed disposition.

- `converged`: advance to root lookback.
- `tasks_appended` or `tasks_reused`: route every new/reused gap to its code/test owner; behavior uses TDD, other work direct verification. Mixed reuse/append returns `tasks_appended`. Description/dependency corrections go to the active definition author. Reuse is not clean convergence; a real regression after completed work is a distinct traceable correction.
- `return_to_definition`: route to `spec|plan|tasks` before speculative appends; authority contradictions use the existing user decision. A first design discovery can return to plan immediately.

Process the resulting evidence through the shared recovery guard; changing task IDs, inputs, phase, or author never resets an unresolved episode.

### 4. Look Back, Review, E2E, and Verify

After clean convergence, the root performs one bounded `lookback` using `references/story-lookback.md`: record a real `## Story Lookback` section, promote only verified reusable guidance, synchronize already-approved project-document deltas, and identify nonblocking follow-ups. `No new reusable lesson` is valid and need not create a lessons file. Validate the source/section/output proof before pinning the quality candidate. A newly discovered definition/authority issue returns to its owner; meaningful later corrections refresh affected lookback evidence, while successful commands and bookkeeping do not rerun it.

Dispatch one fresh `quality-reviewer` (`gpt-6-sol`, medium) over the converged pinned boundary. It judges correctness, security, edge cases, tests, concrete maintainability risks in the affected production/test units, architecture seams, regression risk, and repository standards. Distinguish evidenced risks and documented obligations from stylistic preference; do not inflate minor polish into a blocking severity. Convergence supplies implementation-to-definition coverage, so the reviewer does not repeat a separate acceptance ceremony.

`REVISE` returns raw findings to the applicable test/implementation or active definition owner without routine approval. Behavior correction requires regression RED; definition correction invalidates affected downstream evidence. Rerun invalidated checks, refresh lookback as needed, reuse the retained definition reviewer, and enforce the shared episode allowance. `BLOCKED` prevents advancement independently.

Apply the E2E decision matrix. Required E2E uses `e2e-runner` (`gpt-6-luna`, high), which may edit tests but never production. Product failure returns to TDD; a test defect stays with the runner; ambiguous failure stops for user direction. Any tracked test edit invalidates quality review.

Final verification runs the configured focused tests, build command, full test command, required risk checks, and confirms the reviewed boundary is unchanged. A coverage-based E2E skip must name current-boundary tests proving every triggered observable predicate.

## Recovery Guard

Read `references/review-and-correction.md` on any substantive failure. Initial negative result counts 1; failed repairs count 2 and 3; diagnose after two failed repairs at 3; pause before another repair at 4. Route immediately to the earliest invalid owner regardless of count. Diagnosis normally uses existing evidence in the root turn and does not automatically trigger replanning or Astra. A finite accepted extension retains the episode/count; an upgrade grants no new allowance.

Use the installed validator's narrow CLI with `--repo <root> --story-state <state>`; process a durable result with `--result <evidence>`, then validate the persisted state with `--next-stage <stage>` and, for an author change, `--next-profile <profile>`. Add `--json` for machine-readable output. The root persists proposed transitions and rechecks the boundary immediately before corrective dispatch, escalation, or resume advancement. Retain `transition_head` after closure; stale replay or erased recovery cannot silently restart the budget. A paused snapshot is valid, but dispatch through its pause is rejected. The helper validates mechanical evidence/order; semantic diagnosis and obedience remain agent responsibilities.

Expected RED, `BLOCKED`, invalidation, and intermediate ACCEPT neither increment nor clear recovery. A pre-production episode closes on complete-definition ACCEPT plus required plan conditions; once production begins, closure requires the final accepted/verified candidate. Clearly transient technical failures use a separate maximum of three root-issued invocations including the original, persisted in failure evidence referenced by `resume`. Do not repeat an unchanged negative check without correction or qualifying new evidence. Fatal/unavailable infrastructure and authority failures stop without futile retries.

## Checkpoints, Resume, and Completion

Create recovery checkpoints only when durable content exists: activation; accepted definition; verified implementation group; converged review candidate; correction; completion. Adjacent artifact-only work shares the accepted-definition checkpoint. Every commit uses an explicit path manifest and unique workflow trailers; completion is trusted only after the pending checkpoint ID resolves uniquely to an ancestral commit.

On resume, validate mapping, version/size, checkout identity, artifact hashes, current evidence/transition head, author ownership, lookback freshness, and checkpoint binding. Reuse the active author and retained reviewer, or initialize their replacements from raw artifacts, decisions, failed approaches, current boundary, and ordered verdict references. Never store runtime handles or accept a superseded author's result. Adopt active 3.0 once using bounded current evidence; erased old counts are not proof of no failures. Preserve known failure limits and diagnose uncertain allowances, requesting bounded continuation if still indeterminate. Do not rewrite completed 3.0 states. Restart at the earliest invalid owner without Git rollback.

## User Approval Policy

Human approval remains default. Planned-change authorization after independent plan review, implementation approval after independent definition review, and finite recovery extensions at failure 4 or a reached extension limit may use an explicitly user-designated delegate, each only when named in the user's scope/conditions and permitted by consumer policy. Recovery delegation additionally requires a user-set cumulative ceiling per episode and a fresh bound decision at each reached limit; existing two-gate grants do not include it. Apply `references/review-and-correction.md`'s optional delegated-approval contract. The runner owns every approval-related file write, records the real user designation and delegate reply separately, and binds the current accepted review or paused recovery boundary. No delegation to other gates, hold resolution, or execution permissions is implied.

Pause for direction (human unless explicitly covered above) only for: repository-mandated planned-change authorization; mandatory pre-implementation approval; contradiction with a prior user-approved decision or higher authority; a new architecture decision/change outside the accepted plan; the unresolved episode's fourth failed result or authorized finite extension limit; a hard blocker; or another concrete unresolved high-impact choice. Optional drafting questions are not approval requests. Preserve authority for unchanged semantic scope and bind corrected accepted hashes with an explanation; artifact byte changes alone do not require another approval.

Use `references/human-interaction.md` to make the actual outgoing message decision-ready: optional input during drafting, a consequential decision briefing at a real gate, or a root-led evidence-grounded recovery brief. Keep the pre-task gate's current order and request design. Root checks actual user provenance and continued scope applicability, records the decision once under `review-and-correction.md`, and never converts silence, a guide summary, or a preference into approval. No additional guide role, compulsory tutorial, or approval gate is introduced.

Complete a new-contract story only when definition/tasks and applicable verification evidence pass (RED/GREEN for changed behavior; truthful test-only, refactor, or direct verification otherwise), convergence is clean, current lookback is included in quality ACCEPT, E2E passes or is validly skipped, final verification closes recovery, and `review -> done` is bound to the completion checkpoint. Audit lookback from that commit, so future lesson changes do not invalidate history. Report story, feature, checkpoint, observed checks, lookback disposition, residual nonblocking findings, preserved work, and next queue item. Close an eligible epic only through `$close-governed-epic`.

## Dispatch Contract

Use direct-child project roles only; workers may not delegate. Every dispatch supplies the seven slots in `references/dispatch-contract.md`: role/objective, authoritative inputs, workflow boundary, constraints/skills, write boundary, output/evidence, completion/failure. Send judges raw pinned artifacts and correcting workers unchanged raw findings. A validated independent `ACCEPT`, clean convergence, passing E2E, or passing verification advances without duplicate user confirmation.
