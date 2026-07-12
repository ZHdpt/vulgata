# BMad–SpecKit–Superpowers Sprint Loop Design

**Date:** 2026-07-10

**Status:** Approved for implementation

**Scope:** Repository-local Codex workflow for implementing BMad stories across one or more epics

## Purpose

Create an orchestrator-led development loop that combines:

- BMad for sprint selection, story context, lifecycle tracking, and epic completion.
- SpecKit for story-level specification, technical planning, task decomposition, and cross-artifact analysis.
- Superpowers for fresh-context delegation, test-driven development, independent review, correction loops, and evidence-based completion.
- `improve-codebase-architecture` for an architecture-quality gate before an epic is completed.

The workflow must be directly usable in Codex against Vulgata's existing BMad planning and implementation artifacts. It must also leave reusable design contracts that another coding agent can use to reproduce, customize, or package the workflow.

## Goals

1. Let a user start one BMad story, one epic, or several epics through a single orchestrator.
2. Treat one BMad story as the atomic implementation unit.
3. Map every story to one SpecKit feature directory and preserve traceability between their artifacts.
4. Use independent, fresh-context agents to judge specifications, plans, and code changes.
5. Use a high-reasoning model for technical planning and judgment.
6. Enforce test-first implementation and return Critical or High review findings to development.
7. Run economical E2E automation only when observable risk warrants it.
8. Prevent an epic from becoming `done` until its architecture gate has been resolved.
9. Run `speckit-analyze` at its original SpecKit position: after task generation and before implementation.
10. Commit accepted artifacts and verified code increments to version control at explicit workflow checkpoints.
11. Preserve existing BMad statuses, SpecKit templates, the Vulgata Constitution, ADRs, and Ubiquitous Language.

## Non-goals

- Forking or rewriting installed BMad, SpecKit, Superpowers, or Matt Pocock skills.
- Adding new values to BMad's validated sprint-status vocabulary.
- Making multiple BMad stories share one SpecKit feature directory.
- Allowing judge agents to edit the artifacts they review.
- Automatically accepting architecture changes that require product or maintainer judgment.
- Building a marketplace plugin in the first implementation. The repository-local artifacts should be extractable into a plugin later.

## Ownership Model

| Concern | Authority |
|---|---|
| Story and epic queue | BMad `sprint-status.yaml` and `epics.md` |
| Story implementation context | BMad story file plus source planning artifacts |
| Story requirements | SpecKit `spec.md` |
| Technical design | SpecKit `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md` |
| Executable work breakdown | SpecKit `tasks.md` |
| Project invariants | `.specify/memory/constitution.md`, `AGENTS.md`, ADRs, context docs, and `docs/UBIQUITOUS_LANGUAGE.md` |
| Test-first implementation | Superpowers TDD and subagent-driven concepts |
| Acceptance and quality judgment | Fresh read-only reviewer agents |
| Epic architecture quality | `improve-codebase-architecture` plus explicit user disposition |
| Fine-grained workflow progress | Story-local `workflow-state.json` |

When authorities conflict, the orchestrator stops and reports the conflict. It never silently weakens the Constitution, changes an ADR, or substitutes a synonym for a canonical Vulgata term.

## Atomic Unit and Queueing

One BMad story maps to exactly one SpecKit feature directory:

```text
BMad story key: 4-1-codegraph-integration-and-code-unit-extraction
SpecKit feature: specs/NNN-codegraph-code-unit-extraction/
```

The feature directory records the BMad story key in `workflow-state.json`. The BMad story file records the SpecKit directory in its development record. This two-way link is the stable identity bridge.

A request for an epic or multiple epics creates an ordered queue of story keys. Selection follows BMad sprint order and dependency information. Independent stories may be prepared concurrently, but mutations to shared files and lifecycle state remain serialized. A story never shares an implementation or review context with another story.

## Runtime Components

### Orchestration skills

The repository will add two thin skills:

1. `run-bmad-sprint-loop`
   - Accepts a story key, epic key, list of keys, or `next`.
   - Validates prerequisites and builds the story queue.
   - Dispatches named custom agents.
   - Enforces gates, correction loops, state persistence, and BMad status transitions.
   - Supports safe resume after interruption.

2. `close-bmad-epic`
   - Confirms all epic stories are `done`.
   - Runs full verification.
   - Dispatches the architecture audit.
   - Records user disposition of architecture findings.
   - Allows retrospective and epic completion only after the gate passes.

The skills reference installed skills by name rather than copying their bodies.

### Custom Codex agents

Project-scoped agents live under `.codex/agents/`. Codex currently supports project-scoped agent TOML files with required `name`, `description`, and `developer_instructions`, plus per-agent `model`, `model_reasoning_effort`, and `sandbox_mode` settings. See the [Codex manual](https://developers.openai.com/codex/codex-manual.md).

| Agent | Model and effort | Access | Responsibility |
|---|---|---|---|
| `bmad-sprint-orchestrator` | `gpt-5.6-sol`, high | workspace write | Own the queue, dispatch, gates, state, and lifecycle; do not implement feature code |
| `story-context-synthesizer` | `gpt-5.6-terra`, medium | read-only | Produce a bounded source-grounded story brief |
| `spec-author` | `gpt-5.6-sol`, medium | workspace write | Run `speckit-specify` from the story brief |
| `spec-judge` | `gpt-5.6-sol`, high | read-only | Judge scope, completeness, testability, and BMad alignment |
| `plan-architect` | `gpt-5.6-sol`, high | workspace write | Run `speckit-plan` and make detailed technical decisions |
| `plan-judge` | `gpt-5.6-sol`, high | read-only | Challenge feasibility, architecture, Constitution compliance, and testability |
| `task-author` | `gpt-5.6-sol`, medium | workspace write | Run `speckit-tasks` with explicit test-first ordering |
| `artifact-analyst` | `gpt-5.6-sol`, high | read-only | Run `speckit-analyze` after tasks and report cross-artifact consistency findings |
| `tdd-implementer` | `gpt-5.6-sol`, high | workspace write | Implement a bounded task group with red-green-refactor evidence |
| `acceptance-reviewer` | `gpt-5.6-sol`, high | read-only | Review the diff against BMad and SpecKit requirements |
| `quality-reviewer` | `gpt-5.6-sol`, high | read-only | Review correctness, security, edge cases, architecture, and documented standards |
| `e2e-runner` | `gpt-5.6-terra`, low | workspace write | Add and run E2E/API tests; never repair production code |
| `epic-architecture-auditor` | `gpt-5.6-sol`, high | read-only in repo | Run `improve-codebase-architecture` and emit its temporary report |

Agents are direct children of the orchestrator. The initial design keeps `agents.max_depth = 1` to avoid recursive fan-out. Task-level subagent-driven development is realized by the orchestrator dispatching a fresh implementer for each bounded task group and fresh reviewers afterward.

The selected `bmad-sprint-orchestrator` is the active/root agent for the run; it loads `run-bmad-sprint-loop` and dispatches the other roles. If a normal Codex root invokes the skill directly, that root assumes the orchestrator role instead of spawning an intermediate orchestrator. This keeps worker agents at depth 1.

## Story Lifecycle

### BMad lifecycle

The workflow preserves BMad's existing status vocabulary:

```text
backlog
  -> create BMad story context
ready-for-dev
  -> start SpecKit cycle
in-progress
  -> implementation and automated verification complete
review
  -> independent reviews and conditional E2E gate accepted
done
```

The orchestrator may only make transitions permitted by the BMad workflow. It updates `last_updated` while preserving comments and ordering in `sprint-status.yaml`.

### Internal lifecycle

Detailed progress belongs in `specs/<feature>/workflow-state.json`:

```text
context
  -> specify
  -> spec-review
  -> plan
  -> plan-review
  -> tasks
  -> analyze
  -> implement
  -> acceptance-review + quality-review
  -> conditional-e2e
  -> verify
  -> complete
```

The state document is a checkpoint, not an alternative source of sprint truth. It contains:

- schema version;
- BMad story and epic keys;
- SpecKit feature directory;
- current stage and stage history;
- source artifact paths and source commit;
- dispatched agent role and returned evidence for each gate;
- review iteration counts and verdict paths;
- E2E decision and rationale;
- verification commands and captured outcomes;
- last successful checkpoint and recoverable failure information.

State writes are atomic. On resume, the orchestrator validates referenced artifacts before trusting the checkpoint and restarts the earliest invalid or incomplete stage.

## End-to-End Story Flow

### 1. Select and prepare

1. Load BMad configuration and the complete sprint status.
2. Resolve the requested story queue and dependency order.
3. If a story is `backlog`, run BMad create-story to produce its context and reach `ready-for-dev`.
4. Reject stories already `done`; resume rather than duplicate stories already `in-progress` or `review`.
5. Locate an existing mapped SpecKit feature when resuming. For a new story, keep the proposed feature short name in the dispatch context; `speckit-specify` remains the sole creator of the directory.

### 2. Synthesize context

Dispatch `story-context-synthesizer` with paths, not copied conclusions. It reads only relevant sections of:

- the BMad story and its epic;
- PRD, architecture, UX, research, and implementation-readiness artifacts;
- previous story learnings and relevant recent commits;
- current code files expected to change;
- `.specify/memory/constitution.md`, `AGENTS.md`, ADRs, context docs, and `docs/UBIQUITOUS_LANGUAGE.md`.

It emits a story brief containing source citations, constraints, acceptance criteria, known risks, open conflicts, and the desired observable outcome. It must separate sourced facts from inferences.

### 3. Specify and judge

1. Dispatch `spec-author` with the story brief and run `speckit-specify`.
2. Read the feature directory returned by SpecKit, initialize `workflow-state.json`, and add the two-way mapping to the BMad story's development record.
3. Dispatch a fresh `spec-judge` with `spec.md`, its quality checklist, the BMad story, and authoritative constraints.
4. The judge writes no files and returns the verdict contract.
5. `REVISE` returns findings to a fresh `spec-author` pass.
6. `ACCEPT` advances to planning.

The specification remains technology-agnostic and focused on what and why. Technical details from the BMad story are preserved as planning inputs rather than leaked into functional requirements.

### 4. Plan and judge

1. Dispatch `plan-architect` with the accepted specification and authoritative technical context.
2. Run `speckit-plan`, including research, contracts, data model, quickstart, and Constitution checks where applicable.
3. Dispatch a fresh `plan-judge` with the complete planning artifact set.
4. Return Critical or High findings to a new planning pass.
5. Advance only after an accepted plan has no unresolved clarification or Constitution gate failure.

### 5. Generate tasks

1. Dispatch `task-author` to run `speckit-tasks`.
2. Require test tasks before their corresponding implementation tasks.
3. Require exact paths, dependencies, story labels, independent validation criteria, and bounded task groups.

### 6. Analyze the SpecKit artifacts

This stage preserves the original SpecKit ordering:

```text
speckit-specify -> speckit-plan -> speckit-tasks -> speckit-analyze -> speckit-implement
```

The additional independent spec and plan judges are gates around that flow; they do not replace or move `speckit-analyze`.

1. Immediately after `speckit-tasks`, dispatch the read-only `artifact-analyst` to run `speckit-analyze` across `spec.md`, `plan.md`, and `tasks.md`.
2. Persist its structured report and coverage metrics without changing the analyzed artifacts.
3. If no Critical or High findings exist, record the accepted analysis gate and advance to implementation.
4. If blocking findings exist, present the report and proposed remediation to the user as required by the original read-only SpecKit analysis contract.
5. After explicit approval, route each finding to the author responsible for `spec.md`, `plan.md`, or `tasks.md`.
6. Regenerate dependent downstream artifacts when an upstream artifact changes, then rerun `speckit-analyze` at this same stage.
7. Do not dispatch an implementer until the latest analysis report has no Critical or High findings.

### 7. Implement with TDD

The orchestrator groups tasks only when they share a coherent behavior and file ownership. For each group:

1. Dispatch a fresh `tdd-implementer` with the task IDs, relevant artifact paths, current diff boundary, and verification commands.
2. Require red evidence: the new test fails for the expected missing behavior.
3. Require green evidence: minimal implementation makes the targeted and surrounding tests pass.
4. Permit refactoring only while tests remain green.
5. Mark SpecKit tasks complete only after evidence is returned and independently reviewable.

Parallel implementation is allowed only for task groups with disjoint file ownership and no incomplete dependency. The orchestrator serializes merges and shared-file edits.

### 8. Review and correct

After implementation, dispatch `acceptance-reviewer` and `quality-reviewer` in parallel against the same pinned diff and source commit.

- Acceptance review checks missing, partial, incorrect, or out-of-scope behavior against BMad and SpecKit.
- Quality review checks documented standards, correctness, security, edge cases, test quality, architecture seams, and regression risk.

The orchestrator does not merge the axes. Any blocking finding is routed back to a fresh `tdd-implementer` with the raw finding and evidence. The correction must add or adjust a failing regression test before production code when behavior changes. Both affected review axes rerun after the fix.

### 9. Decide and run E2E automation

The E2E gate is required when the story changes any of:

- a user-visible workflow;
- authentication or authorization behavior;
- API, SignalR, persistence, or multi-module integration;
- an EF migration or observable data lifecycle;
- behavior not adequately established by existing integration tests.

It may be skipped for documentation-only work, internal refactoring with unchanged behavior, or changes already exercised end-to-end by an existing test. A skip must record the exact covering tests and rationale.

When required, dispatch `e2e-runner`. It may add or update test code and run the existing framework. It must not change production code. A product failure returns to `tdd-implementer`; a defective test may be corrected by `e2e-runner`. The E2E gate reruns after either correction.

### 10. Verify and complete

1. Run story-specific tests.
2. Run `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`.
3. Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj`.
4. Confirm every SpecKit task is complete and every required checklist passes.
5. Record command, exit code, and concise output evidence in workflow state and the BMad story's development record.
6. Move the story to `review`, perform final fresh review if the review boundary changed, then mark it `done`.
7. Continue with the next queued story.

## Verdict Contract

Every judge and reviewer returns one JSON-compatible object:

```json
{
  "schema_version": "1.0",
  "gate": "spec-review",
  "verdict": "ACCEPT",
  "reviewed_commit": "<git-sha>",
  "reviewed_artifacts": ["specs/001-example/spec.md"],
  "findings": [],
  "summary": "All blocking criteria satisfied."
}
```

Each finding contains:

- stable ID;
- severity: `CRITICAL`, `HIGH`, `MEDIUM`, or `LOW`;
- category;
- artifact and location;
- requirement or rule affected;
- concise evidence;
- concrete correction outcome.

`ACCEPT` means no Critical or High findings and no violated Constitution MUST. `REVISE` means the responsible author or implementer can correct the findings. `BLOCKED` means external authority, missing information, or an irreconcilable source conflict prevents safe correction.

After three unsuccessful revisions at the same gate, the orchestrator stops and asks the user. It never downgrades severity merely to escape the loop.

## Handoff Contract

Each dispatch prompt contains exactly:

1. Role and bounded objective.
2. Paths to authoritative inputs.
3. Current stage, story key, feature directory, source commit, and diff boundary.
4. Constraints and required skill names.
5. Permitted writes and prohibited adjacent work.
6. Expected output schema and evidence.
7. Done conditions and failure behavior.

It does not preload another agent's conclusions unless the receiving agent is explicitly correcting a finding. Judges receive raw artifacts, not the author's reasoning transcript.

## Epic Completion Gate

`close-bmad-epic` may proceed only when:

1. Every story belonging to the epic is `done`.
2. No unresolved blocking workflow state exists for those stories.
3. Full build and test verification passes at the current commit.
4. `epic-architecture-auditor` runs `improve-codebase-architecture` using Vulgata context and ADRs.
5. Every Strong recommendation is resolved, rejected with a load-bearing reason, deferred through an explicit tracked decision, or accepted by the user.
6. Any ADR conflict is surfaced explicitly.

The architecture skill produces its report in the OS temporary directory as designed; the workflow state records its path, source commit, and disposition summary. Architecture findings do not authorize unrelated refactoring. After the gate is resolved, the orchestrator offers the BMad retrospective and only then marks the epic `done`.

## Version-Control Checkpoints

The orchestrator is the sole version-control committer. Worker, author, judge, reviewer, and E2E agents return changes and evidence but never run concurrent commits. This keeps stage boundaries reproducible and prevents parallel agents from racing over the index or branch.

### Preflight

Before the first mutation, the orchestrator:

1. records the current branch, `HEAD`, and worktree status;
2. requires a non-detached branch unless the Codex host provides an explicit native branch handoff;
3. identifies pre-existing user changes and excludes them from staging and story ownership;
4. stops if active-story changes cannot be separated safely from unrelated changes;
5. never switches branches, pushes, rebases, merges, or opens a pull request unless the user separately requests it.

### Required commits

| Checkpoint | Preconditions | Commit contents |
|---|---|---|
| BMad story prepared | Story context validated and status is `ready-for-dev` | BMad story file and sprint-status transition |
| Specification accepted | Spec judge returns `ACCEPT` | `spec.md`, requirements checklist, workflow mapping, and gate evidence |
| Plan accepted | Plan judge returns `ACCEPT` and Constitution checks pass | `plan.md` and generated research/design/contract artifacts |
| Tasks analyzed | `speckit-analyze` has no Critical or High findings | `tasks.md`, persisted analysis evidence, and workflow checkpoint |
| Task group verified | Red-green-refactor evidence exists and targeted tests pass | One cohesive implementation increment and its tests |
| Review correction verified | The relevant fresh review passes after a blocking finding | Focused regression test, correction, and updated review evidence |
| Story completed | Full build/tests and required E2E gate pass | Final story record, completed task/state metadata, and BMad status transition |
| Epic gate completed | Architecture findings are dispositioned and epic verification passes | Architecture disposition, retrospective/status artifacts, and epic completion transition |

The orchestrator may combine adjacent artifact-only checkpoints when they produce no useful independent recovery point, but it must not combine unaccepted artifacts with implementation code or combine unrelated stories in one commit.

### Commit rules

- Stage explicit owned paths; never use a blanket add that could capture unrelated changes.
- Inspect the staged diff and run the checkpoint's verification before committing.
- Do not commit a rejected gate, failing implementation, failing tests, secrets, credentials, or the temporary HTML architecture report.
- Include the BMad story or epic key and stage in each commit message, for example `feat(4-1): implement Code Unit extraction` or `docs(4-1): accept SpecKit plan`.
- Assign a unique checkpoint ID before committing and include it in `workflow-state.json` plus commit trailers (`BMad-Story`, `Workflow-Stage`, and `Workflow-Checkpoint`). On resume, resolve the immutable commit SHA from that checkpoint ID in Git history; do not try to embed a commit's own SHA inside its contents.
- Pin all subsequent review diffs to committed SHAs rather than a moving worktree whenever possible.
- Review fixes receive focused follow-up commits; they are not silently squashed during the workflow.
- Resume verifies that checkpoint SHAs are ancestors of the current `HEAD`. Divergence stops for user direction; the orchestrator never repairs history with reset, rebase, or force push.
- No push or remote mutation is implied by a local commit.

## Failure, Resume, and Idempotency

- Each stage records its input commit and artifact hashes before advancing.
- Re-running a completed stage with unchanged inputs is a no-op.
- Changed inputs invalidate that stage and all dependent later stages.
- Agent failure records the failure without advancing state.
- A missing or malformed artifact restarts from the earliest stage that owns it.
- A dirty worktree is allowed only when changes belong to the active story and their ownership is recorded. Unrelated user changes are preserved and excluded from agent write scopes.
- Status and state updates occur after evidence is durable, never before.
- A stage that requires a VCS checkpoint is not complete until its commit succeeds and its checkpoint ID resolves to an immutable SHA in Git history.
- The orchestrator stops on source-authority conflicts, unsafe external actions, repeated correction failure, or tests requiring unavailable infrastructure.

## Security and Cost Controls

- Source code and generated Documents remain within the configured infrastructure.
- Review agents are read-only.
- The E2E agent uses the economical model and cannot repair production code.
- High-reasoning agents are reserved for planning, implementation, and judgment where mistakes are expensive.
- Context synthesis uses the economical model but must cite source paths and distinguish inference.
- Concurrency is bounded; direct-child agents and disjoint file ownership prevent uncontrolled fan-out.
- Secrets, credentials, and production databases are outside the workflow's permitted scope.

## Intermediate Documentation

Implementation will create repository documentation under `docs/agent-workflow/`:

1. `architecture.md` — component and data-flow design.
2. `state-machine.md` — BMad/SpecKit lifecycle mapping, invalidation, and recovery.
3. `artifact-contracts.md` — workflow state, handoff, verdict, and evidence schemas.
4. `agent-catalog.md` — role boundaries, model profiles, inputs, outputs, and permissions.
5. `review-and-correction.md` — gate criteria, severity rules, and retry behavior.
6. `e2e-decision-matrix.md` — required/optional E2E predicates and ownership rules.
7. `skill-validation.md` — baseline pressure scenarios and forward-test cases.
8. `customization-guide.md` — how another coding agent or repository can recreate, modify, or package the workflow.
9. `operator-runbook.md` — commands/prompts for one story, one epic, several epics, resume, and recovery.

These are implementation inputs and portability contracts, not duplicated skill instructions.

## Validation Strategy

New orchestration skills follow test-driven skill authoring:

1. Run fresh-context baseline scenarios without the new skill and capture failures.
2. Write the minimal skill that corrects observed failure modes.
3. Run equivalent forward tests with the skill.
4. Close observed loopholes and rerun.
5. Validate skill structure with the installed skill validator.
6. Validate every custom agent TOML against current Codex loading behavior.
7. Run a dry-run story scenario that writes only workflow fixtures, not feature code.
8. Run recovery scenarios for rejected spec, rejected plan, implementation finding, required E2E failure, interrupted stage, and epic architecture finding.

## Success Criteria

The workflow is complete when:

- A user can invoke the orchestrator for a story, epic, or multiple epics without manually selecting each underlying skill.
- The orchestrator maps each selected BMad story to one SpecKit feature and preserves two-way traceability.
- Fresh judge agents gate the specification and plan.
- A high-reasoning planning agent produces the technical design.
- SpecKit tasks and analysis gate implementation.
- `speckit-analyze` runs immediately after `speckit-tasks` and reruns after any approved artifact remediation.
- Fresh implementers follow red-green-refactor and return verifiable evidence.
- Independent acceptance and quality reviews cause blocking findings to loop back to development.
- The economical E2E agent runs only when the recorded decision predicate requires it.
- Epic completion is blocked until architecture findings are dispositioned.
- Accepted artifacts, verified implementation increments, review fixes, and story/epic lifecycle transitions are committed at explicit VCS checkpoints.
- Skills, agents, schemas, documentation, and validation evidence are committed and reproducible.

## Chosen Packaging

The first implementation is repository-local:

```text
.agents/skills/run-bmad-sprint-loop/
.agents/skills/close-bmad-epic/
.codex/config.toml
.codex/agents/*.toml
docs/agent-workflow/*.md
```

This minimizes duplication and integrates directly with Vulgata's existing artifacts. A later plugin can package the same skills and agents after the repository workflow has passed real story and epic runs.
