# Architecture Deepening — Vulgata Epics 1–3 Refactor Plan

**Status:** Design
**Date:** 2026-07-07
**Scope:** Refactor code landed in Epics 1–3 (identity, system/repository domain, LLM provider & DB connection configuration) to address five architecture friction points identified in the 2026-07-07 architecture review.

## Problem statement

An architecture review of Epics 1–3 code surfaced five friction points (see `architecture-review-20260707-110157.html`):

1. **Context write logic has no home** — 6 endpoint handlers inline the same pending/immediate branching, visibility filter, and clear-on-apply contract. `IContextCompositionService` is read-only; the write side has no module.
2. **Per-request user context resolved 32×** — `AuthorizeAsync(AdministratorOnly)` + `FindFirstValue(NameIdentifier)` copy-pasted across 5 API files. Coordinators take `(string userId, bool isAdministrator)`, leaking auth through every seam.
3. **Connection-test interfaces in wrong project** — `ILlmProviderConnectionTestService`, `IDatabaseConnectionTestService` (in Web), `IGitRemoteValidationService` (in Infrastructure) sit at wrong layer. Epic 5 Worker Agents can't reach them without depending on Web.
4. **`DashboardPage.razor` monolith** — ~950 lines, 8 responsibilities, no internal seams. Tests only through HTML-string assertions.
5. **Two byte-identical encryption services** — `ApiKeyEncryptionService` and `DatabaseConnectionEncryptionService` differ only by purpose string. One adapter instantiated per-purpose, not two modules.

The three epic retrospectives (2026-06-29) flagged several of these as known issues; this plan addresses them.

## Design decisions (cross-cutting)

### Refactor depth
Deepen with new seams — introduce `IUserContext`, `IContextManagementCoordinator`, `ISecretEncryptionService`. Deeper interfaces, fewer call sites, better test surfaces. Endpoints stop owning branching logic.

### Sequencing
Four PRs, dependency-ordered:

| PR | Candidates | Dependency | Est. files |
|---|---|---|---|
| PR1 | #1 + #2 paired | None | ~15 (5 new, 10 modified) |
| PR2 | #3 | None | ~11 (3 new, 3 deleted, 5 modified) |
| PR3 | #5 | None | ~12 (3 new, 4 deleted, 5 modified) |
| PR4 | #4 | After PR1 (consumes `IUserContext`) | ~12 (11 new, 1 rewrite) |

PR1 first (top recommendation from architecture review; paired because they share the request pipeline). PR2 next (unblocks Epic 5). PR3 (quick win). PR4 last (lands with Epic 4 scan dashboard).

### Test strategy
Hybrid: keep existing `WebApplicationFactory` HTTP integration tests for end-to-end coverage (Blazor SSR quirks, auth wiring — explicitly flagged in repo memory `vulgata-test-harness`). Add unit tests at new seams (`IUserContext`, `IContextManagementCoordinator`, `ISecretEncryptionService`). Trim integration tests only where a unit test already covers the exact scenario.

### Microsoft Learn research grounding
Three findings shape the design:

1. **`ClaimsPrincipal` is a special-cased minimal API parameter** — bound automatically from `HttpContext.User`. No `[FromServices]` or `IHttpContextAccessor` needed at the seam. `IUserContext` becomes a thin adapter wrapping the principal, resolved once per request. ([Parameter Binding in Minimal API apps](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0#binding-precedence))

2. **Data Protection purpose strings are designed for the purpose-as-parameter pattern** — MS docs explicitly say "Using the namespace and type name of the component consuming the data protection APIs is a good rule of thumb." The two separate services are duplicating the framework's design, not extending it. ([Purpose strings in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/purpose-strings?view=aspnetcore-10.0))

3. **EF Core `DbContext` lifetime in Blazor Server** — scoped registrations live for the circuit, not the request. Existing coordinators inject `VulgataDbContext` directly; redesign must preserve `IDbContextFactory<>` pattern (already used by tests) rather than introduce scoped coordinators holding a context. ([ASP.NET Core Blazor with EF Core](https://learn.microsoft.com/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0))

### Vocabulary
- `IUserContext`, `IContextManagementCoordinator`, `ISecretEncryptionService`, `SecretPurposes`, `ContextScope`, `ContextState`, `SystemOwnerFilter` — infrastructure names, not domain terms. They live in code and this spec; not added to `docs/UBIQUITOUS_LANGUAGE.md`. A `CONTEXT.md` for infrastructure vocabulary will be created lazily during the grilling loop if needed.
- Domain terms (System, Repository, LLM Provider, Database Connection, Pending Context Change, Scan State) come from `docs/UBIQUITOUS_LANGUAGE.md` and are used verbatim.

---

## PR1 — Request pipeline redesign (#1 + #2 paired)

### Goal
Two new seams in the request pipeline: `IUserContext` (resolves auth once per request) and `IContextManagementCoordinator` (write side of Context).

### Seam A: `IUserContext`

**Interface** (`Vulgata.Core/DomainServices/IUserContext.cs`):
```csharp
public interface IUserContext
{
    string UserId { get; }
    bool IsAdministrator { get; }
    SystemOwnerFilter ToSystemOwnerFilter();
}
```

**Adapter** (`Vulgata.Web/RequestPipeline/UserContext.cs`):
- Constructor takes `ClaimsPrincipal` and `IAuthorizationService`.
- Resolves `UserId` from `ClaimTypes.NameIdentifier`.
- Resolves `IsAdministrator` via `AuthorizeAsync(principal, AuthorizationPolicyNames.AdministratorOnly).Succeeded`.
- Registered as **scoped** (one per HTTP request; `ClaimsPrincipal` is request-scoped).

**Endpoints**: take `ClaimsPrincipal user` (special-cased minimal API binding), pass to coordinators. The 32-copy block (`AuthorizeAsync` + `FindFirstValue`) collapses.

**Coordinators**: stop accepting `(string userId, bool isAdministrator)`. Accept `IUserContext`. Three private `GetVisibleSystemAsync(userId, isAdministrator)` helpers across coordinators collapse into one `SystemOwnerFilter` owned by `IUserContext`.

### Seam B: `IContextManagementCoordinator`

**Interface** (`Vulgata.Core/DomainServices/IContextManagementCoordinator.cs`):
```csharp
public interface IContextManagementCoordinator
{
    Task<Result> SaveAsync(ContextScope scope, string value, IUserContext user, CancellationToken ct = default);
    Task<ContextState> GetStateAsync(ContextScope scope, IUserContext user, CancellationToken ct = default);
}
```

**Supporting records** (`Vulgata.Core/DomainServices/`):
- `ContextScope` (record): `Global | System(Guid systemId) | Repository(Guid repositoryId)`
- `ContextState` (record): `CurrentValue`, `PendingValue`, `IsScanRunning`
- `SystemOwnerFilter` (record): `OwnerId?, AllowAll` — replaces 3 private helpers

**Implementation** (`Vulgata.Infrastructure/ContextManagement/ContextManagementCoordinator.cs`):
- Constructor: `IDbContextFactory<VulgataDbContext>`, `IScanStateService`, `ISystemRepository`.
- Owns: visibility filter, scan-state branching, pending row upsert, clear-on-apply contract.
- Per MS guidance: uses `IDbContextFactory<VulgataDbContext>` per operation (not a scoped `DbContext`) to avoid Blazor Server circuit-lifetime issues.

**Endpoints**: 6 handlers in `ContextApiEndpoints.cs` collapse from ~398 lines to ~150. Each becomes: parse body → `coordinator.SaveAsync(scope, value, userContext)` → map `Result` to HTTP.

**`IContextCompositionService`** (read path): unchanged — already correct.

### Files touched

| File | Change |
|---|---|
| `Vulgata.Core/DomainServices/IUserContext.cs` | new |
| `Vulgata.Core/DomainServices/IContextManagementCoordinator.cs` | new |
| `Vulgata.Core/DomainServices/ContextScope.cs`, `ContextState.cs` | new records |
| `Vulgata.Core/DomainServices/SystemOwnerFilter.cs` | new |
| `Vulgata.Web/RequestPipeline/UserContext.cs` | new adapter |
| `Vulgata.Infrastructure/ContextManagement/ContextManagementCoordinator.cs` | new |
| `Vulgata.Web/Api/Context/ContextApiEndpoints.cs` | rewrite 6 handlers (~398 → ~150 lines) |
| `Vulgata.Web/Api/Systems/SystemApiEndpoints.cs` | drop auth-resolution blocks; pass `ClaimsPrincipal` |
| `Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs` | same |
| `Vulgata.Web/Api/LlmProviders/LlmProviderApiEndpoints.cs` | same |
| `Vulgata.Web/Api/Systems/SystemLlmProviderOverrideApiEndpoints.cs` | same |
| `Vulgata.Web/Data/*Coordinator.cs` (5 files) | drop `(string userId, bool isAdministrator)`; accept `IUserContext`; delete private `GetVisible*Async` |
| `Vulgata.Web/Program.cs` | register `IUserContext` (scoped), `IContextManagementCoordinator` (scoped) |

### Internal commit structure (for reviewability)
1. Add `IUserContext` + `UserContext` adapter + `SystemOwnerFilter`; wire into DI.
2. Add `IContextManagementCoordinator` + impl; wire into DI.
3. Migrate each of 5 coordinators to accept `IUserContext` (one commit per coordinator).
4. Rewrite `ContextApiEndpoints.cs` 6 handlers to use the new coordinator.

### Test strategy
- **Unit**: `UserContextTests` (admin/non-admin resolution, userId extraction, owner filter construction). `ContextManagementCoordinatorTests` (pending-vs-immediate branching with scan running/not running, visibility filter for admin/owner/anon, clear-on-apply, FR-14.4). In-memory `IDbContextFactory` + fake `IScanStateService`.
- **Integration**: keep all 6 Context endpoint tests; add cases for admin override, owner access, non-owner 404, scan-running queue path.

### Risks
| Risk | Mitigation |
|---|---|
| `ClaimsPrincipal` binding differs between Blazor SSR and minimal APIs | Endpoints under `/api/` only; Blazor pages use `AuthenticationStateProvider` (untouched) |
| 5 coordinators change signature simultaneously | Mechanical: replace `(string, bool)` with `IUserContext`. One commit per coordinator. |
| New transactional write path in Context Coordinator | Reuse `IDbContextFactory<>` pattern; one `SaveChangesAsync` per operation; no cross-context transactions. |
| Behavior drift on visibility rules | `SystemOwnerFilter` is the only place rules live — unit tests pin all 3 cases. |

### Out of scope
- Blazor pages (use `AuthenticationStateProvider`, not minimal API `ClaimsPrincipal`).
- Pending-context flush hook (Epic 5) — coordinator is the *place* it lives; the hook itself is Epic 5 work.
- `IContextCompositionService` (read path) — already correct.

---

## PR2 — Move connection-test interfaces to Core (#3)

### Goal
Pure interface relocation. Three interfaces move to `Vulgata.Core/DomainServices/`. Implementations stay. No behavior change. Unblocks Epic 5 Worker Agents from reaching connection-test services via Core.

### Changes

**Move to `Vulgata.Core/DomainServices/`:**
- `ILlmProviderConnectionTestService` (from `Vulgata.Web/Data/`)
- `IDatabaseConnectionTestService` (from `Vulgata.Web/Data/`)
- `IGitRemoteValidationService` (from `Vulgata.Infrastructure/Git/`)

**Stay in place (implementations):**
- `LlmProviderConnectionTestService` (in `Vulgata.Web/Data/`)
- `DatabaseConnectionTestService` (in `Vulgata.Web/Data/`)
- `GitRemoteValidationService` (in `Vulgata.Infrastructure/Git/`)

### Why implementations don't move yet
The Epic 3 retrospective named only the two new services. The existing `IGitRemoteValidationService` already lives in Infrastructure — better than Web but still wrong per the project boundary rule ("Interfaces in `Vulgata.Core/DomainServices/`"). Moving all three interfaces to Core aligns with the boundary rule and unblocks Epic 5.

The implementations of the two new services stay in Web for now because:
1. They depend on `HttpClient` / `NpgsqlConnection` — already referenced from Web.
2. Moving them requires adding `Microsoft.Extensions.Http` and `Npgsql` to Infrastructure. That's a real dependency change; out of scope for this PR.
3. Epic 5's `Vulgata.Agents` needs the *interfaces* (Core-referenced) for DI contract; impls register in `Program.cs` for V1.

When Epic 5 lands and agents need to resolve these services outside Web, the impls move to Infrastructure. This PR makes the interface move (the part that unblocks the dependency graph).

### Files touched

| File | Change |
|---|---|
| `Vulgata.Core/DomainServices/ILlmProviderConnectionTestService.cs` | new (moved from Web) |
| `Vulgata.Core/DomainServices/IDatabaseConnectionTestService.cs` | new (moved from Web) |
| `Vulgata.Core/DomainServices/IGitRemoteValidationService.cs` | new (moved from Infrastructure) |
| `Vulgata.Web/Data/ILlmProviderConnectionTestService.cs` | delete |
| `Vulgata.Web/Data/IDatabaseConnectionTestService.cs` | delete |
| `Vulgata.Infrastructure/Git/IGitRemoteValidationService.cs` | delete |
| `Vulgata.Web/Data/LlmProviderConnectionTestService.cs` | update `using` (interface now in Core) |
| `Vulgata.Web/Data/DatabaseConnectionTestService.cs` | update `using` |
| `Vulgata.Infrastructure/Git/GitRemoteValidationService.cs` | update `using` |
| `Vulgata.Web/Program.cs` | DI registrations unchanged (interface type resolves via Core) |

### Test strategy
No new tests. Behavior unchanged. Existing integration tests for connection-test endpoints verify impls still resolve. Full test suite run confirms DI resolution after namespace move.

### Risks
| Risk | Mitigation |
|---|---|
| Namespace change breaks `using` directives in tests | Grep for old namespace; update all references |
| DI registration fails silently at startup | Existing integration tests construct the Web host — catches missing service registration |

---

## PR3 — Collapse encryption services (#5)

### Goal
Two byte-identical services become one. MS Data Protection docs confirm purpose-as-parameter is the framework's intended idiom — the duplication is the anomaly.

### Changes

**New** (`Vulgata.Core/DomainServices/ISecretEncryptionService.cs`):
```csharp
public interface ISecretEncryptionService
{
    Task<string> EncryptAsync(string purpose, string plain, CancellationToken ct = default);
    Task<string> DecryptAsync(string purpose, string cipher, CancellationToken ct = default);
}
```

**New** (`Vulgata.Shared/SecretPurposes.cs`):
```csharp
public static class SecretPurposes
{
    public const string LlmApiKey = "Vulgata.LlmProviders.ApiKey";
    public const string DatabaseConnection = "Vulgata.Repositories.DatabaseConnection";
}
```

**New** (`Vulgata.Web/Data/SecretEncryptionService.cs`): single impl using `IDataProtectionProvider.CreateProtector(purpose)` per call. `IDataProtector` is thread-safe; provider is a singleton; protector creation is cheap (framework caches key derivation internally).

**Delete:**
- `Vulgata.Web/Data/IApiKeyEncryptionService.cs`
- `Vulgata.Web/Data/ApiKeyEncryptionService.cs`
- `Vulgata.Web/Data/IDatabaseConnectionEncryptionService.cs`
- `Vulgata.Web/Data/DatabaseConnectionEncryptionService.cs`

### Interface location
`ISecretEncryptionService` goes in `Vulgata.Core/DomainServices/` (not Web) to:
- Align with PR2's pattern (interfaces in Core).
- Keep the interface free of Data Protection types — exposes only `string`/`Task<string>`.
- Allow Epic 5/7 secrets consumed by `Vulgata.Agents` to reach the seam via Core.

### Purpose constants
- Live in `Vulgata.Shared` (same place as `RoleNames`, `AuthorizationPolicyNames`).
- MS docs warn: "Components shouldn't allow untrusted user input to be the sole source of input for the purposes chain." Purpose is always a `SecretPurposes` constant.
- Versioning: unversioned for V1. If crypto strategy rotates in future, add `.v2` purpose + migration that reads old purpose, writes new.

### Files touched

| File | Change |
|---|---|
| `Vulgata.Core/DomainServices/ISecretEncryptionService.cs` | new interface |
| `Vulgata.Web/Data/SecretEncryptionService.cs` | new impl (single class) |
| `Vulgata.Shared/SecretPurposes.cs` | new constants |
| `Vulgata.Web/Data/IApiKeyEncryptionService.cs` | delete |
| `Vulgata.Web/Data/ApiKeyEncryptionService.cs` | delete |
| `Vulgata.Web/Data/IDatabaseConnectionEncryptionService.cs` | delete |
| `Vulgata.Web/Data/DatabaseConnectionEncryptionService.cs` | delete |
| `Vulgata.Web/Data/LlmProviderManagementCoordinator.cs` | swap to `ISecretEncryptionService`; pass `SecretPurposes.LlmApiKey` |
| `Vulgata.Web/Data/SystemLlmProviderOverrideCoordinator.cs` | same |
| `Vulgata.Web/Data/RepositoryDatabaseConnectionCoordinator.cs` | swap to `ISecretEncryptionService`; pass `SecretPurposes.DatabaseConnection` |
| `Vulgata.Web/Program.cs` | register `ISecretEncryptionService` (singleton); remove two old registrations |

### Migration / backward compat
No data migration. Existing ciphertext was encrypted with the same purpose strings; new `SecretEncryptionService` produces identical bytes for the same purpose. Existing rows decrypt without change. Verified by integration tests.

### Test strategy
- **Unit**: `SecretEncryptionServiceTests` — round-trip for each purpose; isolation between purposes (cannot decrypt with wrong purpose); throws on tampered ciphertext.
- **Integration**: existing LLM provider create/read flows and DB connection create/read flows exercise encrypt-then-decrypt end-to-end. Continue to pass unchanged.
- Delete existing unit tests for `ApiKeyEncryptionService` / `DatabaseConnectionEncryptionService` in isolation — redundant with the unified test.

### Risks
| Risk | Mitigation |
|---|---|
| Ciphertext compatibility | Purpose strings unchanged — same `CreateProtector(purpose)` call, same key derivation. Verified by integration tests. |
| Caller passes wrong purpose constant | Compile-time: parameters are `string`, not enum. Acceptable — purpose strings are internal; wrong constant shows up immediately in integration tests. |
| Future need for purpose hierarchy | Interface accepts `string` — can extend to `params string[] subPurposes` later without breaking callers. |

### ADR offer
The Epic 3 retrospective framed the two-service pattern as a virtue ("Encryption Pattern Unified Across Secrets"). PR3 reverses that judgment. When PR3 lands, offer an ADR: *"Single `ISecretEncryptionService` with purpose-as-parameter; the previous two-service pattern was duplication, not isolation."*

---

## PR4 — Decompose DashboardPage.razor (#4)

### Goal
Split ~950-line monolith into focused components. `DashboardPage` becomes a composition root; each panel earns its own test surface. Pattern proven by `LlmProviderManagementPage.razor` (Epic 3 added a tab without touching the monolith).

### Target component tree

```
Components/Pages/Management/DashboardPage.razor          (composition root, ~150 lines)
  ├── Components/Management/Systems/SystemListPanel.razor
  ├── Components/Management/Repositories/StandaloneReposPanel.razor
  ├── Components/Management/Repositories/RepositoryDetailDialog.razor
  ├── Components/Management/Databases/DatabaseConnectionPanel.razor
  └── Components/Management/Systems/SystemLlmOverridesPanel.razor
```

### Per-component contract

Each panel exposes `[Parameter]` inputs and `[Parameter] EventCallback` outputs — its interface. No shared state, no `CascadingValue` for cross-panel concerns.

| Component | Inputs (Parameters) | Outputs (EventCallbacks) |
|---|---|---|
| `SystemListPanel` | `IReadOnlyList<SystemSummary>` | `OnSystemSelected(Guid)`, `OnCreateRequested`, `OnDeleteRequested(SystemSummary)` |
| `StandaloneReposPanel` | `IReadOnlyList<RepositorySummary>` | `OnRepositorySelected(Guid)`, `OnCreateRequested`, `OnAssignRequested(Guid)` |
| `RepositoryDetailDialog` | `RepositoryDetail? Model`, `bool Visible` | `OnSaved(RepositoryDetail)`, `OnClosed` |
| `DatabaseConnectionPanel` | `Guid RepositoryId`, `DatabaseConnectionDetail? Model` | `OnSaved`, `OnTestRequested` |
| `SystemLlmOverridesPanel` | `Guid SystemId`, `IReadOnlyList<LlmProviderOverride>` | `OnOverrideAdded`, `OnOverrideRemoved(Guid)` |

`DashboardPage`:
- Injects the 5 coordinators (post-PR1, all take `IUserContext`).
- Resolves `IUserContext` once in `OnInitializedAsync`.
- Loads all summaries itself in `OnInitializedAsync` (panels do not own data loading); passes results down as `[Parameter]`.
- Routes panel events to the appropriate coordinator and refreshes the affected panel only via `@ref.RefreshAsync()`.

### Data flow

```
DashboardPage
  ├── OnInitializedAsync → IUserContext + load all summaries in parallel
  ├── SystemListPanel.OnSystemSelected(id) → load detail → show RepositoryDetailDialog
  ├── StandaloneReposPanel.OnRepositorySelected(id) → load detail → show RepositoryDetailDialog
  ├── RepositoryDetailDialog.OnSaved → call coordinator → refresh parent panel's list
  ├── DatabaseConnectionPanel.OnSaved → call coordinator → refresh panel state
  └── SystemLlmOverridesPanel.OnOverrideAdded → call coordinator → refresh panel list
```

No cross-panel state. Each panel owns its own `LoadState` (from `Shared/LoadState.cs`). `DashboardPage` orchestrates by routing events to coordinators and triggering targeted refresh on the affected panel.

### Files touched

| File | Change |
|---|---|
| `Components/Pages/Management/DashboardPage.razor` | rewrite (~950 → ~150 lines): composition root only |
| `Components/Pages/Management/DashboardPage.razor.cs` | new code-behind: data loading, event routing |
| `Components/Management/Systems/SystemListPanel.razor` | new |
| `Components/Management/Repositories/StandaloneReposPanel.razor` | new |
| `Components/Management/Repositories/RepositoryDetailDialog.razor` | new |
| `Components/Management/Databases/DatabaseConnectionPanel.razor` | new |
| `Components/Management/Systems/SystemLlmOverridesPanel.razor` | new |
| `Components/Management/Systems/SystemListPanel.razor.cs` | new code-behind |
| (same for the other 4 panels) | new code-behind |
| `tests/Vulgata.Tests/Vulgata.Tests.csproj` | add `bunit` NuGet package |

### Test strategy
- **Component-level tests** using `bunit`:
  - Render one panel in isolation with mock data.
  - Assert: given a list of 3 systems, `SystemListPanel` renders 3 rows.
  - Assert: clicking a row fires `OnSystemSelected` with the right id.
  - Assert: `LoadState.Loading` shows spinner; `LoadState.Error` shows error message.
- **Integration tests** (existing `WebApplicationFactory`):
  - Keep end-to-end "load dashboard, click system, see repos" test — catches wiring component tests miss.
  - Retire HTML-string assertions for label text (per repo memory `vulgata-test-harness`, brittle). Replace with selector-based assertions on panel-level elements.

### Risks
| Risk | Mitigation |
|---|---|
| Event routing tangles in `DashboardPage` code-behind | Keep one method per event; no cascading refreshes. Each handler calls one coordinator and refreshes one panel. |
| Panel state desyncs after concurrent edits | Each panel exposes `RefreshAsync()` called via `@ref`. `DashboardPage` triggers explicit refresh after coordinator call returns. |
| `bunit` not yet in test project | Add `bunit` NuGet package. Already compatible with xunit + `WebApplicationFactory`. |
| Existing integration tests break on DOM structure change | Update selectors in same PR — accepted cost of refactor. |

### Out of scope
- `LlmProviderManagementPage.razor` — already follows target pattern. Untouched.
- Other management pages — none exist in Epics 1–3 scope.
- Scan dashboard (Epic 4) — separate epic, separate spec.

---

## Cross-PR concerns

### No ADRs yet
`docs/adr/` is empty. When PR3 lands, offer an ADR recording: *"Single `ISecretEncryptionService` with purpose-as-parameter; the previous two-service pattern was duplication, not isolation."* Only record if user rejects PR3 with a load-bearing reason that future explorers would need.

### No data migration
No EF Core migrations in any PR. PR3's ciphertext is byte-compatible (same purpose strings). No schema changes.

### Hybrid test strategy
- Keep existing `WebApplicationFactory` HTTP integration tests throughout.
- New unit tests at new seams land in the same PR as the seam they cover.
- `bunit` added in PR4 only.
- No new test framework for PR1-3 — xunit + in-memory `IDbContextFactory` fakes suffice.

### Vocabulary discipline
- Domain terms (System, Repository, LLM Provider, Database Connection, Pending Context Change, Scan State) from `docs/UBIQUITOUS_LANGUAGE.md`, used verbatim.
- Infrastructure names (`IUserContext`, `IContextManagementCoordinator`, `ISecretEncryptionService`, `SecretPurposes`, `ContextScope`, `ContextState`, `SystemOwnerFilter`) live in code and this spec. Not added to ubiquitous language. `CONTEXT.md` created lazily during grilling loop if needed.

---

## Sources

- [Parameter Binding in Minimal API apps](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0#binding-precedence) — `ClaimsPrincipal` is a special-cased parameter.
- [Purpose strings in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/purpose-strings?view=aspnetcore-10.0) — purpose-as-parameter is the framework's intended idiom.
- [ASP.NET Core Blazor with EF Core](https://learn.microsoft.com/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0) — `DbContext` lifetime in Blazor Server; use `IDbContextFactory<>`.
