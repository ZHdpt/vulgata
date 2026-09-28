# Governed Delivery Architecture

## Purpose

The repository-local loop delivers one lifecycle story as one recoverable SpecKit feature. It uses one unified path and escalates model effort by risk instead of selecting Full, Standard, or deadline modes. BMad 6.11 remains supported; new installations use BMad 6.12.

| Concern | Authority |
|---|---|
| Queue and lifecycle | BMad sprint planning/status and story artifacts |
| Definition | SpecKit specification, plan, tasks, and analyze |
| Implementation completeness | SpecKit converge |
| Behavioral delivery | Shipped governed-TDD discipline and repository tests |
| Repository invariants | Constitution, AGENTS.md, ADRs, context docs, Ubiquitous Language |
| Evidence, checkpoints, Git | Active root orchestrator |
| Epic closure | close-governed-epic |

BMad Build is not nested inside this composite loop. Its implementation and review chain would duplicate the SpecKit definition, convergence, TDD, and review authorities selected here.

## Runtime Topology

```text
active root (sole state writer and committer)
├── story-context-synthesizer       optional, large or conflicting source sets
├── definition-author               persistent spec, plan, tasks, and corrections
├── plan-architect-high             architecture-classified plan only
├── definition-author-recovery[-high] exclusive replacement after diagnosed recovery
├── definition-reviewer             retained plan-only/definition/correction judge
├── tdd-test-author                 retained group test owner and final-shape assessment
├── tdd-implementer                 retained group production owner and final-shape assessment
├── convergence-analyst             appends remaining-work tasks only
├── quality-reviewer                reviews the converged boundary
├── e2e-runner                      conditional, test-only
└── epic-architecture-auditor       epic close only
```

Every worker is a direct child and receives the seven-slot dispatch contract. Workers do not delegate, stage, commit, update lifecycle state, or mutate Git history.

## Story Data Flow

```text
Lifecycle story + authorities
  -> persistent definition author
       -> spec (self-reviewed)
       -> plan + none|bounded|architecture implementation design (self-reviewed and deterministically validated)
       -> tasks (self-reviewed with deterministic design-ID coverage)
  -> independent definition review
  -> informed implementation approval
  -> tests RED -> minimal production GREEN -> final-shape assessment / conditional local refactor -> final GREEN
  -> converge
       ├─ reused/new tasks -> appropriate implementation/test owner -> converge
       ├─ invalid definition -> earliest author phase -> affected downstream checks
       └─ clean
  -> root lookback and selective approved project-document updates
  -> quality review
  -> conditional E2E
  -> pinned verification
  -> bound completion
```

Implementation design stays inside `plan.md`; it adds no stage or ordinary author. Before recovery takeover, `architecture` retains the serialized Sol/high exception. After evidence-backed exclusive Astra takeover, the recovery author owns every classification and definition phase through completion. Existing plan-review/authority triggers remain; reuse the retained independent reviewer with current raw artifacts and ordered prior verdicts.

## Atomic Identity and Ownership

One story maps to exactly one SpecKit directory through activation, workflow state, and development record. New directories use the full canonical story key through the read-only selection helper and explicit-directory adapter; existing names remain unchanged and collisions stop. The 12 KiB state 3.1 holds current recovery, retained author, transition-head, and lookback descriptors, not history. Detailed evidence remains in native artifacts and Git. Historical 3.0 completion validates unchanged. Durable paths are repository-relative; preflight stores checkout hashes.

Only the root writes shared lifecycle/state, stages explicit paths, and creates local commits. The definition author persists across stages but receives a separate phase-scoped write boundary on every follow-up. Definition, tests, production, convergence, review, and E2E retain disjoint ownership. Shared-file work is serialized.

Both TDD owners persist across a coherent task group and are reused for later corrections while available/current, including E2E product failures. A replacement receives the actual raw boundary and evidence. Final-shape assessment is required but refactoring is conditional; no extra agent, method-by-method plan, line-count rule, or quality stage is introduced.

## Risk Escalation

Ordinary Luna/Sol profiles remain economical. Initial architecture planning is Sol/high; difficult diagnosed definition recovery may take over at Astra/medium and then high for a recorded deeper-analysis trigger. No other model changes occur. An upgrade grants no authority or attempts. One episode spans unresolved corrections: initial fail 1, failed repairs 2/3/4, diagnosis at 3, user pause at 4. A targeted evidence-bound guard preserves count across phase/input/author changes; post-implementation closure requires final verification.

Root owns a single bounded story lookback before quality. It promotes only reusable verified lessons, reuses or supersedes touched entries, and synchronizes approved canonical deltas. Context/replan retrieves only relevant current guidance. Proof binds the section and touched outputs without hashing mutable bookkeeping; after completion, audit uses committed content. Semantic diagnosis, lesson value, and deduplication remain model judgments, not claims of deterministic qualification.

## Epic Gate

After every story is durably done, close-governed-epic verifies an exact clean commit, runs the independent architecture audit, resolves reserved findings, binds the epic checkpoint, and optionally runs the evidence-based retrospective. Story completion never implies epic closure.
