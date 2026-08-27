# Track A Integration TDD Evidence

Source boundary: `f1711aacd4b0d6454d603b9b5a26fbc94e85dc21`

## T019 RED

The first run was invalid because the new composition tests lacked imports for
the existing production namespaces. The test owner corrected only that
test-owned compiler defect; no production write occurred.

The valid focused RED selected exactly three T019 tests:

- 1 passed: disposable Story 12.5 database and production-persisted screenshot
  preflight characterization.
- 2 failed: both composition tests reported
  `No service for type 'Vulgata.Core.DomainServices.IProductionScanStatusReader' has been registered.`

This isolated the missing shared DI registration before `Program.cs` changed.

## T019 GREEN

The minimal production change registered scoped
`IProductionScanStatusReader -> ProductionScanStatusReader` and scoped
`ProductionScanStatusCoordinator` beside the durable Scan registrations.

- Exact T019 tests: 3 passed, 0 failed.
- `ProductionScanStatusReaderTests`: 22 passed, 0 failed.
- `ProductionScanPageTests`: 7 passed, 0 failed.
- `ReleaseGate.Story125`: 48 passed, 0 failed.
- `Vulgata.Web` build: 0 errors, with 19 existing NU1507/NU1900 and MSB3277
  warnings.

`Vulgata.Agents/DependencyInjection.cs`, EF configuration, migrations, project
files, Track B/C paths, Story 12.4 state, databases, and fake Backup B release
evidence were not changed or used.
