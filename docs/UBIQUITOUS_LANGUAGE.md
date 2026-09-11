# Ubiquitous Language

## Authority and adoption — 2026-09-11

This is the single canonical domain glossary for the approved generic Scan, Knowledge and Chat target. It accompanies the [current PRD](bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md) and [adopted architecture spine](bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md), under the [approved reconciliation proposal](bmad/planning-artifacts/sprint-change-proposal-2026-09-11-generic-product-reconciliation.md). These definitions do not claim current implementation or runtime qualification. Detailed schemas and algorithms belong to their owning feature contracts, not this glossary.

The [pre-adoption glossary](bmad/history/pre-generic-adoption-2026-09-11/files/docs/UBIQUITOUS_LANGUAGE.md) preserves earlier wording. Historical terms below retain their old contract meaning; their presence in persisted records does not make them generic requirements. There is no competing `CONTEXT.md` glossary.

## Organization and source

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **System** | An organizational grouping of Repositories. Membership is neither communication evidence nor the compulsory boundary of Cross-Repository Resolution. | Communication Scope |
| **Repository** | A registered source-code repository, optionally organized under a System. A standalone Repository, including source-available library code, is eligible for an ordinary Scan. | System |
| **Profiled Revision** | The exact full Git commit identity fixed for a Repository Scan Run. A branch or tag is resolved once; subsequent work and source references retain that committed revision. | Current Revision, Working-Copy Revision, Snapshot Version |
| **Source Snapshot** | The retained exact-source capability for a Repository at its Profiled Revision, providing authorized metadata and reads. It is transparent source access, not another public version identity. Reopening identical bytes creates no new source version. | Source Version, Snapshot Version, Working Copy |
| **Tool Workspace** | A contained, replaceable working area for trusted tools to inspect a Source Snapshot. Tool state is separate from canonical source and does not define its identity. | Source Snapshot, Canonical Source |
| **Source Anchor** | A trace from a knowledge claim to its Repository, Profiled Revision and Code Unit/file/range. Public locations are Repository-relative, not host paths. | Workspace Path |
| **Source Anchoring** | Preservation of exact source locations supporting knowledge and its history. Source availability is separate from a Document Version's continued existence and provenance. | A particular parser or reverse-index schema |
| **Scan Context Snapshot** | Immutable advisory Global, System and Repository context captured for a Repository Scan Run. It aids interpretation but is not source proof; later context edits do not alter pinned inputs. | Source Snapshot, Mutable Scan Context |

## Scan and Agents

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **Scan** | The user-requested source-to-knowledge workflow for exactly one Repository. A System or selection action starts independent Repository Scan Runs, not a shared correctness barrier. | System-level Scan, Pair Scan |
| **Repository Scan Run** | One durable Scan execution bound to one Source Snapshot, with its own work, coverage, recovery and recorded current-promotion or history-only intent. | Paired Scan, Global Scan |
| **Scan Campaign** | Optional batch controls and aggregate progress for independent Repository Scan Runs. It does not own their source or semantic acceptance. | Multi-Repository Scan identity |
| **Scan Control** | The deterministic owner of Scan requests, plans, jobs, progress, fair bounded dispatch and explicit pause/resume/cancel behavior. It dispatches model work but is not an LLM coordinator. | Orchestrator Agent, Magentic Orchestrator |
| **PreScan** | Preparation that records source inventory, format/language/layout facts and unknowns, and establishes required analysis-tool readiness. It neither preallocates semantic Code Units nor determines communication truth. | Semantic Extraction, Complete Coverage Proof |
| **Repository Profile** | The factual inventory and structural description of a Repository's exact source used to plan analysis. Unknowns remain explicit. | Semantic Model, Advisory Overview |
| **Advisory Overview** | A bounded model-assisted interpretation of structure and supplied context, separate from profile facts. Its hypotheses may influence ordering, not remove admitted source or prove coverage. | Confirmed Profile, Coverage Authority |
| **Scan Work Plan** | The explicit policy-based selection and organization of source work. Primary source requires a truthful outcome; supporting source may supply evidence without becoming a Code Logic generation job. | Prepared Scan Manifest |
| **File Workstream** | One logical Pass 1 analysis of a complete admitted file, continued in bounded windows if needed. Neither a file job nor a window is automatically a Code Unit or Document identity. | File Code Unit, Semantic Chunk |
| **Code Unit** | A meaningful source element discovered by analysis, such as an operation, handler or declarative construct. Each owns a Code Logic Logical Document. Units may nest or share anchors; folders and moving ranges do not establish identity. | File Job, Source Window, Preallocated Symbol |
| **Worker Agent** | An LLM agent dispatched for bounded file analysis, focused resolution investigation or Flow synthesis using authorized tools/evidence. It proposes results; it neither accepts Knowledge nor schedules arbitrary Repository work. | Scan Coordinator, Semantic Authority |
| **Pass 1** | File-based analysis that discovers Code Units and proposes their Code Logic Documents, communication observations and Uncertainties. Other source can supply context; validated drafts are not accepted knowledge. | One Agent Call per Code Unit |
| **Accepted File Manifest** | The accepted exact-version result set for a file: its Code Units, Code Logic Documents, observations and truthful coverage disposition. Only accepted results become reconciliation evidence; failed partial drafts do not. | Draft Manifest, Pre-Scan Manifest |
| **Analysis Checkpoint** | A declared point at which accepted file results and explicit gaps can form a selected Code Logic corpus. Root-run static Flow synthesis also requires terminal outcomes for every admitted primary item; paused work is not terminal coverage. | Entire Portfolio Completion |
| **Pass 2** | Synthesis of a supported Business Logic explanation from a pinned Flow Group Version. Additional authorized evidence actually used enters accepted dependencies; synthesis does not create authoritative relationships. | Remote-call-only Generation |
| **Flow Synthesis Job** | Bounded Pass 2 work explaining one fixed Flow Group Version, including conditions, alternatives, effects and unknowns. | New Scan, Relationship Acceptance |
| **Concurrency Control** | Fair, bounded sharing of Scan/Worker capacity, per-run admission, shared Scan/Chat provider capacity and safe tool capacity. Independent ready work can progress; waiting for an unscanned target is not active Worker work. | Rate Limiting alone, Superstep |

## Knowledge, Documents and Logical Flows

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **Knowledge** | Accepted Code Units, Documents, relationship evidence, Uncertainties and their version-bound selections. Knowledge owns semantic acceptance and read selection; model drafts and conversation text are not wiki knowledge. | Agent Output, Chat History |
| **Document** | A generated Code Logic or Business Logic explanation. Use Logical Document for enduring identity and Document Version for particular immutable content. | Mutable Wiki Page |
| **Code Logic Document (CL)** | The explanation owned by one Code Unit, grounded in exact source. A file may contain several Code Units and therefore several CL Logical Documents. | File Document, Whole-Repository Summary |
| **Business Logic Document (BL)** | The explanation owned by one Logical Flow, synthesized from exact accepted dependencies. It may describe local or Cross-Repository behavior and preserves supported alternatives and gaps. | Repository Summary, Proven Runtime Trace |
| **Logical Document** | A stable semantic Document identity: one Code Unit's CL or one Logical Flow's BL. It is distinct from its immutable Document Versions. | Mutable Document, Flow Group Version |
| **Document Version** | One immutable generated content version of a Logical Document. Selection determines what is current; older versions and their exact dependencies remain historical. | Profiled Revision, Source Snapshot |
| **Version Chain** | The preserved succession of a Logical Document's immutable versions. Supersession changes selection, not earlier content or provenance. | Overwritten History |
| **Logical Flow** | A distinct source-backed business-flow identity rooted in an exported operation, handler, schedule, declarative workflow or selected Code Unit, initially homed at its root Repository. Local-only flows qualify; shared helpers, folders and call cycles do not merge business purposes. | Flow Group Version, Folder, Connected Component |
| **Flow Group Version** | One immutable bounded directed evidence slice for a Logical Flow: selected members and exact versions, branches, relationships and uncertain frontiers. It supports BL synthesis, not a claim that every branch executes in one runtime sequence. | Logical Flow, Runtime Trace |
| **Knowledge Publication** | Atomic promotion of a coherent per-Logical-Flow selection, including BL version and exact evidence dependencies. Relationship/Uncertainty selections also have publication identity independent of Document text. This is not global revision synchronization. | Portfolio Barrier, Document Save alone |
| **Knowledge Read Context** | One version-bound authorized selection for discovery, membership, pagination, content and citations. It may represent current selections or explicitly labelled history; it is not a wiki copy. | Current Graph, Cached Documents only |
| **Knowledge Currentness** | Whether selected knowledge reflects desired accepted evidence, or is pending, stale, failed, partial or retired. Operational Scan completion and primary-source coverage are separate facts. | Scan Percentage, Latest Finish Time |
| **Retirement / Withdrawal** | Removal of obsolete current eligibility when validated replacement-source evidence establishes absence. History survives; failed/unfinished analysis, exclusion and model omission alone do not prove deletion. | Deletion of History |

## Relationships, Uncertainties and reconciliation

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **Communication Observation** | A source-backed description of an interaction's owner, role, mechanism, operation, literals/expressions, namespace, evidence status and provenance. Unknown mechanisms and missing bindings remain explicit. | Resolved Endpoint, Deployment Fact |
| **Edge** | A typed directed relationship between exact Document Versions, viewed through a selected relationship/evidence version. Relationship history is preserved independently from Document text. | Untyped Link, Inferred Runtime Event |
| **REFERENCES Edge** | An accepted reference or dependency relationship, within or across Repositories. It states its supported meaning; source linkage does not prove runtime execution. | Every Edge is a Call |
| **GENERATED_FROM Edge** | Exact provenance from a BL Document Version to the CL Document Versions used in synthesis. Historical targets never retarget to today's CL or graph. | Current-source Shortcut |
| **Cross-Repository Edge** | A REFERENCES Edge whose source and target belong to different Repositories, whether or not they share a System. It is not another Edge type. | Cross-System Edge, Cross-Repo Edge |
| **Uncertainty** | An explicit unresolved, ambiguous, missing or conflicting knowledge claim with evidence and limitations. It can coexist with accepted analysis; it is not a fabricated Edge or mandatory owner question. | Error, Assumed Relationship |
| **Dangling Link** | An unresolved Cross-Repository reference whose target cannot currently be established. It does not block Scan completion and may become resolvable when evidence arrives. | Permanently Unresolvable Link |
| **Cross-Repository Resolution** | The domain activity of establishing or revising supported Cross-Repository relationships from accepted evidence. It includes unresolved outcomes rather than guessing missing identity. | Cross-System Resolution, Post-Scan-only Matching |
| **Resolution Reconciliation** | Bounded Cross-Repository Resolution over a finite pinned set of accepted evidence and authorized candidates. Later accepted knowledge can resolve earlier Uncertainties and refresh affected Logical Flows without re-analyzing unchanged source. | Recursive Repository Scan, Global Deadlock Resolution |
| **Resolution Epoch** | One finite reconciliation round with fixed evidence and scope. Publication does not start another round; later independent inputs may justify one. | Portfolio Transaction, Publication Loop |
| **Resolution Change Set** | Accepted relationship/Uncertainty changes from reconciliation, distinct from rewriting caller CL content. | Source Change Set |
| **Focused Resolution Investigation** | Bounded inquiry into a fixed caller/candidate question using available authorized evidence. Supporting/opposing findings inform acceptance; confidence and unavailable tools prove neither matches nor absence. | Peer Superstep Verification, Arbitrary Repository Discovery |
| **Call Cycle** | A cycle in source-backed directed relationships. Traversal records the cycle and terminates within its bound; independent Repository work does not wait for a cycle-breaking scan order. | Deadlock |
| **Uncertain Frontier** | A boundary where supported Flow traversal ends because evidence, scope or bounds do not establish further behavior. | Invented Target, Complete Trace |

## Chat

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **Chat** | Wiki-first conversational synthesis of useful permitted multi-document explanations. It consumes Knowledge and owns dialogue; it does not initiate Scans, repair Knowledge or treat retrieved text as instructions. | Selected-candidate Replay, Source Scanner |
| **Business Mode** | The light-theme conversational and reader mode restricted server-side to BL knowledge. It excludes CL, source code and technical source metadata from tools, history, previews, citations and graphs. | Prompt-only Business Filter |
| **IT Mode** | The dark-theme mode that may read permitted BL and CL, with exceptional read-only exact-source inspection for specific gaps. Mode choice never grants Repository authorization. | Unrestricted Source Access |
| **Conversation** | Durable ordered dialogue, separate from wiki Documents. Authorized history survives browser/circuit lifetime; initially only one answer is active per Conversation. | In-memory Session |
| **Inference Context** | Bounded permitted history, mode and scope supplied to model execution. Changing mode/scope creates fresh inference context without deleting authorized older dialogue or importing prohibited technical material. | Entire Conversation Transcript |
| **Chat Turn** | One identified user request and answer lifecycle, bound to its originating context and one Knowledge Read Context. Later turns can read fresher Knowledge; earlier references keep original versions. | Every Network Reconnect |
| **Provisional Answer** | Genuine live answer text before durable completion, with hypotheses labelled when first shown. Useful interrupted text retains its reason and is not normal completed factual history. | Verified Answer, Completed-answer Typewriter Replay |
| **Durable Completion** | Valid untruncated provider completion with finished tools and one accepted content/reference/status save. Lost save acknowledgements are resolved from durable state, not new generation; completion does not prove every assertion. | Stream End, Semantic Certification |
| **Reattachment** | Authorized browser reconnection to the same host-owned Chat Turn and its current/saved state without new generation or renewed budgets. Disconnect differs from cancellation and host failure. | Regeneration, Transparent Host Recovery |

## Domain relationships and boundary examples

- A System organizes Repositories; a standalone Repository uses the same Scan workflow. Membership proves no communication and does not limit authorized discovery by itself.
- One Repository Scan Run fixes one Profiled Revision through a Source Snapshot. Public source identity remains Repository and Git commit, not a renamed snapshot version.
- One File Workstream may discover several Code Units. Each owns its CL Logical Document; source windows are work boundaries, not identities. Unproven renames, splits and merges cannot silently reuse identity.
- Each Logical Flow owns one BL Logical Document across System/Repository browsing appearances. Each BL version pins its exact CL, relationship, Uncertainty and direct-source dependencies; old readers never follow today's graph by accident.
- Accepted external files may support reconciliation before their whole Repository finishes. Root-run synthesis still needs terminal outcomes for all admitted primary work and explicit dispositions for matching work in the frozen target. Gaps remain visible without a portfolio barrier.
- Later target evidence may resolve an Uncertainty and coalesce bounded BL refresh. Unchanged caller source need not be scanned again; publication starts no reconciliation loop. Call Cycles are finite graph structure, not the historical Deadlock-Breaking Protocol.
- Narrowing discovery scope or losing access changes visibility or records not-evaluated-in-scope; it does not disprove broader accepted relationship evidence. Authorization remains live over every contributor, including composite prose and metadata.
- History survives workspace cleanup. Current Knowledge and active work retain required exact source, including older contributing revisions; initial policy does not automatically prune canonical retained source. Historical source availability remains explicit and separate from immutable provenance.
- Paused Scan work requires explicit prerequisite-checked resume; restart does not resume it. A disconnected browser may reattach to its still-running Chat Turn; host failure instead leaves interrupted work requiring explicit retry.

## Historical vocabulary — not generic execution requirements

Exact earlier definitions and schemas remain in the pre-adoption glossary and archived planning artifacts. Do not silently rename persisted historical contracts into equivalent generic records.

| Historical term | Preserved meaning and current distinction |
| --- | --- |
| **Scan Coordinator / Orchestrator** | Earlier global/per-Repository workflow service names. Generic ownership is Scan Control; old superstep/pair contracts are not inherited. |
| **Superstep** | Earlier batch of preselected Code Units with peer cross-verification. Generic concurrency is independently eligible bounded work. |
| **Document Pre-Allocation** | Earlier precreated Document slots for filtered units. Generic Pass 1 discovers semantic units and Knowledge accepts their identity. |
| **File Code Unit** | Earlier file/segment scheduling identity, including `CodeUnitType.File`. Historical file Documents are not relabelled as newly discovered semantic-unit Documents. |
| **Prepared Scan Manifest** | Earlier deterministic inventory/selection/classification/handoff contract. It is not the generic Scan Work Plan or Accepted File Manifest. |
| **Pre-Scan Result / Pre-Scan Handoff** | Earlier revision-bound deterministic approval/handoff records, which did not dispatch Workers. Generic preparation is part of one Scan action. |
| **Communication Kind Classification / Evidence Locator** | Earlier prepared-kind classification and bounded locator contracts. Generic observations arise from detailed analysis; preparation is not communication truth. |
| **Two-Repository Scan Coordination** | Earlier exact-two, same-System release coordination. A generic Scan Campaign has no pair-only identity or correctness barrier. |
| **Deadlock-Breaking Protocol** | Earlier fewer-pending/older-scan yield rule. Superseded by finite pinned reconciliation and cycle-aware bounded traversal. |
| **Uncertainty sub_status** | Earlier storage refinement such as `cross_repo`, `unknown_target` and `ambiguous`. These persisted fields are not a mandatory generic lifecycle schema. |
| **File Identity Hash** | Earlier first-4096-byte rename heuristic. It is not sufficient semantic-identity proof or a current rename guarantee. |

## Deferred and retained future vocabulary

| Term or capability | Current disposition |
| --- | --- |
| **Impact Analysis / Two-Phase Change Detection** | Automatic/incremental source-update impact and reuse are deferred. The old SQL cascade, line-overlap implementation and hash heuristic are historical proposals, not domain guarantees. Ordinary explicit static Scans, bounded later-knowledge reconciliation and Document history remain current. |
| **Cross-Repo Stale Notice** | Earlier owner-notification approach. Product notification/HITL actions are deferred; visible Knowledge Currentness and bounded accepted-evidence refresh remain current. Use Cross-Repository in current prose. |
| **Human-in-the-Loop (HITL)** | Product notification/inbox/question/answer-dismiss and human `wontfix` actions are deferred. Visible Uncertainties, lower-authority advisory context, historical decisions, operational controls and development approvals remain. |
| **Partial Library Scan** | Worker/caller-triggered partial analysis is deferred; ordinary explicit standalone Repository Scans remain current. |
| **External Operational Knowledge** | New agent-facing operational lookup/integration is deferred. Missing database/gateway/registry facts remain Uncertainties; existing database configuration is preserved. |
| **Outward Knowledge MCP / Chat Attachments / Advanced Graph** | Outward wiki exposure, file/image Chat uploads and live/advanced portfolio visualization are deferred. Native CodeGraph MCP consumption, internal wiki tools, typed Chat, real streaming, reader/history and basic manual graph remain current. |

Deferral is not completion. Prompt Workbench, dedicated unknown-library inference and general custom MCP configuration/consumption remain separately tracked original capabilities in the PRD; absence from the core generic sequence does not erase them.
