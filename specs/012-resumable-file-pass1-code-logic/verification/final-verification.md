# Story 12.3 stable pre-convergence verification

Observed at `2026-08-21T17:23:25Z` against branch `demo0831/story-12-3` and
committed implementation source `cb3fb0f17b2eaf92d93afe71c710cd7b92c1e3bd`,
plus the T046-T049 qualification and test-only compatibility corrections
listed below. This is the T048 pre-convergence barrier; it does not replace
root-owned pinned verification after convergence, quality review, and the E2E
decision.

## Result

Story 12.3 qualification is **PASS with six explicitly isolated repository
baseline/harness checks**. All Story 12.3 focused, adjacent, migration,
PostgreSQL, runtime, composition, exclusion, build, and diff checks pass.
The six isolated checks are unchanged by Story 12.3 or require an unrelated
completion-lane input; none is presented as a passing full-suite result.

## Passing evidence

| Boundary | Result |
|---|---|
| T046 host/runtime plus T047 production boundary | 3 passed, 0 failed |
| real `NavigateCodeRealToolQualificationTests` | 3 passed, 0 failed |
| pinned JDT-LS readiness | 1 passed, 0 failed |
| PostgreSQL 17 pair/document atomicity plus migration | 48 passed, 0 failed |
| focused `PreparedPass1` | 267 passed, 0 failed |
| corrected adjacent DurableScanning/PreparedScanning/KnowledgeStore/AnalysisRuntime | 437 passed, 1 existing skip, 0 failed |
| test-only historical/current migration compatibility | 13 passed, 0 failed |
| Web build | succeeded, 0 errors, 9 warnings |
| broad repository run excluding five proven unrelated baseline checks | 1911 passed, 1 existing skip, 1 parallel-only failure |
| parallel-only ExactCrossRepository migration lifecycle, isolated rerun | 1 passed, 0 failed |
| `git diff --check` | pass |
| workflow schema/contracts validator | pass |

The disposable KnowledgeStore PostgreSQL tests used the dedicated database
name `vulgata_story123_knowledge`; credentials and connection strings are not
persisted here. PostgreSQL reported major version 17. Host tool locations were
provided through local environment configuration and are intentionally absent
from this artifact.

## Test-only compatibility corrections

No production behavior changed during T048 diagnosis. The correction preserves
historical migrations while testing the accepted current model separately:

- the Story 12.2 File Code Unit test now targets the exact
  `PreparedFileCodeUnitDraft.Create` seam instead of selecting any public type
  whose name contains `FileCodeUnit`;
- the Story 12.2 boundary test binds the current accepted spec and plan bytes;
- Knowledge Store tests distinguish the immutable v1 migration from the
  additive current v1/`code-logic/file-v2` model and byte-anchor constraint;
- sequential-runner and earlier PostgreSQL lifecycle tests distinguish their
  historical relational boundary from the later
  `AddResumablePass1CodeLogic` migration;
- the required KnowledgeStore PostgreSQL environment variable was supplied to
  an explicitly disposable database, turning four configuration failures into
  4/4 passes.

## Full-suite baseline treatment

The unfiltered run completed with 1908 passed, 1 skipped, and 9 failed before
the compatibility corrections. Three failures were the Story 12.3 test-only
migration compatibility defects corrected above. One ExactCrossRepository
migration lifecycle check failed only when competing with the parallel full
suite and passed immediately in isolation. The five remaining checks are
outside the Story 12.3 base-to-candidate diff:

1. `CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract`
   requires the absent legacy completion-lane `VULGATA_PROVIDER_CONTRACT_IMAGE`
   input and targets the superseded 0.5.1 qualification lane.
2. `QualificationPreflightTests.Run_WithFakeExecutor_InvokesOnlyFreshBuildAndExactSyntheticCategoryAsync`
   reads a legacy `plan_revision` workflow-state shape that is absent from the
   current copied fixture.
3. `QualificationPreflightTests.Run_WithIncompleteConsumedAttempt_ReconcilesInTempAndNeverCreatesAttempt0003Async`
   is the same unchanged legacy qualification-state lane.
4. `Story43EndToEndTests.ProductionComposition_TwoCommittedRepositories_PersistsIsolatedProfilesAndApprovedPreScanResultsAsync`
   requires the full configured analysis-runtime integrity environment; without
   it the expected coordinator is replaced by the fail-closed runtime-unavailable
   coordinator.
5. `RepositoryManagementTests.SystemOwner_CanCreateRepository_ForAssignedSystem_AndPageShowsRepositoryManagement`
   expects the unrelated page label `最近扫描时间`, which is absent from the
   unchanged current page.

These checks were neither modified nor absorbed. The subsequent broad run
excluded exactly those five checks, leaving only the parallel-only
ExactCrossRepository result; its isolated current-boundary run passed.

## Commands

Commands were run from the repository root with required local environment
values supplied but not printed:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1HostRuntimeQualificationTests|FullyQualifiedName~Pass1ProductionBoundaryQualificationTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1PairPostgreSqlAtomicTests|FullyQualifiedName~Pass1DocumentPostgreSqlAtomicTests|FullyQualifiedName~Pass1MigrationTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreparedPass1"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~DurableScanning|FullyQualifiedName~PreparedScanning|FullyQualifiedName~KnowledgeStore|FullyQualifiedName~AnalysisRuntime"
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
uv run --offline --with PyYAML python validate_workflow.py --repo <repository-root>
```

T049 separately records that no Story 12.4/12.5, `CodeGraphContext`, Pass 2,
Business Logic Document, Chat, final release UI, evaluation, or release-run
production behavior was added.
