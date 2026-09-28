from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
import tomllib
import unicodedata
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

# The distribution stores reusable runtime helpers in the skill; installed
# validators live beside them. Keep both layouts independent of the caller cwd.
_RUNTIME_DIR = Path(__file__).resolve().parent
if not (_RUNTIME_DIR / "decision_policy.py").is_file():
    _RUNTIME_DIR = _RUNTIME_DIR.parent / "skills/run-governed-delivery/scripts"
sys.path.insert(0, str(_RUNTIME_DIR))
if __name__ == "__main__":
    sys.modules.setdefault("validate_workflow", sys.modules[__name__])

REQUIRED_AGENT_FIELDS = {"name", "description", "developer_instructions"}
VALID_VERDICTS = {"ACCEPT", "REVISE", "BLOCKED"}
VALID_GATES = {"plan-review", "definition-review", "quality-review"}
BLOCKING_SEVERITIES = {"CRITICAL", "HIGH"}
VALID_SEVERITIES = BLOCKING_SEVERITIES | {"MEDIUM", "LOW"}
VALID_STAGES = {
    "context", "specify", "plan", "plan-review", "tasks", "definition-review",
    "implementation-approval", "implement", "converge", "lookback", "quality-review", "conditional-e2e", "verify", "complete",
}
VALID_E2E_RESULTS = {
    "pending", "passed", "failed-product", "failed-test", "blocked", "skipped-no-risk", "skipped-covered",
}
SUPPORTED_LIFECYCLE_ADAPTERS = {"bmad-6.11", "bmad-6.12"}
SUPPORTED_DEFINITION_ADAPTERS = {"speckit-0.16", "speckit-1.0.6"}
SHA_RE = re.compile(r"^[0-9a-fA-F]{40}$")
HASH_RE = re.compile(r"^[0-9a-fA-F]{64}$")
TIME_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$")
DESIGN_ID_RE = re.compile(r"ID-([RISAFLTXC])(\d{2})")
DESIGN_ID_CANDIDATE_RE = re.compile(r"(?i)(?<![\w-])id-[A-Za-z0-9_-]*(?![\w-])")
DESIGN_BULLET_RE = re.compile(r"^- \*\*(ID-[^*\s]+)\*\*: (\S.*)$")
TASK_LINE_RE = re.compile(r"^\s{0,3}-\s+\[[ xX]\]\s+T\d{3,}\b")
ATX_HEADING_RE = re.compile(r"^\s{0,3}(#{1,6})[ \t]+(.+?)[ \t]*$")
DESIGN_SECTIONS = {
    "Responsibilities": "R",
    "Interfaces": "I",
    "State and Lifetime": "S",
    "Async and Cancellation": "A",
    "Failure and Retry": "F",
    "Reuse and Locality": "L",
    "Test Seams": "T",
    "Alternatives": "X",
    "Consequences": "C",
}

ROLE_PROFILES = {
    "delivery-orchestrator": ("gpt-6-sol", "medium", "workspace-write"),
    "story-context-synthesizer": ("gpt-6-luna", "high", "read-only"),
    "definition-author": ("gpt-6-sol", "medium", "workspace-write"),
    "definition-author-recovery": ("gpt-6-astra", "medium", "workspace-write"),
    "definition-author-recovery-high": ("gpt-6-astra", "high", "workspace-write"),
    "plan-architect-high": ("gpt-6-sol", "high", "workspace-write"),
    "definition-reviewer": ("gpt-6-sol", "medium", "read-only"),
    "tdd-test-author": ("gpt-6-luna", "high", "workspace-write"),
    "tdd-implementer": ("gpt-6-sol", "medium", "workspace-write"),
    "convergence-analyst": ("gpt-6-luna", "high", "workspace-write"),
    "quality-reviewer": ("gpt-6-sol", "medium", "read-only"),
    "e2e-runner": ("gpt-6-luna", "high", "workspace-write"),
    "epic-architecture-auditor": ("gpt-6-sol", "high", "read-only"),
}
OPTIONAL_ROLE_NAMES = {"epic-architecture-auditor"}
RETIRED_ROLE_FILES = {"spec-author.toml", "plan-architect.toml", "task-author.toml"}
ROLE_INSTRUCTION_MARKERS = {
    "delivery-orchestrator": ("load and follow run-governed-delivery", "never implement feature code", "you alone"),
    "story-context-synthesizer": ("separate sourced facts from inference", "cite repository paths"),
    "definition-author": (
        "speckit-specify", "speckit-plan", "speckit-tasks", "self-review",
        "current phase", "persistent", "spec|plan|tasks", "always classify implementation design",
    ),
    "plan-architect-high": ("speckit-plan", "self-review", "implementation-design classification is architecture"),
    "definition-author-recovery": ("spec|plan|tasks", "self-review", "exclusive", "never write production"),
    "definition-author-recovery-high": ("spec|plan|tasks", "self-review", "exclusive", "never write production"),
    "definition-reviewer": ("speckit-analyze", "earliest_owner", "prior raw verdict", "never edit"),
    "tdd-test-author": ("expected red", "test code and test evidence only", "never edit production code"),
    "tdd-implementer": ("red evidence", "minimal change", "do not write or weaken tests", "verification routing"),
    "convergence-analyst": ("speckit-converge", "append-only", "never edit spec"),
    "quality-reviewer": ("converged immutable boundary", "skip style enforced by tooling", "cite documented standards"),
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
    "schema_version", "bmad_story_key", "bmad_epic_key", "feature_directory", "stage", "preflight", "artifacts",
    "gates", "attempts", "approvals", "convergence", "e2e", "current_evidence_paths", "checkpoint", "resume", "updated_at",
}
MAX_WORKFLOW_STATE_BYTES = 12 * 1024
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


def _front_matter_scalar(value: str) -> str | None:
    value = value.strip()
    if not value or value.startswith("#") or value in {"|", ">", "|-", ">-", "|+", ">+"}:
        return None
    if value[0] in {"'", '"'}:
        if len(value) < 2 or value[-1] != value[0]:
            return None
        return value[1:-1].strip() or None
    value = re.split(r"\s+#", value, maxsplit=1)[0].strip()
    return value or None


def _strip_html_comments(raw: str, in_comment: bool) -> tuple[str, bool]:
    visible: list[str] = []
    index = 0
    while index < len(raw):
        if in_comment:
            closing = raw.find("-->", index)
            if closing < 0:
                return "".join(visible), True
            index = closing + 3
            in_comment = False
            continue
        if raw.startswith("<!--", index):
            in_comment = True
            index += 4
            continue
        if raw[index] == "`":
            end = index + 1
            while end < len(raw) and raw[end] == "`":
                end += 1
            tick_count = end - index
            marker = "`" * tick_count
            backslashes = 0
            preceding = index - 1
            while preceding >= 0 and raw[preceding] == "\\":
                backslashes += 1
                preceding -= 1
            if backslashes % 2:
                visible.append(marker)
                index = end
                continue
            has_closing = re.search(
                rf"(?<!`){re.escape(marker)}(?!`)", raw[end:]
            )
            if not has_closing:
                visible.append(marker)
            else:
                index = end + has_closing.end()
                continue
            index = end
            continue
        visible.append(raw[index])
        index += 1
    return "".join(visible), in_comment


def _fence_opening(line: str) -> tuple[str, int] | None:
    match = re.match(r"^\s{0,3}(`{3,}|~{3,})(.*)$", line)
    if match is None:
        return None
    marker, info = match.groups()
    if marker[0] == "`" and "`" in info:
        return None
    return marker[0], len(marker)


def _atx_heading(line: str) -> tuple[int, str] | None:
    match = ATX_HEADING_RE.fullmatch(line)
    if match is None:
        return None
    marker, text = match.groups()
    text = re.sub(r"[ \t]+#+[ \t]*$", "", text).strip()
    return len(marker), text


def _markdown_content_lines(text: str) -> list[tuple[int, str]]:
    visible: list[tuple[int, str]] = []
    fence_char: str | None = None
    fence_length = 0
    in_comment = False
    for line_number, raw in enumerate(text.splitlines(), start=1):
        if fence_char is not None:
            closing = re.fullmatch(rf"\s{{0,3}}{re.escape(fence_char)}{{{fence_length},}}[ \t]*", raw)
            if closing is not None:
                fence_char, fence_length = None, 0
            continue
        if not in_comment:
            opening = _fence_opening(raw)
            if opening is not None:
                fence_char, fence_length = opening
                continue
        content, in_comment = _strip_html_comments(raw, in_comment)
        if content.startswith("\t") or content.startswith("    "):
            continue
        visible.append((line_number, content.rstrip()))
    return visible


def validate_plan_implementation_design(
    plan_text: str, tasks_text: str | None = None, *, require_explicit: bool = False
) -> list[str]:
    errors: list[str] = []
    if not isinstance(plan_text, str):
        return ["plan content must be text"]
    lines = plan_text.splitlines()
    metadata: dict[str, str] = {}
    explicit = False
    body_start = 0
    governed_fields = {"governed_delivery_classification", "governed_delivery_reason"}

    if lines and lines[0].strip() == "---":
        closing = next((index for index in range(1, len(lines)) if lines[index].strip() == "---"), None)
        if closing is None:
            return ["plan front matter has no closing --- delimiter"]
        body_start = closing + 1
        front_matter_lines = lines[1:closing]
        if any(raw.strip() == "## Implementation Design" for raw in front_matter_lines) or any(
            DESIGN_ID_CANDIDATE_RE.search(raw) for raw in front_matter_lines
        ):
            errors.append("plan front matter cannot contain implementation design content")
        governed_parent_line: int | None = None
        for line_number, raw in enumerate(lines[1:closing], start=2):
            stripped = raw.strip()
            if not stripped or stripped.startswith("#"):
                continue
            if raw != raw.lstrip():
                if governed_parent_line is not None:
                    errors.append(
                        f"line {line_number}: governed front-matter value from line "
                        f"{governed_parent_line} must stay on one physical line"
                    )
                if stripped.startswith("governed_delivery_"):
                    errors.append(f"line {line_number}: governed front-matter fields must be top-level")
                continue
            governed_parent_line = None
            if ":" not in stripped:
                continue
            key, raw_value = stripped.split(":", 1)
            key = key.strip()
            if not key.startswith("governed_delivery_"):
                continue
            governed_parent_line = line_number
            if key not in governed_fields:
                errors.append(f"line {line_number}: unknown governed front-matter field {key}")
                continue
            if key in metadata:
                errors.append(f"line {line_number}: governed front-matter field {key} is duplicated")
                continue
            metadata[key] = _front_matter_scalar(raw_value)
        explicit = bool(metadata) or any(
            raw.strip().startswith("governed_delivery_") for raw in lines[1:closing]
        )

    classification = metadata.get("governed_delivery_classification") if explicit else "none"
    reason = metadata.get("governed_delivery_reason") if explicit else None
    if explicit:
        if classification not in {"none", "bounded", "architecture"}:
            errors.append("governed_delivery_classification must be none, bounded, or architecture")
        if not isinstance(reason, str) or not reason:
            errors.append("governed_delivery_reason must be a non-empty single-line value")
    elif require_explicit:
        errors.append("plan-review requires explicit classification in governed-delivery front matter")

    body_text = "\n".join(lines[body_start:])
    visible_lines = _markdown_content_lines(body_text)
    design_headings = [
        index
        for index, (_, line) in enumerate(visible_lines)
        if _atx_heading(line) == (2, "Implementation Design")
    ]
    all_candidates = {
        match.group(0) for _, line in visible_lines for match in DESIGN_ID_CANDIDATE_RE.finditer(line)
    }
    if not explicit:
        if design_headings or all_candidates:
            errors.append("implementation design content requires explicit governed-delivery front matter")
        task_candidates = (
            {
                match.group(0)
                for _, line in _markdown_content_lines(tasks_text)
                for match in DESIGN_ID_CANDIDATE_RE.finditer(line)
            }
            if tasks_text is not None
            else set()
        )
        if task_candidates:
            errors.append("implicit none plan cannot have implementation-design IDs in tasks")
        return errors

    if classification == "none":
        if design_headings or all_candidates:
            errors.append("none classification cannot contain an Implementation Design section or design IDs")
        task_candidates = (
            {
                match.group(0)
                for _, line in _markdown_content_lines(tasks_text)
                for match in DESIGN_ID_CANDIDATE_RE.finditer(line)
            }
            if tasks_text is not None
            else set()
        )
        if task_candidates:
            errors.append("none classification cannot have implementation-design IDs in tasks")
        return errors
    if classification not in {"bounded", "architecture"}:
        return errors
    if len(design_headings) != 1:
        errors.append("bounded or architecture plan requires exactly one ## Implementation Design section")
        return errors

    start = design_headings[0] + 1
    end = next(
        (
            index
            for index in range(start, len(visible_lines))
            if (_atx_heading(visible_lines[index][1]) or (None, None))[0] == 2
        ),
        len(visible_lines),
    )
    section_lines = visible_lines[start:end]
    current_section: str | None = None
    seen_sections: set[str] = set()
    decisions: dict[str, str] = {}
    for relative_line, raw in section_lines:
        offset = body_start + relative_line
        stripped = raw.strip()
        parsed_heading = _atx_heading(raw)
        if parsed_heading is not None and parsed_heading[0] == 3:
            heading = parsed_heading[1]
            if heading not in DESIGN_SECTIONS:
                errors.append(f"line {offset}: unknown Implementation Design heading {heading}")
                current_section = None
            elif heading in seen_sections:
                errors.append(f"line {offset}: Implementation Design heading {heading} is duplicated")
                current_section = heading
            else:
                seen_sections.add(heading)
                current_section = heading
            continue
        match = DESIGN_BULLET_RE.fullmatch(stripped)
        if match:
            decision_id, description = match.groups()
            if DESIGN_ID_RE.fullmatch(decision_id) is None:
                errors.append(f"line {offset}: design decision {decision_id} has invalid ID syntax")
                continue
            prefix = decision_id[3]
            if current_section is None:
                errors.append(f"line {offset}: design decision {decision_id} has no recognized heading")
            elif DESIGN_SECTIONS[current_section] != prefix:
                errors.append(f"line {offset}: design decision {decision_id} has the wrong section prefix")
            if decision_id in decisions:
                errors.append(f"line {offset}: design decision {decision_id} is duplicated")
            else:
                decisions[decision_id] = description

    parsed_ids = set(decisions)
    for decision_id in sorted(all_candidates - parsed_ids):
        if DESIGN_ID_RE.fullmatch(decision_id) is None:
            errors.append(f"design decision {decision_id} has invalid ID syntax")
        else:
            errors.append(f"design decision {decision_id} must use the required bold bullet syntax")
    required_sections = {"Responsibilities", "Test Seams"}
    if classification == "architecture":
        required_sections |= {"Alternatives", "Consequences"}
    for heading in sorted(required_sections):
        prefix = DESIGN_SECTIONS[heading]
        if not any(decision_id.startswith(f"ID-{prefix}") for decision_id in decisions):
            errors.append(f"{classification} implementation design requires {heading}")

    if tasks_text is not None:
        task_ids: set[str] = set()
        for line_number, line in _markdown_content_lines(tasks_text):
            candidates = {match.group(0) for match in DESIGN_ID_CANDIDATE_RE.finditer(line)}
            if not candidates:
                continue
            if not TASK_LINE_RE.match(line):
                errors.append(f"tasks line {line_number}: implementation-design IDs must be on a task checklist line")
                continue
            for decision_id in candidates:
                if DESIGN_ID_RE.fullmatch(decision_id) is None:
                    errors.append(
                        f"tasks line {line_number}: unknown implementation-design ID "
                        f"{decision_id} has invalid ID syntax"
                    )
                else:
                    task_ids.add(decision_id)
        implementation_ids = {
            decision_id for decision_id in decisions if decision_id[3] not in {"X", "C"}
        }
        for decision_id in sorted(implementation_ids - task_ids):
            errors.append(f"tasks are missing implementation-design ID {decision_id}")
        for decision_id in sorted(task_ids - parsed_ids):
            errors.append(f"tasks reference unknown implementation-design ID {decision_id}")
        for decision_id in sorted(item for item in task_ids if item[3] in {"X", "C"}):
            errors.append(f"tasks reference non-implementation design ID {decision_id}")
    return errors


def _mapped_artifact_text(
    repo: Path, state: dict[str, Any], name: str, errors: list[str]
) -> str | None:
    feature = state.get("feature_directory")
    artifacts = state.get("artifacts")
    artifact = artifacts.get(name) if isinstance(artifacts, dict) else None
    if artifact is None:
        return None
    if not isinstance(feature, str) or not isinstance(artifact, dict):
        errors.append(f"cannot resolve mapped {name} artifact")
        return None
    expected = f"{feature}/{name}.md"
    path_value = artifact.get("path")
    if path_value != expected:
        errors.append(f"mapped {name} path must equal {expected}")
        return None
    path_errors: list[str] = []
    _repo_path(path_value, f"artifacts.{name}.path", path_errors)
    if path_errors:
        errors.extend(path_errors)
        return None
    root = repo.resolve()
    path = (root / Path(*path_value.split("/"))).resolve()
    try:
        path.relative_to(root)
    except ValueError:
        errors.append(f"mapped {name} path escapes repository")
        return None
    try:
        content = path.read_bytes()
    except OSError as error:
        errors.append(f"mapped {name} cannot be read: {error}")
        return None
    expected_hash = artifact.get("sha256")
    actual_hash = hashlib.sha256(content).hexdigest()
    if not isinstance(expected_hash, str) or expected_hash.lower() != actual_hash:
        errors.append(f"mapped {name} hash does not match workflow state")
        return None
    try:
        return content.decode("utf-8")
    except UnicodeDecodeError:
        errors.append(f"mapped {name} must be UTF-8")
        return None


def validate_state_plan_content(state: Any, repo: Path) -> list[str]:
    if not isinstance(state, dict):
        return ["workflow state must be an object for plan validation"]
    errors: list[str] = []
    plan_gate = state.get("gates", {}).get("plan-review") if isinstance(state.get("gates"), dict) else None
    stage = state.get("stage")
    plan_required = stage in {
        "plan-review", "tasks", "definition-review", "implementation-approval", "implement",
        "converge", "lookback", "quality-review", "conditional-e2e", "verify", "complete",
    } or (
        isinstance(plan_gate, dict)
        and plan_gate.get("status") in {"accepted", "revised", "blocked", "invalidated"}
    )
    tasks_required = stage in {
        "definition-review", "implementation-approval", "implement", "converge",
        "lookback", "quality-review", "conditional-e2e", "verify", "complete",
    }
    plan_text = _mapped_artifact_text(Path(repo), state, "plan", errors)
    if plan_text is None:
        if plan_required and not any("mapped plan" in error for error in errors):
            errors.append("mapped plan artifact is required for deterministic plan validation")
        return errors
    tasks_text = _mapped_artifact_text(Path(repo), state, "tasks", errors)
    if tasks_text is None and tasks_required and not any("mapped tasks" in error for error in errors):
        errors.append("mapped tasks artifact is required for deterministic plan/task validation")
    require_explicit = (
        stage != "complete"
        and (
            stage == "plan-review"
            or (
                isinstance(plan_gate, dict)
                and plan_gate.get("status") in {"accepted", "revised", "blocked", "invalidated"}
            )
        )
    )
    errors.extend(
        validate_plan_implementation_design(plan_text, tasks_text, require_explicit=require_explicit)
    )
    return errors


def _checkout_identity_path(value: Any, field: str, errors: list[str]) -> None:
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{field} must be a canonical checkout identity path")
        return
    if "\\" in value or value.startswith("~"):
        errors.append(f"{field} must use canonical forward-slash path syntax")
        return
    absolute = value.startswith("/") or re.match(r"^[A-Za-z]:/", value) is not None
    if not absolute:
        _repo_path(value, field, errors)
        return
    if value.startswith("//"):
        errors.append(f"{field} must be a normalized absolute path")
        return
    if value != "/" and value.endswith("/"):
        errors.append(f"{field} must be a normalized absolute path without a trailing slash")
        return
    if re.match(r"^[A-Za-z]:/", value):
        path_parts = value[3:].split("/")
    else:
        path_parts = value[1:].split("/")
    if any(part in {"", ".", ".."} for part in path_parts):
        errors.append(f"{field} must be a normalized absolute path")


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
            "correction_outcome", "earliest_owner",
        }
        for index, item in enumerate(findings):
            finding = _mapping(item, f"findings[{index}]", errors)
            if finding is None:
                continue
            _required(finding, finding_fields, f"findings[{index}].", errors)
            _unknown(finding, finding_fields, f"findings[{index}].", errors)
            for field in finding_fields - {"severity", "artifact", "earliest_owner"}:
                _nonempty_string(finding.get(field), f"findings[{index}].{field}", errors)
            if finding.get("earliest_owner") not in {"spec", "plan", "tasks", "implement"}:
                errors.append(f"findings[{index}].earliest_owner is invalid")
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


def _validate_compact_gate(value: Any, field: str, errors: list[str]) -> None:
    gate = _mapping(value, field, errors)
    if gate is None:
        return
    fields = {"status", "attempt", "verdict_path"}
    _required(gate, fields, f"{field}.", errors)
    _unknown(gate, fields, f"{field}.", errors)
    status = gate.get("status")
    if status not in {"pending", "accepted", "revised", "blocked", "invalidated"}:
        errors.append(f"{field}.status is invalid")
    attempt = gate.get("attempt")
    if not isinstance(attempt, int) or isinstance(attempt, bool) or attempt < 0:
        errors.append(f"{field}.attempt must be a non-negative integer")
    _repo_path(gate.get("verdict_path"), f"{field}.verdict_path", errors, nullable=True)
    if status in {"accepted", "revised", "blocked"} and gate.get("verdict_path") is None:
        errors.append(f"{field}.verdict_path is required when {status}")


def _validate_compact_checkpoint(value: Any, field: str, errors: list[str]) -> None:
    checkpoint = _mapping(value, field, errors)
    if checkpoint is None:
        return
    fields = {"checkpoint_id", "stage", "commit_sha", "binding_status"}
    _required(checkpoint, fields, f"{field}.", errors)
    _unknown(checkpoint, fields, f"{field}.", errors)
    _nonempty_string(checkpoint.get("checkpoint_id"), f"{field}.checkpoint_id", errors)
    if checkpoint.get("stage") not in VALID_STAGES:
        errors.append(f"{field}.stage is invalid")
    binding = checkpoint.get("binding_status")
    if binding not in {"pending", "bound"}:
        errors.append(f"{field}.binding_status is invalid")
    if binding == "pending" and checkpoint.get("commit_sha") is not None:
        errors.append(f"{field}.commit_sha must be null while binding is pending")
    if binding == "bound":
        _sha(checkpoint.get("commit_sha"), f"{field}.commit_sha", errors)


def _validate_legacy_workflow_state(data: Any) -> list[str]:
    errors: list[str] = []
    state = _mapping(data, "workflow state", errors)
    if state is None:
        return errors
    _required(state, STATE_REQUIRED, "", errors)
    _unknown(state, STATE_REQUIRED, "", errors)
    try:
        canonical_size = len(json.dumps(state, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
    except (TypeError, ValueError):
        canonical_size = MAX_WORKFLOW_STATE_BYTES + 1
    if canonical_size > MAX_WORKFLOW_STATE_BYTES:
        errors.append(f"workflow state canonical JSON exceeds {MAX_WORKFLOW_STATE_BYTES} bytes")
    if state.get("schema_version") != "3.0":
        errors.append("schema_version must equal 3.0")
    _nonempty_string(state.get("bmad_story_key"), "bmad_story_key", errors)
    _nonempty_string(state.get("bmad_epic_key"), "bmad_epic_key", errors)
    _repo_path(state.get("feature_directory"), "feature_directory", errors)
    if isinstance(state.get("feature_directory"), str) and not re.fullmatch(r"specs/[^/]+", state["feature_directory"]):
        errors.append("feature_directory must be exactly one directory under specs/")
    stage = state.get("stage")
    if stage not in VALID_STAGES - {"lookback"}:
        errors.append("stage is invalid")

    preflight = _mapping(state.get("preflight"), "preflight", errors)
    if preflight is not None:
        fields = {"branch", "head", "git_identity_sha256", "status_sha256", "observed_at"}
        _required(preflight, fields, "preflight.", errors)
        _unknown(preflight, fields, "preflight.", errors)
        _nonempty_string(preflight.get("branch"), "preflight.branch", errors)
        _sha(preflight.get("head"), "preflight.head", errors)
        _hash(preflight.get("git_identity_sha256"), "preflight.git_identity_sha256", errors)
        _hash(preflight.get("status_sha256"), "preflight.status_sha256", errors)
        _timestamp(preflight.get("observed_at"), "preflight.observed_at", errors)

    artifacts = _mapping(state.get("artifacts"), "artifacts", errors)
    if artifacts is not None:
        fields = {"spec", "plan", "tasks"}
        _required(artifacts, fields, "artifacts.", errors)
        _unknown(artifacts, fields, "artifacts.", errors)
        for name in sorted(fields):
            value = artifacts.get(name)
            if value is None:
                continue
            artifact = _mapping(value, f"artifacts.{name}", errors)
            if artifact is None:
                continue
            _required(artifact, {"path", "sha256"}, f"artifacts.{name}.", errors)
            _unknown(artifact, {"path", "sha256"}, f"artifacts.{name}.", errors)
            _repo_path(artifact.get("path"), f"artifacts.{name}.path", errors)
            _hash(artifact.get("sha256"), f"artifacts.{name}.sha256", errors)
        if stage in {"plan", "plan-review", "tasks", "definition-review", "implementation-approval", "implement", "converge", "quality-review", "conditional-e2e", "verify", "complete"} and artifacts.get("spec") is None:
            errors.append("artifacts.spec is required at the current stage")
        if stage in {"plan-review", "tasks", "definition-review", "implementation-approval", "implement", "converge", "quality-review", "conditional-e2e", "verify", "complete"} and artifacts.get("plan") is None:
            errors.append("artifacts.plan is required at the current stage")
        if stage in {"definition-review", "implementation-approval", "implement", "converge", "quality-review", "conditional-e2e", "verify", "complete"} and artifacts.get("tasks") is None:
            errors.append("artifacts.tasks is required at the current stage")

    gates = _mapping(state.get("gates"), "gates", errors)
    if gates is not None:
        fields = {"plan-review", "definition-review", "quality-review"}
        _required(gates, fields, "gates.", errors)
        _unknown(gates, fields, "gates.", errors)
        for name in sorted(fields):
            _validate_compact_gate(gates.get(name), f"gates.{name}", errors)

    attempts = _mapping(state.get("attempts"), "attempts", errors)
    if attempts is not None:
        fields = {"definition-review", "converge", "quality-review"}
        _required(attempts, fields, "attempts.", errors)
        _unknown(attempts, fields, "attempts.", errors)
        for name in sorted(fields):
            value = attempts.get(name)
            if not isinstance(value, int) or isinstance(value, bool) or value < 0:
                errors.append(f"attempts.{name} must be a non-negative integer")

    approvals = _array(state.get("approvals"), "approvals", errors)
    if approvals is not None:
        if len(approvals) > 12:
            errors.append("approvals must contain at most 12 current decision references")
        decision_ids: set[str] = set()
        for index, value in enumerate(approvals):
            approval = _mapping(value, f"approvals[{index}]", errors)
            if approval is None:
                continue
            fields = {"decision_id", "status", "scope_sha256", "evidence_path", "decided_at"}
            _required(approval, fields, f"approvals[{index}].", errors)
            _unknown(approval, fields, f"approvals[{index}].", errors)
            decision_id = approval.get("decision_id")
            _nonempty_string(decision_id, f"approvals[{index}].decision_id", errors)
            if isinstance(decision_id, str) and decision_id in decision_ids:
                errors.append(f"approvals[{index}].decision_id is duplicated")
            if isinstance(decision_id, str):
                decision_ids.add(decision_id)
            status = approval.get("status")
            if status not in {"pending", "accepted", "declined"}:
                errors.append(f"approvals[{index}].status is invalid")
            _hash(approval.get("scope_sha256"), f"approvals[{index}].scope_sha256", errors)
            _repo_path(approval.get("evidence_path"), f"approvals[{index}].evidence_path", errors, nullable=True)
            _timestamp(approval.get("decided_at"), f"approvals[{index}].decided_at", errors, nullable=True)
            if status in {"accepted", "declined"} and (approval.get("evidence_path") is None or approval.get("decided_at") is None):
                errors.append(f"approvals[{index}] completed decision requires evidence_path and decided_at")

    convergence = _mapping(state.get("convergence"), "convergence", errors)
    if convergence is not None:
        fields = {"status", "attempt", "outcome", "report_path"}
        _required(convergence, fields, "convergence.", errors)
        _unknown(convergence, fields, "convergence.", errors)
        status = convergence.get("status")
        if status not in {"pending", "accepted", "revised", "blocked", "invalidated"}:
            errors.append("convergence.status is invalid")
        attempt = convergence.get("attempt")
        if not isinstance(attempt, int) or isinstance(attempt, bool) or attempt < 0:
            errors.append("convergence.attempt must be a non-negative integer")
        outcome = convergence.get("outcome")
        if outcome not in {"pending", "converged", "tasks_appended", "blocked", "failed"}:
            errors.append("convergence.outcome is invalid")
        _repo_path(convergence.get("report_path"), "convergence.report_path", errors, nullable=True)
        if outcome == "converged" and (status != "accepted" or convergence.get("report_path") is None):
            errors.append("converged outcome requires accepted status and report_path")
        if outcome == "tasks_appended" and (status != "revised" or convergence.get("report_path") is None):
            errors.append("tasks_appended outcome requires revised status and report_path")

    e2e = _mapping(state.get("e2e"), "e2e", errors)
    if e2e is not None:
        fields = {"result", "decision_path"}
        _required(e2e, fields, "e2e.", errors)
        _unknown(e2e, fields, "e2e.", errors)
        result = e2e.get("result")
        if result not in VALID_E2E_RESULTS:
            errors.append("e2e.result is invalid")
        _repo_path(e2e.get("decision_path"), "e2e.decision_path", errors, nullable=True)
        if result != "pending" and e2e.get("decision_path") is None:
            errors.append("completed E2E decision requires decision_path")

    evidence_paths = state.get("current_evidence_paths")
    _string_array(evidence_paths, "current_evidence_paths", errors, paths=True)
    if isinstance(evidence_paths, list):
        if len(evidence_paths) > 12:
            errors.append("current_evidence_paths must contain at most 12 paths")
        if len(evidence_paths) != len(set(item for item in evidence_paths if isinstance(item, str))):
            errors.append("current_evidence_paths must be unique")

    checkpoint = state.get("checkpoint")
    if checkpoint is not None:
        _validate_compact_checkpoint(checkpoint, "checkpoint", errors)
        if isinstance(checkpoint, dict) and checkpoint.get("stage") == "lookback":
            errors.append("lookback checkpoints require state 3.1")

    resume = state.get("resume")
    if resume is not None:
        resume_data = _mapping(resume, "resume", errors)
        if resume_data is not None:
            fields = {"stage", "reason", "evidence_path"}
            _required(resume_data, fields, "resume.", errors)
            _unknown(resume_data, fields, "resume.", errors)
            if resume_data.get("stage") not in VALID_STAGES - {"lookback"}:
                errors.append("resume.stage is invalid")
            _nonempty_string(resume_data.get("reason"), "resume.reason", errors)
            if isinstance(resume_data.get("reason"), str) and len(resume_data["reason"]) > 240:
                errors.append("resume.reason must not exceed 240 characters")
            _repo_path(resume_data.get("evidence_path"), "resume.evidence_path", errors, nullable=True)

    _timestamp(state.get("updated_at"), "updated_at", errors)
    if attempts is not None:
        reset_conditions = {
            "definition-review": isinstance(gates, dict)
            and isinstance(gates.get("definition-review"), dict)
            and gates["definition-review"].get("status") in {"pending", "accepted", "invalidated"},
            "quality-review": isinstance(gates, dict)
            and isinstance(gates.get("quality-review"), dict)
            and gates["quality-review"].get("status") in {"pending", "accepted", "invalidated"},
            "converge": isinstance(convergence, dict)
            and convergence.get("status") in {"pending", "accepted", "invalidated"},
        }
        for phase, must_reset in reset_conditions.items():
            if must_reset and attempts.get(phase) != 0:
                errors.append(f"attempts.{phase} must reset to 0 after acceptance or boundary replacement")
    if stage == "complete":
        if checkpoint is None:
            errors.append("checkpoint is required for complete state")
        elif isinstance(checkpoint, dict) and checkpoint.get("stage") != "complete":
            errors.append("complete state requires a complete checkpoint")
        if resume is not None:
            errors.append("resume must be null for complete state")
    elif stage in VALID_STAGES and resume is None:
        errors.append("resume is required for non-complete state")
    return errors


def validate_workflow_state(data: Any) -> list[str]:
    try:
        if isinstance(data, dict) and data.get("schema_version") == "3.1":
            from workflow_runtime import validate_current_state
            return validate_current_state(data, _validate_legacy_workflow_state)
        return _validate_legacy_workflow_state(data)
    except (ValueError, TypeError, AttributeError, KeyError) as error:
        return [f"malformed workflow state: {error}"]

def _git(repo: Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(repo), *args], text=True, stderr=subprocess.DEVNULL).strip()


def _has_trailer(body: str, key: str, value: str) -> bool:
    return re.search(rf"^{re.escape(key)}: {re.escape(value)}$", body, re.MULTILINE) is not None


def _legacy_plain_mapping_keys(
    text: str, section: str | None = None, expected_scalar: tuple[str, str] | None = None,
) -> bool:
    """Accept only generated plain-key syntax at the legacy proof's mapping levels.

    The scalar selector is intentionally not a YAML parser. Reject quoted/escaped,
    merge, explicit and flow keys instead of overlooking conflicting aliases.
    """
    seen_root = False
    root_keys: set[str] = set()
    child_keys: set[str] = set()
    selected = False
    child_indent = None
    for line in text.splitlines():
        stripped = line.lstrip()
        if not stripped or stripped.startswith("#"):
            continue
        indent = len(line) - len(stripped)
        if "\t" in line[:indent] or (indent and not seen_root):
            return False
        if not indent:
            if re.fullmatch(r"[A-Za-z0-9_-]+:(?:[ \t].*)?", line) is None:
                return False
            key = line.split(":", 1)[0]
            if key in root_keys:
                return False
            root_keys.add(key)
            seen_root = True
            selected = section is not None and key == section
            child_indent = None
            child_keys = set()
        elif selected:
            if child_indent is None:
                child_indent = indent
            if indent < child_indent or (
                indent == child_indent and re.fullmatch(r"[A-Za-z0-9_-]+:(?:[ \t].*)?", stripped) is None
            ):
                return False
            if indent == child_indent:
                key = stripped.split(":", 1)[0]
                if key in child_keys:
                    return False
                child_keys.add(key)
                if expected_scalar is not None and key == expected_scalar[0]:
                    if _front_matter_scalar(stripped.split(":", 1)[1]) != expected_scalar[1]:
                        return False
    return seen_root


def _legacy_completion_config(repo: Path, commit: str, current: dict, committed: Any) -> dict | None:
    """Recognize the pre-distribution 3.0 contract using only immutable Git evidence.

    This is not a default for missing modern configuration. A repository that has
    ever adopted the portable workflow before this checkpoint cannot downgrade by
    deleting its configuration and restoring legacy marker files.
    """
    if any(
        not isinstance(state, dict)
        or state.get("schema_version") != "3.0"
        or state.get("stage") != "complete"
        or validate_workflow_state(state)
        for state in (current, committed)
    ):
        return None
    if _git(repo, "rev-parse", "--is-shallow-repository") != "false":
        return None  # Truncated ancestry cannot establish pre-adoption provenance.
    portable_paths = (".governed-delivery", ".agents/skills/run-governed-delivery")
    if any(_tree_entry(repo, commit, path) is not None for path in portable_paths):
        return None
    if _git(repo, "log", "--full-history", "--format=%H", "-1", commit, "--", *portable_paths):
        return None

    def regular_text(relative: str) -> str | None:
        entry = _tree_entry(repo, commit, relative)
        if entry is None or entry[0] not in {"100644", "100755"} or entry[1] != "blob":
            return None
        return _git(repo, "show", f"{commit}:{relative}")

    skill = regular_text(".agents/skills/run-bmad-sprint-loop/SKILL.md")
    schema = regular_text(".agents/skills/run-bmad-sprint-loop/references/workflow-state.schema.json")
    manifest = regular_text("_bmad/_config/manifest.yaml")
    if skill is None or schema is None or manifest is None:
        return None
    front_matter = re.match(r"\A---\r?\n(.*?)\r?\n---(?:\r?\n|\Z)", skill, re.DOTALL)
    names = re.findall(r"^name:[ \t]*(.+?)[ \t]*$", front_matter[1], re.MULTILINE) if front_matter else []
    if (
        not front_matter or not _legacy_plain_mapping_keys(front_matter[1])
        or len(names) != 1 or _front_matter_scalar(names[0]) != "run-bmad-sprint-loop"
    ):
        return None

    def unique_object(pairs: list[tuple[str, Any]]) -> dict:
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("ambiguous legacy schema key")
            result[key] = value
        return result

    try:
        legacy_schema = json.loads(schema, object_pairs_hook=unique_object)
        if legacy_schema["properties"]["schema_version"]["const"] != "3.0":
            return None
    except (ValueError, KeyError, TypeError):
        return None
    # Reuse the dependency checker's bounded YAML selector, never execute an old
    # installed validator or infer historical settings from today's installation.
    from check_dependencies import _yaml_selected_scalar
    version = _yaml_selected_scalar(manifest, "installation.version")
    if not isinstance(version, str) or re.fullmatch(r"6\.11\.[0-9]+", version) is None:
        return None
    if not _legacy_plain_mapping_keys(manifest, "installation", ("version", version)):
        return None
    if len(re.findall(r"^installation:\s*$", manifest, re.MULTILINE)) != 1:
        return None
    return {
        "lifecycle": {"adapter": "bmad-6.11"},
        "paths": {"sprint_status": "docs/bmad/implementation-artifacts/sprint-status.yaml"},
    }


def validate_checkpoint_binding(data: Any, repo: Path, state_path: Path) -> list[str]:
    errors: list[str] = []
    if not isinstance(data, dict) or data.get("stage") != "complete":
        return errors
    checkpoint = data.get("checkpoint")
    if not isinstance(checkpoint, dict) or checkpoint.get("stage") != "complete":
        return errors
    checkpoint_id = checkpoint.get("checkpoint_id")
    if not isinstance(checkpoint_id, str) or not checkpoint_id:
        return errors

    try:
        output = _git(
            repo,
            "log",
            "--all",
            "--format=%H",
            "--fixed-strings",
            f"--grep=Workflow-Checkpoint: {checkpoint_id}",
        )
        candidates = []
        for candidate in output.splitlines() if output else []:
            body = _git(repo, "show", "-s", "--format=%B", candidate)
            is_ancestor = subprocess.run(
                ["git", "-C", str(repo), "merge-base", "--is-ancestor", candidate, "HEAD"],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                check=False,
            ).returncode == 0
            if is_ancestor and _has_trailer(body, "Workflow-Checkpoint", checkpoint_id):
                candidates.append(candidate)
    except (subprocess.CalledProcessError, OSError, ValueError) as error:
        return [f"checkpoint binding lookup failed: {error}"]

    if len(candidates) != 1:
        return [f"complete checkpoint must resolve to exactly one commit; found {len(candidates)}"]
    commit = candidates[0]
    cached_commit = checkpoint.get("commit_sha")
    if checkpoint.get("binding_status") == "bound" and cached_commit != commit:
        errors.append("bound checkpoint cached commit does not match the unique trailer commit")
    ancestry = subprocess.run(
        ["git", "-C", str(repo), "merge-base", "--is-ancestor", commit, "HEAD"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    if ancestry.returncode != 0:
        errors.append("complete checkpoint commit is not an ancestor of HEAD")

    try:
        body = _git(repo, "show", "-s", "--format=%B", commit)
        story_key = data.get("bmad_story_key")
        for key, value in (
            ("BMad-Story", story_key),
            ("Workflow-Scope", f"story/{story_key}" if isinstance(story_key, str) else None),
            ("Workflow-Stage", "complete"),
            ("Workflow-Checkpoint", checkpoint_id),
        ):
            if not isinstance(value, str) or not _has_trailer(body, key, value):
                errors.append(f"complete checkpoint commit has invalid {key} trailer")

        relative_state = state_path.resolve().relative_to(Path(repo).resolve()).as_posix()
        committed_state = json.loads(_git(repo, "show", f"{commit}:{relative_state}"))
        if not isinstance(committed_state, dict):
            errors.append("committed workflow state must be an object")
        else:
            committed_errors = validate_workflow_state(committed_state)
            errors.extend(f"committed workflow state is invalid: {error}" for error in committed_errors)
            identity_fields = ("bmad_story_key", "bmad_epic_key", "feature_directory")
            if any(committed_state.get(field) != data.get(field) for field in identity_fields):
                errors.append("committed workflow state identity does not match current state")
            committed_checkpoint = committed_state.get("checkpoint")
            if (
                not isinstance(committed_checkpoint, dict)
                or committed_checkpoint.get("checkpoint_id") != checkpoint_id
                or committed_checkpoint.get("stage") != "complete"
                or committed_checkpoint.get("binding_status") != "pending"
                or committed_checkpoint.get("commit_sha") is not None
            ):
                errors.append("checkpoint commit must contain the matching pending complete checkpoint")

        lifecycle_adapter = None
        workflow_config = {}
        legacy_completion = False
        sprint_status_path = "docs/bmad/implementation-artifacts/sprint-status.yaml"
        try:
            committed_config_text = _git(repo, "show", f"{commit}:.governed-delivery/config.toml")
            workflow_config = tomllib.loads(committed_config_text)
            lifecycle_config = workflow_config.get("lifecycle", {})
            paths_config = workflow_config.get("paths", {})
            if not isinstance(lifecycle_config, dict) or not isinstance(paths_config, dict):
                errors.append("committed governed-delivery lifecycle and paths must be tables")
                lifecycle_config = {}
                paths_config = {}
            lifecycle_adapter = lifecycle_config.get("adapter")
            configured_status_path = paths_config.get("sprint_status", sprint_status_path)
            if not isinstance(configured_status_path, str) or not configured_status_path:
                errors.append("committed sprint_status path must be a non-empty string")
            else:
                sprint_status_path = configured_status_path
        except subprocess.CalledProcessError:
            legacy_config = _legacy_completion_config(repo, commit, data, committed_state)
            if legacy_config is None:
                errors.append("committed governed-delivery configuration is missing")
            else:
                workflow_config = legacy_config
                lifecycle_adapter = legacy_config["lifecycle"]["adapter"]
                sprint_status_path = legacy_config["paths"]["sprint_status"]
                legacy_completion = True
        except tomllib.TOMLDecodeError as error:
            errors.append(f"committed governed-delivery configuration is invalid: {error}")
        if lifecycle_adapter is None:
            if not any("configuration is missing" in error for error in errors):
                errors.append("committed lifecycle adapter is required")
        elif not isinstance(lifecycle_adapter, str):
            errors.append("committed lifecycle adapter must be a string")
        elif lifecycle_adapter not in SUPPORTED_LIFECYCLE_ADAPTERS:
            errors.append(f"unsupported committed lifecycle adapter: {lifecycle_adapter}")
        try:
            status = _git(repo, "show", f"{commit}:{sprint_status_path}")
        except subprocess.CalledProcessError:
            status = None
        if legacy_completion:
            status_entry = _tree_entry(repo, commit, sprint_status_path)
            if status_entry is None or status_entry[0] not in {"100644", "100755"}:
                status = None
            elif status is not None:
                from check_dependencies import _yaml_selected_scalar
                if (
                    not _legacy_plain_mapping_keys(status, "development_status", (story_key, "done"))
                    or len(re.findall(r"^development_status:\s*$", status, re.MULTILINE)) != 1
                    or _yaml_selected_scalar(status, f"development_status.{story_key}") != "done"
                ):
                    errors.append("legacy checkpoint requires one development_status story entry set to done")
        if isinstance(lifecycle_adapter, str) and lifecycle_adapter in SUPPORTED_LIFECYCLE_ADAPTERS:
            if status is None:
                errors.append("BMad story done transition file is missing from checkpoint commit")
            elif isinstance(story_key, str):
                done_pattern = rf"^\s*{re.escape(story_key)}\s*:\s*done\s*(?:#.*)?$"
                if re.search(done_pattern, status, re.MULTILINE) is None:
                    errors.append("checkpoint commit does not contain the BMad story done transition")
        if isinstance(committed_state, dict) and committed_state.get("schema_version") == "3.1":
            from workflow_runtime import safe_path, validate_story_evidence

            def read_committed(relative: str) -> bytes | None:
                if not safe_path(relative):
                    return None
                try:
                    entry = _tree_entry(repo, commit, relative)
                    if entry is None or entry[0] not in {"100644", "100755"}:
                        return None
                    return subprocess.check_output(
                        ["git", "-C", str(repo), "show", f"{commit}:{relative}"], stderr=subprocess.DEVNULL,
                    )
                except (OSError, subprocess.CalledProcessError):
                    return None

            errors.extend(validate_story_evidence(
                committed_state, read_committed, config=workflow_config,
                content_lines=_markdown_content_lines, state_path=relative_state,
            ))
    except (subprocess.CalledProcessError, OSError, ValueError, json.JSONDecodeError) as error:
        errors.append(f"checkpoint commit content validation failed: {error}")
    return errors


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
    configured_command = None
    if repo is not None:
        config_path = Path(repo) / ".governed-delivery" / "config.toml"
        try:
            with config_path.open("rb") as stream:
                commands = tomllib.load(stream).get("commands", {})
            if not isinstance(commands, dict):
                errors.append("governed-delivery commands must be a table")
            else:
                configured_command = commands.get("build" if root.get("stage") == "verify-build" else "test")
        except (FileNotFoundError, OSError, tomllib.TOMLDecodeError):
            configured_command = None
    if not isinstance(root.get("command"), str) or not root["command"].strip():
        errors.append("epic close command must be non-empty")
    elif isinstance(configured_command, str) and configured_command.strip() and root.get("command") != configured_command:
        errors.append("epic close command does not match configured verification command")
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
        from decision_policy import RECOVERY_FIELDS, DEFINITION_AUTHOR_FIELDS, RECOVERY_PROFILES
        top_rules = schema.get("allOf")
        version_rules = {
            _schema_at(rule, "if", "properties", "schema_version", "const"): rule.get("then", {})
            for rule in top_rules if isinstance(rule, dict)
        } if isinstance(top_rules, list) else {}
        legacy_rules = version_rules.get("3.0", {}).get("allOf", [])
        complete_rule = next((
            rule for rule in top_rules
            if isinstance(top_rules, list)
            and _schema_at(rule, "if", "properties", "stage", "const") == "complete"
        ), None) if isinstance(top_rules, list) else None
        complete_constraint = _schema_at(
            complete_rule, "then", "properties", "checkpoint", "allOf"
        ) if isinstance(complete_rule, dict) else None
        complete_detail = complete_constraint[1] if isinstance(complete_constraint, list) and len(complete_constraint) > 1 else {}
        reset_targets = set()
        if isinstance(top_rules, list):
            for rule in legacy_rules:
                attempts_properties = _schema_at(rule, "then", "properties", "attempts", "properties")
                if isinstance(attempts_properties, dict):
                    reset_targets.update(
                        name for name, constraint in attempts_properties.items()
                        if isinstance(constraint, dict) and constraint.get("const") == 0
                    )
        gate_rules = _schema_at(schema, "$defs", "gate", "allOf")
        verdict_output_rule = next((
            rule for rule in gate_rules
            if isinstance(gate_rules, list)
            and _schema_enum(rule, "if", "properties", "status", "enum")
            == {"accepted", "revised", "blocked"}
        ), None) if isinstance(gate_rules, list) else None
        convergence_rules = _schema_at(schema, "$defs", "convergence", "allOf")
        convergence_outcomes = {
            _schema_at(rule, "if", "properties", "outcome", "const")
            for rule in convergence_rules if isinstance(convergence_rules, list) and isinstance(rule, dict)
        } if isinstance(convergence_rules, list) else set()
        checks = [
            (_schema_enum(schema, "properties", "schema_version", "enum") == {"3.0", "3.1"}, "supported state versions"),
            (set(version_rules.get("3.1", {}).get("required", [])) == {"recovery", "transition_head", "definition_author", "lookback"}, "current recovery fields"),
            (version_rules.get("3.1", {}).get("properties", {}).get("attempts") is False, "no current per-phase reset counters"),
            (set(_schema_at(schema, "$defs", "recovery", "required") or []) == RECOVERY_FIELDS, "recovery policy fields"),
            (set(_schema_at(schema, "properties", "definition_author", "required") or []) == DEFINITION_AUTHOR_FIELDS, "definition ownership fields"),
            (_schema_enum(schema, "properties", "definition_author", "properties", "profile", "enum") == set(RECOVERY_PROFILES), "retained author profiles"),
            (_schema_enum(schema, "$defs", "legacyStage", "enum") == VALID_STAGES - {"lookback"}, "historical stage contract"),
            (_schema_enum(schema, "$defs", "stage", "enum") == VALID_STAGES, "stage enum"),
            (
                _schema_enum(schema, "$defs", "e2e", "properties", "result", "enum")
                == VALID_E2E_RESULTS,
                "E2E result enum",
            ),
            (
                _schema_at(schema, "properties", "approvals", "maxItems") == 12
                and _schema_at(schema, "properties", "current_evidence_paths", "maxItems") == 12,
                "bounded current references",
            ),
            (
                _schema_at(schema, "$defs", "preflight", "properties", "head", "$ref") == "#/$defs/sha"
                and _schema_at(schema, "$defs", "preflight", "properties", "git_identity_sha256", "$ref")
                == "#/$defs/hash"
                and _schema_at(schema, "$defs", "preflight", "properties", "status_sha256", "$ref")
                == "#/$defs/hash",
                "compact preflight hashes",
            ),
            (isinstance(verdict_output_rule, dict), "gate raw verdict output rule"),
            ({"converged", "tasks_appended"} <= convergence_outcomes, "convergence outcome rules"),
            (
                _schema_at(complete_detail, "properties", "stage", "const") == "complete"
                and _schema_at(complete_detail, "properties", "binding_status", "const") is None,
                "complete-state pending-or-bound checkpoint rule",
            ),
            (
                {"definition-review", "converge", "quality-review"} <= reset_targets,
                "historical-only consecutive-attempt reset rules",
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
    workflow_config = {}
    if not repo.exists():
        return [f"{repo}: repository path does not exist"]
    if not repo.is_dir():
        return [f"{repo}: repository path is not a directory"]

    distribution_layout = (repo / "skills" / "run-governed-delivery" / "SKILL.md").is_file()
    config_path = repo / "templates" / "codex-config.fragment.toml" if distribution_layout else repo / ".codex" / "config.toml"
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

    skills_root = repo / "skills" if distribution_layout else repo / ".agents" / "skills"
    optional_roles_required = distribution_layout or any(
        (skills_root / name / "SKILL.md").is_file()
        for name in ("close-governed-epic", "audit-governed-architecture")
    )
    required_role_names = set(ROLE_PROFILES)
    if not optional_roles_required:
        required_role_names -= OPTIONAL_ROLE_NAMES

    agent_dir = repo / "agents" if distribution_layout else repo / ".codex" / "agents"
    paths = sorted(agent_dir.glob("*.toml")) if agent_dir.is_dir() else []
    expected_files = {f"{name}.toml" for name in required_role_names}
    workflow_files = {f"{name}.toml" for name in ROLE_PROFILES}
    actual_files = {path.name for path in paths}
    for name in sorted(expected_files - actual_files):
        errors.append(f"{agent_dir / name}: missing required agent file")
    if distribution_layout:
        for name in sorted(actual_files - expected_files):
            errors.append(f"{agent_dir / name}: unexpected agent file")
    else:
        for name in sorted(actual_files & RETIRED_ROLE_FILES):
            errors.append(f"{agent_dir / name}: retired governed-delivery agent file must be removed")
    seen: set[str] = set()
    for path in paths:
        if not distribution_layout and path.name not in workflow_files:
            continue
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

    required_workflow_skills = {
        "run-governed-delivery": ("speckit-specify", "speckit-converge", "definition-reviewer", "tdd-test-author"),
        "governed-tdd": ("red before green", "one slice at a time", "public interface"),
    }
    optional_workflow_skills = {
        "close-governed-epic": ("$audit-governed-architecture", "workflow-manifest-sha256", "pending-binding"),
        "audit-governed-architecture": ("$bmad-review", "$codebase-design", "explore/report"),
    }
    workflow_skills = dict(required_workflow_skills)
    if distribution_layout or any((skills_root / name / "SKILL.md").is_file() for name in optional_workflow_skills):
        workflow_skills.update(optional_workflow_skills)
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
    architecture_enabled = False
    architecture_adapter = None
    lifecycle_adapter = None
    definition_adapter = None
    if not distribution_layout:
        workflow_config_path = repo / ".governed-delivery" / "config.toml"
        try:
            with workflow_config_path.open("rb") as stream:
                workflow_config = tomllib.load(stream)
            architecture_config = workflow_config.get("architecture_review", {})
            lifecycle_config = workflow_config.get("lifecycle", {})
            definition_config = workflow_config.get("definition", {})
            if not isinstance(architecture_config, dict):
                errors.append(f"{workflow_config_path}: architecture_review must be a table")
                architecture_config = {}
            if not isinstance(lifecycle_config, dict):
                errors.append(f"{workflow_config_path}: lifecycle must be a table")
                lifecycle_config = {}
            if not isinstance(definition_config, dict):
                errors.append(f"{workflow_config_path}: definition must be a table")
                definition_config = {}
            architecture_enabled = architecture_config.get("enabled") is True
            architecture_adapter = architecture_config.get("adapter")
            lifecycle_adapter = lifecycle_config.get("adapter")
            definition_adapter = definition_config.get("adapter")
        except FileNotFoundError:
            errors.append(f"{workflow_config_path}: missing required file")
            architecture_enabled = False
        except (OSError, tomllib.TOMLDecodeError) as error:
            errors.append(f"{workflow_config_path}: invalid configuration: {error}")
            architecture_enabled = False
    if not distribution_layout:
        if lifecycle_adapter is None:
            errors.append("lifecycle adapter is required")
        elif not isinstance(lifecycle_adapter, str):
            errors.append("lifecycle adapter must be a string")
        elif lifecycle_adapter not in SUPPORTED_LIFECYCLE_ADAPTERS:
            errors.append(f"unsupported lifecycle adapter: {lifecycle_adapter}")
        if definition_adapter is None:
            errors.append("definition adapter is required")
        elif not isinstance(definition_adapter, str):
            errors.append("definition adapter must be a string")
        elif definition_adapter not in SUPPORTED_DEFINITION_ADAPTERS:
            errors.append(f"unsupported definition adapter: {definition_adapter}")
    if architecture_enabled:
        requirements = {
            "bmad-review-codebase-design": ("bmad-review", "codebase-design"),
        }.get(architecture_adapter)
        if requirements is None:
            errors.append(f"unsupported architecture-review adapter: {architecture_adapter}")
        else:
            for skill_name in requirements:
                upstream = skills_root / skill_name / "SKILL.md"
                if not upstream.is_file():
                    errors.append(f"{upstream}: enabled architecture-review adapter dependency is missing")

    refs = skills_root / "run-governed-delivery" / "references" / "schemas"
    schemas = {
        "verdict.schema.json": VERDICT_REQUIRED,
        "workflow-state.schema.json": STATE_REQUIRED - {"attempts"},
        "epic-command-evidence.schema.json": EPIC_EVIDENCE_REQUIRED,
        "epic-architecture-gate.schema.json": EPIC_GATE_REQUIRED,
        "retrospective-state.schema.json": RETRO_REQUIRED,
    }
    schema_defs = {
        "verdict.schema.json": {"boundary", "finding"},
        "workflow-state.schema.json": {
            "sha", "hash", "timestamp", "path", "stage", "preflight", "artifact", "gate",
            "approval", "convergence", "e2e", "checkpoint", "resume", "legacyStage", "recovery",
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
    for state_path in sorted(repo.glob("specs/*/workflow-state.json")):
        state = _load_json(state_path, errors)
        if state is None:
            continue
        errors.extend(f"{state_path}: {error}" for error in validate_workflow_state(state))
        errors.extend(f"{state_path}: {error}" for error in validate_state_plan_content(state, repo))
        if state.get("schema_version") == "3.1" and state.get("stage") != "complete":
            from workflow_runtime import file_reader, validate_story_evidence
            errors.extend(f"{state_path}: {error}" for error in validate_story_evidence(
                state, file_reader(repo), config=workflow_config,
                content_lines=_markdown_content_lines, state_path=state_path.relative_to(repo).as_posix(),
            ))
        errors.extend(f"{state_path}: {error}" for error in validate_checkpoint_binding(state, repo, state_path))
    return errors


def validate_story_request(repo: Path, state_path: Path, *, result_path: Path | None = None,
                           next_stage: str | None = None, next_profile: str | None = None) -> dict:
    """Validate one story / propose one transition without writing or scanning peers."""
    from decision_policy import recovery_dispatch_decision, technical_retry_decision
    from story_learning import validate_project_lessons_path, validate_lookback
    from workflow_runtime import (
        canonical_bytes, digest, file_reader, propose_transition, read_record,
        safe_path, validate_story_evidence,
    )

    errors: list[str] = []
    response: dict = {"ok": False, "errors": errors}
    root = Path(repo).resolve()
    read = file_reader(root)
    try:
        resolved = (root / state_path).resolve()
        relative = resolved.relative_to(root).as_posix()
        if not safe_path(relative):
            raise ValueError("story-state must be inside the selected repository")
        raw = read(relative)
        state = json.loads(raw) if raw else None
        if not isinstance(state, dict):
            raise ValueError("story-state must contain a readable JSON object")
        if relative != state.get("feature_directory", "") + "/workflow-state.json":
            raise ValueError("story-state must be the mapped feature's workflow-state.json")
        errors.extend(validate_workflow_state(state))
        if errors:
            return response
        config_raw = read(".governed-delivery/config.toml")
        if config_raw is None:
            raise ValueError("missing .governed-delivery/config.toml")
        config = tomllib.loads(config_raw.decode("utf-8"))
        if config.get("lifecycle", {}).get("adapter") not in SUPPORTED_LIFECYCLE_ADAPTERS:
            errors.append("unsupported or missing lifecycle adapter")
        if config.get("definition", {}).get("adapter") not in SUPPORTED_DEFINITION_ADAPTERS:
            errors.append("unsupported or missing definition adapter")
        protected = [a["path"] for a in state["artifacts"].values() if isinstance(a, dict)]
        _, path_errors = validate_project_lessons_path(root, config, protected_paths=protected + [relative])
        errors.extend(path_errors)
        if state["stage"] == "complete":
            if result_path is not None or next_stage is not None or next_profile is not None:
                errors.append("completed stories cannot be reactivated through a transition or dispatch")
            errors.extend(validate_checkpoint_binding(state, root, resolved))
            response["ok"] = not errors
            return response
        errors.extend(validate_state_plan_content(state, root))
        errors.extend(validate_story_evidence(state, read, config=config,
                      content_lines=_markdown_content_lines, state_path=relative,
                      current_boundary=result_path is None))
        if errors:
            return response
        if result_path is not None:
            result_relative = (root / result_path).resolve().relative_to(root).as_posix()
            if result_relative == relative or result_relative in protected:
                raise ValueError("result evidence must not overwrite state or definition artifacts")
            result_raw = read(result_relative)
            record = json.loads(result_raw) if result_raw else None
            if not isinstance(record, dict):
                raise ValueError("result requires readable root-owned transition JSON")
            state, completed = propose_transition(state, record, result_relative, read, config=config)
            canonical = canonical_bytes(completed)
            virtual_read = lambda path: canonical if path == result_relative else read(path)
            errors.extend(validate_workflow_state(state))
            errors.extend(validate_story_evidence(state, virtual_read, config=config,
                          content_lines=_markdown_content_lines, state_path=relative))
            response.update(proposed_state=state, transition_json=canonical.decode("utf-8"),
                            transition_sha256=digest(canonical), transition_path=result_relative)
        else:
            virtual_read = read
        if (next_stage is not None or next_profile is not None) and state["schema_version"] != "3.1":
            errors.append("active state 3.0 requires bounded adoption before dispatch")
        elif next_stage is not None:
            if next_stage not in VALID_STAGES:
                raise ValueError("next-stage is invalid")
            classification = None
            artifact = state.get("artifacts", {}).get("plan")
            if isinstance(artifact, dict):
                text = (virtual_read(artifact["path"]) or b"").decode("utf-8")
                front = re.match(r"\A---\r?\n(.*?)\r?\n---(?:\r?\n|\Z)", text, re.S)
                if front:
                    match = re.search(r"(?m)^governed_delivery_classification:\s*(.+?)\s*$", front.group(1))
                    if match:
                        classification = _front_matter_scalar(match.group(1))
            decision = recovery_dispatch_decision(state, next_stage, next_profile, plan_classification=classification)
            from result_proofs import validate_dispatch_proofs
            errors.extend(validate_dispatch_proofs(state, virtual_read, next_stage, config=config))
            candidate = json.loads(json.dumps(state))
            candidate["stage"] = next_stage
            candidate["resume"] = None if next_stage == "complete" else {"stage": next_stage, "reason": "Validate proposed progression", "evidence_path": None}
            if next_stage == "complete":
                candidate["checkpoint"] = {"checkpoint_id": "dispatch-preflight", "stage": "complete", "commit_sha": None, "binding_status": "pending"}
            errors.extend(validate_workflow_state(candidate))
            errors.extend(validate_state_plan_content(candidate, root))

            def approved(purpose: str) -> bool:
                from result_proofs import validate_gate_approval
                failures = []
                for approval in state.get("approvals", []):
                    if approval.get("status") != "accepted":
                        continue
                    evidence = virtual_read(approval["evidence_path"])
                    try:
                        value = json.loads(evidence) if evidence else {}
                    except (ValueError, UnicodeError):
                        continue
                    if not isinstance(value, dict):
                        continue
                    if value.get("stage") != purpose:
                        continue
                    try:
                        validate_gate_approval(approval, value, state, virtual_read, purpose)
                    except (ValueError, TypeError, KeyError, AttributeError, UnicodeError) as exc:
                        failures.append(f"{purpose} approval: {exc}")
                        continue
                    return True
                errors.extend(failures)
                return False

            if next_stage in {"implement", "converge", "lookback", "quality-review", "conditional-e2e", "verify", "complete"}:
                if state["gates"]["definition-review"]["status"] != "accepted":
                    errors.append("accepted definition review is required before implementation and later stages")
                if not approved("implementation-approval"):
                    errors.append("current scoped implementation approval is required before production progression")
            # An accepted early plan review also represents a consumer-policy
            # trigger (e.g. a tool/Scan change), not just architecture.
            planned_change = classification == "architecture" or state["gates"]["plan-review"]["status"] == "accepted"
            if planned_change and next_stage in {"tasks", "definition-review", "implementation-approval", "implement"}:
                if state["gates"]["plan-review"]["status"] != "accepted" or not approved("plan-review"):
                    trigger = "architecture" if classification == "architecture" else "planned change"
                    errors.append(f"{trigger} requires accepted plan review and scoped planned-change authorization")
            if next_stage == "converge":
                from workflow_runtime import _task_map
                task_artifact = state.get("artifacts", {}).get("tasks")
                tasks = _task_map(virtual_read(task_artifact["path"]) or b"") if isinstance(task_artifact, dict) else {}
                if not tasks or not all(tasks.values()):
                    errors.append("complete existing tasks before convergence; reuse unfinished work")
            if next_stage in {"lookback", "quality-review", "conditional-e2e", "verify", "complete"} and state["convergence"]["status"] != "accepted":
                errors.append("current clean convergence is required")
            if next_stage in {"conditional-e2e", "verify", "complete"} and state["gates"]["quality-review"]["status"] != "accepted":
                errors.append("current accepted quality review is required")
            if next_stage in {"verify", "complete"} and state["e2e"]["result"] not in {"passed", "skipped-no-risk", "skipped-covered"}:
                errors.append("passing E2E or a current valid skip is required")
            if any(g.get("status") == "blocked" for g in state["gates"].values()) or state["convergence"].get("status") == "blocked":
                decision = {"allowed": False, "action": "blocked", "reason": "an independent blocked gate remains unresolved"}
            if next_stage in {"quality-review", "conditional-e2e", "verify", "complete"}:
                errors.extend(validate_lookback(candidate, virtual_read, config=config,
                              content_lines=_markdown_content_lines, state_path=relative))
            head = read_record(state["transition_head"], virtual_read, "transition_head")
            if next_stage == "complete" and head.get("transition", {}).get("event", {}).get("kind") != "close":
                errors.append("completion requires a final verified closure transition")
            technical = head.get("technical_retry")
            if technical is not None:
                technical_decision = technical_retry_decision(technical)
                if not technical_decision["allowed"]:
                    decision = technical_decision
            if next_profile is not None:
                if next_profile not in ROLE_PROFILES:
                    errors.append("next-profile is not a governed role")
                else:
                    phase_roles = {
                        "context": {"story-context-synthesizer", "delivery-orchestrator"},
                        "specify": {state["definition_author"]["profile"]},
                        "plan": {state["definition_author"]["profile"], "plan-architect-high"},
                        "tasks": {state["definition_author"]["profile"]},
                        "plan-review": {"definition-reviewer"}, "definition-review": {"definition-reviewer"},
                        "implement": {"tdd-implementer", "tdd-test-author"},
                        "converge": {"convergence-analyst"}, "quality-review": {"quality-reviewer"},
                        "conditional-e2e": {"e2e-runner", "delivery-orchestrator"},
                    }
                    if next_profile not in phase_roles.get(next_stage, {"delivery-orchestrator"}):
                        errors.append("selected role does not own the requested phase")
                    role_dir = root / ("agents" if (root / "skills/run-governed-delivery/SKILL.md").is_file() else ".codex/agents")
                    role_path = role_dir / f"{next_profile}.toml"
                    errors.extend(validate_agent_file(role_path))
                    if role_path.is_file():
                        profile = tomllib.loads(role_path.read_text(encoding="utf-8"))
                        expected = ROLE_PROFILES[next_profile]
                        if (profile.get("model"), profile.get("model_reasoning_effort"), profile.get("sandbox_mode")) != expected:
                            errors.append("selected profile differs from the approved model/effort/sandbox")
            response["decision"] = decision
            if not decision["allowed"]:
                errors.append(decision["reason"])
        elif next_profile is not None:
            errors.append("next-profile requires next-stage")
        response["ok"] = not errors
    except (ValueError, TypeError, KeyError, AttributeError, OSError) as exc:
        errors.append(str(exc))
    return response


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--epic-gate", type=Path)
    parser.add_argument("--epic-evidence", type=Path)
    parser.add_argument("--retrospective-state", type=Path)
    parser.add_argument("--story-state", type=Path)
    parser.add_argument("--result", type=Path)
    parser.add_argument("--next-stage")
    parser.add_argument("--next-profile")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    if args.story_state is not None:
        if any((args.epic_gate, args.epic_evidence, args.retrospective_state)):
            parser.error("story-state cannot be combined with epic/retrospective validation")
        result = validate_story_request(args.repo, args.story_state, result_path=args.result,
                                        next_stage=args.next_stage, next_profile=args.next_profile)
        if args.json or args.result is not None:
            print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        else:
            print("Story boundary and requested dispatch valid" if result["ok"] else "\n".join(result["errors"]))
        return 0 if result["ok"] else 1
    if args.result is not None or args.next_stage is not None or args.next_profile is not None:
        parser.error("result/next-stage/next-profile require --story-state")
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
    if args.json:
        print(json.dumps({"ok": not errors, "errors": errors}, ensure_ascii=False))
        return 1 if errors else 0
    if errors:
        print("\n".join(errors))
        return 1
    print("Workflow configuration and contracts valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
