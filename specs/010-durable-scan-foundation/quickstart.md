# Quickstart Validation: Durable Scan Foundation & Analysis Runtime

This guide is the post-implementation validation boundary. It does not authorize task generation or implementation.

## Prerequisites

- Windows host with .NET 10 SDK.
- PostgreSQL 17 Docker service available from `docker-compose.yml`.
- `codegraph` resolving to approved `@colbymchenry/codegraph@1.5.0`.
- Java 21 at `C:/Program Files/Java/latest/jdk-21`.
- JDT LS install at `C:/Users/13728/.local/jdt-language-server-1.61.0-202608141332`.
- The exact retained JDT-LS distribution archive and its approved SHA-256 configured before a Ready result is expected. It is currently absent, so the correct current expectation is fail-closed NotReady.

## 1. Reconfirm pinned inputs

```pwsh
git rev-parse HEAD
Get-FileHash specs/010-durable-scan-foundation/spec.md -Algorithm SHA256
codegraph --version
& 'C:/Program Files/Java/latest/jdk-21/bin/java.exe' -version
```

Expected planning boundary: commit `dd3111bebbe295aa43a01e95be7bbdb97d3eecff` and spec hash `DA24F90063D0B66D50D642BED8D1E1709257C6BF8B0B42E4AE89929CE5FEEFB2`. Implementation validation must instead record the accepted candidate commit while preserving the accepted spec hash. CodeGraph reports `1.5.0`; Java reports major 21.

## 2. Start the dated host topology

```pwsh
docker compose up -d postgres
docker compose ps postgres
dotnet run --project src/dotnet/Vulgata.Web/Vulgata.Web.csproj
```

Expected: PostgreSQL 17 is the only started Docker service; Vulgata.Web runs on the Windows host. Ordinary startup applies migrations and performs configured runtime integrity health without creating a Repository CodeGraph index or importing a JDT-LS project. The management application may start with analysis health NotReady, but Worker dispatch remains blocked.

## 3. Focused contract suite

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~DurableScanning|FullyQualifiedName~AnalysisRuntime'
```

Expected coverage: entity transitions, deterministic identity, exactly-two invariants, lease boundaries, state-version conflicts, recovery idempotence, atomic Document/job rollback, process argument allowlists, malformed/oversized responses, cancellation, shutdown/forced cleanup, path containment, authorization, and ProblemDetails.

## 4. PostgreSQL migration lifecycle

Use the test harness to create a uniquely named disposable PostgreSQL database, seed protected historical fixtures, and run:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~DurableScanMigrationPostgreSql'
```

Expected: clean apply, rollback, reapply, concurrent stale-write rejection, and no pending model changes all pass. Existing Document Versions, Source Anchors, Edges, Uncertainties, prepared-demo rows, and historical Scan Executions remain readable and semantically identical. The harness drops the disposable database and records cleanup.

## 5. Restart and manual resume

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~DurableScanRestartPostgreSql'
```

Expected fixture: mixed Pending, Running-valid-lease, Running-expired-lease, RetryScheduled-due, RetryScheduled-future, Completed, Failed, and Cancelled jobs. After a real application-process restart and authorized resume:

- completed Documents remain and do not duplicate;
- only expired Running and due eligible Retry Scheduled jobs become Pending;
- valid leases, future retries, and terminal jobs remain unchanged;
- repeated resume is a no-op;
- each injected authorization/revision/hash/prompt/schema/provider/runtime mismatch blocks eligibility with a classified result.

## 6. CodeGraph real qualification

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~CodeGraphCliQualification'
```

Expected: package/version/bundle and the three approved hashes match; the closed JSON operations succeed on the neutral Repository; forbidden commands/flags are unreachable; and the ordinary-startup fixture contains no new `.codegraph` directory.

## 7. JDT-LS real qualification

Before the archive/checksum is supplied:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~JdtLsMissingArchiveQualification'
```

Expected: NotReady with the bounded missing-archive/checksum reason; the core bundle hash is not accepted.

After the exact archive and approved checksum are configured, run:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~JdtLsRealQualification'
```

Expected: Java 21; initialize/initialized; neutral Java document symbols, definition, references, and implementations; bounded cancellation; shutdown/exit; forced-cleanup test; and rejection of a workspace reused for another Profiled Revision.

## 8. Full gates

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
```

Report build errors separately from warnings, every test count/result, real PostgreSQL/tool evidence, and the missing qualification input if JDT readiness remains NotReady. Do not claim Story 12.1 accepted while the required archive checksum or any later workflow gate is absent.

## 9. Cleanup

```pwsh
docker compose stop postgres
```

Do not remove the persistent development volume unless the operator explicitly requests that destructive action. Disposable test databases/workspaces must be removed by their owning harness after exact-path validation.
