# T039 exact-profile seed PostgreSQL RED

## Boundary

This is the T039 RED slice for the bounded, exactly-two-Repository Prepared
Scan production entrance. The test starts with an authenticated Administrator,
one ordinary System, two ordinary Repositories whose bindings are loaded from
the existing reviewed `ExactResolutionProfileCatalog`, and a newly created
lowercase `vulgata_story125_*` PostgreSQL database. It does not pre-seed Scan
Runs, Scan Executions, coordination, Manifests, File Code Units, Documents,
Document Edges, exact decisions, Edges, or Uncertainties. The HTTP request is
the only Prepared Scan entrance. Revision, snapshot, CodeGraph, JDT readiness,
catalog, and Worker dependencies are bounded test doubles; no remote provider
or real source analysis is used by this RED test.

## Test slice

`tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedPostgreSqlTests.cs`
contains two vertical tests:

1. A matched reviewed pair must publish two Manifests with every required
   score-1,100 segment from both Repositories, while leaving Pass 1 and all
   later-stage state untouched before an explicit Pass 1 start.
2. A repeated preparation request must not start Pass 1 or leak exact
   resolution/Pass 2 state. Required Worker failure compatibility is covered
   separately by T042 against the existing Pass 1 execution surface.

Both tests assert the zero-row precondition before the request and inspect
durable results after the production endpoint returns. No direct store,
planner, resolver, or EF row insertion is used.

## RED observation

The test setup was corrected after the first root-owned rerun exposed two
masked setup defects: the WAF had replaced the health probe but not the
internal `IPreparedScanRuntimeIdentityResolver`, and snapshot fixtures did not
contain the reviewed profile's required paths. The WAF now uses the existing
`PublicPreparedScanRuntimeIdentityResolver` seam and derives the required
consumer/provider/implementation/unsupported-owner paths from the embedded
reviewed profile, adding only bounded fixture fillers.

The subsequent fixture correction binds each snapshot's profile-required paths
to the server-observed Repository ID and alias. No Repository receives the
other Repository's required paths, so a matched run can exercise the authority
without creating an artificial ambiguity. Synthetic XML fixtures use explicit
opening and closing elements (not self-closing roots) so they remain valid for
the accepted safe XML splitter.

The fresh database is now schema-bootstrapped with the repository's existing
EF migrations before the ordinary System/Repository rows are inserted. This
is schema setup only; no migration was created or changed and no Scan or
knowledge-store lifecycle row is pre-seeded.

The reviewed source fixture now uses the profile's actual evidence shape at
each required path: the line-45 `netTools.execute` consumer invocation, the
line-66 OSS observation, the line-2 `trans` XML declaration, and the provider
implementation class. This removes the earlier generic-source fixture defect;
the bounded filler files remain ordinary fallback inputs.

The Pass 1 assertion is a separate orchestration concern. The
`POST /api/systems/{systemId}/prepared-scans` endpoint creates/replays the
prepared pair and Manifests only; it does not start Pass 1. Pass 1 is entered
through the authenticated `POST /api/pass1-executions` endpoint after the two
Manifest IDs exist, and its hosted service requires the provider/Worker
composition. Consequently, a run that stops after the Prepared Scan request
cannot truthfully produce `FailedPass1` (or Pass 1 success). The smallest
honest T039 boundary is the no-preseed Prepared Scan publication and explicit
zero-later-stage assertion now encoded by both tests. T043 owns the follow-on
vertical that reads the two Manifest IDs, invokes the existing authenticated
Pass 1 start/status surface with bounded provider/Worker seams, and then
observes unchanged exact resolution and Uncertainty outcomes. No direct
lifecycle insertion or production change is a substitute.

Root's credential-mapped PostgreSQL rerun after these fixture corrections reached
the production endpoint and returned `Created` with two Manifests and at least
50 Code Units. A subsequent valid `Ready` Java-readiness identity was required
to remove a test-double mismatch that had produced `503`; this was fixture
configuration, not a production failure. The T039 assertions now require four
reviewed score-1,100 selected entries, zero Pass 1 pair executions, and zero
exact decisions/Edges/Uncertainties immediately after preparation. The second
test makes the same no-later-stage assertion rather than claiming a Worker
failure that has not been started.

Command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ReviewedExactProfileSeedPostgreSqlTests"
```

One child rebuild was blocked after compilation by another test process holding
the shared `Vulgata.Tests.dll` output (MSB3021/MSB3027); that was an environment
lock, not a product or TDD failure. An earlier child run also lacked the
process-local PostgreSQL administrator mapping:

```text
System.InvalidOperationException: T011 requires
VULGATA_TEST_POSTGRES_ADMIN for an isolated PostgreSQL 17 database.
```

Those observations were **UNOBSERVED**, not runtime GREEN or RED. The root then
loaded the approved administrator password into a process-local connection
without printing it and reran both tests against fresh fixture-owned lowercase
databases. Final focused result: **2 passed, 0 failed, 0 skipped**, duration
8 seconds. Each fixture disposed only its own strict disposable database. The
project build command was:

```text
dotnet build tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0
```

and completed successfully. This final GREEN proves the no-preseed preparation
slice only; T042 and T043 own the later Pass 1 failure and exact-outcome proof.

## Integrity

`git diff --check -- tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedPostgreSqlTests.cs`
completed without whitespace errors. The only observed Git warnings were the
existing inability to read the user's global Git ignore file. No production,
workflow, task, spec, `.env`, staging, commit, database, or historical run-03
files were changed by this T039 slice.
