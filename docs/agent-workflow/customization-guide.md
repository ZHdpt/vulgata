# Sprint Loop Customization Guide

## Stable Spine

Preserve these invariants when adapting the workflow:

1. BMad owns queue/lifecycle; one story maps to one SpecKit feature.
2. Spec, plan, and tasks each self-review, then one independent definition reviewer judges the complete set.
3. Repository planned-change authorization remains after accepted early plan review and before tasks.
4. Observable behavior changes use honest RED then minimal GREEN.
5. Implementation repeats until speckit-converge reports clean.
6. One independent quality review examines the converged boundary.
7. E2E is predicate-driven; final verification is pinned.
8. The active root alone writes lifecycle/state and commits explicit owned paths.
9. Epic close retains exact-commit verification and independent architecture audit.

## Model Policy

Defaults are stored in `.codex/agents/*.toml` and summarized in agent-catalog.md. Luna/high owns context, tests, convergence, and E2E; Luna/medium owns tasks; Sol/medium owns orchestration, specification, planning, production, definition review, and quality review. Escalate named architecture/risk work to Sol/high and record the trigger in the dispatch.

Changing model IDs must preserve sandbox and write boundaries. Cost reduction cannot combine author and independent reviewer or let a test author edit production.

## Progressive Disclosure

Keep run-bmad-sprint-loop as the short executable spine. Put schema details in artifact-contracts, invalidation/migration in state-machine, models in agent-catalog, corrections in review-and-correction, and operational examples here. One rule has one authoritative home; other documents link rather than restate it.

## Validation

After customization, run:

```powershell
python .agents/skills/run-bmad-sprint-loop/scripts/validate_workflow.py --repo .
cd .agents/skills/run-bmad-sprint-loop/scripts
python -m unittest tests.test_validate_workflow
```

Also verify model-visible discovery from a fresh repository session. Do not modify installed upstream BMad, SpecKit, or Matt Pocock skills; adapt them through repository-owned orchestration roles and contracts.
