# Project state for agent resumption

Updated: 2026-09-16. Story 13.1 has verified incremental capture, scoped reads,
publication/recovery, cleanup, trusted-workspace and fresh-process reopen evidence
with the remote unavailable. Final feature/full-suite verification remains pending.
Use the active feature workflow state for its latest bound checkpoint. Other as-built
notes retain static baseline `2bb83c3b44fc0afc07ae253ec1df5fab4399b964`. Reobserve the
assigned checkout, tracker and source before reuse; do not require an old worktree or
clean unrelated work.

## Current authority and delivery

- [PRD](../bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md): product scope
  and explicit Current/Deferred/History/Original unmet backlog dispositions.
- [Glossary](../UBIQUITOUS_LANGUAGE.md): canonical domain terms.
- [Architecture gateway](../bmad/planning-artifacts/architecture.md) and its spine:
  adopted generic target and project responsibilities; not as-built qualification.
- [Epics](../bmad/epics.md) define delivery; [sprint status](../bmad/implementation-artifacts/sprint-status.yaml)
  owns lifecycle. Epics 13–17 contain 41 definitions (19/7/8/3/4). Story 13.1
  is active; the remaining stories are backlog. Incremental source capability
  evidence is not completed Story 13.1 or generic Scan qualification.
- [Constitution](../../.specify/memory/constitution.md), root AGENTS and the current
  story workflow govern development. The constitution's 2.0.0 local amendment aligns
  prior conflicting rules; it does not claim maintainer PR review/merge.

Story 13.1 deliberately activated `specs/015-retained-source-access`; its plan and
workflow state govern current work. Specs 001–014 retain historical scope. The agent-context extension
targets root AGENTS; pass the selected feature's actual plan path. Its no-argument
auto-discovery chooses by modification time and must not select a historical plan.
Optional after-specify refresh is inapplicable when that feature has no plan yet.

Story definitions were accepted under explicit approval for 13.1–13.3 and a scoped
confidence delegation for the rest. That completed planning delegation is not standing
authority to bypass a later feature's plan/tool/Scan/PRD or implementation approval.
Use the current unified workflow and configured risk escalation; do not revive old
Full/Standard/expedited or overnight/deadline waivers from historical handoffs.

## As-built reuse boundaries

Outside the active retained-source work, these remain earlier static observations.
Use the feature evidence for current controlled source tests; no row proves a full
runtime workflow:

| Area | Existing value and limit |
| --- | --- |
| Administration/persistence | Reusable Identity, roles, configuration and domain persistence; Identity remains in Web/Data. |
| Source preparation | Story 13.1 has verified raw-Git retained capture, canonical manifests, and scoped metadata and byte/text reads. Live denial, concurrent publication and controlled process-crash cleanup are verified; trusted workspace materialization is verified; fresh-process remote-unavailable reopen is verified; source-only boundaries and final gates remain pending in the active feature. Legacy disposable leases and prepared pair contracts remain separate. |
| CodeGraph/LSP | CodeGraph 1.5 integration is CLI/JSON, not native MCP; tool identity/launch paths are Windows-oriented. JDT protocol/containment mechanisms need the new declared Docker/profile qualification. |
| Knowledge | Atomic persistence/provenance mechanics exist, but file-level CL/paired Flow semantics are not the new semantic Code Unit/Logical Flow contracts. |
| Resolution | Finite exact matching is profile-bound; generic inquiry, late-evidence reconciliation and the ordinary production caller are not delivered. |
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

Root `CLAUDE.md` is a symlink to AGENTS; Copilot and Trae share root guidance. The
current Codex role configuration follows the unified workflow. Legacy Copilot
sprint-loop role allowlists/test-ownership adapters are not synchronized with that
workflow and are fenced from current delivery dispatch. Updating their roles/models/
tool permissions is a separate bounded adapter task, not a memory refresh.
Scan architecture helpers use current authorities; July work requires explicit
historical scope. Legacy test/seeding recipes grant no runtime or credential authority.

## Maintenance

Update this compact snapshot after accepted implementation or workflow changes;
replace superseded facts rather than accumulating turn logs. Recheck affected links,
source ownership and harness compatibility. Keep future requirements in PRD/stories,
stable conventions in the constitution/development guide, and observed test/runtime
pitfalls in [testing guidance](testing.md). Current source/lifecycle evidence always
outranks this dated orientation note.
