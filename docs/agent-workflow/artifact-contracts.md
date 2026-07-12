# Sprint Loop Artifact Contracts

All contracts are JSON-compatible even when rendered as Markdown or embedded in a dispatch prompt. Paths are repository-relative with `/` separators. Timestamps use UTC RFC 3339. Git commits are full immutable SHAs except while a not-yet-committed checkpoint is being assembled.

## Workflow State

Each feature owns `specs/<feature>/workflow-state.json`.

| Field | Type | Required | Contract |
|---|---|---|---|
| `schema_version` | string | yes | Contract version, initially `1.0` |
| `bmad_story_key` | string | yes | Exact BMad story key; immutable after mapping |
| `bmad_epic_key` | string | yes | Owning BMad epic key |
| `feature_directory` | string | yes | Unique SpecKit directory for this story |
| `stage` | string | yes | One exact stage from [the state machine](state-machine.md) |
| `active_gates` | object | yes | Per-gate records for concurrent work; keys are `acceptance-review` and `quality-review`, each with `status`, `iteration`, `dispatch_id`, `verdict_path`, `input_boundary`, and `completed_at` |
| `stage_history` | array | yes | Ordered stage records with `stage`, `status`, `started_at`, `completed_at`, `input_commit`, `input_hashes`, `agent_role`, `evidence_paths`, and `failure` |
| `repository_preflight` | object | yes | `branch`, `preflight_head`, `repository_root`, `git_common_directory`, `worktree_path`, `worktree_status`, `owned_paths`, `unrelated_paths`, and `observed_at` |
| `source_artifacts` | array | yes | Objects with `authority`, `path`, `commit`, and `sha256` |
| `dispatches` | array | yes | Role, objective, source boundary, permitted writes, output path, and returned evidence for every agent call |
| `review_iterations` | object | yes | Per-gate integer counts and verdict artifact paths |
| `e2e_decision` | object | yes | Triggered predicates, nullable complete-coverage override, final `required`, rationale, covering tests, owner, result, source boundary, and evidence IDs |
| `verification` | array | yes | Command evidence records in execution order |
| `last_checkpoint` | object or null | yes | Checkpoint ID, workflow stage, committed paths, binding status, and nullable cached SHA; Git trailer resolution is canonical |
| `recoverable_failure` | object or null | yes | Failure evidence and earliest safe resume stage |
| `updated_at` | string | yes | UTC timestamp of the atomic state write |

Stage and active-gate `status` is `pending`, `active`, `accepted`, `revised`, `blocked`, `failed`, or `invalidated`. The scalar `stage` remains canonical. During parallel implementation review it is `acceptance-review`; `active_gates` records both returns independently and deterministically. When both accept, the orchestrator closes `acceptance-review`, records `quality-review` accepted from the stored quality verdict, and advances to `conditional-e2e` atomically. A state file cannot claim a stage accepted without evidence paths and validated inputs. It may contain provisional `stage: "complete"` with a pending final binding, but that claim is untrusted until the BMad story is `done` and the final checkpoint ID resolves uniquely from Git trailers.

`repository_preflight` is refreshed before every mutation and checkpoint. Resume compares its branch, Git/worktree identities, preflight `HEAD`, exact porcelain status snapshot, owned paths, and unrelated paths with current state. A missing ownership field prevents mutation; changed ownership or inseparable edits require user direction rather than inference.

## Story/Feature Mapping

The mapping is two-way and one-to-one.

| Side | Required fields |
|---|---|
| `workflow-state.json` | `bmad_story_key`, `bmad_epic_key`, `feature_directory`, BMad story path in `source_artifacts` |
| BMad story development record | Exact `SpecKit feature directory`, current workflow stage, and latest workflow checkpoint ID |

Mapping creation is atomic with initial workflow state. Resume rejects absent, conflicting, duplicate, or many-to-one mappings rather than guessing.

## Pre-Mapping Story Activation

Before `speckit-specify` creates a feature directory, the root orchestrator atomically writes `docs/bmad/implementation-artifacts/workflow-activations/<bmad-story-key>.json` with `schema_version`, story/epic keys, story path, proposed feature short name, stage `context`, repository preflight, activation checkpoint ID, nullable `feature_directory`, and `superseded_by_state`. The BMad story development record points to this canonical activation path. No worker writes it, and it is not a provisional SpecKit mapping.

The activation checkpoint commits this record with the story `ready-for-dev → in-progress` transition and, when the parent epic is `backlog`, the parent epic `backlog → in-progress` transition in the same commit. Resume derives the canonical path from the exact story key and validates its checkpoint trailer, so it never needs a feature mapping to find active work.

After `spec-author` returns the SpecKit-created feature path and artifacts, the root alone creates `workflow-state.json`, writes both mapping sides atomically, sets the activation record's `feature_directory` and `superseded_by_state`, and validates one-to-one ownership. Resume then follows the activation pointer to the mapped state; a partial or conflicting handoff blocks.

Legacy fallback is deterministic and applies only to a BMad story already `in-progress` or `review` before this workflow introduced activation records. If the canonical activation file is absent, resume may use the feature directory explicitly recorded in the BMad story development record only when that directory contains a valid `workflow-state.json` whose story key, epic key, and feature path point back exactly; no other story or feature claims either side; artifact/checkpoint validation succeeds; and current ownership is separable. The validated pair is treated as a legacy activation source and resumed at the earliest incomplete/invalid stage. It is never duplicated merely because the activation file is absent. Missing, inferred, partial, or conflicting legacy mappings still block. Every story activated by this workflow must have the canonical activation record; the fallback cannot excuse a missing record for new work.

## Verdict

Every judge and reviewer returns one object with these top-level fields:

| Field | Type | Required | Contract |
|---|---|---|---|
| `schema_version` | string | yes | `1.0` |
| `gate` | string | yes | Reviewed gate such as `spec-review`, `plan-review`, `analyze`, `acceptance-review`, or `quality-review` |
| `verdict` | string | yes | `ACCEPT`, `REVISE`, or `BLOCKED` |
| `reviewed_commit` | string | yes | Pinned full commit SHA |
| `reviewed_boundary` | object | yes | Non-recursive identity of the candidate: `base_commit`, nullable `candidate_commit`, `diff_sha256`, `artifact_hashes`, and `owned_paths` |
| `reviewed_artifacts` | array of strings | yes | Every artifact and diff evidence reviewed |
| `findings` | array | yes | Finding objects; empty for a clean acceptance |
| `summary` | string | yes | Concise evidence-based conclusion |

Each finding contains `id`, `severity`, `category`, `artifact`, `location`, `affected_requirement`, `evidence`, and `correction_outcome`. Severity is exactly `CRITICAL`, `HIGH`, `MEDIUM`, or `LOW`. An `ACCEPT` verdict cannot contain a Critical/High finding or a violated Constitution MUST.

`reviewed_commit` always names an existing immutable commit: the committed candidate when one exists, otherwise the current `HEAD` that is the review base. For an uncommitted candidate, `candidate_commit` is null and the content is pinned by a hash of the exact binary diff plus hashes of every reviewed artifact and the explicit owned-path set. Verdict files and workflow-state files are excluded from that diff identity, so recording a verdict cannot recursively change what was reviewed. After any tracked file in the boundary changes, including an E2E test, the old verdict no longer applies.

A schema-valid `ACCEPT` from the independent role assigned to the gate is sufficient stage-approval evidence. It does not require a second `human-approval` evidence object. Human-approval evidence remains required for decisions reserved to the operator contract: blocking analysis remediation; unresolved intent/scope or three failed revision outcomes; Strong architecture recommendations or ADR conflicts; divergent/destructive VCS recovery; inseparable worktree ownership; unavailable required infrastructure; unsafe external actions; and ambiguous E2E failures.

## Analyze Report

The read-only `artifact-analyst` returns the verdict contract with `gate = "analyze"`, plus `coverage_metrics` and hashes of `spec.md`, the complete plan set, and `tasks.md`. `ACCEPT` means no Critical/High finding and advances without duplicate user approval. `REVISE` means blocking artifact findings exist; the report is persisted unchanged and remediation requires explicit user approval. `BLOCKED` means analysis could not complete because inputs, authority, or tooling are unavailable.

`review_iterations.analyze` increments for every completed report. An approved remediation invalidates affected downstream artifacts, then analysis reruns as a new iteration. Three `REVISE` outcomes at the same source scope stop for user direction; the workflow never attempts a fourth remediation automatically. `BLOCKED` stops immediately and is not a revision attempt.

Example:

```json
{
  "schema_version": "1.0",
  "gate": "spec-review",
  "verdict": "ACCEPT",
  "reviewed_commit": "0123456789abcdef0123456789abcdef01234567",
  "reviewed_boundary": {
    "base_commit": "0123456789abcdef0123456789abcdef01234567",
    "candidate_commit": null,
    "diff_sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "artifact_hashes": {"specs/004-codegraph-code-unit-extraction/spec.md": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"},
    "owned_paths": ["specs/004-codegraph-code-unit-extraction/spec.md"]
  },
  "reviewed_artifacts": ["specs/004-codegraph-code-unit-extraction/spec.md"],
  "findings": [],
  "summary": "All blocking criteria are satisfied."
}
```

## Dispatch

Every dispatch prompt contains exactly these seven numbered slots:

1. `role_and_objective`: role and one bounded objective.
2. `authoritative_inputs`: repository paths to inputs; conclusions are not copied in place of sources.
3. `workflow_boundary`: current stage, BMad story key, feature directory, source commit, and diff boundary.
4. `constraints_and_skills`: applicable repository constraints and required installed skill names.
5. `write_boundary`: permitted writes and prohibited adjacent work.
6. `output_and_evidence`: expected output schema, artifact destination, and evidence requirements.
7. `completion_and_failure`: done conditions plus failure, conflict, and escalation behavior.

No extra slot may smuggle in another agent's reasoning. A judge receives raw artifacts and pinned authorities. A correcting author or implementer additionally receives the raw finding object because correction is its explicit objective.

## Evidence

Every durable claim references an evidence object:

| Field | Type | Contract |
|---|---|---|
| `evidence_id` | string | Unique within the story, formatted `story-key/stage/sequence` |
| `kind` | string | `command`, `test-red`, `test-green`, `review`, `analysis`, `artifact-hash`, `e2e-decision`, `human-approval`, `architecture-disposition`, or `failure` |
| `stage` | string | Owning exact workflow stage |
| `source_commit` | string | Full SHA that bounds the evidence |
| `artifact_paths` | array | Repository-relative inputs/outputs; temporary architecture paths are explicitly marked temporary |
| `command` | string or null | Exact local command when applicable |
| `exit_code` | integer or null | Captured result when applicable |
| `observed_at` | string | UTC RFC 3339 time |
| `summary` | string | Concise observed result, not an unsupported success claim |
| `raw_output_path` | string or null | Durable raw output location when retained |

Red evidence states the expected missing behavior and observed failure. Green evidence identifies the same behavior and passing targeted/surrounding tests. Review and analysis evidence link to their complete verdict/report, not only a prose summary.

## Epic Architecture Evidence

The canonical durable record is `docs/bmad/implementation-artifacts/epic-gates/<bmad-epic-key>/architecture-gate.json`. The root orchestrator is its sole atomic writer. It contains `epic_key`, `source_commit`, `report_temporary_path`, `report_sha256`, `report_verified_at`, `audit_tool`, `complete_findings`, `dispositions`, `build_evidence_ids`, `test_evidence_ids`, `audit_iteration`, and `retrospective_decision`. Each complete finding preserves ID, strength, affected paths/context, evidence, ADR relationship, and recommendation text. Each disposition preserves its finding ID, decision, load-bearing rationale or tracked-decision reference, user-approval evidence ID, remediation commits, and status.

The HTML report remains untracked in the OS temporary directory. While it exists, the orchestrator hashes it and verifies that the durable `complete_findings` payload accounts for every report finding. Resume derives the canonical record path from the epic key and validates its checkpoint binding, source `HEAD`, report verification, dispositions, and evidence. If the temporary file later disappears, the audit stays valid only when the durable payload is complete, the stored hash/source commit was verified, and all required dispositions remain auditable. Otherwise the root invalidates the record and reruns the audit. Any remediation commit changes `HEAD`; the root invalidates build/test and audit evidence and requires full build/tests plus a new architecture audit and dispositions at the new `HEAD`.

## Checkpoint ID and Commit Binding

A checkpoint ID is unique and stable, formatted:

```text
bmad-<story-or-epic-key>-<stage>-<UTC-basic-timestamp>-<six-hex-random>
```

Example: `bmad-4-1-analyze-20260711T031500Z-a1b2c3`.

Before commit, the applicable state/activation/epic-gate record stores the ID, stage, intended paths, and a null commit SHA. The root stages only owned paths, verifies them, and commits story checkpoints with these trailers:

```text
BMad-Story: 4-1-codegraph-integration-and-code-unit-extraction
Workflow-Scope: story/4-1-codegraph-integration-and-code-unit-extraction
Workflow-Stage: analyze
Workflow-Checkpoint: bmad-4-1-analyze-20260711T031500Z-a1b2c3
```

Epic checkpoints use `BMad-Epic: <epic-key>` and `Workflow-Scope: epic/<epic-key>` instead of `BMad-Story`. A commit never claims a story scope for an epic checkpoint or vice versa.

The committed state intentionally contains `commit_sha: null` (or `binding_status: "pending"`). The commit trailer binds the stable ID to the commit without self-reference. After commit, the orchestrator and resume derive the immutable SHA by locating exactly one matching `Workflow-Checkpoint` trailer and verifying its ancestry and contents. A cached `commit_sha` may appear in later state, but the trailer-derived SHA is canonical and the checkpoint commit is never amended merely to store itself. Checkpoint records include `checkpoint_id`, `scope_key`, `stage`, `committed_paths`, `verification_evidence_ids`, nullable cached `commit_sha`, and `binding_status`.

For the final checkpoint, the committed state may name stage `complete` with a pending binding. Completion becomes valid only after the commit succeeds, the unique trailer resolves to the current final checkpoint ID, its SHA is an ancestor of `HEAD`, and its committed paths contain the BMad `done` transition. Resume derives this binding before trusting `complete`.

## Checkpoint Content Contract

| Checkpoint | Preconditions | Owned commit contents |
|---|---|---|
| BMad story prepared | Validated story context and `backlog → ready-for-dev` | Story file and sprint-status transition |
| BMad story activated | Preflight/context accepted; story is `ready-for-dev` | Canonical activation record, story development record, sprint-status `ready-for-dev → in-progress`, parent epic `backlog → in-progress` when applicable, and activation checkpoint ID; no unaccepted SpecKit artifact |
| Specification accepted | Fresh spec judge returns `ACCEPT` | `spec.md`, checklist, mapping/state, and gate evidence |
| Plan accepted | Fresh plan judge returns `ACCEPT`; Constitution gates pass | Complete planning artifact set and gate evidence |
| Tasks analyzed | Latest read-only analysis has no Critical/High findings | `tasks.md`, persisted analysis evidence, and workflow state |
| Task group verified | Red-green-refactor evidence exists; targeted tests pass | One cohesive implementation increment and its tests |
| Review correction verified | Affected fresh review passes | Regression test, correction, and updated review evidence |
| Story ready for review | All implementation task groups are verified | Story record and sprint-status `in-progress → review` at the pinned implementation candidate |
| Story completed | Both reviews accepted, required E2E passes or has a valid coverage override, and full verification passes | Story record, completed task/state metadata, and BMad `review → done` transition |
| Epic gate completed | Latest-HEAD architecture findings dispositioned; build/tests and audit pass; retrospective run or explicitly declined | Complete durable architecture record, retrospective/status artifacts, and BMad epic `in-progress → done` transition |

Adjacent artifact-only checkpoints may be combined only when they provide no useful recovery point. Unaccepted artifacts cannot share a commit with implementation, and unrelated stories never share a commit. The temporary HTML architecture report, rejected work, failing tests, secrets, and unrelated user edits are never committed.
