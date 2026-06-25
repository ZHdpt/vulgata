# Story 1.1: Solution Scaffolding & Docker Deployment

Status: ready-for-dev

## Story

As a **developer**,
I want a greenfield Blazor Web App solution with docker-compose, PostgreSQL 17, and the 6-project structure,
so that the team has a deployable foundation for all subsequent epics.

## Acceptance Criteria

### AC-1: Solution Builds
**Given** the solution is scaffolded
**When** the developer runs `dotnet build`
**Then** all 6 projects (Vulgata.Web, Vulgata.Web.ViewModels, Vulgata.Core, Vulgata.Infrastructure, Vulgata.Agents, Vulgata.Shared) shall compile without errors
**And** placeholder directories shall exist at solution root for future Java (`src/java/`), Python (`src/python/`), and Node (`src/node/`) projects

### AC-2: Blazor Web App Template
**Given** the solution uses the Blazor Web App template with ASP.NET Core Identity (Individual Accounts)
**When** the developer inspects the project
**Then** Vulgata.Web shall use interactive server render mode with Fluent UI Blazor as the primary component library
**And** the default Identity pages (Login, Register, Manage) shall be scaffolded with Fluent UI styling

### AC-3: Layouts & Routing
**Given** the solution
**When** configured
**Then** two top-level layouts shall exist: MainLayout (top navbar: brand + 对话/管理后台 nav + bell + avatar) and ManagementLayout (nested holy grail: top tab bar + left sidebar + main content)
**And** the default route "/" shall render the Chat page

### AC-4: Docker Compose
**Given** `docker-compose.yml` exists at solution root
**When** `docker compose up` is executed
**Then** two containers shall start: the Blazor application and PostgreSQL 17
**And** the Blazor container shall have Git and CodeGraph CLI installed in its runtime stage
**And** the Blazor container shall run as a non-root user
**And** a `pgdata` named volume shall persist PostgreSQL data

### AC-5: Multi-Stage Dockerfile
**Given** the Dockerfile
**When** the image is built
**Then** it shall use multi-stage build (SDK stage → publish → runtime stage)
**And** a `./prompts` bind mount shall be configured for prompt file iteration during development

### AC-6: Code Quality Enforcement
**Given** the solution
**When** the developer runs `dotnet format`
**Then** an `.editorconfig` shall enforce naming conventions (async suffix `Async`, `_camelCase` private fields, PascalCase tables/columns)
**And** Roslyn analyzers shall cover patterns `.editorconfig` cannot express

## Tasks / Subtasks

- [ ] Task 1: Solution Scaffolding (AC-1, AC-2)
  - [ ] 1.1 Create solution root structure: `.gitignore`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`
  - [ ] 1.2 Scaffold Vulgata.Web via `dotnet new blazor` with Interactive Server + Individual Accounts
  - [ ] 1.3 Create project stubs for Vulgata.Web.ViewModels, Vulgata.Core, Vulgata.Infrastructure, Vulgata.Agents, Vulgata.Shared
  - [ ] 1.4 Wire project references: Web → all others; Infrastructure → Core; Agents → Core + Infrastructure; Shared → standalone; ViewModels → Shared
  - [ ] 1.5 Add placeholder directories: `src/java/`, `src/python/`, `src/node/` (each with `.gitkeep`)
  - [ ] 1.6 Verify `dotnet build` succeeds for all projects

- [ ] Task 2: Identity & Database Setup (AC-2)
  - [ ] 2.1 Configure ASP.NET Core Identity: `ApplicationUser : IdentityUser` in Vulgata.Web
  - [ ] 2.2 Create `ApplicationDbContext` for Identity with dedicated schema (`identity`)
  - [ ] 2.2 Create `VulgataDbContext` stub in Vulgata.Infrastructure with domain schema (`vulgata`)
  - [ ] 2.3 Configure PostgreSQL via Npgsql in Program.cs: connection string from environment/config
  - [ ] 2.4 Set up `MigrateAsync()` call at startup for both DbContexts (separate migration history tables)
  - [ ] 2.5 Scaffold Identity pages with Fluent UI styling (Login, Register, Manage)

- [ ] Task 3: Fluent UI & Brand Configuration (AC-2, UX)
  - [ ] 3.1 Add NuGet packages: Microsoft.FluentUI.AspNetCore.Components (latest stable), CommunityToolkit.Mvvm (latest stable)
  - [ ] 3.2 Configure Fluent UI in Program.cs: `builder.Services.AddFluentUIComponents()`
  - [ ] 3.3 Create `wwwroot/css/brand.css` with brand tokens: primary `#445E7A` / `#6A83A2`, accent `#B98B6B` / `#D4A88C`
  - [ ] 3.4 Add Noto Serif SC font import (Google Fonts or self-hosted)
  - [ ] 3.5 Apply brand colors to Fluent UI design tokens via `FluentUITheme` configuration

- [ ] Task 4: Layouts & Shell (AC-3, UX-DR-5)
  - [ ] 4.1 Build MainLayout.razor: top navbar (brand "Vulgata" + 对话/管理后台 nav links + bell icon + user avatar dropdown)
  - [ ] 4.2 Build ManagementLayout.razor: nested holy grail — top tab bar (系统管理 | 图谱 | 文档 | 扫描历史 | 设置) + left sidebar placeholder + main content `@Body`
  - [ ] 4.3 Create ChatPage.razor stub at route "/" with Noto Serif SC empty-state greeting
  - [ ] 4.4 Create DashboardPage.razor stub at route "/management" with system tree placeholder
  - [ ] 4.5 Configure routing in App.razor / Routes.razor
  - [ ] 4.6 Add `[Authorize]` directives on management routes

- [ ] Task 5: Docker & Deployment (AC-4, AC-5)
  - [ ] 5.1 Create multi-stage `Dockerfile` in `docker/`: SDK build → publish → runtime (aspnet:10.0)
  - [ ] 5.2 Install Git and CodeGraph CLI in runtime stage via apt-get/curl
  - [ ] 5.3 Configure `USER app` (non-root)
  - [ ] 5.4 Create `docker-compose.yml` at solution root: Blazor app service + PostgreSQL 17 service
  - [ ] 5.5 Configure `pgdata` named volume for PostgreSQL persistence
  - [ ] 5.6 Configure `./prompts` bind mount for prompt iteration
  - [ ] 5.7 Add PostgreSQL health check dependency for app container
  - [ ] 5.8 Create `.dockerignore` excluding bin/obj/node_modules/.git

- [ ] Task 6: Code Quality (AC-6)
  - [ ] 6.1 Write `.editorconfig` rules: `dotnet_naming_rule.async_suffix`, `_camelCase` for private fields, PascalCase for types
  - [ ] 6.2 Enable Roslyn analyzers: `AnalysisMode`, CA rules for naming/style
  - [ ] 6.3 Verify `dotnet format` runs clean on the scaffolded solution

## Dev Notes

### CRITICAL: Project Structure — EXACT LAYOUT REQUIRED

```
vulgata/
├── src/
│   ├── dotnet/
│   │   ├── Vulgata.Web/                 # Blazor Web App (UI host + SignalR + Identity)
│   │   │   ├── Components/
│   │   │   │   ├── Layout/
│   │   │   │   │   ├── MainLayout.razor
│   │   │   │   │   └── ManagementLayout.razor
│   │   │   │   └── Pages/
│   │   │   │       ├── ChatPage.razor           # route "/"
│   │   │   │       └── Management/
│   │   │   │           └── DashboardPage.razor   # route "/management"
│   │   │   ├── wwwroot/
│   │   │   │   └── css/
│   │   │   │       └── brand.css
│   │   │   ├── Program.cs
│   │   │   ├── appsettings.json
│   │   │   └── Vulgata.Web.csproj
│   │   ├── Vulgata.Web.ViewModels/      # MVVM ViewModels (CommunityToolkit.Mvvm)
│   │   │   └── Vulgata.Web.ViewModels.csproj
│   │   ├── Vulgata.Core/                # Domain layer (DDD entities, value objects, domain services)
│   │   │   ├── DomainServices/           # Repository interfaces live here
│   │   │   └── Vulgata.Core.csproj
│   │   ├── Vulgata.Infrastructure/      # Persistence (EF Core), Git, CodeGraph, LLM clients
│   │   │   ├── Data/
│   │   │   │   ├── VulgataDbContext.cs
│   │   │   │   └── Configurations/       # IEntityTypeConfiguration<T> classes
│   │   │   └── Vulgata.Infrastructure.csproj
│   │   ├── Vulgata.Agents/              # MAF agent definitions, workflows, prompts
│   │   │   ├── Prompts/                  # Embedded prompt resources
│   │   │   └── Vulgata.Agents.csproj
│   │   └── Vulgata.Shared/              # DTOs, contracts, constants, LoadState enum
│   │       ├── LoadState.cs
│   │       └── Vulgata.Shared.csproj
│   ├── java/                            # Future Java projects (placeholder)
│   │   └── .gitkeep
│   ├── python/                          # Future Python projects (placeholder)
│   │   └── .gitkeep
│   └── node/                            # Future Node.js projects (placeholder)
│       └── .gitkeep
├── docker/
│   └── Dockerfile                       # Multi-stage: SDK build → runtime + git + codegraph
├── prompts/                             # Externalized agent prompt files (Docker volume mount)
│   └── .gitkeep
├── Vulgata.sln
├── docker-compose.yml
├── .dockerignore
├── .editorconfig
└── .gitignore
```

### CRITICAL: Project Dependencies

| Project | References | Package Dependencies (minimum) |
|---------|-----------|-------------------------------|
| Vulgata.Shared | None | None |
| Vulgata.Core | None | None |
| Vulgata.Infrastructure | Vulgata.Core | Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore.Design |
| Vulgata.Web.ViewModels | Vulgata.Shared | CommunityToolkit.Mvvm |
| Vulgata.Agents | Vulgata.Core, Vulgata.Infrastructure | Microsoft.Agents.AI.Foundry (prerelease, defer to Spike Story), Microsoft.Agents.AI.Workflows (prerelease, defer) |
| Vulgata.Web | All 5 projects | Microsoft.FluentUI.AspNetCore.Components, Microsoft.AspNetCore.Identity.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL |

### CRITICAL: Two DbContexts — Implementation Rules

The architecture mandates two separate DbContexts sharing one PostgreSQL database:

1. **ApplicationDbContext** (in Vulgata.Web/Data/):
   - Inherits from `IdentityDbContext<ApplicationUser>`
   - Uses schema `identity` for Identity tables
   - Separate migration history table: `__IdentityMigrationsHistory`
   - Configured via `optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsHistoryTable("__IdentityMigrationsHistory"))`

2. **VulgataDbContext** (in Vulgata.Infrastructure/Data/):
   - Stub for now — domain entities added in later stories
   - Uses schema `vulgata` for domain tables
   - Default migration history table (`__EFMigrationsHistory`)
   - Register `IEntityTypeConfiguration<T>` via `modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulgataDbContext).Assembly)`

At startup in Program.cs, migrate BOTH:
```csharp
// After app is built, before app.Run():
using var scope = app.Services.CreateScope();
var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var vulgataDb = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
await identityDb.Database.MigrateAsync();
await vulgataDb.Database.MigrateAsync();
```

### CRITICAL: ApplicationUser

```csharp
// Vulgata.Web/Data/ApplicationUser.cs
public class ApplicationUser : IdentityUser
{
    // Additional profile fields added in Story 1.4
}
```

### CRITICAL: Layout Shell Requirements (UX-DR-5, UX-DR-7)

**MainLayout.razor** top navbar structure:
```
┌──────────────────────────────────────────────────┐
│ "Vulgata" | 对话 | 管理后台         🔔(铃铛) 👤(头像) │
├──────────────────────────────────────────────────┤
│ @Body                                            │
└──────────────────────────────────────────────────┘
```
- Brand text: "Vulgata"
- Nav links: "对话" (Chat), "管理后台" (Management) — use `<AuthorizeView>` to hide "管理后台" for non-admin/non-SystemOwner users
- Bell icon: placeholder for Story 8 (HITL notifications)
- User avatar: dropdown with "个人资料" (Profile) and "退出登录" (Logout)

**ManagementLayout.razor** holy grail:
```
┌──────────────────────────────────────────────────┐
│ 管理后台: 系统管理 | 图谱 | 文档 | 扫描历史 | 设置  │
├────────────┬─────────────────────────────────────┤
│ 左侧栏       │  @Body                              │
│ (placeholder)│                                      │
└────────────┴─────────────────────────────────────┘
```
- Tab bar items: "系统管理" (`/management`), "图谱" (`/management/graph`), "文档" (`/management/documents`), "扫描历史" (`/management/scan-history`), "设置" (`/management/settings`)
- Left sidebar: placeholder `<div>` for now (system tree comes in Story 2.1)
- Apply `[Authorize(Roles = "Admin,SystemOwner")]` on ManagementLayout

### CRITICAL: Routing

| Route | Component | Layout | Auth |
|-------|-----------|--------|------|
| `/` | `ChatPage.razor` | MainLayout | `[Authorize]` |
| `/management` | `DashboardPage.razor` | ManagementLayout | `[Authorize(Roles = "Admin,SystemOwner")]` |
| `/management/graph` | Placeholder stub | ManagementLayout | `[Authorize(Roles = "Admin,SystemOwner")]` |
| `/management/documents` | Placeholder stub | ManagementLayout | `[Authorize(Roles = "Admin,SystemOwner")]` |
| `/management/scan-history` | Placeholder stub | ManagementLayout | `[Authorize(Roles = "Admin,SystemOwner")]` |
| `/management/settings` | Placeholder stub | ManagementLayout | `[Authorize(Roles = "Admin,SystemOwner")]` |
| `/Account/Login` | Scaffolded Identity | MainLayout | Anonymous |
| `/Account/Register` | Scaffolded Identity | MainLayout | Anonymous |

Default redirect after login: `RedirectToPage("/")` (Chat page, per UX-DR-5: chat-first landing)

### CRITICAL: Brand CSS Tokens (UX-DR-1, UX-DR-2)

```css
/* wwwroot/css/brand.css */
:root {
    --vulgata-primary: #445E7A;
    --vulgata-primary-foreground: #FFFFFF;
    --vulgata-accent: #B98B6B;
    --vulgata-accent-foreground: #FFFFFF;

    --vulgata-bg: #F6F4F0;
    --vulgata-fg: #1D2026;
    --vulgata-surface: #FFFFFF;
    --vulgata-muted: #EDE9E3;
    --vulgata-muted-fg: #7A7A7A;
    --vulgata-border: #D9D3CA;
}

/* Dark mode — IT/技术模式 */
[data-theme="dark"] {
    --vulgata-primary: #6A83A2;
    --vulgata-accent: #D4A88C;
    --vulgata-accent-foreground: #1D2026;

    --vulgata-bg: #1D2026;
    --vulgata-fg: #F6F4F0;
    --vulgata-surface: #24272E;
    --vulgata-muted: #282B33;
    --vulgata-muted-fg: #9A9DA5;
    --vulgata-border: #383B44;
}

/* Noto Serif SC — Display typography */
@import url('https://fonts.googleapis.com/css2?family=Noto+Serif+SC&display=swap');

.display-serif {
    font-family: 'Noto Serif SC', serif;
    font-weight: 400;
}
.display-serif-lg { font-size: 32px; line-height: 1.3; }
.display-serif-sm { font-size: 22px; line-height: 1.35; }
```

**DO NOT override Fluent UI tokens globally.** Brand colors apply only to:
- Primary buttons (send/scan/save)
- Mode badge
- System/repo tags
- Selected nav items
- Display typography (empty states, greetings, section headers)

Everything else inherits Fluent defaults (UX-DR-19: Component Discipline).

### CRITICAL: Docker Compose Configuration

```yaml
# docker-compose.yml
services:
  vulgata:
    build:
      context: .
      dockerfile: docker/Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=vulgata;Username=vulgata;Password=${DB_PASSWORD:-vulgata_dev}
    volumes:
      - ./prompts:/app/prompts
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: vulgata
      POSTGRES_USER: vulgata
      POSTGRES_PASSWORD: ${DB_PASSWORD:-vulgata_dev}
    volumes:
      - pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U vulgata"]
      interval: 5s
      timeout: 5s
      retries: 5

volumes:
  pgdata:
```

### CRITICAL: Multi-Stage Dockerfile

```dockerfile
# docker/Dockerfile
# Stage 1: SDK Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish src/dotnet/Vulgata.Web/Vulgata.Web.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install Git (required for git clone at scan time)
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*

# Install CodeGraph CLI (placeholder — actual install command when available)
# RUN curl -fsSL https://... | bash

COPY --from=build /app/publish .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Vulgata.Web.dll"]
```

### CRITICAL: .editorconfig Rules

```ini
# .editorconfig
root = true

[*]
indent_style = space
indent_size = 4
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
# Async methods MUST have Async suffix
dotnet_naming_rule.async_suffix_required.severity = error
dotnet_naming_rule.async_suffix_required.symbols = async_methods
dotnet_naming_rule.async_suffix_required.style = async_suffix_style
dotnet_naming_symbols.async_methods.applicable_kinds = method
dotnet_naming_symbols.async_methods.applicable_accessibilities = *
dotnet_naming_symbols.async_methods.required_modifiers = async
dotnet_naming_style.async_suffix_style.capitalization = pascal_case
dotnet_naming_style.async_suffix_style.required_suffix = Async

# Private fields: _camelCase
dotnet_naming_rule.private_fields_camel_underscore.severity = error
dotnet_naming_rule.private_fields_camel_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_camel_underscore.style = camel_underscore_style
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.camel_underscore_style.capitalization = camel_case
dotnet_naming_style.camel_underscore_style.required_prefix = _

# Types and methods: PascalCase
dotnet_naming_rule.pascal_case.severity = error
dotnet_naming_rule.pascal_case.symbols = public_symbols
dotnet_naming_rule.pascal_case.style = pascal_case_style
dotnet_naming_symbols.public_symbols.applicable_kinds = class, struct, interface, enum, property, method
dotnet_naming_symbols.public_symbols.applicable_accessibilities = public, internal
dotnet_naming_style.pascal_case_style.capitalization = pascal_case

# Roslyn analyzers
[*.cs]
dotnet_analyzer_diagnostic.category-Style.severity = warning
dotnet_analyzer_diagnostic.category-Design.severity = suggestion
```

### CRITICAL: NuGet Package Versions

Use Central Package Management via `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.FluentUI.AspNetCore.Components" Version="4.*" />
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.*" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.*" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.*" />
  </ItemGroup>
</Project>
```

### Project References for Vulgata.Web.csproj

```xml
<ItemGroup>
    <ProjectReference Include="..\Vulgata.Core\Vulgata.Core.csproj" />
    <ProjectReference Include="..\Vulgata.Infrastructure\Vulgata.Infrastructure.csproj" />
    <ProjectReference Include="..\Vulgata.Agents\Vulgata.Agents.csproj" />
    <ProjectReference Include="..\Vulgata.Shared\Vulgata.Shared.csproj" />
    <ProjectReference Include="..\Vulgata.Web.ViewModels\Vulgata.Web.ViewModels.csproj" />
</ItemGroup>
```

### Program.cs Startup — Key Configuration

```csharp
// Vulgata.Web/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Fluent UI Blazor
builder.Services.AddFluentUIComponents();

// ASP.NET Core Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsHistoryTable("__IdentityMigrationsHistory")));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Domain DbContext
builder.Services.AddDbContext<VulgataDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CommunityToolkit.Mvvm
builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

// ProblemDetails
builder.Services.AddProblemDetails();

// Razor Pages + Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var vulgataDb = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
    await identityDb.Database.MigrateAsync();
    await vulgataDb.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

### ChatPage.razor Empty State (UX-DR-16)

```razor
@page "/"
@attribute [Authorize]

<div class="chat-empty-state">
    <h1 class="display-serif display-serif-lg" style="color: var(--vulgata-primary)">
        你好，想了解什么业务流程？
    </h1>
</div>
```

### Rules to Never Violate

1. ✅ Use `IEntityTypeConfiguration<T>` in `Configurations/` folder — NEVER data annotations on entities
2. ✅ Use `ApplyConfigurationsFromAssembly()` — NEVER register individual configs manually
3. ✅ Apply `[Authorize]` on all SignalR hubs (when added)
4. ✅ Use `Async` suffix on ALL `Task`/`Task<T>`/`ValueTask`/`ValueTask<T>` returning methods
5. ✅ Use `_camelCase` for ALL private fields
6. ✅ Use `{project-root}/src/dotnet/` for all .NET projects (not `src/` directly)
7. ✅ Chinese-only UI: ALL labels, buttons, placeholders in Chinese (UX-DR-4)
8. ❌ Do NOT add Z.Blazor.Diagrams — that's Epic 8
9. ❌ Do NOT add real SignalR hubs — placeholders OK, real hubs come in Epic 8
10. ❌ Do NOT seed roles yet — role seeding is Story 1.5
11. ❌ Do NOT add MAF packages yet — MAF spike happens before Epic 4; Agents project starts as a stub

### .gitignore Essentials

Must exclude:
- `bin/`, `obj/`, `node_modules/`
- `*.user`, `*.suo`, `.vs/`
- `appsettings.Development.json` (developer-specific)
- Docker volumes
- `.codegraph/` (if any)

### Testing

- Create `tests/Vulgata.Tests/` project (xUnit + test SDK) as a stub
- No tests required for this story — scaffolding verification via `dotnet build`
- Future stories will add tests

### References

- [Source: docs/bmad/planning-artifacts/architecture.md#Solution Structure]
- [Source: docs/bmad/planning-artifacts/architecture.md#Identity & Authorization]
- [Source: docs/bmad/planning-artifacts/architecture.md#Docker Strategy]
- [Source: docs/bmad/planning-artifacts/architecture.md#UI Component Strategy]
- [Source: docs/bmad/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules]
- [Source: docs/bmad/planning-artifacts/architecture.md#Enforcement Guidelines]
- [Source: docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md]
- [Source: docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md]
- [Source: docs/bmad/epics.md#Epic 1: Foundation & Identity]
- [Source: docs/bmad/epics.md#Story 1.1: Solution Scaffolding & Docker Deployment]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
