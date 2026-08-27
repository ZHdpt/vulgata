# Track A Core TDD Evidence

Source boundary: `9c7d8c6d21856237957f5e7048f206dea7819c7e`

## T010-T014 persisted Scan status and management UI

- T010 RED established the missing public `IProductionScanStatusReader` seam.
  Four initial test-fixture defects were corrected by the test owner without
  changing the accepted behavior boundary.
- T011 GREEN: `ProductionScanStatusReaderTests` passed 17/17 and the adjacent
  durable Scan set passed 48/48. The projection is Repository-scoped,
  persistence-backed, bounded, and marks evidence `ProductionPersisted`.
- T012 RED: five management-page behaviors failed while one existing
  characterization passed before the UI implementation existed.
- T013 GREEN: `ProductionScanPageTests` passed 6/6; adjacent management,
  login, and manual-resume coverage passed 31/31; the Web build completed with
  zero errors.
- T014 regression GREEN: `ProductionScanStatusReaderTests` passed 21/21,
  covering Applied, Replay, Conflict, and Rejected resume outcomes followed by
  a persisted re-read. `ManualResumeApiTests` passed 13/13. Existing coverage
  proves stale/hidden missing equivalence and excludes `DemoScanUiState` /
  fake Backup B state.

## T015-T018 semantic release preparation and scoring

- T015 RED: 27 semantic/adversarial vectors failed because
  `ValidateSemanticEnvelope` and `ValidateCrossArtifactConsistency` did not
  exist.
- T016 GREEN: all 27 vectors passed; the complete Story 12.5 ReleaseGate set
  then passed 42/42.
- T017 RED: four scoring vectors failed and one existing characterization was
  green before the scoring implementation.
- T018 GREEN: all five scoring vectors passed; the complete Story 12.5
  ReleaseGate set passed 47/47, and the corpus contract passed 4/4.
- The tasks-only T018 ownership correction was independently accepted with
  zero findings in
  `reviews/definition-review/track-a-scoring-owner-correction-01.md`; it was
  scope-neutral and preserved Track A implementation approval.

## Root verification

- `ProductionScanStatusReaderTests`: 21 passed, 0 failed.
- `ProductionScanPageTests`: 6 passed, 0 failed.
- `ReleaseGate.Story125`: 47 passed, 0 failed.
- `ManualResumeApiTests`: 13 passed, 0 failed.
- `Vulgata.Web` build: 0 errors; existing NU1507/NU1900 and MSB3277 warnings
  remain nonblocking.

No Track B/C production work, EF migration, `VulgataDbContext`, model snapshot,
Story 12.4 worktree, Story 12.4 database, or fake Backup B release evidence was
used.
