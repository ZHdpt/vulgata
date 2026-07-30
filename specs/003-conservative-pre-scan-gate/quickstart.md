# Quickstart: Validate the Conservative Pre-Scan Gate

Run from the repository root after implementation. These commands validate the
accepted Story 4.3 boundary; they do not create tasks or dispatch Worker
Agents.

## Prerequisites

- .NET 10 SDK;
- Docker/PostgreSQL for migration and final integration checks;
- the Story 4.3 implementation based on the accepted plan and subsequent
  explicit Scan-strategy authorization;
- no concurrent Story 11.1 migration/model-snapshot edits.

## 1. Build

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
```

Expected: exit 0 with analyzer/code-style enforcement.

## 2. Focused detector RED-GREEN group

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreScanCommunicationDetectionTests"
```

Expected GREEN after a recorded behavior-level RED:

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

## 3. Atomic profile and idempotency group

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

## 4. Typed handoff, authorization, redaction, and Chinese message

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

## 5. Migration apply, rollback, and model check

Start the normal PostgreSQL development database, then:

```pwsh
dotnet ef database update --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
dotnet ef database update 20260720060442_FrameworkEvidenceAuditCount --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
dotnet ef database update --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
```

Expected: clean apply, successful rollback to the Story 4.2 migration,
successful re-apply, and exit 0/no pending model changes. Also run:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreScanMigrationTests"
```

Expected: PostgreSQL/SQLite model constraints, uniqueness, and transaction
rollback checks pass.

## 6. Full regression

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
```

Expected: all tests pass. Because profiling persistence and the authorized
handoff changed, Story 4.3's later E2E decision must cover the current-boundary
profiling/persistence/read integration. The final clean-database two-Repository
rehearsal and Docker build remain Story 11.5 responsibilities unless Story 4.3
changes container/runtime behavior.
