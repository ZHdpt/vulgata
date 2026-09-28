# Governed Delivery Artifact Contracts

All durable artifact, ownership, and authority paths are repository-relative with `/` separators. Only checkout identity paths may be canonical absolute paths. Timestamps are UTC RFC 3339; commits are full SHAs.

## Compact Workflow State 3.1

Each feature owns `specs/<feature>/workflow-state.json`. It is a current resume snapshot capped at 12 KiB of canonical JSON, not a duplicate evidence ledger.

| Field | Contract |
|---|---|
| `schema_version` | `3.1` for new/active adopted states; historical `3.0` retains original validation |
| story/epic/feature identity | immutable one-to-one BMad and SpecKit mapping |
| `stage` | one unified stage from state-machine.md |
| `preflight` | branch, HEAD, hashes of Git identity and observed status, observation time |
| `artifacts` | current spec/plan/tasks repository-relative path and SHA-256 |
| `gates` | current plan/definition/quality status, attempt, and verdict pointer |
| `recovery` | nullable current episode, failed-result count, finite cumulative pause limit, closing gate, owner/action, outstanding authority/blocked holds, latest result/diagnosis descriptors, extension decision reference |
| `transition_head` | latest root-owned transition evidence path/hash, retained after recovery closure |
| `definition_author` | `{profile, escalation_decision_id}`; selected ordinary/recovery profile retained through completion |
| `lookback` | `{status, evidence}`; status pending/current/invalidated, evidence null or `{path, sha256}` |
| `approvals` | at most 12 current decision pointers: ID, scope hash, status, time, evidence path |
| `convergence` | current status, attempt, outcome, and report pointer |
| `e2e` | current result and decision pointer |
| `current_evidence_paths` | at most 12 unique paths needed to resume or audit the current boundary |
| `checkpoint` | current pending/bound checkpoint identity and optional cached commit SHA |
| `resume` | nonterminal stage, concise reason, and optional evidence pointer |
| `updated_at` | atomic state-write time |

Accepted/revised/blocked gates point to their raw verdict. Completed decisions and E2E results point to durable evidence. A clean convergence result points to its report. Full verdict bodies, command output, dispatch prompts, historical transitions, and superseded boundaries remain in their native review/evidence/development artifacts and Git. Completion requires an effectively bound complete checkpoint and no resume instruction; a persisted pending binding is effective only after repository validation derives its unique ancestral trailer commit.

3.1 removes the historical top-level `attempts` map; gate/convergence `attempt` remains the raw result sequence. Initial substantive failure counts 1, then failed repairs 2/3/4; diagnose at 3 and pause at 4. Input invalidation, phase/profile changes, expected RED, technical failure, BLOCKED, and intermediate ACCEPT never reset recovery. A paused snapshot is valid and resumable, but the targeted dispatch guard enforces its stop.

Each root result/evidence envelope has a stable identity, monotonic story transition sequence, prior transition-head descriptor, and compact resulting recovery/profile/action projection. Preserve raw worker/judge output distinctly and unchanged. Reject out-of-order results, count decreases, wrong-episode extensions, unauthorized profile changes, and projection mismatch. Redelivery of the current head is a no-op; old episodes cannot reopen through replay. Closure advances the head and includes required accepted/verified evidence while setting recovery null. Normal resume reads the head and necessary predecessor/acceptance references, not the entire ledger. Never duplicate complete prior workflow states in evidence.

The ordinary analysis envelope's `transition` is `{story_key, sequence, previous_head, event, projection: {recovery, definition_author}}`; head references use `{path, sha256}`. Negative-result envelopes bind `observed_after` to the head of their issued dispatch and keep `raw_result` as the unchanged original structured output. Apply the existing verdict schema to that raw review value. Root classification metadata never overwrites raw verdicts. New failure must match the currently issued result boundary; repackaging an old verdict cannot reopen a closed episode. Escalation decisions are root analysis evidence, not an additional human approval; independently required scope/architecture/budget decisions still apply.

`recovery.holds` retains unique outstanding `authority`/`blocked` restrictions independently of its current action. Action precedence is pause limit, blocked hold, authority hold, due diagnosis, then ordinary routing. Diagnosis, extension, and ordinary ACCEPT do not clear holds. An accepted result clears only its named `resolved_holds` with root-validated resolution evidence; an episode cannot close while a hold remains.

Technical operation identity, root invocation count (maximum three including original), and latest result belong to existing failure/transition evidence as `technical_retry`, referenced by `resume` and retained until success, not a new state ledger. Active 3.0 adoption inspects bounded existing recovery evidence and preserves known/uncertain allowance instead of assuming erased counters mean zero. Completed 3.0 checkpoints are never rewritten or retrofitted.

### Result and transition fields

Root's transition helper adds `invalidation_policy: "earliest-owner-v1"` to fresh invalidation events. Explicit unsupported values are rejected. Historical unmarked invalidation receipts retain their original projection semantics when replayed; fresh proposals cannot select that legacy behavior. Exact-head replay remains a no-op only for the unchanged immutable record. This discriminator is event metadata, not a new workflow-state field or a history rewrite.

Use `{path, sha256}` for every evidence descriptor below. Each non-initial `event` has `kind` and `result` pointing to existing raw outcome/analysis evidence, not its own transition file. Failure, acceptance, and corrective self-review evidence wrap `observed_after` and unchanged `raw_result`; the head must match the issuing dispatch. A negative raw result is REVISE, a non-converged governed outcome, or an observed nonzero command exit. An accepted result must be a real full-schema ACCEPT, clean convergence, passing required check, or named hold resolution; changing an event label is insufficient. `previous_head` and `sequence` belong to the enclosing transition. `operator-runbook.md` shows activation and CLI persistence.

| Event `kind` | Additional fields and evidence |
|---|---|
| `failure` | `owners`: currently invalid owners from `spec`, `plan`, `tasks`, `implement`, `tests`, `authority`, `blocker`, `uncertain`; `episode_id` when opening. Subsequent failures also need `repair: {obligation, correction, previous_result}`: a nonempty obligation, correction-evidence descriptor, and current `recovery.last_result`. `production_started: true` promotes the closing gate. |
| `diagnosis` | `diagnosis: {evidence, at_count}` with current `failed_results`; `owners` selects the resulting route. Evidence is ordinary `kind: analysis` with a nonempty `summary` stating failure, assumption, changed approach, and next check. |
| `phase_complete` | `phase:"spec"|"plan"|"tasks"` matches the current corrective owner. Fresh wrapped author evidence has `kind:"self-review"`, stage `specify|plan|tasks`, `outcome:"passed"`, nonempty `summary`, and current `input_hashes` covering that phase's artifact. It advances spec -> plan -> tasks -> definition review without an extra phase judge, accepting a gate, closing recovery, or changing its count/profile. Pause, holds, and due diagnosis still block. |
| `extension` | `scope_sha256` plus `decision: {decision_id, status:"accepted", episode_id, scope_sha256, pause_at}`. The finite limit must exceed both count/current limit. Decision fields must match durable JSON evidence and a current accepted entry in `state.approvals`; no reuse of the previous extension ID. A new diagnosis follows extension. |
| `escalation` | `profile` and `decision: {decision_id, episode_id, scope_sha256, trigger, evidence}`. The referenced root analysis repeats decision ID, episode, scope, trigger, and causal `summary`; it is not a new human approval. Medium triggers: `unresolved_definition_reasoning` or `uncovered_consequential_design`. High follows medium only, with `interacting_design_constraints` or `failed_medium_definition_reasoning`. Current diagnosis must name spec/plan and cover the current count; pause/holds still block. |
| `accepted` | Records an intermediate accepted result without closing recovery. Optionally `resolved_holds` names unique current holds and `resolution` references explicit accepted/resolved evidence with `summary`; authority resolution additionally binds an accepted user decision. |
| `invalidation` | Record the actual changed boundary without increment/reset. Optional `owner` is exactly `spec`, `plan`, `tasks`, `implement`, or `tests`. An active episode selects the earlier of its unresolved owner and the declared owner. An explicit material invalidation retires the prior diagnosis even if the owner stays the same; counts/limits/holds/author and required pause/diagnosis remain. Without recovery, route to the declared owner without inventing a failure. Absent `owner` preserves existing behavior. Silence on an optional question is not an invalidation. |
| `expected_red`, `technical_failure`, `blocked`, `production_started` | Record their actual result without increment/reset. `blocked` retains its independent hold; production entry promotes closure to `verify`. |
| `close` | Current `episode_id` when active (omit for happy-path closure), and `proofs` mapping gate names to descriptors. Every closure needs `definition-review` and `plan-review`; post-production/happy-path final closure also needs `converge`, `quality-review`, `e2e`, and `verify` (six proofs total). No outstanding hold may remain. |

Closure proofs must match current accepted gate/report/decision paths and hashes. Independent review proof remains the full original verdict (or unchanged `raw_result` inside its wrapper). Use nonempty `input_hashes: {repository_path: sha256}` with `owned_paths`, or the review's `reviewed_boundary.artifact_hashes/owned_paths`; cover every owned/required path and applicable current definition artifacts. Plan-only proof covers spec/plan. At final closure, preserve the accepted definition's historical tasks hash for mechanical checkbox/convergence updates; current tasks are proved by convergence/quality/verification. When no early plan review was required, its proof is `{disposition:"not-required", reason, input_hashes, owned_paths}` and the current plan gate must be pending/invalidated with non-architecture classification; never fabricate an ACCEPT. Convergence has `outcome:"converged"`. E2E has `stage:"conditional-e2e"`, its current passing/skipped `result`, current manifest, and either command descriptors for a pass or a `reason` for skip. Accepted-result proposals update their identified gate/convergence/E2E status, and failure proposals invalidate affected downstream evidence. The root still owns stage changes, artifact mapping, and all persisted writes.

Final verification is an aggregate of actual required command evidence, not an exit-code assertion alone:

```json
{
  "kind": "analysis",
  "stage": "verify",
  "outcome": "passed",
  "source_commit": "<quality-reviewed full commit SHA>",
  "input_hashes": {"<owned/current input path>": "<SHA-256>"},
  "owned_paths": ["<owned/current input path>"],
  "commands": {
    "build": {"path": "<build evidence>", "sha256": "<SHA-256>"},
    "test": {"path": "<full-test evidence>", "sha256": "<SHA-256>"},
    "focused": []
  },
  "focused_reason": "<why no separate focused command is required, only when the list is empty>"
}
```

Each command proof has `kind:"command"`, stage `verify-build|verify-test|verify-focused`, exact `command`, `source_commit`, UTC `observed_at`, integer `exit_code:0`, `raw_output_path`, `raw_output_sha256`, current `input_hashes`, and `owned_paths`. Build/test command strings match configured commands; all source identities match quality's reviewed commit and manifests cover its required owned paths. Focused entries reference real command proofs; when none apply, explain why. Passed E2E similarly references actual `conditional-e2e` command evidence. Use already-required runs; this record adds no command ceremony.

### Technical operation receipts

Alongside the transition, an optional top-level `technical_event` is `{kind, operation:{command,target,cause}, result}`. Each identity string is nonempty and stable. Persist a root invocation receipt with `kind:"invoke"` before issuing its one operation; then record exactly one `success`, `transient_failure`, or `fatal_failure` result receipt for that running invocation. The CLI derives `technical_retry: {operation, invocations, status, last_result}` in the same transition record and retains it across unrelated transitions. Status is `running`, `transient`, `fatal`, or `repaired`; success clears the projection. A running invocation, fatal failure, or third invocation blocks another call. Do not count hidden SDK attempts.

`external_repair` additionally requires `repair_evidence` containing independently observed `status:"resolved"` and `summary`; it cannot reset an in-flight operation. It establishes zero invocations in `repaired` status for the same identity. Never change command/target/cause to evade an unresolved allowance. The surrounding recovery event can be `technical_failure` with the same result descriptor to preserve the substantive budget. `resume.evidence_path` points to the current failure/transition evidence when stopped. No separate retry state file or history array is needed.

## Story/Feature Mapping and Activation

The mapping is two-way and one-to-one between workflow state and the BMad development record. Before SpecKit creates the directory, the root owns the canonical activation record under `docs/bmad/implementation-artifacts/workflow-activations/`. After specify returns the actual directory, the root writes both mapping sides atomically and marks activation superseded. Missing, conflicting, duplicate, or many-to-one mapping stops.

From 0.7.4, new directories are `specs/<full-canonical-bmad-story-key>`, preserving the epic and story numbers plus lifecycle slug. Existing mappings, including sequential names and completed stories, stay unchanged. The read-only selection helper checks current state mappings and story-number directory collisions; it is not an activation lock or completion validator. Root also reconciles the canonical activation/development record, validates resume eligibility, and verifies SpecKit returned exactly the selected directory before accepting the mapping. See `operator-runbook.md`; never use a numeric allocator or guess a new suffix on conflict.

## Verdict

Independent reviews use `verdict.schema.json` with gate `plan-review`, `definition-review`, or `quality-review`. Verdict is `ACCEPT`, `REVISE`, or `BLOCKED` and binds the reviewed commit, nonrecursive diff identity, artifact hashes, owned paths, raw artifacts, findings, and summary.

Definition findings require `earliest_owner: spec|plan|tasks`. Quality findings use `earliest_owner: spec|plan|tasks|implement`. ACCEPT cannot contain a blocking finding, Critical/High severity, or Constitution MUST violation. Persist every verdict unchanged; do not duplicate it as a second evidence ledger.

Author self-review is an evidence object with `kind: self-review`; it never satisfies an independent gate.

## Convergence

Require implementation-prerequisite evidence before convergence; known unfinished tasks return to their owner first. Preserve raw upstream output and add a distinguishable governed disposition: `converged`, `tasks_appended`, `tasks_reused`, or `return_to_definition`, plus blocked/failed outcomes. Each result carries checked obligations/findings, source/behavior/defect references, unchanged raw findings, reused/appended IDs, before/after tasks hashes, and applicable earliest owner/authority decision.

The governed result uses exact fields `outcome`, `reused_task_ids`, `appended_task_ids`, `tasks_before: {path,sha256}`, `tasks_after: {path,sha256}`, and `owner: "spec"|"plan"|"tasks"` for `return_to_definition`. Before dispatch, root saves/hashes an immutable task preimage under the existing evidence convention; `tasks_before.path` differs from the live task path. `tasks_after` equals the current mapped `state.artifacts.tasks` descriptor after root validation. A clean result also has nonempty current `input_hashes`. Preserve upstream output separately inside the existing result envelope; do not relabel governed dispositions as upstream-native fields. Negative-result wrappers retain `observed_after` and the full original structured result, including these governed fields, as `raw_result`.

Match against unfinished tasks by obligation, behavior, and defect evidence. A shared requirement ID alone does not collapse different defects; a real regression after completed work needs a traceable correction. Mixed reuse/append is `tasks_appended`; reuse alone remains non-converged. Definition/authority discovery returns upstream before speculative append. The only analyst write is append-only Convergence tasks preserving every prior byte/ID; description/dependency corrections belong to the active definition author and mechanical completion markers remain root-owned. Mechanical checks prove append-only shape, actual unique task references, prerequisites, and legal route, not complete semantic equivalence. No additional convergence schema or upstream source mutation is required.

## Story Lookback

`lookback` evidence uses existing `kind: analysis` with stage `lookback`. Bind current convergence/product-and-definition evidence, exactly one genuine `## Story Lookback` section by record path/section hash, touched lesson/project outputs, relevant approved decisions, disposition, and unresolved items. An evidence path without verified contents is insufficient. See `story-lookback.md` for admission and block-aware section rules.

The ordinary proof fields are:

```json
{
  "kind": "analysis",
  "stage": "lookback",
  "evidence_id": "<story key>/lookback/1",
  "record_path": "<existing development record>",
  "section_sha256": "<SHA-256 of extracted section UTF-8>",
  "record_content_sha256": "<SHA-256 of non-bookkeeping record projection>",
  "convergence": {"path": "<current report>", "sha256": "<report SHA-256>"},
  "output_hashes": {},
  "disposition": "no-new-lesson",
  "authority_decisions": [],
  "unresolved_blockers": [],
  "follow_up_paths": []
}
```

Replace placeholders with actual repository-relative paths/hashes. The convergence result must expose `outcome:"converged"` and nonempty current `input_hashes`, including mapped spec/plan/tasks and affected product inputs. A wrapper stores these only inside unchanged `raw_result`; the lookback reader unwraps it consistently without duplicated outcome/hash fields. `output_hashes` maps touched project outputs and the configured lessons file when it exists, even when no lesson changed. It may be empty when no lessons/output file exists; `disposition:"updated"` requires at least one output. `authority_decisions` contains descriptors matching accepted state approvals and JSON decision evidence with `status:"accepted"`. `follow_up_paths` names existing nonblocking records; unresolved authority/blocker items must be empty. Bind the completed proof in `lookback: {status:"current", evidence:{path,sha256}}` and run the guard before quality.

Quality owns and pins the proof, development record, and affected outputs. A changed source, meaningful record content, section, proof, or output invalidates affected current review. `record_content_sha256` hashes `record_content_projection`: preserve record content except its single real `## Workflow Bookkeeping` section and recognized standalone lifecycle `Status:` line; duplicate reserved headings are invalid and examples in fences/comments are not exclusions. Keep all product intent outside this reserved section. A later whole-record byte mismatch is allowed only when both this pinned content projection and `section_sha256` still match. Unresolved authority or required unsynchronized decisions prevent current proof. Completion validates proof/inputs/outputs from its commit; future lesson edits do not invalidate history. Exclude state/checkpoint/proof recursion, never arbitrary story content.

## Dispatch

Every prompt contains exactly seven numbered slots:

1. role and objective;
2. authoritative raw inputs;
3. workflow and pinned source boundary;
4. constraints and required skills;
5. exact write boundary;
6. output and evidence contract;
7. completion and failure behavior.

A judge receives raw artifacts, not author conclusions. Definition-review follow-ups also receive ordered prior raw verdict paths; a recovery replacement receives the same complete history package. A correction receives unchanged raw findings and returns to the retained definition author when available. Runtime-local agent handles are never durable state. Test and production ownership are disjoint.

## Evidence

Evidence records bind an ID, kind, stage, source commit, artifact paths, command/result where applicable, observation time, summary, and optional raw output. Kinds include command, self-review, test-red, test-green, review, analysis, convergence, artifact-hash, E2E decision, human approval, architecture disposition, and failure. Evidence IDs use `<story>/<stage>/<positive integer>`.

## Epic Architecture Evidence

Executable contracts live beside the story schemas as `epic-command-evidence.schema.json`, `epic-architecture-gate.schema.json`, and `retrospective-state.schema.json`. The repository validator also enforces cross-field semantics that JSON Schema cannot express portably, including source/checkout equality, independent-`ACCEPT` reserved-count rules, non-self-referential gate manifests, and completed-retrospective binding.

The canonical durable record is `docs/bmad/implementation-artifacts/epic-gates/<bmad-epic-key>/architecture-gate.json`; the root is its sole atomic writer. It contains `epic_key`, status/phase, full source/preflight target preimages, separate build/audit boundaries and disposal, audit/evidence/dispositions, retrospective choice, checkpoint, typed non-self-referential manifest constraints, resume data, and failure. The gate descriptor includes `semantic_projection_sha256`, computed from the complete canonical gate after replacing only that field's value with JSON `null`; it never includes the gate blob OID. Each worktree is removed and recorded disposed before `pending-binding`.

The root runs workflow-owned `$audit-governed-architecture`, which applies the configured architecture-review adapter under a read-only close boundary. The supported adapter combines installed `codebase-design` vocabulary with applicable `bmad-review` lenses. Canonical raw JSON preserves the complete ordered tool envelope, exact structured-result block index, and parsed value. Runtime validation reparses it, requires durable verdict/candidate equality, compares audit-root/Git/tree/index/input proofs, and rederives byte-range citations. No self-asserted mapping or citation boolean is sufficient.

`audit_validation` records verdict, allowed-vocabulary/schema validation, raw-response path/hash and one-to-one mapping, citation blob/excerpt verification, normalization hash, embedded/extracted/visible-card canonical hashes and field equality, exact counts/ID sets, source equality, and timestamp. Strength is exactly `Strong`, `Worth exploring`, or `Speculative`; severity is mandatory and exactly `CRITICAL`, `HIGH`, `MEDIUM`, or `LOW` under the documented impact rubric. Missing/invalid classification, Critical/High, violated MUST, Strong, and ADR conflict are blocking. The independent worker supplies severity and every applicable category; the root cannot weaken them. An `ACCEPT` is valid only with no reserved finding. Visible cards are generated from the normalized payload and reproduce every field.

Each disposition preserves finding ID, all applicable categories, one subdecision per category, rationale, source-bound evidence, user approval, and status. Every category constraint passes with Critical/High/MUST precedence. A current audit that still contains such a finding permits only `evidence-backed-false-positive`; corrective resolution is represented by a fresh audit in which that finding is absent, never a self-asserted `resolved` disposition. Nonsevere Strong permits load-bearing rejection, tracked deferral, or known-debt acceptance. ADR conflict permits conformance, an already committed superseding decision plus fresh audit, or block.

The HTML report remains untracked in OS temp. The root verifies hash, normalized payload, source, exact count, and ID-set equality while it exists. A later-missing report remains usable only when this complete validation is durable. Active `HEAD`, clean verification checkout, and audit source are compared before audit acceptance, disposition, staging, and commit; inequality invalidates the gate and restarts eligibility/verification.

If the active boundary is dirty for build/audit-affecting paths, verification uses a clean disposable detached checkout at the pinned SHA and records it in `verification_boundary`; dirty-boundary output is invalid. Production/test remediation is never a close-skill commit: BMad correct-course creates/selects a corrective story and `$run-governed-delivery` completes it before close restarts. The retrospective decision is `run-after-close` or `declined`; a scheduled retrospective runs only after final binding in a separate bounded workflow.

A scheduled retrospective uses separate state created after epic binding. It stores the bound epic checkpoint, validated hooks, safe noncolliding paths and their canonical path hash, plus a pending checkpoint ID/expected parent. Its commit trailer carries the full canonical parent-to-commit change digest. Unique trailer, direct parent, digest, and changed-path verification derive effective completion; the committed state remains `pending-binding` and never self-stores its commit SHA. Failure does not reopen the epic.

The durable manifest is the complete parent-to-result change set with rename detection disabled. Each entry is `{path,status,old_mode,old_type,old_oid,new_mode,new_type,new_oid}` with `null` for an absent side; deletions are explicit tombstones. Paths are Unicode NFC, duplicate/case-colliding paths are rejected, entries sort by UTF-8 path bytes then status, and the array uses canonical JSON bytes. The same representation is derived from staged index vs `HEAD` before commit and direct parent vs commit during recovery. Index stage numbers remain precommit safety evidence, not digest fields.

On every stop, the root records state except when pending-binding recovery encounters a mixed user/workflow index: that case preserves all bytes and reports the conflict without writing. Recovery verifies the direct-child commit and `Workflow-Manifest-SHA256` against the canonical change set; success derives `effective_phase = bound` while the committed gate remains pending. Other retryable failures move to `retrying` only after cause/source/preflight revalidation.

## Checkpoint ID and Commit Binding

A checkpoint ID is unique and stable, formatted:

```text
bmad-<story-or-epic-key>-<stage>-<UTC-basic-timestamp>-<six-hex-random>
```

Example: `bmad-4-1-definition-review-20260818T031500Z-a1b2c3`.

Before commit, the applicable state/activation/epic-gate record stores the ID, stage, intended paths, and a null commit SHA. The root stages only owned paths, verifies them, and commits story checkpoints with these trailers:

```text
BMad-Story: 4-1-codegraph-integration-and-code-unit-extraction
Workflow-Scope: story/4-1-codegraph-integration-and-code-unit-extraction
Workflow-Stage: definition-review
Workflow-Checkpoint: bmad-4-1-definition-review-20260818T031500Z-a1b2c3
```

Epic checkpoints use `BMad-Epic: <epic-key>` and `Workflow-Scope: epic/<epic-key>` instead of `BMad-Story`. A commit never claims a story scope for an epic checkpoint or vice versa.

The committed state intentionally contains `commit_sha: null` while `binding_status` is `pending`. The commit trailer binds the stable ID to the commit without self-reference. After commit, the orchestrator and resume derive the immutable SHA by locating exactly one matching `Workflow-Checkpoint` trailer and verifying its ancestry and contents. A cached `commit_sha` may appear in later state, but the trailer-derived SHA is canonical and the checkpoint commit is never amended merely to store itself. The compact state keeps only `checkpoint_id`, `stage`, nullable cached `commit_sha`, and `binding_status`; detailed path and verification manifests stay in checkpoint evidence and Git.

For the final checkpoint, the committed state may name stage `complete` with a pending binding. Completion becomes valid only after the commit succeeds, the unique trailer resolves to the current final checkpoint ID, its SHA is an ancestor of `HEAD`, and its committed paths contain the BMad `done` transition. Resume derives this binding before trusting `complete`.

## Delegated decision evidence

Available from distribution 0.7.3 for review gates and 0.7.4 for recovery extensions; human approval stays default. Keep the existing state `approvals` pointer unchanged. The runner records the user's designation in an ordinary story-local decision JSON artifact with `kind: "user-delegation"`, `status: "accepted"`, the stable `delegate_id`, exact `story_key`, `gates` (a nonempty subset of `plan-review`, `implementation-approval`, `recovery-extension`), `scope` (including conditions), `user_source` (original message locator), and `user_instruction` (actual text). A grant including `recovery-extension` also requires integer `max_pause_at > 4`, explicitly set by the user as the cumulative per-episode ceiling. This is a record of the user's instruction, not a generated charter or inferred delegation. Root must verify its real source and continued applicability; a revoked/expired designation must not be used.

The gate's existing decision artifact retains `decision_id`, `stage`, `status`, `scope_sha256`, and `summary`. For a delegated review-gate decision add:

- `kind: "delegated-approval"`, `delegate_id`, and `story_key`;
- `delegation: {path, sha256}` referencing the actual user-designation record;
- `response_source` and `response_text` preserving the delegate's real response;
- `review: {path, sha256}` referencing the current independent ACCEPT: plan review for planned-change authorization, definition review for implementation approval. Its existing boundary contains the accepted artifact hashes; do not duplicate that manifest.

For these decisions, compute `scope_sha256` with the existing `workflow_runtime.digest(canonical_bytes({"story_key": story_key, "stage": stage, "review": review}))`. Store that value in both the decision and its existing approval pointer. The validator checks identity/story/gate consistency, recorded source presence, referenced hashes, the current accepted review/artifact boundary, and this scope digest. Human/legacy approval records retain their original interpretation. Post-implementation task progress uses existing review-proof allowances, not a new approval for every checkbox. These checks do not authenticate chats or infer the meaning of user scope; root remains responsible for both. The validator never writes records.

For an extension, reuse the same identity/delegation/response fields, with `stage: "recovery-extension"`, `episode_id`, proposed integer `pause_at`, and nonempty `changed_approach`. Instead of `review`, record `recovery_boundary` with exactly the current recovery's `episode_id`, `failed_results`, `pause_at`, `last_result`, plus the current `transition_head` and `artifacts` map. Compute the scope as `digest(canonical_bytes({"story_key": story_key, "stage": "recovery-extension", "recovery_boundary": recovery_boundary, "pause_at": pause_at, "changed_approach": changed_approach}))`. Bind it in the decision, existing approval pointer, and existing extension event. Do not add fields to workflow state or its event decision shape.

The extension validator requires a paused episode, current failure/head/artifact binding, actual designation/response metadata, and a higher finite limit no greater than the user's `max_pause_at`. Reached later limits require new decisions; changing phase, inputs, or model never resets count or ceiling. Extension still invalidates the old diagnosis and preserves all holds. Historical transition replay checks its original recovery boundary without demanding later corrected artifact bytes match it; new extensions always check current bytes. No passing independent review is required merely to authorize bounded recovery, and this decision cannot replace either review/implementation gate.

Install instructions and validators together before using delegation (0.7.4+ for extensions). Earlier validators must not be used for newer delegated evidence; no downgrade or relabeling as human evidence. State schema remains 3.1 and historical records require no migration. Delegated evidence cannot resolve holds or authorize other gates.

## Checkpoint Content Contract

| Checkpoint | Preconditions | Owned contents |
|---|---|---|
| Story activated | preflight and ownership valid | activation, BMad in-progress transition, state identity |
| Definition accepted | persistent author self-reviewed each phase; combined definition ACCEPT; any triggered early plan review/authorization durable | spec, plan set, tasks, mapping/state, ordered raw verdicts |
| Task group verified | honest RED/GREEN or truthful nonbehavioral evidence | one coherent production/test increment and task markers |
| Converged candidate | converge reports clean; lookback proof validated before quality | candidate identity, convergence/lookback evidence and outputs, BMad in-progress to review |
| Correction | invalidated downstream stages rerun and accepted | regression/correction and fresh evidence |
| Story complete | current lookback, quality ACCEPT, E2E decision, final verification, no unresolved recovery | state/evidence and BMad review to done transition |
| Epic complete | close-governed-epic contract passes | epic gate/evidence/disposition/status artifacts |

Adjacent definition artifacts share one checkpoint. Unaccepted artifacts never share an implementation checkpoint. Explicit owned paths only; never commit rejected work, failing tests, secrets, temporary reports, or unrelated edits.
