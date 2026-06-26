# Story 1.4: Profile Management

Status: ready-for-dev

## Story

As a **registered user**,
I want to view and edit my profile information,
so that I can keep my display name, email, and password current.

## Acceptance Criteria

### AC-1: View Current Profile
**Given** I am logged in
**When** I open `个人资料`
**Then** I shall see my current display name and email in a Fluent UI form

### AC-2: Update Display Name
**Given** I am on the Profile page
**When** I change my display name and save
**Then** my display name shall be updated
**And** a success notification shall appear in Chinese

### AC-3: Update Email
**Given** I am on the Profile page / email management page
**When** I change my email to an address not already in use and save
**Then** my email shall be updated
**And** my login username shall remain aligned with the email address
**And** a confirmation / success message shall be displayed in Chinese

### AC-4: Reject Duplicate Email
**Given** I am on the Profile page / email management page
**When** I change my email to one already registered by another user and save
**Then** I shall see a validation error `该邮箱已被注册`
**And** my email shall remain unchanged

### AC-5: Change Password
**Given** I am on the Change Password section of the Profile page
**When** I enter my current password, a new password meeting complexity requirements, and confirm the new password
**Then** my password shall be updated
**And** I shall remain logged in with my current session

### AC-6: Reject Incorrect Current Password
**Given** I enter an incorrect current password in the Change Password form
**When** I submit
**Then** I shall see the error `当前密码不正确`
**And** my password shall not be changed

## Tasks / Subtasks

- [ ] Task 1: Add Display Name Support (AC-1, AC-2)
  - [ ] 1.1 Add `DisplayName` to `ApplicationUser` in `src/dotnet/Vulgata.Web/Data/ApplicationUser.cs`
  - [ ] 1.2 Generate the required Identity migration files for the new `DisplayName` column under `src/dotnet/Vulgata.Web/Data/Migrations/`
  - [ ] 1.3 Keep the column nullable or provide a safe fallback for existing users so pre-existing accounts do not break on first load
  - [ ] 1.4 Use `user.DisplayName ?? user.UserName` as the initial display name fallback for existing accounts created before this story

- [ ] Task 2: Update Profile Page UI (AC-1, AC-2)
  - [ ] 2.1 Update `src/dotnet/Vulgata.Web/Components/Account/Pages/Manage/Index.razor` to edit `DisplayName` instead of `PhoneNumber`
  - [ ] 2.2 Show the current email address on the profile page as read-only context
  - [ ] 2.3 Keep the page in Chinese and aligned with Fluent UI usage already established in Stories 1.1-1.3
  - [ ] 2.4 On successful save, call `RefreshSignInAsync(user)` and show a Chinese success message
  - [ ] 2.5 Do not introduce new profile fields beyond `DisplayName` in this story

- [ ] Task 3: Replace Deferred Email-Confirmation Flow with Direct V1 Email Update (AC-3, AC-4)
  - [ ] 3.1 Update `src/dotnet/Vulgata.Web/Components/Account/Pages/Manage/Email.razor` to directly change the email instead of sending a confirmation link
  - [ ] 3.2 Update both `Email` and `UserName` together so login-by-email continues to work after an email change
  - [ ] 3.3 Reuse the existing `ChineseIdentityErrorDescriber` so duplicate-email failures surface as `该邮箱已被注册`
  - [ ] 3.4 Remove or bypass the no-op email confirmation flow for this page in V1; do not rely on `IdentityNoOpEmailSender` for the happy path
  - [ ] 3.5 Show Chinese status messages for unchanged email / successful email update / duplicate email failure

- [ ] Task 4: Tighten Change Password UX and Messages (AC-5, AC-6)
  - [ ] 4.1 Update `src/dotnet/Vulgata.Web/Components/Account/Pages/Manage/ChangePassword.razor` so incorrect current password shows the exact message `当前密码不正确`
  - [ ] 4.2 Validate the current password explicitly before attempting the password change so the incorrect-current-password path can be distinguished from confirmation-mismatch validation
  - [ ] 4.3 Localize remaining English data-annotation labels, validation messages, and success strings in the password page
  - [ ] 4.4 Keep the user signed in after a successful password change via `RefreshSignInAsync(user)`

- [ ] Task 5: Add Executable Profile Management Tests (AC-1 through AC-6)
  - [ ] 5.1 Add `tests/Vulgata.Tests/ProfileManagementTests.cs`
  - [ ] 5.2 Test that an authenticated user can load the profile page and see current display name + email
  - [ ] 5.3 Test that changing display name persists the new value
  - [ ] 5.4 Test that changing email to a unique address updates both `Email` and `UserName`
  - [ ] 5.5 Test that changing email to an existing address shows `该邮箱已被注册`
  - [ ] 5.6 Test that an incorrect current password shows `当前密码不正确` and leaves the password unchanged
  - [ ] 5.7 Test that a successful password change preserves the authenticated session and invalidates the old password

- [ ] Task 6: Manual Verification Checklist
  - [ ] 6.1 Log in and open `个人资料`
  - [ ] 6.2 Change display name and verify the success message appears in Chinese
  - [ ] 6.3 Change email to a unique address and verify login still works with the new email
  - [ ] 6.4 Attempt to change email to an existing address and verify `该邮箱已被注册`
  - [ ] 6.5 Enter an incorrect current password and verify `当前密码不正确`
  - [ ] 6.6 Change password successfully and verify the session remains active

## Dev Notes

### CRITICAL: What Already Exists — Do NOT Rebuild

Stories 1.1-1.3 already built the Identity and authenticated-shell foundation:

- `ApplicationUser` exists at `src/dotnet/Vulgata.Web/Data/ApplicationUser.cs` and currently has **no custom profile fields**
- `ApplicationDbContext` already uses the `identity` schema and startup migrations are already wired in `Program.cs`
- `MainLayout.razor` already exposes the `个人资料` link in the authenticated avatar menu
- `Manage/Index.razor`, `Manage/Email.razor`, and `Manage/ChangePassword.razor` already exist and are the intended implementation surfaces for this story
- Story 1.2 already introduced `ChineseIdentityErrorDescriber` and bcrypt-based password hashing
- Story 1.3 already added `Microsoft.AspNetCore.Mvc.Testing` and the HTTP-based test harness pattern via `LoginLogoutTests.cs`

### CRITICAL: Existing Page Behavior to Replace Carefully

Current profile-management pages are close, but not aligned with the story:

- `Manage/Index.razor` currently edits **phone number**, not display name
- `Manage/Email.razor` currently sends **confirmation links** instead of directly updating the email
- `Manage/ChangePassword.razor` still contains English labels/messages and currently reports generic identity errors instead of the exact `当前密码不正确` path

### CRITICAL: Email Update Must Keep Username in Sync

This project uses email as the login username in practice:

- registration sets `UserName = Email`
- login authenticates using the email field

Therefore, changing email must also update `UserName` to the same new value. If only `Email` changes and `UserName` does not, Story 1.3 login behavior becomes inconsistent.

### CRITICAL: Display Name Scope

Keep this story narrowly focused:

- add `DisplayName`
- update profile pages to manage `DisplayName`, `Email`, and `Password`
- do **not** add extra claims/transforms or try to re-plumb the entire nav shell to use display name unless required to keep the profile experience coherent

If the top-right avatar continues to show the identity username/email for now, that is acceptable unless the implementation naturally supports updating it without extra architecture.

### CRITICAL: Recommended File Surface

| File | Action | Reason |
|------|--------|--------|
| `src/dotnet/Vulgata.Web/Data/ApplicationUser.cs` | UPDATE | add `DisplayName` |
| `src/dotnet/Vulgata.Web/Data/Migrations/*` | CREATE / UPDATE (generated) | persist schema change |
| `src/dotnet/Vulgata.Web/Components/Account/Pages/Manage/Index.razor` | UPDATE | display name UI |
| `src/dotnet/Vulgata.Web/Components/Account/Pages/Manage/Email.razor` | UPDATE | direct email update flow |
| `src/dotnet/Vulgata.Web/Components/Account/Pages/Manage/ChangePassword.razor` | UPDATE | exact Chinese password UX |
| `tests/Vulgata.Tests/ProfileManagementTests.cs` | CREATE | executable verification |

### CRITICAL: Testing Guidance

Reuse the existing test stack already present in the repo:

- `tests/Vulgata.Tests/LoginLogoutTests.cs` demonstrates the `WebApplicationFactory<Program>` approach for authenticated HTTP flows
- `tests/Vulgata.Tests/IdentityRegistrationTests.cs` demonstrates lower-level identity/component assertions

For this story, prefer **real authenticated HTTP-level tests** where practical because profile and password changes depend on the active authenticated user and current cookie session.

### Previous Story Intelligence

- Story 1.2 established the project’s Chinese identity-error baseline and bcrypt hashing behavior; reuse those instead of adding duplicate validation logic
- Story 1.3 added login/logout tests and confirmed the auth cookie flow works end-to-end; profile-management tests should build on that existing authenticated test harness rather than inventing a new one

### Git Intelligence

Recent commits:

```
bea476f Finalize code review for story 1.3
803c1ec Fix story 1.3 review metadata
259ad03 Record review findings for story 1.3
7a60eb0 Implement story 1.3 login and logout
e00b8cf Prepare story 1.3 for development
```

### Dev Agent Record

#### Agent Model Used

{{agent_model_name_version}}

#### Debug Log References

#### Completion Notes List

#### File List