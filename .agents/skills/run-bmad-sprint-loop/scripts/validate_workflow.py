from __future__ import annotations

import argparse
import json
import re
import tomllib
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

REQUIRED_AGENT_FIELDS = {"name", "description", "developer_instructions"}
VALID_VERDICTS = {"ACCEPT", "REVISE", "BLOCKED"}
VALID_GATES = {"spec-review", "plan-review", "analyze", "acceptance-review", "quality-review"}
BLOCKING_SEVERITIES = {"CRITICAL", "HIGH"}
VALID_SEVERITIES = BLOCKING_SEVERITIES | {"MEDIUM", "LOW"}
VALID_STAGES = {
    "context", "specify", "spec-review", "plan", "plan-review", "tasks", "analyze",
    "implement", "acceptance-review", "quality-review", "conditional-e2e", "verify", "complete",
}
VALID_STATUSES = {"pending", "active", "accepted", "revised", "blocked", "failed", "invalidated"}
VALID_E2E_RESULTS = {
    "pending", "passed", "failed-product", "failed-test", "blocked", "skipped-no-risk", "skipped-covered",
}
VALID_EVIDENCE_KINDS = {
    "command", "test-red", "test-green", "review", "analysis", "artifact-hash", "e2e-decision",
    "human-approval", "architecture-disposition", "failure",
}
SHA_RE = re.compile(r"^[0-9a-fA-F]{40}$")
HASH_RE = re.compile(r"^[0-9a-fA-F]{64}$")
TIME_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$")

ROLE_PROFILES = {
    "bmad-sprint-orchestrator": ("gpt-5.6-sol", "high", "workspace-write"),
    "story-context-synthesizer": ("gpt-5.6-terra", "medium", "read-only"),
    "spec-author": ("gpt-5.6-sol", "medium", "workspace-write"),
    "spec-judge": ("gpt-5.6-sol", "high", "read-only"),
    "plan-architect": ("gpt-5.6-sol", "high", "workspace-write"),
    "plan-judge": ("gpt-5.6-sol", "high", "read-only"),
    "task-author": ("gpt-5.6-sol", "medium", "workspace-write"),
    "artifact-analyst": ("gpt-5.6-sol", "high", "read-only"),
    "tdd-implementer": ("gpt-5.6-sol", "high", "workspace-write"),
    "acceptance-reviewer": ("gpt-5.6-sol", "high", "read-only"),
    "quality-reviewer": ("gpt-5.6-sol", "high", "read-only"),
    "e2e-runner": ("gpt-5.6-terra", "low", "workspace-write"),
    "epic-architecture-auditor": ("gpt-5.6-sol", "high", "read-only"),
}
ROLE_INSTRUCTION_MARKERS = {
    "bmad-sprint-orchestrator": ("load and follow run-bmad-sprint-loop", "never implement feature code", "you alone"),
    "story-context-synthesizer": ("separate sourced facts from inference", "cite repository paths"),
    "spec-author": ("speckit-specify", "what/why", "quality checklist"),
    "spec-judge": ("validated accept", "never edit"),
    "plan-architect": ("speckit-plan", "resolve research", "constitution gate"),
    "plan-judge": ("feasibility", "architecture seams", "testability"),
    "task-author": ("speckit-tasks", "failing tests", "exact paths"),
    "artifact-analyst": ("speckit-analyze", "never edit or remediate", "explicit user approval"),
    "tdd-implementer": ("no production code before observing", "minimal green"),
    "acceptance-reviewer": ("missing, partial, wrong", "scope-creep"),
    "quality-reviewer": ("skip style already enforced by tooling", "cite documented standards"),
    "e2e-runner": ("test files and test evidence only", "never change production code"),
    "epic-architecture-auditor": ("os temporary directory", "never implement or decide dispositions"),
}

VERDICT_REQUIRED = {
    "schema_version", "gate", "verdict", "reviewed_commit", "reviewed_boundary",
    "reviewed_artifacts", "findings", "summary",
}
STATE_REQUIRED = {
    "schema_version", "bmad_story_key", "bmad_epic_key", "feature_directory", "stage", "active_gates",
    "stage_history", "repository_preflight", "source_artifacts", "dispatches", "review_iterations",
    "e2e_decision", "verification", "last_checkpoint", "recoverable_failure", "updated_at",
}


def _mapping(value: Any, field: str, errors: list[str]) -> dict[str, Any] | None:
    if not isinstance(value, dict):
        errors.append(f"{field} must be an object")
        return None
    return value


def _array(value: Any, field: str, errors: list[str]) -> list[Any] | None:
    if not isinstance(value, list):
        errors.append(f"{field} must be an array")
        return None
    return value


def _required(data: dict[str, Any], fields: set[str], prefix: str, errors: list[str]) -> None:
    for field in sorted(fields - data.keys()):
        errors.append(f"{prefix}{field}")


def _unknown(data: dict[str, Any], fields: set[str], prefix: str, errors: list[str]) -> None:
    for field in sorted(data.keys() - fields):
        errors.append(f"{prefix}{field} is not allowed")


def _nonempty_string(value: Any, field: str, errors: list[str]) -> None:
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{field} must be a non-empty string")


def _sha(value: Any, field: str, errors: list[str], nullable: bool = False) -> None:
    if nullable and value is None:
        return
    if not isinstance(value, str) or not SHA_RE.fullmatch(value):
        errors.append(f"{field} must be a 40-character hexadecimal commit")


def _hash(value: Any, field: str, errors: list[str]) -> None:
    if not isinstance(value, str) or not HASH_RE.fullmatch(value):
        errors.append(f"{field} must be a 64-character hexadecimal SHA-256")


def _timestamp(value: Any, field: str, errors: list[str], nullable: bool = False) -> None:
    if nullable and value is None:
        return
    if not isinstance(value, str) or not TIME_RE.fullmatch(value):
        errors.append(f"{field} must be a UTC RFC 3339 timestamp")
        return
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError:
        errors.append(f"{field} must be a real UTC RFC 3339 timestamp")
        return
    if parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        errors.append(f"{field} must use UTC")


def _repo_path(value: Any, field: str, errors: list[str], nullable: bool = False) -> None:
    if nullable and value is None:
        return
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{field} must be a repository-relative path")
        return
    if "\\" in value or value.startswith(("/", "~")) or re.match(r"^[A-Za-z]:", value):
        errors.append(f"{field} must be a repository-relative path")
        return
    if any(part == ".." for part in value.split("/")):
        errors.append(f"{field} must not escape the repository")


def _string_array(value: Any, field: str, errors: list[str], paths: bool = False) -> None:
    items = _array(value, field, errors)
    if items is None:
        return
    try:
        if len(items) != len(set(items)):
            errors.append(f"{field} must contain unique items")
    except TypeError:
        pass
    for index, item in enumerate(items):
        if paths:
            _repo_path(item, f"{field}[{index}]", errors)
        elif not isinstance(item, str) or not item.strip():
            errors.append(f"{field}[{index}] must be a non-empty string")


def _require_items(value: Any, field: str, errors: list[str]) -> None:
    if isinstance(value, list) and not value:
        errors.append(f"{field} must contain at least one item")


def _boundary(value: Any, field: str, errors: list[str]) -> None:
    data = _mapping(value, field, errors)
    if data is None:
        return
    required = {"base_commit", "candidate_commit", "diff_sha256", "artifact_hashes", "owned_paths"}
    _required(data, required, f"{field}.", errors)
    _unknown(data, required, f"{field}.", errors)
    _sha(data.get("base_commit"), f"{field}.base_commit", errors)
    _sha(data.get("candidate_commit"), f"{field}.candidate_commit", errors, nullable=True)
    _hash(data.get("diff_sha256"), f"{field}.diff_sha256", errors)
    hashes = _mapping(data.get("artifact_hashes"), f"{field}.artifact_hashes", errors)
    if hashes is not None:
        for path, digest in hashes.items():
            _repo_path(path, f"{field}.artifact_hashes key", errors)
            _hash(digest, f"{field}.artifact_hashes[{path}]", errors)
    _string_array(data.get("owned_paths"), f"{field}.owned_paths", errors, paths=True)


def validate_agent_file(path: Path) -> list[str]:
    try:
        with path.open("rb") as stream:
            data = tomllib.load(stream)
    except (OSError, tomllib.TOMLDecodeError) as error:
        return [f"invalid TOML: {error}"]
    if not isinstance(data, dict):
        return ["agent file must contain a TOML table"]
    errors = sorted(REQUIRED_AGENT_FIELDS - data.keys())
    for field in REQUIRED_AGENT_FIELDS & data.keys():
        _nonempty_string(data[field], field, errors)
    return errors


def validate_verdict(data: Any) -> list[str]:
    errors: list[str] = []
    verdict = _mapping(data, "verdict document", errors)
    if verdict is None:
        return errors
    _required(verdict, VERDICT_REQUIRED, "", errors)
    _unknown(verdict, VERDICT_REQUIRED, "", errors)
    if verdict.get("schema_version") != "1.0":
        errors.append("schema_version must equal 1.0")
    if verdict.get("gate") not in VALID_GATES:
        errors.append("gate is invalid")
    if verdict.get("verdict") not in VALID_VERDICTS:
        errors.append("verdict is invalid")
    _sha(verdict.get("reviewed_commit"), "reviewed_commit", errors)
    _boundary(verdict.get("reviewed_boundary"), "reviewed_boundary", errors)
    _string_array(verdict.get("reviewed_artifacts"), "reviewed_artifacts", errors, paths=True)
    _nonempty_string(verdict.get("summary"), "summary", errors)
    findings = _array(verdict.get("findings"), "findings", errors)
    severities: set[str] = set()
    if findings is not None:
        finding_fields = {
            "id", "severity", "category", "artifact", "location", "affected_requirement", "evidence",
            "correction_outcome",
        }
        for index, item in enumerate(findings):
            finding = _mapping(item, f"findings[{index}]", errors)
            if finding is None:
                continue
            _required(finding, finding_fields, f"findings[{index}].", errors)
            _unknown(finding, finding_fields, f"findings[{index}].", errors)
            for field in finding_fields - {"severity", "artifact"}:
                _nonempty_string(finding.get(field), f"findings[{index}].{field}", errors)
            _repo_path(finding.get("artifact"), f"findings[{index}].artifact", errors)
            severity = finding.get("severity")
            if severity not in VALID_SEVERITIES:
                errors.append(f"findings[{index}].severity is invalid")
            elif isinstance(severity, str):
                severities.add(severity)
    if verdict.get("verdict") == "ACCEPT" and severities & BLOCKING_SEVERITIES:
        errors.append("ACCEPT verdict contains blocking findings")
    if verdict.get("verdict") == "REVISE" and not (severities & BLOCKING_SEVERITIES):
        errors.append("REVISE verdict requires blocking findings")
    return errors


def _validate_gate_record(value: Any, field: str, errors: list[str]) -> None:
    data = _mapping(value, field, errors)
    if data is None:
        return
    required = {"status", "iteration", "dispatch_id", "verdict_path", "input_boundary", "completed_at"}
    _required(data, required, f"{field}.", errors)
    _unknown(data, required, f"{field}.", errors)
    if data.get("status") not in VALID_STATUSES:
        errors.append(f"{field}.status is invalid")
    if not isinstance(data.get("iteration"), int) or isinstance(data.get("iteration"), bool) or data.get("iteration", -1) < 0:
        errors.append(f"{field}.iteration must be a non-negative integer")
    dispatch_id = data.get("dispatch_id")
    if dispatch_id is not None:
        _nonempty_string(dispatch_id, f"{field}.dispatch_id", errors)
    _repo_path(data.get("verdict_path"), f"{field}.verdict_path", errors, nullable=True)
    if data.get("input_boundary") is not None:
        _boundary(data["input_boundary"], f"{field}.input_boundary", errors)
    _timestamp(data.get("completed_at"), f"{field}.completed_at", errors, nullable=True)
    if data.get("status") == "accepted":
        if data.get("verdict_path") is None:
            errors.append(f"{field}.verdict_path is required when accepted")
        if data.get("input_boundary") is None:
            errors.append(f"{field}.input_boundary is required when accepted")
        if data.get("completed_at") is None:
            errors.append(f"{field}.completed_at is required when accepted")


def _validate_evidence(value: Any, field: str, errors: list[str], story_key: str | None) -> None:
    data = _mapping(value, field, errors)
    if data is None:
        return
    required = {
        "evidence_id", "kind", "stage", "source_commit", "artifact_paths", "command", "exit_code",
        "observed_at", "summary", "raw_output_path",
    }
    _required(data, required, f"{field}.", errors)
    _unknown(data, required, f"{field}.", errors)
    _nonempty_string(data.get("evidence_id"), f"{field}.evidence_id", errors)
    if data.get("kind") not in VALID_EVIDENCE_KINDS:
        errors.append(f"{field}.kind is invalid")
    if data.get("stage") not in VALID_STAGES:
        errors.append(f"{field}.stage is invalid")
    evidence_id = data.get("evidence_id")
    if isinstance(evidence_id, str):
        match = re.fullmatch(r"([^/]+)/([^/]+)/([1-9]\d*)", evidence_id)
        if (
            match is None
            or story_key is None
            or match.group(1) != story_key
            or match.group(2) != data.get("stage")
            or match.group(2) not in VALID_STAGES
        ):
            errors.append(f"{field}.evidence_id must match story-key/stage/positive-sequence")
    _sha(data.get("source_commit"), f"{field}.source_commit", errors)
    _string_array(data.get("artifact_paths"), f"{field}.artifact_paths", errors, paths=True)
    if data.get("command") is not None:
        _nonempty_string(data.get("command"), f"{field}.command", errors)
    if data.get("exit_code") is not None and (not isinstance(data.get("exit_code"), int) or isinstance(data.get("exit_code"), bool)):
        errors.append(f"{field}.exit_code must be an integer or null")
    _timestamp(data.get("observed_at"), f"{field}.observed_at", errors)
    _nonempty_string(data.get("summary"), f"{field}.summary", errors)
    _repo_path(data.get("raw_output_path"), f"{field}.raw_output_path", errors, nullable=True)


def _validate_checkpoint(value: Any, field: str, errors: list[str]) -> None:
    data = _mapping(value, field, errors)
    if data is None:
        return
    required = {
        "checkpoint_id", "scope_key", "stage", "committed_paths", "verification_evidence_ids", "commit_sha",
        "binding_status",
    }
    _required(data, required, f"{field}.", errors)
    _unknown(data, required, f"{field}.", errors)
    _nonempty_string(data.get("checkpoint_id"), f"{field}.checkpoint_id", errors)
    _nonempty_string(data.get("scope_key"), f"{field}.scope_key", errors)
    if data.get("stage") not in VALID_STAGES:
        errors.append(f"{field}.stage is invalid")
    _string_array(data.get("committed_paths"), f"{field}.committed_paths", errors, paths=True)
    _string_array(data.get("verification_evidence_ids"), f"{field}.verification_evidence_ids", errors)
    binding = data.get("binding_status")
    if binding not in {"pending", "bound"}:
        errors.append(f"{field}.binding_status is invalid")
    if binding == "pending" and data.get("commit_sha") is not None:
        errors.append(f"{field}.commit_sha must be null while binding is pending")
    if binding == "bound":
        _sha(data.get("commit_sha"), f"{field}.commit_sha", errors)


def validate_workflow_state(data: Any) -> list[str]:
    errors: list[str] = []
    state = _mapping(data, "workflow state", errors)
    if state is None:
        return errors
    _required(state, STATE_REQUIRED, "", errors)
    _unknown(state, STATE_REQUIRED, "", errors)
    if state.get("schema_version") != "1.0":
        errors.append("schema_version must equal 1.0")
    _nonempty_string(state.get("bmad_story_key"), "bmad_story_key", errors)
    _nonempty_string(state.get("bmad_epic_key"), "bmad_epic_key", errors)
    _repo_path(state.get("feature_directory"), "feature_directory", errors)
    if isinstance(state.get("feature_directory"), str) and not re.fullmatch(r"specs/[^/]+", state["feature_directory"]):
        errors.append("feature_directory must match specs/<feature>")
    if state.get("stage") not in VALID_STAGES:
        errors.append("stage is invalid")

    gates = _mapping(state.get("active_gates"), "active_gates", errors)
    if gates is not None:
        _unknown(gates, {"acceptance-review", "quality-review"}, "active_gates.", errors)
        for name in ("acceptance-review", "quality-review"):
            if name not in gates:
                errors.append(f"active_gates.{name}")
            else:
                _validate_gate_record(gates[name], f"active_gates.{name}", errors)

    history = _array(state.get("stage_history"), "stage_history", errors)
    if history is not None:
        required = {
            "stage", "status", "started_at", "completed_at", "input_commit", "input_hashes", "agent_role",
            "evidence_paths", "failure",
        }
        for index, item in enumerate(history):
            record = _mapping(item, f"stage_history[{index}]", errors)
            if record is None:
                continue
            _required(record, required, f"stage_history[{index}].", errors)
            _unknown(record, required, f"stage_history[{index}].", errors)
            if record.get("stage") not in VALID_STAGES:
                errors.append(f"stage_history[{index}].stage is invalid")
            if record.get("status") not in VALID_STATUSES:
                errors.append(f"stage_history[{index}].status is invalid")
            _timestamp(record.get("started_at"), f"stage_history[{index}].started_at", errors)
            _timestamp(record.get("completed_at"), f"stage_history[{index}].completed_at", errors, nullable=True)
            _sha(record.get("input_commit"), f"stage_history[{index}].input_commit", errors)
            hashes = _mapping(record.get("input_hashes"), f"stage_history[{index}].input_hashes", errors)
            if hashes is not None:
                for path, digest in hashes.items():
                    _repo_path(path, f"stage_history[{index}].input_hashes key", errors)
                    _hash(digest, f"stage_history[{index}].input_hashes[{path}]", errors)
            _nonempty_string(record.get("agent_role"), f"stage_history[{index}].agent_role", errors)
            _string_array(record.get("evidence_paths"), f"stage_history[{index}].evidence_paths", errors, paths=True)
            if record.get("status") == "accepted" and not record.get("evidence_paths"):
                errors.append(f"stage_history[{index}].evidence_paths must not be empty when accepted")
            if record.get("failure") is not None:
                _mapping(record["failure"], f"stage_history[{index}].failure", errors)

    preflight = _mapping(state.get("repository_preflight"), "repository_preflight", errors)
    if preflight is not None:
        fields = {
            "branch", "preflight_head", "repository_root", "git_common_directory", "worktree_path",
            "worktree_status", "owned_paths", "unrelated_paths", "observed_at",
        }
        _required(preflight, fields, "repository_preflight.", errors)
        _unknown(preflight, fields, "repository_preflight.", errors)
        _nonempty_string(preflight.get("branch"), "repository_preflight.branch", errors)
        _sha(preflight.get("preflight_head"), "repository_preflight.preflight_head", errors)
        for key in ("repository_root", "git_common_directory", "worktree_path"):
            _repo_path(preflight.get(key), f"repository_preflight.{key}", errors)
        if not isinstance(preflight.get("worktree_status"), str):
            errors.append("repository_preflight.worktree_status must be a string")
        _string_array(preflight.get("owned_paths"), "repository_preflight.owned_paths", errors, paths=True)
        _string_array(preflight.get("unrelated_paths"), "repository_preflight.unrelated_paths", errors, paths=True)
        _timestamp(preflight.get("observed_at"), "repository_preflight.observed_at", errors)

    sources = _array(state.get("source_artifacts"), "source_artifacts", errors)
    if sources is not None:
        for index, item in enumerate(sources):
            source = _mapping(item, f"source_artifacts[{index}]", errors)
            if source is None:
                continue
            _required(source, {"authority", "path", "commit", "sha256"}, f"source_artifacts[{index}].", errors)
            _unknown(source, {"authority", "path", "commit", "sha256"}, f"source_artifacts[{index}].", errors)
            _nonempty_string(source.get("authority"), f"source_artifacts[{index}].authority", errors)
            _repo_path(source.get("path"), f"source_artifacts[{index}].path", errors)
            _sha(source.get("commit"), f"source_artifacts[{index}].commit", errors)
            _hash(source.get("sha256"), f"source_artifacts[{index}].sha256", errors)

    dispatches = _array(state.get("dispatches"), "dispatches", errors)
    if dispatches is not None:
        fields = {"role", "objective", "source_boundary", "permitted_writes", "output_path", "returned_evidence"}
        for index, item in enumerate(dispatches):
            dispatch = _mapping(item, f"dispatches[{index}]", errors)
            if dispatch is None:
                continue
            _required(dispatch, fields, f"dispatches[{index}].", errors)
            _unknown(dispatch, fields, f"dispatches[{index}].", errors)
            _nonempty_string(dispatch.get("role"), f"dispatches[{index}].role", errors)
            _nonempty_string(dispatch.get("objective"), f"dispatches[{index}].objective", errors)
            _boundary(dispatch.get("source_boundary"), f"dispatches[{index}].source_boundary", errors)
            _string_array(dispatch.get("permitted_writes"), f"dispatches[{index}].permitted_writes", errors, paths=True)
            _repo_path(dispatch.get("output_path"), f"dispatches[{index}].output_path", errors)
            _string_array(dispatch.get("returned_evidence"), f"dispatches[{index}].returned_evidence", errors)

    iterations = _mapping(state.get("review_iterations"), "review_iterations", errors)
    if iterations is not None:
        _unknown(iterations, VALID_GATES, "review_iterations.", errors)
        for gate in VALID_GATES:
            if gate not in iterations:
                errors.append(f"review_iterations.{gate}")
                continue
            record = _mapping(iterations[gate], f"review_iterations.{gate}", errors)
            if record is None:
                continue
            _required(record, {"count", "verdict_paths"}, f"review_iterations.{gate}.", errors)
            _unknown(record, {"count", "verdict_paths"}, f"review_iterations.{gate}.", errors)
            count = record.get("count")
            if not isinstance(count, int) or isinstance(count, bool) or count < 0:
                errors.append(f"review_iterations.{gate}.count must be a non-negative integer")
            _string_array(record.get("verdict_paths"), f"review_iterations.{gate}.verdict_paths", errors, paths=True)

    e2e = _mapping(state.get("e2e_decision"), "e2e_decision", errors)
    if e2e is not None:
        fields = {
            "required", "predicates", "coverage_override", "rationale", "covering_tests", "owner",
            "source_boundary", "result", "evidence_ids",
        }
        _required(e2e, fields, "e2e_decision.", errors)
        _unknown(e2e, fields, "e2e_decision.", errors)
        if not isinstance(e2e.get("required"), bool):
            errors.append("e2e_decision.required must be a boolean")
        required_predicates: set[str] = set()
        predicate_ids: set[str] = set()
        predicates = _array(e2e.get("predicates"), "e2e_decision.predicates", errors)
        if predicates is not None:
            predicate_fields = {
                "predicate_id", "change_type", "gate", "triggered", "affected_paths", "affected_behavior", "evidence",
            }
            for index, item in enumerate(predicates):
                predicate = _mapping(item, f"e2e_decision.predicates[{index}]", errors)
                if predicate is None:
                    continue
                _required(predicate, predicate_fields, f"e2e_decision.predicates[{index}].", errors)
                _unknown(predicate, predicate_fields, f"e2e_decision.predicates[{index}].", errors)
                predicate_id = predicate.get("predicate_id")
                _nonempty_string(predicate_id, f"e2e_decision.predicates[{index}].predicate_id", errors)
                if isinstance(predicate_id, str):
                    if predicate_id in predicate_ids:
                        errors.append("e2e_decision.predicates must have unique predicate_id values")
                    predicate_ids.add(predicate_id)
                _nonempty_string(predicate.get("change_type"), f"e2e_decision.predicates[{index}].change_type", errors)
                if predicate.get("gate") not in {"required", "may-skip"}:
                    errors.append(f"e2e_decision.predicates[{index}].gate is invalid")
                if not isinstance(predicate.get("triggered"), bool):
                    errors.append(f"e2e_decision.predicates[{index}].triggered must be a boolean")
                _string_array(predicate.get("affected_paths"), f"e2e_decision.predicates[{index}].affected_paths", errors, paths=True)
                _nonempty_string(predicate.get("affected_behavior"), f"e2e_decision.predicates[{index}].affected_behavior", errors)
                _string_array(predicate.get("evidence"), f"e2e_decision.predicates[{index}].evidence", errors)
                if predicate.get("triggered") is True and predicate.get("gate") == "required" and isinstance(predicate_id, str):
                    required_predicates.add(predicate_id)
        complete_override = False
        override_value = e2e.get("coverage_override")
        if override_value is not None:
            override = _mapping(override_value, "e2e_decision.coverage_override", errors)
            if override is not None:
                override_fields = {
                    "test_ids", "covered_predicate_ids", "source_boundary", "passing_evidence_ids",
                    "completeness_rationale",
                }
                _required(override, override_fields, "e2e_decision.coverage_override.", errors)
                _unknown(override, override_fields, "e2e_decision.coverage_override.", errors)
                _string_array(override.get("test_ids"), "e2e_decision.coverage_override.test_ids", errors)
                _string_array(override.get("covered_predicate_ids"), "e2e_decision.coverage_override.covered_predicate_ids", errors)
                _boundary(override.get("source_boundary"), "e2e_decision.coverage_override.source_boundary", errors)
                _string_array(override.get("passing_evidence_ids"), "e2e_decision.coverage_override.passing_evidence_ids", errors)
                _require_items(override.get("test_ids"), "e2e_decision.coverage_override.test_ids", errors)
                _require_items(
                    override.get("covered_predicate_ids"),
                    "e2e_decision.coverage_override.covered_predicate_ids",
                    errors,
                )
                _require_items(
                    override.get("passing_evidence_ids"),
                    "e2e_decision.coverage_override.passing_evidence_ids",
                    errors,
                )
                _nonempty_string(override.get("completeness_rationale"), "e2e_decision.coverage_override.completeness_rationale", errors)
                covered = override.get("covered_predicate_ids")
                tests = override.get("test_ids")
                passing = override.get("passing_evidence_ids")
                current_boundary = override.get("source_boundary") == e2e.get("source_boundary")
                complete_override = (
                    isinstance(covered, list)
                    and all(isinstance(item, str) for item in covered)
                    and required_predicates <= set(covered)
                    and isinstance(tests, list) and bool(tests)
                    and isinstance(passing, list) and bool(passing)
                    and current_boundary
                    and isinstance(override.get("completeness_rationale"), str)
                    and bool(override["completeness_rationale"].strip())
                )
                if not current_boundary:
                    errors.append("e2e_decision.coverage_override.source_boundary must match current source_boundary")
                if not required_predicates <= set(covered) if isinstance(covered, list) and all(isinstance(item, str) for item in covered) else True:
                    errors.append("e2e_decision.coverage_override must cover every triggered required predicate")
        _nonempty_string(e2e.get("rationale"), "e2e_decision.rationale", errors)
        _string_array(e2e.get("covering_tests"), "e2e_decision.covering_tests", errors)
        _nonempty_string(e2e.get("owner"), "e2e_decision.owner", errors)
        if e2e.get("owner") not in {"orchestrator", "e2e-runner"}:
            errors.append("e2e_decision.owner is invalid")
        _boundary(e2e.get("source_boundary"), "e2e_decision.source_boundary", errors)
        if e2e.get("result") not in VALID_E2E_RESULTS:
            errors.append("e2e_decision.result is invalid")
        _string_array(e2e.get("evidence_ids"), "e2e_decision.evidence_ids", errors)
        if e2e.get("required") is True and e2e.get("owner") != "e2e-runner":
            errors.append("e2e_decision.owner must be e2e-runner when required")
        if e2e.get("result") == "skipped-covered" and not complete_override:
            errors.append("e2e_decision.coverage_override is required and complete for skipped-covered")
        if e2e.get("result") == "skipped-covered":
            _require_items(e2e.get("covering_tests"), "e2e_decision.covering_tests", errors)
        if required_predicates and e2e.get("required") is not True and not (
            e2e.get("result") == "skipped-covered" and complete_override
        ):
            errors.append("e2e_decision.required must be true without a complete coverage override")

    verification = _array(state.get("verification"), "verification", errors)
    if verification is not None:
        for index, evidence in enumerate(verification):
            _validate_evidence(evidence, f"verification[{index}]", errors, state.get("bmad_story_key"))

    checkpoint = state.get("last_checkpoint")
    if checkpoint is not None:
        _validate_checkpoint(checkpoint, "last_checkpoint", errors)
    failure = state.get("recoverable_failure")
    if failure is not None:
        failure_data = _mapping(failure, "recoverable_failure", errors)
        if failure_data is not None:
            fields = {"stage", "agent_role", "source_boundary", "evidence", "retryable", "earliest_resume_stage"}
            _required(failure_data, fields, "recoverable_failure.", errors)
            _unknown(failure_data, fields, "recoverable_failure.", errors)
            if failure_data.get("stage") not in VALID_STAGES:
                errors.append("recoverable_failure.stage is invalid")
            _nonempty_string(failure_data.get("agent_role"), "recoverable_failure.agent_role", errors)
            _boundary(failure_data.get("source_boundary"), "recoverable_failure.source_boundary", errors)
            _validate_evidence(
                failure_data.get("evidence"), "recoverable_failure.evidence", errors, state.get("bmad_story_key")
            )
            if not isinstance(failure_data.get("retryable"), bool):
                errors.append("recoverable_failure.retryable must be a boolean")
            if failure_data.get("earliest_resume_stage") not in VALID_STAGES:
                errors.append("recoverable_failure.earliest_resume_stage is invalid")
    _timestamp(state.get("updated_at"), "updated_at", errors)

    if state.get("stage") == "complete":
        if checkpoint is None:
            errors.append("last_checkpoint is required for provisional complete state")
        elif isinstance(checkpoint, dict) and checkpoint.get("stage") != "complete":
            errors.append("last_checkpoint.stage must be complete for provisional complete state")
    return errors


def _load_json(path: Path, errors: list[str]) -> dict[str, Any] | None:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except OSError as error:
        errors.append(f"{path}: cannot read: {error}")
        return None
    except json.JSONDecodeError as error:
        errors.append(f"{path}: invalid JSON: {error.msg} at line {error.lineno} column {error.colno}")
        return None
    if not isinstance(data, dict):
        errors.append(f"{path}: schema root must be an object")
        return None
    return data


def _schema_at(schema: dict[str, Any], *keys: str) -> Any:
    value: Any = schema
    for key in keys:
        if not isinstance(value, dict) or key not in value:
            return None
        value = value[key]
    return value


def _schema_enum(schema: dict[str, Any], *keys: str) -> set[str] | None:
    value = _schema_at(schema, *keys)
    if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
        return None
    return set(value)


def _validate_schema_semantics(filename: str, schema: dict[str, Any], path: Path, errors: list[str]) -> None:
    if filename == "verdict.schema.json":
        checks = [
            (_schema_at(schema, "properties", "schema_version", "const") == "1.0", "schema version const"),
            (_schema_enum(schema, "properties", "verdict", "enum") == VALID_VERDICTS, "verdict enum"),
            (_schema_enum(schema, "properties", "gate", "enum") == VALID_GATES, "gate enum"),
            (
                _schema_enum(schema, "$defs", "finding", "properties", "severity", "enum")
                == VALID_SEVERITIES,
                "finding severity enum",
            ),
            (
                _schema_at(schema, "properties", "reviewed_boundary", "$ref") == "#/$defs/boundary",
                "reviewed boundary reference",
            ),
            (
                _schema_at(schema, "$defs", "boundary", "additionalProperties") is False
                and _schema_at(schema, "$defs", "finding", "additionalProperties") is False,
                "strict nested objects",
            ),
        ]
        rules = schema.get("allOf")
        rule_verdicts = {
            _schema_at(rule, "if", "properties", "verdict", "const")
            for rule in rules if isinstance(rules, list) and isinstance(rule, dict)
        } if isinstance(rules, list) else set()
        checks.append(({"ACCEPT", "REVISE"} <= rule_verdicts, "verdict conditional rules"))
    else:
        coverage = _schema_at(schema, "$defs", "coverageOverride", "properties")
        coverage_nonempty = isinstance(coverage, dict) and all(
            _schema_at(coverage, field, "minItems") == 1
            for field in ("test_ids", "covered_predicate_ids", "passing_evidence_ids")
        )
        e2e_rules = _schema_at(schema, "$defs", "e2eDecision", "allOf")
        skipped_rule = next((
            rule for rule in e2e_rules
            if isinstance(e2e_rules, list)
            and _schema_at(rule, "if", "properties", "result", "const") == "skipped-covered"
        ), None) if isinstance(e2e_rules, list) else None
        checks = [
            (_schema_at(schema, "properties", "schema_version", "const") == "1.0", "schema version const"),
            (_schema_enum(schema, "$defs", "stage", "enum") == VALID_STAGES, "stage enum"),
            (_schema_enum(schema, "$defs", "status", "enum") == VALID_STATUSES, "status enum"),
            (
                _schema_enum(schema, "$defs", "e2eDecision", "properties", "result", "enum")
                == VALID_E2E_RESULTS,
                "E2E result enum",
            ),
            (
                _schema_enum(schema, "$defs", "evidence", "properties", "kind", "enum")
                == VALID_EVIDENCE_KINDS,
                "evidence kind enum",
            ),
            (
                _schema_at(schema, "$defs", "e2eDecision", "properties", "source_boundary", "$ref")
                == "#/$defs/boundary",
                "E2E source boundary reference",
            ),
            (coverage_nonempty, "coverage override nonempty arrays"),
            (
                isinstance(skipped_rule, dict)
                and _schema_at(skipped_rule, "then", "properties", "coverage_override", "$ref")
                == "#/$defs/coverageOverride"
                and _schema_at(skipped_rule, "then", "properties", "covering_tests", "minItems") == 1,
                "skipped-covered conditional rule",
            ),
        ]
    for valid, label in checks:
        if not valid:
            errors.append(f"{path}: {label} is inconsistent with the runtime contract")


def validate_repository(repo: Path) -> list[str]:
    repo = Path(repo)
    errors: list[str] = []
    if not repo.exists():
        return [f"{repo}: repository path does not exist"]
    if not repo.is_dir():
        return [f"{repo}: repository path is not a directory"]

    config_path = repo / ".codex" / "config.toml"
    try:
        with config_path.open("rb") as stream:
            config = tomllib.load(stream)
    except FileNotFoundError:
        errors.append(f"{config_path}: missing required file")
        config = {}
    except (OSError, tomllib.TOMLDecodeError) as error:
        errors.append(f"{config_path}: invalid TOML: {error}")
        config = {}
    agents_config = config.get("agents") if isinstance(config, dict) else None
    if not isinstance(agents_config, dict):
        errors.append(f"{config_path}: missing [agents] table")
    else:
        if agents_config.get("max_threads") != 4:
            errors.append(f"{config_path}: agents.max_threads must equal 4")
        if agents_config.get("max_depth") != 1:
            errors.append(f"{config_path}: agents.max_depth must equal 1")

    agent_dir = repo / ".codex" / "agents"
    paths = sorted(agent_dir.glob("*.toml")) if agent_dir.is_dir() else []
    expected_files = {f"{name}.toml" for name in ROLE_PROFILES}
    actual_files = {path.name for path in paths}
    for name in sorted(expected_files - actual_files):
        errors.append(f"{agent_dir / name}: missing required agent file")
    for name in sorted(actual_files - expected_files):
        errors.append(f"{agent_dir / name}: unexpected agent file")
    seen: set[str] = set()
    for path in paths:
        file_errors = validate_agent_file(path)
        errors.extend(f"{path}: {error}" for error in file_errors)
        try:
            with path.open("rb") as stream:
                data = tomllib.load(stream)
        except (OSError, tomllib.TOMLDecodeError):
            continue
        name = data.get("name")
        if name != path.stem:
            errors.append(f"{path}: name must match filename stem {path.stem}")
        if isinstance(name, str) and name in seen:
            errors.append(f"{path}: duplicate agent name {name}")
        if isinstance(name, str):
            seen.add(name)
        if name in ROLE_PROFILES:
            expected = ROLE_PROFILES[name]
            actual = (data.get("model"), data.get("model_reasoning_effort"), data.get("sandbox_mode"))
            if actual != expected:
                errors.append(f"{path}: profile must be model={expected[0]}, effort={expected[1]}, sandbox={expected[2]}")
            instructions = data.get("developer_instructions")
            if isinstance(instructions, str):
                if "seven-slot dispatch contract" not in instructions:
                    errors.append(f"{path}: developer_instructions must require the seven-slot dispatch contract")
                if "commit" not in instructions.lower():
                    errors.append(f"{path}: developer_instructions must define commit boundaries")
                lowered = instructions.lower()
                for marker in ROLE_INSTRUCTION_MARKERS[name]:
                    if marker not in lowered:
                        errors.append(f"{path}: developer_instructions missing required role rule: {marker}")

    refs = repo / ".agents" / "skills" / "run-bmad-sprint-loop" / "references"
    schemas = {
        "verdict.schema.json": VERDICT_REQUIRED,
        "workflow-state.schema.json": STATE_REQUIRED,
    }
    schema_defs = {
        "verdict.schema.json": {"boundary", "finding"},
        "workflow-state.schema.json": {
            "boundary", "gateRecord", "stageRecord", "preflight", "sourceArtifact", "dispatch",
            "reviewIteration", "e2ePredicate", "coverageOverride", "e2eDecision", "evidence", "checkpoint", "failure",
        },
    }
    for filename, required in schemas.items():
        path = refs / filename
        if not path.is_file():
            errors.append(f"{path}: missing required schema")
            continue
        schema = _load_json(path, errors)
        if schema is None:
            continue
        if schema.get("$schema") != "https://json-schema.org/draft/2020-12/schema":
            errors.append(f"{path}: must use JSON Schema draft 2020-12")
        schema_required = schema.get("required")
        if not isinstance(schema_required, list) or set(schema_required) != required:
            errors.append(f"{path}: required fields are inconsistent with the runtime validator")
        definitions = schema.get("$defs")
        if not isinstance(definitions, dict) or not schema_defs[filename] <= definitions.keys():
            errors.append(f"{path}: nested definitions are incomplete")
        semantic_rules = schema.get("allOf")
        minimum_rules = 2 if filename == "verdict.schema.json" else 1
        if not isinstance(semantic_rules, list) or len(semantic_rules) < minimum_rules:
            errors.append(f"{path}: semantic contract rules are incomplete")
        _validate_schema_semantics(filename, schema, path, errors)
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    args = parser.parse_args()
    errors = validate_repository(args.repo)
    if errors:
        print("\n".join(errors))
        return 1
    print("Workflow configuration and contracts valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
