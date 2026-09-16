"""Read-only path and lookback proof checks, usable with files or committed blobs."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Iterable

DEFAULT_PROJECT_LESSONS = "docs/project-lessons.md"
HASH_RE = re.compile(r"[0-9a-fA-F]{64}\Z")
RESERVED_ROOTS = {".git", ".codex", ".agents", ".governed-delivery"}
RESERVED_NAMES = {
    "workflow-state.json", "spec.md", "plan.md", "tasks.md", "config.toml", "agents.md", "readme.md",
    "prd.md", "architecture.md", "constitution.md", "project-context.md", "product-requirements.md",
}
DEFAULT_AUTHORITIES = {
    "repository_instructions": "AGENTS.md", "constitution": ".specify/memory/constitution.md",
    "sprint_status": "docs/bmad/implementation-artifacts/sprint-status.yaml", "epics": "docs/bmad/epics.md",
    "prd": "docs/bmad/prd.md", "architecture": "docs/bmad/architecture.md",
}
ReadBytes = Callable[[str], bytes | None]
ContentLines = Callable[[str], list[tuple[int, str]]]


def _relative(value: Any) -> str:
    if (not isinstance(value, str) or not value or "\\" in value
            or any(ord(char) < 32 for char in value) or ":" in value):
        raise ValueError(f"unsafe repository-relative path: {value!r}")
    parts = value.split("/")
    if any(part in {"", ".", ".."} or part.rstrip(" .") != part for part in parts):
        raise ValueError(f"unsafe repository-relative path: {value!r}")
    if PurePosixPath(value).is_absolute():
        raise ValueError(f"unsafe repository-relative path: {value!r}")
    return value


def _lesson_setting(config: Any) -> tuple[str, dict[str, Any]]:
    if config is None:
        config = {}
    if not isinstance(config, dict) or not isinstance(config.get("paths", {}), dict):
        raise ValueError("config paths must be a table")
    paths = config.get("paths", {})
    return _relative(paths.get("project_lessons", DEFAULT_PROJECT_LESSONS)), paths


def _protected_lesson_paths(paths: dict[str, Any], additional: Iterable[str]) -> set[str]:
    protected = set(DEFAULT_AUTHORITIES.values()) | {
        ".governed-delivery/config.toml", ".codex/config.toml", "README.md", "docs/architecture.md", "docs/prd.md",
    }
    protected.update(value for key, value in paths.items()
                     if key not in {"project_lessons", "feature_root", "implementation_artifacts"}
                     and isinstance(value, str))
    protected.update(additional)
    return protected


def _check_lesson_name(relative: str, protected: Iterable[str]) -> None:
    parts = PurePosixPath(relative).parts
    folded = relative.casefold()
    if PurePosixPath(relative).suffix.casefold() not in {".md", ".markdown"}:
        raise ValueError("paths.project_lessons must target a dedicated Markdown file (.md or .markdown)")
    if (parts[0].casefold() in RESERVED_ROOTS
            or {"evidence", "adr", "adrs", "architecture-decisions"} & {part.casefold() for part in parts}
            or parts[-1].casefold() in RESERVED_NAMES
            or folded in {path.casefold() for path in protected}):
        raise ValueError("paths.project_lessons collides with workflow or canonical authority content")


def validate_project_lessons_path(
    repo: Path, config: Any, *, protected_paths: Iterable[str] = (),
) -> tuple[str | None, list[str]]:
    """Allow missing lessons; fail before a writer can target an alias or authority."""
    try:
        relative, paths = _lesson_setting(config)
        protected = _protected_lesson_paths(paths, protected_paths)
        _check_lesson_name(relative, protected)
        root = repo.resolve()
        candidate = root / relative
        resolved = candidate.resolve()
        resolved.relative_to(root)
        cursor = candidate
        while cursor != root:
            if cursor.is_symlink() or (hasattr(cursor, "is_junction") and cursor.is_junction()):
                raise ValueError("paths.project_lessons cannot use a symlink or junction alias")
            cursor = cursor.parent
        if candidate.exists():
            if not candidate.is_file():
                raise ValueError("paths.project_lessons must target a file, not a directory")
            if candidate.stat().st_nlink > 1:
                raise ValueError("paths.project_lessons cannot use a hardlink alias")
            for item in protected:
                authority = root / _relative(item)
                if authority.exists() and candidate.samefile(authority):
                    raise ValueError("paths.project_lessons aliases canonical authority content")
            candidate.read_text(encoding="utf-8")
        elif candidate.parent.exists() and not candidate.parent.is_dir():
            raise ValueError("paths.project_lessons parent is not a directory")
        return relative, []
    except (OSError, ValueError, UnicodeError) as error:
        return None, [f"invalid paths.project_lessons: {error}"]


def _content_lines(text: str) -> list[tuple[int, str]]:
    """Same fence/comment/indented-code exclusions as the plan heading parser."""
    result: list[tuple[int, str]] = []
    fence: tuple[str, int] | None = None
    comment = False
    for number, raw in enumerate(text.splitlines(), 1):
        if fence:
            if re.fullmatch(rf" {{0,3}}{re.escape(fence[0])}{{{fence[1]},}}[ \t]*", raw):
                fence = None
            continue
        opening = re.match(r"^ {0,3}(`{3,}|~{3,})(.*)$", raw) if not comment else None
        if opening and not (opening[1][0] == "`" and "`" in opening[2]):
            fence = opening[1][0], len(opening[1])
            continue
        visible = ""
        while raw:
            marker = raw.find("-->" if comment else "<!--")
            if marker < 0:
                if not comment:
                    visible += raw
                break
            if not comment:
                visible += raw[:marker]
            raw = raw[marker + (3 if comment else 4):]
            comment = not comment
        if not visible.startswith(("    ", "\t")):
            result.append((number, visible.rstrip()))
    return result


def _body_lines(text: str, content_lines: ContentLines | None = None) -> list[tuple[int, str]]:
    raw_lines = text.splitlines(keepends=True)
    metadata_end = 0
    if raw_lines and raw_lines[0].strip() == "---":
        metadata_end = next((i + 1 for i, line in enumerate(raw_lines[1:], 1)
                             if line.strip() == "---"), len(raw_lines))
    return [(number, line) for number, line in (content_lines or _content_lines)(text) if number > metadata_end]


def _headings(lines: list[tuple[int, str]]) -> list[tuple[int, int, str]]:
    headings: list[tuple[int, int, str]] = []
    previous: tuple[int, str] | None = None
    for number, line in lines:
        match = re.fullmatch(r" {0,3}(#{1,6})(?:[ \t]+(.*?))?[ \t]*", line)
        if match:
            title = re.sub(r"[ \t]+#+[ \t]*$", "", match[2] or "").strip()
            headings.append((number, len(match[1]), title))
        elif (previous and previous[0] == number - 1 and previous[1].strip()
              and re.fullmatch(r" {0,3}(?:=+|-+)[ \t]*", line)):
            headings.append((previous[0], 1 if "=" in line else 2, ""))
        previous = number, line
    return headings


def _section_end(start: int, headings: list[tuple[int, int, str]], length: int) -> int:
    return next((number for number, level, _ in headings if number > start and level <= 2), length + 1)


def record_content_projection(text: str) -> bytes:
    """Preserve story bytes except root-reserved status and Workflow Bookkeeping.

    Hash this projection as record_content_sha256. Fenced, commented, nested or
    indented examples are content, and arbitrary checkpoint sections stay bound.
    """
    raw_lines = text.splitlines(keepends=True)
    visible = _body_lines(text)
    headings = _headings(visible)
    starts = [number for number, level, title in headings if level == 2 and title == "Workflow Bookkeeping"]
    if len(starts) > 1:
        raise ValueError("expected at most one real ## Workflow Bookkeeping section")
    excluded = set()
    if starts:
        excluded.update(range(starts[0], _section_end(starts[0], headings, len(raw_lines))))
    for number, visible_line in visible:
        raw_line = raw_lines[number - 1].rstrip("\r\n")
        if (visible_line == raw_line.rstrip()
                and re.fullmatch(r"Status:[ \t]+(?:backlog|ready-for-dev|in-progress|review|done)[ \t]*", raw_line)):
            excluded.add(number)
    return "".join(line for number, line in enumerate(raw_lines, 1) if number not in excluded).encode("utf-8")


def extract_lookback_section(text: str, *, content_lines: ContentLines | None = None) -> str:
    """Return the exact section bytes as text, independent of surrounding metadata."""
    raw_lines = text.splitlines(keepends=True)
    headings = _headings(_body_lines(text, content_lines))
    targets = [number for number, level, title in headings if level == 2 and title == "Story Lookback"]
    if len(targets) != 1:
        raise ValueError(f"expected exactly one real ## Story Lookback section; found {len(targets)}")
    start = targets[0]
    end = _section_end(start, headings, len(raw_lines))
    section = "".join(raw_lines[start - 1:end - 1])
    if not any(line.strip() for _, line in (content_lines or _content_lines)(section)[1:]):
        raise ValueError("Story Lookback section must contain a disposition, not an empty heading")
    return section


def validate_lookback(
    state: Any, read_bytes: ReadBytes, *, config: Any = None,
    content_lines: ContentLines | None = None, state_path: str | None = None,
) -> list[str]:
    """Check current proof; the caller supplies current files or one completion commit.

    The reader must enforce containment/file type and return None for missing blobs.
    It must not mix committed and working-tree content. This proves recorded bindings,
    not completeness of a model's source manifest or semantic lesson admission.
    """
    if not isinstance(state, dict) or state.get("schema_version") != "3.1":
        return []
    lookback = state.get("lookback")
    required = state.get("stage") in {"quality-review", "conditional-e2e", "verify", "complete"}
    if not isinstance(lookback, dict) or lookback.get("status") != "current":
        return ["current Story Lookback proof is required before quality review/completion"] if required else []
    errors: list[str] = []
    proof_path: str | None = None
    blocked = {state_path} if isinstance(state_path, str) else set()

    def read(path: Any, field: str, *, exclude: Iterable[str] = ()) -> bytes | None:
        try:
            path = _relative(path)
            if (path.casefold() in {item.casefold() for item in exclude if isinstance(item, str)}
                    | {item.casefold() for item in blocked}
                    or PurePosixPath(path).name.casefold() == "workflow-state.json"
                    or PurePosixPath(path).parts[0].casefold() == ".git"):
                raise ValueError("self-referential or workflow-state bookkeeping input")
            content = read_bytes(path)
            if content is None:
                raise ValueError("referenced file is missing")
            if not isinstance(content, bytes):
                raise ValueError("reader must return bytes")
            return content
        except (OSError, ValueError) as error:
            errors.append(f"lookback {field}: {error}")
            return None

    def bound(descriptor: Any, field: str, *, exclude: Iterable[str] = ()) -> bytes | None:
        if not isinstance(descriptor, dict) or set(descriptor) != {"path", "sha256"}:
            errors.append(f"lookback {field} must contain path and sha256")
            return None
        digest = descriptor.get("sha256")
        if not isinstance(digest, str) or not HASH_RE.fullmatch(digest):
            errors.append(f"lookback {field}.sha256 must be a SHA-256")
            return None
        content = read(descriptor.get("path"), field, exclude=exclude)
        if content is not None and hashlib.sha256(content).hexdigest() != digest.lower():
            errors.append(f"lookback {field} hash is stale")
            return None
        return content

    def document(content: bytes | None, field: str) -> dict[str, Any]:
        if content is None:
            return {}
        try:
            value = json.loads(content)
            if not isinstance(value, dict):
                raise ValueError("expected a JSON object")
            return value
        except (ValueError, UnicodeError) as error:
            errors.append(f"lookback {field}: {error}")
            return {}

    proof = document(bound(lookback.get("evidence"), "evidence"), "evidence")
    if not proof:
        return errors
    proof_path = lookback["evidence"]["path"]
    if proof.get("kind") != "analysis" or proof.get("stage") != "lookback":
        errors.append("lookback proof must be ordinary kind analysis, stage lookback evidence")
    if proof.get("disposition") not in ("no-new-lesson", "updated"):
        errors.append("lookback disposition must be no-new-lesson or updated")
    story = state.get("bmad_story_key")
    if (not isinstance(story, str) or not isinstance(proof.get("evidence_id"), str)
            or not re.fullmatch(rf"{re.escape(story)}/lookback/[1-9][0-9]*", proof["evidence_id"])):
        errors.append("lookback evidence_id must identify this story and a positive lookback result")
    if proof.get("unresolved_blockers") != []:
        errors.append("lookback cannot be current with unresolved blockers or authority items")
    record_path = proof.get("record_path")
    record = read(record_path, "record_path", exclude=[proof_path])
    if record is not None:
        try:
            text = record.decode("utf-8")
            section = extract_lookback_section(text, content_lines=content_lines)
            if hashlib.sha256(section.encode("utf-8")).hexdigest() != proof.get("section_sha256"):
                errors.append("lookback Story Lookback section hash is stale")
            if hashlib.sha256(record_content_projection(text)).hexdigest() != proof.get("record_content_sha256"):
                errors.append("lookback story record content hash is missing or stale")
        except (ValueError, UnicodeError) as error:
            errors.append(f"lookback record: {error}")
    convergence_ref = proof.get("convergence")
    convergence = state.get("convergence", {})
    if (not isinstance(convergence, dict) or convergence.get("status") != "accepted"
            or convergence.get("outcome") != "converged" or not isinstance(convergence_ref, dict)
            or convergence_ref.get("path") != convergence.get("report_path")):
        errors.append("lookback must bind the current accepted clean convergence report")
    result = document(bound(convergence_ref, "convergence", exclude=[proof_path, record_path]), "convergence")
    result = result.get("raw_result", result)
    if not isinstance(result, dict):
        errors.append("lookback convergence raw_result must be an object")
        result = {}
    if result.get("outcome") != "converged":
        errors.append("lookback convergence evidence is not converged")
    inputs = result.get("input_hashes")
    outputs = proof.get("output_hashes")
    excluded = [proof_path, record_path, convergence_ref.get("path") if isinstance(convergence_ref, dict) else None]
    for field, mapping in (("input_hashes", inputs), ("output_hashes", outputs)):
        if not isinstance(mapping, dict) or (field == "input_hashes" and not mapping):
            errors.append(f"lookback {field} must be a {'nonempty ' if field == 'input_hashes' else ''}path/hash object")
            continue
        for path, digest in mapping.items():
            bound({"path": path, "sha256": digest}, field, exclude=excluded)
    if isinstance(inputs, dict):
        artifacts = state.get("artifacts", {})
        for name in ("spec", "plan", "tasks"):
            artifact = artifacts.get(name) if isinstance(artifacts, dict) else None
            if (not isinstance(artifact, dict) or not isinstance(artifact.get("path"), str)
                    or inputs.get(artifact["path"]) != artifact.get("sha256")):
                errors.append(f"lookback convergence input_hashes must bind current {name}")
    try:
        lessons, paths = _lesson_setting(config)
        _check_lesson_name(lessons, _protected_lesson_paths(paths, [item for item in excluded if isinstance(item, str)]))
        lesson_bytes = read_bytes(lessons)
        if lesson_bytes is not None and (not isinstance(outputs, dict) or lessons not in outputs):
            errors.append("lookback output_hashes must bind the existing project lessons file")
    except (OSError, ValueError) as error:
        errors.append(f"lookback paths.project_lessons: {error}")
    if proof.get("disposition") == "updated" and not outputs:
        errors.append("lookback updated disposition must bind at least one output")
    decisions = proof.get("authority_decisions")
    if not isinstance(decisions, list):
        errors.append("lookback authority_decisions must be an array of accepted evidence descriptors")
    else:
        approvals = state.get("approvals", [])
        accepted = {item["evidence_path"] for item in approvals if isinstance(item, dict)
                    and item.get("status") == "accepted" and isinstance(item.get("evidence_path"), str)
                    } if isinstance(approvals, list) else set()
        for decision in decisions:
            raw = bound(decision, "authority_decisions", exclude=excluded)
            if (not isinstance(decision, dict) or not isinstance(decision.get("path"), str)
                    or decision["path"] not in accepted):
                errors.append("lookback authority decision must refer to an accepted state approval")
            if raw is not None:
                decision_doc = document(raw, "authority decision")
                if decision_doc.get("status") != "accepted":
                    errors.append("lookback authority decision evidence is not accepted")
    followups = proof.get("follow_up_paths")
    if not isinstance(followups, list):
        errors.append("lookback follow_up_paths must be an array")
    else:
        for path in followups:
            read(path, "follow_up_paths", exclude=excluded)
    return errors
