---
name: close-governed-epic
description: Use when every story in a governed lifecycle epic appears complete and the epic needs final verification, architecture review, retrospective, and status closure.
---

# Close Governed Epic

Close one epic through exact-commit verification, independent architecture audit, reserved dispositions, a bound local checkpoint, and a safe retrospective choice. The active root is the sole state writer and committer.

## Load Authorities and Preflight

Read the complete epic, lifecycle state, every epic story and mapped workflow state; `AGENTS.md`; Constitution; contexts; ADRs; the canonical glossary; and the runtime contracts under `../run-governed-delivery/references/`.

Record branch, full `HEAD`, repository/Git/worktree identities, exact porcelain and index status, and explicit epic-owned/unrelated paths. Preserve user work. Load canonical state and run Step 0 before applying new-transaction preflight. When starting a new close transaction, require the real index to be empty and every prospective close-owned path to be clean relative to pinned `HEAD`; record its preimage blob or absence. User edits in a target path stop closure rather than being swept into the checkpoint. If the user has staged entries, stop for direction rather than mixing, unstaging, or committing them. Stop on inseparable ownership, divergence, or destructive recovery. Never implicitly switch the user's branch, reset, rebase, merge, push, force-push, open a PR, or mutate a remote.

Use `docs/bmad/implementation-artifacts/epic-gates/<epic-key>/architecture-gate.json` as the canonical atomic state/failure record and its `evidence/<evidence-id>.json` files as durable command evidence.

## Gate Sequence

### 0. Recover an interrupted close transaction

Before eligibility, load the canonical gate record. If `transaction_phase == pending-binding`, do not reject an already provisional-`done` epic. Resolve the checkpoint ID first: exactly one matching commit must have the recorded source as direct parent, correct epic/scope/stage/checkpoint trailers, a `Workflow-Manifest-SHA256` trailer equal to the canonical SHA-256 of its complete parent-to-commit change set, exact declared non-self-referential constraints, and the `done` transition. If valid, treat `effective_phase = bound` as durably derived from the immutable gate plus verified commit; the committed gate intentionally remains `pending-binding`. If no matching commit exists and active `HEAD` still equals the recorded source, resume only when the index is empty or its staged change set satisfies the declared constraints; rebuild/commit after revalidation. A mixed user/workflow index is a deliberate no-write exception to failure recording: preserve gate, worktree, and index bytes exactly and stop for direction. Ambiguity, changed source, or mismatch also stops. This recovery runs before new-transaction preflight and Step 1.

### 1. Prove eligibility

Require the epic to be `in-progress`; every member story BMad `done`; unique mappings and bound ancestral final checkpoints; complete tasks/checklists, reviews, E2E, and verification; and no pending/failed/provisional gate, unresolved recoverable failure, `REVISE`, `BLOCKED`, or unresolved Critical/High finding. A retryable failure may transition atomically from `failed` to `retrying` only after its cause is cleared and source/preflight are revalidated; then resume at its recorded safe step. Inconsistent lifecycle or an untrusted story stops closure.

Validate story completion under its recorded version. State 3.1 requires a resolved recovery episode and current lookback proof evaluated from that story's completion commit; later project-lesson changes do not invalidate it. Historical 3.0 completion retains its original contract without retrofit or rewrite.

### 2. Establish an exact-commit verification boundary

Pin active `HEAD`. Create a clean disposable detached build/test worktree at that SHA (or host-native equivalent), even when the user boundary appears clean. Before commands, require its `HEAD`, index tree, and tracked tree to equal the pinned commit tree and require no untracked input. Record its absolute path, source SHA, checks, and disposal status; never label results from the user boundary as pinned-`HEAD` evidence.

Run the repository's configured build and test commands from `.governed-delivery/config.toml` in the clean boundary. Both commands must be non-empty before closure.

For each command atomically write `evidence/<evidence-id>.json` with the full evidence schema from `artifact-contracts.md`, plus a non-null repository-relative raw stdout/stderr path and SHA-256. Write and later commit that raw file with the evidence JSON. Record command, exit code, observed time, source, execution worktree, and boundary validation; summaries without observed raw output do not count. Earlier or inherited results do not count.

Immediately compare active `HEAD`, build/test checkout `HEAD`, its tracked/index tree, and pinned source. Any source inequality or tracked mutation restarts Step 1. Build outputs remain confined to this disposable worktree. After evidence is durable, identity-check, remove, and verify removal of this checkout; record its own `disposed = true` evidence. Failures stop closure.

### 3. Run and validate the architecture audit

Create a second fresh disposable detached audit worktree at the same pinned SHA. Before and after exploration require empty porcelain (including untracked files), `HEAD` equality, and index/tracked tree equality with the pinned commit; no concurrent writer is allowed. Require an enabled compatible architecture-review adapter, then invoke `$audit-governed-architecture`. Dispatch a fresh direct-child `epic-architecture-auditor` as the independent Explore/classification judge using the epic seven-slot variant. This preserves `agents.max_depth = 1`.

Bind the dispatch to the audit checkout's canonical absolute root, Git common/worktree identities, full `HEAD`, tree/index IDs, and authoritative input hashes. The worker must operate on that root, remain read-only, and return those proofs, an `ACCEPT|REVISE|BLOCKED` audit verdict, and every candidate already classified with stable `raw_candidate_id`, strength, severity, applicable categories, MUST/ADR relationships, and source citations. Severity follows impact: `CRITICAL` threatens security/data integrity or makes the epic unsafe to operate; `HIGH` breaks a required invariant/authority or likely causes material failure; `MEDIUM` is bounded architecture debt; `LOW` is localized low-impact friction. The root never weakens these classifications.

Immediately serialize the complete received tool envelope losslessly as canonical JSON, including the exact structured-result content block, its index, and parsed value. Preserve ordered blocks/metadata, UTF-8 without BOM, LF, sorted object keys, and no insignificant whitespace. Write/hash it under the canonical `audit-raw` directory before normalization. Map each raw ID one-to-one without weakening. Every citation uses repository-relative path, pinned blob OID, and a zero-based half-open `bytes:<start>-<end>` range whose exact blob slice hashes to `excerpt_sha256`; the root derives all values with Git. Self-asserted proof is invalid.

The root losslessly normalizes the judge-supplied candidates and embeds the complete payload in the HTML. Human-visible cards are generated deterministically from that payload, never separately authored. Require absolute report path, SHA-256, source commit, iteration, verdict, and every structured finding. Validate before acceptance:

- report source, verification checkout `HEAD`, pinned source, and active `HEAD` are equal;
- IDs are stable and unique; required fields exist;
- strength is exactly `Strong`, `Worth exploring`, or `Speculative`; severity is mandatory and exactly `CRITICAL`, `HIGH`, `MEDIUM`, or `LOW`;
- report finding count and ID set exactly equal the normalized structured payload count and ID set;
- deterministic extraction of the embedded payload and every visible card reproduces every normalized field (severity, strength, evidence, applicable categories, MUST and ADR relationships included) and its canonical SHA-256; extracted, visible, and durable values/hashes match;
- every citation matches the pinned audit-checkout blob and excerpt, and returned checkout/input proofs match root-observed values;
- `ACCEPT` is permitted only when the independent judge returns no reserved/blocking candidate;
- normalized finding content, report hash, source binding, schema result, and validation timestamp are stored.

Missing/invalid classification, Critical/High, a violated Constitution MUST, Strong, or any ADR conflict is blocking and reserved; it cannot be hidden under a weaker strength. A finding may have multiple applicable categories. Hash the report while present and store the complete validated payload in the gate record. The HTML remains temporary/untracked and is never copied or staged. A later-missing report is usable only when the stored complete payload and validation prove the former hash/source/count/ID/visible-field equality; otherwise rerun.

Passing tests, release pressure, executive acceptance, or “architecture later” never waive this separate audit. A schema/source-valid independent `ACCEPT` with no reserved finding advances without duplicate user approval. After audit evidence/citations/report hash are durable, identity-check, remove, and verify removal of the audit checkout; record its separate `disposed = true` evidence. Both worktrees must be disposed before `pending-binding`.

### 4. Obtain allowed dispositions

Recheck active/verification/pinned/audit source equality before presenting or accepting dispositions; inequality restarts Step 1.

Require explicit user approval for every blocking/reserved finding and one subdecision for every applicable category; all category constraints must pass, with Critical/High or MUST rules taking precedence over weaker-category allowances. Missing/invalid classification invalidates the audit and must be rerun; it cannot be dispositioned. A Critical/High or violated MUST finding closes only when a corrective story changes the source and a fresh audit no longer reports it, or when the user approves a load-bearing false-positive record citing source-bound evidence. Risk acceptance or deferral is not closure-eligible for those categories.

A nonsevere Strong recommendation may be rejected with a load-bearing reason; deferred through an external tracked work-item/ADR identifier with owner, rationale, and due/closure condition; or accepted explicitly as known architecture debt by the user. Any claimed resolution or acceptance for corrective work remains blocking until external corrective work completes and a fresh audit no longer reports it. The architecture-gate record cannot self-reference as a tracked decision.

An ADR conflict has only three outcomes: conform the implementation (including rejecting a conflicting recommendation because the governing ADR stands); supersede/amend the ADR through an explicit tracked ADR decision; or block epic closure. Never “accept” an unresolved ADR conflict. Any ADR amendment must already be committed and included in a fresh audit source before closure.

The close skill never authors or commits remediation, including production, tests, documentation, ADRs, or authority-changing decisions. Use `$bmad-correct-course` to create/select corrective BMad work, then `$run-governed-delivery` (or the explicitly approved ADR decision workflow) so bounded ownership, applicable tests/judges/reviews, and checkpoints apply. Keep the epic `in-progress`; after corrective work commits, restart Step 1 at the new `HEAD`. Any tracked or `HEAD` change invalidates all verification, audit, and disposition evidence. The final close commit contains only mechanical gate/evidence/status records derived from the audited source; it cannot introduce new authority.

### 5. Record the retrospective choice

After the current-source architecture gate resolves, offer `$bmad-retrospective` and require an explicit choice: `run-after-close` or `declined`. The offer precedes the provisional close checkpoint; execution follows trusted binding. Silence does not advance. Do not invoke the retrospective before the epic is truly `done`; its workflow assumes completion and its hooks/shared writes must not enter the pending close transaction.

If selected, consume available story lookbacks for cross-story patterns and existing active lessons selectively. Do not require retrofitting old stories, duplicate every lesson, or turn the optional retrospective into a story prerequisite. All reserved architecture dispositions and exact-commit checks remain independent.

For `run-after-close`, resolve and preflight every retrospective activation-prepend, activation-append, and completion hook before final close. Require an allowlisted exact write manifest or explicit user approval; suppress unknown/unsafe hooks. Record only the schedule and approved manifest in the architecture gate; `retrospective-state.json` does not exist and is excluded from the close manifest until after trusted binding. Post-close learning creates follow-up work and never retroactively supplies close-gate evidence.

### 6. Create and bind the provisional checkpoint

Recheck active `HEAD == pinned source == audit source` before staging. Atomically prepare the gate record with transaction phase `prepared`, complete validated findings/dispositions, evidence paths, verification boundary, retrospective choice, checkpoint ID, and non-self-referential manifest constraints: typed expected name/status entries; complete change tuples for every entry except the gate record; and the gate record's path/status/mode/type plus `semantic_projection_sha256`. Compute that projection by replacing only its own hash value with JSON `null`, then canonicalizing the complete gate JSON; this is reproducible without self-reference. Prepare `in-progress -> done` provisionally, then set phase `pending-binding` before staging.

Refresh preflight and prove the index is still empty. The gate's `durable_artifact_paths`, target-preimage keys, and manifest path set must be identical and include the canonical gate, architecture raw response, every build/test evidence JSON and raw output, disposition evidence, sprint status, and epic completion artifact. Stage only that set. Exclude temp HTML, unrelated edits, rejected/failing work, secrets, story scope, new ADR/authority documents, and retrospective outputs. Run:

```powershell
git diff --cached --name-status
git diff --cached --check
```

Compute the complete staged parent-to-result change set with renames disabled. Represent every change as `{path,status,old_mode,old_type,old_oid,new_mode,new_type,new_oid}`, using `null` for an absent side; a deletion is therefore an explicit tombstone. Normalize paths to Unicode NFC, reject duplicate/case-colliding paths, sort by UTF-8 path bytes then status, and serialize the array as canonical JSON (UTF-8 no BOM, sorted keys, no insignificant whitespace). Require exact satisfaction of the non-self-referential constraints and expected name-status set. Hash those bytes as `manifest_sha256`; this includes the final gate blob without asking it to contain its own OID. After commit, recompute the identical representation from direct parent and commit with rename detection disabled; index stages are precommit safety data, not part of the durable digest. Immediately before commit, recheck the digest, source equality, disposed worktree evidence, target preimages, and staged evidence references.

Commit locally with `BMad-Epic`, `Workflow-Scope: epic/<key>`, `Workflow-Stage: complete`, unique `Workflow-Checkpoint`, and `Workflow-Manifest-SHA256: <manifest_sha256>` trailers. Resolve exactly one trailer SHA; verify direct source parent, recomputed complete tuple digest, declared constraints, exact committed durable paths, and the epic `done` transition. Never amend to store its own SHA. The durable binding is the immutable pending gate plus this verified commit; only successful derivation makes `done` trusted.

If `run-after-close` was chosen, atomically create retrospective state only now. Validate hooks and safe noncolliding write paths; store their canonical path-manifest hash and a pending checkpoint ID/expected parent. Stage exactly those paths in an empty index and put the full canonical parent-to-commit change digest in the retrospective commit trailer. Resolve the ID uniquely, verify direct parent/trailers/digest/changed paths, and derive effective completion while the committed state remains `pending-binding`; never self-store its commit SHA. Otherwise report decline.

## Failure and Resume

On any pre-binding stop other than Step 0's mixed-index no-write exception, atomically record in canonical `architecture-gate.json`: transaction phase, working-tree/index snapshots, status, safe resume step, source/boundary, evidence, retryability, and failure. Resume first applies Step 0; other retryable failures prove the cause cleared, write `retrying`, then revalidate. Remove each disposable worktree as soon as its evidence is durable and only after identity/path checks; record per-worktree disposal. Phase cannot become `pending-binding` unless both disposal records are already true, so recovery never needs to mutate an immutable bound gate to record cleanup. Post-binding retrospective failures use only retrospective state.

On success report epic key, bound checkpoint ID/SHA, verified source and clean execution worktree, exact build/test evidence paths, audit path/hash and validation, durable record, dispositions, retrospective choice/result, and preserved unrelated changes. State that no remote mutation occurred.
