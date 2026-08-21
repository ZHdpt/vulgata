# Story 12.2 Final Verification 01

## Result

`passed` at the accepted Story 12.2 product boundary. The reviewed production
and test paths are unchanged from quality-review candidate
`d7262f97dd72ebd33a7d6bdbf77cd3d4624d74f5`; later commits add only review,
E2E, and workflow evidence.

## Story and integration verification

- Complete `Vulgata.Tests.PreparedScanning` namespace: 114 passed, 1 intentional
  SQLite-only skip, 0 failed, 115 total, 1 minute 30 seconds. The skipped
  identical-writer test passed in the mandatory PostgreSQL atomic suite.
- Required conditional E2E: 53/53 passed with no failures or skips: API 5/5,
  PostgreSQL migration 2/2, PostgreSQL atomic lifecycle 31/31, release
  qualification 8/8, and real Windows-host qualification 7/7.
- The real qualification observed PostgreSQL 17.10, CodeGraph
  `@colbymchenry/codegraph@1.5.0`, Java 21.0.12, and the pinned
  `jdt-language-server-1.61.0-202608141332` archive over stdio. It covered exact
  Git revision acquisition, fresh CodeGraph initialize/rebuild/status, JDT-LS
  readiness, revision-bound pair composition, and hard-failure containment.
- Web build: succeeded with 0 errors and 9 existing NU1507/MSB3277 warnings.

## Full regression result

The corrected full-suite run used both required disposable-database variables:
`VULGATA_TEST_POSTGRES_ADMIN` targeted the isolated PostgreSQL 17.10 admin
database, and `VULGATA_TEST_POSTGRES` targeted a dedicated
`vulgata_story122_full_verify` database in that disposable container.

Result: 1,644 passed, 6 failed, 1 skipped, 1,651 total in 5 minutes 42 seconds.
Every Story 12.2 test passed. The six failures exactly match the documented
unrelated baseline:

1. Story 4.3 production composition resolves the runtime-unavailable profiler.
2. Sequential Scan migration expectations omit later accepted constraints.
3. Two legacy CodeGraph qualification fixtures omit `plan_revision`.
4. The legacy provider-contract test lacks `VULGATA_PROVIDER_CONTRACT_IMAGE`.
5. The management page assertion still expects `最近扫描时间`.

The one skip is the intentional SQLite exclusion for the PostgreSQL-only
identical-writer regression.

## Literal, boundary, and cleanup evidence

- Production-literal inspection regenerated after the final build and inspected
  6 production assemblies, 9 embedded resources, and 1 public output. It found
  0 denylisted matches and returned `zero-match`. Artifact SHA-256:
  `8ab1dec8e5852ebe830e183ea145d93e3c3dc1537be6fb42ce47c2e0380fde71`.
- `git diff --exit-code d7262f9 -- src/dotnet tests/Vulgata.Tests` passed, so
  the independently accepted production/test boundary did not drift.
- `git diff --check` passed with line-ending notices only.
- The schema-v3 workflow validator reported
  `Workflow configuration and contracts valid` before this checkpoint.
- The dedicated full-suite database was contained inside the disposable
  `vulgata-story122-qr001-pg` container. That container was stopped and
  auto-removed after verification. The existing `vulgata-postgres` service
  remained healthy and untouched on port 5432.
- No CodeGraphContext, Story 12.3 Worker execution, Document generation, LLM
  call, final Edge/Uncertainty, Cross-Repository Resolution, Pass 2, or Chat
  behavior entered the Story 12.2 boundary.

Verified at `2026-08-21T04:52:50Z`.
