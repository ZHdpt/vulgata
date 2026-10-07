# Project state for agent resumption

Test infrastructure (2026-09-29): the standard command is now
`pwsh -NoProfile -File eng/test.ps1`, which builds current sources and selects the
classified SelfContained suite. Direct CLI/IDE execution has the same safe default
and external fixture guards. PostgreSQL qualification uses a new owned PostgreSQL 17
container; tool/provider/E2E and historical suites remain explicit and retain their
feature-specific gates. See [testing guidance](testing.md). This changes the routine
regression boundary, not historical product evidence or story lifecycle state.

Updated: 2026-10-07 (Business B0 reader, Explore E0, Scan S1 and controlled Chat integrated; runtime qualification retains its bounded scope).
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
  Tracker refreshes require both epic files. Story 13.1 and October Stories 18.1
  and 18.4 are done. The narrowed 18.2 and 18.3 slices are integrated; their original
  full-story requirements remain incomplete and skipped, not silently completed.
  Integration does not activate another story or resume the frozen tool workflows.
- The [October delivery plan, revised October 6](../bmad/planning-artifacts/october-delivery-plan-2026-09-30.md)
  now selects the first useful outcomes of four feature groups and reserves an
  [integration phase](../bmad/planning-artifacts/october-integration-plan-2026-10-06.md).
  The user abandoned full 18.5 execution for this delivery; OCT-BUSINESS owns the
  minimum Knowledge foundation derived from its plan. Old 18.5 evidence remains
  historical and incomplete; resuming its held workflow is not a new prerequisite.
  Follow the revised briefs rather than copying the full original story matrices
  or skipped tool tasks. Initial Knowledge K/CP-K1 is accepted at `12d92d80`
  (candidate `07c3fe44`), with Scan/Chat/Explore conformance and 89/89 combined
  controlled passes. Business B0a T007–T013 is now locally implemented and
  independently accepted: 160/160 focused SelfContained, 9/9 owned PostgreSQL 17,
  and 1/1 owned cross-process logging qualification, with zero skips and verified
  cleanup. Its source commit is imported with B0b; see the Business
  [execution/handoff record](../../specs/20-1-Oct14-business/quickstart.md).
  Local B0b T014–T018 is also independently accepted: exact immutable corpus/CL
  publication, live ordinary-reader authorization, bounded protected reads and
  feature-local composition. Final affected verification passed 373/373 SelfContained,
  20/20 owned PostgreSQL and 1/1 cross-process logging checks, with zero skips and
  verified cleanup. The writer now additionally requires trusted host freeze/inventory
  authority. The October 7 integration imports Business `b65213ad`, Explore E0
  `50be5782` and Scan S1 `50779d73`, and wires opt-in exact reader composition to
  the frozen DefaultConnection with live ordinary-user authorization. Full
  SelfContained regression passed 2,592/2,592 and owned PostgreSQL passed 21/21,
  zero skips. See the [integration handoff](../bmad/planning-artifacts/october-reader-integration-2026-10-07.md)
  for configuration, evidence and remaining resource limits. Real Scan S2 adapters,
  retained source-backed publication, Business T020 and Explorer's real generated-CL
  qualification remain pending. This is not Chat W0/live Chat completion.
  The subsequent controlled Chat checkpoint `e4689f0b` (including entry parent
  `80314207`) is integrated at `10db709b`. Combined SelfContained verification
  passed 2,775/2,775, zero skips, including 193 Chat and 15 reader-integration
  cases. October generation remains disabled; prompt embedding, raw transport and
  Web admission, full-contributor disclosure and durable dialogue remain missing.
  See the [Chat integration/next-step handoff](../bmad/planning-artifacts/october-chat-integration-2026-10-07.md).
  The subsequent user-authorized [October 7 delivery wave](../bmad/planning-artifacts/october-wave-2026-10-07.md)
  supersedes that pause: four-lane scoped development and incremental integration
  may proceed under its dependency/ownership boundaries. Shared dialogue/disclosure
  agreement precedes its consumers; real resource authority remains separate.
  The first wave integrates Chat tool progress `df7f09cc`, Explorer Identity-claim
  correction `d28ba236` and host Chat-context/prompt preparation `0949d633`.
  Combined source `84d60d8d` passed 2,798/2,798 SelfContained tests, zero skips.
  The subsequent wave accepts Business canonical dialogue/disclosure `d914fea8`
  with both consumer agreements, imports Scan configuration `8cc06035` and its Worker
  prompt `2106af16`, and adds controlled fresh-read Web provider binding. Final combined
  SelfContained verification passed 2,859/2,859, zero failures/skips. All four lanes
  are released within the wave boundaries; no all-Chat or all-lane barrier applies.
  Reviewed raw Chat guard `26a89712` is subsequently integrated: 509/509 selected
  related SelfContained cases pass (not another full-suite run). This supplies strict
  streaming validation and guard cleanup, not selected-client/admission composition.
  Chat generation remains disabled; owner disclosure/dialogue implementation, W1
  admission/client composition and real-source retained qualification remain open.
- [Constitution](../../.specify/memory/constitution.md), root AGENTS and the current
  story workflow govern development. The constitution's 2.0.0 local amendment aligns
  prior conflicting rules; it does not claim maintainer PR review/merge.

Story 18.4 is integrated from `7e19bf69`; its
`specs/017-october-batch-setup/workflow-state.json` records completion and closed
recovery. Independent batch setup preserves source authority, context provenance,
isolation and explicit provider binding. Ordinary Web uses startup
`DefaultConnection`, normal migrations/roles/browser/provider administration and
no October mode or generator prerequisite. Its retained evidence records 174
focused and 2,149 SelfContained passes, a successful Web build, and owned
PostgreSQL/generator/Web process qualification; this is not later Scan or paid
provider qualification. See `evidence/verify-aggregate-01.json` and
`evidence/ordinary-web-runtime-qualification-final-green-01.json` in that feature.

Story 18.2 is integrated from `9be46d6a` and Story 18.3 from `870dc49a` under the
user's narrowed direct-SpecKit targets. Their original governed workflows remain
frozen; prior approvals and skipped tasks are historical, not new execution gates.
The JDT slice has independent dedicated-root startup/navigation/lifetime evidence
in `specs/18-2-navigate-prepared-java-source-with-restricted-jdt-ls/evidence/immediate-lifetime-verification-01.json`.
The CodeGraph slice prepares in place and returns the descriptor actually used
for native MCP discovery/invocation; its bounded process-exit correction is in
`specs/18-3-use-native-codegraph-mcp-against-prepared-source/evidence/process-exit-correction-01.json`.
Neither narrow slice proves customer Repository qualification, mixed-root JDT and
CodeGraph coexistence, or the deferred ordinary-reader integration. The trusted
native profiles do not establish preventive filesystem/network isolation.

Workflow installation itself activated no story. Verify current runner ownership
and its assigned checkout before interpreting the integration tracker. Story 13.1's
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
| CodeGraph/LSP | Integrated narrow adapters: 18.2 records 121 controlled JDT checks, real pinned JDT 1.58 navigation under application-supplied JDK 21, and 2,063 SelfContained passes. 18.3 records actual returned-descriptor native MCP use, process-exit corrections and 2,019 SelfContained passes. These are separate branch/owned-fixture results, not combined-root or customer-Repository qualification. General source-authority/reader integration remains unfinished; no preventive OS filesystem/network isolation is claimed. |
| Knowledge | OCT-BUSINESS B0a implements atomic semantic file acceptance, immutable CL history, exact replay/lookup and sanitized logging in October persistence; focused controlled, owned PostgreSQL and two-process logger evidence is recorded in its quickstart. Real Scan authority/source adapters and B0b publication/live reads remain unqualified; legacy stores are not substitutes. |
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
