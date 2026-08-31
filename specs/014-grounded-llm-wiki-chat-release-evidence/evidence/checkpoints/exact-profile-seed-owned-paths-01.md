# Exact-profile seed recovery checkpoint owned paths

Checkpoint: `bmad-12-5-implement-20260831T015751Z-exact-owner-gap`

The root integration owner stages only these paths:

```text
artifacts/qualification/story-12.2/production-literal-inspection.json
specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md
specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/exact-profile-seed-owned-paths-01.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/diagnostics/exact-resolution-owner-binding-gap-01.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/handoffs/bmad-correct-course-exact-owner-binding-01.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/exact-profile-seed-entry-01.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/exact-profile-seed-authority-red-01.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/exact-profile-seed-builder-red-01.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/exact-profile-seed-postgresql-red-01.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/exact-profile-required-pass1-gate-01.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/exact-profile-seed-selection-01.md
src/dotnet/Vulgata.Core/DomainServices/IReviewedExactProfileSeedAuthority.cs
src/dotnet/Vulgata.Core/DomainServices/PreparedScanPlanner.cs
src/dotnet/Vulgata.Infrastructure/CrossRepository/ReviewedExactProfileSeedAuthority.cs
src/dotnet/Vulgata.Web/Data/PreparedScanPairBuilder.cs
src/dotnet/Vulgata.Web/Program.cs
tests/Vulgata.Tests/PreparedPass1/Pass1PairPostgreSqlAtomicTests.cs
tests/Vulgata.Tests/PreparedScanning/PreparedScanBoundaryTests.cs
tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationApiTests.cs
tests/Vulgata.Tests/PreparedScanning/PreparedScanProductionLiteralQualificationTests.cs
tests/Vulgata.Tests/PreparedScanning/PreparedScanReleaseQualificationTests.cs
tests/Vulgata.Tests/PreparedScanning/PreparedSelectionPlannerTests.cs
tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedAuthorityTests.cs
tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedPostgreSqlTests.cs
```

Explicit exclusions: `.env.example`; every EF migration, `VulgataDbContext`,
model snapshot, Story 12.4 contract, retained database, temporary source root,
credential, and provider output.

Verification: PostgreSQL diagnostic class 4/4; surrounding T037-T042 89/89;
Web build 0 errors; workflow validator valid; diff checks clean. T043 remains
unchecked and routes to the plan owner through the recorded correct-course handoff.
