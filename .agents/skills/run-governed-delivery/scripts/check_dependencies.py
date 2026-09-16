from __future__ import annotations

import argparse
import importlib.util
import json
import re
import tomllib
from pathlib import Path, PurePosixPath
from typing import Any


RETIRED_AGENT_PROFILES = ("spec-author.toml", "plan-architect.toml", "task-author.toml")


def _check_project_lessons(target: Path, config: dict, errors: list[str]) -> None:
    helper = Path(__file__).resolve().with_name("story_learning.py")
    if not helper.is_file():
        helper = Path(__file__).resolve().parents[1] / "skills/run-governed-delivery/scripts/story_learning.py"
    if not helper.is_file():
        errors.append("missing governed-delivery story learning validator")
        return
    spec = importlib.util.spec_from_file_location("governed_story_learning", helper)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    _, path_errors = module.validate_project_lessons_path(target, config)
    errors.extend(path_errors)


def _load_toml(path: Path) -> dict:
    with path.open("rb") as stream:
        return tomllib.load(stream)


def _safe_repo_path(target: Path, relative: object, field: str, errors: list[str]) -> Path | None:
    if not isinstance(relative, str) or not relative or "\\" in relative or "\x00" in relative:
        errors.append(f"unsafe {field}: {relative}")
        return None
    pure = PurePosixPath(relative)
    if pure.is_absolute() or ".." in pure.parts or re.match(r"^[A-Za-z]:", relative):
        errors.append(f"unsafe {field}: {relative}")
        return None
    root = target.resolve()
    candidate = (root / Path(*pure.parts)).resolve()
    try:
        candidate.relative_to(root)
    except ValueError:
        errors.append(f"unsafe {field}: {relative}")
        return None
    return candidate


def _safe_skill_id(name: object, field: str, errors: list[str]) -> str | None:
    if not isinstance(name, str) or re.fullmatch(r"[a-z0-9][a-z0-9-]{0,63}", name) is None:
        errors.append(f"unsafe {field}: {name}")
        return None
    return name


def _yaml_selected_scalar(text: str, selector: object) -> str | None:
    if not isinstance(selector, str):
        return None
    parts = selector.split(".")
    if len(parts) != 2 or any(re.fullmatch(r"[A-Za-z0-9_-]+", part) is None for part in parts):
        return None
    section, key = parts
    in_section = False
    direct_indent: int | None = None
    values: list[str] = []
    for line in text.splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if not line[0].isspace():
            in_section = line.strip() == f"{section}:"
            direct_indent = None
            continue
        if in_section:
            indent = len(line) - len(line.lstrip())
            if direct_indent is None:
                direct_indent = indent
            if indent != direct_indent:
                continue
            match = re.fullmatch(rf"\s{{{direct_indent}}}{re.escape(key)}:\s*['\"]?([^'\"#\s]+)['\"]?\s*(?:#.*)?", line)
            if match:
                values.append(match.group(1))
    return values[0] if len(values) == 1 else None


def _adapter_catalog(adapter_root: Path, errors: list[str]) -> dict[str, dict[str, Any]]:
    catalog: dict[str, dict[str, Any]] = {}
    if not adapter_root.is_dir():
        errors.append(f"adapter root is missing: {adapter_root}")
        return catalog
    for path in adapter_root.rglob("adapter.toml"):
        try:
            adapter = _load_toml(path)
        except (OSError, tomllib.TOMLDecodeError) as error:
            errors.append(f"invalid adapter manifest {path}: {error}")
            continue
        adapter_id = adapter.get("id") if isinstance(adapter, dict) else None
        if not isinstance(adapter_id, str) or not adapter_id:
            errors.append(f"adapter manifest has no id: {path}")
        elif adapter_id in catalog:
            errors.append(f"duplicate adapter id: {adapter_id}")
        else:
            catalog[adapter_id] = adapter
    return catalog


def _selected_adapter(
    catalog: dict[str, dict[str, Any]], adapter_id: object, capability: str, errors: list[str]
) -> dict[str, Any] | None:
    if not isinstance(adapter_id, str) or adapter_id not in catalog:
        errors.append(f"unsupported {capability} adapter: {adapter_id}")
        return None
    adapter = catalog[adapter_id]
    if adapter.get("capability") != capability:
        errors.append(f"adapter {adapter_id} does not provide {capability}")
        return None
    return adapter


def _validate_upstream_version(target: Path, adapter: dict[str, Any], errors: list[str]) -> None:
    upstream = adapter.get("upstream")
    if not isinstance(upstream, dict):
        errors.append(f"adapter {adapter.get('id')} upstream must be a table")
        return
    relative = upstream.get("version_path")
    source = upstream.get("version_source")
    path = _safe_repo_path(target, relative, f"{adapter.get('id')} version_path", errors)
    if path is None:
        return
    accepted_versions = upstream.get("accepted_versions")
    version_prefix = upstream.get("version_prefix")
    if accepted_versions is not None and (
        not isinstance(accepted_versions, list)
        or not accepted_versions
        or not all(isinstance(item, str) and item for item in accepted_versions)
    ):
        errors.append(f"adapter {adapter.get('id')} has an invalid version contract")
        return
    if accepted_versions is None and (not isinstance(version_prefix, str) or not version_prefix):
        errors.append(f"adapter {adapter.get('id')} has an invalid version contract")
        return
    if not path.is_file():
        errors.append(f"missing version source for {adapter.get('id')}: {relative}")
        return
    try:
        if source == "json-key":
            document = json.loads(path.read_text(encoding="utf-8"))
            key = upstream.get("version_key")
            version = document.get(key) if isinstance(document, dict) and isinstance(key, str) else None
        elif source == "yaml-selector":
            version = _yaml_selected_scalar(path.read_text(encoding="utf-8"), upstream.get("version_selector"))
        else:
            errors.append(f"adapter {adapter.get('id')} has unsupported version_source: {source}")
            return
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"invalid version source {relative}: {error}")
        return
    semantic_version = isinstance(version, str) and re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version) is not None
    accepted = semantic_version and (
        (isinstance(accepted_versions, list) and version in accepted_versions)
        or (accepted_versions is None and isinstance(version_prefix, str) and version.startswith(version_prefix))
    )
    if not accepted:
        errors.append(f"adapter {adapter.get('id')} does not accept installed version: {version}")


def _validate_adapter_files_and_skills(
    target: Path, adapter: dict[str, Any], extra_roots: list[Path], errors: list[str]
) -> None:
    required_files = adapter.get("required_files", [])
    if not isinstance(required_files, list) or not all(isinstance(item, str) for item in required_files):
        errors.append(f"adapter {adapter.get('id')} required_files must be a string array")
    else:
        for relative in required_files:
            path = _safe_repo_path(target, relative, f"{adapter.get('id')} required_files", errors)
            if path is not None and not path.is_file():
                errors.append(f"missing {adapter.get('capability')} file: {relative}")

    required_skills = adapter.get("required_skills", [])
    if not isinstance(required_skills, list) or not all(isinstance(item, str) for item in required_skills):
        errors.append(f"adapter {adapter.get('id')} required_skills must be a string array")
        required_skills = []
    markers = adapter.get("skill_markers", {})
    if not isinstance(markers, dict):
        errors.append(f"adapter {adapter.get('id')} skill_markers must be a table")
        markers = {}
    roots = [target / ".agents" / "skills", *extra_roots]
    target_skill_root = (target / ".agents" / "skills").resolve()
    for name in required_skills:
        safe_name = _safe_skill_id(name, f"{adapter.get('id')} required_skills", errors)
        if safe_name is None:
            continue
        skill_path = None
        for root in roots:
            candidate = root / safe_name / "SKILL.md"
            if not candidate.is_file():
                continue
            if root == target / ".agents" / "skills":
                try:
                    candidate.resolve().relative_to(target_skill_root)
                except ValueError:
                    errors.append(f"unsafe target skill path for {safe_name}")
                    continue
            skill_path = candidate
            break
        if skill_path is None:
            errors.append(f"missing discoverable {adapter.get('capability')} skill: {name}")
            continue
        expected_markers = markers.get(name, [])
        if not isinstance(expected_markers, list) or not all(isinstance(item, str) for item in expected_markers):
            errors.append(f"adapter {adapter.get('id')} markers for {name} must be a string array")
            continue
        body = skill_path.read_text(encoding="utf-8").lower()
        for marker in expected_markers:
            if marker.lower() not in body:
                errors.append(f"{skill_path}: missing adapter marker {marker}")


def _validate_definition_scripts(target: Path, config: dict[str, Any], adapter: dict[str, Any], errors: list[str]) -> None:
    script_files = adapter.get("script_files")
    if script_files is None:
        return
    if not isinstance(script_files, dict):
        errors.append(f"adapter {adapter.get('id')} script_files must be a table")
        return
    init_options_path = target / ".specify" / "init-options.json"
    try:
        init_options = json.loads(init_options_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"invalid .specify/init-options.json: {error}")
        return
    definition = config.get("definition", {})
    requested = definition.get("script") if isinstance(definition, dict) else None
    selected = init_options.get("script") if isinstance(init_options, dict) else None
    aliases = {"powershell": "ps", "bash": "sh", "python": "py"}
    if not isinstance(selected, str):
        errors.append(".specify/init-options.json script must be a string")
        return
    selected = aliases.get(selected, selected)
    if requested is not None:
        if not isinstance(requested, str):
            errors.append("config definition.script must be a string")
        else:
            requested = aliases.get(requested, requested)
            if requested != selected:
                errors.append("config definition.script does not match .specify/init-options.json")
    required = script_files.get(selected)
    if not isinstance(required, list) or not all(isinstance(item, str) for item in required):
        errors.append(f"adapter {adapter.get('id')} does not support script type: {selected}")
        return
    for relative in required:
        path = _safe_repo_path(target, relative, f"{adapter.get('id')} script_files.{selected}", errors)
        if path is not None and not path.is_file():
            errors.append(f"missing selected SpecKit script: {relative}")


def inspect_target(target: Path, extra_roots: list[Path], adapter_root: Path | None = None) -> dict:
    config_path = target / ".governed-delivery" / "config.toml"
    errors: list[str] = []
    warnings: list[str] = []

    if not target.is_dir():
        return {"target": str(target), "ok": False, "errors": ["target directory is missing"], "warnings": []}
    if not (target / ".git").exists():
        errors.append("target is not a Git repository or worktree")

    for name in RETIRED_AGENT_PROFILES:
        if (target / ".codex" / "agents" / name).is_file():
            errors.append(
                f"retired governed-delivery agent profile is still installed: .codex/agents/{name}; "
                "rerun the workflow installer with -Force"
            )

    try:
        config = _load_toml(config_path)
    except FileNotFoundError:
        errors.append(f"missing {config_path}")
        config = {}
    except (OSError, tomllib.TOMLDecodeError) as error:
        errors.append(f"invalid {config_path}: {error}")
        config = {}

    _check_project_lessons(target, config, errors)

    lifecycle = config.get("lifecycle", {}) if isinstance(config, dict) else {}
    if not isinstance(lifecycle, dict):
        errors.append("config lifecycle must be a table")
        lifecycle = {}
    definition = config.get("definition", {}) if isinstance(config, dict) else {}
    if not isinstance(definition, dict):
        errors.append("config definition must be a table")
        definition = {}
    if adapter_root is None:
        installed_adapters = target / ".governed-delivery" / "adapters"
        adapter_root = installed_adapters if installed_adapters.is_dir() else Path(__file__).resolve().parents[1] / "adapters"
    catalog = _adapter_catalog(adapter_root, errors)
    lifecycle_adapter = _selected_adapter(catalog, lifecycle.get("adapter"), "lifecycle", errors)
    definition_adapter = _selected_adapter(catalog, definition.get("adapter"), "definition", errors)
    for adapter in (lifecycle_adapter, definition_adapter):
        if adapter is not None:
            _validate_upstream_version(target, adapter, errors)
            _validate_adapter_files_and_skills(target, adapter, extra_roots, errors)
    if definition_adapter is not None:
        _validate_definition_scripts(target, config, definition_adapter, errors)

    architecture = config.get("architecture_review", {}) if isinstance(config, dict) else {}
    if not isinstance(architecture, dict):
        errors.append("config architecture_review must be a table")
        architecture = {}
    if architecture.get("enabled") is True:
        architecture_adapter = _selected_adapter(
            catalog, architecture.get("adapter"), "architecture-review", errors
        )
        if architecture_adapter is not None:
            _validate_adapter_files_and_skills(target, architecture_adapter, extra_roots, errors)
    else:
        warnings.append("epic architecture review is disabled")

    codex_config = target / ".codex" / "config.toml"
    try:
        agents = _load_toml(codex_config).get("agents", {})
        if not isinstance(agents, dict):
            errors.append(".codex/config.toml agents must be a table")
            agents = {}
        if agents.get("max_depth") != 1:
            errors.append(".codex/config.toml must set agents.max_depth = 1")
        if not isinstance(agents.get("max_threads"), int) or agents["max_threads"] < 4:
            errors.append(".codex/config.toml must set agents.max_threads >= 4")
    except FileNotFoundError:
        errors.append("missing .codex/config.toml; merge the generated fragment")
    except (OSError, tomllib.TOMLDecodeError) as error:
        errors.append(f"invalid .codex/config.toml: {error}")

    commands = config.get("commands", {}) if isinstance(config, dict) else {}
    if not isinstance(commands, dict):
        errors.append("config commands must be a table")
        commands = {}
    for name in ("build", "test"):
        if not isinstance(commands.get(name), str) or not commands[name].strip():
            errors.append(f"configure commands.{name} before activation")

    return {"target": str(target.resolve()), "ok": not errors, "errors": errors, "warnings": warnings}


def main() -> int:
    parser = argparse.ArgumentParser(description="Check a target repository for governed-delivery dependencies.")
    parser.add_argument("--target", type=Path, required=True)
    parser.add_argument("--skills-root", type=Path, action="append", default=[])
    parser.add_argument("--adapter-root", type=Path)
    args = parser.parse_args()
    result = inspect_target(args.target, args.skills_root, args.adapter_root)
    print(json.dumps(result, indent=2))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
