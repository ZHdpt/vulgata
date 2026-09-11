# Sprint Change Proposal: Generic Product Planning Reconciliation

Date: 2026-09-11

Mode: Batch, following the completed architecture walkthrough

Scope: Major planning correction; documentation and backlog only

Status: Approved by the user on 2026-09-11; canonical adoption in progress

## 1. Issue and intended outcome

The user requests formal reconciliation of the PRD, glossary and architecture with the finalized generic Scan/Knowledge/Chat design, and proposes abandoning unstarted epics in favor of a replacement backlog. This continues the approved September 1 CGC decommission and the subsequent assessment/design phase; it does not resume a competition release.

The repository is clean at `dev`, commit `2bb83c3b44fc0afc07ae253ec1df5fab4399b964`, before this proposal. Current documentation still directs future work toward expired August overlays, mandatory pair preparation, preallocated Code Units, post-scan deadlock-breaking, and incompatible Chat/graph behavior. The code contains valuable partial implementations but does not implement the newly finalized generic workflow. Formal adoption must distinguish target requirements, implemented assets and verified behavior.

The load-bearing target is the externally reviewed `ARCHITECTURE-SPINE.md`, finalized September 10 with AD-1 through AD-32 and SHA256 `CC7E5AC92810734549607F38399720A027E3E237320ACEC30926FBF24D6FE093`, under `C:/Users/13728/Documents/VulgataSepAudtion/scan-architecture-2026-09-06/`. The original assessment in the parent directory is supporting evidence, not requirements. The final review summary and chronological Scan/Chat decisions establish the accepted scope and explicit deferrals.

**Outcome of this correction:** one coherent current PRD, one canonical glossary, a clear architecture entry point and adopted spine, a new active epic/story sequence, and explicit preservation/supersession records. No production code, tests, migrations, packages, Docker configuration, database data or runtime services are changed by this planning correction.

## 2. Recommended route

Use a forward planning correction, not a rollback and not another patch to the competition queue:

1. Preserve exact pre-adoption snapshots and hashes of the canonical planning documents and sprint tracker.
2. Reconcile the current PRD in place, preserving existing FR/NFR identifiers and mapping each requirement to retained, revised, deferred or historical status. Append new identifiers only where necessary.
3. Update the existing canonical glossary rather than introducing a competing second glossary.
4. Make `architecture.md` the current authority/platform entry point and adopt one copy of the reviewed AD-1–AD-32 spine as its generic workflow companion. Mark superseded detailed plans as historical, not current fallback instructions.
5. Reconcile dependent UX and contributor/README guidance only where it contradicts the accepted target or misstates the current implementation.
6. Replace the active unstarted backlog with newly numbered, outcome-oriented epics and detailed stories after the reconciled requirements/architecture are accepted. Archive the previous tracker; do not fabricate completion statuses for unfinished work.

| Alternative | Assessment |
| --- | --- |
| Continue Epics 5–10 unchanged | Not suitable: boundaries assume mechanisms replaced by the accepted design. |
| Roll back Epics 4/11/12 | Not recommended: would discard useful persistence, source, agent, provider and transport work. |
| Keep all old plans active and add an overlay | Not recommended: creates competing authorities and more stale resume paths. |
| Adopt the reviewed target and replace the active backlog | Recommended: preserves evidence while providing one future delivery direction. |

Planning effort is medium; subsequent implementation effort is high and must be sized from new stories. The main risk is dropping quiet requirements or treating old green evidence as new qualification. No competition deadline, latest-completion promise or fixed throughput claim is introduced.

## 3. Historical delivery and active backlog

The detailed epic disposition was checked against the complete `epics.md` and `sprint-status.yaml`, feature workflow states and later termination records. The confirmed distinction is:

- Epics 1–4: preserve completed history and reusable implementation; new architecture does not retroactively revoke story completion.
- Epics 5–10: genuinely unstarted; replace their active planning scope, preserving the old definitions as history.
- Epic 11: preserve completed Stories 11.1–11.4 and the incomplete state of 11.5. Do not repeat the September 1 proposal's overbroad description of all Epic 11 as completed.
- Epic 12: preserve accepted Stories 12.1–12.4; Story 12.5 remains terminated incomplete. The epic is historical, not the active sequence.
- The SP.1 spike and previous retrospective/review/qualification records remain historical evidence, not new release acceptance.

Old IDs must not be reused. New epics begin after the highest previously used epic ID. Historical workflow-state files and checkpoint evidence are not rewritten to manufacture terminal success. Current planning entry points must explicitly prevent their accidental resumption.

| Existing epic | Recorded stories | Disposition |
| --- | --- | --- |
| 1 — Foundation & Identity | 6 done | Completed history; preserve implementation. |
| 2 — System & Repository Management | 5 done | Completed history; preserve implementation. |
| 3 — LLM Provider & Database Connection Configuration | 3 done | Completed history; preserve implementation. |
| 4 — Pre-Scan Profiling | 3 done | Completed historical delivery; later CGC retirement does not change that record. |
| 5 — Core Single Repository Scanning | 10 backlog | Replace active decomposition; reuse equivalent assets from later work. |
| 6 — Third-Party Library Handling | 3 backlog | Replace; ordinary standalone scanning stays current, specialized work has explicit dispositions. |
| 7 — Cross-Repository Detection | 9 backlog | Replace with generic observations, investigation and bounded reconciliation. |
| 8 — Dashboard & Real-Time Monitoring | 7 backlog | Split retained progress/reader/basic graph from deferred HITL/live graph. |
| 9 — Chat Interface | 5 backlog | Replace with the approved wiki-first conversational contract. |
| 10 — Git Monitoring & Incremental Re-scan | No drafted stories | Preserve deferred FR-9 scope; remove obsolete active-queue entry. |
| 11 — August 15 Demonstration Slice | 4 done; 11.5 incomplete | Retire release routing, preserve exact completed and incomplete evidence. |
| 12 — August 30 Bounded Production Scan Recovery | 4 done; 12.5 terminated incomplete | Preserve accepted portions; no automatic resumption or completed-release claim. |

There are 61 drafted numbered stories: 25 recorded done, 34 backlog, and the two unfinished release stories. SP.1 is an additional completed historical gate. No Epic/Story 13+ was found in the bounded current planning/specification inventory.

### Proposed replacement epic topology

These are outcome boundaries for approval, not implementation-ready story definitions. Detailed stories and acceptance criteria follow reconciled PRD/architecture approval through the current epic-authoring workflow. Use new IDs, not renamed Story 5.x/7.x/9.x tasks.

| Proposed epic | User-visible outcome and essential boundary | Dependencies |
| --- | --- | --- |
| 13 — Independent Repository Scan and Local Wiki | One real Scan action, declared bundled Docker tools, retained exact source, generic preparation, fair bounded file work, meaningful Code Units, local BL publication and usable basic document reads/progress. Accept shared identity/acceptance/read-selection contracts before dependent implementations split. | Preserved administration/persistence assets, adapted explicitly. |
| 14 — Cross-Repository Business Understanding | Source-backed communication observations, missing/late targets, one-to-many relationships, focused investigation, cycles, generic Logical Flows and bounded/coalesced refresh across independent Repositories and Systems. No fixed-pair or exact-demo profile authority. | 13's accepted source/Knowledge/Scan contracts. |
| 15 — Useful Streaming Wiki Conversations | Multi-document/follow-up Business and IT answers, server-enforced modes, authorized scope selection, durable conversations, stable turn reads, genuine provisional streaming, exact useful references and same-turn reconnect. Preserve the original authorized auto-scope behavior when selection is empty. | 13's published Knowledge contract; 14 supplies Cross-Repository answer scenarios. |
| 16 — Version-Aware Knowledge Exploration | Complete reader/history/citation navigation, CL source and BL Logical Flow browsing, basic manual-refresh scoped graph and truthful partial/stale/unavailable-source states. Reader access is not management authority. | 13–14; shared citation/read contract with 15. |
| 17 — Recoverable Product Operations and Qualification | Portable configuration, sanitized activity trace including the retained log output obligation, coherent data/source/key backup/restore, readiness/limits/diagnostics, recovery and representative whole-product quality qualification. | 13–16's delivered boundaries. |

Docker, safety, recovery and testing are **not postponed wholesale to Epic 17**: each earlier epic must work through the declared runtime and verify its own failure/access/publication boundary. Epic 17 integrates and qualifies the complete service. Likewise Epic 13's local path is an intermediate outcome, not completion of the Cross-Repository product.

Keep an explicit original-capability backlog for Prompt Workbench (FR-5.9), dedicated unknown-library inference (FR-8.5), and general Repository/System/global MCP configuration/consumption (FR-13.1/13.2/13.5). Their requirements are not erased or marked done by the five core replacement epics. They need their own later design/decomposition; absence from AD-1–AD-32 is not a deferral approval. Preserve their existing priority and do not auto-dispatch undefined work from this register.

Completing Epics 13–17 would not, by itself, complete every PRD obligation. These retained requirements remain outstanding until delivered or explicitly deferred/retired in a later approved disposition. The new epic artifact must expose that distinction instead of reporting full-V1 coverage from the core sequence alone.

## 4. Product and terminology changes

The complete source PRD contains 89 FRs and 15 NFRs. Every one is individually mapped in the supporting requirement disposition report. The accepted direction requires these changes:

| Area | Old direction | Proposed current direction |
| --- | --- | --- |
| Scan scope | Fixed two-Repository release coordination or separate stage calls | One Repository Scan action owns durable source-to-knowledge work; System/selection actions start independent runs. |
| Preparation | Java/XML-specific semantic preparation, preallocated units and communication truth | Generic inventory, advisory overview and required tool preparation; detailed meaning belongs in Pass 1. |
| File and Code Unit | A Worker per preallocated unit, or a file/segment treated as the Document identity | One whole-file workstream discovers meaningful Code Units and their separate Code Logic Documents. |
| Source | Disposable prepared workspace and public snapshot/version confusion | Retained exact committed source behind a Source Snapshot capability; visible provenance remains Repository plus Profiled Revision and source locations. |
| Tools | Custom graph-query protocol or inherited CGC expectations | Native CodeGraph MCP with application-owned bindings; language-neutral LSP tools and bounded source/format access. No CGC restoration. |
| Scheduling | Superstep/draft cross-verification and pair-wide barriers | Deterministic fair bounded concurrency, accepted file evidence, root-run checkpoints and explicit recovery. |
| Resolution | Fixed communication profiles, global timing assumptions and deadlock-breaking | Generic source-backed observations, pinned bounded reconciliation and focused investigation; cycles and missing external facts remain explicit. |
| Business Logic | Mandatory remote trace and no V1 refresh after resolution | Local or Cross-Repository Logical Flows; coherent per-flow synthesis/publication can refresh after new accepted evidence without re-analyzing unchanged source. |
| Knowledge | Source folders as the only organization and current-graph traversal of old Documents | Stable Logical Documents with immutable versions; CL source views, BL Logical Flow views and exact historical dependency selections. |
| Chat | Selected-candidate answers, in-memory history and completed-answer replay | Useful multi-document wiki synthesis, durable conversations and genuine provisional streaming with one durable completion. |
| Chat access | Prompt-only interpretation of Business/IT differences | Business reads BL only; IT may read CL and exceptional exact source. Enforce scope and live authorization in tools, history and views. |
| Reader/graph | Management-only access and live portfolio animation | Knowledge-authorized reader/history/citations and a basic manually refreshed scoped graph; real Scan progress and Chat streaming remain. |
| Runtime | Host-installed August runtime or assumed Docker readiness | Target two-service Docker: app plus bundled local tools, and PostgreSQL; portable configured limits, source/key persistence and qualified restore. |

### Exact requirement and collateral dispositions

| Source clauses | Before | Proposed replacement or preservation |
| --- | --- | --- |
| PRD §0.1, §5.16, competition portions of §0/1/2/7/8/9/10 | August release acceptance, fixed pair/sample/language scope, screenshots and deadline cut rules appear current. | Historical appendix and scoped supersession links. Current success measures cover useful generic one/many/standalone knowledge and Chat, not evidence-production ceremonies. Preserve readability, usability and source-traceability outcomes. |
| FR-3.1/3.3, FR-4.1–4.7/4.9, FR-12.1–12.4 | Model/planning roles, preallocated units, supersteps, fixed preparation and stage boundaries. | Approved AD-1–AD-14/17–23 ownership, generic file policy, separate advisory overview, semantic-unit acceptance, pinned flow generation and real progress. Provider configuration remains; no automatic failover is inferred. |
| FR-4.10, FR-6.2/6.3/6.6; glossary resolution examples | Only source changes regenerate; post-scan deadlock protocol and owner-only stale notices. | Immutable history plus bounded accepted-evidence reconciliation/per-flow refresh; no source-scan cascade or global wait. Scope narrowing alone cannot invalidate broader accepted relationship evidence. |
| FR-5.1/5.8/5.13/5.14, FR-8.4/8.5 | Mandatory catalog/deployed mappings, mock topology and blanket library understanding. | Optional snapshotted context and generic observations. Decisive absent external facts stay Uncertainties; hypotheses are labelled, never source-qualified proof. Preserve dedicated inference/workbench backlog without making it a Scan prerequisite. |
| FR-5.2 and §10 | gRPC is named in a broad example but explicitly excluded from guaranteed protocol scope. | Preserve the explicit protocol-specific exclusion; generic unknown-mechanism observations remain possible. No silent gRPC feature expansion. |
| FR-4.8 | Literal append-only `log.md` with timestamped operation/target entries. | Retain the sanitized activity-trace/output obligation; AD-32 alone does not erase its format. Never log source, credentials or hidden reasoning. A later storage/output redesign needs its own explicit disposition. |
| FR-4.11/4.12 | Week-1 spike and same-superstep peer verification. | Preserve historical spike evidence; no repeating dated gate. Mandatory peer/superstep mechanism is superseded, not falsely credited to focused resolution investigation. Any future peer-review capability needs its own scope. |
| FR-7.1/7.2/7.4, FR-6.4 human action, FR-12.5 | Mandatory owner questions, notification/answer/dismiss/wontfix workflows. | Apply the explicit product HITL deferral; preserve visible Uncertainties, historical decisions and FR-7.3's lower authority of human input. Operational and development approvals remain. |
| FR-8.2/8.3, FR-6.7, FR-9.1–9.3/9.5 | Caller-triggered partial library scans and incremental/source-update engine. | Explicitly deferred. FR-8.1 ordinary standalone Scan and FR-9.4 Document history remain current. |
| FR-10.1–10.3 | Database administration and Worker database lookups are bundled. | Preserve existing configuration; defer new agent-facing schema/sample lookup under AD-22. Non-production/read-only restrictions survive. |
| FR-11.1–11.8, FR-12.6 | Fixed tool/packet/answer selection, technical citation leakage risk, attachments and live graph. | AD-24–AD-29 wiki-first multi-turn streaming and mode-safe reads/history; attachments and advanced graph deferred. Retain selection defaults/authorized auto-scope and basic graph/reader. |
| FR-13.1–13.5, FR-15.1–15.3 | Blanket MCP/LSP backlog grouping. | Required native CodeGraph MCP and selected LSP are current; outward wiki MCP deferred; general custom MCP management stays original backlog with separate purpose approvals. Initial LSP disables automatic import/build/restore, not all network traffic. |
| FR-14.4 | Context edits have a mandatory HITL exception. | Snapshot advisory context for active runs; no hidden HITL mutation path. Later Chat contexts use their own scoped current knowledge. |
| NFR-1.1–1.3, NFR-3.1–3.3, NFR-4.1 | Demo timing, universal 30-second/five-Document target, live-graph timer, automatic recovery and host-only exception. | Configured, measured qualification, honest progressive coverage and bounded recovery; no five-Document cap. Explicit Scan resume; Chat interruption differs. Two-service Docker and coherent recovery are target obligations, not claimed current behavior. |
| NFR-5.2 and A-6 | Accessibility noncommitment justified by the competition. | Keep the basic accessibility floor and existing no-certification commitment; remove only the expired competition rationale. |

Retain unaffected FR-1/2, applicable FR-3/5/6/7/8/9/10/13/14 clauses and NFR security/usability/maintainability constraints as individually recorded in the requirement map. Mixed clauses are split rather than discarding safety or history with a deferred UI feature. Stable IDs remain intact even where the obligation is now explicitly historical/deferred.

### Canonical glossary edits

Retain stable System/Repository/Document/Edge concepts. Revise Worker Agent, Pass 1, Pass 2, Profiled Revision, Source Anchoring, Cross-Repository Resolution, Dangling Link and concurrency definitions. Add Source Snapshot, Tool Workspace, Repository Scan Run, Scan Campaign, accepted file manifest, Logical Flow, Flow Group Version, Resolution Reconciliation and Chat read/turn context terms where needed by the adopted design.

Superstep, Document Pre-Allocation, File Code Unit, Prepared Scan Manifest, Pre-Scan Result/Handoff, Communication Kind Classification, Two-Repository Scan Coordination and Deadlock-Breaking Protocol describe historical contracts, not compulsory generic behavior. The current glossary must label/move their historical meanings explicitly rather than silently redefine persisted types. Keep update/impact concepts as deferred, without making the old four-step SQL cascade or first-4096-byte rename heuristic a new semantic guarantee. No second `CONTEXT.md` glossary should compete with `docs/UBIQUITOUS_LANGUAGE.md`.

### Scope preservation

Preserve identity/roles, System and Repository management (including standalone Repositories), provider configuration/overrides, encrypted secrets, database configuration and advisory Global/System/Repository context. Preserve Chinese-only UI, Library Oak styling, chat-first navigation, the basic accessibility floor, validation and authorization requirements. No undocumented feature deletion or new compliance certification is implied by removing competition language.

The user's explicit deferrals remain deferred: automatic/incremental update scanning; external operational-information integration; outward knowledge MCP; Chat attachments; product HITL notification/inbox/answer-dismiss; Worker-triggered partial library scanning; multiple LSPs per snapshot and dependency-aware imports/restores; advanced/live portfolio graph. These do not remove ordinary standalone scans, visible Uncertainties, internal wiki tools, native CodeGraph MCP, operational pause/resume, or development approval gates.

## 5. Artifact adoption and verification

Canonical updates require complete-proposal approval under `bmad-correct-course`. The approved edit boundary will be:

| Artifact | Proposed edit |
| --- | --- |
| `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md` | Revise in place, retain 104 IDs, explicit current/deferred/original-backlog/history classification and new accepted clauses. Snapshot old content; preserve the existing historical decision log. |
| `docs/UBIQUITOUS_LANGUAGE.md` | One canonical target glossary; concise generic definitions and clearly separated historical terms. Update PRD glossary references consistently. |
| `docs/bmad/planning-artifacts/architecture.md` | Current authority gateway plus preserved platform/project/security conventions; remove active August/readiness assertions from the current contract. Preserve the exact former body in the pre-adoption archive. |
| `docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md` | Adopt the reviewed AD-1–AD-32 content once, preserving IDs/substance, with portable repository references and explicit design-versus-runtime status. Keep external original/reviews unchanged. |
| Five old `architecture/` companions | Retain dated bodies and paths; add scoped historical/supersession notices after preserving exact originals. For the decision log, append a dated disposition rather than rewriting prior entries. |
| `docs/bmad/epics.md` | New active epic/story definitions plus concise historical/disposition links and retained-capability register. No reuse of 1–12 or relabeling old stories as new work. |
| `docs/bmad/implementation-artifacts/sprint-status.yaml` | Archive exact prior state; replace obsolete August scope/sequence with accepted new backlog through the current sprint-planning skill. Use supported statuses only; preserve old incomplete/terminated facts in historical records. |
| `.specify/feature.json` | Archive and remove the selector of terminated `specs/014`; leave that feature directory untouched. No fake replacement feature. With no selection, feature-specific commands must fail closed until deliberate new activation. Clear only stale `SPECIFY_FEATURE*` overrides in the execution session, not global user settings. |
| `AGENTS.md` | Current authority/status pointers, deterministic Scan Control/MAF distinction, no implied failover, immutable evidence-driven regeneration. Remove the stale assertion that completed specs/010 is the current plan; preserve coding/security/UI/testing and all approval gates. Respect managed markers; select a real new feature before a context updater writes its plan. |
| `README.md`, `README.zh-CN.md` | Fix current CGC/runtime/readiness claims and July architecture links; distinguish retained code from the unimplemented target. Keep English/Chinese versions aligned. |
| UX `DESIGN.md`, `EXPERIENCE.md` | Scope-align HITL/graph, CL versus BL organization, Knowledge-authorized reader, mode-safe citations, real streaming/reattachment, explicit Scan resume and truthful progress. Preserve brand, Chinese UI, basic interactions and accessibility floor; no broad visual redesign. |
| Pre-adoption archive and adoption ledger | Exact snapshots/hashes and old→new requirement/story/authority crosswalk. Mark old readiness reports, specs, workflow states, evidence and correction records historical through the current gateway; do not rewrite them to claim new completion. |

The five companions are `scanning-v1-technical-plan.md`, `scanning-v1-data-contracts.md`, `august-30-bounded-scanning-design.md`, `document-graph-anchordb-plus-plan.md`, and `document-graph-decision-log.md` under the architecture directory. All existing `specs/*`, story evidence, migrations and source remain unchanged by this correction.

Static selector inspection found no dedicated clear command: installed `.specify` resolution accepts an absent selector only as a missing-context error/null, without branch/highest-feature fallback. A stale `SPECIFY_FEATURE_DIRECTORY` can recreate the selector, so it must not be inherited into adoption. Ordinary prerequisite/setup commands can persist context; use only the explicitly non-persisting path for diagnostics. This proposal changes selection metadata, not the historical feature's lifecycle record or the scripts themselves.

Verification must establish:

- Every existing requirement has an explicit disposition; all AD-1–AD-32 decisions are represented without contradiction.
- Current planning documents agree on vocabulary, ownership, scope, tool policy, publication/Chat semantics and deployment.
- No active queue or current entry point directs execution back to the retired competition stories.
- Historical completion/termination evidence and pre-adoption file contents remain recoverable with exact hashes.
- New epics/stories cover retained next-phase requirements and declared dependencies; deferred requirements remain discoverable, not silently lost.
- Current workflow validators accept the regenerated tracker; obsolete readiness reports remain historical rather than claiming the new plan is implementation-ready.
- The change set is limited to approved documentation/tracking paths. Production/runtime qualification remains future work.

### Change-navigation checklist

| Checklist | Status and evidence |
| --- | --- |
| 1.1–1.3 Trigger/context/evidence | Complete: failed CGC path, terminated release, approved decommission, pinned assessment and reviewed target. |
| 2.1–2.5 Epic impact/replacement/order | Complete: all 12 epics classified, 34 unstarted stories identified, histories retained, replacement topology proposed. |
| 3.1–3.4 Artifact impact | Complete: all 104 normative requirements mapped; glossary, architecture, UX, entry points and selectors covered. |
| 4.1–4.4 Alternatives/path | Complete: forward replacement selected; no code rollback, another active overlay or implicit scope shrink. |
| 5.1–5.5 Proposal and ownership | Complete: above outcomes/deltas/validation and below staged handoff. |
| 6.1–6.2 Completeness/accuracy | Source checks complete; final proposal consistency check recorded with supporting evidence before approval request. |
| 6.3 Complete-proposal approval | Complete: user explicitly replied "Approve" on 2026-09-11 to the complete proposal. |
| 6.4 Tracker update | Pending approval and accepted replacement definitions; not performed by this proposal. |
| 6.5 Handoff | Defined below; execution begins only after approval. |

### Supporting evidence

The detailed requirement map (104 individual rows), complete epic/story disposition and architecture heading-level before/after extraction are retained under `C:/Users/13728/Documents/VulgataSepAudtion/formal-reconciliation-2026-09-11/`. They are supporting analyses, not coequal PRDs or implementation authority. The pinned repository commit and pre-adoption hashes identify source boundaries. Later explicit decisions override only their stated scope; document date alone is never blanket precedence.

### Proposal validation checkpoint — 2026-09-11

An independent consistency review returned **approval-ready, no concrete material fixes**. A separate mechanical check confirmed 104 unique source FR/NFR IDs and 104 individual mapping rows, with zero missing/extra/duplicate IDs. All 16 canonical document/selector pre-images in `source-boundary.json` remained hash-identical; the only repository addition is this proposal. Whitespace checking found no whitespace errors (Git reported only normal new-file/line-ending differences). No build, test, runtime or provider operation was performed. The supporting `proposal-review.md` is an approval-readiness check, not user approval or completed formal adoption.

## 6. Approval and handoff

The user explicitly approved this complete proposal on 2026-09-11 by replying "Approve" after the proposal approval request. The approved pre-image SHA256 is `EE7878FD8AF814BCFAE2D2ECA6C7305BB56BBF2618E4AD124F8CDB97432C675A`, preserved in the pre-adoption archive. Approval accepts the above documentation/backlog correction, including retirement of the active Epics 5–10 definitions, explicit incomplete-history disposition of 11.5, and the proposed five-epic replacement topology. It does not mark the goal complete: canonical adoption, full story decomposition and verification still have to be performed.

| Owner/lane | Next authorized responsibility after approval |
| --- | --- |
| Coordinating design session | Maintain authority/requirement crosswalk and sole integration ownership; preserve snapshots, resolve conflicts and verify the complete planning change. |
| PRD/domain work | Update the canonical product clauses/glossary with stable IDs and explicit dispositions; source reconciliation and review before finalization. |
| Architecture work | Adopt the existing reviewed spine and coherent gateway/platform rules; no new tool or Scan implementation hidden in document edits. |
| Epic/sprint work | Derive complete user-value stories from accepted inputs, review coverage/dependencies, regenerate the supported queue and verify no stale selectors. |
| Future story runner | Only after separate story activation: implementation plans, triggered approvals, TDD, reviews and proportional real qualification. |

After proposal approval, use the current PRD/domain/architecture workflows to adopt the changes, then `bmad-create-epics-and-stories` and sprint planning for the replacement queue. Reload current skills rather than importing old workflow instructions. Story implementation remains governed by the current `run-bmad-sprint-loop`; tool, Scan-strategy and PRD planned-change gates are not waived. No expired overnight authority applies, and this planning correction does not authorize production implementation or a Git commit.
