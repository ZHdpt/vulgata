# Quickstart: Validate Language and Framework Detection

This is a validation guide, not implementation code. Run from the Repository
root at the pinned accepted boundary.

## Prerequisites

- .NET 10 SDK
- Docker for the mandatory final-image gate
- PostgreSQL 17 for application smoke validation; SQLite is used by the test
  harness
- exact `docker/codegraph/requirements.lock`
- explicit accepted-correction/image inputs required by the provider-contract
  completion lane

## 1. Verify the planning boundary

```pwsh
git rev-parse HEAD
Get-FileHash `
  specs/002-language-framework-detection/spec.md,`
  specs/002-language-framework-detection/checklists/requirements.md,`
  specs/002-language-framework-detection/evidence/plan-blocker-1.json `
  -Algorithm SHA256
```

Expected HEAD:
`492008ebd787ff71d45054db89b5e0303997c61f`.

Expected hashes:

```text
spec.md            cb484113c956e62bd5cc56345b5b81dd6a3e930e6414bcfca37a56a91f1f46fb
requirements.md    9494531bcd320317c2e74a7cae5cfef7a687e2664b09b11e442c3dae53276bf0
plan-blocker-1.json 828402b90c6ca3deff20a123bd15cdfc3fa4d481864f0deede5cf78c1eed4bbd
```

## 2. Observe RED before production changes

Add focused tests first and run their fully qualified names. The initial
failures must demonstrate:

- Projection A has no `parser_language`;
- a response truncated at the provider's default 100 can currently pass the
  owned envelope;
- Code Units are constructed with null language;
- a supported empty file can currently become a File Code Unit;
- no framework catalog/entity/summary persistence exists;
- the Repository overview has no language/framework/unknown audit output.

Record the failing test names and assertion messages as RED evidence. Do not
accept compile-only RED or a failure caused by a broken fixture.

## 3. Provider and language tests

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~CodeGraphContextProvider|FullyQualifiedName~RepositoryProfiling"
```

Expected GREEN:

- private gateway receives only the `250001` execute-query limit;
- exactly two read-only queries run under one immutable Context;
- Projection A accepts exact nullable `parser_language`;
- one path/one token assigns every Code Unit;
- extension/token mismatch follows the parser token;
- a novel valid token is retained;
- missing identity produces one `Filtered/UnknownLanguage` and zero units;
- conflicting tokens, truncation metadata, 250001 combined rows, query drift,
  process failure, or cleanup failure commits no provider-derived structure.

## 4. Framework detector tests

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~FrameworkDetection"
```

Expected GREEN:

- supported direct evidence yields scoped `High` facts;
- paired committed locks yield scoped `Medium` facts;
- direct beats transitive; stable source tie-break is repeatable;
- nested scopes do not leak facts; same-root ambiguity creates none;
- unsupported/missing/out-of-scope/symbol-like evidence creates none;
- malformed, escape, reparse, per-file bound failures are isolated;
- global bounds fail before structural persistence;
- no process/package manager/network/LLM is invoked.

## 5. Persistence, API, UI, and authorization

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName~RepositoryProfiling|FullyQualifiedName~RepositoryApi|FullyQualifiedName~RepositoryDetailDialog"
```

Expected GREEN:

- composite FKs and unique Code Unit/framework key are enforced;
- language summary equals distinct retained paths;
- complete profile persists in one transaction;
- an injected final SaveChanges failure rolls back the draft, reloads the
  durable still-Profiling ScanRun in a fresh unit of work, and persists Failed;
- a provider/detector/global-bound failure retains local filter audit but no
  provider-derived structural/framework/summary rows;
- authorized overview renders Chinese labels, all language counts, framework
  confidence/source, and sorted unknown paths;
- unauthorized/hidden/revoked access returns established redacted behavior.

## 6. Migration and rollback rehearsal

```pwsh
dotnet ef migrations script `
  --project src/dotnet/Vulgata.Infrastructure `
  --startup-project src/dotnet/Vulgata.Web

dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
```

Review that migration SQL is additive, does not backfill guessed languages,
and its Down removes only Story 4.2 tables/indexes. In staging, prove a
historical Story 4.1 ScanRun remains readable before running a fresh Story 4.2
profile.

## 7. Mandatory final-image provider gate

Build the exact final runtime image using the existing Story 4.1 process, set
the gate's required accepted-correction/image inputs, and run:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj `
  --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract"
```

The completion lane must fail if the image/environment/evidence is absent. A
pass proves:

- exact final/verifier image, provider/lock/wheel, and production query hashes;
- exact structural `lang` alias and per-file reconciliation;
- supported empty file becomes one `Filtered/UnknownLanguage`;
- each projection returns more than 100 rows completely;
- no `truncated`/`result_limit`;
- injected completeness drift fails atomically;
- cleanup succeeds and bounded evidence retains no forbidden content.

Do not modify or retry qualification attempt-0003.

## 8. Final quality gates

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
git status --short
```

Before task generation, an independent plan review must accept these artifacts
and the orchestrator must obtain explicit planned-change authorization for the
Scan parsing/filtering change described in [plan.md](plan.md).
