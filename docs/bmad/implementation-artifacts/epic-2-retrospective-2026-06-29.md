---
date: 2026-06-29
epic: 2
status: done
title: "Epic 2 Retrospective: Domain Model & Management Backend"
project: vulgata
stories_completed: 5
---

# Epic 2 Retrospective: Domain Model & Management Backend

## Epic Summary

Epic 2 established the core domain model (Systems, Repositories, SystemOwnerAssignments) and the full management backend UI. All 5 stories completed. The management dashboard evolved from a placeholder page to a feature-complete administration surface supporting system CRUD, ownership delegation, repository management with git validation, standalone repositories, and user-supplied context at three levels.

| Story | Title | Status |
|-------|-------|--------|
| 2.1 | System CRUD Admin | done |
| 2.2 | Grant System Ownership | done |
| 2.3 | Repository Management | done |
| 2.4 | Standalone Repository Creation | done |
| 2.5 | User-Supplied Context | done |

### Requirements Satisfied

- **FR-2.1**: Create, edit, and delete Systems with name, description, and optional context
- **FR-2.2**: Add Repositories to a System
- **FR-2.3**: Remove Repositories from a System
- **FR-2.4**: Create standalone Repositories (not belonging to any System)
- **FR-2.5**: Validate git URL reachability when a Repository is added
- **FR-14.1**: Administrators supply context at the global level
- **FR-14.2**: System Owners supply context at the System level
- **FR-14.3**: System Owners supply context at the Repository level
- **FR-14.4**: Context changes during active Scan are queued (deferred enforcement, interface in place)

---

## What Went Well

### 1. Authorization Model Extended Cleanly

The `ManagementAccess` policy and `AdministratorOnly` policy established in Epic 1 extended seamlessly into Epic 2:

- **SystemOwner scoping**: Story 2.1 introduced `ListVisibleAsync` filtering by owner assignment; Story 2.2 completed the assignment lifecycle with `SystemOwnershipCoordinator` (role grant on assignment, role removal on last-assignment-loss, `User` role fallback).
- **Repository authorization**: Story 2.3's critical review fix — replacing `GetByIdAsync` with `GetVisibleByIdAsync` in all three repository endpoints — closed a privilege-escalation gap where any authenticated user could access any system's repositories.
- **Standalone visibility**: Story 2.4 explicitly expressed "all `ManagementAccess` users see standalone repos" in the coordinator, rather than relying on accidental null-foreign-key behavior.
- **Context authorization**: Story 2.5 extended the same pattern — `AdministratorOnly` for global context, system visibility for system context, repository visibility (including standalone) for repository context.

No new authorization mechanisms were created. The policy-based approach from Epic 1 carried the full load.

### 2. Coordinator Pattern Proliferated Successfully

Epic 1's `AdministratorRoleCoordinator` pattern (serialized mutations, Chinese error mapping, DI registration) was consciously replicated:

| Coordinator | Story | Purpose |
|---|---|---|
| `AdministratorRoleCoordinator` | 1.6 | Admin role grant/remove with last-admin guard |
| `SystemOwnershipCoordinator` | 2.2 | Owner assignment lifecycle with role sync |
| `RepositoryManagementCoordinator` | 2.3/2.4 | Repository creation with git validation, duplicate-name handling, DbUpdateException catch |
| Context coordinator | 2.5 | Multi-level context read/write with queuing abstraction |

Each coordinator centralizes cross-cutting mutation logic that would otherwise scatter across Razor pages and API endpoints. The pattern is now a team convention.

### 3. Git Validation with Credential Sanitization

Story 2.3 introduced `IGitRemoteValidationService` with `git ls-remote` as the first infrastructure service that shells out to an external process. Key design wins:

- **Credential sanitization**: `SanitizeSensitiveText()` strips embedded credentials from error messages before they reach the UI (AC-4: "认证要求错误不得泄露敏感信息").
- **Auth detection via keyword matching**: Distinguishes authentication failures from network failures without parsing git's unstructured output.
- **Interface abstraction**: `IGitRemoteValidationService` is replaceable — tests use a TCP listener (`UnauthorizedGitRemoteServer`) to simulate auth-required remotes.
- **Reuse in 2.4**: Standalone repository creation reused the exact same validation path with zero code duplication.

### 4. SQLite/PostgreSQL Dual-Database Testing Discipline

Every story maintained the dual-database test pattern (integration tests on SQLite, production on PostgreSQL). This surfaced real compatibility issues:

- **Story 2.2**: `DateTimeOffset` ORDER BY crashes on SQLite (fixed by removing server-side ordering)
- **Story 2.4**: `NULL` unique index semantics differ between SQLite and PostgreSQL (filtered partial unique index with `HasFilter`)

These were caught in tests, not in production. The SQLite test harness paid for itself multiple times.

### 5. Review-Driven Quality

Code reviews found 8 critical/high bugs across 5 stories, all fixed before completion:

| Story | Critical | High | Medium | Low |
|-------|----------|------|--------|-----|
| 2.1 | 1 (AsNoTracking silent update loss) | 6 (FluentTreeView, DTO location, ViewModel, sidebar, dialogs) | 4 | — |
| 2.2 | 1 (SQLite DateTimeOffset crash) | — | 2 (positional records, test order) | 3 |
| 2.3 | 3 (auth bypass, wrong API contract, missing UI) | — | — | 1 |
| 2.4 | 1 (FluentValidationValidator removal) | 1 (race condition) | — | 2 |

The most valuable catches:

- **2.1 AsNoTracking**: `GetByIdAsync` used `.AsNoTracking()` — all edits were silently discarded. Would have been discovered as "edit doesn't work" in manual testing, but review caught the root cause.
- **2.3 Auth bypass**: All three repository endpoints used existence-check-only lookup, allowing any authenticated user to manipulate any system's repositories. Review caught this architectural gap.
- **2.4 Race condition**: Concurrent standalone repo creation with same name would throw unhandled `DbUpdateException` → 500 error. Fixed with try/catch returning a clean Chinese validation message.

### 6. Incremental DashboardPage Evolution

`DashboardPage.razor` evolved across all 5 stories without regressions:

- 2.1: System list table + inline CRUD forms
- 2.2: "管理所有者" button + `ManageSystemOwnersDialog`
- 2.3: Per-system repository tables + `+ 新建仓库` + inline creation dialog
- 2.4: Standalone repository section before system list
- 2.5: Context display/edit integration

Each story added capability without breaking previous features. The Story 2.4 review explicitly verified: "Story 2.3 的系统内仓库接口和页面列表在本故事落地后仍保持正常."

### 7. Test Coverage

~35 integration tests across 5 test files:

| Test File | Coverage |
|-----------|----------|
| `SystemCrudTests.cs` | System CRUD, duplicate names, delete guards, authorization, tree/page visibility |
| `GrantSystemOwnershipTests.cs` | Owner assignment, duplicate rejection, candidate filtering, role sync, last-owner cleanup |
| `RepositoryManagementTests.cs` | System repo CRUD, git validation (reachable/unreachable/auth), auth bypass, admin visibility, standalone repo CRUD, standalone uniqueness |
| `UserSuppliedContextTests.cs` | Global/system/repo context CRUD, authorization, combination order, scan-queuing |
| `ArchitectureScaffoldingTests.cs` | Build verification, project structure |

Tests use `WebApplicationFactory<Program>` with SQLite overrides, maintaining the pattern established in Epic 1.

---

## What Could Be Improved

### 1. DashboardPage.razor Monolith

`DashboardPage.razor` grew into a single component handling: system tree, system CRUD, owner management dialog, per-system repository tables, repository CRUD, standalone repository section, context display/edit, and delete confirmations. The story specs called for separate components (`CreateSystemDialog.razor`, `EditSystemDialog.razor`, `DeleteSystemDialog.razor`, `CreateRepositoryDialog.razor`, `DeleteRepositoryDialog.razor`) but the implementation embedded most functionality inline.

**Impact**: The file is now the single largest Razor component in the project. Future stories (Epic 3 LLM provider config, Epic 4 scan dashboard) that extend the management surface will compound this unless extracted.

**Recommendation**: Before Epic 3, extract at minimum the standalone repository section and the context editing sections into dedicated components. The coordinator pattern is already in place — the extraction is primarily a refactoring of the Razor layer.

### 2. DTO and File Organization Drift

Story 2.1 placed `CreateSystemRequest` and `UpdateSystemRequest` in `Vulgata.Web.Validators/` instead of `Vulgata.Shared/Systems/` as specified. API endpoints stayed inline in `Program.cs` instead of `Endpoints/SystemEndpoints.cs`. Story 2.4 review noted this but accepted it as non-blocking.

**Impact**: These organizational deviations don't affect functionality but erode the codebase's navigability. New developers (or LLM agents) looking for DTOs in `Shared/` won't find them.

**Recommendation**: A dedicated cleanup story or the first story of Epic 3 should relocate misplaced files and extract endpoints to their specified locations. This is low-risk, high-clarity work.

### 3. Missing FluentTreeView

AC-1 of Story 2.1 specifies "system tree view in the left sidebar using Fluent UI patterns." The implementation uses a flat HTML `<table>` instead. The left sidebar in `ManagementLayout.razor` still shows placeholder text.

**Impact**: UX deviates from UX-DR-10 (Fluent TreeView with scan status dots). The table works functionally but doesn't provide the hierarchical tree navigation the UX spec calls for.

**Recommendation**: Implement `FluentTreeView` as part of Epic 3's management UI expansion, when the tree needs to display scan status dots (gray/green/red/pulsing) that the flat table cannot represent.

### 4. ViewModel Pattern Not Fully Exercised

`SystemsPageViewModel` was specified but not created. The `DashboardPage.razor` makes direct `ISystemRepository` and coordinator calls, bypassing the MVVM layer. The `Vulgata.Web.ViewModels` project remains a stub.

**Impact**: `LoadState` enum (Idle, Loading, Loaded, Refreshing, Empty, NoResults, Error) specified in the architecture is not used by the management dashboard. Loading states, error states, and empty states are handled ad-hoc in the Razor code-behind.

**Recommendation**: Epic 3's management UI (LLM provider configuration) should be the forcing function to adopt ViewModels with `LoadState`. Starting fresh on a new management tab is easier than retrofitting the existing DashboardPage.

### 5. Story 2.1 UX Deviations Accumulated

Six HIGH-level issues from Story 2.1's review remain unfixed:

1. No `FluentTreeView`
2. DTOs in wrong project
3. Missing `SystemsPageViewModel`
4. `+ 新建系统` not in left sidebar
5. No inline Fluent dialogs for create/edit
6. Endpoints inline in `Program.cs`

These were all judged "UX/structural deviations that should be addressed in a follow-up refinement story but do not block functionality." They remain open technical debt.

### 6. Pre-Existing Race Conditions Not Addressed

Story 2.4 review noted that the same `DbUpdateException` race condition (check-then-act on name uniqueness) exists in `RepositoryManagementCoordinator.CreateAsync` for system repositories. The fix was applied only to `CreateStandaloneAsync`. Epic 1's retrospective explicitly flagged check-then-act patterns as a lesson — the system repository path still has the gap.

**Recommendation**: Apply the same `try/catch (DbUpdateException)` pattern to `CreateAsync` in the first Epic 3 story that touches repository creation.

---

## Lessons Learned

### For Epic 3

1. **Extract components before adding features**: `DashboardPage.razor` is at its complexity ceiling. Epic 3's LLM provider configuration should start with a new management tab and dedicated components rather than adding to the existing monolith.

2. **The coordinator pattern is proven and should be the default**: Every coordinator introduced in Epic 2 (ownership, repository management, context) successfully centralized cross-cutting logic. Epic 3's provider configuration mutations (default provider changes, per-agent overrides, connection testing) should use the same pattern from the start.

3. **Auth bypass is the highest-value review catch**: Story 2.3's `GetByIdAsync` → `GetVisibleByIdAsync` fix prevented a privilege escalation vulnerability. Epic 3's provider configuration endpoints should be designed with the same visibility-filtering discipline from the start. Every API endpoint that serves scoped data should ask: "Does this use visibility-filtered lookup or existence-only lookup?"

4. **Git validation pattern is reusable**: `IGitRemoteValidationService` with sanitized error output and auth detection is a clean abstraction. Epic 3's LLM provider connection testing (FR-3.4) should follow the same pattern: interface in Core, implementation in Infrastructure, sanitized error messages, testability via mock/fake.

5. **Dual-database testing catches real issues**: Continue running integration tests against SQLite. The `DateTimeOffset` ORDER BY and `NULL` unique index issues would have been production bugs without it.

6. **File organization matters for agent navigability**: The DTO-in-wrong-project and endpoints-in-Program.cs drift makes the codebase harder for LLM agents (and humans) to navigate. Epic 3 should start with a cleanup pass.

### For Epic 4 (Scanning Pipeline)

1. **Context combination service is ready**: Story 2.5 established the `全局 → 系统 → 仓库` context combination contract. Epic 4's Worker Agents should consume this through the single entry point, not by directly reading database fields.

2. **Scan state abstraction is designed for replacement**: Story 2.5's scan-state query interface (`IScanStateService` or equivalent) was deliberately minimal — it only needs to answer "is there an active scan for scope X?" Epic 4 should replace the stub implementation with the real scan state from the Scan Coordinator.

3. **The queued-context mechanism is in place**: FR-14.4 (context changes during active scan are queued) has the persistence model and API contract. Epic 4's Scan Coordinator should flush queued context changes when a scan completes.

4. **Repository visibility rules are settled**: System repos use system ownership, standalone repos use `ManagementAccess`. Epic 4's scan dispatch must respect these same boundaries — don't create a parallel authorization system for scan operations.

---

## Architecture Adherence

| Decision | Status | Notes |
|----------|--------|-------|
| ASP.NET Core Identity + cookie auth | ✅ Maintained | Extended with SystemOwner role lifecycle |
| PostgreSQL 17 + EF Core + Npgsql | ✅ Maintained | Two DbContexts, separate schemas, new domain migrations |
| Docker compose (2 containers) | ✅ Maintained | Git installed in runtime for `git ls-remote` |
| Fluent UI Blazor components | ✅ Partial | Used for forms/dialogs; FluentTreeView deferred |
| MVVM pattern (CommunityToolkit.Mvvm) | ⏳ Not exercised | ViewModels project still stub; DashboardPage uses code-behind |
| DDD for domain logic | ✅ Adopted | System/Repository aggregate roots, factory methods, coordinator pattern |
| Repository pattern (interface in Core, impl in Infrastructure) | ✅ Maintained | `ISystemRepository`, `IRepositoryRepository`, dedicated abstractions |
| Coordinator pattern | ✅ Established | 3 new coordinators, consistent with Epic 1's AdministratorRoleCoordinator |
| FluentValidation + ProblemDetails | ✅ Maintained | Chinese error messages, no custom envelopes |
| Naming conventions (.editorconfig) | ✅ Maintained | Async suffix, _camelCase fields, PascalCase types |
| `/api/` prefix + plural nouns | ✅ Maintained | `/api/systems`, `/api/repositories/standalone` |
| Chinese-only UI | ✅ Maintained | All labels, validation, error messages in Simplified Chinese |
| Git operations via shell | ✅ New capability | `git ls-remote` with credential sanitization |
| Policy-based authorization | ✅ Extended | No new mechanisms; existing policies carried all new auth |

**Assessment**: Epic 2 delivered the domain model and management backend as prescribed. Two architectural patterns (MVVM ViewModels, FluentTreeView) are deferred but their interfaces are designed. The coordinator pattern is now an established team convention. No architectural decisions were contradicted.

---

## Testing Coverage

### Coverage by Story

| Story | Test File | Test Count | Critical Paths Covered |
|-------|-----------|------------|----------------------|
| 2.1 | `SystemCrudTests.cs` | 7 | CRUD, uniqueness, delete guards, authorization, tree visibility |
| 2.2 | `GrantSystemOwnershipTests.cs` | 6 | Assignment, duplicate rejection, candidate filtering, role sync, last-owner cleanup |
| 2.3 | `RepositoryManagementTests.cs` | 8 | System repo CRUD, git reachable/unreachable/auth, visibility bypass, admin global access |
| 2.4 | `RepositoryManagementTests.cs` | 7 | Standalone CRUD, SystemId=null, uniqueness, visibility, Story 2.3 regression |
| 2.5 | `UserSuppliedContextTests.cs` | 8 | Global/system/repo context CRUD, authorization, combination order, scan queuing |

### Coverage Gaps

- **Concurrent request testing**: Only Story 2.4's race condition has a `DbUpdateException` catch; the system repo path and role assignment path are untested under concurrency.
- **Git validation edge cases**: Non-HTTP git URLs (SSH, file://), very large repositories (timeout behavior), and IPv6 addresses are not tested.
- **DashboardPage rendering**: No component-level tests verify the UI renders correctly for each user role. All UI verification is through integration test HTML string assertions.
- **Docker deployment**: No automated tests for the Dockerfile or docker-compose behavior with the new domain migrations.

### Test Quality Observations

- **Good**: Consistent `{Method}_{Scenario}_{ExpectedResult}` naming across all 5 test files
- **Good**: SQLite dual-database testing caught real compatibility issues (DateTimeOffset ORDER BY, NULL unique index)
- **Good**: Auth bypass tests actually attempt the bypass (not just verify UI element visibility)
- **Good**: Credential sanitization test uses a real TCP listener to simulate auth-required remotes
- **Improvement area**: Tests depend on HTML string matching for UI verification — fragile against label text changes
- **Improvement area**: No test for the `CreateAsync` race condition that was explicitly noted in 2.4 review

---

## Code Quality

### Patterns Established

1. **Coordinator pattern**: Centralized mutation logic with Chinese error mapping, DI registration, and testability. Three new coordinators joined `AdministratorRoleCoordinator` from Epic 1.

2. **Visibility-filtered repository methods**: `GetVisibleByIdAsync` pattern replaces existence-only `GetByIdAsync` for all scoped data access. This prevents the auth bypass class of bugs.

3. **Aggregate root factory methods**: `System.AddRepository()`, `Repository.Create()`, `Repository.CreateStandalone()` — entities are never constructed incompletely from outside the domain layer.

4. **Infrastructure service abstraction**: `IGitRemoteValidationService` with sanitized output establishes the pattern for external process integration (CodeGraph CLI in Epic 4, connection testing in Epic 3).

5. **Scan-state abstraction**: Story 2.5's `IScanStateService` interface demonstrates designing for future replacement — minimal surface area, clear contract, stub implementation that Epic 4 can swap out.

6. **Dual-scope uniqueness**: Repository names are unique per-system for system repos and unique within the standalone scope for standalone repos. The filtered partial unique index pattern handles both SQLite and PostgreSQL.

### Code Organization

```
src/dotnet/
├── Vulgata.Shared/            # RoleNames, PolicyNames, DTOs, Validators (partial)
├── Vulgata.Core/              # System, Repository, SystemOwnerAssignment entities + repository interfaces
├── Vulgata.Infrastructure/    # VulgataDbContext (3 DbSets), EF Configs, Repositories, GitRemoteValidationService
├── Vulgata.Web.ViewModels/    # Stub (SystemsPageViewModel not created)
├── Vulgata.Agents/            # Stub
└── Vulgata.Web/               # Program.cs (Identity + API endpoints + DI), DashboardPage, Dialogs, Coordinators
```

The project dependency graph remains clean. No circular dependencies.

---

## Epic 1 Retrospective Follow-Through

| # | Action Item (from Epic 1) | Status | Evidence |
|---|--------------------------|--------|----------|
| 1 | Create reusable Blazor SSR test helpers | ⏳ Partial | Test patterns reused (WebApplicationFactory, SQLite, form extraction) but no shared fixture base class created |
| 2 | Implement SystemOwner per-system scoping | ✅ Done | `SystemOwnershipCoordinator` + `ListVisibleAsync` + `GetVisibleByIdAsync` across all endpoints |
| 3 | Address CSP headers and crossorigin font | ❌ Not done | Deferred-work list not revisited |
| 4 | Document code patterns for team reference | ⏳ Partial | Coordinator pattern is implicit convention but not formally documented |
| 5 | Consider email confirmation flow | ❌ Not done | `RequireConfirmedAccount = false` still set |
| 6 | Review deferred-work list for quick wins | ❌ Not done | 11 items from Story 1.1 remain unaddressed |

**Key insight**: The most impactful Epic 1 action item (#2, SystemOwner scoping) was fully executed and directly prevented the auth bypass bug in Story 2.3's review. The test helper investment (#1) partially paid off — patterns were reused even if not formalized into a base class.

---

## Next Epic Preparation

### Epic 3 Scope

Epic 3 introduces LLM provider configuration and database connection management:

| Story | Description | Epic 2 Dependencies |
|-------|-------------|---------------------|
| 3.1 | LLM Provider Configuration Admin | Management backend, authorization policies, coordinator pattern |
| 3.2 | Per-System LLM Provider Override | System CRUD, SystemOwner authorization, system scoping |
| 3.3 | Database Connection Configuration | Repository management, credential encryption |

### Prerequisites & Risks

1. **DashboardPage extraction**: The management dashboard is at its complexity ceiling. Before adding a new management tab for LLM provider configuration, extract standalone repo and context sections into dedicated components to make room.

2. **Credential encryption (NFR-2.2)**: Epic 3 introduces API keys for LLM providers and database connection strings. ASP.NET Core Data Protection is specified in the architecture but has not been exercised yet. **Risk**: Medium — first use of the encryption infrastructure.

3. **Connection testing pattern**: FR-3.4 (LLM provider connection test) follows the same pattern as git URL validation. `IGitRemoteValidationService` provides the template: interface in Core, implementation in Infrastructure, sanitized errors, testable abstraction.

4. **Provider failover (NFR-3.2)**: Multi-provider configuration with automatic failover is architecturally significant. The coordinator pattern from Epic 2 should be extended to handle provider selection and fallback logic.

5. **ViewModel adoption opportunity**: Epic 3's new management tab (LLM provider configuration) is a greenfield UI surface — ideal for adopting the MVVM pattern with `LoadState` that was deferred in Epic 2.

### Recommended Adjustments

1. **Extract DashboardPage sub-components before Epic 3 starts**: Move standalone repo section and context editors to dedicated Razor components. Low-risk refactoring that prevents compounding the monolith problem.

2. **Apply `DbUpdateException` catch to system repository creation**: Fix the pre-existing race condition noted in Story 2.4 review as part of the first Epic 3 story.

3. **Start with a ViewModel for Epic 3's provider configuration**: Use the greenfield management tab to establish the ViewModel + LoadState pattern before retrofitting it onto Epic 2's DashboardPage.

4. **Relocate misplaced files**: Move DTOs from `Vulgata.Web.Validators/` to `Vulgata.Shared/` and extract endpoints from `Program.cs` to `Endpoints/` as a cleanup commit before Epic 3 implementation begins.

---

## Action Items

| # | Action | Owner | Priority |
|---|--------|-------|----------|
| 1 | Extract DashboardPage sub-components (standalone repos, context editors) into dedicated Razor components | Developer | High |
| 2 | Apply `try/catch (DbUpdateException)` race-condition fix to `RepositoryManagementCoordinator.CreateAsync` | Developer | High |
| 3 | Adopt MVVM ViewModel + LoadState pattern for Epic 3's LLM provider configuration tab | Developer | High |
| 4 | Relocate misplaced DTOs to `Vulgata.Shared/` and extract API endpoints from `Program.cs` to `Endpoints/` | Developer | Medium |
| 5 | Create reusable Blazor SSR test fixture base class (form submission, auth client, role setup) | Developer | Medium |
| 6 | Implement FluentTreeView in management sidebar with scan status dots | Developer | Medium |
| 7 | Address CSP headers and `crossorigin` font from Epic 1 deferred-work list | Developer | Low |
| 8 | Formally document coordinator pattern as team convention in `docs/agents/domain.md` | Developer | Low |

---

## Team Reflection

Epic 2 transformed the management backend from a placeholder page into a feature-complete administration surface. The review-heavy quality process caught 8 critical/high bugs before they reached production — most notably an authorization bypass that would have allowed any authenticated user to manipulate any system's repositories, and a silent-update-loss bug from `AsNoTracking()` that would have manifested as "edits don't work."

The coordinator pattern from Epic 1 proved its value and was consciously replicated across three new domains. The dual-database testing strategy (SQLite for tests, PostgreSQL for production) caught real compatibility issues in two separate stories.

The primary area for improvement is front-end architecture discipline: the DashboardPage monolith, deferred FluentTreeView, and skipped ViewModel pattern are technical debt that Epic 3's new management surfaces should not inherit. The team should invest in component extraction and ViewModel adoption before adding the next management tab.
