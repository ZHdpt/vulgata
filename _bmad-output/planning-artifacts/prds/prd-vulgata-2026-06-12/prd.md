---
title: "PRD: Vulgata"
status: draft
created: 2026-06-12
updated: 2026-06-12
---

# PRD: Vulgata

## 0. Document Purpose

This PRD defines the V1 scope for Vulgata, an LLM-powered cross-system business logic extraction platform. It is written for the development team (2 people + AI coding agents), competition judges evaluating the project, and downstream BMad workflows (architecture, epics, stories). The PRD builds on the product brief, PRFAQ, brainstorming session, domain research, and technical research already completed. It uses a Glossary-anchored vocabulary — every domain term is defined once and used consistently throughout. Features are grouped with nested functional requirements (FRs) numbered globally. Assumptions are tagged inline and indexed.

## 1. Vision

Vulgata is a platform that reads source code across multiple systems and repositories, extracts the business logic embedded within, and makes it accessible to everyone in the organization — product managers, compliance officers, new team members, and developers alike. It answers the question that currently takes days of manual code-diving: "what actually happens when a user does X?"

In a large bank, business logic is scattered across dozens of systems in different languages and architectures. A single business flow — a risk evaluation in the mobile app — can touch six or more systems. No single person, and no single document, holds the full picture. Vulgata closes this gap by scanning every repository connected to a business flow, producing structured documents that explain both what the code does and what business rules it implements, and linking documents across system boundaries to form a complete, navigable map.

The V1 demo scope covers two systems consisting of approximately a dozen repositories — including microservices, a web application, and an Android application. The vision is larger: Vulgata becomes the knowledge backbone for the department that builds it, then expands across the organization, and eventually becomes a standard part of the enterprise development lifecycle — where the gap between "what the business thinks the systems do" and "what the code actually does" is closed, and stays closed.

## 2. Why Now

Vulgata is being developed for an internal AI programming competition with a 9-week timeline (June 12 → August 15, 2026). The competition rubric allocates points across four dimensions: innovation and creativity (30 points), technical implementation and complexity (25 points), practical value and deployability (25 points), and user experience and interaction design (20 points). The primary success criterion is a working end-to-end live demo that traces a real cross-system business flow.

Beyond the competition, three forces make this the right moment: (1) LLM inference costs have dropped to <1 CNY per million input tokens, making agent-based code analysis economically viable; (2) the Microsoft Agent Framework provides production-grade multi-agent orchestration with MCP integration; (3) the industry is shifting from "AI as code generator" to "AI as code understander" — and no existing tool addresses the cross-system business logic extraction problem that Vulgata targets.

## 3. Glossary

*Every domain term used in this PRD is defined here. FRs, UJs, and SMs use these terms exactly. No synonyms.*

- **System** — A logical grouping of repositories that together implement a business capability (e.g., "Mobile Banking," "Risk Assessment"). A system contains one or more repositories. Cardinality: 1 system → N repositories.
- **Repository** — A git repository containing source code. Belongs to exactly one system, or can be standalone (for shared libraries not owned by any system). Linked to a remote git URL.
- **Scan** — The process of reading a repository's source code with LLM-powered agents and producing structured documents. A scan targets either a single repository or an entire system (all its repositories). Scans are incremental when triggered by code changes.
- **Code Unit** — The smallest unit of code processed by a worker agent. Typically a function, method, class, or file — determined by the language and framework detected during pre-scan profiling.
- **Document** — A structured markdown file produced by a worker agent, linked to one or more code units. Immutable — can only change when the linked code changes and is re-scanned. Two classifications: Code Logic Document and Business Logic Document.
- **Code Logic Document** — A document describing what code does structurally: architecture, data flow, call chains, framework usage. Readable by developers and AI agents.
- **Business Logic Document** — A document describing the business rules and processes implemented by code: validation rules, decision flows, regulatory checks, business process steps. Readable by non-technical users. Built on top of Code Logic Documents.
- **Orchestrator Agent** — The agent responsible for managing a scan: dispatching worker agents, tracking progress, handling errors, and coordinating cross-system dependency resolution.
- **Worker Agent** — An agent dispatched by the orchestrator to process a specific code unit. Reads the code, identifies business logic, and produces a document.
- **Uncertainty** — A metadata field attached to a document when the generating agent cannot determine something with confidence. Three states: resolved (linked to a document in another system), discovered-but-unresolved (endpoint recorded, target not yet scanned), unknown (no evidence of the target). Uncertainties are collected and processed by the uncertainty resolution system.
- **Cross-System Communication Detection** — A prompt-driven subsystem that identifies when code in one System communicates with another System. Covers RPC, HTTP APIs, message queues, file-based communication (local filesystem, remote filesystem, OSS, HDFS, etc.), and cross-system page navigation (in native and web apps). For each detected communication, identifies the provider role and consumer role, tags consumer count per interface, and handles cases where provider or consumer is absent (not in any scanned System). Uses a per-system Communication Pattern Catalog, object flow analysis, and service topology resolution. Note: cross-system page navigation (e.g., navigating from one app to a page in another system) is inherently loose-coupled — provider and consumer identification may be unreliable and is flagged as low-confidence when detected.
- **Communication Pattern Catalog** — A per-system configuration file specifying how that System communicates externally: RPC frameworks, HTTP API patterns, message queue topics, file paths and storage systems, cross-system navigation patterns. Written by the System Owner configuring the scan.
- **Provider Role** — The System (or external entity) that exposes a communication interface (RPC service, HTTP endpoint, message queue topic, file location, navigable page). Tagged on each detected cross-system communication.
- **Consumer Role** — The System (or external entity) that calls or reads from a communication interface. Tagged on each detected cross-system communication. Multiple consumers may exist per interface; consumer count is tracked.
- **Service Topology** — The mapping of service codes and communication endpoints to target Systems. Resolved via an MCP tool that queries a mock service platform.
- **Prompt Workbench** — A development-time tool for iterating on agent prompts against known code patterns. Feeds validated prompts into the Communication Pattern Catalog.
- **Pre-Scan Profiling** — The initial reconnaissance phase of a scan: identifies languages, frameworks, conventions, and valid source files before dispatching worker agents.
- **LLM-Wiki** — Vulgata's search and retrieval approach: an auto-generated index (index.md) catalogs every document; the chat agent reads the index, selects relevant documents, reads them in full, and synthesizes answers. No vector database or embedding pipeline.
- **index.md** — Auto-generated after each scan. One entry per document with path, title, and one-line summary.
- **log.md** — Append-only activity log with parseable timestamps. Records all scans, queries, and system events.
- **Business Mode** — Chat interface mode producing business-level narrative answers without code references. Uses a lighter UI theme and a system prompt focused on business process explanation. For non-technical users.
- **IT Mode** — Chat interface mode producing full technical answers with code references and source links. Uses a darker UI theme and a system prompt focused on technical detail. For developers and AI agents.
- **System Owner** — A user role responsible for adding and configuring systems in Vulgata: creating systems, linking repositories, configuring Communication Pattern Catalogs, assigning LLM Providers to agents, and starting scans. Distinct from knowledge-seeking users and platform administrators.
- **MCP (Model Context Protocol)** — Anthropic's open protocol for AI-tool integration. Vulgata both consumes MCP tools (for scanning and chat) and exposes its knowledge base as an MCP server (for external AI coding agents).
- **Human-in-the-Loop (HITL)** — The mechanism by which agents surface ambiguities as questions to users during scanning. User answers are marked as human input and carry lower priority than code-derived facts.
- **CodeGraph** — A pre-indexed code intelligence system that provides structural information (call graphs, symbol resolution, dependency maps) to accelerate scanning.
- **LLM Provider** — A configured LLM backend (e.g., DeepSeek V4, self-hosted model) with an endpoint, API key, and supported APIs (chat, responses, messages). Multiple providers can be configured; each agent can be assigned a specific provider.
- **LSP (Language Server Protocol)** — Optional integration for scanning. Provides IDE-level code intelligence (symbol resolution, type information, references) to augment LLM-based analysis. V1 only if time permits.

## 4. Target User

### 4.1 Jobs To Be Done

- **JTBD-1:** When I need to understand how business logic works across systems — whether I'm a product manager proposing a change, a new hire learning the landscape, a compliance officer verifying a rule, or a developer tracing a flow — I ask Vulgata a natural-language question and receive an answer grounded in scanned Documents. I can switch between Business Mode (business-level narrative, non-technical language, lighter UI theme) and IT Mode (full technical detail, code references, darker UI theme) depending on what I need. Each mode uses a different system prompt so the LLM focuses its answers appropriately.

- **JTBD-2:** When I own or manage one or more Systems, I add them to Vulgata — creating the System, linking its Repositories, configuring Communication Pattern Catalogs, assigning LLM Providers to agents, and starting Scans. I monitor Scan progress and respond to agent questions during the Human-in-the-Loop process. This role is distinct from both the knowledge-seeking user and the Vulgata platform administrator.

### 4.2 Key User Journeys

- **UJ-1. Lin the PM asks how risk evaluation works.** Lin, a product manager planning a feature change, logs into Vulgata, selects the Mobile Banking System, ensures she is in Business Mode, and asks "what happens during a risk evaluation in the mobile app?" She receives a step-by-step business narrative tracing the flow across Systems. She can toggle to IT Mode at any time to see the technical details behind any step.

- **UJ-2. Chen the new hire explores the systems.** Chen, a new business analyst in her first week, opens Vulgata, browses the System overview, and asks "how does loan approval work?" in Business Mode. The chat guides her through the relevant Systems and Documents. She reads the Business Logic Documents to understand the process without touching code.

- **UJ-3. Wang the developer traces a cross-system flow.** Wang, a developer assigned to build a new feature, switches to IT Mode and asks the same risk evaluation question. He receives the full technical trace with code references, follows the cross-system links to understand every communication (RPC, HTTP, message queue, file), and reads the Code Logic Documents for implementation detail.

- **UJ-4. Zhang the System Owner onboards his systems.** Zhang manages two Systems in his department. He creates both Systems in the dashboard, adds their Repositories with git URLs, configures the Communication Pattern Catalog for each System's communication mechanisms, assigns LLM Providers to the orchestrator and worker agents, and starts the first Scan. He monitors progress on the dashboard as Documents appear and cross-system links resolve. When agents surface questions, he answers them to improve Scan quality.

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

The core engine. Pre-scan profiling → orchestrator dispatches workers → document generation → index/log generation.

- **FR-4.1:** Pre-Scan Profiling shall run before worker dispatch. It shall identify: programming languages present, frameworks in use, coding conventions, and valid source file paths. It shall produce a scan filter excluding non-code files (build artifacts, generated code, test fixtures, configuration data).
- **FR-4.2:** The Orchestrator Agent shall dispatch Worker Agents to process Code Units in batches (supersteps). Each superstep fans out a batch of code units to workers, then fans in results before dispatching the next batch.
- **FR-4.3:** Each Worker Agent shall read its assigned Code Unit, identify the business logic, and produce a structured Document linked to the source code location.
- **FR-4.4:** Worker Agents shall produce Code Logic Documents describing structural code behavior: architecture, data flow, call chains, framework usage.
- **FR-4.5:** Worker Agents shall produce Business Logic Documents describing business rules and processes: validation rules, decision flows, regulatory checks, business process steps.
- **FR-4.6:** Generated Documents shall be organized in a tree structure mirroring the source code directory hierarchy.
- **FR-4.7:** After each Scan completes, the system shall auto-generate index.md — one entry per Document with file path, title, and one-line summary.
- **FR-4.8:** The system shall maintain an append-only log.md recording all Scans, queries, and system events with parseable timestamps in the format `## [YYYY-MM-DD HH:MM] operation | target`.
- **FR-4.9:** The scanning pipeline shall integrate with CodeGraph for pre-indexed structural information (call graphs, symbol resolution, dependency maps) to accelerate agent processing.
- **FR-4.10:** Documents shall be immutable — the only mutation path is code change → re-scan → regenerate. Human edits to Documents are not supported.
- **FR-4.11:** Week 1 shall include a Magentic orchestration validation spike: validate that the Microsoft Agent Framework's Magentic pattern works with Vulgata's custom agent types before committing to the full architecture. If the spike fails, fall back to a simpler agent pattern.
- **FR-4.12:** When two Worker Agents process related Code Units (e.g., caller and callee within the same System), they shall cross-verify business logic claims where possible. Cross-verified claims shall be tagged as such in the generated Documents.

### 5.5 Cross-System Communication Detection

Identifies all ways systems communicate, tags provider/consumer roles, and feeds the uncertainty resolution pipeline. This is the subsystem that makes cross-system tracing possible.

- **FR-5.1:** System Owners shall configure a Communication Pattern Catalog per System, specifying how that System communicates externally: RPC frameworks, HTTP API patterns, message queue topics, file paths and storage systems, cross-system navigation patterns.
- **FR-5.2:** The detection layer shall identify RPC calls crossing System boundaries, using the Communication Pattern Catalog and object flow analysis.
- **FR-5.3:** The detection layer shall identify HTTP API calls crossing System boundaries.
- **FR-5.4:** The detection layer shall identify message queue production and consumption crossing System boundaries.
- **FR-5.5:** The detection layer shall identify file-based communication crossing System boundaries — including local filesystem paths, remote filesystem mounts, and storage systems (OSS, HDFS, etc.).
- **FR-5.6:** The detection layer shall identify cross-system page navigation in native and web applications. Detected navigations shall be flagged as low-confidence due to inherently loose coupling.
- **FR-5.7:** For each detected communication, the system shall identify and tag the Provider Role — the System (or external entity) that exposes the interface.
- **FR-5.8:** For each detected communication, the system shall identify and tag the Consumer Role — the System (or external entity) that calls or reads from the interface.
- **FR-5.9:** The system shall track consumer count per communication interface. An interface with zero known consumers or zero known providers is valid — the absent party is recorded as an unresolved uncertainty.
- **FR-5.10:** Detection shall function when the provider or consumer is absent (not in any scanned System). The absent party shall be recorded as an unresolved uncertainty with whatever identifying information is available (endpoint URL, topic name, file path, page route).
- **FR-5.11:** The Prompt Workbench shall provide a development-time interface for iterating on detection prompts against known code patterns. Validated prompts shall be exportable to the Communication Pattern Catalog.
- **FR-5.12:** The detection layer shall perform object flow analysis — tracing how communication client objects are created, configured, and invoked — to supplement pattern-based detection.
- **FR-5.13:** The detection layer shall resolve communication endpoints to target Systems via a Service Topology MCP tool that queries a mock service platform. [ASSUMPTION: The mock service platform is pre-populated with known service-to-system mappings before the demo.]
- **FR-5.14:** The system shall inject organization-wide communication framework knowledge into all agent prompts as a baseline, supplementing per-system Communication Pattern Catalogs. This ensures agents have a starting point for communication detection even when per-system catalogs are incomplete.

### 5.6 Uncertainty Resolution

Processes detected-but-unresolved cross-system communications, links documents across System boundaries, and manages scan dependencies.

- **FR-6.1:** The system shall maintain a 3-state uncertainty model for each detected cross-system communication: **resolved** (linked to a Document in the target System), **discovered-but-unresolved** (endpoint recorded, target not yet scanned), **unknown** (no evidence of the target System).
- **FR-6.2:** When a target System has been scanned, the system shall resolve uncertainties by linking the source Document to the relevant target Document. The link shall include the target Document path and a one-line summary.
- **FR-6.3:** When a target System is currently being scanned, the source System's scan shall wait for the target scan to complete (avoiding deadlocks) but shall continue responding to uncertainty queries from other Systems.
- **FR-6.4:** Dangling links (discovered-but-unresolved uncertainties that never resolve) are acceptable. Each dangling link is a known unknown — recorded with available identifying information — and does not block scan completion.

### 5.7 Human-in-the-Loop

Surfaces agent questions to users during scanning. User answers improve scan quality but carry lower authority than code-derived facts.

- **FR-7.1:** When a Worker Agent encounters an ambiguity it cannot resolve from code alone, it shall surface a question to the System Owner. Questions appear as notifications in the dashboard.
- **FR-7.2:** System Owners shall answer agent questions through the dashboard. Answers are attached to the relevant Document as metadata.
- **FR-7.3:** Human-provided answers shall be marked as "human input" and shall carry lower priority than code-derived facts when conflicts arise.
- **FR-7.4:** The dashboard shall display a count of pending questions. System Owners shall be able to view, answer, or dismiss questions.

### 5.8 Third-Party Library Handling

Handles dependencies on external libraries without treating every import as a cross-system boundary.

- **FR-8.1:** System Owners shall add libraries with organization-owned source code as standalone Repositories (not belonging to any System).
- **FR-8.2:** Standalone Repositories shall be scanned partially and on-demand — only the Code Units actually called by other Repositories are scanned, triggered by the Worker Agent responsible for the caller.
- **FR-8.3:** The system shall use a lock mechanism to prevent concurrent scanning of the same standalone Repository from multiple callers.
- **FR-8.4:** Well-known common libraries (standard libraries, widely-used open-source packages) shall be treated as understood and shall not generate cross-system uncertainties.
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
- **FR-11.6:** The chat agent shall use LLM-wiki retrieval: (a) read index.md to identify relevant Documents, (b) select 3-5 most relevant Documents, (c) read each selected Document in full, (d) follow cross-system links to related Documents, (e) synthesize an answer with citations to source Documents.
- **FR-11.7:** Every answer shall include citations linking claims to the source Documents and, where applicable, to the source code lines that generated those Documents.

### 5.12 Dashboard & Scan Progress

Real-time visibility into scanning operations. Includes the live knowledge graph visualization.

- **FR-12.1:** The dashboard shall display real-time Scan status for each System: not started, profiling, scanning, completed, failed.
- **FR-12.2:** During an active Scan, the dashboard shall display the number of active Worker Agents.
- **FR-12.3:** During an active Scan, the dashboard shall display the count of files processed and total files to process.
- **FR-12.4:** During an active Scan, the dashboard shall display the count of errors encountered.
- **FR-12.5:** The dashboard shall display pending Human-in-the-Loop questions with the ability to view, answer, or dismiss each.
- **FR-12.6:** The dashboard shall include a live knowledge graph visualization using Blazor.Diagrams. As the Scan progresses: Documents shall appear as nodes, cross-system links shall appear as edges when resolved, and unresolved uncertainties shall appear as dashed edges to placeholder nodes.

### 5.13 MCP Integration

Model Context Protocol integration for tool consumption and knowledge serving. **Lowest priority — may be deferred if time is tight.**

- **FR-13.1:** The scanning pipeline shall be able to consume MCP tools registered at the Repository, System, or global level.
- **FR-13.2:** The chat interface shall be able to consume MCP tools registered at the Repository, System, or global level.
- **FR-13.3:** MCP tools shall require separate approval for scanning use and chat use. A tool approved for scanning is not automatically available in chat, and vice versa.
- **FR-13.4:** Vulgata shall expose its knowledge base (Documents, index.md, cross-system links) as an MCP server, allowing external AI coding agents to query business logic.
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
- **NFR-1.3:** The live knowledge graph visualization shall update within 5 seconds of a Document being generated or a cross-system link being resolved.

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

- **SM-1 (Competition Demo):** A live demo traces a cross-system business question ("what happens during a risk evaluation in the mobile app?") across at least two Systems, with every step linked to source code. The demo runs live, not as a recording.
- **SM-2 (Scan Quality):** Generated Business Logic Documents are readable and understandable by a non-technical reviewer (product manager or business analyst) without developer assistance.
- **SM-3 (Cross-System Accuracy):** When System A communicates with System B, the Document for System A references the correct Document in System B. Provider and consumer roles are correctly tagged.
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

- **OQ-1:** What are the specific demo repositories, and do they contain compelling cross-system communication patterns (RPC, HTTP, message queue, file, page navigation)?
- **OQ-2:** What communication mechanisms do the demo Systems use, and can the Communication Pattern Catalog cover them?
- **OQ-3:** Will the Business Logic Documents produced by the scanning pipeline actually be readable by a non-technical reviewer?
- **OQ-4:** What is the fallback LLM Provider if DeepSeek V4 is unavailable during the demo?
- **OQ-5:** What is the target scan duration for the demo repositories? (To be established during development benchmarking.)
- **OQ-6:** How are System-to-System communication patterns discovered when the System Owner does not know all of them upfront? (Human-in-the-Loop discovery vs. pre-configuration.)
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
- Database shared access as a cross-system communication pattern
- Production database access
- Webhook-based git change detection
- WCAG compliance
