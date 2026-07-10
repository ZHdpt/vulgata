---
title: 'Restore the green identity test baseline'
type: 'bugfix'
created: '2026-07-10'
status: 'done'
baseline_commit: 'a5a4dd7'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/bmad/implementation-artifacts/1-3-login-and-logout.md'
  - '{project-root}/docs/bmad/implementation-artifacts/1-6-administrator-role-assignment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deterministic baseline tests fail because later UI changes drifted from the accepted Story 1.3 and Story 1.6 behavior contracts. The login source test still expects `FluentTextField` after the deliberate Identity POST fix moved the page to `InputText`, while four administrator tests still post named SSR forms that were removed in favor of interactive-only click handlers.

**Approach:** Preserve the login form's semantic email input while aligning its source assertion with the POST-compatible `InputText` implementation. Make administrator management explicitly static SSR and restore antiforgery-protected POST forms for role changes, while preserving the existing serialized role coordinator, isolated query scope, Chinese status messages, authorization behavior, and all working tests.

## Boundaries & Constraints

**Always:** Keep `InputText`, `type="email"`, and the Bootstrap form classes on the Login page because the existing HTTP login tests prove that form POST flow. Mark administrator management with `[ExcludeFromInteractiveRouting]` and use named SSR forms with complete hidden inputs for grant/remove actions. Route removal through `IAdministratorRoleCoordinator`; retain `IServiceScopeFactory` in `LoadRowsAsync`; preserve Chinese UI and current roles/policies. Follow the repository's Blazor SSR form-test harness rules.

**Ask First:** Adding a test framework or package, changing role-transition semantics, replacing HTTP integration coverage with browser-only/component-only coverage, or changing files outside the Code Map.

**Never:** Reintroduce `FluentTextField` on Login, weaken last-administrator protection, bypass antiforgery, remove serialized role mutation, stage unrelated workflow work, or hide the failures by skipping/renaming tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Login source contract | Current `Login.razor` | Test requires `InputText`, `type="email"`, `form-floating`, and `form-control`; rejects `FluentTextField` | Test fails on future control or semantic drift |
| Grant administrator | Administrator posts a named grant form for a User | Target gains `Administrator`, sees immediate access, and confirmation remains Chinese | Unknown user or Identity failure returns a Chinese error status |
| Remove administrator | Administrator posts a named remove form | Target loses `Administrator`; retains `SystemOwner` or receives `User` fallback | Coordinator preserves role invariants |
| Remove last administrator | Only one Administrator remains | No visible remove form; fallback named form POST is blocked by coordinator | Chinese last-administrator error; role remains |

</frozen-after-approval>

## Code Map

- `tests/Vulgata.Tests/LoginLogoutTests.cs` -- contains the stale source-shape assertion; eight behavioral tests already pass.
- `src/dotnet/Vulgata.Web/Components/Account/Pages/Login.razor` -- retain the intentional `InputText` implementation and restore its semantic email type.
- `src/dotnet/Vulgata.Web/Components/Pages/Management/UserManagementPage.razor` -- restore named SSR form transport while preserving coordinator/scoped-query logic.
- `tests/Vulgata.Tests/AdministratorRoleAssignmentTests.cs` -- four existing failing integration tests are the RED evidence and should pass unchanged.

## Tasks & Acceptance

**Execution:**
- [x] `src/dotnet/Vulgata.Web/Components/Account/Pages/Login.razor` -- add `type="email"` to the username `InputText` so the POST-compatible control retains the accepted semantic input contract.
- [x] `tests/Vulgata.Tests/LoginLogoutTests.cs` -- rename the stale source test and require `InputText`, `type="email"`, Bootstrap form structure, and absence of `FluentTextField`.
- [x] `src/dotnet/Vulgata.Web/Components/Pages/Management/UserManagementPage.razor` -- opt out of interactive routing, then restore hidden fallback, grant, and remove SSR POST forms plus form-bound dispatch; retain the current coordinator and isolated query scope.
- [x] `tests/Vulgata.Tests/AdministratorRoleAssignmentTests.cs` -- run unchanged as regression evidence; change only if rendered .NET 10 handler markup proves the existing extractor factually wrong.

**Acceptance Criteria:**
- Given the current Login page, when its source contract test runs, then it passes while requiring the POST-compatible `InputText` structure and semantic email input.
- Given administrator role UI scenarios, when the four named-form tests post all rendered hidden fields, then grant, remove, fallback-role, and last-administrator behavior pass.
- Given the complete identity test classes, when run together, then every LoginLogout and AdministratorRoleAssignment test passes.
- Given the repository test suite, when run after the focused tests, then all tests pass without suppressing or skipping failures.

## Spec Change Log

## Design Notes

The form-based administrator transport is preferred over rewriting the tests around interactive SignalR clicks: it restores the already accepted HTTP integration seam, preserves antiforgery/authorization/cookie coverage, and avoids introducing a browser or component-test framework. `[ExcludeFromInteractiveRouting]` makes the page's `HttpContext` and `[SupplyParameterFromForm]` assumptions explicit. Commit `e380934` already isolated row-loading queries while SSR forms were present, so restoring the transport does not require reverting the concurrency fix. Story 1.3's `FluentTextField` implementation detail is superseded by the later POST fix; its user-facing semantic email requirement remains authoritative.

## Verification

**Commands:**
- `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~LoginLogoutTests.LoginPageSource_UsesPostCompatibleInputsAndRemovesOutOfScopeSections|FullyQualifiedName~AdministratorRoleAssignmentTests"` -- expected: 8 tests pass.
- `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~LoginLogoutTests|FullyQualifiedName~AdministratorRoleAssignmentTests"` -- expected: 16 tests pass.
- `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore` -- expected: all 89 tests pass.
- `git diff --check` -- expected: no whitespace errors.

## Suggested Review Order

**Static SSR role-management boundary**

- Static routing makes form binding, antiforgery, and `HttpContext` availability explicit.
  [`UserManagementPage.razor:4`](../../../src/dotnet/Vulgata.Web/Components/Pages/Management/UserManagementPage.razor#L4)

- Named forms preserve the accepted HTTP integration seam for role mutations.
  [`UserManagementPage.razor:25`](../../../src/dotnet/Vulgata.Web/Components/Pages/Management/UserManagementPage.razor#L25)

- Form-bound dispatch validates input before routing to bounded role operations.
  [`UserManagementPage.razor:89`](../../../src/dotnet/Vulgata.Web/Components/Pages/Management/UserManagementPage.razor#L89)

- Coordinator-backed removal preserves last-administrator and fallback-role invariants.
  [`UserManagementPage.razor:153`](../../../src/dotnet/Vulgata.Web/Components/Pages/Management/UserManagementPage.razor#L153)

**Login semantics and regression evidence**

- POST-compatible input retains browser and accessibility email semantics.
  [`Login.razor:22`](../../../src/dotnet/Vulgata.Web/Components/Account/Pages/Login.razor#L22)

- Source contract scopes semantic and Bootstrap assertions to the email control.
  [`LoginLogoutTests.cs:32`](../../../tests/Vulgata.Tests/LoginLogoutTests.cs#L32)
