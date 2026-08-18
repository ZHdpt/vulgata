# Vulgata

Vulgata is an LLM-powered, cross-repository/cross-system codebase knowledge base generator. It scans source code with multi-agent orchestration, produces structured **Code Logic Documents** and **Business Logic Documents** linked to the source, resolves cross-repo references (with uncertainties when the target is unscanned), and exposes a chat interface (Business / IT modes) over the generated wiki. Target users include non-technical staff who need to understand business logic spread across many systems and repositories (e.g. a bank's system portfolio).

- **Goal:** extract and explain business logic from source code across systems and repositories.
- **Key concepts:** System → Repository → Code Unit → Document (CL/BL) → Edge (REFERENCES / GENERATED_FROM) → Uncertainty → Cross-Repository Resolution. See `docs/UBIQUITOUS_LANGUAGE.md` for the canonical glossary — use these terms verbatim in output.
- **Status:** V1 in progress. Identity, system/repo management, LLM provider config, database connections, and user-supplied context are implemented; the scanning pipeline, agent orchestration, graph visualization, and chat are planned. See `docs/bmad/epics.md` for the epic breakdown and `docs/requirement-draft.md` for the original vision.

## Tech stack

- **.NET 10** (`net10.0`), Blazor Web App (interactive server render mode), ASP.NET Core Identity (cookie auth, individual accounts).
- **PostgreSQL 17** via EF Core + Npgsql. Two `DbContext`s sharing one database: `ApplicationDbContext` (Identity, schema `identity`) and `VulgataDbContext` (domain, schema `vulgata`). Migrations run at startup via `MigrateAsync()`.
- **UI:** Microsoft Fluent UI Blazor (primary components), Z.Blazor.Diagrams (graph viz, planned), CommunityToolkit.Mvvm (MVVM source generators).
- **Validation:** FluentValidation (no data annotations on domain entities).
- **Real-time (planned):** SignalR hubs (`ScanHub`, `GraphHub`), all `[Authorize]`.
- **Agents (planned):** Microsoft agent framework with Magentic orchestration; OpenAI SDK against OpenAI-compatible endpoints (DeepSeek V4) with multi-provider failover.
- **Deployment:** single Docker container + PostgreSQL via `docker-compose.yml` (see `docker/Dockerfile`).

## Solution layout

```
Vulgata.slnx
├── src/dotnet/
│   ├── Vulgata.Core/          # Domain entities, domain services (interfaces), no EF/infra deps
│   ├── Vulgata.Infrastructure/# EF Core, DbContexts, migrations, repository implementations, git
│   ├── Vulgata.Shared/        # DTOs, validators, LoadState, authorization policy names, role names
│   ├── Vulgata.Agents/        # LLM agent types, prompts (embedded resources) — planned
│   ├── Vulgata.Web.ViewModels/# MVVM view models (CommunityToolkit.Mvvm) — placeholder
│   └── Vulgata.Web/           # Blazor host: Components/, Api/, Data/, Validators/, Program.cs
├── src/{java,python,node}/    # Placeholder dirs for future polyglot work
├── tests/Vulgata.Tests/       # xunit + WebApplicationFactory<Program> integration tests
├── docs/                      # Domain, agents, bmad artifacts, ubiquitous language
└── prompts/                   # Bind-mounted agent prompt iteration (see docker-compose.yml)
```

### Project boundaries (enforced)

- **Vulgata.Core:** entities (`Entities/`) and domain service interfaces (`DomainServices/`). No `Microsoft.EntityFrameworkCore.*` references.
- **Vulgata.Infrastructure:** `VulgataDbContext`, `ApplicationDbContext`, `Data/Configurations/` (one `IEntityTypeConfiguration<T>` per aggregate), repository implementations, `Git/`. Owns EF migrations.
- **Vulgata.Shared:** request/response DTOs, FluentValidation validators, `LoadState`, `AuthorizationPolicyNames`, `RoleNames`. Referenced by Web and Tests.
- **Vulgata.Web:** `Program.cs` (DI + middleware + startup migration), `Components/` (Razor), `Api/` (minimal API endpoint groups), `Data/` (Identity glue, `RoleSeeder`, password hasher), `Validators/`.
- **Vulgata.Agents:** agent types and `Prompts/` (embedded resources). Prompts are externalized text, never hardcoded strings.

## Build, run, test

```pwsh
# Build the web project (also available as the build-vulgata-web task)
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj

# Run locally (needs PostgreSQL on localhost:5432 — see appsettings.json default connection)
dotnet run --project src/dotnet/Vulgata.Web/Vulgata.Web.csproj

# Full Docker stack (Blazor app + PostgreSQL 17)
docker compose up --build

# Run all tests
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj

# Add an EF migration (from the Infrastructure project)
dotnet ef migrations add <Name> --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
```

Migrations are applied automatically at startup; the app fails fast if migration/seeding fails. Local dev without Docker is possible — just run a PostgreSQL container separately.

## Coding rules & disciplines

- **Nullable + implicit usings** enabled solution-wide via `Directory.Build.props`. `AnalysisMode=AllEnabledByDefault`, `EnforceCodeStyleInBuild=true`. Central package versioning via `Directory.Packages.props`.
- **Naming:** `Async` suffix mandatory on async methods. `_camelCase` for private fields. PascalCase for DB tables/columns. Minimal API endpoints under `/api/` with plural nouns. Domain events and SignalR hub methods use past tense.
- **Repository pattern:** specific repository per aggregate root (e.g. `ISystemRepository`, `IRepositoryRepository`). Interfaces in `Vulgata.Core/DomainServices/`, implementations in `Vulgata.Infrastructure/Data/`. **No generic `IRepository<T>`.** Never expose `IQueryable` or `DbSet` outside Infrastructure.
- **EF Core:** entity configuration via `IEntityTypeConfiguration<T>` in `Infrastructure/Data/Configurations/`, registered by assembly scan. **No data annotations** on entities. Domain `DbContext` uses schema `vulgata`; Identity `DbContext` uses schema `identity` with a separate migration history table.
- **Validation:** FluentValidation for all input. Validators registered via `AddValidatorsFromAssemblyContaining<T>()`. Blazor forms use the FluentValidation validator component.
- **API design:** minimal APIs under `/api/`, grouped in `Vulgata.Web/Api/<area>/` and registered via `MapVulgataApiEndpoints()`. `ProblemDetails` for errors — no wrapping envelope. API requests are excluded from status-code-page rewrites and cookie redirects (see `IsApiRequest` in `Program.cs`) so API auth failures return proper 401/403.
- **Authorization:** three roles seeded at startup — Administrator, SystemOwner, User (see `Vulgata.Shared/RoleNames.cs`). Policies: `AdministratorOnly`, `ManagementAccess` (see `AuthorizationPolicyNames.cs`). API keys (LLM providers, DB connections) encrypted at rest via ASP.NET Core Data Protection.
- **ViewModels:** every data-loading ViewModel exposes a `LoadState` (`Idle|Loading|Loaded|Refreshing|Empty|NoResults|Error|Cancelling`) — see `Vulgata.Shared/LoadState.cs`.
- **Security:** passwords hashed with bcrypt (`BcryptPasswordHasher<ApplicationUser>`). DB connection tools are read-only / non-production only (NFR-2.4). Source code and generated documents must never leave org infrastructure when using a self-hosted LLM provider.
- **Agent prompts:** externalized as embedded resources in `Vulgata.Agents/Prompts/`, never hardcoded. The Communication Pattern Catalog is a documented format, not code.
- **Documents (domain):** immutable — the only mutation path is code change → re-scan → regenerate. Previous versions preserved via `superseded_by_id` (Version Chain). Old versions are never deleted.

## Planned-change authorization

After an independent `plan-review` accepts a story plan and before task generation,
the orchestrator MUST pause for explicit user approval when the plan does any of the
following:

- adds, exposes, or materially changes a **Worker Agent** tool or other agent tool;
- adds or materially changes a **Scan** strategy, including parsing, filtering,
  scheduling, grouping, retry, or dispatch behavior; or
- changes, weakens, or conflicts with a requirement in the current PRD.

The approval request MUST identify the trigger, explain the proposed approach and
its effects, state the orchestrator's implementation intent, and cite the affected
plan and PRD material. It is approval to proceed from the accepted plan to task
generation; it does not waive any later analysis, TDD, review, E2E, security, or
verification gate. If none of these triggers applies, the ordinary workflow
advances without this additional approval.

## UI conventions

- **Language:** Chinese-only UI (Simplified) for V1 — no i18n. Identity error messages use `ChineseIdentityErrorDescriber`; DataAnnotations that must render Chinese need explicit `ErrorMessage` text (see repo memory `vulgata-identity-localization`).
- **Theme:** "Library Oak" — warm professional register. Primary `#445E7A` (light) / `#6A83A2` (dark), accent `#B98B6B` / `#D4A88C`. Business mode locked to light, IT mode locked to dark. Display text in Noto Serif SC. See `docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/`.
- **IA:** two top-level routes — 对话 (chat, default landing) and 管理后台 (management, holy-grail layout). Chat-first: login lands in chat.

## Testing discipline

- Single test project `tests/Vulgata.Tests/` mirroring source structure. xunit + `WebApplicationFactory<Program>` integration tests (requires `public partial class Program` in `Program.cs`, SQLite overrides for both DbContexts).
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`.
- Known harness pitfalls (see repo memory `vulgata-test-harness`): post Blazor SSR forms with all rendered hidden inputs (not just the antiforgery token); target forms by `action`/`formname` because the logout form precedes page forms; assert AccessDenied redirects with a path prefix; reflection-driven component harnesses must set every `@inject`; avoid `OrderBy` on `DateTimeOffset` under SQLite (use `FirstOrDefault`/client-side ordering); `UseStatusCodePagesWithReExecute` can mutate API status codes — keep it off `/api` paths.

## Domain docs & decisions

- **Ubiquitous language:** `docs/UBIQUITOUS_LANGUAGE.md` is authoritative. Use its terms verbatim; don't drift to synonyms the glossary avoids (e.g. "Cross-System" → "Cross-Repository").
- **ADRs:** `docs/adr/` (system-wide) and `src/<context>/docs/adr/` (context-scoped) when present. If your output contradicts an ADR, surface it explicitly rather than silently overriding.
- **Multi-context layout:** `CONTEXT-MAP.md` at repo root points at one `CONTEXT.md` per context. See `docs/agents/domain.md`.

## Agent skills

### Issue tracker

Issues live in GitHub Issues on `ZHdpt/vulgata`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default label vocabulary (needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix). See `docs/agents/triage-labels.md`.

### Domain docs

Multi-context layout via `CONTEXT-MAP.md`. See `docs/agents/domain.md`.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
`specs/008-demo-chat-release-gate/plan.md`
<!-- SPECKIT END -->
