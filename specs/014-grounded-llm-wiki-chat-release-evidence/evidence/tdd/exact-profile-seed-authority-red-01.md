# T037 Exact-Profile Seed Authority RED

Date: 2026-08-31

## Scope

This is the test-first RED slice for the bounded reviewed exact-profile seed
authority. It changes only the assigned test seams:

- tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedAuthorityTests.cs
- tests/Vulgata.Tests/PreparedScanning/PreparedSelectionPlannerTests.cs

No production, workflow, task, database, staging, or Git-history changes were
made by T037.

## Required behavior pinned

The tests require:

1. exact ordinal persisted Repository.Name, safe full ref, and lowercase
   Profiled-Revision matching for both server-observed Repositories;
2. a canonical four-role union over consumer, provider declaration, provider
   implementation, and unsupported-observation owner paths;
3. request-order-independent source identity and profile fingerprint;
4. a closed Unavailable result for catalog failure, without nonmatch fallback;
5. a new reviewed-profile evidence value scoring 1,100 with
   DirectBusinessEvidence;
6. every required split segment surviving the unchanged top-20-per-Repository
   and 150-unit pair bounds; and
7. complete nonmatch neutrality for generic candidates, scores, order, audit
   rows, selection, and fingerprint.

The authority tests read the existing validated embedded profile at test
runtime; they do not duplicate profile values in production code.

## RED observation

Command:

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --filter "FullyQualifiedName~ReviewedExactProfileSeedAuthorityTests|FullyQualifiedName~PreparedSelectionPlannerTests" --logger "console;verbosity=normal"

Result: **12 total, 6 passed, 6 failed**.

Expected RED failures:

- PreparedBusinessEvidence has no ReviewedExactResolutionProfile value, so the
  score/selection tests fail while parsing the absent evidence value.
- ReviewedExactProfileSeedAuthority is absent from Infrastructure.
- ReviewedExactProfileSeedAuthorityResult is absent from Core.

The focused test assembly was built successfully first with:

    dotnet build tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0

Result: **Build succeeded, 0 errors** (existing NU1507/MSB3277 warnings).
The RED is therefore the missing contract/value/adapter behavior, not a
compile or environment failure.

## Hygiene

    git diff --check -- tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedAuthorityTests.cs tests/Vulgata.Tests/PreparedScanning/PreparedSelectionPlannerTests.cs specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/exact-profile-seed-authority-red-01.md

Result: clean. Existing line-ending warnings are informational only.

T040 is the sole coordinated production GREEN owner for the missing Core
contract/evidence value, Infrastructure adapter, planner mapping, builder
composition, and default DI.
