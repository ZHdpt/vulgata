---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - prds/prd-vulgata-2026-06-12/prd.md
  - architecture.md
  - ux-designs/ux-vulgata-2026-06-22/DESIGN.md
  - ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md
---

# Vulgata - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Vulgata, decomposing the requirements from the PRD and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-1.1: Users shall register with email and password.
FR-1.2: Users shall log in and log out.
FR-1.3: Users shall view and edit their own profile (display name, email, password change).
FR-1.4: The system shall support basic role-based access: Administrator (full platform management), System Owner (manage assigned systems), User (chat and document access).

FR-2.1: System Owners shall create, edit, and delete Systems. A System has a name, description, and optional supplementary context.
FR-2.2: System Owners shall add Repositories to a System. A Repository has a name, description, git remote URL, and optional supplementary context.
FR-2.3: System Owners shall remove Repositories from a System.
FR-2.4: System Owners shall create standalone Repositories (not belonging to any System) for shared libraries whose source code is owned by the organization.
FR-2.5: The system shall validate git URL reachability when a Repository is added.

FR-3.1: Administrators shall configure LLM Providers at the global level. Each provider has a name, base endpoint URL, API key, supported API types (chat completions, responses, messages), and default LLM task-type assignments (pre-scan, worker, chat).
FR-3.2: The system shall support multiple LLM Providers simultaneously.
FR-3.3: System Owners may override the global default LLM Provider for each LLM task role within their System. The deterministic Orchestrator does not require an LLM Provider assignment.
FR-3.4: The system shall provide a connection test for each configured LLM Provider.
FR-4.1: Pre-Scan Profiling shall run before worker dispatch. It shall use CodeGraph/tree-sitter to parse source into Code Units with exact line ranges, identify programming languages and frameworks, and apply a scan filter excluding non-code files. After profiling, agents may surface questions to the System Owner; the scan waits for user answers and approval before proceeding to worker dispatch.
FR-4.2: The Scan Coordinator (non-LLM background service) shall manage the scan queue, enforce concurrency limits, perform git operations, and trigger one deterministic Orchestrator per Repository scan. The Orchestrator shall dispatch Worker Agents through typed jobs to process Code Units in batches (supersteps).
FR-4.3: Each Worker Agent shall read its assigned Code Unit, identify the business logic, and produce a structured Document linked to the source code location. Worker Agents shall detect cross-repo communication during Code Unit processing (Model A — Worker-embedded detection).
FR-4.4: In Pass 1, Worker Agents shall produce Code Logic Documents describing structural code behavior. Intra-repo REFERENCES edges shall be created immediately via Document Pre-Allocation. Cross-repo calls shall be recorded as Uncertainties.
FR-4.5: In Pass 2, the Orchestrator shall perform graph-first, path-aware flow tracing from Pass 1 entry points and deterministically cluster traces into flow groups. One Worker Agent per flow group shall synthesize a Business Logic Document from the prepared trace packet and Code Logic Documents. Business Logic Documents shall have GENERATED_FROM edges to the Code Logic Document Versions actually used.
FR-4.6: Generated Documents shall be organized in a tree structure mirroring the source code directory hierarchy. The tree shall be computed from document paths at display time (virtual tree).
FR-4.7: After each Scan commits Document Versions, the system shall materialize PostgreSQL-backed retrieval indexes, including `index_entries` and `document_sections`, from document metadata and `content_jsonb`. The system may also auto-generate `index.md` as an export/readability artifact, but `index.md` is not the canonical retrieval source.
FR-4.8: The system shall maintain an append-only log.md recording all Scans, queries, and system events with parseable timestamps.
FR-4.9: The scanning pipeline shall integrate with CodeGraph for pre-indexed structural information (call graphs, symbol resolution, dependency maps).
FR-4.10: Documents shall be immutable — the only mutation path is code change → re-scan → regenerate. Previous versions shall be preserved via a superseded_by_id linked list (Version Chain).
FR-4.11: Week 1 shall include a Worker Agent execution adapter validation spike: validate that the Microsoft Agent Framework can execute Vulgata Worker Agents and typed tool calls under deterministic Orchestrator control before committing to the Worker Agent adapter.
FR-4.12: When two Worker Agents process related Code Units in the same superstep, they shall cross-verify business logic claims. Disputed edges shall be kept with lowered confidence (0.5) and rendered as yellow dashed lines pending HITL resolution.
FR-5.1: System Owners shall configure a Communication Pattern Catalog per System specifying how that System's repositories communicate externally: RPC frameworks, HTTP API patterns, message queue topics, file paths and storage systems, cross-repo navigation patterns. RPC catalogs shall be included in the pre-scan context; agents may attempt to validate catalog entries during pre-scan profiling.
FR-5.2: The detection layer shall identify when code in one Repository communicates with another Repository. Communication types include: RPC calls, HTTP API calls, message queue production and consumption, file-based communication, and cross-repo page navigation.
FR-5.3: For each detected cross-repo communication, the system shall identify the provider role and the consumer role.
FR-5.4: The system shall track consumer count per communication interface.
FR-5.5: The system shall handle cases where the provider or consumer is absent (not in any scanned Repository). Absent entities shall be recorded as external references.
FR-5.6: Cross-repo page navigation detection shall be flagged as low-confidence due to inherently loose coupling.
FR-5.7: The system shall use object flow analysis to trace communication targets through object creation and provenance.
FR-5.8: The Communication Pattern Catalog shall be used during scanning to guide detection. The Worker Agent's prompt shall dynamically inject the current System's Communication Pattern Catalog.
FR-5.9: The Prompt Workbench shall provide a development-time tool for iterating on agent prompts against known code patterns, feeding validated prompts into the Communication Pattern Catalog.
FR-5.10: The system shall record all detected RPC endpoints as named entities even when the provider is unscanned or unknown.
FR-5.11: The system shall handle file-based communication detection: local filesystem paths, remote filesystem mounts, OSS object storage, HDFS, and other file storage systems.
FR-5.12: The system shall detect cross-repo page navigation: URL patterns, deep links, and app-to-app navigation flows in both native and web applications.
FR-5.13: The system shall resolve communication endpoints to target Repositories using a Service Topology — resolved via an MCP tool that queries a mock service platform.
FR-5.14: The detection system shall be extensible — new communication patterns shall be addable to the Communication Pattern Catalog without code changes.

FR-6.1: When a Worker Agent detects a cross-repo communication but the target Repository has not been scanned, it shall record an Uncertainty with status unresolved and sub_status cross_repo.
FR-6.2: Cross-Repository Resolution shall run after each Repository scan completes. It shall match unresolved cross-repo uncertainties against the newly-scanned repo's documents using repo-qualified endpoint matching. Resolution is bidirectional.
FR-6.3: When two Repositories have mutual pending cross-repo uncertainties (deadlock), the Deadlock-Breaking Protocol shall apply: the repo with fewer pending uncertainties resolves first; if tied, the older scan yields.
FR-6.4: The system shall support Dangling Links — cross-repo uncertainties that never resolve. The wontfix terminal state shall be available for human-accepted dangling links.
FR-6.5: The system shall provide a chat-accessible uncertainty summary: "This Document has N unresolved cross-repo links" with endpoint identifiers and target repo names where known.
FR-6.6: Cross-Repo Stale Notices shall be posted when a document changes, notifying other repos that link to it. No automatic cascade — eventually consistent by design.
FR-6.7: The system shall provide an Impact Analysis query that finds all Documents affected by a set of changed source files: direct impact → intra-repo transitive closure → cross-repo reachability → uncertainty reopening.

FR-7.1: When a Worker Agent encounters an ambiguity it cannot resolve from code alone, it shall surface a question to the System Owner as a dashboard notification.
FR-7.2: System Owners shall answer agent questions through the dashboard. Answers are attached to the relevant Document as metadata.
FR-7.3: Human-provided answers shall be marked as "human input" and shall carry lower priority than code-derived facts when conflicts arise.
FR-7.4: The dashboard shall display a count of pending questions. System Owners shall be able to view, answer, or dismiss questions.

FR-8.1: System Owners shall add libraries with organization-owned source code as standalone Repositories (not belonging to any System).
FR-8.2: Standalone Repositories shall be scanned partially and on-demand — only the Code Units actually called by other Repositories are scanned.
FR-8.3: The system shall use a lock mechanism to prevent concurrent scanning of the same standalone Repository from multiple callers.
FR-8.4: Well-known common libraries (standard libraries, widely-used open-source packages) shall be treated as understood and shall not generate cross-repo uncertainties.
FR-8.5: For unknown libraries without available source code, the system shall attempt LLM-based inference of behavior. If inference fails, the user shall be notified to provide context manually.

FR-9.1: The system shall monitor connected git repositories for remote changes (periodic polling).
FR-9.2: When the local clone is behind the remote, the system shall pull changes.
FR-9.3: After pulling changes, the system shall incrementally re-scan only the affected Code Units.
FR-9.4: The system shall preserve Document history — previous versions of updated Documents shall be archived and accessible.
FR-9.5: Incremental re-scan shall not start if a Scan is already in progress for the same Repository.

FR-10.1: System Owners shall configure database connections per Repository — specifying connection string, database type, and access credentials.
FR-10.2: During scanning, Worker Agents shall be able to inspect database schemas (tables, columns, types, constraints) via a tool interface.
FR-10.3: During scanning, Worker Agents shall be able to query sample data (limited rows) via a tool interface.

FR-11.1: Business Mode shall present a lighter UI theme and use a system prompt focused on business process explanation. Answers shall be business-level narratives without code references.
FR-11.2: IT Mode shall present a darker UI theme and use a system prompt focused on technical detail. Answers shall include code references and source links.
FR-11.3: Users shall switch between Business Mode and IT Mode from the chat interface by confirming a new chat session in the selected mode.
FR-11.4: Users shall select which Systems or Repositories to include as context for their questions.
FR-11.5: Users shall upload files (documents, images) as supplementary context for their questions.
FR-11.6: The Chat Agent shall use typed LLM-Wiki retrieval tools over database-backed indexes and graph tables: search, section retrieval, graph expansion, uncertainty, endpoint, flow-trace, source-anchor, and scan-status context. Retrieval shall be section-level by default and `index.md` shall not be the canonical retrieval input.
FR-11.7: Every answer shall include citations linking claims to the source Documents and source code lines.
FR-11.8: When no Systems or Repositories are selected, Vulgata shall autonomously determine which Systems or Repositories are relevant to the user's question.

FR-12.1: The dashboard shall display real-time Scan status for each System: not started, profiling, scanning, completed, failed.
FR-12.2: During an active Scan, the dashboard shall display the number of active Worker Agents.
FR-12.3: During an active Scan, the dashboard shall display the count of files processed and total files to process.
FR-12.4: During an active Scan, the dashboard shall display the count of errors encountered.
FR-12.5: The dashboard shall display pending Human-in-the-Loop questions with the ability to view, answer, or dismiss each.
FR-12.6: The dashboard shall include a live knowledge graph visualization using Blazor.Diagrams: Documents as nodes, cross-repo links as edges, unresolved uncertainties as dashed edges to placeholder nodes.

FR-13.1: The scanning pipeline shall be able to consume MCP tools registered at the Repository, System, or global level. (DEFERRED)
FR-13.2: The chat interface shall be able to consume MCP tools registered at the Repository, System, or global level. (DEFERRED)
FR-13.3: MCP tools shall require separate approval for scanning use and chat use. (DEFERRED)
FR-13.4: Vulgata shall expose its knowledge base through the same authorization-aware retrieval model used by chat, including Documents, `index_entries`, `document_sections`, graph relationships, flow traces, uncertainties, source anchors, and cross-repo links. (DEFERRED)
FR-13.5: MCP tools shall be configurable at three levels: Repository, System, and Global. (DEFERRED)

FR-14.1: Administrators shall supply context at the global level (applies to all Systems).
FR-14.2: System Owners shall supply context at the System level (applies to all Repositories in that System).
FR-14.3: System Owners shall supply context at the Repository level (applies to a single Repository).
FR-14.4: User-supplied context shall only be applied when no Scan is running for the affected scope, or when responding to an agent question during HITL. Context changes during an active Scan shall be queued.

FR-15.1: The scanning pipeline shall optionally integrate with Language Servers for symbol resolution. (DEFERRED)
FR-15.2: The scanning pipeline shall optionally use LSP type information to augment LLM-based code understanding. (DEFERRED)
FR-15.3: LSP integration shall be configurable per Repository, with language-specific server configuration. (DEFERRED)

### NonFunctional Requirements

NFR-1.1: A Scan of a repository with ~10,000 source files shall complete within a timeframe that allows the demo to be prepared in advance.
NFR-1.2: Chat responses shall be delivered within 30 seconds for questions spanning up to 5 Documents.
NFR-1.3: The live knowledge graph visualization shall update within 5 seconds of a Document being generated or a cross-repo link being resolved.

NFR-2.1: All user passwords shall be hashed using a modern algorithm (bcrypt or Argon2).
NFR-2.2: API keys for LLM Providers and database connections shall be stored encrypted at rest.
NFR-2.3: Source code and generated Documents shall never leave the organization's infrastructure when using a self-hosted LLM Provider.
NFR-2.4: Database connection tools shall only connect to non-production environments or read-only replicas. Write operations on connected databases shall be blocked.

NFR-3.1: A failed Worker Agent task shall not fail the entire Scan. The Orchestrator shall retry the task once, then record the failure and continue.
NFR-3.2: The system shall gracefully handle LLM API unavailability — queuing or pausing agent tasks until the API recovers.
NFR-3.3: Scan state shall be persisted such that a server restart does not lose progress on an in-progress Scan.

NFR-4.1: Vulgata shall deploy as a single Docker container (docker-compose with PostgreSQL).

NFR-5.1: A non-technical user shall be able to log in, select a System, and ask a business question in Business Mode without training or documentation.
NFR-5.2: The dashboard shall use Blazor's built-in accessibility features.

NFR-6.1: Agent prompts shall be externalized from code — stored as configurable text resources, not hardcoded strings.
NFR-6.2: The Communication Pattern Catalog format shall be documented so that System Owners can write catalogs for new Systems without developer assistance.

### Additional Requirements

From Architecture — Technical implementation requirements that affect epic and story creation:

- **Starter Template**: Architecture specifies a greenfield Blazor Web App with ASP.NET Core Identity (individual accounts). The Blazor template's built-in Login/Register/Manage pages are used with Fluent UI styling. This impacts Epic 1 Story 1.
- **Database**: PostgreSQL 17 via EF Core with Npgsql provider. Two DbContexts (ApplicationDbContext for Identity, VulgataDbContext for domain) sharing one database with separate schemas. Migrations run at startup via MigrateAsync().
- **Container Deployment**: docker-compose with two containers (Blazor app + PostgreSQL 17). Multi-stage Dockerfile. Git and CodeGraph CLI installed in runtime stage. Non-root user.
- **UI Framework**: Microsoft Fluent UI Blazor as primary component library. Z.Blazor.Diagrams for graph visualization only. CommunityToolkit.Mvvm for MVVM source generators.
- **Agent Framework**: Microsoft.Agents.AI.Foundry + Microsoft.Agents.AI.Workflows (prerelease) for Worker Agent execution. Week 1 Worker Agent adapter validation spike required with documented fallback.
- **LLM Client**: OpenAI SDK for DeepSeek V4 via OpenAI-compatible endpoint. Multi-provider with automatic failover.
- **Real-time**: ASP.NET Core SignalR with two hubs (ScanHub, GraphHub). Diff-based graph updates. All hubs require [Authorize].
- **Code Analysis**: CodeGraph CLI for pre-scan structural analysis. Shell invocation via CodeGraphCliService.
- **Git Operations**: System git installed in container. GitCloneService shells out for clone/pull at scan time.
- **Background Jobs**: BackgroundService + Channel<T> for agent task execution. ScanJobWorker dequeues and processes work items off the request thread.
- **Domain Events**: Collected during SaveChanges, dispatched after commit via IDomainEventDispatcher. Separate from agent integration events.
- **Validation**: FluentValidation for all input validation. Validators registered via AddValidatorsFromAssemblyContaining<T>(). Blazor forms use FluentValidationValidator component.
- **API Design**: Minimal API endpoints under /api/ prefix. ProblemDetails for error responses. No wrapping envelope.
- **Repository Pattern**: Specific repositories per aggregate root. Interfaces in Core, implementations in Infrastructure. No generic IRepository<T>. No IQueryable or DbSet exposure.
- **EF Core Configuration**: IEntityTypeConfiguration<T> classes in Configurations/ folder. Assembly scanning for registration. No data annotations on entities.
- **Logging**: ILogger<T> with structured logging. Decision rules for log levels (Debug/Information/Warning/Error/Critical).
- **LoadState Enum**: Idle, Loading, Loaded, Refreshing, Empty, NoResults, Error, Cancelling. Every data-loading ViewModel exposes LoadState.
- **Agent Task State Machine**: Queued → Running → Completed | Failed | Cancelled. One retry with fallback LLM provider on failure.
- **SignalR Reconnection**: On reconnect, server replays current scan/graph status. Scan progress uses structured model (Phase, CurrentFile, FilesProcessed, TotalFiles, ReferencesFound, EstimatedSecondsRemaining).
- **Naming Conventions**: Async suffix mandatory. _camelCase private fields. PascalCase tables/columns. /api/ prefix + plural nouns. Past-tense domain events and hub methods.
- **Project Structure**: 5 .NET projects (Web, Web.ViewModels, Core, Infrastructure, Agents) + Shared. Future Java/Python/Node placeholder directories.
- **Security**: ASP.NET Core Identity with cookie auth. Three roles (Administrator, SystemOwner, User) seeded at startup. API keys encrypted via ASP.NET Core Data Protection.
- **Configuration Externalization**: Agent prompts as embedded resources in Vulgata.Agents/Prompts/. Communication Pattern Catalog as documented format.
- **PostgreSQL Specific**: Recursive CTEs for graph traversal. MVCC for concurrent read/write. Connection pooling via Npgsql. Separate migration history tables for two DbContexts.
- **Docker Volumes**: pgdata named volume for PostgreSQL persistence. ./prompts bind mount for prompt iteration.
- **Development Workflow**: dotnet run, dotnet build, dotnet test, dotnet format. Hot reload enabled. Local dev without Docker possible (just PostgreSQL container).
- **Testing**: Single test project (Vulgata.Tests) mirroring source structure. Integration tests with SampleRepositories. Test naming: {Method}_{Scenario}_{ExpectedResult}.
- **Enforcement**: .editorconfig for naming rules. Roslyn analyzers for patterns .editorconfig cannot express. dotnet format in CI. Code review checklist.

### UX Design Requirements

From `DESIGN.md` and `EXPERIENCE.md` (`docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/`).

**UX-DR-1 (Brand & Style):** Library Oak theme — warm professional register. Two brand colors: primary `#445E7A` (light) / `#6A83A2` (dark), accent `#B98B6B` (light) / `#D4A88C` (dark). All UI inherits Fluent UI Blazor defaults except primary buttons, mode badge, system/repo tags, and display typography.

**UX-DR-2 (Typography):** Display text in Noto Serif SC (思源宋体) at 32px/22px — used for empty-state greetings, section headers, mode-select banner. All body, label, button, form, and chat text inherits Fluent's default sans-serif.

**UX-DR-3 (Dual-Mode Theme):** 业务模式 locked to light theme, 技术模式 locked to dark theme. Mode switch starts a new session with confirmation dialog. Theme transition 200-400ms.

**UX-DR-4 (Language):** Chinese-only UI (Simplified). All labels, buttons, placeholders, empty states, and system messages are in Chinese. No i18n for V1.

**UX-DR-5 (Information Architecture):** Two top-level routes — 对话 (chat, default landing) and 管理后台 (management). Chat-first: login lands in chat, not a dashboard. Top navbar: brand + 对话/管理后台 nav + bell icon (HITL notifications) + user avatar.

**UX-DR-6 (Management Holy Grail Layout):** 管理后台 uses holy grail layout — top tab bar (系统管理/图谱/文档/扫描历史/设置) + left sidebar (content varies by tab) + main content area. System/repo creation via inline dialogs, not separate pages.

**UX-DR-7 (Chat Interface Structure):** Top navbar → mode selector (业务/技术 toggle) → inline system/repo selector → message bubble stream → fixed-bottom input. User bubbles right-aligned (surface bg), assistant left-aligned (muted bg) with markdown + citation links.

**UX-DR-8 (Mode Selector):** Dual-tab toggle above chat input. Switching modes shows confirmation dialog and starts a new session. Current mode shown as compact badge (non-interactive label).

**UX-DR-9 (System/Repo Selector):** Inline Fluent Dropdown multi-select above chat, below mode selector. Remember last selection; default to all authorized systems.

**UX-DR-10 (System Tree View):** Fluent TreeView — systems as parent nodes, repos as children. Active node highlighted in accent color. Scan status dots: gray (unscanned), pulsing green (scanning), green (complete), red (failed). "+ 新建系统" button at top.

**UX-DR-11 (Dashboard — Repo Detail):** Repo name, description, edit/delete. Info cards: git URL, last scan, scan status, document count. "开始扫描" button (when idle) or progress bar + phase indicator (when scanning).

**UX-DR-12 (Dashboard — System Detail):** System name, description, edit/delete. Stat cards: total repos, scanning, completed, total docs. FluentDataGrid of repos with status, last scan, doc count. "+ 新建仓库" button.

**UX-DR-13 (Knowledge Graph):** Full-screen Z.Blazor.Diagrams page under 管理后台 → 图谱. Floating toolbar: layout toggle (hierarchical/force-directed), zoom, system/repo filter. Nodes: blue/green by doc type, red border for failed docs. Edges: solid gray (intra-repo), dashed colored (cross-repo), dashed + red diamond (unresolved), yellow dashed + warning icon (disputed). 200-400ms animations.

**UX-DR-14 (Document Viewer):** Split-pane under 管理后台 → 文档. Left: virtual directory tree with type badges (blue=CL, green=BL) + search. Right: Markdown-rendered doc with title, type badge, repo/source path.

**UX-DR-15 (Notification Center):** Slide-out right panel (400px) triggered by bell icon. HITL question cards: repo name, question summary, timestamp, answer/dismiss. Red badge count on bell icon for unread.

**UX-DR-16 (Empty States):** Noto Serif SC greeting per mode ("你好，想了解什么业务流程？" / "你好，需要追踪哪段逻辑？"). No-system: CTA "创建系统". No-repo: CTA "新建仓库". No-graph/docs: CTA "去系统管理". No-notifications: informational text.

**UX-DR-17 (LoadState Enum):** Loading=Fluent Shimmer skeleton; Refreshing=content visible + top indeterminate bar; Empty=guided CTA; NoResults=filter adjustment guidance; Error=non-blaming + retry (tech details in IT mode only).

**UX-DR-18 (Voice & Tone):** 业务模式 — plain, narrative, no jargon. 技术模式 — precise, code-aware, direct. Notifications — neutral, actionable. Errors — no blame, state fact + next step.

**UX-DR-19 (Component Discipline):** Fluent UI defaults for 90% of components. Brand overrides only for primary buttons (send/scan/save), mode badge, system/repo tags, chat bubbles, mode-select banner, HITL notification dot, tree-view active node, and graph. No gradient surfaces, no pill shapes, no custom radius/spacing.

### FR Coverage Map

| FR | Epic | Description |
|----|------|-------------|
| FR-1.1 | Epic 1 | User registration with email and password |
| FR-1.2 | Epic 1 | User login and logout |
| FR-1.3 | Epic 1 | View and edit own profile |
| FR-1.4 | Epic 1 | Role-based access (Administrator, SystemOwner, User) |
| FR-2.1 | Epic 2 | Create, edit, delete Systems |
| FR-2.2 | Epic 2 | Add Repositories to a System |
| FR-2.3 | Epic 2 | Remove Repositories from a System |
| FR-2.4 | Epic 2 | Create standalone Repositories |
| FR-2.5 | Epic 2 | Validate git URL reachability |
| FR-3.1 | Epic 3 | Configure LLM Providers at global level with default LLM task-type assignment |
| FR-3.2 | Epic 3 | Support multiple LLM Providers simultaneously |
| FR-3.3 | Epic 3 | System Owners may override global default provider per LLM task role |
| FR-3.4 | Epic 3 | Connection test for each LLM Provider |
| FR-10.1 | Epic 3 | Configure database connections per Repository (connection string, type, credentials) |
| FR-4.1 | Epic 4 | Pre-Scan Profiling: CodeGraph → Code Units; agents may ask questions; scan waits for approval |
| FR-4.11 | Spike Gate | Week 1 Worker Agent execution adapter validation spike (go/no-go gate) |
| FR-4.2 | Epic 5 | Scan Coordinator + deterministic Orchestrator: queue, concurrency, git ops, typed Worker Agent dispatch |
| FR-4.3 | Epic 5 | Worker Agent: read Code Unit, identify business logic, produce Document |
| FR-4.4 | Epic 5 | Pass 1: Code Logic Documents, intra-repo REFERENCES, cross-repo Uncertainties |
| FR-4.5 | Epic 5 | Pass 2: path-aware flow tracing, flow-group clustering, BL Docs with GENERATED_FROM edges |
| FR-4.6 | Epic 5 | Document tree structure mirroring source directory hierarchy |
| FR-4.7 | Epic 5 | Materialize `index_entries` and `document_sections`; export `index.md` only as readability artifact |
| FR-4.8 | Epic 5 | Append-only log.md with parseable timestamps |
| FR-4.9 | Epic 5 | CodeGraph integration for pre-indexed structural information |
| FR-4.10 | Epic 5 | Document immutability with Version Chain (superseded_by_id) |
| FR-4.12 | Epic 5 | Cross-verification of related Code Units in same superstep |
| FR-10.2 | Epic 5 | Worker Agent database schema inspection tool |
| FR-10.3 | Epic 5 | Worker Agent sample data query tool (limited rows) |
| FR-6.1 | Epic 5 | Record Uncertainty with unresolved/cross_repo status |
| FR-6.2 | Epic 5 + Epic 7 | Epic 5 builds same-repo/best-effort infrastructure; Epic 7 completes bidirectional Cross-Repository Resolution with full detection evidence |
| FR-6.3 | Epic 5 + Epic 7 | Deadlock-Breaking Protocol recorded in infrastructure and applied during final resolution |
| FR-6.4 | Epic 5 | Dangling Links — wontfix terminal state |
| FR-6.5 | Epic 5 + Epic 8 | Chat/dashboard-accessible uncertainty summary |
| FR-6.6 | Epic 5 + Epic 7 | Cross-Repo Stale Notices on document change after resolution updates graph truth |
| FR-6.7 | Epic 5 + Epic 7 | Impact Analysis query (direct → intra-repo → cross-repo → uncertainty) |
| FR-8.1 | Epic 6 | Add org-owned libraries as standalone Repositories |
| FR-8.2 | Epic 6 | Partial on-demand scanning of standalone repos |
| FR-8.3 | Epic 6 | Lock mechanism preventing concurrent standalone repo scanning |
| FR-8.4 | Epic 6 | Well-known common libraries treated as understood |
| FR-8.5 | Epic 6 | LLM-based inference for unknown libraries without source |
| FR-5.1 | Epic 7 | Configure CP Catalog; RPC catalogs in pre-scan context with agent validation |
| FR-5.2 | Epic 7 | Detect cross-repo communication: RPC, HTTP, MQ, file, page nav |
| FR-5.3 | Epic 7 | Identify provider and consumer roles |
| FR-5.4 | Epic 7 | Track consumer count per communication interface |
| FR-5.5 | Epic 7 | Handle absent provider/consumer as external references |
| FR-5.6 | Epic 7 | Flag cross-repo page navigation as low-confidence |
| FR-5.7 | Epic 7 | Object flow analysis for indirect communication targets |
| FR-5.8 | Epic 7 | Dynamic CP Catalog injection into Worker prompt |
| FR-5.9 | Epic 7 | Prompt Workbench for agent prompt iteration |
| FR-5.10 | Epic 7 | Record RPC endpoints as named entities (even unscanned) |
| FR-5.11 | Epic 7 | File-based communication detection (local, remote, OSS, HDFS) |
| FR-5.12 | Epic 7 | Cross-repo page navigation detection (URLs, deep links, app-to-app) |
| FR-5.13 | Epic 7 | Service Topology resolution via MCP tool |
| FR-5.14 | Epic 7 | Extensible detection — new patterns without code changes |
| FR-7.1 | Epic 8 | Surface agent ambiguities as dashboard notifications |
| FR-7.2 | Epic 8 | Answer agent questions through dashboard |
| FR-7.3 | Epic 8 | Human input marked with lower priority than code-derived facts |
| FR-7.4 | Epic 8 | Dashboard pending question count with view/answer/dismiss |
| FR-12.1 | Epic 8 | Real-time Scan status: not started, profiling, scanning, completed, failed |
| FR-12.2 | Epic 8 | Display active Worker Agent count during Scan |
| FR-12.3 | Epic 8 | Display files processed / total files during Scan |
| FR-12.4 | Epic 8 | Display error count during Scan |
| FR-12.5 | Epic 8 | Display pending HITL questions with view/answer/dismiss |
| FR-12.6 | Epic 8 | Live knowledge graph: Blazor.Diagrams, nodes, edges, dashed uncertainties |
| FR-11.1 | Epic 9 | Business Mode: lighter UI, business narrative, no code refs |
| FR-11.2 | Epic 9 | IT Mode: darker UI, technical detail, code references |
| FR-11.3 | Epic 9 | Switch between Business Mode and IT Mode via confirmed new session |
| FR-11.4 | Epic 9 | Select Systems/Repositories as chat context |
| FR-11.5 | Epic 9 | Upload files as supplementary chat context |
| FR-11.6 | Epic 9 | Database-backed LLM-Wiki retrieval: typed tools → sections → graph expansion → synthesize |
| FR-11.7 | Epic 9 | Citations linking claims to source Documents and code lines |
| FR-11.8 | Epic 9 | Auto-determine relevant Systems/Repos when none selected |
| FR-9.1 | Epic 10 | Monitor git repos for remote changes (periodic polling) |
| FR-9.2 | Epic 10 | Pull changes when local clone behind remote |
| FR-9.3 | Epic 10 | Incremental re-scan of only affected Code Units |
| FR-9.4 | Epic 10 | Preserve Document history for updated Documents |
| FR-9.5 | Epic 10 | Block incremental re-scan if Scan in progress |
| FR-13.1 | Deferred | MCP tool consumption for scanning pipeline |
| FR-13.2 | Deferred | MCP tool consumption for chat interface |
| FR-13.3 | Deferred | Separate MCP approval for scanning vs chat |
| FR-13.4 | Deferred | Expose authorization-aware knowledge base retrieval as MCP server |
| FR-13.5 | Deferred | MCP tools at Repository/System/Global levels |
| FR-14.1 | Epic 2 | Administrators supply context at global level |
| FR-14.2 | Epic 2 | User-supplied context at System level |
| FR-14.3 | Epic 2 | User-supplied context at Repository level |
| FR-14.4 | Epic 2 | Context applied only when no Scan running or during HITL |
| FR-15.1 | Deferred | LSP integration for symbol resolution |
| FR-15.2 | Deferred | LSP type information for code understanding |
| FR-15.3 | Deferred | LSP configurable per Repository with language-specific settings |

## Epic List

### Epic 1: Foundation & Identity
Users can register, log in, manage their profile, and access the application with role-based authorization. Establishes the Blazor Web App shell with Fluent UI layout, navigation, Docker + PostgreSQL infrastructure, and ASP.NET Core Identity with three seeded roles.
**FRs covered:** FR-1.1, FR-1.2, FR-1.3, FR-1.4
**Architecture:** SP-1 — Greenfield Blazor Web App starter template, docker-compose with PostgreSQL 17, ASP.NET Core Identity (cookie auth), Fluent UI shell, role seeding (Administrator/SystemOwner/User), ApplicationUser + ApplicationDbContext.

### Epic 2: System & Repository Management
System Owners can create and manage Systems, add/remove Repositories with git URL validation, and supply context at system and repository levels. Administrators supply global-level context. Context changes are applied only when no scan is running or during HITL.
**FRs covered:** FR-2.1, FR-2.2, FR-2.3, FR-2.4, FR-2.5, FR-14.1, FR-14.2, FR-14.3, FR-14.4
**Architecture:** SP-2 — System and Repository domain entities (aggregate roots), ISystemRepository/IRepositoryRepository, CRUD pages with Fluent UI DataGrid, git URL reachability check, user-supplied context with scope-based application timing rules (global=Administrator only, system/repo=SystemOwner).

### Epic 3: LLM Provider & Database Connection Configuration
Administrators configure multiple LLM backends at the global level with default LLM task-type assignments, and test connections. System Owners may override global defaults per LLM task role within their System. The deterministic Orchestrator does not need an LLM Provider assignment. System Owners also configure database connections per Repository for schema inspection and sample data queries during scanning. Includes automatic provider failover and API key encryption.
**FRs covered:** FR-3.1, FR-3.2, FR-3.3, FR-3.4, FR-10.1
**Architecture:** LLMProvider entity, LLMProviderManager with multi-provider failover, API key encryption via ASP.NET Core Data Protection, connection testing, global default + per-System override provider assignment, database connection configuration per Repository, Administrator configuration pages.

### Pre-Epic Gate: Worker Agent Execution Adapter Validation Spike
Before any scanning infrastructure is built, validate that the Microsoft Agent Framework can execute Vulgata Worker Agents and typed tool calls under deterministic Orchestrator control. This is a go/no-go decision point for the Worker Agent execution adapter; if MAF fails, a documented fallback adapter must be selected before Worker Agent implementation proceeds.
**FRs covered:** FR-4.11
**Architecture:** Isolated prototype — instantiate the deterministic Orchestrator harness and a Worker Agent execution adapter, dispatch mock typed jobs, verify tool-call wiring and result collection. Document fallback options (direct OpenAI SDK, LangChain.NET, custom worker adapter) with migration effort estimates.

### Epic 4: Pre-Scan Profiling
Before worker dispatch, the system profiles each Repository using CodeGraph/tree-sitter to parse source into Code Units with exact line ranges, identify programming languages and frameworks, apply a scan filter, and derive deterministic communication-kind hypotheses. For the August 15 demonstration, all profiling outcomes continue conservatively to an automatically approved pre-scan handoff; the LLM question generator and Human-in-the-Loop review lifecycle are deferred.
**FRs covered:** FR-4.1
**Architecture:** CodeGraphContextProvider HTTP integration, Code Unit data model (file path, line range, language, framework), scan filter rules, deterministic communication-kind hypotheses, and a persisted automatically approved handoff. Output: a structured pre-scan result ready for the deadline delivery slice; Epic 4 does not implement the Scan Coordinator queue or Worker Agent dispatch.

### Epic 11: August 15 Demonstration Slice
Deliver one prepared two-Repository path from source profiling through sequential Worker Agent generation, exact RPC/HTTP Cross-Repository matching, database-backed retrieval, and a cited Business/IT chat answer. This is the deadline delivery overlay; it implements deliberately narrow seams that later Epics 5–10 deepen rather than duplicate.
**FRs covered for the demonstration:** FR-4.2–4.8 (sequential/minimal subset), FR-5.1–5.5 and FR-5.8 (configured RPC/HTTP subset), FR-6.1–6.3 (exact prepared-flow subset), FR-11.1–11.6 (prepared-scope subset), NFR-2.3, NFR-3.3, NFR-4.1, NFR-6.1
**Architecture:** PostgreSQL-backed minimal Document/source/reference/retrieval store; one-Scan-at-a-time deterministic runner; one typed Worker Agent path for Pass 1 and Pass 2; configured exact RPC/HTTP matching for two prepared Repositories; Chinese Business/IT chat with citations; one final clean-database E2E and Docker build.

### Epic 5: Core Single Repository Scanning
The core scanning engine: Document Graph Store as the data foundation, Scan Coordinator managing the scan lifecycle, deterministic Orchestrator planning the per-Repository workflow, and LLM-powered Worker Agents generating Code Logic Documents (Pass 1) and Business Logic Documents (Pass 2). Cross-repo calls detected during scanning are recorded as Uncertainties and dispatch sites; Epic 5 owns same-Repository resolution and best-effort matching against already-scanned Repositories, while final Cross-Repository Resolution is completed in Epic 7 after full detection evidence exists. Includes database schema inspection and sample data query tools. The dashboard receives its first minimal version (scan progress only) during this epic.
**FRs covered:** FR-4.2, FR-4.3, FR-4.4, FR-4.5, FR-4.6, FR-4.7, FR-4.8, FR-4.9, FR-4.10, FR-4.12, FR-6.1, FR-6.2, FR-6.3, FR-6.4, FR-6.5, FR-6.6, FR-6.7, FR-10.2, FR-10.3
**Architecture:** SP-3 + SP-4 + SP-5 + SP-7 — Logical Document/Document Version, Edge, Uncertainty, ScanRun, entry point, endpoint, dispatch-site, flow-group, flow-trace, `index_entries`, and `document_sections` entities; recursive CTE query services; export `index.md`/`log.md` generation; BackgroundService + Channel<T> scan job worker; GitCloneService; CodeGraphCliService; V1 concurrency control; deterministic Orchestrator; Worker Agent execution adapter; superstep dispatch; Pass 1 CL Docs; bounded refinement; Pass 2 path-aware trace planning and flow-group BL Docs; lightweight cross-verification; document pre-allocation; uncertainty recording engine with cross-repo deferral; deadlock-breaking protocol; dangling links; stale notices; impact analysis; database schema inspection + sample data query tools; minimal scan progress dashboard.

### Epic 6: Third-Party Library Handling
System Owners can add organization-owned libraries as standalone repositories for partial on-demand scanning. Well-known libraries are treated as understood; unknown libraries trigger LLM-based inference with user notification fallback.
**FRs covered:** FR-8.1, FR-8.2, FR-8.3, FR-8.4, FR-8.5
**Architecture:** Standalone Repository scanning (no System), on-demand partial scan triggered by caller's Worker Agent, lock mechanism preventing concurrent scans, well-known library allowlist, LLM inference fallback for unknown libraries.

### Epic 7: Cross-Repository Detection
The detection layer identifies when code in one Repository communicates with another across five communication types (RPC, HTTP, MQ, file-based, page navigation). Includes Communication Pattern Catalog configuration with RPC catalog validation during pre-scan, provider/consumer role tagging, object flow analysis, Prompt Workbench, Service Topology resolution, and extensible pattern support. This epic also completes the cross-repo uncertainties that were recorded but left unresolved during Epic 5 — it runs the resolution engine with the newly built cross-repo detection results.
**FRs covered:** FR-5.1, FR-5.2, FR-5.3, FR-5.4, FR-5.5, FR-5.6, FR-5.7, FR-5.8, FR-5.9, FR-5.10, FR-5.11, FR-5.12, FR-5.13, FR-5.14, FR-6.2, FR-6.3, FR-6.6, FR-6.7
**Architecture:** SP-6 — CommunicationPattern entity + catalog, RPC catalog pre-scan validation, Worker prompt dynamic injection, provider/consumer role detection, consumer count tracking, absent entity external references, page navigation low-confidence flagging, object flow analysis, Prompt Workbench development tool, named RPC endpoint recording, file-based detection (local/remote/OSS/HDFS), page navigation detection (URLs/deep links/app-to-app), Service Topology MCP tool integration, extensible catalog format, cross-repo uncertainty resolution engine integration.

### Epic 8: Dashboard & Real-Time Monitoring
System Owners see live scan progress (minimal version available after Epic 5, enhanced here) and a real-time knowledge graph visualization using Blazor.Diagrams showing Documents as nodes, cross-repo links as edges, and unresolved uncertainties as dashed edges. System Owners can answer agent questions surfaced during scanning via Human-in-the-Loop.
**FRs covered:** FR-7.1, FR-7.2, FR-7.3, FR-7.4, FR-6.5, FR-12.1, FR-12.2, FR-12.3, FR-12.4, FR-12.5, FR-12.6
**Architecture:** SP-9 — ScanHub + GraphHub (SignalR with [Authorize]), Blazor.Diagrams live graph (nodes=Documents, edges=REFERENCES, dashed=uncertainties, yellow=disputed), scan progress dashboard (status, workers, files, errors), HITL question management (notifications, answer, dismiss), diff-based graph updates with viewport preservation, SignalR reconnection with state replay.

### Epic 9: Chat Interface
Users ask natural-language questions in Business Mode (non-technical narrative) or IT Mode (technical detail with code references) and receive grounded answers with citations using database-backed LLM-Wiki retrieval across scanned Documents. When no Systems or Repositories are selected, Vulgata autonomously determines the relevant scope.
**FRs covered:** FR-11.1, FR-11.2, FR-11.3, FR-11.4, FR-11.5, FR-11.6, FR-11.7, FR-11.8
**Architecture:** SP-8 — Chat Agent, typed retrieval tools over `index_entries`, `document_sections`, graph relationships, endpoint records, flow traces, uncertainties, and source anchors; Business/IT dual mode with theme switching and system prompt changes; System/Repository context selection; auto-scope determination when nothing selected; file upload support; citation linking to source Documents and code lines.

### Epic 10: Git Monitoring & Incremental Re-scan ⚠️ Deferrable
Documents stay current with code changes — periodic git polling detects remote changes, pulls updates, and incrementally re-scans only affected Code Units using two-phase change detection.
**FRs covered:** FR-9.1, FR-9.2, FR-9.3, FR-9.4, FR-9.5
**Architecture:** Git polling service, pull on behind-remote detection, two-phase change detection (line-overlap filter → hash comparison), incremental re-scan with impact analysis, document history preservation, concurrent scan guard.

### Deferred Features
**FRs covered:** FR-13.1, FR-13.2, FR-13.3, FR-13.4, FR-13.5 (MCP Integration), FR-15.1, FR-15.2, FR-15.3 (LSP Support)
Architecture supports adding these later via Infrastructure project and MCP/LSP client packages. No stories generated for deferred features.

---

## Stories

### Pre-Epic Gate: Worker Agent Execution Adapter Validation Spike

#### Story SP.1: Worker Agent Execution Adapter Validation Spike

As a **developer**,
I want to validate that the Microsoft Agent Framework can execute Vulgata Worker Agents through typed jobs under deterministic Orchestrator control,
So that the team can commit to an execution adapter for Worker Agent tasks or select a documented fallback.

**Acceptance Criteria:**

**Given** a .NET console prototype project with Microsoft.Agents.AI.Foundry and Microsoft.Agents.AI.Workflows packages referenced
**When** the deterministic Orchestrator harness dispatches a mock superstep (batch of 3 synthetic Code Units) as typed Worker Agent jobs
**Then** all 3 Worker Agents shall receive and process their assigned Code Units independently
**And** the Orchestrator shall collect and aggregate all Worker Agent results through the adapter
**And** each Worker Agent shall log its assigned Code Unit path, start time, end time, and status (Completed/Failed)

**Given** one Worker Agent is configured to throw an exception during processing
**When** the Orchestrator dispatches the superstep
**Then** the failed Worker Agent task shall be retried once
**And** the Orchestrator shall continue processing remaining Worker Agents
**And** the final result shall include both successful results and the failed task with error details

**Given** the spike completes successfully
**When** the developer documents the results
**Then** a spike report shall be produced containing: (1) pass/fail verdict, (2) latency measurements per superstep, (3) any MAF API limitations discovered, (4) fallback architecture options with estimated migration effort if MAF is rejected

---

### Epic 1: Foundation & Identity

#### Story 1.1: Solution Scaffolding & Docker Deployment

As a **developer**,
I want a greenfield Blazor Web App solution with docker-compose, PostgreSQL 17, and the 6-project structure,
So that the team has a deployable foundation for all subsequent epics.

**Acceptance Criteria:**

**Given** the solution is scaffolded
**When** the developer runs `dotnet build`
**Then** all 6 projects (Vulgata.Web, Vulgata.Web.ViewModels, Vulgata.Core, Vulgata.Infrastructure, Vulgata.Agents, Vulgata.Shared) shall compile without errors
**And** placeholder directories shall exist at solution root for future Java, Python, and Node projects

**Given** the solution uses the Blazor Web App template with ASP.NET Core Identity (Individual Accounts)
**When** the developer inspects the project
**Then** Vulgata.Web shall use interactive server render mode with Fluent UI Blazor as the primary component library
**And** the default Identity pages (Login, Register, Manage) shall be scaffolded with Fluent UI styling

**Given** the solution
**When** configured
**Then** two top-level layouts shall exist: MainLayout (top navbar: brand + 对话/管理后台 nav + bell + avatar) and ManagementLayout (nested holy grail: top tab bar + left sidebar + main content)
**And** the default route "/" shall render the Chat page

**Given** docker-compose.yml exists at solution root
**When** `docker compose up` is executed
**Then** two containers shall start: the Blazor application and PostgreSQL 17
**And** the Blazor container shall have Git and CodeGraph CLI installed in its runtime stage
**And** the Blazor container shall run as a non-root user
**And** a pgdata named volume shall persist PostgreSQL data

**Given** the Dockerfile
**When** the image is built
**Then** it shall use multi-stage build (SDK stage → publish → runtime stage)
**And** a ./prompts bind mount shall be configured for prompt file iteration during development

**Given** the solution
**When** the developer runs `dotnet format`
**Then** an .editorconfig shall enforce naming conventions (async suffix, _camelCase private fields, PascalCase tables/columns)
**And** Roslyn analyzers shall cover patterns .editorconfig cannot express

#### Story 1.2: User Registration

As a **new user**,
I want to register with my email and password,
So that I can access Vulgata.

**Acceptance Criteria:**

**Given** I am on the registration page
**When** I enter a valid email and password meeting complexity requirements (minimum 8 characters, uppercase, lowercase, digit, special character)
**Then** my account shall be created
**And** my password shall be stored hashed using bcrypt or Argon2 (NFR-2.1)
**And** I shall be redirected to the login page or auto-signed-in

**Given** I enter an email already registered
**When** I submit the registration form
**Then** I shall see a validation error "该邮箱已被注册" (This email is already registered)

**Given** I enter a password that does not meet complexity requirements
**When** I submit the registration form
**Then** I shall see a validation error describing the missing requirements
**And** my account shall not be created

**Given** the ApplicationDbContext schema
**When** the database is migrated
**Then** Identity tables shall reside in a dedicated schema, separate from the Vulgata domain schema

#### Story 1.3: Login & Logout

As a **registered user**,
I want to log in with my email and password and securely log out,
So that I can access my authorized features and protect my session.

**Acceptance Criteria:**

**Given** I have a registered account
**When** I enter correct email and password on the login page
**Then** I shall be authenticated via ASP.NET Core cookie authentication
**And** I shall be redirected to the Chat page (default landing, UX-DR-5)

**Given** I enter incorrect credentials
**When** I submit the login form
**Then** I shall see a generic error "邮箱或密码错误" (Invalid email or password)
**And** I shall NOT be authenticated

**Given** I am logged in
**When** I click Logout
**Then** my authentication cookie shall be cleared
**And** I shall be redirected to the login page

**Given** I am logged in
**When** my authentication cookie remains valid
**Then** navigating to any page shall maintain my authenticated session
**And** the top navbar shall display: brand, 对话/管理后台 nav, bell icon, and user avatar dropdown (profile, logout)

**Given** I attempt to access a page requiring authentication while logged out
**When** I navigate to a protected route
**Then** I shall be redirected to the login page with a return URL parameter

#### Story 1.4: Profile Management

As a **registered user**,
I want to view and edit my profile information,
So that I can keep my display name, email, and password current.

**Acceptance Criteria:**

**Given** I am logged in
**When** I click my avatar in the top navbar and select "个人资料" (Profile)
**Then** I shall see my current display name and email displayed in a Fluent UI form

**Given** I am on the Profile page
**When** I change my display name and save
**Then** my display name shall be updated
**And** a success notification shall appear

**Given** I am on the Profile page
**When** I change my email to an address not already in use and save
**Then** my email shall be updated
**And** a confirmation message shall be displayed

**Given** I am on the Profile page
**When** I change my email to one already registered by another user and save
**Then** I shall see a validation error
**And** my email shall remain unchanged

**Given** I am on the Change Password section of the Profile page
**When** I enter my current password, a new password meeting complexity requirements, and confirm the new password
**Then** my password shall be updated
**And** I shall remain logged in with my current session

**Given** I enter an incorrect current password in the Change Password form
**When** I submit
**Then** I shall see an error "当前密码不正确" (Current password is incorrect)
**And** my password shall not be changed

#### Story 1.5: Role Seeding & Authorization Policies

As an **administrator**,
I want three predefined roles seeded at startup with appropriate authorization policies,
So that role-based access control is enforced from day one.

**Acceptance Criteria:**

**Given** the application starts for the first time
**When** the database is migrated
**Then** three roles shall be seeded: Administrator, SystemOwner, and User

**Given** the authorization policies
**When** configured
**Then** Administrator shall have full platform management access
**And** SystemOwner shall have access scoped to the Systems they are explicitly assigned to
**And** User shall have read-only access to chat and documents

**Given** I am logged in as a User
**When** I attempt to access an Administrator-only page
**Then** I shall be redirected to an "Access Denied" page
**And** the 管理后台 nav link shall not be visible in my top navbar (SystemOwner-only and Administrator, UX-DR-10)

**Given** the role seeding mechanism
**When** the application restarts
**Then** existing roles shall not be duplicated (idempotent seed)

#### Story 1.6: Administrator Role Assignment

As an **administrator**,
I want to promote a registered user to the Administrator role,
So that I can delegate platform management.

**Acceptance Criteria:**

**Given** I am logged in as an Administrator
**When** I navigate to 管理后台 → 设置 → 用户管理 (User Management)
**Then** I shall see a list of all registered users with their current roles

**Given** I am on the User Management page
**When** I select a User and assign them the Administrator role
**Then** the user shall immediately gain Administrator privileges
**And** a confirmation notification shall appear

**Given** I am on the User Management page
**When** I remove the Administrator role from a user
**Then** the user shall revert to the User role (unless they hold SystemOwner on specific Systems)
**And** they shall immediately lose Administrator access

**Given** I am logged in as a SystemOwner or User
**When** I view the management interface
**Then** the 用户管理 page shall not be visible in settings navigation
**And** I shall be denied access if I attempt to navigate to it directly

**Given** the first Administrator account
**When** the application starts with an empty database
**Then** the registration of the very first user shall automatically assign the Administrator role
**And** subsequent registrations shall default to the User role

---

### Epic 2: System & Repository Management

#### Story 2.1: System CRUD (SystemOwner/Administrator)

As a **SystemOwner or administrator**,
I want to create, edit, and delete Systems,
So that I can organize repositories into logical groupings.

**Acceptance Criteria:**

**Given** I am logged in as a SystemOwner or Administrator
**When** I navigate to 管理后台 → 系统管理
**Then** I shall see the system tree view in the left sidebar (FluentTreeView, UX-DR-10)
**And** the main content area shall show system stats when a system is selected

**Given** I am on the system tree view
**When** I click "+ 新建系统" and enter a name (required), description (optional), and plain-text supplementary context (optional) in an inline CreateSystemDialog
**Then** the System shall be created
**And** it shall appear in the system tree immediately
**And** if I am a SystemOwner, I shall be assigned as an owner of the newly-created System

**Given** a System exists
**When** I right-click the system node and select "编辑" (Edit) to open an edit dialog
**Then** I shall be able to modify its name, description, or plain-text context
**And** the changes shall be saved and reflected immediately

**Given** a System exists with no assigned SystemOwners and no repositories
**When** I right-click and select "删除" (Delete)
**Then** a confirmation dialog shall appear
**And** upon confirmation, the System shall be removed from the tree

**Given** a System has assigned SystemOwners or repositories
**When** I attempt to delete it
**Then** I shall see an error "Cannot delete a System with assigned owners or repositories — remove them first"

**Given** I attempt to create a System with a name that already exists
**When** I submit the dialog
**Then** I shall see a validation error

**Given** I am logged in as a SystemOwner
**When** I view the 系统管理 page
**Then** I shall see only Systems I am assigned to in the tree
**And** I shall see the "+ 新建系统" button

**Given** I am logged in as a regular User
**When** I view the application
**Then** the 管理后台 nav link shall not be visible

#### Story 2.2: Grant System Ownership

As an **administrator**,
I want to assign a registered user as the SystemOwner of a specific System,
So that the user can manage that System's repositories, scans, and context.

**Acceptance Criteria:**

**Given** I am logged in as an Administrator viewing a System's detail in 系统管理
**When** I see the System detail panel (UX-DR-12: name, description, stat cards, repo grid)
**Then** there shall be an "管理所有者" (Manage Owners) action
**And** it shall display the current list of SystemOwners for that System
**And** include a searchable user picker to add new owners

**Given** I select a registered user in the SystemOwner assignment dialog
**When** I confirm the assignment
**Then** the user shall be granted the SystemOwner role for that System
**And** the user shall immediately see the System in their system tree view
**And** the user shall be able to manage repositories, trigger scans, and supply context for that System

**Given** a user is assigned as SystemOwner for a System
**When** I remove their SystemOwner assignment
**Then** they shall immediately lose access to manage that System
**And** if they held no other SystemOwner assignments or Administrator role, 管理后台 shall no longer appear in their navbar

**Given** I assign the SystemOwner role to a user who currently holds only the User role
**When** the assignment is saved
**Then** the user shall be promoted to the SystemOwner role
**And** their authorization scope shall be limited to the assigned System(s)

**Given** I attempt to assign a user who is already an Administrator as a SystemOwner
**When** I search for the user in the assignment dialog
**Then** the user shall be excluded from the available list (Administrators already have full access)

#### Story 2.3: Repository Management

As a **SystemOwner**,
I want to add and remove repositories to my granted Systems with git URL validation,
So that Vulgata can scan the source code within those repositories.

**Acceptance Criteria:**

**Given** I am logged in as a SystemOwner viewing a System I am assigned to in 系统管理
**When** I select the System in the tree
**Then** the main content area shall show the system detail with a FluentDataGrid of repositories (UX-DR-12)
**And** each row shall display: repo name, scan status, last scan time, document count, and a "查看" action

**Given** I click "+ 新建仓库" above the repo grid
**When** I enter name (required), description (optional), git remote URL (required), and plain-text context (optional) in an inline CreateRepoDialog
**Then** the system shall validate the git URL is reachable by attempting `git ls-remote`
**And** on success, the Repository shall be created and added to the System
**And** it shall appear in the repo grid and system tree immediately

**Given** I enter a git URL that is not reachable
**When** the validation runs
**Then** I shall see an error "Git URL 不可达：{details}" (Git URL is not reachable)
**And** the Repository shall not be created

**Given** I enter a git URL that requires authentication
**When** validation runs
**Then** I shall see an error indicating authentication is required
**And** the URL validation shall not expose credentials in error messages

**Given** a Repository exists in my System
**When** I select it in the tree and use the "删除" (Delete) action
**Then** a confirmation dialog shall appear
**And** upon confirmation, the Repository shall be removed
**And** all Documents and scan results for that Repository shall be preserved

**Given** I am a SystemOwner of System A
**When** I view System B (not assigned to me) in the tree
**Then** System B shall not appear

#### Story 2.4: Standalone Repository Creation

As a **SystemOwner**,
I want to create standalone Repositories not belonging to any System,
So that shared libraries with organization-owned source code can be scanned independently.

**Acceptance Criteria:**

**Given** I am logged in as a SystemOwner
**When** I navigate to the system tree view
**Then** I shall see a section for "独立仓库" (Standalone Repositories) below the system list
**And** a "新建独立仓库" button shall be available

**Given** I create a Standalone Repository
**When** I enter name, description, git URL, and optional plain-text context in the CreateRepoDialog
**Then** the git URL shall be validated for reachability
**And** the Repository shall be created without a System association
**And** it shall appear under the "独立仓库" section in the tree

**Given** a Standalone Repository exists
**When** displayed in the tree
**Then** it shall be visually distinguished from System-associated repositories
**And** its detail panel shall display "独立仓库" as its System

**Given** I am a SystemOwner who created a Standalone Repository
**When** other SystemOwners view the tree
**Then** they shall see all Standalone Repositories (shared across SystemOwners)

#### Story 2.5: User-Supplied Context

As an **administrator or SystemOwner**,
I want to supply plain-text context at global, System, or Repository level,
So that agents have supplementary information when scanning and answering questions.

**Acceptance Criteria:**

**Given** I am logged in as an Administrator
**When** I navigate to 管理后台 → 设置 → 全局上下文 (Global Context)
**Then** I shall see a plain-text context field labelled "全局上下文（适用于所有系统）"
**And** I shall be able to edit and save it

**Given** I am logged in as a SystemOwner viewing a System I am assigned to
**When** I open the System's "设置" (Settings)
**Then** I shall see a plain-text context field labelled "系统上下文"
**And** I shall be able to edit and save it

**Given** I am logged in as a SystemOwner viewing a Repository in my System
**When** I open the Repository's "设置" (Settings)
**Then** I shall see a plain-text context field labelled "仓库上下文"
**And** I shall be able to edit and save it

**Given** context is supplied at multiple levels (global + system + repo)
**When** agents use context during scanning or chat
**Then** all three levels shall be combined (global → system → repo) and provided to the agent
**And** context shall be plain text only — no structured formats, markdown, or attachments at this stage

**Given** a Scan is running for a Repository
**When** a SystemOwner attempts to change the Repository's context
**Then** the change shall be queued and applied after the Scan completes
**And** the SystemOwner shall see a notification "上下文修改将在当前扫描完成后生效"

**Given** no Scan is running
**When** context is updated at any level
**Then** the change shall take effect immediately for subsequent scans and chat queries

**Given** I am logged in as a regular User
**When** I view System or Repository settings
**Then** context fields shall be visible as read-only
**And** I shall not see edit controls

---

### Epic 3: LLM Provider & Database Connection Configuration

#### Story 3.1: LLM Provider Configuration (Administrator)

As an **administrator**,
I want to configure multiple LLM providers at the global level with default LLM task-type assignments and test connections,
So that the platform has reliable LLM access with automatic failover for all LLM-powered tasks.

**Acceptance Criteria:**

**Given** I am logged in as an Administrator
**When** I navigate to 管理后台 → 设置 → LLM 提供商
**Then** I shall see a list of all configured LLM Providers with their name, base URL, supported API types, and default LLM task-type assignment
**And** a "添加提供商" button shall be available

**Given** I click "添加提供商"
**When** I enter a provider name (required), base endpoint URL (required), API key (required, masked input), supported API types — chat completions, responses, messages (multi-select checkboxes), and a default LLM task-type assignment — pre-scan, worker, chat (dropdown per task type)
**Then** the API key shall be stored encrypted at rest via ASP.NET Core Data Protection (NFR-2.2)
**And** the provider shall appear in the list

**Given** a provider exists in the list
**When** I click "测试连接"
**Then** the system shall send a minimal API call to verify the endpoint and credentials
**And** display a success notification "连接成功" or an error "连接失败：{details}"
**And** the test shall not consume significant token quota (minimal payload)

**Given** a provider in the list
**When** I edit its name, URL, API key, API types, or default LLM task-type assignment
**Then** the changes shall be saved immediately
**And** the provider's updated defaults shall apply to all Systems that use the global default (no per-System override set)

**Given** a provider is currently in use by any active Scan
**When** I view the provider in the list
**Then** the "删除" button shall be disabled
**And** a tooltip shall display "该提供商正被扫描使用，无法删除"

**Given** a provider is not in use by any active Scan
**When** I click "删除"
**Then** a confirmation dialog shall appear
**And** upon confirmation, the provider shall be removed
**And** if this provider was the only one configured, the system shall warn "删除后无可用提供商，扫描和对话功能将不可用"

**Given** multiple providers are configured
**When** the LLMProviderManager selects a provider for an LLM task
**Then** the default provider per task type shall be used first
**And** on failure, the next available provider supporting the required API type shall be used (automatic failover)

**Given** I am logged in as a SystemOwner or User
**When** I view settings
**Then** the LLM Provider configuration section shall not be visible

#### Story 3.2: Per-System LLM Provider Override

As a **SystemOwner**,
I want to override the global default LLM provider for each LLM task role within my System,
So that different Systems can use different LLM backends.

**Acceptance Criteria:**

**Given** I am logged in as a SystemOwner viewing a System I am assigned to
**When** I navigate to the System's 设置 page
**Then** I shall see an "LLM 提供商" section showing the current provider assignment for each LLM task type (pre-scan, worker, chat)
**And** each shall default to "使用全局默认 — {provider name}"

**Given** I am on the System LLM settings page
**When** I select a different provider from the dropdown for the pre-scan task type
**Then** that System's pre-scan LLM profiling tasks shall use the selected provider instead of the global default
**And** the dropdown shall only show providers whose supported API types include the required capability for that task role

**Given** I have overridden the worker agent provider for System A
**When** System A triggers a scan
**Then** Worker Agents for that System shall use the overridden provider
**And** Systems without overrides shall continue using global defaults

**Given** I have overridden a provider for my System
**When** I select "使用全局默认" from the dropdown
**Then** the override shall be cleared
**And** the System shall revert to the global default for that LLM task type

**Given** the global default provider is deleted by an Administrator while a System was using it without an override
**When** the deletion completes
**Then** the System's LLM task type shall fall back to the next available global default provider
**And** the SystemOwner shall see a notification "全局默认提供商已变更" on next visit

**Given** I am a SystemOwner of System A
**When** I view System B's settings (not assigned to me)
**Then** System B shall not be accessible

#### Story 3.3: Database Connection Configuration

As a **SystemOwner**,
I want to configure database connections per Repository with encrypted credentials,
So that Worker Agents can inspect database schemas and query sample data during scanning.

**Acceptance Criteria:**

**Given** I am logged in as a SystemOwner viewing a Repository in my System
**When** I navigate to the Repository's 设置 → 数据库连接
**Then** I shall see a form to add a database connection with fields: connection name (required), connection string (required, masked input), database type — PostgreSQL, SQL Server, MySQL, Oracle (dropdown), and credentials — username/password (required, masked input)
**And** a list of any already-configured connections for this Repository

**Given** I submit a valid database connection configuration
**When** I click "测试连接"
**Then** the system shall attempt to connect to the database with read-only access
**And** display success "连接成功" or error "连接失败：{details}"
**And** credentials shall be stored encrypted at rest via ASP.NET Core Data Protection (NFR-2.2)

**Given** I configure a database connection
**When** saved
**Then** the connection shall default to read-only mode (NFR-2.4)
**And** write operations shall be blocked at the connection level

**Given** a database connection is configured
**When** I edit its name, connection string, type, or credentials
**Then** the changes shall be saved and encrypted immediately

**Given** a database connection is configured
**When** I delete it
**Then** a confirmation dialog shall appear
**And** upon confirmation, the connection shall be removed
**And** encrypted credentials shall be purged

**Given** I have configured a database connection for a Repository
**When** that Repository is scanned
**Then** Worker Agents shall be able to use the connection for schema inspection (FR-10.2) and sample data queries (FR-10.3) via tool interfaces implemented in Story 5.9

**Given** I am a SystemOwner of Repository A
**When** I view Repository B (not in my System)
**Then** I shall not see its database connection settings

---

### Epic 4: Pre-Scan Profiling

#### Story 4.1: CodeGraph Integration & Code Unit Extraction

As a **SystemOwner**,
I want the system to automatically parse a repository's source code into Code Units with call-graph edges using CodeGraph before scanning begins,
So that Worker Agents receive well-defined, line-range-bounded code segments with structural context instead of raw files.

**Acceptance Criteria:**

**Given** a Repository has been added with a validated git URL
**When** the SystemOwner triggers a scan (via "开始扫描" button in RepoDetail)
**Then** the system shall clone or pull the repository via GitCloneService into a local working directory
**And** invoke CodeGraph CLI in batches of ~1,000 files with a configurable per-file timeout (default 30s)
**And** the parse output shall be captured as a `CodeGraphParseResult` containing: list of extracted CodeUnits, list of skipped files with reasons, list of errored files with error messages, parse duration, and total files parsed

**Given** the CodeGraph parse output is received
**When** the results are validated
**Then** malformed JSON entries shall be rejected and logged at Warning level with file path
**And** files that cause parse hangs or crashes shall be isolated: skipped with error reason recorded, processing continues with remaining files
**And** if parse returns 0 Code Units from 500+ source files, a Warning shall be logged and the scan flagged for human review

**Given** the CodeGraph parse completes
**When** the results are processed
**Then** a scan filter shall exclude non-code files: generated code, vendored directories, binary files, minified JavaScript, auto-generated protobuf/Thrift stubs, and files in `.gitignore`-excluded paths
**And** filtered Code Units shall be stored with status `Filtered` (preserved for auditability, not deleted)

**Given** valid Code Units have been extracted
**When** stored to the `code_units` table
**Then** each Code Unit shall contain: `id` (UUID), `scan_id` (FK → scan_runs), `repo_id` (FK → repositories), `file_path`, `file_identity_hash` (SHA-256 of canonical path — for rename detection in Epic 10), `line_start`, `line_end`, `symbol_name`, `unit_type` (enum mapping from CodeGraph symbol kind: function→Function, class→Class, method→Method, namespace→Namespace, module→Module), `source_hash` (SHA-256 of source content — for change detection in Epic 10), `range_confidence` (enum: Exact, Approximate, Heuristic), `language` (populated in Story 4.2), and `status` (Extracted → Pending → Dispatched → Completed | Failed | Skipped, with Filtered as a terminal state for excluded files)
**And** the status shall be set to `Pending` for all non-filtered Code Units

**Given** CodeGraph produces call-graph edges (symbol → symbol relationships)
**When** the results are stored
**Then** each call-graph edge shall be stored in a `code_unit_edges` table containing: `id` (UUID), `source_code_unit_id` (FK), `target_code_unit_id` (FK), `edge_type` (enum: Calls, Imports, Inherits, Contains), and `is_resolved` (boolean — whether the target symbol was found in the same repository)
**And** these edges shall be available for: superstep grouping (batch related code units), Worker context injection (each Worker receives its callers/callees), and cross-repo detection seed data (unresolved edges = cross-repo candidates for Epic 7)

**Given** a repository has 10,000 source files
**When** the CodeGraph parse runs
**Then** extraction shall complete within a timeframe that does not block the overall scan pipeline
**And** progress shall be reported via minimal dashboard (files parsed / total files, Epic 8 builds the full dashboard)

**Given** CodeGraph CLI is not installed in the container
**When** the parse is triggered
**Then** the system shall log an Error-level message "CodeGraph CLI not found" and abort the scan with a user-visible error

**Given** the repository clone fails (network error, auth failure)
**When** the scan is triggered
**Then** the system shall log the error and display "仓库克隆失败：{details}" to the SystemOwner
**And** the scan shall transition to Failed status

#### Story 4.2: Language & Framework Detection

As a **SystemOwner**,
I want the system to automatically identify the programming languages and frameworks used in each Code Unit during pre-scan profiling,
So that Worker Agents receive accurate language context for prompt generation.

**Acceptance Criteria:**

**Given** Code Units have been extracted from a repository
**When** profiling continues
**Then** each Code Unit's `language` column shall be populated using tree-sitter parser identification via CodeGraph (not file extension heuristics alone)
**And** supported languages shall include: C#, Java, Python, JavaScript, TypeScript, Go, Kotlin, Swift, Rust, C, C++, and any language CodeGraph supports at the CLI level

**Given** a repository's Code Units span multiple languages
**When** profiling completes
**Then** the system shall produce a language breakdown visible in the scan overview (e.g., "C#: 450 files, TypeScript: 120 files, Python: 30 files")

**Given** Code Units are language-tagged
**When** profiling continues
**Then** each Code Unit shall be tagged with detected frameworks where identifiable from imports, package references, or project files (e.g., ASP.NET Core, Spring Boot, React, Express, Django, Blazor)
**And** framework detection shall be a deterministic pass — package manifest analysis only (.csproj, package.json, pom.xml, requirements.txt, go.mod, Cargo.toml, etc.) — no LLM involvement
**And** detected frameworks shall be stored in a `code_unit_frameworks` join table: `code_unit_id` (FK), `framework_name`, `detection_source` (e.g., "package.json:dependencies.react"), `confidence` (High for direct dependency declaration, Medium for transitive inference)
**And** unknown or ambiguous frameworks shall be recorded as empty — no guesswork, no LLM inference at this stage

**Given** a repository contains a code file with an unrecognized file extension
**When** profiling runs
**Then** the file shall be tagged as language "Unknown" and filtered out of the scan with status `Filtered`
**And** the profiling summary shall include a count of skipped unknown files with their paths for audit

#### Story 4.3: Conservative Pre-Scan Gate & Communication Type Detection

As a **SystemOwner**,
I want the system to detect Cross-Repository communication kinds
deterministically after profiling and persist a conservative automatically
approved pre-scan handoff,
So that the August 15 Scan can proceed without treating weak hints as confirmed
facts or waiting for an LLM/Human-in-the-Loop cycle.

**Acceptance Criteria:**

**Given** pre-scan profiling has completed for a Repository (Code Units extracted, languages and frameworks tagged)
**When** the deterministic communication type detection pass runs
**Then** the system shall scan the profiling results (imports, annotations, package manifests, config files) for evidence of cross-repo communication technologies — RPC frameworks (Dubbo, gRPC, Thrift, SOFA annotations/imports), HTTP API patterns (REST controllers, HttpClient calls, OpenAPI/Swagger), message queue usage (Kafka, RabbitMQ, RocketMQ configuration/annotations), file-based communication (shared filesystems, object storage references), and page navigation patterns (URLs, deep links)
**And** each detected type shall include evidence (e.g., "Dubbo annotations in 12 files: com.example.rpc.ServiceA, com.example.rpc.ServiceB")
**And** this is a purely deterministic pass using CodeGraph structural data and package manifest analysis — no LLM involvement

**Given** the deterministic communication type detection pass completes
**When** the results are compiled
**Then** high-confidence detections (framework annotations found, config entries matched) shall be automatically marked as confirmed and seeded into the Communication Pattern Catalog for the System
**And** ordinary low-confidence hints (only import found without config, weak URL or path hints) shall be retained as conservative, auditable scan metadata without blocking the scan
**And** blocking ambiguities shall be converted to conservative scope warnings for the August 15 demonstration rather than invoking an LLM or waiting for human review
**And** warnings shall not be promoted to confirmed code-derived facts

**Given** pre-scan profiling and deterministic communication-kind detection complete
**When** the August 15 pre-scan gate runs
**Then** the persisted result shall transition directly to `pre_scan_approved` without an LLM call or human wait
**And** it shall contain confirmed high-confidence types, conservative hints and warnings, evidence locators, and provenance as bounded supplementary scan metadata
**And** it shall be exposed through a typed handoff without implementing a Scan Coordinator queue, Worker Agent lease, retry policy, or dispatch
**And** the scan log shall record "Pre-scan approved with conservative warnings — proceeding to the sequential demo runner"

**Deferred beyond the August 15 demonstration:** bounded LLM question generation, the five-question limit, confirm/exclude/add interactions, the `等待回答` state, the bell badge, the seven-day review lifecycle, and manual `确认并开始扫描` approval. These move to post-deadline Story 8.7 with the existing Epic 8 HITL stories and are not claimed complete by Story 4.3.

---

### Epic 11: August 15 Demonstration Slice

This deadline overlay delivers one prepared two-Repository business path. Its
minimal interfaces become foundations for the post-demo Epics 5–10; it does
not mark their broader acceptance criteria complete.

#### Story 11.1: Minimal Knowledge Store & Retrieval Projection

As a **SystemOwner**,
I want generated Documents, source anchors, essential references, and retrieval sections stored durably,
So that the demonstration can persist and retrieve a traceable Cross-Repository business flow.

**Acceptance Criteria:**

**Given** the VulgataDbContext is migrated
**When** the deadline knowledge-store migration is applied
**Then** it shall add the minimum Logical Document, immutable Document Version, source-anchor, REFERENCES/GENERATED_FROM relationship, flow-evidence, `index_entries`, and `document_sections` shapes required by the prepared demo
**And** entity configuration shall remain in Infrastructure with indexed foreign keys and Repository-scoped authorization paths
**And** a clean database shall migrate successfully with no pending model changes

**Given** a Code Logic Document or Business Logic Document is committed
**When** its retrieval projection is rebuilt
**Then** its current version, source anchors, essential relationships, Markdown/content payload, and searchable sections shall be retrievable without reading `index.md` as canonical state
**And** superseded immutable versions shall not be deleted or mutated

#### Story 11.2: Sequential Scan Runner & Typed Agent Dispatch

As a **SystemOwner**,
I want one prepared Repository Scan to run sequentially from approved pre-scan metadata into typed Worker Agent jobs,
So that the demonstration has a reliable execution path without unfinished concurrency infrastructure.

**Acceptance Criteria:**

**Given** a Repository has a `pre_scan_approved` result
**When** the SystemOwner starts the deadline Scan
**Then** deterministic application code shall create one durable Scan execution, process one Repository at a time, and dispatch typed jobs sequentially
**And** the runner shall persist enough state to report current phase, success, and failure after a server restart
**And** the runner shall not implement concurrent supersteps, dynamic leases, state replay, provider failover, or multi-Repository scheduling

**Given** a Worker Agent job fails
**When** the sequential runner records the failure
**Then** the Scan shall stop with a durable, Chinese user-visible failure rather than continue from partially trusted output
**And** no generated Document Version shall be committed from an invalid result

#### Story 11.3: Pass 1 & Pass 2 Demo Document Generation

As a **SystemOwner**,
I want bounded Worker Agents to generate technical and business explanations for the prepared flow,
So that the demonstration produces source-grounded Code Logic Documents and an understandable Business Logic Document.

**Acceptance Criteria:**

**Given** the sequential runner dispatches a Pass 1 job
**When** the Worker Agent receives a bounded Code Unit packet
**Then** it shall return schema-validated Code Logic Document content, source claims, essential local references, and communication evidence
**And** prompts shall be embedded resources and only the source/context required for the job shall be transmitted

**Given** the prepared Pass 1 Documents and exact flow evidence exist
**When** the runner dispatches the single prepared Pass 2 flow
**Then** one schema-validated Business Logic Document shall explain the business process in language suitable for a non-technical reviewer
**And** it shall persist GENERATED_FROM provenance to the Code Logic Document Versions actually used
**And** bounded refinement, cross-verification waves, partial-failure continuation, and general flow clustering shall remain deferred

#### Story 11.4: Exact RPC/HTTP Cross-Repository Demo Resolution

As a **SystemOwner**,
I want one configured RPC or HTTP communication path resolved across the two prepared Repositories,
So that the demonstration can trace a real business flow across a Repository boundary.

**Acceptance Criteria:**

**Given** a version-controlled demo communication configuration identifies the allowed RPC interface or HTTP route patterns
**When** Pass 1 results from both prepared Repositories are available
**Then** deterministic matching shall identify provider and consumer roles from exact normalized evidence
**And** the resulting Cross-Repository REFERENCES relationship shall preserve endpoint identifiers, evidence, confidence, and source anchors
**And** ambiguous or unmatched evidence shall remain a warning or Uncertainty and shall not create a false exact relationship

**Given** a communication type is MQ, file/storage, page navigation, indirect object flow, or requires Service Topology MCP
**When** the deadline resolver encounters it
**Then** it shall record the unsupported/deferred condition without attempting general resolution

#### Story 11.5: Demo Chat, Citations & Release Gate

As a **user**,
I want to ask Business or IT questions about the prepared two-Repository flow and verify every answer,
So that the competition demonstration proves Vulgata's end-to-end value.

**Acceptance Criteria:**

**Given** I open the Chinese chat page
**When** I select Business or IT mode and the prepared System/Repositories
**Then** the chat shall use database-backed retrieval over the current Documents, sections, source anchors, and exact Cross-Repository relationship
**And** Business mode shall return an understandable narrative while IT mode may include technical detail
**And** autonomous scope selection and file upload shall remain deferred

**Given** the Chat Agent returns an answer
**When** claims are rendered
**Then** every material claim shall cite a persisted Document and available source line range
**And** unauthorized Systems, Repositories, Documents, source anchors, and credentials shall not be retrieved or rendered

**Given** the deadline slice is ready for release
**When** final verification runs from a clean database
**Then** the included tests, authorization checks, migrations, Web build, one two-Repository end-to-end demonstration path, and one final Docker image build shall pass
**And** no deliberately failing or unresolved RED test shall be committed
**And** known post-demo limitations shall be shown separately from completed acceptance criteria

---

### Epic 5: Core Single Repository Scanning

**Post-August 15 continuation:** this epic deepens the minimal Epic 11 seams
with the full graph store, concurrent Scan Coordinator, deterministic
Orchestrator, retries, Worker Agent policies, uncertainty infrastructure, and
database tools. Epic 11 completion does not mark these stories done.

#### Story 5.1: Document Version & Source Anchor Store

As a **SystemOwner**,
I want each generated Document to be stored as an immutable version linked to exact source code anchors,
So that scan results remain auditable, citeable, and safe to supersede when code changes.

**Acceptance Criteria:**

**Given** the VulgataDbContext is migrated
**When** the document storage schema is created
**Then** the minimum V1 storage model shall represent Logical Documents and immutable Document Versions with: stable logical identity, immutable version identity, `doc_type`, `content_jsonb`, `content_markdown`, `export_md_path`, `schema_version`, `is_current`, `superseded_by_id`, `scan_id`, `generation_status`, `resolution_outdated_at`, and `resolution_outdated_reason`
**And** all timestamps shall use `timestamp with time zone`
**And** all foreign keys shall be indexed
**And** current-version lookup indexes shall support Repository, Logical Document, Document Version, and `doc_type` queries

**Given** a Worker Agent produces a Document Version
**When** the Document Version is committed
**Then** `document_sources` shall store the Repository, file path, source hash, line range, symbol name, and Code Unit reference used to generate the Document Version
**And** `reverse_index` shall map source files, source hashes, and Code Units back to impacted Document Versions for later impact analysis
**And** citations shall resolve from a Document Version to exact source file and line-range anchors

**Given** a Document Version is generated
**When** stored
**Then** the Document content shall be immutable — no UPDATE on content fields
**And** when code changes trigger a re-scan (Epic 10), the new version shall be inserted with a `superseded_by_id` pointing to the old version
**And** the old version's `is_current` shall become false and its status shall transition to `Superseded`
**And** the Version Chain shall be traversable via `superseded_by_id`

#### Story 5.2: Graph Edge, Endpoint & Uncertainty Store

As a **SystemOwner**,
I want scan results to persist references, communication surfaces, dispatch sites, and unresolved uncertainties as structured graph facts,
So that Vulgata can resolve relationships deterministically and expose trustworthy graph and chat evidence.

**Acceptance Criteria:**

**Given** the VulgataDbContext is migrated
**When** the graph fact schema is created
**Then** `edges` shall store Document Version endpoints, edge_type `REFERENCES`/`GENERATED_FROM`, source and target Repository IDs, resolution metadata, candidate-group metadata, confidence, disputed flag, and impact traversal eligibility
**And** same-scan references staged against Logical Documents shall be resolved to generated Document Versions during scan finalization or downgraded to Uncertainties
**And** indexes shall support edge traversal by source Document Version, target Document Version, source Repository, target Repository, edge type, and confidence

**Given** a Worker Agent identifies a provider-side communication surface
**When** the Document Version is committed
**Then** `communication_endpoints` shall store protocol, endpoint identifier, normalized identifier, provider Repository, provider Document Version, service/code hints, catalog evidence, and confidence
**And** duplicate endpoint identifiers in the same Repository shall be collapsed into one canonical endpoint with multiple source evidence rows when appropriate

**Given** a Worker Agent identifies a consumer-side invocation
**When** the Document Version is committed
**Then** `dispatch_sites` shall store the caller Document Version, protocol, endpoint identifier, normalized identifier, invocation expression, object-flow provenance, candidate target hints, and confidence
**And** `dispatch_site_endpoint_candidates` shall store candidate endpoint matches with match reason, rank, confidence, and resolution status

**Given** a target cannot be resolved during the Repository scan
**When** the graph facts are committed
**Then** an `uncertainties` row shall store status, sub_status, endpoint identifier, protocol, source Document Version, source Repository, target Repository hint, resolved Document Version fields, confidence, and HITL metadata
**And** `cross_repo` Uncertainties shall remain unresolved until same-System evidence or Epic 7 detection/resolution can resolve them

#### Story 5.3: Flow Trace & Provenance Store

As a **SystemOwner**,
I want Pass 2 business-flow synthesis to persist the traces and evidence it used,
So that every Business Logic Document can explain which Code Logic Documents and graph paths generated it.

**Acceptance Criteria:**

**Given** Pass 1 has produced Code Logic Documents and graph facts
**When** the Orchestrator plans Pass 2
**Then** `entry_points` shall identify starting Document Versions and source symbols eligible for business-flow tracing
**And** `flow_groups` shall store the Repository, scan, business-flow title candidate, grouping reason, and synthesis status
**And** `flow_traces` shall store ordered trace IDs, entry point, termination reason, loop summary, and confidence

**Given** a flow trace is persisted
**When** it contains graph traversal decisions
**Then** `flow_trace_steps` shall store ordered Document Version visits and edge traversals
**And** `flow_trace_candidate_selections` shall store unresolved candidate groups, selected candidate, rejected candidates, and selection reason
**And** `flow_trace_dispatch_bindings` and `flow_trace_endpoint_bindings` shall connect trace steps to dispatch sites and communication endpoints used during synthesis

**Given** a Business Logic Document is generated
**When** it is stored
**Then** `GENERATED_FROM` edges shall connect the Business Logic Document Version to each Code Logic Document Version actually used
**And** the Business Logic Document shall store references to its flow group and flow traces
**And** query services shall be able to retrieve the trace evidence packet for chat citations and stale-notice analysis

#### Story 5.4: Retrieval Indexes, Virtual Tree & Export Artifacts

As a **user**,
I want generated Documents to be searchable, browsable by source path, and exportable,
So that chat and management views can find the right knowledge without reading `index.md` as canonical data.

**Acceptance Criteria:**

**Given** a Scan commits Document Versions for a repository
**When** the post-scan hook runs
**Then** `index_entries` shall be rebuilt from Document metadata and `content_jsonb`
**And** `document_sections` shall be rebuilt from `content_jsonb.sections`
**And** `scan_llm_usage` shall record LLM provider, task role, token usage where available, latency, and failure status for scan observability
**And** `index_entries` and `document_sections` shall be treated as the canonical retrieval sources for chat and MCP knowledge serving

**Given** the document tree query is invoked
**When** a repository's documents are queried by virtual path
**Then** the tree structure shall be computed from document paths at display time (no stored tree — virtual computation)
**And** the query shall support depth limiting and path prefix filtering
**And** directory nodes shall be derived from source paths rather than stored as synthetic Documents

**Given** a Scan completes for a repository
**When** export artifacts are generated
**Then** an `index.md` file may be generated with YAML frontmatter per current Document Version as an export/readability artifact
**And** the generated `index.md` shall not be stored as the canonical retrieval source or as a special SYSTEM Document
**And** an append-only `log.md` shall record each scan/query/system event with ISO 8601 timestamp, event_type, actor, details JSON, and scan_id where applicable
**And** `log.md` shall be stored as an export/system artifact but shall not replace structured scan state in the database

#### Story 5.5: Scan Coordinator

As a **SystemOwner**,
I want a Scan Coordinator that manages the scan lifecycle — queuing, cloning, concurrency control, and state transitions,
So that scans execute reliably without overwhelming the system.

**Acceptance Criteria:**

**Given** a SystemOwner clicks "开始扫描" for a Repository that has passed pre-scan profiling and the question gate (Epic 4)
**When** the scan is triggered
**Then** the Scan Coordinator shall enqueue a scan work item into a `Channel<ScanWorkItem>`
**And** respond to the SystemOwner immediately (async — no blocking)

**Given** a scan work item is dequeued by the BackgroundService
**When** processing begins
**Then** the Scan Coordinator shall clone or pull the repository via `GitCloneService` into a unique working directory per scan
**And** handle git operation errors gracefully: network timeout → retry once, auth failure → transition scan to Failed with error detail

**Given** a scan is in progress for a Repository
**When** another scan is triggered for the same Repository
**Then** the system shall reject the request with "该仓库已有扫描任务进行中"

**Given** the system is under load
**When** multiple scans are triggered
**Then** the Scan Coordinator shall enforce a configurable maximum concurrent scans (default: 3)
**And** new scans shall be queued and shown as "排队中" in the dashboard
**And** the scan_runs table shall track each scan's status through the state machine: NotStarted → Profiling → AwaitingApproval → Scanning → Completed | Failed | Expired

**Given** a scan is running
**When** the server restarts
**Then** any in-progress scan shall be marked as Failed with a note "服务器重启，扫描中断"
**And** the SystemOwner shall be notified of the failure on next dashboard visit

**Given** the Scan Coordinator dispatches work to the Orchestrator
**When** the Orchestrator starts processing (Story 5.6)
**Then** the scan coordinator shall expose progress via SignalR ScanHub: `Phase`, `CurrentFile`, `FilesProcessed`, `TotalFiles`, `ReferencesFound`, `EstimatedSecondsRemaining`
**And** the minimal dashboard (from Epic 4) shall display this progress

#### Story 5.6: Orchestrator & Superstep Dispatch

As a **developer**,
I want a deterministic Orchestrator that batches Code Units into supersteps using CodeGraph call-graph edges and dispatches Worker Agents through typed jobs,
So that related Code Units are processed together and cross-verification is possible.

**Acceptance Criteria:**

**Given** a scan work item is ready and Code Units with `code_unit_edges` exist for the repository (from Epic 4)
**When** the Orchestrator starts
**Then** it shall group Code Units into supersteps using a scheduling algorithm: caller/callee pairs from `code_unit_edges` shall be placed in the same superstep where possible
**And** each superstep shall respect `MaxWorkersPerScan` (default: 5 for V1)
**And** superstep order shall respect dependency chains where a Code Unit's callees are processed before or alongside its caller

**Given** a superstep is assembled
**When** the Orchestrator dispatches Worker Agents (Story 5.7)
**Then** each Worker Agent shall receive a typed job packet with: its assigned Code Unit, exact source slice, file path and symbol name, neighboring symbols, CodeGraph callers/callees/imports/implementations, relevant resource files, pre-scan hints, the System's Communication Pattern Catalog (seeded in Epic 4), user-supplied context (global + system + repo), and related outputs for lightweight cross-verification
**And** dispatch shall use the selected Worker Agent execution adapter validated in the pre-epic spike

**Given** a Worker Agent task completes
**When** the result is collected
**Then** the Orchestrator shall aggregate results and track superstep completion
**And** on Worker failure, the task shall be retried once with the fallback LLM provider
**And** after retry exhaustion, the failure shall be recorded in the scan log and the scan shall continue with remaining tasks

**Given** all supersteps complete
**When** Pass 1 generation is done
**Then** the Orchestrator shall run bounded refinement only for satisfied material-enrichment predicates
**And** the Orchestrator shall initiate Pass 2 (Story 5.8) by planning path-aware traces, clustering flow groups, and selecting one Worker Agent per flow group to synthesize Business Logic Documents

**Given** the Orchestrator runs
**When** scanning is in progress
**Then** progress events (superstep started/completed, document generated) shall be pushed via the ScanHub for real-time dashboard updates

#### Story 5.7: Worker Agent — Pass 1 Code Logic Documents

As a **SystemOwner**,
I want Worker Agents to read Code Units and produce Code Logic Documents describing the structural code behavior,
So that the codebase is documented at a technical level with intra-repo references and cross-verification.

**Acceptance Criteria:**

**Given** a Worker Agent receives a superstep assignment
**When** it processes each Code Unit
**Then** it shall read the source code within the Code Unit's line range from the working copy
**And** produce a structured Code Logic Document (doc_type=CL) containing: title (symbol_name), file path, line range, language, framework, summary (2-3 sentence technical description), key_symbols (array of referenced symbols), content_markdown (detailed code behavior description), and confidence (0.0–1.0)

**Given** the Worker Agent identifies a reference to another symbol within the same repository
**When** the Code Unit is processed
**Then** an intra-repo `REFERENCES` edge shall be created between the source document and the target document
**And** document pre-allocation shall be used: if the referenced symbol's document doesn't exist yet, a placeholder document with status `Pending` shall be created so the edge can be established immediately
**And** the edge confidence shall be `High` (1.0) for same-file references, `Medium` (0.7) for same-repo different-file references

**Given** the Worker Agent identifies a call to a symbol that may reside in another repository
**When** the Code Unit is processed
**Then** an `Uncertainty` shall be recorded with: status `unresolved`, sub_status `cross_repo`, the endpoint identifier, the inferred communication type (from the CP Catalog), the source document reference, and the target repo hint if known
**And** the Uncertainty shall be left unresolved — final cross-repo resolution is completed in Epic 7 after full Cross-Repository Communication Detection

**Given** two Worker Agents in the same superstep process related Code Units (caller/callee pair)
**When** both documents are generated
**Then** a cross-verification step shall compare the business logic claims from both sides
**And** if claims agree → the existing edge is kept at current confidence
**And** if claims disagree → the edge confidence shall be lowered to 0.5 and flagged as `disputed`
**And** disputed edges shall render as yellow dashed lines in the knowledge graph (UX-DR-13)

**Given** a Worker Agent encounters a database connection configured for the repository (Story 3.3)
**When** the code references database tables or queries
**Then** the Worker Agent may invoke the schema inspection tool (Story 5.9) to validate table/column references
**And** may invoke the sample data query tool (Story 5.9) to understand data patterns
**And** database-derived information shall be cited in the document with a `db_source` annotation

**Given** a Worker Agent fails during Code Unit processing
**When** the error is caught
**Then** the task shall be retried once with the fallback LLM provider
**And** on second failure, the Code Unit shall be marked as Failed with error details in the scan log
**And** the scan shall continue with remaining Code Units

#### Story 5.8: Worker Agent — Pass 2 Business Logic Documents

As a **SystemOwner**,
I want the system to synthesize Business Logic Documents from completed Code Logic Documents,
So that business stakeholders can understand business processes without reading code.

**Acceptance Criteria:**

**Given** Pass 1 is complete for a Repository and all required bounded refinements have either run or been explicitly skipped
**When** Pass 2 begins
**Then** the Orchestrator shall start from Pass 1 entry points, traverse REFERENCES edges, candidate groups, dispatch sites, endpoint records, and uncertainties with path state, persist flow traces used for BL generation, and cluster traces into flow groups
**And** one Worker Agent per flow group shall be assigned to synthesize a Business Logic Document
**And** the Worker Agent shall receive a prepared flow-group packet containing ordered traces, trace steps, termination reasons, loop summaries, candidate selections, unresolved candidate groups, dispatch bindings, endpoint bindings, relevant Code Logic Documents, relevant resource/config Documents, and Cross-Repository uncertainties

**Given** the Worker Agent processes Pass 2
**When** a Business Logic Document is generated
**Then** it shall contain (doc_type=BL): title (business process name), summary (narrative in plain language, no code references — Business Mode compatible), key_business_entities, flow_description, trace_evidence_refs, unresolved_branch_notes, source_documents, generation-time resolution state, and confidence
**And** `GENERATED_FROM` edges shall connect the Business Logic Document Version to each Code Logic Document Version actually used

**Given** the BL Doc synthesis
**When** the Worker Agent identifies a business flow that spans multiple repositories
**Then** the BL Doc shall reference all involved repositories
**And** Cross-Repository scope shall be derived from Repository boundaries on REFERENCES edges, endpoint bindings, and uncertainties rather than stored as a separate edge type
**And** the BL Doc summary shall acknowledge cross-repo scope (e.g., "This process spans 手机银行, 风控引擎, and 核心系统")

**Given** a BL Doc is generated
**When** the document content is reviewed
**Then** the executive summary shall be in non-technical language (glossary-anchored terminology from the System's context)
**And** flow descriptions shall be in the pattern: "Step 1: User submits X → Step 2: System validates Y → Step 3: ..."
**And** no source code references, line numbers, or code snippets shall appear in the BL Doc body

**Given** Pass 2 completes
**When** all BL Docs are generated
**Then** `index_entries` and `document_sections` shall be rebuilt for the new Business Logic Document Versions
**And** export artifacts such as `index.md` may be regenerated to include BL Docs
**And** the scan status shall transition through post-scan and Cross-Repository Resolution states before `Completed`

#### Story 5.9: Database Tools for Worker Agents

As a **SystemOwner**,
I want Worker Agents to inspect database schemas and query sample data during scanning,
So that business logic involving database interactions is accurately documented.

**Acceptance Criteria:**

**Given** a Repository has a configured database connection (Story 3.3)
**When** a Worker Agent processes a Code Unit that references database tables or queries
**Then** the Worker Agent shall be able to invoke a schema inspection tool that returns: table names, column names, data types, primary/foreign keys, indexes, and constraints for the specified schema or entire database
**And** the tool shall operate read-only — no DDL or write operations permitted (NFR-2.4)

**Given** the schema inspection tool is invoked
**When** the database connection is configured as read-only
**Then** schema metadata shall be returned
**And** if the database is not read-only, the tool shall refuse with "数据库连接未配置为只读模式"

**Given** a Worker Agent needs to understand data patterns
**When** the sample data query tool is invoked with a table name and optional column filter
**Then** the tool shall execute `SELECT * FROM {table} LIMIT 10` (configurable row limit, default 10, max 100)
**And** results shall be returned as structured JSON with column names and sample values
**And** connection strings and credentials shall never be exposed to the Worker Agent's prompt — the tool abstracts access

**Given** a database query times out or fails
**When** the tool is invoked
**Then** the error shall be caught and returned to the Worker Agent as "数据库查询超时/失败：{reason}"
**And** the Worker Agent shall continue processing with the note "Database verification skipped — connection issue"

**Given** a Code Unit references a table or column that does not exist in the connected database
**When** the schema inspection tool is used
**Then** the Worker Agent shall record an `Uncertainty` with: "Referenced table/column {name} not found in database schema"
**And** the uncertainty shall be surfaced for HITL review

#### Story 5.10: Uncertainty Recording & Same-Repository Resolution Infrastructure

As a **SystemOwner**,
I want cross-repo calls detected during scanning to be recorded as Uncertainties with same-Repository and best-effort same-System resolution infrastructure,
So that Epic 7 can complete final Cross-Repository Resolution after full detection evidence is available.

**Acceptance Criteria:**

**Given** Worker Agents have generated Pass 1 documents with cross-repo Uncertainties
**When** a Repository's scan completes
**Then** all Uncertainties with sub_status `cross_repo` shall be preserved in the `uncertainties` table with status `unresolved`
**And** intra-repo Uncertainties (where both caller and callee are in the same repository) shall be resolved during this epic using the same-repo document graph
**And** resolved intra-repo Uncertainties shall transition to status `resolved` with `resolved_by_edge_id` pointing to the established edge

**Given** multiple Repositories within the same System have been scanned
**When** each Repository scan completes
**Then** a cross-repository check shall run: for each unresolved Uncertainty from the just-completed repo, search for a matching endpoint in already-scanned repos within the same System (best-effort partial resolution)
**And** if a match is found, the Uncertainty shall be resolved immediately
**And** if no match is found, the Uncertainty remains `unresolved` for Epic 7 to handle with full cross-repo detection

**Given** two Repositories have mutual pending cross-repo Uncertainties (deadlock)
**When** the deadlock-breaking protocol evaluates
**Then** the repo with fewer pending uncertainties shall resolve first
**And** if tied, the older scan shall yield
**And** the protocol decision shall be recorded in the scan log

**Given** a cross-repo Uncertainty never resolves (target repo never scanned, endpoint removed)
**When** a SystemOwner reviews it
**Then** they shall be able to mark it as `wontfix` (terminal state for dangling links)
**And** the dashboard shall display the `wontfix` status with a note

**Given** scan results are queried for chat or dashboard
**When** a Document has unresolved cross-repo links
**Then** a chat-accessible summary shall be available: "This Document has N unresolved cross-repo links: endpoint1→unknown, endpoint2→targetRepo"

**Given** a Document changes (re-scanned in Epic 10 or during partial resolution)
**When** the document graph updates
**Then** cross-repo stale notices shall be posted to other repositories that reference the changed Document
**And** the notices shall be advisory only — no automatic cascade (eventually consistent by design)

**Given** the impact analysis query is invoked
**When** a set of changed source files is provided
**Then** the query shall return: (1) directly impacted Documents, (2) intra-repo transitive closure via REFERENCES edges, (3) Cross-Repository reachability derived from source and target Repository IDs on REFERENCES edges, (4) Uncertainties that should be reopened due to the change
**And** the query shall use recursive CTE with `SET max_recursion_depth` (PostgreSQL-specific, configurable limit)

---

### Epic 6: Third-Party Library Handling

#### Story 6.1: Well-Known Library Allowlist

As a **developer**,
I want a well-known library allowlist so common open-source libraries are treated as understood,
So that agents don't waste time generating documents for standard libraries or creating cross-repo uncertainties for them.

**Acceptance Criteria:**

**Given** a Worker Agent encounters an import/reference to an external library during Code Unit processing
**When** the reference is evaluated against the cross-repo detection logic
**Then** the system shall first check the reference against the well-known library allowlist
**And** if the library is on the allowlist, no Uncertainty shall be generated for it
**And** no on-demand scan shall be triggered for it

**Given** the well-known library allowlist
**When** it is loaded at application startup
**Then** it shall be sourced from a configurable external file (JSON or YAML) stored in the `prompts/` directory (bind-mounted volume for hot-reload)
**And** the allowlist shall be loaded into memory with a configurable refresh interval (default: every 5 minutes)

**Given** the default allowlist
**When** shipped with Vulgata
**Then** it shall include at minimum: standard libraries for each supported language (System.*, java.*, javax.*, Python stdlib, Node.js built-ins, Go standard library), widely-used open-source packages (Newtonsoft.Json, AutoMapper, Dapper, Serilog, ASP.NET Core packages, Spring Boot packages, Express.js, React, Vue.js, axios, lodash, etc.), and testing frameworks (NUnit, xUnit, JUnit, pytest, Jest, Mocha)
**And** the allowlist format shall include: `name`, `package_pattern` (regex for import/package matching), `language`, `notes`

**Given** an Administrator wants to customize the allowlist
**When** they edit the allowlist file
**Then** additions shall take effect within the configurable refresh interval (no restart required)
**And** removals from the allowlist shall cause existing documents that relied on that library being understood to be flagged for review during the next scan

**Given** a library reference is ambiguous — could match an allowlist entry or could be an internal org library
**When** the evaluation runs
**Then** the ambiguity shall be surfaced to the System Owner as a question in the post-deadline pre-scan approval lifecycle (Story 8.7)
**And** the System Owner's answer shall be cached per Repository for future scans

#### Story 6.2: On-Demand Standalone Repository Scanning

As a **SystemOwner**,
I want standalone organization-owned libraries to be partially scanned on-demand when called by other repositories,
So that shared library code is documented without scanning every unused function.

**Acceptance Criteria:**

**Given** a Worker Agent in Repository A encounters a call to a function in an organization-owned standalone library (Repository B, a standalone repository created in Story 2.4)
**When** the Worker Agent processes the Code Unit
**Then** it shall record an Uncertainty pointing to Repository B's function
**And** if Repository B has not yet been scanned for that function, an on-demand partial scan shall be triggered: only the Code Units directly called by Repository A (and their transitive callers/callees within Repository B) shall be scanned
**And** the partial scan shall produce documents scoped to the requested Code Units — not the full repository

**Given** an on-demand scan is triggered for a standalone Repository
**When** another Worker Agent (from a different Repository) also triggers scanning the same standalone Repository
**Then** a lock mechanism shall prevent concurrent scanning of the same standalone Repository
**And** the second caller shall wait for the first scan to complete or re-use the first scan's results if the requested Code Units overlap
**And** the lock shall be in-memory with a configurable timeout (default: 30 minutes)

**Given** a partial scan of a standalone Repository completes
**When** documents are generated
**Then** the scanned functions shall be documented with CL Docs
**And** unscanned functions shall remain unscanned (no placeholder documents)
**And** a scan note shall record "Partial scan: {N} of {M} Code Units scanned — triggered by {caller_repo}"

**Given** the same standalone Repository is later referenced by additional callers needing different Code Units
**When** a new on-demand scan is triggered
**Then** only newly-requested Code Units shall be scanned (incremental partial scan)
**And** previously scanned Code Units shall not be re-scanned unless their source code has changed

**Given** a standalone Repository is a direct dependency (not a transitive one)
**When** the SystemOwner views the repository
**Then** they shall be able to trigger a full scan manually (not just on-demand partial)
**And** a full scan shall produce documents for all Code Units in the repository

#### Story 6.3: Unknown Library LLM Inference

As a **SystemOwner**,
I want the system to attempt LLM-based inference for unknown libraries without source code,
So that I receive a reasonable behavioral summary rather than a blank gap in the knowledge graph.

**Acceptance Criteria:**

**Given** a Worker Agent encounters an import/reference to an external library that is NOT on the well-known library allowlist AND has no standalone Repository in Vulgata
**When** the library is unknown (no source code available)
**Then** the system shall attempt LLM-based inference: the Worker Agent's LLM provider shall be asked to describe the likely behavior of the library based on the library name, the import statement, the calling code context, and the function/method names being invoked
**And** the inference result shall include a `confidence` score (0.0–1.0) reflecting how speculative the description is
**And** the result shall be stored as a Document with doc_type=CL, status `Inferred`, and a citation `"Inferred from LLM knowledge — no source code available"`

**Given** LLM inference produces a result with confidence ≥ 0.7
**When** the document is stored
**Then** it shall be used as a normal CL Doc (with the `Inferred` status visible in the document viewer)
**And** edges from calling code to the inferred document shall carry `Inferred` confidence

**Given** LLM inference produces a result with confidence < 0.7 OR the LLM call fails
**When** the inference attempt concludes
**Then** the SystemOwner shall be notified: "无法推断 {library_name} 的功能，请手动提供上下文"
**And** a placeholder Document shall be created with status `Missing`
**And** the SystemOwner shall be able to supply plain-text context for the library via a "提供上下文" action, which shall be stored as a user-supplied document

**Given** an Administrator has configured an LLM provider that does not support general knowledge queries (code-only model)
**When** inference is attempted and fails due to model capability
**Then** the system shall fall back immediately to the manual context notification (skip inference)
**And** the scan log shall record "LLM inference skipped — provider does not support general knowledge"

**Given** the same unknown library is encountered by multiple Worker Agents across different repositories
**When** inference results are shared
**Then** the first inference result shall be cached per library identifier
**And** subsequent encounters shall reuse the cached result (not re-query the LLM)
**And** the cache shall be invalidated if an Administrator or SystemOwner provides manual context for the library

---

### Epic 7: Cross-Repository Detection

#### Story 7.1: Communication Pattern Catalog Management

As a **SystemOwner**,
I want to define the communication patterns used by my System,
So that Worker Agents can detect cross-Repository communication using organization-specific conventions instead of guessing.

**Acceptance Criteria:**

**Given** I am a SystemOwner viewing a System I manage
**When** I open the Communication Pattern Catalog editor
**Then** I shall be able to configure patterns for RPC frameworks, HTTP API conventions, message queue topics, file/storage paths, and cross-repo page navigation
**And** each pattern shall include: pattern type, human-readable name, match rules, endpoint identifier extraction rule, provider/consumer hints, confidence default, examples, and active/inactive status

**Given** I save a catalog
**When** validation runs
**Then** invalid match rules shall be rejected with field-level errors
**And** duplicate active pattern names within the same System shall be rejected
**And** the catalog shall be versioned so scans can record which catalog version influenced their results

**Given** a scan is already running for the System
**When** I edit the catalog
**Then** the change shall be queued for the next scan and shall not alter the active scan's prompt context
**And** I shall see "通信目录修改将在当前扫描完成后生效"

**Given** a new communication pattern is needed
**When** I add it through the catalog editor
**Then** no code deployment shall be required for Worker Agents to receive the new pattern in future scans
**And** the catalog format shall be documented in the UI help text with examples suitable for System Owners

#### Story 7.2: Worker Prompt Injection & Detection Result Contract

As a **SystemOwner**,
I want each Worker Agent to receive the active Communication Pattern Catalog and return structured detection results,
So that cross-Repository findings are consistent, parseable, and traceable to evidence.

**Acceptance Criteria:**

**Given** a Worker Agent job is created for Pass 1
**When** the Orchestrator builds the typed job packet
**Then** it shall include the active System Communication Pattern Catalog version and only the patterns relevant to the Code Unit's language/framework hints
**And** the Worker prompt shall include pattern examples, endpoint extraction rules, and confidence guidance

**Given** a Worker Agent detects external communication
**When** it returns the Pass 1 result
**Then** the result shall include a structured `detected_communications` collection with: communication type, source code location, endpoint identifier, normalized endpoint identifier, provider/consumer role, matched catalog pattern, confidence, evidence excerpt, object-flow provenance if known, and unresolved reason if not resolved
**And** malformed detection results shall fail validation and be retried once as a bounded refinement request

**Given** no catalog pattern matches but the Worker Agent sees likely cross-repo communication
**When** the result is committed
**Then** it shall create a low-confidence detection candidate
**And** it shall surface a Human-in-the-Loop question asking the SystemOwner to confirm or add the missing catalog pattern

#### Story 7.3: RPC and HTTP Endpoint Detection

As a **SystemOwner**,
I want Vulgata to detect RPC and HTTP provider/consumer relationships across Repositories,
So that cross-service business flows can be traced through service boundaries.

**Acceptance Criteria:**

**Given** a Worker Agent processes code that exposes an RPC or HTTP endpoint
**When** the provider-side pattern matches
**Then** a `communication_endpoint` shall be recorded with protocol, endpoint identifier, normalized endpoint identifier, provider Repository, provider Document Version, source location, catalog evidence, and confidence
**And** RPC endpoints shall be recorded as named entities even when no consumer has been scanned yet

**Given** a Worker Agent processes code that calls an RPC or HTTP endpoint
**When** the consumer-side pattern matches
**Then** a `dispatch_site` shall be recorded with protocol, invocation expression, normalized endpoint identifier, source Document Version, caller Repository, and object-flow hints
**And** the system shall attempt to match the dispatch site to known communication endpoints in already-scanned Repositories

**Given** a provider and consumer are matched
**When** the match is committed
**Then** the REFERENCES edge shall identify provider and consumer roles
**And** the provider endpoint's consumer count shall increase
**And** the edge shall cite the dispatch-site evidence and provider endpoint evidence

**Given** only one side is scanned
**When** no matching Repository can be found
**Then** the unresolved side shall be recorded as an external reference with available service name, URL, RPC interface, or endpoint identifier

#### Story 7.4: Message Queue and File-Based Communication Detection

As a **SystemOwner**,
I want message queue and file/storage communication to be detected across Repositories,
So that asynchronous and file-based business processes are not invisible in the knowledge graph.

**Acceptance Criteria:**

**Given** a Worker Agent processes producer or consumer code for a message queue
**When** a catalog pattern matches a topic, queue, exchange, routing key, or subscription
**Then** the system shall record a communication endpoint or dispatch site with protocol `mq`, normalized topic identifier, provider/consumer role, source location, and confidence
**And** producer and consumer roles shall be matched by normalized topic and System catalog rules

**Given** a Worker Agent processes file or object-storage access
**When** a catalog pattern matches local filesystem, remote filesystem, OSS object storage, HDFS, or equivalent path conventions
**Then** the system shall record a file-based communication endpoint with normalized storage system, path pattern, provider/consumer role, source Document Version, and confidence
**And** wildcard or environment-specific paths shall be stored with both raw and normalized forms

**Given** a file or message communication target cannot be resolved
**When** the detection is committed
**Then** the system shall create an Uncertainty with sub_status `cross_repo`, endpoint identifier, protocol, target hint where known, and candidate resolution notes
**And** the dashboard shall later be able to present this Uncertainty for HITL review

#### Story 7.5: Cross-Repo Page Navigation Detection

As a **SystemOwner**,
I want Vulgata to detect cross-Repository page navigation while marking it as low-confidence,
So that UI navigation flows are visible without overstating loose coupling as hard service dependency.

**Acceptance Criteria:**

**Given** a Worker Agent processes frontend, mobile, or routing code
**When** it detects URL patterns, deep links, app-to-app navigation, route names, or webview launches that match the Communication Pattern Catalog
**Then** it shall record a page-navigation dispatch site with source route/screen, target URL or route pattern, source Document Version, and extracted target hints
**And** ownership evidence shall include at least one of: route ownership, base path, host/tenant mapping, deployment topology, or Service Topology evidence
**And** confidence shall default below service-call confidence unless ownership evidence confirms the target Repository

**Given** page navigation is detected
**When** provider or consumer role cannot be identified reliably
**Then** the detection shall be stored as low-confidence
**And** graph rendering shall use the unresolved/low-confidence visual treatment rather than a confirmed solid edge

**Given** a SystemOwner reviews page-navigation Uncertainties
**When** they confirm a target Repository or mark the link as external
**Then** the Uncertainty shall record the human input and retain lower priority than code-derived facts if later evidence conflicts

#### Story 7.6: Object Flow Analysis for Indirect Targets

As a **SystemOwner**,
I want Vulgata to trace communication targets through object creation and assignment,
So that indirect service clients, factories, and configuration-driven targets can still be resolved.

**Acceptance Criteria:**

**Given** a Code Unit contains a communication call through a client object, injected dependency, factory, configuration object, or wrapper method
**When** Worker Agent detection runs
**Then** the detection result shall include object-flow provenance: source symbol, assignment chain, constructor/factory call, configuration key where known, and confidence
**And** CodeGraph caller/callee/import evidence shall be included in the Worker job packet when available

**Given** object-flow analysis finds multiple possible targets
**When** the detection is committed
**Then** candidate targets shall be stored in `dispatch_site_endpoint_candidates` with rank, confidence, and match reason
**And** the Orchestrator shall not force a single target when confidence is below the configured threshold

**Given** object-flow evidence is insufficient
**When** no safe target can be selected
**Then** the system shall create an Uncertainty and ask a HITL question only if the ambiguity affects a generated Document or cross-Repository edge

#### Story 7.7: Service Topology MCP Resolution

As a **SystemOwner**,
I want Vulgata to use the mock Service Topology platform to map communication endpoints to Repositories,
So that endpoint names can resolve even when code-level naming differs across Repositories.

**Acceptance Criteria:**

**Given** the Service Topology MCP tool is configured and approved for scanning
**When** a communication endpoint or dispatch site has a service code, URL host, topic namespace, or other topology key
**Then** the Cross-Repository Resolution service shall query the mock Service Topology tool for candidate Repositories
**And** the query and response shall be recorded as resolution evidence without exposing secrets in prompts or logs

**Given** the Service Topology returns one high-confidence Repository match
**When** provider/consumer evidence supports the match
**Then** the Uncertainty shall transition to `resolved`
**And** a REFERENCES edge shall be created between the consumer and provider Document Versions

**Given** the Service Topology returns no match or multiple conflicting matches
**When** code evidence cannot decide
**Then** the system shall preserve the Uncertainty with candidate Repository hints
**And** the HITL dashboard shall show the candidates and evidence for SystemOwner review

**Given** the target provider or consumer is not in any scanned Repository
**When** resolution completes
**Then** the absent entity shall be recorded as an external reference with the available identifier, protocol, service/topology hints, and confidence

#### Story 7.8: Prompt Workbench for Detection Patterns

As a **developer or SystemOwner**,
I want a Prompt Workbench to test detection prompts against known code examples,
So that catalog patterns and Worker prompt changes can be validated before they affect production scans.

**Acceptance Criteria:**

**Given** I open the Prompt Workbench
**When** I select a Communication Pattern Catalog pattern and provide a code example
**Then** the workbench shall run the Worker detection prompt against the example using the selected LLM provider
**And** it shall show structured detection output, validation errors, confidence, and evidence excerpts

**Given** I have expected detection output for a sample
**When** the prompt run completes
**Then** the workbench shall compare actual output to expected output
**And** mismatches shall be highlighted by field: communication type, endpoint identifier, provider/consumer role, source location, and confidence

**Given** a prompt or catalog pattern is validated
**When** I choose to save it
**Then** the updated pattern shall be saved as a new catalog version
**And** existing scans shall continue using their original catalog version
**And** the scan log shall record future scans that use the new version

#### Story 7.9: Cross-Repository Resolution Finalization

As a **SystemOwner**,
I want unresolved cross-repo detections to resolve once enough provider, consumer, and topology evidence exists,
So that Vulgata's graph shows actual Cross-Repository relationships rather than permanent unknowns.

**Acceptance Criteria:**

**Given** Epic 5 has recorded cross-repo Uncertainties and Epic 7 has committed provider/consumer detections
**When** a Repository scan completes
**Then** Cross-Repository Resolution shall evaluate unresolved dispatch sites, communication endpoints, topology candidates, and Uncertainties across all scanned Repositories in the same System
**And** resolution shall use repo-qualified endpoint matching before falling back to lower-confidence name or topology hints

**Given** a resolution match is accepted
**When** the graph is updated
**Then** the system shall create or update REFERENCES edges with provider/consumer roles, confidence, evidence links, and source/target Repository IDs
**And** consumer counts per communication interface shall be recalculated from current resolved edges
**And** resolved Uncertainties shall reference the edge or Document Version that resolved them

**Given** mutual pending cross-repo Uncertainties create a resolution ordering conflict
**When** the Deadlock-Breaking Protocol runs
**Then** the Repository with fewer pending Uncertainties shall resolve first
**And** if tied, the older scan yields
**And** the decision shall be recorded in `log.md` and structured scan events

**Given** resolution changes graph truth after a Business Logic Document was generated
**When** the affected Document Version is queried
**Then** Vulgata shall mark the affected Business Logic Document Version with `resolution_outdated_at` or an equivalent stale notice
**And** no automatic cascade regeneration shall occur in V1
**And** chat shall be able to cite both the stale Document Version and newer graph evidence

---

### Epic 8: Dashboard & Real-Time Monitoring

#### Story 8.1: Scan Progress Dashboard

As a **SystemOwner**,
I want to see real-time scan status and progress for my Systems and Repositories,
So that I know whether Vulgata is profiling, scanning, blocked, completed, or failed.

**Acceptance Criteria:**

**Given** I open 管理后台 → 系统管理
**When** a System or Repository has scan history
**Then** the detail panel shall show the latest scan status: not started, profiling, awaiting approval, scanning, completed, failed, or expired
**And** status labels shall use Chinese UI text and Fluent UI state components

**Given** a scan is active
**When** ScanHub publishes progress
**Then** the dashboard shall show active Worker Agent count, files processed, total files, current file or phase, references found, errors encountered, and estimated seconds remaining where available
**And** refreshing progress shall not shift the surrounding layout

**Given** a scan fails
**When** the dashboard renders the failure
**Then** it shall show a non-blaming error message with retry affordance where safe
**And** technical details shall be available to SystemOwners without exposing API keys, database credentials, or source snippets beyond authorized scope

#### Story 8.2: HITL Notification Center

As a **SystemOwner**,
I want agent questions to appear in a global notification center,
So that I can quickly find ambiguities that need human input before scanning can continue.

**Acceptance Criteria:**

**Given** a Worker Agent or pre-scan gate creates a HITL question
**When** the question is persisted
**Then** the app shell bell icon shall show a pending-count badge
**And** the notification panel shall list each question with System, Repository, scan phase, summary, created time, confidence, and urgency

**Given** I click the bell icon
**When** the notification panel opens
**Then** HITL questions shall be grouped by System and Repository
**And** each item shall provide "回答" and "忽略" actions
**And** questions I am not authorized to answer shall not appear

**Given** there are no pending questions
**When** I open the notification panel
**Then** it shall show the UX empty state text and no disabled fake actions

#### Story 8.3: HITL Answer, Dismiss & Metadata Flow

As a **SystemOwner**,
I want to answer or dismiss agent questions with traceable metadata,
So that human input improves scans without overwriting code-derived facts.

**Acceptance Criteria:**

**Given** I answer a HITL question
**When** I submit the answer
**Then** the answer shall be attached to the relevant Document, Uncertainty, scan run, or catalog candidate as metadata
**And** the answer shall be marked as `human input`
**And** the scan or pending refinement shall be notified that the answer is available

**Given** human input conflicts with code-derived evidence
**When** the graph or document generator evaluates the conflict
**Then** code-derived facts shall retain higher priority
**And** the conflict shall remain visible as a disputed or low-confidence item rather than silently overwriting evidence

**Given** I dismiss a HITL question
**When** dismissal is submitted
**Then** the question shall leave the pending list with status `dismissed`
**And** the scan shall either continue with lowered confidence or remain blocked according to the question's required/optional flag
**And** the decision shall be recorded in scan events

#### Story 8.4: SignalR State Replay and Authorization

As a **SystemOwner**,
I want real-time dashboard and graph updates to survive reconnects and respect authorization,
So that monitoring remains accurate during long-running scans.

**Acceptance Criteria:**

**Given** ScanHub and GraphHub are registered
**When** a client connects
**Then** both hubs shall require authorization
**And** hub groups shall be scoped by authorized System and Repository IDs
**And** unauthorized users shall not receive scan, question, document, or graph events for inaccessible Systems

**Given** the browser loses and regains the SignalR connection
**When** the client reconnects
**Then** the server shall replay the latest scan status, pending HITL count, and graph snapshot/diff sequence for the authorized scope
**And** duplicate events shall not create duplicate UI rows or graph nodes

**Given** hub delivery fails
**When** the dashboard detects stale connection state
**Then** it shall show a reconnecting indicator
**And** the user shall still be able to refresh the page to retrieve current state from database-backed queries

#### Story 8.5: Live Knowledge Graph Projection

As a **SystemOwner**,
I want a live knowledge graph page showing generated Documents, references, and unresolved Uncertainties,
So that I can understand scan progress and relationship quality visually.

**Acceptance Criteria:**

**Given** a scan generates or updates a Document Version
**When** GraphHub publishes a `NodeAdded`, `NodeUpdated`, or `NodeRemoved` diff event
**Then** the graph page shall add or update a node for the current Logical Document/Document Version with doc type, title, Repository, status, and confidence
**And** Code Logic Documents and Business Logic Documents shall use distinct type badges consistent with UX-DR-13

**Given** a REFERENCES or GENERATED_FROM edge is created or updated
**When** GraphHub publishes an `EdgeAdded`, `EdgeUpdated`, or `EdgeRemoved` diff event
**Then** the graph shall render intra-repo edges as solid gray, cross-repo edges as dashed colored, unresolved Uncertainties as dashed edges to placeholder nodes, and disputed edges as yellow dashed lines with warning icon
**And** edge metadata shall include source/target Document Versions, source/target Repositories, confidence, provider/consumer roles where available, and Uncertainty status where relevant

**Given** graph updates arrive while the user is panning or zooming
**When** nodes or edges change
**Then** the viewport shall be preserved
**And** updates shall animate within the 200-400ms UX budget without resetting layout

#### Story 8.6: Graph Filtering, Details & Error States

As a **SystemOwner**,
I want graph controls and detail panels for scanning evidence,
So that I can inspect the specific Documents, edges, and Uncertainties behind a graph view.

**Acceptance Criteria:**

**Given** I open 管理后台 → 图谱
**When** the graph page renders
**Then** it shall provide a floating toolbar with layout toggle (hierarchical/force-directed), zoom controls, and System/Repository filter
**And** filters shall update the graph without navigating away from the page

**Given** I select a graph node
**When** the detail panel opens
**Then** it shall show Document title, type, Repository, source path, summary, scan status, confidence, and a link to 管理后台 → 文档
**And** failed or superseded Documents shall show their status and explanation

**Given** I select a graph edge or unresolved placeholder
**When** the detail panel opens
**Then** it shall show edge type, confidence, provider/consumer role, evidence references, Uncertainty status, and any HITL actions available to my role

**Given** there is no graph data
**When** the page renders
**Then** it shall show the UX empty state "尚无扫描数据。启动一次扫描后，知识图谱将在此展示。" with a primary action to go to 系统管理
**And** graph loading and error states shall use the shared `LoadState` conventions

#### Story 8.7: Pre-Scan LLM Question & Approval Lifecycle

As a **SystemOwner**,
I want blocking pre-scan ambiguities converted into bounded LLM-generated
questions with an explicit approval lifecycle,
So that post-deadline Scans can resolve genuine unknowns without asking about
deterministically known facts.

**Acceptance Criteria:**

**Given** Story 4.3 has persisted deterministic facts, conservative hints, and
blocking ambiguity evidence
**When** the post-deadline question generator runs
**Then** it shall ask at most five concise questions only for ambiguities that
materially affect Scan scope or Cross-Repository interpretation
**And** it shall not ask about high-confidence detections or ordinary weak hints
**And** every question shall preserve its evidence locators and provenance

**Given** at least one blocking question is generated
**When** the pre-scan lifecycle begins
**Then** the Scan shall enter `等待回答`
**And** authorized System Owners shall receive the Epic 8 notification and
answer/dismiss interactions
**And** answers shall support confirm, exclude, or add semantics without
promoting unsupported LLM output to code-derived fact

**Given** all blocking questions are resolved
**When** an authorized System Owner selects `确认并开始扫描`
**Then** the system shall persist the reviewed pre-scan result and approval
metadata before dispatch
**And** unanswered reviews shall remain available for seven days under the
documented lifecycle rather than silently autoapproving

---

### Epic 9: Chat Interface

#### Story 9.1: Chat Page Shell & Mode Switching

As a **user**,
I want a chat interface as the default landing page with Business Mode and IT Mode switching,
So that I can choose the right interaction style for my role.

**Acceptance Criteria:**

**Given** I log in to Vulgata
**When** authenticated
**Then** I shall land on the Chat page at route "/" (not a dashboard — chat-first per UX-DR-5)

**Given** the Chat page loads
**When** rendered
**Then** the layout shall be top-to-bottom: top navbar (brand + 对话/管理后台 nav + bell + avatar), mode selector (业务模式/技术模式 toggle), inline system/repo selector, message bubble stream, and fixed-bottom input with "发送" button

**Given** I am in a chat session
**When** I click the mode selector to switch modes (e.g., 业务模式 → 技术模式)
**Then** a confirmation dialog shall appear: "切换模式将开始新对话，当前对话将被保存。确定切换？"
**And** upon confirmation, the chat area shall clear, the theme shall transition (light ↔ dark, 200-400ms), the new session welcome message shall appear in Noto Serif SC: "你好，想了解什么业务流程？" (Business) or "你好，需要追踪哪段逻辑？" (IT)
**And** the previous session shall be saved and accessible in chat history

**Given** I am in 业务模式
**When** the ChatAgent receives my question
**Then** the system prompt shall instruct: use plain narrative language, no code references, business-level explanations, glossary-anchored terminology
**And** the UI shall use light theme tokens (background #F6F4F0, foreground #1D2026, per UX-DR-3)

**Given** I am in 技术模式
**When** the ChatAgent receives my question
**Then** the system prompt shall instruct: use precise technical language, include code references and source file paths, detailed call chains
**And** the UI shall use dark theme tokens (background #1D2026, foreground #F6F4F0, per UX-DR-3)

**Given** a mode switch occurs mid-session
**When** the new session starts
**Then** the mode-select banner shall display centered in Noto Serif SC: "开始新的对话 — {模式名}" (UX-DR-7)
**And** the current mode badge shall show the active mode as a compact label above the input

**Given** I am awaiting a response
**When** the ChatAgent is processing
**Then** the chat shall show a three-dot typing indicator with 200ms interval
**And** a temporary "正在查找相关文档…" hint shall appear during the retrieval phase
**And** responses shall stream in character-by-character with a blinking cursor

#### Story 9.2: System & Repository Context Selection

As a **user**,
I want to select which Systems or Repositories to include as chat context or have Vulgata auto-determine the scope,
So that my questions target the right codebase without manual selection when I'm unsure.

**Acceptance Criteria:**

**Given** I am on the Chat page
**When** the inline system/repo selector renders (Fluent Dropdown multi-select, UX-DR-9)
**Then** it shall list all Systems I have access to, with repositories as expandable children
**And** I shall be able to select entire Systems (all repos) or individual Repositories
**And** selected Systems/Repos shall appear as accent-colored tags above the chat input with ✕ to remove

**Given** I am a SystemOwner
**When** the system/repo selector loads
**Then** I shall see only the Systems I am assigned to
**And** standalone Repositories (shared) shall be listed separately

**Given** I am a regular User
**When** the system/repo selector loads
**Then** I shall see all Systems and Repositories (read-only access) for context selection

**Given** I start a new session
**When** the system/repo selector defaults
**Then** it shall remember my last-used selection from the previous session
**And** on first-ever use, it shall default to all authorized Systems

**Given** I submit a question with no Systems or Repositories selected
**When** the question is received (FR-11.8)
**Then** Vulgata shall autonomously determine which Systems or Repositories are relevant by: (1) extracting keywords and entities from the question, (2) calling `search_documents` over authorized `index_entries` and `document_sections` across all accessible Systems, including exact symbols, endpoint identifiers, business-term aliases, and Chinese/mixed-language trigram fallback, (3) ranking relevance by match strength and recency, (4) selecting the top 3 matching Systems
**And** the auto-determined scope shall be displayed in the chat: "已自动选择以下系统：{system_names}" with an option to adjust

**Given** I change the system/repo selection mid-session
**When** I submit a new question
**Then** the new selection shall apply to the next question only (not retroactively to previous answers)
**And** the selection shall persist for subsequent questions until changed

#### Story 9.3: File Upload for Chat Context

As a **user**,
I want to upload documents or images as supplementary context for my questions,
So that the ChatAgent can incorporate information beyond the scanned codebase.

**Acceptance Criteria:**

**Given** I am on the Chat page
**When** I click the "📎" attachment button next to the input
**Then** a file picker shall open accepting: documents (.txt, .md, .pdf, .docx) and images (.png, .jpg, .webp)
**And** maximum file size shall be 10 MB per file, 5 files per message

**Given** I upload a text document
**When** the file is received
**Then** the document content shall be extracted and appended to the chat context alongside the user's question
**And** the uploaded file shall appear as a chip above the message: file name + size + ✕ to remove

**Given** I upload an image
**When** the file is received
**Then** the image shall be sent to the LLM provider if the provider supports vision/image inputs
**And** if the provider does not support images, the user shall be notified "当前 LLM 提供商不支持图片识别"
**And** the image shall render as a thumbnail in the chat with the ability to expand

**Given** I remove an uploaded file before sending
**When** I click ✕ on the file chip
**Then** the file shall be removed from the message context
**And** the file shall not be permanently stored — ephemeral, session-only

**Given** file upload fails (network error, file too large, unsupported format)
**When** the error occurs
**Then** a non-blaming error shall appear: "文件上传失败：{reason}" with retry option
**And** the chat input shall remain intact

#### Story 9.4: Database-Backed LLM-Wiki Retrieval Pipeline

As a **user**,
I want the ChatAgent to find the most relevant documents, read them, follow cross-repo links, and synthesize an answer,
So that I get comprehensive, well-grounded responses across the scanned knowledge base.

**Acceptance Criteria:**

**Given** I submit a question in the Chat page
**When** the ChatAgent begins retrieval
**Then** it shall execute the LLM-Wiki pipeline through typed retrieval tools: (1) call `search_documents` for the selected Systems/Repos, (2) retrieve relevant sections using `get_document_sections`, (3) expand related Documents using `expand_related_documents`, (4) enrich evidence with `get_uncertainty_summary`, `get_endpoint_context`, `get_flow_trace_context`, and `get_source_anchor_context` as needed, (5) synthesize a final answer grounded in the retrieved sections and evidence
**And** the pipeline shall be section-level by default and have a maximum retrieval budget of 10 Documents total (3-5 direct + up to 5 graph-expanded related Documents)
**And** `index.md` shall not be read as the canonical retrieval input

**Given** the retrieval identifies relevant Documents
**When** reading each Document
**Then** the ChatAgent shall prioritize: BL Docs for 业务模式 questions (business narratives), CL Docs for 技术模式 questions (code details)
**And** if a Document has a `confidence < 0.5`, it shall be excluded from retrieval (too unreliable)

**Given** a Document is too large for the context window
**When** the ChatAgent reads it
**Then** retrieval shall prefer relevant `document_sections` over full `content_markdown`
**And** if section-level retrieval is still too large, a summarization fallback shall use the Document summary plus the highest-ranked sections
**And** the answer shall note "文档内容过长，已根据摘要和相关章节回答"

**Given** the retrieval finds zero relevant Documents
**When** the pipeline completes
**Then** the ChatAgent shall respond: "未找到相关文档。试试调整系统选择范围或换个问法。" (UX-DR-16 NoResults)
**And** PostgreSQL full-text search shall run where it works well, with exact symbol/endpoint matching and trigram fallback for Chinese and mixed Chinese/English text
**And** if full-text search also returns nothing, the response shall suggest "请确认相关系统已完成扫描"

**Given** a retrieved Business Logic Document Version has `resolution_outdated_at` set
**When** the ChatAgent synthesizes an answer
**Then** it shall distinguish generation-time BL prose from query-time graph evidence
**And** citations shall expose both the stale BL Document Version and the newer graph evidence where they differ

**Given** the retrieval finds Documents but the LLM call fails
**When** the error occurs
**Then** the system shall retry once with the fallback provider
**And** on second failure, respond: "暂时无法回答，请稍后重试。"
**And** log the error with the query and attempted documents

**Given** the ChatAgent synthesizes an answer
**When** the response is streamed to the user
**Then** the response shall be in the voice appropriate to the mode: business narrative (业务模式) or technical detail (技术模式) per UX-DR-18
**And** response time shall be within 30 seconds for questions spanning up to 5 Documents (NFR-1.2)

#### Story 9.5: Citation Linking & Answer Rendering

As a **user**,
I want every system answer to include citations linking claims to source Documents and code lines,
So that I can verify the information and navigate to the original source.

**Acceptance Criteria:**

**Given** the ChatAgent synthesizes an answer
**When** the response includes claims from retrieved Documents
**Then** each claim shall be appended with a citation marker: `[1]`, `[2]`, etc.
**And** at the end of the response, a "参考资料" section shall list each citation: doc title, doc type badge (blue=CL, green=BL), repository name, file path

**Given** a citation is rendered
**When** I click on a citation link
**Then** an inline preview panel shall expand showing: the Document's summary, source code location (file:line for CL Docs), and a "在文档查看器中打开" link
**And** the preview shall not navigate away from the chat — inline overlay only

**Given** a citation references a CL Doc
**When** I click "在文档查看器中打开"
**Then** the 管理后台 → 文档 page shall open in a new browser tab with the specific document selected

**Given** message bubbles render
**When** displayed
**Then** user messages shall be right-aligned with surface background (UX-DR-7)
**And** assistant messages shall be left-aligned with muted background
**And** assistant messages shall render Markdown (headings, lists, bold, italic, code blocks with muted background)
**And** code blocks within assistant messages shall include language labels where detected

**Given** a response contains low-confidence information (confidence < 0.7)
**When** the answer is rendered
**Then** the ChatAgent shall include a disclaimer in the response: "⚠️ 以下信息置信度较低，请验证"
**And** the corresponding citations shall be flagged with a confidence indicator

**Given** no relevant Documents were found (Story 9.4 fallback exhausted)
**When** the empty result renders
**Then** the chat shall display: "未找到相关文档。试试调整系统选择范围或换个问法。" (UX-DR-16) with suggestions for broader scope or different phrasing
**And** in 技术模式, the response may additionally show: "搜索关键词：{extracted_keywords}，搜索范围：{selected_systems}"

---

### Epic 10: Git Monitoring & Incremental Re-scan
**Deferred beyond initial Sprint Planning** — 5 FRs (FR-9.1–9.5). Detail this epic only if git monitoring and incremental re-scan are reprioritized into an implementation wave.

---

### Deferred Features
**FRs covered:** FR-13.1, FR-13.2, FR-13.3, FR-13.4, FR-13.5 (MCP Integration), FR-15.1, FR-15.2, FR-15.3 (LSP Support)
Architecture supports adding these later via Infrastructure project and MCP/LSP client packages. No stories generated for deferred features.
