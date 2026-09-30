# Governed-delivery 0.7 adoption

Updated: 2026-09-29. This is Vulgata's installation/resumption record, not product
verification or authorization to start a story.

## Installed boundary

On September 30, a user-authorized bounded local validator correction restored the
required independent plan-review route after a fresh corrected architecture plan
advances recovery ownership to tasks, including an already-started implementation
whose recovery closes only at final verification. The correction authenticates the
current plan hash and transition ancestry, retains reviewer/hold/budget/gate checks,
and does not accept the review or authorize tasks. Focused dispatch, ACCEPT-recording,
task-phase, stale-proof and restriction regressions pass 7/7; targeted dependency and live-story
guard checks pass. This is an installed Vulgata workflow correction, not a Story 18.4
product or architecture decision and not an upstream distribution release.

- Distribution: **0.7.4**, installed September 28 from the validated governed-delivery
  working tree based on source HEAD `992cf3b96c08a3501e1d80115b7cf4f41a1a202c`;
  release changes remain uncommitted in the source project. This updates workflow
  assets, profiles, adapters and validators. At installation Vulgata retained its
  two-gate delegation policy. The separate September 29 policy decision below now
  permits explicitly bounded recovery delegation; installation alone did not grant it.
  No story state or upstream installation changed.

- Distribution: **0.7.3**, installed September 28 from the validated governed-delivery
  working tree based on source HEAD `0c0bf0cfb05edc5b28a346d8f3a63002d1e781ef`;
  the 0.7.3 source changes remain uncommitted. This updates skills, profiles,
  adapters and
  validators and opts Vulgata into the two-gate delegation rule documented in its
  root AGENTS.md. No story state or upstream installation changed.

- Distribution: **0.7.2**. The September 26 update installs the validated local
  source working tree based on `992cf3b96c08a3501e1d80115b7cf4f41a1a202c`;
  the 0.7.2 source changes remain uncommitted; this consumer checkpoint records
  the installed upgrade. Sol/Luna now use
  GPT-6 with unchanged efforts and Astra recovery. Verification routing prevents
  manufactured RED/tests and arbitrary third-party integrity requirements while
  preserving required behavioral and authorized integrity verification.
  The initial 0.7.0 installation on September 16 came from
  `e2ca94dcdd15832381b589bbcee76a4981bb490d`, with the source project read-only.
  The user separately authorized the September 17 compatibility patch and lifted
  that restriction for the fix. Only the installed validator changed at runtime,
  copied from source commit `992cf3b96c08a3501e1d80115b7cf4f41a1a202c`.
- BMad: **6.12.0**, upgraded with the pinned official installer while retaining
  core/BMM, Codex/Copilot/Claude/Trae integrations, project paths and existing shims.
  See the [upstream release](https://github.com/bmad-code-org/BMAD-METHOD/releases/tag/v6.12.0).
- SpecKit: **1.0.6**, PowerShell scripts; existing installation/extensions retained.
- Installed skills: `run-governed-delivery`, `governed-tdd`, `close-governed-epic`,
  `audit-governed-architecture`. Their installed references own workflow rules;
  old documentation paths now forward instead of maintaining duplicate contracts.
- [Configuration](../../.governed-delivery/config.toml) selects `bmad-6.12`,
  `speckit-1.0.6`, and enabled `bmad-review-codebase-design` architecture review.
  The required BMad review and Matt Pocock `codebase-design` skills are present.
- Existing Codex topology already matches the generated fragment: four threads,
  one delegation level. Current profiles are installed together with validators.
  Do not dispatch the retired author/orchestrator roles.
- Build/test commands retain the Vulgata Web build and `Vulgata.Tests` test project.
  Their presence in configuration is not permission for unscoped runtime/DB work.

Local checkpoints:

1. `53f4afe22f424b2c55c97477a9968444a346d822` — BMad dependency upgrade.
2. `015ec2de8e89e2b3f51c25bf054483da1cb755fa` — new workflow/configuration and
   legacy installation retirement.
3. `755c75c7cf0c097bab3cc69ee6054bfd2ff5b950` — initial handoff, authority pointers
   and project-memory updates.
4. The subsequent `fix: apply governed delivery 0.7.1 checkpoint compatibility`
   commit records the bounded validator patch and corrected handoff.

No branch switch, history rewrite, push, product-code change or story activation
belongs to this installation.

## Current delegated approval and management roles

On September 29 the user explicitly expanded permitted delegation to continuation
at failure 4 or a reached approved extension limit and assigned three management
roles. Authority source: `Vulgata Master`, chat
`019fcbe8-f5db-73b3-914c-b29f75688a22`, user message
`01a0ead7-a263-73c0-9eaa-7862c864f476` in turn
`01a0ead7-9a95-7951-9641-8dcca3038ebb`.

Root AGENTS permits this purpose under the installed 0.7.4 contract. A runner still
needs the actual story-scoped user designation and a user-set `max_pause_at` before
using recovery delegation. No numeric ceiling was supplied in that message; none
is inferred here. Earlier two-gate grants remain insufficient for recovery. Each
reached limit requires a fresh decision against the paused episode, not blanket
permission to continue, clear holds or change scope. The runner owns the record
and validation; these notes are not a substitute for actual user provenance.

| Chat | Stable ID | Responsibility |
| --- | --- | --- |
| Vulgata October Approver | `01a0e708-c489-7871-9f73-31413f20bf2c` | Decisions within the user's explicit gate/scope designation; escalate human-reserved choices. |
| Schedule Vulgata release monitoring | `01a0eacd-a033-74b0-88e5-557ac3853181` | Monitor explicitly registered workers; relay decision requests and meaningful milestones without approving or dispatching. |
| Parallel Development schedule | `01a0e5e1-ee94-7362-9b1f-2c61e2c5d536` | Dependency, parallel-start and integration planning; no approval or implicit Git execution authority. |

These bindings replace the earlier unbound name "Vulgata October Scheduler";
they do not register story runners or lift a plan-only stop. Monitoring forwards
approval requests to the approver and completion/dependency milestones to parallel
scheduling, using the pinned IDs rather than reselecting a same-title chat. The
user's synchronization request authorizes relaying this update to these three chats;
ongoing runner communication must retain its explicit user authorization.

Existing worktrees must use compatible installed assets and reconcile the applicable
consumer policy through their own owners. Updating this checkout does not update
another worktree, authorize a merge, or modify story state. Preserve current model
selection, independent gates, source/runtime permissions and historical evidence.

## Preserved history and common selection

All 14 existing `specs/*/workflow-state.json` files, plans, evidence, activation
receipts, epic gate/retrospective records and sprint lifecycle were left unchanged.
Twelve states are complete, including Story 13.1. Story 11.5 remains historical
incomplete work; Story 12.5 remains terminated incomplete. Neither is a resume queue.
No active current-queue state needs 3.0-to-3.1 adoption; new activations use 3.1.

The ignored local `.specify/feature.json` selection of completed Story 13.1 was
cleared. No replacement feature was selected. Corrected AGENTS context states that
13.1 is complete and 13.2 remains backlog. Deliberately select the next authorized
feature; do not infer it from file modification times.

`paths.project_lessons` uses `docs/project-lessons.md`. No empty lessons file or
speculative retrospective was created. Root curates verified reusable lessons during
future bounded story lookback; existing development/testing guidance remains valid.

Removed installation assets: three old workflow skill trees, four superseded Codex
profiles, thirteen old Copilot workflow profiles, and the two old run/close prompts.
Their tracked versions remain recoverable in Git. Unrelated helpers, third-party
skills (including Matt Pocock TDD), and historical workflow reports were preserved.
The local Git safety hook was renamed, not removed; Copilot workflow parity is not
claimed by preserving that hook or unrelated helpers.

## Verification and unresolved compatibility

September 28 upgrade to 0.7.4: installed workflow assets and profiles match the
validated source release; dependency and targeted Story 13.1 checks pass. The
aggregate validator still reports the **256 historical findings**. All 14 workflow
states, config, sprint tracker, product files and upstream skills are preserved; no
story or installation errors are reported and no story is activated. At that
installation checkpoint Vulgata allowed only planned-change and implementation
delegation. The September 29 consumer-policy decision above supersedes that
restriction without changing this historical installation evidence.

September 28 upgrade to 0.7.3: installed workflow assets and profiles match the
validated source release; dependency and targeted Story 13.1 checks pass. The
aggregate validator still reports the **256 historical findings**. All 14 workflow
states, config, sprint tracker, product files and upstream skills are preserved; no
story or installation errors are reported and no story is activated. Root AGENTS.md
opts Vulgata into only the two delegated gates supported by 0.7.3. The user designates
a stable task/chat ID, story scope and conditions in an actual instruction; root
records it and the delegate response in each story's existing decision evidence.

For example: "For stories [list], I designate [task ID/chat link] to approve planned
changes after independent plan review and implementation after independent definition
review, within [scope/conditions], until [end condition]. The runner and this task may
exchange gate requests/responses. No other authority is delegated." Use the actual
task ID/chat link; a title alone is not enough. The delegate returns a decision, while
the story root records the approval files and checks current accepted hashes. All
other gates and execution permissions keep their existing rules.

September 26 upgrade: all **225 distribution tests** and distribution/workflow,
lockfile, whitespace and **four official skill checks** passed. PyYAML was installed
only in a temporary validation environment with user authorization, resolving the
previous validation blocker. No live-agent forward test or product build/test was
performed; the existing accepted forward-testing risk remains.

Installed dependency checks pass without warnings; Story 13.1 still passes its
targeted historical completion validation. All **56 installed files** match source
byte-for-byte, and all four installed skills pass the official validator. Aggregate validation has the identical
**256 historical findings**, with no additions or removals. The upgrade preserves
configuration, topology, upstream installations, product files, lifecycle and all
14 workflow states. No migration or story activation occurred. Restart the workflow
in a fresh session to load the updated profiles and instructions.

Initial installation checks (September 16):

- BMad tracker validation: valid, no problems or legacy status mappings.
- BMad sprint-planning tests: **37 passed**.
- Installed dependency check: passed, no warnings; all four official skill
  validations passed; **56 installed files** matched the release source byte-for-byte.
- Renamed Git guard: five non-executing command-payload checks passed.
- Release regression suite: **204 passed**, plus distribution/source-contract
  validation. Run from the distribution root with bytecode writes disabled;
  an initial invocation from Vulgata had import-path errors, corrected by using
  the suite's required working directory. All **85 tracked source files** retained
  their pre-installation hashes; source HEAD and clean status were unchanged.
- Product authorities, story artifacts and lifecycle match the pre-upgrade baseline
  `8d937054025bf8865dff537c59ec253d5048c6b8`.

The initial aggregate validator reported 258 historical findings. After the 0.7.1
patch it reports **256 remaining findings**, all under preserved historical records,
with no installation/profile/schema or configuration-contract errors. These include
old completion-commit schemas/pending snapshots, unresolved checkpoint IDs/trailers,
unqualified historical configuration, plan hashes/classification and old counters.
The old validator did not perform all these checks; no unrelated defect was waived.

The Story 13.1 blocker is **resolved**. Its original completion commit
`6f312858264ecba8088c87c6b09a00813a652091` still predates portable configuration,
but the patched validator proves its original legacy skill/schema, BMad 6.11
manifest, full pre-adoption ancestry, exact tracker entry and existing checkpoint
binding. It does not copy today's settings backward, invent a new completion,
execute historical scripts, or change any story artifact. Modern/configured
checkpoints, deletion downgrades, shallow history and ambiguous proof remain strict.

Patch verification (September 17): **225 regression tests passed**, including 21
new real-Git compatibility/rejection methods; independent review, distribution,
workflow, lockfile and all four skill checks passed. Installed Story 13.1 validation
returns `ok: true`; dependencies pass without warnings. The installed validator
matches the release file. This removes that specific Epic 13 eligibility blocker;
it is not an epic-close execution, gate waiver or new product qualification.

No application build/full product test suite, live agent forward-test, paid model
probe, provider run or database/runtime operation was performed for this installation.
Prior Story 13.1 product results remain their own historical evidence.

## Next authorized run

Start a fresh Codex session in the intended Vulgata checkout so the installed skills
and profiles are discovered. For orientation without activation:

```text
Use $run-governed-delivery dry-run for next.
```

Recheck current queue, ownership and dependencies. New story execution uses the
installed targeted guard against that story's state, not a bulk history rewrite:

```powershell
python .agents/skills/run-governed-delivery/scripts/check_dependencies.py --target .
python .agents/skills/run-governed-delivery/scripts/validate_workflow.py --repo . --story-state specs/<selected-feature>/workflow-state.json --next-stage <stage> --json
```

Preserve planned-change and implementation gate order; apply only the explicit,
story-scoped delegate designation described above. Optional drafting input is not
consent, and installation approval does not activate Story 13.2.
