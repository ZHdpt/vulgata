"""Behavioral coverage for explicit human recovery-counter reset support.

Tests enter through the installed transition proposal and transition-head replay
APIs. Synthetic histories retain the prior failures and finite extension so the
assertions cover the new event's place in the existing story history.
"""

import copy
import json
import unittest

from test_plan_review_recovery import (
    EPISODE,
    _append,
    _initial_state,
    _put,
    _record_current_plan_review_acceptance,
    _synthetic_story18_4_history,
)
from decision_policy import recovery_dispatch_decision
from workflow_runtime import canonical_bytes, digest, propose_transition, validate_transition_head


def _append_failure(state, files, owners=("tasks",)):
    count = state["recovery"]["failed_results"] + 1
    previous = copy.deepcopy(state["recovery"]["last_result"])
    correction = _put(files, f"{state['feature_directory']}/evidence/reset-correction-{count}.json", {
        "kind": "analysis",
        "summary": f"Synthetic correction {count} records a changed approach before another check.",
    })
    return _append(files=files, state=state, label=f"reset-failure-{count}", event={
        "kind": "failure",
        "episode_id": EPISODE,
        "owners": list(owners),
        "repair": {
            "obligation": "Retain the current synthetic recovery boundary.",
            "correction": correction,
            "previous_result": previous,
        },
    }, raw_result={
        "kind": "command",
        "stage": "tasks",
        "status": "finished",
        "command": f"synthetic task check {count}",
        "exit_code": 1,
        "summary": f"Synthetic task check {count} reports a correctable failure.",
    })


def _advance_to_failure_count(state, files, target):
    while state["recovery"]["failed_results"] < target:
        state, _ = _append_failure(state, files)
    return state


def _record_finite_extension_to_twelve(state, files):
    decision_id = "synthetic/recovery-extension/12"
    scope = "c" * 64
    evidence_path = f"{state['feature_directory']}/evidence/extension-to-twelve.json"
    decision = {
        "kind": "human-approval",
        "stage": "recovery-extension",
        "status": "accepted",
        "decision_id": decision_id,
        "episode_id": state["recovery"]["episode_id"],
        "scope_sha256": scope,
        "pause_at": 12,
        "summary": "Synthetic human evidence grants one finite extension to twelve.",
    }
    _put(files, evidence_path, decision)
    state["approvals"].append({
        "decision_id": decision_id,
        "status": "accepted",
        "scope_sha256": scope,
        "evidence_path": evidence_path,
        "decided_at": "2026-10-01T00:00:00Z",
    })
    return _append(files=files, state=state, label="extension-to-twelve", event={
        "kind": "extension",
        "episode_id": decision["episode_id"],
        "scope_sha256": scope,
        "decision": {key: decision[key] for key in
                     ("decision_id", "status", "episode_id", "scope_sha256", "pause_at")},
    }, raw_result={
        "kind": "analysis",
        "summary": "The synthetic finite recovery limit is twelve.",
    })[0]


def _prepare_reset_case(*, preserve_holds=False):
    state, files = _synthetic_story18_4_history()
    state = _advance_to_failure_count(state, files, 5)
    state = _record_finite_extension_to_twelve(state, files)
    state = _advance_to_failure_count(state, files, 11)
    if preserve_holds:
        state, _ = _append_failure(state, files, owners=("authority", "blocker"))

    before = copy.deepcopy(state["recovery"])
    previous_head = copy.deepcopy(state["transition_head"])
    boundary = {
        "story_key": state["bmad_story_key"],
        "episode_id": before["episode_id"],
        "count_from": before["failed_results"],
        "reset_to": 0,
        "recovery_before": before,
        "previous_head": previous_head,
    }
    scope = digest(canonical_bytes({"stage": "recovery-counter-reset", **boundary}))
    decision_id = "synthetic/recovery-counter-reset/1"
    decision = {
        "decision_id": decision_id,
        "status": "accepted",
        "scope_sha256": scope,
        **boundary,
    }
    evidence_path = f"{state['feature_directory']}/evidence/human-counter-reset.json"
    _put(files, evidence_path, {
        "kind": "human-approval",
        "stage": "recovery-counter-reset",
        "status": "accepted",
        **decision,
        "user_source": "Synthetic direct human instruction",
        "user_instruction": "Approve. And clear the recovery counter for this story.",
    })
    state["approvals"].append({
        "decision_id": decision_id,
        "status": "accepted",
        "scope_sha256": scope,
        "evidence_path": evidence_path,
        "decided_at": "2026-10-01T00:00:00Z",
    })
    event = {
        "kind": "human_counter_reset",
        "episode_id": before["episode_id"],
        "count_from": before["failed_results"],
        "reset_to": 0,
        "recovery_before": before,
        "previous_head": previous_head,
        "scope_sha256": scope,
        "decision": decision,
    }
    return state, files, before, event


def _apply_reset(state, files, event):
    return _append(files=files, state=state, label="human-counter-reset", event=event, raw_result={
        "kind": "human-direction",
        "status": "accepted",
        "summary": "The user explicitly directed a zero counter reset for this story.",
    })[0]


def _read_reset_approval(state, files):
    ref = next(item for item in state["approvals"]
               if item["decision_id"] == "synthetic/recovery-counter-reset/1")
    return ref, json.loads(files[ref["evidence_path"]].decode("utf-8"))


class HumanRecoveryCounterResetTests(unittest.TestCase):
    def test_explicit_human_reset_clears_count_and_preserves_recovery_and_gate_history(self):
        state, files, before, event = _prepare_reset_case()
        self.assertEqual(before["failed_results"], 11)
        self.assertEqual(before["pause_at"], 12)
        gates_before = copy.deepcopy(state["gates"])
        author_before = copy.deepcopy(state["definition_author"])
        approvals_before = copy.deepcopy(state["approvals"])
        prior_head = copy.deepcopy(state["transition_head"])

        state = _apply_reset(state, files, event)

        self.assertEqual(state["recovery"]["failed_results"], 0)
        self.assertIsNone(state["recovery"]["diagnosis"])
        self.assertEqual(state["recovery"]["episode_id"], before["episode_id"])
        self.assertEqual(state["recovery"]["pause_at"], 12)
        self.assertEqual(state["recovery"]["holds"], before["holds"])
        self.assertEqual(state["recovery"]["last_result"], before["last_result"])
        self.assertEqual(state["recovery"]["earliest_owner"], before["earliest_owner"])
        self.assertEqual(state["recovery"]["closing_gate"], before["closing_gate"])
        self.assertEqual(state["recovery"]["extension_decision_id"], before["extension_decision_id"])
        self.assertEqual(state["definition_author"], author_before)
        self.assertEqual(state["gates"], gates_before)
        self.assertEqual(state["approvals"][:len(approvals_before)], approvals_before)
        self.assertEqual(validate_transition_head(state, files.get), [])

        diagnosis = _put(files, f"{state['feature_directory']}/evidence/diagnosis-at-zero.json", {
            "kind": "analysis",
            "summary": "The fresh zero-count episode boundary is diagnosed before further work.",
        })
        state, _ = _append(files=files, state=state, label="diagnosis-at-zero", event={
            "kind": "diagnosis",
            "owners": ["tasks"],
            "diagnosis": {"at_count": 0, "evidence": diagnosis},
        }, raw_result={"kind": "analysis", "summary": "The reset boundary has a fresh diagnosis."})
        self.assertEqual(state["recovery"]["failed_results"], 0)
        self.assertEqual(state["recovery"]["diagnosis"]["at_count"], 0)

        state, _ = _append_failure(state, files)
        self.assertEqual(state["recovery"]["failed_results"], 1)
        self.assertEqual(state["recovery"]["episode_id"], before["episode_id"])
        record, path = _record_current_plan_review_acceptance(state, files, "after-reset")
        state, complete = propose_transition(state, record, path, files.get)
        files[path] = canonical_bytes(complete)
        self.assertEqual(state["recovery"]["failed_results"], 1)
        self.assertEqual(state["gates"]["plan-review"]["status"], "accepted")
        self.assertEqual(validate_transition_head(state, files.get), [])

    def test_reset_preserves_authority_and_blocker_holds_and_keeps_dispatch_blocked(self):
        state, files, before, event = _prepare_reset_case(preserve_holds=True)
        self.assertEqual(before["failed_results"], 12)
        self.assertEqual(before["holds"], ["authority", "blocked"])
        gates_before = copy.deepcopy(state["gates"])

        state = _apply_reset(state, files, event)

        self.assertEqual(state["recovery"]["failed_results"], 0)
        self.assertEqual(state["recovery"]["holds"], ["authority", "blocked"])
        self.assertEqual(state["recovery"]["action"], "blocked")
        self.assertEqual(state["gates"], gates_before)
        dispatch = recovery_dispatch_decision(state, "implement", "tdd-implementer")
        self.assertFalse(dispatch["allowed"])
        self.assertEqual(dispatch["action"], "blocked")
        self.assertEqual(validate_transition_head(state, files.get), [])

    def test_direct_projection_edit_cannot_replace_an_authenticated_reset(self):
        state, files, _, event = _prepare_reset_case()
        state = _apply_reset(state, files, event)
        altered = copy.deepcopy(state)
        altered["recovery"]["failed_results"] = 1

        errors = validate_transition_head(altered, files.get)

        self.assertTrue(errors, "replay must reject a recovery count that has no transition event")

    def test_reset_rejects_missing_foreign_stale_delegated_or_mutated_authority(self):
        def remove_decision(state, files, event):
            event.pop("decision")

        def mark_delegated(state, files, event):
            ref, proof = _read_reset_approval(state, files)
            proof.update(kind="delegated-approval", delegate_id="synthetic-delegate")
            _put(files, ref["evidence_path"], proof)

        def relabel_delegated_as_human(state, files, event):
            ref, proof = _read_reset_approval(state, files)
            proof["delegate_id"] = "synthetic-delegate"
            proof["kind"] = "human-approval"
            _put(files, ref["evidence_path"], proof)

        def bind_foreign_story(state, files, event):
            ref, proof = _read_reset_approval(state, files)
            foreign_key = "18-99-foreign-story"
            proof["story_key"] = foreign_key
            event["decision"]["story_key"] = foreign_key
            _put(files, ref["evidence_path"], proof)

        def bind_stale_head(state, files, event):
            ref, proof = _read_reset_approval(state, files)
            stale = {"path": f"{state['feature_directory']}/evidence/transition-000.json",
                     "sha256": "0" * 64}
            proof["previous_head"] = stale
            event["previous_head"] = stale
            event["decision"]["previous_head"] = stale
            _put(files, ref["evidence_path"], proof)

        def change_event_scope(state, files, event):
            event["scope_sha256"] = "f" * 64

        def change_recovery_preimage(state, files, event):
            event["recovery_before"]["failed_results"] -= 1

        def omit_user_source(state, files, event):
            ref, proof = _read_reset_approval(state, files)
            proof["user_source"] = " "
            _put(files, ref["evidence_path"], proof)

        invalid_authorities = (
            ("missing decision", remove_decision),
            ("delegated decision", mark_delegated),
            ("delegated decision relabeled human", relabel_delegated_as_human),
            ("foreign story", bind_foreign_story),
            ("stale head", bind_stale_head),
            ("changed scope", change_event_scope),
            ("changed recovery preimage", change_recovery_preimage),
            ("missing actual user source", omit_user_source),
        )
        for label, mutate in invalid_authorities:
            with self.subTest(label=label):
                state, files, _, event = _prepare_reset_case()
                mutate(state, files, event)
                with self.assertRaises(ValueError):
                    _apply_reset(state, files, event)

    def test_reset_rejects_nonzero_or_boolean_targets(self):
        for target in (1, True):
            with self.subTest(reset_to=target):
                state, files, _, event = _prepare_reset_case()
                event["reset_to"] = target
                with self.assertRaises(ValueError):
                    _apply_reset(state, files, event)

    def test_ordinary_phase_completion_does_not_reset_a_nonzero_count(self):
        files = {}
        state = _initial_state(files)
        state, _ = _append(files=files, state=state, label="ordinary-first-failure", event={
            "kind": "failure",
            "episode_id": "synthetic-ordinary-events",
            "owners": ["tasks"],
        }, raw_result={
            "kind": "command", "stage": "tasks", "status": "finished",
            "command": "synthetic first task check", "exit_code": 1,
            "summary": "The first synthetic check needs correction.",
        })
        task_path = f"{state['feature_directory']}/tasks.md"
        task_bytes = b"# Synthetic tasks\n\n- [x] T001 Keep phase completion scoped.\n"
        files[task_path] = task_bytes
        state["artifacts"]["tasks"] = {"path": task_path, "sha256": digest(task_bytes)}
        state, _ = _append(files=files, state=state, label="ordinary-phase-complete", event={
            "kind": "phase_complete", "phase": "tasks",
        }, raw_result={
            "kind": "self-review", "stage": "tasks", "outcome": "passed",
            "summary": "The retained author completed the synthetic task phase.",
            "input_hashes": {task_path: digest(task_bytes)},
        })

        self.assertEqual(state["recovery"]["failed_results"], 1)
        self.assertEqual(state["recovery"]["action"], "proceed")
        self.assertEqual(validate_transition_head(state, files.get), [])
