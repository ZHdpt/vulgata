---
title: "PRD: Vulgata"
status: final
created: 2026-06-12
updated: 2026-09-11
---

# PRD: Vulgata

## 0. Purpose, authority and scope

Vulgata turns source code into a source-grounded, cross-Repository knowledge base explaining technical behavior and business logic. This canonical PRD is finalized under the user-approved [September 11 reconciliation](../../sprint-change-proposal-2026-09-11-generic-product-reconciliation.md), following input reconciliation, independent rubric/cross-document review and editorial checks. It defines target obligations, not implemented or qualified behavior.

Use the [canonical glossary](../../../../UBIQUITOUS_LANGUAGE.md), [architecture entry point](../../architecture.md) and adopted [generic architecture spine](../../architecture/ARCHITECTURE-SPINE.md) together. AD-1–AD-32 own technical decisions; this PRD states product implications. The [exact pre-adoption PRD](../../../history/pre-generic-adoption-2026-09-11/files/docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md) and original [.decision-log.md](.decision-log.md) preserve earlier wording, not competing current instructions.

All 89 original FR and 15 original NFR identifiers remain. Status labels mean:

- **Current:** required by adopted product scope, preserved or revised; not a claim of implementation.
- **Deferred:** explicitly postponed; the ID and retained safeguards remain visible.
- **Original unmet backlog:** an original obligation outside the core redesign, neither newly deferred nor completed by adjacent work.
- **History:** a superseded mechanism or dated activity, preserved rather than re-executed.

Mixed clauses separate current and deferred portions. Replacement Epics 13–17 cover the core redesign, not every V1 obligation. Prompt Workbench, dedicated unknown-library inference and general configurable MCP management need later decomposition or explicit disposition. Their absence from that sequence cannot establish full-V1 completion.

This revision claims no generic runtime, tool/provider readiness, Docker operation, recovery or explanation-quality qualification. Implementation follows governed specification, independent review, applicable planned-change approval, testing and qualification. Documentation approval grants no production, migration, provider or database execution authority.

## 1. Vision and intended users

Business logic is distributed across Repositories, languages and Systems. Understanding a risk evaluation, payment decision or customer journey can require days of code reading and coordination. Vulgata gives business staff and developers a navigable explanation of what available source supports, where evidence comes from and what remains unknown.

The product supports independent Scans of one, several or standalone Repositories. Local explanations are useful, but local-only delivery does not fulfill the Cross-Repository goal. System membership organizes access and browsing; it neither proves communication nor restricts resolution to a fixed pair or one System. Source-backed relationships are not claims that a path actually executed in deployment.

- **JTBD-1 — Understand behavior:** ask questions, read explanations and follow permitted references. Business Mode emphasizes business meaning; IT Mode adds technical detail and exceptional source inspection.
- **JTBD-2 — Supply knowledge:** onboard Systems/Repositories, configure providers and advisory context, initiate Scans, understand progress/gaps and explicitly recover interrupted work.

Existing journeys remain: Lin the PM asks about risk evaluation in Business Mode; Chen the new analyst explores Business Logic Documents; Wang the developer follows technical evidence in IT Mode; Zhang the System Owner onboards Repositories and starts Scans. None requires competition samples, maintained service mappings or a mandatory question inbox.

## 2. Concepts and preserved experience

The canonical glossary owns definitions. These distinctions organize requirements:

- A Repository Scan Run fixes one Source Snapshot. System/selection actions may aggregate independent runs through a Scan Campaign, not pair-wide correctness.
- A file workstream is not necessarily a Code Unit. Pass 1 discovers Code Units and their Code Logic Documents; stable Logical Documents have immutable Document Versions.
- Cross-Repository Resolution evaluates observations and records Edges and Uncertainties. Later accepted knowledge can resolve gaps without re-analyzing unchanged source.
- A Logical Flow identifies a source-backed root. Its Flow Group Version selects bounded evidence for Pass 2's Business Logic Document and exact GENERATED_FROM dependencies.
- Operational progress, primary-source coverage and knowledge currentness differ. Accepted history survives failure, cancellation, replacement and workspace cleanup.

Preserve Simplified Chinese-only V1 UI, localized errors, Library Oak visual language, Business light/IT dark themes, chat-first login, the distinct management surface and established keyboard/basic accessibility behavior. This is scope reconciliation, not visual redesign. Reader placement must not turn ordinary Knowledge access into management permission.

## 3. Functional requirements

### 3.1 Authentication and user management

- **FR-1.1:** [Current] Users shall register with email and password.
- **FR-1.2:** [Current] Users shall log in and log out.
- **FR-1.3:** [Current] Users shall view/edit their profile, including display name, email and password change.
- **FR-1.4:** [Current] Retain Administrator, System Owner and User roles. Administrators assign roles; System Owners manage authorized Systems; Users consume permitted Chat/Documents. Self-service role requests and new fine-grained RBAC are outside scope. Knowledge, history and source disclosure shall enforce current server-side authorization.

### 3.2 System and Repository management

- **FR-2.1:** [Current] System Owners shall create/edit/delete authorized Systems with name, description and optional context. Organizational changes do not themselves prove or disprove communication.
- **FR-2.2:** [Current] System Owners shall add Repositories with name, description, Git remote and optional context, without fixed-pair, sample-name or release-profile prerequisites.
- **FR-2.3:** [Current] System Owners shall remove Repositories from Systems. Membership/visibility changes preserve immutable history and current access rules; they shall not silently replace accepted relationship evidence.
- **FR-2.4:** [Current] System Owners shall create standalone Repositories for organization-owned shared-library source and explicitly Scan them through the ordinary static pipeline.
- **FR-2.5:** [Current] Repository creation includes best-effort Git reachability validation; authentication failure does not prevent creating the record. A later Scan must acquire/verify exact authorized source or report its prerequisite failure.

### 3.3 LLM Provider management

- **FR-3.1:** [Current; revised] Administrators shall maintain the global provider registry and default task assignments, including provider names, endpoints, protected keys and supported API types. Preserve chat-completions, responses and messages configuration vocabulary without implying every adapter is qualified. PreScan model work is bounded advisory overview, not deterministic inventory or scheduling.
- **FR-3.2:** [Current] Multiple providers shall be configurable simultaneously. This does not imply automatic switching or silently changing a running task's provider identity.
- **FR-3.3:** [Current; revised] System Owners shall assign providers per System to permitted overview, Worker Agent and Chat Agent roles using the global registry. Different roles and different Systems may use different providers; deterministic Scan Control needs no model assignment. Configuration remains usable outside demo composition.
- **FR-3.4:** [Current] Administrators shall explicitly test provider connections. Routine health checks shall not incur paid inference; provider availability remains separate from application, database and analysis-tool readiness.

### 3.4 Scan and knowledge production

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

### 3.5 Cross-Repository Communication Detection

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

### 3.6 Resolution, uncertainty and knowledge refresh

- **FR-6.1:** [Current; revised] Record Uncertainties for missing/ambiguous targets, bindings, mechanisms and capabilities, retaining known/unknown-target distinctions where meaningful. Workers propose; Knowledge owns accepted evidence/state. Valid semantic uncertainty may accompany accepted analysis.
- **FR-6.2:** [Current; revised] Reconcile finite pinned accepted evidence across authorized discovery, including other Systems, late targets and accepted files of unfinished external runs. Select a designated analysis candidate per Repository, not arbitrary history. A unique visible candidate, shared name or confidence alone cannot prove a match. Focused bounded inquiry gathers supporting/opposing evidence; domain validation decides relationships. Deduplicate unchanged inquiries and recheck access/bindings before acceptance; publication cannot recursively trigger investigation.
- **FR-6.3:** [Current; revised] Preserve cycle safety without historical fewer-pending-first/older-scan-yields waiting. Record cycles, terminate bounded traversal and give every selected matching item an explicit outcome. Unknown targets shall not hold Worker capacity indefinitely or create global completion barriers.
- **FR-6.4:** [Current / Deferred] Preserve nonblocking Dangling Links, available identifiers and historical human wontfix decisions. Defer new owner accept/dismiss/wontfix actions with product HITL; uncertainty visibility/history remain current.
- **FR-6.5:** [Current] Provide Chat-accessible unresolved-link explanations/counts, with endpoint/target information only where permitted by evidence, mode and live authorization. Aggregates must not leak restricted facts.
- **FR-6.6:** [Current; revised] Show stale/pending/failed refresh. Changed accepted evidence may trigger bounded reconciliation and coalesced per-flow refresh without source re-analysis; active inputs stay fixed. Publication shall not create endless refresh. Scope narrowing/access loss alone changes visibility or records unevaluated scope, not broader accepted relationship truth. Automatic source-Scan cascades remain outside current scope.
- **FR-6.7:** [Deferred] Incremental changed-source Impact Analysis/reuse, including the former four-stage cascade, remain deferred. Static replacement/retirement and later-knowledge reconciliation are current, not completed incremental scanning.

### 3.7 Human input

- **FR-7.1:** [Deferred] Worker question notifications and mandatory owner response are deferred. Visible source-backed Uncertainties may state missing information without creating an inbox or blocking ordinary analysis.
- **FR-7.2:** [Deferred] Dashboard answering and answer attachment are deferred; no automatic repair workflow is substituted.
- **FR-7.3:** [Current] Identify human-supplied context and give it lower authority than code-derived facts. Preserve the distinction in Documents, prompts and dialogue; conflicts shall not silently turn assertions into source-qualified knowledge.
- **FR-7.4:** [Deferred] Pending-question counts and view/answer/dismiss UI are deferred. Operational status, pause/resume, authorization and development approvals remain.

### 3.8 Third-party libraries

- **FR-8.1:** [Current] Organization-owned library source is addable as ordinary standalone Repositories and eligible for explicit static Scan.
- **FR-8.2:** [Deferred] Caller/Worker-triggered partial on-demand library Scans are deferred. Missing source shall not cause implicit dependency downloads or arbitrary Repository work.
- **FR-8.3:** [Deferred] Multiple-caller library-Scan coordination is deferred with that feature. Ordinary Repository/file ownership and resource controls remain mandatory.
- **FR-8.4:** [Current; revised] Common library imports shall not automatically become cross-Repository communication gaps. Familiarity does not prove all behavior; decisive unsupported behavior remains a limitation or labelled hypothesis.
- **FR-8.5:** [Original unmet backlog / Deferred] Retain dedicated inference for unknown source-unavailable libraries outside the core redesign. Ordinary useful hypotheses do not complete it, and inference is not source proof. Mandatory owner notification is deferred with HITL; external-information lookup is not implied.

### 3.9 Git monitoring and incremental Scan

- **FR-9.1:** [Deferred] Automatic remote monitoring remains deferred; periodic polling is the retained original direction, not a current background task. Webhook triggers remain excluded.
- **FR-9.2:** [Deferred] Automatic pull-on-behind is deferred. Explicit static acquisition still resolves a selected Git ref once and shall not change an existing run's source.
- **FR-9.3:** [Deferred] Incremental re-scan of changed/dependent Code Units and analysis reuse remain deferred. Repeated explicit static Scans and late-knowledge reconciliation remain current.
- **FR-9.4:** [Current] Previous Document Versions and original dependencies remain accessible under authorization. Reader/history/citation navigation is current minimum product, independent of incremental scanning.
- **FR-9.5:** [Deferred] The incremental-Scan overlap rule stays with deferred update scheduling. Current static runs follow exclusive ownership and ordered publication, not an unintended blanket prohibition inherited from an incremental-only clause.

### 3.10 Database connections

- **FR-10.1:** [Current] Preserve per-Repository database type, connection details and protected credentials managed by authorized System Owners. Administration does not grant new agent-facing access.
- **FR-10.2:** [Deferred] New Worker database-schema inspection is deferred with external operational-information tools. Available source/accepted knowledge remain the investigation boundary; no placeholder tool is introduced.
- **FR-10.3:** [Deferred] New sample-data query tools are deferred. Any preserved/future surface retains limited rows, non-production/read-only access and blocked writes. Production database access is not authorized.

### 3.11 Chat and knowledge consumption

- **FR-11.1:** [Current; revised] Business Mode provides business narratives in the light theme and reads Business Logic only. Enforce this in tools, history, summaries, graphs and citation targets, not just prompts. Useful Business Logic references remain available without Code Logic/source disclosure.
- **FR-11.2:** [Current; revised] Dark-themed IT Mode permits authorized Business Logic, Code Logic and technical references. Exceptional read-only source inspection may address specific gaps at the recorded Profiled Revision; Chat shall not routinely re-prove the wiki, initiate Scans or repair knowledge.
- **FR-11.3:** [Current; revised] Confirm mode switches, save the authorized session and start a new session with the selected theme/prompt and fresh inference context. Mode/scope changes shall not import disallowed technical history or cached summaries. Preserve durable ordered conversations and bounded useful completed history separately from model context. Summaries retain hypothesis status and contributor access scope, never upgrade speculation. Initially permit one active answer per conversation; replay shall not generate twice.
- **FR-11.4:** [Current; revised] Users select authorized Systems/Repositories; first use defaults to all accessible Systems and permits adjustment/clearing. Knowledge provides a stable published read selection per turn, including discovery, pagination and later-discovered Documents. New publication alone does not invalidate that turn; later turns use fresh permitted knowledge. Authorization stays live.
- **FR-11.5:** [Deferred] Chat file/image upload, attachment storage and parsing are deferred. Typed questions, dialogue and permitted advisory text remain.
- **FR-11.6:** [Current; revised] Synthesize useful multi-document/follow-up explanations through shared wiki search, browse, section/content and existing-related-Document navigation. Tools return provenance, coverage and Uncertainties under the same turn/mode/access context. Retrieval is evidence, not instructions or infallible truth. Do not mandate per-record tools, fixed candidate sentences, embeddings or index.md input. Shared code alone does not prove business dependency.
- **FR-11.7:** [Current; revised] Provide useful claim-linked references to exact permitted Document Versions/sections and appropriate IT source locations. Show genuine provisional deltas/concise progress, never hidden reasoning/secrets; label hypotheses from their first text. Completion requires valid provider completion, no truncation/unfinished tools and one durable content/reference/status save, not mandatory complete-candidate JSON or whole-answer semantic verification. Preserve useful authorized interrupted text with its reason, not as normal completed factual history. Never automatically restart generation after visible output. Browser disconnect is not cancellation: host-owned work may continue within original time/cost limits, and authorized reconnect joins the same turn without duplicate generation or lost/duplicated text. Host failure interrupts orphaned turns and requires explicit retry. Revocation stops further unauthorized disclosure; references retain original versions, and history/summary reuse rechecks contributing access.
- **FR-11.8:** [Current; retained original behavior] When selection is empty, determine relevant Systems/Repositories using retrieval signals inside server-approved authorized discovery and display scope for adjustment. Inferred relevance shall not grant the model authority to choose access scope.

### 3.12 Scan progress and knowledge exploration

- **FR-12.1:** [Current; revised] Show real per-Repository operational status and System/selection aggregates separately from primary coverage and knowledge freshness. Include queued, executing, paused, cancelled and terminal outcomes with gaps. Users need no separate Pass 1/resolution/Pass 2 start calls. Recovery distinguishes explicit Scan resume from Chat retry.
- **FR-12.2:** [Current] Show real active Worker counts for permitted work, without fictional progress or multiplying per-run limits into uncontrolled global totals.
- **FR-12.3:** [Current; revised] Show admitted primary files and accepted/pending/failed coverage. File, semantic Code Unit and Document counts differ; sample quotas or completed-job counts are not coverage proof.
- **FR-12.4:** [Current] Show errors, classified failures and accessible warning/failed-file summaries. Missing analysis capabilities need not prevent reading existing knowledge. Production status uses real state, never demo fixtures.
- **FR-12.5:** [Deferred] Dashboard HITL answer/dismiss UI is deferred; retain Uncertainty disclosure and operational controls.
- **FR-12.6:** [Current / Deferred] Provide an authorized Document reader, exact cited sections and basic manual-refresh Logical Flow/Document-neighborhood graph bound to versions/evidence. Enforce mode/user scope server-side for nodes, relationships, labels, counts and tooltips. Show stale, partial and unavailable-source states; sparse graphs are valid. Layout interaction cannot edit domain Edges. Live animation/advanced portfolio visualization are deferred, not Scan progress or genuine Chat streaming.

### 3.13 MCP integration

- **FR-13.1:** [Current / Original unmet backlog] Native CodeGraph MCP consumption is required under application-selected source/tool authority. General Repository/System/global registered MCP consumption remains distinct original backlog outside the core redesign; this adapter neither fulfills it nor authorizes arbitrary tools. Preserve original low priority without treating it as approved deferral.
- **FR-13.2:** [Original unmet backlog] General externally configured MCP consumption in Chat remains original backlog. Internal wiki tools and exceptional scoped IT source reads do not complete it or add external operational-information authority.
- **FR-13.3:** [Current] Scan and Chat tool use require separate approvals/scopes. Scan approval never implies Chat approval or Business access; enforce purpose per invocation.
- **FR-13.4:** [Deferred] Outward wiki MCP serving is deferred, without an endpoint or placeholder. Internal wiki tools/native CodeGraph MCP consumption remain current.
- **FR-13.5:** [Original unmet backlog] Retain three-level Repository/System/global configurable MCP management with purpose-specific approvals. Later design/decomposition is required; native tools provide neither implicit execution authority nor completion credit.

### 3.14 User-supplied context

- **FR-14.1:** [Current] Preserve authorized global advisory context applying to Systems, without broadening global-management permissions.
- **FR-14.2:** [Current] Preserve System advisory context for its Repositories, without making membership communication evidence.
- **FR-14.3:** [Current] Preserve Repository advisory context, including standalone Repositories.
- **FR-14.4:** [Current; revised] Active Scans retain selected context; edits during affected work queue for later application rather than silently changing pinned analysis. Deferred HITL supplies no current mutation exception. Chat histories/summaries/mode contexts have separate access and selection rules and cannot mutate Scan evidence.

### 3.15 LSP support

- **FR-15.1:** [Current; revised] Provide applicable declared LSP navigation alongside required CodeGraph/source access. Initial offline/local-source operation is temporary and disables automatic imports/builds/dependency restoration; it is neither permanent local-only topology nor network air-gapping. Readiness does not imply semantic completeness.
- **FR-15.2:** [Current; revised] Available type/reference information may augment understanding with explicit capability/classpath limitations. Missing tool results are not proof that a relationship does not exist.
- **FR-15.3:** [Current; revised] Profiles are application-selected per Repository; models cannot select arbitrary adapters, snapshots or physical workspaces. Initially allow at most one LSP session per snapshot. Dependency-aware imports/restores and multiple sessions are later scope requiring separate authorization/qualification.

## 4. Non-functional requirements

### 4.1 Performance and resource behavior

- **NFR-1.1:** [Current; revised] Qualify Scan duration, coverage and resource use against representative Repositories/configurations, including progressive large files. No fixed sample quota or competition preparation deadline applies. Configured limits may queue, pause or honestly fail work, never silently exclude source; one machine's measurements are not universal throughput guarantees.
- **NFR-1.2:** [Current; revised] Measure first useful answer delivery, total duration and multi-document/follow-up usefulness under declared provider/configuration limits. Replace the universal 30-second/five-Document guarantee with environment-qualified results and bounded execution. Five Documents is not a retrieval cap; no replacement universal latency promise is invented.
- **NFR-1.3:** [Deferred] The five-second live-graph target is deferred with animation. Real Scan status and Chat streaming retain measured responsiveness/correctness obligations.

### 4.2 Security

- **NFR-2.1:** [Current] Hash passwords using a modern algorithm such as bcrypt or Argon2.
- **NFR-2.2:** [Current] Encrypt provider/database secrets at rest. Preserve required Data Protection keys across replacement and coherent backup/restore; credentials must not enter prompts, logs or diagnostics.
- **NFR-2.3:** [Current; strengthened] With self-hosted providers, source/Documents stay inside organization infrastructure; external providers receive only necessary authorized task context. Bind tools/source to granted scope, budgets and provenance. Recheck live authority over every composite contributor, not merely displayed citations, before publication/disclosure. Restricted histories, counts and paths cannot leak through otherwise permitted output. Agents cannot choose credentials, arbitrary host paths or domain writes.
- **NFR-2.4:** [Current] Database tools remain non-production/read-only-replica only, with writes blocked. This survives tool deferral and binds preserved or later authorized surfaces.

### 4.3 Reliability and recovery

- **NFR-3.1:** [Current; revised] Isolated Worker failures shall not collapse unrelated work. Distinguish integrity/security failures, recoverable job errors and provider outages; use bounded classified retries, not universal once-and-continue. Accept no partial failed-file drafts as knowledge. Expose gaps and settle owned work/capacity on cancellation, timeout or lost ownership.
- **NFR-3.2:** [Current; revised] Temporary Scan-provider failures receive bounded increasing retries then durable pause; configuration failures require correction. Explicit resume rechecks prerequisites, source and authority. Restart shall not resume paused work or silently switch provider/source. Shared provider admission covers Scan/Chat, but Chat follows its distinct interruption/completion rules and never silently restarts a visible answer.
- **NFR-3.3:** [Current; revised] Preserve authoritative Scan progress, accepted knowledge, source and durable Chat history across restart. Replayed acceptance/save acknowledgements shall not duplicate knowledge or generate again. Browser reconnect reuses existing bounded work; host failure interrupts orphaned Chat and leaves Scan explicitly recoverable. Recovery promises neither zero repeated external billing nor transparent per-token replay.

### 4.4 Deployment and operations

- **NFR-4.1:** [Current; revised] Deploy two Compose services: Vulgata with bundled local analysis tools, and PostgreSQL. Declare tool versions/dependencies and portable storage/provider/Worker/tool limits without host-profile dependencies or an extra remote tool bridge. Preserve database data, required source and keys apart from disposable workspaces/caches. Initially do not auto-prune canonical retained source; clean only unleased disposable state. Provide coherent operator maintenance-window backup/restore of data, source, keys and restore configuration outside primary volumes; volumes alone are not backups. Isolated restore qualification disables provider dispatch, invalidates old ownership and prevents automatic replay of saved running work. Recheck source/access before explicit resume/retry.

### 4.5 Usability and maintainability

- **NFR-5.1:** [Current] Nontechnical users shall log in, select permitted context and understand Business answers without training/documentation. Assess explanation usefulness and citation faithfulness separately from schema validity or reference identity.
- **NFR-5.2:** [Current] Preserve Blazor's basic accessibility facilities and established accessible interactions. No new WCAG certification/compliance commitment is introduced; remove the expired competition rationale.
- **NFR-6.1:** [Current] Prompts remain externalized configurable text, never hardcoded strings. Native agent-framework adoption does not change this.
- **NFR-6.2:** [Current; revised] Document supported catalog/advisory-context formats so owners contribute framework knowledge without developer assistance. Do not require detailed service mappings or promise arbitrary executable dialect extensions through configuration.

## 5. Success measures and acceptance boundaries

- **SM-1 — Generic understanding:** prove real one/several/standalone Repository operation and source-backed Cross-Repository explanations, including beyond-pair contracts, late targets, one-to-many cases and cycles. Local synthesis is intermediate, not sufficient completion.
- **SM-2 — Scan quality:** nontechnical reviewers understand Business Logic; technical reviewers trace representative claims/unknowns to exact accepted evidence. Schema correctness alone is insufficient.
- **SM-3 — Relationship accuracy:** accepted links identify supported providers/consumers correctly; missing bindings/incomplete tools remain visible. Low Uncertainty counts are not a quality goal by themselves.
- **SM-4 — Chat usefulness:** representative multi-document/follow-up questions yield understandable mode-appropriate explanations, useful exact references, truthful gaps and real streaming. Qualify context switches, revocation, reattachment, interruption and save replay.
- **SM-5 — Incremental update:** remains deferred; repeated static Scans and later-knowledge reconciliation do not prove it.

Record declared source/tool/provider configuration, durations and available usage/costs; unknown usage is not zero. Track failed/pending coverage, stale refresh and unresolved frontiers without optimizing them away through exclusion. Separate application liveness, database readiness, Scan-tool readiness and provider availability using sanitized correlation/status information and no paid routine health calls.

Qualify real declared tools, retained source, bundled Docker, coherent restore, parallel admission/cleanup, durable recovery, compatible version selection and authorized publication/history reads. Explain useful content quality independently of schema and structural checks. Existing tests/checkpoints may support reuse but cannot alone qualify changed contracts. Current design adoption supplies no runtime evidence.

## 6. Boundaries and future decisions

Initial acquisition uses committed Git. Additional origins, dependency-aware import/build/restore, private mirrors, multiple LSP sessions per snapshot, separate-business-root merging, automatic whole-Repository expansion and update triggers are later scope. No remote analysis bridge, local LLM deployment, distributed scheduler, privileged Docker control, HA/PITR service or automatic historical-source pruning is required.

Existing exclusions remain: enterprise SSO, new fine-grained RBAC, production database access, webhook monitoring, formal WCAG compliance, automated compliance reporting, separate Audition Agent, dedicated answer-confidence scoring, gRPC-specific guaranteed detection and shared-database communication detection. Normal authorization/scope safety is mandatory, not an optional “chat boundary guards” feature. Generic unfamiliar-mechanism/source recording does not promise universal language/protocol support.

No unresolved product choice blocks this approved reconciliation. Owning plans settle detailed identity/version/lifecycle contracts, configured defaults, budgets, retention procedures and fixtures before dependent work splits. Detail cannot reintroduce fixed pairs, excluded source, compulsory human mappings, inaccessible readers or competing acceptance owners. Retained original backlog requires later explicit design, not automatic dispatch or false full-V1 completion.

## 7. Historical scope and supersession

Preserve historical decisions through the archive and dated proposals; dates alone do not establish blanket precedence:

- [August 17 recovery](../../sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md) temporarily replaced specified clauses with Epic 12's Java/XML pair release scope while retaining longer-term backlog.
- [August 18 host-runtime amendment](../../sprint-change-proposal-2026-08-18-august-demo-host-runtime-and-jdtls-pin.md) changed deployment proof/tool pins only, not every Scan/evidence rule.
- [August 29 Track C-MVP](../../sprint-change-proposal-2026-08-29-story-12-5-track-c-mvp.md) simplified release-proof machinery without weakening then-accepted Track A/B behavior.
- [August 31 termination/integration baseline](../../../implementation-artifacts/epic-12-post-termination-chat-integration-baseline-2026-08-31.md) preserves selective Chat hardening and Story 12.5's terminated-incomplete status. It neither authorized the later streaming replacement nor completed real release qualification.
- [September 1 decommission](../../sprint-change-proposal-2026-09-01-cgc-executable-decommission.md) retired Epic 12's active deadline authority, removed executable CGC and preserved useful assets. Compilation-only acceptance applied solely to cleanup; PRD/architecture decisions awaited this correction.
- September 11 approval adopts the finalized generic design and replaces active unstarted backlog. Preserve completed Epics 1–4, Epic 11 Stories 11.1–11.4, incomplete Story 11.5, accepted Epic 12 Stories 12.1–12.4 and terminated-incomplete Story 12.5 without rewriting their evidence.

Competition dates, judging rubrics, samples, quotas, mandatory mock topology, Windows-host exception, screenshot ceremonies and attestation machinery are not current acceptance. The former §0.1/§5.16 overlays remain archived, not active fallback requirements. Generic design explicitly replaces preallocation, superstep waiting, deadlock yielding, code-change-only regeneration and full-answer-before-reveal Chat; immutable history/security survive those mechanism changes.
