# Project state for agent resumption

Test infrastructure (2026-09-29): the standard command is now
`pwsh -NoProfile -File eng/test.ps1`, which builds current sources and selects the
classified SelfContained suite. Direct CLI/IDE execution has the same safe default
and external fixture guards. PostgreSQL qualification uses a new owned PostgreSQL 17
container; tool/provider/E2E and historical suites remain explicit and retain their
feature-specific gates. See [testing guidance](testing.md). This changes the routine
regression boundary, not historical product evidence or story lifecycle state.

Updated: 2026-10-06 (Story 18.4 completion and Story 18.2 immediate-objective checkpoint integrated; qualification remains scoped to their recorded evidence).
Story 13.1 is complete in `specs/015-retained-source-access`.
Exact retained capture, scoped reads, publication/recovery, cleanup, trusted
workspaces and separate-process reopening have bounded controlled evidence.
Convergence 02 is clean; quality review 03 accepted with no findings; required
HTTP deletion checks using production startup and Identity cookies pass. Final
verification passed 3,135 tests with 15 existing skips. This includes 167 retained-source
passes and four child-only skips covered by parent tests. Web build: zero errors,
nine existing warnings. See the feature's `evidence/final-verification-01.json`.

The approved capacity-test polling repair preserves the launch timeout, counts and
required terminal state. An earlier failure after 48 worker launches remains
unexplained and is separate from the repaired terminal observation; controlled
passing runs are not a causal explanation. The existing exact-resolution stable-key
limitation below remains outside this story and unqualified. Story 13.2 remains
backlog; Epic 13 remains in-progress. Use the feature's `workflow-state.json` for
the bound completion checkpoint. Other as-built notes retain baseline
`2bb83c3b44fc0afc07ae253ec1df5fab4399b964` and require fresh checkout/source
verification before reuse.

## Current authority and delivery

- [PRD](../bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md): product scope
  and explicit Current/Deferred/History/Original unmet backlog dispositions.
- [Glossary](../UBIQUITOUS_LANGUAGE.md): canonical domain terms.
- [Architecture gateway](../bmad/planning-artifacts/architecture.md) and its spine:
  adopted generic target and project responsibilities; not as-built qualification.
- [Epics](../bmad/epics.md) define delivery; [sprint status](../bmad/implementation-artifacts/sprint-status.yaml)
  owns lifecycle. Epics 13–17 contain 41 definitions (19/7/8/3/4). The user approved
  adding October Epics 18–20 and their 17 definitions (12/3/2) to that tracker on
  September 27; [October release epics](../bmad/planning-artifacts/october-release-epics.md)
  owns their scoped requirements and definitions, routed from the canonical epic index.
  Tracker refreshes require both epic files. Story 13.1 and October Story 18.1 are
  done; this checkout includes Story 18.1's completed baseline at `3632b2cc`. October
  Story 18.4 is the only feature activated in this assigned checkout, on
  `oct14/story-18-4`, mapped to `specs/017-october-batch-setup`.
  Independent batch execution retains source18.1 authority/context lifetime,
  isolation, context provenance and explicit provider binding. Ordinary Web uses
  only startup DefaultConnection, with normal migrations/roles/browser/provider
  administration and no October mode, batch, setup operator or generator provider
  prerequisite. Plan-review15/definition-review14 and scoped approvals06/08 remain
  current; retained definition owner is Astra/medium. Historical special-Web
  evidence is not current qualification.
  All30 original tasks now have verified completion markers. Current controlled
  October checks pass174/174, full SelfContained2149/2149 with zero skips and Web
  build zeroerrors/35existing package-feed/source-mapping/EF-conflict warnings.
  Owned PostgreSQL and actual generator/Web A/B process qualification pass,
  including separate generator Identity, ordinary startup before October setup,
  normal browser authorization and synthetic provider CRUD/encryption. No paid
  provider calls or later generation/native-tool qualification is claimed. Exact
  run-owned resource cleanup is verified. See the feature's
  `evidence/ordinary-web-runtime-qualification-final-green-01.json` and
  `evidence/t030-implementation-prerequisites-01.json`.
  The human approved finite recovery7-to8 after the recurring role-order fixture
  defect; count7 is retained, delegate ceiling7 unchanged, holds empty. Transition127
  records the passing exact configured build; convergence01 is independently clean.
  The corrected full-suite environment adds only the hash-verified existing npm
  prefix for an unchanged static integrity lookup, not CodeGraph launch or scope.
  Recovery entered independent gates at7/8; final closure is proved only by the
  accepted/verified feature state. A counted failure8
  needs a fresh human decision. Convergence, lookback, quality review, conditional
  E2E decision and pinned final verification are recorded in feature state; tests
  alone never prove completion. No merge/push/history
  rewrite or next-story work is authorized. Unrelated activation draft and historical
  dirty evidence are preserved. Status in other worktrees must be verified there.
- [Constitution](../../.specify/memory/constitution.md), root AGENTS and the current
  story workflow govern development. The constitution's 2.0.0 local amendment aligns
  prior conflicting rules; it does not claim maintainer PR review/merge.

Story 18.4 is the only feature activated in this checkout after the workflow
installation. Its `specs/017-october-batch-setup` state retains the Astra-medium
recovery author and history of the7/8 episode; formal closure is in feature state.
Affected authority/composition tasks were
reopened; unchanged completed work and historical evidence remain. Expected TDD
RED and ordinary first-pass development build failures do not consume recovery
allowance; independent negative review/convergence and recurrent defects after
repair remain counted. The bounded console testability correction preserves
authentication policy; controlled terminal tests are not native-terminal proof. Runtime
PostgreSQL/process qualification now has an explicit fresh owned-resource grant in
`runtime-qualification-resource-authorization-01.json`; this grants no revised-plan
approval, recovery extension or waiver. Workflow
installation itself activated no story; verify current runner ownership and its
assigned checkout before interpreting the integration tracker. Story 13.1's
`specs/015-retained-source-access` plan, state and evidence are completed history;
Story 13.2 remains backlog and requires deliberate activation. Specs 001–014 also
retain historical scope. The agent-context extension targets root AGENTS; pass the
selected feature's actual plan path. Its no-argument auto-discovery chooses by
modification time and must not select a historical plan. The optional context hook
was not run for 18.4 because its author write boundaries excluded root AGENTS.

Story definitions were accepted under explicit approval for 13.1–13.3 and a scoped
confidence delegation for the rest. That completed planning delegation is not standing
authority to bypass a later feature's plan/tool/Scan/PRD or implementation approval.
Use the current unified workflow and configured risk escalation; do not revive old
Full/Standard/expedited or overnight/deadline waivers from historical handoffs.

The installed workflow is governed-delivery 0.7.4 (updated 2026-09-28), using BMad 6.12 and SpecKit
1.0.6 through `.governed-delivery/config.toml`. Use `$run-governed-delivery`;
new story states use schema 3.1. Sol/Luna profiles use GPT-6, with existing efforts
and Astra recovery unchanged. Verification follows accepted behavior/risk, not file
type; nonbehavioral work does not require manufactured RED or new tests. This upgrade
does not activate a story or alter existing states. Root AGENTS now permits explicit
story-scoped delegation for planned changes, first implementation and finite recovery
extensions. Recovery requires a separate user-set cumulative per-episode ceiling and
a fresh bound decision at every reached limit; policy support alone is not a grant.
The story root records and validates decisions. Human approval remains default;
other reserved gates and execution permissions are unchanged. See the adoption
record for current management roles and the remaining designation requirements.
One persistent Sol/medium definition author owns
spec, plan, tasks and later document corrections; each coherent implementation
group retains separate Luna/high test and Sol/medium production owners. Root owns
lifecycle, evidence, approvals, selective project lessons and Git. See the installed
[agent catalog](../../.agents/skills/run-governed-delivery/references/agent-catalog.md)
for conditional architecture/recovery escalation, and the
[adoption record](../agent-workflow/adoption-0.7.md) for installation verification.
Consumer development rules and planned-change/implementation approval gates remain
in force; installation grants no product-story authorization.

Existing story-state files and historical activation/epic receipts were not migrated.
The 0.7.1 compatibility patch validates Story 13.1's unchanged original completion
checkpoint from its actual pre-distribution evidence; its missing-configuration
blocker is resolved without migration or rebinding. This is not Epic 13 closure or
new product verification. Aggregate validation still reports 256 other historical
findings; no waiver or history rewrite was applied. See the adoption record for the
exact boundary. New configured checkpoints use the current contract. A genuinely
active 3.0 state would need bounded root-owned adoption, not a
bulk rewrite; no such current story was found. Project lessons are maintained
selectively when warranted, not by creating an empty file or retrofitting completed
stories.

## As-built reuse boundaries

Outside the completed retained-source work, these remain earlier static observations.
Use the feature evidence for current controlled source tests; no row proves a full
runtime workflow:

| Area | Existing value and limit |
| --- | --- |
| Administration/persistence | Reusable Identity, roles, configuration and domain persistence; Identity remains in Web/Data. |
| Source preparation | Story 13.1 has verified raw-Git retained capture, canonical manifests, and scoped metadata and byte/text reads. Live denial, concurrent publication and controlled process-crash cleanup are verified; trusted workspace materialization is verified; fresh-process remote-unavailable reopen is verified; source-only boundaries and diagnostics are verified; controlled full-suite and all Story 13.1 independent gates pass; no generic Scan qualification is implied. Legacy disposable leases and prepared pair contracts remain separate. |
| CodeGraph/LSP | Historical CodeGraph 1.5 integration is CLI/JSON, not native MCP. October Story 18.2 in its assigned branch has 121 controlled JDT LocalTools passes (including bounded authority waits and public disposal uncertainty) and a passing real pinned JDT1.58 fixture under application-supplied JDK21: definition/reference function results, bounded shutdown, owned cleanup and unchanged source bytes. Final basic SelfContained passes 2063/2063 and excludes JDT/native LocalTools. Prepared Repository, existing JDT-filter metadata, mixed roots and integrated producer handoff remain unqualified. Historical failures remain recorded; no universal filesystem/network isolation is claimed. Old unfinished tasks/workflow are preserved and stopped. See the [18.2 development record](../bmad/implementation-artifacts/18-2-navigate-prepared-java-source-with-restricted-jdt-ls.md). |
| Knowledge | Atomic persistence/provenance mechanics exist, but file-level CL/paired Flow semantics are not the new semantic Code Unit/Logical Flow contracts. |
| Resolution | Finite exact matching is profile-bound; generic inquiry, late-evidence reconciliation and the ordinary production caller are not delivered. Controlled baseline qualification also exposed an unchanged consumer candidate missing ConsumerEntryStableKey, which the V2 store rejects with Conflict; see specs/015-retained-source-access/evidence/canonical-correction-affected-fixture-diagnosis-01.json. |
| Chat | Provider SSE is ingested but the current answer path buffers strict candidates and reveals accepted text in the UI. Pair-bound scope and process-local dialogue/publication are not genuine generic streaming/durable reattachment or Business BL-only behavior. |
| Reader/graph | Current pages are placeholders; existing Knowledge/citation code is not a working exact-version reader or graph. The planned diagram package is not installed at this baseline. |
| Runtime/operations | The current Compose profile is a baseline, not bundled-tool/source/key persistence or coherent backup/restore proof. Startup applies migrations/seeding. |

Consult actual `src/dotnet/`, `tests/Vulgata.Tests`, Docker/package files and the owning
story before reuse. Earlier external assessment notes may help locate code, but the
repository's current source and accepted documents remain the authorities. In
particular, a registered service is not proof of a reachable end-to-end route.

## Historical material

The [adoption ledger](../bmad/planning-artifacts/generic-adoption-2026-09-11.md) and
[archive manifest](../bmad/history/pre-generic-adoption-2026-09-11/manifest.json)
preserve exact pre-images. Epics 1–4 retain recorded completion; 11.1–11.4 and
12.1–12.4 retain their accepted history. Story 11.5 remains incomplete and 12.5
terminated incomplete. Old Epics 5–10, July plans and historical specs are not active
queue or fallback instructions. Preserve their evidence rather than rewriting it as
generic success.

Prompt Workbench, dedicated unknown-library inference and general configurable MCP
remain original unmet backlog outside the five-epic core sequence. Deferred features
remain deferred; native CodeGraph/internal wiki tools do not complete general MCP.

## Harness compatibility

Root `CLAUDE.md` is a symlink to AGENTS; Copilot and Trae share root guidance.
Only the installed Codex governed-delivery profiles currently support governed
story delivery. Obsolete Copilot sprint-loop agents and run/close prompts were
removed; remaining unrelated helpers are not alternative delivery orchestrators.
Copilot/Trae assistance remains subject to AGENTS and must not imply workflow
harness parity. Start a fresh Codex session after installation so the new skills and
profiles are discovered. Any other delivery-harness adapter requires a separate
bounded compatibility task.
Scan architecture helpers use current authorities; July work requires explicit
historical scope. Legacy test/seeding recipes grant no runtime or credential authority.

## Maintenance

Update this compact snapshot after accepted implementation or workflow changes;
replace superseded facts rather than accumulating turn logs. Recheck affected links,
source ownership and harness compatibility. Keep future requirements in PRD/stories,
stable conventions in the constitution/development guide, and observed test/runtime
pitfalls in [testing guidance](testing.md). Current source/lifecycle evidence always
outranks this dated orientation note.
