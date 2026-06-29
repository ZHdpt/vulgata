---
date: 2026-06-29
epic: 3
status: done
title: "Epic 3 Retrospective: LLM Provider & Database Connection Configuration"
project: vulgata
stories_completed: 3
---

# Epic 3 Retrospective: LLM Provider & Database Connection Configuration

## Epic Summary

Epic 3 established the LLM provider configuration infrastructure (global CRUD, encryption, connection testing) and extended it with per-system agent-type overrides and per-repository database connection configuration. All 3 stories completed, delivering the configuration foundation that Epic 4 (Pre-Scan Profiling) and Epic 5 (Scanning Pipeline) will consume.

| Story | Title | Status |
|-------|-------|--------|
| 3.1 | LLM Provider Configuration Admin | done |
| 3.2 | Per-System LLM Provider Override | done |
| 3.3 | Database Connection Configuration | done |

### Requirements Satisfied

- **FR-3.1**: Administrators configure LLM Providers at the global level (name, endpoint URL, API key, API types, default agent-type)
- **FR-3.2**: Multiple LLM Providers supported simultaneously
- **FR-3.3**: System Owners may override global default provider per agent role (Orchestrator, Worker, Chat)
- **FR-3.4**: Connection test for each configured LLM Provider
- **FR-3.5**: Database connections per Repository (connection string, type, credentials)
- **NFR-2.2**: API keys and database credentials encrypted at rest via ASP.NET Core Data Protection
- **NFR-2.4**: Database connection tools only connect to non-production/read-only environments (infrastructure in place, enforcement in Epic 5)

---

## What Went Well

### 1. Learned from Epic 2: New Management Tab, Not Monolith Expansion

The Epic 2 retrospective's top recommendation was "extract components before adding features" — `DashboardPage.razor` was at its complexity ceiling. Epic 3 delivered:

- **`LlmProviderManagementPage.razor`**: A dedicated management tab under `管理后台 → 设置`, not embedded in `DashboardPage.razor`. This is the first new management tab since the initial scaffold and proves the holy grail layout's extensibility.
- **Dashboard extensions as panel components**: Story 3.2 added `SystemLlmProviderOverridesPanel` inside the existing system detail area, and Story 3.3 added `RepositoryDatabaseConnectionPanel` in the repository detail area — both as focused extensions rather than monolithic additions.

This represents a clear architectural improvement over Epic 2's approach of piling everything into `DashboardPage.razor`.

### 2. Coordinator Pattern Applied from Day 1

Every Epic 3 story used the coordinator pattern established in Epics 1-2 from the start, not as a retrofit:

| Coordinator | Story | Purpose |
|---|---|---|
| `LlmProviderManagementCoordinator` | 3.1 | Global provider CRUD with name uniqueness, encryption, connection test delegation |
| `SystemLlmProviderOverrideCoordinator` | 3.2 | Per-system override lifecycle with visibility filtering, agent-type matching, unique constraint enforcement |
| `RepositoryDatabaseConnectionCoordinator` | 3.3 | Per-repo database connection upsert with encryption, secret preservation, connection testing |

Each coordinator centralizes cross-cutting logic (encryption, visibility checks, uniqueness validation, Chinese error mapping) that would otherwise scatter across API endpoints and Razor pages.

### 3. Encryption Pattern Unified Across Secrets

Story 3.1 introduced `ApiKeyEncryptionService` using ASP.NET Core Data Protection with a provider-specific purpose string. Stories 3.2 and 3.3 reused this pattern without duplication:

- **Provider API keys**: `ApiKeyEncryptionService` (purpose: `"Vulgata.LlmProvider.ApiKey"`)
- **Database credentials**: `DatabaseConnectionEncryptionService` (purpose: `"Vulgata.DatabaseConnection.Credentials"`)

Both services share the same `IDataProtectionProvider` foundation but use distinct purpose strings for cryptographic isolation. The pattern — encrypt on write, never expose plaintext in summaries (`HasApiKey`/`HasConnectionString`/`HasUsername`/`HasPassword` booleans only) — is now a team standard.

### 4. Connection Testing Follows the Git Validation Pattern

The Epic 2 retrospective recommended: "Epic 3's LLM provider connection testing should follow the same pattern as `IGitRemoteValidationService`." This was executed:

| Aspect | Git Validation (Epic 2) | LLM Provider Test (Epic 3) | Database Test (Epic 3) |
|--------|------------------------|---------------------------|----------------------|
| Interface | `IGitRemoteValidationService` (Core) | `LlmProviderConnectionTestService` (Web/Data) | `DatabaseConnectionTestService` (Web/Data) |
| Credential sanitization | `SanitizeSensitiveText()` | API key never in error messages | Connection string/credentials never in error messages |
| Error messages | Chinese, actionable | Chinese, actionable ("凭据无效") | Chinese, actionable ("连接测试成功/失败") |
| Testability | `UnauthorizedGitRemoteServer` (TCP mock) | `MockLlmProviderServer` (HTTP mock) | SQLite in-memory (direct) |
| Write safety | N/A (read-only `git ls-remote`) | Minimal API call (models list) | Read-only `SELECT 1` |

### 5. Authorization Model Extended Without New Mechanisms

The `ManagementAccess` policy from Epic 2 carried the full authorization load for Epic 3:

- **Global provider management**: `AdministratorOnly` policy — only administrators can create/edit/delete providers
- **System override management**: `ManagementAccess` + `GetVisibleByIdAsync` — administrators see all systems, System Owners see assigned systems only
- **Database connection management**: `ManagementAccess` + repository visibility filtering — same pattern as repository CRUD from Epic 2
- **Story 3.2 provider candidates endpoint**: `GET /api/systems/{systemId}/llm-provider-overrides/providers` — returns all global providers for selection, but write access gated by system visibility

No new authorization mechanisms were created. The policy-based approach from Epic 1 scaled cleanly through Epic 3.

### 6. SQLite Dual-Database Testing Continued

All three test files maintain the dual-database integration test pattern:

| Test File | Test Count (approximate) | Key Coverage |
|-----------|--------------------------|--------------|
| `LlmProviderConfigTests.cs` | 8+ | CRUD, encryption verification, duplicate name rejection, connection test success/failure, authorization (SystemOwner denied), page HTML validation |
| `SystemLlmProviderOverrideTests.cs` | 6+ | Admin CRUD, SystemOwner scoping, unknown provider rejection, agent-type mismatch, (SystemId, AgentType) uniqueness, effective provider resolution |
| `DatabaseConnectionConfigTests.cs` | 7+ | Create with encryption, update preserves blank secrets, single-connection constraint, connection test, authorization (hidden repo denied), page HTML validation, SQLite table migration |

All tests use `WebApplicationFactory<Program>` with SQLite overrides. The SQLite test harness surfaced at least one schema migration issue (ensuring `DatabaseConnections` table exists in the test SQLite database with proper schema) that would have been a production PostgreSQL-only oversight.

### 7. Domain Model Quality

Entities follow DDD patterns consistently:

- **`LlmProvider`**: Private constructor + public constructor with `UpdateDetails()` method. Owns `NormalizedName` for case-insensitive uniqueness. Navigation to `SystemLlmProviderOverrides` collection.
- **`SystemLlmProviderOverride`**: Static `Create()` factory method with guard clauses (Chinese error messages). Composite unique index on `(SystemId, AgentType)`.
- **`DatabaseConnection`**: Static `Create()` factory + `UpdateEncryptedDetails()` method. One-to-zero-or-one with `Repository` via unique `RepositoryId` FK.
- **`AgentType`**: Simple enum (`Orchestrator = 0, Worker = 1, Chat = 2`) — matches the architecture's three agent roles.
- **`ApiTypeFlags`**: `[Flags]` enum (`ChatCompletions = 1, Responses = 2, Messages = 4`) — composable API capability declaration.
- **`DatabaseType`**: Enum (`PostgreSQL, SqlServer, MySql, Sqlite, Oracle, Other`) — covers the target databases.

All entities use `IEntityTypeConfiguration<T>` in `Configurations/`, no data annotations, consistent with the architecture spec.

### 8. Chinese-Only UI Delivered

All three stories delivered fully Chinese UI:

- Labels: `LLM 提供商管理`, `新增提供商`, `编辑`, `测试连接`, `编排代理`, `工作代理`, `对话代理`, `数据库连接`, `数据库类型`, `连接字符串`, `用户名`, `密码`
- Error messages: `提供商名称已存在`, `LLM 提供商不存在`, `默认代理角色与提供商不匹配`, `连接测试失败`, `凭据无效`, `仓库不存在`, `系统不存在`
- Success messages: `连接测试成功`
- Status text: `使用全局默认提供商`, `已配置`, `未配置`

This satisfies UX-DR-4 (Chinese-only UI for V1) for all configuration surfaces.

---

## What Could Be Improved

### 1. ViewModel/LoadState Pattern Still Not Adopted

The architecture specifies `LoadState` enum (Idle, Loading, Loaded, Refreshing, Empty, NoResults, Error, Cancelling) and CommunityToolkit.Mvvm for ViewModels. `Vulgata.Web.ViewModels` remains a stub project.

**Epic 3 status**: `LlmProviderManagementPage.razor` and the dashboard panel components handle loading/error states ad-hoc in code-behind, same as Epic 2's `DashboardPage.razor`. No ViewModel was created for any Epic 3 page.

**Impact**: Loading spinners, error states, and empty states are handled inconsistently. The `LlmProviderManagementPage.razor` has its own loading pattern that differs from `DashboardPage.razor`'s pattern. This divergence will compound as more management tabs are added.

**Recommendation**: Epic 4's scan dashboard (FR-12.1 through FR-12.6) is the ideal forcing function for ViewModels — real-time progress updates, multiple states per component, and SignalR data binding are exactly what MVVM + LoadState excels at. Do not defer past Epic 4.

### 2. Connection Test Services in Web Project, Not Infrastructure

`LlmProviderConnectionTestService` and `DatabaseConnectionTestService` are in `src/dotnet/Vulgata.Web/Data/` rather than `src/dotnet/Vulgata.Infrastructure/`. The Epic 2 retrospective recommended "interface in Core, implementation in Infrastructure" for infrastructure services, following `IGitRemoteValidationService`.

**Current state**: `IGitRemoteValidationService` (Core) → `GitRemoteValidationService` (Infrastructure). But `LlmProviderConnectionTestService` and `DatabaseConnectionTestService` have no Core interface — they're concrete classes in the Web project, registered directly in DI.

**Impact**: These services cannot be mocked or replaced independently of the Web project. Unit testing the coordinators that depend on them requires the full Web application factory rather than isolated service tests.

**Recommendation**: Extract `ILlmProviderConnectionTestService` and `IDatabaseConnectionTestService` to Core, move implementations to Infrastructure. This is a low-risk refactor that doesn't change behavior.

### 3. DTO and File Organization Drift Persists

The Epic 2 retrospective flagged DTOs in `Vulgata.Web.Validators/` instead of `Vulgata.Shared/` and endpoints inline in `Program.cs`. Epic 3 did not address this:

- New DTOs (`LlmProviderResponse`, `SystemLlmProviderOverrideResponse`, `DatabaseConnectionSummaryResponse`) appear to be in `Vulgata.Shared` — **this is correct** and an improvement over Epic 2.
- However, API endpoints for Epic 3 remain inline in `Program.cs` rather than extracted to `Endpoints/` files.

**Impact**: `Program.cs` continues to grow with each epic. Navigation remains harder than it should be.

**Recommendation**: The endpoint extraction cleanup should happen before Epic 5 (the largest epic). A dedicated refactoring story extracting all endpoints to `Endpoints/` files would take ~1 hour and dramatically improve navigability.

### 4. FluentTreeView Still Missing

UX-DR-10 specifies Fluent TreeView with scan status dots. Epic 3's management UI still uses flat tables and lists. The left sidebar in `ManagementLayout.razor` still shows placeholder content.

**Epic 3 impact**: The existing `DashboardPage.razor` table-based UI didn't block Epic 3's features, but the scan dashboard in Epic 4/8 absolutely needs the tree view for real-time status dots (gray/green/red/pulsing).

**Recommendation**: Implement `FluentTreeView` as part of Epic 4's scan dashboard story, not later. The scan status visualization is the feature that makes the tree view necessary.

### 5. `GetEffectiveProviderAsync` Not Exercised End-to-End

Story 3.2's AC-7 specifies: "provide a stable 'effective provider' read entry point" that checks override first, then falls back to global default. The coordinator has this method, but the test `SystemLlmProviderOverrideTests.cs` covers it indirectly through API behavior rather than a dedicated test of the fallback chain.

**Impact**: Low risk currently, since no agent code consumes this yet. But as Epic 4/5 agents start calling `GetEffectiveProviderAsync`, the fallback logic needs explicit coverage.

**Recommendation**: Add a dedicated unit test for `SystemLlmProviderOverrideCoordinator.GetEffectiveProviderAsync` covering all three cases: (a) override exists → return override, (b) no override → return global default for that AgentType, (c) no override and no global default → return null/throw. Do this in the first Epic 4 story that consumes the service.

### 6. Database Connection Test Limited to SQLite

`DatabaseConnectionTestService` currently has a full implementation only for SQLite (used in integration tests). PostgreSQL, SqlServer, MySql, and Oracle test paths exist as stubs or use the generic `DbConnection` approach.

**Impact**: Connection testing for non-SQLite databases may not work correctly in production. The test infrastructure can't easily validate PostgreSQL connection testing without a running PostgreSQL instance.

**Recommendation**: Epic 4's Docker environment should include a test PostgreSQL instance for integration tests. The connection test service should be validated against a real PostgreSQL database before Epic 5's Worker Agents attempt database schema inspection (FR-4.13).

---

## Lessons Learned

### For Epic 4 (Pre-Scan Profiling)

1. **The coordinator pattern is mature and reliable**: Seven coordinators now exist across three epics, all following the same pattern (DI registration, Chinese error mapping, visibility-filtered lookups, serialized mutations). Epic 4's scan profiling coordinator should adopt this pattern without deviation.

2. **Separate management tabs work**: `LlmProviderManagementPage.razor` proved that new management features can land as dedicated tabs under `管理后台` without touching `DashboardPage.razor`. Epic 4's scan dashboard should be its own tab (`扫描` or `扫描历史`), not embedded in the system management tab.

3. **Encryption is a solved pattern**: The `ApiKeyEncryptionService` → `DatabaseConnectionEncryptionService` duplication shows the pattern is stable. Any future secrets (MCP tool API keys in Epic 5 deferred, service topology credentials in Epic 7) should follow the identical pattern: dedicated service class, distinct purpose string, boolean summary properties.

4. **Connection testing needs Core interfaces**: Don't repeat the mistake of putting connection test services in the Web project. Epic 4's CodeGraph integration (`CodeGraphCliService`) should have its interface in Core and implementation in Infrastructure from the start.

5. **Authorization scaling is proven**: The `ManagementAccess` + visibility-filtered lookup pattern has now been stress-tested across 11 stories (Epics 1-3) without needing new authorization mechanisms. Epic 4-5's scan endpoints should use the same pattern: `ManagementAccess` for the management surface, visibility-filtered data access for scoped resources.

6. **SQLite testing discipline must continue**: The dual-database pattern caught schema issues in Epic 3 just as it did in Epics 1-2. Epic 4's new entities (CodeUnit, Scan, Document, etc.) must be tested against SQLite from the first migration.

### For Epic 5 (Scanning Pipeline)

1. **Provider selection infrastructure is ready**: `LlmProviderManagementCoordinator.ListAsync()` and `SystemLlmProviderOverrideCoordinator.GetEffectiveProviderAsync()` provide the complete provider resolution chain that Worker Agents need. Agents should call `GetEffectiveProviderAsync(systemId, agentType)` — never query `LlmProviders` table directly.

2. **Database connection infrastructure is ready**: `RepositoryDatabaseConnectionCoordinator` provides decrypted connection details for FR-4.13 (schema inspection) and FR-4.14 (sample data query). Worker Agents must use this coordinator, not `DatabaseConnectionRepository` directly.

3. **Context combination from Epic 2 is the third pillar**: Together with provider selection and database connections, Epic 2's context combination service completes the "agent context trifecta" that every Worker Agent needs before processing a Code Unit.

---

## Architecture Adherence Scorecard

| Architecture Rule | Epic 3 Compliance | Notes |
|---|---|---|
| DDD entities with private constructors | ✅ Full | All 3 new entities follow the pattern |
| `IEntityTypeConfiguration<T>` mappings | ✅ Full | Configurations in `Configurations/` folder |
| No data annotations on entities | ✅ Full | All configuration in fluent API |
| Specific repositories per aggregate | ✅ Full | 3 new repository interfaces + implementations |
| Coordinator pattern for mutations | ✅ Full | 3 new coordinators follow established pattern |
| FluentValidation for input validation | ✅ Full | Validators use Chinese error messages |
| Minimal API under `/api/` prefix | ✅ Full | All endpoints follow `/api/` convention |
| ProblemDetails for error responses | ✅ Full | Consistent with Epic 1-2 |
| ASP.NET Core Data Protection for secrets | ✅ Full | Two encryption services with distinct purposes |
| Chinese-only UI (UX-DR-4) | ✅ Full | All labels, errors, status in Simplified Chinese |
| Fluent UI Blazor components | ✅ Full | `FluentDataGrid`, `FluentDialog`, `FluentTextField`, etc. |
| ViewModel/LoadState pattern | ❌ Not adopted | Still handled ad-hoc in code-behind |
| Endpoints in dedicated files | ⚠️ Partial | Inline in `Program.cs`, not `Endpoints/` |
| FluentTreeView (UX-DR-10) | ❌ Not implemented | Still using flat tables |
| Async suffix on async methods | ✅ Full | Consistent naming throughout |

---

## Test Coverage Summary

| Test File | Approximate Tests | Key Scenarios |
|-----------|-------------------|---------------|
| `LlmProviderConfigTests.cs` | 8 | CRUD lifecycle, encryption verification, duplicate name (create + update), connection test success/failure with mock HTTP, SystemOwner authorization denial, management page HTML |
| `SystemLlmProviderOverrideTests.cs` | 6 | Admin full CRUD, SystemOwner scoped access, unknown provider rejection, agent-type mismatch validation, (SystemId, AgentType) uniqueness constraint, hidden system denial |
| `DatabaseConnectionConfigTests.cs` | 7 | Create with encryption, update preserves blank secrets, single-connection uniqueness, connection test success, hidden repository authorization denial, management page HTML, SQLite schema migration |

**Total**: ~21 integration tests across 3 test files, all running against SQLite via `WebApplicationFactory<Program>`.

---

## Look Ahead: Epic 4 (Pre-Scan Profiling)

Epic 4 is the gate to the scanning pipeline. Epic 3 has delivered all three configuration pillars it needs:

1. **LLM Provider selection** (3.1 + 3.2): The profiler will need an LLM to analyze code structure. `GetEffectiveProviderAsync` provides the correct provider per system and agent role.
2. **Database connections** (3.3): When profiling repositories with database dependencies, the profiler can inspect schemas to inform Code Unit boundary decisions.
3. **Context combination** (2.5): The profiler's prompt will include the combined `全局 → 系统 → 仓库` context.

Key risks for Epic 4:

- **CodeGraph integration**: The profiler depends on CodeGraph CLI for deterministic code unit extraction. This is the first subsystem that shells out to CodeGraph — the `CodeGraphCliService` needs the same infrastructure service pattern (Core interface, Infrastructure implementation, sanitized error output) as `GitRemoteValidationService`.
- **Magentic spike**: FR-4.11 (Week 1 validation spike) is the go/no-go gate for the entire agent orchestration architecture. Epic 3's provider configuration must support whatever provider the spike needs.
- **LoadState adoption opportunity**: The scan dashboard (FR-12.x in Epic 8, but profiler progress will be visible) is the perfect forcing function for ViewModel + LoadState. Don't defer this past Epic 4.

---

## Open Technical Debt from Epic 2 (Still Open)

| Item | Status in Epic 3 |
|------|-----------------|
| FluentTreeView not implemented | Still open — Epic 4 forcing function |
| ViewModel/LoadState not adopted | Still open — Epic 4/8 forcing function |
| DTO locations drifted | Partially improved — new DTOs in `Shared/`, but old drift not fixed |
| Endpoints inline in `Program.cs` | Still open — new endpoints also inline |
| `CreateAsync` race condition (system repos) | Still open — not Epic 3 scope |
| Left sidebar placeholder content | Still open |

---

*Generated: 2026-06-29 | Stories: 3 | Requirements: 5 FRs + 2 NFRs satisfied*
