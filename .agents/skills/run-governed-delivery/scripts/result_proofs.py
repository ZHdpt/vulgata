"""Authenticate ordinary accepted-result and closure evidence through a reader.

This checks declared boundaries and observed output, not semantic scope completeness
or whether a recorded command was truthfully executed. The root still determines
owned paths, required focused checks, and whether a definition change needs review.
"""
from __future__ import annotations

import hashlib
import re
from collections.abc import Mapping
from typing import Callable

Reader = Callable[[str], bytes | None]
SHA = re.compile(r"[0-9a-fA-F]{40}\Z")
HASH = re.compile(r"[0-9a-fA-F]{64}\Z")
TIMESTAMP = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z\Z")
DELEGATED_GATES = {"plan-review": "plan-review", "implementation-approval": "definition-review"}
DELEGATED_PURPOSES = {*DELEGATED_GATES, "recovery-extension"}


def is_delegated_decision(record):
    """Recognize delegation even if its kind was incorrectly relabeled human."""
    def marked(value):
        return isinstance(value, Mapping) and (
            value.get("kind") in ("delegated-approval", "user-delegation")
            or "delegate_id" in value or "delegation" in value
        )
    return marked(record) or (isinstance(record, Mapping) and marked(record.get("raw_result")))


def require_nondelegated_decision(record):
    if is_delegated_decision(record):
        raise ValueError("delegated approval cannot resolve holds or authorize this purpose")


def _delegation(pointer, record, state, read, purpose):
    """Shared evidence checks; the runner still verifies real messages and scope."""
    _object(record, "delegated approval evidence")
    if (record.get("kind") != "delegated-approval" or purpose not in DELEGATED_PURPOSES
            or record.get("stage") != purpose or record.get("status") != "accepted"
            or pointer.get("status") != "accepted"
            or record.get("decision_id") != pointer.get("decision_id")
            or record.get("scope_sha256") != pointer.get("scope_sha256")):
        raise ValueError("delegated approval must match its accepted pointer, purpose and scope")
    delegate = _text(record.get("delegate_id"), "delegate_id")
    _text(record.get("response_source"), "delegate response source")
    _text(record.get("response_text"), "delegate response text")
    grant = _record(record.get("delegation"), read, "actual user delegation")
    if grant.get("kind") != "user-delegation" or grant.get("status") != "accepted":
        raise ValueError("delegated approval requires an accepted actual user delegation")
    story = state.get("bmad_story_key")
    if grant.get("delegate_id") != delegate or grant.get("story_key") != story or record.get("story_key") != story:
        raise ValueError("delegated approval must match the designated delegate and current story")
    gates = grant.get("gates")
    if (not isinstance(gates, list) or not gates
            or any(not isinstance(gate, str) or gate not in DELEGATED_PURPOSES for gate in gates)
            or len(set(gates)) != len(gates) or purpose not in gates):
        raise ValueError("user delegation must explicitly cover this gate and no unsupported gate")
    for field in ("user_source", "user_instruction", "scope"):
        _text(grant.get(field), f"user delegation {field}")
    if "recovery-extension" in gates and (type(grant.get("max_pause_at")) is not int or grant["max_pause_at"] <= 4):
        raise ValueError("recovery delegation requires a user-set finite max_pause_at above 4")
    return grant


def validate_gate_approval(pointer, record, state, read, purpose):
    """Check recorded delegation/boundary consistency, not message authenticity.

    Human records retain their existing contract. Root alone verifies the actual
    user instruction, response source, semantic scope and current conditions.
    """
    from workflow_runtime import canonical_bytes, digest
    _object(record, "approval evidence")
    if (pointer.get("status") != "accepted" or record.get("status") != "accepted"
            or record.get("stage") != purpose
            or record.get("decision_id") != pointer.get("decision_id")
            or record.get("scope_sha256") != pointer.get("scope_sha256")):
        raise ValueError("approval must match its accepted pointer, purpose and scope")
    if not is_delegated_decision(record):
        return
    if record.get("kind") != "delegated-approval" or purpose not in DELEGATED_GATES:
        raise ValueError("this review-gate check permits only planned-change and implementation authorization; recovery requires an extension transition")
    _delegation(pointer, record, state, read, purpose)
    review = record.get("review")
    # The existing review proof binds all accepted artifact hashes. Reuse its
    # normal post-implementation allowance for task checkboxes/convergence work.
    recovery = state.get("recovery")
    has_implementation = state.get("stage") in {
        "implement", "converge", "lookback", "quality-review", "conditional-e2e", "verify", "complete"
    } or (isinstance(recovery, Mapping) and recovery.get("closing_gate") == "verify")
    _review(DELEGATED_GATES[purpose], review, state, read, historical_context=has_implementation)
    expected = digest(canonical_bytes({"story_key": state.get("bmad_story_key"), "stage": purpose, "review": review}))
    if record.get("scope_sha256") != expected:
        raise ValueError("delegated approval scope must bind this story, gate and accepted review boundary")


def validate_recovery_extension(pointer, record, state, read, *, current=True, expected_head=None):
    """Permit only an explicitly delegated, finite extension of this paused episode.

    Replay checks the recorded dispatch boundary, not later corrected file bytes.
    Human extension records retain the existing recovery-policy contract.
    """
    from workflow_runtime import canonical_bytes, digest, reference
    if not is_delegated_decision(record):
        return
    grant = _delegation(pointer, record, state, read, "recovery-extension")
    recovery = _object(state.get("recovery"), "active recovery")
    boundary = _object(record.get("recovery_boundary"), "recovery boundary")
    expected = {key: recovery.get(key) for key in ("episode_id", "failed_results", "pause_at", "last_result")}
    expected["transition_head"] = expected_head or state.get("transition_head")
    if (set(boundary) != {*expected, "artifacts"}
            or any(boundary.get(key) != value for key, value in expected.items())
            or record.get("episode_id") != recovery.get("episode_id")):
        raise ValueError("delegated extension must bind the current episode, count, limit and failure boundary")
    count, old_limit, new_limit = recovery.get("failed_results"), recovery.get("pause_at"), record.get("pause_at")
    if (type(count) is not int or type(old_limit) is not int or count < old_limit
            or type(new_limit) is not int or new_limit <= max(count, old_limit)
            or new_limit > grant["max_pause_at"]):
        raise ValueError("delegated extension requires a paused episode and a higher limit within the user's max_pause_at")
    _record(boundary["last_result"], read, "latest recovery failure")
    artifacts = _object(boundary.get("artifacts"), "recovery boundary artifacts")
    if current and artifacts != state.get("artifacts"):
        raise ValueError("delegated extension artifacts differ from the current boundary")
    for artifact in artifacts.values():
        if not reference(artifact):
            raise ValueError("delegated extension requires valid hashed artifact references")
        if current:
            content = read(artifact["path"])
            if content is None or digest(content) != artifact["sha256"]:
                raise ValueError("delegated extension artifact is missing or stale")
    approach = _text(record.get("changed_approach"), "changed recovery approach")
    scope = digest(canonical_bytes({"story_key": state.get("bmad_story_key"), "stage": "recovery-extension",
                                   "recovery_boundary": boundary, "pause_at": new_limit, "changed_approach": approach}))
    if record.get("scope_sha256") != scope:
        raise ValueError("delegated extension scope must bind the recovery boundary, new limit and changed approach")


def _object(value, label):
    if not isinstance(value, Mapping):
        raise ValueError(f"{label} must be an object")
    return value


def _text(value, label):
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{label} requires nonempty text")
    return value


def _record(ref, read, label):
    # Lazy imports preserve both installed and standalone validator layouts.
    from workflow_runtime import read_record
    return read_record(ref, read, label)


def _raw(record):
    return _object(record.get("raw_result", record), "raw result")


def _paths(value, label, *, nonempty=True):
    from workflow_runtime import safe_path
    if not isinstance(value, list) or (nonempty and not value) or any(not safe_path(path) for path in value):
        raise ValueError(f"{label} requires repository-relative paths")
    if len(value) != len(set(value)):
        raise ValueError(f"{label} contains duplicate paths")
    return set(value)


def _lookback_binding(state, read):
    """Authenticate the one record projection through its accepted quality proof.

    No other lifecycle/authority file receives a whole-file-hash exception. The
    pinned proof supplies both substantive-record and exact-lookback-section
    hashes; its quality boundary must also include every maintained output.
    """
    from story_learning import extract_lookback_section, record_content_projection
    from validate_workflow import validate_verdict
    from workflow_runtime import safe_path
    lookback = _object(state.get("lookback"), "current lookback")
    if lookback.get("status") != "current":
        raise ValueError("quality boundary requires a current lookback")
    ref = lookback.get("evidence")
    proof = _record(ref, read, "lookback")
    if proof.get("kind") != "analysis" or proof.get("stage") != "lookback":
        raise ValueError("lookback must be ordinary analysis evidence")
    record_path = proof.get("record_path")
    if not safe_path(record_path):
        raise ValueError("lookback record_path must be repository-relative")
    content = read(record_path)
    if content is None:
        raise ValueError("lookback record is missing")
    text = content.decode("utf-8")
    if hashlib.sha256(record_content_projection(text)).hexdigest() != proof.get("record_content_sha256"):
        raise ValueError("lookback substantive record content changed after review")
    if hashlib.sha256(extract_lookback_section(text).encode("utf-8")).hexdigest() != proof.get("section_sha256"):
        raise ValueError("Story Lookback section changed after review")
    outputs = _object(proof.get("output_hashes"), "lookback.output_hashes")
    for path, expected in outputs.items():
        if not safe_path(path) or not isinstance(expected, str) or HASH.fullmatch(expected) is None:
            raise ValueError("lookback output has an invalid path or SHA-256")
        output = read(path)
        if output is None or hashlib.sha256(output).hexdigest().lower() != expected.lower():
            raise ValueError(f"lookback output changed after review: {path}")
    gate = _object(state.get("gates", {}).get("quality-review"), "quality-review gate")
    path = gate.get("verdict_path")
    if gate.get("status") != "accepted" or not safe_path(path):
        raise ValueError("record projection requires the current accepted quality review")
    quality_bytes = read(path)
    if quality_bytes is None:
        raise ValueError("current quality review is missing")
    quality = _raw(_record({"path": path, "sha256": hashlib.sha256(quality_bytes).hexdigest()}, read, "quality-review"))
    errors = validate_verdict(quality)
    if errors or quality.get("gate") != "quality-review" or quality.get("verdict") != "ACCEPT":
        raise ValueError("record projection requires a valid independent quality ACCEPT")
    boundary = quality["reviewed_boundary"]
    hashes = boundary["artifact_hashes"]
    required = {ref["path"], record_path, *outputs}
    if not required.issubset(hashes) or not required.issubset(boundary["owned_paths"]):
        raise ValueError("quality review must include the lookback proof, record, and every maintained output")
    if hashes.get(ref["path"]) != ref["sha256"] or any(hashes.get(path) != expected for path, expected in outputs.items()):
        raise ValueError("quality review must pin the current lookback proof and output hashes")
    return record_path


def _manifest(record, state, read, *, names=("spec", "plan", "tasks"), historical_paths=(), required_paths=()):
    from workflow_runtime import safe_path
    boundary = record.get("reviewed_boundary", record)
    boundary = _object(boundary, "input boundary")
    hashes = boundary.get("artifact_hashes") if "reviewed_boundary" in record else boundary.get("input_hashes")
    hashes = _object(hashes, "input_hashes or reviewed_boundary.artifact_hashes")
    if not hashes:
        raise ValueError("accepted boundary requires nonempty input hashes")
    owned = _paths(boundary.get("owned_paths"), "owned_paths")
    if not owned.issubset(hashes) or not set(required_paths).issubset(hashes):
        raise ValueError("boundary hashes must cover every owned and required path")
    for path, expected in hashes.items():
        if not safe_path(path) or not isinstance(expected, str) or HASH.fullmatch(expected) is None:
            raise ValueError("boundary contains an invalid path or SHA-256")
        if path in historical_paths:
            continue
        content = read(path)
        if content is None or hashlib.sha256(content).hexdigest().lower() != expected.lower():
            lookback = state.get("lookback")
            if content is not None and isinstance(lookback, Mapping) and lookback.get("status") == "current" and _lookback_binding(state, read) == path:
                continue
            raise ValueError(f"accepted boundary input is missing or stale: {path}")
    artifacts = _object(state.get("artifacts"), "state.artifacts")
    for name in names:
        artifact = _object(artifacts.get(name), f"artifacts.{name}")
        if hashes.get(artifact.get("path")) != artifact.get("sha256"):
            raise ValueError(f"accepted boundary does not cover current artifacts.{name}")
    work_kind = record.get("work_kind")
    if work_kind is not None:
        if work_kind not in ("behavioral", "test-only", "nonbehavioral"):
            raise ValueError("work_kind must be behavioral, test-only, or nonbehavioral")
        required = ("production_paths", "test_paths") if work_kind == "behavioral" else ("test_paths",) if work_kind == "test-only" else ()
        for field in required:
            if not _paths(record.get(field), field).issubset(owned):
                raise ValueError(f"{field} must be included in owned_paths and the hashed boundary")
    return hashes, owned


def _plan_classification(state, read):
    from validate_workflow import _front_matter_scalar, validate_plan_implementation_design
    artifact = _object(state.get("artifacts", {}).get("plan"), "artifacts.plan")
    content = read(artifact.get("path"))
    if content is None:
        raise ValueError("plan classification requires current plan content")
    text = content.decode("utf-8-sig")
    errors = validate_plan_implementation_design(text)
    if errors:
        raise ValueError("plan classification is invalid: " + "; ".join(errors))
    lines = text.splitlines()
    if not lines or lines[0].strip() != "---":
        return "none"
    for line in lines[1:]:
        if line.strip() == "---":
            break
        if line.startswith("governed_delivery_classification:"):
            return _front_matter_scalar(line.split(":", 1)[1])
    return "none"


def _review(name, ref, state, read, *, historical_tasks=False, historical_context=False):
    from validate_workflow import validate_verdict
    raw = _raw(_record(ref, read, name))
    gate = _object(state.get("gates", {}).get(name), f"current {name} gate")
    if gate.get("status") != "accepted" or gate.get("verdict_path") != ref.get("path"):
        raise ValueError(f"{name} must be the current accepted gate")
    errors = validate_verdict(raw)
    if errors:
        raise ValueError(f"{name} raw verdict is invalid: " + "; ".join(errors))
    if raw.get("gate") != name or raw.get("verdict") != "ACCEPT":
        raise ValueError(f"{name} must contain its independent ACCEPT")
    if name == "quality-review":
        _lookback_binding(state, read)
    names = ("spec", "plan") if name == "plan-review" or historical_tasks or historical_context else ("spec", "plan", "tasks")
    # Definition review predates mechanical checkbox completion and convergence
    # appends. Preserve that raw task hash; current tasks are checked downstream.
    task_artifact = state.get("artifacts", {}).get("tasks")
    historical = [task_artifact["path"]] if (historical_tasks or name == "plan-review") and isinstance(task_artifact, Mapping) else []
    if historical_context:
        # The accepted definition can cite the preimplementation source baseline.
        # Current spec/plan remain binding; current tasks/code are proved by the
        # later convergence/quality/verification boundary, not this old context.
        definition_paths = {state["artifacts"][name]["path"] for name in names}
        historical = set(raw["reviewed_boundary"]["artifact_hashes"]) - definition_paths
    _manifest(raw, state, read, names=names, historical_paths=historical)
    return raw


def _plan_review(ref, state, read, *, historical_context=False):
    raw = _raw(_record(ref, read, "plan-review"))
    if raw.get("disposition") != "not-required":
        return _review("plan-review", ref, state, read, historical_context=historical_context)
    _text(raw.get("reason"), "plan-review exemption reason")
    if _plan_classification(state, read) == "architecture":
        raise ValueError("architecture classification requires an independent plan review")
    gate = _object(state.get("gates", {}).get("plan-review"), "plan-review gate")
    if gate.get("status") not in ("pending", "invalidated"):
        raise ValueError("plan-review exemption conflicts with the existing gate disposition")
    historical = ()
    if historical_context:
        definition_paths = {state["artifacts"][name]["path"] for name in ("spec", "plan")}
        historical = set(_object(raw.get("input_hashes"), "plan-review exemption inputs")) - definition_paths
    _manifest(raw, state, read, names=("spec", "plan"), historical_paths=historical)
    return raw


def _convergence(ref, state, read):
    raw = _raw(_record(ref, read, "converge"))
    current = _object(state.get("convergence"), "current convergence")
    if current.get("status") != "accepted" or current.get("outcome") != "converged" or current.get("report_path") != ref.get("path") or raw.get("outcome") != "converged":
        raise ValueError("closure requires the current clean convergence result")
    if raw.get("reused_task_ids") or raw.get("appended_task_ids") or raw.get("unresolved_blockers"):
        raise ValueError("converged proof cannot contain unresolved corrective work")
    _manifest(raw, state, read)
    return raw


def _command(ref, state, read, *, stage, expected_command=None, source_commit=None, required_paths=()):
    from workflow_runtime import safe_path
    raw = _raw(_record(ref, read, stage))
    if raw.get("kind") != "command" or raw.get("stage") != stage:
        raise ValueError(f"{stage} requires an observed command record")
    if type(raw.get("exit_code")) is not int or raw["exit_code"] != 0:
        raise ValueError(f"{stage} requires integer exit_code 0")
    command = _text(raw.get("command"), f"{stage}.command")
    if expected_command is not None and command != expected_command:
        raise ValueError(f"{stage} command differs from the configured required command")
    source = raw.get("source_commit")
    if not isinstance(source, str) or SHA.fullmatch(source) is None or (source_commit is not None and source != source_commit):
        raise ValueError(f"{stage} must identify the reviewed source commit")
    observed = raw.get("observed_at")
    if not isinstance(observed, str) or TIMESTAMP.fullmatch(observed) is None:
        raise ValueError(f"{stage} requires an observed_at UTC timestamp")
    path, expected = raw.get("raw_output_path"), raw.get("raw_output_sha256")
    if not safe_path(path) or not isinstance(expected, str) or HASH.fullmatch(expected) is None:
        raise ValueError(f"{stage} requires a raw output path and SHA-256")
    output = read(path)
    if output is None or hashlib.sha256(output).hexdigest().lower() != expected.lower():
        raise ValueError(f"{stage} raw command output is missing or stale")
    _manifest(raw, state, read, required_paths=required_paths)
    return raw


def _e2e(ref, state, read, *, required_paths=(), source_commit=None):
    raw = _raw(_record(ref, read, "e2e"))
    current = _object(state.get("e2e"), "current E2E disposition")
    if current.get("decision_path") != ref.get("path") or current.get("result") != raw.get("result"):
        raise ValueError("E2E proof must match its current decision")
    if raw.get("stage") != "conditional-e2e" or raw.get("result") not in ("passed", "skipped-no-risk", "skipped-covered"):
        raise ValueError("E2E proof must be a passing or justified skip disposition")
    _manifest(raw, state, read, required_paths=required_paths)
    if raw["result"] == "passed":
        commands = raw.get("commands")
        if not isinstance(commands, list) or not commands:
            raise ValueError("passed E2E requires actual command evidence")
        for command in commands:
            _command(command, state, read, stage="conditional-e2e", source_commit=source_commit, required_paths=required_paths)
    else:
        _text(raw.get("reason"), "E2E skip reason")
    return raw


def _verification(ref, state, read, config, quality):
    raw = _raw(_record(ref, read, "verify"))
    if raw.get("kind") != "analysis" or raw.get("stage") != "verify" or raw.get("outcome") != "passed":
        raise ValueError("verify requires a passed aggregate of observed verification commands")
    configured = _object(_object(config, "verification config").get("commands"), "configured commands")
    expected = {name: _text(configured.get(name), f"configured {name} command") for name in ("build", "test")}
    commands = _object(raw.get("commands"), "verify.commands")
    if set(commands) != {"build", "test", "focused"}:
        raise ValueError("verify.commands must include build, test, and focused disposition")
    required_paths = quality["reviewed_boundary"]["owned_paths"]
    source = quality["reviewed_commit"]
    if raw.get("source_commit") != source:
        raise ValueError("verification aggregate must bind the quality-reviewed source commit")
    _manifest(raw, state, read, required_paths=required_paths)
    for name in ("build", "test"):
        _command(commands[name], state, read, stage=f"verify-{name}", expected_command=expected[name], source_commit=source, required_paths=required_paths)
    focused = commands.get("focused")
    if not isinstance(focused, list):
        raise ValueError("verify focused commands must be a list")
    if not focused:
        _text(raw.get("focused_reason"), "verify empty focused disposition reason")
    for ref in focused:
        _command(ref, state, read, stage="verify-focused", source_commit=source, required_paths=required_paths)
    return raw


def validate_closure(event, state, read: Reader, config=None) -> list[str]:
    """Require explicit current proof refs, preserving historical definition tasks.

    Preproduction requires plan-review and definition-review. Postproduction and
    happy-path final closure also require converge, quality-review, e2e, and verify.
    A plan-review ref may point to a reasoned not-required disposition. Final verify
    is an ordinary analysis record aggregating configured build/test and focused
    command evidence; no additional tool invocation is implied by this contract.
    """
    try:
        _object(event, "closure event")
        _object(state, "state")
        proofs = _object(event.get("proofs"), "closure.proofs")
        recovery = state.get("recovery")
        preproduction = isinstance(recovery, Mapping) and recovery.get("closing_gate") == "definition-review"
        required = {"plan-review", "definition-review"}
        if not preproduction:
            required |= {"converge", "quality-review", "e2e", "verify"}
        if not required.issubset(proofs):
            raise ValueError("closure is missing required proofs: " + ", ".join(sorted(required - set(proofs))))
        _plan_review(proofs["plan-review"], state, read, historical_context=not preproduction)
        _review("definition-review", proofs["definition-review"], state, read, historical_context=not preproduction)
        if not preproduction:
            _convergence(proofs["converge"], state, read)
            quality = _review("quality-review", proofs["quality-review"], state, read)
            _e2e(proofs["e2e"], state, read, required_paths=quality["reviewed_boundary"]["owned_paths"], source_commit=quality["reviewed_commit"])
            _verification(proofs["verify"], state, read, config, quality)
        return []
    except (ValueError, TypeError, KeyError, AttributeError, UnicodeError, OSError) as exc:
        return [f"closure proof: {exc}"]


def validate_dispatch_proofs(state, read: Reader, next_stage, config=None) -> list[str]:
    """Authenticate current boundaries separately from immutable historical heads.

    The caller retains stage/status/approval routing. This guard proves the raw
    accepted artifacts needed for the requested forward dispatch, without making
    old transition receipts depend on today's mutable source files.
    """
    from workflow_runtime import safe_path

    def current(path, label):
        content = read(path) if safe_path(path) else None
        if content is None:
            raise ValueError(f"current {label} evidence is missing")
        return {"path": path, "sha256": hashlib.sha256(content).hexdigest()}

    production = {"implement", "converge", "lookback", "quality-review", "conditional-e2e", "verify", "complete"}
    try:
        _object(state, "state")
        if next_stage not in production:
            return []
        gates = _object(state.get("gates"), "current gates")
        definition = _object(gates.get("definition-review"), "definition-review gate")
        recovery = state.get("recovery")
        has_implementation = state.get("stage") in production or (isinstance(recovery, Mapping) and recovery.get("closing_gate") == "verify")
        _review("definition-review", current(definition.get("verdict_path"), "definition-review"), state, read, historical_context=has_implementation)
        if next_stage in {"lookback", "quality-review", "conditional-e2e", "verify", "complete"}:
            convergence = _object(state.get("convergence"), "current convergence")
            _convergence(current(convergence.get("report_path"), "convergence"), state, read)
        quality = None
        if next_stage in {"conditional-e2e", "verify", "complete"}:
            gate = _object(gates.get("quality-review"), "quality-review gate")
            quality = _review("quality-review", current(gate.get("verdict_path"), "quality-review"), state, read)
        if next_stage in {"verify", "complete"}:
            e2e = _object(state.get("e2e"), "current E2E disposition")
            _e2e(current(e2e.get("decision_path"), "E2E"), state, read,
                 required_paths=quality["reviewed_boundary"]["owned_paths"], source_commit=quality["reviewed_commit"])
        return []
    except (ValueError, TypeError, KeyError, AttributeError, UnicodeError, OSError) as exc:
        return [f"dispatch proof: {exc}"]


def validate_result(event, state, read: Reader, config=None, expected_head=None) -> list[str]:
    """Authenticate accepted events; other normalized event types remain upstream.

    Root envelopes keep observed_after beside unchanged raw_result. Replay uses
    expected_head; current proposals default to state.transition_head. A renamed
    REVISE or an old ACCEPT cannot grant forward progression.
    """
    try:
        _object(event, "event")
        if event.get("kind") == "close":
            return validate_closure(event, state, read, config)
        if event.get("kind") != "accepted":
            return []
        ref = event.get("result")
        envelope = _record(ref, read, "accepted result")
        if envelope.get("observed_after") != (expected_head if expected_head is not None else state.get("transition_head")):
            raise ValueError("accepted result must be observed after the current dispatch head")
        raw = _raw(envelope)
        recovery = state.get("recovery")
        definition_pending = isinstance(recovery, Mapping) and (recovery.get("action") == "correct" or state.get("gates", {}).get("definition-review", {}).get("status") != "accepted")
        if definition_pending and recovery.get("earliest_owner") in ("spec", "plan", "tasks") and not event.get("resolved_holds"):
            gates = ("definition-review",) if recovery["earliest_owner"] == "tasks" else ("plan-review", "definition-review")
            if raw.get("verdict") != "ACCEPT" or raw.get("gate") not in gates:
                raise ValueError("definition-owned recovery advances through author phase_complete or its relevant independent review, not an unrelated passing check")
        if raw.get("verdict") == "ACCEPT":
            gate = raw.get("gate")
            if gate not in ("plan-review", "definition-review", "quality-review"):
                raise ValueError("accepted review has no authoritative gate")
            _review(gate, ref, state, read)
        elif raw.get("outcome") == "converged":
            _convergence(ref, state, read)
        elif raw.get("stage") == "verify" and raw.get("outcome") == "passed":
            from workflow_runtime import safe_path
            path = state.get("gates", {}).get("quality-review", {}).get("verdict_path")
            content = read(path) if safe_path(path) else None
            if content is None:
                raise ValueError("verification acceptance requires current quality-review evidence")
            quality_ref = {"path": path, "sha256": hashlib.sha256(content).hexdigest()}
            quality = _review("quality-review", quality_ref, state, read)
            _verification(ref, state, read, config, quality)
        elif raw.get("stage") == "conditional-e2e" and raw.get("result") in ("passed", "skipped-no-risk", "skipped-covered"):
            _e2e(ref, state, read)
        elif raw.get("kind") == "command":
            stage = raw.get("stage")
            if stage not in ("verify-build", "verify-test", "verify-focused", "conditional-e2e"):
                raise ValueError("accepted command must be a required verification check")
            expected = None
            if stage in ("verify-build", "verify-test"):
                commands = _object(_object(config, "verification config").get("commands"), "configured commands")
                expected = _text(commands.get(stage.removeprefix("verify-")), "configured verification command")
            _command(ref, state, read, stage=stage, expected_command=expected)
        elif raw.get("status") in ("accepted", "resolved") and event.get("resolved_holds"):
            _text(raw.get("summary"), "accepted resolution summary")
            # Named hold/authority resolution authenticity is checked upstream.
        else:
            raise ValueError("accepted event requires a real ACCEPT, clean convergence, passing command, or named resolution")
        return []
    except (ValueError, TypeError, KeyError, AttributeError, UnicodeError, OSError) as exc:
        return [f"accepted result: {exc}"]
