from __future__ import annotations

from collections.abc import Mapping, Sequence
from copy import deepcopy
import re


ARCHITECTURE_SIGNALS = frozenset(
    {
        "authority_change",
        "new_agent_tool",
        "new_external_dependency",
        "public_contract",
        "repository_mandated_strategy",
        "shared_persistence_boundary",
    }
)
BOUNDED_SIGNALS = frozenset(
    {
        "adapter_boundary",
        "async_semantics",
        "cancellation_semantics",
        "cross_module_coordination",
        "error_semantics",
        "internal_interface",
        "responsibility_boundary",
        "retry_idempotency",
        "state_lifetime",
    }
)
REFACTOR_FORBIDDEN_SIGNALS = frozenset(
    {"architecture", "authority", "dependency", "migration", "public_contract", "schema"}
)


def classify_implementation_design(signals: Mapping[str, object]) -> str:
    active = {name for name, enabled in signals.items() if enabled is True}
    if active & ARCHITECTURE_SIGNALS:
        return "architecture"
    if active & BOUNDED_SIGNALS:
        return "bounded"
    return "none"


def route_design_discovery(
    classification: str, *, accepted_plan_covers: bool, authorized_scope_changed: bool
) -> dict[str, object]:
    if classification not in {"none", "bounded", "architecture"}:
        raise ValueError("classification must be none, bounded, or architecture")
    if accepted_plan_covers:
        if authorized_scope_changed:
            return {"stage": "implementation-approval", "renew_approval": True}
        return {"stage": "implement", "renew_approval": False}
    return {
        "stage": "plan",
        "renew_approval": classification == "architecture" or authorized_scope_changed,
    }


def can_refactor_after_green(signals: Mapping[str, object]) -> bool:
    if signals.get("observed_green") is not True:
        return False
    if signals.get("task_owned_paths") is not True:
        return False
    if signals.get("behavior_preserving") is not True:
        return False
    if any(signals.get(name) is not False for name in REFACTOR_FORBIDDEN_SIGNALS):
        return False
    known = REFACTOR_FORBIDDEN_SIGNALS | {"observed_green", "task_owned_paths", "behavior_preserving"}
    return not any(name not in known and enabled is True for name, enabled in signals.items())


# These functions decide transitions; they never read or write files. The root's
# validator verifies referenced raw evidence, current input hashes, approval scope,
# and transition order before accepting the proposed projection.
RECOVERY_PROFILES = ("definition-author", "definition-author-recovery", "definition-author-recovery-high")
RECOVERY_OWNERS = ("spec", "plan", "tasks", "implement", "tests")
RECOVERY_ACTIONS = {"correct", "diagnose", "pause", "authority", "blocked", "proceed"}
INVALIDATION_POLICY = "earliest-owner-v1"
RECOVERY_FIELDS = {"episode_id", "failed_results", "pause_at", "closing_gate", "earliest_owner", "action", "holds", "last_result", "diagnosis", "extension_decision_id"}
DEFINITION_AUTHOR_FIELDS = {"profile", "escalation_decision_id"}
_SHA256 = re.compile(r"[0-9a-f]{64}\Z")
_OWNER_STAGE = {"spec": "specify", "plan": "plan", "tasks": "tasks", "implement": "implement", "tests": "implement"}
_PRODUCTION_STAGES = {"implement", "converge", "lookback", "quality-review", "conditional-e2e", "e2e", "verify", "complete"}


def _mapping(value: object, label: str) -> Mapping:
    if not isinstance(value, Mapping):
        raise ValueError(f"{label} must be an object")
    return value


def _text(value: object, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{label} must be nonempty text")
    return value


def _integer(value: object, label: str, minimum: int = 0) -> int:
    if type(value) is not int or value < minimum:
        raise ValueError(f"{label} must be an integer >= {minimum}")
    return value


def _evidence(value: object, label: str) -> dict:
    value = _mapping(value, label)
    if set(value) != {"path", "sha256"}:
        raise ValueError(f"{label} requires only path and sha256")
    path = _text(value.get("path"), f"{label}.path")
    if "\\" in path or ":" in path or any(part in {"", ".", ".."} or part.endswith((" ", ".")) for part in path.split("/")) or any(ord(char) < 32 for char in path):
        raise ValueError(f"{label}.path must be a normalized repository-relative path")
    if not isinstance(value.get("sha256"), str) or not _SHA256.fullmatch(value["sha256"]):
        raise ValueError(f"{label}.sha256 must be a lowercase SHA-256")
    return dict(value)


def validate_recovery_projection(state: Mapping) -> list[str]:
    """Validate the small projection, independently of filesystem proof validity."""
    errors = []
    try:
        _mapping(state, "state")
        author = _mapping(state.get("definition_author"), "definition_author")
        if set(author) != DEFINITION_AUTHOR_FIELDS or author.get("profile") not in RECOVERY_PROFILES:
            raise ValueError("definition_author has invalid fields or profile")
        escalation = author.get("escalation_decision_id")
        if author["profile"] == "definition-author":
            if escalation is not None:
                raise ValueError("ordinary definition_author cannot retain an escalation decision")
        else:
            _text(escalation, "definition_author.escalation_decision_id")
        recovery = state.get("recovery")
        if recovery is None:
            return errors
        recovery = _mapping(recovery, "recovery")
        if set(recovery) != RECOVERY_FIELDS:
            raise ValueError("recovery has invalid fields")
        _text(recovery.get("episode_id"), "recovery.episode_id")
        count = _integer(recovery.get("failed_results"), "recovery.failed_results", 1)
        limit = _integer(recovery.get("pause_at"), "recovery.pause_at", 4)
        if recovery.get("closing_gate") not in {"definition-review", "verify"}:
            raise ValueError("recovery.closing_gate must be definition-review or verify")
        if recovery.get("earliest_owner") not in RECOVERY_OWNERS:
            raise ValueError("recovery.earliest_owner is invalid")
        if recovery.get("action") not in RECOVERY_ACTIONS:
            raise ValueError("recovery.action is invalid")
        holds = recovery.get("holds")
        if not isinstance(holds, list) or any(not isinstance(hold, str) or hold not in {"authority", "blocked"} for hold in holds) or holds != sorted(set(holds)):
            raise ValueError("recovery.holds must be a sorted unique list of authority or blocked")
        _evidence(recovery.get("last_result"), "recovery.last_result")
        diag = recovery.get("diagnosis")
        if diag is not None:
            diag = _mapping(diag, "recovery.diagnosis")
            if set(diag) != {"evidence", "at_count"}:
                raise ValueError("recovery.diagnosis requires evidence and at_count")
            _evidence(diag.get("evidence"), "recovery.diagnosis.evidence")
            if _integer(diag.get("at_count"), "recovery.diagnosis.at_count", 1) > count:
                raise ValueError("diagnosis cannot cover a future failure count")
        extension = recovery.get("extension_decision_id")
        if extension is not None:
            _text(extension, "recovery.extension_decision_id")
        if limit > 4 and extension is None:
            raise ValueError("extended pause_at requires extension_decision_id")
        if count >= limit and recovery["action"] != "pause":
            raise ValueError("recovery at its pause limit must remain paused")
        if count < limit and recovery["action"] == "pause":
            raise ValueError("recovery cannot pause before its current pause limit")
        held_action = "blocked" if "blocked" in holds else "authority" if "authority" in holds else None
        if count < limit and held_action is not None and recovery["action"] != held_action:
            raise ValueError("recovery must preserve its unresolved holds before continuation")
        if recovery["action"] in {"authority", "blocked"} and recovery["action"] not in holds:
            raise ValueError("recovery restricted action requires its durable hold")
        if count < limit and _diagnosis_due(recovery) and recovery["action"] not in {"diagnose", "blocked", "authority"}:
            raise ValueError("recovery requires diagnosis before continuation")
    except (TypeError, ValueError) as exc:
        errors.append(str(exc))
    return errors


def _diagnosis_due(recovery: Mapping) -> bool:
    diagnosis = recovery.get("diagnosis")
    if diagnosis is None:
        return recovery["failed_results"] >= 3 or recovery.get("extension_decision_id") is not None
    return recovery["failed_results"] - diagnosis["at_count"] >= 2


def _owners(event: Mapping, fallback: str = "implement") -> tuple[str, str]:
    owners = event.get("owners", [fallback])
    if not isinstance(owners, list) or not owners or any(not isinstance(owner, str) for owner in owners):
        raise ValueError("owners must contain the currently invalid owners")
    owners = ["implement" if owner == "implementation" else owner for owner in owners]
    if any(owner not in {*RECOVERY_OWNERS, "authority", "blocker", "uncertain"} for owner in owners):
        raise ValueError("owners contains an unknown owner")
    ordinary = [owner for owner in owners if owner in RECOVERY_OWNERS]
    earliest = min(ordinary, key=RECOVERY_OWNERS.index) if ordinary else fallback
    action = "blocked" if "blocker" in owners else "authority" if "authority" in owners else "diagnose" if "uncertain" in owners else "correct"
    return earliest, action


def _budget_action(recovery: Mapping, otherwise: str) -> str:
    if recovery["failed_results"] >= recovery["pause_at"]:
        return "pause"
    if "blocked" in recovery["holds"]:
        return "blocked"
    if "authority" in recovery["holds"]:
        return "authority"
    return "diagnose" if _diagnosis_due(recovery) else otherwise


def _add_hold(recovery: dict, action: str) -> None:
    if action in {"authority", "blocked"}:
        recovery["holds"] = sorted(set(recovery["holds"]) | {action})


def _add_event_holds(recovery: dict, event: Mapping) -> None:
    # _owners validates the list first. Both restrictions may occur in one result.
    owners = event.get("owners", [])
    for owner, hold in (("authority", "authority"), ("blocker", "blocked")):
        if owner in owners:
            _add_hold(recovery, hold)


def _decision(state: Mapping, event: Mapping, recovery: Mapping) -> Mapping:
    decision = _mapping(event.get("decision"), "decision")
    _text(decision.get("decision_id"), "decision.decision_id")
    if decision.get("status") != "accepted" or decision.get("episode_id") != recovery["episode_id"]:
        raise ValueError("decision must be accepted for the current episode")
    scope = decision.get("scope_sha256")
    if not isinstance(scope, str) or not _SHA256.fullmatch(scope) or event.get("scope_sha256") != scope:
        raise ValueError("decision must bind the current scope_sha256")
    approvals = state.get("approvals", [])
    if not isinstance(approvals, list) or not any(isinstance(item, Mapping) and item.get("decision_id") == decision["decision_id"] and item.get("status") == "accepted" and item.get("scope_sha256") == scope for item in approvals):
        raise ValueError("decision must match a current accepted approval")
    return decision


def _escalation_decision(event: Mapping, recovery: Mapping) -> Mapping:
    """Root routing evidence, not a new human-approval requirement."""
    decision = _mapping(event.get("decision"), "decision")
    _text(decision.get("decision_id"), "decision.decision_id")
    if decision.get("episode_id") != recovery["episode_id"]:
        raise ValueError("escalation decision must bind the current episode")
    if decision.get("status", "accepted") != "accepted":
        raise ValueError("escalation decision cannot be declined or pending")
    scope = decision.get("scope_sha256")
    if not isinstance(scope, str) or not _SHA256.fullmatch(scope) or event.get("scope_sha256", scope) != scope:
        raise ValueError("escalation decision must bind its current scope_sha256")
    _evidence(decision.get("evidence"), "decision.evidence")
    return decision


def advance_recovery(state: Mapping, event: Mapping) -> dict:
    """Propose recovery/author only; the root writes and binds transition_head.

    Every event has kind and result {path,sha256}. Failures have owners and an
    episode_id when opening, then repair {obligation,correction,previous_result}.
    A repair can cite independently observed new evidence as its correction;
    the root must verify that it justifies a new check rather than blind reruns.
    Diagnosis has diagnosis {evidence,at_count}, plus currently invalid owners.
    Extension carries a decision matching state.approvals and event.scope_sha256.
    Escalation carries a root decision bound to diagnosis, episode, and scope;
    it does not introduce human approval. Close carries current gate proofs.

    This function does not authenticate semantic classifications or evidence.
    Call validate_transition_order and validate raw proofs before accepting it.
    """
    problems = validate_recovery_projection(state)
    if problems:
        raise ValueError("; ".join(problems))
    event = _mapping(event, "event")
    result = _evidence(event.get("result"), "event.result")
    kind = _text(event.get("kind"), "event.kind")
    projection = deepcopy({"recovery": state.get("recovery"), "definition_author": state["definition_author"]})
    recovery = projection["recovery"]
    if recovery and event.get("episode_id", recovery["episode_id"]) != recovery["episode_id"]:
        raise ValueError("an unresolved episode cannot be replaced")
    if kind == "failure":
        if recovery and result == recovery["last_result"]:
            return projection
        owner, action = _owners(event)
        if recovery is None:
            recovery = projection["recovery"] = {
                "episode_id": _text(event.get("episode_id"), "episode_id"),
                "failed_results": 1, "pause_at": 4,
                "closing_gate": "verify" if event.get("production_started") is True or state.get("stage") in _PRODUCTION_STAGES else "definition-review",
                "earliest_owner": owner, "action": action, "holds": [], "last_result": result,
                "diagnosis": None, "extension_decision_id": None,
            }
        else:
            repair = _mapping(event.get("repair"), "repair")
            _text(repair.get("obligation"), "repair.obligation")
            _evidence(repair.get("correction"), "repair.correction")
            if repair.get("previous_result") != recovery["last_result"]:
                raise ValueError("repair.previous_result must identify the current negative result")
            recovery["failed_results"] += 1
            # Root supplies all currently invalid owners. Previously accepted
            # spec repairs must not make spec a permanently sticky destination.
            recovery["earliest_owner"] = owner
            recovery["last_result"] = result
        if event.get("production_started") is True or state.get("stage") in _PRODUCTION_STAGES:
            recovery["closing_gate"] = "verify"
        _add_event_holds(recovery, event)
        recovery["action"] = _budget_action(recovery, action)
    elif kind == "phase_complete":
        phase = event.get("phase")
        proceeding_tasks = (recovery is not None and phase == "tasks"
                            and recovery["action"] == "proceed"
                            and recovery["earliest_owner"] == "tasks")
        if (recovery is None or (recovery["action"] != "correct" and not proceeding_tasks)
                or recovery["holds"] or _diagnosis_due(recovery)
                or recovery["failed_results"] >= recovery["pause_at"]):
            raise ValueError("phase_complete requires an unblocked corrective author phase")
        if phase not in ("spec", "plan", "tasks") or phase != recovery["earliest_owner"]:
            raise ValueError("phase_complete.phase must match the current definition owner")
        if phase == "tasks":
            recovery["action"] = "proceed"
        else:
            recovery["earliest_owner"] = "plan" if phase == "spec" else "tasks"
        # Self-review advances document writing only; it cannot accept a gate,
        # close the episode, replenish the budget, or replace the retained author.
    elif kind in {"invalidation", "expected_red", "technical_failure", "accepted", "blocked", "production_started"}:
        invalidation_owner = None
        if kind == "invalidation" and event.get("invalidation_policy", INVALIDATION_POLICY) != INVALIDATION_POLICY:
            raise ValueError("invalidation_policy must be earliest-owner-v1")
        if kind == "invalidation" and "owner" in event:
            invalidation_owner = _text(event["owner"], "invalidation.owner")
            if invalidation_owner not in RECOVERY_OWNERS:
                raise ValueError("invalidation.owner must be spec, plan, tasks, implement, or tests")
        if recovery:
            if invalidation_owner is not None:
                owner = min((recovery["earliest_owner"], invalidation_owner), key=RECOVERY_OWNERS.index)
                # A material invalidation changes the causal boundary even within one owner.
                recovery["diagnosis"] = None
                recovery["earliest_owner"] = owner
                action = "diagnose" if recovery["action"] == "diagnose" else "correct"
                recovery["action"] = _budget_action(recovery, action)
            if kind == "production_started" or event.get("production_started") is True:
                recovery["closing_gate"] = "verify"
            if kind == "blocked":
                _add_hold(recovery, "blocked")
                recovery["action"] = _budget_action(recovery, "blocked")
            elif kind == "accepted":
                if "resolved_holds" in event:
                    resolved = event["resolved_holds"]
                    if not isinstance(resolved, list) or not resolved or any(not isinstance(hold, str) or hold not in recovery["holds"] for hold in resolved) or len(set(resolved)) != len(resolved):
                        raise ValueError("accepted.resolved_holds must name unique currently held restrictions")
                    _evidence(event.get("resolution"), "accepted.resolution")
                    recovery["holds"] = [hold for hold in recovery["holds"] if hold not in resolved]
                elif "resolution" in event:
                    raise ValueError("accepted.resolution must name resolved_holds explicitly")
                recovery["action"] = _budget_action(recovery, "correct" if event.get("resolved_holds") else "proceed")
    elif kind == "diagnosis":
        if recovery is None:
            raise ValueError("diagnosis requires an active recovery episode")
        diagnosis = _mapping(event.get("diagnosis"), "diagnosis")
        if type(diagnosis.get("at_count")) is not int or diagnosis["at_count"] != recovery["failed_results"]:
            raise ValueError("diagnosis.at_count must cover the current failed_results")
        recovery["diagnosis"] = {"evidence": _evidence(diagnosis.get("evidence"), "diagnosis.evidence"), "at_count": diagnosis["at_count"]}
        owner, action = _owners(event, recovery["earliest_owner"])
        recovery["earliest_owner"] = owner
        # Diagnosis can discover another restriction but cannot release a hold.
        _add_event_holds(recovery, event)
        recovery["action"] = _budget_action(recovery, action)
    elif kind == "extension":
        if recovery is None or recovery["failed_results"] < recovery["pause_at"]:
            raise ValueError("extension requires a paused active episode")
        decision = _decision(state, event, recovery)
        limit = _integer(decision.get("pause_at"), "decision.pause_at", 4)
        if limit <= max(recovery["pause_at"], recovery["failed_results"]):
            raise ValueError("decision.pause_at must grant a finite higher cumulative limit")
        if decision["decision_id"] == recovery["extension_decision_id"]:
            raise ValueError("an extension decision cannot be reused")
        recovery.update(pause_at=limit, extension_decision_id=decision["decision_id"], diagnosis=None)
        recovery["action"] = _budget_action(recovery, "diagnose")
    elif kind == "close":
        if recovery is not None and recovery["holds"]:
            raise ValueError("close requires explicit resolution of every authority or blocker hold")
        if recovery is not None and event.get("episode_id") != recovery["episode_id"]:
            raise ValueError("close requires the current episode_id")
        if recovery is None and event.get("episode_id") is not None:
            raise ValueError("happy-path close cannot name an inactive episode")
        proofs = _mapping(event.get("proofs"), "close.proofs")
        required = ("definition-review", "plan-review") if recovery and recovery["closing_gate"] == "definition-review" else ("converge", "quality-review", "e2e", "verify")
        for gate in required:
            _evidence(proofs.get(gate), f"close.proofs.{gate}")
        projection["recovery"] = None
    elif kind == "escalation":
        if recovery is None or recovery["action"] in {"pause", "blocked", "authority", "diagnose"}:
            raise ValueError("escalation requires an unblocked, diagnosed recovery")
        if recovery["diagnosis"] is None or recovery["diagnosis"]["at_count"] != recovery["failed_results"] or recovery["earliest_owner"] not in {"spec", "plan"}:
            raise ValueError("escalation requires a current diagnosis of definition reasoning failure")
        decision = _escalation_decision(event, recovery)
        current = projection["definition_author"]["profile"]
        profile = event.get("profile")
        trigger = _text(decision.get("trigger"), "decision.trigger")
        if profile == "definition-author-recovery":
            if current != "definition-author" or trigger not in {"unresolved_definition_reasoning", "uncovered_consequential_design"}:
                raise ValueError("medium escalation requires ordinary author and a definition reasoning trigger")
        elif profile == "definition-author-recovery-high":
            if current != "definition-author-recovery" or trigger not in {"interacting_design_constraints", "failed_medium_definition_reasoning"}:
                raise ValueError("high escalation requires retained medium and a deeper-analysis trigger")
        else:
            raise ValueError("escalation profile is not an approved recovery tier")
        projection["definition_author"] = {"profile": profile, "escalation_decision_id": decision["decision_id"]}
    else:
        raise ValueError(f"unknown recovery event kind: {kind}")
    return projection


def plan_review_recovery_candidate(state: Mapping, next_stage: str, next_profile: str | None,
                                   plan_classification: str | None, *, gate_statuses=frozenset({"revised", "invalidated"})) -> bool:
    """Identify the review owed after a corrected plan advances ownership to tasks.

    Raw transition and current-plan freshness are authenticated by result_proofs.
    This does not accept the review or authorize task progression.
    """
    recovery = state.get("recovery")
    gate = state.get("gates", {}).get("plan-review")
    return (next_stage == "plan-review" and next_profile == "definition-reviewer"
            and plan_classification == "architecture"
            and isinstance(recovery, Mapping) and recovery.get("action") == "correct"
            and recovery.get("earliest_owner") == "tasks"
            and recovery.get("closing_gate") in {"definition-review", "verify"}
            and not recovery.get("holds")
            and recovery.get("failed_results", 0) < recovery.get("pause_at", 0)
            and isinstance(gate, Mapping) and gate.get("status") in gate_statuses)


def recovery_dispatch_decision(state: Mapping, next_stage: str, next_profile: str | None = None, *, plan_classification: str | None = None) -> dict:
    """Guard corrections/author continuity; ordinary lifecycle gates stay upstream."""
    def decision(allowed: bool, action: str, reason: str) -> dict:
        return {"action": action, "allowed": allowed, "reason": reason}
    if not isinstance(next_stage, str) or not next_stage or (next_profile is not None and not isinstance(next_profile, str)):
        return decision(False, "invalid", "next_stage and optional next_profile must be text")
    problems = validate_recovery_projection(state)
    if problems:
        return decision(False, "invalid", "; ".join(problems))
    recovery = state.get("recovery")
    if recovery:
        action = recovery["action"]
        if action in {"pause", "authority", "blocked", "diagnose"}:
            return decision(False, action, f"resolve recovery action {action} before dispatch")
        if (action == "correct" and next_stage != _OWNER_STAGE[recovery["earliest_owner"]]
                and not plan_review_recovery_candidate(state, next_stage, next_profile, plan_classification)):
            return decision(False, "return_to_owner", f"correct the earliest invalid owner: {recovery['earliest_owner']}")
    profile = state["definition_author"]["profile"]
    if next_profile in {*RECOVERY_PROFILES, "plan-architect-high"}:
        if next_stage not in {"specify", "plan", "tasks"}:
            return decision(False, "ownership", "definition authors own only spec, plan, and tasks")
        if next_profile == "plan-architect-high" and profile == "definition-author" and next_stage == "plan":
            classification = plan_classification if plan_classification is not None else state.get("implementation_design_classification")
            if classification != "architecture":
                return decision(False, "classification", "architecture-plan exception requires current architecture classification")
            return decision(True, "dispatch", "existing initial architecture-plan exception")
        if next_profile != profile:
            return decision(False, "ownership", "only the retained definition author may receive this dispatch")
    elif next_profile is not None and next_stage in {"specify", "plan", "tasks"}:
        return decision(False, "ownership", "definition writing requires the retained definition-author profile")
    return decision(True, "dispatch", "recovery permits dispatch; required lifecycle gates still apply")


def validate_transition_order(head: Mapping | None, head_sequence: int, event: Mapping) -> bool:
    """Return True for exact current-head replay; otherwise reject stale linkage."""
    _integer(head_sequence, "head_sequence")
    result = _evidence(event.get("result"), "event.result")
    sequence = _integer(event.get("sequence"), "event.sequence", 1)
    if head is not None:
        _evidence(head, "transition_head")
        if result == head and sequence == head_sequence:
            return True
        if result["path"] == head["path"]:
            raise ValueError("the current evidence path cannot be rewritten or resequenced")
    elif head_sequence != 0:
        raise ValueError("a missing transition head requires initial sequence zero")
    if sequence != head_sequence + 1 or event.get("previous_head") != head:
        raise ValueError("transition must extend the current head at the next sequence")
    return False


def technical_retry_decision(retry: Mapping | None) -> dict:
    if retry is None:
        return {"allowed": True, "action": "invoke", "reason": "initial root invocation"}
    _mapping(retry, "technical_retry")
    count = _integer(retry.get("invocations"), "technical_retry.invocations")
    status = _text(retry.get("status"), "technical_retry.status")
    if status not in {"running", "transient", "fatal", "repaired"}:
        raise ValueError("technical_retry.status is invalid")
    if status in {"fatal", "running"} or count >= 3:
        return {"allowed": False, "action": "blocked", "reason": "operation is in flight, fatal, or its three-invocation allowance is exhausted"}
    return {"allowed": True, "action": "invoke", "reason": "bounded root invocation remains"}


def advance_technical_retry(retry: Mapping | None, event: Mapping) -> dict | None:
    """Persist this projection in root failure evidence, outside workflow state.

    invoke counts one root call; transient_failure/fatal_failure records outcome.
    success clears it. external_repair requires independent repair_evidence and
    opens a fresh allowance for the repaired cause. SDK retries are not counted.
    """
    operation = _mapping(event.get("operation"), "technical_retry.operation")
    if set(operation) != {"command", "target", "cause"}:
        raise ValueError("operation requires command, target, and cause identity")
    for name in operation:
        _text(operation[name], f"operation.{name}")
    result = _evidence(event.get("result"), "technical_retry.result")
    kind = _text(event.get("kind"), "technical_retry.event.kind")
    if retry is not None and retry.get("operation") != operation:
        raise ValueError("resolve the current operation before replacing its identity")
    if retry is not None and retry.get("last_result") == result:
        return deepcopy(retry)
    if kind == "invoke":
        if not technical_retry_decision(retry)["allowed"]:
            raise ValueError("technical retry exhausted, fatal, or already running")
        return {"operation": dict(operation), "invocations": (retry["invocations"] if retry else 0) + 1, "status": "running", "last_result": result}
    if retry is None:
        raise ValueError("technical result requires an existing invocation")
    candidate = deepcopy(dict(retry))
    if kind in {"success", "transient_failure", "fatal_failure"}:
        if retry.get("status") != "running":
            raise ValueError("technical outcome requires an in-flight invocation")
        if kind == "success":
            return None
        candidate.update(status="transient" if kind == "transient_failure" else "fatal", last_result=result)
    elif kind == "external_repair":
        if retry.get("status") == "running":
            raise ValueError("cannot reset an in-flight operation")
        _evidence(event.get("repair_evidence"), "technical_retry.repair_evidence")
        candidate.update(invocations=0, status="repaired", last_result=result)
    else:
        raise ValueError("unknown technical retry event")
    return candidate


def match_convergence_task(finding: Mapping, tasks: Sequence[Mapping]) -> str | None:
    """Exact normalized source/behavior/defect match only; no semantic guarantee."""
    keys = ("source", "behavior", "defect")
    if any(not isinstance(finding.get(key), str) or not finding[key].strip() for key in keys):
        return None
    for task in tasks:
        if task.get("complete") is False and all(task.get(key) == finding[key] for key in keys):
            return _text(task.get("id"), "task.id")
    return None


def validate_convergence_tasks(before: bytes | str, after: bytes | str, before_tasks: Mapping[str, bool], after_tasks: Mapping[str, bool], result: Mapping) -> list[str]:
    """Validate writes and parsed IDs; caller uses its existing Markdown parser.

    Task maps are id -> complete. For duplicate-ID detection the caller must
    reject duplicates while parsing (a mapping cannot retain duplicates).
    """
    errors = []
    disposition = result.get("disposition")
    if disposition not in {"converged", "tasks_appended", "tasks_reused", "return_to_definition", "blocked", "failed"}:
        errors.append("unknown governed convergence disposition")
    if not isinstance(before, type(after)) or not after.startswith(before):
        errors.append("convergence must preserve every existing task byte")
    reused, appended = result.get("reused_task_ids", []), result.get("appended_task_ids", [])
    for label, ids in (("reused_task_ids", reused), ("appended_task_ids", appended)):
        if not isinstance(ids, list) or any(not isinstance(item, str) for item in ids):
            return errors + [f"{label} must be a list of task IDs"]
        if len(ids) != len(set(ids)):
            errors.append(f"{label} contains duplicate IDs")
    for task in reused:
        if task not in before_tasks or before_tasks[task] is not False:
            errors.append(f"reused task {task} must already exist and be unfinished")
    for task, complete in before_tasks.items():
        if task not in after_tasks or after_tasks[task] != complete:
            errors.append(f"convergence cannot change existing task {task}")
    added = set(after_tasks) - set(before_tasks)
    if set(appended) != added or set(appended) & set(before_tasks):
        errors.append("appended IDs must exactly describe genuinely new tasks")
    if any(after_tasks[task] is not False for task in added):
        errors.append("new corrective tasks must be unfinished")
    if disposition == "converged" and (reused or appended or any(value is False for value in after_tasks.values())):
        errors.append("unfinished, reused, or appended work cannot claim convergence")
    if disposition == "tasks_appended" and not appended:
        errors.append("tasks_appended requires newly appended tasks")
    if disposition == "tasks_reused" and (not reused or appended):
        errors.append("tasks_reused requires unfinished reused work and no new tasks")
    if disposition == "return_to_definition" and result.get("owner") not in {"spec", "plan", "tasks"}:
        errors.append("return_to_definition requires spec, plan, or tasks owner")
    if disposition != "tasks_appended" and before != after:
        errors.append("only tasks_appended may write tasks; upstream defects take precedence")
    return errors
