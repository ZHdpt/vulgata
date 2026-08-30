# T011 public Prepared Scan initiation observation

Status: **RED — honest public endpoint failure**

## Intended behavioral slice

`PostPreparedScan_NoPreseededLifecycleState_CreatesCompletePairAndManifestsAsync`
exercises the authenticated public `POST /api/systems/{systemId}/prepared-scans`
boundary with one ordinary System and exactly two ordinary Repositories. The
fixture intentionally creates no Scan Run, Pre-Scan Result, Scan Execution,
Two-Repository coordination, Prepared Scan Manifest, File Code Unit, or
Document. It uses a newly generated lowercase `vulgata_story125_*` PostgreSQL
database and would verify the 201 response, complete five-row initiation tuple,
and two published manifests.

## Observation

After the test host was corrected to use a private writable Data Protection key
ring and production-matching PostgreSQL migration-history schemas, the test
completed its setup and reached the public endpoint. The fixture created the
fresh isolated database `vulgata_story125_dc93398e5e8142ce9f0bea800a89486a`
against PostgreSQL 17. Before the request, the lifecycle counts were all zero:

```text
ScanRuns=0, PreScanResults=0, ScanExecutions=0, Coordinations=0,
Manifests=0, CodeUnits=0, LogicalDocuments=0
```

The authenticated Administrator request to
`POST /api/systems/{systemId}/prepared-scans` returned **404 Not Found** rather
than the expected 201 Created. This is the expected current-production RED:
`PreparedScanCoordinator` delegates directly to `PreparedScanPairBuilder`,
which requires a pre-existing Preparing Two-Repository coordination and bound
Scan Runs. With the required no-preseed starting state, the builder returns the
missing-coordination result and the endpoint maps it to 404. No production code
was changed to obtain this result.

## Test command and result

Planned command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~PreparedScanInitiationApiTests.PostPreparedScan_NoPreseededLifecycleState_CreatesCompletePairAndManifestsAsync -p:WarningLevel=0
```

Result: **1 failed, 0 passed**. The failure is the endpoint assertion at
`PreparedScanInitiationApiTests.cs:93`: expected HTTP 201, actual HTTP 404.
The test reached the endpoint after authenticated setup; this is not a compile,
database, authentication, or test-host failure.

## Minimal production owner after prerequisites are available

If the clean focused run reaches the current endpoint and fails because the
pair-builder cannot find pre-created initiation state, the minimal GREEN owner
is the accepted Prepared Scan initiation coordinator/endpoint seam from the
Story 12.5 plan. This test does not authorize or include that production work.
