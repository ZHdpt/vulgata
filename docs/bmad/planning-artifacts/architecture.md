---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - briefs/brief-vulgata-2026-06-11/brief.md
  - briefs/brief-vulgata-2026-06-11/addendum.md
  - prds/prd-vulgata-2026-06-12/prd.md
  - prds/prd-vulgata-2026-06-12/.decision-log.md
  - research/technical-vulgata-core-technologies-research-2026-06-12.md
  - research/domain-enterprise-code-analysis-llm-business-logic-extraction-research-2026-06-12.md
  - docs/requirement-draft.md
  - docs/agents/domain.md
workflowType: 'architecture'
project_name: 'vulgata'
user_name: 'zhdpt'
date: '2026-06-18'
lastStep: 8
status: 'complete'
completedAt: '2026-06-18'
updated: '2026-08-17'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## August 30, 2026 Bounded Scanning Correction

The approved August 30 correction is the current implementation authority for
the competition release:

- [August 30 Bounded Scanning and LLM-Wiki Design](architecture/august-30-bounded-scanning-design.md)
- [Sprint Change Proposal: August 30 Bounded Scanning Recovery](sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md)

Where those artifacts conflict with this document or the July scanning
supplements, the August 30 design supersedes the older scanning, orchestration,
tooling, Cross-Repository Resolution, Pass2, Chat retrieval, deployment, and
release-verification details for Epic 12.

In particular, CodeGraphContext is removed from the production/release critical
path. The bounded path uses revision-bound CodeGraph and Eclipse JDT LS adapters,
safe catalog-driven XML parsing, File Code Units, deterministic bounded Worker
scheduling, durable pause/resume, endpoint-centered Pass2 grouping, and
non-vector database-backed LLM-Wiki retrieval. Epics 5–10 retain the broader V1
architecture as post-deadline backlog.

## July 2026 Scanning Update

The scanning subsystem was refined after this June architecture document. Use these July-merged artifacts as the implementation handoff for scanning:

- [Vulgata V1 Scanning Technical Plan](architecture/scanning-v1-technical-plan.md)
- [Vulgata V1 Scanning Data Contracts](architecture/scanning-v1-data-contracts.md)

Where these documents conflict with older scanning, document graph, orchestration, storage, or retrieval details below, the July scanning artifacts supersede the older text. On 2026-07-10, the main body of this architecture document was reconciled to the July scanning baseline; the two linked artifacts remain the detailed implementation source for scanning schemas and contracts.

## Project Context Analysis

### Requirements Overview

**Functional Requirements:** 89 FRs across 15 feature groups. The scanning pipeline (FR-4.x, 12 FRs) and cross-repository communication detection (FR-5.x, 14 FRs) are the architecturally heaviest subsystems — they form the core engine. Authentication (FR-1.x), HITL (FR-7.x), user-supplied context (FR-14.x), chat auto-scope (FR-11.8), and database tools (FR-10.x) are lighter supporting features. Git monitoring (FR-9.x), MCP integration (FR-13.x), and LSP support (FR-15.x) are deferrable or optional.

**Non-Functional Requirements:** 15 NFRs across 6 categories. The single Docker container constraint (NFR-4.1) is the most architecturally significant — it rules out multi-service deployments and external database dependencies. Security NFRs require encrypted secrets at rest and read-only database access. Reliability NFRs require worker retry, graceful API degradation, and scan state persistence.

**Scale & Complexity:**

- Primary domain: Full-stack web application with deterministic scan orchestration and LLM Worker Agent execution
- Complexity level: High — deterministic scan orchestration, bounded LLM analysis, cross-repo detection, document graph with recursive traversal, flow-trace planning, real-time graph visualization, incremental re-scan with impact analysis
- Estimated architectural components: 9 major subsystems (Web UI, Scan Coordinator, Orchestrator, Worker Agents, Cross-Repository Resolution, Document Graph Store, Chat Agent, 管理后台 Hub, LLM Provider Manager)

### Technical Constraints & Dependencies

**Hard Constraints:**

- .NET 10, C#, Blazor UI, deterministic Orchestrator, Microsoft Agent Framework for Worker Agent execution if the adapter spike validates it
- PostgreSQL + EF Core for demo; docker-compose deployment
- MVVM pattern for UI; DDD where possible for domain logic
- Source structure must accommodate future Java/Python/Node projects (separate top-level directories)
- UI/UX per DESIGN.md and EXPERIENCE.md in `docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/`
- Latest stable/prerelease versions of frameworks and libraries preferred

**Key Dependencies:**

- `Microsoft.Agents.AI.Foundry` + `Microsoft.Agents.AI.Workflows` (prerelease) — candidate Worker Agent execution and typed-tool adapter
- `ModelContextProtocol` (prerelease) — MCP tool integration
- `Microsoft.EntityFrameworkCore.Sqlite` — data persistence
- `Z.Blazor.Diagrams` — live knowledge graph visualization
- DeepSeek V4 via OpenAI-compatible endpoint — primary LLM provider
- CodeGraph — pre-scan structural analysis (deterministic code unit extraction)

**Pre-existing Decisions (50 logged, reconciled with July 2026 scanning update):** Scan Coordinator (non-LLM) + deterministic per-Repository Orchestrator architecture; Worker-embedded cross-repo detection (Model A); two-pass document generation (Pass 1 CL Docs → Pass 2 flow-group BL Docs); document pre-allocation; deterministic Cross-Repository Resolution; PostgreSQL + EF Core with recursive CTE; `index_entries` and `document_sections` for retrieval; Blazor.Diagrams + SignalR for live graph; database-backed LLM-Wiki retrieval through typed tools; Communication Pattern Catalog per-system for V1.

### Cross-Cutting Concerns Identified

| Concern                        | Impact                                      | Architectural Significance                                                                                           |
| ------------------------------ | ------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Authentication & Authorization | All UI surfaces, API endpoints              | ASP.NET Core Identity + role-based access; shapes middleware pipeline                                                |
| Concurrency Control            | Scan pipeline, worker dispatch              | Configurable limits at multiple levels; prevents resource exhaustion                                                 |
| Real-time Updates              | 管理后台, live graph                         | SignalR hub for scan progress, document generation, link resolution events                                           |
| LLM Provider Management        | LLM task execution                          | Multi-provider config for pre-scan, worker, and chat tasks; connection testing; API key encryption                   |
| Agent Error Handling           | Worker Agents, Orchestrator                 | Retry once, record failure, continue; graceful API degradation                                                       |
| Observability                  | System-wide                                 | log.md (append-only, parseable) + structured .NET logging                                                            |
| Configuration Externalization  | Agent prompts, CP catalogs, provider config | Prompts as configurable text resources; CP catalog documented format                                                 |
| Data Isolation & Security      | Secrets, source code, documents             | Encrypted at rest for API keys/connection strings; read-only DB access; code never leaves infra with self-hosted LLM |

### Risk-Adjusted Architectural Concerns

**Critical Assumptions Requiring Architectural Mitigation:**

1. **Cross-Repo Detection Accuracy (A3):** Design the document graph edge model with confidence levels (confirmed vs. inferred). The live graph should visually distinguish these. The chat agent should acknowledge uncertainty when traversing low-confidence edges. Include a pre-scan calibration step on known cross-repo call patterns.
2. **Worker Agent Execution Adapter (A1):** The validation spike must produce a binary decision with a documented fallback adapter. The Orchestrator remains deterministic application code either way. If MAF cannot execute Vulgata Worker Agents and typed tool calls reliably, use a direct OpenAI SDK or custom worker adapter without changing the scan state machine.
3. **BL Document Readability (A4):** The BL document schema must include mandatory non-technical elements: executive summary, glossary-anchored terminology, and visual flow descriptions. Implement a readability review gate during Pass 2 — sample documents assessed by a non-technical-simulating LLM before full generation.
4. **LLM Provider Failover (A2):** The LLM Provider Manager must support automatic failover between configured providers. Worker agents should be able to retry with a fallback provider on failure. This is not optional — it's architectural insurance against demo-day API issues.
5. **Scope Degradation Cut Lines (A5):** Beyond the already-deferred features, identify internal cut lines: (a) single-repo demo with manual cross-repo links if detection is unreliable, (b) pre-generated documents with live chat if scanning is too slow, (c) static graph screenshot if real-time Blazor.Diagrams proves unstable. Each cut line preserves the demo narrative while reducing risk.

### Second-Order Architectural Implications

**PostgreSQL + docker-compose Cascade:**

- PostgreSQL 17 → recursive CTEs for all graph operations → production-grade concurrency (no single-writer limitation)
- docker-compose deployment → two containers (app + PostgreSQL) → `docker-compose up` single command
- Proper RDBMS → no WAL-mode hacks needed → full multi-reader/multi-writer from day one
- No Redis backplane → SignalR works trivially now but blocks horizontal scaling of the app tier → acceptable for V1, document as known limitation
- Volume mount for PostgreSQL data → data persistence across rebuilds → standard Docker pattern

**Worker-Embedded Detection Cascade:**

- Worker context budget shared between code reading and CP catalog → detection quality varies by code unit size → consider context-budget-aware superstep grouping
- No cross-file pattern correlation → cross-verification only within same superstep → superstep composition strategy matters for detection accuracy
- Prompt Workbench becomes primary quality control mechanism → invest early in prompt iteration tooling

**Document Immutability Cascade:**

- No human edits → every correction requires re-scan → HITL context injection is the only pre-re-scan correction path
- Version chains grow unbounded → demo scale fine, but note compaction strategy for production
- Cross-repo stale notices are the only update signal → periodic health check needed to detect missed notices

**PostgreSQL Graph Cascade:**

- Recursive CTE with cycle detection → document and enforce max graph depth via `SET max_recursion_depth`
- Full MVCC concurrency → no read/write contention → graph queries and scan writes proceed simultaneously
- In-memory graph cache → must support incremental updates during active scans, not just startup rebuild

**Database-Backed LLM-Wiki Cascade:**

- `index_entries` and `document_sections` quality is critical → build them from committed Document Versions and `content_jsonb`
- Context window ceiling → retrieve sections first, then summarize large Documents from summary + highest-ranked sections
- Built-in PostgreSQL full-text search is not a complete Chinese tokenizer → use exact symbols, endpoint identifiers, business aliases, graph expansion, and trigram fallback for Chinese and mixed Chinese/English queries
- `index.md` remains an export/readability artifact, not the canonical retrieval input

### Subsystem Decomposition

The system decomposes into 9 independently testable subsystems with clean, unidirectional boundaries:

| #    | Subsystem                   | Type          | Depends On            | Key Risk                      |
| ---- | --------------------------- | ------------- | --------------------- | ----------------------------- |
| SP-1 | Web Application Shell       | Non-LLM       | None                  | Low — standard Blazor         |
| SP-2 | System & Repository CRUD    | Non-LLM       | None                  | Low — standard EF Core CRUD   |
| SP-3 | Scan Coordinator            | Non-LLM       | SP-2                  | Medium — concurrency, git ops |
| SP-4 | Repo Workflow Orchestrator  | Deterministic | SP-3, SP-5, SP-7      | Medium — state machine, trace planning |
| SP-5 | Worker Agent Execution      | LLM           | SP-4 (typed dispatch) | High — detection accuracy, adapter reliability |
| SP-6 | Cross-Repository Resolution | Deterministic | SP-7                  | Low — pure SQL + matching     |
| SP-7 | Document Graph Store        | Data Layer    | None (infrastructure) | Medium — schema stability     |
| SP-8 | Chat Agent                  | LLM           | SP-7                  | Medium — retrieval quality    |
| SP-9 | 管理后台 & Live Graph       | UI (SignalR)  | SP-3, SP-7            | Medium — real-time perf       |

**Development Ordering Implication:**

- SP-1, SP-2, SP-3, SP-7 can start immediately (no LLM dependency)
- SP-4 can start once SP-7's schema contracts are stable; it does not depend on LLM-led orchestration
- SP-5 can be developed in parallel through prompt contracts and the Worker Agent execution adapter spike
- SP-8, SP-9 are pure consumers — build last with real data

**Architectural Keystone:** SP-7 (Document Graph Store). Its schema and query API must stabilize first — every other subsystem reads from or writes to it.

**Boundary Validation:** All 10 subsystem boundaries are clean with no circular dependencies. Each subsystem can be built and tested independently. The real-time event flow is unidirectional: SP-3/SP-4/SP-5/SP-6 → SP-7 → SP-9.

## Solution Architecture

### Technology Stack

| Layer               | Technology                              | Version             | Rationale                                                                        |
| ------------------- | --------------------------------------- | ------------------- | -------------------------------------------------------------------------------- |
| Runtime             | .NET                                    | 10.0                | Latest stable; required by MAF prerelease packages                               |
| Language            | C#                                      | 14                  | Latest C# with .NET 10                                                           |
| UI Framework        | Blazor (Interactive Server)             | .NET 10             | Native .NET UI, no JS build chain, SignalR built-in                              |
| UI Components       | Microsoft Fluent UI Blazor              | Latest stable       | Official Microsoft library; DataGrid, forms, nav, dialogs; modern Microsoft look |
| Graph Visualization | Z.Blazor.Diagrams                       | Latest stable       | Specialized graph canvas; only non-Fluent UI component                           |
| MVVM Toolkit        | CommunityToolkit.Mvvm                   | Latest stable       | Source generators for ObservableProperty, RelayCommand                           |
| Agent Framework     | Microsoft.Agents.AI.Foundry + Workflows | Latest prerelease   | Candidate Worker Agent execution adapter; deterministic Orchestrator owns scheduling |
| LLM Client          | OpenAI SDK                              | Latest stable       | DeepSeek V4 via OpenAI-compatible endpoint                                       |
| Database            | PostgreSQL via EF Core                  | 17                  | Production-grade RDBMS; recursive CTEs for graph traversal; full concurrency     |
| ORM                 | Npgsql.EntityFrameworkCore.PostgreSQL   | 9.x                 | EF Core PostgreSQL provider                                                      |
| Identity            | ASP.NET Core Identity + EF Core         | .NET 10             | Cookie auth, role-based authorization, passkey support available                 |
| Real-time           | ASP.NET Core SignalR                    | .NET 10             | Built-in; scan progress + graph events pushed to UI                              |
| Code Analysis       | CodeGraph CLI                           | Latest              | Pre-scan structural analysis; deterministic code unit extraction                 |
| Git                 | System git                              | Container-installed | Clone/pull at scan time via shell invocation                                     |
| Container           | Docker                                  | Latest              | Single container; multi-stage build; Linux-based                                 |

### Solution Structure

The source tree is organized to accommodate future non-.NET projects. The `src/dotnet/` directory contains all .NET projects; `src/java/`, `src/python/`, and `src/node/` are placeholder directories for future expansion.

```
vulgata/
├── src/
│   ├── dotnet/
│   │   ├── Vulgata.Web/                 # Blazor Web App (UI host + SignalR + Identity)
│   │   ├── Vulgata.Web.ViewModels/      # MVVM ViewModels (CommunityToolkit.Mvvm)
│   │   ├── Vulgata.Core/                # Domain layer (DDD entities, value objects, domain services)
│   │   ├── Vulgata.Infrastructure/      # Persistence (EF Core), Git, CodeGraph, LLM clients
│   │   ├── Vulgata.Agents/              # Worker/Chat agent definitions, result contracts, prompts
│   │   └── Vulgata.Shared/              # DTOs, contracts, constants shared across projects
│   ├── java/                            # Future Java projects (placeholder)
│   ├── python/                          # Future Python projects (placeholder)
│   └── node/                            # Future Node.js projects (placeholder)
├── docker/
│   └── Dockerfile                       # Multi-stage: SDK build → runtime + git + codegraph
├── prompts/                             # Externalized agent prompt files (Docker volume mount)
├── docs/                                # Project documentation
├── Vulgata.sln
└── .gitignore
```

### Project Responsibilities

| Project                    | Layer               | Key Contents                                                                                                                                                                                                           |
| -------------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Vulgata.Web**            | Presentation + Host | Blazor components (Pages, Layouts, Shared), SignalR hubs, Identity setup (ApplicationUser, ApplicationDbContext), Program.cs host configuration. Top navbar with 对话/管理后台 routes. References all other projects. |
| **Vulgata.Core**           | Domain (DDD)        | Entities (System, Repository, ScanRun, Document, Edge, Uncertainty), ValueObjects, DomainServices interfaces, Repository interfaces, DomainEvents. Zero external dependencies.                                         |
| **Vulgata.Infrastructure** | Infrastructure      | VulgataDbContext + EF Core migrations, repository implementations, Orchestrator workflow services, GitCloneService (shells out to git), CodeGraphCliService (shells out to codegraph CLI), LLMProviderManager, OpenAI client. References Vulgata.Core. |
| **Vulgata.Agents**         | Agent execution     | WorkerAgent, ChatAgent, Worker execution adapter contracts, structured result contracts, embedded prompt resources. References Vulgata.Core and infrastructure abstractions as needed.                                   |
| **Vulgata.Web.ViewModels**  | Presentation (MVVM) | MVVM ViewModels using CommunityToolkit.Mvvm. Separate project for testability without web host. References Vulgata.Shared.                                                                                            |
| **Vulgata.Shared**         | Cross-cutting       | DTOs, API contracts, enum definitions, role name constants. Zero or minimal dependencies.                                                                                                                              |

### Identity & Authorization

**Decision:** ASP.NET Core Identity with cookie-based authentication and role-based authorization.

**Roles (seeded at startup):**

| Role        | Access Level                                               | PRD Reference |
| ----------- | ---------------------------------------------------------- | ------------- |
| Administrator | Full system access: all CRUD, all scans, all configuration | FR-1.4        |
| SystemOwner | Manage assigned systems/repositories, run scans, view results | FR-1.4        |
| User        | Read-only chat and document access; no 管理后台 entry by default | FR-1.4        |

**Implementation:**

- System ownership is represented as scoped assignments between users and Systems; SystemOwner access is limited to assigned Systems unless the user is also an Administrator.
- `ApplicationDbContext` (Identity tables) and `VulgataDbContext` (domain tables) are separate DbContexts sharing the same PostgreSQL database.
- Razor components use `[Authorize(Roles = "Administrator")]` and `<AuthorizeView Roles="SystemOwner">` for access control.
- The Blazor template's built-in Login/Register/Manage pages are used with Fluent UI styling.

### Docker Strategy

**Decision:** docker-compose with two containers: Blazor app + PostgreSQL 17.

**Key points:**

- **App container:** multi-stage build — `mcr.microsoft.com/dotnet/sdk:10.0` for build, `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime.
- **Git:** Installed via `apt-get` in the runtime stage (required for `git clone` at scan time).
- **CodeGraph CLI:** Downloaded during build, copied into runtime stage. Invoked via shell by `CodeGraphCliService`.
- **Non-root user:** `USER app` (built into .NET images since .NET 8) for security.
- **PostgreSQL container:** `postgres:17-alpine` — lightweight (\~150MB), production-grade.
- **Volumes:** `pgdata` named volume for PostgreSQL data persistence; `./prompts` bind mount for prompt iteration.
- **Port:** `8080` (ASP.NET Core default since .NET 8).
- **Startup:** `docker-compose up` — single command, both containers orchestrated.
- **Health check:** app depends on PostgreSQL health check before starting.

### UI Component Strategy

**Decision:** Microsoft Fluent UI Blazor as the primary component library, with Z.Blazor.Diagrams for graph visualization only.

| Feature                                 | Component Library                                       |
| --------------------------------------- | ------------------------------------------------------- |
| Top navbar (品牌 + 对话/管理后台 + bell)  | Fluent UI (FluentNavbar, FluentNavMenu)                 |
| 管理后台 holy grail layout               | Blazor nested layouts + Fluent UI                       |
| Chat interface                          | Fluent UI (FluentTextArea, FluentMessageBar)            |
| Forms, inputs, selects, buttons         | Fluent UI (FluentTextField, FluentSelect, FluentButton) |
| Data tables (system/repo list)          | Fluent UI (FluentDataGrid)                              |
| Dialogs, cards, progress bars           | Fluent UI (FluentDialog, FluentCard, FluentProgressBar) |
| Auth pages (login, register)            | Built-in Identity Razor pages + Fluent UI styling       |
| Knowledge graph canvas                  | Z.Blazor.Diagrams (specialized graph rendering)         |
| System/Repo tree view                   | Fluent UI (FluentTreeView)                              |
| Notification slide-out panel            | Fluent UI (FluentPanel)                                 |
| Document viewer (Markdown)              | Fluent UI + Markdown rendering library                  |

**Rationale:** Fluent UI Blazor is the official Microsoft component library with a modern Microsoft look, no JS build chain, and comprehensive coverage of standard UI patterns. Z.Blazor.Diagrams is the only exception — used exclusively for the live knowledge graph visualization, which requires specialized node/edge rendering not available in standard component libraries.

### Blazor Layout & Routing Strategy

**Top-Level Routes:**
- `/` or `/chat` → `ChatPage.razor` (default landing after login, uses `MainLayout`)
- `/management` → `ManagementLayout.razor` (holy grail layout with sub-routes)
- `/auth/login` → `LoginPage.razor`
- `/auth/register` → `RegisterPage.razor`

**MainLayout (Top Navbar):**
```
┌──────────────────────────────────────────────────┐
│ 品牌标识 | 对话 | 管理后台         🔔(铃铛) 👤(头像) │
├──────────────────────────────────────────────────┤
│ @Body (child route renders here)                 │
└──────────────────────────────────────────────────┘
```

**ManagementLayout (Holy Grail, nested under `/management`):**
```
┌──────────────────────────────────────────────────┐
│ 管理后台顶栏: 系统管理 | 图谱 | 文档 | 扫描历史 | 设置 │
├────────────┬─────────────────────────────────────┤
│ 左侧栏       │  @Body (sub-route renders here)     │
│ (导航 + 树)  │                                      │
└────────────┴─────────────────────────────────────┘
```

**管理后台 Sub-Routes:**
| Route | Page | Left Sidebar | Main Content |
|---|---|---|---|
| `/management` or `/management/dashboard` | `DashboardPage.razor` | System tree view | System detail / Repo detail (split view) |
| `/management/graph` | `GraphPage.razor` | — (hidden) | Full-screen Z.Blazor.Diagrams canvas with floating toolbar |
| `/management/documents` | `DocumentsPage.razor` | Document tree (virtual dir) | Markdown document viewer (split-pane) |
| `/management/scan-history` | `ScanHistoryPage.razor` | Scan run list | Scan detail + results |
| `/management/settings` | `SettingsPage.razor` | Settings nav | LLM Provider config / User management |

**Dashboard (系统管理) — inline creation:**
- "+ 新建系统" button on tree view → opens `CreateSystemDialog.razor`
- "+ 新建仓库" button on system detail DataGrid → opens `CreateRepoDialog.razor`
- No separate create pages — dialogs keep context.

### Key NuGet Packages

| Package                                             | Purpose                                                 |
| --------------------------------------------------- | ------------------------------------------------------- |
| `Microsoft.FluentUI.AspNetCore.Components`          | UI component library                                    |
| `CommunityToolkit.Mvvm`                             | MVVM source generators                                  |
| `Microsoft.Agents.AI.Foundry` (prerelease)          | Candidate Worker Agent execution infrastructure         |
| `Microsoft.Agents.AI.Workflows` (prerelease)        | Candidate typed worker workflow adapter                 |
| `Npgsql.EntityFrameworkCore.PostgreSQL`             | EF Core PostgreSQL provider                             |
| `Microsoft.EntityFrameworkCore.Design`              | EF Core migrations tooling                              |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Identity with EF Core storage                           |
| `Z.Blazor.Diagrams`                                 | Knowledge graph visualization                           |
| `OpenAI`                                            | LLM client (DeepSeek V4 via OpenAI-compatible endpoint) |

### Known Limitations & Future Considerations

- **SignalR without Redis:** Single app instance only. Acceptable for V1 demo; Redis backplane needed for horizontal scaling of the app tier in production.
- **docker-compose vs. Kubernetes:** Two-container docker-compose is sufficient for V1; orchestration upgrade path exists (Kubernetes, Docker Swarm) if scaling beyond single app instance.
- **In-process agents:** Worker exceptions could crash the application. Aggressive error boundaries and try/catch isolation required.
- **CodeGraph as external CLI:** Shell invocation adds latency. Acceptable for demo scale; in-process integration possible post-V1.
- **Fluent UI version coupling:** Component library updates may introduce breaking changes. Pin to specific version in V1.
- **MAF prerelease packages:** API surface may change. Worker Agent adapter spike must validate compatibility; fallback to direct OpenAI SDK or custom worker adapter if needed.

## Implementation Patterns & Consistency Rules

### Critical Conflict Points Identified

28 areas where AI agents could make different choices — all resolved below across 5 categories plus 6 additional patterns surfaced during cross-agent review.

### Naming Patterns

**Database Naming Conventions:**

- EF Core default PascalCase: tables `Systems`, `ScanRuns`, `Documents`; columns `RepositoryId`, `CreatedAt`
- No custom naming policy — zero config, matches C# entity class names exactly
- Foreign keys: EF Core convention (`{NavigationProperty}Id`)
- Indexes: EF Core convention (`IX_{Table}_{Column}`)
- Table names: singular PascalCase matching entity name (not pluralized)

**API Naming Conventions:**

- `/api/` prefix + plural nouns: `GET /api/systems`, `POST /api/scans`, `GET /api/documents/{id}`
- Route parameters: `{id}` (lowercase, no type prefix)
- Query parameters: camelCase (`?systemId=5&includeInactive=false`)
- Clear separation from Blazor `@page` routes (no `/api/` prefix on pages)
- Pluralization: use standard English plural (`/api/repositories`, not `/api/repositorys`); for irregular nouns, prefer the established REST convention (`/api/people` not `/api/persons`)

**Code Naming Conventions:**

- Async methods: mandatory `Async` suffix on all `Task`/`Task<T>`/`ValueTask`/`ValueTask<T>` returning methods (`GetSystemAsync()`, `RunScanAsync()`)
- Private fields: `_camelCase` (`_logger`, `_dbContext`, `_httpClient`)
- Interfaces: `I` prefix (`ISystemRepository`, `IDocumentGraphStore`)
- Domain model types (entities, value objects, aggregates): no suffix — context implies role (`System`, `ScanRun`, `Document`, `Edge`)
- Architectural role types: keep suffix for clarity (`ISystemRepository`, `ScanCoordinatorService`, `OrchestratorService`, `DocumentValidator`, `ScanHub`, `SystemsViewModel`)
- Blazor components: feature folders under `Pages/` and `Components/`, routable pages get `Page` suffix (`ChatPage.razor`, `DashboardPage.razor`)
- LLM agents: role-based + `Agent` suffix (`WorkerAgent`, `ChatAgent`)
- ViewModels: `{Feature}ViewModel` (`SystemsViewModel`, `ScanProgressViewModel`)
- Domain events: past-tense noun (`ScanCompletedDomainEvent`, `DocumentGeneratedDomainEvent`)
- SignalR hub methods: past-tense noun (`ScanCompleted`, `DocumentGenerated`, `LinkResolved`)

### Structure Patterns

**Project Organization:**

- ViewModels in separate `Vulgata.Web.ViewModels` project (clean separation, testable without web host)
- Blazor pages under `Pages/` root folder, reusable components under `Components/` root folder
- DTOs all in `Vulgata.Shared` project (single source of truth; WASM client references this)
- Tests in single `tests/Vulgata.Tests/` project

**File Structure Patterns:**

- EF Core entity configuration: `IEntityTypeConfiguration<T>` classes in `Vulgata.Infrastructure/Configurations/`
- Domain service interfaces in `Vulgata.Core/DomainServices/`, implementations in `Vulgata.Infrastructure/Services/`
- Worker and Chat Agent definitions in `Vulgata.Agents/`, prompts as embedded resources in `Vulgata.Agents/Prompts/`
- FluentValidation validators co-located with validated types (e.g., `Vulgata.Shared/Validators/` for DTO validators)
- Query services for complex read operations (recursive CTEs, graph traversal) in `Vulgata.Infrastructure/Queries/`

**EF Core Configuration Registration:**

- Use assembly scanning: `modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulgataDbContext).Assembly)`
- Never register individual `IEntityTypeConfiguration<T>` instances manually

**EF Core Migration Strategy:**

- Two DbContexts (`ApplicationDbContext` for Identity, `VulgataDbContext` for domain) share one PostgreSQL database
- `VulgataDbContext` owns the domain schema; `ApplicationDbContext` uses a separate migration history table via `optionsBuilder.UseNpgsql(..., b => b.MigrationsHistoryTable("__IdentityMigrationsHistory"))`
- Run migrations for both contexts at startup via `context.Database.MigrateAsync()`

### Format Patterns

**API Response Formats:**

- Success: direct return of DTO or collection (`[SystemDto]`, `SystemDto`) with appropriate status code (200, 201)
- Error: ASP.NET Core ProblemDetails (RFC 7807) via `AddProblemDetails()` — standardized `type`, `title`, `status`, `detail` fields
- No wrapping envelope — status codes distinguish success from error
- ProblemDetails applies to Minimal API endpoints (called by WASM client and external consumers); Blazor Server pages handle errors via circuit, not HTTP

**Data Exchange Formats:**

- JSON serialization: camelCase (ASP.NET Core default, `JsonSerializerDefaults.Web`)
- Date/time: UTC everywhere, ISO 8601 strings in API (`"2026-06-18T14:30:00Z"`)
- Boolean: `true`/`false` (JSON native)
- Null: omit from response when possible; `null` when field is semantically absent
- PostgreSQL `timestamp with time zone` storage; all `DateTime` properties use `DateTimeKind.Utc`

**EF Core Configuration Style:**

- Fluent API only in `IEntityTypeConfiguration<T>` classes — entities remain pure POCOs
- No data annotations on domain entities (preserves DDD purity)
- Configuration classes registered via `modelBuilder.ApplyConfigurationsFromAssembly()`

### Communication Patterns

**Event System Patterns:**

- SignalR: domain-specific hubs — `ScanHub` (scan progress, document generation events), `GraphHub` (node/edge changes)
- Maximum 3-5 hubs total (one per bounded context); if the domain model grows beyond this, consolidate into fewer hubs with domain-scoped groups
- Hub method naming: past-tense noun (`ScanCompleted`, `DocumentGenerated`)
- All hubs require `[Authorize]` attribute; unauthenticated connections rejected
- `OnDisconnectedAsync` must clean up per-connection state

**SignalR Reconnection Strategy:**

- On client reconnect, request current scan/graph status from server
- Server replays latest progress/state to reconnecting client
- Scan progress messages use structured model: `Phase`, `CurrentFile`, `FilesProcessed`, `TotalFiles`, `ReferencesFound`, `EstimatedSecondsRemaining`

**Graph Update Protocol:**

- Diff-based updates only: `NodeAdded`, `NodeRemoved`, `EdgeAdded`, `EdgeRemoved` events
- Never send full-graph replacement on incremental changes
- Preserve client viewport (zoom/pan) across updates
- New nodes fade in; removed nodes fade out (200-400ms transition)

**State Management Patterns:**

- UI messaging: CommunityToolkit `IMessenger` (`WeakReferenceMessenger.Default`) for ViewModel-to-ViewModel communication
- Domain events: custom `IDomainEventDispatcher` interface
- Domain events are collected during `SaveChangesAsync`, then dispatched **after** the transaction commits (not inside the transaction — prevents nested write contention from event handlers that touch the database)
- Domain event handlers implement `IDomainEventHandler<TEvent>` and are registered via DI
- Agent integration events: separate from domain events; published via a channel for cross-boundary communication; distinct dispatcher, distinct handlers, distinct naming

**Logging Patterns:**

- `ILogger<T>` with structured logging templates (no Serilog — built-in is sufficient for V1)
- Log levels with decision rules:

| Level           | Decision Rule                                                                                                                   | Example                                                     |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| **Debug**       | Only useful during local development. Must not appear in production logs.                                                       | "Query compiled in 3ms with parameters: ..."                |
| **Information** | A normal, expected business event that confirms the system is working.                                                          | "Scan {ScanId} started for system {SystemId}"               |
| **Warning**     | An unexpected condition that the system handled gracefully. No human action required now, but might indicate a growing problem. | "Retry 2/3 for LLM API call — timeout"                      |
| **Error**       | A failure in the current operation. A human should investigate within the current sprint.                                       | "Failed to save document {DocId}. Transaction rolled back." |
| **Critical**    | The application or a subsystem cannot continue without intervention.                                                            | "Database connection pool exhausted. Health check failing." |

### Process Patterns

**Error Handling Patterns:**

- Exceptions for unexpected failures; global `ExceptionHandlerMiddleware` catches unhandled exceptions
- Global handler maps exceptions to ProblemDetails responses with appropriate status codes
- Domain-level validation errors: throw `DomainException` (caught by middleware, mapped to 422)
- Agent-level errors: retry once with fallback provider, then record failure and continue scan
- All exception boundaries must log before re-throwing or handling

**Validation Patterns:**

- FluentValidation for all input validation (API requests, DTOs, command objects)
- Validators registered via `AddValidatorsFromAssemblyContaining<T>()` in DI
- Minimal API endpoints use endpoint filters for automatic validation
- Blazor forms use `FluentValidationValidator` component (not MVC middleware — `AddFluentValidationAutoValidation` does not apply to Blazor)
- Domain entities enforce invariants in constructors/methods (no external validator needed for domain rules)

**Repository Patterns:**

- Specific repositories per aggregate root: `ISystemRepository`, `IRepositoryRepository`, `IScanRunRepository`, `IDocumentRepository`
- Interfaces in `Vulgata.Core`, implementations in `Vulgata.Infrastructure`
- Each repository exposes only the query/command methods relevant to that aggregate
- No generic `IRepository<T>` — each repository is purpose-built for its aggregate
- Complex read queries (recursive CTEs, graph traversal, cross-repo matching) go in dedicated query services, not repositories

**Persistence Abstraction:**

- Repository interfaces return domain entities only — never `IQueryable<T>`, `DbSet<T>`, or EF Core-specific types
- Core project has zero references to EF Core or PostgreSQL packages
- Query services encapsulate storage-specific query logic behind interfaces in Core
- Migration path: swap Infrastructure implementations (e.g., `SqliteDocumentRepository` → `MongoDocumentRepository`); Core stays untouched
- This abstraction enables future migration to MongoDB, PostgreSQL, or other storage without changing domain logic

**Persistence Concurrency (PostgreSQL MVCC):**

- PostgreSQL uses Multi-Version Concurrency Control (MVCC) — readers never block writers and vice versa
- No special configuration needed; concurrent reads and writes work out of the box
- Document that this architecture supports a single app instance; horizontal scaling of the app tier requires SignalR Redis backplane
- PostgreSQL connection pooling managed by Npgsql (`Max Pool Size` configurable via connection string)

**Loading State Patterns:**

- `LoadState` enum in `Vulgata.Shared`: `Idle`, `Loading`, `Loaded`, `Refreshing`, `Empty`, `NoResults`, `Error`, `Cancelling`
- Every ViewModel that loads data exposes a `LoadState State` property
- `Loading`: initial load, show skeleton or full-page spinner
- `Refreshing`: background refresh with existing content still visible, show subtle progress indicator
- `Empty`: no data exists yet, show onboarding CTA
- `NoResults`: search/filter returned zero results, show filter adjustment guidance
- `Error`: include error kind for differentiated messaging (network, auth, server, validation)
- `Cancelling`: brief transitional state (user clicked cancel), then transitions to `Idle` or `Loaded`
- Blazor components bind to `State` for conditional rendering
- SignalR real-time updates transition `Loaded` content without returning to `Loading`
- State transitions should use 200-400ms animations (fade, slide) for perceived smoothness

**Agent Task State Machine:**

- Agent task lifecycle: `Queued → Running → Completed | Failed | Cancelled`
- Retry policy: one retry with fallback LLM provider on failure
- After retry exhaustion: record failure, log at Error level, continue scan with remaining tasks
- Cancellation: cooperative via `CancellationToken`; respect cancellation within 5 seconds
- No dead-letter queue for V1; failed tasks are visible in scan results for manual review

**Background Job Pattern:**

- Long-running agent tasks (document generation) execute via `BackgroundService` / `IHostedService`, not on the ASP.NET request thread
- Job queue: `Channel<T>` for in-process task dispatch
- Scan Coordinator enqueues work items; BackgroundService dequeues and executes
- Progress reported via SignalR; completion triggers domain events (dispatched after persistence)
- Blazor UI polls or receives SignalR notifications — never blocks on agent completion

### Enforcement Guidelines

**All AI Agents MUST:**

- Follow all naming conventions above — no deviation on table names, API routes, async suffixes, or event naming
- Place files in the correct project and folder as defined in Structure Patterns
- Use `IEntityTypeConfiguration<T>` for all EF Core configuration (never data annotations on entities)
- Use FluentValidation for all input validation (never manual `if` checks in endpoints)
- Use `LoadState` enum for all data-loading ViewModels (never bare `bool IsLoading`)
- Log at appropriate levels as defined in the decision rules table (never `Console.WriteLine` or `Debug.WriteLine`)
- Return ProblemDetails for all Minimal API error responses (never custom error DTOs)
- Dispatch domain events through `IDomainEventDispatcher` **after** `SaveChangesAsync` commits (never inline during the transaction)
- Use assembly scanning for `IEntityTypeConfiguration<T>` registration (never manual registration)
- Apply `[Authorize]` to all SignalR hubs
- Run long-running agent work through `BackgroundService`, not on the request thread

**Pattern Enforcement:**

- `.editorconfig` file encodes naming rules as analyzable conventions (`_camelCase` fields, PascalCase types, `Async` suffix)
- Roslyn analyzer project for rules `.editorconfig` cannot express (ViewModel suffix, Agent suffix, `LoadState` usage)
- `dotnet format` runs in CI to enforce conventions automatically
- Code review checklist in `docs/agents/code-review-checklist.md` references these patterns
- Architecture document is the single source of truth — patterns here override any agent defaults
- Pattern violations found in review must be fixed before merge
- New patterns discovered during implementation must be proposed as updates to this document

### Pattern Examples

**Good Example — Repository (Persistence-Abstracted):**

```csharp
// Vulgata.Core/DomainServices/ISystemRepository.cs
public interface ISystemRepository
{
    Task<System?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<System>> GetByOwnerAsync(string ownerId, CancellationToken ct);
    Task AddAsync(System system, CancellationToken ct);
    Task UpdateAsync(System system, CancellationToken ct);
}
```

**Good Example — ViewModel with LoadState:**

```csharp
// Vulgata.Web.ViewModels/SystemsViewModel.cs
public partial class SystemsViewModel : ObservableObject
{
    [ObservableProperty]
    private LoadState _state = LoadState.Idle;

    [ObservableProperty]
    private IReadOnlyList<SystemDto> _systems = Array.Empty<SystemDto>();

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        State = LoadState.Loading;
        try
        {
            Systems = await _systemService.GetAllAsync();
            State = Systems.Count > 0 ? LoadState.Loaded : LoadState.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load systems");
            ErrorMessage = "Could not load systems. Please try again.";
            State = LoadState.Error;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        State = LoadState.Refreshing;
        // ... keep existing Systems visible during refresh
    }
}
```

**Good Example — Domain Event Dispatch After Commit:**

```csharp
// Vulgata.Infrastructure/VulgataDbContext.cs
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var domainEvents = ChangeTracker.Entries<Entity>()
        .SelectMany(e => e.Entity.PopDomainEvents())
        .ToList();

    var result = await base.SaveChangesAsync(ct);

    // Dispatch AFTER commit — prevents nested write contention
    foreach (var domainEvent in domainEvents)
        await _domainEventDispatcher.DispatchAsync(domainEvent, ct);

    return result;
}
```

**Good Example — Background Job with Channel:**

```csharp
// Vulgata.Infrastructure/BackgroundJobs/ScanJobWorker.cs
public sealed class ScanJobWorker : BackgroundService
{
    private readonly Channel<ScanWorkItem> _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessItemAsync(item, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Scan job {JobId} failed", item.JobId);
            }
        }
    }
}
```

**Anti-Patterns — What to Avoid:**

- ❌ `[Table("systems")]` attribute on entity — use `IEntityTypeConfiguration<T>` instead
- ❌ `if (string.IsNullOrEmpty(request.Name)) return Results.BadRequest("Name required");` — use FluentValidation
- ❌ `bool IsLoading { get; set; }` — use `LoadState` enum
- ❌ `GET /systems` as API route — use `GET /api/systems`
- ❌ `Console.WriteLine("Scan done")` — use `_logger.LogInformation("Scan {ScanId} completed", scanId)`
- ❌ `public class SystemEntity` — no suffix on domain model types; use `public class System`
- ❌ `await _documentRepository.AddAsync(doc); await _eventDispatcher.DispatchAsync(...);` — dispatch in SaveChanges, after commit
- ❌ `public IQueryable<Document> Documents { get; }` in repository — leaks EF Core; use specific query methods
- ❌ `modelBuilder.ApplyConfiguration(new DocumentConfiguration())` — use `ApplyConfigurationsFromAssembly`
- ❌ `Task.Run(() => agent.ExecuteAsync(...))` in a controller — use `BackgroundService` + `Channel<T>`

## Project Structure & Boundaries

### Complete Project Directory Structure

```
vulgata/
├── .gitignore
├── .editorconfig
├── Vulgata.sln
├── Directory.Build.props                    # Shared MSBuild properties (nullable, implicit usings, etc.)
├── Directory.Packages.props                 # Central package management
│
├── src/
│   ├── dotnet/
│   │   ├── Vulgata.Web/                     # Blazor Web App (UI host + SignalR + Identity)
│   │   │   ├── Vulgata.Web.csproj
│   │   │   ├── Program.cs                   # Host builder, DI, middleware pipeline
│   │   │   ├── appsettings.json
│   │   │   ├── appsettings.Development.json
│   │   │   ├── Properties/
│   │   │   │   └── launchSettings.json
│   │   │   ├── Components/
│   │   │   │   ├── _Imports.razor
│   │   │   │   ├── App.razor                # Root component, render mode assignment
│   │   │   │   ├── Routes.razor             # Route table
│   │   │   │   ├── Layout/
│   │   │   │   │   ├── MainLayout.razor        # Top navbar (品牌 + 对话 | 管理后台 + 🔔 + 👤)
│   │   │   │   │   ├── MainLayout.razor.css
│   │   │   │   │   └── ManagementLayout.razor  # Nested holy grail layout (sidebar + content)
│   │   │   │   ├── Pages/
│   │   │   │   │   ├── Chat/
│   │   │   │   │   │   └── ChatPage.razor             # Default route "/" — chat interface
│   │   │   │   │   ├── Management/                     # 管理后台 — holy grail layout
│   │   │   │   │   │   ├── Dashboard/                  # 系统管理 — system/repo scan progress
│   │   │   │   │   │   │   └── DashboardPage.razor     # System tree (left) + System/Repo detail (right)
│   │   │   │   │   │   ├── Graph/
│   │   │   │   │   │   │   └── GraphPage.razor         # Full-screen Z.Blazor.Diagrams + toolbar
│   │   │   │   │   │   ├── Documents/
│   │   │   │   │   │   │   └── DocumentsPage.razor     # Split-pane: doc tree (left) + viewer (right)
│   │   │   │   │   │   ├── ScanHistory/
│   │   │   │   │   │   │   └── ScanHistoryPage.razor   # Scan run list + detail
│   │   │   │   │   │   └── Settings/
│   │   │   │   │   │       └── SettingsPage.razor       # LLM Provider config + User management
│   │   │   │   │   └── Auth/
│   │   │   │   │       ├── LoginPage.razor
│   │   │   │   │       └── RegisterPage.razor
│   │   │   │   ├── Shared/                              # Reusable UI components
│   │   │   │   │   ├── LoadingOverlay.razor
│   │   │   │   │   ├── ErrorDisplay.razor
│   │   │   │   │   ├── EmptyState.razor
│   │   │   │   │   ├── ConfirmDialog.razor
│   │   │   │   │   ├── ScanProgressBar.razor
│   │   │   │   │   ├── NotificationPanel.razor          # Slide-out HITL notification panel
│   │   │   │   │   ├── CreateSystemDialog.razor         # Inline system creation dialog
│   │   │   │   │   ├── CreateRepoDialog.razor           # Inline repo creation dialog
│   │   │   │   │   ├── ModeSelector.razor               # 业务模式/技术模式 toggle
│   │   │   │   │   └── InlineSelector.razor             # System/repo multi-select for chat
│   │   │   ├── Hubs/
│   │   │   │   ├── ScanHub.cs                # Scan progress + document events
│   │   │   │   └── GraphHub.cs               # Node/edge change events
│   │   │   └── wwwroot/
│   │   │       ├── app.css
│   │   │       └── favicon.ico
│   │   │
│   │   ├── Vulgata.Web.ViewModels/           # MVVM ViewModels
│   │   │   ├── Vulgata.Web.ViewModels.csproj
│   │   │   ├── Chat/
│   │   │   │   └── ChatViewModel.cs
│   │   │   ├── Management/
│   │   │   │   ├── Dashboard/
│   │   │   │   │   └── DashboardViewModel.cs         # System tree + System/Repo detail
│   │   │   │   ├── Graph/
│   │   │   │   │   └── GraphViewModel.cs             # Graph layout, filter, zoom state
│   │   │   │   ├── Documents/
│   │   │   │   │   └── DocumentsViewModel.cs         # Doc tree + viewer state
│   │   │   │   ├── ScanHistory/
│   │   │   │   │   └── ScanHistoryViewModel.cs       # Scan run list + detail
│   │   │   │   └── Settings/
│   │   │   │       └── SettingsViewModel.cs          # LLM config + user mgmt
│   │   │   └── Auth/
│   │   │       ├── LoginViewModel.cs
│   │   │       └── RegisterViewModel.cs
│   │   │
│   │   ├── Vulgata.Core/                     # Domain layer (DDD, zero external deps)
│   │   │   ├── Vulgata.Core.csproj
│   │   │   ├── Entity.cs                     # Base class with domain event collection
│   │   │   ├── AggregateRoot.cs              # Marker for aggregate roots
│   │   │   ├── ValueObject.cs                # Base class for value objects
│   │   │   ├── DomainException.cs
│   │   │   ├── Entities/
│   │   │   │   ├── System.cs                 # Aggregate root
│   │   │   │   ├── Repository.cs             # Entity (child of System)
│   │   │   │   ├── ScanRun.cs                # Aggregate root
│   │   │   │   ├── Document.cs               # Logical Document aggregate root
│   │   │   │   ├── DocumentVersion.cs        # Immutable generated snapshot
│   │   │   │   ├── DocumentSection.cs        # Section-level retrieval row
│   │   │   │   ├── IndexEntry.cs             # Document-level retrieval row
│   │   │   │   ├── Edge.cs                   # Document Version graph edge
│   │   │   │   ├── Uncertainty.cs            # Known unknown
│   │   │   │   ├── EntryPoint.cs             # Pass 2 trace start metadata
│   │   │   │   ├── CommunicationEndpoint.cs  # Provider-side surface
│   │   │   │   ├── DispatchSite.cs           # Consumer-side invocation
│   │   │   │   ├── FlowGroup.cs              # Pass 2 clustered flow group
│   │   │   │   ├── FlowTrace.cs              # Persisted trace evidence
│   │   │   │   ├── CommunicationPattern.cs   # Entity (cross-repo catalog entry)
│   │   │   │   └── LLMProvider.cs            # Entity (provider configuration)
│   │   │   ├── ValueObjects/
│   │   │   │   ├── DocumentType.cs           # CL or BL
│   │   │   │   ├── EdgeConfidence.cs         # Confirmed or Inferred
│   │   │   │   ├── ScanStatus.cs
│   │   │   │   └── DocumentStatus.cs
│   │   │   ├── DomainServices/               # Interfaces only
│   │   │   │   ├── ISystemRepository.cs
│   │   │   │   ├── IRepositoryRepository.cs
│   │   │   │   ├── IScanRunRepository.cs
│   │   │   │   ├── IDocumentRepository.cs
│   │   │   │   ├── IEdgeRepository.cs
│   │   │   │   ├── IUncertaintyRepository.cs
│   │   │   │   ├── ILLMProviderRepository.cs
│   │   │   │   ├── IDocumentGraphQueryService.cs
│   │   │   │   ├── IRetrievalQueryService.cs
│   │   │   │   ├── IFlowTraceQueryService.cs
│   │   │   │   └── ICrossRepoResolutionService.cs
│   │   │   └── DomainEvents/
│   │   │       ├── IDomainEvent.cs
│   │   │       ├── IDomainEventDispatcher.cs
│   │   │       ├── IDomainEventHandler.cs
│   │   │       ├── ScanCompletedDomainEvent.cs
│   │   │       ├── DocumentGeneratedDomainEvent.cs
│   │   │       ├── EdgeCreatedDomainEvent.cs
│   │   │       └── UncertaintyResolvedDomainEvent.cs
│   │   │
│   │   ├── Vulgata.Infrastructure/           # Persistence, external services
│   │   │   ├── Vulgata.Infrastructure.csproj
│   │   │   ├── VulgataDbContext.cs           # Domain tables
│   │   │   ├── ApplicationDbContext.cs       # Identity tables
│   │   │   ├── DependencyInjection.cs        # AddVulgataInfrastructure() extension
│   │   │   ├── Configurations/               # IEntityTypeConfiguration<T>
│   │   │   │   ├── SystemConfiguration.cs
│   │   │   │   ├── RepositoryConfiguration.cs
│   │   │   │   ├── ScanRunConfiguration.cs
│   │   │   │   ├── DocumentConfiguration.cs
│   │   │   │   ├── DocumentVersionConfiguration.cs
│   │   │   │   ├── DocumentSectionConfiguration.cs
│   │   │   │   ├── IndexEntryConfiguration.cs
│   │   │   │   ├── EdgeConfiguration.cs
│   │   │   │   ├── UncertaintyConfiguration.cs
│   │   │   │   ├── EntryPointConfiguration.cs
│   │   │   │   ├── CommunicationEndpointConfiguration.cs
│   │   │   │   ├── DispatchSiteConfiguration.cs
│   │   │   │   ├── FlowGroupConfiguration.cs
│   │   │   │   ├── FlowTraceConfiguration.cs
│   │   │   │   ├── CommunicationPatternConfiguration.cs
│   │   │   │   └── LLMProviderConfiguration.cs
│   │   │   ├── Repositories/                 # Interface implementations
│   │   │   │   ├── SystemRepository.cs
│   │   │   │   ├── RepositoryRepository.cs
│   │   │   │   ├── ScanRunRepository.cs
│   │   │   │   ├── DocumentRepository.cs
│   │   │   │   ├── EdgeRepository.cs
│   │   │   │   ├── UncertaintyRepository.cs
│   │   │   │   └── LLMProviderRepository.cs
│   │   │   ├── Queries/                      # Complex read queries
│   │   │   │   ├── DocumentGraphQueryService.cs    # Recursive CTE traversal
│   │   │   │   ├── RetrievalQueryService.cs        # index_entries/document_sections search
│   │   │   │   ├── FlowTraceQueryService.cs        # flow trace context
│   │   │   │   └── CrossRepoResolutionService.cs   # Cross-Repository matching
│   │   │   ├── Services/                     # Domain service implementations
│   │   │   │   ├── DomainEventDispatcher.cs
│   │   │   │   ├── GitCloneService.cs        # Shells out to git
│   │   │   │   ├── CodeGraphCliService.cs    # Shells out to codegraph CLI
│   │   │   │   ├── OrchestratorService.cs    # Deterministic per-Repository workflow
│   │   │   │   └── LLMProviderManager.cs     # Multi-provider, failover, API key encryption
│   │   │   ├── BackgroundJobs/
│   │   │   │   ├── ScanJobWorker.cs          # BackgroundService + Channel<T>
│   │   │   │   └── ScanWorkItem.cs           # Work item DTO
│   │   │   ├── Identity/
│   │   │   │   └── ApplicationUser.cs        # Extends IdentityUser
│   │   │   └── Migrations/                   # EF Core migrations (auto-generated)
│   │   │       ├── VulgataDbContextModelSnapshot.cs
│   │   │       └── ApplicationDbContextModelSnapshot.cs
│   │   │
│   │   ├── Vulgata.Agents/                   # Worker/Chat agent execution and prompts
│   │   │   ├── Vulgata.Agents.csproj
│   │   │   ├── DependencyInjection.cs        # AddVulgataAgents() extension
│   │   │   ├── WorkerAgent.cs                # LLM agent: code analysis + doc generation
│   │   │   ├── ChatAgent.cs                  # LLM agent: document Q&A synthesis
│   │   │   ├── Contracts/
│   │   │   │   ├── Pass1Result.cs
│   │   │   │   ├── Pass1RefinementResult.cs
│   │   │   │   └── Pass2Result.cs
│   │   │   ├── Execution/
│   │   │   │   └── IWorkerAgentExecutor.cs   # Adapter boundary for MAF/OpenAI/custom execution
│   │   │   └── Prompts/                      # Embedded resources
│   │   │       ├── pass1-code-logic-worker.md
│   │   │       ├── pass1-refinement-worker.md
│   │   │       ├── pass2-business-flow-worker.md
│   │   │       └── chat-retrieval-synthesis.md
│   │   │
│   │   └── Vulgata.Shared/                   # DTOs, enums, constants
│   │       ├── Vulgata.Shared.csproj
│   │       ├── DTOs/
│   │       │   ├── SystemDto.cs
│   │       │   ├── RepositoryDto.cs
│   │       │   ├── ScanRunDto.cs
│   │       │   ├── DocumentDto.cs
│   │       │   ├── DocumentVersionDto.cs
│   │       │   ├── EdgeDto.cs
│   │       │   ├── UncertaintyDto.cs
│   │       │   ├── LLMProviderDto.cs
│   │       │   ├── ScanProgressDto.cs        # Phase, CurrentFile, FilesProcessed, etc.
│   │       │   ├── GraphUpdateDto.cs         # NodeAdded, EdgeAdded, etc.
│   │       │   └── ChatMessageDto.cs
│   │       ├── Enums/
│   │       │   ├── LoadState.cs              # Idle, Loading, Loaded, Refreshing, Empty, NoResults, Error, Cancelling
│   │       │   ├── ScanStatus.cs
│   │       │   ├── DocumentStatus.cs
│   │       │   ├── DocumentType.cs
│   │       │   ├── EdgeConfidence.cs
│   │       │   └── AgentTaskState.cs         # Queued, Running, Completed, Failed, Cancelled
│   │       ├── Constants/
│   │       │   ├── RoleNames.cs              # Administrator, SystemOwner, User
│   │       │   └── RouteConstants.cs         # /api/systems, etc.
│   │       └── Validators/
│   │           ├── SystemDtoValidator.cs
│   │           ├── RepositoryDtoValidator.cs
│   │           ├── ScanRequestValidator.cs
│   │           └── LLMProviderDtoValidator.cs
│   │
│   ├── java/                                # Future Java projects (placeholder)
│   │   └── .gitkeep
│   ├── python/                              # Future Python projects (placeholder)
│   │   └── .gitkeep
│   └── node/                                # Future Node.js projects (placeholder)
│       └── .gitkeep
│
├── tests/
│   └── Vulgata.Tests/
│       ├── Vulgata.Tests.csproj
│       ├── Core/
│       │   ├── Entities/
│       │   │   ├── SystemTests.cs
│       │   │   ├── ScanRunTests.cs
│       │   │   └── DocumentTests.cs
│       │   └── DomainServices/
│       │       └── DomainEventDispatcherTests.cs
│       ├── Infrastructure/
│       │   ├── Repositories/
│       │   │   ├── SystemRepositoryTests.cs
│       │   │   └── DocumentRepositoryTests.cs
│       │   ├── Queries/
│       │   │   ├── DocumentGraphQueryServiceTests.cs
│       │   │   └── CrossRepoResolutionServiceTests.cs
│       │   └── Services/
│       │       ├── GitCloneServiceTests.cs
│       │       └── LLMProviderManagerTests.cs
│       ├── Agents/
│       │   ├── WorkerAgentTests.cs
│       │   └── ChatAgentTests.cs
│       ├── Orchestration/
│       │   └── OrchestratorServiceTests.cs
│       ├── Web/
│       │   └── ViewModels/
│       │       ├── SystemsViewModelTests.cs
│       │       └── ScanRunViewModelTests.cs
│       ├── Integration/
│       │   ├── ScanPipelineTests.cs
│       │   └── DocumentGraphTests.cs
│       └── TestData/
│           └── SampleRepositories/           # Small git repos for integration tests
│
├── docker/
│   ├── Dockerfile                            # Multi-stage: SDK build → runtime + git + codegraph
│   ├── docker-compose.yml                    # App + PostgreSQL 17
│   └── .dockerignore
│
├── prompts/                                  # Externalized prompts (volume-mountable)
│   └── .gitkeep
│
└── docs/
    ├── agents/
    │   ├── domain.md
    │   ├── issue-tracker.md
    │   ├── triage-labels.md
    │   └── code-review-checklist.md
    ├── design/
    │   └── document-graph-anchordb-plus-plan.md
    ├── requirement-draft.md
    ├── UBIQUITOUS_LANGUAGE.md
    └── CONTEXT-MAP.md
```

### Architectural Boundaries

**API Boundaries:**

- Minimal API endpoints in `Vulgata.Web/Program.cs` (mapped via `MapGroup("/api/")`)
- All endpoints under `/api/` prefix; Blazor pages under `/` (no prefix)
- WASM client calls `/api/*` via `HttpClient`; Server-rendered pages use direct DI service calls
- Authentication boundary: `[Authorize]` on hub classes and API endpoint groups; cookie-based
- ProblemDetails for all API error responses; Blazor circuit errors handled via `ErrorDisplay.razor`

**Component Boundaries:**

- `Vulgata.Web` → references all other projects (composition root)
- `Vulgata.Web.ViewModels` → references `Vulgata.Shared`, `Vulgata.Core` (domain services interfaces)
- `Vulgata.Agents` → references `Vulgata.Core` and infrastructure abstractions needed for Worker/Chat execution
- `Vulgata.Infrastructure` → references `Vulgata.Core`
- `Vulgata.Core` → zero project references (pure domain)
- `Vulgata.Shared` → zero project references (standalone DTOs/enums)

**Service Boundaries:**

- Domain services: interfaces in `Core/DomainServices/`, implementations in `Infrastructure/Services/`
- Repositories: interfaces in `Core/DomainServices/`, implementations in `Infrastructure/Repositories/`
- Query services: interfaces in `Core/DomainServices/`, implementations in `Infrastructure/Queries/`
- Orchestrator: deterministic workflow service in `Infrastructure/Services/`, depends on Core interfaces and the Worker Agent executor boundary
- Worker/Chat Agents: defined in `Agents/`, depend on Core contracts and typed retrieval/tool interfaces
- Background jobs: `Infrastructure/BackgroundJobs/`, registered as `IHostedService`

**Data Boundaries:**

- `VulgataDbContext`: owns all domain tables (Systems, Repositories, Logical Documents, Document Versions, Edges, Uncertainties, endpoints, dispatch sites, flow traces, `index_entries`, `document_sections`, etc.)
- `ApplicationDbContext`: owns Identity tables only; separate migration history
- Both share single PostgreSQL database; separate schemas (`vulgata` for domain, `identity` for auth)
- Repository interfaces never expose `IQueryable<T>` or `DbSet<T>`
- Query services encapsulate raw SQL/CTE queries behind interfaces in Core

### Requirements to Structure Mapping

| FR Category                   | Primary Location                                                                                         | Subsystems       |
| ----------------------------- | -------------------------------------------------------------------------------------------------------- | ---------------- |
| FR-1 (Auth)                   | `Vulgata.Web` (Identity pages), `Vulgata.Infrastructure` (ApplicationUser, ApplicationDbContext)         | SP-1             |
| FR-2 (System & Repo CRUD)     | `Vulgata.Web/Components/Pages/Management/`, `Vulgata.Core`, `Vulgata.Infrastructure`                     | SP-2             |
| FR-3 (LLM Provider Config)    | `Vulgata.Web/Components/Pages/Management/`, `Vulgata.Infrastructure/Services/LLMProviderManager.cs`      | SP-1, SP-2       |
| FR-4 (Scan Pipeline)          | `Vulgata.Infrastructure/BackgroundJobs/`, `Vulgata.Infrastructure/Services/OrchestratorService.cs`, `Vulgata.Agents/` | SP-3, SP-4, SP-5 |
| FR-5 (Cross-Repo Detection)   | `Vulgata.Agents/Prompts/`, endpoint/dispatch tables, `Vulgata.Infrastructure/Queries/`                   | SP-5, SP-6, SP-7 |
| FR-6 (Uncertainty Resolution) | `Vulgata.Core` (Uncertainty), `Vulgata.Infrastructure/Queries/CrossRepoResolutionService.cs`             | SP-6, SP-7       |
| FR-7 (HITL)                   | `Vulgata.Web/Components/Pages/Management/`, `Vulgata.Core` (Uncertainty)                                 | SP-1, SP-9       |
| FR-8 (Third-Party Libraries)  | `Vulgata.Core`, `Vulgata.Infrastructure/Services/`, Worker Agent prompts                                 | SP-3, SP-5       |
| FR-9 (Git Monitoring)         | `Vulgata.Infrastructure/Services/GitCloneService.cs`, impact-analysis queries                            | SP-3, SP-7       |
| FR-10 (Database Tools)        | `Vulgata.Infrastructure/Queries/` and Worker Agent tool interfaces                                       | SP-5, SP-7       |
| FR-11 (Chat Interface)        | `Vulgata.Agents/ChatAgent.cs`, typed retrieval tools, `Vulgata.Web/Components/Pages/Chat/`               | SP-8             |
| FR-12 (Dashboard & Graph)     | `Vulgata.Web/Hubs/`, `Vulgata.Web/Components/Pages/Management/`, `Vulgata.Web.ViewModels/`               | SP-9             |
| FR-13 (MCP Integration)       | `Vulgata.Infrastructure/` (deferred)                                                                     | —                |
| FR-14 (User-Supplied Context) | `Vulgata.Web/Components/Pages/Management/`, `Vulgata.Core`                                               | SP-1, SP-2       |
| FR-15 (LSP Support)           | Deferred                                                                                                 | —                |

### Integration Points

**Internal Communication:**

- Scan Coordinator → BackgroundService (Channel<T>): enqueues Repository scan work items
- BackgroundService → OrchestratorService: executes deterministic per-Repository workflow
- OrchestratorService → WorkerAgent executor: dispatches typed pre-scan, Pass 1, refinement, and Pass 2 jobs
- WorkerAgent executor → VulgataDbContext: persists Document Versions and structured graph facts via repositories/services
- SaveChanges → DomainEventDispatcher: dispatches after commit
- DomainEventDispatcher → SignalR Hub: pushes events to UI
- ScanHub → Blazor interactive server client: real-time scan progress
- GraphHub → Blazor interactive server client: diff-based graph updates
- ViewModels ↔ IMessenger: in-process UI eventing

**External Integrations:**

- Git CLI: `GitCloneService` shells out for clone/pull at scan time
- CodeGraph CLI: `CodeGraphCliService` shells out for pre-scan structural analysis
- LLM API: `LLMProviderManager` → OpenAI SDK → DeepSeek V4 (or configured provider) for pre-scan, worker, and chat LLM task roles
- MCP: deferred for V1

**Data Flow (Scan Pipeline):**

```
User clicks Scan → ScanRun created (Queued)
  → ScanJobWorker dequeues → GitCloneService clones/pulls repo
  → CodeGraphCliService extracts Code Units and Source Anchors
  → OrchestratorService plans scan states and groups supersteps
  → WorkerAgent executor processes typed Pass 1 jobs
    → Cross-repo detection emits endpoints, dispatch sites, edges, and uncertainties
  → Document Versions and graph facts persisted via repositories/services
  → OrchestratorService runs bounded refinement predicates
  → OrchestratorService plans flow traces and clusters flow groups
  → Pass 2 WorkerAgent executor synthesizes BL Docs per flow group
  → index_entries, document_sections, and export artifacts rebuilt
  → Domain events dispatched after commit
  → SignalR pushes ScanCompleted, DocumentGenerated to UI
  → CrossRepoResolutionService matches endpoints/uncertainties across Repositories
  → Edges and stale notices created, GraphHub pushes NodeAdded/EdgeAdded
```

### File Organization Patterns

**Configuration Files:**

- `appsettings.json` / `appsettings.Development.json`: ASP.NET Core config (connection strings, LLM endpoints, logging)
- `Directory.Build.props`: shared MSBuild properties (nullable enable, implicit usings, treat warnings as errors)
- `Directory.Packages.props`: central package version management
- `.editorconfig`: naming conventions, code style (enforces `_camelCase`, `Async` suffix, etc.)
- `.gitignore`: standard .NET + Docker ignores

**Source Organization:**

- One `.csproj` per project; solution file at root
- Feature-folders within each project (not type-folders)
- Embedded resources for agent prompts (not external files at runtime)
- Migrations auto-generated by EF Core tooling

**Test Organization:**

- Single test project mirrors source structure
- `TestData/SampleRepositories/` for integration test fixtures
- Test naming: `{ClassUnderTest}Tests.cs`, method: `{Method}_{Scenario}_{ExpectedResult}`

**Asset Organization:**

- `Vulgata.Web/wwwroot/`: static CSS, favicon
- Fluent UI Blazor provides component CSS (no custom CSS framework)
- Z.Blazor.Diagrams provides graph CSS

### Development Workflow Integration

**Development Server:**

- `dotnet run --project src/dotnet/Vulgata.Web` starts the Blazor app
- Hot reload enabled for `.razor` and `.cs` changes
- PostgreSQL connection string in `appsettings.Development.json` points to local PostgreSQL instance
- For local dev without Docker: install PostgreSQL 17 locally or use `docker-compose up vulgata-db` for just the database

**Build Process:**

- `dotnet build Vulgata.sln` builds all projects
- `dotnet test tests/Vulgata.Tests` runs all tests
- `dotnet format Vulgata.sln` enforces `.editorconfig` conventions

**Docker Build:**

- `docker-compose build` builds the app image
- `docker-compose up` starts both app + PostgreSQL
- Multi-stage Dockerfile: SDK stage builds/publishes, runtime stage adds git + codegraph
- Volume: `pgdata` named volume for PostgreSQL data persistence
- Volume: `./prompts:/app/prompts` bind mount for prompt iteration

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:**

| Pair                                          | Status | Notes                                        |
| --------------------------------------------- | ------ | -------------------------------------------- |
| .NET 10 + MAF prerelease                      | ✅      | MAF targets .NET 8+                          |
| .NET 10 + EF Core 10.x + Npgsql 9.x           | ✅      | Npgsql targets .NET 10                       |
| .NET 10 + Fluent UI Blazor                    | ✅      | Fluent UI targets .NET 8+                    |
| .NET 10 + Z.Blazor.Diagrams                   | ✅      | Targets .NET 8+                              |
| Blazor Server + SignalR                       | ✅      | Built-in, same framework                     |
| Blazor interactive server + Minimal APIs      | ✅      | UI components call /api/\* where endpoint boundaries are useful |
| PostgreSQL 17 + EF Core + Npgsql              | ✅      | Full provider support, recursive CTEs        |
| docker-compose + PostgreSQL + Git + CodeGraph | ✅      | App container + postgres:17-alpine           |
| ASP.NET Core Identity + cookie auth + Blazor  | ✅      | Standard template pattern                    |
| CommunityToolkit.Mvvm + Blazor                | ✅      | Source generators, DI-compatible             |
| FluentValidation + Blazor forms               | ✅      | FluentValidationValidator component          |
| OpenAI SDK + DeepSeek V4                      | ✅      | OpenAI-compatible endpoint                   |
| Two DbContexts + single PostgreSQL DB         | ✅      | Separate schemas, separate migration history |

**Pattern Consistency:**

| Check                                                                | Status                                                              |
| -------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Repository interfaces return domain entities only (no IQueryable)    | ✅ Aligns with Persistence Abstraction                               |
| Domain events dispatched after SaveChanges                           | ✅ Good practice; PostgreSQL MVCC handles concurrent writes natively |
| BackgroundService + Channel<T> for agent work                        | ✅ Aligns with docker-compose, in-process constraint                 |
| SignalR hubs with \[Authorize]                                       | ✅ Aligns with cookie auth                                           |
| ProblemDetails for API errors                                        | ✅ Aligns with /api/ prefix separation                               |
| LoadState enum with 8 states                                         | ✅ Covers all UX states identified                                   |
| Agent Task State Machine (Queued→Running→Completed/Failed/Cancelled) | ✅ Aligns with Background Job pattern                                |
| Integration events separate from domain events                       | ✅ Prevents agent lifecycle coupling to DB transactions              |

**Structure Alignment:**

| Check                                                  | Status                  |
| ------------------------------------------------------ | ----------------------- |
| All 9 subsystems have a home in the project tree       | ✅                       |
| Core project has zero external dependencies            | ✅ (pure DDD)            |
| Infrastructure encapsulates all external service calls | ✅ (Git, CodeGraph, LLM) |
| Feature-folders in Web match page structure            | ✅                       |
| Tests mirror source structure                          | ✅                       |

### Requirements Coverage Validation ✅

**FR Category Coverage:**

| FR Category                   | Architecturally Supported? | Location                                                      |
| ----------------------------- | -------------------------- | ------------------------------------------------------------- |
| FR-1 (Auth)                   | ✅                          | ASP.NET Core Identity + roles, Auth pages                     |
| FR-2 (System CRUD)            | ✅                          | Systems pages + ISystemRepository                             |
| FR-3 (Repo CRUD)              | ✅                          | Repositories pages + IRepositoryRepository                    |
| FR-4 (Scan Pipeline)          | ✅                          | BackgroundService + deterministic Orchestrator + Worker Agent executor |
| FR-5 (Cross-Repo Detection)   | ✅                          | Worker prompt injection + endpoint/dispatch tables + CrossRepoResolutionService |
| FR-6 (Uncertainty Resolution) | ✅                          | Uncertainty aggregate + deterministic Cross-Repository Resolution |
| FR-7 (HITL)                   | ✅                          | Uncertainty entity + HITL pages                               |
| FR-8 (Document Viewing)       | ✅                          | Documents pages + ViewModels                                  |
| FR-9 (Git Monitoring)         | ✅                          | GitCloneService (clone/pull at scan time)                     |
| FR-10 (Database Tools)        | ✅                          | Query services (recursive CTE, graph traversal)               |
| FR-11 (LLM Provider Config)   | ✅                          | SettingsPage + LLMProviderManager                              |
| FR-12 (Dashboard & Graph)     | ✅                          | ScanHub + GraphHub + management pages                         |
| FR-13 (MCP Integration)       | ⚠️ Deferred                | Documented as V1 deferral; architecture supports adding later |
| FR-14 (User-Supplied Context) | ✅                          | Pages + Core context injection                                |
| FR-15 (LSP Support)           | ⚠️ Deferred                | Documented as V1 deferral                                     |

**NFR Coverage:**

| NFR                                         | Status                                                       |
| ------------------------------------------- | ------------------------------------------------------------ |
| NFR-4.1 (Docker deployment)                 | ✅ docker-compose, two containers, single `docker-compose up` |
| Security (encrypted secrets at rest)        | ✅ ASP.NET Core Data Protection for API keys                  |
| Security (read-only DB access)              | ✅ Role-based authorization                                   |
| Reliability (worker retry)                  | ✅ Agent task state machine, one retry + fallback             |
| Reliability (graceful API degradation)      | ✅ LLMProviderManager failover                                |
| Reliability (scan state persistence)        | ✅ PostgreSQL + EF Core, ScanRun status tracking              |
| Observability (log.md + structured logging) | ✅ ILogger<T> with decision rules                             |

### Implementation Readiness Validation ✅

**Decision Completeness:**

| Dimension                                   | Status | Evidence                                          |
| ------------------------------------------- | ------ | ------------------------------------------------- |
| Critical decisions documented with versions | ✅      | Technology stack table with versions              |
| Implementation patterns comprehensive       | ✅      | 28 conflict points + 6 additional patterns        |
| Consistency rules clear and enforceable     | ✅      | .editorconfig + Roslyn analyzers + checklist      |
| Examples for major patterns                 | ✅      | Repository, ViewModel, DomainEvent, BackgroundJob |
| Project structure complete                  | ✅      | Full tree with \~150 files/directories            |
| Integration points specified                | ✅      | Internal + external + data flow diagram           |
| Component boundaries defined                | ✅      | Project reference rules + service boundaries      |
| FR-to-structure mapping                     | ✅      | 15 FR categories mapped to locations              |
| Anti-patterns documented                    | ✅      | 10 concrete anti-patterns with corrections        |

### Gap Analysis Results

**Critical Gaps:** None — all blocking architectural decisions are made.

**Important Gaps:**

| Gap                                 | Priority | Mitigation                                                                                                          |
| ----------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------- |
| MCP integration (FR-13)             | Deferred | Documented as V1 deferral; architecture supports adding later via Infrastructure project                            |
| LSP support (FR-15)                 | Deferred | Documented as V1 deferral                                                                                           |
| Worker Agent execution adapter not yet validated | High     | Validation spike required; fallback adapter can use direct OpenAI SDK or custom worker execution without changing deterministic Orchestrator |
| Prompt Workbench not designed       | Medium   | Prompts are externalized as embedded resources; iteration tooling TBD during implementation                         |

**Nice-to-Have Gaps:**

| Gap                               | Notes                                                                                 |
| --------------------------------- | ------------------------------------------------------------------------------------- |
| OpenTelemetry/distributed tracing | Structured logging sufficient for V1; add when multi-agent debugging becomes complex  |
| Caching strategy                  | Implicit via repository pattern; explicit cache layer not needed at V1 scale          |
| Health check endpoint             | Would help Docker orchestration; not blocking for docker-compose demo                 |
| CI/CD pipeline                    | `dotnet build` + `dotnet test` + `dotnet format` defined; GitHub Actions workflow TBD |

### Architecture Completeness Checklist

**Requirements Analysis**

- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped

**Architectural Decisions**

- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed

**Implementation Patterns**

- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented

**Project Structure**

- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION

**Confidence Level:** High — all 16 checklist items confirmed, no critical gaps, deferred features explicitly documented with architectural support paths.

**Key Strengths:**

- Clean DDD separation with zero-dependency Core project enables future storage migration
- Persistence Abstraction layer explicitly designed for PostgreSQL with migration path to other storage
- PostgreSQL 17 provides production-grade MVCC concurrency — no single-writer limitation
- docker-compose deployment is a single command (`docker-compose up`) despite two containers
- Comprehensive implementation patterns (34 total) prevent AI agent divergence
- Machine-enforceable conventions via `.editorconfig` + Roslyn analyzers
- BackgroundService + Channel<T> pattern keeps agent work off the request thread
- Domain events dispatched after commit prevents nested write contention
- Diff-based graph updates + viewport preservation prevents the most common graph UX failure

**Areas for Future Enhancement:**

- Worker Agent execution adapter spike may require swapping the adapter implementation, but not changing deterministic Orchestrator responsibilities
- OpenTelemetry for multi-agent observability post-V1
- Redis backplane for SignalR if horizontal scaling of app tier needed
- MCP integration when tool ecosystem matures

### Implementation Handoff

**AI Agent Guidelines:**

- Follow all architectural decisions exactly as documented in this file
- Use implementation patterns consistently across all components
- Respect project structure and boundaries — files go in their designated projects
- Refer to this document as the single source of truth for all architectural questions
- Run `dotnet format` before committing to enforce `.editorconfig` conventions
- Consult the anti-patterns section when unsure about an approach

**First Implementation Priority (from subsystem analysis):**

1. SP-7 (Document Graph Store) — architectural keystone, schema must stabilize first
2. SP-1 (Web Application Shell) — Blazor host, layout, auth, navigation
3. SP-2 (System & Repository CRUD) — foundational data entry
4. SP-3 (Scan Coordinator) — non-LLM pipeline infrastructure
5. SP-4 + SP-5 (Orchestrator + Worker Agent execution) — Orchestrator can start from the July contracts; Worker execution proceeds after adapter validation
6. SP-8 + SP-9 (Chat + Dashboard/Graph) — pure consumers, build last with real data
