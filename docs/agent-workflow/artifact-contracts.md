# Sprint Loop Artifact Contracts

All durable artifact, ownership, and authority paths are repository-relative with `/` separators. Only checkout identity paths may be canonical absolute paths. Timestamps are UTC RFC 3339; commits are full SHAs.

## Compact Workflow State v3

Each feature owns `specs/<feature>/workflow-state.json`. It is a current resume snapshot capped at 12 KiB of canonical JSON, not a duplicate evidence ledger.

| Field | Contract |
|---|---|
| `schema_version` | `3.0` |
| story/epic/feature identity | immutable one-to-one BMad and SpecKit mapping |
| `stage` | one unified stage from state-machine.md |
| `preflight` | branch, HEAD, hashes of Git identity and observed status, observation time |
| `artifacts` | current spec/plan/tasks repository-relative path and SHA-256 |
| `gates` | current plan/definition/quality status, attempt, and verdict pointer |
| `attempts` | consecutive current-boundary REVISE/non-converged counters; reset after ACCEPT/converged or upstream-boundary replacement |
| `approvals` | at most 12 current decision pointers: ID, scope hash, status, time, evidence path |
| `convergence` | current status, attempt, outcome, and report pointer |
| `e2e` | current result and decision pointer |
| `current_evidence_paths` | at most 12 unique paths needed to resume or audit the current boundary |
| `checkpoint` | current pending/bound checkpoint identity and optional cached commit SHA |
| `resume` | nonterminal stage, concise reason, and optional evidence pointer |
| `updated_at` | atomic state-write time |

Accepted/revised/blocked gates point to their raw verdict. Completed decisions and E2E results point to durable evidence. A clean convergence result points to its report. Full verdict bodies, command output, dispatch prompts, historical transitions, and superseded boundaries remain in their native review/evidence/development artifacts and Git. Completion requires a bound complete checkpoint and no resume instruction.

## Story/Feature Mapping and Activation

The mapping is two-way and one-to-one between workflow state and the BMad development record. Before SpecKit creates the directory, the root owns the canonical activation record under `docs/bmad/implementation-artifacts/workflow-activations/`. After specify returns the actual directory, the root writes both mapping sides atomically and marks activation superseded. Missing, conflicting, duplicate, or many-to-one mapping stops.

## Verdict

Independent reviews use `verdict.schema.json` with gate `plan-review`, `definition-review`, or `quality-review`. Verdict is `ACCEPT`, `REVISE`, or `BLOCKED` and binds the reviewed commit, nonrecursive diff identity, artifact hashes, owned paths, raw artifacts, findings, and summary.

Definition findings require `earliest_owner: spec|plan|tasks`. Quality findings use `earliest_owner: spec|plan|tasks|implement`. ACCEPT cannot contain a blocking finding, Critical/High severity, or Constitution MUST violation. Persist every verdict unchanged; do not duplicate it as a second evidence ledger.

Author self-review is an evidence object with `kind: self-review`; it never satisfies an independent gate.

## Convergence

The convergence analyst returns `converged` or `tasks_appended`, counts checked obligations/findings, classifies each finding, identifies source references, lists appended task IDs, and hashes the resulting tasks file. Its only permitted write is the append-only Convergence section in `tasks.md`. An intent/authority defect is returned separately for earliest-owner routing.

## Dispatch

Every prompt contains exactly seven numbered slots:

1. role and objective;
2. authoritative raw inputs;
3. workflow and pinned source boundary;
4. constraints and required skills;
5. exact write boundary;
6. output and evidence contract;
7. completion and failure behavior.

A judge receives raw artifacts, not author conclusions. A correction receives unchanged raw findings. Test and production ownership are disjoint.

## Evidence

Evidence records bind an ID, kind, stage, source commit, artifact paths, command/result where applicable, observation time, summary, and optional raw output. Kinds include command, self-review, test-red, test-green, review, analysis, convergence, artifact-hash, E2E decision, human approval, architecture disposition, and failure. Evidence IDs use `<story>/<stage>/<positive integer>`.

## Epic Architecture Evidence

Executable contracts live beside the story schemas as `epic-command-evidence.schema.json`, `epic-architecture-gate.schema.json`, and `retrospective-state.schema.json`. The repository validator also enforces cross-field semantics that JSON Schema cannot express portably, including source/checkout equality, independent-`ACCEPT` reserved-count rules, non-self-referential gate manifests, and completed-retrospective binding.

The canonical durable record is `docs/bmad/implementation-artifacts/epic-gates/<bmad-epic-key>/architecture-gate.json`; the root is its sole atomic writer. It contains `epic_key`, status/phase, full source/preflight target preimages, separate build/audit boundaries and disposal, audit/evidence/dispositions, retrospective choice, checkpoint, typed non-self-referential manifest constraints, resume data, and failure. The gate descriptor includes `semantic_projection_sha256`, computed from the complete canonical gate after replacing only that field's value with JSON `null`; it never includes the gate blob OID. Each worktree is removed and recorded disposed before `pending-binding`.

The root runs workflow-owned `$audit-epic-architecture`, which applies the installed `improve-codebase-architecture` Explore/report process under a read-only close boundary. The canonical raw JSON preserves the complete ordered tool envelope, the exact structured-result block index, and parsed value. Runtime validation reparses it, requires durable verdict/candidate equality, compares audit-root/Git/tree/index/input proofs, and rederives byte-range citations. No self-asserted mapping or citation boolean is sufficient.

`audit_validation` records verdict, allowed-vocabulary/schema validation, raw-response path/hash and one-to-one mapping, citation blob/excerpt verification, normalization hash, embedded/extracted/visible-card canonical hashes and field equality, exact counts/ID sets, source equality, and timestamp. Strength is exactly `Strong`, `Worth exploring`, or `Speculative`; severity is mandatory and exactly `CRITICAL`, `HIGH`, `MEDIUM`, or `LOW` under the documented impact rubric. Missing/invalid classification, Critical/High, violated MUST, Strong, and ADR conflict are blocking. The independent worker supplies severity and every applicable category; the root cannot weaken them. An `ACCEPT` is valid only with no reserved finding. Visible cards are generated from the normalized payload and reproduce every field.

Each disposition preserves finding ID, all applicable categories, one subdecision per category, rationale, source-bound evidence, user approval, and status. Every category constraint passes with Critical/High/MUST precedence. A current audit that still contains such a finding permits only `evidence-backed-false-positive`; corrective resolution is represented by a fresh audit in which that finding is absent, never a self-asserted `resolved` disposition. Nonsevere Strong permits load-bearing rejection, tracked deferral, or known-debt acceptance. ADR conflict permits conformance, an already committed superseding decision plus fresh audit, or block.

The HTML report remains untracked in OS temp. The root verifies hash, normalized payload, source, exact count, and ID-set equality while it exists. A later-missing report remains usable only when this complete validation is durable. Active `HEAD`, clean verification checkout, and audit source are compared before audit acceptance, disposition, staging, and commit; inequality invalidates the gate and restarts eligibility/verification.

If the active boundary is dirty for build/audit-affecting paths, verification uses a clean disposable detached checkout at the pinned SHA and records it in `verification_boundary`; dirty-boundary output is invalid. Production/test remediation is never a close-skill commit: BMad correct-course creates/selects a corrective story and `$run-bmad-sprint-loop` completes it before close restarts. The retrospective decision is `run-after-close` or `declined`; a scheduled retrospective runs only after final binding in a separate bounded workflow.

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

## Checkpoint Content Contract

| Checkpoint | Preconditions | Owned contents |
|---|---|---|
| Story activated | preflight and ownership valid | activation, BMad in-progress transition, state identity |
| Definition accepted | authors self-reviewed; combined definition ACCEPT; any triggered early plan review/authorization durable | spec, plan set, tasks, mapping/state, raw verdicts |
| Task group verified | honest RED/GREEN or truthful nonbehavioral evidence | one coherent production/test increment and task markers |
| Converged candidate | converge reports clean | candidate identity, convergence evidence, BMad in-progress to review |
| Correction | invalidated downstream stages rerun and accepted | regression/correction and fresh evidence |
| Story complete | quality ACCEPT, E2E decision, final verification | state/evidence and BMad review to done transition |
| Epic complete | close-bmad-epic contract passes | epic gate/evidence/disposition/status artifacts |

Adjacent definition artifacts share one checkpoint. Unaccepted artifacts never share an implementation checkpoint. Explicit owned paths only; never commit rejected work, failing tests, secrets, temporary reports, or unrelated edits.
