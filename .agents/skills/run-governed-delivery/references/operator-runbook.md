# Governed Delivery Operator Runbook

## Start

Examples:

```text
Use $run-governed-delivery for next.
Use $run-governed-delivery for story 4-2.
Use $run-governed-delivery for epic 4.
Use $run-governed-delivery resume.
Use $run-governed-delivery dry-run for epic 4.
```

New work uses state 3.1. Active 3.0 resumes adopt it once using bounded evidence; completed 3.0 stories retain their original checkpoint contract.

### Select the feature directory

After resolving the full canonical story key and ordinary preflight/dependency checks, root runs:

```powershell
python .agents/skills/run-governed-delivery/scripts/feature_selection.py --repo . --story-key 18-1-cancellation-boundary
```

The read-only JSON result is `mode: create` with `feature_directory`, `spec_file`, and an explicit `override` input, or `mode: resume` with the existing directory. New output is `specs/18-1-cancellation-boundary`, not a self-incrementing feature number. Resume retains old numeric paths; validate the mapped state/checkpoint and lifecycle before advancing, and never reactivate a completed story. The helper scans mapping identity only, not full historical evidence, and does not reserve a name or write files. Dry-run stops after reporting.

Reconcile the existing activation/development record and root ownership before creating anything. Pass `SPECIFY_FEATURE_DIRECTORY` explicitly through the selected adapter. Preserve upstream template resolution and hooks; root owns `.specify/feature.json` persistence and all lifecycle/mapping records. The definition author writes only its dispatched definition paths. Recheck name availability after pre-hooks before creation; after specify, verify the returned path matches the selection before persisting both mapping sides. No counter, branch rename, init-options edit, upstream script patch, or historical directory migration is needed. Duplicate mappings, conflicting same-story directories, malformed state, or an adapter lacking explicit-directory support stop for reconciliation; do not overwrite, increment, or invent a suffix.

## Expected Progress

The operator should see compact stage updates, not repeated ceremony explanations:

1. selected story, dependencies, branch/HEAD, ownership;
2. one persistent definition author's spec/plan/tasks self-review plus deterministic plan metadata, headings, hashes, and design-ID coverage results;
3. one retained independent definition-review cycle with raw verdict history;
4. one informed implementation approval that confirms disclosed test seams;
5. retained test/production owners, RED/minimal-GREEN/final-GREEN evidence, and brief final-shape assessment dispositions by coherent behavior group;
6. convergence disposition with reused/new task counts or earliest-owner go-back;
7. one root lookback and quality verdict, including a valid no-new-lesson outcome;
8. E2E decision, final verification, bound checkpoint, and next queue item.

## Human Gates

Pause only for:

- planned-change authorization after accepted early plan review and before tasks;
- accepted-definition implementation approval before the first production write;
- contradiction with prior user approval/higher authority, or a new architecture decision outside the accepted plan;
- unresolved product intent, hard blocker, BLOCKED, or the unresolved episode's fourth failed result/accepted finite extension limit;
- inseparable ownership or divergent/destructive Git recovery;
- unavailable required infrastructure or unsafe external action;
- ambiguous E2E failure;
- reserved epic architecture findings and retrospective choice.

Ordinary REVISE correction, independent ACCEPT, clean convergence, passing commands, and safe checkpoint binding do not need confirmation. Keep the pre-task gate's existing order and request design. At a real decision, expect an explanation of the choice, principal tradeoff, uncertainty, and precise authorization before supporting links. At a recovery stop, expect what failed, what different repairs established, the current hypothesis/unknowns, and the next bounded approach, not just a retry request. See `human-interaction.md`.

Human approval is default. The user may separately designate a delegate for planned-change authorization, implementation approval, or finite recovery extensions within a user-set per-episode ceiling. At every reached recovery limit a fresh bound decision is required. This exception does not resolve holds or authorize the other listed gates; see `review-and-correction.md`.

During spec/planning, optional questions may arrive while drafting continues. A missing answer creates no decision or retry. Actual late replies are reconciled against the current candidate and routed through existing invalidation/review; they never retroactively approve implementation. A deeper separate discussion task is created only when explicitly requested and supported, with read-only scope; root reads the actual user instruction before recording any applicable approval.

## Checkpoints

Ordinary recovery boundaries are activation, accepted definition, each coherent verified implementation group, converged review candidate, real correction, and completion. Stage explicit owned paths only. Preserve unrelated work. Never blanket-stage.

## Recovery

Use the state machine's earliest-owner table and retained author/reviewer sessions when addressable. A replacement receives current raw artifacts and ordered raw verdict history. Do not reconstruct missing verdicts, fabricate RED, guess mappings, or repair Git history. Detailed history is recovered from durable evidence and Git, not accumulated in workflow state.

Initial negative result counts 1, failed repairs 2/3/4: root diagnosis at 3, user pause at 4. Go-back preserves valid work; changed phase, tasks, inputs, or model does not reset allowance. Diagnose using failed evidence, affected assumption, changed approach, and next check. Astra is a conditional exclusive definition-author takeover, never an automatic response to count or environment failure. Post-implementation recovery closes only on the final verified candidate.

Use the installed narrow guard:

```powershell
python .agents/skills/run-governed-delivery/scripts/validate_workflow.py --repo . --story-state specs/<feature>/workflow-state.json --result <result-evidence> --json
python .agents/skills/run-governed-delivery/scripts/validate_workflow.py --repo . --story-state specs/<feature>/workflow-state.json --next-stage <stage> --next-profile <profile> --json
```

The first returns canonical `transition_json` and `proposed_state`: root persists the returned transition bytes at the proposed head path, then the proposed snapshot. The second checks that persisted current boundary before dispatch. Omit `--next-profile` unless selecting an author profile. Result envelopes bind `observed_after` to the issuing head and retain unchanged `raw_result`; do not relabel an old verdict as a new failure. A paused snapshot validates, but repair through its pause fails. Technical retries retain `technical_retry` in existing evidence with the separate three-root-invocation allowance. These mechanical checks do not prove semantic diagnosis or live agent compliance.

`--json` returns `ok`, `errors`, and, for a result proposal, `transition_path`, `transition_json`, `transition_sha256`, and `proposed_state`; a dispatch request also returns `decision: {allowed, action, reason}`. Write only a successful proposal. Persist the exact returned UTF-8 transition text including its final LF before the state that references its hash, then run the separate persisted-state dispatch check. The helper does not write files. `--next-profile` requires `--next-stage`; result/dispatch flags require `--story-state` and cannot be mixed with epic/retrospective options. Narrow mode checks this story and local required configuration, not historical peers.

### Initialize and process a result

For a new activation, root writes an ordinary analysis record with the following transition and binds its `{path, sha256}` in the new state's `transition_head`. Serialize it as sorted-key, compact UTF-8 JSON followed by LF; hash the actual bytes. Keep the usual story metadata/evidence convention around this minimal contract:

```json
{
  "kind": "analysis",
  "transition": {
    "story_key": "<mapped story key>",
    "sequence": 1,
    "previous_head": null,
    "event": {"kind": "activation"},
    "projection": {
      "recovery": null,
      "definition_author": {"profile": "definition-author", "escalation_decision_id": null}
    }
  }
}
```

In the normal compact state use `schema_version: "3.1"`, remove historical `attempts`, copy that projection's two fields, bind `transition_head`, and initialize `lookback: {"status":"pending","evidence":null}`. Run the narrow validator before dispatch. Existing active 3.0 uses an `adoption` result with `legacy_evidence` instead; it must not be initialized as a clean new activation. Its evidence binds `legacy_sha256` of canonical old state, established/uncertain `confidence`, known `failed_results`, `earliest_owner`, `episode_id` when recovering, and a current `clean_boundary_evidence` descriptor when claiming no unresolved failure. Unknown allowance remains paused, never silently zeroed.

For subsequent outcomes, root writes immutable evidence first. Failure, accepted result, and corrective author self-review wrappers use `{"observed_after": <issuing transition_head>, "raw_result": <unchanged original result>}`; compute the descriptor from those actual bytes. Then write a draft analysis record with `transition: {story_key, sequence, previous_head, event}` using the current head and next sequence. Leave `projection` absent for the CLI to compute. Never make `event.result` refer to its own transition; it refers to raw outcome/analysis/receipt evidence. Use `phase_complete` for successful corrective spec -> plan -> tasks authoring, without adding independent phase reviews or resetting the episode. Only genuine accepted checks advance gate status. Final `close` needs plan/definition/convergence/quality/E2E plus actual aggregate verification proofs. Exact event and proof fields are in `artifact-contracts.md`.

At context/replan, retrieve only relevant active lessons. At lookback, update touched guidance or approved canonical sections selectively; no default learning worker or lesson quota. An 8 KiB active lessons target prompts consolidation, not truncation or a failed gate. See `story-lookback.md`.

Reserve `## Workflow Bookkeeping` in the development record for root lifecycle/checkpoint metadata only. The lookback proof pins both `section_sha256` and `record_content_sha256`; quality reviews the proof, record, and outputs. Routine status/bookkeeping can then change without invalidating unchanged intent, while any other record-body edit requires refreshed proof/review. Historical transition checks validate durable evidence integrity; fresh accepted results and forward dispatch still require their current input boundary.

## Epic Close

After all stories are bound done, use close-governed-epic. It verifies a clean pinned commit, runs the architecture audit, resolves reserved dispositions, binds epic done, then runs or declines the retrospective as selected.
