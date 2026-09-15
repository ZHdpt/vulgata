# Pending baseline-test reconciliation decision

Story 13.1 remains in `implement`. T001-T022 and T024-T030 are verified. T023's new retention-conflict behavior passes, but its required surrounding test gate remains blocked. The accepted task map makes T031 depend on T023.

## Concrete proposed scope

No production or UI changes, no historical feature activation, and no skipped required verification.

1. Preserve exact pre-edit copies of `tests/Vulgata.Tests/RepositoryApiTests.cs` and `tests/Vulgata.Tests/RepositoryManagementTests.cs` under this feature's `evidence/history/baseline-tests-before-reconciliation/`, with original paths, working-byte SHA-256 and Git blob identities.
2. Retire only these four private-`Map` tests from the active Repository API test file, retaining their exact original assertions in that archive:
   - `ScanRunSummaryMapping_WithUnsortedLanguageSummaries_ReturnsOrdinallySortedBreakdown`
   - `ScanRunSummaryMapping_WithDuplicateUnsortedUnknownOutcomes_ReturnsExactDistinctOrdinalPaths`
   - `ManagementScanRunRoute_WithDurableFrameworkAudit_ReturnsClosedChineseDetailAsync`
   - `ManagementScanRunRoute_WithAdversarialDurableTerminalMessage_ReturnsClosedChineseSummaryAsync`
3. Keep the other thirteen API tests unchanged. The four retired tests require a private method absent from the deliberately decommissioned `RepositoryProfilingCoordinator`; restoring that legacy implementation is not proposed.
4. In `SystemOwner_CanCreateRepository_ForAssignedSystem_AndPageShowsRepositoryManagement`, change only the expected label from `最近扫描时间` to the existing UI's `最后扫描时间`. Preserve all creation, ownership, visibility and other UI assertions.
5. Rerun Repository API/management and source regressions. The full-suite, convergence, independent quality review, E2E decision and final verification gates remain required. Any further baseline issue is reported separately, not silently included in this scope.

## Current evidence

`baseline-verification-blocker-01.json` proves the affected baseline files are unchanged since `bff540980800eb67254170a0c82e38678b63576d`. Current owned-key runs show API13/17 and management8/9; source retention, reads, identity and login checks pass. `SystemListPanel.razor` currently declares `最后扫描时间`.

Latest verified implementation checkpoint: `06b3585423a17c896dc9a2b4c3ea4d49228c1719` (104/104 focused/regression checks). This is not completed Story 13.1 or full-suite acceptance.

## Why authorization is needed

The requested scope is Story13.1 only. The sprint-loop skill says: "Never expand scope to an unmet prerequisite without user authorization." This is a bounded baseline-test maintenance expansion, not an inherited competition or verification waiver.

The existing asynchronous question remains pending. No reconciliation edits or archives have been created. If this expansion is declined, the five tests stay untouched and the strict gate remains blocked.
