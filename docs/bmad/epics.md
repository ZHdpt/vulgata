---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md
  - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md
  - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-11-generic-product-reconciliation.md
currentStep: complete
status: final
definitionValidatedOn: 2026-09-11
readiness: ready-for-governed-story-planning
newStoryImplementationStatus: backlog
inputSetConfirmed: 2026-09-11
requirementsConfirmed: true
requirementsConfirmedOn: 2026-09-11
epicDesignConfirmed: true
epicDesignConfirmedOn: 2026-09-11
epicDesignSourceSha256: 83B4E0157D968A998AC1DCAF3B40CACAAE629893652801C70FE3CD9E7DE80281
storiesConfirmed: true
storiesConfirmedOn: 2026-09-11
epicDefinitionsCompleted: [13, 14, 15, 16, 17]
approvedStories: ['13.1', '13.2', '13.3', '13.4', '13.5', '13.6', '13.7', '13.8', '13.9', '13.10', '13.11', '13.12', '13.13', '13.14', '13.15', '13.16', '13.17', '13.18', '13.19', '14.1', '14.2', '14.3', '14.4', '14.5', '14.6', '14.7', '15.1', '15.2', '15.3', '15.4', '15.5', '15.6', '15.7', '15.8', '16.1', '16.2', '16.3', '17.1', '17.2', '17.3', '17.4']
explicitlyApprovedStories: ['13.1', '13.2', '13.3']
delegatedStories: ['13.4', '13.5', '13.6', '13.7', '13.8', '13.9', '13.10', '13.11', '13.12', '13.13', '13.14', '13.15', '13.16', '13.17', '13.18', '13.19', '14.1', '14.2', '14.3', '14.4', '14.5', '14.6', '14.7', '15.1', '15.2', '15.3', '15.4', '15.5', '15.6', '15.7', '15.8', '16.1', '16.2', '16.3', '17.1', '17.2', '17.3', '17.4']
boundaryApprovalMode: delegated-when-confident
boundaryApprovalDelegatedOn: 2026-09-11
---

# Vulgata - Epic Breakdown

## October release routing — adopted September 27, 2026

The user approved adding October Epics 18–20 to the canonical sprint tracker while
preserving Epics 13–17 and their existing statuses. The authoritative October
requirements, shared rules and 17 story definitions remain in
[October release epics](planning-artifacts/october-release-epics.md). Read that
document for any 18.x, 19.x or 20.x story; the generic definitions below do not
override its explicitly scoped October decisions.

When refreshing sprint tracking, pass both this file and the October release epic
file to `sprint_plan.py generate`; neither input alone represents the whole tracker.
October stories are backlog. This routing does not activate a feature or authorize
implementation; use `run-governed-delivery` and retain all formal story gates.
The September 11 metadata and definitions below retain their original scope.

## Overview

The user confirmed the requirement inventory and approved the five-epic design with C under the [September 11 planning correction](planning-artifacts/sprint-change-proposal-2026-09-11-generic-product-reconciliation.md). All four definition steps are complete: 41 stories across Epics 13–17 passed coverage, ownership, dependency and readiness checks. Stories 13.1–13.3 were explicitly approved; later boundaries were accepted under the user's confident-boundary delegation. The backlog is ready for governed story specification and technical planning, not direct implementation or runtime qualification. [Sprint tracking](implementation-artifacts/sprint-status.yaml) owns the active delivery queue; deliberate feature activation and later plan/approval/verification gates remain required.

The inventory preserves all 89 functional and 15 non-functional requirement IDs and their exact canonical PRD text/status. Architecture and UX requirements are extracted separately; they add implementation constraints without replacing product dispositions. Current means required, not delivered. Deferred, Original unmet backlog and History never earn current coverage credit. Mixed-status requirements retain each clause's disposition. The canonical [glossary](../UBIQUITOUS_LANGUAGE.md) owns terminology.

### Historical disposition and reserved future IDs

The [exact pre-adoption epic document](history/pre-generic-adoption-2026-09-11/files/docs/bmad/epics.md) preserves the former definitions and evidence. Its SHA-256 before replacement was `2BD311C87BA4595FD567F3F7722EF88859894EA64E2CC04A8D25FA610F2E67C3`; the active pre-image was verified identical.

- Epics 1–4 retain completed history and reusable implementation.
- Stories 11.1–11.4 retain completed history; Story 11.5 remains incomplete. The old release route is not active.
- Stories 12.1–12.4 retain accepted/completed history; Story 12.5 remains terminated incomplete. No release-completion or automatic-resume claim is made.
- Old Epics 5–10 are superseded as active planning definitions, not marked done. SP.1 and previous reviews/qualification remain historical evidence.
- Epic IDs 13–17 and all 41 story definitions are accepted; new story implementation remains backlog. IDs 1–12 will not be reused.

The original-capability register remains visible through FR-5.9 (Prompt Workbench), FR-8.5 (dedicated unknown-library inference, with the notification portion deferred), and FR-13.1/13.2/13.5 (general Repository/System/global MCP configuration/consumption). These need later design/decomposition or explicit disposition. The five core replacement epics do not by themselves represent full-V1 coverage, and this register does not authorize dispatch.

### Extracted source boundary

Hashes identify the read inputs, not runtime acceptance. Revalidate affected requirements/stories if an owning source changes; do not silently carry these definition approvals onto changed scope.

| Input | SHA-256 |
| --- | --- |
| [Canonical PRD](planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md) | `9DEE4DDA7920C9DFAB8D0FF4E6FFA5AEE2F6DE7942BECBF32349C9E3584A1399` |
| [Canonical glossary](../UBIQUITOUS_LANGUAGE.md) | `680494A9E766270A18AE02D5C92670771A72912A87F632946350431D4696550D` |
| [Architecture gateway](planning-artifacts/architecture.md) | `DDD60353114C2C5A16C36074DA170A5AF97DB2C97C0BB0DC4B481A5F471F667D` |
| [Architecture spine](planning-artifacts/architecture/ARCHITECTURE-SPINE.md) | `A55381DF21FE95E827664CA6B2082FB0D30054CFC6A075AD93DF3638F6261943` |
| [UX DESIGN](planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md) | `87B022E601BC4603AB75DC0211C1E10F1F0F4B90CEB3250ECCA8166FFA3C2CAC` |
| [UX EXPERIENCE](planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md) | `A550D650B01F6EF732FC4F11004D6EADDCCF511FE34916F1B1CE5ECE3AF1B87F` |
| [Approved proposal](planning-artifacts/sprint-change-proposal-2026-09-11-generic-product-reconciliation.md) | `C62312D92E5F32CFCCCE639D79AB7AA46C15F2223836679AAE09F842921D3BAA` |

## Requirements Inventory

### Functional Requirements

All 89 entries below are copied verbatim from the canonical PRD §3, preserving every ID and status classification.

- **FR-1.1:** [Current] Users shall register with email and password.

- **FR-1.2:** [Current] Users shall log in and log out.

- **FR-1.3:** [Current] Users shall view/edit their profile, including display name, email and password change.

- **FR-1.4:** [Current] Retain Administrator, System Owner and User roles. Administrators assign roles; System Owners manage authorized Systems; Users consume permitted Chat/Documents. Self-service role requests and new fine-grained RBAC are outside scope. Knowledge, history and source disclosure shall enforce current server-side authorization.

- **FR-2.1:** [Current] System Owners shall create/edit/delete authorized Systems with name, description and optional context. Organizational changes do not themselves prove or disprove communication.

- **FR-2.2:** [Current] System Owners shall add Repositories with name, description, Git remote and optional context, without fixed-pair, sample-name or release-profile prerequisites.

- **FR-2.3:** [Current] System Owners shall remove Repositories from Systems. Membership/visibility changes preserve immutable history and current access rules; they shall not silently replace accepted relationship evidence.

- **FR-2.4:** [Current] System Owners shall create standalone Repositories for organization-owned shared-library source and explicitly Scan them through the ordinary static pipeline.

- **FR-2.5:** [Current] Repository creation includes best-effort Git reachability validation; authentication failure does not prevent creating the record. A later Scan must acquire/verify exact authorized source or report its prerequisite failure.

- **FR-3.1:** [Current; revised] Administrators shall maintain the global provider registry and default task assignments, including provider names, endpoints, protected keys and supported API types. Preserve chat-completions, responses and messages configuration vocabulary without implying every adapter is qualified. PreScan model work is bounded advisory overview, not deterministic inventory or scheduling.

- **FR-3.2:** [Current] Multiple providers shall be configurable simultaneously. This does not imply automatic switching or silently changing a running task's provider identity.

- **FR-3.3:** [Current; revised] System Owners shall assign providers per System to permitted overview, Worker Agent and Chat Agent roles using the global registry. Different roles and different Systems may use different providers; deterministic Scan Control needs no model assignment. Configuration remains usable outside demo composition.

- **FR-3.4:** [Current] Administrators shall explicitly test provider connections. Routine health checks shall not incur paid inference; provider availability remains separate from application, database and analysis-tool readiness.

- **FR-4.1:** [Current; revised] PreScan shall inventory exact committed source, record language/format/layout facts and unknowns, and separately provide bounded advisory overview. Explicit policy selects primary/supporting evidence. Handwritten code, configuration, templates, schemas and DSLs remain eligible; tests, identifiable generated source and vendored dependencies default to supporting evidence. README/explanatory documents supply context, not automatic Code Logic jobs. Index omission, filenames, unfamiliar extensions or overview ranking shall not silently exclude admitted source.

- **FR-4.2:** [Current; revised] One Repository Scan action durably initiates preparation through knowledge production; System/selection actions start independent runs. Deterministic Scan Control fairly schedules bounded Repository, file, inquiry and synthesis work with shared limits and exclusive file ownership. Workers neither schedule arbitrary Repositories nor await unscanned targets while consuming capacity. Cancellation stops request-owned pending/downstream work without deleting accepted history or cancelling independent consumers of shared evidence. Unavailable generic capabilities remain visibly unavailable; a request shall not silently switch to a legacy pair workflow.

- **FR-4.3:** [Current; revised] Pass 1 processes a logical file workstream, discovering meaningful Code Units and a separate Code Logic Logical Document for each. Large files may require several runs/windows; those boundaries do not redefine semantics or truncate coverage. Other files provide authorized context. Workers propose evidence/content; the application derives identities, validates provenance and accepts knowledge. Legacy file Documents shall not be relabelled as newly analyzed semantic-unit results; preserve their historical meaning and explicit lineage into new accepted analysis.

- **FR-4.4:** [Current; revised] Accept validated file results and authoritative progress together. Only accepted results expose Code Units, Documents and observations to resolution; drafts remain unavailable as knowledge. Failed required files expose gaps, and zero-unit outcomes require truthful dispositions. Preallocated tool symbols and model-produced edges are not permanent identity or relationship authority.

- **FR-4.5:** [Current; revised] Pass 2 explains pinned bounded directed evidence for source-backed exported operations, handlers, schedules, workflows or selected Code Units. Each root keeps distinct Logical Flow identity; helpers/folders do not merge business purpose. Local/Cross-Repository flows qualify. Synthesis requires explicit outcomes for all admitted primary work in its root run and frozen matching target; paused work does not qualify. Accepted external files may contribute before their whole runs finish. Synthesis may verify details with the same authorized source and declared tools, without changing accepted Code Logic or authoritative Edges; discrepancies become explicit uncertainty or bounded correction proposals, not recursive automatic repair. Explanations include branches, effects, bounds and unknowns, with exact dependencies for every Code Logic/source contribution actually used.

- **FR-4.6:** [Current; revised] Code Logic browsing provides a virtual Repository/file/Code Unit hierarchy without synthetic directory Documents. Business Logic browsing groups Logical Flows through System/Repository views without duplicating cross-Repository Documents. Preserve nesting and justified identity lineage; moving ranges, unproven renames, splits or merges shall not silently reuse identity.

- **FR-4.7:** [Current; revised] Persist canonical Code Logic and Business Logic Document content as structured JSON; Markdown or other human-readable forms are derived when needed, not parallel editable authorities. Accepted knowledge supports PostgreSQL-backed sections/search and authorized version-aware browsing. Current Code Logic selection and coherent per-flow Business Logic publication differ from drafts and labelled history. Failed current files shall not silently inherit old versions as newly analyzed. Other staged/history results require labelled inspection; selection must expose coverage gaps. A flow's content, dependencies and selection become visible together. Optional index.md remains an export, never canonical retrieval; incompatible evidence selections require explicit qualification.

- **FR-4.8:** [Current] Maintain an append-only sanitized log.md trace for Scans, queries and system events using `## [YYYY-MM-DD HH:MM] operation | target`. Represent queries/targets without raw source, credentials, hidden reasoning or unnecessary sensitive content. Structured diagnostics may supplement, not silently replace, this retained output.

- **FR-4.9:** [Current; revised] Require authorized Source Snapshot acquisition and CodeGraph preparation/native MCP access, with declared applicable LSP and structured-content capabilities. Readers get scoped retained-source access, not arbitrary host paths. Tools provide evidence/limitations, not complete coverage or permanent Code Unit identity. Retain exact source needed by selected current knowledge and active work, including older contributing revisions; reacquisition must verify identical bytes. Public provenance remains Repository, Profiled Revision and source locations, not another snapshot version axis. Required CodeGraph failure shall not silently trigger source-only fallback; CGC is not restored.

- **FR-4.10:** [Current; revised] Documents remain immutable without human content editing. New versions may follow source analysis or changed accepted relationship/flow/evidence dependencies. Preserve exact historical provenance and Version Chains; REFERENCES may change without rewriting caller Code Logic or retargeting old GENERATED_FROM. Ordered current-promotion intent prevents late older work replacing newer selections; history-only analysis cannot implicitly promote. When validated replacement-source evidence proves removal, withdraw/retire that current knowledge while preserving history. Omission, exclusion, access loss or failed analysis alone cannot prove deletion or erase history.

- **FR-4.11:** [History] Preserve the original Week-1 Worker adapter spike and actual evidence, without repeating its expired schedule or treating it as generic runtime qualification. Current agent/tool integration requires independent qualification.

- **FR-4.12:** [History] Mandatory same-superstep peer verification, fixed 0.5 confidence and yellow-dashed/HITL treatment are superseded. Focused resolution inquiry is not its completion. Future peer verification requires its own scope; confidence alone never proves an Edge.

- **FR-5.1:** [Current; revised] Preserve optional per-System Communication Pattern Catalog/framework context. Generic Scan shall not require a catalog, detailed service-to-Repository mappings or their maintenance workflow. Context assists interpretation, not deployed identity. The earlier deferred per-Repository catalog migration note grants no automatic migration.

- **FR-5.2:** [Current; revised] Detect source-supported RPC, HTTP, queue, file/storage and page-navigation communication, recording mechanism, operation, roles, namespaces, source literals/expressions and unknowns. Extensible observations do not guarantee universal protocol understanding. gRPC-specific guaranteed implementation remains excluded; unknown mechanisms may still be represented honestly.

- **FR-5.3:** [Current] Record provider/consumer roles with supporting evidence, including unknown/absent parties. Static linkage is not proof of deployed routing or runtime execution.

- **FR-5.4:** [Current] Support multiple consumers per interface with evidence/access-scoped counts. Counts must not leak restricted Repositories or imply complete deployment inventory.

- **FR-5.5:** [Current] Record available identifiers for unscanned, unavailable or unknown parties as external references and Uncertainties. Their absence shall not block otherwise valid analysis or initiate automatic Scans.

- **FR-5.6:** [Current; revised] Page-navigation observations disclose loose coupling and unresolved identity. Confidence labels alone cannot establish provider/consumer relationships.

- **FR-5.7:** [Current; revised] Investigate indirect targets through available object/provenance evidence. Bounded tools/inquiry may clarify candidates; missing classpaths, incomplete indexes and unresolved expressions remain limitations, not negative proof or invented endpoint values.

- **FR-5.8:** [Current; revised] Workers receive relevant bounded snapshotted advisory context, including supplied framework/catalog knowledge. Mutable catalogs, fixed XML recipes and demo profiles are not mandatory communication truth. Preserve context provenance and separation from source facts.

- **FR-5.9:** [Original unmet backlog] Retain a dedicated development-time Prompt Workbench for refining prompts against known patterns. Validated prompts/context may assist detection, without mandatory catalog coupling. Externalized prompts/native tools do not fulfill this distinct feature; later design or explicit disposition is required outside the core sequence.

- **FR-5.10:** [Current; revised] Preserve named RPC endpoint evidence for unknown providers. It is discoverable through authorized mode-appropriate wiki content/retrieval, without raw technical disclosure to Business Mode or a separate tool per record type.

- **FR-5.11:** [Current; revised] Treat supported file/storage communication as observations with roles/locations. Shared paths, mounts or object labels alone cannot prove cross-Repository identity. Do not fetch external storage or operational configuration to fill gaps in this scope.

- **FR-5.12:** [Current; revised] Record supported URL patterns, deep links and app-to-app navigation with explicit uncertainty about bindings/counterparties. A possible static path is not an executed journey.

- **FR-5.13:** [Deferred / History] Retire mandatory mock Service Topology MCP and prepopulated owner mappings. Defer a uniform third-party operational-information interface, including adapters, credentials and refresh policy. Decisive absent database, gateway or registry facts remain Uncertainties; introduce no placeholder integration.

- **FR-5.14:** [Current; revised] Documented configuration supports new patterns and organization-wide framework knowledge without code changes within its supported format. New syntax/dialects may require implementation; dynamic parser generation, plugin loading, configuration execution and external fetching are not promised. Preserve structure/locations despite unknown meaning and distinguish malformed recognized content.

- **FR-6.1:** [Current; revised] Record Uncertainties for missing/ambiguous targets, bindings, mechanisms and capabilities, retaining known/unknown-target distinctions where meaningful. Workers propose; Knowledge owns accepted evidence/state. Valid semantic uncertainty may accompany accepted analysis.

- **FR-6.2:** [Current; revised] Reconcile finite pinned accepted evidence across authorized discovery, including other Systems, late targets and accepted files of unfinished external runs. Select a designated analysis candidate per Repository, not arbitrary history. A unique visible candidate, shared name or confidence alone cannot prove a match. Focused bounded inquiry gathers supporting/opposing evidence; domain validation decides relationships. Deduplicate unchanged inquiries and recheck access/bindings before acceptance; publication cannot recursively trigger investigation.

- **FR-6.3:** [Current; revised] Preserve cycle safety without historical fewer-pending-first/older-scan-yields waiting. Record cycles, terminate bounded traversal and give every selected matching item an explicit outcome. Unknown targets shall not hold Worker capacity indefinitely or create global completion barriers.

- **FR-6.4:** [Current / Deferred] Preserve nonblocking Dangling Links, available identifiers and historical human wontfix decisions. Defer new owner accept/dismiss/wontfix actions with product HITL; uncertainty visibility/history remain current.

- **FR-6.5:** [Current] Provide Chat-accessible unresolved-link explanations/counts, with endpoint/target information only where permitted by evidence, mode and live authorization. Aggregates must not leak restricted facts.

- **FR-6.6:** [Current; revised] Show stale/pending/failed refresh. Changed accepted evidence may trigger bounded reconciliation and coalesced per-flow refresh without source re-analysis; active inputs stay fixed. Publication shall not create endless refresh. Scope narrowing/access loss alone changes visibility or records unevaluated scope, not broader accepted relationship truth. Automatic source-Scan cascades remain outside current scope.

- **FR-6.7:** [Deferred] Incremental changed-source Impact Analysis/reuse, including the former four-stage cascade, remain deferred. Static replacement/retirement and later-knowledge reconciliation are current, not completed incremental scanning.

- **FR-7.1:** [Deferred] Worker question notifications and mandatory owner response are deferred. Visible source-backed Uncertainties may state missing information without creating an inbox or blocking ordinary analysis.

- **FR-7.2:** [Deferred] Dashboard answering and answer attachment are deferred; no automatic repair workflow is substituted.

- **FR-7.3:** [Current] Identify human-supplied context and give it lower authority than code-derived facts. Preserve the distinction in Documents, prompts and dialogue; conflicts shall not silently turn assertions into source-qualified knowledge.

- **FR-7.4:** [Deferred] Pending-question counts and view/answer/dismiss UI are deferred. Operational status, pause/resume, authorization and development approvals remain.

- **FR-8.1:** [Current] Organization-owned library source is addable as ordinary standalone Repositories and eligible for explicit static Scan.

- **FR-8.2:** [Deferred] Caller/Worker-triggered partial on-demand library Scans are deferred. Missing source shall not cause implicit dependency downloads or arbitrary Repository work.

- **FR-8.3:** [Deferred] Multiple-caller library-Scan coordination is deferred with that feature. Ordinary Repository/file ownership and resource controls remain mandatory.

- **FR-8.4:** [Current; revised] Common library imports shall not automatically become cross-Repository communication gaps. Familiarity does not prove all behavior; decisive unsupported behavior remains a limitation or labelled hypothesis.

- **FR-8.5:** [Original unmet backlog / Deferred] Retain dedicated inference for unknown source-unavailable libraries outside the core redesign. Ordinary useful hypotheses do not complete it, and inference is not source proof. Mandatory owner notification is deferred with HITL; external-information lookup is not implied.

- **FR-9.1:** [Deferred] Automatic remote monitoring remains deferred; periodic polling is the retained original direction, not a current background task. Webhook triggers remain excluded.

- **FR-9.2:** [Deferred] Automatic pull-on-behind is deferred. Explicit static acquisition still resolves a selected Git ref once and shall not change an existing run's source.

- **FR-9.3:** [Deferred] Incremental re-scan of changed/dependent Code Units and analysis reuse remain deferred. Repeated explicit static Scans and late-knowledge reconciliation remain current.

- **FR-9.4:** [Current] Previous Document Versions and original dependencies remain accessible under authorization. Reader/history/citation navigation is current minimum product, independent of incremental scanning.

- **FR-9.5:** [Deferred] The incremental-Scan overlap rule stays with deferred update scheduling. Current static runs follow exclusive ownership and ordered publication, not an unintended blanket prohibition inherited from an incremental-only clause.

- **FR-10.1:** [Current] Preserve per-Repository database type, connection details and protected credentials managed by authorized System Owners. Administration does not grant new agent-facing access.

- **FR-10.2:** [Deferred] New Worker database-schema inspection is deferred with external operational-information tools. Available source/accepted knowledge remain the investigation boundary; no placeholder tool is introduced.

- **FR-10.3:** [Deferred] New sample-data query tools are deferred. Any preserved/future surface retains limited rows, non-production/read-only access and blocked writes. Production database access is not authorized.

- **FR-11.1:** [Current; revised] Business Mode provides business narratives in the light theme and reads Business Logic only. Enforce this in tools, history, summaries, graphs and citation targets, not just prompts. Useful Business Logic references remain available without Code Logic/source disclosure.

- **FR-11.2:** [Current; revised] Dark-themed IT Mode permits authorized Business Logic, Code Logic and technical references. Exceptional read-only source inspection may address specific gaps at the recorded Profiled Revision; Chat shall not routinely re-prove the wiki, initiate Scans or repair knowledge.

- **FR-11.3:** [Current; revised] Confirm mode switches, save the authorized session and start a new session with the selected theme/prompt and fresh inference context. Mode/scope changes shall not import disallowed technical history or cached summaries. Preserve durable ordered conversations and bounded useful completed history separately from model context. Summaries retain hypothesis status and contributor access scope, never upgrade speculation. Initially permit one active answer per conversation; replay shall not generate twice.

- **FR-11.4:** [Current; revised] Users select authorized Systems/Repositories; first use defaults to all accessible Systems and permits adjustment/clearing. Knowledge provides a stable published read selection per turn, including discovery, pagination and later-discovered Documents. New publication alone does not invalidate that turn; later turns use fresh permitted knowledge. Authorization stays live.

- **FR-11.5:** [Deferred] Chat file/image upload, attachment storage and parsing are deferred. Typed questions, dialogue and permitted advisory text remain.

- **FR-11.6:** [Current; revised] Synthesize useful multi-document/follow-up explanations through shared wiki search, browse, section/content and existing-related-Document navigation. Tools return provenance, coverage and Uncertainties under the same turn/mode/access context. Retrieval is evidence, not instructions or infallible truth. Do not mandate per-record tools, fixed candidate sentences, embeddings or index.md input. Shared code alone does not prove business dependency.

- **FR-11.7:** [Current; revised] Provide useful claim-linked references to exact permitted Document Versions/sections and appropriate IT source locations. Show genuine provisional deltas/concise progress, never hidden reasoning/secrets; label hypotheses from their first text. Completion requires valid provider completion, no truncation/unfinished tools and one durable content/reference/status save, not mandatory complete-candidate JSON or whole-answer semantic verification. Preserve useful authorized interrupted text with its reason, not as normal completed factual history. Never automatically restart generation after visible output. Browser disconnect is not cancellation: host-owned work may continue within original time/cost limits, and authorized reconnect joins the same turn without duplicate generation or lost/duplicated text. Host failure interrupts orphaned turns and requires explicit retry. Revocation stops further unauthorized disclosure; references retain original versions, and history/summary reuse rechecks contributing access.

- **FR-11.8:** [Current; retained original behavior] When selection is empty, determine relevant Systems/Repositories using retrieval signals inside server-approved authorized discovery and display scope for adjustment. Inferred relevance shall not grant the model authority to choose access scope.

- **FR-12.1:** [Current; revised] Show real per-Repository operational status and System/selection aggregates separately from primary coverage and knowledge freshness. Include queued, executing, paused, cancelled and terminal outcomes with gaps. Users need no separate Pass 1/resolution/Pass 2 start calls. Recovery distinguishes explicit Scan resume from Chat retry.

- **FR-12.2:** [Current] Show real active Worker counts for permitted work, without fictional progress or multiplying per-run limits into uncontrolled global totals.

- **FR-12.3:** [Current; revised] Show admitted primary files and accepted/pending/failed coverage. File, semantic Code Unit and Document counts differ; sample quotas or completed-job counts are not coverage proof.

- **FR-12.4:** [Current] Show errors, classified failures and accessible warning/failed-file summaries. Missing analysis capabilities need not prevent reading existing knowledge. Production status uses real state, never demo fixtures.

- **FR-12.5:** [Deferred] Dashboard HITL answer/dismiss UI is deferred; retain Uncertainty disclosure and operational controls.

- **FR-12.6:** [Current / Deferred] Provide an authorized Document reader, exact cited sections and basic manual-refresh Logical Flow/Document-neighborhood graph bound to versions/evidence. Enforce mode/user scope server-side for nodes, relationships, labels, counts and tooltips. Show stale, partial and unavailable-source states; sparse graphs are valid. Layout interaction cannot edit domain Edges. Live animation/advanced portfolio visualization are deferred, not Scan progress or genuine Chat streaming.

- **FR-13.1:** [Current / Original unmet backlog] Native CodeGraph MCP consumption is required under application-selected source/tool authority. General Repository/System/global registered MCP consumption remains distinct original backlog outside the core redesign; this adapter neither fulfills it nor authorizes arbitrary tools. Preserve original low priority without treating it as approved deferral.

- **FR-13.2:** [Original unmet backlog] General externally configured MCP consumption in Chat remains original backlog. Internal wiki tools and exceptional scoped IT source reads do not complete it or add external operational-information authority.

- **FR-13.3:** [Current] Scan and Chat tool use require separate approvals/scopes. Scan approval never implies Chat approval or Business access; enforce purpose per invocation.

- **FR-13.4:** [Deferred] Outward wiki MCP serving is deferred, without an endpoint or placeholder. Internal wiki tools/native CodeGraph MCP consumption remain current.

- **FR-13.5:** [Original unmet backlog] Retain three-level Repository/System/global configurable MCP management with purpose-specific approvals. Later design/decomposition is required; native tools provide neither implicit execution authority nor completion credit.

- **FR-14.1:** [Current] Preserve authorized global advisory context applying to Systems, without broadening global-management permissions.

- **FR-14.2:** [Current] Preserve System advisory context for its Repositories, without making membership communication evidence.

- **FR-14.3:** [Current] Preserve Repository advisory context, including standalone Repositories.

- **FR-14.4:** [Current; revised] Active Scans retain selected context; edits during affected work queue for later application rather than silently changing pinned analysis. Deferred HITL supplies no current mutation exception. Chat histories/summaries/mode contexts have separate access and selection rules and cannot mutate Scan evidence.

- **FR-15.1:** [Current; revised] Provide applicable declared LSP navigation alongside required CodeGraph/source access. Initial offline/local-source operation is temporary and disables automatic imports/builds/dependency restoration; it is neither permanent local-only topology nor network air-gapping. Readiness does not imply semantic completeness.

- **FR-15.2:** [Current; revised] Available type/reference information may augment understanding with explicit capability/classpath limitations. Missing tool results are not proof that a relationship does not exist.

- **FR-15.3:** [Current; revised] Profiles are application-selected per Repository; models cannot select arbitrary adapters, snapshots or physical workspaces. Initially allow at most one LSP session per snapshot. Dependency-aware imports/restores and multiple sessions are later scope requiring separate authorization/qualification.

### NonFunctional Requirements

All 15 entries below are copied verbatim from the canonical PRD §4, preserving every ID and status classification.

- **NFR-1.1:** [Current; revised] Qualify Scan duration, coverage and resource use against representative Repositories/configurations, including progressive large files. No fixed sample quota or competition preparation deadline applies. Configured limits may queue, pause or honestly fail work, never silently exclude source; one machine's measurements are not universal throughput guarantees.

- **NFR-1.2:** [Current; revised] Measure first useful answer delivery, total duration and multi-document/follow-up usefulness under declared provider/configuration limits. Replace the universal 30-second/five-Document guarantee with environment-qualified results and bounded execution. Five Documents is not a retrieval cap; no replacement universal latency promise is invented.

- **NFR-1.3:** [Deferred] The five-second live-graph target is deferred with animation. Real Scan status and Chat streaming retain measured responsiveness/correctness obligations.

- **NFR-2.1:** [Current] Hash passwords using a modern algorithm such as bcrypt or Argon2.

- **NFR-2.2:** [Current] Encrypt provider/database secrets at rest. Preserve required Data Protection keys across replacement and coherent backup/restore; credentials must not enter prompts, logs or diagnostics.

- **NFR-2.3:** [Current; strengthened] With self-hosted providers, source/Documents stay inside organization infrastructure; external providers receive only necessary authorized task context. Bind tools/source to granted scope, budgets and provenance. Recheck live authority over every composite contributor, not merely displayed citations, before publication/disclosure. Restricted histories, counts and paths cannot leak through otherwise permitted output. Agents cannot choose credentials, arbitrary host paths or domain writes.

- **NFR-2.4:** [Current] Database tools remain non-production/read-only-replica only, with writes blocked. This survives tool deferral and binds preserved or later authorized surfaces.

- **NFR-3.1:** [Current; revised] Isolated Worker failures shall not collapse unrelated work. Distinguish integrity/security failures, recoverable job errors and provider outages; use bounded classified retries, not universal once-and-continue. Accept no partial failed-file drafts as knowledge. Expose gaps and settle owned work/capacity on cancellation, timeout or lost ownership.

- **NFR-3.2:** [Current; revised] Temporary Scan-provider failures receive bounded increasing retries then durable pause; configuration failures require correction. Explicit resume rechecks prerequisites, source and authority. Restart shall not resume paused work or silently switch provider/source. Shared provider admission covers Scan/Chat, but Chat follows its distinct interruption/completion rules and never silently restarts a visible answer.

- **NFR-3.3:** [Current; revised] Preserve authoritative Scan progress, accepted knowledge, source and durable Chat history across restart. Replayed acceptance/save acknowledgements shall not duplicate knowledge or generate again. Browser reconnect reuses existing bounded work; host failure interrupts orphaned Chat and leaves Scan explicitly recoverable. Recovery promises neither zero repeated external billing nor transparent per-token replay.

- **NFR-4.1:** [Current; revised] Deploy two Compose services: Vulgata with bundled local analysis tools, and PostgreSQL. Declare tool versions/dependencies and portable storage/provider/Worker/tool limits without host-profile dependencies or an extra remote tool bridge. Preserve database data, required source and keys apart from disposable workspaces/caches. Initially do not auto-prune canonical retained source; clean only unleased disposable state. Provide coherent operator maintenance-window backup/restore of data, source, keys and restore configuration outside primary volumes; volumes alone are not backups. Isolated restore qualification disables provider dispatch, invalidates old ownership and prevents automatic replay of saved running work. Recheck source/access before explicit resume/retry.

- **NFR-5.1:** [Current] Nontechnical users shall log in, select permitted context and understand Business answers without training/documentation. Assess explanation usefulness and citation faithfulness separately from schema validity or reference identity.

- **NFR-5.2:** [Current] Preserve Blazor's basic accessibility facilities and established accessible interactions. No new WCAG certification/compliance commitment is introduced; remove the expired competition rationale.

- **NFR-6.1:** [Current] Prompts remain externalized configurable text, never hardcoded strings. Native agent-framework adoption does not change this.

- **NFR-6.2:** [Current; revised] Document supported catalog/advisory-context formats so owners contribute framework knowledge without developer assistance. Do not require detailed service mappings or promise arbitrary executable dialect extensions through configuration.

### Additional Requirements

These are requirement-level extractions from the [architecture gateway](planning-artifacts/architecture.md) and [AD-1–AD-32 spine](planning-artifacts/architecture/ARCHITECTURE-SPINE.md). The cited AD remains the normative technical rule; this inventory does not create a parallel detailed design. No greenfield starter template or new Epic 1/Story 1 is required: preserve and explicitly adapt the existing modular monolith.

#### Preserved platform and delivery constraints

- Preserve .NET 10/Blazor Interactive Server, PostgreSQL 17/EF Core/Npgsql, Fluent UI, CommunityToolkit.Mvvm and the six existing .NET project boundaries. The spine's version seed is observed context, not a package/tool upgrade instruction. Conceptual modules need not become new projects/services.
- Keep domain entities and aggregate-specific interfaces in Core, persistence/adapters behind Infrastructure, DTOs/validators/LoadState in Shared, injected bounded execution/externalized prompts in Agents, testable ViewModels, and Web composition/presentation. No EF dependency in Core, generic repository, exposed IQueryable/DbSet, or cross-module table mutation. Preserve the current Web placement of Identity context/migrations as an existing exception, not a relocation task.
- Preserve structured JSON as canonical Code Logic/Business Logic content behind the existing ContentJson/PostgreSQL jsonb boundary; Markdown and retrieval projections derive from it, never competing editable authorities. This is wiki content, not a requirement for Chat complete-candidate JSON or buffered answers.
- Preserve separate identity/vulgata schemas and migration histories, startup migration/role-seeding behavior, nullable/analyzer/central-package conventions, Async/private-field naming, explicit existing mappings and assembly-scanned entity configurations without domain data annotations.
- Preserve FluentValidation, plural-noun /api/ groups, direct DTOs/ProblemDetails and correct API 401/403 behavior without cookie/status-page rewrites; UTC timestamps, established JSON conventions, authorized hubs and past-tense events/hub methods.
- Preserve cookie Identity, bcrypt, seeded roles/scoped System Owner assignments, protected provider/database secrets and required Data Protection keys. Knowledge reader/history access is not management access. Continue source/Document organization-boundary protection with self-hosted providers and read-only/non-production database-tool safeguards.
- Keep long-running jobs outside HTTP/circuit lifetimes, durable ownership/acceptance as truth and in-memory wakeups/post-commit notifications replaceable. Preserve sanitized structured diagnostics plus the separate exact log.md output obligation. Externalized prompts support declared deployment overrides; retrieved source/wiki content is evidence, not instructions.
- Before dependent builders split, a single owner must settle shared identity/selectors/lineage, lifecycle schemas, acceptance/publication/read-context contracts and common runtime limits. One integration owner controls any future migrations; isolated databases precede user databases.
- Use current governed feature activation, analysis, honest TDD, independent reviews and conditional E2E/integration/real qualification. Preserve the single xunit/WebApplicationFactory test project, naming and existing harness cautions; SQLite tests are not PostgreSQL/runtime proof. After accepted plan review and before tasks, applicable Agent-tool, Scan-strategy or PRD changes still require explicit user approval. No code, test, migration, provider or runtime action is authorized by this inventory.

#### Architecture requirements

- **AR-1 — Independent source ownership (AD-1):** One Repository Scan Run fixes one Source Snapshot; System/selection actions create independent runs. Standalone Repositories use the same pipeline. Optional Scan Campaign controls/progress create no pair-wide correctness identity, communication proof or global barrier.
- **AR-2 — Acceptance and state ownership (AD-2):** Scan Control owns durable requests/plans/jobs/progress; Knowledge accepts semantics and selects knowledge; Resolver/Flow Planner proposes; Chat owns dialogue; MAF executes injected bounded tools. Infrastructure supplies the shared fenced atomic acceptance and Chat completion contracts, not a competing policy. Domain builders derive public identities/fingerprints.
- **AR-3 — Exact source and contained tools (AD-3):** Resolve committed Git once; expose Repository/full Profiled Revision/relative Source Anchors. Server-granted metadata/list/range reads carry cumulative budgets/deadlines; only trusted adapters obtain contained physical Tool Workspace leases. Retain exact source for selected knowledge/active work, including old contributors; verify identical bytes on reopening. Tool state/source/version identity remain distinct, and historical provenance survives cleanup.
- **AR-4 — Generic preparation and declared readiness (AD-4):** Separate factual inventory/profile/unknowns from bounded advisory overview. Require CodeGraph preparation/native MCP before Pass 1 and applicable declared LSP. Initially use whole-Repository local-source workspaces, separate tool state and at most one LSP session per snapshot; disable Maven/Gradle import/execution/download. Dependency-aware analysis needs separate authority/qualification, and readiness proves no semantic completeness.
- **AR-5 — Explicit coverage and structured source (AD-5):** Apply explicit primary/supporting policy to source/configuration/templates/schemas/DSLs; explanatory documents are context. File names, unfamiliar extensions, index omissions and overview ranking cannot silently exclude admitted source. One extensible Source Content Analysis contract preserves locations, compact structure and on-demand detail, including unknown versus malformed content, without executable configuration, external fetch, dynamic parser generation or mandatory demo recipes/quotas.
- **AR-6 — Native exploration under application policy (AD-6):** MAF owns conversations/tool loops; expose the native recommended CodeGraph MCP surface with application-bound projectPath, language-neutral LSP and bounded source/content tools. Models cannot choose arbitrary adapters/snapshots/workspaces/credentials or domain writes. Return normalized locations/evidence/limitations/receipts and recover required CodeGraph failures without automatic source-only fallback or CGC restoration.
- **AR-7 — File work and semantic identities (AD-7):** A complete logical File Workstream discovers meaningful possibly nested Code Units, each owning CL Logical Documents/immutable versions; windows and tool IDs are not identities. Other files supply context, not duplicate authorship. Unify selectors/nesting/aliases/lineage; unproven rename/split/merge mappings cannot reuse identity silently.
- **AR-8 — Durable progressive acceptance (AD-8):** Continue large files across bounded windows/runs with atomic validated-draft/progress checkpoints. Only accepted file manifests expose units/Documents/observations. Required unprocessed source remains pending; terminal failures expose gaps but no partial drafts. Zero-unit results need truthful dispositions. Fenced idempotence prevents duplicate knowledge, not repeated external billing.
- **AR-9 — Evidence-based communication (AD-9):** Preserve owner/role/mechanism/operation/literals/expressions/namespaces/status/provenance, including unknown mechanisms; matching records its inputs/rule/outcome. A lone visible candidate, shared path/topic or confidence cannot prove identity. Missing/ambiguous bindings remain Uncertainties; static links are not deployed execution proof.
- **AR-10 — Finite reconciliation and coalesced refresh (AD-10):** Pin finite accepted matching targets, handle late knowledge without unchanged-source re-analysis, terminate cycles and deduplicate affected flows. Root-run synthesis needs terminal outcomes for every admitted primary item and explicit matching outcomes; paused work is not terminal. Accepted external files may qualify before their runs finish. Coalesce latest unstarted flow demand while active inputs stay fixed; publication starts no new resolution epoch.
- **AR-11 — Rooted Logical Flows (AD-11):** Give source-backed operations/handlers/schedules/workflows/selected Code Units distinct Logical Flow identities, initially homed at root Repositories. Cross-Repository browsing appearances share one Logical Document. Plan bounded directed evidence slices with branches/frontiers/exact versions; local flows qualify. Helpers/folders/cycles do not merge purpose; separate-business-root merging is later scope.
- **AR-12 — Supported Pass 2 explanation (AD-12):** Explain pinned Flow Group Versions with conditions, alternatives, data/state effects and unknowns. Include supplementary authorized CL actually used in final validated dependencies. Synthesis cannot widen access, invent targets or alter accepted relationships.
- **AR-13 — Coherent per-flow publication (AD-13):** Atomically promote Flow Group/BL versions, dependencies and selection; pin exact GENERATED_FROM, relationship/resolution/frontier/direct-source inputs. Relationship/Uncertainty publication has independent identity; REFERENCES can advance without CL rewrite, while historical provenance never retargets. Expose stale/pending/failed/incompatible selections and recheck all composite contributors, counts and paths.
- **AR-14 — One real workflow and truthful controls (AD-14):** A single Scan action durably starts preparation through knowledge; System/selection aggregates independent runs. Report operations, primary coverage and Knowledge Currentness separately using real permitted data. Cancellation fences owned pending/unpublished downstream work without deleting accepted history or cancelling independent consumers. Demos require separate composition.
- **AR-15 — Explicit bounded recovery (AD-15):** Use bundled-tool two-service Docker with recoverable source/keys and temporary no-import/no-restore LSP policy, not network air-gapping. Temporary Scan-provider failures receive bounded increasing retries then durable pause; configuration failures need correction. Release permits/unneeded leases, fence late writers, and require prerequisite-checked explicit resume. Restart never auto-resumes paused work or substitutes source/provider; Chat follows its distinct lifecycle.
- **AR-16 — Versioned replacement and honest history (AD-16):** Introduce explicit generic contracts/production routing and migrate only equivalent semantics. Preserve pair-era artifacts and historical file Documents; new semantic analysis requires explicit lineage. Cut over capabilities after generic vertical/history-read proof, with Cross-Repository proof beyond local synthesis. Unimplemented generic routes remain visibly unavailable, never legacy fallback.
- **AR-17 — Selected evidence and public visibility (AD-17):** Knowledge pins root binding and accepted exact manifests from one designated analysis candidate per other authorized Repository, not all history or only one System. Current-promotion checkpoints expose accepted CL and explicit gaps; default readers use selected corpora/published dependencies. Other staged/history views are labelled. Failed current files cannot inherit old analysis; narrowed scope/access loss does not disprove broader relationship truth.
- **AR-18 — Ordered conditional publication (AD-18):** Record current-promotion/history-only intent and order selection within Repository lineage. Transactionally validate expected head, desired target, authorization and cancellation before publication. Late superseded results may remain history but cannot displace newer selection; commit hashes/finish times do not establish recency. Manual work retains its initiating identity/approved scope; service-principal policy is separate.
- **AR-19 — Evidence-based withdrawal (AD-19):** Withdraw current units/observations/roots only when validated selected replacement-source evidence proves absence. Omission/exclusion/failed or unfinished analysis cannot prove deletion. Preserve history, expose revalidation gaps, reopen affected references and retire absent roots through bounded reconciliation. Revocation blocks disclosure immediately.
- **AR-20 — Focused investigation (AD-20):** Dispatch bounded inquiry over a finite application-pinned caller/candidate/evidence/source/access set. Return supporting/opposing evidence, conclusions/conditions and capability limits, not Worker-written Edges. Deduplicate on original question/input/policy fingerprints excluding outputs/receipts; retry by bounded policy. Unavailable tools are not negative proof. Revalidate target/access/fence; publication cannot recursively inquire.
- **AR-21 — Bounded synthesis verification (AD-21):** Pass 2 may use granted snapshot source/structured/native CodeGraph/LSP tools to verify pinned evidence. Record every extra CL/direct-source contribution and Source Anchor. Tools cannot widen source scope or select arbitrary paths; discrepancies become uncertainty or bounded proposals to owners, never accepted CL/Edge rewrites or recursive repair scheduling.
- **AR-22 — External-information boundary (AD-22; deferred integration):** Require no owner-maintained service-to-Repository mapping. Use authorized source/accepted knowledge and optional advisory context; absent decisive database/gateway/registry facts remain Uncertainties. Defer uniform external-information APIs/adapters/credentials/permissions/refresh with no placeholder. Required CodeGraph and configured provider execution remain current.
- **AR-23 — Shared bounded parallelism (AD-23):** Deterministically and fairly admit independent Repository/file/inquiry/flow work with one valid run/snapshot/file lease owner, global/per-run Worker limits and shared Scan/Chat provider admission. Tool Runtime bounds/serializes unqualified shared operations. Workers cannot allocate Workers or hold capacity awaiting targets. Pause/cancel/timeout/lease loss must stop and clean owned work/permits, not merely fence writes.
- **AR-24 — Wiki-first mode-safe Chat (AD-24):** Provide shared authorized search/browse/section/content/related-Document tools with provenance/coverage/pagination/Uncertainties. Business reads BL only; IT adds CL and exceptional exact-source gaps. Synthesize useful multi-document/follow-up answers and labelled hypotheses, without per-record tools, mandatory vectors, routine source re-proof, source scanning or Knowledge repair. Shared code does not prove business dependency; citation identity is not semantic certification.
- **AR-25 — Stable turn read selection (AD-25):** One Knowledge Read Context pins discovery/membership/pagination/content/citations, including later-discovered Documents. Later publication does not retarget the turn; later turns read fresh selections while original references persist. Live authorization still applies. Accept one owning storage/index contract; a Document cache, wiki copy or long-lived network transaction is not a substitute.
- **AR-26 — Durable isolated dialogue (AD-26):** Store ordered Conversations/Chat Turns separately from Documents and bounded Inference Context, initially one active answer per Conversation with identity replay deduplication. Reuse only permitted completed history/references; summaries preserve hypotheses/contributor scope. Mode/scope changes get fresh contexts; late output/saves remain origin-bound. Recheck access and exclude affected generated material on revocation; persistence triggers no reconciliation.
- **AR-27 — Genuine output and durable completion (AD-27):** Relay actual provisional deltas/concise progress with first-text hypothesis labels and authorized references, not hidden reasoning or completed-answer replay. Completion needs valid untruncated provider termination, finished tools and one fenced idempotent content/reference/status save. Resolve lost acknowledgements from storage; telemetry cannot undo completion. Retain useful authorized interrupted text/reason separately from completed history; visible output never auto-restarts. Host failure interrupts orphaned turns and requires explicit retry/cleanup, not transparent token replay.
- **AR-28 — Same-turn reattachment (AD-28):** Host-owned started turns may finish after browser disconnect within original budgets/scope/context. Authorized reconnect joins saved/current state and subsequent updates without loss, duplication, regeneration or renewed limits. Disconnect differs from cancellation/host failure; explicit cancellation, exhaustion, access loss and terminal failures still settle work.
- **AR-29 — Reader and basic scoped graph (AD-29):** Provide Knowledge-authorized Document/version/history/exact-section navigation and available authorized recorded-revision IT source. Manual-refresh Logical Flow/Document-neighborhood graph binds exact evidence/version and filters every node/edge/label/count/tooltip/target server-side, including Business BL-only access. Show sparse/partial/stale/unavailable-source states; layout edits no domain Edges. Advanced/live graph is deferred, not real Scan/Chat streaming.
- **AR-30 — Portable two-service deployment (AD-30):** Bundle Vulgata/CodeGraph/JDT-LS with declared tool versions/dependencies and PostgreSQL as the second Compose service; use configured durable source/data/key volumes separate from disposable tool state. Run app/tools non-root with required writable mounts. Configure Worker/per-run/provider/tool/storage limits without host-profile paths or machine-defined coverage. Exhaustion queues/pauses/fails honestly; volumes are neither backups nor automatic migration. No remote bridge/local LLM/distributed scheduler/privileged Docker control is required.
- **AR-31 — Conservative retention and coherent restore (AD-31):** Preserve immutable history and source pinned by knowledge/work; initially auto-clean only unleased disposable state, never canonical retained source. Quiesce writers/cleanup for protected outside-volume backups of coherent PostgreSQL/source/keys/restore configuration including required roles/extensions. Qualify isolated restore with providers disabled; reconcile completions/invalidate old leases/prevent saved-state replay. Unfinished Scans become explicitly recoverable/paused and Chat interrupted, with source/access checks before resume/retry. No online/PITR/HA service is required.
- **AR-32 — Observable environment-qualified delivery (AD-32):** Document bounded deployment-overridable defaults and distinct app/database/Scan-tool/provider readiness, preserving existing wiki reads when analysis is unavailable. Emit sanitized correlated states/counts/durations/failures/available usage; unknown usage is not zero, and routine health uses no paid inference. Qualify real two-service tools/persistence/restore; one/several/standalone and beyond-pair/late/cycle cases; progressive source/semantic coverage/recovery/concurrency/compatible reads; useful multi-document/follow-up streaming Chat with exact references and lifecycle checks. Assess usefulness/faithfulness independently of schemas and record configuration/timing/cost without universal deadline/throughput claims.

#### Product success and scope guardrails

PRD §5 requires source-backed Cross-Repository understanding beyond local-only delivery, independently assessed Code Logic/Business Logic usefulness, evidence/relationship accuracy with visible gaps, and representative mode-appropriate multi-document/follow-up Chat with exact references and correct streaming/recovery. Incremental-update success remains deferred. Architecture verification obligations are future evidence, not completed tests.

PRD §6 and the approved proposal retain explicit later scope: new source origins, dependency-aware import/build/restore/private mirrors, multiple LSPs per snapshot, separate-business-root merging, automatic whole-Repository expansion/update triggers, product HITL, caller-triggered partial library scans, external operational information, outward wiki MCP, attachments and advanced/live portfolio graph. Preserve the distinct original unmet backlog, not an invented blanket deferral. Existing exclusions remain enterprise SSO/new fine-grained RBAC, production database access, webhooks, formal WCAG certification, automated compliance reporting, separate Audition Agent, dedicated answer-confidence scoring, guaranteed gRPC-specific detection and shared-database communication detection. Safety, basic readers, real progress, real streaming, static Scans and bounded accepted-evidence reconciliation remain current.

### UX Design Requirements

The paired [DESIGN.md](planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md) and [EXPERIENCE.md](planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md) are first-class inputs. Each entry identifies its source section. Requirements marked Deferred/History preserve boundaries and are not active UI work. The June mockups remain visual/layout references, not authority to restore superseded behavior.

#### Foundation, tokens and layout

- **UX-DR1 — Platform and compatibility boundary [Current]:** Preserve the desktop-browser Blazor Interactive Server/SignalR experience. There is no offline-operation promise, mobile-specific breakpoint contract, new responsive redesign or new accessibility certification commitment in this input set. Distinguish browser reconnect from host failure. (EXPERIENCE: Foundation.)
- **UX-DR2 — Chinese-only voice [Current]:** All V1 labels, buttons, placeholders, errors, system messages and empty states use Simplified Chinese; no i18n surface. Business explanations use plain narrative language without technical jargon; IT explanations use precise code-aware language. Uncertainty/status/errors are neutral, non-blaming and give a permitted next step without requiring an owner inbox. (DESIGN: Brand & Style; EXPERIENCE: Voice and Tone.)
- **UX-DR3 — Brand theme tokens [Current]:** Preserve Library Oak primary light/dark #445E7A/#6A83A2 and primary foreground #FFFFFF; accent light/dark #B98B6B/#D4A88C with foreground #FFFFFF/#1D2026. Business Mode is locked to light and IT Mode to dark; no independent mid-session theme toggle. Primary marks actions/mode/selected navigation/link underlines; accent marks contextual System/Repository selection only, never status/chrome/decoration/hover affordances. (DESIGN: Colors.)
- **UX-DR4 — Surface tokens [Current]:** Preserve light background/foreground/surface/muted/muted-foreground/border #F6F4F0/#1D2026/#FFFFFF/#EDE9E3/#7A7A7A/#D9D3CA and dark #1D2026/#F6F4F0/#24272E/#282B33/#9A9DA5/#383B44. Use flat solid backgrounds, Fluent semantic destructive/warning/success tokens and no third brand color/gradients. Surface layering, not new shadow effects, conveys depth. (DESIGN: Colors; Elevation & Depth.)
- **UX-DR5 — Display typography [Current]:** Noto Serif SC display is 32px/400/1.3, with display-sm 22px/400/1.35, restricted to Chinese greetings/new-chat banners, designated section headers and display empty states. Body/labels/captions/buttons/inputs/grids/chat content inherit Fluent sans-serif; no serif interactive elements or bold display. (DESIGN: Typography and frontmatter.)
- **UX-DR6 — Fluent spacing and shapes [Current]:** Retain Fluent's 4px-based inherited spacing scale, subtle default elevation and native 4px/8px/12px radii. No custom spacing/radius system, pill shapes or decorative elevation. (DESIGN: Layout & Spacing; Elevation & Depth; Shapes.)
- **UX-DR7 — Standard components [Current]:** Preserve uncustomized FluentButton non-primary variants, FluentDialog, FluentDataGrid, FluentNavMenu, FluentTextField, FluentTextArea, FluentToast, FluentTooltip, FluentBreadcrumb, FluentProgressBar, FluentProgressRing, FluentBadge, FluentIcon, FluentDivider, FluentTreeView, FluentTab, FluentCard, FluentCombobox, FluentSelect and FluentSwitch, except the explicit brand-layer deltas below. (DESIGN: Components.)
- **UX-DR8 — Shared navigation shell [Current]:** After login, land directly in Chat, not a landing dashboard. Preserve the shared navbar with brand, 「对话」, authorized 「管理后台」 and avatar actions 「个人资料」/「退出登录」. Chat and management remain two top-level routes; the peripheral Chat management link is 「管理」, not its primary action. (DESIGN: Layout & Spacing; EXPERIENCE: Information Architecture.)
- **UX-DR9 — Chat layout [Current]:** Keep a single-column comfortable-max-width Chat surface with visible mode control, inline System/Repository selector, message stream and bottom-fixed input. Mode selection stays above the input, with EXPERIENCE's explicit component order placing it above the System/Repository selector and conversation. Selection is not a separate page or modal. (DESIGN: Layout & Spacing; EXPERIENCE: Chat structure/ModeSelector/InlineSelector.)
- **UX-DR10 — Management layout/navigation [Current]:** Retain the holy-grail topbar/left-sidebar/main-detail shell; default management landing is System management. Sidebar/main content changes for System tree/details, graph permitted scope/neighborhood, mode-appropriate Document tree/version reader, Scan history list/results, and Administrator settings navigation/forms. Do not add a new top-level reader layout. (EXPERIENCE: Information Architecture.)
- **UX-DR11 — Knowledge access independent of management [Current]:** Authorized Users can reach Document/history/citation/basic graph reads through Chat and reader navigation without management rights. Existing Administrator/SystemOwner and assigned-System policies control management actions; UI placement never adds management authorization to Knowledge reads. (DESIGN: Layout & Spacing; EXPERIENCE: App Shell and component entry points.)
- **UX-DR12 — Preserved administration flows [Current]:** Retain email/password registration/login, profile/logout, authorized System/Repository CRUD including standalone Repositories, provider configuration/overrides, protected database-connection configuration and Global/System/Repository advisory context. LLM configuration/User management use standard Fluent forms/grids and only specified primary/surface styling, with no new role/management authority. (DESIGN: Admin Pages; EXPERIENCE: Scope adoption/Information Architecture; PRD preserved scope.)

#### Brand components and Chat behavior

- **UX-DR13 — Primary Button [Current]:** Apply primary fill/primary-foreground and 8px radius to 「发送」, 「开始扫描」 and management 「保存」; keep other button variants Fluent-default. (DESIGN: Components—Primary Button.)
- **UX-DR14 — Mode Badge [Current]:** Show a compact non-interactive 「业务模式」/「技术模式」 session-state label above input/in the conversation header using primary/primary-foreground and 4px radius; switching is a separate control. (DESIGN: Mode Badge; EXPERIENCE: ChatArea.)
- **UX-DR15 — System/Repository Tags [Current]:** Render selected scope as selectable/removable inline tags with accent/accent-foreground and 4px radius, including × removal above input. Use the same contextual accent for active tree nodes; do not use it for operational status. (DESIGN: System/Repo Tag; EXPERIENCE: ChatArea.)
- **UX-DR16 — User and assistant bubbles [Current]:** Right-align plain-text user bubbles on surface light/dark tokens; left-align assistant Markdown on muted light/dark tokens. Display genuine provisional deltas, then explicit saved/interrupted outcome with permitted references; never hidden reasoning/secrets or completed-answer typewriter replay. (DESIGN: Chat Bubble components; EXPERIENCE: ChatArea.)
- **UX-DR17 — Mode-Select Banner [Current]:** A confirmed mode switch opens a centered new-chat banner 「开始新的对话」 with selected mode beneath, display typography and primary text. Preserve 200–400ms theme transition and new-chat welcome without claiming an unsaved answer was saved. (DESIGN: Mode-Select Banner; EXPERIENCE: Mode switch state.)
- **UX-DR18 — Chat input and greetings [Current]:** Enter sends and Shift+Enter adds a newline; placeholder changes by mode. New Business sessions show 「你好，想了解什么业务流程？」 and IT sessions 「你好，需要追踪哪段逻辑？」 with designated display typography, as specified by EXPERIENCE's concrete ChatArea/EmptyState rules. DESIGN's alternative greeting remains a visual example, not a second required simultaneous greeting. (EXPERIENCE: ChatArea/EmptyState; DESIGN: Typography.)
- **UX-DR19 — ModeSelector [Current]:** Use the two-label control above the inline selector with primary selected state. Confirm using 「切换模式将开始新对话，已保存的历史仍可查看。确定切换？」 before creating fresh inference context/new-chat surface and matching theme. Confirmed switching has no undo. Preserve authorized older dialogue; active turn output/saves remain in the originating context, never old IT prompts/summaries/caches/late deltas in Business. (EXPERIENCE: ModeSelector; State Patterns; Interaction Primitives.)
- **UX-DR20 — InlineSelector options/defaults [Current]:** Use inline Fluent multi-selection for only authorized Systems/Repositories including standalone Repositories, supporting whole-System and individual Repository selection. Retain last-used selection filtered by live access; first use selects all permitted Systems and eligible standalone Repositories. Grouping is not communication evidence or management authority. (EXPERIENCE: InlineSelector.)
- **UX-DR21 — Empty and changed scope [Current]:** Empty selection may automatically infer relevant scope only within current authorization; show 「已自动选择以下范围：{范围名称}」 and 「调整范围」. Never broaden access or initiate a Scan. Scope changes create fresh inference context and recheck completed history/summaries; older turns/references retain originating contexts. (EXPERIENCE: InlineSelector.)
- **UX-DR22 — Claim-linked citations/previews [Current]:** Attach useful exact permitted Document Version/section references to supported claims, not fabricated references for greetings/no-results. Clicking opens a mode-safe inline preview and exact-section reader; old references never retarget to current versions. Business shows BL title/section/business content only; IT may follow permitted CL/BL and relevant available recorded-revision source. Recheck live contributor access before disclosure. (EXPERIENCE: ChatArea and Chat references.)
- **UX-DR23 — Stable reading and durable history [Current]:** Within a turn, discovery/related membership/pagination/content/citations share one Knowledge Read Context. Later publications do not replace the active answer; later turns may read newer knowledge. Durable ordered conversations/original references survive reconnect; initially one answer is active per Conversation. Only permitted completed history feeds inference, preserving interrupted/hypothesis status. (EXPERIENCE: ChatArea.)
- **UX-DR24 — Waiting, retrieval and generation states [Current]:** Show the three-dot typing indicator at 200ms intervals when awaiting reply, 「正在查找相关文档…」 while retrieving and 「正在生成」 for actual provisional output. Label hypotheses from first text; do not show hidden reasoning or buffered complete-answer replay. No-results uses 「未找到相关文档。试试调整系统选择范围或换个问法。」. (EXPERIENCE: Chat loading states.)
- **UX-DR25 — Save and completion states [Current]:** Show 「正在保存」 only after valid provider completion, not yet completed history; truncation/unfinished tools cannot complete. Display completed only after one durable content/reference/status save. On lost save acknowledgement show 「正在确认保存结果」 while checking durable state; on failure show 「回答未完成保存」 with useful permitted text/status and retry the same save where possible, never generation. Telemetry failure cannot undo saved completion. (EXPERIENCE: Chat loading states.)
- **UX-DR26 — Disconnect and reattachment [Current]:** Show 「连接已断开，重新连接后将查看同一条回答。」. Host-owned work may finish within original limits; authorized reconnect joins that same answer and reconciles accumulated/new text and status without loss/duplication/new generation/renewed budgets, including completion during attachment. Disconnect is not cancellation. (EXPERIENCE: Chat loading states.)
- **UX-DR27 — Interruption and live revocation [Current]:** Explicit cancellation, limits or terminal/host failure shows 「回答已中断」 plus reason and useful authorized partial text, separate from completed factual history. Host failure requires explicit 「重试」; never auto-restart generation after visible output. Revocation stops further unauthorized disclosure and excludes affected generated history/summary content, even within a stable turn selection. (EXPERIENCE: Chat loading states.)

#### Management, Scan and Knowledge components

- **UX-DR28 — SystemTree [Current]:** Use Fluent TreeView with System parents, Repository children, expand/collapse arrows, accent active node and 「+ 新建系统」. Clicking selects corresponding main details; System context actions offer authorized edit/delete. Preserve assigned-System visibility. Real Scan states use text/icons, with an adjacent FluentProgressRing for active work, never color-only or fabricated progress. (DESIGN: Management Tree View; EXPERIENCE: SystemTree.)
- **UX-DR29 — RepoDetail [Current]:** Show Repository name/description/edit/delete, Git URL, last Scan, operational status and Document count. 「开始扫描」 starts one durable source-to-knowledge workflow; active/paused work exposes real progress and authorized controls/dashboard. 「查看文档」 opens permitted selected Knowledge including gaps. Only authorized owners edit metadata. (EXPERIENCE: RepoDetail and Information Architecture.)
- **UX-DR30 — SystemDetail [Current]:** Show System name/description/edit/delete, Repository total, currently scanning/completed Repository counts and Document total. Use a FluentDataGrid for Repository name/Scan status/last Scan/Document count and row 「查看」, plus 「+ 新建仓库」. Batch System/selection actions start independent Repository Scan Runs; aggregate Scan Campaign progress introduces no pair/global barrier. (EXPERIENCE: SystemDetail.)
- **UX-DR31 — ScanDashboard entry and data [Current]:** Preserve the existing inline/detail dashboard arrangement for active, paused and recorded terminal work. Use genuine SignalR state: admitted/accepted/pending/failed primary-source counts, explicit gaps, active Worker/error counts and separate knowledge publication/refresh state. No elapsed-time simulation or fictional percentages. (EXPERIENCE: ScanDashboard.)
- **UX-DR32 — Scan work indicators [Current]:** Show observed operations such as 「源代码准备」, 「代码分析」, 「跨仓库解析」, 「业务说明生成」 and 「知识发布」 without asserting a single sequential bar. Independent runs/flow refresh overlap; file windows and files are not Code Unit/Document counts. A percentage needs its real defined denominator and does not merge work, coverage and publication. (EXPERIENCE: ScanDashboard and Scan states.)
- **UX-DR33 — Explicit Scan recovery/control [Current]:** Temporary-provider retries are bounded/increasing and may end in 「已暂停」 with sanitized cause. Configuration errors require correction; 「恢复扫描」 explicitly checks source/prerequisites/authorization. Restart/provider recovery never auto-resumes paused work or switches provider/source. Permitted pause/cancel controls settle owned unfinished work without deleting accepted history or cancelling independent consumers. No separate user Pass 1/resolution/Pass 2 start calls. (EXPERIENCE: RepoDetail/ScanDashboard/Scan entry.)
- **UX-DR34 — Scan state presentation [Current]:** Unscanned shows gray point plus 「未扫描」 and start; queued shows observed 「排队中」, not begun Worker work; active shows 「扫描中」/real operations/dashboard; paused shows cause/resume; cancellation distinguishes 「正在取消」 cleanup from 「已取消」; terminal shows actual successful or gap-bearing result/timestamp/coverage; failure shows sanitized reason and permitted explicit recovery. Accepted history remains readable; terminal state proves neither total coverage nor all BL refresh current. These are display meanings, not new lifecycle schemas. (EXPERIENCE: Scan states.)
- **UX-DR35 — Separate scoped counts/currentness [Current]:** All trees/details/dashboard/history expose operational status separately from primary admitted/accepted/pending/failed coverage and knowledge published/pending/stale/failed/retired state as applicable. Count only permitted scope, including contributor-aware access; never infer coverage from jobs/file windows, hide failed files using old analysis, or leak restricted counts. (EXPERIENCE: SystemTree/ScanDashboard/Scan state notes.)
- **UX-DR36 — Dedicated GraphView [Current]:** Provide Knowledge-authorized 「图谱」 and reader navigation to the sole non-Fluent Z.Blazor.Diagrams basic Logical Flow/Document-neighborhood view, with 「刷新」, pan/zoom and authorized scope filters. Background follows light/dark background tokens and active toolbar controls primary; interaction changes presentation only, never domain Edges. (DESIGN: Knowledge Graph; EXPERIENCE: GraphView.)
- **UX-DR37 — Graph evidence/style [Current]:** Bind all graph data to selected versions/evidence. Preserve permitted CL blue/BL green node styling, intra-Repository solid light-gray versus Cross-Repository dashed references and explicit Uncertainty dashed frontier/diamond using Fluent warning semantics. Label actual REFERENCES/GENERATED_FROM meanings; create no invented targets/business dependencies/failed-draft Documents. Shared code alone proves no business dependency. (DESIGN: Knowledge Graph; EXPERIENCE: GraphView.)
- **UX-DR38 — Graph authorization and refresh [Current]:** Server-filter every node/Edge/label/count/tooltip/destination by live user/mode/scope and composite contributors; Business receives BL-only data, never fetched hidden IT graphs. Manual refresh may select newer Knowledge and reports selection/currentness; historical graphs remain version-bound. Sparse/partial/stale permitted graphs are valid. (EXPERIENCE: GraphView.)
- **UX-DR39 — DocumentTree organization/search [Current]:** Authorized readers enter via 「文档」 or exact Chat references without management rights. CL tree is Repository/file/Code Unit, supporting several semantic-unit Documents per file without synthetic directory Documents; BL view is Logical Flows grouped by System/Repository, sharing one Logical Document across appearances. Business exposes BL only, IT may show both type badges. Sidebar title/keyword search filters results. (DESIGN: Document Viewer; EXPERIENCE: DocumentTree.)
- **UX-DR40 — Document Viewer and history [Current]:** Render selected authorized Markdown on surface with primary titles, blue CL/green BL type badges and IT code blocks on muted background. Provide explicit 「版本历史」 and exact cited sections. Historical Documents preserve original dependencies/relationships rather than today's graph; show pending/stale/failed refresh and coverage gaps. (DESIGN: Document Viewer; EXPERIENCE: DocumentTree.)
- **UX-DR41 — Mode-safe source detail [Current]:** Business receives no CL/code/file paths/ranges/Git metadata in body/header/preview/citation targets. IT may show Repository/full Profiled Revision/relative source locations and open only authorized recorded-revision source. If unavailable, show 「该版本源代码暂不可用，文档及来源记录仍保留。」, never substitute current checkout. Source Snapshot is not a new public version label; retained history does not promise perpetual source availability. Recheck live access to composite/history contributors. (DESIGN: Document Viewer; EXPERIENCE: DocumentTree.)
- **UX-DR42 — Scan history navigation [Current]:** Preserve management 「扫描历史」 with a record-list sidebar and selected detailed results. Active/paused/terminal dashboards remain reachable from Repository details; retained accepted history, actual outcomes and exact provenance stay distinct from current-selection claims or new generic qualification. (EXPERIENCE: Information Architecture; ScanDashboard.)

#### Empty/loading/error states and accessible interaction

- **UX-DR43 — EmptyState: System and Repository administration [Current]:** Empty management shows 「尚未添加任何系统。创建你的第一个系统开始扫描。」 and 「创建系统」. An empty System Repository list shows 「该系统下暂无仓库。添加第一个仓库。」 and 「新建仓库」. Show creation actions only under management authorization; follow the concrete EmptyState body/display rules. (EXPERIENCE: EmptyState/SystemTree.)
- **UX-DR44 — EmptyState: Knowledge readers [Current]:** Graph empty state is 「当前范围暂无可用图谱。可调整范围或稍后刷新。」 with permitted adjust/refresh; Document empty state is 「当前范围暂无可用文档。可调整范围或稍后再试。」 with permitted scope adjustment. Do not force readers to management or fabricate content to fill sparse data. (EXPERIENCE: GraphView/DocumentTree/EmptyState.)
- **UX-DR45 — Loading and refresh [Current]:** Every data-loading ViewModel preserves the shared Idle/Loading/Loaded/Refreshing/Empty/NoResults/Error/Cancelling vocabulary. Loading uses 4–6 Fluent Shimmer skeleton rows matching expected layout; Refreshing retains visible content with a thin indeterminate top bar and does not block interaction. (EXPERIENCE: LoadState; architecture gateway: Security and user experience.)
- **UX-DR46 — Empty/search/error feedback [Current]:** Empty views offer the scoped guidance and one permitted primary action from the EmptyState contract. NoResults shows 「未找到匹配结果。试试调整关键词或系统选择范围。」. Error shows non-blaming next-step/retry text; Business exposes no technical details and IT only simplified safe diagnostics. Distinguish missing data/no results/partial/stale/error rather than substituting fixtures. (EXPERIENCE: LoadState; Voice and Tone.)
- **UX-DR47 — Keyboard interactions [Current]:** Preserve reading-order Tab navigation, Enter send, Shift+Enter newline and Esc closing the active/topmost dialog or panel. Chat remains keyboard-first; mouse-oriented management still retains Fluent's existing keyboard floor. (EXPERIENCE: Interaction Primitives and Accessibility Floor.)
- **UX-DR48 — Mouse and dialog/paging limits [Current]:** Citations open mode-safe preview/exact reader; Repository-row hover reveals actions; all actions give immediate feedback. No drag/drop or drag reordering, infinite scrolling, or stacked modals: use pagination/「加载更多」 and at most one dialog. Graph pan/zoom is presentation, not domain editing. (EXPERIENCE: Interaction Primitives.)
- **UX-DR49 — Screen-reader and contrast floor [Current]:** Announce view switches as 「对话界面」/「管理系统界面」. Operational states and meaningful generation/completion/interruption changes use accessible text and announcements, not color-only meaning or every streamed token. Verify contrast/keyboard/screen-reader behavior during implementation while preserving Fluent/Library Oak; make no fresh qualification or certification claim from adoption. (EXPERIENCE: Accessibility Floor.)

#### End-to-end journeys and explicit deferrals

- **UX-DR50 — Business question/follow-up journey [Current]:** Preserve Lin's path from email login directly to default light Business Chat, authorized inline scope selection and Enter-submitted question, retrieval feedback, genuine streamed Chinese business narrative with BL references and a useful follow-up. Optional confirmed IT/new-chat switch creates a separate dark inference context and grants no new access. Example bank names are illustrative, not required samples. (EXPERIENCE: Key Flows—Lin.)
- **UX-DR51 — Business exploration journey [Current]:** Preserve Chen's path from Business Chat through permitted System selection/question, understandable business-only explanation, citation preview/Document reading and a more specific follow-up, without code disclosure or management access prerequisites. (EXPERIENCE: Key Flows—Chen.)
- **UX-DR52 — IT evidence journey [Current]:** Preserve Wang's confirmed IT switch, authorized Repository question, source-supported Cross-Repository explanation and exact cited section/available recorded-revision source navigation. Show branches/cycles/missing bindings and explain Document Versions/Profiled Revisions/Uncertainties when asked. Do not claim executed paths, automatic update detection or knowledge of unseen latest commits. (EXPERIENCE: Key Flows—Wang.)
- **UX-DR53 — Owner-to-Knowledge journey [Current]:** Preserve Zhang's authorized management tree, Repository creation/form/save, detail selection, single Scan action and real dashboard; show gaps/Uncertainties and explicit prerequisite-corrected resume after bounded provider pause. After actual checkpoint/publication, allow manual graph refresh and CL/BL/history reading, then Business Chat over permitted published BL with provisional output/durable completion and same-answer reconnect. Terminal Scan alone does not assert current BL. (EXPERIENCE: Key Flows—Zhang.)
- **UX-DR54 — HITL shell/NotificationCenter [Deferred/History]:** Do not require the historical global bell, contextual dot, unread/count badges, 400px notification panel/cards, inbox, question/answer-dismiss or new human 「不再处理」 action. Preserve visible Uncertainties, historical decisions and lower-authority advisory text; no missing fact forces intervention or invalidates otherwise accepted analysis. Operational controls and development approvals remain current. (DESIGN: HITL Notification Icon; EXPERIENCE: NotificationCenter/Scope adoption.)
- **UX-DR55 — Chat attachments [Deferred]:** Do not add file/image upload buttons, attachment storage or parsing. Typed questions, dialogue and permitted advisory text remain current. (DESIGN: Scope adoption; EXPERIENCE: ChatArea attachments.)
- **UX-DR56 — Advanced/live graph [Deferred]:** Do not add live node animation, advanced portfolio visualization or its former five-second target. Retain dedicated basic manual-refresh graph, real Scan progress and genuine live Chat/reattachment. (DESIGN: Knowledge Graph; EXPERIENCE: GraphView/Scope adoption; NFR-1.3.)
- **UX-DR57 — Historical mockup authority [History]:** Keep June Chat/management/IA mockups as visual/layout references only; their superseded HITL, advanced graph, technical source-preview and recovery behaviors cannot override the current component/state/access contracts. No fixed illustrative System/Repository/code example is a runtime prerequisite or proof. (DESIGN/EXPERIENCE: Mockups; EXPERIENCE: Key Flows preface.)

### FR Coverage Map

F1/F2/F3 mean preserved Foundation & Identity, System & Repository Management, and Provider/Database Configuration respectively. They are historical foundations, not new active epics or blanket proof of revised clauses. E13–E17 refer to the approved delivery epics in the Epic List. Ownership here is delivery responsibility; semantic records retain the single owners in the architecture.

Every FR appears below. Non-current feature clauses receive an explicit disposition instead of a fabricated active-epic assignment or completion credit. Current safeguards surviving inside deferred/history clauses remain binding where noted. This preserves the approved PRD rather than forcing all V1 work into the core sequence.

| FR ID | Exact PRD status | Retained/current ownership and clause boundary | Non-current disposition; no core completion credit |
| --- | --- | --- | --- |
| FR-1.1 | Current | F1: retained email/password registration. | None. |
| FR-1.2 | Current | F1: retained login/logout. | None. |
| FR-1.3 | Current | F1: retained profile, display name, email and password change. | None. |
| FR-1.4 | Current | F1: existing roles/assignment/management scope. E13: live Knowledge/history/source authorization baseline; E14: composite contributors; E15: tools/history/summaries/citations; E16: all exploration surfaces; E17: integrated verification. | No self-service role requests or new fine-grained RBAC. |
| FR-2.1 | Current | F2: authorized System CRUD/context. E13/E14: organizational changes cannot prove or disprove communication or silently rewrite evidence. | None. |
| FR-2.2 | Current | F2: Repository metadata/Git/context CRUD. E13: generic onboarding and Scan without pair, sample or release-profile prerequisites. | None. |
| FR-2.3 | Current | F2: remove Repository membership. E13: immutable history/current access; E14: membership/visibility changes do not replace accepted relationship evidence. | None. |
| FR-2.4 | Current | F2: standalone organization-owned library Repository creation. E13: same explicit ordinary static Scan as any Repository. | No caller-triggered partial library scanning inferred. |
| FR-2.5 | Current | F2: best-effort Git reachability with authentication failure allowed at record creation. E13: exact authorized source acquisition/verification or truthful prerequisite failure at Scan. | None. |
| FR-3.1 | Current; revised | F3: registry/default assignments/names/endpoints/protected keys/API vocabulary. E13: bounded advisory overview separate from factual inventory/control; E15: declared Chat adapter consumption; E17: integrated adapter/readiness qualification. | Configuration vocabulary does not qualify every adapter; no model scheduling role. |
| FR-3.2 | Current | F3: multiple simultaneous provider configurations. E13/E15: preserve a task's selected provider identity; E17: qualified operational behavior. | No automatic failover or silent switching. |
| FR-3.3 | Current; revised | F3: authorized per-System role assignment through global registry. E13: overview/Worker consumption outside demo composition, no Scan Control model; E15: Chat role consumption. | None. |
| FR-3.4 | Current | F3: explicit administrator connection test. E13/E15: no paid-inference routine health and truthful prerequisite handling; E17: separate app/database/tool/provider readiness and integrated verification. | Explicit provider testing is not automatic inference health probing. |
| FR-4.1 | Current; revised | E13: exact committed inventory, language/format/layout facts and unknowns, separate bounded overview, explicit primary/supporting policy, source/configuration/template/schema/DSL eligibility, contextual README, truthful non-exclusion. | No index/ranking/filename/extension-based silent coverage cuts. |
| FR-4.2 | Current; revised | E13: one durable Scan action, independent runs, shared bounded fair work/file ownership, cancellation settlement and visibly unavailable capabilities. E14: inquiry/flow scheduling under same control; E15: shared provider admission; E17: integrated limits/recovery qualification. | No Worker-controlled arbitrary scans, indefinite target waiting, pair fallback or cancellation of independent consumers. |
| FR-4.3 | Current; revised | E13: whole logical file workstreams, progressive windows, meaningful nested Code Units and separate CL Logical Documents, application-derived identities/provenance/acceptance, preserved historical file semantics and explicit lineage. | No relabelling old file Documents as new semantic-unit analysis. |
| FR-4.4 | Current; revised | E13: atomic accepted file results/progress, truthful zero-unit outcomes/gaps and draft isolation. E14 consumes accepted observations only and validates relationships. | Tool symbols/model-proposed edges are not permanent identity/relationship authority. |
| FR-4.5 | Current; revised | E13: distinct rooted local Logical Flows, terminal primary/matching outcomes before synthesis, pinned evidence, bounded verification and exact contributions. E14: Cross-Repository flows, frozen targets and accepted external-file contributions before entire external runs finish. | No paused-work qualification, purpose-merging helpers, accepted Edge/CL rewrite or recursive repair during synthesis. |
| FR-4.6 | Current; revised | E13: usable virtual Repository/file/Code Unit and local Logical Flow reads with truthful identity/nesting. E14: one cross-Repository Logical Document across appearances. E16: complete organized CL/BL browsing and lineage/navigation experience. | No synthetic directory Documents or unjustified identity reuse. |
| FR-4.7 | Current; revised | E13: canonical structured JSON, derived presentation/search/sections, selected CL, atomic local BL publication, gaps, labelled history/staging and compatible read contract. E14: cross-contributor per-flow publication and incompatible-selection handling. E15: stable turn use of published read selection. E16: full version-aware browsing. | index.md remains optional export, never retrieval authority; failed current files cannot inherit old analysis as new. |
| FR-4.8 | Current | E13: sanitized append-only Scan/system log.md trace in required timestamp/operation/target format. E15: sanitized query trace. E17: consolidated operational trace retention/output and qualification. | Structured diagnostics do not replace log.md; no raw source/secrets/hidden reasoning/unnecessary sensitive payloads. |
| FR-4.9 | Current; revised | E13: authorized exact Source Snapshot, required CodeGraph/native MCP, applicable LSP/structured capabilities, scoped source access and retained selected/active revisions. E14: scoped inquiry/synthesis use; E15: exceptional IT reads; E16: recorded-revision reader UX; E17: durable storage/restore qualification. | No arbitrary host paths, snapshot public-version axis, source-only fallback after required CodeGraph failure, or CGC restoration. |
| FR-4.10 | Current; revised | E13: immutable versions/Version Chains, exact historical provenance, ordered promotion/history-only intent and replacement-evidence withdrawal/retirement. E14: accepted relationship/evidence-dependent versions/refresh without caller CL rewrite or historical GENERATED_FROM retargeting. E16: complete historical exploration. | Omission/exclusion/access loss/failure do not prove deletion; no human content editing. |
| FR-4.11 | History | Preserve the original spike/evidence as history. E13/E14/E15 qualify their new agent/tool integrations; E17 integrates qualification under current obligations, not as completion of this spike. | Week-1 adapter spike/schedule remains historical; no repeated dated gate or generic qualification credit. |
| FR-4.12 | History | Preserve supersession record. E13/E14 retain evidence-based validation under current FRs; bounded inquiry is not peer-verification completion. | Mandatory same-superstep peer verification, fixed 0.5 confidence and yellow-dashed/HITL mechanism superseded. Future peer verification needs separate scope. |
| FR-5.1 | Current; revised | F2: retained System advisory context administration. E13: optional catalog/framework context, never an onboarding/Scan prerequisite. E14: interpretation without treating context as deployed identity. | No mandatory maintained service mappings or automatic per-Repository catalog migration. |
| FR-5.2 | Current; revised | E13: accepted observation contract and truthful initial gaps. E14: source-supported RPC/HTTP/queue/file-storage/page-navigation mechanisms, operations, roles, namespaces, literals/expressions and unknowns. | No universal protocol claim; guaranteed gRPC-specific implementation remains excluded. |
| FR-5.3 | Current | E14: provider/consumer roles backed by evidence, including unknown/absent parties and explicit static-versus-runtime meaning. E13 supplies accepted local evidence. | Static linkage does not prove deployed routing/execution. |
| FR-5.4 | Current | E14: one-to-many consumers with evidence/access-scoped counts. E15/E16: authorized explanation/exploration of those relationships/counts. | Counts are not complete deployment inventory and must not leak restricted Repositories. |
| FR-5.5 | Current | E13: unscanned/unavailable/unknown-party identifiers and nonblocking Uncertainties in local results. E14: durable external-reference resolution/reconciliation behavior. | No automatic target Scan or blocking otherwise valid analysis. |
| FR-5.6 | Current; revised | E14: page-navigation observation semantics, loose coupling and unresolved identity; E13 retains truthful unresolved evidence. | Confidence labels do not prove relationships. |
| FR-5.7 | Current; revised | E13: bounded source/tool evidence with capability limitations. E14: focused indirect-target investigation using object/provenance evidence. | Incomplete indexes/classpaths/unresolved expressions are not negative proof or invented endpoint values. |
| FR-5.8 | Current; revised | F2: context supply. E13: bounded snapshotted Worker context with provenance and source/advisory distinction. E14: relevant optional detection context. | No mutable-catalog, fixed-XML-recipe or demo-profile communication authority. |
| FR-5.9 | Original unmet backlog | No active core owner/completion credit. Preserve an explicit original-capability register entry. | Dedicated development-time Prompt Workbench needs later design/decomposition or explicit disposition; externalized prompts/native tools do not fulfill it. No new Epic 18 invented. |
| FR-5.10 | Current; revised | E14: preserve named RPC endpoint evidence for unknown providers. E13: authorized wiki representation/read contract; E15: mode-appropriate retrieval/explanation; E16: permitted exploration. | No raw technical disclosure in Business Mode and no required per-record tool. |
| FR-5.11 | Current; revised | E14: supported file/storage observations with roles/locations and honest identity limits. E13: capture source-backed facts through admitted analysis. | Shared paths/mounts/object labels alone prove no cross-Repository identity; no external storage/configuration fetching. |
| FR-5.12 | Current; revised | E14: URL patterns/deep links/app navigation with uncertain bindings/counterparties; E15/E16 expose permitted supported explanation/evidence. | Possible static path is not executed journey. |
| FR-5.13 | Deferred / History | E13/E14: absent decisive database/gateway/registry facts remain Uncertainties, with no placeholder integration. | History: mandatory mock topology MCP/prepopulated owner mappings retired. Deferred: uniform operational-information interface/adapters/credentials/refresh. |
| FR-5.14 | Current; revised | E13: documented supported configuration/content-analysis format, preservation of structure/locations and unknown-versus-malformed distinction. E14: supported new patterns/framework knowledge via that format. | No arbitrary new syntax through config, dynamic parsers/plugins/executable configuration or external fetching. |
| FR-6.1 | Current; revised | E13: accepted Uncertainty contract/source-backed gaps, Worker proposal versus Knowledge authority. E14: missing/ambiguous targets/bindings/mechanisms/capabilities, including meaningful known/unknown-target distinctions. | Valid semantic uncertainty does not invalidate otherwise accepted analysis. |
| FR-6.2 | Current; revised | E14: finite pinned accepted evidence across authorized Systems, late targets/accepted unfinished-run files, designated candidate per Repository, bounded supporting/opposing inquiry, deduplication and live revalidation before domain acceptance. | Unique visible candidate/name/confidence alone proves no match; publication does not recursively investigate. |
| FR-6.3 | Current; revised | E13: no indefinite Worker waiting/global completion barrier. E14: cycle-safe bounded traversal with explicit outcome for every selected matching item. | Historical fewer-pending-first/older-scan-yields deadlock protocol is not restored. |
| FR-6.4 | Current / Deferred | E13/E14: nonblocking Dangling Links, identifiers, visible Uncertainties and preserved historical human wontfix decisions. E16: history/exploration presentation. | Deferred: new owner accept/dismiss/wontfix actions and product HITL. |
| FR-6.5 | Current | E14: truthful unresolved-link evidence/counts contract. E15: useful Chat-accessible explanations under mode/live authorization, using existing wiki tools. | No restricted aggregate/endpoint/target leakage. |
| FR-6.6 | Current; revised | E13: truthful currentness/coverage presentation baseline. E14: changed-accepted-evidence reconciliation and coalesced per-flow refresh with fixed active inputs and finite publication; E15/E16: stale/pending/failed visibility in reads. | No source re-analysis cascade; narrowed access/selection alone is not disproval of broader accepted truth. |
| FR-6.7 | Deferred | E13: explicit static replacement/retirement. E14: later-knowledge reconciliation. These remain independently current and do not earn incremental-feature credit. | Deferred: changed-source Impact Analysis/reuse/four-stage cascade. |
| FR-7.1 | Deferred | E13/E14: source-backed visible Uncertainties may explain missing information without blocking ordinary analysis. | Deferred: Worker question notifications and mandatory owner response/inbox. |
| FR-7.2 | Deferred | E13/E14: preserve uncertainty and bounded-correction boundaries; do not substitute an automatic repair workflow. | Deferred: dashboard answering and answer attachment. |
| FR-7.3 | Current | F2: advisory context supply. E13/E14: source facts outrank human assertions in prompts/Documents/accepted evidence. E15: dialogue/hypotheses preserve that lower authority and conflicts. | No human assertion becomes source-qualified Knowledge silently. |
| FR-7.4 | Deferred | F1: authorization preserved. E13: current operational status/pause/resume; E17: integrated recovery. Existing development approval gates remain binding throughout. | Deferred: pending-question counts/view/answer/dismiss UI. |
| FR-8.1 | Current | F2: organization-owned library source as standalone Repository. E13: explicit ordinary static Scan, same pipeline and user outcome. | Not specialized on-demand library work. |
| FR-8.2 | Deferred | E13: no implicit dependency downloads/arbitrary Repository work; explicit standalone Scan remains available under FR-8.1. | Deferred: caller/Worker-triggered partial on-demand library Scans. |
| FR-8.3 | Deferred | E13: ordinary Repository/file ownership and shared resource controls. E17: integrated concurrency/cleanup qualification. | Deferred: multiple-caller coordination for the specialized partial library-Scan feature. |
| FR-8.4 | Current; revised | E13: imports do not automatically create communication gaps; unfamiliar/unsupported behavior remains a limitation. E14: distinguish library use from communication evidence; E15: label useful hypotheses without claiming source proof. | Familiarity does not establish all library behavior or fulfill dedicated inference. |
| FR-8.5 | Original unmet backlog / Deferred | No active core completion credit for dedicated inference. E13–15 honor hypothesis/source-authority safeguards in ordinary analysis/Chat. | Original unmet backlog: dedicated unknown source-unavailable library inference. Deferred: mandatory owner notification. No external-information lookup implied; no new epic invented. |
| FR-9.1 | Deferred | E13: Scans remain explicit; do not create background monitoring. | Deferred: automatic remote monitoring, with periodic polling retained only as original future direction; webhooks excluded. |
| FR-9.2 | Deferred | E13: explicit static acquisition resolves selected Git ref once and pins unchanged source for that run. | Deferred: automatic pull-on-behind. |
| FR-9.3 | Deferred | E13: repeated explicit static Scans. E14: late-knowledge reconciliation without unchanged-source re-analysis. | Deferred: incremental changed/dependent Code Unit re-scan and analysis reuse. |
| FR-9.4 | Current | E13: exact accessible previous versions/dependencies and minimum history reader from the first new Scan result. E15: original-version citation navigation. E16: complete dedicated history exploration. | No dependency on incremental scanning. |
| FR-9.5 | Deferred | E13: exclusive current static-work ownership and ordered publication; E17: integrated concurrent-run qualification. | Deferred: incremental scheduling overlap rule; do not inherit a blanket ban into static runs. |
| FR-10.1 | Current | F3: authorized per-Repository DB type/connection/protected-credential administration. E13/E15: configuration grants no new agent-facing access; E17: preservation/security qualification. | No new schema/sample tool entitlement. |
| FR-10.2 | Deferred | E13/E14: source/accepted Knowledge remains investigation boundary; required missing operational facts stay explicit. | Deferred: new Worker database-schema inspection under external operational-information scope; no placeholder tool. |
| FR-10.3 | Deferred | F3: preserve existing configuration protection. E13–17: any preserved surface continues non-production/read-only/limited-row/no-write safeguards; this imposes no new tool feature. | Deferred: new sample-data query tools. Production database access remains unauthorized. |
| FR-11.1 | Current; revised | E15: useful light-theme Business narrative, BL-only tools/history/summaries/citation targets. E13 establishes mode-safe Knowledge reads; E16 applies same server-side rule to graph/exploration. | No CL/source/technical target disclosure to Business Mode. |
| FR-11.2 | Current; revised | E15: dark IT mode with authorized BL/CL/technical references and exceptional gap-specific exact-revision source reads; E13 supplies scoped source contract, E16 reader navigation. | No routine source re-proof, Scan initiation or Knowledge repair from Chat. |
| FR-11.3 | Current; revised | E15: confirmed mode switch, authorized saved session/new isolated inference context, ordered durable conversations, bounded completed history, access-aware non-upgrading summaries and one active answer with replay deduplication. | No disallowed history/cache import across mode/scope changes. |
| FR-11.4 | Current; revised | E13: owning stable published read-selection contract. E15: authorized user selection/defaults/clearing and per-turn discovery/pagination/later-document stability under live access; later turns use fresh selections. | Publication alone does not invalidate or retarget active turn. |
| FR-11.5 | Deferred | E15: typed questions, dialogue and permitted advisory text remain current. | Deferred: file/image upload, attachment storage and parsing. |
| FR-11.6 | Current; revised | E15: useful multi-document/follow-up synthesis through shared authorized wiki search/browse/sections/content/related navigation, carrying provenance/coverage/Uncertainties. E13 supplies owning wiki contract; E14 supplies richer cross-Repository evidence. | No mandatory per-record tools, candidate sentences, embeddings or index.md; retrieval is evidence, not instructions; shared code is not business dependency. |
| FR-11.7 | Current; revised | E15: exact claim-linked references and working previews/readers; genuine provisional deltas, first-text hypotheses, valid-provider/untruncated/tool-finished durable completion, useful interrupted text, same-turn reconnect, host-failure explicit retry, no duplicate generation and live contributor revocation/history checks. E16 may extend navigation, never gate these basics; E17 integrates lifecycle qualification. | No hidden reasoning/secrets, full-answer replay, mandatory complete-candidate JSON/whole-answer semantic verification, or automatic restart after visible output. |
| FR-11.8 | Current; retained original behavior | E15: empty selection infers relevant Systems/Repositories only through server-authorized retrieval/discovery, displays scope and supports adjustment. | Inferred relevance grants no model access authority. |
| FR-12.1 | Current; revised | E13: real per-Repository/aggregate operational status, gaps, separate primary coverage/Knowledge currentness and one end-to-end Scan action; E14: independent-run/refresh states; E15: distinct Chat retry; E17: integrated recovery diagnostics. | No staged user start calls, fake progress or conflation of Scan resume with Chat retry. |
| FR-12.2 | Current | E13: actual permitted active Worker counts under global/per-run limits; E14: count bounded inquiry/flow activity honestly; E17: integrated concurrency/resource qualification. | No fictional counts or multiplied uncontrolled global budgets. |
| FR-12.3 | Current; revised | E13: admitted primary/accepted/pending/failed counts distinct from file windows, semantic Code Units and Documents; E14/E16 preserve scoped presentation; E17 qualifies representative coverage. | No sample quota/job-count coverage proof. |
| FR-12.4 | Current | E13: actual classified errors/warnings/failed-file summaries and existing Knowledge access when analysis capabilities unavailable; E14/E15: truthful component failures; E17: consolidated sanitized diagnostics/readiness. | No demo fixtures in production status. |
| FR-12.5 | Deferred | E13/E14: visible Uncertainties/current operational controls. E17 integrates those controls, not an inbox. | Deferred: dashboard HITL answer/dismiss UI. |
| FR-12.6 | Current / Deferred | E13: authorized basic Document/history reader and truthful gaps/source availability. E15: exact cited-section reader. E16: complete basic manual-refresh Logical Flow/Document-neighborhood graph and version/evidence-aware exploration, server-filtering all node/relationship/label/count/tooltip data by mode/user/contributors. | Deferred: live animation and advanced portfolio visualization. Layout cannot edit domain Edges; real Scan progress/Chat streaming stay current. |
| FR-13.1 | Current / Original unmet backlog | E13: required native CodeGraph MCP under application-selected source/tool authority; E14: reuse approved bounded Scan invocation. | Original unmet backlog: general Repository/System/global registered MCP consumption, preserving original low priority; native adapter gives no completion credit or arbitrary tool permission. |
| FR-13.2 | Original unmet backlog | No active core owner/completion credit; retain explicit original-capability register. E15 internal wiki/exceptional scoped source tools remain separate. | General externally configured MCP consumption in Chat needs later design/disposition; no external operational-information authority inferred. |
| FR-13.3 | Current | E13/E14: Scan-purpose granted scope per invocation. E15: separate Chat-purpose approval/scope and Business restriction. E16: reader access cannot inherit agent tool authority. E17: integrated permission qualification. | Scan approval never grants Chat/Business approval. |
| FR-13.4 | Deferred | E13/E14: native CodeGraph MCP remains current. E15: internal wiki tools remain current. | Deferred: outward wiki MCP serving; no endpoint/placeholder. |
| FR-13.5 | Original unmet backlog | No active core owner/completion credit; preserve explicit original-capability register with purpose approvals. | Three-level Repository/System/global configurable MCP management requires later design/decomposition; native tools do not fulfill it or grant implicit execution authority. |
| FR-14.1 | Current | F2: authorized global advisory context applied to Systems. E13: bounded pinned application in Scan; E15: any permitted dialogue context obeys separate access rules. | No expanded global-management authority. |
| FR-14.2 | Current | F2: System advisory context for Repositories. E13: snapshotted advisory use; E14: membership is not communication evidence. | None. |
| FR-14.3 | Current | F2: Repository advisory context including standalone Repositories. E13: pinned analysis consumption. | None. |
| FR-14.4 | Current; revised | F2: retained editing surface, adapted by E13 so affected active work retains selected context and edits queue for later application. E15: separate history/summary/mode context rules that cannot mutate Scan evidence. | Deferred HITL supplies no mutation exception. |
| FR-15.1 | Current; revised | E13: applicable declared language-neutral LSP plus required CodeGraph/source capability, qualified temporary offline/local-source operation with no automatic import/build/dependency restore. E14 uses same bounded declared navigation; E17 integrates runtime qualification. | Temporary policy is not permanent topology or network air-gapping; readiness is not semantic completeness. |
| FR-15.2 | Current; revised | E13: available type/reference evidence with explicit capability/classpath limits. E14: limitations remain explicit in target investigation and relationship decisions. | Missing tool result is not proof of no relationship. |
| FR-15.3 | Current; revised | E13: application-selected Repository profiles, contained adapters/snapshots/workspaces, initially at most one LSP session per snapshot; E14 follows same authority; E17 qualifies containment/limits. | Dependency-aware import/restore and multiple sessions require later separate authorization/qualification. |

### Cross-cutting NFR responsibility

| NFR | Earliest/current responsibility | Later integration without delaying earlier safety |
| --- | --- | --- |
| NFR-1.1 | E13 Scan coverage/budgets/performance; E14 adds Cross-Repository workloads | E17 records representative whole-product results |
| NFR-1.2 | E15 useful-delta/terminal timing and multi-document quality under declared limits | E17 integrated workloads; no fixed five-Document cap or universal SLA |
| NFR-1.3 | Live graph target remains Deferred; E13 Scan/E15 Chat responsiveness remain current | E16 manual graph only; no deferred-feature completion credit |
| NFR-2.1 | F1 password-hashing foundation preserved by every changed path | Regression/integration checks, not identity reimplementation |
| NFR-2.2 | F3 protected configuration; E13 persistent usable keys/source/data baseline | E17 coherent backup/restore protection |
| NFR-2.3 | E13 source/job/read scope; E14 composite evidence; E15 dialogue/tools; E16 all reader/graph surfaces | E17 integrated permission/recovery qualification |
| NFR-2.4 | F3 and all preserved surfaces retain read-only/non-production restrictions | New database tools remain deferred; no implied access |
| NFR-3.1 | E13 classified failure/isolation/cleanup; E14 inquiry/flow work and E15 Chat follow their owners | E17 integrated failure qualification |
| NFR-3.2 | E13 explicit Scan backoff/pause/resume; E14 same controller; E15 distinct Chat interruption | E17 recovery scenarios, not first implementation |
| NFR-3.3 | E13 durable acceptance/progress/source; E14 versioned refresh; E15 durable Chat/save/reconnect | E17 coherent service restore |
| NFR-4.1 | E13 bundled two-service/non-root runtime, configured limits, persistent source/keys and safe cache cleanup | E17 complete backup/restore/operator procedures and qualification |
| NFR-5.1 | E13 useful local BL reading; E14 cross-Repository explanations; E15 usable Business Chat; E16 exploration | E17 quality evidence; schemas alone prove no usefulness |
| NFR-5.2 | Every changed surface preserves basic accessibility; E15/16 verify their journeys | No new WCAG certification promise |
| NFR-6.1 | E13 externalized overview/Worker prompts; E14 inquiry/synthesis; E15 Chat | Each owner verifies configuration, not a late rewrite |
| NFR-6.2 | E13 documented supported advisory/format interface; E14 communication use | Preserve original workbench/MCP backlog separately |

## Architecture and UX allocation

All architecture invariants bind their consumers; assignment identifies the first owning contract, not permission for another epic to violate it.

| Architecture requirements | Delivery allocation |
| --- | --- |
| AR-1–AR-8 | E13 shared scope/ownership/source/preparation/tool/semantic-unit/durable-acceptance contracts |
| AR-9–AR-13 | E13 local evidence/flow/publication baseline; E14 Cross-Repository extension and reconciliation |
| AR-14–AR-19 | E13 real control, recovery, transition/current selection/withdrawal; E14 extends evidence changes; E15/16 consume exact read/history rules |
| AR-20 | E14 focused investigation under existing E13 owners |
| AR-21–AR-23 | E13 bounded Pass 2 tools/external-information safeguards/shared admission; E14 expands inquiry/evidence; E15 joins shared provider admission |
| AR-24–AR-28 | E13 establishes the shared Knowledge read contract; E15 owns conversational execution, completion and reattachment |
| AR-29 | E13 minimum reader/history; E15 usable citations; E16 complete exploration/basic graph |
| AR-30–AR-32 | E13 runtime/persistence/limits/readiness baseline; each epic verifies its own contracts; E17 coherent restore/operator and integrated qualification |

| UX requirements | Delivery allocation |
| --- | --- |
| UX-DR1–UX-DR12 | Retained platform/brand/navigation/administration constraints on all changed surfaces; E13 adapts Scan/reader, E15 Chat, E16 exploration |
| UX-DR13–UX-DR18 | Existing component styling retained; E13 Scan/save/read actions and E15 Chat components |
| UX-DR19–UX-DR27 | E15 mode/scope/history/citation/live-output/save/reconnect/interruption journeys |
| UX-DR28–UX-DR35 | E13 real management/Scan/control/count/currentness; E14 adds reconciliation states without another dashboard owner |
| UX-DR36–UX-DR42 | E13 minimum readable results/history/source; E15 working citation routes; E16 complete graph/tree/history navigation |
| UX-DR43–UX-DR49 | Every owning surface supplies its empty/loading/error/paging/keyboard/accessibility behavior; never wait for a later polish epic |
| UX-DR50–UX-DR52 | E15 Business/IT dialogue and evidence journeys; E14 supplies Cross-Repository scenarios, E16 enriches exploration |
| UX-DR53 | E13 owner-to-local-Knowledge journey, extended by E14–16 as those capabilities arrive |
| UX-DR54–UX-DR57 | Preserve explicit Deferred/History boundaries and still-current uncertainty/safety guidance; no active feature completion credit |

## Epic List

### Epic 13: Independent Repository Scan and Local Wiki

**User outcome:** An authorized owner can start one ordinary Repository Scan, follow its real progress, and read useful local Code Logic and Business Logic Documents with exact provenance, history and honest gaps. System/selection actions queue independent runs, and standalone Repositories use the same route.

**Completion boundary:** Deliver the declared bundled-tool Docker baseline, retained exact Source Snapshots, generic lightweight preparation, native CodeGraph MCP and applicable local-source LSP, progressive whole-file analysis and semantic Code Units, accepted-file evidence, local references/Logical Flows, per-flow publication and a minimum authorized reader. Source, selection, acceptance and read-reference contracts are established here before dependent implementation splits. The reader can open selected/historical versions and exact locations without needing Chat or the graph. Core pause/resume, cancellation, idempotency, shared admission, key/source persistence, sanitized Scan/system log.md and truthful coverage/currentness are part of this epic.

**Primary FRs covered:** FR-4.1–4.10; FR-5.1/5.5/5.8/5.14; FR-6.1/6.3/6.4 current clauses; FR-7.3; FR-8.1/8.4; FR-9.4; FR-12.1–12.4; FR-13.1 native-tool and FR-13.3 Scan-purpose clauses; FR-14.4; FR-15.1–15.3. The coverage map identifies foundation integration and later extensions.

**Dependencies:** Retained foundation capabilities only. No dependency on Epics 14–17.

**Boundary:** Capture external communication observations/Uncertainties without pretending general Cross-Repository resolution is complete. Unavailable generic capabilities stay visibly unavailable, with no silent legacy-pair fallback. Local-only success is an intermediate outcome, not completion of the full product.

### Epic 14: Cross-Repository Business Understanding

**User outcome:** Users can follow source-supported business behavior across independently scanned Repositories/Systems, understand missing or ambiguous links, and see explanations improve when later accepted knowledge arrives without rescanning unchanged source.

**Completion boundary:** Extend the existing Scan Control/Knowledge owners with generic communication evidence, one-to-many relationships, finite evidence selection, bounded focused investigation, cycle handling, late/missing targets and coalesced per-flow refresh. Cover source-supported RPC/HTTP, messaging, files and navigation within declared capability limits, not fixed profiles or guaranteed universal protocol detection. Reuse Epic 13's reader and source/publication contracts; produce readable Cross-Repository results before Chat or graph exploration exists.

**Primary FRs covered:** FR-5.2–5.8, FR-5.10–5.12/5.14, FR-6.1–6.6 current clauses; Cross-Repository extensions of FR-4.2/4.4–4.7/4.9–4.10 and FR-8.4. Preserve optional context rather than mandatory owner mappings.

**Dependencies:** Epic 13. No dependency on Epics 15–17.

**Boundary:** Missing deployed/registry/database facts remain Uncertainties. No external-information connector, automatic source-update engine, human question inbox, confidence-only matching or recursive regeneration flood is introduced.

### Epic 15: Useful Streaming Wiki Conversations

**User outcome:** Business and technical users can hold useful multi-document, follow-up wiki conversations with exact usable references, genuine live output and durable dialogue that survives browser disconnect without duplicate generation.

**Completion boundary:** Deliver Business BL-only and IT BL/CL plus exceptional exact-source access, authorized manual/automatic relevance scope selection, stable per-turn Knowledge reads, bounded authorized history, isolated mode/scope contexts and real provisional streaming. Valid provider completion and one durable terminal save govern completion; reconnect joins the same bounded turn, while host failure interrupts it. Include working mode-safe citation previews and exact-section/history/source navigation on the existing reader. Add sanitized query log.md events.

**Primary FRs covered:** FR-11.1–11.4/11.6–11.8, FR-6.5 Chat consumption, FR-7.3 dialogue authority, FR-13.3 Chat-purpose approval; consumer extensions of FR-4.7–4.9 and FR-9.4. Provider/role configuration is retained and adapted, not rebuilt.

**Dependencies:** Epic 13's published Knowledge/read contracts. Epic 14 supplies the planned Cross-Repository answer scenarios; the default delivery order completes 14 before 15. Local dialogue does not intrinsically require 14. No dependency on Epic 16 for usable citations or on Epic 17 for completion/reconnect safety.

**Boundary:** No candidate-sentence restriction, mandatory whole-answer JSON/semantic verifier, routine source audit, Scan/repair authority, attachments or general custom MCP delivery. Canonical JSON wiki Documents do not turn Chat prose into a buffered JSON answer contract.

### Epic 16: Version-Aware Knowledge Exploration

**User outcome:** Authorized readers can explore Code Logic by source structure and Business Logic by Logical Flow, inspect history and exact dependencies, and navigate a useful manually refreshed scoped graph without management privileges or hidden technical-data disclosure.

**Completion boundary:** Extend the minimum reader already delivered by 13 and citation navigation delivered by 15 into cohesive dedicated browsing/search/history and graph journeys. Reuse selected-version/source/read-reference contracts; show sparse, stale, partial, retired and unavailable-source states truthfully. Filter every graph node, relationship, label, count, tooltip and destination server-side. Graph layout changes presentation, not domain Edges.

**Primary FRs covered:** Exploration portions of FR-4.6/4.7/4.10, FR-9.4 and FR-12.6 current clauses; permitted relationship presentation under FR-5.4/5.10–5.12 and FR-6.4/6.6. Existing readers must not be withheld until this epic.

**Dependencies:** Epics 13–14 and their versioned Knowledge. Reuse the shared citation contract, including 15's delivered behavior when present; the default order follows 15, but independent reader/graph correctness does not require a Chat session.

**Boundary:** No new publication authority, business relationships inferred merely from shared code, live graph animation, advanced portfolio graph, human editing of Documents or new HITL workflow.

### Epic 17: Recoverable Product Operations and Qualification

**User outcome:** An operator can configure, observe, back up and restore the delivered knowledge service coherently, and users retain authorized usable knowledge/dialogue after supported interruptions rather than losing source/key dependencies or replaying paid work.

**Completion boundary:** Integrate portable resource/storage configuration, distinct app/database/tool/provider readiness, sanitized activity traces and useful diagnostics. Deliver the operator maintenance-window backup/isolated-restore procedure for database, retained source, keys and required restore configuration. Restored leases/running states never regain execution authority; preserve completed outcomes and require fresh checks plus explicit Scan resume or Chat retry. Qualify representative complete-product behavior, performance/usage and explanation/citation quality across one/several/standalone Repositories, late targets, cycles and lifecycle failures.

**Primary FRs covered:** Integrated operational aspects of FR-3.4, FR-4.8, FR-12.1–12.4 and the reliability/deployment/security NFRs. It integrates earlier functional outcomes rather than acquiring their ownership.

**Dependencies:** Delivered Epics 13–16.

**Boundary:** Earlier epics already ship their own safety, Docker execution, source/key persistence, core recovery and verification. This is not a testing-only or deferred infrastructure epic. No extra tool service, distributed scheduler, HA/PITR daemon, paid routine health probes, new universal throughput guarantee or competition evidence ceremony is required.

### Dependency and overlap decision

| Epic | What must already work | What later epics must not supply |
| --- | --- | --- |
| 13 | Retained identity/administration/configuration/persistence, adapted explicitly | First usable reader, source retention, core Docker/recovery or truthful progress |
| 14 | 13's accepted source/Knowledge/Scan contracts and readable local outcomes | Chat or graph as the only way to consume resolved knowledge |
| 15 | 13's published read/source contracts; 14 precedes full Cross-Repository scenarios in the chosen sequence | 16's full exploration or 17's operational integration as prerequisites for citations/stream completion |
| 16 | 13–14's selected version/evidence; one shared reader/reference contract | A new competing Knowledge writer or later publication implementation |
| 17 | The delivered 13–16 product boundaries | Any essential safety or verification omitted from earlier completion |

Retain the five-way split. Shared Knowledge storage, source adapters and views are meaningful overlap, but the risk/feedback boundaries differ: first real semantic Scan, Cross-Repository interpretation, conversational usefulness, exploration usability, then coherent operational recovery. Settle one owning shared contract early and extend it; do not create rival implementations per epic. This is not an authorization to dispatch parallel stories; default execution remains sequential.

## Epic 13: Independent Repository Scan and Local Wiki

An authorized owner can start one ordinary Repository Scan, follow its real progress, and read useful local Code Logic and Business Logic Documents with exact provenance, history and honest gaps. System/selection actions queue independent runs, and standalone Repositories use the same route. The approved Epic List and coverage map above govern the complete outcome; the stories below are authored and approved sequentially.

### Story 13.1: Retain exact Repository source with scoped read access

As a System Owner,
I want the source used for authorized Repository analysis to be pinned, retained and accessible through a common read-only interface,
So that analysis and later knowledge reads can use the same committed code despite working-copy changes, temporary cleanup or process restarts.

**Acceptance Criteria:**

**Given** an existing authorized Repository, including an eligible standalone Repository, and a permitted Git ref,
**When** a trusted application service acquires a Source Snapshot,
**Then** it checks Repository authority and resolves/pins one full Profiled Revision before capture,
**And** the resulting source identity is Repository plus the exact Git commit, with integrity metadata and an internal storage locator,
**And** it requires neither a Repository pair/PairExecutionId nor a parent System solely to capture standalone source. (FR-2.4/2.5, FR-4.9; AR-1/3.)

**Given** a completed capture and a server-granted read context,
**When** an ordinary consumer lists directories/files or reads a bounded source range/content after a working copy or remote ref has changed,
**Then** the common interface returns metadata/content from the captured committed bytes, with Repository-relative locations,
**And** it never substitutes the mutable checkout, changes canonical bytes through decoding or uses tool-index data as the source authority,
**And** unavailable text/entry capabilities are explicit rather than silently altered or omitted. (FR-4.9; AR-3/5.)

**Given** a completed capture stored under deployment-configured durable source storage,
**When** the original consumer lease is disposed and a fresh process/application instance reopens the source while the original remote is unavailable,
**Then** a newly authorized consumer can verify and read the same retained bytes without relying on the former in-memory token registry, temporary operation directory or a new remote clone,
**And** source retention/configuration is independent of CodeGraph's cache/data-root lifetime. (FR-4.9; source portions of NFR-3.3/4.1; AR-3/30/31.)

**Given** a source read request,
**When** its grant has expired, access has been revoked, its Repository/revision binding is wrong, or its path is absolute, escaping or resolves outside the captured source,
**Then** the application denies the operation without disclosing unauthorized source or metadata,
**And** ordinary read contracts and sanitized errors do not expose the infrastructure's physical RootPath or credentials,
**And** any necessary physical-path seam remains restricted to trusted adapters rather than becoming an ordinary Scan/Agent file API. (FR-4.9; NFR-2.3; AR-3/6.)

**Given** a server-issued source-access budget and deadline,
**When** multiple operations exhaust the cumulative permitted work or cancellation occurs,
**Then** the operation stops with an explicit bounded limit/cancellation outcome and releases its owned transient resources,
**And** repeated calls cannot bypass the shared read-context limits by receiving an automatic fresh budget,
**And** cancellation never promotes a partial capture to a completed source object. (FR-4.9; NFR-3.1; AR-3/8/15.)

**Given** retries or multiple authorized consumers referring to the same Repository and Profiled Revision,
**When** capture completion is published, a verified capture is reopened, or one lease is released,
**Then** source identity/integrity remain stable and no reader observes a partly published capture,
**And** cleanup of one operation does not invalidate another consumer or delete verified canonical source,
**And** the initial policy performs no automatic pruning of retained canonical source; only safe unretained/disposable operation state is cleaned. (FR-4.9; NFR-3.3/4.1 source portions; AR-3/31.)

**Given** Git authentication/transport/ref failure, missing or corrupted retained bytes, or unavailable storage capacity,
**When** acquisition or reopening cannot meet the exact-source contract,
**Then** it returns a truthful sanitized failure/limitation with enough logical Repository/revision context for an authorized caller to respond,
**And** it neither silently selects another ref/source nor labels failed/partial data as a usable completed capture,
**And** any later reacquisition/retry requires fresh authority and exact-byte verification rather than trusting a new temporary path. (FR-2.5/4.9; NFR-2.3/3.1/3.3.)

**Given** this story's source capability and isolated local Git fixtures,
**When** its acquisition, bounded listing/reading, denial, concurrent-lifetime and fresh-process reopening behavior is verified against declared persistent-storage configuration,
**Then** it works independently without invoking CodeGraph, LSP, an LLM or future semantic Code Unit/Document/flow tables,
**And** only source-owned records/configuration needed for this capability are added or adapted,
**And** no test or result labels this source-only capability as a completed Scan, generated wiki or newly qualified full Docker analysis stack. (AR-2/3/16/30/32.)

**Scope and dependencies:**

- **User-visible value:** exact source survives beyond the former disposable lease, so later authorized evidence reads have a trustworthy committed input. This is delivered as a callable application/source capability; no new public Snapshot UI is required.
- **Prior dependencies:** retained Repository management/authorization and existing Git acquisition primitives only. This story does not depend on any later Story 13.x.
- **Included:** source identity/atomic capture, durable retention/reopen, generic bounded relative metadata/list/read operations, source authority and safe operation cleanup. Define the source contract before consumers depend on it.
- **Compatibility boundary:** implement a new generic durable source owner using reusable acquisition/reading mechanics. Preserve existing legacy pair consumers unless a minimal compatibility adapter is needed; wholesale pair-registry migration or rewriting every RootPath consumer is outside this story. Ordinary consumers of the new interface still receive only scoped relative access.
- **Excluded:** CodeGraph/JDT-LS installation or invocation, detailed parsing/semantic units, advisory LLM overview, file scheduling, Document/Edge/flow generation, Chat, full Scan activation and operator backup/restore. Their requirements remain in Epic 13 or later approved epic scope; they are not credited as complete here.
- **Scope of requirement credit:** completes the source acquisition/read/retention contribution to FR-4.9 and the corresponding source/privacy/durability portions of the cited NFR/ARs. It does not complete all composite FR-4.9 or NFR-4.1 obligations.
- **Later gates:** story approval accepts this delivery definition only. Technical planning must still pass independent review and any triggered Agent-tool/Scan-strategy/PRD authorization before tasks/implementation; data/schema changes remain owned and minimal.

**Definition approval:** The user approved Story 13.1 on 2026-09-11. Approved proposal SHA-256: `2FAB3D492CA0DD5958B60DB5BF07DAEBEF4BEFA337FB59897A3C8FFD89003F83`. This is story-definition approval, not implementation, feature activation or runtime qualification.

### Story 13.2: Run Vulgata in Docker with durable storage and bundled analysis tools

As an Administrator,
I want to run Vulgata and PostgreSQL through a documented two-service Docker deployment with retained data/source/keys and declared bundled tools,
So that I can develop and operate the application without installing its analysis tools on the host or losing protected information when containers are replaced.

**Acceptance Criteria:**

**Given** a declared supported container platform and isolated operator-configured storage and credentials,
**When** the documented Compose deployment starts from a clean database,
**Then** it starts only the Vulgata application and PostgreSQL services, applies the existing startup migration/seeding contract to the configured database, and makes the existing authenticated administration capabilities usable,
**And** application startup requires no host-installed Git/Node/Java/CodeGraph/JDT-LS, local LLM service, remote analysis bridge or live provider inference,
**And** no fixed sample Repository or exactly-two Repository binding is a deployment prerequisite. (NFR-4.1; AR-15/30/32; retained foundation and UX-DR1–12 constraints.)

**Given** the versioned application image and its declared tool inventory,
**When** the image is built and its installed assets are checked on the supported container platform,
**Then** it contains the required Git/runtime dependencies, CodeGraph 1.5.0, Java 21 and JDT-LS archive 1.61.0-202608141332 at container-owned configured paths,
**And** tool versions, platform-specific asset identities and installation inputs are reproducible and checked rather than inferred from a developer's host installation or copied Windows executable hashes,
**And** missing or mismatched required build assets fail explicitly; an installation/version check never claims a connected native MCP session, an indexed Source Snapshot or usable LSP navigation. (FR-4.9/15.1 deployment contribution; NFR-4.1; AR-6/30/32.)

**Given** the application image and its required source/key/workspace/log locations,
**When** the application and bounded installation-check processes run,
**Then** they run as a non-root container user with writable access limited to the required configured state locations,
**And** ordinary runtime operation requires neither privileged containers/Docker-daemon access nor a mounted developer home, source checkout or host tool installation,
**And** missing required mount permissions produce a sanitized actionable failure rather than elevating privileges or silently switching to ephemeral storage or a fresh temporary key ring,
**And** container health checks use commands actually present in the image. (Architecture gateway runtime security baseline; NFR-2.2/2.3/4.1; AR-3/30.)

**Given** persisted administration data, a protected test credential and an exact Source Snapshot captured through Story 13.1 in the container,
**When** the application and database containers are recreated with the same configured durable volumes and the original Git remote is unavailable,
**Then** the application can still read the persisted records, use the protected credential through its existing authorized boundary, and reopen/read the same exact retained source through a fresh authorized grant,
**And** this does not depend on a previous process, in-memory lease registry or disposable Tool Workspace,
**And** if container termination interrupts another Source Snapshot capture, restart does not expose that unfinished capture as completed, while previously verified retained source remains reopenable through fresh authorization,
**And** canonical source, PostgreSQL data and Data Protection keys remain separate from replaceable tool state; disposing unleased temporary state does not delete retained source, keys or database data. (FR-4.9 source contribution; NFR-2.2/3.3/4.1; AR-3/30/31.)

**Given** deployment-provided storage locations, container resource limits and the source limits already owned by Story 13.1,
**When** these settings are changed within the declared supported profile or required storage is invalid, unavailable or exhausted,
**Then** the application uses the declared configuration and reports the affected capability truthfully without relying on a particular workstation path or capacity,
**And** failure does not silently redirect durable state, prune canonical source, alter Repository eligibility or report incomplete capture as success,
**And** documentation distinguishes these implemented limits from later Worker/provider/tool admission controls and distinguishes retained volumes from backup or cross-host migration. (NFR-3.1/4.1 source/runtime contribution; AR-30–32.)

**Given** an otherwise usable application/database with analysis integration unavailable, invalid tool configuration or no configured provider,
**When** an operator inspects readiness or a user opens existing authorized application/knowledge surfaces,
**Then** application/database status, bundled-tool installation status and unqualified/unavailable Scan/provider capability are distinguishable,
**And** unavailable analysis does not by itself prevent existing authorized reads or falsely mark the generic Scan ready,
**And** checking packaged Linux assets never satisfies an old pair-era Worker runtime gate or enables legacy Prepared Scan/Pass 1/Resolution/Pass 2 execution as the generic workflow,
**And** routine startup/health inspection makes no provider inference calls and does not dispatch saved or legacy Scan work as a fallback; any necessary composition guard is limited to enforcing that boundary. (FR-3.4/12.4; NFR-3.2/3.3; AR-15/16/32.)

**Given** the deployment instructions and this story's isolated fixtures,
**When** its runtime boundary is qualified,
**Then** evidence covers a real two-service startup, non-root identity and installation checks, Story 13.1 source use, container recreation with data/key/source continuity, and truthful failure/readiness behavior,
**And** configuration/proxy/CA/secret injection and stop/recreate instructions avoid committed credentials, baked-in secrets and privileged host prerequisites,
**And** the result records the tested platform/configuration and does not claim backup/restore, automatic Scan recovery, functional MCP/LSP analysis, a generated wiki, real LLM answers or universal deployment/performance qualification. (NFR-2.2/4.1; AR-30–32.)

**Scope and dependencies:**

- **User value:** a usable application and durable source capability on the actual development deployment, with fewer hidden host dependencies and no loss of encrypted configuration on container replacement.
- **Prior dependencies:** approved Story 13.1 plus retained application/Identity/administration/persistence. No future Epic 13 story or Epic 14–17 is needed for the acceptance scenarios.
- **Included:** image/Compose packaging, supported platform/tool installation identity, configured durable mounts and key persistence, minimal startup/configuration/readiness adaptation, a non-root runtime, and bounded real container qualification of this slice.
- **Not an empty infrastructure task:** completion requires authenticated application use and source capture/reopen across recreation, not only a successful image build or a list of installed binaries.
- **Compatibility boundary:** preserve retained administration and historical read behavior. Make only the startup/configuration guards needed to avoid invalid global tool assumptions and unintended legacy dispatch. Do not migrate the pair-era execution model or implement the generic Scan coordinator here.
- **Excluded:** CodeGraph indexing/native MCP exposure, snapshot-bound LSP launch/navigation and its no-import/no-restore enforcement, file classification/structured-content analysis, Agent execution, Worker/provider/tool scheduling, Scan activation/recovery and new knowledge readers. Those remain later Epic 13 work. No XML/sample workflow, new tool service, package-upgrade campaign, general deployment framework or public Snapshot UI is added.
- **Later operations:** coherent backup/isolated restore, cross-host migration and comprehensive operator diagnostics are not container recreation and remain outside this story. The initial source policy still forbids automatic canonical-source pruning.
- **Requirement credit:** only the described Docker/installation/persistence/readiness contributions to the cited composite requirements. Native tools, full recovery/admission and complete NFR-4.1 qualification remain outstanding. Existing Chinese UI/accessibility/authorization constraints apply to any touched surface; no UX redesign is required.
- **Later gates:** story approval accepts the definition only. A concrete technical plan must pass independent review and any triggered Agent-tool/Scan-strategy/PRD authorization before task generation, with proportionate TDD/review/runtime verification afterward.

**Definition approval:** The user approved Story 13.2 on 2026-09-11. Approved proposal SHA-256: `60A06088341CA9C7251D7947EEA3B337A60631BD6E3DF98C94B27E00D5100B00`. This is story-definition approval, not implementation, feature activation or runtime qualification.


### Story 13.3: Inventory Repository source and explain file eligibility

As a System Owner,
I want an exact-source inventory and a factual Repository Profile with explicit file-selection reasons,
So that analysis can cover the Repository deliberately without silently losing unfamiliar source or confusing preparation with completed understanding.

**Acceptance Criteria:**

**Given** an authorized retained Source Snapshot, including one from a standalone Repository,
**When** the inventory capability enumerates it through Story 13.1's scoped source interface,
**Then** it records the captured directory/file structure and available file metadata under the exact Repository and Profiled Revision,
**And** every discovered entry has an explicit outcome, with Repository-relative locations rather than physical workspace paths,
**And** directories remain structural metadata, not synthetic files, work items or Code Units,
**And** enumeration requires neither a Repository pair nor CodeGraph, LSP, an LLM or the mutable working copy. (FR-4.1/4.9 inventory contribution; AR-1/3/4.)

**Given** source metadata and bounded non-executing format/signature inspection,
**When** preparation records language, format and layout information,
**Then** the Repository Profile distinguishes observed facts, evidence-backed classifications and unknown or ambiguous information,
**And** bounded probes identify the detector/rule, inspected extent and any limitation rather than claiming whole-file understanding,
**And** filename/extension hints do not become unsupported framework or business-behavior claims,
**And** any shared format recognition uses the Source Content Analysis contract, not a second preparation-only parser system,
**And** this step does not extract semantic Code Units, interpret a fixed XML/RPC workflow, execute source/configuration/builds or fetch external information. (FR-4.1; AR-4/5/7.)

**Given** an explicit application-owned Scan policy and the factual inventory,
**When** Scan Control derives the file-selection contribution to the Scan Work Plan,
**Then** it records a primary, supporting, context-only or explicitly excluded disposition and the applicable policy/evidence reason for each inventoried file,
**And** handwritten code, configuration, templates, schemas and DSLs remain eligible; identified tests, generated source and vendored dependencies default to supporting evidence, while README/explanatory documents supply context rather than automatic Code Logic work,
**And** automatic classification cannot silently exclude or downgrade source based solely on a filename, unfamiliar extension, CodeGraph omission or advisory ranking,
**And** an explicit policy exclusion remains visible as a policy choice rather than a claim that the source was analyzed. (FR-4.1; AR-2/5.)

**Given** unfamiliar extensions/dialects, conflicting format hints, unsupported content access or files too large for a bounded inspection,
**When** the inventory/profile result is produced,
**Then** unknown classification, capability limitation and source eligibility remain separate facts, with unfamiliar source kept eligible under the declared policy rather than silently discarded,
**And** the result distinguishes observed entry/disposition counts from source actually analyzed, Code Units or generated Documents,
**And** neither bounded inspection nor an empty eligible set is reported as completed semantic coverage. (FR-4.1; NFR-1.1; AR-4/5/8; UX-DR35 data contract.)

**Given** a completed inventory/profile result and its exact-source and policy bindings,
**When** a later authorized consumer reads it or the same declared inputs are evaluated again,
**Then** the result has stable, inspectable source/policy provenance and does not follow a moved Git ref or silently adopt changed policy,
**And** identical source, policy and declared inspection rules produce the same factual classifications and selection outcomes rather than relying on incidental enumeration order,
**And** a changed policy produces a separately identified selection result without rewriting source facts or earlier results,
**And** Source Snapshot owns inventory; Scan Control owns the Repository Profile and policy selection; only records needed for these results are introduced, not future job, Code Unit or Knowledge schemas. (FR-4.1/4.9; AR-2/3/4 and the shared record-ownership table.)

**Given** bounded traversal/inspection limits, pagination, cancellation, access revocation or an unreadable entry,
**When** the capability cannot finish the requested inventory or inspection,
**Then** it returns an explicit incomplete/limited/failed outcome and preserves the distinction between enumerated entries and unknown remainder; pagination or capped output cannot masquerade as a complete inventory,
**And** it honors the shared source grant/budget, stops owned work and exposes no unauthorized content, paths or counts,
**And** a limited inspection of one entry is distinguished from incomplete Repository enumeration; neither silently turns unfinished source into excluded or analyzed work. (NFR-1.1/2.3/3.1; AR-3/4/5/8.)

**Given** isolated committed-source fixtures on the Story 13.2 deployment baseline,
**When** this capability is verified against mixed languages, configuration/templates/schemas/DSLs, unfamiliar extensions, misleading filenames, supporting/context files, explicit policy exclusions and interrupted/limited enumeration,
**Then** exact binding, reasoned dispositions, explicit unknowns and honest completeness/counts are observable through the real application/source contracts without any model or analysis-tool calls,
**And** these checks do not activate the generic Scan route, manufacture Code Units/Documents, qualify tool readiness or waive required CodeGraph preparation before Pass 1. (FR-4.1/4.9; AR-4/5/7/16/32.)

**Scope and dependencies:**

- **Value:** explain what exact source exists and why it is eligible, supporting, context-only or excluded before expensive analysis. A callable result is sufficient; no new inventory-management UI or additional user start button is required.
- **Dependencies:** Stories 13.1–13.2 and retained Repository authorization only. This capability must work without future structured parsers, tool sessions, Worker execution or the complete Scan coordinator.
- **No hidden execution prerequisite:** bind directly to the Source Snapshot, policy and declared inspection rules; require no ScanRun/Repository Scan Run or job record. Reusing an existing persistence type must not introduce those prerequisites. Later Scan Control can attach the result to its execution contract.
- **Ownership:** establish inventory and profile/policy-selection behavior under their adopted owners. Later ordering/scheduling extends the same Scan Control selection contract; it must not create a competing eligibility authority. Technical planning chooses the minimal result storage/bindings required, without preallocating the entire Scan lifecycle.
- **Shallow inspection only:** bounded signature/format recognition and layout facts are in scope. Implement only the factual probing operation needed now behind the single Source Content Analysis contract, without a parallel temporary parser or placeholder future methods. Structured trees, dialect meaning, detailed on-demand content tools and dynamic parser/plugin generation are not required here; unfamiliar content remains accessible through scoped source reads.
- **Excluded:** LLM Advisory Overview, CodeGraph indexing/MCP, LSP startup/navigation, semantic Code Unit extraction, communication classification/resolution, Worker scheduling, full Scan recovery/progress UI, Knowledge publication and automatic update scanning. No fixed language/sample-repository recipe is adopted.
- **Requirement credit:** the inventory/profile/explicit file-selection portion of FR-4.1 and related source/coverage/privacy invariants only. This does not complete all PreScan, FR-4.9, source analysis or the full UX-DR35 presentation requirement.
- **Later gates:** definition approval does not authorize implementation. The concrete Scan-strategy plan requires independent plan review and the explicit planned-change approval before task generation, followed by normal verification gates.

**Definition approval:** The user approved Story 13.3 on 2026-09-11. Approved proposal SHA-256: `B32234963DBF46FADAE1091B155AE379046970728A521F3D69BD1B6D01F083C6`. This is story-definition approval, not implementation, feature activation or runtime qualification.


### Story 13.4: Inspect structured and unfamiliar source without executing it

As a System Owner,
I want exact-source configuration, schema, template and DSL content to remain inspectable through a shared safe format capability,
So that unfamiliar meaning does not silently remove useful evidence or become invented business behavior.

**Acceptance Criteria:**

**Given** an authorized Source Snapshot and Story 13.3's factual inventory,
**When** a consumer requests a compact format summary or bounded structural detail,
**Then** the same Source Content Analysis contract used for shallow recognition provides the declared XML, JSON, YAML and properties support,
**And** results retain Repository, Profiled Revision, relative locations, inspected extent and format/profile provenance, without treating parsed nodes as Code Units. (FR-4.1/4.9/5.14; AR-3/5.)

**Given** recognized syntax containing an unfamiliar framework/dialect or unsupported semantic construct,
**When** the format reader processes it,
**Then** useful supported structure and source locations remain available while unknown meaning and any unsupported portion are explicit,
**And** an optional declared dialect interpreter cannot turn absent knowledge into confirmed communication or deployment facts. (FR-5.8/5.14; AR-4/5/22.)

**Given** unrecognized syntax, ambiguous format hints or malformed recognized content,
**When** inspection finishes,
**Then** results distinguish these outcomes from one another and from source-access failure,
**And** authorized source reading remains available for unfamiliar syntax; any partial structure is labelled with its limitation,
**And** no outcome silently changes Story 13.3's eligibility or claims whole-file analysis. (FR-4.1/5.14; AR-3/5.)

**Given** documented supported format/profile configuration,
**When** an owner supplies supported framework/context patterns or a consumer parses hostile content,
**Then** permitted declarative configuration works without a code change within its documented format,
**And** the capability performs no configuration execution, external entity/resource fetching, dependency restoration, dynamic parser generation or plugin loading,
**And** unsupported syntax extensions explicitly require implementation rather than pretending configuration can supply arbitrary parsers. (FR-5.14; NFR-2.3/6.2; AR-5/22.)

**Given** cumulative source/output/deadline limits, deep or expansive input, pagination, cancellation or access revocation,
**When** requested inspection exceeds its authority or safe budget,
**Then** it stops with an explicit bounded/incomplete outcome, preserving located evidence already permitted without presenting truncation as completeness,
**And** repeated detail calls share their granted budget and disclose no unauthorized source, counts or physical paths. (NFR-1.1/2.3/3.1; AR-3/5/6.)

**Given** isolated committed fixtures containing all declared formats, an unfamiliar extension/dialect and malformed/hostile content,
**When** real application consumers call summary and detail operations on the Docker baseline,
**Then** verification demonstrates preserved locations, safe bounded inspection and truthful unknown/malformed distinctions without an LLM, CodeGraph or LSP requirement,
**And** no result is labelled a generated Document, accepted communication observation or completed Scan. (FR-4.9/5.14; AR-5/16/32.)

**Scope and dependencies:**

- Depends only on Stories 13.1–13.3. Extend their Source Content Analysis seam and only necessary storage; no parallel parser, fixed RPC/XML recipe or mandatory dialect interpreter.
- Callable inspection needs no new preparation button. UX-DR35/46 constrain truthful limitations/counts; preserve UX-DR45/47–49 if UI changes, without claiming full dashboard/reader delivery.
- The spine identifies `Infrastructure/CommunicationDetection/` and source readers as reuse candidates, not proven generic support. Technical planning verifies reuse and passes the Agent-tool/Scan-strategy approval gate.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.4 grants no implementation or runtime authority.

### Story 13.5: Prepare exact Repository source for native CodeGraph MCP use

As a System Owner,
I want required CodeGraph preparation and native MCP access bound to the exact authorized Repository source,
So that later analysis can investigate real structural evidence without querying a different checkout or silently losing a required capability.

**Acceptance Criteria:**

**Given** an authorized Source Snapshot on Story 13.2's Docker baseline,
**When** the application requests CodeGraph preparation,
**Then** a trusted adapter materializes or reopens a contained Tool Workspace from verified canonical bytes, performs required native preparation and connects the native MCP surface,
**And** preparation records its source binding, declared tool/configuration identity and actual outcome; installation alone is not readiness. (FR-4.9/13.1; AR-3/4/6/30.)

**Given** a prepared authorized binding and an approved Scan-purpose invocation,
**When** the native recommended CodeGraph MCP tools are exposed or invoked,
**Then** the application fixes `projectPath` outside model-controlled arguments and applies granted scope, cumulative budgets and provenance,
**And** a caller cannot switch project, Repository, revision, credentials or physical workspace through arguments, identifiers or returned paths,
**And** Scan permission grants neither Chat approval nor Business Mode source access. (FR-13.1/13.3; NFR-2.3; AR-3/6.)

**Given** successful native tool results, index omissions or unsupported constructs,
**When** the application returns evidence to an authorized consumer,
**Then** native exploration remains usable with exact source provenance and explicit capability/index limitations,
**And** any returned source obeys the same disclosure authority as scoped source reads,
**And** transient tool symbols, absent results and index completion establish neither permanent Code Unit identity nor complete source coverage. (FR-4.1/4.9; AR-4/5/6/7.)

**Given** startup, preparation, connection or later required-CodeGraph failure,
**When** capability readiness is evaluated,
**Then** the result identifies the failing prerequisite and permits an authorized bounded retry of that exact binding,
**And** required readiness remains unavailable until re-established, without source-only fallback, CGC restoration or legacy pair routing,
**And** application/database readiness and existing authorized Knowledge reading remain separate. (FR-4.9/12.4; NFR-3.1; AR-15/16/32.)

**Given** concurrent consumers, cancellation, timeout, access loss or preparation failure,
**When** the Tool Runtime settles the operation,
**Then** configured tool capacity and ownership prevent unsafe shared mutation, stop owned work and release disposable leases/process resources within a declared finite cleanup policy,
**And** cleanup preserves canonical retained source and another authorized consumer's still-owned tool state. (NFR-2.3/3.1/4.1; AR-3/23/31.)

**Given** isolated real Repository fixtures inside the two-service deployment,
**When** qualification exercises actual preparation, MCP discovery/invocation, wrong-project rejection, limitations and failure/reopen paths,
**Then** evidence shows real native MCP use against the captured revision without host-tool installations or a remote bridge,
**And** records distinguish native-tool qualification from model quality or completed Scan. (FR-4.9/13.1; NFR-4.1; AR-30/32.)

**Scope and dependencies:**

- Depends on Stories 13.1–13.4, not a Repository Scan Run or future Worker/Knowledge schemas. Tool Runtime owns process/workspace leases; later durable control composes this capability.
- Native recommended exploration, not a homegrown query protocol. General configurable MCP remains original backlog. No LSP, semantic acceptance or public Scan cutover.
- UX-DR34/46 constrain readiness feedback; preserve UX-DR45/47–49 if touched. The spine identifies `Infrastructure/Analysis/CodeGraph/` for reuse, not runtime proof. Technical planning qualifies native package/launch behavior and obtains tool/Scan-strategy approval.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.5 grants no implementation or runtime authority.

### Story 13.6: Navigate exact source through one declared language-neutral LSP session

As a System Owner,
I want applicable language navigation over the exact Repository source with honest tool limitations,
So that analysis can use available type/reference evidence without silently importing dependencies or confusing incomplete navigation with absent behavior.

**Acceptance Criteria:**

**Given** a retained Source Snapshot and factual Repository Profile,
**When** the application selects the declared applicable LSP profile,
**Then** selection is deterministic application policy, with JDT-LS as the initially implemented adapter and an explicit not-applicable/unsupported outcome elsewhere,
**And** callers cannot choose adapters, revisions or physical workspaces; language-neutral capability contracts do not promise support for unimplemented languages. (FR-15.1/15.3; AR-4/6.)

**Given** an applicable JDT-LS profile on the bundled Docker baseline,
**When** Tool Runtime starts or reuses navigation,
**Then** at most one LSP session serves that Source Snapshot using its whole-Repository contained workspace and separate replaceable state,
**And** canonical source remains unchanged and session provenance records the selected profile, tool/runtime inputs and exact source binding. (FR-4.9/15.3; AR-3/4/30.)

**Given** source containing Maven/Gradle build declarations, wrappers or missing dependencies,
**When** initial local-source navigation initializes and runs,
**Then** the qualified launch policy disables automatic project import, build/wrapper execution and dependency restoration/download rather than merely documenting client settings,
**And** it does not acquire hidden host dependency state,
**And** unavailable classpath/dependency evidence is explicit; this temporary policy neither promises permanent local-only topology nor imposes network air-gapping. (FR-15.1–15.3; AR-4/15.)

**Given** an authorized source location and a declared navigation capability,
**When** a consumer requests supported definition/type/reference information,
**Then** normalized Repository-relative locations or evidence handles, limitations and invocation receipts support subsequent scoped source reads,
**And** the response distinguishes successful, incomplete, unsupported and failed navigation,
**And** empty results or missing external types do not prove absence of a relationship or complete semantic understanding. (FR-15.2; NFR-2.3; AR-3/6.)

**Given** concurrent requests, exhausted limits, cancellation, access revocation or a failed session,
**When** Tool Runtime processes or settles work,
**Then** shared operations queue or serialize where safety is unqualified, respect cumulative deadlines/budgets and stop owned requests within the declared cleanup bound,
**And** source grants and session leases prevent cross-Repository disclosure or disposal underneath independent authorized consumers,
**And** any replacement session retains the same verified source/profile binding. (NFR-3.1/4.1; AR-3/6/23/31.)

**Given** real isolated Java Repository fixtures in Docker, including whole-snapshot local references and missing-dependency/build-trigger cases,
**When** qualification exercises supported navigation, concurrent reuse, denied scope and failure cleanup,
**Then** evidence proves actual JDT-LS requests and the no-import/build/restore policy at the tested configuration, not merely archive presence or startup,
**And** results make no complete-classpath, multiple-session, multi-language or full-Scan claim. (FR-15.1–15.3; AR-4/30/32.)

**Scope and dependencies:**

- Depends only on Stories 13.1–13.5, not future Workers/Knowledge schemas. LSP never substitutes for required CodeGraph.
- Initially only JDT-LS; no dependency-aware import/builds/restores, multiple sessions, remote bridge or host-install prerequisite. One runtime contract owns tool/profile details.
- UX-DR34/46 constrain applicability/limitations; preserve UX-DR45/47–49 if touched. The spine identifies `Infrastructure/Analysis/Java/` for reuse; assets and VS Code settings do not qualify standalone behavior. Agent-tool/Scan-strategy plan approval remains mandatory.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.6 grants no implementation or runtime authority.

### Story 13.7: Recover and control durable Repository preparation with shared limits

As a System Owner,
I want Repository preparation to have durable ownership, real status and explicit recovery under shared limits,
So that interruption or competing work does not lose exact-source progress, double-own work or consume capacity indefinitely.

**Acceptance Criteria:**

**Given** an authorized Repository preparation request, including a standalone Repository,
**When** Scan Control accepts it through a callable application entry point,
**Then** durable execution fixes one Source Snapshot, intent, authority, preparation jobs and outcomes outside request/circuit lifetime,
**And** existing inventory/tool capabilities require no pair identity or Repository completion barrier. (FR-4.2/4.9; AR-1/2/3/14.)

**Given** concurrent eligible preparation and bounded source-inspection jobs,
**When** Scan Control admits work,
**Then** deterministic fair scheduling enforces configured global/per-run capacity and one valid run/snapshot/file lease owner for the relevant exclusive work,
**And** Agent Execution provider admission and Tool Runtime capacity remain application-wide, not multiplied per run,
**And** waiting work holds no Worker permit while awaiting unavailable dependencies. (FR-4.2/12.2; AR-2/23/30.)

**Given** a job completion replay, lost acknowledgement, expired ownership or host restart,
**When** durable state is reconciled,
**Then** completed results are reused idempotently and stale attempts cannot advance progress,
**And** unfinished work becomes explicitly recoverable/paused with old ownership invalidated, rather than automatically replaying saved running state,
**And** source, preparation evidence and history remain intact. (NFR-3.3/4.1; AR-15/31.)

**Given** temporary provider errors, configuration failure or required-tool failure,
**When** the controller applies its classified recovery policy,
**Then** temporary Scan-provider failures receive bounded increasing retries and then durable pause; configuration failures require correction,
**And** explicit resume rechecks current authority, exact source, provider identity and required capabilities without silent provider/source substitution,
**And** Chat retains its distinct lifecycle. (NFR-3.1/3.2; AR-15/23.)

**Given** pause, cancellation, deadline expiry or lease loss during queued/running work,
**When** control settles that request,
**Then** it fences late results, stops owned pending/downstream work and completes or reports failed cleanup within a configured finite bound,
**And** permits/processes/unneeded leases are reclaimed without cancelling independent consumers or deleting retained source,
**And** cancelling remains distinct from confirmed cancellation; one reusable append-only `log.md` sink records controller/system events as `## [YYYY-MM-DD HH:MM] operation | target`, without source, credentials or hidden reasoning; available usage is reported and unknown usage is not zero. (FR-4.8/12.4; NFR-3.1; AR-14/23/32.)

**Given** completed preparation, a failure, or exhaustion of available capacity,
**When** status is read and isolated restart/concurrency/replay scenarios are verified,
**Then** actual operations and admitted primary pending work are inspectable separately from semantic coverage and Knowledge Currentness,
**And** preparation completion never marks files analyzed, invents Worker/Document counts or enables the incomplete generic Scan action. (FR-12.1–12.4; AR-8/14/16/32; UX-DR31–35.)

**Scope and dependencies:**

- Depends on Stories 13.1–13.6. Deliver preparation/inspection handlers and callable status/control; only necessary execution/lease/progress records, no Knowledge schemas, full dashboard or stage-start buttons.
- Own shared admission/recovery; later jobs extend it. Controlled clients verify provider-failure policy before 13.8's real overview. Later Scan stages/Epic 15 queries reuse the log sink; no whole-FR-4.8 credit yet.
- UX-DR33–35/46 constrain control/status; preserve UX-DR45/47–49 if touched. The spine flags pair coordination for decomposition, not automatic migration. Independent review and Scan-strategy approval remain required.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.7 grants no implementation or runtime authority.

### Story 13.8: Add bounded advisory Repository overview and file ordering

As a System Owner,
I want a bounded overview of Repository structure and supplied context with traceable advisory ordering,
So that analysis can start with useful orientation while source facts, file eligibility and scheduling authority remain reliable.

**Acceptance Criteria:**

**Given** authorized Global, System and Repository context plus an exact-source inventory/profile and selection,
**When** Scan Control creates the overview job,
**Then** it captures the immutable Scan Context Snapshot with provenance, including absent context for standalone Repositories,
**And** edits queue for later application; optional context/catalog assertions are not source proof and require no detailed service-to-Repository mappings. (FR-5.1/5.8/14.1–14.4; AR-2/4/22.)

**Given** retained global provider registration, simultaneous provider configurations, per-System role assignments and an externalized prompt,
**When** Agent Execution runs the bounded model task,
**Then** the application pins provider/task assignment and prompt; standalone Repositories use the documented global assignment without a System override or automatic failover,
**And** MAF owns the loop under shared admission/recovery and separate Scan-purpose tool grants,
**And** shared Agent Execution safeguards keep source/Documents within organization infrastructure for self-hosted providers; external providers receive only necessary authorized context,
**And** bounded inputs grant no credential, workspace, Repository-selection or Worker-allocation authority. (FR-3.1–3.3/5.8/13.3; NFR-2.3/6.1; AR-2/4/6/23.)

**Given** model-assisted interpretations, suggested priorities and unknowns,
**When** Scan Control validates and stores the response,
**Then** Advisory Overview is separate from the factual Repository Profile and identifies its source/context/provider/prompt inputs, limitations and hypotheses,
**And** advisory claims do not become semantic Code Units, Code Logic Documents, accepted observations or source-confirmed framework facts. (FR-4.1/5.8; AR-2/4/5/7.)

**Given** Story 13.3's explicit file dispositions and valid advisory ordering hints,
**When** Scan Control extends that same Scan Work Plan with ordering,
**Then** the admitted primary set is unchanged and every admitted file remains represented, including unfamiliar formats and files omitted from model output or CodeGraph indexes,
**And** duplicate/invalid hints are rejected or ignored with a recorded reason while deterministic application tie-breaking preserves a complete order,
**And** ranking cannot create primary jobs from supporting/context-only files. (FR-4.1; AR-4/5/23.)

**Given** hostile instructions in supplied text, truncated/invalid output, access revocation, cancellation or provider failure,
**When** overview execution or acceptance cannot safely complete,
**Then** content remains data, disclosure stays authorized and the durable job records its actual failure/pause outcome,
**And** no partial overview becomes completed advice; resume preserves pinned source/context/provider/prompt, while changed inputs require separately identified subsequent work. (NFR-2.3/3.1/3.2; AR-4/15/23.)

**Given** mixed-source and standalone fixtures plus supplied context changed during execution,
**When** boundary tests and a recorded real configured-provider Docker run exercise the capability,
**Then** evidence verifies provider input boundaries, useful orientation, context provenance and unchanged eligibility, with measured limits and explicit unknown usage,
**And** the result is callable/readable without Worker dispatch or a completed Scan; routine health checks perform no inference. (FR-3.4/4.1; NFR-1.1/6.2; AR-32.)

**Scope and dependencies:**

- Depends on Stories 13.1–13.7 and retained administration; only necessary overview/context records, no Knowledge schema prerequisite.
- Bounded structure/supplied context only: no detailed source analysis, unrestricted navigation, new admin editor, Prompt Workbench, fixed recipe or model scheduler. Unavailable advice remains explicit.
- UX-DR35/46/53 cover preparation/provenance only; preserve UX-DR45/47–49 if touched. The gateway identifies provider/context reuse, not qualified adapters. Scan-strategy/tool plan approval follows independent review.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.8 grants no implementation or runtime authority.

### Story 13.9: Accept semantic file Knowledge with stable identities and ordered selection

As a System Owner,
I want validated whole-file results to become durable, versioned Code Logic Knowledge,
So that accepted analysis has trustworthy identity and provenance without unfinished or older work replacing it.

**Acceptance Criteria:**

**Given** a complete file proposal bound to an admitted File Workstream,
**When** Knowledge validates its meaningful Code Units,
**Then** one owning versioned contract defines canonical selectors, nesting, aliases and justified identity lineage, with application-derived identities, fingerprints and source/run bindings,
**And** each Code Unit owns a CL Logical Document; shared anchors are allowed, but conflicting identity and duplicate cross-file authorship are rejected,
**And** tool IDs, window boundaries, moving ranges and unproven rename/split/merge mappings cannot silently establish identity. (FR-4.3/4.6; AR-2/7/16.)

**Given** validated source provenance, content, observations and complete-file disposition,
**When** the current lease owner submits acceptance,
**Then** Infrastructure atomically persists the Accepted File Manifest, Code Units, immutable canonical JSON CL versions, Communication Observations, accepted Uncertainties and authoritative Scan Control progress under the same fence,
**And** every contribution retains its exact Source Anchor and access scope; generic observations preserve available roles/mechanisms/operations/namespaces/literals/expressions and explicit unknowns, including available identifiers and known/unknown-target distinctions for missing parties,
**And** existing historical human wontfix decisions remain preserved without new HITL actions; Workers neither assign authoritative relationships nor directly write Knowledge. (FR-4.4/4.7/4.9, FR-5.5, FR-6.1/6.4; NFR-2.3/3.3; AR-2/8/9.)

**Given** a zero-unit proposal or replacement-source absence claim,
**When** validation evaluates its evidence,
**Then** zero units require an explicit truthful whole-file disposition; omission or an incomplete response cannot masquerade as successful analysis,
**And** validated replacement-source evidence may withdraw obsolete current Code Units/observations while preserving history; exclusion, failed/unfinished analysis, model omission or access loss alone cannot prove removal. (FR-4.4/4.10; AR-8/19.)

**Given** an Analysis Checkpoint with accepted files and explicit pending/failed gaps,
**When** current Code Logic selection is requested,
**Then** Knowledge transactionally checks recorded ordered current-promotion intent, expected selected head, still-current target, cancellation and live initiating-identity/contributor authority,
**And** history-only or superseded results cannot replace newer intent; Git hashes and finish times do not determine recency,
**And** failed current files do not silently inherit old analysis, while other staged/history results remain explicitly distinguished. (FR-4.7/4.10, FR-12.3; AR-17/18; UX-DR35 data contract.)

**Given** replay, lost acknowledgement, cancellation, revocation or expired ownership,
**When** acceptance or selection is retried,
**Then** durable identity returns the existing committed result or rejects stale work without duplicate Documents/progress or unauthorized promotion,
**And** pre-commit failure exposes no partial accepted file; post-commit failure preserves accepted history and recoverable acknowledgement. (NFR-2.3/3.1/3.3; AR-8/18/23.)

**Given** isolated exact-source fixtures and test-only proposals,
**When** these contracts are qualified,
**Then** evidence covers multi-unit/nested identity, zero-unit validation, version lineage, partial checkpoints, reversed completion order and failure/replay boundaries,
**And** legacy file Documents retain their meaning and provenance rather than becoming newly analyzed semantic units. (FR-9.4; AR-16/32.)

**Scope and dependencies:** Depends on Stories 13.1–13.3 and 13.7; consumes their source, eligibility, request-intent and fencing contracts. Knowledge owns accepted semantics/selection; Scan Control retains operational state. Establish only required CL/manifest/observation/Uncertainty/selection records. No mandatory LLM execution, reader UI, full Scan completion, local relationship resolution, Logical Flow/BL schemas or later-epic prerequisite. Requirement credit is limited to this acceptance/selection contribution; test proposals are never production fixture fallback.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.9 grants no implementation or runtime authority.

### Story 13.10: Read selected Code Logic, exact history and authorized source

As an authorized IT reader,
I want to browse Code Logic and open its exact versions, sections and source,
So that I can inspect accepted explanations and their limitations without management rights, Chat or a graph.

**Acceptance Criteria:**

**Given** permitted selected CL accepted through Story 13.9,
**When** I open 「文档」 or a Repository's 「查看文档」 entry,
**Then** a minimal Repository/file/Code Unit hierarchy supports several Documents per file, title/keyword search, sections and bounded pagination,
**And** readable Markdown derives from canonical JSON without a second editable content store or synthetic directory Documents,
**And** Document access does not require System Owner or management permissions. (FR-1.4, FR-4.6/4.7, FR-12.6 reader clause; AR-29; UX-DR29/39/40.)

**Given** a Knowledge Read Context for authorized current or explicitly labelled staged/historical selection,
**When** discovery, later pages, existing-related-Document navigation or section/content reads occur while newer Knowledge becomes selected,
**Then** all operations retain that context's exact version/membership selection, including Documents not previously read,
**And** references bind the same exact Document Version/section; an explicitly fresh context may observe newer Knowledge,
**And** empty related results remain empty rather than inventing Edges. (FR-4.7, FR-9.4; AR-24/25 read-contract contribution; UX-DR23 contract/48.)

**Given** selected, staged, withdrawn or historical accepted results,
**When** I inspect a Document or 「版本历史」,
**Then** default reads use selected Knowledge and expose coverage gaps; other staged/history inspection is explicitly labelled,
**And** old references retain original content, provenance and any recorded dependencies instead of following today's selection,
**And** failed drafts never appear as Documents or substitute for failed current files. (FR-4.7/4.10, FR-9.4; AR-16–19/29; UX-DR35/40.)

**Given** a request, page cursor, exact reference or cached context,
**When** mode, scope or contributor authority disallows disclosure or is revoked,
**Then** the server denies or filters content, sections, labels, metadata, related membership and counts before disclosure,
**And** Business Mode receives no CL, code, file/range/Git metadata or source targets; this CL-only slice may truthfully return no permitted Documents,
**And** a stable context never freezes authorization or grants Scan/Agent tool authority. (NFR-2.3; AR-24/25/29; UX-DR41.)

**Given** an authorized IT Source Anchor,
**When** I open its recorded location,
**Then** Story 13.1 supplies only bounded exact Profiled Revision source with relative locations; neither host paths nor the current checkout substitutes for it,
**And** unavailable source shows 「该版本源代码暂不可用，文档及来源记录仍保留。」 while preserving permitted Document/history reading. (FR-4.9, FR-9.4; AR-3/29; UX-DR41.)

**Given** empty, loading, no-results, failed or partial reader states,
**When** I navigate, refresh or recover,
**Then** the Chinese Fluent/Library Oak reader uses existing LoadState and accessible keyboard/status patterns, truthful scoped messages, pagination and at most one dialog,
**And** isolated accepted fixtures verify these journeys, exact-reference stability and live revocation without a provider call. (NFR-5.2; UX-DR44–49.)

**Scope and dependencies:** Depends on Stories 13.1 and 13.9 plus preserved authorization/UI foundations. Knowledge owns the reusable read-context/storage/index/reference contract; later BL, graph and Chat consume it rather than create competing readers. Introduce only current reader needs, not future flow/BL/conversation schemas, embeddings, outward MCP, graph exploration or Scan-stage controls. This establishes the minimum CL reader/shared read contract, not complete later consumer requirements.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.10 grants no implementation or runtime authority.

### Story 13.11: Produce useful whole-file Code Logic through bounded real Worker analysis

As a System Owner,
I want an admitted file analyzed by a real bounded Worker Agent using the declared tools,
So that I can read meaningful source-grounded Code Logic rather than only validated synthetic proposals.

**Acceptance Criteria:**

**Given** an authorized File Workstream that fits the declared bounded whole-file execution envelope,
**When** Scan Control admits it after exact-source, profile, required native CodeGraph and applicable LSP readiness,
**Then** Microsoft Agent Framework executes the configured Worker conversation/tool loop with the selected provider and pinned Scan Context Snapshot/Advisory Overview,
**And** the application injects Stories 13.4–13.6's source/content/native MCP/language-neutral tools under Story 13.7's leases and shared admission; no pair or sample profile is required. (FR-4.3/4.9, FR-5.8, FR-15.1–15.3; AR-1/4/6/23.)

**Given** handwritten or declarative source and permitted supporting context,
**When** the Worker completes detailed file analysis,
**Then** it proposes meaningful Code Units and separate source-grounded CL content explaining behavior, conditions, effects and limitations, without requiring one model invocation per unit,
**And** other files provide attributable context rather than duplicate authorship; unfamiliar syntax and incomplete tool knowledge remain explicit,
**And** source-backed Communication Observations retain available evidence and unknowns without inventing deployed bindings or authoritative Edges,
**And** common-library imports alone never create communication gaps; familiarity does not prove unsourced behavior, and decisive unsupported behavior remains a limitation or labelled hypothesis. (FR-4.3, FR-5.5/5.8/5.14, FR-6.1, FR-8.4; AR-5/7/9.)

**Given** retrieved source, supplied advisory context and tool responses,
**When** they enter a model/tool interaction,
**Then** externalized prompts treat them as evidence rather than executable instructions, preserve advisory-versus-source provenance and restrict every invocation to its granted purpose/source/budget,
**And** Agent Execution keeps source/Documents inside organization infrastructure with self-hosted providers and sends external providers only necessary authorized task context; qualification checks actual provider inputs and boundaries without logging raw source or credentials,
**And** the Worker cannot choose credentials/workspaces, import/build/restore dependencies, schedule arbitrary work or write Knowledge; missing required CodeGraph capability follows explicit failure handling, not source-only fallback. (FR-7.3, FR-13.3; NFR-2.3/6.1; AR-6/15.)

**Given** a valid complete-file proposal,
**When** Story 13.9 accepts it and a permitted Analysis Checkpoint selects it,
**Then** Story 13.10 displays its actual immutable CL versions, exact sections/source and visible Uncertainties,
**And** truncation, unfinished tools, rejected provenance or a merely schema-valid partial response cannot accept the file,
**And** larger unfinished files remain explicitly pending without falsely completing the Repository Scan or silently excluding them. (FR-4.4/4.7, FR-12.3; AR-8/17; UX-DR35/39–41.)

**Given** provider failure, cancellation, limits or lost ownership,
**When** bounded execution cannot finish,
**Then** the existing controller classifies retry/pause/failure, fences late writes and settles owned work/permits while independent work and accepted reading remain available,
**And** no fallback provider, automatic paused-work restart or fresh budget is inferred. (NFR-1.1/3.1–3.3; AR-15/23.)

**Given** the declared two-service environment and representative bounded committed-source fixtures,
**When** real provider/MAF/tool execution is qualified through acceptance and reading,
**Then** evidence demonstrates useful multiple-unit analysis and faithful exact references, exercising the required native tools and applicable declared navigation,
**And** separately evaluate explanatory usefulness/unsupported claims versus schema/identity validity; record provider/configuration, timing, limits and available usage without universal SLA or historical-test substitution. (AR-32; UX-DR53 CL contribution.)

**Scope and dependencies:** Depends on Stories 13.1–13.10. This production-composed file-analysis capability uses Scan Control, not a new user-facing Pass 1 start action. No complete ordinary-Scan/BL outcome, Cross-Repository Resolution, automatic library Scan or large-file continuation is claimed. Generic observation capture prepares later resolution without preallocating schemas or requiring another Repository.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.11 grants no implementation or runtime authority.

### Story 13.12: Continue large-file analysis with durable whole-file progress

As a System Owner,
I want large admitted files to continue across bounded analysis windows and Worker runs,
So that context limits and interruptions do not truncate coverage or expose unfinished analysis as Knowledge.

**Acceptance Criteria:**

**Given** an admitted file exceeding its single-run context/window limit,
**When** Scan Control continues its File Workstream across bounded windows and completed MAF runs,
**Then** required source remains represented by authoritative pending/completed work, including constructs spanning windows,
**And** one whole-file semantic result may contain nested/shared-anchor Code Units; windows, repeated reads and individual runs do not create permanent identities or duplicate authorship,
**And** no fixed terminal-turn count silently ends required analysis. (FR-4.3/4.4; NFR-1.1; AR-7/8.)

**Given** a current attempt's validated draft/progress,
**When** a continuation checkpoint is saved,
**Then** Scan Control atomically persists both under its ownership/version fence with exact source/context/binding and sufficient continuation information to resume consistently,
**And** replay or lost acknowledgement resolves the same durable checkpoint without advancing progress twice,
**And** conversation history may assist recovery but cannot replace authoritative draft/progress or become Knowledge. (FR-4.4; NFR-3.3; AR-2/8.)

**Given** limits, provider outage or host interruption between windows or checkpoints,
**When** work pauses or becomes recoverable,
**Then** committed drafts/progress survive, uncommitted progress cannot become authoritative, and unfinished required source remains pending with a truthful cause,
**And** explicit resume uses Story 13.7's prerequisite/authority checks and Story 13.1's identical-source verification while retaining provider/context identity and accounted consumption,
**And** restart/provider recovery neither resumes paused work nor resets cumulative budgets. (NFR-1.1/3.2/3.3; AR-3/8/15; UX-DR33/34 data contract.)

**Given** cancellation, timeout, access revocation or expired file ownership,
**When** execution and tool activity settle,
**Then** the controller fences late checkpoints/acceptance, stops owned dispatch/activity and releases permits/unneeded disposable leases under existing runtime ownership,
**And** cleanup preserves durable accepted history, required source and independent consumers; failed partial drafts remain unavailable to readers or reconciliation,
**And** terminal file failure records a coverage gap rather than an accepted prefix. (NFR-2.3/3.1/3.3; AR-8/15/23.)

**Given** a validated whole-file proposal after several continuations,
**When** Story 13.9 accepts it,
**Then** its semantic units, canonical CL versions, observations and authoritative progress appear atomically through the existing acceptance contract,
**And** exactly one accepted result is recoverable after an acknowledgement failure; earlier windows never become standalone Documents,
**And** current selection still follows ordered intent, not the final window's finish time. (FR-4.3/4.4/4.7/4.10; AR-7/8/18.)

**Given** a fixture requiring real multiple-run continuation and controlled interruptions,
**When** the capability is qualified on the declared deployment,
**Then** evidence covers a boundary-spanning unit, checkpoint/restart/replay, explicit resume, stale-writer rejection, cleanup and reader fidelity,
**And** report file/window/unit/Document counts separately, account for limits, and assess whole-file usefulness independently of structural validity; acceptance idempotence promises no duplicate Knowledge, not zero repeated external billing. (NFR-1.1/3.3; AR-32; UX-DR32/35.)

**Scope and dependencies:** Depends on Stories 13.7, 13.9–13.11 and their source/tool foundations. Extend Scan Control's draft/progress contract and Knowledge acceptance with necessary continuation state only. No separate reader, scheduling service, token ledger, automatic source-update Scan, full-Scan completion claim or later-epic prerequisite. Technical planning sets bounded window/progress/default policies without changing semantics or coverage.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.12 grants no implementation or runtime authority.


### Story 13.13: Plan rooted local Logical Flows from accepted evidence

As an authorized Repository owner,
I want accepted local relationships organized into bounded source-backed Logical Flows,
So that business explanations can follow supported behavior without merging unrelated purposes or waiting for unscanned systems.

**Acceptance Criteria:**

**Given** an exact root Repository Scan Run with accepted file results and admitted primary work,
**When** Scan Control requests its static Flow planning checkpoint,
**Then** the checkpoint requires explicit terminal outcomes for every admitted primary item; paused or unprocessed work does not qualify,
**And** Knowledge pins exact Accepted File Manifests, CL versions, coverage gaps and a finite local matching target; each matching item receives a completed or explicit failure/Uncertainty disposition before synthesis eligibility.

**Given** source-backed reference proposals from accepted file analysis,
**When** Knowledge validates local relationship evidence,
**Then** accepted REFERENCES record exact endpoints, supported relationship meaning, inputs/rule/outcome and provenance, independently of unchanged CL text,
**And** drafts, shared names, lone candidates, confidence and missing tool results cannot alone prove identity; unscanned/external parties retain available identifiers and nonblocking Uncertainties without automatic Scans or Cross-Repository matching.

**Given** exported operations, handlers, schedules, declarative workflows or selected Code Units supported by accepted source,
**When** Flow Planner proposes roots and Knowledge accepts their identities,
**Then** each distinct business root owns a Logical Flow initially homed at its root Repository,
**And** two roots sharing helpers or a Call Cycle remain distinct; folders, connected components and transient tool identifiers neither create nor merge business purpose, and local-only behavior qualifies.

**Given** pinned roots, exact accepted relationships and configured traversal bounds,
**When** Flow Planner derives a Flow Group Version,
**Then** it records an immutable bounded directed evidence slice with exact member versions, branches, cycles, conditions supported by evidence and Uncertain Frontiers,
**And** traversal terminates on cycles/bounds, records the stopping reason, preserves alternatives and does not describe all branches as one executed runtime sequence; identical inputs yield the same canonical planning fingerprint.

**Given** accepted local evidence and proposed Flow Group Versions,
**When** persistence is replayed, ownership is lost, or an authorized reader inspects results,
**Then** existing fenced acceptance prevents duplicate or stale acceptance and the Story 13.10 read contract exposes permitted local references with exact versions and gaps,
**And** integration checks inspect accepted roots/membership through the owning contracts without an LLM, graph UI, BL draft publication or future-epic service; revoked evidence is not disclosed.

**Scope and traceability:**

- **Prior dependencies:** 13.7 and 13.9–13.12, including their source/identity/read contracts. Knowledge owns references, Uncertainties, Logical Flows and immutable membership; Flow Planner proposes; Scan Control owns checkpoint/job eligibility.
- **Independent value/boundary:** accepted local references are inspectable and local BL inputs are finite and reproducible. No Cross-Repository Resolver/investigation, remote-target waiting, business-root merging, graph presentation or synthesis execution.
- **Mapped requirements:** local FR-4.5/4.6/4.10, FR-5.5, FR-6.1/6.3/6.4 current clauses; NFR-2.3/3.1/3.3; AR-2/9–11/13/17/23; UX-DR35/39–41 data contributions only.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.13 grants no implementation or runtime authority.

### Story 13.14: Publish and read coherent local Business Logic versions

As an authorized Knowledge reader,
I want each local Business Logic version published with its exact evidence and preserved history,
So that I can read one coherent explanation without hidden partial updates or changed historical references.

**Acceptance Criteria:**

**Given** a pinned Flow Group Version and a validated BL proposal,
**When** Knowledge accepts its publication,
**Then** immutable canonical structured JSON, derived sections/search/Markdown, BL version, Flow Group Version, dependencies and selected publication become visible atomically,
**And** dependencies pin every actually used CL version through GENERATED_FROM, relationship/resolution evidence, Uncertain Frontier and direct-source Source Anchor; absent, incompatible or ungranted contributions cannot silently enter current Knowledge.

**Given** an ordered current-promotion request or explicitly history-only result,
**When** the publication transaction executes,
**Then** it checks the expected selected head, still-current desired flow target, initiating identity/approved scope, all contributor authority and cancellation/lease fences together,
**And** late superseded work cannot replace newer desired/current selections; history-only work cannot implicitly promote, and Git hashes or completion times never determine recency.

**Given** concurrent publication, a process failure or a lost commit acknowledgement,
**When** the operation is retried through the existing acceptance owner,
**Then** readers see either the prior coherent selection or the committed new selection and replay resolves from durable state without duplicate versions/dependencies,
**And** unrelated flows remain independently publishable; pending, stale, failed or incompatible refresh remains visible without a Repository-wide publication barrier.

**Given** a prior BL version and separately advancing relationship/Uncertainty selections,
**When** current or historical content is read,
**Then** historical BL retains its original exact dependencies and relationship selection; old GENERATED_FROM never retargets and unchanged caller CL need not be rewritten,
**And** every discovery, membership, page, section and exact-reference operation uses Story 13.10's stable Knowledge Read Context rather than a new BL-only cache or today's graph.

**Given** an authorized reader without management permissions,
**When** they browse local Logical Flows and open selected BL, exact sections or 「版本历史」,
**Then** the existing reader presents derived content with original versions, gaps and refresh state, grouped through System/Repository views without duplicate Logical Documents,
**And** Business Mode receives only permitted BL business content; IT may follow permitted CL and available recorded-revision source, with established Chinese unavailable-source/empty/loading states.

**Given** a contributor whose access is revoked, including one used in prose but omitted from displayed citations,
**When** publication, search, counts, metadata, content or history is requested,
**Then** live contributor authorization blocks prohibited disclosure without deleting immutable history,
**And** test-only proposals exercise the actual persistence/reader operations and concurrent failure boundaries; no production fixture BL or LLM call is required to prove this story.

**Scope and traceability:**

- **Prior dependencies:** 13.7, 13.9, 13.10 and 13.13. Extend their fenced acceptance, selection ordering and reader; introduce only needed BL/publication records.
- **Independent value/boundary:** coherent local BL publication and usable authorized history. No synthesis executor, graph, Chat, automatic source updates or general Cross-Repository matching.
- **Mapped requirements:** FR-4.5–4.7/4.10, FR-9.4, FR-11.1/11.2 reader portions, FR-12.6 reader portion; NFR-2.3/3.3/5.2; AR-2/12/13/17/18/25/29; UX-DR2–7/11/39–41/44–49.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.14 grants no implementation or runtime authority.

### Story 13.15: Synthesize useful local business explanations with bounded verification

As a permitted business reader,
I want useful local Business Logic synthesized from accepted source-backed evidence,
So that I understand supported conditions, alternatives, effects and source limitations.

**Acceptance Criteria:**

**Given** a synthesis-eligible pinned Flow Group Version and authorized configured provider assignment,
**When** Scan Control dispatches its Flow Synthesis Job,
**Then** real MAF execution uses an externalized prompt, accepted CL/relationship inputs and bounded snapshotted advisory context to propose structured BL,
**And** the existing shared admission, job lease, deadline/call/cost limits and recorded provider identity apply; Workers neither create jobs nor write accepted Knowledge.
**And** shared Agent Execution policy keeps source/Documents inside organization infrastructure with self-hosted providers; external providers receive only necessary authorized context.

**Given** a precise detail needing verification within the supplied evidence set,
**When** the Worker uses source, Source Content Analysis, native CodeGraph MCP or applicable language-neutral LSP,
**Then** Stories 13.4–13.6 enforce granted snapshot handles, Scan-purpose authority, cumulative budgets, exact locations and capability limitations,
**And** tools cannot select arbitrary paths/snapshots/adapters, restore dependencies or widen Repository access; at most one LSP session per snapshot remains enforced and required CodeGraph failure has no automatic source-only fallback.

**Given** supplementary authorized CL or direct source actually used to explain behavior,
**When** the final proposal reaches Story 13.14 acceptance,
**Then** every additional contribution and exact Source Anchor enters validated dependencies, including details absent from CL,
**And** changed authorization, invalid provenance, incompatible inputs or a superseded desired target prevent inappropriate current publication; citation lists alone do not define contributor scope.

**Given** unsupported bindings, conflicting source/CL, missing external facts or incomplete tool evidence,
**When** synthesis describes the flow,
**Then** it preserves explicit Uncertainties, bounds and lower-authority advisory claims without inventing remote parties or deployed execution,
**And** discrepancies may produce bounded proposals to the owning analysis/resolution contract, never accepted CL/Edge rewrites, recursive correction scheduling, automatic library Scans or external-information tools.

**Given** representative local roots with shared helpers, alternatives, a cycle and uncertain frontiers,
**When** real provider results are published and reviewed through the existing reader,
**Then** explanations identify the business purpose, conditions, branches, data/state effects and unknowns in usable Simplified Chinese,
**And** independent content review records usefulness and claim-to-source faithfulness separately from schema/reference validity, with exact examples and declared provider/configuration/timing/available usage; local success supplies no Cross-Repository qualification.

**Given** temporary provider failure, invalid configuration, timeout, cancellation or lost ownership during synthesis,
**When** recovery and acceptance are exercised,
**Then** the Story 13.7 policy bounds increasing retries, durably pauses where required, releases owned capacity and requires prerequisite-checked explicit resume without provider/source substitution,
**And** late proposals cannot publish, prior accepted history survives and other ready flows continue; restart/replay does not claim zero repeated external billing.

**Scope and traceability:**

- **Prior dependencies:** 13.4–13.8, 13.10, 13.13 and 13.14. Reuse established owners.
- **Independent value/boundary:** real useful local BL is readable end-to-end from accepted CL. No general Cross-Repository reconciliation, Chat, graph or automatic repair/update pipeline.
- **Mapped requirements:** FR-4.5/4.7/4.9, FR-5.1/5.8, FR-6.1, FR-7.3, FR-13.3 Scan portion, FR-14.4, FR-15.1–15.3; NFR-1.1/2.3/3.1–3.3/5.1/6.1; AR-6/12/13/15/18/21–23/32; UX-DR2/40/41/46.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.15 grants no implementation or runtime authority.

### Story 13.16: Replace current local Knowledge without erasing history

As an authorized Repository owner and Knowledge reader,
I want explicit static replacement Scans to select newer evidence and retire proven-absent behavior safely,
So that current explanations do not preserve obsolete behavior or erase history because analysis was incomplete.

**Acceptance Criteria:**

**Given** a Repository with accepted local Knowledge,
**When** full static replacement analysis exercises existing callable source/analysis/publication contracts with recorded owner current-promotion or history-only intent,
**Then** it fixes exact source and uses ordered selection/publication without mutating prior runs,
**And** history-only analysis never implicitly promotes, late older work cannot displace newer intent and no remote polling, automatic pull, incremental impact/reuse or background update trigger is added.

**Given** validated selected replacement-source evidence proving that a prior file, Code Unit, observation or flow root is absent,
**When** Knowledge evaluates withdrawal,
**Then** inventory-confirmed deletion or validated semantic absence may withdraw obsolete current eligibility and retire absent roots' current flow selections,
**And** the evidence, prior identity, affected selections and reason remain recorded; unproven rename/split/merge mappings cannot silently reuse identity.

**Given** a model omission, policy exclusion, failed/unfinished replacement file or unresolved identity correspondence,
**When** current replacement results are selected,
**Then** absence remains a coverage/revalidation gap rather than proof of deletion,
**And** failed current files cannot silently inherit earlier CL as newly analyzed; authorized prior versions remain labelled history, while current coverage and stale/pending/failed Knowledge are explicitly distinguished.

**Given** validated withdrawal or changed accepted local dependencies,
**When** affected local Logical Flows are reconsidered,
**Then** a finite target reopens affected local references as needed, retires missing roots and replans surviving rooted slices through Story 13.13,
**And** unstarted refresh demands coalesce to the latest desired target while active inputs remain fixed; Story 13.14 prevents outdated promotion and publication itself initiates no recursive planning/synthesis loop.

**Given** System membership, selected discovery scope or contributor authorization changes without evidence of source removal,
**When** visibility and replacement eligibility are evaluated,
**Then** restricted disclosure stops immediately and any unevaluated scope is recorded without treating access loss or scope narrowing as relationship disproof,
**And** immutable content/provenance is preserved; publication still rechecks the initiating identity and every actual contributor rather than silently adopting service-principal authority.

**Given** two explicit revisions, an out-of-order completion, a history-only run and both proven-deletion and failed-file cases,
**When** the owner/reader inspects current and historical results,
**Then** expected selections and retirement/gap labels are demonstrable through the existing reader, including original GENERATED_FROM, relationship evidence and exact Source Anchors,
**And** retained old contributors remain source-pinned where required; unavailable historical source is explicit, and pair-era file Documents retain historical meaning rather than being relabelled generic semantic analysis.

**Scope and traceability:**

- **Prior dependencies:** 13.1, 13.3, 13.7 and 13.9–13.15. Use existing callable owners; no public Scan-start route or later-story prerequisite.
- **Independent value/boundary:** full static replacement yields honest current/history selections and retirement through component contracts. No incremental scanning, generic Cross-Repository refresh, new source origins, automatic historical-source pruning or editable Documents.
- **Mapped requirements:** FR-2.3 preservation, FR-4.7/4.10, FR-6.6 local currentness contribution, FR-9.4; NFR-2.3/3.3; AR-3/7/13/16–19/31; UX-DR35/39–42/46.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.16 grants no implementation or runtime authority.

### Story 13.17: Complete one real Repository Scan through the ordinary product route

As an authorized Repository owner,
I want one Scan action to produce readable local Knowledge with truthful status and explicit recovery,
So that I can complete the ordinary workflow without manually starting internal stages.

**Acceptance Criteria:**

**Given** an authorized ordinary or standalone source-available Repository,
**When** its owner selects 「开始扫描」,
**Then** one durable generic request composes exact acquisition, inventory/overview, required tools, policy planning, progressive Pass 1, accepted local evidence/Flow planning, Pass 2 and publication through established owners,
**And** the owner needs no separate stage-start calls, fixed sample name, catalog recipe, paired Repository, PairExecutionId or parent System for standalone work.

**Given** queued, active, paused, cancelling, cancelled or terminal work,
**When** the owner opens or refreshes Repository details,
**Then** minimal Chinese status reads actual durable state and provides permitted controls plus separate coverage/gap and Knowledge Currentness summaries,
**And** queued work does not imply Worker execution and terminal status never asserts complete primary coverage or current BL; this usable route requires neither a future dashboard nor fabricated progress.

**Given** a bounded provider outage, configuration failure, host restart or requested cancellation,
**When** the owner uses the existing controls,
**Then** Repository details expose sanitized reasons, truthful 「正在取消」 cleanup and explicit 「恢复扫描」 with source/prerequisite/live-authority checks,
**And** restart/provider recovery cannot auto-resume paused work or switch provider/source; owned downstream work is fenced and cleaned while accepted history and independent consumers survive.

**Given** accepted generic or historical Knowledge and temporarily unavailable analysis capabilities,
**When** a permitted User opens 「查看文档」, exact sections or history,
**Then** Story 13.10/13.14 readers remain usable without management rights and preserve mode/contributor authorization, currentness, gaps and recorded-revision source behavior,
**And** the user can inspect selected and historical CL/BL now, without waiting for detailed Scan-history UI, Chat or graph exploration.

**Given** generic production routing and retained historical artifacts,
**When** a request is dispatched or its capability is unavailable,
**Then** it has one explicit versioned workflow owner, with no silent legacy-pair execution; unimplemented Cross-Repository capability remains visibly unavailable and demos remain separate,
**And** historical file Documents retain their original meaning and required data/source/key persistence remains active; existing readiness/diagnostics stay sanitized and routine checks incur no inference.

**Given** the two-service runtime and representative ordinary plus standalone/library-source Repositories,
**When** source-to-local-wiki journeys exercise bounded and progressive files, recovery and history reads,
**Then** configured native tools/providers and persistence produce useful CL/BL with exact references and truthful gaps before cutover,
**And** record configuration, timing, available usage and independent usefulness/faithfulness review separately from tests/history; claim no quota, universal throughput or Cross-Repository completion.

**Scope and traceability:**

- **Prior dependencies:** 13.1–13.16 plus retained authorized administration. Integration and minimal Repository-detail/routing adapters only; missing core behavior returns to its earlier owner.
- **Independent value/boundary:** one usable local Scan with status/recovery and existing reader/history. Detailed dashboards/Scan-history presentation, System/batch initiation, graph, Chat, generic Cross-Repository matching and operator restore are excluded.
- **Mapped requirements:** FR-2.2/2.4/2.5 integration, FR-4.2/4.9, FR-8.1, FR-12.1/12.3/12.4 minimal status portions; NFR-1.1/2.2/2.3/3.1–3.3/4.1 runtime portion/5.1/5.2; AR-1/14–16/29/30/32; UX-DR1–13/29/33–35/45–49/53 Scan-reader portion/57.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.17 grants no implementation or runtime authority.

### Story 13.18: Follow real Scan progress, controls and history in the dashboard

As an authorized Repository owner,
I want a live dashboard and navigable Scan history showing real work, coverage and Knowledge Currentness,
So that I can understand outcomes and recover affected work without confusing operational completion with complete Knowledge.

**Acceptance Criteria:**

**Given** active, paused and terminal Repository Scan Runs,
**When** the owner opens 「仪表盘」 from Repository details or the Scan-history record list,
**Then** inline/detail arrangement shows observed operations, active Worker/error counts and admitted/accepted/pending/failed primary coverage separately from Knowledge publication/refresh state,
**And** windows, files, Code Units and Documents retain distinct counts, percentages declare genuine denominators, and overlapping work is not depicted as one mandatory sequential progress bar.

**Given** an authorized dashboard subscription, connection loss or publication while attaching,
**When** SignalR updates arrive or the browser reconnects,
**Then** permitted live updates reconcile with durable state without duplicate totals, lost final outcomes or elapsed-time simulation,
**And** live access filters every run, contributor-dependent count, label and detail; refresh/reconnection neither dispatches another Scan nor grants source/management authority, and unavailable analysis does not disable existing Knowledge reads.

**Given** queued, running, paused, cancelling, cancelled or failed work,
**When** the owner uses dashboard pause/resume/cancel controls,
**Then** the existing Scan Control contract supplies sanitized causes, bounded-retry/pause state, explicit prerequisite-checked 「恢复扫描」 and 「正在取消」 until cleanup completes,
**And** restart/provider recovery cannot auto-resume or change provider/source; cancellation respects request-owned downstream work, accepted history and independent consumers rather than adding UI-owned lifecycle rules.

**Given** current, history-only, superseded, gap-bearing or retired outcomes,
**When** the owner selects a recorded Scan and follows its accepted results,
**Then** details distinguish operational outcome, primary coverage and published/pending/stale/failed/retired Knowledge and link to the existing exact-version reader/history,
**And** old references retain original dependencies, historical artifacts gain no generic qualification, restricted metadata is absent and reader access itself never requires management permission.

**Given** real stage/controller/system events across the completed route,
**When** the dashboard and operational evidence are checked,
**Then** the Story 13.7 logging owner receives the wired events and appends sanitized `log.md` entries using `## [YYYY-MM-DD HH:MM] operation | target`, supplemented by correlated states/counts/durations/failures/available usage,
**And** secrets, raw source and hidden reasoning are absent, unknown usage is not zero and routine readiness makes no inference calls; structured logging does not replace the required file format.

**Given** real bundled-runtime Scan, pause/resume/cancel, failure, reconnect and history scenarios,
**When** presentation is qualified,
**Then** Simplified Chinese Fluent/Library Oak views preserve tree/detail/history navigation, LoadState, loading/refresh/empty/error behavior, keyboard interaction and meaningful accessible announcements,
**And** evidence records responsiveness and status/count correctness without fictional fixtures, a universal latency guarantee, advanced graph animation or a new accessibility certification claim.

**Scope and traceability:**

- **Prior dependencies:** 13.7 and 13.17, consuming established source, Knowledge, reader and control owners. Presentation/event wiring and its qualification only; no new scheduler, lifecycle, logging sink or recovery engine.
- **Independent value/boundary:** live operational inspection, recovery controls and Scan-history navigation for an already usable single-Repository route. No Campaign initiation, Chat, graph or operator restore.
- **Mapped requirements:** FR-4.8, FR-9.4 navigation, FR-12.1–12.4; NFR-1.1/2.3/3.1–3.3/5.2; AR-14–16/29/32; UX-DR1–13/28/29/31–35/42–49/53 operational portion/57.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.18 grants no implementation or runtime authority.

### Story 13.19: Run and control several independent Repository Scans fairly

As an authorized System Owner,
I want System and selected-Repository actions to run independent Scans under shared bounded capacity,
So that useful results progress fairly even when another Repository is delayed, paused or cancelled.

**Acceptance Criteria:**

**Given** authorized Repositories across Systems and standalone Repositories,
**When** the owner starts a System or selected-Repository Scan action,
**Then** each selected Repository obtains an independent Repository Scan Run with its Source Snapshot, intent, coverage, recovery and local Knowledge lifecycle,
**And** optional Scan Campaign identity owns batch controls/aggregation only; duplicate submission handling does not create unintended runs, and membership neither proves communication nor creates a correctness barrier.

**Given** more ready Repository/file/Flow Synthesis work than configured capacity,
**When** Story 13.7's shared scheduler admits work,
**Then** global Worker, per-run, shared provider and Tool Runtime limits remain bounded without multiplying by Repository count; eligible work eventually receives capacity under the declared fair policy,
**And** one valid run/snapshot/file lease owner performs each work item, unsafe shared tool operations serialize, and Workers do not allocate Workers or occupy capacity waiting for unscanned targets.

**Given** concurrently progressing independent runs,
**When** one suffers a file failure, tool prerequisite failure, provider pause or cancellation,
**Then** unrelated eligible work can continue and publish its local Knowledge without waiting for every Campaign member,
**And** affected dispatch/work/permits are stopped and cleaned, late writers are fenced, and shared evidence/history or independent consumers are not cancelled merely because the originating request stops.

**Given** a Campaign and individually controllable runs,
**When** authorized pause/resume/cancel is requested for one run or an explicit batch scope,
**Then** existing controls act only on the addressed owned work under live authorization, including request-owned pending downstream jobs,
**And** each resumed run independently rechecks source/provider/tools/authority; successful members are not restarted and new/out-of-scope Repositories are not silently added to an earlier action.

**Given** mixed queued, executing, paused, failed and published results,
**When** System details, Repository grids and Campaign/dashboard views aggregate progress,
**Then** permitted run/Worker counts, admitted/accepted/pending/failed coverage and published/pending/stale/failed/retired Knowledge remain separate and reconcile with per-run durable state,
**And** live access filters every count/title/detail and contributor-dependent Document total; updates/reconnects create neither fictional progress nor duplicate totals, preserving Chinese states, LoadState and accessible feedback.

**Given** the bundled two-service runtime with at least three concurrent Repositories spanning multiple Systems and a standalone case,
**When** qualification applies bounded capacity, delays one run and cancels or pauses another,
**Then** another independently completes usable local CL/BL and history reads; admission/cleanup and ownership evidence verifies fairness and bounded totals without pair-specific contracts,
**And** check shared-provider competition through the existing admission contract without requiring Chat; report configuration/timing/available usage and local-only evidence, not Cross-Repository relationship qualification.

**Scope and traceability:**

- **Prior dependencies:** 13.7, 13.17 and 13.18, consuming established owners. No new scheduler, admission policy, reader or future-epic dependency.
- **Independent value/boundary:** multi-Repository initiation, control and aggregate progress with real beyond-pair independence. No global semantic publication, general Cross-Repository Resolution, distributed scheduler or automatic Scan expansion.
- **Mapped requirements:** FR-2.4/4.2/8.1, FR-12.1–12.4; NFR-1.1/2.3/3.1–3.3/4.1 limits portion/5.2; AR-1/14/15/18/23/30/32; UX-DR28/30–35/42/45–49.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 13.19 grants no implementation or runtime authority.

**Epic 13 definition acceptance:** All 19 story boundaries are accepted: 13.1–13.3 explicitly by the user; 13.4–13.19 under delegated confidence after independent coverage/dependency review. Core source-to-local-Knowledge, readers, Docker, safety, recovery and real qualification are covered without later-epic prerequisites. This completes definition of the epic, not its implementation. Proceed to Epic 14 under the same bounded delegation.


## Epic 14: Cross-Repository Business Understanding

Users can follow source-supported business behavior across independently scanned Repositories/Systems, understand missing or ambiguous links, and see explanations improve when later accepted knowledge arrives without rescanning unchanged source. Epic 13 supplies the established source, control, Knowledge and reader owners; this epic requires no later Chat, graph or operations epic.


### Story 14.1: Preserve generic source-backed communication observations

As an authorized IT reader,
I want accepted analysis to describe communication mechanisms, parties and unresolved bindings consistently,
So that I can distinguish source-supported interactions from missing information before any Cross-Repository match is accepted.

**Acceptance Criteria:**

**Given** admitted exact source containing RPC/HTTP, queue, file/storage or page-navigation behavior,
**When** the existing Pass 1 Worker analyzes complete File Workstreams,
**Then** proposals retain owner, provider/consumer role, mechanism, operation, logical namespace, source literals/expressions, evidence status and exact Source Anchors,
**And** representative cases cover each listed family without a fixed demo profile, mandatory XML workflow, universal protocol claim or guaranteed gRPC-specific implementation.

**Given** indirect expressions, unknown mechanisms or unscanned/unavailable counterparties,
**When** observations are accepted through Story 13.9,
**Then** available identifiers and meaningful known/unknown-target distinctions survive with explicit Uncertainties, including named RPC endpoints,
**And** missing database/gateway/registry facts remain nonblocking gaps; Workers invent neither deployed values nor relationships and initiate no automatic target/library Scan.

**Given** optional catalog/framework context in Story 13.4's documented supported format,
**When** an owner supplies a new supported pattern and a later Scan snapshots that context,
**Then** interpretation can use it without application-code changes within that format, preserving context provenance and lower authority than source,
**And** edits do not mutate active inputs; new syntax may require implementation, and dynamic parsers/plugins, executable configuration, external fetching or maintained service mappings are not promised.

**Given** shared file paths/topics, navigation links or familiar library imports,
**When** observations describe their meaning,
**Then** file/storage roles and locations, URL patterns/deep links and loose navigation coupling remain explicit without claiming executed routing,
**And** shared names/paths and confidence do not prove counterpart identity; ordinary library use alone creates no communication gap, while unsupported behavior remains a limitation.

**Given** valid proposals, failed drafts and restricted source contributions,
**When** acceptance and existing wiki discovery/section reads are exercised,
**Then** only accepted exact-version observations are visible under live mode/contributor authorization; IT can find permitted endpoint evidence, while Business receives no technical CL/source metadata,
**And** real bounded Worker examples independently verify observation/source faithfulness, unknown-versus-malformed handling and truthful gaps, not merely JSON validity; shared Agent Execution preserves organization boundaries and necessary authorized external-provider context.

**Scope and traceability:**

- **Prior dependencies:** 13.4–13.12 and 13.17. Extend existing content, Worker, acceptance and reader owners; no Cross-Repository matcher or new tool type.
- **Independent value/boundary:** useful accepted communication evidence is discoverable before resolution. Preserve historical human wontfix decisions; no new HITL actions, external operational lookup or dedicated unknown-library inference.
- **Mapped requirements:** FR-5.1–5.3/5.5–5.8/5.10–5.12/5.14, FR-6.1/6.4 current clauses, FR-7.3/8.4/14.4; NFR-2.3/3.1/6.1/6.2; AR-5/6/8/9/22; UX-DR2/35/39–41/46.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 14.1 grants no implementation or runtime authority.

### Story 14.2: Pin finite authorized evidence targets across Repositories

As an authorized Scan owner,
I want reconciliation to use a finite reproducible selection of accepted Repository evidence,
So that available knowledge can contribute without mixing arbitrary history or waiting for an entire portfolio.

**Acceptance Criteria:**

**Given** a root Repository Scan Run and its initiating identity/approved discovery scope,
**When** Knowledge constructs a reconciliation target,
**Then** it fixes the root binding and one designated analysis candidate per other authorized Repository, pinning exact Accepted File Manifest, Document and observation versions with the supplied selection policy,
**And** scope may include other Systems and standalone Repositories only when authorized; organizational membership grants no additional access or compulsory same-System restriction.

**Given** accepted files in an external Repository whose run is unfinished,
**When** their designated candidate is selected,
**Then** those committed files may enter the target before the external run's checkpoint/completion,
**And** partial drafts, failed-file drafts and unrelated historical candidates cannot enter; absent files remain explicit gaps rather than silently inherited old analysis or a requirement that every Repository finish.

**Given** a frozen target and later accepted evidence,
**When** work reads the target or records its matching-item dispositions,
**Then** its finite membership and exact versions remain unchanged and every selected item can retain an explicit completed, failure or Uncertainty outcome,
**And** later evidence requires a distinct target rather than changing active inputs; root-run BL eligibility still requires terminal admitted primary work and terminal matching-target outcomes, not merely successful target construction.

**Given** a narrowed scope or revoked contributor access after broader evidence was selected,
**When** selection, disclosure or application is revalidated,
**Then** restricted data is withheld immediately and unavailable scope is recorded as not evaluated where appropriate,
**And** scope narrowing/access loss alone cannot disprove or replace broader accepted relationship truth; current authority is checked without deleting original provenance or inventing service-principal permission.

**Given** multiple candidate histories, concurrent acceptance and interrupted/replayed target creation,
**When** isolated integration checks use the existing callable Knowledge/control contracts,
**Then** deterministic canonical identity and durable fences reproduce the selected target without duplication, mutable membership or cross-scope leakage,
**And** default readers remain on selected CL/published-flow dependencies while target/staged/history inspection is explicitly labelled; no accepted target is presented as a resolved relationship or completed Scan.

**Scope and traceability:**

- **Prior dependencies:** 14.1 plus 13.7, 13.9/13.10 and 13.13/13.14. Knowledge owns evidence sets; Scan Control owns operational target work. Only target selection/inspection records needed here are added.
- **Independent value/boundary:** a bounded reproducible evidence set is callable and inspectable, including accepted unfinished-run contributions. No matching algorithm, inquiry execution, automatic source acquisition, future-epic reader or global correctness barrier.
- **Mapped requirements:** FR-4.4/4.5, FR-6.2/6.3/6.6 scope clauses, FR-2.1/2.3 preservation; NFR-2.3/3.1/3.3; AR-1/2/10/17/18/23; UX-DR35/40/41 data/inspection portions.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 14.2 grants no implementation or runtime authority.

### Story 14.3: Accept evidence-backed one-to-many relationships and unresolved outcomes

As an authorized Knowledge reader,
I want Cross-Repository relationships and unresolved outcomes selected from explicit evidence,
So that I can follow supported references without guessing counterpart identity or changing historical explanations.

**Acceptance Criteria:**

**Given** Story 14.2's finite pinned caller/candidate evidence,
**When** Resolver applies declared deterministic matching rules,
**Then** each decision records exact input versions, applicable mechanism/operation/role/namespace evidence, rule identity and outcome for Knowledge validation,
**And** a lone visible candidate, shared name/path/topic, organizational membership or model confidence alone cannot establish identity; incompatible or insufficient bindings remain explicit Uncertainties.

**Given** source-supported evidence for several consumers of one provider/interface,
**When** relationships are accepted,
**Then** each justified pair becomes an exact-version REFERENCES Edge with its supported meaning, without a single-consumer or exactly-two-Repository restriction,
**And** counts include only supported currently permitted relationships and do not imply a complete deployment inventory; source linkage and possible navigation remain distinct from executed routing.
**And** multiple evidence-supported counterparties remain valid; candidate cardinality alone determines neither matching nor ambiguity.

**Given** unknown parties, ambiguous alternatives or missing operational facts,
**When** matching settles the frozen target,
**Then** every item receives a supported match or explicit unresolved/ambiguous/failure disposition, retaining known identifiers and meaningful known/unknown-target distinctions,
**And** Dangling Links do not block otherwise valid analysis or hold Worker capacity awaiting a target; no automatic Scan, mandatory owner question or confidence-only fallback occurs.

**Given** accepted relationship/Uncertainty changes and an existing CL body/publication,
**When** Knowledge commits their selection through the fenced acceptance owner,
**Then** relationship evidence has independent publication identity without rewriting unchanged caller CL; expected selection, target, initiating authority, contributor and cancellation checks apply atomically,
**And** replay or late superseded results cannot duplicate/regress selections, while historical REFERENCES and old BL GENERATED_FROM retain original versions rather than following today's relationship selection.
**And** existing BL remains readable at original dependencies; affected dependency changes immediately mark it stale/pending through existing currentness, without refresh execution.

**Given** current/history reads, live revocation or narrowed discovery scope,
**When** users inspect related Documents, unresolved explanations or scoped counts through the existing read contract,
**Then** stable Knowledge Read Context and live contributor/mode authorization govern all content, metadata and membership, without management-only access or a separate tool per record,
**And** narrowed scope/access loss does not disprove broader relationships; matched, ambiguous, one-to-many and contradictory-namespace cases independently demonstrate decision correctness and visibility without an LLM call.

**Scope and traceability:**

- **Prior dependencies:** 14.1/14.2 plus 13.7, 13.9/13.10 and 13.13/13.14. Extend existing Knowledge relationship/publication/read ownership; Resolver proposes only.
- **Independent value/boundary:** readers follow Cross-Repository references and unresolved evidence before Cross-Repository BL. Deterministic matching settles without later inquiry. No focused Worker, graph, Chat, human dismissal workflow or external-information adapter.
- **Mapped requirements:** FR-4.4/4.7/4.10, FR-5.3–5.6/5.10–5.12, FR-6.1–6.5 evidence/count clauses; NFR-2.3/3.1/3.3; AR-9/13/17/18/22/25; UX-DR11/35/39–41/46.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 14.3 grants no implementation or runtime authority.

### Story 14.4: Investigate ambiguous targets through bounded source-backed inquiry

As an authorized Scan owner,
I want ambiguous indirect targets investigated when useful evidence is available,
So that supported relationships can improve without treating model confidence or unavailable tools as proof.

**Acceptance Criteria:**

**Given** an ambiguous matching question with useful available evidence,
**When** Scan Control dispatches a Focused Resolution Investigation,
**Then** Knowledge pins the finite caller/candidate set, exact snapshots/accepted Documents, original question and approved access/policy inputs,
**And** real MAF execution uses an externalized prompt and existing bounded admission; uninvestigable deployment/Repository facts remain explicit nonblocking Uncertainties instead of speculative inquiry loops.

**Given** a dispatched inquiry,
**When** its Worker inspects CL/source/configuration through existing Source Content Analysis, native CodeGraph MCP and applicable LSP,
**Then** Stories 13.4–13.6 enforce granted handles, Scan-purpose scope, cumulative budgets, locations and limitations, with at most one LSP session per snapshot,
**And** no arbitrary paths/snapshots/adapters, dependency restoration, external-information tools or Repository Scan initiation is permitted; required CodeGraph failure has no automatic source-only fallback.

**Given** supporting/opposing findings and candidate conclusions,
**When** inquiry completion reaches the shared fenced acceptance owner,
**Then** accepted findings, exact evidence/conditions/limitations and terminal Scan Control job progress commit atomically after bound-target, live initiating/contributor-authority and job-fence checks,
**And** Workers neither write Edges nor overwrite CL; Story 14.3 applies relationships through its separate idempotent owning contract, and unresolved expressions, incomplete indexes/classpaths or unavailable tools cannot become negative proof or fabricated endpoint values.

**Given** a completed inquiry or repeated delivery of the same question,
**When** eligibility is evaluated again,
**Then** deduplication uses the original question/candidate/evidence/policy fingerprint, excluding inquiry outputs and receipts,
**And** unchanged inputs do not redispatch completed inquiry; independently changed inputs or an explicit authorized operator request may justify bounded reinvestigation, but publication never recursively triggers it.
**And** pre-commit failure exposes neither accepted findings nor completed job; post-commit/lost-acknowledgement replay returns durable findings and progress without repeating the model call, verified at both completion boundaries.

**Given** provider outage, configuration failure, cancellation, deadline or ownership loss,
**When** the existing controller settles inquiry work,
**Then** classified increasing bounded retries/pause, explicit prerequisite-checked resume and owned capacity cleanup apply without provider/source substitution,
**And** late application is fenced and unrelated work continues; shared Agent Execution keeps self-hosted source/Documents inside organization infrastructure and external-provider context necessary and authorized.

**Given** representative indirect-target ambiguity with both clarifying evidence and unavailable evidence,
**When** actual bounded MAF/tool execution is qualified,
**Then** review separates useful source-faithful findings, domain matching decisions and unchanged Uncertainties from schema validity,
**And** original-input replay, revocation and failure checks produce sanitized timing/usage evidence without claiming zero repeated external billing or restoring historical peer-verification/HITL rules.

**Scope and traceability:**

- **Prior dependencies:** 14.1–14.3 plus 13.4–13.7 and 13.11. Add inquiry jobs/evidence under existing owners, without new tools or scheduler.
- **Independent value/boundary:** a real focused inquiry can improve an existing matching outcome or explain why it cannot. No arbitrary discovery, automatic correction or new service-principal policy.
- **Mapped requirements:** FR-4.2/4.9, FR-5.7/5.8, FR-6.1/6.2, FR-13.1 native/13.3 Scan portions, FR-15.1–15.3; NFR-1.1/2.3/3.1–3.3/6.1; AR-2/6/9/15/17/18/20/22/23; UX-DR33–35/40/46.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 14.4 grants no implementation or runtime authority.

### Story 14.5: Reconcile later evidence with finite cycles and coalesced refresh

As an authorized Knowledge reader,
I want accepted later evidence to improve relationships without rescanning unchanged source,
So that missing targets can become understandable while refresh remains bounded and honest.

**Acceptance Criteria:**

**Given** existing Dangling Links and independently accepted new Repository evidence,
**When** reconciliation is requested under the recorded initiating authority/approved scope,
**Then** Scan Control creates a finite Resolution Epoch using Story 14.2's pinned selection, including eligible accepted files of unfinished external runs,
**And** it neither re-analyzes unchanged caller source nor starts automatic source acquisition, whole-Repository expansion or incremental update scanning.

**Given** late/missing targets and directed Call Cycles in that target,
**When** matching and eligible bounded inquiry are completed,
**Then** traversal records cycles and terminates within configured bounds, with each matching item completed or explicitly failed/uncertain,
**And** unknown targets hold no Worker capacity indefinitely; BL eligibility additionally requires terminal outcomes for every admitted primary item of the root run, so paused root work never qualifies.

**Given** validated changed relationship/Uncertainty evidence,
**When** Knowledge accepts a Resolution Change Set,
**Then** unchanged caller CL and original source remain intact while affected Logical Flows are deduplicated from relevant content/evidence versions and membership,
**And** unrelated flows do not refresh; publication of Documents or relationship selections starts neither another Resolution Epoch nor recursive inquiry.

**Given** several newer checkpoints affecting one flow while another target is active,
**When** desired refresh work is recorded,
**Then** unstarted demands coalesce to one latest desired target and active inputs stay fixed,
**And** existing expected-head/desired-target/cancellation checks prevent outdated current promotion; pending/stale/failed refresh is visible, not a global publication barrier or silently rewritten active job.

**Given** validated replacement evidence, failed source analysis or narrowed access/scope,
**When** references and affected roots are reconsidered,
**Then** Story 13.16's evidence-based withdrawal may reopen obsolete references or retire absent roots while preserving exact history,
**And** model omission/exclusion/failure cannot prove deletion, and narrower scope/access loss alone records visibility/not-evaluated changes rather than disproving broader accepted relationships.

**Given** duplicate events, lost acknowledgement, restart or cancellation during reconciliation,
**When** the existing controller and acceptance contracts recover,
**Then** durable targets/outcomes/demands remain idempotent, old ownership is fenced and paused work requires explicit prerequisite-checked resume with owned cleanup,
**And** callable-contract tests demonstrate late relationship improvement, cycle termination and coalesced demands through existing readers/status without requiring Cross-Repository BL execution or a future UI.

**Scope and traceability:**

- **Prior dependencies:** 14.1–14.4 plus 13.7, 13.10 and 13.13–13.16. Reuse established relationship, flow-target, publication and recovery owners.
- **Independent value/boundary:** later evidence improves readable relationship selections and creates finite durable refresh demand. No source rescans, new controller, recursive regeneration flood, Chat or graph.
- **Mapped requirements:** FR-4.2/4.5/4.10, FR-5.5, FR-6.2–6.6, FR-12.1–12.4 state portions; NFR-1.1/2.3/3.1–3.3; AR-10/14/15/17–20/23; UX-DR31–35/40/42/46.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 14.5 grants no implementation or runtime authority.

### Story 14.6: Publish useful Cross-Repository business explanations with exact history

As an authorized business or IT reader,
I want coherent Business Logic that follows supported behavior across Repositories,
So that I understand the flow's conditions, effects and limits through usable exact references.

**Acceptance Criteria:**

**Given** accepted Cross-Repository relationships and a source-backed root,
**When** Flow Planner extends its bounded directed slice,
**Then** one Logical Flow retains its root-Repository home and Logical Document across System/Repository browsing appearances, with exact members, branches, cycles and Uncertain Frontiers,
**And** shared helpers/folders/cycles never merge distinct business roots or imply a business dependency merely from shared code.

**Given** a proposed Cross-Repository Flow Group Version,
**When** synthesis eligibility is evaluated,
**Then** every admitted primary root-run item has a terminal disposition and every frozen matching item is completed or explicitly failed/uncertain,
**And** paused root work does not qualify; accepted external files may contribute while their runs continue, without requiring global completion or globally synchronized source revisions.

**Given** an eligible fixed Flow Group Version,
**When** Story 13.15 executes real MAF synthesis,
**Then** it explains purpose, conditions, alternatives, effects and unknowns from accepted evidence, using only existing granted source/content/CodeGraph/LSP verification capabilities,
**And** every supplementary CL/direct-source contribution actually used enters exact validated dependencies; discrepancies remain Uncertainties/bounded proposals, never Worker-written CL/Edges or recursive repair, and existing provider/data-boundary policy applies.

**Given** a validated BL proposal with several contributors,
**When** Story 13.14 publishes it,
**Then** structured JSON, derived presentation, Flow Group/BL versions and dependencies become selected atomically under expected-head/desired-target, history-only intent, cancellation and live authority checks over every contributor including composite prose,
**And** incompatible evidence is qualified, late superseded results cannot regress current selection and pending/stale/failed refresh remains visible; historical GENERATED_FROM/relationships/direct-source dependencies never retarget.

**Given** a permitted reader without management rights,
**When** they browse the flow, follow exact sections or open history using the existing Knowledge Read Context,
**Then** discovery/membership/pagination/content/references remain version-bound with live contributor authorization; Business receives BL-only business content, while IT may follow permitted exact CL/source,
**And** counts/titles/paths cannot leak restricted contributors, unavailable recorded-revision source is explicit and no current checkout substitutes for it.

**Given** representative supported branches, shared helpers and unresolved external facts,
**When** real published explanations are independently assessed through the reader,
**Then** usable Simplified Chinese narrative and claim-to-source faithfulness are evaluated separately from schema/reference validity with exact examples,
**And** bounds, static-versus-executed meaning and missing facts remain clear without a graph, Chat, owner mappings or external-information integration.

**Scope and traceability:**

- **Prior dependencies:** 14.1–14.5 plus 13.4–13.7 and 13.10/13.13–13.15. Extend callable flow/synthesis/publication and existing reader contracts only.
- **Independent value/boundary:** readable real Cross-Repository BL and history before broad route qualification. No new reader, business-root merging, conversation/graph surface or global transaction.
- **Mapped requirements:** FR-4.5–4.7/4.9/4.10, FR-6.1/6.5/6.6, FR-9.4; NFR-1.1/2.3/3.3/5.1/5.2/6.1; AR-11–13/17/18/21/25/29; UX-DR2/11/35/39–41/44–49/52 reader-evidence portion.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 14.6 grants no implementation or runtime authority.

### Story 14.7: Qualify ordinary Cross-Repository workflow beyond a pair

As an authorized owner and Knowledge reader,
I want ordinary Scans to deliver and refresh reliable Cross-Repository explanations,
So that late evidence and independent failures need no special demo operations.

**Acceptance Criteria:**

**Given** the one-action Scan route and independently accepted file evidence,
**When** generic reconciliation and Cross-Repository flow work are composed,
**Then** Scan Control routes matching/inquiry/refresh/publication without separate stage-start calls, fixed samples, pair identities or mandatory catalogs,
**And** each request retains one explicit owner; local capability remains usable and Cross-Repository cutover occurs only after real replacement/history-read proof, with no legacy fallback.

**Given** at least three representative Repositories spanning multiple Systems under explicit authorized scope,
**When** real bounded tools/providers exercise supported one-to-many, late, missing, ambiguous and cyclic cases,
**Then** supported CL references and useful Cross-Repository BL include an accepted target file from an unfinished external run,
**And** unchanged caller source is not rescanned, missing operational facts remain nonblocking Uncertainties and configured bounds terminate cycles rather than imposing a shared completion barrier.

**Given** concurrent independent runs, inquiry and flow refresh,
**When** one run pauses/cancels or its provider/tools fail,
**Then** shared global/per-run/provider/tool limits and fair admission remain bounded; owned pending/downstream work is fenced and cleaned without cancelling independent consumers or deleting accepted evidence,
**And** other eligible work progresses while explicit resume checks current prerequisites/source/authority without provider substitution or automatic restart dispatch.

**Given** overlapping replacement/publication, access revocation or a lost acknowledgement,
**When** durable recovery and read operations are exercised,
**Then** stale writers cannot regress desired/current selections, replay duplicates no accepted Knowledge, and every composite contributor remains authorized before disclosure/publication,
**And** retained history preserves exact original dependencies; restricted counts/paths vanish without treating access loss as relationship disproof or requiring whole-product restore implementation.

**Given** reconciliation/inquiry/refresh activity,
**When** the established dashboard/history/SignalR and wiki contracts expose results,
**Then** scoped unresolved explanations/counts and real Worker/error activity remain distinct from primary coverage and Knowledge Currentness,
**And** reader access is not management-only, Business receives no technical endpoint/source metadata, Chinese LoadState/accessibility remains intact and sanitized correlated events use the existing required `log.md` sink.

**Given** the bundled two-service environment and version-bound evidence,
**When** this integration is accepted,
**Then** evidence separates automated boundary checks, real tools/provider execution, independently reviewed explanation usefulness/faithfulness and historical results,
**And** record configuration, capabilities, durations and available usage with unknown usage explicit; claim neither universal protocol detection/throughput nor Chat, graph or deferred operational-information completion.

**Scope and traceability:**

- **Prior dependencies:** 14.1–14.6 and 13.17–13.19. Composition/qualification only; missing core behavior returns to its owner.
- **Independent value/boundary:** readable beyond-pair understanding and bounded later-knowledge improvement. No new scheduler, separately deployed service, graph, Chat, automatic source updates or backup/restore feature.
- **Mapped requirements:** FR-4.2/4.5/4.7–4.10, FR-5.2–5.8/5.10–5.12, FR-6.1–6.6, FR-12.1–12.4; NFR-1.1/2.3/3.1–3.3/4.1 runtime portion/5.1/5.2; AR-1/9–18/20–23/30/32; UX-DR28–35/39–42/45–49/52–53 non-Chat/non-graph portions/57.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 14.7 grants no implementation or runtime authority.

**Epic 14 definition acceptance:** Seven story boundaries accepted under the user's confident-boundary delegation. Independent review found no forward dependency or new product choice; its inquiry atomic-completion finding was corrected and verified. This completes definition, not implementation or runtime qualification. Proceed to Epic 15.


## Epic 15: Useful Streaming Wiki Conversations

Business and technical users can hold useful multi-document, follow-up wiki conversations with exact usable references, genuine live output and durable dialogue that survives browser disconnect without duplicate generation. This epic consumes the established Knowledge/read/source/admission owners and requires no later graph or operations epic.


### Story 15.1: Explore permitted wiki evidence through mode-safe Chat tools

As an authorized Business or IT user,
I want Chat to discover and read permitted wiki evidence within my selected scope,
So that answers can use relevant Knowledge without exposing restricted content or starting another Scan.

**Acceptance Criteria:**

**Given** authorized Systems/Repositories, including standalone Repositories,
**When** scope preparation receives manual selection, first use, remembered selection or an empty selection,
**Then** it preserves whole-System/individual choices, defaults initially to permitted Systems/eligible standalone Repositories and filters remembered choices by live access,
**And** empty-selection relevance uses retrieval only inside server-approved discovery and returns the inferred scope for adjustment; model relevance grants no authority.

**Given** an approved Chat-purpose binding,
**When** search, browse, section/content or existing-related-Document tools are invoked,
**Then** they consume the existing Knowledge read contract and return bounded permitted evidence, exact reference descriptors, provenance, coverage, Uncertainties and pagination,
**And** no-results and empty related membership remain truthful; neither a fixed five-Document limit, per-record tools, embeddings nor index.md is mandatory.

**Given** a Knowledge Read Context pinned before discovery,
**When** later pages, newly discovered Documents, related membership or content are read during concurrent publication,
**Then** all calls/references retain that exact selection rather than merely caching previously read Documents,
**And** a fresh context may observe newer Knowledge; historical references never retarget and material stale/conflicting evidence remains explicit.

**Given** Business or IT mode and separate purpose-specific approval,
**When** a tool request is authorized,
**Then** Business receives BL-only content/references without CL, code, paths, ranges or Git metadata,
**And** IT may read permitted CL/BL and exceptionally inspect bounded source for a specific gap at its recorded Profiled Revision through Story 13.1; unavailable source is explicit, never substituted with current source.

**Given** forged references/cursors, scope widening or revoked contributor access,
**When** any tool would disclose data,
**Then** the server validates live user/mode/purpose/scope and every composite contributor before returning content, metadata, counts or targets,
**And** Scan approval grants no Chat authority; tools cannot grant CodeGraph/LSP access, choose host paths, initiate Scans, repair Knowledge or fetch external operational facts.

**Given** isolated selected and historical Knowledge fixtures,
**When** controlled callers exercise these callable tools,
**Then** checks demonstrate cross-System/standalone scope, empty-scope inference, later-document stability, pagination, revocation and exact-source denial without a model call,
**And** permissions are enforced in actual returned inputs, not prompt instructions or hidden UI elements.

**Scope and traceability:**

- **Prior dependencies:** 13.1, 13.7, 13.10 and 13.14; retained role/provider configuration. Knowledge keeps read-context/storage/index/reference ownership; this story adds the Chat-purpose tool adapter and bounded scope preparation, not durable Conversations or UI.
- **Mapped requirements:** FR-4.7/4.9, FR-6.5 read support, FR-11.1/11.2/11.4/11.6/11.8, FR-13.3; NFR-1.2/2.3; AR-3/23–25/29; UX-DR20–23/41 backend portions. Later UI displays scope/preview states; no later-story dependency or general MCP connector.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 15.1 grants no implementation or runtime authority.

### Story 15.2: Preserve durable dialogue with isolated bounded inference contexts

As an authorized Chat user,
I want ordered Conversations and useful permitted history to survive beyond a browser session,
So that follow-up questions retain context without mixing modes, scopes or unfinished answers.

**Acceptance Criteria:**

**Given** an authorized user starting or reopening a Conversation,
**When** Chat stores and loads its ordered questions, Chat Turns and original references,
**Then** durable records preserve ownership, originating mode/scope, Inference Context, Knowledge Read Context and terminal/hypothesis meaning independently of circuit lifetime,
**And** dialogue remains separate from immutable wiki Documents and creates no Knowledge publication or reconciliation.

**Given** duplicate initiation or concurrent questions for one Conversation,
**When** the callable turn-initiation operation executes,
**Then** atomic initiation permits one active answer and binds one turn identity to its original question/context; replay returns that same turn without a second dispatch,
**And** conflicting identity reuse is rejected, and pending/saving/unconfirmed work cannot silently admit a competing answer before authoritative terminal settlement.

**Given** prior completed answers, questions and optional advisory text,
**When** Chat prepares bounded Inference Context,
**Then** useful permitted completed history/references fit declared limits instead of copying an unbounded transcript,
**And** any summaries preserve hypothesis status, contributor scope and exact references; interrupted text is not promoted to completed factual history, and human context remains lower-authority advice.

**Given** a confirmed mode change or a changed scope,
**When** the backend creates the fresh Inference Context/new-chat binding required by that operation,
**Then** authorized older dialogue remains durable while the new context excludes disallowed prompts, histories, summaries and caches,
**And** active-turn deltas/results/saves remain attached to their originating turn/context; late IT output cannot enter the new Business context.

**Given** contributor revocation or narrower current scope,
**When** stored dialogue is disclosed or reused for inference,
**Then** every contributing source is reauthorized, including composite answers/summaries whose visible citations omit a restricted contributor,
**And** affected generated material is excluded from disclosure/reuse without erasing original provenance; knowing a Conversation/turn identifier grants no access.

**Given** isolated ordered-history fixtures and controlled initiation clients,
**When** restart, concurrent initiation, replay, context change and live revocation are exercised,
**Then** the callable persistence/context contracts retain correct order, identity and bounded permitted history without provider generation,
**And** history fixtures do not create production completion shortcuts or require a later UI story.

**Scope and traceability:**

- **Prior dependencies:** 15.1 and preserved Identity/persistence. Chat owns Conversation/turn/Inference Context and bounded-history records; Infrastructure persists them, and Knowledge still owns read selection. Establish only records needed here; terminal-save safety follows in 15.3 without being a prerequisite for initiation/history tests.
- **Mapped requirements:** FR-7.3, FR-9.4 reference preservation, FR-11.1–11.4/11.7; NFR-2.3/3.3; AR-2/25/26; UX-DR19/21/23/27 backend portions. UI confirmation/theme/history navigation remains later work; no attachments or new Knowledge storage.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 15.2 grants no implementation or runtime authority.

### Story 15.3: Save terminal Chat outcomes once and expose truthful progress

As a Chat user,
I want answer progress and completion to reflect durable reality,
So that a failed or unconfirmed save never appears completed and recovery never regenerates my answer accidentally.

**Acceptance Criteria:**

**Given** a turn initiated through Story 15.2 and a controlled streaming client,
**When** retrieval, actual answer deltas and provider completion are observed,
**Then** one Chat-owned output contract distinguishes waiting/retrieval, provisional generation, saving, save-unconfirmed, completed and interrupted/failed-save outcomes,
**And** when hypothesis-labelled content is emitted, its label accompanies the first provisional text and survives interruption/terminal save; controlled clients verify no unlabelled interval, without semantic hypothesis detection or whole-answer verification,
**And** it relays actual provisional text/status rather than buffered complete-answer replay, exposing neither hidden reasoning nor secrets.

**Given** a provider reports completion,
**When** Chat evaluates terminal eligibility,
**Then** completed status requires valid provider completion, no truncation and no unfinished tools, followed by one durable content/reference/status save,
**And** stream closure, schema validity or emitted text alone cannot complete; no mandatory whole-answer JSON or semantic-verification gate is introduced.

**Given** an eligible terminal outcome and originating turn/context,
**When** Infrastructure executes Chat's fenced idempotent save,
**Then** content, exact authorized reference descriptors, contributor/hypothesis metadata and terminal status commit together once under current ownership/context checks,
**And** readers see the previous state or committed outcome; conflicting replay, stale writers and cross-context saves cannot overwrite it, and live authority still governs disclosure.

**Given** lost save acknowledgement, storage failure or telemetry failure,
**When** completion is reconciled,
**Then** durable lookup by the same turn/save identity recovers an existing commit or permits retry of the same pending save, never another model generation,
**And** unconfirmed/failed-save status remains truthful with useful permitted text; telemetry failure cannot undo a committed completion or fabricate one.

**Given** cancellation, limits, truncation, unfinished tools, revocation or terminal execution failure,
**When** the turn settles,
**Then** useful authorized partial text and its reason retain interrupted status distinct from completed factual history; affected revoked material is withheld,
**And** owned work/capacity is stopped and settled, late completion is fenced and visible output never triggers automatic generation restart.

**Given** controlled valid/invalid endings and fault injection around the terminal transaction,
**When** pre-commit failure, post-commit acknowledgement loss, replay, stale ownership and disclosure are checked,
**Then** durable outcomes and emitted statuses agree without duplicate completed turns or regenerated answers,
**And** checks prove lifecycle/storage behavior, not explanation truth or real-provider qualification.

**Scope and traceability:**

- **Prior dependencies:** 15.1/15.2 and 13.7's shared admission/diagnostic contracts. Chat owns streamed-output/terminal policy; Infrastructure implements the atomic completion contract. Controlled clients precede real MAF; no production fixture-answer route is added.
- **Mapped requirements:** FR-11.3/11.7; NFR-2.3/3.2/3.3; AR-2/23/26/27; UX-DR23–25/27 status-contract portions. Later UI maps these states, and 15.5 adds browser attachment behavior; neither is required for this callable boundary.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 15.3 grants no implementation or runtime authority.

### Story 15.4: Stream useful real wiki conversations with faithful references

As a Business or IT user,
I want useful multi-document answers and follow-ups generated from permitted Knowledge,
So that I can understand supported behavior and uncertainties while seeing genuine progress.

**Acceptance Criteria:**

**Given** an authorized initiated turn with bounded history and one Knowledge Read Context,
**When** Agent Execution runs the configured Chat provider through Microsoft Agent Framework,
**Then** the real conversation/tool loop uses externalized mode prompts and Story 15.1's wiki tools to synthesize useful explanations rather than fixed candidate sentences,
**And** native framework execution does not grant extra tools, domain writes or a second completion owner.

**Given** Business/IT questions, follow-ups and retrieved multi-document evidence,
**When** the model explains behavior,
**Then** Business uses BL-only permitted business content; IT may use authorized CL/BL and exceptional gap-specific recorded-revision source without routinely re-proving the wiki,
**And** retrieval/advisory text is evidence rather than instructions; shared code alone proves no business dependency, and unsupported or conflicting facts remain explicit limitations.

**Given** answer generation and claim-linked references,
**When** actual provider deltas arrive,
**Then** provisional text and concise progress are observable before generation finishes, with hypotheses labelled from their first text and exact permitted Document Version/section references,
**And** greetings/no-results need no invented citations; hidden reasoning, secrets and buffered typewriter replay are excluded without requiring complete-answer JSON or whole-answer semantic approval.

**Given** shared provider demand and execution/data limits,
**When** inference and tool calls are admitted,
**Then** existing application-wide admission and cumulative time/call/cost budgets apply; self-hosted source/Documents stay inside organization infrastructure and external providers receive only necessary authorized context,
**And** actual provider inputs are checked against these boundaries; no silent provider substitution, fresh budget on retry or automatic restart after visible output occurs.

**Given** valid completion or interrupted/failed execution,
**When** real provider events reach Story 15.3,
**Then** its sole terminal-save contract determines durable completion, interrupted text and acknowledgement recovery without regeneration,
**And** sanitized query/system events reach Story 13.7's existing `log.md` sink; available usage stays separate from unknown usage and telemetry cannot undo completion.

**Given** multi-document and follow-up questions, sparse Knowledge and explicit uncertainties,
**When** conversations are independently assessed under declared configuration,
**Then** evidence evaluates useful Simplified Chinese explanations and claim-to-source faithfulness separately from valid IDs/schema, measures first useful delivery and total duration, and includes discovery beyond five Documents when needed,
**And** no universal latency, mandatory retrieval count, whole-wiki audit or historical qualification claim replaces those results.

**Scope and traceability:**

- **Prior dependencies:** 15.1–15.3, 13.7/13.8's shared Agent Execution policy and existing published Knowledge; Epic 14 supplies Cross-Repository scenarios. Real callable streamed conversation requires no later UI.
- **Mapped requirements:** FR-4.8, FR-6.5, FR-7.3, FR-11.1/11.2/11.4/11.6/11.7, FR-13.3; NFR-1.2/2.3/3.2/3.3/5.1/6.1; AR-23–27/32; UX-DR22–25/27 backend portions. No Scan/repair scheduling, CodeGraph/LSP Chat grant, external connector or new reader.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 15.4 grants no implementation or runtime authority.

### Story 15.5: Reattach to the same bounded host-owned Chat Turn

As a Chat user,
I want a browser disconnect to preserve the answer already in progress,
So that reconnecting shows the same text and outcome without another generation or lost history.

**Acceptance Criteria:**

**Given** a started turn whose browser transport disconnects,
**When** the host remains available,
**Then** Chat may continue that same turn outside request/circuit lifetime within its original identity, mode/scope, Knowledge Read Context and time/cost limits,
**And** disconnection is not cancellation, completion or permission to restart generation; finishing while detached still uses Story 15.3's sole durable save.

**Given** an authorized client reconnecting to a running, saving, interrupted or completed turn,
**When** it attaches through the callable reattachment contract,
**Then** the server validates Conversation/turn ownership and live contributor access and joins the existing work or saved outcome,
**And** attachment neither initiates a new turn/model call nor renews budgets, changes source selection or retargets references.

**Given** accumulated text/status and concurrent new deltas or terminal completion,
**When** initial attachment state is reconciled with subsequent updates,
**Then** the client-observable sequence contains no lost or duplicated answer text/status, including completion during attachment and repeated reconnects,
**And** updates remain origin-bound so a switched mode/scope cannot receive late content belonging to another context.

**Given** host-process failure instead of browser disconnection,
**When** durable conversation state is reopened,
**Then** completed saves remain authoritative and orphaned unfinished turns become interrupted with preserved durable history and explicit retry required,
**And** recovery first resolves saved completion rather than overwriting it, never automatically resumes generation and promises neither transparent per-token host recovery nor a durable token ledger.

**Given** explicit cancellation, budget exhaustion, revoked contributor access or lost execution ownership,
**When** an attached or detached turn settles,
**Then** owned inference/tool activity and capacity are stopped/released, late writes/disclosure are fenced and useful permitted interrupted text retains its reason,
**And** revocation blocks future unauthorized output/history/summary reuse without deleting provenance or pretending already delivered text can be recalled; unrelated work survives.

**Given** controlled detach/attach races and real bounded streaming conversations,
**When** repeated reconnect, completion-during-attachment, save acknowledgement loss, cancellation, revocation and host failure are qualified,
**Then** evidence distinguishes same-generation reattachment from explicit retry and demonstrates nonduplicating/lossless browser recovery under original limits,
**And** durable outcomes remain correct without requiring a future UI, operator restore or fresh provider generation to recover a saved answer.

**Scope and traceability:**

- **Prior dependencies:** 15.1–15.4 and 13.7's shared capacity contracts. Chat owns host execution/attachment state; Infrastructure supplies existing persistence, and transport adapters expose the callable contract. Do not add another lifecycle, reader or token store.
- **Mapped requirements:** FR-11.3/11.7; NFR-2.3/3.2/3.3; AR-23/25–28; UX-DR19/23/25–27 backend portions. Later UI renders disconnect/rejoin/retry states; no source Scan, automatic regeneration or new tool authority.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 15.5 grants no implementation or runtime authority.

### Story 15.6: Use Chinese Chat with real scope, streaming and durable history

As an authorized user,
I want a usable Chinese Chat interface for asking questions and following answers through their real lifecycle,
So that I can understand permitted Knowledge without managing execution stages or losing the meaning of interrupted work.

**Acceptance Criteria:**

**Given** successful login or a new Conversation,
**When** I enter 「对话」,
**Then** the Chinese Fluent/Library Oak single-column Chat retains mode control, inline scope, message stream and bottom input in the specified order, with light Business/dark IT themes,
**And** successful login defaults to 「对话」 in light Business Mode; explicit IT switching or authorized session navigation preserves the intended mode,
**And** new-session greetings are 「你好，想了解什么业务流程？」 or 「你好，需要追踪哪段逻辑？」; mode-specific placeholders, 「发送」, Enter-send and Shift+Enter-newline work.

**Given** my current Repository permissions,
**When** I select, remove or clear whole Systems or individual/standalone Repositories,
**Then** options and remembered selection are access-filtered; first use selects permitted Systems and eligible standalone Repositories,
**And** empty-selection inference stays within authorized discovery and displays 「已自动选择以下范围：{范围名称}」 with 「调整范围」, without initiating Scans.

**Given** a requested mode or scope change,
**When** I confirm 「切换模式将开始新对话，已保存的历史仍可查看。确定切换？」 for a mode switch,
**Then** 「开始新的对话」 and the 200–400ms theme transition accompany fresh inference context; scope changes also isolate context,
**And** permitted saved history remains accessible, but old IT prompts, summaries, caches and late output/saves never enter Business; unsaved work is not declared saved.

**Given** an active answer,
**When** retrieval, genuine deltas and durable completion advance,
**Then** the UI shows the 200ms three-dot waiting indicator, 「正在查找相关文档…」, genuine 「正在生成」, first-text hypothesis labels and 「正在保存」 only after valid provider completion,
**And** only durable save marks completion; acknowledgement recovery shows 「正在确认保存结果」, while 「回答未完成保存」 permits the same save retry, never generation replay.

**Given** disconnect, cancellation, interruption or reopened history,
**When** I reconnect or use a permitted recovery action,
**Then** disconnect shows 「连接已断开，重新连接后将查看同一条回答。」 and permits same-turn lossless/nonduplicating reattachment; interruption shows 「回答已中断」 with reason/useful authorized text, and host failure requires explicit 「重试」,
**And** one active answer per Conversation, origin-bound history and server-side Business BL-only disclosure remain enforced,
**And** usable claim-linked exact-section links open the existing reader now, without waiting for previews.

**Given** loading, empty/no-results/error states and keyboard/screen-reader interaction,
**When** the implemented Chat journeys are verified,
**Then** shared LoadState, specified Chinese feedback (including 「未找到相关文档。试试调整系统选择范围或换个问法。」), Fluent loading/refresh, Tab/Esc, pagination and meaningful announcements work without color-only states or token-by-token announcements,
**And** verification checks contrast, live revocation, correct state transitions and readable references; no fixtures or completed-answer typewriter simulate production.

**Scope and dependencies:**

- Depends on 15.1–15.5 and the existing 13.10/13.14 reader; adapt existing Chat components, not administration or a second lifecycle owner. No attachments, new graph or preview prerequisite.
- Mapped: FR-11.1–11.4/11.6–11.8, FR-9.4; NFR-2.3/3.3/5.1/5.2; AR-24–29; UX-DR1–9/11–27/45–50/53. UX-DR10/12/43–44 remain preserved outside changed surfaces; preview-specific UX-DR22/51 continues in 15.7.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 15.6 grants no implementation or runtime authority.

### Story 15.7: Preview exact claim-linked references without crossing mode or access boundaries

As an authorized Knowledge reader,
I want to preview an answer's cited evidence and open its exact section, history or permitted source,
So that I can inspect what supported the claim without references silently changing or revealing restricted information.

**Acceptance Criteria:**

**Given** an answer with an authorized claim-linked reference,
**When** I select its citation,
**Then** an inline preview identifies the exact permitted Document Version/section and offers the working existing-reader destination,
**And** the reference retains the originating turn's Knowledge Read Context and exact evidence binding; a title or current-document lookup cannot replace it.

**Given** Business Mode,
**When** preview, reader, history or cached reference data is requested,
**Then** server-side authorization returns only permitted BL titles, sections and business content,
**And** CL, source code, technical paths/ranges/Git metadata and forbidden target/count/tooltip data are neither returned nor merely hidden by the UI,
**And** reading needs Knowledge permission, not management rights.

**Given** IT Mode and a relevant exact reference or specific source-evidence gap,
**When** I follow permitted CL/BL history or an available Source Anchor,
**Then** the existing reader opens the recorded Document Version/section and bounded source at its Profiled Revision under separate current authority,
**And** unavailable source shows 「该版本源代码暂不可用，文档及来源记录仍保留。」; current checkout, arbitrary host paths, routine source auditing and Scan/repair actions are unavailable.

**Given** newer publication, changed relationships or a reference from an older Conversation,
**When** I reopen that reference and inspect history,
**Then** original content and exact dependencies remain selected, with applicable historical/partial/stale/unavailable states,
**And** deliberate navigation to another permitted version does not rewrite the original citation or retarget old GENERATED_FROM evidence.

**Given** access revoked for any contributor, including one used in composite prose but absent from displayed citations,
**When** preview, content, history or reference navigation would disclose affected material,
**Then** the shared server-side read/disclosure contracts block that material and associated metadata before further disclosure,
**And** stable selections and cached previews grant no authorization exception; feedback is safe and does not identify restricted contributors.

**Given** greetings, no-result answers, sparse references or unavailable/denied targets,
**When** citation interactions and error recovery are verified,
**Then** no reference is invented; Chinese feedback distinguishes absence, limitation and denial without fabricating evidence,
**And** keyboard activation, Esc dismissal, focus return and exact-reader navigation work with at most one dialog; tests cover Business/IT, old versions and live revocation,
**And** reference identity is not presented as proof that every answer claim is correct.

**Scope and dependencies:**

- Depends on 15.1–15.6 and 13.10/13.14. Add preview/navigation presentation to existing Knowledge authorization/history/source contracts, not a separate reader, cache authority or source Agent.
- Mapped: FR-4.9/9.4/11.1–11.4/11.6/11.7; NFR-2.3/5.1/5.2; AR-3/24–26/29; UX-DR2–7/11/22/23/39–41/44–49/51/52. No Epic 16 prerequisite, attachments, outward MCP, graph expansion or new tool approval scope.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 15.7 grants no implementation or runtime authority.

### Story 15.8: Deliver and qualify ordinary production wiki conversations

As a Business or IT user,
I want the ordinary Chat route to provide useful real wiki answers and reliable recovery,
So that I can rely on the product's actual behavior rather than a sample-specific demonstration.

**Acceptance Criteria:**

**Given** authorized published Knowledge and configured providers,
**When** the ordinary application composes Chat,
**Then** it uses Stories 15.1–15.7 through one explicit production route without fixed Repository pairs, sample identities, candidate-sentence answers or silent legacy fallback,
**And** existing authorized history keeps its recorded meaning; only equivalent semantics migrate, and unavailable capabilities remain visibly unavailable.

**Given** real local and Cross-Repository multi-document Knowledge,
**When** representative Business/IT multi-round questions run through configured MAF/provider execution and the actual UI,
**Then** useful Chinese explanations address follow-ups, supported alternatives, gaps and Uncertainties with readable exact references and first-text hypothesis labels,
**And** Business remains BL-only; IT stays wiki-first with exceptional scoped source, not a code Agent,
**And** explanatory usefulness and citation faithfulness are assessed separately from schemas/reference identity, without a fixed five-Document cap.

**Given** new publication during a turn, later-discovered Documents and mode/scope changes,
**When** retrieval, later pages, follow-ups and historical citations are exercised,
**Then** active discovery/content/references retain one stable selection, later turns can use fresh Knowledge and earlier citations remain exact,
**And** fresh inference context excludes disallowed prompts, history, summaries/caches and late output; live contributor revocation overrides selection stability.

**Given** production-composed streaming and controlled completion/storage faults,
**When** genuine generation, truncation, unfinished tools, save failure or lost acknowledgement occur,
**Then** useful deltas precede valid completion, invalid termination cannot appear completed and durable-state reconciliation avoids duplicate save/generation,
**And** interrupted permitted text retains its reason; no whole-answer JSON/semantic gate, hidden reasoning or buffered typewriter replaces genuine streaming.

**Given** detach/reattach during generation or saving, cancellation, host failure and authorization loss,
**When** isolated lifecycle checks exercise the real hosting/transport boundaries,
**Then** same-turn attachment reconciles text/status without loss, duplication or renewed budgets; host failure requires explicit retry,
**And** affected work/permits settle within declared bounds, revocation stops further disclosure and visible answers never automatically restart.

**Given** actual queries and lifecycle events in the two-service Docker deployment,
**When** integration evidence is recorded,
**Then** Story 13.7's shared append-only `log.md` sink receives sanitized query events using `## [YYYY-MM-DD HH:MM] operation | target`, without raw sensitive queries/source, credentials or hidden reasoning,
**And** evidence records configuration, first-useful/total timing, limits, available usage and quality/access outcomes; unknown usage is not zero, routine health makes no inference and no universal latency/cost guarantee is claimed.

**Scope and dependencies:**

- Depends on 15.1–15.7 and Epics 13–14. Own production cutover/integration; earlier stories verify their own capabilities. No deferred single evidence gate or Epic 16–17 prerequisite.
- Mapped: FR-4.8/6.5/7.3/11.1–11.4/11.6–11.8/13.3; NFR-1.2/2.3/3.1–3.3/5.1/5.2/6.1; AR-16/23–29/32; UX-DR19–27/45–53. No attachments, vector service, outward MCP or expanded tool authority. Qualification requires actual evidence.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 15.8 grants no implementation or runtime authority.

**Epic 15 definition acceptance:** Eight story boundaries accepted under the user's confident-boundary delegation after independent scope/dependency review and closure of default-entry/first-text-label findings. Stable reads, real streaming, durable completion and same-turn reattachment are defined without later-reader or graph prerequisites. This is definition acceptance only. Proceed to Epic 16.


## Epic 16: Version-Aware Knowledge Exploration

Authorized readers can explore the integrated Document workspace, exact history and a basic manually refreshed Logical Flow/Document-neighborhood graph. Existing minimum readers remain available; all exploration reuses Knowledge's version selection and live authorization, without a Chat-session or later-operations prerequisite.


### Story 16.1: Complete cohesive dedicated Document browsing and history navigation

As an authorized Knowledge reader,
I want the dedicated Document workspace to connect source-oriented and Logical Flow browsing with exact reading and history,
So that I can explore available explanations coherently without management privileges or losing which evidence I am viewing.

**Acceptance Criteria:**

**Given** the existing CL/BL reader and Chat reference routes,
**When** I enter 「文档」, open an exact reference or navigate between permitted browsing views,
**Then** the existing sidebar/main-detail shell coherently selects the corresponding tree item, Document Version/section and history state,
**And** this works without a Chat session, graph or management permission; no new top-level reader layout or role is introduced.

**Given** permitted local and Cross-Repository Knowledge,
**When** I browse CL or BL,
**Then** the integrated sidebar preserves CL Repository/file/Code Unit nesting and BL Logical Flows grouped through System/Repository views,
**And** multiple appearances identify the same Logical Document, without synthetic directory Documents, duplicate BL identities or inferred rename/split/merge lineage,
**And** Business shows only permitted BL; IT may use both types.

**Given** title/keyword search, authorized paging and selected or explicitly labelled staged/historical reading,
**When** I refine results or navigate while newer Knowledge is published,
**Then** the existing Knowledge Read Context keeps discovery, pages, selected content and exact links coherent,
**And** explicit fresh-selection navigation may observe newer Knowledge without retargeting an existing historical reference or claiming failed drafts as Documents.

**Given** historical versions, coverage gaps or pending/stale/failed/retired Knowledge,
**When** I open 「版本历史」 or return to selected content,
**Then** the workspace displays actual selection/currentness and gaps separately from operational Scan completion,
**And** old dependencies remain exact; failed current analysis cannot be disguised by presenting older analysis as newly accepted.

**Given** source navigation or changing contributor authority,
**When** reader headers, sections, metadata, counts or source targets are disclosed,
**Then** shared live authorization protects every contributor; Business receives no technical metadata,
**And** IT sees only permitted Repository/full Profiled Revision/relative locations; unavailable historical source uses the established Chinese message without current-checkout substitution or a new Source Snapshot version axis.

**Given** the completed navigation integration,
**When** isolated application/UI checks exercise Business/IT, standalone/Cross-Repository browsing, old references, revocation and loading/error paths,
**Then** Chinese Fluent/Library Oak styling, shared LoadState, title/keyword search, pagination, Tab/Esc/focus and accessible feedback remain usable,
**And** empty/partial/unavailable states stay truthful; existing administration and Scan-history routes remain intact without a graph or provider prerequisite.

**Scope and dependencies:**

- Depends on 13.10/13.14, Epic 14's published Knowledge and 15.6/15.7 navigation. Integrate existing UI/read contracts only; no new storage/index/acceptance authority, editable Documents or duplicate minimum-reader implementation.
- Mapped: FR-4.6/4.7/4.9/4.10/9.4/12.6 reader portions; NFR-2.3/5.1/5.2; AR-13/17/19/25/29; UX-DR10–12/39–49/51–53. UX-DR12/42/43 are preservation constraints, not new administration delivery. Verify this story independently; no future graph or Epic 17 gate.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 16.1 grants no implementation or runtime authority.

### Story 16.2: Query an authorized graph of exact Knowledge evidence

As an authorized Knowledge consumer,
I want a bounded Logical Flow or Document-neighborhood graph bound to selected versions and evidence,
So that exploration can show actual relationships and limits without inventing dependencies or disclosing restricted Knowledge.

**Acceptance Criteria:**

**Given** a permitted Logical Flow or Document and an existing Knowledge Read Context,
**When** the application requests its bounded graph projection,
**Then** Knowledge supplies version-bound nodes, recorded relationships, exact reader targets and applicable Uncertain Frontiers from accepted evidence,
**And** selected/current or historical context is explicit; CodeGraph indexes, model output and failed drafts are not wiki graph authorities.

**Given** REFERENCES, GENERATED_FROM, unresolved evidence and shared dependencies,
**When** the projection describes links and frontiers,
**Then** it preserves their actual directed meanings and exact historical dependencies without retargeting old GENERATED_FROM,
**And** no shared helper, name, organizational membership or absence of visible candidates creates a business dependency, deployed-path claim or fabricated target,
**And** a frontier remains an explicit limitation rather than an accepted relationship.

**Given** mode/user/scope restrictions or access loss for any contributor,
**When** graph data is requested, paged or refreshed,
**Then** the server filters every node, Edge, title, label, count, tooltip and destination before the client receives it, including composite contributors not named in visible citations,
**And** Business receives only permitted BL data, never an IT graph hidden by presentation; narrowed visibility does not rewrite accepted relationships.

**Given** large neighborhoods, cycles, sparse results or configured query limits,
**When** the graph operation executes,
**Then** traversal terminates within declared bounds with honest scope, partial/limited status and supported continuation rather than pretending the result is complete,
**And** permitted counts distinguish returned/bounded coverage without leaking excluded contributors; sparse or empty graphs are valid.

**Given** newer publication or changed current selection,
**When** an explicit manual refresh requests a fresh current graph,
**Then** all returned graph fields share the newly selected read context rather than mixing old and new evidence,
**And** historical graphs retain their original version/evidence binding; currentness and historical-source availability remain separate,
**And** reading/refresh starts no Scan, reconciliation, model generation or publication.

**Given** the callable query on isolated accepted local/Cross-Repository data,
**When** integration checks exercise cycles, unresolved frontiers, concurrent publication, restrictive Business views and live contributor revocation,
**Then** real persistence/query results demonstrate exact bindings, boundedness and non-disclosure without a graph UI or provider call,
**And** sanitized failures identify unavailable data/capabilities without arbitrary host paths or false completeness.

**Scope and dependencies:**

- Depends on the existing 13.10/13.14 Knowledge read contract, Epic 14 evidence and 16.1 reader destinations. Knowledge owns projection semantics; Infrastructure implements its queries. No graph database, independent snapshot cache authority, new relationship owner or mandatory index.
- Mapped: FR-4.7/4.10/5.4/5.10–5.12/6.4/6.6/9.4/12.6; NFR-2.3/3.1; AR-2/9/13/17/25/29; UX-DR36–38/40/41/44/46 data-contract portions. This delivers usable query/refresh capability, not UI qualification or a freshness SLA.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 16.2 grants no implementation or runtime authority.

### Story 16.3: Explore the basic graph through manual refresh and exact reader navigation

As a Business or IT reader,
I want a clear Chinese graph that I can refresh, pan, zoom and use to open exact Knowledge,
So that I can explore a permitted neighborhood while understanding its versions, gaps and limits.

**Acceptance Criteria:**

**Given** permitted Knowledge and Story 16.2's projection,
**When** I open 「图谱」 or follow authorized reader navigation,
**Then** the existing application shell presents the basic Z.Blazor.Diagrams Logical Flow/Document-neighborhood view, authorized scope filters and 「刷新」 without requiring management rights,
**And** Business/light and IT/dark backgrounds, primary toolbar controls and existing Fluent conventions are retained.

**Given** visible permitted nodes, relationships and Uncertain Frontiers,
**When** the graph renders,
**Then** CL blue/BL green styling, intra-Repository solid light-gray versus Cross-Repository dashed references and explicit warning frontier/diamond styling accompany meaningful labels,
**And** REFERENCES/GENERATED_FROM meanings are distinguishable without color alone; no invented targets, hidden fetched IT data or implied business dependencies fill sparse views.

**Given** a selected current or historical graph,
**When** I pan, zoom, select a node or deliberately refresh,
**Then** interactions provide immediate feedback and exact reader/history navigation, while refresh reports the applicable selection/currentness,
**And** historical targets remain bound to original evidence; no drag/drop, node/Edge editing or automatic live-graph animation changes the domain or silently switches history to current.

**Given** loading, refresh, empty, partial, stale, retired, unavailable-source or error results,
**When** state changes,
**Then** shared LoadState and Chinese feedback distinguish these outcomes; empty graph shows 「当前范围暂无可用图谱。可调整范围或稍后刷新。」,
**And** refreshing retains permitted content with the specified progress treatment, limits/gaps remain visible and the existing reader handles unavailable source without replacing historical bytes.

**Given** keyboard/screen-reader use, mode/scope changes or contributor revocation,
**When** I navigate controls, inspect nodes or open reader targets,
**Then** focus order, keyboard-accessible selection/navigation, Esc dismissal, contrast and meaningful announcements meet the existing accessibility floor,
**And** subsequent disclosures recheck current authority, including labels/tooltips/counts; Business receives BL-only content and at most one dialog is active.

**Given** isolated accepted local/Cross-Repository Knowledge in the ordinary application,
**When** the declared Z.Blazor.Diagrams dependency and Business/IT reader/graph/Chat-reference journeys are qualified through real query/UI integration,
**Then** manual refresh, exact historical navigation, sparse/partial results and authorization changes behave as specified without provider inference to render the graph,
**And** existing real Scan progress and genuine Chat streaming/reattachment remain unchanged; recorded evidence identifies tested configuration and limits without a live-graph or certification claim.

**Scope and dependencies:**

- Depends on 16.1–16.2 and prior 13–15 reader/Chat delivery. Include the planned Z.Blazor.Diagrams package through central versioning and qualify integration; it is the adopted non-Fluent exception, not an already-installed dependency. No Epic 17 prerequisite.
- Mapped: FR-4.6/4.7/4.10/9.4/12.6; NFR-2.3/5.1/5.2; AR-25/29/32; UX-DR10/11/36–41/44–49/51–53. No advanced portfolio graph, live animation, authoring, new top-level layout/roles, publication authority or artificial freshness SLA.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 16.3 grants no implementation or runtime authority.

**Epic 16 definition acceptance:** Three bounded definitions accepted under delegated confidence after independent review found no material gap or forward dependency. Existing reader ownership, live mode/contributor filtering, exact history and manual graph boundaries are preserved. This is definition acceptance only. Proceed to Epic 17.


## Epic 17: Recoverable Product Operations and Qualification

An operator can configure, observe, back up and restore the delivered knowledge service coherently. Authorized users retain usable Knowledge and dialogue after supported interruptions without losing required source/keys or replaying paid work. Earlier epics already own their functional safety and qualification; this epic adds coherent operations and integrated proof.


### Story 17.1: Configure and diagnose the product through one operational contract

As an authorized operator,
I want portable configuration and distinct trustworthy readiness information,
So that I can identify prerequisite and capacity problems without guessing whether Knowledge remains usable.

**Acceptance Criteria:**

**Given** the existing two-service deployment and operational owners,
**When** deployment configuration is documented and validated,
**Then** bounded defaults and overrides cover storage/retention, container resources, Worker/per-run/provider/tool admission, tool versions/dependencies and required secret/proxy/CA configuration,
**And** examples require no developer-profile paths, host-installed tools, fixed machine capacity, extra service or new configuration owner; invalid required values fail explicitly.

**Given** application, database, installed tools, snapshot-bound analysis and provider observations,
**When** readiness is inspected,
**Then** each category reports its own scope, observation time, known/unknown state and sanitized cause,
**And** installed assets do not imply native MCP/index/LSP readiness or semantic completeness; missing analysis capabilities need not disable authorized existing wiki/history reads.

**Given** routine health polling or an Administrator's explicit provider connection test,
**When** availability is checked,
**Then** routine health performs no paid inference and cannot dispatch saved work; explicit testing remains separately authorized and identified,
**And** stale/unknown provider results are not fabricated readiness, and neither failures nor multiple configured providers imply automatic failover or provider identity changes.

**Given** exhausted storage/capacity or classified operational failure,
**When** existing controllers report queue/pause/failure and recovery choices,
**Then** diagnostics preserve explicit gaps and separate operational state, primary coverage and Knowledge Currentness with permitted real counts,
**And** exhaustion cannot silently exclude admitted source, delete pinned Knowledge/source or multiply global limits; source/key/data retention stays separate from disposable tool-state cleanup.

**Given** Scan, query and system events across the delivered routes,
**When** operational evidence is collected,
**Then** the existing append-only sink retains `log.md` entries as `## [YYYY-MM-DD HH:MM] operation | target`, supplemented by correlation IDs, states/counts, durations, failures and available usage,
**And** raw source, credentials, hidden reasoning and unnecessary sensitive payloads are absent; unknown usage is not zero, and documented output/access/retention handling never silently substitutes structured logs for the required trace.

**Given** the declared deployment and controlled prerequisite/resource failures,
**When** operator procedures and existing status surfaces are qualified,
**Then** configuration portability, distinct readiness, explicit test authorization, sanitized diagnostics and continued permitted reading are demonstrated,
**And** evidence identifies the tested configuration without claiming backup/restore, universal throughput or complete provider-adapter coverage.

**Scope and traceability:**

- **Prior dependencies:** 13.2/13.7/13.18, 14.7, 15.4/15.8 and delivered reader/status contracts. Consolidate operator documentation/configuration/diagnostic adapters, not schedulers, recovery engines, logging sinks or product roles. No new operational dashboard is required.
- **Mapped requirements:** FR-3.4/4.8/12.1–12.4; NFR-1.1/2.2/2.3/3.1/3.2/4.1; AR-15/23/30/32; UX-DR12/31–35/45–49 existing-surface portions. Missing earlier behavior returns to its owner.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 17.1 grants no implementation or runtime authority.

### Story 17.2: Capture a coherent protected maintenance-window backup

As an authorized operator,
I want an inspectable backup of the service's coherent durable state,
So that database records do not outlive the exact source, keys or configuration required to recover them.

**Acceptance Criteria:**

**Given** explicit source/destination locations and a planned maintenance window,
**When** backup preflight evaluates the selected resources,
**Then** the procedure identifies the owned application/database/source/key/configuration boundary, required capacity and a protected destination outside primary volumes,
**And** destination mistakes, insufficient access/capacity or missing required components stop capture without overwriting live resources or an earlier successful backup.

**Given** active or queued Scan, inquiry, publication, Chat and administration work,
**When** maintenance quiescence begins,
**Then** new writers/dispatch are prevented and existing writers settle through their owners before capture; source cleanup and key/configuration mutations also remain quiesced,
**And** the procedure verifies that stable boundary throughout capture rather than relying only on a database dump while source, keys or selections continue changing.

**Given** verified quiescence,
**When** the backup is captured,
**Then** it includes a consistent PostgreSQL view of Identity/domain data, retained canonical source including older pinned contributors, required Data Protection keys and matched restore configuration,
**And** required existing database roles/extensions and component/schema/tool compatibility information are retained; disposable Tool Workspaces/caches are not substituted for canonical source or treated as recovery authority.

**Given** the captured components,
**When** the procedure assembles and checks their backup manifest,
**Then** one capture identity binds required components, integrity evidence, source references and configuration/version information, with sensitive restore material protected from unauthorized access,
**And** inspection verifies completeness and referenced-source consistency without exposing secrets in manifests/logs; copied volumes alone never constitute a successful coherent-backup claim.

**Given** interruption, failed copying, failed integrity checks or loss of quiescence,
**When** capture cannot complete,
**Then** the result is explicitly failed/incomplete and cannot be selected as a successful backup; live state and previous successful backups remain intact,
**And** maintenance exit follows the existing owners' safe controls, without automatically resuming paused work, replaying Chat or silently changing source/provider/configuration.

**Given** isolated owned fixtures with accepted history, active-work checkpoints, source and protected credentials,
**When** the operator performs capture and independent manifest/component inspection,
**Then** evidence demonstrates stable capture, complete/incomplete classification, protected external storage and useful failure guidance without provider dispatch,
**And** this proves an inspectable backup artifact, not successful restoration, cross-host migration or a future story's outcome.

**Scope and traceability:**

- **Prior dependencies:** 17.1 and Epics 13–16's persistence/ownership contracts. Infrastructure implements the operator procedure; Scan Control/Chat and configuration owners quiesce their work. Only needed maintenance/capture state and artifacts are added; no restore prerequisite or backup UI.
- **Mapped requirements:** FR-4.9/9.4 preservation; NFR-2.2/2.3/3.3/4.1; AR-2/3/30/31; UX-DR25/27/33–35 truthful maintenance outcomes. No new roles, backup daemon, HA/PITR service or automatic canonical-source pruning.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 17.2 grants no implementation or runtime authority.

### Story 17.3: Restore isolated service state without replaying paid work

As an authorized operator,
I want a qualified isolated restoration with explicit recovery decisions,
So that restored Knowledge remains usable without copied leases or running records starting unauthorized work.

**Acceptance Criteria:**

**Given** a successful Story 17.2 backup and explicitly selected isolated owned resources,
**When** restore preflight validates destination, integrity, required components and compatible configuration,
**Then** the procedure rejects incomplete/mismatched captures before opening writers and does not overwrite unrelated/live resources,
**And** provider dispatch is disabled before service startup and remains disabled throughout restore qualification, including explicit connection-test paths and copied pending requests.

**Given** a validated capture,
**When** PostgreSQL data, existing required roles/extensions, exact retained source, Data Protection keys and restore configuration are restored together,
**Then** referenced source/configuration and protected-key usability are checked before normal service reopening,
**And** missing bytes, incompatible configuration or unusable keys produce explicit recovery failure; current checkout, temporary caches, new credentials or a freshly acquired revision cannot silently substitute.

**Given** copied completion records, leases and queued/running operational states,
**When** recovery reconciles durable state,
**Then** pre-restore ownership is invalidated and confirmed accepted Knowledge/Chat saves are preserved before remaining unfinished Scan work becomes recoverable/paused and orphaned Chat becomes interrupted,
**And** copied wakeups, leases and running statuses never authorize dispatch; replay cannot duplicate Knowledge, regress current selection or overwrite a confirmed completion as interrupted.

**Given** restored users, selected/historical Knowledge, source references and Conversations,
**When** authorized application reads and protected-credential checks are exercised,
**Then** actual restored CL/BL/history, exact dependencies, available recorded-revision source and ordered dialogue remain usable under live mode/contributor authorization,
**And** key checks reveal no secret values and make no provider request; unavailable historical source remains distinct from retained Document provenance.

**Given** unfinished restored work and an explicit Scan-resume or Chat-retry request,
**When** existing eligibility contracts evaluate recovery,
**Then** fresh authority, exact source where applicable, configuration and prerequisite checks are mandatory; prior identity or restored permission data alone does not reinstate execution ownership,
**And** qualification retains its dispatch guard, validates eligible/denied outcomes without real inference, and distinguishes Chat retry from browser reattachment or transparent host token replay.

**Given** restore interruptions, corrupted components and copied active/completed work,
**When** the isolated procedure is qualified,
**Then** evidence demonstrates safe failure, coherent reads/key use, completion reconciliation and zero real provider requests throughout restoration,
**And** the operator receives explicit reopening/recovery steps; restoration never implies automatic job resume or another environment's throughput.

**Scope and traceability:**

- **Prior dependencies:** 17.1/17.2, 13.7/13.16, 14.5 and 15.2/15.3/15.5 with existing readers. Restore integration only; earlier owners retain lifecycle/authorization rules. No production cutover, new service-principal policy, HA/PITR or token ledger.
- **Mapped requirements:** FR-4.9/9.4/11.3/11.7/12.1/12.4; NFR-2.2/2.3/3.1–3.3/4.1; AR-3/15/18/26–28/30/31; UX-DR23/25/27/33–35/40/41/46.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 17.3 grants no implementation or runtime authority.

### Story 17.4: Qualify complete-product behavior under declared operating limits

As a product owner and operator,
I want evidence of useful, secure and recoverable behavior,
So that acceptance reflects demonstrated configuration-specific outcomes rather than inherited tests or competition claims.

**Acceptance Criteria:**

**Given** delivered Epics 13–16 and Stories 17.1–17.3,
**When** the qualification boundary and representative workloads are fixed,
**Then** record application/schema/tool/provider versions, two-service non-root configuration, storage/resource/admission limits and scenario expectations,
**And** reuse earlier evidence only where contract, configuration and exercised behavior remain applicable; changed or uncovered combinations require fresh evidence, not automatic completion credit.

**Given** one/several/standalone Repositories and beyond-pair scope,
**When** real source-to-Knowledge routes exercise mixed supported source and progressive large files,
**Then** exact source, required native tools, accepted semantic units, useful CL/BL and readable history work without sample identities or fixed quotas,
**And** report admitted/accepted/pending/failed coverage separately from file windows, Code Units, Documents and operational completion, retaining explicit tool/content limitations.

**Given** independently arriving evidence, multiple Systems, one-to-many relationships and cycles,
**When** bounded matching/inquiry and per-flow refresh execute,
**Then** late accepted target files can contribute without global completion or unchanged-source rescanning; cycles terminate and missing facts remain Uncertainties,
**And** ordered publication, coalesced demands, exact historical dependencies and incompatible/partial/stale states remain truthful under concurrency.

**Given** Business/IT multi-document questions, follow-ups and sparse or conflicting Knowledge,
**When** real Chat, exact citations, reader/history and manual graph journeys are assessed,
**Then** genuine provisional output, first-text hypotheses, durable completion and same-turn reconnect behave correctly; independent review records explanation usefulness and claim-to-source faithfulness separately from schema/identifier validity,
**And** measure first useful answer and total duration without a fixed five-Document cap or universal latency promise.

**Given** competing Scan/Chat demand, revocation, failures and recovery boundaries,
**When** the integrated product is exercised,
**Then** shared limits/fairness, cleanup, pause/resume versus Chat retry, save/acceptance replay and live composite authorization prevent duplicate work authority, unauthorized disclosure and capacity leaks,
**And** actual provider inputs respect self-hosted organization boundaries and necessary authorized external context; source/data/key continuity and backup/restore proof remain valid, distinguishing provider-disabled restore qualification from authorized normal-operation inference.

**Given** results and gaps,
**When** acceptance evidence is assembled,
**Then** it distinguishes static checks, automated boundaries, actual runtime/provider behavior, recovery proof and independently reviewed quality, with configuration, timings, available usage/cost and unknowns explicit,
**And** missing core behavior returns to its owning story before qualification; no green schema, old artifact or final-story implementation bucket substitutes for the required outcome.

**Scope and traceability:**

- **Prior dependencies:** 17.1–17.3 and Epics 13–16. Own the integrated workload/evidence and narrowly required operational composition only; no new product capabilities, competition rubric, fixed sample names or universal protocol/throughput guarantee.
- **Mapped requirements:** integrated FR-3.4/4.8/12.1–12.4 and earlier journey obligations; NFR-1.1/1.2/2.2/2.3/3.1–3.3/4.1/5.1/5.2; AR-23/25–32; UX-DR19–27/31–41/45–53. Preserve Chinese/accessibility conventions; no new certification, advanced graph, external connector or deferred-feature completion credit.

**Definition acceptance:** Accepted under the user's 2026-09-11 confident-boundary delegation after source-grounded scope/coverage checks. Story 17.4 grants no implementation or runtime authority.

**Epic 17 definition acceptance:** Four definitions accepted under delegated confidence after independent review found no material gap or forward dependency. Coherent protected backup, isolated provider-disabled restore, earlier-owner reuse and honest integrated qualification are explicit. No backup, restore, service or provider action was performed.

**Step 3 completion:** All five epic definitions and 41 story boundaries are accepted, preserving every requirement's current, deferred, historical or original-backlog disposition. Under the user's 2026-09-11 confident-boundary delegation, Continue is selected for final definition validation. This does not select a feature or authorize implementation.
