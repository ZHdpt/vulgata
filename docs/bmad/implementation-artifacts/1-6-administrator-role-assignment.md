---
baseline_commit: e7b2d65a9206d99267e33caa8fd2796db5b64221
---

# Story 1.6: Administrator Role Assignment

Status: review

## Story

As an **administrator**,
I want to promote a registered user to the Administrator role,
so that I can delegate platform management.

## Acceptance Criteria

### AC-1: Administrator Can View User Management
**Given** I am logged in as an `Administrator`
**When** I navigate to `管理后台 → 设置 → 用户管理`
**Then** I shall see a list of all registered users with their current roles

### AC-2: Assign Administrator Role
**Given** I am on the user-management page
**When** I assign the `Administrator` role to a user
**Then** the user shall immediately gain administrator privileges
**And** a confirmation message shall appear in Chinese

### AC-3: Remove Administrator Role
**Given** I am on the user-management page
**When** I remove the `Administrator` role from a user
**Then** the user shall revert to `User` unless they still have `SystemOwner`
**And** they shall immediately lose administrator-only access

### AC-4: Non-Administrators Cannot Access User Management
**Given** I am logged in as a `SystemOwner` or `User`
**When** I view the management interface or attempt direct navigation
**Then** the user-management page shall not be visible in settings navigation
**And** I shall be denied access if I attempt to navigate to it directly

### AC-5: First Registered User Becomes Administrator
**Given** the application starts with an empty database
**When** the first user registers
**Then** that user shall automatically receive the `Administrator` role
**And** subsequent registrations shall default to the `User` role

## Tasks / Subtasks

- [x] Task 1: Promote the First Registered User Automatically (AC-5)
  - [x] 1.1 Update the registration flow in `Register.razor` to detect whether any administrator exists yet
  - [x] 1.2 If no administrator exists, assign the new user to the `Administrator` role immediately after successful creation
  - [x] 1.3 If an administrator already exists, keep the default registration outcome as `User`
  - [x] 1.4 Reuse the centralized role names from Story 1.5 instead of introducing new literals

- [x] Task 2: Build Administrator-Only User Management UI (AC-1, AC-2, AC-3, AC-4)
  - [x] 2.1 Add a user-management page under the settings area of the management UI
  - [x] 2.2 Show all registered users and their current roles in a simple list or grid
  - [x] 2.3 Add an administrator-only action to assign the `Administrator` role to a selected user
  - [x] 2.4 Add an administrator-only action to remove the `Administrator` role from a selected user
  - [x] 2.5 Keep the UI Chinese and aligned with Fluent UI patterns already established in the project

- [x] Task 3: Enforce Admin-Only Access (AC-1, AC-4)
  - [x] 3.1 Protect the user-management page with administrator-only authorization
  - [x] 3.2 Add a settings-navigation entry for user management that is visible only to administrators
  - [x] 3.3 Verify direct navigation by non-admin users reaches the access-denied flow

- [x] Task 4: Role Transition Rules (AC-2, AC-3)
  - [x] 4.1 When granting `Administrator`, ensure the role assignment is idempotent
  - [x] 4.2 When removing `Administrator`, preserve `SystemOwner` if the user still holds it
  - [x] 4.3 When removing `Administrator` from a user with no other elevated role, ensure they still retain at least the `User` role
  - [x] 4.4 Prevent administrators from accidentally breaking the system by removing the role from the last remaining administrator unless the story’s UI explicitly handles that case safely

- [x] Task 5: Add Executable User-Management Tests (AC-1 through AC-5)
  - [x] 5.1 Add `tests/Vulgata.Tests/AdministratorRoleAssignmentTests.cs`
  - [x] 5.2 Test that the first registered user becomes `Administrator`
  - [x] 5.3 Test that later registered users default to `User`
  - [x] 5.4 Test that an administrator can grant the `Administrator` role to another user
  - [x] 5.5 Test that an administrator can remove the `Administrator` role while preserving fallback roles correctly
  - [x] 5.6 Test that a non-admin user cannot access the user-management page
  - [x] 5.7 Test that the management settings navigation does not expose the user-management entry to non-admin users

- [x] Task 6: Manual Verification Checklist
  - [x] 6.1 Start from a fresh database and verify the first registered account becomes `Administrator`
  - [x] 6.2 Register a second user and verify they do not become `Administrator` automatically
  - [x] 6.3 As an administrator, open `管理后台 → 设置 → 用户管理` and verify the user list and roles display
  - [x] 6.4 Grant administrator to another user and verify their access updates
  - [x] 6.5 Remove administrator from a user and verify the fallback role behavior
  - [x] 6.6 Verify non-admins cannot see or open the user-management page

### Review Findings

- [x] [Review][Patch] Concurrent first-user registration can assign `Administrator` to more than one account, so the bootstrap is not deterministic as written [src/dotnet/Vulgata.Web/Components/Account/Pages/Register.razor:125]
- [x] [Review][Patch] Concurrent administrator removals can still drop the system to zero administrators because the last-admin guard reads and removes in separate steps [src/dotnet/Vulgata.Web/Components/Pages/Management/UserManagementPage.razor:154]

## Dev Notes

### CRITICAL: What Already Exists — Do NOT Rebuild

Stories 1.1-1.5 already established the full identity baseline:

- registration, login, logout, and profile-management flows are implemented
- centralized role names and authorization policy names were introduced in Story 1.5
- startup role seeding already creates `Administrator`, `SystemOwner`, and `User`
- the shell already distinguishes management access vs ordinary user access

This story should build on that foundation rather than reworking it.

### CRITICAL: Likely Implementation Surface

| File | Action | Reason |
|------|--------|--------|
| `src/dotnet/Vulgata.Web/Components/Account/Pages/Register.razor` | UPDATE | first-user administrator assignment |
| `src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor` | UPDATE | settings navigation / user-management entry |
| `src/dotnet/Vulgata.Web/Components/Pages/Management/` | ADD / UPDATE | user-management page |
| `src/dotnet/Vulgata.Shared/RoleNames.cs` | REUSE | do not duplicate role literals |
| `tests/Vulgata.Tests/AdministratorRoleAssignmentTests.cs` | CREATE | executable verification |

### CRITICAL: Scope Boundary with Later Epics

Keep Story 1.6 limited to platform administrator role assignment.

Do **not** implement:

- Epic 2 system ownership assignment flows beyond what’s needed to preserve roles safely
- LLM/provider management UI beyond the user-management entry itself
- broad management dashboard redesigns

### CRITICAL: First-User Bootstrap Rule

The first-registered-user bootstrap must be safe and deterministic. The simplest acceptable approach is:

- after `UserManager.CreateAsync()` succeeds
- query whether any user is already in the `Administrator` role
- if none exist, add the new user to `Administrator`
- otherwise add the user to `User` if needed

Keep this logic transactionally simple and testable.

### CRITICAL: Last-Administrator Safety

The story requires role removal, but removing the last remaining administrator is dangerous. The implementation should guard that path or otherwise ensure the system cannot end up with zero administrators by accident.

### Git Intelligence

Recent commits:

```
f6cd41e Finalize code review for story 1.5
0327fc6 Implement story 1.5 role seeding and authorization
26cbcbb Prepare story 1.5 for development
f341363 Finalize code review for story 1.4
342d1d8 Address story 1.4 review findings
```

### Dev Agent Record

#### Agent Model Used

GPT-5.3-Codex

#### Debug Log References

- `dotnet test .\tests\Vulgata.Tests\Vulgata.Tests.csproj --filter "FullyQualifiedName~AdministratorRoleAssignmentTests"`
- `dotnet test .\tests\Vulgata.Tests\Vulgata.Tests.csproj --filter "FullyQualifiedName~IdentityRegistrationTests.RegisterUserStoresBcryptPasswordHashThroughIdentityPipeline|FullyQualifiedName~IdentityRegistrationTests.RegisterUserSignsInAndRedirectsToChatPageAfterSuccessfulRegistration"`
- `dotnet test .\tests\Vulgata.Tests\Vulgata.Tests.csproj --filter "FullyQualifiedName~IdentityRegistrationTests.AdministratorRoleCoordinator_AssignInitialRoleAsync_ConcurrentCallsPromoteExactlyOneAdministrator|FullyQualifiedName~IdentityRegistrationTests.AdministratorRoleCoordinator_RemoveAdministratorAsync_ConcurrentCallsPreserveLastAdministrator"`
- `dotnet test .\tests\Vulgata.Tests\Vulgata.Tests.csproj --filter "FullyQualifiedName~AdministratorRoleAssignmentTests|FullyQualifiedName~IdentityRegistrationTests.RegisterUserStoresBcryptPasswordHashThroughIdentityPipeline|FullyQualifiedName~IdentityRegistrationTests.RegisterUserSignsInAndRedirectsToChatPageAfterSuccessfulRegistration|FullyQualifiedName~IdentityRegistrationTests.AdministratorRoleCoordinator_AssignInitialRoleAsync_ConcurrentCallsPromoteExactlyOneAdministrator|FullyQualifiedName~IdentityRegistrationTests.AdministratorRoleCoordinator_RemoveAdministratorAsync_ConcurrentCallsPreserveLastAdministrator"`
- `dotnet build .\Vulgata.slnx`
- `dotnet test .\Vulgata.slnx`

#### Completion Notes List

- Verified the existing Story 1.6 app implementation against administrator role bootstrap, administrator-only user management, non-admin denial, administrator grant/remove flows, fallback-role preservation, and last-administrator protection.
- Updated `AdministratorRoleAssignmentTests` so the harness finds Blazor SSR forms through the generated handler hidden input instead of relying on literal `formname` attributes, and made hidden-input extraction resilient to attribute ordering.
- Extended `IdentityRegistrationTests` in-memory Identity doubles with role-store behavior so Story 1.6's first-user administrator bootstrap works without regressing earlier registration tests.
- Added `AdministratorRoleCoordinator` and routed registration bootstrap plus administrator removal through a shared serialized role-mutation path to eliminate check-then-mutate races identified in review.
- Added deterministic concurrency tests that execute the two critical mutations in parallel and assert exactly one bootstrap administrator and preserved single-administrator floor on concurrent removals.
- Validation passed with the focused Story 1.6 suite, the targeted registration regression tests, the full solution build, and the full solution test suite.
- Manual verification checklist scenarios were exercised through executed end-to-end integration coverage on the real host/test pipeline rather than a separate browser-only pass.

#### File List

- docs/bmad/implementation-artifacts/1-6-administrator-role-assignment.md
- docs/bmad/implementation-artifacts/sprint-status.yaml
- src/dotnet/Vulgata.Web/Components/Account/Pages/Register.razor
- src/dotnet/Vulgata.Web/Components/Pages/Management/SettingsPage.razor
- src/dotnet/Vulgata.Web/Components/Pages/Management/UserManagementPage.razor
- src/dotnet/Vulgata.Web/Data/AdministratorRoleCoordinator.cs
- src/dotnet/Vulgata.Web/Data/ManagementAccessRequirement.cs
- src/dotnet/Vulgata.Web/Program.cs
- tests/Vulgata.Tests/AdministratorRoleAssignmentTests.cs
- tests/Vulgata.Tests/IdentityRegistrationTests.cs

#### Change Log

- 2026-06-27: Addressed Story 1.6 review concurrency findings by serializing first-user administrator bootstrap and administrator demotion flows, added deterministic concurrency tests, and returned story status to review.
- 2026-06-27: Validated Story 1.6 administrator role assignment, fixed Blazor form discovery in the new test harness, updated registration test doubles for first-user role bootstrap coverage, and moved the story to review.