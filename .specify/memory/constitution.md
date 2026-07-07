<!--
Sync Impact Report
==================
Version change: 0.0.0 (uninitialized template) → 1.0.0
Bump rationale: MINOR — initial ratification. The file was the unfilled
constitution-template.md with bracketed placeholders. Filling all
placeholders with concrete project-derived content is a material
addition of governance, so the initial ratified version is 1.0.0.

Modified principles (old → new):
  - [PRINCIPLE_1_NAME] → I. Layered Project Boundaries
  - [PRINCIPLE_2_NAME] → II. Domain Purity & Ubiquitous Language
  - [PRINCIPLE_3_NAME] → III. Validation & API Contract Discipline
  - [PRINCIPLE_4_NAME] → IV. Test-First Integration Harness
  - [PRINCIPLE_5_NAME] → V. Security & Data Stewardship

Added sections:
  - "Additional Constraints" (was [SECTION_2_NAME])
  - "Development Workflow & Quality Gates" (was [SECTION_3_NAME])
  - Governance section filled

Removed sections: none

Templates requiring updates:
  - .specify/templates/plan-template.md — ✅ no change needed; its
    "Constitution Check" gate is generic ("Gates determined based on
    constitution file") and resolves against this file at plan time.
  - .specify/templates/spec-template.md — ✅ no change needed; spec
    template is technology-agnostic by design and does not reference
    constitution principles by name.
  - .specify/templates/tasks-template.md — ✅ no change needed; task
    phases (Setup / Foundational / User Story / Polish) already align
    with Principle IV's integration-test-first discipline.
  - .specify/templates/commands/*.md — N/A (no commands folder exists).

Follow-up TODOs: none. All placeholders resolved.
-->

# Vulgata Constitution

## Core Principles

### I. Layered Project Boundaries

Vulgata is a multi-project .NET 10 solution. Each project has a single,
enforced responsibility, and dependencies flow in one direction:
Shared → Core ← Infrastructure ← Web ← Tests.

- **Vulgata.Core** holds domain entities (`Entities/`) and domain service
  interfaces (`DomainServices/`) only. It MUST NOT reference
  `Microsoft.EntityFrameworkCore.*` or any infrastructure package.
- **Vulgata.Infrastructure** owns `VulgataDbContext`, `ApplicationDbContext`,
  EF migrations, `Data/Configurations/` (one `IEntityTypeConfiguration<T>` per
  aggregate), repository implementations, and `Git/`. It is the only project
  permitted to touch EF Core or the database.
- **Vulgata.Shared** holds request/response DTOs, FluentValidation validators,
  `LoadState`, `AuthorizationPolicyNames`, and `RoleNames`. It is referenced by
  Web and Tests but MUST NOT depend on Infrastructure or Web.
- **Vulgata.Web** is the Blazor host: `Program.cs` (DI + middleware + startup
  migration), `Components/`, `Api/` (minimal API groups), `Data/`, `Validators/`.
- **Vulgata.Agents** holds agent types and `Prompts/` as embedded resources.

Rationale: the layering prevents domain logic from leaking into UI/infra code
and keeps the domain testable in isolation. Violations are caught at compile
time via project references and MUST be fixed, not worked around.

### II. Domain Purity & Ubiquitous Language

Domain entities and documents are the source of truth; the database is a
persistence detail.

- **No data annotations** on domain entities. All EF mapping lives in
  `IEntityTypeConfiguration<T>` classes under
  `Infrastructure/Data/Configurations/`, registered by assembly scan.
- **No generic `IRepository<T>`.** Each aggregate root gets a specific
  repository interface in `Core/DomainServices/` with a concrete
  implementation in `Infrastructure/Data/`.
- **Never expose `IQueryable` or `DbSet`** outside Infrastructure. Repositories
  return materialized results or tracked entities only.
- **Documents are immutable.** The only mutation path is code change → re-scan
  → regenerate. Previous versions are preserved via `superseded_by_id`
  (Version Chain) and are never deleted.
- **Ubiquitous language is authoritative.** `docs/UBIQUITOUS_LANGUAGE.md`
  defines canonical terms (System, Repository, Code Unit, Document, Edge,
  Uncertainty, Cross-Repository Resolution, etc.). Output MUST use these terms
  verbatim and MUST NOT drift to deprecated synonyms (e.g. "Cross-System" →
  use "Cross-Repository"). If a needed concept is absent from the glossary,
  surface the gap rather than inventing language.

Rationale: a stable, code-free domain model keeps the business logic
inspectable by non-technical staff and keeps the wiki generation pipeline
deterministic.

### III. Validation & API Contract Discipline

All input is validated; all API errors are structured; all UI is Chinese.

- **FluentValidation** is the sole input validation mechanism. Validators are
  registered via `AddValidatorsFromAssemblyContaining<T>()` and Blazor forms
  use the FluentValidation validator component. Data annotations are not used
  for domain validation.
- **Minimal APIs** live under `/api/` with plural nouns, grouped in
  `Web/Api/<area>/` and registered via `MapVulgataApiEndpoints()`. Errors use
  `ProblemDetails` — no wrapping envelope. API requests are excluded from
  status-code-page rewrites and cookie redirects (see `IsApiRequest` in
  `Program.cs`) so auth failures return proper 401/403.
- **Chinese-only UI (Simplified)** for V1 — no i18n. Identity error messages
  use `ChineseIdentityErrorDescriber`. DataAnnotations that must render
  Chinese need explicit `ErrorMessage` text (see repo memory
  `vulgata-identity-localization`).
- **Naming:** `Async` suffix mandatory on async methods. `_camelCase` for
  private fields. PascalCase for DB tables/columns. Domain events and SignalR
  hub methods use past tense.

Rationale: a single validation + error contract keeps the API machine-readable
and the UI consistent for the target audience.

### IV. Test-First Integration Harness

Tests are integration-first and mirror the source structure.

- Single test project `tests/Vulgata.Tests/` mirrors `src/dotnet/` layout.
  xunit + `WebApplicationFactory<Program>` integration tests (requires
  `public partial class Program` in `Program.cs`, SQLite overrides for both
  DbContexts).
- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}`.
- **Known harness pitfalls MUST be respected** (see repo memory
  `vulgata-test-harness`): post Blazor SSR forms with all rendered hidden
  inputs (not just the antiforgery token); target forms by `action`/`formname`
  because the logout form precedes page forms; assert AccessDenied redirects
  with a path prefix; reflection-driven component harnesses MUST set every
  `@inject`; avoid `OrderBy` on `DateTimeOffset` under SQLite (use
  `FirstOrDefault`/client-side ordering); `UseStatusCodePagesWithReExecute`
  can mutate API status codes — keep it off `/api` paths.
- New features SHOULD add or update an integration test that exercises the
  happy path and at least one authorization boundary.

Rationale: the Blazor SSR + Identity + two-DbContext stack has well-documented
test harness traps; encoding them as non-negotiable rules prevents repeated
regressions.

### V. Security & Data Stewardship

Secrets are encrypted; source never leaves the org; DB tools are read-only.

- **Passwords** are hashed with bcrypt (`BcryptPasswordHasher<ApplicationUser>`).
- **API keys** (LLM providers, DB connections) are encrypted at rest via
  ASP.NET Core Data Protection (`ApiKeyEncryptionService`,
  `DatabaseConnectionEncryptionService`).
- **Three roles** are seeded at startup — Administrator, SystemOwner, User
  (`Vulgata.Shared/RoleNames.cs`). Policies: `AdministratorOnly`,
  `ManagementAccess` (`AuthorizationPolicyNames.cs`).
- **DB connection tools are read-only / non-production only** (NFR-2.4). Write
  operations on connected databases MUST be blocked.
- **Source code and generated documents MUST never leave the organization's
  infrastructure** when using a self-hosted LLM provider (NFR-2.3).
- **Agent prompts** are externalized as embedded resources in
  `Vulgata.Agents/Prompts/`, never hardcoded strings. The Communication
  Pattern Catalog is a documented format, not code.

Rationale: Vulgata ingests proprietary source code and database schemas;
these constraints are regulatory, not stylistic.

## Additional Constraints

**Technology stack (fixed for V1):**

- .NET 10 (`net10.0`), Blazor Web App (interactive server render mode),
  ASP.NET Core Identity (cookie auth, individual accounts).
- PostgreSQL 17 via EF Core + Npgsql. Two `DbContext`s sharing one database:
  `ApplicationDbContext` (schema `identity`, separate migration history table
  `__IdentityMigrationsHistory`) and `VulgataDbContext` (schema `vulgata`).
  Migrations run at startup via `MigrateAsync()`; the app fails fast if
  migration/seeding fails.
- Microsoft Fluent UI Blazor (primary components), Z.Blazor.Diagrams (graph
  viz, planned), CommunityToolkit.Mvvm (MVVM source generators).
- SignalR hubs `ScanHub` / `GraphHub` (planned), all `[Authorize]`.
- Microsoft agent framework with Magentic orchestration (planned); OpenAI SDK
  against OpenAI-compatible endpoints with multi-provider failover.
- Deployment: single Docker container + PostgreSQL via `docker-compose.yml`
  (see `docker/Dockerfile`).

**Solution-wide build settings** (`Directory.Build.props`): `Nullable=enable`,
`ImplicitUsings=enable`, `AnalysisMode=AllEnabledByDefault`,
`EnforceCodeStyleInBuild=true`. Central package versioning via
`Directory.Packages.props`.

**UI theme:** "Library Oak" — primary `#445E7A` (light) / `#6A83A2` (dark),
accent `#B98B6B` / `#D4A88C`. Business mode locked to light, IT mode locked to
dark. Display text in Noto Serif SC. See
`docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/`.

**Information architecture:** two top-level routes — 对话 (chat, default
landing) and 管理后台 (management, holy-grail layout). Chat-first: login
lands in chat, not a dashboard.

**ViewModels:** every data-loading ViewModel exposes a `LoadState`
(`Idle|Loading|Loaded|Refreshing|Empty|NoResults|Error|Cancelling`) — see
`Vulgata.Shared/LoadState.cs`.

## Development Workflow & Quality Gates

**Build, run, test commands:**

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet run --project src/dotnet/Vulgata.Web/Vulgata.Web.csproj
docker compose up --build
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
dotnet ef migrations add <Name> --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
```

**Quality gates (every change):**

- `dotnet build` MUST succeed with `AnalysisMode=AllEnabledByDefault` and
  `EnforceCodeStyleInBuild=true` — analyzer warnings are errors in practice.
- `dotnet test` MUST pass. New features SHOULD add a covering integration test.
- EF migrations: add from the Infrastructure project with the Web project as
  startup. Migrations are applied automatically at startup; never require
  manual `database update` in normal dev flow.
- Code review MUST verify: project boundary compliance (Principle I), no data
  annotations on entities (Principle II), FluentValidation used for input
  (Principle III), integration test added/updated (Principle IV), secrets
  encrypted and DB tools read-only (Principle V).

**Domain docs & decisions:**

- `docs/UBIQUITOUS_LANGUAGE.md` is authoritative for terminology.
- `docs/adr/` (system-wide) and `src/<context>/docs/adr/` (context-scoped)
  hold ADRs. If a change contradicts an ADR, surface it explicitly rather than
  silently overriding.
- `CONTEXT-MAP.md` at repo root points at one `CONTEXT.md` per context (see
  `docs/agents/domain.md`).

**Issue tracker:** GitHub Issues on `ZHdpt/vulgata`. Triage labels:
`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`
(see `docs/agents/triage-labels.md`).

## Governance

This constitution is the highest-authority development guidance for Vulgata.
It supersedes conflicting ad-hoc practices but does not override the
ubiquitous language (`docs/UBIQUITOUS_LANGUAGE.md`) or ADRs (`docs/adr/`) —
those are peer authorities and conflicts MUST be surfaced explicitly.

**Amendment procedure:**

1. Propose an amendment as a PR that edits `.specify/memory/constitution.md`.
2. The PR MUST include: the old text, the new text, the rationale, and a
   migration plan for any in-flight work that the amendment affects.
3. Amendment requires review by at least one Administrator-role maintainer.
4. On merge, update `LAST_AMENDED_DATE` and bump `CONSTITUTION_VERSION`
   per semantic versioning:
   - **MAJOR**: backward-incompatible governance change or principle
     removal/redefinition.
   - **MINOR**: new principle or materially expanded guidance.
   - **PATCH**: clarification, wording, typo, non-semantic refinement.

**Versioning policy:** `MAJOR.MINOR.PATCH` (semantic versioning). The version
line at the bottom of this file is the single source of truth for the current
constitution version.

**Compliance review:** every PR MUST self-certify compliance with the five
Core Principles. Reviewers MAY block a PR for any principle violation. When a
principle is intentionally violated for a justified reason, the PR MUST record
the violation, the justification, and the rejected simpler alternative in the
plan's Complexity Tracking table (see `.specify/templates/plan-template.md`).

**Runtime development guidance:** see `AGENTS.md` at the repo root for
build/run/test commands, project boundaries, coding rules, UI conventions,
and testing discipline. This constitution defines the non-negotiable rules;
`AGENTS.md` provides the day-to-day operating instructions.

**Version**: 1.0.0 | **Ratified**: 2026-07-07 | **Last Amended**: 2026-07-07
