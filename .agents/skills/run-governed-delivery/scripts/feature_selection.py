"""Read-only story-to-feature selection; root still owns activation and mapping.

Run normal dependency/preflight validation first. This helper neither initializes
SpecKit nor changes its templates, hooks, selector, branch, or workflow state.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import tomllib


STORY = re.compile(r"([1-9][0-9]*)-([1-9][0-9]*)-[a-z0-9]+(?:-[a-z0-9]+)*\Z")


def _plain(path: Path) -> Path:
    # Do not follow symlinks/junctions when deciding whether a name is free.
    if path.is_symlink() or path.resolve() != path:
        raise ValueError(f"aliased feature-selection path is not supported: {path}")
    return path


def _number(key: str):
    match = re.match(r"([0-9]+)-([0-9]+)(?:-|$)", key)
    return tuple(int(part) for part in match.groups()) if match else None


def _override(repo: Path) -> str:
    config = tomllib.loads(_plain(repo / ".governed-delivery/config.toml").read_text(encoding="utf-8"))
    definition = config.get("definition")
    if not isinstance(definition, dict) or not isinstance(definition.get("adapter"), str):
        raise ValueError("configuration must select a definition adapter")
    selected = definition["adapter"]
    adapters = _plain(repo / ".governed-delivery/adapters/speckit")
    matches = []
    for path in adapters.glob("*/adapter.toml"):
        adapter = tomllib.loads(_plain(path).read_text(encoding="utf-8"))
        if adapter.get("id") == selected and adapter.get("capability") == "definition":
            matches.append(adapter)
    activation = matches[0].get("activation") if len(matches) == 1 else None
    if not isinstance(activation, dict) or activation.get("feature_directory_override") != "SPECIFY_FEATURE_DIRECTORY":
        raise ValueError("new story-named features require a supported explicit-directory adapter (SpecKit 1.0.6); existing mappings may resume")
    return "SPECIFY_FEATURE_DIRECTORY"


def select_feature(repo: Path, story_key: str) -> dict:
    """Return the existing mapping or a collision-free story-derived proposal."""
    if STORY.fullmatch(story_key) is None:
        raise ValueError("story key must be the canonical <epic>-<story>-<lowercase-slug> key")
    number = _number(story_key)
    root = repo.resolve(strict=True)
    specs = _plain(root / "specs")
    mappings, collisions = [], []
    if specs.exists():
        if not specs.is_dir():
            raise ValueError("specs must be a directory")
        for feature in specs.iterdir():
            _plain(feature)
            relative = feature.relative_to(root).as_posix()
            if _number(feature.name.lower()) == number:
                collisions.append(relative)
            if not feature.is_dir():
                continue
            state_path = _plain(feature / "workflow-state.json")
            if not state_path.exists():
                continue
            state = json.loads(state_path.read_text(encoding="utf-8-sig"))
            if (not isinstance(state, dict) or not isinstance(state.get("bmad_story_key"), str)
                    or not state["bmad_story_key"] or state.get("feature_directory") != relative):
                raise ValueError(f"invalid story/feature mapping: {state_path}")
            key = state["bmad_story_key"]
            if key == story_key:
                mappings.append(relative)
            elif _number(key) == number:
                raise ValueError(f"story number already mapped under a different key: {key}")
    if len(mappings) > 1:
        raise ValueError("duplicate story/feature mappings; root must reconcile before activation")
    if mappings:
        if any(path != mappings[0] for path in collisions):
            raise ValueError("story-number directory conflicts with the existing mapping")
        return {"mode": "resume", "story_key": story_key, "feature_directory": mappings[0]}
    if collisions:
        raise ValueError("story-number directory already exists without a matching mapping; do not overwrite or increment")
    override = _override(root)
    relative = f"specs/{story_key}"
    return {"mode": "create", "story_key": story_key, "feature_directory": relative,
            "override": {override: relative}, "spec_file": f"{relative}/spec.md"}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--story-key", required=True)
    args = parser.parse_args()
    try:
        result = select_feature(args.repo, args.story_key)
    except (OSError, ValueError) as error:
        print(json.dumps({"ok": False, "error": str(error)}))
        return 1
    print(json.dumps({"ok": True, **result}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
