---
baseline_commit: f9d4fa9eb762752e5bd461ba9fe51512023fa95
---

# Story 1.3: Login & Logout

Status: ready-for-dev

## Story

As a **registered user**,
I want to log in with my email and password and securely log out,
so that I can access my authorized features and protect my session.

## Acceptance Criteria

### AC-1: Successful Login
**Given** I have a registered account
**When** I enter correct email and password on the login page
**Then** I shall be authenticated via ASP.NET Core cookie authentication
**And** I shall be redirected to the Chat page ("/", default landing per UX-DR-5)

### AC-2: Failed Login — Generic Error
**Given** I enter incorrect credentials
**When** I submit the login form
**Then** I shall see the error "邮箱或密码错误" (Invalid email or password)
**And** I shall NOT be authenticated
**And** the error shall NOT reveal whether the email or password was wrong (security best practice)

### AC-3: Logout
**Given** I am logged in
**When** I click "退出登录" (Logout) from the avatar dropdown
**Then** my authentication cookie shall be cleared via `SignInManager.SignOutAsync()`
**And** I shall be redirected to the login page (`/Account/Login`)

### AC-4: Authenticated Session Persistence
**Given** I am logged in
**When** my authentication cookie remains valid
**Then** navigating to any page shall maintain my authenticated session
**And** the top navbar shall display: brand "Vulgata", "对话" nav link, "管理后台" nav link (if Administrator or SystemOwner role), bell icon, and user avatar dropdown with "个人资料" and "退出登录"

### AC-5: Protected Route Redirect
**Given** I attempt to access a page requiring authentication while logged out
**When** I navigate to a protected route (e.g., "/", "/management")
**Then** I shall be redirected to the login page (`/Account/Login`) with a `returnUrl` query parameter
**And** after successful login, I shall be redirected to the original target URL

### AC-6: Login Page UX
**Given** the login page
**When** rendered
**Then** the UI shall use Fluent UI Blazor components (FluentTextField for email, a password input for password, FluentButton for submit)
**And** the submit button shall use `Appearance.Accent` (brand primary button)
**And** all labels and placeholders shall be in Chinese (Simplified)
**And** the page shall NOT display Passkey, External Login, or "重新发送邮箱确认" sections (not in V1 scope)
**And** the page shall display "忘记密码？" and "注册新用户" links

## Tasks / Subtasks

- [ ] Task 1: Fix Login Error Message (AC-2)
  - [ ] 1.1 In `Login.razor`, change error message from `"错误：登录失败，请检查邮箱或密码。"` to `"邮箱或密码错误"`
  - [ ] 1.2 Verify the error is displayed ONLY after a failed password login attempt (not on initial GET render, not on passkey errors)
  - [ ] 1.3 Ensure the error message does NOT distinguish between "email not found" vs "wrong password" — always the same generic message

- [ ] Task 2: Clean Up Login Page UI (AC-1, AC-6, UX-DR-4)
  - [ ] 2.1 Remove `PasskeySubmit` component and all passkey-related razor markup from `Login.razor`
  - [ ] 2.2 Remove `PasskeyInputModel Passkey` property from `InputModel`
  - [ ] 2.3 Remove `PasskeySignInAsync` code path from `LoginUser()` — simplify to only `PasswordSignInAsync`
  - [ ] 2.4 Remove `ExternalLoginPicker` component and the entire "使用其他方式登录" section
  - [ ] 2.5 Remove "重新发送邮箱确认" link (not needed — `RequireConfirmedAccount = false` in V1)
  - [ ] 2.6 Replace Bootstrap `form-floating` / `InputText` with Fluent UI components:
    - Email field: `FluentTextField` with `type="email"`, `autocomplete="username webauthn"`, placeholder "name@example.com"
    - Password field: `FluentTextField` with `type="password"`, `autocomplete="current-password"`, placeholder "密码"
    - Retain `InputCheckbox` for "记住我" (checkbox is fine as-is)
  - [ ] 2.7 Verify `Appearance.Accent` on the submit button matches brand primary button spec (per DESIGN.md)
  - [ ] 2.8 Verify page title is "登录" and headings use Chinese labels
  - [ ] 2.9 Retain the "忘记密码？" and "注册新用户" links (functional per template scaffolding)
- [ ] 2.10 Do NOT remove `PasskeyCreationOptions` and `PasskeyRequestOptions` endpoints — they are still used by `Manage/Passkeys.razor` (passkey creation in profile management, Story 1.4 scope). Only remove the passkey sign-in code path from `Login.razor`.
- [ ] 2.11 Do NOT delete `PasskeySubmit.razor` — it is also used by `Manage/Passkeys.razor` and `App.razor` loads its JS module. Keep the component; only remove the `<PasskeySubmit>` usage from `Login.razor`.

- [ ] Task 3: Fix Logout Redirect to Login Page (AC-3)
  - [ ] 3.1 In `MainLayout.razor`, update the logout form: change `ReturnUrl` from `_currentUrl` to a fixed redirect to `Account/Login`
  - [ ] 3.2 Verify the `/Account/Logout` endpoint (`IdentityComponentsEndpointRouteBuilderExtensions.cs`) calls `SignInManager.SignOutAsync()` and then redirects
  - [ ] 3.3 Confirm no stale cookie remains after logout (test by accessing a protected page after logout)

- [ ] Task 4: Verify Authenticated Navbar Elements (AC-4)
  - [ ] 4.1 Verify `MainLayout.razor` displays all required elements for authenticated users: "Vulgata" brand, "对话", "管理后台" (if Admin/SystemOwner), bell icon, user avatar
  - [ ] 4.2 Verify `<AuthorizeView Roles="Administrator,SystemOwner">` wraps "管理后台" nav link correctly
  - [ ] 4.3 Verify "退出登录" form in avatar dropdown posts to `Account/Logout` with AntiforgeryToken
  - [ ] 4.4 Verify "登录" link is displayed for unauthenticated users (NotAuthorized template)
  - [ ] 4.5 Verify the avatar dropdown preserves current URL tracking for redirect after login (`_currentUrl` field used for ReturnUrl on login link)

- [ ] Task 5: Verify Protected Route Redirect (AC-5)
  - [ ] 5.1 Verify `RedirectToLogin.razor` includes `returnUrl` in the redirect: `Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}`
  - [ ] 5.2 Verify `[Authorize]` attribute is applied to all protected pages:
    - `ChatPage.razor` at route "/"
    - `DashboardPage.razor` at route "/management"
    - All Management sub-pages (`GraphPage`, `DocumentsPage`, `ScanHistoryPage`, `SettingsPage`)
  - [ ] 5.3 Verify `Login.razor` correctly reads `ReturnUrl` from query string and passes it to `IdentityRedirectManager.RedirectTo(ReturnUrl ?? "/")` on success
  - [ ] 5.4 Verify `NotFound.razor` has appropriate auth handling (no redirect loop for unauthenticated users on 404s)

- [ ] Task 6: Add Login/Logout Tests (AC-1, AC-2, AC-3, AC-5)
  - [ ] 6.1 Add integration test class `LoginLogoutTests.cs` in `tests/Vulgata.Tests/`
  - [ ] 6.2 Test: `Login_WithValidCredentials_RedirectsToChatPage` — create user via factory, POST login form, assert 302 redirect to "/"
  - [ ] 6.3 Test: `Login_WithInvalidPassword_ShowsGenericError` — POST login form with wrong password, assert "邮箱或密码错误" in response, assert NOT authenticated (no auth cookie)
  - [ ] 6.4 Test: `Login_WithNonexistentEmail_ShowsSameGenericError` — POST login form with unregistered email, assert same "邮箱或密码错误" (no email enumeration)
  - [ ] 6.5 Test: `Logout_ClearsCookieAndRedirectsToLogin` — login, then POST logout, assert redirect to "/Account/Login", assert auth cookie cleared
  - [ ] 6.6 Test: `ProtectedRoute_Unauthenticated_RedirectsToLogin` — GET "/" without auth cookie, assert redirect to "/Account/Login?returnUrl=%2F"
  - [ ] 6.7 Test: `Login_WithReturnUrl_RedirectsToOriginalTarget` — POST login with returnUrl, assert redirect to original URL
  - [ ] 6.8 Use the same test infrastructure pattern established in Story 1.2 (`IdentityRegistrationTests.cs` as reference) for component-level unit tests where applicable. For HTTP-level tests (cookie assertion, redirect verification), use `WebApplicationFactory<Program>` with `AllowAutoRedirect = false` as shown in the code examples above. Note: Story 1.2 used component-level tests with test doubles because registration tests don't require real HTTP cookies; login/logout tests DO require real HTTP for cookie round-trips.

## Dev Notes

### CRITICAL: What Story 1.1 & 1.2 Already Built — Do NOT Rebuild

Story 1.1 scaffolded the complete Identity infrastructure. Story 1.2 enhanced it with bcrypt and Chinese errors. This story **polishes** the login/logout UX — do NOT recreate:

- **`Login.razor`** — exists at `Vulgata.Web/Components/Account/Pages/Login.razor`. Already has functional login via `SignInManager.PasswordSignInAsync`, Chinese labels ("登录", "邮箱", "密码", "记住我"), FluentButton with `Appearance.Accent`, `ReturnUrl` parameter handling, and error display via `StatusMessage`. This story CLEANS UP this page by removing passkey/external-login sections and replacing Bootstrap inputs with Fluent UI.

- **`MainLayout.razor`** — exists at `Vulgata.Web/Components/Layout/MainLayout.razor`. Already has the full shell: brand, "对话"/"管理后台" nav (with AuthorizeView for Admin/SystemOwner), bell icon, avatar dropdown with "个人资料" and "退出登录" form (posts to `Account/Logout`), "登录" link for unauthenticated users. This story UPDATES the logout form to redirect to `/Account/Login`.

- **`RedirectToLogin.razor`** — exists at `Vulgata.Web/Components/Account/Shared/RedirectToLogin.razor`. Already redirects unauthenticated users to `Account/Login?returnUrl=...`. No changes needed — just verify.

- **`IdentityComponentsEndpointRouteBuilderExtensions.cs`** — exists. Already has `/Account/Logout` POST endpoint that calls `SignInManager.SignOutAsync()` and redirects. No changes needed — the passkey endpoints (`PasskeyCreationOptions`, `PasskeyRequestOptions`) stay in place because `Manage/Passkeys.razor` still uses them for profile passkey creation (Story 1.4). The external login endpoint (`PerformExternalLogin`) has no passkey-related code and stays as-is.

- **`Program.cs`** — Identity pipeline is fully configured. No changes needed. Do NOT touch: `AddIdentityCore<ApplicationUser>`, `AddEntityFrameworkStores<ApplicationDbContext>`, `AddSignInManager`, bcrypt registration, `AddErrorDescriber<ChineseIdentityErrorDescriber>`, `AddAuthentication().AddIdentityCookies()`.

- **`ApplicationUser`** — exists at `Vulgata.Web/Data/ApplicationUser.cs`. No changes needed.

- **`ApplicationDbContext`** — exists. Identity schema `identity`, separate migration history table. No changes needed.

- **`IdentityRegistrationTests.cs`** — exists at `tests/Vulgata.Tests/IdentityRegistrationTests.cs`. Reference this for test infrastructure patterns (component-level unit tests with test doubles, test naming conventions, bcrypt hasher verification). Note: Story 1.2 used component-level tests because registration doesn't need real HTTP cookies. This story's login/logout tests should use `WebApplicationFactory<Program>` for real HTTP cookie round-trips.

### CRITICAL: Login Page — EXACT Changes Required

**Current state (before this story):**
```razor
@page "/Account/Login"
@using System.ComponentModel.DataAnnotations
@using Microsoft.AspNetCore.Authentication
@using Microsoft.AspNetCore.Identity
@using Vulgata.Web.Data

@inject UserManager<ApplicationUser> UserManager
@inject SignInManager<ApplicationUser> SignInManager
@inject ILogger<Login> Logger
@inject NavigationManager NavigationManager
@inject IdentityRedirectManager RedirectManager

<PageTitle>登录</PageTitle>
<h1>登录</h1>
<!-- Bootstrap form-floating layout with InputText -->
<!-- PasskeySubmit component -->
<!-- ExternalLoginPicker component -->
<!-- Links: ForgotPassword, Register, ResendEmailConfirmation -->
```

**Target state (after this story):**
- Remove `@using Microsoft.AspNetCore.Authentication` (only needed for ExternalScheme signout in OnInitializedAsync — remove that too)
- Remove passkey code: `PasskeySubmit`, `Input.Passkey`, `PasskeySignInAsync` branch in `LoginUser()`, `PasskeyInputModel? Passkey` property in `InputModel`
- Remove external login: `ExternalLoginPicker` component, "使用其他方式登录" heading
- Remove `HttpContext.SignOutAsync(IdentityConstants.ExternalScheme)` in `OnInitializedAsync` (only needed for external login flow)
- Remove "重新发送邮箱确认" link
- Replace Bootstrap `InputText` with Fluent UI `FluentTextField` (email `type="email"`, password `type="password"`)
- Keep: "忘记密码？" link, "注册新用户" link, "记住我" checkbox
- Update error message: `"邮箱或密码错误"` (NOT `"错误：登录失败，请检查邮箱或密码。"`)

**Simplified `LoginUser()` method:**
```csharp
public async Task LoginUser()
{
    if (!editContext.Validate())
    {
        return;
    }

    var result = await SignInManager.PasswordSignInAsync(
        Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

    if (result.Succeeded)
    {
        Logger.LogInformation("User logged in.");
        RedirectManager.RedirectTo(ReturnUrl ?? "/");
    }
    else if (result.RequiresTwoFactor)
    {
        RedirectManager.RedirectTo(
            "Account/LoginWith2fa",
            new() { ["returnUrl"] = ReturnUrl, ["rememberMe"] = Input.RememberMe });
    }
    else if (result.IsLockedOut)
    {
        Logger.LogWarning("User account locked out.");
        RedirectManager.RedirectTo("Account/Lockout");
    }
    else
    {
        errorMessage = "邮箱或密码错误";
    }
}
```

**Simplified `InputModel`:**
```csharp
private sealed class InputModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "记住我")]
    public bool RememberMe { get; set; }
}
```

### CRITICAL: Logout Redirect Fix

**Current behavior:** `MainLayout.razor` logout form passes `_currentUrl` as `ReturnUrl`, so after logout the user is redirected back to the page they were on (which would trigger `RedirectToLogin` → login page). This creates an extra redirect hop.

**Required behavior:** After logout, redirect directly to `/Account/Login`.

**Fix:**
In `MainLayout.razor`, change the logout form's hidden input:
```razor
<!-- BEFORE (current): -->
<input type="hidden" name="ReturnUrl" value="@_currentUrl" />

<!-- AFTER (required): -->
<input type="hidden" name="ReturnUrl" value="Account/Login" />
```

The `/Account/Logout` endpoint at `IdentityComponentsEndpointRouteBuilderExtensions.cs:41-46` already does:
```csharp
accountGroup.MapPost("/Logout", async (
    ClaimsPrincipal user,
    [FromServices] SignInManager<ApplicationUser> signInManager,
    [FromForm] string returnUrl) =>
{
    await signInManager.SignOutAsync();
    return TypedResults.LocalRedirect($"~/{returnUrl}");
});
```
So it will redirect to `~/Account/Login` — which is the login page. No change needed to the endpoint.

### CRITICAL: Passkey & External Login Cleanup

**Files to modify:**
- `Login.razor` — remove passkey UI and code, remove external login section, remove "重新发送邮箱确认" link
- `IdentityComponentsEndpointRouteBuilderExtensions.cs` — NO changes needed. Do NOT remove `PasskeyCreationOptions` or `PasskeyRequestOptions` — they are still used by `Manage/Passkeys.razor` for passkey creation in profile management. The external login endpoint (`PerformExternalLogin`) stays as-is (it has no passkey-related code).
- `PasskeySubmit.razor` — Do NOT delete. It is referenced by `Manage/Passkeys.razor` and `App.razor` loads `PasskeySubmit.razor.js`. Only remove the `<PasskeySubmit>` usage from `Login.razor`.

**Verified references (as of this story):**
```bash
grep -r "PasskeySubmit" src/dotnet/Vulgata.Web/
# Results:
# Login.razor — REMOVE this usage
# Manage/Passkeys.razor — KEEP (profile management, Story 1.4)
# App.razor — KEEP (loads PasskeySubmit.razor.js script)
```

### CRITICAL: Fluent UI Component Migration

Story 1.1 established Fluent UI Blazor as the primary component library. The Login page currently uses Bootstrap form controls (scaffolded by the Blazor template). This story aligns it with the component discipline:

| Current (Bootstrap) | Replace With (Fluent UI) |
|---|---|
| `<InputText @bind-Value="Input.Email" class="form-control" ...>` | `<FluentTextField @bind-Value="Input.Email" type="email" ...>` |
| `<InputText type="password" @bind-Value="Input.Password" class="form-control" ...>` | `<FluentTextField @bind-Value="Input.Password" type="password" ...>` |
| `<div class="form-floating mb-3">` wrapper | Remove — Fluent components manage their own layout |
| `<ValidationSummary class="text-danger" />` | Keep as-is (validation summary is fine) |
| `<ValidationMessage For="() => Input.Email" class="text-danger" />` | `<ValidationMessage For="() => Input.Email" class="text-danger" />` (keep as-is; Fluent UI Blazor has no FluentValidationMessage) |

**Note on FluentTextField with EditForm:** Fluent UI Blazor's `FluentTextField` works with Blazor's `EditForm` and `DataAnnotationsValidator`. The `@bind-Value` binding pattern is compatible. Keep the standard `<ValidationMessage>` component — Fluent UI Blazor v4.x does not provide a `FluentValidationMessage` component. All existing pages in the project use `<ValidationMessage class="text-danger" />`.

**Important — remove Bootstrap CSS classes:** The `form-floating`, `form-control`, `form-check-input`, `d-flex`, `flex-column`, `text-secondary`, `mx-auto`, `col-lg-*`, `col-lg-offset-*` classes are Bootstrap-specific. After migrating to Fluent UI components, remove:
- The Bootstrap column layout (`<div class="row">`, `<div class="col-lg-6">`, `<div class="col-lg-4 col-lg-offset-2">`)
- Use a simpler single-column layout with Fluent's default spacing
- Remove Bootstrap `class="row"` and related grid classes

**Simplified page structure (post-migration):**
```razor
<PageTitle>登录</PageTitle>

<h1>登录</h1>

<section>
    <StatusMessage Message="@errorMessage" />
    <EditForm EditContext="editContext" method="post" OnSubmit="LoginUser" FormName="login">
        <DataAnnotationsValidator />
        <h2>使用本地账户登录</h2>
        <hr />
        <ValidationSummary />
        
        <FluentTextField @bind-Value="Input.Email" type="email"
            autocomplete="username webauthn" placeholder="name@example.com"
            Label="邮箱" />
        <ValidationMessage For="() => Input.Email" class="text-danger" />
        
        <FluentTextField @bind-Value="Input.Password" type="password"
            autocomplete="current-password" placeholder="密码"
            Label="密码" />
        <ValidationMessage For="() => Input.Password" class="text-danger" />
        
        <div>
            <label>
                <InputCheckbox @bind-Value="Input.RememberMe" />
                记住我
            </label>
        </div>
        
        <FluentButton Type="ButtonType.Submit" Appearance="Appearance.Accent" Class="w-100">
            登录
        </FluentButton>
        
        <hr />
        <div>
            <p><a href="Account/ForgotPassword">忘记密码？</a></p>
            <p><a href="@(NavigationManager.GetUriWithQueryParameters("Account/Register", new Dictionary<string, object?> { ["ReturnUrl"] = ReturnUrl }))">注册新用户</a></p>
        </div>
    </EditForm>
</section>
```

### CRITICAL: @using Cleanup

After removing the external login section, the `OnInitializedAsync` method no longer needs to call `HttpContext.SignOutAsync(IdentityConstants.ExternalScheme)`. Remove the `@using Microsoft.AspNetCore.Authentication` directive and the entire `if (HttpMethods.IsGet(HttpContext.Request.Method))` block from `OnInitializedAsync`.

Simplified `OnInitializedAsync`:
```csharp
protected override void OnInitializedAsync()
{
    Input ??= new();
    editContext = new EditContext(Input);
}
```

### CRITICAL: Test Infrastructure Reference

Story 1.2's tests (`tests/Vulgata.Tests/IdentityRegistrationTests.cs`) established component-level unit testing patterns. This story requires **integration tests** with real HTTP because login/logout involves cookie round-trips. Use the following test infrastructure:

- **Test fixture**: Use `WebApplicationFactory<Program>` with `CustomWebApplicationFactory` pattern (NEW for this story — Story 1.2 used component-level test doubles)
- **Database**: Use a dedicated test database or in-memory provider; isolate tests
- **Service scope**: Create scope via `factory.Services.CreateScope()` to access `UserManager`, `SignInManager`, `ApplicationDbContext`
- **HttpClient**: Use `factory.CreateClient()` with `AllowAutoRedirect = false` for redirect assertion tests
- **Cookie assertion**: After login, check for `.AspNetCore.Identity.Application` cookie in response headers
- **Test naming**: `{Method}_{Scenario}_{ExpectedResult}` (consistent with Story 1.2) (e.g., `Login_WithValidCredentials_RedirectsToChatPage`)
- **Component-level tests from Story 1.2** (`IdentityRegistrationTests.cs`): Reference for naming conventions, helper patterns (`GetRepoRoot()`, `SetProperty()`), and bcrypt hasher tests only

**Test for login flow (reference pattern):**
```csharp
[Fact]
public async Task Login_WithValidCredentials_RedirectsToChatPage()
{
    // Arrange: create user via factory scope
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });
    
    var postData = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["Input.Email"] = "test@example.com",
        ["Input.Password"] = "Test@1234",
        ["Input.RememberMe"] = "false",
        ["__RequestVerificationToken"] = await GetAntiForgeryTokenAsync(client, "/Account/Login")
    });
    
    // Act
    var response = await client.PostAsync("/Account/Login", postData);
    
    // Assert
    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.Equal("/", response.Headers.Location?.OriginalString);
    Assert.True(response.Headers.Contains("Set-Cookie"));
    var cookies = response.Headers.GetValues("Set-Cookie");
    Assert.Contains(cookies, c => c.Contains(".AspNetCore.Identity.Application"));
}
```

### Architecture Compliance Checklist

| Requirement | Source | Status |
|---|---|---|
| Fluent UI Blazor components for all form UI | Architecture §UI Component Strategy | This story migrates Login page |
| Chinese-only UI labels (Simplified) | UX-DR-4, DESIGN.md | All labels already Chinese |
| Brand primary button (`Appearance.Accent`) for submit | DESIGN.md §Components | Already `Appearance.Accent` |
| `[Authorize]` on all protected routes | Architecture §Identity & Authorization | Verify in Task 5. Use role name `Administrator` (not `Admin`) — matches seeded roles and existing `MainLayout.razor` |
| Cookie-based auth via ASP.NET Core Identity | Architecture §Identity & Authorization | Already configured |
| bcrypt password hashing (from Story 1.2) | Story 1.2 | Already implemented |
| Chinese Identity error messages (from Story 1.2) | Story 1.2 | Already configured |
| No data annotations on domain entities | Architecture §Format Patterns | N/A (Identity is infra, not domain) |
| `dotnet format` compliance | Architecture §Enforcement | Verify before commit |
| Test naming: `{Method}_{Scenario}_{ExpectedResult}` | Architecture §Testing | Follow in Task 6 |

### Dependencies on Previous Stories

| Story | Dependency | Impact |
|---|---|---|
| 1.1 | Identity infrastructure (ApplicationUser, ApplicationDbContext, Program.cs config, MainLayout, Login.razor scaffold) | Foundation — this story polishes what 1.1 built |
| 1.2 | bcrypt password hasher (NFR-2.1), ChineseIdentityErrorDescriber (Chinese error messages) | Login uses bcrypt via SignInManager → BcryptPasswordHasher; Chinese errors for validation |

### Important UX Requirements for Login/Logout

From `DESIGN.md` and `EXPERIENCE.md`:

- **UX-DR-4**: Chinese-only UI (Simplified). All labels, buttons, placeholders, error messages in Chinese. ✓
- **UX-DR-5**: Chat-first — login lands at "/" (ChatPage). ✓ (ReturnUrl defaults to "/")
- **UX-DR-19**: Component discipline — Fluent UI defaults for 90%. Brand overrides only for primary buttons (send/scan/save). ✓
- **DESIGN.md Components**: Primary Button uses `{colors.primary}` fill (#445E7A), `{colors.primary-foreground}` text (#FFFFFF), `{rounded.md}` corner. The `FluentButton Appearance="Appearance.Accent"` should be verified to use these tokens.

### Files to Modify

| File | Action | Reason |
|---|---|---|
| `Vulgata.Web/Components/Account/Pages/Login.razor` | MODIFY | Clean up UI, remove passkey/external login, migrate to Fluent UI |
| `Vulgata.Web/Components/Layout/MainLayout.razor` | MODIFY | Change logout redirect from current URL to `/Account/Login` |
| `tests/Vulgata.Tests/LoginLogoutTests.cs` | CREATE | New integration tests for login/logout flow |

### Dev Agent Record

### Agent Model Used

GitHub Copilot (deepseek-v4-pro)

### Debug Log References

### Completion Notes List

### File List
