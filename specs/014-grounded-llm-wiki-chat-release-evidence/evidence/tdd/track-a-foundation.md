# Track A Foundation TDD Evidence

Source boundary: `651ce053056e564614ef7fd8f0d466bd812c20a9`

## Existing-correct characterization

- T002 no-migration/ownership guard: GREEN, 1 passed, 0 failed. No RED was
  claimed because the protected boundary was already correct.
- T005 accepted schema characterization: GREEN, 9 passed, 0 failed. No RED was
  claimed because the accepted schema documents already existed.

## T004 database ownership slice

- RED command: `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:IsTestProject=true --filter FullyQualifiedName~Story125DatabaseNameValidator_GeneratesUniqueLowercaseNamesAndRejectsProtectedTargets --logger "console;verbosity=minimal"`
- RED result: 1 failed because the public `Story125ReleaseHarness` seam was
  absent; the test compiled and no database connection was opened.
- GREEN result: 1 passed, 0 failed after the minimal name-generation and closed
  ownership validator was added. The validator accepts only lowercase
  `vulgata_story125_*` identities and rejects protected/malformed names.

## T006-T007 corpus slice

- RED command: `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Story125QuestionCorpus" --no-build --verbosity normal`
- RED result: 4 failed only because
  `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json`
  was intentionally absent.
- GREEN command: `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Story125EvaluationContractTests" --no-restore`
- GREEN result: 13 passed, 0 failed, 0 skipped. The fixture contains 16
  schema-valid source-traceable cases with Business/IT, Cross-Repository,
  Flow Group, failure/decision, follow-up, insufficient-evidence, and
  authorization-negative coverage and no embedded provider answer.

All commands completed with zero build errors. Existing NU1507/NU1900 package
source/feed warnings and MSB3277 EF-version warnings remain nonblocking.
