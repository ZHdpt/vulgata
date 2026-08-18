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
    gate = {
        "status": "pending",
        "iteration": 0,
        "dispatch_id": None,
        "verdict_path": None,
        "input_boundary": None,
        "completed_at": None,
    }
    return {
        "schema_version": "2.0",
        "bmad_story_key": "4-1-example",
        "bmad_epic_key": "4",
        "feature_directory": "specs/001-example",
        "execution_level": "unified",
        "stage": "context",
        "active_gates": {
            "definition-review": copy.deepcopy(gate),
            "quality-review": copy.deepcopy(gate),
        },
        "stage_history": [{
            "stage": "context", "status": "active", "started_at": "2026-07-12T00:00:00Z",
            "completed_at": None, "input_commit": SHA, "input_hashes": {},
            "agent_role": "story-context-synthesizer", "evidence_paths": [], "failure": None,
        }],
        "repository_preflight": {
            "branch": "feature/example", "preflight_head": SHA, "repository_root": ".",
            "git_common_directory": ".git", "worktree_path": ".", "worktree_status": "",
            "owned_paths": ["specs/001-example"], "unrelated_paths": [],
            "observed_at": "2026-07-12T00:00:00Z",
        },
        "source_artifacts": [{
            "authority": "BMad story", "path": "docs/bmad/story.md", "commit": SHA, "sha256": HASH,
        }],
        "dispatches": [{
            "role": "story-context-synthesizer", "objective": "Synthesize context",
            "source_boundary": copy.deepcopy(BOUNDARY), "permitted_writes": [],
            "output_path": "specs/001-example/context-brief.md", "returned_evidence": [],
        }],
        "review_iterations": {
            "plan-review": {"count": 0, "verdict_paths": []},
            "definition-review": {"count": 0, "verdict_paths": []},
            "converge": {"count": 0, "verdict_paths": []},
            "quality-review": {"count": 0, "verdict_paths": []},
        },
        "convergence": {
            "status": "pending", "iteration": 0, "outcome": "pending", "findings_count": 0,
            "appended_task_ids": [], "artifact_hashes": {}, "completed_at": None,
        },
        "e2e_decision": {
            "required": False, "predicates": [], "coverage_override": None,
            "rationale": "Not evaluated", "covering_tests": [], "owner": "orchestrator",
            "source_boundary": copy.deepcopy(BOUNDARY), "result": "pending", "evidence_ids": [],
        },
        "verification": [{
            "evidence_id": "4-1-example/context/1", "kind": "command", "stage": "context",
            "source_commit": SHA, "artifact_paths": [], "command": "git status --short",
            "exit_code": 0, "observed_at": "2026-07-12T00:00:00Z", "summary": "clean",
            "raw_output_path": None,
        }],
        "last_checkpoint": None,
        "recoverable_failure": None,
        "legacy_migration": None,
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

    def test_workflow_state_accepts_canonical_absolute_checkout_identities(self):
        state = valid_state()
        state["repository_preflight"]["git_common_directory"] = "C:/source/vulgata/.git"
        state["repository_preflight"]["worktree_path"] = "C:/source/vulgata/.worktrees/story-11-1"

        self.assertEqual([], validate_workflow_state(state))

    def test_workflow_state_keeps_repository_and_artifact_paths_relative(self):
        state = valid_state()
        state["repository_preflight"]["repository_root"] = "C:/source/vulgata"
        state["repository_preflight"]["owned_paths"] = ["C:/source/vulgata/specs/001-example"]
        state["source_artifacts"][0]["path"] = "C:/source/vulgata/docs/bmad/story.md"
        state["dispatches"][0]["output_path"] = "C:/source/vulgata/specs/001-example/context-brief.md"
        state["verification"][0]["raw_output_path"] = "C:/source/vulgata/specs/001-example/context.log"

        errors = validate_workflow_state(state)

        for expected in [
            "repository_preflight.repository_root",
            "repository_preflight.owned_paths[0]",
            "source_artifacts[0].path",
            "dispatches[0].output_path",
            "verification[0].raw_output_path",
        ]:
            self.assertTrue(any(expected in error for error in errors), (expected, errors))

    def test_workflow_state_requires_unified_execution_level(self):
        state = valid_state()
        del state["execution_level"]
        self.assertTrue(any("execution_level" in error for error in validate_workflow_state(state)))

        for legacy in ("full", "standard", "expedited-mvp"):
            with self.subTest(legacy=legacy):
                invalid = valid_state()
                invalid["execution_level"] = legacy
                errors = validate_workflow_state(invalid)
                self.assertTrue(any("execution_level must equal unified" in error for error in errors), errors)

    def test_convergence_outcomes_are_semantically_bound(self):
        state = valid_state()
        state["convergence"].update(
            status="accepted", outcome="converged", completed_at="2026-07-12T01:00:00Z"
        )
        self.assertEqual([], validate_workflow_state(state))

        state["convergence"].update(
            status="revised", outcome="tasks_appended", findings_count=1,
            appended_task_ids=["T042"], completed_at="2026-07-12T02:00:00Z",
        )
        self.assertEqual([], validate_workflow_state(state))

        state["convergence"]["appended_task_ids"] = []
        errors = validate_workflow_state(state)
        self.assertTrue(any("tasks_appended outcome" in error for error in errors), errors)

    def test_legacy_migration_records_expired_level_without_reauthorizing_it(self):
        state = valid_state()
        state["legacy_migration"] = {
            "source_state_path": "specs/001-example/workflow-state-v1.json",
            "source_state_sha256": HASH,
            "source_execution_level": "expedited-mvp",
            "source_stage": "quality-review",
            "mapped_stage": "converge",
            "decision_evidence_path": "specs/001-example/evidence/migration.json",
            "migrated_at": "2026-08-18T01:00:00Z",
        }
        self.assertEqual([], validate_workflow_state(state))
        state["execution_level"] = "expedited-mvp"
        self.assertTrue(any("execution_level must equal unified" in e for e in validate_workflow_state(state)))

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
            "tdd-test-author",
            "gpt-5.6-luna",
            "expired august 15 exception cannot authorize",
        ]:
            self.assertIn(marker, skill)
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


    def test_every_nonterminal_stage_accepts_a_well_formed_active_record(self):
        stages = ["context", "specify", "plan", "plan-review", "tasks", "definition-review",
                  "implement", "converge", "quality-review", "conditional-e2e", "verify"]
        for stage in stages:
            with self.subTest(stage=stage):
                state = valid_state()
                state["stage"] = stage
                state["stage_history"][0]["stage"] = stage
                state["verification"][0]["stage"] = stage
                state["verification"][0]["evidence_id"] = f"4-1-example/{stage}/1"
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
        del state["active_gates"]["quality-review"]
        state["repository_preflight"]["owned_paths"] = ["../escape"]
        state["source_artifacts"][0]["sha256"] = "bad"
        state["dispatches"][0]["permitted_writes"] = "all"
        state["review_iterations"]["definition-review"]["count"] = -1
        state["e2e_decision"]["result"] = "magic"
        state["verification"][0]["source_commit"] = "bad"
        errors = validate_workflow_state(state)
        for expected in ["bmad_story_key", "feature_directory", "active_gates.quality-review",
                         "repository_preflight.owned_paths[0]", "source_artifacts[0].sha256",
                         "dispatches[0].permitted_writes", "review_iterations.definition-review.count",
                         "e2e_decision.result", "verification[0].source_commit"]:
            self.assertTrue(any(expected in error for error in errors), (expected, errors))

    def test_complete_state_requires_checkpoint_binding_semantics(self):
        state = valid_state()
        state["stage"] = "complete"
        self.assertTrue(any("last_checkpoint" in error for error in validate_workflow_state(state)))
        state["last_checkpoint"] = {
            "checkpoint_id": "bmad-4-1-complete-20260712T000000Z-abcdef", "scope_key": "4-1-example",
            "stage": "complete", "committed_paths": ["specs/001-example/workflow-state.json"],
            "verification_evidence_ids": ["4-1-example/context/1"], "commit_sha": None,
            "binding_status": "pending",
        }
        self.assertEqual([], validate_workflow_state(state))
        state["last_checkpoint"]["binding_status"] = "bound"
        self.assertTrue(any("commit_sha" in error for error in validate_workflow_state(state)))

    def test_accepted_gate_requires_pinned_verdict_evidence(self):
        state = valid_state()
        state["active_gates"]["definition-review"]["status"] = "accepted"
        errors = validate_workflow_state(state)
        for expected in ["verdict_path", "input_boundary", "completed_at"]:
            self.assertTrue(any(f"active_gates.definition-review.{expected}" in error for error in errors))

    def test_e2e_semantics_require_runner_and_coverage_record(self):
        state = valid_state()
        state["e2e_decision"].update(required=True, owner="orchestrator")
        errors = validate_workflow_state(state)
        self.assertTrue(any("owner must be e2e-runner" in error for error in errors))
        state["e2e_decision"].update(required=False, result="skipped-covered", coverage_override=None)
        errors = validate_workflow_state(state)
        self.assertTrue(any("coverage_override is required" in error for error in errors))

    def test_e2e_required_predicates_need_execution_or_complete_current_boundary_override(self):
        state = valid_state()
        state["e2e_decision"]["predicates"] = [{
            "predicate_id": "user-visible-workflow",
            "change_type": "User-visible workflow",
            "gate": "required",
            "triggered": True,
            "affected_paths": ["src/dotnet/Vulgata.Web/Example.razor"],
            "affected_behavior": "用户完成示例工作流",
            "evidence": ["4-1-example/context/1"],
        }]
        errors = validate_workflow_state(state)
        self.assertTrue(any("required must be true" in error for error in errors))
        state["e2e_decision"].update(
            required=False,
            result="skipped-covered",
            covering_tests=["ExampleFlow_Completes_Succeeds"],
            coverage_override={
                "test_ids": ["ExampleFlow_Completes_Succeeds"],
                "covered_predicate_ids": ["user-visible-workflow"],
                "source_boundary": copy.deepcopy(BOUNDARY),
                "passing_evidence_ids": ["4-1-example/context/1"],
                "completeness_rationale": "Existing integration test covers the complete changed workflow.",
            },
        )
        self.assertEqual([], validate_workflow_state(state))
        state["e2e_decision"]["coverage_override"]["covered_predicate_ids"] = []
        errors = validate_workflow_state(state)
        self.assertTrue(any("cover every triggered required predicate" in error for error in errors))

    def test_coverage_override_and_skipped_coverage_require_nonempty_test_evidence(self):
        state = valid_state()
        state["e2e_decision"]["coverage_override"] = {
            "test_ids": [],
            "covered_predicate_ids": [],
            "source_boundary": copy.deepcopy(BOUNDARY),
            "passing_evidence_ids": [],
            "completeness_rationale": "Claimed complete coverage.",
        }
        errors = validate_workflow_state(state)
        for field in ["test_ids", "covered_predicate_ids", "passing_evidence_ids"]:
            self.assertTrue(any(f"coverage_override.{field}" in error and "at least one" in error for error in errors))

        state["e2e_decision"].update(
            result="skipped-covered",
            covering_tests=[],
            predicates=[{
                "predicate_id": "user-visible-workflow",
                "change_type": "User-visible workflow",
                "gate": "required",
                "triggered": True,
                "affected_paths": ["src/dotnet/Vulgata.Web/Example.razor"],
                "affected_behavior": "User completes the changed workflow.",
                "evidence": ["4-1-example/context/1"],
            }],
        )
        state["e2e_decision"]["coverage_override"].update(
            test_ids=["ExampleFlow_Completes_Succeeds"],
            covered_predicate_ids=["user-visible-workflow"],
            passing_evidence_ids=["4-1-example/context/1"],
        )
        errors = validate_workflow_state(state)
        self.assertTrue(any("covering_tests" in error and "at least one" in error for error in errors))

    def test_runtime_rejects_unknown_properties_duplicates_and_nested_feature_path(self):
        verdict = valid_verdict()
        verdict["reviewed_boundary"]["extra"] = True
        verdict["reviewed_artifacts"] *= 2
        errors = validate_verdict(verdict)
        self.assertTrue(any("reviewed_boundary.extra is not allowed" in error for error in errors))
        self.assertTrue(any("reviewed_artifacts must contain unique items" in error for error in errors))
        state = valid_state()
        state["feature_directory"] = "specs/group/001-example"
        state["repository_preflight"]["extra"] = True
        errors = validate_workflow_state(state)
        self.assertTrue(any("feature_directory must match specs/<feature>" in error for error in errors))
        self.assertTrue(any("repository_preflight.extra is not allowed" in error for error in errors))

    def test_timestamps_must_be_real_rfc3339_utc_instants(self):
        state = valid_state()
        state["updated_at"] = "2026-02-30T00:00:00Z"
        self.assertTrue(any("updated_at" in error for error in validate_workflow_state(state)))
        state["updated_at"] = "2026-07-12T08:00:00+08:00"
        self.assertTrue(any("updated_at" in error for error in validate_workflow_state(state)))

    def test_evidence_id_must_match_story_stage_and_positive_sequence(self):
        state = valid_state()
        state["verification"][0]["evidence_id"] = "other-story/context/1"
        self.assertTrue(any("verification[0].evidence_id" in error for error in validate_workflow_state(state)))
        state["verification"][0]["evidence_id"] = "4-1-example/context/0"
        self.assertTrue(any("verification[0].evidence_id" in error for error in validate_workflow_state(state)))
        state["verification"][0]["evidence_id"] = "4-1-example/plan/1"
        self.assertTrue(any("verification[0].evidence_id" in error for error in validate_workflow_state(state)))

    def test_wrong_state_types_never_crash(self):
        errors = validate_workflow_state({"schema_version": "1.0", "active_gates": [], "stage_history": "bad"})
        self.assertTrue(any("active_gates" in error for error in errors))
        self.assertTrue(any("stage_history" in error for error in errors))

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
            state["execution_level"] = "full"
            state_path.write_text(json.dumps(state), encoding="utf-8")
            errors = validate_repository(root)
            self.assertTrue(any(
                str(state_path) in error and "execution_level must equal unified" in error
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
            "boundary", "gateRecord", "stageRecord", "preflight", "sourceArtifact", "dispatch",
            "reviewIteration", "convergenceRecord", "legacyMigration", "e2eDecision", "evidence",
            "checkpoint", "failure", "checkoutIdentityPath",
        } <= set(state["$defs"]))
        self.assertTrue(state["allOf"])
        self.assertFalse(verdict["additionalProperties"])
        self.assertFalse(verdict["$defs"]["boundary"]["additionalProperties"])
        self.assertTrue(verdict["properties"]["reviewed_artifacts"]["uniqueItems"])
        self.assertFalse(state["additionalProperties"])
        self.assertEqual(".", state["$defs"]["preflight"]["properties"]["repository_root"]["const"])
        for field in ("git_common_directory", "worktree_path"):
            self.assertEqual(
                "#/$defs/checkoutIdentityPath",
                state["$defs"]["preflight"]["properties"][field]["$ref"],
            )
        self.assertEqual("#/$defs/boundary", state["$defs"]["e2eDecision"]["properties"]["source_boundary"]["$ref"])
        self.assertFalse(state["$defs"]["e2ePredicate"]["additionalProperties"])
        self.assertFalse(state["$defs"]["coverageOverride"]["additionalProperties"])
        self.assertEqual(
            {"context", "specify", "plan", "plan-review", "tasks", "definition-review", "implement",
             "converge", "quality-review", "conditional-e2e", "verify", "complete"},
            set(state["$defs"]["stage"]["enum"]),
        )
        self.assertEqual({"required", "may-skip"}, set(state["$defs"]["e2ePredicate"]["properties"]["gate"]["enum"]))
        for field in ["test_ids", "covered_predicate_ids", "passing_evidence_ids"]:
            self.assertEqual(1, state["$defs"]["coverageOverride"]["properties"][field]["minItems"])
        self.assertEqual(
            {"pending", "passed", "failed-product", "failed-test", "blocked", "skipped-no-risk", "skipped-covered"},
            set(state["$defs"]["e2eDecision"]["properties"]["result"]["enum"]),
        )
        skipped_rule = state["$defs"]["e2eDecision"]["allOf"][1]
        self.assertEqual("skipped-covered", skipped_rule["if"]["properties"]["result"]["const"])
        self.assertEqual(1, skipped_rule["then"]["properties"]["covering_tests"]["minItems"])

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
