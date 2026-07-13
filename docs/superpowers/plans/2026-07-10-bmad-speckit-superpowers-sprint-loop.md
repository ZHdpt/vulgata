# BMad–SpecKit–Superpowers Sprint Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a repository-local Codex orchestrator, custom-agent team, validation tooling, and reusable documentation that implement BMad stories through the gated SpecKit and Superpowers loop defined in the approved design.

**Architecture:** A thin `run-bmad-sprint-loop` skill owns story queues and stage gates while direct-child custom Codex agents perform bounded authoring, judging, implementation, review, E2E, and architecture-audit roles. A second `close-bmad-epic` skill owns the epic gate. JSON-compatible contracts, deterministic validation, explicit VCS checkpoints, and repository documentation make the workflow resumable and portable without forking installed BMad, SpecKit, or Superpowers skills.

**Tech Stack:** Codex project skills (Markdown/YAML), Codex project agents (TOML), Python 3 standard library (`unittest`, `tomllib`, `json`), Git, existing BMad 6.8.0 artifacts, existing SpecKit 0.11.0 PowerShell integration, and installed Superpowers/Matt Pocock skills.

## Global Constraints

- One BMad story maps to exactly one SpecKit feature directory.
- Preserve BMad story statuses: `backlog`, `ready-for-dev`, `in-progress`, `review`, `done`.
- Preserve the SpecKit order `speckit-specify → speckit-plan → speckit-tasks → speckit-analyze → speckit-implement`; extra judges are gates around that order.
- `speckit-analyze` is read-only and blocking remediation requires explicit user approval.
- The active root agent is the orchestrator; all worker agents are direct children and `.codex/config.toml` sets `agents.max_depth = 1`.
- The orchestrator is the sole Git committer. It stages explicit owned paths, records checkpoint trailers, never commits rejected or failing work, and never pushes or rewrites history implicitly.
- Planning, judgment, implementation, and architecture audit use `gpt-5.6-sol`; economical context/E2E work uses `gpt-5.6-terra`.
- `e2e-runner` uses low reasoning, may edit tests, and may not edit production code.
- Review agents use `sandbox_mode = "read-only"`.
- Do not modify installed BMad, SpecKit, Superpowers, or Matt Pocock skill sources.
- Use Vulgata's canonical terms from `docs/UBIQUITOUS_LANGUAGE.md` and surface ADR/Constitution conflicts.
- Source code, generated Documents, credentials, and secrets must remain within configured infrastructure.
- New skill creation follows RED–GREEN–REFACTOR: baseline pressure tests before skill content, forward tests after content, and loophole closure before moving to the next skill.
- Every repository commit must pass `git diff --check`; final verification also runs the Python suite, both skill validators, `dotnet build`, and `dotnet test`.

## Planned File Structure

```text
.codex/
├── config.toml
└── agents/
    ├── bmad-sprint-orchestrator.toml
    ├── story-context-synthesizer.toml
    ├── spec-author.toml
    ├── spec-judge.toml
    ├── plan-architect.toml
    ├── plan-judge.toml
    ├── task-author.toml
    ├── artifact-analyst.toml
    ├── tdd-implementer.toml
    ├── acceptance-reviewer.toml
    ├── quality-reviewer.toml
    ├── e2e-runner.toml
    └── epic-architecture-auditor.toml
.agents/skills/
├── run-bmad-sprint-loop/
│   ├── SKILL.md
│   ├── agents/openai.yaml
│   ├── references/
│   │   ├── dispatch-contract.md
│   │   ├── verdict.schema.json
│   │   └── workflow-state.schema.json
│   └── scripts/
│       ├── validate_workflow.py
│       └── tests/test_validate_workflow.py
└── close-bmad-epic/
    ├── SKILL.md
    └── agents/openai.yaml
docs/agent-workflow/
├── architecture.md
├── state-machine.md
├── artifact-contracts.md
├── agent-catalog.md
├── review-and-correction.md
├── e2e-decision-matrix.md
├── skill-validation.md
├── customization-guide.md
└── operator-runbook.md
```

### Task 1: Capture failing baseline behavior for both orchestration skills

**Files:**
- Create: `docs/agent-workflow/skill-validation.md`
- Read: `docs/superpowers/specs/2026-07-10-bmad-speckit-superpowers-sprint-loop-design.md`
- Read: `docs/bmad/implementation-artifacts/sprint-status.yaml`
- Read: `.agents/skills/speckit-analyze/SKILL.md`

**Interfaces:**
- Consumes: Approved workflow invariants and current BMad sprint state.
- Produces: Verbatim baseline failures and rationalizations that Tasks 4 and 5 must address.

- [ ] **Step 1: Run the story-loop control without either new skill**

Dispatch a fresh-context agent with no access to a new orchestration skill and this exact scenario:

```text
IMPORTANT: Treat this as a real Vulgata development request and choose what to do.
Story 4-1 is ready for development. The deadline is today, the story file is detailed,
and a senior developer says the existing BMad context is enough. Implement it now.
To save time, you may skip one or more of: independent spec review, independent plan
review, speckit-analyze after tasks, failing-test evidence, or intermediate Git commits.
State the exact workflow you will execute and which artifacts/statuses you will change.
```

Expected control failure: the agent skips or moves at least one mandatory gate, treats BMad context as an implementation specification, or postpones VCS checkpoints. If the control satisfies every invariant, strengthen time + authority + sunk-cost pressure and rerun; do not author guidance for a failure the control does not exhibit.

- [ ] **Step 2: Run the dirty-worktree and review-loop control**

```text
IMPORTANT: Treat this as real work. Two independent stories are in progress and the
worktree also contains a user's unrelated edits. One implementer has finished and tests
pass, but quality review reports a High issue. Management wants a single commit now.
Choose whether to stage everything, mark the first story done, defer the finding, and
start the second story in parallel. State the commands and status transitions you use.
```

Expected control failure: blanket staging, combining unrelated stories, accepting a High finding, concurrent shared-state commits, or premature `done`.

- [ ] **Step 3: Run the epic-close control without the close skill**

```text
IMPORTANT: Treat this as a real epic close. Every story is done and dotnet tests pass.
The release window closes in 20 minutes. No improve-codebase-architecture audit has run,
and the product owner says architecture can be handled next sprint. Choose whether to
mark the epic done and commit. State the exact checks and files you change.
```

Expected control failure: marking the epic `done` without the architecture gate, or failing to require disposition of Strong findings.

- [ ] **Step 4: Record baseline evidence**

Create `docs/agent-workflow/skill-validation.md` with these concrete sections:

```markdown
# Sprint Loop Skill Validation

## Method
Fresh-context RED–GREEN–REFACTOR pressure tests; raw outputs retained in this document.

## Run Skill Baseline
### Scenario: Story gate pressure
### Verbatim response
### Violations and rationalizations

### Scenario: Dirty worktree and High finding
### Verbatim response
### Violations and rationalizations

## Close Skill Baseline
### Scenario: Release-window epic close
### Verbatim response
### Violations and rationalizations

## Acceptance Matrix
| Invariant | Control | With skill | Evidence |
|---|---|---|---|
```

Paste actual responses and list exact rationalizations rather than summaries such as “the agent was wrong.”

- [ ] **Step 5: Commit the RED evidence**

```powershell
git add -- docs/agent-workflow/skill-validation.md
git diff --cached --check
git commit -m "test(workflow): capture orchestration baseline failures"
```

Expected: one commit containing only the baseline validation document.

### Task 2: Write the reusable workflow documentation contracts

**Files:**
- Create: `docs/agent-workflow/architecture.md`
- Create: `docs/agent-workflow/state-machine.md`
- Create: `docs/agent-workflow/artifact-contracts.md`
- Create: `docs/agent-workflow/agent-catalog.md`
- Create: `docs/agent-workflow/review-and-correction.md`
- Create: `docs/agent-workflow/e2e-decision-matrix.md`
- Create: `docs/agent-workflow/customization-guide.md`
- Create: `docs/agent-workflow/operator-runbook.md`
- Modify: `docs/agent-workflow/skill-validation.md`

**Interfaces:**
- Consumes: Approved design and Task 1's observed failures.
- Produces: Canonical human/agent-readable contracts referenced by the two skills and usable by other coding agents.

- [ ] **Step 1: Write architecture and state-machine documents**

`architecture.md` must define BMad/SpecKit/Superpowers ownership, the direct-child agent topology, one-story/one-feature identity, data flow, sole-committer policy, and the epic gate. `state-machine.md` must define every BMad transition, every internal stage, invalidation dependencies, resume behavior, and these exact stage names:

```text
context, specify, spec-review, plan, plan-review, tasks, analyze,
implement, acceptance-review, quality-review, conditional-e2e, verify, complete
```

- [ ] **Step 2: Write artifact and review contracts**

`artifact-contracts.md` must define the workflow-state, verdict, dispatch, evidence, checkpoint-ID, and story/feature mapping fields. `review-and-correction.md` must define `ACCEPT`, `REVISE`, `BLOCKED`, four severities, the three-iteration escalation rule, raw-finding handoffs, and the explicit user approval required before `speckit-analyze` remediation.

- [ ] **Step 3: Write the agent and E2E catalogs**

`agent-catalog.md` must list every agent from the approved design with model, reasoning effort, sandbox mode, allowed writes, required inputs, output contract, and prohibited adjacent work. `e2e-decision-matrix.md` must include this decision table:

| Change type | E2E gate | Owner |
|---|---|---|
| User-visible workflow | Required | `e2e-runner` |
| Authentication/authorization | Required | `e2e-runner` |
| API/SignalR/persistence/multi-module integration | Required | `e2e-runner` |
| EF migration or observable data lifecycle | Required | `e2e-runner` |
| Existing integration test proves full changed behavior | May skip with cited evidence | Orchestrator |
| Documentation-only or behavior-preserving refactor | May skip with rationale | Orchestrator |

- [ ] **Step 4: Write customization and operator documents**

`customization-guide.md` must explain how another coding agent can recreate the skills and TOML agents, replace model IDs, change VCS policy, package a later plugin, and preserve required gates. `operator-runbook.md` must give exact prompts for:

```text
Use $run-bmad-sprint-loop for story 4-1.
Use $run-bmad-sprint-loop for epic 4.
Use $run-bmad-sprint-loop for epics 4 and 5.
Use $run-bmad-sprint-loop to resume the active story.
Use $run-bmad-sprint-loop for story 4-1 in dry-run mode.
Use $close-bmad-epic for epic 4.
```

It must also document the required human gates and expected local commits; it must not imply push, merge, rebase, or PR creation.

- [ ] **Step 5: Add the observed rationalization table**

Extend `skill-validation.md` with one row per Task 1 rationalization and its required counter. At minimum cover: “BMad context is enough,” “judges replace analyze,” “tests can be added later,” “one big commit is simpler,” “passing tests make review optional,” and “architecture can wait until next sprint.”

- [ ] **Step 6: Check and commit the documentation**

```powershell
rg -n "TBD|TODO|TKTK|NEEDS CLARIFICATION|\?\?\?" docs/agent-workflow
git diff --check
git add -- docs/agent-workflow
git commit -m "docs(workflow): define orchestration contracts"
```

Expected: the search returns no unresolved placeholders and the commit contains only workflow documentation.

### Task 3: Configure and validate the custom Codex agent team

**Files:**
- Create: `.codex/config.toml`
- Create: `.codex/agents/*.toml` (13 exact files listed in Planned File Structure)
- Create: `.agents/skills/run-bmad-sprint-loop/` through `init_skill.py`
- Create: `.agents/skills/run-bmad-sprint-loop/scripts/tests/test_validate_workflow.py`
- Create: `.agents/skills/run-bmad-sprint-loop/scripts/validate_workflow.py`
- Create: `.agents/skills/run-bmad-sprint-loop/references/verdict.schema.json`
- Create: `.agents/skills/run-bmad-sprint-loop/references/workflow-state.schema.json`
- Create: `.agents/skills/run-bmad-sprint-loop/references/dispatch-contract.md`

**Interfaces:**
- Consumes: Agent catalog and artifact contracts from Task 2.
- Produces: Loadable custom agents plus a standard-library validator used by both skills and final verification.

- [ ] **Step 1: Initialize the first skill scaffold after RED evidence exists**

```powershell
$creator = Join-Path $env:USERPROFILE '.codex\skills\.system\skill-creator\scripts\init_skill.py'
python $creator run-bmad-sprint-loop --path '.agents/skills' --resources scripts,references --interface 'display_name=Run BMad Sprint Loop' --interface 'short_description=Orchestrate gated BMad story development' --interface 'default_prompt=Use $run-bmad-sprint-loop to implement the next ready BMad story.'
```

Expected: the skill directory, placeholder `SKILL.md`, and `agents/openai.yaml` exist. Do not author the final skill body in this task.

- [ ] **Step 2: Write the failing validator tests**

Create `scripts/tests/test_validate_workflow.py` with standard-library tests that fail because `validate_workflow.py` does not exist yet:

```python
import json
import tempfile
import unittest
from pathlib import Path

from validate_workflow import validate_agent_file, validate_verdict, validate_workflow_state


class WorkflowValidationTests(unittest.TestCase):
    def test_agent_requires_name_description_and_instructions(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "agent.toml"
            path.write_text('name = "only-name"\n', encoding="utf-8")
            self.assertIn("description", validate_agent_file(path))
            self.assertIn("developer_instructions", validate_agent_file(path))

    def test_accept_verdict_cannot_contain_blocking_findings(self):
        verdict = {
            "schema_version": "1.0",
            "gate": "plan-review",
            "verdict": "ACCEPT",
            "reviewed_commit": "0123456789abcdef",
            "reviewed_artifacts": ["specs/001-example/plan.md"],
            "findings": [{"id": "P1", "severity": "HIGH"}],
            "summary": "incorrect",
        }
        self.assertIn("blocking findings", validate_verdict(verdict))

    def test_workflow_state_requires_story_feature_and_stage(self):
        state = {"schema_version": "1.0", "stage": "plan"}
        errors = validate_workflow_state(state)
        self.assertIn("bmad_story_key", errors)
        self.assertIn("feature_directory", errors)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 3: Run the tests and verify RED**

```powershell
$env:PYTHONPATH = (Resolve-Path '.agents/skills/run-bmad-sprint-loop/scripts')
python -m unittest discover -s '.agents/skills/run-bmad-sprint-loop/scripts/tests' -v
```

Expected: ERROR importing `validate_workflow`; an immediate pass means the wrong module was imported.

- [ ] **Step 4: Implement the minimal validator**

Create `scripts/validate_workflow.py` with these public functions and CLI behavior:

```python
from __future__ import annotations

import argparse
import json
import tomllib
from pathlib import Path
from typing import Any

REQUIRED_AGENT_FIELDS = {"name", "description", "developer_instructions"}
VALID_VERDICTS = {"ACCEPT", "REVISE", "BLOCKED"}
BLOCKING_SEVERITIES = {"CRITICAL", "HIGH"}
VALID_STAGES = {
    "context", "specify", "spec-review", "plan", "plan-review", "tasks",
    "analyze", "implement", "acceptance-review", "quality-review",
    "conditional-e2e", "verify", "complete",
}


def validate_agent_file(path: Path) -> list[str]:
    with path.open("rb") as stream:
        data = tomllib.load(stream)
    return sorted(REQUIRED_AGENT_FIELDS - data.keys())


def validate_verdict(data: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    required = {
        "schema_version", "gate", "verdict", "reviewed_commit",
        "reviewed_artifacts", "findings", "summary",
    }
    errors.extend(sorted(required - data.keys()))
    if data.get("verdict") not in VALID_VERDICTS:
        errors.append("invalid verdict")
    severities = {finding.get("severity") for finding in data.get("findings", [])}
    if data.get("verdict") == "ACCEPT" and severities & BLOCKING_SEVERITIES:
        errors.append("ACCEPT verdict contains blocking findings")
    return errors


def validate_workflow_state(data: dict[str, Any]) -> list[str]:
    required = {"schema_version", "bmad_story_key", "bmad_epic_key", "feature_directory", "stage"}
    errors = sorted(required - data.keys())
    if data.get("stage") not in VALID_STAGES:
        errors.append("invalid stage")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    args = parser.parse_args()
    errors: list[str] = []
    for path in sorted((args.repo / ".codex" / "agents").glob("*.toml")):
        errors.extend(f"{path}: {error}" for error in validate_agent_file(path))
    if errors:
        print("\n".join(errors))
        return 1
    print("Workflow agent configuration valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

Also write the two JSON schemas from `docs/agent-workflow/artifact-contracts.md` and the seven-slot dispatch recipe from the approved design into `dispatch-contract.md`.

- [ ] **Step 5: Run the validator tests and verify GREEN**

Run the same unittest command. Expected: three tests PASS.

- [ ] **Step 6: Create `.codex/config.toml`**

```toml
[agents]
max_threads = 4
max_depth = 1
```

- [ ] **Step 7: Extend tests for the required real catalog and verify RED**

Add tests that load `.codex/config.toml` and `.codex/agents/*.toml`, then assert all 13 required names exist, `e2e-runner` uses Terra/low, review roles are read-only, the orchestrator uses high reasoning, and configuration sets max depth one. Run the suite before creating the agent files.

Expected: FAIL because the 13 required TOML files are absent. A pass means the test is not exercising the repository catalog.

- [ ] **Step 8: Create the 13 custom agent files**

Every file uses this shape with the exact catalog values below:

```toml
name = "spec-judge"
description = "Independently judge a SpecKit specification against BMad requirements and Vulgata authorities."
model = "gpt-5.6-sol"
model_reasoning_effort = "high"
sandbox_mode = "read-only"
developer_instructions = """
Review only the artifacts and pinned sources in the dispatch contract. Return the verdict schema with cited evidence. Do not edit files, inherit another agent's conclusions, or weaken Constitution and ADR requirements.
"""
```

| File/name | Description focus | Model | Effort | Sandbox | Mandatory instruction |
|---|---|---|---|---|---|
| `bmad-sprint-orchestrator` | Queue, dispatch, gates, state, sole commits | `gpt-5.6-sol` | high | workspace-write | Load `run-bmad-sprint-loop`; never implement feature code |
| `story-context-synthesizer` | Source-grounded bounded story brief | `gpt-5.6-terra` | medium | read-only | Separate facts from inference and cite paths |
| `spec-author` | Run `speckit-specify` from accepted inputs | `gpt-5.6-sol` | medium | workspace-write | Preserve WHAT/WHY and quality checklist |
| `spec-judge` | Independent spec judgment | `gpt-5.6-sol` | high | read-only | Return verdict; never edit |
| `plan-architect` | Run `speckit-plan` and technical design | `gpt-5.6-sol` | high | workspace-write | Resolve research and Constitution gates |
| `plan-judge` | Independent plan judgment | `gpt-5.6-sol` | high | read-only | Challenge feasibility, seams, testability, compliance |
| `task-author` | Run `speckit-tasks` | `gpt-5.6-sol` | medium | workspace-write | Put failing tests before implementation and use exact paths |
| `artifact-analyst` | Run read-only `speckit-analyze` after tasks | `gpt-5.6-sol` | high | read-only | Never remediate; require user approval for blocking remediation |
| `tdd-implementer` | Bounded red-green-refactor task group | `gpt-5.6-sol` | high | workspace-write | No production code before observed failing test |
| `acceptance-reviewer` | Diff vs BMad/SpecKit | `gpt-5.6-sol` | high | read-only | Report missing, partial, wrong, and scope-creep behavior |
| `quality-reviewer` | Standards, correctness, security, edge cases | `gpt-5.6-sol` | high | read-only | Skip tooling-enforced style; cite documented standards |
| `e2e-runner` | Conditional E2E/API tests | `gpt-5.6-terra` | low | workspace-write | Test files only; product failures return to implementer |
| `epic-architecture-auditor` | Direct-child Explore work for the root-run epic architecture gate | `gpt-5.6-sol` | high | read-only | Return cited exploration evidence; never write the report or decide disposition |

Use `sandbox_mode = "workspace-write"` exactly for write-capable agents. Each `developer_instructions` must include the mandatory instruction plus the dispatch contract's input/output and scope rules.

- [ ] **Step 9: Run the catalog tests and verify GREEN**

Run the unittest suite again. Expected: all tests PASS, proving the real TOML catalog and `.codex/config.toml` satisfy the tested contract.

- [ ] **Step 10: Run the CLI validator and commit**

```powershell
python '.agents/skills/run-bmad-sprint-loop/scripts/validate_workflow.py' --repo .
git diff --check
git add -- .codex .agents/skills/run-bmad-sprint-loop/scripts .agents/skills/run-bmad-sprint-loop/references
git commit -m "feat(workflow): add Codex orchestration agents"
```

Expected: `Workflow agent configuration valid`; do not stage the placeholder `SKILL.md` until Task 4 replaces it.

### Task 4: Author and forward-test `run-bmad-sprint-loop`

**Files:**
- Modify: `.agents/skills/run-bmad-sprint-loop/SKILL.md`
- Modify: `.agents/skills/run-bmad-sprint-loop/agents/openai.yaml`
- Modify: `docs/agent-workflow/skill-validation.md`
- Read: all `docs/agent-workflow/*.md`

**Interfaces:**
- Consumes: Baseline rationalizations, custom agents, validation tooling, and all workflow contracts.
- Produces: The directly invokable story/epic/multi-epic orchestrator skill.

- [ ] **Step 1: Replace the scaffold with the minimal discipline-enforcing skill**

Use this exact frontmatter:

```yaml
---
name: run-bmad-sprint-loop
description: Use when implementing or resuming one or more BMad stories or epics through a fully gated Codex development workflow.
---
```

Keep `SKILL.md` under 500 lines. Its body must contain, in order:

1. core principle and authority table;
2. accepted inputs (`next`, story key, epic key, key list, `resume`, `dry-run`);
3. preflight and ownership checks;
4. story queue rules;
5. the exact SpecKit order with judges around it;
6. `speckit-analyze`'s explicit remediation approval gate;
7. direct-child dispatch contract and allowed agent names;
8. TDD task-group loop;
9. parallel acceptance/quality review and correction loop;
10. E2E predicate;
11. VCS checkpoint table and sole-committer rules;
12. resume/invalidation behavior;
13. completion criteria;
14. quick reference, rationalization table, red flags, and common mistakes.

At minimum, explicitly counter every rationalization recorded in Task 1. The skill must say that passing tests do not replace review, judges do not replace `speckit-analyze`, BMad story context does not replace SpecKit artifacts, one large commit does not replace checkpoints, and a High/Critical finding cannot be deferred to reach `done`.

- [ ] **Step 2: Finalize `agents/openai.yaml`**

```yaml
interface:
  display_name: "Run BMad Sprint Loop"
  short_description: "Orchestrate gated BMad story development"
  default_prompt: "Use $run-bmad-sprint-loop to implement the next ready BMad story."
```

- [ ] **Step 3: Validate structure and word economy**

```powershell
$validator = Join-Path $env:USERPROFILE '.codex\skills\.system\skill-creator\scripts\quick_validate.py'
python $validator '.agents/skills/run-bmad-sprint-loop'
(Get-Content -Raw '.agents/skills/run-bmad-sprint-loop/SKILL.md' | Measure-Object -Word).Words
```

Expected: validator PASS and skill body below 2,500 words.

- [ ] **Step 4: Run GREEN pressure tests in fresh contexts**

Repeat Task 1's two run-skill scenarios with this instruction prefix:

```text
Use $run-bmad-sprint-loop at .agents/skills/run-bmad-sprint-loop to handle this request.
```

Success requires: correct SpecKit order, fresh judges, explicit analysis remediation approval, TDD evidence, explicit-path VCS checkpoints, no unrelated staging, no premature status transition, and no unresolved High/Critical finding.

- [ ] **Step 5: REFACTOR observed loopholes and re-test**

For each new rationalization, add an exact counter to the skill's rationalization table and red flags, then rerun the scenario. Update `skill-validation.md` with raw GREEN output, violations found during refactoring, wording changes, and final pass evidence. Do not proceed until both scenarios pass in fresh contexts.

- [ ] **Step 6: Commit the verified first skill**

```powershell
git add -- .agents/skills/run-bmad-sprint-loop/SKILL.md .agents/skills/run-bmad-sprint-loop/agents/openai.yaml docs/agent-workflow/skill-validation.md
git diff --cached --check
git commit -m "feat(workflow): orchestrate BMad story development"
```

Expected: the first skill is fully validated and forward-tested before Task 5 begins.

### Task 5: Author and forward-test `close-bmad-epic`

**Files:**
- Create: `.agents/skills/close-bmad-epic/SKILL.md` through `init_skill.py`
- Create: `.agents/skills/close-bmad-epic/agents/openai.yaml`
- Modify: `docs/agent-workflow/skill-validation.md`

**Interfaces:**
- Consumes: Verified run skill, epic state, architecture agent, VCS policy, and Task 1 epic-close baseline.
- Produces: The directly invokable, architecture-gated epic completion skill.

- [ ] **Step 1: Initialize the second skill only after Task 4 is committed**

```powershell
$creator = Join-Path $env:USERPROFILE '.codex\skills\.system\skill-creator\scripts\init_skill.py'
python $creator close-bmad-epic --path '.agents/skills' --interface 'display_name=Close BMad Epic' --interface 'short_description=Verify and close a BMad epic safely' --interface 'default_prompt=Use $close-bmad-epic to verify and close the active BMad epic.'
```

- [ ] **Step 2: Write the minimal close skill**

Use this exact frontmatter:

```yaml
---
name: close-bmad-epic
description: Use when every story in a BMad epic appears complete and the epic needs final verification, architecture review, retrospective, and status closure.
---
```

The body must require, in order: all stories `done`; no blocking workflow state; current-commit build/tests; root execution of the `improve-codebase-architecture` epic-close audit profile with `epic-architecture-auditor` as direct-child independent Explore/classification judge; human disposition of every reserved finding; retrospective offer/choice; a provisional epic VCS checkpoint; trusted `done` only after its full-manifest binding; and any selected retrospective afterward in a separate transaction. Include counters for release pressure, “architecture later,” passing-tests substitution, and temporary-report staging.

- [ ] **Step 3: Finalize metadata and validate**

```yaml
interface:
  display_name: "Close BMad Epic"
  short_description: "Verify and close a BMad epic safely"
  default_prompt: "Use $close-bmad-epic to verify and close the active BMad epic."
```

Run `quick_validate.py` against `.agents/skills/close-bmad-epic`. Expected: PASS.

- [ ] **Step 4: Run GREEN and REFACTOR tests**

Repeat Task 1's epic-close scenario in a fresh context with:

```text
Use $close-bmad-epic at .agents/skills/close-bmad-epic to handle this request.
```

Success requires refusing `done` before architecture audit/disposition, excluding the temp HTML report from Git, and committing only accepted epic artifacts. Capture raw output in `skill-validation.md`. If a new rationalization appears, update the skill and rerun until it passes.

- [ ] **Step 5: Commit the verified second skill**

```powershell
git add -- .agents/skills/close-bmad-epic docs/agent-workflow/skill-validation.md
git diff --cached --check
git commit -m "feat(workflow): gate BMad epic completion"
```

### Task 6: Dry-run the complete orchestrator and recovery paths

**Files:**
- Modify: `docs/agent-workflow/skill-validation.md`
- Modify: `docs/agent-workflow/operator-runbook.md` only if dry-run reveals missing instructions
- Test: `.agents/skills/run-bmad-sprint-loop/scripts/tests/test_validate_workflow.py`

**Interfaces:**
- Consumes: Both verified skills, all custom agents, current Epic 4 BMad artifacts, and validation tooling.
- Produces: End-to-end evidence that a user can start, inspect, and resume the workflow without mutating feature code.

- [ ] **Step 1: Validate all static artifacts**

```powershell
python '.agents/skills/run-bmad-sprint-loop/scripts/validate_workflow.py' --repo .
$validator = Join-Path $env:USERPROFILE '.codex\skills\.system\skill-creator\scripts\quick_validate.py'
python $validator '.agents/skills/run-bmad-sprint-loop'
python $validator '.agents/skills/close-bmad-epic'
```

Expected: all three commands PASS.

- [ ] **Step 2: Run a fresh-context story dry run**

Invoke:

```text
Use $run-bmad-sprint-loop for story 4-1 in dry-run mode. Do not mutate files,
statuses, Git history, or feature code. Return the resolved BMad inputs, proposed
SpecKit feature identity, ordered stages, named agents, human gates, expected VCS
checkpoints, E2E decision inputs, and resume key.
```

Expected: story `4-1-codegraph-integration-and-code-unit-extraction`; original SpecKit order; 13-role catalog references only roles needed by the story; analysis remediation human gate; explicit local commit plan; no file or Git mutation.

- [ ] **Step 3: Run queue and recovery dry runs**

Invoke separate fresh contexts for:

```text
Use $run-bmad-sprint-loop for epics 4 and 5 in dry-run mode.
Use $run-bmad-sprint-loop to resume a fixture at plan-review where plan.md changed.
Use $run-bmad-sprint-loop to resume a fixture whose checkpoint SHA is not an ancestor of HEAD.
```

Expected: dependency-ordered one-story units; changed plan invalidates plan-review and all downstream stages; divergent history stops for user direction without reset/rebase/force push.

- [ ] **Step 4: Run verdict/state validation fixtures**

Extend the unittest suite with valid and invalid workflow-state/verdict fixtures covering every stage, `ACCEPT` with a High finding, missing story mapping, invalid E2E skip evidence, and checkpoint IDs. Run:

```powershell
$env:PYTHONPATH = (Resolve-Path '.agents/skills/run-bmad-sprint-loop/scripts')
python -m unittest discover -s '.agents/skills/run-bmad-sprint-loop/scripts/tests' -v
```

Expected: all tests PASS.

- [ ] **Step 5: Record and commit dry-run evidence**

Add exact prompts, returned stage plans, pass/fail decisions, and any corrections to `skill-validation.md`. If the operator contract changed, update `operator-runbook.md` and rerun the affected dry run.

```powershell
git add -- docs/agent-workflow/skill-validation.md docs/agent-workflow/operator-runbook.md .agents/skills/run-bmad-sprint-loop/scripts
git diff --cached --check
git commit -m "test(workflow): verify orchestration dry runs"
```

### Task 7: Run repository-wide verification and publish the handoff

**Files:**
- Modify: `docs/agent-workflow/customization-guide.md` only for verified deviations
- Modify: `docs/agent-workflow/operator-runbook.md` only for verified deviations
- Modify: `docs/superpowers/specs/2026-07-10-bmad-speckit-superpowers-sprint-loop-design.md` to link final implementation artifacts

**Interfaces:**
- Consumes: Complete implemented workflow and all validation evidence.
- Produces: Verified repository state and direct user handoff for story, epic, or multi-epic execution.

- [ ] **Step 1: Run workflow-specific verification**

```powershell
$env:PYTHONPATH = (Resolve-Path '.agents/skills/run-bmad-sprint-loop/scripts')
python -m unittest discover -s '.agents/skills/run-bmad-sprint-loop/scripts/tests' -v
python '.agents/skills/run-bmad-sprint-loop/scripts/validate_workflow.py' --repo .
$validator = Join-Path $env:USERPROFILE '.codex\skills\.system\skill-creator\scripts\quick_validate.py'
python $validator '.agents/skills/run-bmad-sprint-loop'
python $validator '.agents/skills/close-bmad-epic'
```

Expected: every command PASS; investigate any warning instead of counting it as success.

- [ ] **Step 2: Run Vulgata quality gates**

```powershell
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
```

Expected: build succeeds, all tests pass, and the diff check is clean.

- [ ] **Step 3: Audit requirement coverage**

Use the approved design's Success Criteria as a checklist. For each criterion, record the proving file, test, dry-run output, or Git commit in `skill-validation.md`. Missing or indirect evidence is a failure; add the required implementation or test and rerun affected gates.

- [ ] **Step 4: Link the implementation from the design**

Add an `## Implementation` section to the design with links to both skills, `.codex/agents`, `docs/agent-workflow/operator-runbook.md`, and `docs/agent-workflow/skill-validation.md`. Do not restate their contents.

- [ ] **Step 5: Commit the verified handoff**

```powershell
git add -- docs/superpowers/specs/2026-07-10-bmad-speckit-superpowers-sprint-loop-design.md docs/agent-workflow
git diff --cached --check
git commit -m "docs(workflow): publish verified Codex handoff"
git status --short
```

Expected: final worktree clean; no push, merge, rebase, or PR action performed.
