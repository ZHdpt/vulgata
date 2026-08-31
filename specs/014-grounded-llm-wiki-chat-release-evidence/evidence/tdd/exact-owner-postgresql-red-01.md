# T046 exact-owner PostgreSQL RED preparation

Date: 2026-08-31
Story: 12.5 Track C, T046
Classification: retained pre-GREEN behavioral **RED** plus post-GREEN retryability characterization

## Boundary

The assigned test extends
`tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedPostgreSqlTests.cs`
with one authenticated public Prepared Scan vertical. Each scenario begins
with a newly created lowercase `vulgata_story125_*` database, existing schema
only, one ordinary System, and exactly two ordinary Repositories bound to the
reviewed profile's exact refs and revisions. It does not pre-seed Scan Runs,
Pre-Scan Results, Scan Executions, Two-Repository coordination, Prepared Scan
Manifests, File Code Units, or Documents.

The first scenario sends a nonmatching revision through the production
endpoint and verifies generic/path-valued preparation remains successful and
neutral: persisted `SymbolName` values equal each relative path and no
semantic-owner symbol is introduced. The second scenario sends the exact
matched reviewed pair through the same authenticated endpoint with bounded
CodeGraph, JDT-LS, revision, snapshot, catalog, and symbol-source doubles. The
original pre-T047 run asserted that the old implementation failed closed before
publication and left the durable tuple unchanged. That original run is retained
as the honest product RED. After T047, the accepted initiation design
intentionally leaves the durable two-Run/one-coordination tuple in place when
preparation fails, so an exact retry can resume it; only Manifest/content/later-
stage publication must remain empty.

No direct planner, store, resolver, SQLite substitute, pre-seeded content, or
Story 12.4 fixture is used. No production source, model, migration, workflow
state, or retained database is modified.

## Observation

The focused test was compiled successfully as part of:

```text
dotnet build tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0
```

Build result: **succeeded**, 0 errors. Existing NU1507 and MSB3277 dependency
warnings remain.

The runtime command was:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~ReviewedExactProfileSeedPostgreSqlTests.PostPreparedScan_MatchedSemanticOwnerFailsBeforePublication" --logger "console;verbosity=minimal"
```

With the approved process-local PostgreSQL administrator mapping, the original
pre-T047 focused run reached the production endpoint and produced the expected
product RED:

* HTTP status was **201 Created**, instead of the required fail-closed **422**.
* Before the request, every relevant durable count was zero.
* After the matched request, the sanitized count tuple
  `(ScanRuns, ScanExecutions, Coordinations, Manifests, CodeUnits,
  HighValueSelected, ExactDecisions, ExactEdges, Uncertainties,
  Pass1PairExecutions, FailedPass1)` was
  `(2, 2, 1, 2, 124, 4, 0, 0, 0, 0, 0)`.
* Therefore the matched semantic-owner path published durable Prepared Scan
  state rather than rejecting before publication. The expected assertion failed;
  this is a product RED, not an environment failure.

The post-T047 rerun used the exact filter below after rebuilding the test
assembly:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ReviewedExactProfileSeedPostgreSqlTests.PostPreparedScan_MatchedSemanticOwnerFailsBeforePublication_AndNonmatchingInputRemainsByteNeutralAsync" --logger "console;verbosity=minimal"
```

It passed **1/1**, 0 skipped. The response was HTTP **422**. The pre-request
tuple was all zeroes. After the fail-closed preparation attempt, the exact
sanitized tuple was `(2, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0)` for
`(ScanRuns, ScanExecutions, Coordinations, Manifests, CodeUnits,
HighValueSelected, ExactDecisions, ExactEdges, Uncertainties,
Pass1PairExecutions, FailedPass1)`. This is the accepted retryable initiation
boundary: two runs, two executions, and one coordination persist; no Manifest,
content, selection, relationship, uncertainty, or Pass 1 publication exists.
The nonmatching path still returned HTTP 201 and remained path-valued and
byte-neutral.

The fixture completed its normal cleanup for the generated disposable
database. No response body, connection string, credential, host, database
name, or secret was emitted. The retained Story 12.4 database and development
database were not used.

## Checks and ownership

Changed paths are limited to:

* `tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedPostgreSqlTests.cs`
* this evidence file

`git diff --check` for the assigned test and evidence paths completed without
whitespace errors. Existing unrelated test edits and `.env.example` remain
untouched. No staging or commit was performed by this test owner.
