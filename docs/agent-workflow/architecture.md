# Sprint Loop Architecture

## Purpose

This repository-local workflow implements one or more BMad stories through an orchestrator-led loop. BMad owns sprint selection and lifecycle, SpecKit owns the story-level requirements-to-tasks chain, Superpowers supplies fresh-context delegation and red-green-refactor discipline, and `improve-codebase-architecture` supplies the final epic architecture gate.

The workflow does not fork installed skills. The project skills dispatch them by name and persist only the state and evidence needed to resume safely.

## Ownership Boundaries

| Concern | Owner | Canonical artifact |
|---|---|---|
| Epic/story queue and lifecycle | BMad | `docs/bmad/implementation-artifacts/sprint-status.yaml`, `docs/bmad/epics.md`, and the BMad story file |
| Story implementation context | BMad | Story file plus cited PRD, architecture, UX, research, and readiness sources |
| Story requirements | SpecKit | `spec.md` and its requirements checklist |
| Technical design | SpecKit | `plan.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md` |
| Executable work | SpecKit | `tasks.md` |
| Cross-artifact consistency | SpecKit | Read-only `speckit-analyze` report after `tasks.md` |
| Project invariants | Repository authorities | `.specify/memory/constitution.md`, `AGENTS.md`, ADRs, context docs, and `docs/UBIQUITOUS_LANGUAGE.md` |
| Test-first delivery | Superpowers | Red-green-refactor evidence for each bounded task group |
| Acceptance and quality judgment | Fresh reviewer agents | JSON-compatible verdicts over a pinned diff |
| Epic architecture quality | Matt Pocock architecture skill | `improve-codebase-architecture` temporary report and recorded disposition |
| Fine-grained progress | Sprint-loop workflow | Story-local `workflow-state.json` |
| Local Git commits | Active root orchestrator | Explicit-path commits with workflow trailers |

When authorities conflict, the orchestrator stops and reports the conflict. It does not weaken a Constitution requirement, silently override an ADR, or substitute noncanonical terms such as “Cross-System” for **Cross-Repository**.

## Atomic Identity

One BMad story maps to exactly one SpecKit feature directory. For example:

```text
BMad story key: 4-1-codegraph-integration-and-code-unit-extraction
SpecKit feature directory: specs/004-codegraph-code-unit-extraction/
```

`workflow-state.json` records the BMad story and epic keys. The BMad story development record records the SpecKit feature directory. This two-way mapping is the stable identity bridge. A feature directory is never shared by stories, and a story is never split across feature directories.

An epic or multi-epic request becomes an ordered story queue derived from BMad sprint order and dependencies. Independent context preparation may overlap, but story implementation contexts remain separate and all shared-file, lifecycle, index, and commit mutations are serialized.

## Runtime Topology

The selected `bmad-sprint-orchestrator` is the active root for a run. A normal Codex root that invokes `$run-bmad-sprint-loop` assumes the same role instead of spawning an intermediate orchestrator.

```text
active root orchestrator
├── story-context-synthesizer
├── spec-author
├── spec-judge
├── plan-architect
├── plan-judge
├── task-author
├── artifact-analyst
├── tdd-implementer (fresh per task group or correction)
├── acceptance-reviewer
├── quality-reviewer
├── e2e-runner (conditional)
└── epic-architecture-auditor (epic close only)
```

Every worker is a direct child. `.codex/config.toml` sets `agents.max_depth = 1`; workers do not delegate. Freshness comes from a new direct-child dispatch for each authoring pass, review, correction, or bounded task group. Parallel work is limited to independent reads or disjoint file ownership; the root owns stage advancement and all shared mutations.

Two thin skills control the topology:

- `$run-bmad-sprint-loop` resolves story queues, dispatches story stages, persists state, gates correction, and updates BMad lifecycle state.
- `$close-bmad-epic` verifies all stories, dispatches the architecture audit, records human dispositions, offers the retrospective, and closes the epic only after all gates pass.

## Story Data Flow

1. The root resolves a BMad story and records branch, preflight `HEAD`, repository/Git/worktree identities, porcelain status, exact owned paths, and unrelated paths. After context succeeds it writes the canonical story-keyed activation record and checkpoints `ready-for-dev → in-progress` before specification begins, atomically lifting a `backlog` parent epic to `in-progress` when applicable.
2. `story-context-synthesizer` emits a cited brief, separating sourced facts from inference.
3. `spec-author` runs `speckit-specify` and returns the created feature path/artifacts without writing workflow state or BMad mappings. The root initializes workflow state and both mapping sides atomically; `spec-judge` independently accepts or returns raw findings.
4. `plan-architect` runs `speckit-plan`; `plan-judge` independently tests feasibility, architecture, Constitution compliance, and testability.
5. `task-author` runs `speckit-tasks` with tests before implementation tasks.
6. `artifact-analyst` immediately runs read-only `speckit-analyze` across `spec.md`, `plan.md`, and `tasks.md`. Blocking remediation requires explicit user approval; approved upstream edits regenerate all dependent downstream artifacts before analysis reruns.
7. Fresh `tdd-implementer` children deliver bounded task groups with observed red and green evidence.
8. When all implementation groups pass, the root checkpoints `in-progress → review`, then fresh `acceptance-reviewer` and `quality-reviewer` children review the same pinned commit/diff identity. Blocking findings return raw to a fresh implementer and both axes rerun after any changed boundary.
9. The root applies the E2E decision matrix. `e2e-runner` may edit test code but never production code. Any tracked test edit changes the reviewed boundary and reruns both review axes before E2E can advance.
10. The root runs story verification, records evidence, transitions `review → done`, and continues the queue.

The required SpecKit order is always:

```text
speckit-specify -> speckit-plan -> speckit-tasks -> speckit-analyze -> speckit-implement
```

Independent specification and plan judges surround this flow; they never replace or relocate `speckit-analyze`.

## Gate Decision Policy

An independent gate's validated `ACCEPT` verdict is the approval for that stage. The orchestrator persists the verdict and advances without asking the user to approve the same specification, plan, implementation acceptance, or implementation quality result again. A clean read-only analysis, passing required E2E, and successful contract-defined verification likewise advance without redundant confirmation.

Human direction remains mandatory when evidence exposes a decision that an agent cannot safely make: Critical/High `speckit-analyze` remediation; unresolved product intent/scope or three failed revision outcomes; a Strong epic architecture recommendation or ADR conflict; divergent history or destructive VCS recovery; inseparable worktree ownership; unavailable required infrastructure; unsafe external actions; or ambiguous E2E failures. An agent verdict cannot manufacture product authority or authorize history rewriting.

## Sole-Committer Boundary

The active root orchestrator is the only agent allowed to stage or commit. Authors, implementers, reviewers, E2E, and architecture agents return files and evidence without modifying the index or Git history.

Before any mutation, the root records branch, preflight `HEAD`, repository root, Git common directory, worktree path, exact porcelain status, owned paths, and unrelated paths; requires a non-detached branch unless the host supplies an explicit native handoff; identifies unrelated user changes; and stops when active-story ownership cannot be separated safely. Divergent history or any proposed destructive recovery requires explicit user direction; the workflow never performs reset, rebase, force-push, or equivalent recovery implicitly. It stages explicit owned paths, inspects the staged diff, runs checkpoint verification, and commits only accepted artifacts or verified increments.

Each checkpoint receives a unique workflow checkpoint ID stored in its state, activation, or epic-gate record and in commit trailers. Story commits use `BMad-Story` plus `Workflow-Scope: story/<key>`; epic commits use `BMad-Epic` plus `Workflow-Scope: epic/<key>`. Both use `Workflow-Stage` and `Workflow-Checkpoint`. Resume resolves the ID to an immutable commit SHA and verifies it is an ancestor of current `HEAD`. The workflow does not reset, rebase, force-push, switch branches, merge, push, or open a pull request implicitly.

## Epic Gate

`$close-bmad-epic` may close an `in-progress` epic only when every story is `done`, no blocking story workflow state remains, full build and tests pass at an exact clean checkout of the pinned current commit, and the root has run workflow-owned `$audit-epic-architecture` against a separate clean audit checkout. The adapter applies the installed `improve-codebase-architecture` Explore/report process without modifying it; candidate grilling and all side effects return to corrective work. The root dispatches `epic-architecture-auditor` as direct-child independent Explore/classification judge and emits the temporary report, preserving `agents.max_depth = 1`. Raw response, classifications, and citations are independently verified before normalization. Prospective close-owned paths must be clean; disposable worktrees are identity-checked and removed. Only this gate can produce trusted `done`.

Audit acceptance validates source equality, schema, allowed strength/severity vocabulary, stable unique finding IDs, report/payload count and ID-set equality, normalized payload, and report hash. Critical/High findings, violated MUSTs, Strong recommendations, missing/invalid classifications, and ADR conflicts block. The user approves every reserved disposition. A deferred Strong finding names an external tracked work item/ADR, owner, rationale, and due/closure condition. ADR conflicts may only conform implementation, supersede/amend the ADR through a tracked decision, or block closure.

The HTML report remains temporary and never staged. It embeds the root-normalized machine payload; deterministic extraction must match every finding field and canonical hash. Durable evidence stores report/payload hashes, source, complete findings, validation result, citations, and category-valid dispositions; command evidence plus hashed raw output lives under the epic gate's `evidence/` directory. Source/tree equality is rechecked before audit acceptance, disposition, staging, and commit; any change restarts verification.

The close skill never authors or commits remediation or new authority. It routes correction through BMad correct-course plus `$run-bmad-sprint-loop` or an explicit ADR decision workflow. Pending-binding recovery runs before new-transaction empty-index preflight. To avoid self-hashing, the gate stores exact non-self-referential constraints; after staging the root hashes the complete Git tuple set and binds it in `Workflow-Manifest-SHA256`. Trusted `effective_phase = bound` is derived from the immutable pending gate plus the verified direct-child commit. Before that checkpoint the user chooses `run-after-close` or `declined`; selected retrospective state is created only after trusted binding.

## Related Contracts

- [State machine](state-machine.md)
- [Artifact contracts](artifact-contracts.md)
- [Agent catalog](agent-catalog.md)
- [Review and correction](review-and-correction.md)
- [E2E decision matrix](e2e-decision-matrix.md)
- [Operator runbook](operator-runbook.md)
