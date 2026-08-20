import copy
import hashlib
import json
import shutil
import tempfile
import tomllib
import unittest
from pathlib import Path

from validate_workflow import (
    validate_agent_file,
    validate_epic_command_evidence,
    validate_epic_gate,
    validate_repository,
    validate_retrospective_state,
    validate_verdict,
    validate_workflow_state,
)


SHA = "a" * 40
HASH = "b" * 64
BOUNDARY = {
    "base_commit": SHA,
    "candidate_commit": None,
    "diff_sha256": HASH,
    "artifact_hashes": {"specs/001-example/spec.md": HASH},
    "owned_paths": ["specs/001-example/spec.md"],
}


def valid_verdict():
    return {
        "schema_version": "1.0",
        "gate": "definition-review",
        "verdict": "ACCEPT",
        "reviewed_commit": SHA,
        "reviewed_boundary": copy.deepcopy(BOUNDARY),
        "reviewed_artifacts": ["specs/001-example/plan.md"],
        "findings": [],
        "summary": "accepted",
    }


def valid_state():
    gate = {"status": "pending", "attempt": 0, "verdict_path": None}
    return {
        "schema_version": "3.0",
        "bmad_story_key": "4-1-example",
        "bmad_epic_key": "4",
        "feature_directory": "specs/001-example",
        "stage": "context",
        "preflight": {
            "branch": "feature/example",
            "head": SHA,
            "git_identity_sha256": HASH,
            "status_sha256": HASH,
            "observed_at": "2026-07-12T00:00:00Z",
        },
        "artifacts": {"spec": None, "plan": None, "tasks": None},
        "gates": {
            "plan-review": copy.deepcopy(gate),
            "definition-review": copy.deepcopy(gate),
            "quality-review": copy.deepcopy(gate),
        },
        "attempts": {"definition-review": 0, "converge": 0, "quality-review": 0},
        "approvals": [],
        "convergence": {
            "status": "pending", "attempt": 0, "outcome": "pending", "report_path": None,
        },
        "e2e": {"result": "pending", "decision_path": None},
        "current_evidence_paths": [],
        "checkpoint": None,
        "resume": {"stage": "context", "reason": "Begin story context synthesis.", "evidence_path": None},
        "updated_at": "2026-07-12T00:00:00Z",
    }


class WorkflowValidationTests(unittest.TestCase):
    REPOSITORY_ROOT = Path(__file__).resolve().parents[5]
    PROFILES = {
        "bmad-sprint-orchestrator": ("gpt-5.6-sol", "medium", "workspace-write"),
        "story-context-synthesizer": ("gpt-5.6-luna", "high", "read-only"),
        "spec-author": ("gpt-5.6-sol", "medium", "workspace-write"),
        "plan-architect": ("gpt-5.6-sol", "medium", "workspace-write"),
        "plan-architect-high": ("gpt-5.6-sol", "high", "workspace-write"),
        "task-author": ("gpt-5.6-luna", "medium", "workspace-write"),
        "definition-reviewer": ("gpt-5.6-sol", "medium", "read-only"),
        "tdd-test-author": ("gpt-5.6-luna", "high", "workspace-write"),
        "tdd-implementer": ("gpt-5.6-sol", "medium", "workspace-write"),
        "convergence-analyst": ("gpt-5.6-luna", "high", "workspace-write"),
        "quality-reviewer": ("gpt-5.6-sol", "medium", "read-only"),
        "e2e-runner": ("gpt-5.6-luna", "high", "workspace-write"),
        "epic-architecture-auditor": ("gpt-5.6-sol", "high", "read-only"),
    }
    INSTRUCTION_MARKERS = {
        "bmad-sprint-orchestrator": ("load and follow run-bmad-sprint-loop", "never implement feature code", "you alone"),
        "story-context-synthesizer": ("separate sourced facts from inference", "cite repository paths"),
        "spec-author": ("speckit-specify", "self-review", "measurable acceptance"),
        "plan-architect": ("speckit-plan", "self-review", "architecture-decision classification"),
        "plan-architect-high": ("speckit-plan", "self-review", "architecture-decision trigger"),
        "task-author": ("speckit-tasks", "self-review", "source-reference coverage"),
        "definition-reviewer": ("speckit-analyze", "earliest_owner", "never edit"),
        "tdd-test-author": ("expected red", "test code and test evidence only", "never edit production code"),
        "tdd-implementer": ("red evidence", "minimal change", "do not write or weaken tests"),
        "convergence-analyst": ("speckit-converge", "append-only", "never edit spec"),
        "quality-reviewer": ("converged immutable boundary", "skip style enforced by tooling", "cite documented standards"),
        "e2e-runner": ("test files and test evidence only", "never change production code"),
            "epic-architecture-auditor": (
                "independent explore/classification judge", "canonical absolute audit root", "git common/worktree identities",
                "raw_candidate_id", "all applicable categories", "pinned blob oid", "excerpt sha-256",
                "do not write or open the html report", "never implement",
            ),
    }

    def test_verdict_accepts_complete_contract(self):
        self.assertEqual([], validate_verdict(valid_verdict()))

    def test_verdict_rejects_bad_boundary_finding_and_revision_semantics(self):
        verdict = valid_verdict()
        verdict.update(schema_version="2", gate="wrong", verdict="REVISE", reviewed_commit="short")
        verdict["reviewed_boundary"] = {"base_commit": "bad", "candidate_commit": 4}
        verdict["reviewed_artifacts"] = ["../escape"]
        verdict["findings"] = [{"id": "x", "severity": "MEDIUM"}]
        errors = validate_verdict(verdict)
        for expected in ["schema_version", "gate", "reviewed_commit", "reviewed_boundary.diff_sha256",
                         "findings[0].category", "REVISE verdict requires blocking findings", "reviewed_artifacts[0]"]:
            self.assertTrue(any(expected in error for error in errors), (expected, errors))

    def test_verdict_wrong_types_never_crash(self):
        errors = validate_verdict({"schema_version": [], "findings": "bad", "reviewed_boundary": []})
        self.assertIn("findings must be an array", errors)
        self.assertTrue(any("reviewed_boundary" in error for error in errors))

    def test_workflow_state_accepts_complete_contract(self):
        self.assertEqual([], validate_workflow_state(valid_state()))

    def test_workflow_state_is_a_bounded_current_snapshot(self):
        state = valid_state()
        state["current_evidence_paths"] = [f"specs/001-example/evidence/{index:02d}.json" for index in range(13)]
        self.assertTrue(any("at most 12" in error for error in validate_workflow_state(state)))
        state = valid_state()
        state["resume"]["reason"] = "x" * 13000
        self.assertTrue(any("exceeds 12288 bytes" in error for error in validate_workflow_state(state)))

    def test_workflow_state_requires_compact_v3_and_rejects_removed_ledger_fields(self):
        state = valid_state()
        state["schema_version"] = "2.0"
        state["stage_history"] = []
        errors = validate_workflow_state(state)
        self.assertTrue(any("schema_version must equal 3.0" in error for error in errors), errors)
        self.assertTrue(any("stage_history is not allowed" in error for error in errors), errors)

    def test_convergence_outcomes_are_semantically_bound(self):
        state = valid_state()
        state["convergence"].update(status="accepted", attempt=1, outcome="converged",
                                    report_path="specs/001-example/reviews/converge.md")
        self.assertEqual([], validate_workflow_state(state))
        state["convergence"].update(status="revised", outcome="tasks_appended")
        self.assertEqual([], validate_workflow_state(state))
        state["convergence"]["report_path"] = None
        errors = validate_workflow_state(state)
        self.assertTrue(any("tasks_appended outcome" in error for error in errors), errors)

    def test_sprint_loop_skill_defines_unified_convergent_flow(self):
        skill = (
            self.REPOSITORY_ROOT / ".agents/skills/run-bmad-sprint-loop/SKILL.md"
        ).read_text(encoding="utf-8").lower()

        for marker in [
            "## unified story flow",
            "speckit-specify + self-review",
            "speckit-plan + self-review",
            "speckit-tasks + self-review",
            "independent definition-review",
            "speckit-converge <-> implementation until converged",
            "implementation approval",
            "fourth consecutive `revise`",
            "tdd-test-author",
            "gpt-5.6-luna",
        ]:
            self.assertIn(marker, skill)
        self.assertNotIn("workflow-state-v1", skill)
        self.assertNotIn("superpowers controls implementation discipline", skill)

    def test_planned_change_review_remains_before_tasks(self):
        skill = (
            self.REPOSITORY_ROOT / ".agents/skills/run-bmad-sprint-loop/SKILL.md"
        ).read_text(encoding="utf-8").lower()
        early_review = skill.index("planned-change trigger")
        before_tasks = skill.index("before task generation", early_review)
        task_dispatch = skill.index("after tasks, dispatch", before_tasks)
        self.assertLess(early_review, before_tasks)
        self.assertLess(before_tasks, task_dispatch)

    def test_approval_policy_is_informed_and_avoids_routine_revise_ceremony(self):
        skill = (
            self.REPOSITORY_ROOT / ".agents/skills/run-bmad-sprint-loop/SKILL.md"
        ).read_text(encoding="utf-8").lower()
        decision_contract = (
            self.REPOSITORY_ROOT / "docs/agent-workflow/review-and-correction.md"
        ).read_text(encoding="utf-8").lower()
        for marker in [
            "without asking the user to approve ordinary correction",
            "mandatory pre-implementation approval",
            "contradiction with a prior user-approved decision or higher authority",
            "new architecture decision/change outside the accepted plan",
            "fourth consecutive `revise`",
            "hard blocker",
        ]:
            self.assertIn(marker, skill)
        for marker in [
            "topic and exact decision",
            "why user judgment is required",
            "viable options",
            "recommendation",
            "design concepts",
            "exact authorized scope",
            "downstream gates",
        ]:
            self.assertIn(marker, decision_contract)


    def test_every_nonterminal_stage_accepts_a_well_formed_active_record(self):
        stages = ["context", "specify", "plan", "plan-review", "tasks", "definition-review",
                  "implementation-approval", "implement", "converge", "quality-review",
                  "conditional-e2e", "verify"]
        for stage in stages:
            with self.subTest(stage=stage):
                state = valid_state()
                state["stage"] = stage
                state["resume"]["stage"] = stage
                if stage not in {"context", "specify"}:
                    state["artifacts"]["spec"] = {"path": "specs/001-example/spec.md", "sha256": HASH}
                if stage not in {"context", "specify", "plan", "plan-review"}:
                    state["artifacts"]["plan"] = {"path": "specs/001-example/plan.md", "sha256": HASH}
                if stage in {"definition-review", "implementation-approval", "implement", "converge",
                             "quality-review", "conditional-e2e", "verify"}:
                    state["artifacts"]["tasks"] = {"path": "specs/001-example/tasks.md", "sha256": HASH}
                self.assertEqual([], validate_workflow_state(state))

    def test_accept_with_high_finding_is_rejected(self):
        verdict = valid_verdict()
        verdict["findings"] = [{
            "id": "high-1", "severity": "HIGH", "category": "correctness",
            "artifact": "src/A.cs", "location": "line 1", "affected_requirement": "FR-001",
            "evidence": "src/A.cs:1", "correction_outcome": "Correct behavior", "earliest_owner": "implement",
        }]
        errors = validate_verdict(verdict)
        self.assertTrue(any("ACCEPT" in error and "blocking" in error for error in errors))

    def test_missing_story_mapping_is_rejected(self):
        state = valid_state()
        state["bmad_story_key"] = ""
        state["feature_directory"] = ""
        errors = validate_workflow_state(state)
        self.assertTrue(any("bmad_story_key" in error for error in errors))
        self.assertTrue(any("feature_directory" in error for error in errors))

    def test_workflow_state_enforces_nested_safety_contracts(self):
        state = valid_state()
        state["bmad_story_key"] = ""
        state["feature_directory"] = "C:/absolute"
        del state["gates"]["quality-review"]
        state["preflight"]["head"] = "bad"
        state["attempts"]["definition-review"] = -1
        state["e2e"]["result"] = "magic"
        state["current_evidence_paths"] = ["../escape"]
        errors = validate_workflow_state(state)
        for expected in ["bmad_story_key", "feature_directory", "gates.quality-review",
                         "preflight.head", "attempts.definition-review", "e2e.result",
                         "current_evidence_paths[0]"]:
            self.assertTrue(any(expected in error for error in errors), (expected, errors))

    def test_complete_state_requires_checkpoint_binding_semantics(self):
        state = valid_state()
        state["stage"] = "complete"
        state["resume"] = None
        state["artifacts"] = {
            "spec": {"path": "specs/001-example/spec.md", "sha256": HASH},
            "plan": {"path": "specs/001-example/plan.md", "sha256": HASH},
            "tasks": {"path": "specs/001-example/tasks.md", "sha256": HASH},
        }
        self.assertTrue(any("checkpoint is required" in error for error in validate_workflow_state(state)))
        state["checkpoint"] = {
            "checkpoint_id": "bmad-4-1-complete-20260712T000000Z-abcdef",
            "stage": "complete", "commit_sha": SHA, "binding_status": "bound",
        }
        self.assertEqual([], validate_workflow_state(state))
        state["checkpoint"]["binding_status"] = "pending"
        state["checkpoint"]["commit_sha"] = None
        self.assertTrue(any("bound complete checkpoint" in error for error in validate_workflow_state(state)))

    def test_accepted_gate_requires_pinned_verdict_evidence(self):
        state = valid_state()
        state["gates"]["definition-review"]["status"] = "accepted"
        errors = validate_workflow_state(state)
        self.assertTrue(any("gates.definition-review.verdict_path" in error for error in errors))

    def test_completed_e2e_and_approval_require_durable_pointer(self):
        state = valid_state()
        state["e2e"]["result"] = "skipped-covered"
        errors = validate_workflow_state(state)
        self.assertTrue(any("decision_path" in error for error in errors))
        state = valid_state()
        state["approvals"] = [{
            "decision_id": "implementation", "status": "accepted", "scope_sha256": HASH,
            "evidence_path": None, "decided_at": None,
        }]
        errors = validate_workflow_state(state)
        self.assertTrue(any("completed decision requires" in error for error in errors))

    def test_runtime_rejects_unknown_properties_duplicates_and_nested_feature_path(self):
        verdict = valid_verdict()
        verdict["reviewed_boundary"]["extra"] = True
        verdict["reviewed_artifacts"] *= 2
        errors = validate_verdict(verdict)
        self.assertTrue(any("reviewed_boundary.extra is not allowed" in error for error in errors))
        self.assertTrue(any("reviewed_artifacts must contain unique items" in error for error in errors))
        state = valid_state()
        state["feature_directory"] = "specs/group/001-example"
        state["preflight"]["extra"] = True
        state["current_evidence_paths"] = ["specs/x/a", "specs/x/a"]
        errors = validate_workflow_state(state)
        self.assertTrue(any("feature_directory must be exactly one directory" in error for error in errors))
        self.assertTrue(any("preflight.extra is not allowed" in error for error in errors))
        self.assertTrue(any("current_evidence_paths must be unique" in error for error in errors))

    def test_timestamps_must_be_real_rfc3339_utc_instants(self):
        state = valid_state()
        state["updated_at"] = "2026-02-30T00:00:00Z"
        self.assertTrue(any("updated_at" in error for error in validate_workflow_state(state)))
        state["updated_at"] = "2026-07-12T08:00:00+08:00"
        self.assertTrue(any("updated_at" in error for error in validate_workflow_state(state)))

    def test_wrong_state_types_never_crash(self):
        errors = validate_workflow_state({"schema_version": "1.0", "gates": [], "attempts": "bad"})
        self.assertTrue(any("gates" in error for error in errors))
        self.assertTrue(any("attempts" in error for error in errors))

    def test_real_catalog_has_exact_files_profiles_and_instructions(self):
        agent_dir = self.REPOSITORY_ROOT / ".codex" / "agents"
        self.assertEqual({f"{name}.toml" for name in self.PROFILES}, {p.name for p in agent_dir.glob("*.toml")})
        seen = set()
        for path in agent_dir.glob("*.toml"):
            with path.open("rb") as stream:
                data = tomllib.load(stream)
            self.assertEqual(path.stem, data["name"])
            self.assertNotIn(data["name"], seen)
            seen.add(data["name"])
            self.assertEqual(self.PROFILES[data["name"]],
                             (data["model"], data["model_reasoning_effort"], data["sandbox_mode"]))
            for field in ["description", "developer_instructions"]:
                self.assertIsInstance(data[field], str)
                self.assertTrue(data[field].strip())
            self.assertIn("seven-slot dispatch contract", data["developer_instructions"])
            self.assertTrue("commit" in data["developer_instructions"].lower())
            instructions = data["developer_instructions"].lower()
            for marker in self.INSTRUCTION_MARKERS[data["name"]]:
                self.assertIn(marker, instructions)

    def test_workflow_owned_architecture_adapter_preserves_upstream_skill(self):
        skills = self.REPOSITORY_ROOT / ".agents" / "skills"
        adapter = (skills / "audit-epic-architecture" / "SKILL.md").read_text(encoding="utf-8").lower()
        close = (skills / "close-bmad-epic" / "SKILL.md").read_text(encoding="utf-8").lower()
        upstream = (skills / "improve-codebase-architecture" / "SKILL.md").read_text(encoding="utf-8").lower()
        self.assertIn("$improve-codebase-architecture", adapter)
        self.assertIn("do not modify that upstream skill", adapter)
        self.assertIn("$audit-epic-architecture", close)
        self.assertNotIn("read-only epic-close profile", upstream)

    def test_agent_validation_handles_malformed_toml_and_empty_fields(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "broken.toml"
            path.write_text('name = "broken"\ninvalid = [', encoding="utf-8")
            self.assertTrue(any("invalid TOML" in error for error in validate_agent_file(path)))
            path.write_text('name="broken"\ndescription=""\ndeveloper_instructions=""\n', encoding="utf-8")
            errors = validate_agent_file(path)
            self.assertTrue(any("description" in error for error in errors))

    def test_repository_validation_rejects_missing_and_empty_repo(self):
        with tempfile.TemporaryDirectory() as directory:
            empty = Path(directory)
            self.assertTrue(validate_repository(empty))
            self.assertTrue(validate_repository(empty / "missing"))

    def test_repository_validation_reports_malformed_schema_without_traceback(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".codex" / "agents").mkdir(parents=True)
            (root / ".codex" / "config.toml").write_text("[agents]\nmax_threads=4\nmax_depth=1\n", encoding="utf-8")
            refs = root / ".agents" / "skills" / "run-bmad-sprint-loop" / "references"
            refs.mkdir(parents=True)
            (refs / "verdict.schema.json").write_text("{", encoding="utf-8")
            (refs / "workflow-state.schema.json").write_text("{}", encoding="utf-8")
            errors = validate_repository(root)
            self.assertTrue(any("verdict.schema.json: invalid JSON" in error for error in errors))

    def test_repository_validation_discovers_story_workflow_states(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            state_path = root / "specs" / "001-example" / "workflow-state.json"
            state_path.parent.mkdir(parents=True)
            state = valid_state()
            state["schema_version"] = "2.0"
            state_path.write_text(json.dumps(state), encoding="utf-8")
            errors = validate_repository(root)
            self.assertTrue(any(
                str(state_path) in error and "schema_version must equal 3.0" in error
                for error in errors
            ), errors)

    def test_repository_validation_rejects_schema_semantic_corruption(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            shutil.copytree(self.REPOSITORY_ROOT / ".codex", root / ".codex")
            source_refs = self.REPOSITORY_ROOT / ".agents/skills/run-bmad-sprint-loop/references"
            refs = root / ".agents/skills/run-bmad-sprint-loop/references"
            shutil.copytree(source_refs, refs)
            verdict_path = refs / "verdict.schema.json"
            verdict = json.loads(verdict_path.read_text(encoding="utf-8"))
            verdict["properties"]["verdict"]["enum"] = ["BANANA"]
            verdict_path.write_text(json.dumps(verdict), encoding="utf-8")
            errors = validate_repository(root)
            self.assertTrue(any("verdict enum" in error for error in errors), errors)

    def test_repository_validation_passes_real_repository(self):
        self.assertEqual([], validate_repository(self.REPOSITORY_ROOT))
        for schema in (self.REPOSITORY_ROOT / ".agents/skills/run-bmad-sprint-loop/references").glob("*.schema.json"):
            json.loads(schema.read_text(encoding="utf-8"))

    def test_schemas_encode_nested_contract_and_verdict_semantics(self):
        refs = self.REPOSITORY_ROOT / ".agents/skills/run-bmad-sprint-loop/references"
        verdict = json.loads((refs / "verdict.schema.json").read_text(encoding="utf-8"))
        self.assertTrue({"boundary", "finding"} <= set(verdict["$defs"]))
        self.assertGreaterEqual(len(verdict["allOf"]), 2)
        self.assertEqual("1.0", verdict["properties"]["schema_version"]["const"])
        self.assertEqual({"ACCEPT", "REVISE", "BLOCKED"}, set(verdict["properties"]["verdict"]["enum"]))
        self.assertEqual(
            {"plan-review", "definition-review", "quality-review"},
            set(verdict["properties"]["gate"]["enum"]),
        )
        self.assertEqual(
            {"CRITICAL", "HIGH", "MEDIUM", "LOW"},
            set(verdict["$defs"]["finding"]["properties"]["severity"]["enum"]),
        )
        state = json.loads((refs / "workflow-state.schema.json").read_text(encoding="utf-8"))
        self.assertTrue({
            "sha", "hash", "timestamp", "path", "stage", "preflight", "artifact", "gate",
            "approval", "convergence", "e2e", "checkpoint", "resume",
        } <= set(state["$defs"]))
        self.assertTrue(state["allOf"])
        self.assertFalse(verdict["additionalProperties"])
        self.assertFalse(verdict["$defs"]["boundary"]["additionalProperties"])
        self.assertTrue(verdict["properties"]["reviewed_artifacts"]["uniqueItems"])
        self.assertFalse(state["additionalProperties"])
        self.assertEqual(
            {"context", "specify", "plan", "plan-review", "tasks", "definition-review",
             "implementation-approval", "implement", "converge", "quality-review",
             "conditional-e2e", "verify", "complete"},
            set(state["$defs"]["stage"]["enum"]),
        )
        self.assertEqual(12, state["properties"]["approvals"]["maxItems"])
        self.assertEqual(12, state["properties"]["current_evidence_paths"]["maxItems"])
        self.assertEqual(
            {"pending", "passed", "failed-product", "failed-test", "blocked", "skipped-no-risk", "skipped-covered"},
            set(state["$defs"]["e2e"]["properties"]["result"]["enum"]),
        )

    def test_epic_close_runtime_contracts_accept_valid_and_reject_unsafe_states(self):
        evidence = {
            "schema_version": "1.0", "evidence_id": "4/verify-build/1", "kind": "command",
            "stage": "verify-build", "source_commit": SHA,
            "artifact_paths": ["docs/bmad/implementation-artifacts/epic-gates/4/evidence/build.log"],
            "command": "dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj", "exit_code": 0,
            "observed_at": "2026-07-13T00:00:00Z",
            "summary": "passed", "raw_output_path": "docs/bmad/implementation-artifacts/epic-gates/4/evidence/build.log", "raw_output_sha256": HASH,
            "execution_worktree": "C:/tmp/build", "execution_worktree_head": SHA,
            "execution_worktree_git_identity": "build-id", "boundary_clean": True,
            "source_equality_validated_at": "2026-07-13T00:00:01Z", "environment_summary": ".NET 10",
        }
        self.assertEqual([], validate_epic_command_evidence(evidence))
        bad_evidence = copy.deepcopy(evidence)
        bad_evidence["execution_worktree_head"] = "c" * 40
        bad_evidence["exit_code"] = 99
        bad_evidence["command"] = "false"
        self.assertTrue(validate_epic_command_evidence(bad_evidence))
        with tempfile.TemporaryDirectory() as temp:
            self.assertTrue(any("missing" in error for error in validate_epic_command_evidence(evidence, Path(temp))))

        gate_path = "docs/bmad/implementation-artifacts/epic-gates/4/architecture-gate.json"
        audit_raw = "docs/bmad/implementation-artifacts/epic-gates/4/audit-raw/dispatch-1.json"
        build_path = "docs/bmad/implementation-artifacts/epic-gates/4/evidence/build.json"
        test_path = "docs/bmad/implementation-artifacts/epic-gates/4/evidence/test.json"
        status_paths = ["docs/bmad/implementation-artifacts/sprint-status.yaml", "docs/bmad/epics.md"]
        durable_paths = [gate_path, audit_raw, build_path, test_path, *status_paths]
        non_gate = [{"path": path, "status": "A", "old_mode": None, "old_type": None, "old_oid": None,
            "new_mode": "100644", "new_type": "blob", "new_oid": SHA} for path in durable_paths if path != gate_path]
        gate = {
            "schema_version": "1.0", "epic_key": "4", "status": "ready",
            "transaction_phase": "prepared", "source_commit": SHA,
            "repository_preflight": {"branch": "codex/test", "head": SHA, "repository_root": "G:/repo",
                "git_common_dir": "G:/repo/.git", "worktree": "G:/repo", "index_empty_at_start": True,
                "target_preimages": {path: None for path in durable_paths}},
            "verification_boundary": {
                "build": {"path": "C:/tmp/build", "git_identity": "build-id", "head": SHA,
                    "tree_id": SHA, "index_id": SHA, "clean": True, "disposed": True},
                "audit": {"path": "C:/tmp/audit", "git_identity": "audit-id", "head": SHA,
                    "tree_id": SHA, "index_id": SHA, "clean": True, "disposed": True},
                "source_equal": True},
            "audit_validation": {"verdict": "ACCEPT", "audit_source_commit": SHA,
                "authoritative_input_hashes": {"docs/UBIQUITOUS_LANGUAGE.md": HASH},
                "raw_response_path": audit_raw, "raw_response_sha256": HASH,
                "report_temporary_path": "C:/tmp/report.html", "report_sha256": HASH, "audit_iteration": 1,
                "normalized_payload_sha256": HASH, "one_to_one_mapping": True, "citations_verified": True,
                "visible_fields_equal": True, "schema_valid": True, "finding_count": 0, "finding_ids": [],
                "reserved_finding_count": 0, "validated_at": "2026-07-13T00:00:02Z"},
            "complete_findings": [], "dispositions": [], "build_evidence_paths": [build_path],
            "test_evidence_paths": [test_path], "completion_status_paths": status_paths,
            "durable_artifact_paths": durable_paths, "retrospective_decision": "declined",
            "checkpoint": {"id": "cp", "expected_parent": SHA},
            "manifest_constraints": {"expected_name_status": [{"path": path, "status": "A"} for path in durable_paths],
                "non_gate_tuples": non_gate, "gate_descriptor": {"path": gate_path, "status": "A",
                    "mode": "100644", "object_type": "blob", "semantic_projection_sha256": HASH}},
            "earliest_safe_resume_step": 0, "retryability": "retryable", "failure": None,
        }
        projection = copy.deepcopy(gate)
        projection["manifest_constraints"]["gate_descriptor"]["semantic_projection_sha256"] = None
        gate["manifest_constraints"]["gate_descriptor"]["semantic_projection_sha256"] = hashlib.sha256(
            json.dumps(projection, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
        ).hexdigest()
        self.assertEqual([], validate_epic_gate(gate))
        bad_gate = copy.deepcopy(gate)
        bad_gate["audit_validation"]["reserved_finding_count"] = 1
        bad_gate["manifest_constraints"]["gate_descriptor"]["oid"] = "d" * 40
        errors = validate_epic_gate(bad_gate)
        self.assertTrue(any("reserved" in error for error in errors))
        self.assertTrue(any("self-referential" in error for error in errors))

        unsafe_gate = copy.deepcopy(gate)
        unsafe_gate["audit_validation"].update({"verdict": "REVISE", "finding_count": 1,
            "finding_ids": ["arch-1"], "reserved_finding_count": 1})
        unsafe_gate["complete_findings"] = [{"raw_candidate_id": "arch-1", "strength": "Strong",
            "severity": "HIGH", "applicable_categories": ["critical-high", "must", "strong"],
            "must_violation": True, "adr_conflict": False,
            "citations": [{"path": "src/A.cs", "blob_oid": SHA, "range": "L1-L2", "excerpt_sha256": HASH}]}]
        unsafe_gate["dispositions"] = [{"finding_id": "arch-1",
            "category_decisions": {"critical-high": "risk-accepted", "strong": "accepted-known-debt"},
            "user_approval_evidence_id": "approval-1", "status": "accepted"}]
        unsafe_gate["repository_preflight"]["head"] = "c" * 40
        unsafe_errors = validate_epic_gate(unsafe_gate)
        self.assertTrue(any("preflight HEAD" in error for error in unsafe_errors))
        self.assertTrue(any("every reserved category" in error for error in unsafe_errors))

        retro_paths = ["docs/bmad/implementation-artifacts/epic-gates/4/retrospective-state.json", "docs/retro.md"]
        retro_hash = hashlib.sha256(json.dumps(retro_paths, ensure_ascii=False, separators=(",", ":")).encode("utf-8")).hexdigest()
        retro = {"schema_version": "1.0", "epic_key": "4", "status": "active",
            "source_epic_checkpoint": {"id": "cp", "commit_sha": SHA, "manifest_sha256": HASH},
            "hooks": [], "write_manifest": retro_paths, "approved_manifest_sha256": retro_hash,
            "evidence_paths": [], "failure": None,
            "checkpoint": None}
        self.assertEqual([], validate_retrospective_state(retro))
        bad_retro = copy.deepcopy(retro)
        bad_retro["status"] = "complete"
        self.assertTrue(validate_retrospective_state(bad_retro))


if __name__ == "__main__":
    unittest.main()
