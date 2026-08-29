# Story 12.5 Track C-MVP Tier 1 verification 01

## Candidate boundary

- Base definition checkpoint: `2b60081a514cc718895aed935cd5db4b494523b6`
- Production source, EF/migrations/snapshot, Story 12.4, Scan strategy,
  `Program.cs`, endpoint/project/package delta: none
- Track A/B production and tests remain immutable.

## Story 12.5 contract and guard support

`Story125TrackCMvpReportContractTests`, `Story125TrackCMvpPreflightTests`,
`Story125Tier2PostgreSqlTests`, `Story125ReleaseHarnessTests`, and
`Story125EvaluationContractTests`: `93 passed, 0 failed, 0 skipped`.

## Prepared Chat, streaming, publication, page and Scan regression

Unfiltered accepted matrix result: `239 passed, 2 failed, 0 skipped`.
Both failures are known sandbox-only Data Protection default-key-path tests:

- `ProductionComposition_KeyRingPathWithoutApplicationName_PreservesDefaultsAsync`
- `ProductionComposition_ApplicationNameWithoutKeyRingPath_PreservesDefaultsAsync`

Both failed only because the sandbox denied writes beneath the current user's
default ASP.NET Data Protection key directory. They are environment failures,
not product, review or convergence failures.

The same matrix excluding exactly those two environment-only cases passed:
`239 passed, 0 failed, 0 skipped` in 1m07s. It covered Prepared Chat integration
and coordination, retrieval/currentness/relationship expansion, Evidence Packet,
strict primary/repair streaming, atomic accepted publication, accepted-only page
reveal, Business/IT behavior and production Scan status.

## Web build

The ordinary build was blocked by the same unidentified processes holding shared
`obj` assemblies. No process was killed. A Web-only build reused the successfully
compiled dependency references and wrote its intermediate/output files to a
Git-ignored isolated directory:

    dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore -p:BuildProjectReferences=false -p:IntermediateOutputPath=<ignored-intermediate> -p:OutputPath=<ignored-output> -p:WarningLevel=0

Result: build succeeded, `0 errors`, six existing NU1507 warnings.

## Forbidden-diff and integrity guards

- `git diff --check`: passed; line-ending advisories only.
- `git diff <base> -- src/dotnet`: empty.
- EF/DbContext/migrations/model snapshot diff: empty.
- Story 12.4/spec 013 diff: empty.
- `Program.cs`, endpoint, solution/project/package diff: empty.
- No fake Backup B state is used.
- Accepted Track B content-dark mandatory-streaming observations are quoted, not
  recomputed; no Track C attestation or release-envelope machinery was restored.

Verdict: `PASS`. The only failures observed were classified environment-only and
were separated from the clean product regression before advancing.
