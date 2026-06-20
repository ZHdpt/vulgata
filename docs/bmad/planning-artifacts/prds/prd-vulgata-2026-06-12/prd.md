---
title: "PRD: Vulgata"
status: draft
created: 2026-06-12
updated: 2026-06-16
---

# PRD: Vulgata

## 0. Document Purpose

This PRD defines the V1 scope for Vulgata, an LLM-powered cross-repository business logic extraction platform. It is written for the development team (2 people + AI coding agents), competition judges evaluating the project, and downstream BMad workflows (architecture, epics, stories). The PRD builds on the product brief, PRFAQ, brainstorming session, domain research, and technical research already completed. It uses a Glossary-anchored vocabulary — every domain term is defined once and used consistently throughout. Features are grouped with nested functional requirements (FRs) numbered globally. Assumptions are tagged inline and indexed.

## 1. Vision

Vulgata is a platform that reads source code across multiple systems and repositories, extracts the business logic embedded within, and makes it accessible to everyone in the organization — product managers, compliance officers, new team members, and developers alike. It answers the question that currently takes days of manual code-diving: "what actually happens when a user does X?"

In a large bank, business logic is scattered across dozens of systems in different languages and architectures. A single business flow — a risk evaluation in the mobile app — can touch six or more systems. No single person, and no single document, holds the full picture. Vulgata closes this gap by scanning every repository connected to a business flow, producing structured documents that explain both what the code does and what business rules it implements, and linking documents across system boundaries to form a complete, navigable map.

The V1 demo scope covers two systems consisting of approximately a dozen repositories — including microservices, a web application, and an Android application. The vision is larger: Vulgata becomes the knowledge backbone for the department that builds it, then expands across the organization, and eventually becomes a standard part of the enterprise development lifecycle — where the gap between "what the business thinks the systems do" and "what the code actually does" is closed, and stays closed.

## 2. Why Now

Vulgata is being developed for an internal AI programming competition with a 9-week timeline (June 12 → August 15, 2026). The competition rubric allocates points across four dimensions: innovation and creativity (30 points), technical implementation and complexity (25 points), practical value and deployability (25 points), and user experience and interaction design (20 points). The primary success criterion is a working end-to-end live demo that traces a real cross-repo business flow.

Beyond the competition, three forces make this the right moment: (1) LLM inference costs have dropped to <1 CNY per million input tokens, making agent-based code analysis economically viable; (2) the Microsoft Agent Framework provides production-grade multi-agent orchestration with MCP integration; (3) the industry is shifting from "AI as code generator" to "AI as code understander" — and no existing tool addresses the cross-repository business logic extraction problem that Vulgata targets.

## 3. Glossary

*Every domain term used in this PRD is defined here. FRs, UJs, and SMs use these terms exactly. No synonyms.*

### Core Concepts

- **System** — A logical grouping of repositories that together implement a business capability (e.g., "Mobile Banking," "Risk Assessment"). Purely organizational — used for dashboard grouping and chat context selection. Has no detection significance. Cardinality: 1 system → N repositories.
- **Repository** — A git repository containing source code. Belongs to exactly one system, or can be standalone (for shared libraries not owned by any system). Linked to a remote git URL. The fundamental unit of scanning and detection.
- **Scan** — The process of reading a single repository's source code with LLM-powered agents and producing structured documents. Scans are always per-repository. Scanning all repos in a System is a convenience trigger that queues N independent repo scans. Scans are incremental when triggered by code changes.
- **Code Unit** — The smallest unit of code processed by a worker agent. Typically a function, method, class, or file — determined by CodeGraph/tree-sitter during pre-scan profiling with exact line ranges.
- **Document** — A structured markdown file produced by a worker agent, linked to one or more code units. Immutable — can only change when the linked code changes and is re-scanned. Two classifications: Code Logic Document and Business Logic Document. Organized in a tree structure mirroring the source directory hierarchy.
- **Code Logic Document** — A document describing what code does structurally: architecture, data flow, call chains, framework usage. Readable by developers and AI agents. Generated in Pass 1.
- **Business Logic Document** — A document describing the business rules and processes implemented by code: validation rules, decision flows, regulatory checks, business process steps. Readable by non-technical users. Synthesized from Code Logic Documents in Pass 2.

### Scanning Pipeline

- **Scan Coordinator** — A non-LLM background service that manages the operational lifecycle of scans: queuing requests, enforcing concurrency limits, performing git operations, spawning Orchestrator Agents, and triggering cross-repo resolution after each repo scan completes. Not an LLM agent — deterministic application code.
- **Orchestrator Agent** — An LLM-powered agent scoped to a single repository scan. Dispatches Worker Agents in superstep batches, coordinates cross-verification, handles errors, and triggers post-scan steps (reverse_index rebuild, index.md write, log.md append, cross-repo stale notices).
- **Worker Agent** — An LLM agent dispatched by the Orchestrator to process a single Code Unit (Pass 1) or synthesize Business Logic Documents from Code Logic Documents (Pass 2). Detects cross-repo communication and produces structured output with metadata.
- **Superstep** — A batch of related Code Units dispatched concurrently to Worker Agents, enabling cross-verification within the batch before the next superstep begins.
- **Pass 1** — The first phase of document generation: Code Logic Documents are produced per Code Unit, with intra-repo REFERENCES edges created immediately and cross-repo calls recorded as uncertainties.
- **Pass 2** — The second phase: one Worker per System synthesizes Business Logic Documents from the completed Code Logic Documents, identifying business flows autonomously from the graph structure.
- **Document Pre-Allocation** — Bulk-creating documents rows for every filtered Code Unit before any Worker runs, so all document IDs are known and intra-repo REFERENCES edges can be created immediately. Eliminates dangling references within the same repo.
- **Cross-Verification** — When two Worker Agents process related Code Units in the same superstep, they read each other's draft output and confirm or dispute claims. Disputed edges are kept with lowered confidence (0.5) and rendered as yellow dashed lines pending HITL resolution.

### Graph & Relationships

- **Edge** — A typed, directed relationship between two Documents in the document graph. Stored in a single edges table with edge_type discriminator.
- **REFERENCES Edge** — A doc→doc dependency edge: Document A's content references or depends on Document B. Used for both intra-repo and cross-repo relationships. Cross-repo-ness is discovered at query time when source.repo_id != target.repo_id — not a separate edge type.
- **GENERATED_FROM Edge** — A provenance edge from a Business Logic Document to the Code Logic Document(s) it was synthesized from. Drives two-pass re-scan impact analysis: if a CL Doc changes, all BL Docs with GENERATED_FROM edges to it are flagged stale.
- **Version Chain** — A linked list of document versions via superseded_by_id, preserving full history. Old versions are never deleted (immutability guarantee).
- **Dangling Link** — A cross-repo uncertainty that never resolves — a known unknown. Recorded with available identifying information; does not block scan completion. Acceptable per FR-6.4.

### Cross-Repository Communication Detection

- **Cross-Repository Communication Detection** — A prompt-driven subsystem that identifies when code in one Repository communicates with another Repository. Covers RPC, HTTP APIs, message queues, file-based communication (local filesystem, remote filesystem, OSS, HDFS, etc.), and cross-repo page navigation (in native and web apps). Detection happens during Worker Agent processing (Model A — Worker-embedded). For each detected communication, identifies the provider role and consumer role, tags consumer count per interface, and handles cases where provider or consumer is absent (not in any scanned Repository). Uses the Communication Pattern Catalog, object flow analysis, and service topology resolution. Note: cross-repo page navigation is inherently loose-coupled — provider and consumer identification may be unreliable and is flagged as low-confidence when detected.
- **Communication Pattern Catalog** — A per-system configuration specifying how that System's repositories communicate externally: RPC frameworks, HTTP API patterns, message queue topics, file paths and storage systems, cross-repo navigation patterns. Written by the System Owner. [NOTE: Architecture decision is to move this to per-repo (matching the repo-boundary scan model), but that migration is deferred to a separate design doc. V1 ships with per-system CP as specified.]
- **Provider Role** — The Repository (or external entity) that exposes a communication interface (RPC service, HTTP endpoint, message queue topic, file location, navigable page). Tagged on each detected cross-repo communication.
- **Consumer Role** — The Repository (or external entity) that calls or reads from a communication interface. Tagged on each detected cross-repo communication. Multiple consumers may exist per interface; consumer count is tracked.
- **Service Topology** — The mapping of service codes and communication endpoints to target Repositories. Resolved via an MCP tool that queries a mock service platform.

### Uncertainty & Resolution

- **Uncertainty** — A first-class entity attached to a document when the generating agent cannot determine something with confidence. Four states: resolved (linked to a document in another repo), unresolved/cross_repo (target repo known, waiting for scan), unresolved/unknown_target (no repo identified), wontfix (terminal non-resolution — human decision). Uncertainties are collected and processed by the cross-repo resolution engine.
- **Cross-Repository Resolution** — Deterministic post-processing (not an LLM agent) that runs after a repository scan completes. Matches unresolved cross-repo uncertainties against the newly-scanned repo's documents using repo-qualified endpoint matching. Creates REFERENCES edges for matches. Bidirectional — resolves both outgoing and incoming uncertainties.
- **Deadlock-Breaking Protocol** — When two repos have mutual pending cross-repo uncertainties, the repo with fewer pending resolves first; if tied, the older scan yields. Prevents circular dependency stalls.

### Re-Scan & Impact

- **Impact Analysis** — A 4-step SQL cascade that finds all Documents affected by a set of changed source files: direct impact (reverse_index) → intra-repo transitive closure (recursive CTE) → cross-repo reachability → uncertainty reopening.
- **Two-Phase Change Detection** — Phase 1 (line-overlap filter via reverse_index) eliminates most candidates; Phase 2 (hash comparison via source_hash) verifies the remaining candidates. Fast AND correct for incremental re-scan.
- **Source Anchoring** — Three-layer traceability: CodeGraph parses source into Code Units (structural), document_sources links Documents to Code Units (semantic), reverse_index provides O(1) line→document lookup (fast).
- **File Identity Hash** — SHA-256 of the first 4096 bytes of a source file, used to detect renames during incremental re-scan.
- **Cross-Repo Stale Notice** — An informational record posted when a document changes, notifying other repos that link to it. No automatic cascade — each repo's owner decides when to re-scan. Eventually consistent by design.

### Resource Management

- **Concurrency Control** — Configurable limits on concurrent scans (global) and workers per scan, enforced by the Scan Coordinator. Prevents resource exhaustion. Limits can be changed dynamically during scans.

### Tools & Interfaces

- **Prompt Workbench** — A development-time tool for iterating on agent prompts against known code patterns. Feeds validated prompts into the Communication Pattern Catalog.
- **Pre-Scan Profiling** — The initial reconnaissance phase of a scan: CodeGraph parses source into code_units, identifies languages and frameworks, applies scan filter excluding non-code files, and groups code_units into supersteps.
- **LLM-Wiki** — Vulgata's search and retrieval approach: an auto-generated index (index.md) catalogs every document; the chat agent reads the index, selects relevant documents, reads them in full, and synthesizes answers. No vector database or embedding pipeline.
- **index.md** — Auto-generated after each scan. YAML frontmatter with structured entries (doc_id, title, doc_type, system, repo, key_symbols, summary, path). One entry per document.
- **log.md** — Append-only activity log with parseable timestamps in format `## [YYYY-MM-DD HH:MM] operation | target`. Records all scans, queries, and system events.
- **Business Mode** — Chat interface mode producing business-level narrative answers without code references. Uses a lighter UI theme and a system prompt focused on business process explanation. For non-technical users.
- **IT Mode** — Chat interface mode producing full technical answers with code references and source links. Uses a darker UI theme and a system prompt focused on technical detail. For developers and AI agents.
- **System Owner** — A user role responsible for adding and configuring systems in Vulgata: creating systems, linking repositories, configuring Communication Pattern Catalogs, assigning LLM Providers to agents, and starting scans. Distinct from knowledge-seeking users and platform administrators.
- **MCP (Model Context Protocol)** — Anthropic's open protocol for AI-tool integration. Vulgata both consumes MCP tools (for scanning and chat) and exposes its knowledge base as an MCP server (for external AI coding agents).
- **Human-in-the-Loop (HITL)** — The mechanism by which agents surface ambiguities as questions to users during scanning. User answers are marked as human input and carry lower priority than code-derived facts.
- **CodeGraph** — A pre-indexed code intelligence system that provides structural information (call graphs, symbol resolution, dependency maps) to accelerate scanning. Used during pre-scan profiling for deterministic code_unit extraction.
- **LLM Provider** — A configured LLM backend (e.g., DeepSeek V4, self-hosted model) with an endpoint, API key, and supported APIs (chat, responses, messages). Multiple providers can be configured; each agent can be assigned a specific provider. Per-system default + per-repository override.
- **LSP (Language Server Protocol)** — Optional integration for scanning. Provides IDE-level code intelligence (symbol resolution, type information, references) to augment LLM-based analysis. V1 only if time permits.

## 4. Target User

### 4.1 Jobs To Be Done

- **JTBD-1:** When I need to understand how business logic works across systems — whether I'm a product manager proposing a change, a new hire learning the landscape, a compliance officer verifying a rule, or a developer tracing a flow — I ask Vulgata a natural-language question and receive an answer grounded in scanned Documents. I can switch between Business Mode (business-level narrative, non-technical language, lighter UI theme) and IT Mode (full technical detail, code references, darker UI theme) depending on what I need. Each mode uses a different system prompt so the LLM focuses its answers appropriately.

- **JTBD-2:** When I own or manage one or more Systems, I add them to Vulgata — creating the System, linking its Repositories, configuring Communication Pattern Catalogs, assigning LLM Providers to agents, and starting Scans. I monitor Scan progress and respond to agent questions during the Human-in-the-Loop process. This role is distinct from both the knowledge-seeking user and the Vulgata platform administrator.

### 4.2 Key User Journeys

- **UJ-1. Lin the PM asks how risk evaluation works.** Lin, a product manager planning a feature change, logs into Vulgata, selects the Mobile Banking System, ensures she is in Business Mode, and asks "what happens during a risk evaluation in the mobile app?" She receives a step-by-step business narrative tracing the flow across Repositories. She can toggle to IT Mode at any time to see the technical details behind any step.

- **UJ-2. Chen the new hire explores the systems.** Chen, a new business analyst in her first week, opens Vulgata, browses the System overview, and asks "how does loan approval work?" in Business Mode. The chat guides her through the relevant Systems and Documents. She reads the Business Logic Documents to understand the process without touching code.

- **UJ-3. Wang the developer traces a cross-repo flow.** Wang, a developer assigned to build a new feature, switches to IT Mode and asks the same risk evaluation question. He receives the full technical trace with code references, follows the cross-repo links to understand every communication (RPC, HTTP, message queue, file), and reads the Code Logic Documents for implementation detail.

- **UJ-4. Zhang the System Owner onboards his systems.** Zhang manages two Systems in his department. He creates both Systems in the dashboard, adds their Repositories with git URLs, configures the Communication Pattern Catalog for each System's communication mechanisms, assigns LLM Providers to the orchestrator and worker agents, and starts the first Scan. He monitors progress on the dashboard as Documents appear and cross-repo links resolve. When agents surface questions, he answers them to improve Scan quality.

## 5. Features

### 5.1 Authentication & User Management

Basic web application identity. Not the product's core value — sufficient for demo and single-department deployment.

- **FR-1.1:** Users shall register with email and password.
- **FR-1.2:** Users shall log in and log out.
- **FR-1.3:** Users shall view and edit their own profile (display name, email, password change).
- **FR-1.4:** The system shall support basic role-based access: Administrator (full platform management), System Owner (manage assigned systems), User (chat and document access). [ASSUMPTION: Roles are assigned by an administrator; self-service role requests are out of scope.]

### 5.2 System & Repository Management

The administrative surface for System Owners to onboard their systems into Vulgata.

- **FR-2.1:** System Owners shall create, edit, and delete Systems. A System has a name, description, and optional supplementary context.
- **FR-2.2:** System Owners shall add Repositories to a System. A Repository has a name, description, git remote URL, and optional supplementary context.
- **FR-2.3:** System Owners shall remove Repositories from a System.
- **FR-2.4:** System Owners shall create standalone Repositories (not belonging to any System) for shared libraries whose source code is owned by the organization.
- **FR-2.5:** The system shall validate git URL reachability when a Repository is added. [ASSUMPTION: Validation is a best-effort connectivity check; authentication failures do not block repository creation.]

### 5.3 LLM Provider Management

System-level configuration of LLM backends. Enables per-agent provider assignment and self-hosted deployments.

- **FR-3.1:** Administrators shall configure LLM Providers at the system level. Each provider has a name, base endpoint URL, API key, and supported API types (chat completions, responses, messages).
- **FR-3.2:** The system shall support multiple LLM Providers simultaneously.
- **FR-3.3:** System Owners shall assign a specific LLM Provider to each agent role (orchestrator agent, worker agents) within a System. Different agents may use different providers.
- **FR-3.4:** The system shall provide a connection test for each configured LLM Provider.

### 5.4 Scanning Pipeline

The core engine. Scan Coordinator queues repo scans → Orchestrator Agent per repo dispatches workers in two passes → document generation → index/log generation → cross-repo resolution.

- **FR-4.1:** Pre-Scan Profiling shall run before worker dispatch. It shall use CodeGraph/tree-sitter to parse source into Code Units with exact line ranges, identify programming languages and frameworks, and apply a scan filter excluding non-code files (build artifacts, generated code, test fixtures, configuration data).
- **FR-4.2:** The Scan Coordinator (non-LLM background service) shall manage the scan queue, enforce concurrency limits, perform git operations, and spawn one Orchestrator Agent per Repository scan. The Orchestrator Agent shall dispatch Worker Agents to process Code Units in batches (supersteps). Each superstep fans out a batch of code units to workers, then fans in results before dispatching the next batch.
- **FR-4.3:** Each Worker Agent shall read its assigned Code Unit, identify the business logic, and produce a structured Document linked to the source code location. Worker Agents shall detect cross-repo communication during Code Unit processing (Model A — Worker-embedded detection).
- **FR-4.4:** In Pass 1, Worker Agents shall produce Code Logic Documents describing structural code behavior: architecture, data flow, call chains, framework usage. Intra-repo REFERENCES edges shall be created immediately via Document Pre-Allocation. Cross-repo calls shall be recorded as Uncertainties.
- **FR-4.5:** In Pass 2, one Worker Agent per System shall synthesize Business Logic Documents from the completed Code Logic Documents, identifying business flows autonomously from the graph structure. Business Logic Documents shall have GENERATED_FROM edges to their source Code Logic Documents.
- **FR-4.6:** Generated Documents shall be organized in a tree structure mirroring the source code directory hierarchy. The tree shall be computed from document paths at display time (virtual tree); no synthetic directory-document rows shall be created.
- **FR-4.7:** After each Scan completes, the system shall auto-generate index.md with YAML frontmatter — structured entries per Document with doc_id, title, doc_type, system, repo, key_symbols, summary, and path.
- **FR-4.8:** The system shall maintain an append-only log.md recording all Scans, queries, and system events with parseable timestamps in the format `## [YYYY-MM-DD HH:MM] operation | target`.
- **FR-4.9:** The scanning pipeline shall integrate with CodeGraph for pre-indexed structural information (call graphs, symbol resolution, dependency maps) to accelerate agent processing and provide deterministic code_unit boundaries.
- **FR-4.10:** Documents shall be immutable — the only mutation path is code change → re-scan → regenerate. Human edits to Documents are not supported. Previous versions shall be preserved via a superseded_by_id linked list (Version Chain).
- **FR-4.11:** Week 1 shall include a Magentic orchestration validation spike: validate that the Microsoft Agent Framework's Magentic pattern works with Vulgata's custom agent types before committing to the full architecture. If the spike fails, fall back to a simpler agent pattern.
- **FR-4.12:** When two Worker Agents process related Code Units (e.g., caller and callee within the same Repository) in the same superstep, they shall cross-verify business logic claims where possible. Disputed edges shall be kept with lowered confidence (0.5) and rendered as yellow dashed lines pending HITL resolution. Cross-verified claims shall be tagged as such in the generated Documents.

### 5.5 Cross-Repository Communication Detection

The subsystem that identifies when code in one Repository communicates with another Repository. Detection happens during Worker Agent processing (Model A — Worker-embedded). Covers RPC, HTTP APIs, message queues, file-based communication, and page navigation.

- **FR-5.1:** System Owners shall configure a Communication Pattern Catalog per System specifying how that System's repositories communicate externally: RPC frameworks, HTTP API patterns, message queue topics, file paths and storage systems, cross-repo navigation patterns. [NOTE: Architecture decision is to move this to per-repo, but that migration is deferred. V1 ships with per-System CP.]
- **FR-5.2:** The detection layer shall identify when code in one Repository communicates with another Repository. Communication types include: RPC calls (SOFA, Dubbo, gRPC), HTTP API calls (REST, GraphQL), message queue production and consumption, file-based communication (local filesystem, remote filesystem, OSS, HDFS, etc.), and cross-repo page navigation (in native and web apps).
- **FR-5.3:** For each detected cross-repo communication, the system shall identify the provider role (the Repository or external entity exposing the interface) and the consumer role (the Repository or external entity calling or reading from the interface).
- **FR-5.4:** The system shall track consumer count per communication interface — a single provider interface may have multiple consumers across different Repositories.
- **FR-5.5:** The system shall handle cases where the provider or consumer is absent (not in any scanned Repository). Absent entities shall be recorded as external references with available identifying information (service name, URL pattern, topic name, file path, etc.).
- **FR-5.6:** Cross-repo page navigation detection (e.g., navigating from one app to a page in another repo's app) shall be flagged as low-confidence due to inherently loose coupling. Provider and consumer identification may be unreliable for page navigation.
- **FR-5.7:** The system shall use object flow analysis to trace communication targets through object creation and provenance, resolving indirect calls where the target is accessed through intermediate objects or factories.
- **FR-5.8:** The Communication Pattern Catalog shall be used during scanning to guide detection. The Worker Agent's prompt shall dynamically inject the current System's Communication Pattern Catalog.
- **FR-5.9:** The Prompt Workbench shall provide a development-time tool for iterating on agent prompts against known code patterns, feeding validated prompts into the Communication Pattern Catalog.
- **FR-5.10:** The system shall record all detected RPC endpoints as named entities even when the provider is unscanned or unknown. These named entities shall be discoverable through the chat interface.
- **FR-5.11:** The system shall handle file-based communication detection: local filesystem paths, remote filesystem mounts, OSS object storage, HDFS, and other file storage systems. File paths shall be treated as communication endpoints with provider and consumer roles.
- **FR-5.12:** The system shall detect cross-repo page navigation: URL patterns, deep links, and app-to-app navigation flows in both native and web applications. Page navigation detection shall be flagged as low-confidence (see FR-5.6).
- **FR-5.13:** The system shall resolve communication endpoints to target Repositories using a Service Topology — the mapping of service codes and communication endpoints to Repositories. This shall be resolved via an MCP tool that queries a mock service platform. [ASSUMPTION: The mock service platform is pre-populated with known service-to-repo mappings before the demo.]
- **FR-5.14:** The detection system shall be extensible — new communication patterns shall be addable to the Communication Pattern Catalog without code changes. The catalog format shall support organization-wide framework knowledge (e.g., "all services in this organization use SOFA RPC with these conventions").

### 5.6 Uncertainty Resolution

Handles the gaps that cross-repo detection creates. Uncertainties are first-class entities with four states. Resolution is deterministic post-processing triggered after each repo scan completes.

- **FR-6.1:** When a Worker Agent detects a cross-repo communication but the target Repository has not been scanned, it shall record an Uncertainty with status `unresolved` and sub_status `cross_repo`. The Uncertainty shall include the endpoint identifier, protocol, and any available identifying information.
- **FR-6.2:** Cross-Repository Resolution shall run after each Repository scan completes. It shall match unresolved cross-repo uncertainties against the newly-scanned repo's documents using repo-qualified endpoint matching. Matched uncertainties shall be resolved and REFERENCES edges created. Resolution is bidirectional — both outgoing and incoming uncertainties are resolved.
- **FR-6.3:** When two Repositories have mutual pending cross-repo uncertainties (deadlock), the Deadlock-Breaking Protocol shall apply: the repo with fewer pending uncertainties resolves first; if tied, the older scan yields.
- **FR-6.4:** The system shall support Dangling Links — cross-repo uncertainties that never resolve. These shall be recorded with available identifying information and shall not block scan completion. The `wontfix` terminal state shall be available for human-accepted dangling links.
- **FR-6.5:** The system shall provide a chat-accessible uncertainty summary: "This Document has N unresolved cross-repo links" with the endpoint identifiers and target repo names where known.
- **FR-6.6:** Cross-Repo Stale Notices shall be posted when a document changes, notifying other repos that link to it. No automatic cascade — each repo's owner decides when to re-scan. Eventually consistent by design.
- **FR-6.7:** The system shall provide an Impact Analysis query that finds all Documents affected by a set of changed source files: direct impact → intra-repo transitive closure → cross-repo reachability → uncertainty reopening. Cycle detection via UNION (not UNION ALL) with depth guard.

### 5.7 Human-in-the-Loop

Surfaces agent questions to users during scanning. User answers improve scan quality but carry lower authority than code-derived facts.

- **FR-7.1:** When a Worker Agent encounters an ambiguity it cannot resolve from code alone, it shall surface a question to the System Owner. Questions appear as notifications in the dashboard.
- **FR-7.2:** System Owners shall answer agent questions through the dashboard. Answers are attached to the relevant Document as metadata.
- **FR-7.3:** Human-provided answers shall be marked as "human input" and shall carry lower priority than code-derived facts when conflicts arise.
- **FR-7.4:** The dashboard shall display a count of pending questions. System Owners shall be able to view, answer, or dismiss questions.

### 5.8 Third-Party Library Handling

Handles dependencies on external libraries without treating every import as a cross-repo boundary.

- **FR-8.1:** System Owners shall add libraries with organization-owned source code as standalone Repositories (not belonging to any System).
- **FR-8.2:** Standalone Repositories shall be scanned partially and on-demand — only the Code Units actually called by other Repositories are scanned, triggered by the Worker Agent responsible for the caller.
- **FR-8.3:** The system shall use a lock mechanism to prevent concurrent scanning of the same standalone Repository from multiple callers.
- **FR-8.4:** Well-known common libraries (standard libraries, widely-used open-source packages) shall be treated as understood and shall not generate cross-repo uncertainties.
- **FR-8.5:** For unknown libraries without available source code, the system shall attempt LLM-based inference of behavior. If inference fails, the user shall be notified to provide context manually.

### 5.9 Git Monitoring & Incremental Scan

Keeps Documents current with code changes. **Deferrable — may be dropped if time is tight.**

- **FR-9.1:** The system shall monitor connected git repositories for remote changes. [ASSUMPTION: Monitoring uses periodic polling; webhook-based triggers are out of scope for V1.]
- **FR-9.2:** When the local clone is behind the remote, the system shall pull changes.
- **FR-9.3:** After pulling changes, the system shall incrementally re-scan only the affected Code Units (files that changed, plus files that depend on changed files).
- **FR-9.4:** The system shall preserve Document history — previous versions of updated Documents shall be archived and accessible.
- **FR-9.5:** Incremental re-scan shall not start if a Scan is already in progress for the same Repository.

### 5.10 Database Connection Tools

Enables LLM agents to inspect database schemas and query sample data during scanning, improving document accuracy for data-dependent business logic.

- **FR-10.1:** System Owners shall configure database connections per Repository — specifying connection string, database type, and access credentials.
- **FR-10.2:** During scanning, Worker Agents shall be able to inspect database schemas (tables, columns, types, constraints) via a tool interface.
- **FR-10.3:** During scanning, Worker Agents shall be able to query sample data (limited rows) via a tool interface. [ASSUMPTION: Database connections are to non-production environments or read-only replicas. Production database access is explicitly out of scope for V1.]

### 5.11 Chat Interface

The primary user-facing surface for knowledge consumption. Two modes, LLM-wiki retrieval, and source-grounded answers.

- **FR-11.1:** Business Mode shall present a lighter UI theme and use a system prompt focused on business process explanation. Answers shall be business-level narratives without code references.
- **FR-11.2:** IT Mode shall present a darker UI theme and use a system prompt focused on technical detail. Answers shall include code references and source links.
- **FR-11.3:** Users shall switch between Business Mode and IT Mode at any time during a chat session. The UI theme shall change immediately; the system prompt shall change for subsequent messages.
- **FR-11.4:** Users shall select which Systems or Repositories to include as context for their questions. When no selection is made, all Systems the user has access to are included.
- **FR-11.5:** Users shall upload files (documents, images) as supplementary context for their questions.
- **FR-11.6:** The chat agent shall use LLM-wiki retrieval: (a) read index.md to identify relevant Documents, (b) select 3-5 most relevant Documents, (c) read each selected Document in full, (d) follow cross-repo links to related Documents, (e) synthesize an answer with citations to source Documents.
- **FR-11.7:** Every answer shall include citations linking claims to the source Documents and, where applicable, to the source code lines that generated those Documents.

### 5.12 Dashboard & Scan Progress

Real-time visibility into scanning operations. Includes the live knowledge graph visualization.

- **FR-12.1:** The dashboard shall display real-time Scan status for each System: not started, profiling, scanning, completed, failed.
- **FR-12.2:** During an active Scan, the dashboard shall display the number of active Worker Agents.
- **FR-12.3:** During an active Scan, the dashboard shall display the count of files processed and total files to process.
- **FR-12.4:** During an active Scan, the dashboard shall display the count of errors encountered.
- **FR-12.5:** The dashboard shall display pending Human-in-the-Loop questions with the ability to view, answer, or dismiss each.
- **FR-12.6:** The dashboard shall include a live knowledge graph visualization using Blazor.Diagrams. As the Scan progresses: Documents shall appear as nodes, cross-repo links shall appear as edges when resolved, and unresolved uncertainties shall appear as dashed edges to placeholder nodes.

### 5.13 MCP Integration

Model Context Protocol integration for tool consumption and knowledge serving. **Lowest priority — may be deferred if time is tight.**

- **FR-13.1:** The scanning pipeline shall be able to consume MCP tools registered at the Repository, System, or global level.
- **FR-13.2:** The chat interface shall be able to consume MCP tools registered at the Repository, System, or global level.
- **FR-13.3:** MCP tools shall require separate approval for scanning use and chat use. A tool approved for scanning is not automatically available in chat, and vice versa.
- **FR-13.4:** Vulgata shall expose its knowledge base (Documents, index.md, cross-repo links) as an MCP server, allowing external AI coding agents to query business logic.
- **FR-13.5:** MCP tools shall be configurable at three levels: Repository (scoped to one repo), System (scoped to all repos in a system), and Global (available to all systems).

### 5.14 User-Supplied Context

Allows System Owners to provide supplementary information that improves scan quality, applied at controlled times to avoid inconsistency.

- **FR-14.1:** System Owners shall supply context at the global level (applies to all Systems).
- **FR-14.2:** System Owners shall supply context at the System level (applies to all Repositories in that System).
- **FR-14.3:** System Owners shall supply context at the Repository level (applies to a single Repository).
- **FR-14.4:** User-supplied context shall only be applied when no Scan is running for the affected scope, or when the user is responding to an agent question during the Human-in-the-Loop process. Context changes during an active Scan shall be queued and applied after the Scan completes.

### 5.15 LSP Support

Optional Language Server Protocol integration for IDE-level code intelligence during scanning. **V1 only if time permits.**

- **FR-15.1:** The scanning pipeline shall optionally integrate with Language Servers for symbol resolution (go-to-definition, find-references).
- **FR-15.2:** The scanning pipeline shall optionally use LSP type information to augment LLM-based code understanding.
- **FR-15.3:** LSP integration shall be configurable per Repository, with language-specific server configuration.

## 6. Non-Functional Requirements

### 6.1 Performance

- **NFR-1.1:** A Scan of a repository with ~10,000 source files shall complete within a timeframe that allows the demo to be prepared in advance (scans are not performed live during the demo). Exact scan duration depends on LLM API throughput, agent count, and code complexity — benchmarking during development will establish realistic expectations.
- **NFR-1.2:** Chat responses shall be delivered within 30 seconds for questions spanning up to 5 Documents.
- **NFR-1.3:** The live knowledge graph visualization shall update within 5 seconds of a Document being generated or a cross-repo link being resolved.

### 6.2 Security

- **NFR-2.1:** All user passwords shall be hashed using a modern algorithm (bcrypt or Argon2).
- **NFR-2.2:** API keys for LLM Providers and database connections shall be stored encrypted at rest.
- **NFR-2.3:** Source code and generated Documents shall never leave the organization's infrastructure when using a self-hosted LLM Provider. When using external LLM Providers, only the code context necessary for the current agent task shall be transmitted.
- **NFR-2.4:** Database connection tools shall only connect to non-production environments or read-only replicas. Write operations on connected databases shall be blocked.

### 6.3 Reliability

- **NFR-3.1:** A failed Worker Agent task shall not fail the entire Scan. The orchestrator shall retry the task once, then record the failure and continue with remaining Code Units.
- **NFR-3.2:** The system shall gracefully handle LLM API unavailability — queuing or pausing agent tasks until the API recovers.
- **NFR-3.3:** Scan state shall be persisted such that a server restart does not lose progress on an in-progress Scan. The specific persistence mechanism is deferred to architecture.

### 6.4 Deployability

- **NFR-4.1:** Vulgata shall deploy as a single Docker container. Specific deployment plan, database selection, and infrastructure decisions are deferred to architecture discussion.

### 6.5 Usability

- **NFR-5.1:** A non-technical user shall be able to log in, select a System, and ask a business question in Business Mode without training or documentation.
- **NFR-5.2:** The dashboard shall use Blazor's built-in accessibility features. WCAG compliance is not required for the competition demo.

### 6.6 Maintainability

- **NFR-6.1:** Agent prompts shall be externalized from code — stored as configurable text resources, not hardcoded strings.
- **NFR-6.2:** The Communication Pattern Catalog format shall be documented so that System Owners can write catalogs for new Systems without developer assistance.

## 7. Success Metrics

- **SM-1 (Competition Demo):** A live demo traces a cross-repo business question ("what happens during a risk evaluation in the mobile app?") across at least two Repositories, with every step linked to source code. The demo runs live, not as a recording.
- **SM-2 (Scan Quality):** Generated Business Logic Documents are readable and understandable by a non-technical reviewer (product manager or business analyst) without developer assistance.
- **SM-3 (Cross-Repo Accuracy):** When Repository A communicates with Repository B, the Document for Repository A references the correct Document in Repository B. Provider and consumer roles are correctly tagged.
- **SM-4 (Chat Quality):** A non-technical user can log in, select a System, ask a business question in Business Mode, and receive an answer they understand — without training or documentation.
- **SM-5 (Incremental Update):** After an initial Scan, pushing a code change triggers an incremental re-scan that updates only affected Documents. *(Deferrable — measured only if FR-9.x is implemented.)*

**Counter-metrics (what we track but don't optimize for in V1):**
- Scan duration (acceptable as long as demo can be prepared in advance)
- Token cost per scan (acceptable given DeepSeek V4 pricing)
- Dangling link count (acceptable — each is a known unknown)

## 8. Assumptions Index

*All [ASSUMPTION] tags from FRs and NFRs, collected here for visibility.*

- **A-1:** User roles (Administrator, System Owner, User) are assigned by an administrator; self-service role requests are out of scope. (FR-1.4)
- **A-2:** Git URL validation is a best-effort connectivity check; authentication failures do not block repository creation. (FR-2.5)
- **A-3:** The mock service platform for Service Topology resolution is pre-populated with known service-to-system mappings before the demo. (FR-5.13)
- **A-4:** Git monitoring uses periodic polling; webhook-based triggers are out of scope for V1. (FR-9.1)
- **A-5:** Database connections are to non-production environments or read-only replicas. Production database access is explicitly out of scope for V1. (FR-10.3)
- **A-6:** WCAG compliance is not required for the competition demo. (NFR-5.2)

## 9. Open Questions

*Questions that should be resolved before or during architecture. Not blockers for PRD approval.*

- **OQ-1:** What are the specific demo repositories, and do they contain compelling cross-repo communication patterns (RPC, HTTP, message queue, file, page navigation)?
- **OQ-2:** What communication mechanisms do the demo Repositories use, and can the Communication Pattern Catalog cover them?
- **OQ-3:** Will the Business Logic Documents produced by the scanning pipeline actually be readable by a non-technical reviewer?
- **OQ-4:** What is the fallback LLM Provider if DeepSeek V4 is unavailable during the demo?
- **OQ-5:** What is the target scan duration for the demo repositories? (To be established during development benchmarking.)
- **OQ-6:** How are cross-repo communication patterns discovered when the System Owner does not know all of them upfront? (Human-in-the-Loop discovery vs. pre-configuration.)
- **OQ-7:** How do we validate document correctness before the demo? (Manual review of critical paths, cross-verification between agents, sample question testing.)

## 10. Out of Scope

*Explicitly excluded from V1. Listed here so they are not rediscovered during architecture or implementation.*

- Full RBAC with fine-grained permissions
- Multi-language scanning beyond the languages present in the demo repositories
- Automated compliance reporting or audit trail generation
- High-availability deployment or multi-instance orchestration
- Integration with enterprise SSO or identity providers
- Audition Agent (independent document verification)
- Answer confidence scoring and chat boundary guards
- gRPC communication detection (not used in the target organization)
- Database shared access as a cross-repo communication pattern
- Production database access
- Webhook-based git change detection
- WCAG compliance
