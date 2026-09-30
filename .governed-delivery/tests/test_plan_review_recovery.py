"""Regression tests for the installed plan-review recovery dispatch boundary.

The accepted Story 18.4 repair is intentionally narrow: after the retained
definition author has completed a fresh corrected architecture plan, its
independent plan review must run before tasks even though phase completion
advances the recovery owner to tasks. The tests use the installed transition
and dispatch APIs against an isolated synthetic story, never the live story
state or a private implementation hook.
"""

import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPTS_ROOT = REPOSITORY_ROOT / ".agents" / "skills" / "run-governed-delivery" / "scripts"
sys.path.insert(0, str(SCRIPTS_ROOT))

from decision_policy import recovery_dispatch_decision
from workflow_runtime import canonical_bytes, digest, projection, propose_transition


FEATURE = "specs/synthetic-plan-review-recovery"
STATE_PATH = f"{FEATURE}/workflow-state.json"
STORY_KEY = "18-4-synthetic-plan-review-recovery"
EPISODE = "18-4-synthetic-recovery-01"


def _put(files, path, value):
    raw = canonical_bytes(value)
    files[path] = raw
    return {"path": path, "sha256": digest(raw)}


def _plan_bytes():
    return b"""---
governed_delivery_classification: architecture
governed_delivery_reason: Synthetic fixture exercises the accepted plan-review recovery boundary.
---
# Plan

## Implementation Design

### Responsibilities
- **ID-R01**: Keep plan review independent from task authoring.

### Test Seams
- **ID-T01**: Observe recovery dispatch through the installed workflow validator.

### Alternatives
- **ID-X01**: Preserve task authorization as a separate gate.

### Consequences
- **ID-C01**: A fresh corrected architecture plan is reviewed before tasks.
"""


def _initial_state(files):
    spec_path = f"{FEATURE}/spec.md"
    plan_path = f"{FEATURE}/plan.md"
    files[spec_path] = b"# Synthetic recovery story\n"
    files[plan_path] = _plan_bytes()
    gate = {"status": "pending", "attempt": 0, "verdict_path": None}
    sha = "a" * 40
    state = {
        "schema_version": "3.1",
        "bmad_story_key": STORY_KEY,
        "bmad_epic_key": "18",
        "feature_directory": FEATURE,
        "stage": "plan-review",
        "preflight": {"branch": "feature/synthetic-recovery", "head": sha,
                      "git_identity_sha256": "b" * 64, "status_sha256": "c" * 64,
                      "observed_at": "2026-09-30T00:00:00Z"},
        "artifacts": {
            "spec": {"path": spec_path, "sha256": digest(files[spec_path])},
            "plan": {"path": plan_path, "sha256": digest(files[plan_path])},
            "tasks": None,
        },
        "gates": {name: copy.deepcopy(gate) for name in
                  ("plan-review", "definition-review", "quality-review")},
        "approvals": [],
        "convergence": {"status": "pending", "attempt": 0, "outcome": "pending", "report_path": None},
        "e2e": {"result": "pending", "decision_path": None},
        "current_evidence_paths": [],
        "checkpoint": None,
        "resume": {"stage": "plan-review", "reason": "Recover the corrected plan boundary.", "evidence_path": None},
        "updated_at": "2026-09-30T00:00:00Z",
        "recovery": None,
        "definition_author": {"profile": "definition-author", "escalation_decision_id": None},
        "lookback": {"status": "pending", "evidence": None},
    }
    transition_path = f"{FEATURE}/evidence/transition-001-activation.json"
    activation = {"kind": "analysis", "transition": {
        "story_key": STORY_KEY, "sequence": 1, "previous_head": None,
        "event": {"kind": "activation"}, "projection": projection(state),
    }}
    files[transition_path] = canonical_bytes(activation)
    state["transition_head"] = {"path": transition_path, "sha256": digest(files[transition_path])}
    return state


def _next_record(state, files, event):
    head = json.loads(files[state["transition_head"]["path"]])
    sequence = head["transition"]["sequence"] + 1
    path = f"{FEATURE}/evidence/transition-{sequence:03d}-{event['kind']}.json"
    record = {"kind": "analysis", "transition": {
        "story_key": STORY_KEY, "sequence": sequence,
        "previous_head": copy.deepcopy(state["transition_head"]), "event": event,
    }}
    return record, path


def _append(state, files, label, event, raw_result):
    sequence = json.loads(files[state["transition_head"]["path"]])["transition"]["sequence"] + 1
    result = _put(files, f"{FEATURE}/evidence/{sequence:03d}-{label}.json", {
        "observed_after": copy.deepcopy(state["transition_head"]), "raw_result": raw_result,
    })
    record, path = _next_record(state, files, {**event, "result": result})
    state, complete = propose_transition(state, record, path, files.get)
    files[path] = canonical_bytes(complete)
    return state, result


def _synthetic_story18_4_history():
    """Replay failure/extension/diagnosis/escalation/invalidation/fresh-review order."""
    files = {}
    state = _initial_state(files)

    # Three failures put the episode at its default diagnosis threshold. Each
    # repeated failure cites a distinct negative check and the prior receipt.
    previous = None
    for count in range(1, 4):
        result_raw = {"kind": "command", "stage": "plan", "status": "finished",
                      "command": f"synthetic plan check {count}", "exit_code": 1,
                      "summary": f"Synthetic plan review input {count} needs correction."}
        event = {"kind": "failure", "episode_id": EPISODE, "owners": ["plan"]}
        if previous is not None:
            correction = _put(files, f"{FEATURE}/evidence/correction-{count}.json", {
                "kind": "analysis", "summary": "A bounded correction was made before the next check."})
            event["repair"] = {"obligation": "Correct the observed plan issue.",
                               "correction": correction, "previous_result": previous}
        state, previous = _append(state, files, f"failure-{count}", event, result_raw)

    diagnosis = _put(files, f"{FEATURE}/evidence/diagnosis-03.json", {
        "kind": "analysis", "summary": "The remaining issue needs retained architecture author reasoning."})
    state, _ = _append(state, files, "diagnosis-03", {
        "kind": "diagnosis", "owners": ["plan"],
        "diagnosis": {"at_count": 3, "evidence": diagnosis},
    }, {"kind": "analysis", "summary": "The count-three failure is diagnosed."})

    # The fourth negative result reaches the original pause boundary. The
    # scoped human extension is explicit and finite; no count is reset.
    correction = _put(files, f"{FEATURE}/evidence/correction-4.json", {
        "kind": "analysis", "summary": "A changed approach is ready for one bounded check."})
    state, last_result = _append(state, files, "failure-4", {
        "kind": "failure", "episode_id": EPISODE, "owners": ["plan"],
        "repair": {"obligation": "Apply the diagnosed plan correction.",
                   "correction": correction, "previous_result": previous},
    }, {"kind": "command", "stage": "plan", "status": "finished",
        "command": "synthetic plan check 4", "exit_code": 1,
        "summary": "The corrected architecture plan still requires independent review."})
    decision_id = "synthetic/recovery-extension/1"
    scope = "d" * 64
    approval_path = f"{FEATURE}/evidence/extension-decision.json"
    decision_record = {"kind": "human-direction", "status": "accepted",
                       "decision_id": decision_id, "episode_id": EPISODE,
                       "scope_sha256": scope, "pause_at": 5,
                       "summary": "Authorize one finite recovery extension."}
    _put(files, approval_path, decision_record)
    state["approvals"] = [{"decision_id": decision_id, "status": "accepted",
                           "scope_sha256": scope, "evidence_path": approval_path,
                           "decided_at": "2026-09-30T00:00:00Z"}]
    state, _ = _append(state, files, "extension", {
        "kind": "extension", "episode_id": EPISODE, "scope_sha256": scope,
        "decision": {key: decision_record[key] for key in
                     ("decision_id", "status", "episode_id", "scope_sha256", "pause_at")},
    }, {"kind": "analysis", "summary": "The finite extension is recorded."})

    diagnosis = _put(files, f"{FEATURE}/evidence/diagnosis-04.json", {
        "kind": "analysis", "summary": "The bounded escalation trigger is confirmed."})
    state, _ = _append(state, files, "diagnosis-04", {
        "kind": "diagnosis", "owners": ["plan"],
        "diagnosis": {"at_count": 4, "evidence": diagnosis},
    }, {"kind": "analysis", "summary": "The count-four recovery is diagnosed."})
    escalation_id = "synthetic/definition-escalation/1"
    escalation_scope = "e" * 64
    escalation_evidence = _put(files, f"{FEATURE}/evidence/escalation-decision.json", {
        "kind": "analysis", "decision_id": escalation_id, "episode_id": EPISODE,
        "trigger": "unresolved_definition_reasoning", "scope_sha256": escalation_scope,
        "summary": "Retain the recovery definition-author profile for plan reasoning."})
    state, _ = _append(state, files, "escalation", {
        "kind": "escalation", "episode_id": EPISODE, "profile": "definition-author-recovery",
        "scope_sha256": escalation_scope,
        "decision": {"decision_id": escalation_id, "episode_id": EPISODE,
                     "trigger": "unresolved_definition_reasoning", "scope_sha256": escalation_scope,
                     "evidence": escalation_evidence},
    }, {"kind": "analysis", "summary": "The medium recovery profile is selected."})

    def finish_plan(label):
        self_review = {"kind": "self-review", "stage": "plan", "outcome": "passed",
                       "summary": "The corrected architecture plan has fresh author self-review.",
                       "input_hashes": {state["artifacts"]["plan"]["path"]:
                                        state["artifacts"]["plan"]["sha256"]}}
        state_result = _put(files, f"{FEATURE}/evidence/{label}.json", {
            "observed_after": copy.deepcopy(state["transition_head"]), "raw_result": self_review,
        })
        record, path = _next_record(state, files, {"kind": "phase_complete", "phase": "plan",
                                                    "result": state_result})
        updated, complete = propose_transition(state, record, path, files.get)
        files[path] = canonical_bytes(complete)
        return updated

    state = finish_plan("plan-self-review-1")
    # Material plan invalidation resets diagnosis and returns ownership to the
    # plan; the next fresh diagnosis and self-review move ownership to tasks.
    state, _ = _append(state, files, "plan-invalidation", {
        "kind": "invalidation", "invalidation_policy": "earliest-owner-v1", "owner": "plan",
    }, {"kind": "human-direction", "status": "accepted",
        "summary": "A new independent review finding invalidates the plan boundary."})
    diagnosis = _put(files, f"{FEATURE}/evidence/diagnosis-after-invalidation.json", {
        "kind": "analysis", "summary": "The revised plan boundary is freshly diagnosed."})
    state, _ = _append(state, files, "diagnosis-after-invalidation", {
        "kind": "diagnosis", "owners": ["plan"],
        "diagnosis": {"at_count": 4, "evidence": diagnosis},
    }, {"kind": "analysis", "summary": "The invalidated plan boundary is diagnosed."})
    state = finish_plan("plan-self-review-2")
    state, _ = _append(state, files, "technical-failure", {
        "kind": "technical_failure",
    }, {"kind": "analysis", "status": "blocked",
        "summary": "The targeted dispatch check found a workflow routing deadlock."})

    # The earlier independent plan review is current-revised, so the next
    # required dispatch is the reviewer, not tasks.
    revise = _put(files, f"{FEATURE}/evidence/initial-plan-review-revise.json", {
        "observed_after": copy.deepcopy(state["transition_head"]),
        "raw_result": {"schema_version": "1.0", "gate": "plan-review", "verdict": "REVISE",
                       "reviewed_commit": "a" * 40,
                       "reviewed_boundary": {"base_commit": "a" * 40, "candidate_commit": None,
                                             "diff_sha256": "f" * 64,
                                             "artifact_hashes": {item["path"]: item["sha256"]
                                                                 for item in state["artifacts"].values()
                                                                 if isinstance(item, dict)},
                                             "owned_paths": [item["path"] for item in state["artifacts"].values()
                                                             if isinstance(item, dict)]},
                       "reviewed_artifacts": [state["artifacts"][name]["path"] for name in ("spec", "plan")],
                       "findings": [{"id": "PR-001", "severity": "HIGH",
                                     "category": "underspecification",
                                     "summary": "Correct the plan before independent acceptance."}],
                       "summary": "The initial plan requires bounded correction."},
    })
    state["gates"]["plan-review"] = {"status": "revised", "attempt": 1,
                                       "verdict_path": revise["path"]}
    return state, files


def _postproduction_recovery(state, files):
    """Advance the synthetic recovery through the public production-start event."""
    state, _ = _append(state, files, "production-started", {
        "kind": "production_started",
    }, {"kind": "analysis", "summary": "The accepted definition has entered implementation."})
    tasks_path = f"{FEATURE}/tasks.md"
    files[tasks_path] = (
        b"# Tasks\n\n"
        b"- [ ] T001 [ID-R01] Continue the synthetic accepted implementation.\n"
        b"- [ ] T002 [ID-T01] Keep the accepted recovery dispatch observable.\n"
    )
    state["artifacts"]["tasks"] = {"path": tasks_path, "sha256": digest(files[tasks_path])}
    state["stage"] = "implement"
    state["resume"] = {"stage": "implement", "reason": "Continue the open production recovery.",
                        "evidence_path": None}
    return state


def _record_current_plan_review_acceptance(state, files, suffix):
    artifact_hashes = {
        state["artifacts"][name]["path"]: state["artifacts"][name]["sha256"]
        for name in ("spec", "plan")
    }
    owned_paths = list(artifact_hashes)
    result = _put(files, f"{FEATURE}/evidence/current-plan-review-{suffix}.json", {
        "observed_after": copy.deepcopy(state["transition_head"]),
        "raw_result": {
            "schema_version": "1.0",
            "gate": "plan-review",
            "verdict": "ACCEPT",
            "reviewed_commit": state["preflight"]["head"],
            "reviewed_boundary": {
                "base_commit": state["preflight"]["head"],
                "candidate_commit": None,
                "diff_sha256": "f" * 64,
                "artifact_hashes": artifact_hashes,
                "owned_paths": owned_paths,
            },
            "reviewed_artifacts": owned_paths,
            "findings": [],
            "summary": "The independently reviewed corrected plan is accepted.",
        },
    })
    record, path = _next_record(state, files, {"kind": "accepted", "result": result})
    return record, path


def _run_dispatch(state, files, *, alter=None):
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        (root / ".governed-delivery").mkdir(parents=True)
        (root / ".codex" / "agents").mkdir(parents=True)
        (root / ".governed-delivery" / "config.toml").write_bytes(
            (REPOSITORY_ROOT / ".governed-delivery" / "config.toml").read_bytes())
        (root / ".codex" / "agents" / "definition-reviewer.toml").write_bytes(
            (REPOSITORY_ROOT / ".codex" / "agents" / "definition-reviewer.toml").read_bytes())
        (root / STATE_PATH).parent.mkdir(parents=True, exist_ok=True)
        (root / STATE_PATH).write_bytes(canonical_bytes(state))
        for relative, raw in files.items():
            destination = root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(raw)
        if alter:
            alter(root)
        return subprocess.run([
            sys.executable, str(SCRIPTS_ROOT / "validate_workflow.py"),
            "--repo", str(root), "--story-state", STATE_PATH,
            "--next-stage", "plan-review", "--next-profile", "definition-reviewer", "--json",
        ], capture_output=True, text=True, check=False)


class PlanReviewRecoveryTests(unittest.TestCase):
    def test_fresh_corrected_architecture_plan_can_dispatch_review_before_tasks(self):
        state, files = _synthetic_story18_4_history()

        decision = recovery_dispatch_decision(
            state, "plan-review", "definition-reviewer", plan_classification="architecture")
        dispatch = _run_dispatch(state, files)

        self.assertEqual(0, dispatch.returncode, dispatch.stdout + dispatch.stderr)
        self.assertTrue(decision["allowed"], f"recovery API rejected the required reviewer dispatch: {decision}")

    def test_fresh_corrected_architecture_plan_can_dispatch_review_in_open_production_recovery(self):
        state, files = _synthetic_story18_4_history()
        state = _postproduction_recovery(state, files)

        self.assertEqual("implement", state["stage"])
        self.assertEqual("verify", state["recovery"]["closing_gate"])
        self.assertEqual("tasks", state["recovery"]["earliest_owner"])
        self.assertEqual(4, state["recovery"]["failed_results"])
        self.assertEqual(5, state["recovery"]["pause_at"])
        self.assertEqual([], state["recovery"]["holds"])

        decision = recovery_dispatch_decision(
            state, "plan-review", "definition-reviewer", plan_classification="architecture")
        dispatch = _run_dispatch(state, files)

        self.assertTrue(decision["allowed"], f"recovery API rejected the post-production reviewer dispatch: {decision}")
        self.assertEqual(0, dispatch.returncode, dispatch.stdout + dispatch.stderr)

    def test_independent_plan_acceptance_records_after_corrected_plan_in_both_recovery_modes(self):
        for mode in ("definition-review", "verify"):
            with self.subTest(closing_gate=mode):
                state, files = _synthetic_story18_4_history()
                if mode == "verify":
                    state = _postproduction_recovery(state, files)
                else:
                    self.assertEqual("definition-review", state["recovery"]["closing_gate"])
                self.assertEqual("tasks", state["recovery"]["earliest_owner"])
                self.assertEqual("correct", state["recovery"]["action"])

                recovery_before = copy.deepcopy(state["recovery"])
                approvals_before = copy.deepcopy(state["approvals"])
                tasks_before = copy.deepcopy(state["artifacts"]["tasks"])
                unrelated_gates_before = {
                    name: copy.deepcopy(state["gates"][name])
                    for name in ("definition-review", "quality-review")
                }
                record, path = _record_current_plan_review_acceptance(state, files, mode)

                accepted, completed_record = propose_transition(state, record, path, files.get)

                self.assertEqual("accepted", accepted["gates"]["plan-review"]["status"])
                self.assertEqual(record["transition"]["event"]["result"]["path"],
                                 accepted["gates"]["plan-review"]["verdict_path"])
                for field in ("episode_id", "failed_results", "pause_at", "closing_gate",
                              "earliest_owner", "holds", "last_result", "diagnosis",
                              "extension_decision_id"):
                    self.assertEqual(recovery_before[field], accepted["recovery"][field], field)
                self.assertEqual("proceed", accepted["recovery"]["action"])
                self.assertEqual(approvals_before, accepted["approvals"])
                self.assertEqual(tasks_before, accepted["artifacts"]["tasks"])
                for name, gate in unrelated_gates_before.items():
                    self.assertEqual(gate, accepted["gates"][name], name)
                self.assertEqual("accepted", completed_record["transition"]["event"]["kind"])

    def test_plan_review_recovery_keeps_freshness_roles_and_restrictions(self):
        state, files = _synthetic_story18_4_history()

        wrong_role = recovery_dispatch_decision(
            state, "plan-review", "tdd-implementer", plan_classification="architecture")
        held = copy.deepcopy(state)
        held["recovery"].update(action="blocked", holds=["blocked"])
        hold_result = recovery_dispatch_decision(
            held, "plan-review", "definition-reviewer", plan_classification="architecture")
        paused = copy.deepcopy(state)
        paused["recovery"].update(action="pause", failed_results=5, pause_at=5)
        pause_result = recovery_dispatch_decision(
            paused, "plan-review", "definition-reviewer", plan_classification="architecture")
        stale_plan = _run_dispatch(state, files, alter=lambda root: (root / FEATURE / "plan.md").write_bytes(b"# stale plan\n"))

        self.assertFalse(wrong_role["allowed"], wrong_role)
        self.assertFalse(hold_result["allowed"], hold_result)
        self.assertFalse(pause_result["allowed"], pause_result)
        self.assertNotEqual(0, stale_plan.returncode, stale_plan.stdout + stale_plan.stderr)

    def test_plan_self_review_from_before_invalidation_cannot_be_replayed_as_current(self):
        state, files = _synthetic_story18_4_history()
        old_review_path = min(
            (json.loads(raw)["transition"]["sequence"],
             json.loads(raw)["transition"]["event"]["result"]["path"])
            for path, raw in files.items()
            if "/transition-" in path
            if json.loads(raw).get("transition", {}).get("event", {}).get("kind") == "phase_complete"
        )[1]
        record, path = _next_record(state, files, {
            "kind": "phase_complete", "phase": "plan",
            "result": _reference(files, old_review_path),
        })

        with self.assertRaisesRegex(ValueError, "observed after"):
            propose_transition(state, record, path, files.get)


def _reference(files, path):
    return {"path": path, "sha256": digest(files[path])}


if __name__ == "__main__":
    unittest.main()
