---
name: run-bmad-sprint-loop
description: Use when implementing or resuming one or more BMad stories or epics under repository-governed delivery controls.
---

# Run BMad Sprint Loop

## Core Principle and Authorities

Treat one BMad story as one recoverable development unit: BMad controls lifecycle, SpecKit controls requirements-to-tasks, Superpowers controls implementation discipline, and the active root orchestrator controls evidence, gates, and Git. Violating a gate's letter violates the workflow.

Before execution, read `docs/agent-workflow/architecture.md`, `state-machine.md`, `artifact-contracts.md`, `agent-catalog.md`, `review-and-correction.md`, `e2e-decision-matrix.md`, and `operator-runbook.md`. Use Vulgata terms from `docs/UBIQUITOUS_LANGUAGE.md` verbatim and surface Constitution or ADR conflicts.

| Concern | Authority |
|---|---|
| Queue, story context, lifecycle | BMad sprint status, epic, and story artifacts |
| Requirements, design, work breakdown | SpecKit `spec.md`, plan set, and `tasks.md` |
| Cross-artifact consistency | Full: read-only `speckit-analyze`; Standard: eligibility plus root coverage validation |
| Repository invariants | Constitution, `AGENTS.md`, ADRs, context docs, Ubiquitous Language |
| Implementation | Superpowers red-green-refactor with fresh bounded implementers |
| Stage judgment | Fresh read-only judges/reviewers over pinned raw evidence |
| Local history | Active root orchestrator only |

One BMad story maps to exactly one SpecKit feature directory through both `workflow-state.json` and the BMad development record. Never use BMad context as a substitute for SpecKit artifacts.

## Accepted Inputs

Resolve exactly one target:

- `next`: first dependency-ready, non-`done` story in BMad sprint order;
- one story key;
- one epic key: all its stories in dependency order;
- a story/epic key list: flatten and de-duplicate while preserving dependency order;
- `resume`: the active mapped story, never a duplicate;
- `dry-run`: resolve and report all of the above without file, status, index, history, or feature-code mutation.

Reject ambiguous keys and new-work requests for `done` stories. Resume `in-progress` or `review` stories. The only story statuses are `backlog`, `ready-for-dev`, `in-progress`, `review`, and `done`.

Resolve the dependency graph before startup. Reorder prerequisites already inside the requested set. If an unmet prerequisite lies outside it, report the exact blocker and stop for user authorization; never silently expand scope. An explicit story request that is not dependency-ready remains selected but does not activate. Surface contradictory prerequisite authorities as an authority conflict.

## Execution Levels

Resolve one execution level independently for every selected story. Full is the default when the user omits a level. Full may always be requested. `Standard` must be explicitly requested, assessed before activation, persisted as `execution_level: "standard"` in `workflow-state.json`, and recorded in the BMad development record. An epic-level Standard request applies the eligibility test separately to every story.

### Standard Eligibility

Standard is allowed only when all of these are evidenced:

- scope and acceptance criteria are bounded under the current PRD, Constitution, ADRs, and accepted architecture, with no unresolved intent or authority conflict;
- behavior is deterministic, local, additive or safely reversible, and can be covered by current-boundary automated tests;
- the story does not add or materially change a Worker Agent tool, agent execution adapter, agent result contract that mutates knowledge, orchestration, dispatch, scheduling, retry, provider failover, external MCP/tool access, untrusted-file processing, authorization/security boundary, graph-truth or Cross-Repository Resolution algorithm, concurrent lifecycle/replay behavior, or destructive/irreversible operation;
- any migration is additive and its invariants can be proven by migration plus persistence integration tests;
- any authorization behavior only reuses an already accepted policy and receives adversarial integration coverage.

A deterministic extension of an already accepted Scan strategy may use Standard only when it introduces no new scheduling, grouping, retry, dispatch, or external-tool behavior. It still requires independent `plan-review` and the repository's planned-change authorization after plan acceptance and before task generation.

Standard never permits bypassing a planned-change authorization, Constitution/ADR conflict, required E2E predicate, observed RED, independent final review, security verification, explicit-path staging, checkpoint binding, or epic-close architecture gate. If eligibility is uncertain or becomes false, stop before further mutation, report the exact Full trigger, and request `Standard -> Full` authorization. Never silently upgrade, downgrade, or keep Standard to meet a deadline. A resumed story keeps its recorded level; changing it follows the same explicit stop and earliest-invalid-stage analysis.

| Level | Selection | Planning gates | Consistency gate | Final review |
|---|---|---|---|---|
| Full | Default or explicit | Fresh spec and plan judges | `speckit-analyze` | Separate acceptance and quality reviewers |
| Standard | Explicit and eligible per story | Conditional plan judge; planned-change authorization remains mandatory | Root acceptance/task/test coverage validation | One fresh combined acceptance-and-quality reviewer |

### Level Flows

Full follows every gate in **Full Story Flow** below.

Standard follows this exact reduced flow:

```text
context
-> speckit-specify
-> speckit-plan
-> conditional plan-review + planned-change authorization
-> speckit-tasks
-> speckit-implement
-> quality-review (combined acceptance-and-quality)
-> conditional-e2e -> verify -> complete
```

Standard still creates one mapped SpecKit feature with concise `spec.md`, `plan.md`, and `tasks.md`. The root validates acceptance coverage, test-first task ordering, repository invariants, and unresolved placeholders before implementation. Standard omits `spec-review`, `speckit-analyze`, and the separate `acceptance-review`; set `active_gates.acceptance-review.status` to `invalidated`, iteration zero, with no verdict. Use a fresh `quality-reviewer` over the pinned boundary for the Standard-level combined acceptance-and-quality review. Its raw inputs include the BMad story, concise SpecKit artifacts, repository authorities, implementation/tests, and evidence.

Run `plan-review` in Standard when the plan triggers the repository's planned-change authorization or the root finds a material architecture/testability uncertainty. A clean plan judge `ACCEPT` does not replace the mandatory user approval. For other eligible Standard stories, the root's mechanical plan/task validation is sufficient.

## Preflight and Ownership

Before any mutation:

1. Load the complete sprint state, story/epic sources, Constitution, relevant ADR/context documents, and repository instructions.
2. Record branch, full `HEAD`, repository root, Git common directory, worktree path, exact porcelain status, owned paths, unrelated paths, and observation time.
3. Require a non-detached branch unless the host supplies an explicit native handoff. Preserve all unrelated user edits.
4. Define exact story ownership before dispatch. Stop for user direction if owned and unrelated changes are inseparable.
5. Validate `.codex/config.toml`, required agent TOMLs, state/verdict schemas, and required infrastructure.

Never switch branches, reset, rebase, force-push, merge, push, or open a pull request implicitly. Dry-run stops after reporting resolved inputs, proposed mapping, stages, agents, human gates, checkpoints, E2E inputs, and resume key.

## Story Queue Rules

Prepare `backlog` stories with the applicable BMad create-story workflow, validate their context, checkpoint `backlog -> ready-for-dev`, then queue them. Execute each story as an isolated SpecKit feature. Do not guess or preallocate the SpecKit number: persist the exact directory returned by `speckit-specify`, fail on collision, and bind that actual path in both mapping records. Independent reads or disjoint preparation may overlap; serialize shared-file edits, lifecycle/state writes, index operations, and commits.

After successful context/preflight, the root writes the canonical story activation record and checkpoints `ready-for-dev -> in-progress` before specification; lift a `backlog` parent epic to `in-progress` in the same commit. Never combine unrelated stories in a checkpoint. When requested epic queues finish, hand each eligible epic to `$close-bmad-epic`; this skill never bypasses its architecture gate.

## Full Story Flow

For `execution_level: "full"`, follow this order without omission or relocation:

```text
context
-> speckit-specify -> spec-review
-> speckit-plan -> plan-review
-> speckit-tasks
-> speckit-analyze
-> speckit-implement
-> acceptance-review + quality-review
-> conditional-e2e -> verify -> complete
```

Dispatch `story-context-synthesizer`, `spec-author`, fresh `spec-judge`, `plan-architect`, fresh `plan-judge`, `task-author`, `artifact-analyst`, and fresh bounded implementers in that sequence. Judges add gates around SpecKit; they never replace `speckit-analyze`.

Validate every returned artifact, source boundary, and schema. A validated independent `ACCEPT` from `spec-judge`, `plan-judge`, `acceptance-reviewer`, or `quality-reviewer` is approval for that stage: persist it, checkpoint when required, and advance without redundant user confirmation. Likewise advance on clean analysis, passing E2E, and passing verification.

For an in-scope Full specification or plan `REVISE`, pass raw findings to a fresh responsible agent and rerun that gate, up to three outcomes. Analysis `REVISE` follows only the explicit approval flow below; never remediate it through this generic loop. Any Full implementation-review correction changes the candidate boundary: invalidate both review verdicts and rerun both axes, regardless of finding severity. In Standard, a `quality-review` correction invalidates that combined verdict and reruns it after fresh TDD correction. Stop immediately on `BLOCKED`; it consumes no revision attempt. Pause for unresolved intent/scope or the third `REVISE`.

Keep explicit human gates only for blocking analysis remediation; unresolved intent/scope or three `REVISE` outcomes; Strong architecture recommendations or ADR conflicts; divergent history or destructive VCS recovery; inseparable ownership; unavailable required infrastructure; unsafe external actions; and ambiguous E2E failures.

## Analyze Remediation Approval

For Full, run read-only `speckit-analyze` immediately after the current `tasks.md` and before any implementation. Persist its untouched report, metrics, and artifact hashes. Standard does not run this stage; its eligibility gate and root plan/task coverage validation must succeed instead.

- No Critical/High finding: advance without duplicate approval.
- Critical/High finding: present the raw findings and artifact-specific remediation; obtain explicit user approval before editing `spec.md`, any plan artifact, or `tasks.md`.
- Approval withheld or analysis `BLOCKED`: record the stop and do not implement.
- Approval granted: use a fresh responsible author, regenerate every dependent downstream artifact, and rerun analysis. A spec change regenerates plan and tasks; a plan change regenerates tasks.

Analysis approval is limited to the presented remediation. It cannot authorize product-scope expansion, ADR changes, code implementation, or unrelated cleanup.

## Planned-Change Authorization

For either level, after an independent `plan-review` accepts and before task generation, pause when `AGENTS.md` identifies a planned-change trigger. The request must name the trigger, explain the proposed approach and effects, state the root's implementation intention, and cite the affected plan and PRD material. Approval authorizes only progression to task generation; it never waives later TDD, review, E2E, security, verification, or Git gates. Standard eligibility cannot override this authorization.

## Direct-Child Dispatch

Use only these project agents: `bmad-sprint-orchestrator`, `story-context-synthesizer`, `spec-author`, `spec-judge`, `plan-architect`, `plan-judge`, `task-author`, `artifact-analyst`, `tdd-implementer`, `acceptance-reviewer`, `quality-reviewer`, `e2e-runner`, and `epic-architecture-auditor`. The active root assumes the orchestrator role; every worker is its direct child and may not delegate.

Use the catalog's `gpt-5.6-sol` profiles for orchestration, authoring, planning, judgment, implementation, review, and architecture audit. Use `gpt-5.6-terra` only for economical context synthesis and E2E at the cataloged effort. Model substitution must preserve the catalog's capability, effort, and sandbox boundaries.

Every dispatch contains exactly the seven slots defined in `references/dispatch-contract.md`:

1. `role_and_objective`
2. `authoritative_inputs`
3. `workflow_boundary`
4. `constraints_and_skills`
5. `write_boundary`
6. `output_and_evidence`
7. `completion_and_failure`

Send judges raw pinned artifacts, not author conclusions. Send a correcting agent the unchanged raw finding. Workers never stage or commit.

## TDD Task-Group Loop

Partition `tasks.md` into dependency-ready groups with coherent behavior and explicit, disjoint ownership. For each group, dispatch a fresh `tdd-implementer` with exact task IDs, accepted artifacts, boundary, owned paths, and commands.

Require, in order:

1. a new test and observed failure for the expected missing behavior;
2. minimal production change and targeted green evidence;
3. surrounding tests passing;
4. refactoring only while green;
5. changed paths, risks, evidence IDs, and task markers returned;
6. root validation and a focused checkpoint before the next shared mutation.

No production code before observed RED. Tests added later, manual testing, inherited prototypes, or fabricated history do not satisfy this gate. For inherited code, create RED for each behavior being changed or completed; do not claim TDD for unchanged code.

After all groups pass, the root checkpoints `in-progress -> review` and pins one candidate boundary before reviewer dispatch.

## Parallel Review and Correction

For Full, dispatch fresh `acceptance-reviewer` and `quality-reviewer` in parallel against the identical pinned boundary. Keep both verdicts independent; passing tests replace neither axis.

- Both `ACCEPT`: persist in deterministic acceptance-then-quality order and advance without asking the user again.
- Any `REVISE`: persist raw verdicts, return raw findings to a fresh `tdd-implementer`, require regression RED for behavior changes, verify green, pin the changed boundary, and rerun both axes. Only after both fresh verdicts `ACCEPT` may the root create the correction checkpoint.
- Intent/scope decision, third `REVISE`, or `BLOCKED`: pause under the reserved human gate.

The BMad story remains `review` during correction. Never defer, waive, or “risk accept” a Critical/High finding to reach `done`.

For Standard, dispatch one fresh `quality-reviewer` with the Standard-level combined acceptance-and-quality contract. It checks every BMad acceptance criterion and SpecKit/task outcome plus correctness, security, edge cases, tests, architecture seams, and regression risk. `REVISE` returns raw findings to a fresh `tdd-implementer`; changed behavior requires regression RED, then the combined review reruns on the new boundary. The third `REVISE` or any `BLOCKED` stops for user direction.

## Conditional E2E

Apply `docs/agent-workflow/e2e-decision-matrix.md`. E2E is required for user-visible workflows, authorization/authentication, API/SignalR/persistence/multi-module integration, migrations, observable data lifecycle, or behavior lacking complete current-boundary integration coverage. Skip only documentation/behavior-preserving changes or when cited passing integration tests prove every triggered observable boundary.

When required, dispatch low-effort `gpt-5.6-terra` `e2e-runner`. It may edit tests, never production. Route product failures to a fresh TDD implementer; let the runner correct test defects; pause on ambiguous failure. Any tracked test edit changes the candidate boundary. Full invalidates and reruns both review axes; Standard invalidates and reruns its combined `quality-review`.

## VCS Checkpoints and Sole Committer

Only the active root may stage explicit owned paths and create local commits. Before every commit, refresh ownership/preflight, inspect the staged diff, run `git diff --cached --check`, and run checkpoint-specific verification. Never use blanket staging. Never commit rejected work, failing code/tests, secrets, temporary reports, or unrelated edits.

| Checkpoint | Required durable content |
|---|---|
| Story prepared | Validated story and `backlog -> ready-for-dev` |
| Story activated | Activation record, development record, `ready-for-dev -> in-progress`, optional epic activation |
| Specification accepted | `spec.md`, checklist, mapping/state, accepted verdict |
| Plan accepted | Complete plan set, Constitution evidence, accepted verdict |
| Tasks analyzed | `tasks.md`, clean analysis evidence, state |
| Task group verified | One cohesive implementation/test increment and RED/GREEN evidence |
| Ready for review | Candidate identity and `in-progress -> review` |
| Review correction | Regression test, correction, fresh accepted reviews |
| Story complete | Reviews, E2E decision/result, verification, `review -> done` |

Adjacent artifact-only checkpoints may combine only when no useful recovery boundary is lost. Unaccepted artifacts cannot share an implementation commit. Each checkpoint gets a unique ID in durable state and trailers: `BMad-Story`, `Workflow-Scope`, `Workflow-Stage`, and `Workflow-Checkpoint`. Resolve its immutable SHA from Git; never amend a commit to embed its own SHA.

For Standard, combine specification, plan, any required accepted plan verdict/approval, tasks, mapping, and state into one pre-implementation checkpoint. Keep story prepared, story activated, implementation recovery boundaries, ready-for-review, correction when needed, and story complete as distinct checkpoints. Do not create separate specification/plan/tasks artifact-only commits.

## Resume and Invalidation

On resume, validate BMad state, canonical activation, unique two-way mapping, branch/Git/worktree identity, ownership, artifact hashes, exact review boundary, checkpoint trailer uniqueness, and ancestor relationship to `HEAD`. Permit the documented legacy fallback only for pre-workflow `in-progress`/`review` stories with an explicit, valid, unique mapping.

Unchanged completed stages are no-ops. Restart at the earliest invalid owner: source/context change at `context`; spec at `specify`; plan at `plan`; tasks at `tasks`; Full analysis at `analyze`; implementation/test at `implement`; tracked E2E test at the applicable Full reviews or Standard combined review; verification boundary at `verify`. Preserve prior durable records as invalidated/superseded history and clear only dependent later claims. Hash/boundary invalidation is not a judge `REVISE` and does not increment the three-revision counter.

Record recoverable agent/tool failures without advancing and retry the same stage with a fresh child when safe. Stop for divergent history, destructive recovery, inseparable ownership, authority conflict, unavailable infrastructure, unsafe external action, or reserved human decisions. Report the canonical checkpoint ID/SHA, active `HEAD`, and identities; resume only after separately authorized user-chosen recovery and a full new preflight. If divergence coexists with a dirty/inseparable boundary, preserve every byte and stop without writing there. Never repair resume state with reset, rebase, force-push, silent branch switching, implicit merge/cherry-pick, or guessed mappings.

## Completion Criteria

Before assembling Full completion, require: every task/checklist complete; accepted spec and plan; latest analysis without Critical/High findings; observed task-group RED/GREEN evidence; both reviews accepted on the current boundary; required E2E passed or a complete current-boundary coverage override; story-specific tests, web build, and full tests passing; unchanged review boundary; and durable evidence.

Before assembling Standard completion, require: eligibility still true; every concise spec/plan/task acceptance item complete; any required plan review and planned-change authorization durable; observed task-group RED/GREEN evidence; the combined `quality-review` accepted on the current boundary; required E2E passed or has a complete current-boundary coverage override; story-specific tests, web build, and full tests passing; unchanged review boundary; and durable evidence.

For either level, stage explicit completion paths containing provisional `stage: complete`, `review -> done`, and a pending final checkpoint ID; commit with its trailer; resolve that ID to the new SHA; and trust/report completion only after unique trailer binding and ancestry validation succeed.

Report the completed story, feature directory, local checkpoint SHAs, tests/build, E2E decision, review verdicts, residual nonblocking findings, and next queued story. Do not claim epic completion; use `$close-bmad-epic` after all epic stories qualify.

## Quick Reference, Rationalizations, Red Flags, and Mistakes

| Pressure or excuse | Required response |
|---|---|
| “BMad context/prototype is enough.” | Cite it as input; still create one feature and run the full SpecKit cycle. |
| “Call it Standard to skip a risky gate.” | Standard is eligibility-based; an ineligible story stops and requires explicit `Standard -> Full` authorization. |
| “Skip fresh judges for the deadline.” | Full keeps every judge; Standard keeps conditional plan judgment and its independent combined final review. |
| “Judges replace analyze.” | Full runs read-only analysis after tasks; Standard omits it only under its recorded eligibility and coverage contract. |
| “Tests can be added later/where practical.” | Observe RED before each changed behavior's production code. |
| “Meaningful units/one big commit are simpler.” | Use gate checkpoints; isolate stories and unrelated edits. |
| “Include the whole worktree.” | Stage explicit owned paths only; stop if inseparable. |
| “Passing tests make review optional.” | Full requires both axes; Standard requires its combined review; both require the E2E decision and verification. |
| “A High finding can be accepted/deferred.” | Correct it with fresh TDD work and rerun the applicable Full axes or Standard combined review. |
| “Use `ready-for-review`.” | Use only BMad's validated status vocabulary; the status is `review`. |
| “Ask the user after every AI review.” | A validated independent `ACCEPT` is the stage approval; do not duplicate it. |
| “Passing tests are enough to close the epic.” | Verification and the current architecture audit are separate mandatory gates. |
| “Executive acceptance replaces the audit/architecture can wait.” | Never close the epic without a current audit and approved Strong/ADR dispositions. |

Red flags: unrecorded or changed execution level; claiming Standard without eligibility evidence; bypassing planned-change authorization; skipping or reordering a level-required stage; Full implementation before analysis; editing after a pinned review without invalidation; workers staging/committing; `git add -A`; mixed-story commits; invented status; marking `done` with unresolved Critical/High; claiming completion from tests alone; silently repairing Git history; or asking for duplicate approval after validated `ACCEPT`. Stop and restore the contract.

Common mistakes are trusting summaries instead of raw artifacts, dispatching an intermediate orchestrator, combining review axes, letting E2E edit production, treating test-only edits as review-neutral, committing before evidence is durable, and forgetting dependent regeneration after analysis remediation.
