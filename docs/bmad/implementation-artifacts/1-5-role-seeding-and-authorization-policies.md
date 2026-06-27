---
baseline_commit: 26cbcbb490890e5f337ccb5c931137314ffc2ea6
---

# Story 1.5: Role Seeding & Authorization Policies

Status: done

## Story

As an **administrator**,
I want three predefined roles seeded at startup with appropriate authorization policies,
so that role-based access control is enforced from day one.

## Acceptance Criteria

### AC-1: Seed Roles on First Startup
**Given** the application starts for the first time
**When** the database is migrated
**Then** three roles shall be seeded: `Administrator`, `SystemOwner`, and `User`

### AC-2: Authorization Policies Reflect Role Scope
**Given** the authorization policies are configured
**When** the application evaluates access
**Then** `Administrator` shall have full platform management access
**And** `SystemOwner` shall have access scoped to the systems they are explicitly assigned to
**And** `User` shall have read-only access to chat and documents

### AC-3: User Cannot Access Administrator-Only Pages
**Given** I am logged in as a `User`
**When** I attempt to access an administrator-only page
**Then** I shall be redirected to an `访问被拒绝` page
**And** the `管理后台` nav link shall not be visible in my top navbar

### AC-4: Idempotent Seeding
**Given** the role seeding mechanism
**When** the application restarts
**Then** existing roles shall not be duplicated

## Tasks / Subtasks

- [x] Task 1: Centralize Role Names and Policies (AC-1, AC-2)
  - [x] 1.1 Add role constants in `src/dotnet/Vulgata.Shared/` (for example `RoleNames.cs`) with exact values `Administrator`, `SystemOwner`, and `User`
  - [x] 1.2 Replace hard-coded role strings in currently implemented UI/auth surfaces with the shared constants where practical
  - [x] 1.3 Add explicit authorization policies in `Program.cs` for administrator-only and management access instead of relying only on scattered string literals
  - [x] 1.4 Keep Story 1.5 focused on seeding and policy registration; do not build Story 1.6 user-management UI here

- [x] Task 2: Implement Role Seeding at Startup (AC-1, AC-4)
  - [x] 2.1 Create a startup seeding component/service under `src/dotnet/Vulgata.Web/` or `src/dotnet/Vulgata.Infrastructure/` that uses `RoleManager<IdentityRole>`
  - [x] 2.2 Seed `Administrator`, `SystemOwner`, and `User` after Identity migrations complete
  - [x] 2.3 Make the seeding idempotent by checking for existing roles before creating them
  - [x] 2.4 Add startup logging around role seeding so failures are diagnosable without exposing sensitive details

- [x] Task 3: Enforce Navigation and Route Access (AC-2, AC-3)
  - [x] 3.1 Verify `MainLayout.razor` hides `管理后台` for users who are not `Administrator` or `SystemOwner`
  - [x] 3.2 Verify management routes remain protected with `Administrator` / `SystemOwner` access only
  - [x] 3.3 Ensure the Access Denied page remains Chinese and is reached for unauthorized protected-route access
  - [x] 3.4 Keep chat access available for ordinary `User` role accounts

- [x] Task 4: Preserve the Current Auth Story Contract (AC-2)
  - [x] 4.1 Do not introduce full system-scoping persistence for `SystemOwner` yet if the domain model for assigned systems is not implemented; document the temporary limitation in the story file completion notes if needed
  - [x] 4.2 Implement policy hooks so Story 2 can plug real system-scoping in without rewriting the auth foundation
  - [x] 4.3 Avoid inventing placeholder admin pages beyond what is necessary to verify authorization behavior

- [x] Task 5: Add Executable Authorization/Seeding Tests (AC-1 through AC-4)
  - [x] 5.1 Add `tests/Vulgata.Tests/RoleSeedingAndAuthorizationTests.cs`
  - [x] 5.2 Test that the three roles are created after startup / migration bootstrap
  - [x] 5.3 Test that rerunning startup does not duplicate the roles
  - [x] 5.4 Test that a `User` cannot access `/management` and receives the access-denied flow
  - [x] 5.5 Test that the `管理后台` link is absent for a plain `User`
  - [x] 5.6 Test that an `Administrator` (and, if implemented, `SystemOwner`) can reach management routes

- [x] Task 6: Manual Verification Checklist
  - [x] 6.1 Start the app on a fresh database and verify `Administrator`, `SystemOwner`, and `User` roles exist
  - [x] 6.2 Restart the app and verify no duplicate roles are created
  - [x] 6.3 Log in as a plain `User` and verify `管理后台` is hidden
  - [x] 6.4 Attempt to browse to `/management` as a plain `User` and verify `访问被拒绝`

## Dev Notes

### CRITICAL: What Already Exists — Do NOT Rebuild

Stories 1.1-1.4 already established the following:

- Identity storage is in `ApplicationDbContext` under the `identity` schema
- Login / logout / authenticated-shell behavior is already working
- `MainLayout.razor` currently gates the `管理后台` link with `<AuthorizeView Roles="Administrator,SystemOwner">`
- Management route pages already use `[Authorize(Roles = "Administrator,SystemOwner")]`
- `AccessDenied.razor` already exists and is localized to Chinese
- No user-management UI or role assignment UI exists yet; that belongs to Story 1.6

### CRITICAL: Current Gap to Close

The codebase already refers to the roles `Administrator` and `SystemOwner`, but there is **no seeding mechanism** yet and no centralized role constants. Story 1.5 should close that infrastructure gap so the existing UI/route guards are backed by real Identity roles.

### CRITICAL: Scope Boundary with Story 1.6

Do **not** implement:

- a user management page
- first-user auto-promotion to Administrator
- admin UI for assigning/removing roles

Those are Story 1.6 responsibilities. Story 1.5 is the authorization foundation only.

### CRITICAL: `SystemOwner` Scoping Limitation

The architecture language says `SystemOwner` access is scoped to assigned systems, but the current domain model and UI do not yet implement system assignments. Story 1.5 should therefore:

- seed the `SystemOwner` role
- register policies / hooks that recognize it
- preserve the route/nav distinction already present
- avoid faking system-assignment persistence that belongs to Epic 2

If a temporary coarse-grained `SystemOwner` access model is used for management routes before Epic 2 introduces true ownership assignment, keep that limitation explicit in completion notes and tests.

### CRITICAL: Likely File Surface

| File | Action | Reason |
|------|--------|--------|
| `src/dotnet/Vulgata.Shared/RoleNames.cs` | CREATE | centralize exact role names |
| `src/dotnet/Vulgata.Web/Program.cs` | UPDATE | register policies and call seeding service |
| `src/dotnet/Vulgata.Web/Components/Layout/MainLayout.razor` | UPDATE if needed | replace string literals with constants if practical |
| `src/dotnet/Vulgata.Web/Components/Account/Pages/AccessDenied.razor` | VERIFY / update only if needed | keep Chinese denial UX |
| `tests/Vulgata.Tests/RoleSeedingAndAuthorizationTests.cs` | CREATE | executable verification |

### CRITICAL: Testing Guidance

Reuse the existing HTTP-based integration test stack already added in Stories 1.3 and 1.4.

- `LoginLogoutTests.cs` demonstrates authenticated routing and cookie flows
- `ProfileManagementTests.cs` demonstrates authenticated user mutation patterns

For Story 1.5, prefer integration tests that exercise startup seeding and authorization behavior through the real host where practical.

### Git Intelligence

Recent commits:

```
f341363 Finalize code review for story 1.4
342d1d8 Address story 1.4 review findings
fe29691 Record review findings for story 1.4
1b80062 Implement story 1.4 profile management
5dc9285 Prepare story 1.4 for development
```

### Dev Agent Record

#### Agent Model Used

GPT-5.3-Codex

#### Debug Log References

- `dotnet test .\tests\Vulgata.Tests\Vulgata.Tests.csproj --filter "FullyQualifiedName~RoleSeedingAndAuthorizationTests"`
- `dotnet test .\Vulgata.slnx`
- `dotnet build .\Vulgata.slnx`

#### Completion Notes List

- Added centralized role constants (`Administrator`, `SystemOwner`, `User`) and centralized authorization policy names in shared code.
- Added explicit authorization policies in startup and a dedicated management-access requirement/handler to provide a policy hook for future Story 2 system-scoping integration.
- Added startup role seeding service using `RoleManager<IdentityRole>` after migrations; seeding is idempotent and logs both skip/create/failure paths.
- Updated management route protection and navbar visibility to use centralized management-access policy rather than scattered role string literals.
- Updated unauthorized-route behavior so authenticated-but-forbidden access is redirected to `/Account/AccessDenied` while unauthenticated access still redirects to `/Account/Login`.
- Added integration coverage in `RoleSeedingAndAuthorizationTests` for role creation, restart idempotency, user denial/nav hiding, and administrator/system-owner management access.
- Temporary limitation recorded: `SystemOwner` currently uses coarse-grained management access (no persisted assigned-system scope yet); policy hook is in place for Story 2 refinement.

#### File List

- src/dotnet/Vulgata.Shared/RoleNames.cs
- src/dotnet/Vulgata.Shared/AuthorizationPolicyNames.cs
- src/dotnet/Vulgata.Web/Program.cs
- src/dotnet/Vulgata.Web/Data/RoleSeeder.cs
- src/dotnet/Vulgata.Web/Data/ManagementAccessRequirement.cs
- src/dotnet/Vulgata.Web/Components/_Imports.razor
- src/dotnet/Vulgata.Web/Components/Layout/MainLayout.razor
- src/dotnet/Vulgata.Web/Components/Account/Shared/RedirectToLogin.razor
- src/dotnet/Vulgata.Web/Components/Pages/Management/DashboardPage.razor
- src/dotnet/Vulgata.Web/Components/Pages/Management/GraphPage.razor
- src/dotnet/Vulgata.Web/Components/Pages/Management/DocumentsPage.razor
- src/dotnet/Vulgata.Web/Components/Pages/Management/ScanHistoryPage.razor
- src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor
- tests/Vulgata.Tests/LoginLogoutTests.cs
- tests/Vulgata.Tests/RoleSeedingAndAuthorizationTests.cs

#### Change Log

- 2026-06-27: Implemented Story 1.5 role seeding and authorization policies; added integration tests and moved story to review.
- 2026-06-27: Code review completed with no findings; story marked done.