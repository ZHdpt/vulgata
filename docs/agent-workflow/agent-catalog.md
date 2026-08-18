# Sprint Loop Agent Catalog

Defaults follow the economical policy approved for the unified workflow. Sol/high is a dispatch-time escalation for architecture and the named risk triggers; it is not the ordinary profile.

| Agent | Default model / effort | Sandbox | Responsibility |
|---|---|---|---|
| bmad-sprint-orchestrator | gpt-5.6-sol / medium | workspace-write | Queue, state, gates, explicit-path checkpoints, sole local committer |
| story-context-synthesizer | gpt-5.6-luna / high | read-only | Optional cited source synthesis for large or conflicting authority sets |
| spec-author | gpt-5.6-sol / medium | workspace-write | speckit-specify plus inline self-review; spec/checklist only |
| plan-architect | gpt-5.6-sol / medium | workspace-write | speckit-plan plus inline self-review; plan set only |
| plan-architect-high | gpt-5.6-sol / high | workspace-write | Conditional architecture-decision plan plus inline self-review |
| task-author | gpt-5.6-luna / medium | workspace-write | speckit-tasks plus inline self-review; tasks only |
| definition-reviewer | gpt-5.6-sol / medium | read-only | Plan-only trigger review or one combined definition/analyze verdict |
| tdd-test-author | gpt-5.6-luna / high | workspace-write | Behavioral tests and honest RED/GREEN evidence; no production |
| tdd-implementer | gpt-5.6-sol / medium | workspace-write | Minimal production GREEN; no tests or loop-time refactor |
| convergence-analyst | gpt-5.6-luna / high | workspace-write | speckit-converge; append-only tasks write |
| quality-reviewer | gpt-5.6-sol / medium | read-only | Correctness, security, tests, architecture, regression on converged boundary |
| e2e-runner | gpt-5.6-luna / high | workspace-write | Conditional E2E/API/integration tests; never production |
| epic-architecture-auditor | gpt-5.6-sol / high | read-only | Independent pinned epic architecture classification |

## Escalation

Use `plan-architect-high` only when the plan contains architecture decisions, and record the trigger in the dispatch. Other story roles retain their listed defaults. Epic architecture audit remains Sol/high because it is a separate close gate.

## Dispatch Rules

Every worker is a direct child and receives exactly the seven slots in the dispatch contract. Authors self-review their own artifact but never supply independent acceptance. Definition and quality reviewers receive raw pinned artifacts, never author conclusions. Correcting workers receive unchanged raw findings. Workers never update lifecycle state, stage, commit, or change Git history.

A clean independent verdict, clean convergence, passing E2E, or passing verification advances without duplicate user approval. User authority remains required for planned-change triggers, Critical/High definition remediation, unresolved intent, destructive recovery, inseparable ownership, unsafe external action, and reserved epic findings.
