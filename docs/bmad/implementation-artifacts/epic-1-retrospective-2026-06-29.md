---
date: 2026-06-29
epic: 1
status: done
title: "Epic 1 Retrospective: User Authentication & Authorization Foundation"
project: vulgata
stories_completed: 6
---

# Epic 1 Retrospective: User Authentication & Authorization Foundation

## Epic Summary

Epic 1 established the authentication and authorization foundation for Vulgata — a greenfield Blazor Web App with ASP.NET Core Identity, PostgreSQL 17, Docker deployment, and role-based access control. All 6 stories were completed across the sprint.

| Story | Title | Status |
|-------|-------|--------|
| 1.1 | Solution Scaffolding & Docker Deployment | done |
| 1.2 | User Registration | done |
| 1.3 | Login & Logout | done |
| 1.4 | Profile Management | done |
| 1.5 | Role Seeding & Authorization Policies | done |
| 1.6 | Administrator Role Assignment | done |

### Requirements Satisfied

- **FR-1.1**: Users register with email and password
- **FR-1.2**: Users log in and log out
- **FR-1.3**: Users view and edit profile (display name, email, password change)
- **FR-1.4**: Role-based access: Administrator, System Owner, User

---

## What Went Well

### 1. Architecture Adherence

The implementation closely followed the architecture decision document:

- **Two-DbContext Pattern**: `ApplicationDbContext` (`identity` schema) + `VulgataDbContext` (`vulgata` schema) with separate migration history tables. Schema isolation worked cleanly from Story 1.1 and caused zero friction downstream.
- **PostgreSQL 17 + EF Core**: Migrations ran at startup via `MigrateAsync()` for both contexts. Connection string from environment/config.
- **Docker Compose**: Two containers (Blazor app + PostgreSQL 17) deploy with `docker compose up`. Multi-stage Dockerfile with non-root user and Git/CodeGraph CLI pre-installed in runtime.
- **Fluent UI Blazor**: Primary component library applied consistently across all pages. Brand tokens (`#445E7A`, `#B98B6B`) configured via design tokens.
- **bcrypt Password Hashing**: Replaced ASP.NET Core Identity's default PBKDF2 with bcrypt (work factor 12, SHA-384) satisfying NFR-2.1.

### 2. Incremental Story Development

Each story built cleanly on the previous one's foundation with no regression-causing rewrites:

- Story 1.1 → scaffolded Identity + PostgreSQL + Docker + Fluent UI
- Story 1.2 → added bcrypt hashing + Chinese error messages on top
- Story 1.3 → polished login UX (removed passkey/external-login boilerplate)
- Story 1.4 → added `DisplayName` field + direct email/password management
- Story 1.5 → centralized role names + policies + startup seeding
- Story 1.6 → first-user bootstrap + administrator-only user management

This linear dependency chain with clear "what already exists" documentation in each story's Dev Notes prevented redundant work and kept scope boundaries visible.

### 3. Centralized Constants & Policy Hooks

Story 1.5 introduced `RoleNames.cs` and `AuthorizationPolicyNames.cs` in the shared project, replacing scattered string literals across route attributes, `AuthorizeView` components, and `Program.cs`. The `ManagementAccessRequirement` handler provides a forward-compatible policy hook for Epic 2's system-scoping refinement — no auth foundation rewrite needed.

### 4. Chinese Localization

All Identity error messages, form labels, validation messages, and UI text were delivered in Simplified Chinese from the start:

- `ChineseIdentityErrorDescriber` overrides all `IdentityErrorDescriber` methods
- Fluent UI form labels use Chinese display names
- Access-denied page, navbar, and profile management pages all use Chinese

### 5. Testing Coverage

34+ integration tests across 6 test files, using consistent patterns:

| Test File | Coverage |
|-----------|----------|
| `IdentityRegistrationTests.cs` | Registration, bcrypt hashing, duplicate email, password complexity |
| `LoginLogoutTests.cs` | Login success/failure, logout, protected-route redirect, returnUrl flow |
| `ProfileManagementTests.cs` | Display name, email update, duplicate email rejection, password change |
| `RoleSeedingAndAuthorizationTests.cs` | Role creation, idempotency, access denial, navbar visibility |
| `AdministratorRoleAssignmentTests.cs` | First-user bootstrap, admin grant/remove, non-admin denial, fallback roles |
| `ArchitectureScaffoldingTests.cs` | Build verification, project structure |

Tests use `WebApplicationFactory<Program>` with SQLite overrides for HTTP-level integration and reflection-driven component harnesses where appropriate.

### 6. Review-Driven Quality

Code reviews caught meaningful issues that were addressed before completion:

- Story 1.2: `RequireUniqueEmail` not enforced in production `Program.cs`; fixed
- Story 1.4: Remaining English `Error:` prefix on profile pages localized
- Story 1.6: [Critical] Concurrent first-user registration could produce multiple administrators; fixed with `AdministratorRoleCoordinator` serializing role mutations
- Story 1.6: [Critical] Concurrent administrator removals could drop to zero administrators; fixed with last-admin guard in serialized coordinator

The concurrency fixes in Story 1.6 were particularly valuable — they added deterministic tests that execute parallel mutations and assert exactly-one-admin invariants.

---

## What Could Be Improved

### 1. Concurrency Surprises

The two concurrency bugs in Story 1.6 (first-user bootstrap race, last-admin removal race) were found in code review, not during implementation. Both were classic check-then-act patterns that were obvious in hindsight. **Pattern**: Any "first-X" or "last-X" check that reads-then-writes should be designed for concurrency from the start.

### 2. Blazor SSR Testing Friction

Integration testing of Blazor Server-Side Rendered forms required discovering implementation details (generated handler hidden inputs, `formname` attribute quirks, cookie round-trip patterns). The test harness evolved across stories as each one encountered a different Blazor form peculiarity:

- Story 1.3: Cookie authentication needs `AllowAutoRedirect = false`
- Story 1.6: `@formname` renders handler hidden inputs, not literal `formname` attributes
- Story 1.6: Authenticated pages with multiple POST forms (nav logout + page form) need form targeting by `action`

This knowledge was captured in `/memories/repo/vulgata-test-harness.md`, but a reusable test helper base class or fixture could reduce the per-story discovery cost.

### 3. Story 1.3 UI Cleanup Scope Creep

Story 1.3 was scoped as "polish login/logout UX" but involved removing significant template boilerplate (passkey sign-in, external login picker, email confirmation resend link) while preserving passkey components used by profile management (Story 1.4). The story's Dev Notes carefully documented this, but the cross-story component dependencies (keeping `PasskeySubmit.razor` but removing its usage from `Login.razor`) added subtlety that required review iteration.

### 4. Deferred Work Accumulation

Story 1.1's review produced a deferred-work list with 11 items (no CSP headers, no HTTPS in Docker, `apt-get` no retry, etc.). While all were correctly judged as acceptable for V1, the list is worth revisiting before the competition demo. Items like CSP headers are low-effort but visible to security-conscious judges.

### 5. Email Confirmation Deferred

`RequireConfirmedAccount = false` is configured project-wide. Story 1.4 replaced the email-change confirmation-link flow with a direct update. This is acceptable for V1 demo but email confirmation would be expected in any production deployment. If there's time before the competition, adding email confirmation (with the existing `IEmailSender` infrastructure) would demonstrate completeness.

---

## Lessons Learned

### For Epic 2

1. **Concurrency-first design for state-mutating operations**: Any operation that reads state then writes based on that state (especially "first"/"last" invariants) should be designed with serialization from the start. The `AdministratorRoleCoordinator` pattern (serialized mutation path) is reusable.

2. **Test harness maturity reduces story friction**: Epic 2 stories will encounter new Blazor SSR testing patterns. Investing in reusable test helpers (form discovery, authenticated client factory, role assignment helpers) before Story 2.1 starts would prevent the per-story discovery tax.

3. **Policy hooks work**: The `ManagementAccessRequirement` and policy-based authorization introduced in Story 1.5 provide a clean extension point for Epic 2's `SystemOwner` scoping. Epic 2 should plug into these rather than creating parallel auth mechanisms.

4. **Centralized constants are worth the upfront cost**: `RoleNames.cs` and `AuthorizationPolicyNames.cs` prevented string-literal drift across 6 stories. Epic 2 should continue this pattern for new domain constants (e.g., system statuses, repository states).

5. **Dev Notes section is invaluable**: Each story's "What Already Exists — Do NOT Rebuild" section prevented redundant work. Epic 2 stories should maintain this convention.

6. **Review concurrency early**: The Story 1.6 concurrency findings suggest the review step caught what implementation missed. For Epic 2 stories involving concurrent operations (scan queue, git operations, worker dispatch), explicitly listing concurrency concerns in Dev Notes would front-load the design.

### For Epic 3

1. **Provider/configuration management will need the same discipline**: LLM provider configuration (Epic 3) will involve mutable state with consistency requirements (default provider changes, per-agent overrides). The `AdministratorRoleCoordinator` serialization pattern should be studied as a template.

2. **The two-DbContext pattern will need domain entity migration**: Currently `VulgataDbContext` is a stub. Epic 2 will add System, Repository, and related entities. The separate-schema pattern is proven and should be maintained.

---

## Architecture Adherence

| Decision | Status | Notes |
|----------|--------|-------|
| ASP.NET Core Identity + cookie auth | ✅ Implemented | With bcrypt override, Chinese error describer |
| PostgreSQL 17 + EF Core + Npgsql | ✅ Implemented | Two DbContexts, separate schemas, startup migration |
| Docker compose (2 containers) | ✅ Implemented | Multi-stage Dockerfile, non-root user, Git + CodeGraph in runtime |
| Fluent UI Blazor components | ✅ Implemented | Applied across all pages, brand tokens configured |
| MVVM pattern (CommunityToolkit.Mvvm) | ✅ Partial | Package added; ViewModels project exists but not heavily exercised yet |
| DDD for domain logic | ⏳ Deferred | Domain entities arrive in Epic 2 |
| Agent framework (MAF) | ⏳ Deferred | Vulgata.Agents project stubbed; spike in Epic 4 |
| SignalR hubs | ⏳ Deferred | Epic 4 (scan dashboard, live graph) |
| Naming conventions (.editorconfig) | ✅ Implemented | Async suffix, _camelCase fields, PascalCase types |
| Roslyn analyzers | ✅ Implemented | AnalysisMode enabled |

**Assessment**: Epic 1 delivered exactly the infrastructure layer the architecture prescribed. No architectural decisions were overridden or contradicted.

---

## Testing Coverage

### Coverage by Story

| Story | Test File | Test Count (approx.) | Critical Paths Covered |
|-------|-----------|---------------------|----------------------|
| 1.1 | `ArchitectureScaffoldingTests.cs` | 1-2 | Solution builds, project references valid |
| 1.2 | `IdentityRegistrationTests.cs` | 6+ | Registration success, bcrypt hash format, duplicate email, password complexity, schema isolation |
| 1.3 | `LoginLogoutTests.cs` | 7 | Valid/invalid login, generic error message, logout cookie clearance, protected redirect, returnUrl flow |
| 1.4 | `ProfileManagementTests.cs` | 7 | Display name load/update, email update with username sync, duplicate email, incorrect password, password change |
| 1.5 | `RoleSeedingAndAuthorizationTests.cs` | 6 | Role creation, idempotency, user access denial, navbar hiding, admin/systemowner management access |
| 1.6 | `AdministratorRoleAssignmentTests.cs` | 7 | First-user bootstrap, subsequent-user default role, admin grant, admin remove with fallback, non-admin denial, nav visibility |

### Coverage Gaps

- **HTTP error handling**: No tests for 500/503 responses or database-connection-failure scenarios
- **Docker deployment**: No automated tests verify the Dockerfile or docker-compose behavior
- **Cross-browser**: All tests run in the test host, not a browser
- **Accessibility**: No automated a11y checks (not scoped for Epic 1)

### Test Quality Observations

- **Good**: Consistent naming convention (`Method_Scenario_ExpectedResult`)
- **Good**: Real HTTP integration tests for auth flows (cookie round-trips, redirects)
- **Good**: Concurrency tests for critical invariants (Story 1.6)
- **Good**: Test harness knowledge captured in repo memory (`vulgata-test-harness.md`)
- **Improvement area**: Some tests depend on implementation details (hidden input field ordering, form targeting by action string)

---

## Code Quality

### Patterns Established

1. **Service registration organization**: `Program.cs` groups Identity configuration, database setup, authorization policies, and service registration in clearly separated blocks.

2. **Migration idempotency**: Both startup migrations and role seeding check for existing state before mutating. The `RoleSeeder` logs skip/create/failure paths.

3. **Shared constants**: `RoleNames.cs` and `AuthorizationPolicyNames.cs` in `Vulgata.Shared` are referenced by Web, test projects, and authorization handlers.

4. **Localized error handling**: `ChineseIdentityErrorDescriber` centralizes all Identity error messages. Form validation uses DataAnnotations with Chinese `DisplayAttribute`.

5. **Policy-based authorization**: Rather than scattering `[Authorize(Roles = "...")]` everywhere, management routes use a centralized `ManagementAccess` policy with an `IAuthorizationHandler` that can be extended for system-scoping.

6. **Coordinator pattern for serialized mutations**: `AdministratorRoleCoordinator` serializes role mutations that need atomicity guarantees — a pattern reusable for Epic 3's provider configuration mutations.

### Code Organization

```
src/dotnet/
├── Vulgata.Shared/            # RoleNames, AuthorizationPolicyNames, LoadState
├── Vulgata.Core/              # Domain interfaces (stub)
├── Vulgata.Infrastructure/    # VulgataDbContext (stub)
├── Vulgata.Web.ViewModels/    # MVVM ViewModels (stub)
├── Vulgata.Agents/            # Agent definitions (stub)
└── Vulgata.Web/               # Identity, Blazor pages, Program.cs
```

The project dependency graph is clean: Web → all; Infrastructure → Core; Agents → Core + Infrastructure; ViewModels → Shared; Shared → standalone. No circular dependencies.

---

## Next Epic Preparation

### Epic 2 Scope

Epic 2 introduces the domain model: Systems, Repositories, and user-supplied context. Stories:

| Story | Description | Epic 1 Dependencies |
|-------|-------------|---------------------|
| 2.1 | System CRUD (System Owners create/edit/delete Systems) | Management route protection, `SystemOwner` role, management layout |
| 2.2 | Grant System Ownership (Administrators assign SystemOwner role + system scope) | `AdministratorRoleCoordinator`, user management page |
| 2.3 | Repository Management (add/remove repos to Systems, git URL validation) | System CRUD |
| 2.4 | Standalone Repository Creation (org-owned shared libs, no System) | Repository management |
| 2.5 | User-Supplied Context (global/System/Repository-level context configuration) | System & Repository CRUD |

### Prerequisites & Risks

1. **`SystemOwner` Scoping**: Currently `SystemOwner` uses coarse-grained management access (same as `Administrator` for management routes). Epic 2 must implement per-system ownership persistence. The `ManagementAccessRequirement` handler from Story 1.5 provides the policy hook. **Risk**: Low — the hook is in place.

2. **`VulgataDbContext` Domain Entities**: The DbContext is currently a stub with no `DbSet<>` properties. Epic 2 will add `System`, `Repository`, and related entities with EF Core configurations. **Risk**: Low — standard EF Core migrations.

3. **Git URL Validation (Story 2.3)**: Requires network access for reachability checks. Docker container already has Git installed. **Risk**: Medium — need to handle network failures gracefully; test infrastructure needs a way to mock or bypass actual git operations.

4. **User-Supplied Context (Story 2.5)**: Requires context queuing during active scans (FR-14.4). Since Epic 4 (scanning) is far out, this can be implemented as a pure CRUD + validation story with the queuing behavior deferred. **Risk**: Low if scoped appropriately.

5. **Blazor Component Complexity**: The management UI will grow from a single DashboardPage stub to multiple management pages (Systems, Repositories, Settings with user management). The `ManagementLayout.razor` holy grail layout (tab bar + sidebar + content) needs to materialize. **Risk**: Medium — this is the first real test of the management layout design.

### Recommended Adjustments

1. **Create reusable test helpers before Story 2.1**: Extract common test patterns (authenticated HTTP client, form submission, role assignment) into a `TestFixtureBase` or `TestHelpers` class. The `vulgata-test-harness.md` memory documents the patterns; codifying them reduces per-story friction.

2. **Address deferred work**: Items from Story 1.1's deferred-work list that are low-effort (CSP headers, `crossorigin` on font import) could be addressed in Story 2.1 as part of infrastructure hardening.

3. **Consider Story 2.2 ordering**: Granting System Ownership requires both System CRUD (2.1) and the administrator user management page (1.6). Story 2.2 could be developed in parallel with 2.3 (repository management) after 2.1 is complete.

4. **Watch for Epic 1 regression**: Epic 2 stories will add Entity Framework migrations to `VulgataDbContext`. Ensure the two-DbContext migration pattern (separate history tables) remains intact.

---

## Action Items

| # | Action | Owner | Priority |
|---|--------|-------|----------|
| 1 | Create reusable Blazor SSR test helpers (authenticated client factory, form submission, role setup) | Developer | High |
| 2 | Implement `SystemOwner` per-system scoping via `ManagementAccessRequirement` extension | Epic 2.2 | High |
| 3 | Address CSP headers and `crossorigin` font from deferred-work list | Developer | Medium |
| 4 | Document code patterns established in Epic 1 for team reference | Developer | Medium |
| 5 | Consider adding email confirmation flow before competition demo | Developer | Low |
| 6 | Review Epic 1 deferred-work list before competition for quick wins | Developer | Low |

---

## Team Reflection

Epic 1 successfully delivered a production-quality authentication and authorization foundation. The incremental story approach with clear scope boundaries, review-driven quality improvements, and comprehensive integration testing established patterns that will benefit all subsequent epics. The primary takeaway for Epic 2 is to invest in test infrastructure early and design state-mutating operations for concurrency from the start.
