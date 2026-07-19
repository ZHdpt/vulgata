from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import tomllib
import unicodedata
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
    "epic-architecture-auditor": (
        "independent explore/classification judge", "canonical absolute audit root", "git common/worktree identities",
        "raw_candidate_id", "all applicable categories", "pinned blob oid", "excerpt sha-256",
        "do not write or open the html report", "never implement",
    ),
}

VERDICT_REQUIRED = {
    "schema_version", "gate", "verdict", "reviewed_commit", "reviewed_boundary",
    "reviewed_artifacts", "findings", "summary",
}
STATE_REQUIRED = {
    "schema_version", "bmad_story_key", "bmad_epic_key", "feature_directory", "execution_level", "stage", "active_gates",
    "stage_history", "repository_preflight", "source_artifacts", "dispatches", "review_iterations",
    "e2e_decision", "verification", "last_checkpoint", "recoverable_failure", "updated_at",
}
EPIC_EVIDENCE_REQUIRED = {
    "schema_version", "evidence_id", "kind", "stage", "source_commit", "artifact_paths", "command",
    "exit_code", "observed_at", "summary", "raw_output_path", "raw_output_sha256", "execution_worktree",
    "execution_worktree_head", "execution_worktree_git_identity", "boundary_clean", "source_equality_validated_at", "environment_summary",
}
EPIC_GATE_REQUIRED = {
    "schema_version", "epic_key", "status", "transaction_phase", "source_commit", "repository_preflight",
    "verification_boundary", "audit_validation", "complete_findings", "dispositions", "build_evidence_paths",
    "test_evidence_paths", "completion_status_paths", "durable_artifact_paths", "retrospective_decision", "checkpoint", "manifest_constraints",
    "earliest_safe_resume_step", "retryability", "failure",
}
RETRO_REQUIRED = {
    "schema_version", "epic_key", "status", "source_epic_checkpoint", "hooks", "write_manifest", "approved_manifest_sha256",
    "evidence_paths", "failure", "checkpoint",
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
    if state.get("execution_level") not in {"full", "standard"}:
        errors.append("execution_level must be full or standard")
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
        if state.get("execution_level") == "standard":
            acceptance = gates.get("acceptance-review")
            if not isinstance(acceptance, dict) or acceptance.get("status") != "invalidated":
                errors.append(
                    "active_gates.acceptance-review.status must be invalidated for standard execution_level"
                )
            elif (
                acceptance.get("iteration") != 0
                or any(
                    acceptance.get(field) is not None
                    for field in ("dispatch_id", "verdict_path", "input_boundary", "completed_at")
                )
            ):
                errors.append(
                    "active_gates.acceptance-review must be unused at iteration zero for standard execution_level"
                )

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


def _git(repo: Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(repo), *args], text=True, stderr=subprocess.DEVNULL).strip()


def _has_trailer(body: str, key: str, value: str) -> bool:
    return re.search(rf"^{re.escape(key)}: {re.escape(value)}$", body, re.MULTILINE) is not None


def _tree_entry(repo: Path, commit: str, path: str) -> tuple[str, str, str] | None:
    output = _git(repo, "ls-tree", commit, "--", path)
    if not output:
        return None
    metadata, returned_path = output.split("\t", 1)
    if returned_path != path:
        return None
    mode, object_type, oid = metadata.split()
    return mode, object_type, oid


def _index_entry(repo: Path, path: str) -> tuple[str, str, str] | None:
    output = _git(repo, "ls-files", "--stage", "--", path)
    if not output:
        return None
    lines = output.splitlines()
    if len(lines) != 1:
        raise ValueError("index path has multiple stages")
    metadata, returned_path = lines[0].split("\t", 1)
    mode, oid, stage = metadata.split()
    if returned_path != path or stage != "0":
        raise ValueError("index path is not a single stage-0 entry")
    object_type = _git(repo, "cat-file", "-t", oid)
    return mode, object_type, oid


def validate_epic_command_evidence(
    data: Any, repo: Path | None = None, expected_epic: str | None = None, expected_stage: str | None = None
) -> list[str]:
    errors: list[str] = []
    root = _mapping(data, "evidence", errors)
    if root is None:
        return errors
    _required(root, EPIC_EVIDENCE_REQUIRED, "missing ", errors)
    _unknown(root, EPIC_EVIDENCE_REQUIRED, "evidence.", errors)
    if root.get("schema_version") != "1.0": errors.append("evidence.schema_version must equal 1.0")
    if root.get("kind") != "command": errors.append("evidence.kind must equal command")
    if root.get("exit_code") != 0: errors.append("epic close command evidence must have exit_code 0")
    expected_commands = {
        "verify-build": "dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj",
        "verify-test": "dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj",
    }
    if root.get("command") != expected_commands.get(root.get("stage")):
        errors.append("epic close command does not match its verification stage")
    _sha(root.get("source_commit"), "evidence.source_commit", errors)
    _sha(root.get("execution_worktree_head"), "evidence.execution_worktree_head", errors)
    if root.get("source_commit") != root.get("execution_worktree_head"):
        errors.append("evidence execution worktree HEAD must equal source commit")
    if root.get("boundary_clean") is not True: errors.append("evidence.boundary_clean must be true")
    _hash(root.get("raw_output_sha256"), "evidence.raw_output_sha256", errors)
    _repo_path(root.get("raw_output_path"), "evidence.raw_output_path", errors)
    evidence_id = root.get("evidence_id")
    epic_key = evidence_id.split("/", 1)[0] if isinstance(evidence_id, str) and "/" in evidence_id else None
    if expected_epic is not None and epic_key != expected_epic:
        errors.append("epic command evidence belongs to a different epic")
    if expected_stage is not None and root.get("stage") != expected_stage:
        errors.append("epic command evidence belongs to the wrong verification stage")
    expected_prefix = f"docs/bmad/implementation-artifacts/epic-gates/{epic_key}/evidence/" if epic_key else None
    raw_path = root.get("raw_output_path")
    if expected_prefix is None or not isinstance(raw_path, str) or not raw_path.startswith(expected_prefix):
        errors.append("epic close raw output must live under its epic gate evidence directory")
    if isinstance(root.get("artifact_paths"), list) and raw_path not in root.get("artifact_paths"):
        errors.append("raw output path must be listed in artifact_paths")
    if repo is not None and isinstance(raw_path, str):
        candidate = Path(repo) / raw_path
        if not candidate.is_file():
            errors.append("epic close raw output file is missing")
        else:
            actual_hash = hashlib.sha256(candidate.read_bytes()).hexdigest()
            if actual_hash.lower() != str(root.get("raw_output_sha256", "")).lower():
                errors.append("epic close raw output hash does not match")
            raw_text = candidate.read_text(encoding="utf-8", errors="replace")
            if re.search(r"(?:Build|Test Run) FAILED|\bFailed!\b|[1-9][0-9]* Error\(s\)", raw_text, re.IGNORECASE):
                errors.append("epic close raw output reports failure despite exit_code 0")
    _timestamp(root.get("observed_at"), "evidence.observed_at", errors)
    _timestamp(root.get("source_equality_validated_at"), "evidence.source_equality_validated_at", errors)
    _nonempty_string(root.get("execution_worktree_git_identity"), "evidence.execution_worktree_git_identity", errors)
    return errors


def validate_epic_gate(data: Any, repo: Path | None = None) -> list[str]:
    errors: list[str] = []
    root = _mapping(data, "epic gate", errors)
    if root is None:
        return errors
    _required(root, EPIC_GATE_REQUIRED, "missing ", errors)
    _unknown(root, EPIC_GATE_REQUIRED, "epic_gate.", errors)
    if root.get("schema_version") != "1.0": errors.append("epic_gate.schema_version must equal 1.0")
    _sha(root.get("source_commit"), "epic_gate.source_commit", errors)
    phase = root.get("transaction_phase")
    if phase not in {"idle", "prepared", "pending-binding"}: errors.append("epic_gate.transaction_phase is invalid")
    if phase == "pending-binding" and root.get("status") != "provisional":
        errors.append("pending-binding epic gate must be provisional")
    if phase == "pending-binding" and repo is None:
        errors.append("pending-binding epic gate validation requires repository context")
    if phase == "pending-binding" and root.get("retrospective_decision") not in {"run-after-close", "declined"}:
        errors.append("pending-binding epic gate requires a retrospective decision")
    preflight = _mapping(root.get("repository_preflight"), "epic_gate.repository_preflight", errors)
    if preflight is not None:
        for field in ("branch", "repository_root", "git_common_dir", "worktree"):
            _nonempty_string(preflight.get(field), f"epic_gate.repository_preflight.{field}", errors)
        _sha(preflight.get("head"), "epic_gate.repository_preflight.head", errors)
        if preflight.get("head") != root.get("source_commit"):
            errors.append("epic gate preflight HEAD must equal source commit")
        if preflight.get("index_empty_at_start") is not True:
            errors.append("new epic close preflight index must be empty")
        preimages = preflight.get("target_preimages")
        if not isinstance(preimages, dict) or not preimages:
            errors.append("epic gate preflight requires nonempty target preimages")
        elif any(value is not None and (not isinstance(value, str) or not SHA_RE.fullmatch(value)) for value in preimages.values()):
            errors.append("epic gate target preimages must be blob OIDs or null")
        else:
            for path in preimages:
                _repo_path(path, f"epic gate target preimage path {path}", errors)
    boundary = _mapping(root.get("verification_boundary"), "epic_gate.verification_boundary", errors)
    if boundary is not None:
        if boundary.get("source_equal") is not True:
            errors.append("epic gate verification sources must be equal")
        for name in ("build", "audit"):
            worktree = boundary.get(name)
            if not isinstance(worktree, dict):
                errors.append(f"epic gate requires {name} worktree boundary")
                continue
            for field in ("path", "git_identity"):
                _nonempty_string(worktree.get(field), f"epic_gate.verification_boundary.{name}.{field}", errors)
            _sha(worktree.get("head"), f"epic_gate.verification_boundary.{name}.head", errors)
            _sha(worktree.get("tree_id"), f"epic_gate.verification_boundary.{name}.tree_id", errors)
            _sha(worktree.get("index_id"), f"epic_gate.verification_boundary.{name}.index_id", errors)
            if worktree.get("head") != root.get("source_commit"):
                errors.append(f"epic gate {name} worktree HEAD must equal source commit")
            if worktree.get("tree_id") != worktree.get("index_id"):
                errors.append(f"epic gate {name} tree and index IDs must match")
            if worktree.get("clean") is not True:
                errors.append(f"epic gate {name} worktree must be clean")
            if phase == "pending-binding" and worktree.get("disposed") is not True:
                errors.append(f"pending-binding requires disposed {name} worktree")
    audit = _mapping(root.get("audit_validation"), "epic_gate.audit_validation", errors)
    if audit is not None:
        for field in ("verdict", "audit_source_commit", "authoritative_input_hashes", "raw_response_path", "raw_response_sha256",
                      "report_temporary_path", "report_sha256", "audit_iteration", "normalized_payload_sha256",
                      "one_to_one_mapping", "citations_verified", "visible_fields_equal", "schema_valid",
                      "finding_count", "finding_ids", "reserved_finding_count", "validated_at"):
            if field not in audit: errors.append(f"missing epic_gate.audit_validation.{field}")
        _sha(audit.get("audit_source_commit"), "epic_gate.audit_validation.audit_source_commit", errors)
        if audit.get("verdict") not in VALID_VERDICTS:
            errors.append("epic architecture audit verdict is invalid")
        if audit.get("audit_source_commit") != root.get("source_commit"):
            errors.append("epic architecture audit source must equal gate source")
        input_hashes = audit.get("authoritative_input_hashes")
        if not isinstance(input_hashes, dict) or not input_hashes or any(not isinstance(value, str) or not HASH_RE.fullmatch(value) for value in input_hashes.values()):
            errors.append("epic architecture audit requires authoritative input hashes")
        _repo_path(audit.get("raw_response_path"), "epic_gate.audit_validation.raw_response_path", errors)
        expected_audit_prefix = f"docs/bmad/implementation-artifacts/epic-gates/{root.get('epic_key')}/audit-raw/"
        if not isinstance(audit.get("raw_response_path"), str) or not audit.get("raw_response_path").startswith(expected_audit_prefix):
            errors.append("epic architecture raw response path is outside its audit-raw directory")
        _hash(audit.get("raw_response_sha256"), "epic_gate.audit_validation.raw_response_sha256", errors)
        _nonempty_string(audit.get("report_temporary_path"), "epic_gate.audit_validation.report_temporary_path", errors)
        _hash(audit.get("report_sha256"), "epic_gate.audit_validation.report_sha256", errors)
        _hash(audit.get("normalized_payload_sha256"), "epic_gate.audit_validation.normalized_payload_sha256", errors)
        _timestamp(audit.get("validated_at"), "epic_gate.audit_validation.validated_at", errors)
        if not isinstance(audit.get("audit_iteration"), int) or audit.get("audit_iteration") < 1:
            errors.append("epic gate audit iteration must be positive")
        for field in ("one_to_one_mapping", "citations_verified", "visible_fields_equal", "schema_valid"):
            if audit.get(field) is not True: errors.append(f"epic_gate.audit_validation.{field} must be true")
        if audit.get("verdict") == "ACCEPT" and audit.get("reserved_finding_count") != 0:
            errors.append("ACCEPT architecture audit cannot contain reserved findings")
    findings = _array(root.get("complete_findings"), "epic_gate.complete_findings", errors)
    ids: set[str] = set()
    findings_by_id: dict[str, dict[str, Any]] = {}
    if findings is not None:
        for index, finding in enumerate(findings):
            item = _mapping(finding, f"epic_gate.complete_findings[{index}]", errors)
            if item is None: continue
            candidate_id = item.get("raw_candidate_id")
            _nonempty_string(candidate_id, f"epic_gate.complete_findings[{index}].raw_candidate_id", errors)
            if isinstance(candidate_id, str) and candidate_id in ids: errors.append(f"duplicate raw_candidate_id {candidate_id}")
            if isinstance(candidate_id, str): ids.add(candidate_id)
            if isinstance(candidate_id, str): findings_by_id[candidate_id] = item
            if item.get("strength") not in {"Strong", "Worth exploring", "Speculative"}: errors.append(f"finding {candidate_id} has invalid strength")
            if item.get("severity") not in VALID_SEVERITIES: errors.append(f"finding {candidate_id} has invalid severity")
            categories = item.get("applicable_categories")
            if not isinstance(categories, list) or not categories: errors.append(f"finding {candidate_id} requires applicable categories")
            elif any(category not in {"critical-high", "must", "strong", "adr-conflict", "ordinary"} for category in categories):
                errors.append(f"finding {candidate_id} has invalid applicable category")
            else:
                if item.get("severity") in BLOCKING_SEVERITIES and "critical-high" not in categories: errors.append(f"finding {candidate_id} missing critical-high category")
                if item.get("strength") == "Strong" and "strong" not in categories: errors.append(f"finding {candidate_id} missing strong category")
                if item.get("must_violation") is True and "must" not in categories: errors.append(f"finding {candidate_id} missing must category")
                if item.get("adr_conflict") is True and "adr-conflict" not in categories: errors.append(f"finding {candidate_id} missing ADR category")
            citations = item.get("citations")
            if not isinstance(citations, list) or not citations: errors.append(f"finding {candidate_id} requires citations")
            else:
                for citation_index, citation in enumerate(citations):
                    if not isinstance(citation, dict) or set(citation) != {"path", "blob_oid", "range", "excerpt_sha256"}:
                        errors.append(f"finding {candidate_id} citation {citation_index} is incomplete")
                        continue
                    _repo_path(citation.get("path"), f"finding {candidate_id} citation path", errors)
                    _sha(citation.get("blob_oid"), f"finding {candidate_id} citation blob", errors)
                    _nonempty_string(citation.get("range"), f"finding {candidate_id} citation range", errors)
                    _hash(citation.get("excerpt_sha256"), f"finding {candidate_id} citation excerpt", errors)
    if audit is not None:
        if audit.get("finding_count") != len(ids): errors.append("audit finding count must equal complete findings")
        if not isinstance(audit.get("finding_ids"), list) or set(audit.get("finding_ids")) != ids:
            errors.append("audit finding IDs must equal complete findings")
    dispositions = root.get("dispositions")
    disposition_ids: set[str] = set()
    allowed = {
        "critical-high": {"evidence-backed-false-positive"},
        "must": {"evidence-backed-false-positive"},
        "strong": {"load-bearing-rejection", "deferred-external-work", "accepted-known-debt"},
        "adr-conflict": {"conform-implementation", "supersede-or-amend-adr", "block"},
    }
    if isinstance(dispositions, list):
        for index, disposition in enumerate(dispositions):
            if not isinstance(disposition, dict): errors.append(f"disposition {index} must be an object"); continue
            finding_id = disposition.get("finding_id")
            if finding_id not in findings_by_id: errors.append(f"disposition {index} references unknown finding"); continue
            if finding_id in disposition_ids: errors.append(f"duplicate disposition for {finding_id}")
            disposition_ids.add(finding_id)
            decisions = disposition.get("category_decisions")
            reserved = set(findings_by_id[finding_id].get("applicable_categories", [])) & set(allowed)
            if not isinstance(decisions, dict) or set(decisions) != reserved:
                errors.append(f"disposition {finding_id} must cover every reserved category exactly")
            else:
                for category, decision in decisions.items():
                    if decision not in allowed[category]: errors.append(f"disposition {finding_id} has invalid {category} decision")
                if any(decision in {"deferred-external-work", "supersede-or-amend-adr"} for decision in decisions.values()):
                    tracked = disposition.get("tracked_work")
                    if not isinstance(tracked, dict) or not all(isinstance(tracked.get(field), str) and tracked.get(field).strip() for field in ("id", "owner", "closure_condition")):
                        errors.append(f"disposition {finding_id} requires tracked work id, owner, and closure condition")
            _nonempty_string(disposition.get("rationale"), f"disposition {finding_id} rationale", errors)
            source_evidence = disposition.get("source_bound_evidence")
            if not isinstance(source_evidence, list) or not source_evidence:
                errors.append(f"disposition {finding_id} requires source-bound evidence")
            else:
                for evidence_index, citation in enumerate(source_evidence):
                    if not isinstance(citation, dict) or set(citation) != {"path", "blob_oid", "range", "excerpt_sha256"}:
                        errors.append(f"disposition {finding_id} evidence {evidence_index} is not a complete citation")
                        continue
                    _repo_path(citation.get("path"), f"disposition {finding_id} evidence path", errors)
                    _sha(citation.get("blob_oid"), f"disposition {finding_id} evidence blob", errors)
                    if re.fullmatch(r"bytes:\d+-\d+", str(citation.get("range"))) is None: errors.append(f"disposition {finding_id} evidence range is invalid")
                    _hash(citation.get("excerpt_sha256"), f"disposition {finding_id} evidence excerpt", errors)
            _nonempty_string(disposition.get("user_approval_evidence_id"), f"disposition {finding_id} approval", errors)
            if disposition.get("status") != "accepted": errors.append(f"disposition {finding_id} remains blocking")
    reserved_ids = {candidate_id for candidate_id, item in findings_by_id.items() if set(item.get("applicable_categories", [])) & set(allowed)}
    if audit is not None and audit.get("reserved_finding_count") != len(reserved_ids):
        errors.append("audit reserved finding count must equal classified reserved findings")
    if phase == "pending-binding" and disposition_ids != reserved_ids:
        errors.append("pending-binding gate requires exactly one accepted disposition per reserved finding")
    manifest = _mapping(root.get("manifest_constraints"), "epic_gate.manifest_constraints", errors)
    if manifest is not None:
        names = manifest.get("expected_name_status")
        if not isinstance(names, list) or not names:
            errors.append("epic gate manifest requires expected name-status entries")
        else:
            for index, item in enumerate(names):
                if not isinstance(item, dict) or set(item) != {"path", "status"} or item.get("status") not in {"A", "M", "D", "T"}:
                    errors.append(f"invalid expected name-status entry {index}")
        tuples = manifest.get("non_gate_tuples")
        tuple_fields = {"path", "status", "old_mode", "old_type", "old_oid", "new_mode", "new_type", "new_oid"}
        if not isinstance(tuples, list):
            errors.append("epic gate manifest requires non-gate tuples")
        else:
            for index, item in enumerate(tuples):
                if not isinstance(item, dict) or set(item) != tuple_fields or item.get("status") not in {"A", "M", "D", "T"}:
                    errors.append(f"invalid non-gate change tuple {index}")
                    continue
                old_present = item.get("old_oid") is not None
                new_present = item.get("new_oid") is not None
                if item.get("status") == "A" and (old_present or not new_present): errors.append(f"added tuple {index} has invalid sides")
                if item.get("status") == "D" and (not old_present or new_present): errors.append(f"deleted tuple {index} has invalid tombstone")
                if item.get("status") in {"M", "T"} and (not old_present or not new_present): errors.append(f"changed tuple {index} requires both sides")
                for side in ("old", "new"):
                    oid = item.get(f"{side}_oid")
                    mode, object_type = item.get(f"{side}_mode"), item.get(f"{side}_type")
                    if oid is not None:
                        _sha(oid, f"change tuple {index} {side}_oid", errors)
                        if not isinstance(mode, str) or re.fullmatch(r"[0-7]{6}", mode) is None: errors.append(f"change tuple {index} {side}_mode is invalid")
                        if object_type not in {"blob", "tree", "commit"}: errors.append(f"change tuple {index} {side}_type is invalid")
                    elif mode is not None or object_type is not None:
                        errors.append(f"change tuple {index} absent {side} must null mode/type")
        gate = _mapping(manifest.get("gate_descriptor"), "epic_gate.manifest_constraints.gate_descriptor", errors)
        if gate is not None:
            if set(gate) != {"path", "status", "mode", "object_type", "semantic_projection_sha256"}:
                errors.append("gate descriptor fields are incomplete")
            if any(key in gate for key in ("oid", "object_oid")):
                errors.append("gate descriptor must not contain its self-referential object OID")
            if gate.get("object_type") != "blob" or gate.get("status") not in {"A", "M"}:
                errors.append("gate descriptor type/status is invalid")
            if not isinstance(gate.get("mode"), str) or re.fullmatch(r"[0-7]{6}", gate.get("mode")) is None:
                errors.append("gate descriptor mode is invalid")
            _hash(gate.get("semantic_projection_sha256"), "epic_gate.manifest_constraints.gate_descriptor.semantic_projection_sha256", errors)
            canonical_gate_path = f"docs/bmad/implementation-artifacts/epic-gates/{root.get('epic_key')}/architecture-gate.json"
            if gate.get("path") != canonical_gate_path:
                errors.append("gate descriptor path must be the canonical epic architecture gate")
            projection = json.loads(json.dumps(root))
            try:
                projection["manifest_constraints"]["gate_descriptor"]["semantic_projection_sha256"] = None
                projection_bytes = json.dumps(projection, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
                projection_hash = hashlib.sha256(projection_bytes).hexdigest()
                if str(gate.get("semantic_projection_sha256", "")).lower() != projection_hash:
                    errors.append("gate semantic projection hash does not match canonical content")
            except (KeyError, TypeError):
                errors.append("gate semantic projection cannot be computed")
        if isinstance(names, list) and isinstance(tuples, list) and gate is not None:
            expected_pairs = {(item.get("path"), item.get("status")) for item in names if isinstance(item, dict)}
            actual_pairs = {(item.get("path"), item.get("status")) for item in tuples if isinstance(item, dict)} | {(gate.get("path"), gate.get("status"))}
            if expected_pairs != actual_pairs: errors.append("name-status set must equal non-gate tuples plus gate descriptor")
            paths = [item.get("path") for item in names if isinstance(item, dict)]
            normalized = [unicodedata.normalize("NFC", path) for path in paths if isinstance(path, str)]
            if len(normalized) != len(set(normalized)) or len({path.casefold() for path in normalized}) != len(normalized):
                errors.append("manifest paths contain duplicate or case-colliding entries")
            if any(path != unicodedata.normalize("NFC", path) for path in paths if isinstance(path, str)):
                errors.append("manifest paths must be Unicode NFC")
            durable = root.get("durable_artifact_paths")
            if not isinstance(durable, list) or set(paths) != set(durable):
                errors.append("manifest paths must exactly equal durable artifact paths")
            required_durable = {gate.get("path"), audit.get("raw_response_path") if audit else None}
            for field in ("build_evidence_paths", "test_evidence_paths", "completion_status_paths"):
                value = root.get(field)
                if not isinstance(value, list) or not value:
                    errors.append(f"epic gate requires nonempty {field}")
                else:
                    required_durable.update(value)
            if not required_durable <= set(durable or []):
                errors.append("durable artifact manifest omits required gate, audit, evidence, or status path")
            preimages = root.get("repository_preflight", {}).get("target_preimages") if isinstance(root.get("repository_preflight"), dict) else None
            if not isinstance(preimages, dict) or set(preimages) != set(durable or []):
                errors.append("target preimages must exactly cover durable artifact paths")
            status_paths = root.get("completion_status_paths")
            if isinstance(status_paths, list) and "docs/bmad/implementation-artifacts/sprint-status.yaml" not in status_paths:
                errors.append("completion status paths must include sprint-status.yaml")
    checkpoint = _mapping(root.get("checkpoint"), "epic_gate.checkpoint", errors)
    if checkpoint is not None:
        _nonempty_string(checkpoint.get("id"), "epic_gate.checkpoint.id", errors)
        _sha(checkpoint.get("expected_parent"), "epic_gate.checkpoint.expected_parent", errors)
        if checkpoint.get("expected_parent") != root.get("source_commit"):
            errors.append("epic checkpoint expected parent must equal source commit")
    if repo is not None and phase == "pending-binding":
        repo = Path(repo).resolve()
        preflight = root.get("repository_preflight")
        try:
            observed_root = Path(_git(repo, "rev-parse", "--show-toplevel")).resolve()
            common_raw = Path(_git(repo, "rev-parse", "--git-common-dir"))
            observed_common = (repo / common_raw).resolve() if not common_raw.is_absolute() else common_raw.resolve()
            observed_branch = _git(repo, "branch", "--show-current")
            if not isinstance(preflight, dict) or Path(str(preflight.get("repository_root"))).resolve() != observed_root:
                errors.append("epic gate repository root identity does not match Git")
            if not isinstance(preflight, dict) or Path(str(preflight.get("worktree"))).resolve() != observed_root:
                errors.append("epic gate worktree identity does not match Git")
            if not isinstance(preflight, dict) or Path(str(preflight.get("git_common_dir"))).resolve() != observed_common:
                errors.append("epic gate common Git directory does not match Git")
            if not isinstance(preflight, dict) or preflight.get("branch") != observed_branch:
                errors.append("epic gate branch does not match Git")
        except (OSError, subprocess.CalledProcessError):
            errors.append("epic gate repository identities cannot be resolved")
        try:
            source_tree = _git(repo, "rev-parse", f"{root.get('source_commit')}^{{tree}}")
        except (OSError, subprocess.CalledProcessError):
            errors.append("epic gate source commit/tree cannot be resolved")
            source_tree = None
        try:
            current_head = _git(repo, "rev-parse", "HEAD")
            checkpoint_id = root.get("checkpoint", {}).get("id") if isinstance(root.get("checkpoint"), dict) else None
            if current_head != root.get("source_commit") and isinstance(checkpoint_id, str):
                candidates = _git(repo, "log", "--all", "--format=%H", "--fixed-strings", f"--grep=Workflow-Checkpoint: {checkpoint_id}").splitlines()
                matches = [commit for commit in candidates if _has_trailer(_git(repo, "show", "-s", "--format=%B", commit), "Workflow-Checkpoint", checkpoint_id)]
                if len(matches) != 1:
                    errors.append("epic pending checkpoint trailer must resolve exactly once")
                else:
                    close_commit = matches[0]
                    body = _git(repo, "show", "-s", "--format=%B", close_commit)
                    parent = _git(repo, "rev-parse", f"{close_commit}^")
                    try:
                        subprocess.check_call(["git", "-C", str(repo), "merge-base", "--is-ancestor", close_commit, current_head], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                    except subprocess.CalledProcessError:
                        errors.append("epic close checkpoint is not ancestral to active HEAD")
                    trailer = re.search(r"^Workflow-Manifest-SHA256: ([0-9a-fA-F]{64})$", body, re.MULTILINE)
                    if parent != root.get("source_commit"): errors.append("epic close checkpoint is not a direct child of source")
                    if (not _has_trailer(body, "BMad-Epic", str(root.get("epic_key")))
                            or not _has_trailer(body, "Workflow-Scope", f"epic/{root.get('epic_key')}")
                            or not _has_trailer(body, "Workflow-Stage", "complete") or trailer is None):
                        errors.append("epic close checkpoint trailers do not bind")
                    digest, changed = _commit_change_digest(repo, parent, close_commit)
                    if trailer is not None and digest.lower() != trailer.group(1).lower(): errors.append("epic close full change digest does not match trailer")
                    if changed != set(root.get("durable_artifact_paths", [])): errors.append("epic close committed paths do not equal durable manifest")
                    sprint_path = "docs/bmad/implementation-artifacts/sprint-status.yaml"
                    try:
                        before = _git(repo, "show", f"{parent}:{sprint_path}")
                        after = _git(repo, "show", f"{close_commit}:{sprint_path}")
                        status_pattern = re.compile(rf"(?m)^\s*epic-{re.escape(str(root.get('epic_key')))}\s*:\s*(backlog|in-progress|done)\s*(?:#.*)?$")
                        before_matches, after_matches = status_pattern.findall(before), status_pattern.findall(after)
                        if before_matches != ["in-progress"] or after_matches != ["done"]:
                            errors.append("canonical epic status entry does not transition in-progress to done")
                    except (OSError, subprocess.CalledProcessError):
                        errors.append("canonical sprint status transition cannot be read")
        except (OSError, subprocess.CalledProcessError):
            errors.append("epic pending checkpoint cannot be resolved")
        if boundary is not None and source_tree:
            for name in ("build", "audit"):
                worktree = boundary.get(name)
                if isinstance(worktree, dict) and (worktree.get("tree_id") != source_tree or worktree.get("index_id") != source_tree):
                    errors.append(f"epic gate {name} tree/index IDs do not match source tree")
                if isinstance(worktree, dict) and worktree.get("disposed") is True and Path(str(worktree.get("path"))).exists():
                    errors.append(f"epic gate {name} worktree is recorded disposed but still exists")
            try:
                registered = _git(repo, "worktree", "list", "--porcelain")
            except (OSError, subprocess.CalledProcessError):
                errors.append("Git worktree registry cannot be inspected")
                registered = ""
            for name in ("build", "audit"):
                worktree = boundary.get(name)
                if isinstance(worktree, dict) and str(worktree.get("path")) in registered:
                    errors.append(f"epic gate {name} checkout remains registered")
        durable = root.get("durable_artifact_paths")
        if isinstance(durable, list):
            for path in durable:
                candidate = repo / path
                if not candidate.is_file(): errors.append(f"durable epic artifact is missing: {path}")
                else:
                    try:
                        index_entry = _index_entry(repo, path)
                        worktree_oid = _git(repo, "hash-object", "--", path)
                        if index_entry is None or index_entry[2] != worktree_oid:
                            errors.append(f"durable epic artifact differs from staged index: {path}")
                    except (OSError, subprocess.CalledProcessError, ValueError):
                        errors.append(f"durable epic artifact is not a single staged entry: {path}")
        if audit is not None and isinstance(audit.get("raw_response_path"), str):
            raw = repo / audit["raw_response_path"]
            if raw.is_file() and hashlib.sha256(raw.read_bytes()).hexdigest().lower() != str(audit.get("raw_response_sha256", "")).lower():
                errors.append("architecture raw response hash does not match")
            if raw.is_file():
                try:
                    envelope = json.loads(raw.read_text(encoding="utf-8"))
                    structured = envelope["structured_result"]
                    block_index = envelope["structured_result_source_block_index"]
                    blocks = envelope["tool_envelope"]["content"]
                    block_value = json.loads(blocks[block_index]["text"])
                    if block_value != structured:
                        errors.append("architecture structured result does not equal its raw source block")
                    if structured.get("verdict") != audit.get("verdict") or structured.get("candidates") != root.get("complete_findings"):
                        errors.append("architecture raw verdict/candidates do not equal durable findings")
                    proof = structured.get("audit_root_proof")
                    audit_boundary = boundary.get("audit") if isinstance(boundary, dict) else None
                    preflight_common = root.get("repository_preflight", {}).get("git_common_dir") if isinstance(root.get("repository_preflight"), dict) else None
                    if (not isinstance(proof, dict) or proof.get("source_commit") != root.get("source_commit")
                            or not isinstance(audit_boundary, dict) or proof.get("git_identity") != audit_boundary.get("git_identity")
                            or proof.get("audit_root") != audit_boundary.get("path")
                            or proof.get("git_common_dir") != preflight_common
                            or proof.get("tree_id") != audit_boundary.get("tree_id")
                            or proof.get("index_id") != audit_boundary.get("index_id")
                            or proof.get("authoritative_input_hashes") != audit.get("authoritative_input_hashes")):
                        errors.append("architecture raw audit-root proof does not match boundary")
                except (KeyError, IndexError, TypeError, json.JSONDecodeError):
                    errors.append("architecture raw response envelope is invalid")
        if audit is not None and isinstance(audit.get("authoritative_input_hashes"), dict):
            for authority_path, recorded_hash in audit["authoritative_input_hashes"].items():
                try:
                    entry = _tree_entry(repo, str(root.get("source_commit")), authority_path)
                    if entry is None: raise ValueError("missing authority")
                    blob = subprocess.check_output(["git", "-C", str(repo), "cat-file", "blob", entry[2]])
                    if hashlib.sha256(blob).hexdigest().lower() != str(recorded_hash).lower():
                        errors.append(f"authoritative input hash does not match pinned blob: {authority_path}")
                except (OSError, subprocess.CalledProcessError, ValueError):
                    errors.append(f"authoritative input cannot be resolved at source: {authority_path}")
        payload_bytes = json.dumps(root.get("complete_findings"), ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
        if audit is not None and hashlib.sha256(payload_bytes).hexdigest().lower() != str(audit.get("normalized_payload_sha256", "")).lower():
            errors.append("normalized architecture payload hash does not match findings")
        if findings is not None:
            for finding in findings:
                if not isinstance(finding, dict): continue
                for citation in finding.get("citations", []):
                    if not isinstance(citation, dict): continue
                    try:
                        entry = _tree_entry(repo, str(root.get("source_commit")), str(citation.get("path")))
                        if entry is None or entry[2] != citation.get("blob_oid"):
                            errors.append(f"citation blob does not match source tree: {citation.get('path')}")
                            continue
                        match = re.fullmatch(r"bytes:(\d+)-(\d+)", str(citation.get("range")))
                        if match is None:
                            errors.append("citation range must be zero-based half-open bytes:start-end")
                            continue
                        start, end = int(match.group(1)), int(match.group(2))
                        blob = subprocess.check_output(["git", "-C", str(repo), "cat-file", "blob", entry[2]])
                        if start < 0 or end <= start or end > len(blob) or hashlib.sha256(blob[start:end]).hexdigest().lower() != str(citation.get("excerpt_sha256", "")).lower():
                            errors.append(f"citation excerpt hash/range is invalid: {citation.get('path')}")
                    except (OSError, subprocess.CalledProcessError, ValueError):
                        errors.append(f"citation cannot be verified: {citation.get('path')}")
        if isinstance(dispositions, list):
            for disposition in dispositions:
                if not isinstance(disposition, dict): continue
                for citation in disposition.get("source_bound_evidence", []):
                    if not isinstance(citation, dict): continue
                    try:
                        entry = _tree_entry(repo, str(root.get("source_commit")), str(citation.get("path")))
                        match = re.fullmatch(r"bytes:(\d+)-(\d+)", str(citation.get("range")))
                        if entry is None or entry[2] != citation.get("blob_oid") or match is None:
                            errors.append(f"disposition evidence does not bind source: {citation.get('path')}")
                            continue
                        start, end = int(match.group(1)), int(match.group(2))
                        blob = subprocess.check_output(["git", "-C", str(repo), "cat-file", "blob", entry[2]])
                        if start < 0 or end <= start or end > len(blob) or hashlib.sha256(blob[start:end]).hexdigest().lower() != str(citation.get("excerpt_sha256", "")).lower():
                            errors.append(f"disposition evidence excerpt is invalid: {citation.get('path')}")
                    except (OSError, subprocess.CalledProcessError, ValueError):
                        errors.append(f"disposition evidence cannot be verified: {citation.get('path')}")
        if isinstance(preflight, dict) and isinstance(preflight.get("target_preimages"), dict):
            for path, recorded in preflight["target_preimages"].items():
                try: observed = _tree_entry(repo, str(root.get("source_commit")), path)
                except (OSError, subprocess.CalledProcessError, ValueError): observed = None
                observed_oid = observed[2] if observed else None
                if recorded != observed_oid: errors.append(f"target preimage does not match source: {path}")
        manifest = root.get("manifest_constraints")
        if isinstance(manifest, dict):
            if current_head == root.get("source_commit"):
                staged_raw = subprocess.check_output([
                    "git", "-C", str(repo), "diff", "--cached", "--name-only", "-z", "--no-renames"
                ])
                staged_paths = {part.decode("utf-8") for part in staged_raw.split(b"\0") if part}
                if staged_paths != set(root.get("durable_artifact_paths", [])):
                    errors.append("complete staged path set does not equal durable manifest")
            for item in manifest.get("non_gate_tuples", []):
                if not isinstance(item, dict): continue
                path = item.get("path")
                try:
                    old = _tree_entry(repo, str(root.get("source_commit")), path)
                    new = _index_entry(repo, path)
                except (OSError, subprocess.CalledProcessError, ValueError):
                    errors.append(f"manifest tuple cannot be derived from Git: {path}")
                    continue
                recorded_old = (item.get("old_mode"), item.get("old_type"), item.get("old_oid")) if item.get("old_oid") is not None else None
                recorded_new = (item.get("new_mode"), item.get("new_type"), item.get("new_oid")) if item.get("new_oid") is not None else None
                if old != recorded_old or new != recorded_new: errors.append(f"manifest tuple does not match source/index: {path}")
                derived_status = "A" if old is None else "D" if new is None else "T" if old[1] != new[1] else "M"
                if item.get("status") != derived_status: errors.append(f"manifest status does not match source/index: {path}")
            gate_descriptor = manifest.get("gate_descriptor")
            if isinstance(gate_descriptor, dict):
                try:
                    gate_old = _tree_entry(repo, str(root.get("source_commit")), gate_descriptor.get("path"))
                    gate_index = _index_entry(repo, gate_descriptor.get("path"))
                except (OSError, subprocess.CalledProcessError, ValueError): gate_index = None
                if gate_index is None or gate_index[0] != gate_descriptor.get("mode") or gate_index[1] != gate_descriptor.get("object_type"):
                    errors.append("canonical gate descriptor does not match staged index")
                elif gate_index is not None:
                    try:
                        staged_gate = json.loads(subprocess.check_output(
                            ["git", "-C", str(repo), "cat-file", "blob", gate_index[2]], text=True
                        ))
                        if staged_gate != root: errors.append("staged canonical gate content differs from validated gate")
                    except (OSError, subprocess.CalledProcessError, json.JSONDecodeError):
                        errors.append("staged canonical gate content cannot be validated")
                expected_gate_status = "A" if gate_old is None else "M"
                if gate_descriptor.get("status") != expected_gate_status: errors.append("canonical gate status does not match source/index")
        seen_evidence: set[str] = set()
        evidence_required_paths: set[str] = set()
        for field, expected_stage in (("build_evidence_paths", "verify-build"), ("test_evidence_paths", "verify-test")):
            value = root.get(field)
            if isinstance(value, list):
                for path in value:
                    if path in seen_evidence: errors.append("build and test evidence paths must be distinct")
                    seen_evidence.add(path)
                    candidate = repo / path
                    try:
                        evidence_data = json.loads(candidate.read_text(encoding="utf-8"))
                    except (OSError, json.JSONDecodeError):
                        errors.append(f"invalid epic command evidence: {path}")
                        continue
                    errors.extend(f"{path}: {error}" for error in validate_epic_command_evidence(
                        evidence_data, repo, str(root.get("epic_key")), expected_stage
                    ))
                    build_boundary = boundary.get("build") if isinstance(boundary, dict) else None
                    if not isinstance(build_boundary, dict) or evidence_data.get("execution_worktree") != build_boundary.get("path") or evidence_data.get("execution_worktree_git_identity") != build_boundary.get("git_identity"):
                        errors.append(f"{path}: command evidence checkout identity does not match build boundary")
                    if isinstance(evidence_data.get("artifact_paths"), list): evidence_required_paths.update(evidence_data["artifact_paths"])
                    if isinstance(evidence_data.get("raw_output_path"), str): evidence_required_paths.add(evidence_data["raw_output_path"])
        durable_set = set(root.get("durable_artifact_paths", [])) if isinstance(root.get("durable_artifact_paths"), list) else set()
        if not evidence_required_paths <= durable_set:
            errors.append("durable manifest omits command evidence raw/artifact paths")
    return errors


def _commit_change_digest(repo: Path, parent: str, commit: str) -> tuple[str, set[str]]:
    raw_paths = subprocess.check_output([
        "git", "-C", str(repo), "diff", "--name-only", "-z", "--no-renames", parent, commit
    ])
    paths = [part.decode("utf-8") for part in raw_paths.split(b"\0") if part]
    changes: list[dict[str, Any]] = []
    for path in paths:
        old, new = _tree_entry(repo, parent, path), _tree_entry(repo, commit, path)
        status = "A" if old is None else "D" if new is None else "T" if old[1] != new[1] else "M"
        changes.append({
            "path": unicodedata.normalize("NFC", path), "status": status,
            "old_mode": old[0] if old else None, "old_type": old[1] if old else None, "old_oid": old[2] if old else None,
            "new_mode": new[0] if new else None, "new_type": new[1] if new else None, "new_oid": new[2] if new else None,
        })
    changes.sort(key=lambda item: (item["path"].encode("utf-8"), item["status"]))
    payload = json.dumps(changes, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(payload).hexdigest(), set(paths)


def validate_retrospective_state(data: Any, repo: Path | None = None) -> list[str]:
    errors: list[str] = []
    root = _mapping(data, "retrospective state", errors)
    if root is None:
        return errors
    _required(root, RETRO_REQUIRED, "missing ", errors)
    _unknown(root, RETRO_REQUIRED, "retrospective_state.", errors)
    if root.get("schema_version") != "1.0": errors.append("retrospective_state.schema_version must equal 1.0")
    if root.get("status") not in {"active", "failed", "pending-binding"}: errors.append("retrospective_state.status is invalid")
    source_checkpoint = _mapping(root.get("source_epic_checkpoint"), "retrospective_state.source_epic_checkpoint", errors)
    if source_checkpoint is not None:
        _nonempty_string(source_checkpoint.get("id"), "retrospective_state.source_epic_checkpoint.id", errors)
        _sha(source_checkpoint.get("commit_sha"), "retrospective_state.source_epic_checkpoint.commit_sha", errors)
        _hash(source_checkpoint.get("manifest_sha256"), "retrospective_state.source_epic_checkpoint.manifest_sha256", errors)
    hooks = root.get("hooks")
    if not isinstance(hooks, list): errors.append("retrospective_state.hooks must be an array")
    else:
        for index, hook in enumerate(hooks):
            if not isinstance(hook, dict) or set(hook) != {"name", "decision"} or not isinstance(hook.get("name"), str) or not hook.get("name").strip() or hook.get("decision") not in {"allow", "deny", "user-approved"}:
                errors.append(f"retrospective hook {index} is invalid")
    writes = root.get("write_manifest")
    if not isinstance(writes, list) or not writes: errors.append("retrospective_state.write_manifest must be nonempty")
    else:
        for index, path in enumerate(writes): _repo_path(path, f"retrospective_state.write_manifest[{index}]", errors)
        normalized = [unicodedata.normalize("NFC", path) for path in writes if isinstance(path, str)]
        if len(normalized) != len(writes) or len(set(normalized)) != len(normalized) or len({path.casefold() for path in normalized}) != len(normalized):
            errors.append("retrospective write manifest contains invalid or colliding paths")
        manifest_bytes = json.dumps(sorted(normalized, key=lambda value: value.encode("utf-8")), ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        if hashlib.sha256(manifest_bytes).hexdigest().lower() != str(root.get("approved_manifest_sha256", "")).lower():
            errors.append("retrospective approved manifest hash does not match")
        expected_state_path = f"docs/bmad/implementation-artifacts/epic-gates/{root.get('epic_key')}/retrospective-state.json"
        if expected_state_path not in writes: errors.append("retrospective write manifest must include canonical state")
    _hash(root.get("approved_manifest_sha256"), "retrospective_state.approved_manifest_sha256", errors)
    if root.get("status") == "pending-binding" and (root.get("failure") is not None or not isinstance(root.get("checkpoint"), dict)):
        errors.append("pending-binding retrospective requires checkpoint and no failure")
    if root.get("status") == "pending-binding" and repo is None:
        errors.append("pending-binding retrospective validation requires repository context")
    checkpoint = root.get("checkpoint")
    if isinstance(checkpoint, dict):
        if set(checkpoint) != {"id", "expected_parent"}: errors.append("retrospective pending checkpoint fields are incomplete")
        _nonempty_string(checkpoint.get("id"), "retrospective_state.checkpoint.id", errors)
        _sha(checkpoint.get("expected_parent"), "retrospective_state.checkpoint.expected_parent", errors)
        if isinstance(source_checkpoint, dict) and checkpoint.get("expected_parent") != source_checkpoint.get("commit_sha"):
            errors.append("retrospective expected parent must equal bound epic commit")
    if repo is not None and root.get("status") == "pending-binding" and isinstance(source_checkpoint, dict) and isinstance(checkpoint, dict):
        repo = Path(repo)
        try:
            source_body = _git(repo, "show", "-s", "--format=%B", source_checkpoint.get("commit_sha"))
            source_parent = _git(repo, "rev-parse", f"{source_checkpoint.get('commit_sha')}^")
            source_digest, _ = _commit_change_digest(repo, source_parent, source_checkpoint.get("commit_sha"))
            if (not _has_trailer(source_body, "Workflow-Checkpoint", str(source_checkpoint.get("id")))
                    or not _has_trailer(source_body, "Workflow-Manifest-SHA256", str(source_checkpoint.get("manifest_sha256")))
                    or not _has_trailer(source_body, "BMad-Epic", str(root.get("epic_key")))
                    or not _has_trailer(source_body, "Workflow-Scope", f"epic/{root.get('epic_key')}")
                    or not _has_trailer(source_body, "Workflow-Stage", "complete")
                    or source_digest.lower() != str(source_checkpoint.get("manifest_sha256")).lower()):
                errors.append("source epic checkpoint trailers do not bind")
            candidates = _git(repo, "log", "--all", "--format=%H", "--fixed-strings", f"--grep=Workflow-Checkpoint: {checkpoint.get('id')}").splitlines()
            matches = [commit for commit in candidates if _has_trailer(_git(repo, "show", "-s", "--format=%B", commit), "Workflow-Checkpoint", str(checkpoint.get("id")))]
            if len(matches) != 1:
                errors.append("retrospective checkpoint trailer must resolve exactly once")
            else:
                retro_commit = matches[0]
                retro_body = _git(repo, "show", "-s", "--format=%B", retro_commit)
                retro_parent = _git(repo, "rev-parse", f"{retro_commit}^")
                active_head = _git(repo, "rev-parse", "HEAD")
                try:
                    subprocess.check_call(["git", "-C", str(repo), "merge-base", "--is-ancestor", retro_commit, active_head], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                except subprocess.CalledProcessError:
                    errors.append("retrospective checkpoint is not ancestral to active HEAD")
                if retro_parent != source_checkpoint.get("commit_sha"): errors.append("retrospective checkpoint is not a direct child of epic checkpoint")
                trailer_match = re.search(r"^Workflow-Manifest-SHA256: ([0-9a-fA-F]{64})$", retro_body, re.MULTILINE)
                if (not _has_trailer(retro_body, "Workflow-Checkpoint", str(checkpoint.get("id")))
                        or not _has_trailer(retro_body, "BMad-Epic", str(root.get("epic_key")))
                        or not _has_trailer(retro_body, "Workflow-Scope", f"epic/{root.get('epic_key')}")
                        or not _has_trailer(retro_body, "Workflow-Stage", "retrospective") or trailer_match is None):
                    errors.append("retrospective checkpoint trailers do not bind")
                digest, changed = _commit_change_digest(repo, retro_parent, retro_commit)
                if trailer_match is not None and digest.lower() != trailer_match.group(1).lower(): errors.append("retrospective full change digest does not match trailer")
                if changed != set(writes or []): errors.append("retrospective checkpoint changed paths do not equal approved manifest")
                state_path = f"docs/bmad/implementation-artifacts/epic-gates/{root.get('epic_key')}/retrospective-state.json"
                try:
                    committed_state = json.loads(_git(repo, "show", f"{retro_commit}:{state_path}"))
                    if committed_state != root: errors.append("committed retrospective state differs from validated state")
                except (subprocess.CalledProcessError, json.JSONDecodeError):
                    errors.append("retrospective commit does not contain canonical state")
        except (OSError, subprocess.CalledProcessError):
            errors.append("retrospective checkpoint commits cannot be resolved")
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
    elif filename == "workflow-state.schema.json":
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
    elif filename == "epic-command-evidence.schema.json":
        checks = [
            (_schema_at(schema, "properties", "schema_version", "const") == "1.0", "schema version const"),
            (_schema_at(schema, "properties", "kind", "const") == "command", "command kind const"),
            (_schema_at(schema, "properties", "boundary_clean", "const") is True, "clean-boundary const"),
        ]
    elif filename == "epic-architecture-gate.schema.json":
        checks = [
            (_schema_at(schema, "properties", "schema_version", "const") == "1.0", "schema version const"),
            (_schema_enum(schema, "properties", "transaction_phase", "enum") == {"idle", "prepared", "pending-binding"}, "transaction phase enum"),
            (_schema_at(schema, "$defs", "manifest", "properties", "gate_descriptor", "$ref") is None, "non-self-referential manifest"),
        ]
    elif filename == "retrospective-state.schema.json":
        checks = [
            (_schema_at(schema, "properties", "schema_version", "const") == "1.0", "schema version const"),
            (_schema_enum(schema, "properties", "status", "enum") == {"active", "failed", "pending-binding"}, "retrospective status enum"),
        ]
    else:
        checks = [(False, "known schema filename")]
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

    workflow_skills = {
        "run-bmad-sprint-loop": ("speckit-specify", "speckit-analyze", "tdd-implementer"),
        "close-bmad-epic": ("$audit-epic-architecture", "workflow-manifest-sha256", "pending-binding"),
        "audit-epic-architecture": ("$improve-codebase-architecture", "do not modify that upstream skill", "explore/report"),
    }
    skills_root = repo / ".agents" / "skills"
    for skill_name, markers in workflow_skills.items():
        skill_dir = skills_root / skill_name
        skill_path = skill_dir / "SKILL.md"
        metadata_path = skill_dir / "agents" / "openai.yaml"
        if not skill_path.is_file():
            errors.append(f"{skill_path}: missing required workflow skill")
            continue
        if not metadata_path.is_file():
            errors.append(f"{metadata_path}: missing required skill interface")
        body = skill_path.read_text(encoding="utf-8").lower()
        for marker in markers:
            if marker not in body:
                errors.append(f"{skill_path}: missing required workflow rule: {marker}")
    upstream_architecture = skills_root / "improve-codebase-architecture" / "SKILL.md"
    if not upstream_architecture.is_file():
        errors.append(f"{upstream_architecture}: required installed Matt Pocock skill is missing")
    elif "read-only epic-close profile" in upstream_architecture.read_text(encoding="utf-8").lower():
        errors.append(f"{upstream_architecture}: upstream skill must remain unmodified; use audit-epic-architecture adapter")

    refs = repo / ".agents" / "skills" / "run-bmad-sprint-loop" / "references"
    schemas = {
        "verdict.schema.json": VERDICT_REQUIRED,
        "workflow-state.schema.json": STATE_REQUIRED,
        "epic-command-evidence.schema.json": EPIC_EVIDENCE_REQUIRED,
        "epic-architecture-gate.schema.json": EPIC_GATE_REQUIRED,
        "retrospective-state.schema.json": RETRO_REQUIRED,
    }
    schema_defs = {
        "verdict.schema.json": {"boundary", "finding"},
        "workflow-state.schema.json": {
            "boundary", "gateRecord", "stageRecord", "preflight", "sourceArtifact", "dispatch",
            "reviewIteration", "e2ePredicate", "coverageOverride", "e2eDecision", "evidence", "checkpoint", "failure",
        },
        "epic-command-evidence.schema.json": {"sha", "hash", "time", "repoPath"},
        "epic-architecture-gate.schema.json": {"sha", "hash", "repoPath", "preflight", "boundary", "worktreeBoundary", "audit", "finding", "disposition", "checkpoint", "nameStatus", "changeTuple", "manifest"},
        "retrospective-state.schema.json": {"hook", "checkpoint", "pendingCheckpoint"},
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
    parser.add_argument("--epic-gate", type=Path)
    parser.add_argument("--epic-evidence", type=Path)
    parser.add_argument("--retrospective-state", type=Path)
    args = parser.parse_args()
    errors = validate_repository(args.repo)
    for path, validator in (
        (args.epic_gate, lambda data: validate_epic_gate(data, args.repo)),
        (args.epic_evidence, lambda data: validate_epic_command_evidence(data, args.repo)),
        (args.retrospective_state, lambda data: validate_retrospective_state(data, args.repo)),
    ):
        if path is None:
            continue
        data = _load_json(path, errors)
        if data is not None:
            errors.extend(f"{path}: {error}" for error in validator(data))
    if errors:
        print("\n".join(errors))
        return 1
    print("Workflow configuration and contracts valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
