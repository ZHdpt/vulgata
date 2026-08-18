# Sprint Loop Architecture

## Purpose

The repository-local loop delivers one BMad story as one recoverable SpecKit feature. It uses one unified path and escalates model effort by risk instead of selecting Full, Standard, or deadline modes.

| Concern | Authority |
|---|---|
| Queue and lifecycle | BMad sprint planning/status and story artifacts |
| Definition | SpecKit specification, plan, tasks, and analyze |
| Implementation completeness | SpecKit converge |
| Behavioral delivery | Installed TDD discipline and repository tests |
| Repository invariants | Constitution, AGENTS.md, ADRs, context docs, Ubiquitous Language |
| Evidence, checkpoints, Git | Active root orchestrator |
| Epic closure | close-bmad-epic |

BMad Build is not nested inside this composite loop. Its implementation and review chain would duplicate the SpecKit definition, convergence, TDD, and review authorities selected here.

## Runtime Topology

```text
active root (sole state writer and committer)
├── story-context-synthesizer       optional, large or conflicting source sets
├── spec-author                     writes spec and self-reviews
├── plan-architect                  writes plan and self-reviews
├── plan-architect-high             conditional architecture-decision plan
├── task-author                     writes tasks and self-reviews
├── definition-reviewer             plan-only trigger gate or combined definition gate
├── tdd-test-author                 owns behavioral tests and RED/GREEN evidence
├── tdd-implementer                 owns minimal production GREEN
├── convergence-analyst             appends remaining-work tasks only
├── quality-reviewer                reviews the converged boundary
├── e2e-runner                      conditional, test-only
└── epic-architecture-auditor       epic close only
```

Every worker is a direct child and receives the seven-slot dispatch contract. Workers do not delegate, stage, commit, update lifecycle state, or mutate Git history.

## Story Data Flow

```text
BMad story + authorities
  -> spec (self-reviewed)
  -> plan (self-reviewed)
  -> tasks (self-reviewed)
  -> independent definition review
  -> tests RED -> production GREEN
  -> converge
       ├─ tasks appended -> classified implementation -> converge
       └─ clean
  -> quality review
  -> conditional E2E
  -> pinned verification
  -> bound completion
```

A repository planned-change trigger inserts an independent plan-only review and explicit user authorization after plan acceptance and before tasks. The later definition review covers the complete artifact set without duplicating accepted unchanged plan conclusions.

## Atomic Identity and Ownership

One story maps to exactly one SpecKit directory through canonical activation, workflow state, and the BMad development record. Durable paths are repository-relative; checkout identity paths are the only absolute-path exception. Preflight binds branch, full HEAD, Git/worktree identities, exact status, owned paths, and unrelated paths.

Only the root writes shared lifecycle/state, stages explicit paths, and creates local commits. Definition authors, tests, production, convergence, review, and E2E have disjoint write boundaries. Shared-file work is serialized.

## Risk Escalation

The default profiles are economical: former Terra medium-or-lower work maps to Luna/high, while former Terra high-or-higher work maps to Sol/medium. The sole story-flow exception is `plan-architect-high` for a plan that contains architecture decisions. Epic architecture audit remains Sol/high as a separate close gate. Model selection does not change workflow stages or evidence standards.

## Epic Gate

After every story is durably done, close-bmad-epic verifies an exact clean commit, runs the independent architecture audit, resolves reserved findings, binds the epic checkpoint, and optionally runs the evidence-based retrospective. Story completion never implies epic closure.
