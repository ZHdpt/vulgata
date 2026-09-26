# Governed Delivery Agent Catalog

Defaults remain economical. Initial architecture planning uses its serialized Sol/high exception; diagnosed difficult definition recovery has an exclusive Astra author route. Other roles retain their listed model/effort.

As of workflow 0.7.2, Sol and Luna mean GPT-6 Sol and GPT-6 Luna. This generation update preserves every role's reasoning effort, sandbox, ownership, and escalation conditions; it requires no state migration.

| Agent | Default model / effort | Sandbox | Responsibility |
|---|---|---|---|
| delivery-orchestrator | gpt-6-sol / medium | workspace-write | Queue, state, gates, explicit-path checkpoints, sole local committer |
| story-context-synthesizer | gpt-6-luna / high | read-only | Optional cited source synthesis for large or conflicting authority sets |
| definition-author | gpt-6-sol / medium | workspace-write | Persistent, phase-scoped spec/plan/tasks author and correction owner |
| plan-architect-high | gpt-6-sol / high | workspace-write | Retained current exception for architecture-classified plan and its corrections; plan set only |
| definition-author-recovery | gpt-6-astra / medium | workspace-write | Exclusive retained spec/plan/tasks author after diagnosed difficult recovery |
| definition-author-recovery-high | gpt-6-astra / high | workspace-write | Same exclusive ownership after a recorded deeper-analysis trigger |
| definition-reviewer | gpt-6-sol / medium | read-only | Retained plan-only, complete-definition, and correction review plus speckit-analyze |
| tdd-test-author | gpt-6-luna / high | workspace-write | Retained group test owner; behavioral tests, final-shape assessment, honest RED/final-GREEN |
| tdd-implementer | gpt-6-sol / medium | workspace-write | Retained group production owner; minimal GREEN, final-shape assessment and conditional local refactor; no tests |
| convergence-analyst | gpt-6-luna / high | workspace-write | speckit-converge; append-only tasks write |
| quality-reviewer | gpt-6-sol / medium | read-only | Correctness, security, tests, architecture, regression on converged boundary |
| e2e-runner | gpt-6-luna / high | workspace-write | Conditional E2E/API/integration tests; never production |
| epic-architecture-auditor | gpt-6-sol / high | read-only | Independent pinned epic architecture classification |

## Escalation

Before recovery takeover, use `plan-architect-high` only for `Implementation Design: architecture` with its recorded trigger; `none|bounded` stay with the ordinary author. A spec/classification finding first requests fresh classification. This plan-only exception is serialized with ordinary authoring.

After diagnosis, select `definition-author-recovery` only when the present author has not resolved a definition reasoning failure or evidence reveals a consequential design problem beyond its accepted approach. Local code defects, missing tasks, expected RED, and infrastructure failures do not alone justify it. The two-failed-repair diagnosis is not automatic escalation. Astra/high additionally needs unresolved interacting design constraints or a failed Astra-medium repair whose diagnosis shows the reasoning problem remains. Do not select xhigh/max or upgrade other roles.

Run budget/authority/dispatch guards before changing profiles. Check installed/runtime capability without paid probe calls; an unavailable route blocks explicitly rather than silently falling back. Preserve the session when runtime model/effort changes retain its instructions; otherwise hand over current raw artifacts, decisions, ordered raw verdict paths, failed approaches/evidence, classification, and still-valid implementation once. Finish or suspend the old dispatch, transfer exclusive ownership, and reject late superseded results.

After takeover, stop dispatching both `definition-author` and `plan-architect-high` for that story. The selected recovery author owns fresh classification and all spec/plan/tasks corrections, including architecture, through completion. Retain its tier after recovery closes and on session replacement. Persist only profile/decision reference; never runtime handles. Model choice changes neither repair allowance nor user authority. Epic architecture audit stays Sol/high.

## Dispatch Rules

Every worker is a direct child and receives exactly the seven slots in the dispatch contract. Spawn one definition author per story and reuse it through phase-scoped follow-ups for spec, plan, tasks, and document corrections. Authors self-review their own artifact but never supply independent acceptance. Reuse one definition reviewer across its story's review cycle; every turn receives current raw pinned artifacts and ordered prior raw verdict paths. A replacement reviewer receives the same complete package, never only author conclusions. Correcting workers receive unchanged raw findings. Workers never update lifecycle state, stage, commit, or change Git history.

Retain both TDD owners across their coherent task group and reuse its available/current pair for later corrections, including E2E product failures. Keep their writes disjoint and supply each turn's actual boundary and evidence. If a session or capacity is unavailable, hand over current raw artifacts, accepted scope, findings, and RED/GREEN observations explicitly; continuity does not waive capacity limits or allow stale results. Final-shape assessment belongs to both authors, not another agent or gate. Runtime handles stay out of state.

A clean independent verdict and safe ordinary correction advance without duplicate user approval. Apply the shared recovery guard: initial failed result 1, failed repairs 2/3, diagnosis at 3, user pause at 4 or the accepted finite extension limit. Existing planned-change, first implementation, authority, architecture, blocker, ownership, external-action, and reserved epic gates remain.
