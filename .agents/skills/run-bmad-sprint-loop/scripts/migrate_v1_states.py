from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from validate_workflow import validate_workflow_state


EXPECTED_SOURCES = {
    "specs/001-codegraph-extraction/workflow-state.json": ("fb112c24c0a0bb4a0f02dba7fcdf4c6d76a62760370ca43bb3dccd02ced23c7c", 10),
    "specs/002-language-framework-detection/workflow-state.json": ("41cece90b232e51b8698aa3687437d509b24254131c224b3e4f1c5cc0c49c4c3", 2),
    "specs/003-conservative-pre-scan-gate/workflow-state.json": ("c14787a4881b74c6debb403e9b13ffae99bef918d106b02f81ae3da01bd440fd", 133),
    "specs/004-minimal-knowledge-store/workflow-state.json": ("afbbdaadfa2c91fefc05dfccd951caa8ed0421f072ae819f4b8a8c67a9d88188", 0),
    "specs/005-sequential-scan-runner/workflow-state.json": ("f26245acd4b5791dae747c0f3704d0997b22a31204ab62a6ca659f3f41dc9dd9", 93),
    "specs/006-demo-document-generation/workflow-state.json": ("17b7961a436fde2257361b2044b7c9e4853902cfb9f59f6a977c56fbbe38e893", 38),
    "specs/007-exact-cross-repo-resolution/workflow-state.json": ("ee35661b5a30ad636b53deeddf900f6047b0df92686ae0c0734afb397bc55037", 210),
    "specs/008-demo-chat-release-gate/workflow-state.json": ("3ab3a86a634d0265a501bb1fdcf7e946730fa67f998112b61721335399796e17", 780),
}

COMPLETION_COMMITS = {
    "4-1-codegraph-integration-and-code-unit-extraction": "4a6527d20e86fb13d6812939fae22b58dd886841",
    "4-2-language-and-framework-detection": "1b9c98bb4999e254ab19704643477732f92c842c",
    "4-3-conservative-pre-scan-gate-and-communication-type-detection": "68792e5dbd8a95eaee47d5cc98ef8e4bae0ab928",
    "11-1-minimal-knowledge-store-and-retrieval-projection": "96ba064ca321427d7f8bd5aa914a6026536608b5",
    "11-2-sequential-scan-runner-and-typed-agent-dispatch": "c59171b2e5fb726f0ca40e15dcdeea2749744d01",
    "11-3-pass-1-and-pass-2-demo-document-generation": "67b5eaf45ff3c3348e240e323bed6f80c9e885e0",
    "11-4-exact-rpc-http-cross-repository-demo-resolution": "c07df7d8789e7b975dc7fb6a121dff564e8549ad",
}

REPORT_PATH = "docs/bmad/implementation-artifacts/workflow-migrations/v1-to-v2-2026-08-18.json"
SCRIPT_PATH = ".agents/skills/run-bmad-sprint-loop/scripts/migrate_v1_states.py"


def git(repo: Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(repo), *args], text=True, encoding="utf-8").strip()


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def repo_path(path: Path, repo: Path) -> str:
    return path.relative_to(repo).as_posix()


def sprint_statuses(repo: Path) -> dict[str, str]:
    text = (repo / "docs/bmad/implementation-artifacts/sprint-status.yaml").read_text(encoding="utf-8")
    return dict(re.findall(r"^  ([0-9]+-[0-9]+-[^:]+): (backlog|ready-for-dev|in-progress|review|done)$", text, re.MULTILINE))


def find_status_transition(repo: Path, story: str, head: str) -> str:
    status_path = "docs/bmad/implementation-artifacts/sprint-status.yaml"
    for commit in git(repo, "log", "--all", "--format=%H", "--", status_path).splitlines():
        if subprocess.run(["git", "-C", str(repo), "merge-base", "--is-ancestor", commit, head]).returncode != 0:
            continue
        diff = git(repo, "diff", f"{commit}^", commit, "--", status_path)
        if re.search(rf"^\+  {re.escape(story)}: done$", diff, re.MULTILINE):
            return commit
    raise ValueError(f"{story}: no ancestral commit records the BMad done transition")


def completion_proof(repo: Path, story: str, state: dict[str, Any], status: str, head: str) -> dict[str, Any]:
    commit = COMPLETION_COMMITS.get(story)
    if status != "done" or commit is None:
        raise ValueError(f"{story}: complete migration requires BMad done and a pinned completion commit")
    if subprocess.run(["git", "-C", str(repo), "merge-base", "--is-ancestor", commit, head]).returncode != 0:
        raise ValueError(f"{story}: completion commit is not ancestral to HEAD")
    body = git(repo, "show", "-s", "--format=%B", commit)
    checkpoint_id = state["last_checkpoint"]["checkpoint_id"]
    modern = f"Workflow-Checkpoint: {checkpoint_id}" in body
    legacy = (
        f"BMad-Story: {story}" in body
        and "BMad-Workflow-Stage: complete" in body
        and "BMad-Workflow-Checkpoint: story-complete" in body
    )
    if not modern and not legacy:
        raise ValueError(f"{story}: completion commit has neither modern nor accepted legacy binding trailers")
    changed = git(repo, "diff-tree", "--no-commit-id", "--name-only", "-r", commit).splitlines()
    state_path = state["feature_directory"] + "/workflow-state.json"
    status_path = "docs/bmad/implementation-artifacts/sprint-status.yaml"
    if state_path not in changed:
        raise ValueError(f"{story}: completion commit does not contain the legacy workflow state")
    status_transition_commit = commit if status_path in changed else find_status_transition(repo, story, head)
    return {
        "commit": commit,
        "checkpoint_id": checkpoint_id,
        "binding_format": "modern" if modern else "legacy-bmad-bridge",
        "committed_paths": changed,
        "ancestral_to_migration_head": True,
        "bmad_status": status,
        "bmad_status_transition_commit": status_transition_commit,
        "bmad_status_co_committed": status_transition_commit == commit,
    }


def boundary(head: str, source_path: str, digest: str, owned_paths: list[str]) -> dict[str, Any]:
    return {
        "base_commit": head,
        "candidate_commit": None,
        "diff_sha256": sha256(b""),
        "artifact_hashes": {source_path: digest},
        "owned_paths": owned_paths,
    }


def gate(status: str = "invalidated") -> dict[str, Any]:
    return {
        "status": status,
        "iteration": 0,
        "dispatch_id": None,
        "verdict_path": None,
        "input_boundary": None,
        "completed_at": None,
    }


def migrate_state(
    old: dict[str, Any], digest: str, legacy_errors: int, migration_time: str,
    head: str, branch: str, worktree_status: str, proof: dict[str, Any] | None,
) -> tuple[dict[str, Any], dict[str, Any]]:
    feature = old["feature_directory"]
    story = old["bmad_story_key"]
    archive_path = f"{feature}/workflow-state-v1.json"
    source_path = f"{feature}/workflow-state.json"
    owned = [archive_path, f"{feature}/workflow-state.json", REPORT_PATH, SCRIPT_PATH]
    mapped_stage = "complete" if proof is not None else "converge"
    evidence_id = f"{story}/{mapped_stage}/1"
    source_boundary = boundary(head, source_path, digest, owned)
    summary = (
        "Terminal v1 completion accepted through verified BMad status, ancestral completion commit, and binding bridge."
        if proof is not None
        else "Legacy quality-review state conservatively mapped to converge; no legacy review is treated as v2 acceptance."
    )
    state: dict[str, Any] = {
        "schema_version": "2.0",
        "bmad_story_key": story,
        "bmad_epic_key": old["bmad_epic_key"],
        "feature_directory": feature,
        "execution_level": "unified",
        "stage": mapped_stage,
        "active_gates": {
            "definition-review": gate(),
            "quality-review": gate(),
        },
        "stage_history": [{
            "stage": mapped_stage,
            "status": "accepted" if proof is not None else "active",
            "started_at": migration_time,
            "completed_at": migration_time if proof is not None else None,
            "input_commit": head,
            "input_hashes": {source_path: digest},
            "agent_role": "bmad-sprint-orchestrator",
            "evidence_paths": [REPORT_PATH],
            "failure": None,
        }],
        "repository_preflight": {
            "branch": branch,
            "preflight_head": head,
            "repository_root": ".",
            "git_common_directory": ".git",
            "worktree_path": ".",
            "worktree_status": worktree_status,
            "owned_paths": owned,
            "unrelated_paths": [],
            "observed_at": migration_time,
        },
        "source_artifacts": [],
        "dispatches": [],
        "review_iterations": {
            "plan-review": {"count": 0, "verdict_paths": []},
            "definition-review": {"count": 0, "verdict_paths": []},
            "converge": {"count": 0, "verdict_paths": []},
            "quality-review": {"count": 0, "verdict_paths": []},
        },
        "convergence": {
            "status": "accepted" if proof is not None else "pending",
            "iteration": 0,
            "outcome": "converged" if proof is not None else "pending",
            "findings_count": 0,
            "appended_task_ids": [],
            "artifact_hashes": {archive_path: digest} if proof is not None else {},
            "completed_at": migration_time if proof is not None else None,
        },
        "e2e_decision": {
            "required": False,
            "predicates": [],
            "coverage_override": None,
            "rationale": summary if proof is not None else "Re-evaluate after v2 convergence.",
            "covering_tests": [],
            "owner": "orchestrator",
            "source_boundary": source_boundary,
            "result": "passed" if proof is not None else "pending",
            "evidence_ids": [evidence_id] if proof is not None else [],
        },
        "verification": [{
            "evidence_id": evidence_id,
            "kind": "analysis",
            "stage": mapped_stage,
            "source_commit": head,
            "artifact_paths": [archive_path, REPORT_PATH],
            "command": "python .agents/skills/run-bmad-sprint-loop/scripts/migrate_v1_states.py --repo .",
            "exit_code": 0,
            "observed_at": migration_time,
            "summary": summary,
            "raw_output_path": REPORT_PATH,
        }],
        "last_checkpoint": None,
        "recoverable_failure": None,
        "legacy_migration": {
            "source_state_path": archive_path,
            "source_state_sha256": digest,
            "source_execution_level": old["execution_level"],
            "source_stage": old["stage"],
            "mapped_stage": mapped_stage,
            "decision_evidence_path": REPORT_PATH,
            "migrated_at": migration_time,
        },
        "updated_at": migration_time,
    }
    if proof is not None:
        state["last_checkpoint"] = {
            "checkpoint_id": proof["checkpoint_id"],
            "scope_key": story,
            "stage": "complete",
            "committed_paths": proof["committed_paths"],
            "verification_evidence_ids": [evidence_id],
            "commit_sha": proof["commit"],
            "binding_status": "bound",
        }
    report_entry = {
        "story": story,
        "feature_directory": feature,
        "source_state_path": archive_path,
        "source_sha256": digest,
        "source_execution_level": old["execution_level"],
        "source_stage": old["stage"],
        "archived_v1_validator_error_count": legacy_errors,
        "archived_v1_validator_result": "valid" if legacy_errors == 0 else "legacy-schema-drift-preserved",
        "mapped_stage": mapped_stage,
        "completion_proof": proof,
        "decision": summary,
    }
    return state, report_entry


def main() -> int:
    parser = argparse.ArgumentParser(description="One-time migration of the pinned Vulgata v1 workflow states.")
    parser.add_argument("--repo", default=".")
    args = parser.parse_args()
    repo = Path(args.repo).resolve()
    status_before = git(repo, "status", "--porcelain=v1", "--untracked-files=all")
    preexisting_paths = {
        line[3:].replace("\\", "/") for line in status_before.splitlines() if len(line) > 3
    }
    if preexisting_paths - {SCRIPT_PATH}:
        raise SystemExit("migration requires a clean worktree apart from its own untracked script")
    head = git(repo, "rev-parse", "HEAD")
    branch = git(repo, "branch", "--show-current")
    if not branch:
        raise SystemExit("migration requires a non-detached branch")
    statuses = sprint_statuses(repo)
    migration_time = utc_now()
    outputs: list[tuple[Path, Path, bytes, dict[str, Any]]] = []
    report_entries: list[dict[str, Any]] = []

    for relative, (expected_hash, legacy_errors) in EXPECTED_SOURCES.items():
        source = repo / relative
        raw = source.read_bytes()
        digest = sha256(raw)
        if digest != expected_hash:
            raise SystemExit(f"{relative}: source hash changed; expected {expected_hash}, observed {digest}")
        old = json.loads(raw.decode("utf-8-sig"))
        if old.get("schema_version") != "1.0":
            raise SystemExit(f"{relative}: expected schema 1.0")
        archive = source.with_name("workflow-state-v1.json")
        if archive.exists():
            raise SystemExit(f"{repo_path(archive, repo)} already exists")
        proof = None
        if old["stage"] == "complete":
            proof = completion_proof(repo, old["bmad_story_key"], old, statuses.get(old["bmad_story_key"], ""), head)
        state, report_entry = migrate_state(
            old, digest, legacy_errors, migration_time, head, branch, status_before, proof,
        )
        errors = validate_workflow_state(state)
        if errors:
            raise SystemExit(f"{relative}: generated v2 state invalid: {'; '.join(errors)}")
        outputs.append((source, archive, raw, state))
        report_entries.append(report_entry)

    report = {
        "schema_version": "1.0",
        "migration": "workflow-state-v1-to-v2",
        "migration_head": head,
        "archived_validator_source_commit": "7e411dddc32237c1cb72f88206182f43022a692a",
        "archived_schema_path": ".agents/skills/run-bmad-sprint-loop/references/workflow-state-v1.schema.json",
        "branch": branch,
        "migrated_at": migration_time,
        "source_count": len(report_entries),
        "policy": {
            "original_bytes_preserved": True,
            "legacy_schema_drift_is_not_silently_repaired": True,
            "complete_requires_bmad_done_and_ancestral_binding_commit": True,
            "nonterminal_quality_review_maps_to": "converge",
        },
        "states": report_entries,
    }
    report_file = repo / REPORT_PATH
    report_file.parent.mkdir(parents=True, exist_ok=True)
    report_file.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    for source, archive, raw, state in outputs:
        archive.write_bytes(raw)
        source.write_text(json.dumps(state, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Migrated {len(outputs)} workflow states; report: {REPORT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
