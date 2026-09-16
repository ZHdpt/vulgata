"""Read-only story recovery/evidence integration, shared by both install layouts.

Semantic diagnoses remain root/independent-review judgments. This module checks
their durable bindings and deterministic control decisions; it never writes.
"""
from __future__ import annotations

import copy
import hashlib
import json
import re
from pathlib import Path
from typing import Any, Callable

HASH = re.compile(r"^[0-9a-f]{64}$")
CURRENT_FIELDS = {"recovery", "transition_head", "definition_author", "lookback"}
LATE_STAGES = {"quality-review", "conditional-e2e", "verify", "complete"}
PROFILE_NAMES = {"definition-author", "definition-author-recovery", "definition-author-recovery-high"}
Reader = Callable[[str], bytes | None]


def canonical_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def digest(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def safe_path(value: Any) -> bool:
    return (isinstance(value, str) and bool(value) and "\\" not in value
            and not value.startswith("/") and ":" not in value
            and all(part not in {"", ".", ".."} for part in value.split("/")))


def reference(value: Any) -> bool:
    return (isinstance(value, dict) and set(value) == {"path", "sha256"}
            and safe_path(value.get("path")) and isinstance(value.get("sha256"), str)
            and HASH.fullmatch(value["sha256"]) is not None)


def file_reader(repo: Path) -> Reader:
    root = repo.resolve()

    def read(relative: str) -> bytes | None:
        if not safe_path(relative):
            return None
        try:
            path = (root / relative).resolve()
            path.relative_to(root)
            return path.read_bytes() if path.is_file() else None
        except (OSError, ValueError):
            return None
    return read


def read_record(ref: Any, read: Reader, label: str) -> dict:
    if not reference(ref):
        raise ValueError(f"{label} requires a repository-relative path and SHA-256")
    raw = read(ref["path"])
    if raw is None or digest(raw) != ref["sha256"]:
        raise ValueError(f"{label} is missing or its hash is stale: {ref['path']}")
    try:
        data = json.loads(raw)
    except (ValueError, UnicodeError) as exc:
        raise ValueError(f"{label} must contain readable JSON: {exc}") from exc
    if not isinstance(data, dict):
        raise ValueError(f"{label} must contain an object")
    return data


def _task_map(raw: bytes) -> dict[str, bool]:
    from story_learning import _content_lines
    tasks: dict[str, bool] = {}
    for _, line in _content_lines(raw.decode("utf-8")):
        match = re.match(r"^ {0,3}-\s+\[([ xX])\]\s+(T\d{3,})\b\s+\S", line)
        if match:
            if match[2] in tasks:
                raise ValueError(f"duplicate task ID: {match[2]}")
            tasks[match[2]] = match[1].lower() == "x"
    return tasks


def validate_convergence_record(record: dict, state: dict, read: Reader) -> list[str]:
    from decision_policy import validate_convergence_tasks
    try:
        before_ref, after_ref = record.get("tasks_before"), record.get("tasks_after")
        if not reference(before_ref) or not reference(after_ref):
            raise ValueError("convergence requires hashed tasks_before preimage and tasks_after")
        if before_ref["path"] == after_ref["path"]:
            raise ValueError("convergence preimage must be an immutable evidence copy, not the live tasks path")
        if after_ref != state.get("artifacts", {}).get("tasks"):
            raise ValueError("convergence tasks_after must match the mapped current tasks")
        before, after = read(before_ref["path"]), read(after_ref["path"])
        if before is None or after is None or digest(before) != before_ref["sha256"] or digest(after) != after_ref["sha256"]:
            raise ValueError("convergence task preimage/current bytes are missing or stale")
        result = {**record, "disposition": record.get("outcome")}
        errors = validate_convergence_tasks(before, after, _task_map(before), _task_map(after), result)
        if record.get("outcome") == "converged":
            _hashes_match(record, state, read)
        return errors
    except (ValueError, TypeError, KeyError, UnicodeError) as exc:
        return [f"convergence: {exc}"]


def projection(state: dict) -> dict:
    return {name: copy.deepcopy(state.get(name)) for name in ("recovery", "definition_author")}


def _technical_projection(record: dict, previous: dict | None, read: Reader) -> dict | None:
    from decision_policy import advance_technical_retry
    prior = previous.get("technical_retry") if previous else None
    event = record.get("technical_event")
    if event is None:
        return copy.deepcopy(prior)
    if not isinstance(event, dict):
        raise ValueError("technical_event must be an object")
    read_record(event.get("result"), read, "technical invocation/result")
    if event.get("kind") == "external_repair":
        repair = read_record(event.get("repair_evidence"), read, "external repair")
        if repair.get("status") != "resolved" or not repair.get("summary"):
            raise ValueError("external repair requires independently recorded resolved evidence")
    return advance_technical_retry(prior, event)


def validate_current_state(state: dict, legacy_validator: Callable[[Any], list[str]]) -> list[str]:
    from decision_policy import validate_recovery_projection

    legacy = copy.deepcopy(state)
    for name in CURRENT_FIELDS:
        legacy.pop(name, None)
    errors = [f"{name} is required in state 3.1" for name in CURRENT_FIELDS if name not in state]
    if "attempts" in state:
        errors.append("attempts is not allowed in state 3.1; use recovery.failed_results")
    legacy["schema_version"] = "3.0"
    legacy["attempts"] = {"definition-review": 0, "converge": 0, "quality-review": 0}
    for container in (legacy, legacy.get("resume"), legacy.get("checkpoint")):
        if isinstance(container, dict) and container.get("stage") == "lookback":
            container["stage"] = "quality-review"
    convergence = legacy.get("convergence")
    if isinstance(convergence, dict) and convergence.get("outcome") in {"tasks_reused", "return_to_definition"}:
        convergence["outcome"] = "tasks_appended"
    errors.extend(legacy_validator(legacy))
    if len(canonical_bytes(state).rstrip(b"\n")) > 12288:
        errors.append("workflow state canonical JSON exceeds 12288 bytes")
    errors.extend(validate_recovery_projection(state))
    if not reference(state.get("transition_head")):
        errors.append("transition_head requires a path/hash even after recovery closes")
    lookback = state.get("lookback")
    if not isinstance(lookback, dict) or set(lookback) != {"status", "evidence"}:
        errors.append("lookback requires exactly status and evidence")
    else:
        if lookback["status"] not in ("pending", "current", "invalidated"):
            errors.append("lookback.status is invalid")
        if lookback["evidence"] is not None and not reference(lookback["evidence"]):
            errors.append("lookback.evidence is invalid")
        if lookback["status"] == "current" and not reference(lookback["evidence"]):
            errors.append("current lookback requires evidence")
    if state.get("stage") == "complete":
        if state.get("recovery") is not None:
            errors.append("complete state cannot contain unresolved recovery")
        if not isinstance(lookback, dict) or lookback.get("status") != "current":
            errors.append("complete state requires current lookback")
        for name in ("definition-review", "quality-review"):
            if state.get("gates", {}).get(name, {}).get("status") != "accepted":
                errors.append(f"complete state requires accepted {name}")
        if state.get("convergence", {}).get("status") != "accepted" or state.get("convergence", {}).get("outcome") != "converged":
            errors.append("complete state requires clean convergence")
        if state.get("e2e", {}).get("result") not in ("passed", "skipped-no-risk", "skipped-covered"):
            errors.append("complete state requires a passing or validly skipped E2E decision")
    return errors


def _transition(record: dict, story: str) -> dict:
    transition = record.get("transition")
    fields = {"story_key", "sequence", "previous_head", "event", "projection"}
    if record.get("kind") != "analysis" or not isinstance(transition, dict) or set(transition) != fields:
        raise ValueError("transition evidence requires kind analysis and the exact transition fields")
    if transition["story_key"] != story:
        raise ValueError("transition belongs to another story")
    if type(transition["sequence"]) is not int or transition["sequence"] < 1:
        raise ValueError("transition.sequence must be a positive integer")
    if not isinstance(transition["event"], dict) or not isinstance(transition["projection"], dict):
        raise ValueError("transition event/projection must be objects")
    if set(transition["projection"]) != {"recovery", "definition_author"}:
        raise ValueError("transition projection must bind recovery and definition_author")
    return transition


def _hashes_match(data: dict, state: dict, read: Reader, *, names=("spec", "plan", "tasks")) -> None:
    hashes = data.get("input_hashes")
    if hashes is None and isinstance(data.get("reviewed_boundary"), dict):
        hashes = data["reviewed_boundary"].get("artifact_hashes")
    if not isinstance(hashes, dict) or not hashes:
        raise ValueError("accepted proof requires a nonempty input_hashes or reviewed boundary manifest")
    for path, expected in hashes.items():
        raw = read(path) if safe_path(path) else None
        if raw is None or digest(raw) != expected:
            raise ValueError(f"accepted proof input is missing or stale: {path}")
    for name in names:
        artifact = state.get("artifacts", {}).get(name)
        if isinstance(artifact, dict) and hashes.get(artifact.get("path")) != artifact.get("sha256"):
            raise ValueError("accepted proof does not cover the current definition artifacts")




def validate_event_evidence(event: dict, state: dict, read: Reader, *, current: bool = True,
                            expected_head: dict | None = None, config: dict | None = None) -> None:
    from result_proofs import validate_result
    proof_errors = validate_result(event, state, read, config=config, expected_head=expected_head) if current else []
    if proof_errors:
        raise ValueError("; ".join(proof_errors))
    kind = event.get("kind")
    if reference(event.get("result")):
        result = read_record(event["result"], read, "result")
        if kind in ("failure", "accepted", "phase_complete"):
            if result.get("observed_after") != (expected_head or state.get("transition_head")):
                raise ValueError("result was not observed after its dispatch head (stale raw result)")
        result = result.get("raw_result", result)
        if not isinstance(result, dict):
            raise ValueError("raw result must be an object")
        if kind == "failure" and not (result.get("verdict") == "REVISE"
                or result.get("outcome") in ("tasks_appended", "tasks_reused", "return_to_definition")
                or (type(result.get("exit_code")) is int and result["exit_code"] != 0)):
            raise ValueError("failure event must reference an observed negative result")
        if not current and kind == "accepted":
            _historical_accept(result)
        if current and result.get("outcome") in ("tasks_appended", "tasks_reused", "return_to_definition", "converged"):
            convergence_errors = validate_convergence_record(result, state, read)
            if convergence_errors:
                raise ValueError("; ".join(convergence_errors))
            if result["outcome"] == "return_to_definition" and result.get("owner") not in event.get("owners", []):
                raise ValueError("upstream convergence owner must be preserved in the recovery event")
    elif kind not in ("activation", "adoption"):
        raise ValueError("event requires raw result evidence")
    repair = event.get("repair")
    if isinstance(repair, dict):
        read_record(repair.get("correction"), read, "correction")
    if event.get("resolution") is not None:
        resolved = read_record(event["resolution"], read, "blocker resolution")
        if resolved.get("status") not in ("accepted", "resolved") or not resolved.get("summary"):
            raise ValueError("blocker resolution needs explicit accepted/resolved evidence")
        if "authority" in event.get("resolved_holds", []):
            if not any(a.get("status") == "accepted" and a.get("evidence_path") == event["resolution"]["path"] for a in state.get("approvals", [])):
                raise ValueError("authority resolution must bind an accepted user decision")
    diagnosis = event.get("diagnosis")
    if isinstance(diagnosis, dict):
        evidence = read_record(diagnosis.get("evidence"), read, "diagnosis")
        if evidence.get("kind") != "analysis" or not evidence.get("summary"):
            raise ValueError("diagnosis evidence requires an analysis summary")
    decision = event.get("decision")
    if isinstance(decision, dict) and kind == "escalation":
        evidence = read_record(decision.get("evidence"), read, "root escalation decision")
        if evidence.get("kind") != "analysis" or not evidence.get("summary"):
            raise ValueError("root escalation requires recorded causal analysis")
        for key in ("decision_id", "episode_id", "trigger", "scope_sha256"):
            if evidence.get(key) != decision.get(key):
                raise ValueError("escalation metadata differs from root analysis evidence")
    elif isinstance(decision, dict):
        approval = next((a for a in state.get("approvals", []) if a.get("decision_id") == decision.get("decision_id")), None)
        if not approval or approval.get("status") != "accepted" or approval.get("scope_sha256") != decision.get("scope_sha256"):
            raise ValueError("decision must match a current accepted scoped approval")
        raw = read(approval.get("evidence_path")) if safe_path(approval.get("evidence_path")) else None
        try:
            decision_record = json.loads(raw) if raw else None
        except (ValueError, UnicodeError):
            decision_record = None
        if not isinstance(decision_record, dict) or any(decision_record.get(k) != v for k, v in decision.items()):
            raise ValueError("decision payload differs from durable user/authorization evidence")
    if kind == "phase_complete":
        envelope = read_record(event.get("result"), read, "author self-review")
        raw = envelope.get("raw_result", {})
        expected_stage = {"spec": "specify", "plan": "plan", "tasks": "tasks"}.get(event.get("phase"))
        if (envelope.get("observed_after") != (expected_head or state.get("transition_head"))
                or raw.get("kind") != "self-review" or raw.get("stage") != expected_stage
                or raw.get("outcome") != "passed" or not raw.get("summary")):
            raise ValueError("phase_complete requires the current phase's fresh successful author self-review")
        if current:
            _hashes_match(raw, state, read, names=(event["phase"],))
    if not current and kind == "close":
        for ref in event.get("proofs", {}).values():
            record = read_record(ref, read, "historical closure proof")
            raw = record.get("raw_result", record)
            if raw.get("disposition") != "not-required":
                _historical_accept(raw)


def _historical_accept(raw: dict) -> None:
    """Authenticate immutable receipt shape, never re-authorize today's inputs.

    Fresh results, forward dispatch, and committed completion validate the actual
    input manifests. Rechecking old bytes here would prevent legitimate repair.
    """
    if raw.get("verdict") == "ACCEPT":
        from validate_workflow import validate_verdict
        errors = validate_verdict(raw)
        if errors:
            raise ValueError("historical review receipt is invalid: " + "; ".join(errors))
    elif raw.get("outcome") == "converged":
        if not isinstance(raw.get("input_hashes"), dict) or not raw["input_hashes"]:
            raise ValueError("historical convergence requires its recorded input manifest")
    elif raw.get("stage") == "verify" and raw.get("outcome") == "passed":
        if not isinstance(raw.get("commands"), dict) or not {"build", "test", "focused"} <= raw["commands"].keys():
            raise ValueError("historical verification requires its command references")
    elif raw.get("stage") == "conditional-e2e" and raw.get("result") in ("passed", "skipped-no-risk", "skipped-covered"):
        pass
    elif raw.get("kind") == "command" and type(raw.get("exit_code")) is int and raw["exit_code"] == 0:
        if not raw.get("command") or not raw.get("raw_output_sha256"):
            raise ValueError("historical passing command requires its command/output receipt")
    elif raw.get("status") in ("accepted", "resolved") and raw.get("summary"):
        pass
    else:
        raise ValueError("historical accepted event does not contain an accepted result")


def validate_transition_head(state: dict, read: Reader, *, config: dict | None = None) -> list[str]:
    from decision_policy import advance_recovery, validate_recovery_projection
    try:
        record = read_record(state.get("transition_head"), read, "transition_head")
        transition = _transition(record, state["bmad_story_key"])
        if transition["projection"] != projection(state):
            raise ValueError("state recovery/author differs from durable transition head (erased or stale state)")
        event = transition["event"]
        if state.get("stage") == "complete" and event.get("kind") != "close":
            raise ValueError("complete state requires final verified closure evidence, not an activation/partial transition")
        previous = transition["previous_head"]
        if previous is None:
            if transition["sequence"] != 1 or event.get("kind") not in ("activation", "adoption"):
                raise ValueError("only activation/adoption can initialize a transition head")
            if event["kind"] == "activation" and projection(state) != {
                    "recovery": None, "definition_author": {"profile": "definition-author", "escalation_decision_id": None}}:
                raise ValueError("activation cannot grant recovery or escalation")
            if event["kind"] == "adoption":
                if not reference(event.get("legacy_evidence")):
                    raise ValueError("adoption must retain its legacy evidence")
                legacy_evidence = read_record(event["legacy_evidence"], read, "legacy adoption")
                expected = _adoption_projection(state, event, legacy_evidence, read)
                if expected != transition["projection"]:
                    raise ValueError("adoption projection erased or changed its evidenced recovery budget")
            if record.get("technical_retry") is not None:
                raise ValueError("initial transition cannot invent a technical retry projection")
        else:
            previous_record = read_record(previous, read, "previous transition")
            predecessor = _transition(previous_record, state["bmad_story_key"])
            if transition["sequence"] != predecessor["sequence"] + 1:
                raise ValueError("transition sequence must advance exactly once")
            prior = {**state, **predecessor["projection"]}
            validate_event_evidence(event, prior, read, current=state.get("stage") == "complete", expected_head=previous, config=config)
            replay_event = event
            if event.get("kind") == "invalidation" and "invalidation_policy" not in event:
                # Distribution 0.6 ignored owner during recovery. Replay that immutable
                # receipt as issued; every fresh proposal below pins the current policy.
                replay_event = {key: value for key, value in event.items() if key != "owner"}
            expected = advance_recovery(prior, replay_event)
            if expected != transition["projection"]:
                raise ValueError("transition projection does not follow the previous durable result")
            if record.get("technical_retry") != _technical_projection(record, previous_record, read):
                raise ValueError("technical retry projection was reset or changed without its operation result")
        return validate_recovery_projection(state)
    except (KeyError, TypeError, ValueError, OSError) as exc:
        return [f"transition: {exc}"]


def validate_story_evidence(state: dict, read: Reader, *, config: dict | None = None,
                            content_lines=None, state_path: str | None = None,
                            current_boundary: bool = True) -> list[str]:
    if state.get("schema_version") != "3.1":
        return []
    errors = validate_transition_head(state, read, config=config)
    from story_learning import validate_lookback
    lookback = state.get("lookback", {})
    convergence = state.get("convergence", {})
    if current_boundary and convergence.get("status") == "accepted" and safe_path(convergence.get("report_path")):
        raw = read(convergence["report_path"])
        try:
            record = json.loads(raw) if raw else None
            if not isinstance(record, dict):
                raise ValueError("accepted convergence report is missing")
            record = record.get("raw_result", record)
            errors.extend(validate_convergence_record(record, state, read))
        except (ValueError, TypeError, AttributeError) as exc:
            errors.append(str(exc))
    if current_boundary and (lookback.get("status") == "current" or (state.get("stage") in LATE_STAGES and state.get("recovery") is None)):
        errors.extend(validate_lookback(state, read, config=config, content_lines=content_lines, state_path=state_path))
    resume = state.get("resume")
    if isinstance(resume, dict) and safe_path(resume.get("evidence_path")):
        raw = read(resume["evidence_path"])
        try:
            record = json.loads(raw) if raw else {}
        except (ValueError, UnicodeError):
            record = {}
        technical = record.get("technical_retry") if isinstance(record, dict) else None
        if isinstance(technical, dict) and (type(technical.get("invocations")) is not int or technical.get("invocations", 0) < 0):
            errors.append("technical retry evidence requires a nonnegative invocation count")
    return errors


def propose_transition(state: dict, record: dict, result_path: str, read: Reader, *, config: dict | None = None) -> tuple[dict, dict]:
    from decision_policy import INVALIDATION_POLICY, advance_recovery
    if state.get("schema_version") != "3.1":
        return propose_adoption(state, record, result_path, read)
    current_record = read_record(state.get("transition_head"), read, "transition_head")
    current_transition = _transition(current_record, state["bmad_story_key"])
    incoming = record.get("transition")
    if not isinstance(incoming, dict):
        raise ValueError("result requires a transition envelope")
    if incoming.get("previous_head") != state["transition_head"]:
        raw = read(result_path)
        if raw is not None and {"path": result_path, "sha256": digest(raw)} == state["transition_head"]:
            if record != current_record:
                raise ValueError("exact-head replay must match the immutable current record")
            return copy.deepcopy(state), record
        raise ValueError("stale/out-of-order result: previous_head must equal current transition_head")
    if incoming.get("story_key") != state["bmad_story_key"] or type(incoming.get("sequence")) is not int or incoming["sequence"] != current_transition["sequence"] + 1:
        raise ValueError("result must advance this story's transition sequence exactly once")
    event = incoming.get("event")
    if not isinstance(event, dict):
        raise ValueError("result transition requires an event")
    event = copy.deepcopy(event)
    if event.get("kind") == "invalidation":
        event.setdefault("invalidation_policy", INVALIDATION_POLICY)
    assessed = _apply_result_status(state, event, read)
    validate_event_evidence(event, assessed, read, config=config)
    updated = advance_recovery(state, event)
    if "projection" in incoming and incoming["projection"] != updated:
        raise ValueError("supplied transition projection differs from deterministic policy")
    complete = copy.deepcopy(record)
    complete["kind"] = "analysis"
    complete["transition"]["event"] = event
    complete["transition"]["projection"] = updated
    technical = _technical_projection(complete, current_record, read)
    if "technical_retry" in record and record["technical_retry"] != technical:
        raise ValueError("supplied technical retry projection differs from its operation result")
    if technical is not None or "technical_retry" in current_record:
        complete["technical_retry"] = technical
    proposed = {**assessed, **updated}
    if event.get("kind") == "failure" or event.get("kind") == "invalidation":
        owner = (updated.get("recovery") or {}).get("earliest_owner", event.get("owner", "implement"))
        _invalidate_downstream(proposed, owner)
        if event.get("kind") == "invalidation" and proposed.get("recovery") is None:
            proposed["stage"] = {"spec": "specify", "plan": "plan", "tasks": "tasks"}.get(owner, "implement")
            proposed["resume"] = {"stage": proposed["stage"], "reason": "Revalidate the changed boundary", "evidence_path": result_path}
    proposed["transition_head"] = {"path": result_path, "sha256": digest(canonical_bytes(complete))}
    return proposed, complete


def _apply_result_status(state: dict, event: dict, read: Reader) -> dict:
    result = copy.deepcopy(state)
    if event.get("kind") != "accepted":
        return result
    record = read_record(event.get("result"), read, "accepted result")
    raw = record.get("raw_result", record)
    if not isinstance(raw, dict):
        raise ValueError("accepted raw result must be an object")
    gate = raw.get("gate")
    if raw.get("verdict") == "ACCEPT" and gate in ("plan-review", "definition-review", "quality-review"):
        previous = result["gates"][gate]
        previous.update(status="accepted", attempt=previous["attempt"] + 1, verdict_path=event["result"]["path"])
    elif raw.get("outcome") == "converged":
        result["convergence"].update(status="accepted", outcome="converged",
                                     attempt=result["convergence"]["attempt"] + 1, report_path=event["result"]["path"])
    elif raw.get("stage") == "conditional-e2e" and raw.get("result") in ("passed", "skipped-no-risk", "skipped-covered"):
        result["e2e"].update(result=raw["result"], decision_path=event["result"]["path"])
    return result


def _invalidate_downstream(state: dict, owner: str) -> None:
    for name in ("plan-review", "definition-review", "quality-review"):
        affected = name == "quality-review" or (name == "definition-review" and owner in ("spec", "plan", "tasks")) or (name == "plan-review" and owner in ("spec", "plan"))
        gate = state["gates"][name]
        if affected and gate.get("verdict_path") is not None:
            gate["status"] = "invalidated"
    state["convergence"].update(status="invalidated", outcome="pending")
    state["e2e"].update(result="pending", decision_path=None)
    state["lookback"]["status"] = "invalidated"


def propose_adoption(state: dict, record: dict, result_path: str, read: Reader) -> tuple[dict, dict]:
    if state.get("schema_version") != "3.0" or state.get("stage") == "complete":
        raise ValueError("only active state 3.0 can adopt 3.1; completed states remain historical")
    transition = record.get("transition", {})
    event = transition.get("event", {})
    if event.get("kind") != "adoption" or transition.get("previous_head") is not None or transition.get("sequence") != 1 or transition.get("story_key") != state.get("bmad_story_key"):
        raise ValueError("adoption requires this story's initial adoption transition")
    evidence = read_record(event.get("legacy_evidence"), read, "legacy adoption")
    if evidence.get("legacy_sha256") != digest(canonical_bytes(state)):
        raise ValueError("legacy adoption evidence does not match the current 3.0 state")
    count = evidence.get("failed_results")
    if type(count) is not int or count < 0:
        raise ValueError("adoption requires an explicit nonnegative known failure count")
    known = max(state.get("attempts", {}).values(), default=0)
    revised = any(g.get("status") in ("revised", "blocked", "invalidated") for g in state.get("gates", {}).values()) or state.get("convergence", {}).get("status") in ("revised", "blocked", "invalidated")
    if count < known:
        raise ValueError("adoption cannot erase known failure counts")
    established = evidence.get("confidence") == "established"
    if count == 0 and (revised or not established or not evidence.get("clean_boundary_evidence")):
        count = 4  # An unknown old allowance is not a new automatic allowance.
        established = False
    if established and evidence.get("clean_boundary_evidence"):
        read_record(evidence["clean_boundary_evidence"], read, "clean legacy boundary")
    prepared_event = {**event, "origin_stage": state["stage"],
                      "legacy_constraints": {"observed_failures": known, "requires_recovery": revised}}
    prepared_evidence = {**evidence, "failed_results": count, "confidence": "established" if established else "uncertain"}
    projected = _adoption_projection(state, prepared_event, prepared_evidence, read)
    proposed = copy.deepcopy(state)
    proposed.pop("attempts", None)
    proposed.update(schema_version="3.1", **projected, lookback={"status": "pending", "evidence": None})
    complete = copy.deepcopy(record)
    complete["kind"] = "analysis"
    complete["transition"]["event"] = prepared_event
    complete["transition"]["projection"] = projection(proposed)
    proposed["transition_head"] = {"path": result_path, "sha256": digest(canonical_bytes(complete))}
    return proposed, complete


def _adoption_projection(state: dict, event: dict, evidence: dict, read: Reader) -> dict:
    count = evidence.get("failed_results")
    if type(count) is not int or count < 0:
        raise ValueError("adoption requires a known nonnegative failed_results")
    established = evidence.get("confidence") == "established"
    constraints = event.get("legacy_constraints", {})
    if not isinstance(constraints, dict):
        raise ValueError("legacy_constraints must be an object")
    known = constraints.get("observed_failures", 0)
    if type(known) is not int or known < 0 or count < known:
        raise ValueError("adoption cannot erase its observed legacy failure lower bound")
    if count == 0 and constraints.get("requires_recovery") is True:
        count, established = 4, False
    if count == 0:
        clean = evidence.get("clean_boundary_evidence")
        if established and reference(clean):
            accepted = read_record(clean, read, "clean legacy boundary")
            if accepted.get("verdict") != "ACCEPT" and accepted.get("outcome") != "converged":
                raise ValueError("clean legacy boundary must be ACCEPT or converged, not a negative result")
            _hashes_match(accepted, state, read)
        else:
            count, established = 4, False
    if not established:
        count = max(count, 4)
    owner = evidence.get("earliest_owner", "plan")
    if owner not in ("spec", "plan", "tasks", "implement", "tests"):
        raise ValueError("adoption earliest_owner is invalid")
    recovery = None
    if count:
        recovery = {"episode_id": evidence.get("episode_id", "adopt-" + state["bmad_story_key"]),
                    "failed_results": count, "pause_at": 4,
                    "closing_gate": "definition-review" if event.get("origin_stage", state["stage"]) in ("context", "specify", "plan", "plan-review", "tasks", "definition-review") else "verify",
                    "earliest_owner": owner, "action": "pause" if count >= 4 else "diagnose",
                    "last_result": event["legacy_evidence"], "diagnosis": None, "extension_decision_id": None, "holds": []}
    return {"recovery": recovery, "definition_author": {"profile": "definition-author", "escalation_decision_id": None}}
