# Story 1.2: User Registration

Status: ready-for-dev

## Story

As a **new user**,
I want to register with my email and password,
so that I can access Vulgata.

## Acceptance Criteria

### AC-1: Successful Registration
**Given** I am on the registration page
**When** I enter a valid email and password meeting complexity requirements (minimum 8 characters, uppercase, lowercase, digit, special character)
**Then** my account shall be created
**And** my password shall be stored hashed using bcrypt (NFR-2.1)
**And** I shall be auto-signed-in and redirected to the Chat page ("/")

### AC-2: Duplicate Email Rejection
**Given** I enter an email that is already registered
**When** I submit the registration form
**Then** I shall see a validation error "该邮箱已被注册" (This email is already registered)
**And** my account shall not be created

### AC-3: Weak Password Rejection
**Given** I enter a password that does not meet complexity requirements
**When** I submit the registration form
**Then** I shall see a validation error describing the missing requirements (in Chinese)
**And** my account shall not be created

### AC-4: Schema Isolation Verification
**Given** the ApplicationDbContext schema
**When** the database is migrated
**Then** Identity tables shall reside in the `identity` schema, separate from the `vulgata` domain schema

### AC-5: Identity Errors in Chinese
**Given** any Identity operation fails with a known error code
**When** the error is returned to the UI
**Then** all Identity error messages (DuplicateEmail, InvalidEmail, PasswordTooShort, PasswordRequiresDigit, PasswordRequiresLower, PasswordRequiresUpper, PasswordRequiresNonAlphanumeric, etc.) shall be displayed in Chinese

## Tasks / Subtasks

- [ ] Task 1: Implement bcrypt Password Hasher (AC-1, NFR-2.1)
    - [ ] 1.1 Add `BCrypt.Net-Next` using centralized package management: add `<PackageVersion Include="BCrypt.Net-Next" Version="4.0.3" />` to `Directory.Packages.props` and `<PackageReference Include="BCrypt.Net-Next" />` to `Vulgata.Web.csproj`
  - [ ] 1.2 Create `BcryptPasswordHasher.cs` in `Vulgata.Web/Data/` implementing `IPasswordHasher<ApplicationUser>`
  - [ ] 1.3 Register `IPasswordHasher<ApplicationUser>` in Program.cs: `builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher<ApplicationUser>>()`
  - [ ] 1.4 Verify hash format is bcrypt (starts with `$2a$`, `$2b$`, or `$2y$`)

- [ ] Task 2: Chinese Identity Error Messages (AC-2, AC-5)
  - [ ] 2.1 Create `ChineseIdentityErrorDescriber.cs` in `Vulgata.Web/Data/` inheriting from `IdentityErrorDescriber`
  - [ ] 2.2 Override all error methods with Chinese messages:
    - `DuplicateEmail("该邮箱已被注册")`
    - `InvalidEmail("邮箱格式无效")`
    - `PasswordTooShort("密码长度不足")`
    - `PasswordRequiresDigit("密码必须包含数字")`
    - `PasswordRequiresLower("密码必须包含小写字母")`
    - `PasswordRequiresUpper("密码必须包含大写字母")`
    - `PasswordRequiresNonAlphanumeric("密码必须包含特殊字符")`
    - `PasswordMismatch("密码与确认密码不一致")`
    - `InvalidUserName`, `InvalidToken`, `DefaultError`, etc.
  - [ ] 2.3 Register in Program.cs: `builder.Services.AddIdentityCore<ApplicationUser>(...).AddErrorDescriber<ChineseIdentityErrorDescriber>()`

- [ ] Task 3: Registration Form Validation (AC-3, UX)
  - [ ] 3.1 Verify existing `InputModel` DataAnnotations in `Register.razor` produce Chinese validation messages
  - [ ] 3.2 Ensure `Password` field: `[StringLength(100, MinimumLength = 8)]` + `[DataType(DataType.Password)]`
  - [ ] 3.3 Ensure `ConfirmPassword` field: `[Compare("Password")]` with Chinese mismatch message
  - [ ] 3.4 Ensure `Email` field: `[Required]` + `[EmailAddress]` with Chinese display name
  - [ ] 3.5 Test: empty email → "邮箱字段是必需的", invalid email → "邮箱字段不是有效的电子邮件地址", short password → min-length error

- [ ] Task 4: Integration Verification (AC-4)
  - [ ] 4.1 Verify `ApplicationDbContext.OnModelCreating` sets `builder.HasDefaultSchema("identity")`
  - [ ] 4.2 Verify `VulgataDbContext.OnModelCreating` sets schema to `vulgata` or default
  - [ ] 4.3 Verify `Program.cs` runs `MigrateAsync()` for both DbContexts at startup
  - [ ] 4.4 Confirm `RequireConfirmedAccount = false` (auto-sign-in after registration, per AC-1)

- [ ] Task 5: Manual Testing Checklist
  - [ ] 5.1 Register a new user → verify auto-sign-in, redirect to Chat page
  - [ ] 5.2 Register with same email → verify "该邮箱已被注册"
  - [ ] 5.3 Register with password "abc" → verify password complexity error in Chinese
  - [ ] 5.4 Register with mismatched password/confirm → verify mismatch error in Chinese
  - [ ] 5.5 Check database: verify password hash starts with `$2a$` / `$2b$` / `$2y$`
  - [ ] 5.6 Check database: verify Identity tables are in `identity` schema, domain tables in `vulgata` schema

## Dev Notes

### CRITICAL: What Story 1.1 Already Built — Do NOT Rebuild

Story 1.1 scaffolded the complete Identity infrastructure. This story extends it — do NOT recreate:

- `ApplicationUser : IdentityUser` — exists at `Vulgata.Web/Data/ApplicationUser.cs`. No changes needed now (profile fields added in Story 1.4).
- `ApplicationDbContext : IdentityDbContext<ApplicationUser>` — exists, uses `identity` schema, separate migration history table `__IdentityMigrationsHistory`.
- `VulgataDbContext` — exists at `Vulgata.Infrastructure/Data/`, uses `vulgata` schema.
- `Register.razor` — exists at `Vulgata.Web/Components/Account/Pages/Register.razor`. Already has Fluent UI button (`FluentButton`), Chinese labels ("邮箱", "密码", "确认密码", "注册"), and the full `RegisterUser()` handler.
- `RegisterConfirmation.razor` — exists but essentially bypassed since `RequireConfirmedAccount = false`.
- `Program.cs` — Identity is fully configured: `AddIdentityCore<ApplicationUser>`, password options (RequiredLength=8, RequireDigit/RequireLower/RequireUpper/RequireNonAlphanumeric=true), `AddEntityFrameworkStores<ApplicationDbContext>`, `AddSignInManager`.
- Database migrations run at startup via `MigrateAsync()` for both DbContexts.
- `brand.css` — brand tokens already defined (`Vulgata.Web/wwwroot/css/brand.css`).

### CRITICAL: bcrypt Implementation Details

ASP.NET Core Identity defaults to PBKDF2 (HMACSHA256). This story replaces it with bcrypt.

**Package:** `BCrypt.Net-Next` (most active .NET bcrypt fork — `BCrypt.Net.BCrypt` is stale, `BCrypt.Net-Next.BCrypt` is maintained).

**Package management note:** this repo uses centralized package management via `Directory.Packages.props`. Do NOT put a `Version` attribute on the `PackageReference` in `Vulgata.Web.csproj`, or the build will fail with NU1008.

**Implementation pattern:**

```csharp
// Vulgata.Web/Data/BcryptPasswordHasher.cs
using Microsoft.AspNetCore.Identity;

namespace Vulgata.Web.Data;

public class BcryptPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    public string HashPassword(TUser user, string password)
    {
        // BCrypt.Net-Next auto-generates salt; work factor 12 is appropriate for 2026.
        return BCrypt.Net.BCrypt.EnhancedHashPassword(
            password,
            workFactor: 12,
            hashType: BCrypt.Net.HashType.SHA384);
    }

    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        if (BCrypt.Net.BCrypt.EnhancedVerify(providedPassword, hashedPassword))
            return PasswordVerificationResult.Success;
        return PasswordVerificationResult.Failed;
    }
}
```

**Registration in Program.cs** — add AFTER the `AddIdentityCore<ApplicationUser>` block:

```csharp
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher<ApplicationUser>>();
```

### CRITICAL: Chinese Error Messages

ASP.NET Core Identity errors are English by default. Override via `IdentityErrorDescriber`:

```csharp
// Vulgata.Web/Data/ChineseIdentityErrorDescriber.cs
using Microsoft.AspNetCore.Identity;

namespace Vulgata.Web.Data;

public class ChineseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "该邮箱已被注册" };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "邮箱格式无效" };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"密码长度不能少于 {length} 位" };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "密码必须包含数字" };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "密码必须包含小写字母" };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "密码必须包含大写字母" };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "密码必须包含特殊字符" };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "密码与确认密码不一致" };

    // Remaining methods for completeness
    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "用户名无效" };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "验证令牌无效" };

    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "发生未知错误，请重试" };
}
```

**Registration** — add `.AddErrorDescriber<ChineseIdentityErrorDescriber>()` to the `AddIdentityCore<ApplicationUser>` chain.

### CRITICAL: Architected Naming & Convention Rules

- File: `BcryptPasswordHasher.cs` (PascalCase, no prefix)
- File: `ChineseIdentityErrorDescriber.cs` (PascalCase)
- All public methods: PascalCase
- All private fields: `_camelCase`
- All Chinese strings: Use `"..."` literals — no resource files for V1 (UX-DR-4: no i18n)
- Async method suffix: Not applicable to these synchronous hasher/describer methods

### CRITICAL: Files to Modify vs. Create

| File | Action | Location |
|------|--------|----------|
| `Directory.Packages.props` | **UPDATE** — Add `BCrypt.Net-Next` PackageVersion entry | repo root |
| `BcryptPasswordHasher.cs` | **CREATE** | `Vulgata.Web/Data/` |
| `ChineseIdentityErrorDescriber.cs` | **CREATE** | `Vulgata.Web/Data/` |
| `Vulgata.Web.csproj` | **UPDATE** — Add `BCrypt.Net-Next` PackageReference | `src/dotnet/Vulgata.Web/` |
| `Program.cs` | **UPDATE** — Register BcryptPasswordHasher + ChineseIdentityErrorDescriber | `src/dotnet/Vulgata.Web/` |

**Files NOT touched (read-only context):**
- `Register.razor` — Form and code-behind already correct; Chinese DataAnnotation messages already present
- `RegisterConfirmation.razor` — Bypassed (auto-sign-in, no confirmation)
- `ApplicationUser.cs` — No changes (profile fields in Story 1.4)
- `ApplicationDbContext.cs` — Schema already correct
- `VulgataDbContext.cs` — Not relevant to registration
- `brand.css` — Already defined
- All layout files — Already built

### Project Structure Notes

Files relative to `src/dotnet/Vulgata.Web/`:

```
Vulgata.Web/
├── Data/
│   ├── ApplicationUser.cs                  # NO CHANGE (profile in 1.4)
│   ├── ApplicationDbContext.cs             # NO CHANGE (identity schema done)
│   ├── BcryptPasswordHasher.cs             # NEW — bcrypt IPasswordHasher<T>
│   └── ChineseIdentityErrorDescriber.cs    # NEW — Chinese IdentityErrorDescriber
├── Components/
│   └── Account/
│       └── Pages/
│           ├── Register.razor              # NO CHANGE (already Fluent UI + Chinese)
│           └── RegisterConfirmation.razor  # NO CHANGE (bypassed)
├── Program.cs                               # UPDATE — register hasher + error describer
└── Vulgata.Web.csproj                       # UPDATE — add BCrypt.Net-Next
```

### Architecture Compliance

- **Security (NFR-2.1):** bcrypt with SHA-384 and work factor 12 replaces PBKDF2
- **Security (NFR-2.2):** Not applicable — API key encryption is in Epic 3
- **Database:** Identity tables remain in `identity` schema — no change
- **UI Framework:** Fluent UI Blazor already applied to Register page in Story 1.1
- **Language (UX-DR-4):** All new strings in Chinese (Simplified)
- **Validation:** FluentValidation not yet added to solution — Story 1.2 uses Identity's built-in error describer + DataAnnotations (register FluentValidation in a future story)
- **Naming:** All new files PascalCase, all async suffixed if async
- **Logging:** ILogger already injected in Register.razor; log statement "User created a new account with password." unchanged
- **Error Handling:** Identity errors returned via `identityErrors` list rendered by `StatusMessage` component — no change needed

### Testing Requirements

- **Manual testing** sufficient for V1 of this story — no automated tests required
- Testing checklist (Task 5) covers all ACs
- Future: Integration tests in `Vulgata.Tests/` can test bcrypt hasher + Chinese error messages

### References

- [Epic 1: Foundation & Identity](docs/bmad/epics.md) — Story 1.2 requirements and ACs
- [Architecture — Security NFRs](docs/bmad/planning-artifacts/architecture.md) — NFR-2.1 bcrypt/Argon2 requirement
- [Architecture — Technology Stack](docs/bmad/planning-artifacts/architecture.md#Solution-Architecture) — .NET 10, ASP.NET Core Identity, PostgreSQL 17
- [Architecture — Project Responsibilities](docs/bmad/planning-artifacts/architecture.md#Project-Responsibilities) — Vulgata.Web hosts Identity
- [UX DESIGN.md](docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md) — Brand colors, typography, component discipline
- [UX EXPERIENCE.md](docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md) — Login/Register flow, Chinese-only UI
- [Story 1.1 Dev Notes](docs/bmad/implementation-artifacts/1-1-solution-scaffolding-and-docker-deployment.md) — What was already built; ApplicationUser, ApplicationDbContext, Program.cs Identity config, Register.razor, brand.css

## Previous Story Intelligence (Story 1.1)

**Key learnings from Story 1.1:**

- Story 1.1 was implemented cleanly — all 6 tasks completed, `dotnet build` passes, `docker compose up` works
- The `.editorconfig` rules were successfully applied; all naming conventions are enforced
- Brand CSS tokens are in `wwwroot/css/brand.css` with light/dark variants — do not duplicate or override
- Identity pages already have Chinese labels via `[Display(Name = "邮箱")]` DataAnnotations — test this before adding new error messages
- Fluent UI `FluentButton` with `Appearance="Appearance.Accent"` is used for primary actions — keep consistent
- `RequireConfirmedAccount = false` was set in Story 1.1 — registration auto-signs-in, no email confirmation needed for V1
- The `IdentityNoOpEmailSender` is registered — confirmation emails are NOT actually sent; the confirmation link is generated but never emailed

**Patterns established:**
- New Data-layer classes go in `Vulgata.Web/Data/` (for Identity concerns)
- NuGet packages are declared in `Directory.Packages.props` for centralized version management (if used)
- Program.cs uses `builder.Services.Add*` pattern consistently

## Git Intelligence

Recent commits (HEAD):
```
2c874a0 Add codegraph sync hook for copilot
4fbe454 Implement and review story 1-1
d2318cc Story 1.1: Solution Scaffolding & Docker Deployment
```

**Key observations:**
- Story 1.1 was implemented in a single commit (`4fbe454`) after the baseline scaffolding (`d2318cc`)
- The project is in a clean, buildable state
- No outstanding branches or half-done work

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
