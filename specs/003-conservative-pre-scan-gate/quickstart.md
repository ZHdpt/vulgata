# Quickstart: Validate the Conservative Pre-Scan Gate

Run from the repository root after implementation. These commands validate the
accepted Story 4.3 boundary; they do not create tasks or dispatch Worker
Agents.

## Prerequisites

- .NET 10 SDK;
- Docker/PostgreSQL for migration and final integration checks;
- the Story 4.3 implementation based on the accepted plan and subsequent
  explicit Scan-strategy authorization;
- source based on the pinned architecture-audit boundary
  `1b9c98bb4999e254ab19704643477732f92c842c`;
- no migration or model-snapshot edit in this correction.

## 1. Build

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
```

Expected: exit 0 with analyzer/code-style enforcement.

## 2. Architecture-remediation RED-GREEN group

Add the behavior/contract tests first and record their failing output before
production edits:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "Name~ArchiveOnlyLease|Name~PublicConstructors|Name~FinalPersistenceContract"
```

Required RED:

- the real framework detector fails on a materialized archive lease without
  `.git`;
- at least one public coordinator constructor lacks the real committed
  snapshot or Pre-Scan detector dependency;
- `IScanRunRepository` exposes the profile-only final overload.

Required GREEN after correction:

- `IFrameworkEvidenceDetector` detects expected framework evidence from the
  live archive-only `ICommittedRepositorySnapshotLease` and never runs Git
  against `RootPath`;
- every public coordinator constructor requires both service interfaces and no
  empty/missing adapter remains;
- exactly one final persistence method accepts
  `RepositoryProfileCommitDraft`.

Do not checkpoint or commit the failing state.

## 3. Production-composition regression

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "Name~ProductionComposition"
```

Expected:

- the coordinator is resolved through the Web production composition root;
- only unavoidable external CodeGraph behavior is replaced;
- real committed snapshot, framework detector, communication detector, and
  Scan Run repository implementations are crossed through public interfaces;
- the materialized snapshot contains no `.git`;
- framework and prepared RPC/HTTP evidence share the exact Profiled Revision;
- profile facts and the approved Pre-Scan Result become visible atomically;
- no empty detector, profile-only commit, Worker Agent job, or dispatch occurs.

## 4. Focused detector regression group

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreScanCommunicationDetectionTests"
```

Expected GREEN:

- direct prepared RPC and HTTP evidence is Confirmed/High;
- import/package/framework-only evidence remains Hint/Conservative;
- malformed, unsafe, oversized, contradictory, and unsupported-family inputs
  create no confirmed fact;
- duplicate evidence is deterministic and bounded;
- more than 100 direct matches across Rpc and Http preserve both confirmed
  kinds, record total counts, and retain the deterministic reserved samples;
- a committed-snapshot revision change during detection fails closed;
- dirty reusable checkout bytes and a tracked-byte mutation that leaves HEAD
  unchanged publish no approved result;
- paths/locators contain no source, secret, or host path;
- zero evidence and warnings-only outcomes still approve.

## 5. Atomic profile and idempotency group

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~RepositoryProfilingTests&Name~PreScan"
```

Expected:

- profile and approved result commit in one transaction;
- injected persistence failure leaves zero partial result/classification rows;
- retry for the same Repository/ScanRun returns one logical result and no
  duplicate facts;
- the profile, result, and aggregate fingerprint retain the same Profiled
  Revision;
- provider profiling failure publishes no result;
- zero Worker Agent job, dispatch, REFERENCES, Uncertainty, or endpoint
  resolution is created.

## 6. Typed handoff, authorization, redaction, and Chinese message

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreScanHandoffTests"
```

Expected:

- authorized system and standalone reads return every schema `1.0` field;
- confirmed evidence includes one deterministic reserved direct sample for
  each confirmed kind even when the full evidence set exceeds 100 records;
- after the working copy advances, the existing handoff remains bound to the
  original Profiled Revision;
- a Story 11.2-style consumer presented with a different revision fails closed;
- hidden, wrong-System, and visibility-revoked reads are indistinguishable from
  not found;
- warning approval returns and durably stores
  `预扫描已批准并保留审慎警告——继续进入顺序演示运行器`;
- captured logs contain only safe identifiers/counts and bounded closed
  warning data.

## 7. Migration-neutral model check

Run the existing migration/model regressions without creating or editing a
migration:

```pwsh
dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreScanMigrationTests"
```

Expected: exit 0/no pending model changes and all existing migration tests
pass. Any generated migration or model diff is a scope failure.

## 8. Story 4.3 E2E, full regression, and Docker

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Story43EndToEndTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
docker build --progress=plain -f docker/Dockerfile -t vulgata:story-4-3-architecture-remediation .
```

Expected: the required Story 4.3 clean-database/two-Repository E2E boundary,
all tests, and the Docker build pass. The E2E evidence must include the
production-composition regression and exact Profiled Revision binding. No
failing test may be committed.
