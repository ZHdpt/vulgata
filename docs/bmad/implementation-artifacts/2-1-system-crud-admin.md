---
story_key: 2-1-system-crud-admin
title: Story 2.1: System CRUD Admin
Status: in-progress
Epic: 2
Story: 1
created: 2026-06-29
baseline_commit: 2bf8c0d2b71a3bdd6f37a736f846b916005d4bed
depends_on:
  - 1-5-role-seeding-and-authorization-policies
  - 1-6-administrator-role-assignment
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md
  - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md
  - docs/bmad/implementation-artifacts/1-6-administrator-role-assignment.md
  - docs/bmad/implementation-artifacts/epic-1-retrospective-2026-06-29.md
---

# Story 2.1: System CRUD Admin

## Story

As an **administrator**,
I want to create, edit, and delete Systems,
so that I can organize repositories into logical groupings before delegating ownership.

## Scope Note

Epic 2 FR-2.1 describes System Owners managing Systems, but the current epic breakdown defines Story 2.1 as the administrator-led CRUD slice. Follow the epic story scope for this artifact:

- Story 2.1: administrator creates, edits, and deletes Systems
- Story 2.2: administrator assigns System Owners to Systems
- Story 2.1 may include read-only filtering for System Owners when assignment data exists, but it must not expand into ownership-management UI

## Acceptance Criteria

### AC-1: 管理后台显示系统树与系统详情

**Given** I am logged in as an `Administrator`
**When** I navigate to `管理后台 -> 系统管理`
**Then** I shall see the system tree view in the left sidebar using Fluent UI patterns
**And** the main content area shall show the selected system's detail and summary data

### AC-2: 新建系统

**Given** I am on `管理后台 -> 系统管理`
**When** I click `+ 新建系统` and submit a valid name with optional description and optional supplementary context
**Then** the System shall be created successfully
**And** it shall appear in the system tree immediately
**And** the UI confirmation and labels shall be in Simplified Chinese

### AC-3: 编辑系统

**Given** a System exists
**When** I open the inline edit dialog and update its name, description, or supplementary context
**Then** the System shall be updated successfully
**And** the system tree and detail panel shall refresh immediately

### AC-4: 删除空系统

**Given** a System exists with no repositories and no assigned System Owners
**When** I choose `删除` and confirm the action
**Then** the System shall be removed from persistence
**And** it shall disappear from the system tree immediately

### AC-5: 阻止删除非空系统

**Given** a System has repositories or assigned System Owners
**When** I attempt to delete it
**Then** the operation shall be rejected
**And** I shall see a Chinese validation/error message explaining that dependent data must be removed first

### AC-6: 唯一名称校验

**Given** I attempt to create or rename a System to a name that already exists
**When** I submit the dialog
**Then** the request shall fail validation
**And** the UI shall show a Chinese validation message next to the form

### AC-7: SystemOwner 只看到获授权系统

**Given** I am logged in as a `SystemOwner`
**When** I view `管理后台 -> 系统管理`
**Then** I shall only see Systems assigned to me
**And** I shall not see the `+ 新建系统` action

### AC-8: 普通用户无管理入口

**Given** I am logged in as a regular `User`
**When** I view the application shell
**Then** the `管理后台` navigation entry shall not be visible

## Functional Requirements Extracted for This Story

- FR-2.1: create, edit, and delete Systems
- A System includes `Name`, `Description`, and optional supplementary `Context`
- Management UI lives under `管理后台` and uses the holy grail layout already scaffolded in the web project
- UI interactions use inline dialogs rather than separate create/edit pages
- System tree and detail panel must update immediately after CRUD operations
- Unique-name validation must exist at both validation and persistence boundaries
- Delete must respect future dependency rules for repositories and SystemOwner assignments

## Non-Functional and Cross-Cutting Requirements

- Chinese-only UI (Simplified Chinese) for all labels, placeholders, buttons, dialogs, and validation messages
- Fluent UI Blazor components for forms, buttons, dialogs, tree view, cards, and data presentation
- Minimal API endpoints under `/api/` with ProblemDetails error responses and no custom response envelope
- Repository pattern only: `ISystemRepository` in Core, `SystemRepository` in Infrastructure; do not expose `DbSet` or `IQueryable`
- EF Core configuration via `IEntityTypeConfiguration<System>` in `Configurations/`
- FluentValidation for request/input validation; avoid data annotations on domain entities
- Structured logging with `ILogger<T>` for CRUD operations and rejection paths
- Authorization must extend existing `ManagementAccess` and `AdministratorOnly` policy hooks instead of creating ad hoc checks
- ViewModels that load data should expose `LoadState` per the architecture and UX state model

## Domain Model Details

### Aggregate Root: System

Required domain shape for this story:

- `System` is an aggregate root in `Vulgata.Core`
- Required property: `Name`
- Optional properties: `Description`, `Context`
- Persistence key: add an explicit identity key suitable for EF Core and API transport
- Name must be unique after normalization/trim

### Relationships

- `System` will later own many `Repository` records (Story 2.3)
- `System` will later have SystemOwner assignments (Story 2.2)
- Story 2.1 should model deletion through an invariant-aware repository/service path so the later owner-assignment rule does not require UI rewrites

### Recommended Persistence Invariants

- Trim and normalize `Name` before uniqueness checks
- Reject blank or whitespace-only `Name`
- Store `Description` and `Context` as plain text, not markdown-specific content
- Enforce unique system name in the database, not only in UI validation

## UX Requirements Applied

### Management Surface

- Reuse `ManagementLayout.razor` holy grail shell
- Left sidebar becomes the real system tree (`FluentTreeView`) instead of placeholder text
- Main content becomes a system detail view with Chinese heading, summary cards, and repository placeholder counts
- `+ 新建系统` stays in the left sidebar per UX-DR-10

### Dialog Behavior

- Use inline Fluent dialogs for create/edit
- Use a confirmation dialog for delete
- Keep copy Chinese-only, concise, and neutral
- Show validation near the relevant field, not only as a global error

### Empty and Loading States

- No systems: `尚未添加任何系统。创建你的第一个系统开始扫描。`
- Loading: Fluent shimmer or equivalent skeleton pattern
- Error: neutral Chinese message plus retry action

## Technical Implementation Plan

### Core Domain

Create or update the following files:

- `src/dotnet/Vulgata.Core/Entities/System.cs`
- `src/dotnet/Vulgata.Core/DomainServices/ISystemRepository.cs`

Implementation expectations:

- Define the `System` aggregate root with `Name`, `Description`, and `Context`
- Keep the entity free of UI concerns and data annotations
- Shape repository methods around story use cases, for example: list visible systems, get by id, add, update, delete, name-exists, and dependency-aware delete guard

### Shared Contracts and Validation

Create or update the following files:

- `src/dotnet/Vulgata.Shared/Systems/SystemSummaryDto.cs`
- `src/dotnet/Vulgata.Shared/Systems/SystemDetailDto.cs`
- `src/dotnet/Vulgata.Shared/Systems/CreateSystemRequest.cs`
- `src/dotnet/Vulgata.Shared/Systems/UpdateSystemRequest.cs`
- `src/dotnet/Vulgata.Shared/Systems/DeleteSystemRequest.cs`
- `src/dotnet/Vulgata.Shared/Validators/Systems/CreateSystemRequestValidator.cs`
- `src/dotnet/Vulgata.Shared/Validators/Systems/UpdateSystemRequestValidator.cs`

Implementation expectations:

- Keep request/response contracts in Shared for Web and future consumers
- Register FluentValidation through assembly scanning already prescribed by the architecture
- Validation messages shown to users must be Chinese

### Infrastructure

Create or update the following files:

- `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SystemConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Repositories/SystemRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/` (new domain migration for `System`)

Implementation expectations:

- Add a `DbSet<System>` to `VulgataDbContext`
- Configure schema details in `SystemConfiguration` only
- Use a unique index/constraint for normalized system name
- Keep repository implementation persistence-focused and free of page logic

### Web and API Surface

Create or update the following files:

- `src/dotnet/Vulgata.Web/Program.cs`
- `src/dotnet/Vulgata.Web/Endpoints/SystemEndpoints.cs`
- `src/dotnet/Vulgata.Web/Components/Layout/ManagementLayout.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/CreateSystemDialog.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/EditSystemDialog.razor`
- `src/dotnet/Vulgata.Web/Components/Pages/Management/DeleteSystemDialog.razor`

Implementation expectations:

- Map minimal APIs under `/api/systems`
- Use ProblemDetails for duplicate-name, not-found, forbidden, and delete-blocked failures
- Wire the dashboard page to list systems and select one for detail display
- Remove the current placeholder text in `ManagementLayout.razor` and `DashboardPage.razor`
- Keep `管理后台` hidden from regular users in the existing shell

### ViewModels

Create or update the following files:

- `src/dotnet/Vulgata.Web.ViewModels/Management/SystemsPageViewModel.cs`

Implementation expectations:

- Expose `LoadState`
- Encapsulate API calls, selected-system state, and dialog command state
- Keep Razor pages thin where practical

## Validation Rules

- `Name` is required
- `Name` must not be whitespace-only after trimming
- `Name` must be unique across Systems after normalization
- `Description` is optional
- `Context` is optional supplementary plain text
- Delete is blocked when repositories or SystemOwner assignments exist
- Authorization is enforced server-side; hidden buttons are not sufficient

## Test Plan

### New Test File

- `tests/Vulgata.Tests/SystemCrudAdminTests.cs`

### Expected Test Coverage

1. `Administrator` can open `管理后台 -> 系统管理` and see Chinese system-management UI
2. `Administrator` can create a system and the created system appears in the rendered tree/detail surface
3. Duplicate system name is rejected with a Chinese validation or ProblemDetails message
4. `Administrator` can edit system name/description/context
5. `Administrator` can delete an empty system
6. Delete is blocked when dependent repositories or owner assignments exist
7. `SystemOwner` only sees assigned systems and does not see `+ 新建系统`
8. Regular `User` does not see the `管理后台` nav entry
9. Minimal API routes enforce authorization and return ProblemDetails on error paths

### Test Pattern Notes from Epic 1

- Prefer `WebApplicationFactory<Program>` integration tests for auth-sensitive flows
- Reuse hidden-input/form-handler extraction patterns from Story 1.6 for Blazor SSR forms
- Assert AccessDenied redirects with a prefix because `ReturnUrl` may be appended
- Seed roles and assignment data through service scope helpers rather than brittle HTML setup

## Dev Notes

### CRITICAL: What Already Exists — Do NOT Rebuild

- `ManagementLayout.razor` already defines the top navbar, management tabs, and placeholder left sidebar
- `DashboardPage.razor` already exists as the `/management` route and is authorized via `ManagementAccess`
- `SettingsPage.razor` already hides administrator-only settings entries correctly
- `Program.cs` already wires PostgreSQL, Identity, policies, role seeding, and ProblemDetails
- Story 1.6 already established the pattern for Chinese management UI plus integration-test form handling

### CRITICAL: Scope Boundary

Do not pull Story 2.2 ownership-management UI into this story. If owner-based visibility is exercised in tests, seed assignment data directly or scaffold only the minimum persistence boundary needed for filtering.

### CRITICAL: Concurrency and Integrity

Epic 1 retrospective showed that check-then-act mutations are easy to get wrong. Apply that lesson here:

- uniqueness must be protected by the database as well as validation
- delete checks must run server-side against current state
- avoid UI-only assumptions for permission or dependency checks

### CRITICAL: Management Auth Hook

Extend the existing policy-based authorization model. Do not introduce ad hoc role checks scattered through pages and endpoints when the existing requirement/handler path can carry Epic 2 scoping.

### Recommended Sequence

1. Add domain entity, repository contract, EF configuration, and migration
2. Add DTOs, validators, and minimal API endpoints
3. Wire dashboard/tree/detail UI and dialogs
4. Add focused integration tests for CRUD, authorization, and delete-guard behavior

## References to Follow

- Story pattern reference: `docs/bmad/implementation-artifacts/1-6-administrator-role-assignment.md`
- Retrospective lessons: `docs/bmad/implementation-artifacts/epic-1-retrospective-2026-06-29.md`
- Architecture rules: repository pattern, FluentValidation, Minimal API, ProblemDetails, EF configuration in `Configurations/`
- UX rules: holy grail management layout, inline dialogs, Chinese-only labels, Fluent UI discipline