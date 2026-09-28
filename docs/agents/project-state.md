# Project state for agent resumption

Updated: 2026-09-28. Story 13.1 is complete in `specs/015-retained-source-access`.
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
  Tracker refreshes require both epic files. Story 13.1 is done. October Story 18.4
  is active for definition/planning only in `specs/016-october-batch-setup`; its
  corrected architecture plan has raw independent plan-only ACCEPT, but no tasks or
  implementation authority. Story 18.1 is actively implemented in another assigned
  worktree and is not integrated or verified in this checkout; this checkout retains
  its tracker entry as backlog and does not claim its dependency satisfied. Every
  other story remains backlog. Sprint planning alone activated no feature.
  Its bounded retained-source
  qualification does not prove the generic Scan workflow.
- [Constitution](../../.specify/memory/constitution.md), root AGENTS and the current
  story workflow govern development. The constitution's 2.0.0 local amendment aligns
  prior conflicting rules; it does not claim maintainer PR review/merge.

Story 18.4 is the only feature activated in this checkout after the workflow
installation. Its `specs/016-october-batch-setup` state stops before tasks: prepared
source identity/grant/tool seams remain dependent on an integrated, verified Story
18.1 baseline and mandatory reconciliation/review. Story 13.1's
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

The installed workflow is governed-delivery 0.7.3 (updated 2026-09-28), using BMad 6.12 and SpecKit
1.0.6 through `.governed-delivery/config.toml`. Use `$run-governed-delivery`;
new story states use schema 3.1. Sol/Luna profiles use GPT-6, with existing efforts
and Astra recovery unchanged. Verification follows accepted behavior/risk, not file
type; nonbehavioral work does not require manufactured RED or new tests. This upgrade
does not activate a story or alter existing states. User-designated delegates may
authorize only planned changes and first implementation within their recorded scope;
the story root records and validates those decisions. Human approval remains default,
and all other gates remain human-only. One persistent Sol/medium definition author owns
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
| CodeGraph/LSP | CodeGraph 1.5 integration is CLI/JSON, not native MCP; tool identity/launch paths are Windows-oriented. JDT protocol/containment mechanisms need the new declared Docker/profile qualification. |
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
