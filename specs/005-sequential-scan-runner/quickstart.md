# Quickstart: Sequential Scan Runner & Typed Agent Dispatch

**Date**: 2026-07-31 | **Plan**: [plan.md](plan.md)

This is a validation guide, not an implementation. It documents the runnable
scenarios that prove the feature works end to end at the current boundary.
Implementation details live in `tasks.md` and the implementation phase.

## Prerequisites

- .NET 10 SDK
- PostgreSQL 17 (local container or local instance)
- The Vulgata solution builds: `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`
- Story 11.1 boundary is present (knowledge store migration
  `20260731041654_AddMinimalKnowledgeStore` applies cleanly)

## Scenario 1: typed contract invariants (G1)

**Validates**: closed `WorkerAgentJobKind`; finite UTF-8/count limits;
sensitive-content rejection (absolute host path, secret, raw-source carrier,
unbounded diagnostic); deterministic ordering; packet/result identity match;
outcome closed enum.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~SequentialScanRunnerContractTests
```

Expected: all tests green. Negative fixtures (forbidden content, oversized
packet, over-limit collection, unauthorized Repository) are rejected before
dispatch.

## Scenario 2: start and refusal (G2)

**Validates**: a valid start from an approved handoff creates exactly one
`ScanExecution` in `Pass1Planning` with exact identity links; refusal on
missing/not-approved/missing-revision/duplicate-active.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~SequentialScanRunnerStartTests
```

Expected: all tests green.

## Scenario 3: sequential dispatch (G3)

**Validates**: `Pass1Planning`→`Pass1Running`; one job per prepared Code Unit
in deterministic order; one at a time; `TotalJobCount` set; the fake
dispatcher records the exact sequence; no concurrency/lease/replay/failover/
retry.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~SequentialScanRunnerDispatchTests
```

Expected: all tests green.

## Scenario 4: durable state across restart (G4)

**Validates**: persisted state reports phase, in-flight job, completed count;
restart increments the bounded `RestartIndicator` when a job was in flight;
no silent LLM resume; `ReadStateAsync` distinguishes `Pass1Running` from
`Completed`/`Failed`/`Cancelled` using persisted state alone.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~SequentialScanRunnerRestartTests
```

Expected: all tests green.

## Scenario 5: failure isolation (G5)

**Validates**: schema-invalid/error/unbounded → `Failed` with a Chinese
message naming the failing Repository; no Document Version committed; no
Story 11.1 invariant weakened; no partial runner state visible; boundary
rejection (missing Code Unit, oversized packet, forbidden content,
over-limit, unauthorized Repository).

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~SequentialScanRunnerFailureTests
```

Expected: all tests green.

## Scenario 6: migration lifecycle (G6)

**Validates**: the additive `ScanExecutions` migration applies from a clean
database, rolls back to the Story 11.1 baseline
(`20260731041654_AddMinimalKnowledgeStore`) with Story 11.1 tables remaining
and `ScanExecutions` disappearing, reapplies with constraints/indexes
present, and leaves no pending EF model changes.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~SequentialScanRunnerMigrationTests
```

Expected: all tests green.

## Scenario 7: full regression and Web build (verify)

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName!~CodeGraphProviderContractTests&FullyQualifiedName!~KnowledgeStorePostgreSqlConcurrencyTests
```

Expected: Web build zero errors; full non-final-image regression green.
Existing package-source and EF-version warnings may remain.

## Out of scope at this boundary

- clean-database two-Repository demonstration (Story 11.5)
- final Docker image verification (Story 11.5)
- LLM-backed Worker Agent (Story 11.3)
- Pass 2 flow planning, flow-group clustering, candidate selection,
  dispatch-site/endpoint binding, general Cross-Repository Resolution
  (Epic 5 / Epic 7)
