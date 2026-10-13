# Testing and verification

Read this before test authoring, runtime qualification or interpreting a failure.
The current story workflow owns exact TDD/review/verification gates; these notes
preserve stack-specific pitfalls and evidence boundaries.

## Default execution and explicit qualification

Run `pwsh -NoProfile -File eng/test.ps1` from the repository root. This builds current
sources and runs the complete **SelfContained** suite. Use `-Filter
'FullyQualifiedName~Feature'` for a focused selection; the filter is intersected
with the suite. Zero selected tests, all-skipped selections, process failures and
incomplete TRX outcomes and unexpected skips in any suite are non-success. Build logs, discovery and TRX results live
in a unique `TestResults/isolation-*` directory. `-PolicyOnly` validates sources
without building or acquiring services. The normal gate also runs economical
policy/outcome/timeout regressions and, after building, the fixture guard probes.
These probes call the underlying helpers/direct testhost, never the runner recursively.

The single xUnit 2.9.3 project retains these explicit suites:

| Suite | Resource boundary |
| --- | --- |
| SelfContained | Owned temporary files, SQLite, in-memory TestServer and controlled transports/clocks |
| PostgreSql | Runner-created PostgreSQL 17 container and only databases created by the run; some cases also use Git or child dotnet |
| LocalTools | Explicit Git, JDT, CodeGraph or native-process qualification with test-owned paths |
| External | Explicit live provider qualification; existing source/provider consent flags still apply |
| E2E | Explicit combined resource qualification, including loopback HTTP; select `-WithPostgreSql` when needed |
| HistoricalBoundary | Explicit retained-checkpoint reproduction; historical assertions remain intact |

Select these with `-Suite PostgreSql`, `-Suite LocalTools`, etc. External/E2E cases
requiring a database also need `-WithPostgreSql`; this creates a new container and
never uses ambient application/admin connections. `-WithPostgreSql` is accepted
only for PostgreSql, External and E2E; other combinations fail before provisioning.
Docker readiness failures are
prerequisite failures. Generated credentials and loopback ports exist only in the
runner process and its test children. PostgreSQL readiness is checked over TCP at
127.0.0.1. Each container mounts a fresh run-labeled named data volume. The runner
records mount IDs, verifies the exact run labels, removes the container with its
volumes, explicitly removes its named volume, then verifies volume absence.
Cleanup failures are non-success and still restore saved environment variables.
Fixtures check the run scope before opening connections;
database cleanup is confined to successful creates. Existing provider/tool flags
are requirements in addition to suite selection. The only permitted skips, for
every suite, are the four exact `RetainedSourceSeparateProcessChild` operation
methods and `PreparedScanStoreRegressionTests.PreparedStore_ConcurrentIdenticalWriters_CommitOnceAndReplayOnceAsync`.
Their full names are checked in `eng/test-results.ps1`; they remain visibly skipped.
All-skipped selections fail even when every skip is allowlisted.

`-TestTimeoutSeconds` defaults to 300 seconds per test. `-CommandTimeoutSeconds`
defaults to 600 seconds for each whole build, discovery, test execution or import
invocation. Docker control operations and guard probes have shorter bounded limits.
For intentional long qualification, configure both timeouts as needed; increasing
only the per-test limit does not extend the whole native invocation. Timeout kills
only the process tree started by that invocation, reports failure, and enters cleanup.

### Explicit prepared-input seed import

T062/T124 prepared inputs must originate from an explicitly supplied **local
custom-format pg_dump archive** (`PGDMP`, generated with `pg_dump -Fc`). The runner
never exports a shared database and does not execute plain SQL files. For example:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite External -WithPostgreSql -SeedDump C:\owned-seeds\approved.dump -Filter 'FullyQualifiedName~Pass1ProductionExecutionTests'
```

The archive is copied into the fresh container and restored transactionally into
a newly created, recorded `vulgata_story124_seed_*` database, without restoring
ownership or privileges. Import logs suppress archive contents and diagnostics
that could contain SQL data. The runner supplies this generated connection as
`VULGATA_T062_QUALIFICATION_CONNECTION` and `VULGATA_T124_SOURCE_CONNECTION`, and
sets `VULGATA_T124_POSTGRES_ADMIN` to the same owned instance. It preserves supplied
manifest/system IDs, source roots and provider/source consent flags; it does not
invent manifests or enable consent. T124 validates both administration and recorded
source ownership before connecting, and records successful clones before EF access.
All imported and cloned data disappears with the owned container/volume.

`pwsh -NoProfile -File eng/test-seed-regressions.ps1` is the controlled import/clone
probe: it creates its own marker-only source container and local archive, then runs
only `SeededTargetTests`. It does not export existing databases or call providers.

Project runsettings positively select SelfContained for ordinary CLI/IDE execution.
A filter override does not authorize external execution: suite attributes and the
xUnit executor validate the actual guarded Fact/Theory attribute against its suite
trait and exclude disabled cases before collection/class fixtures initialize.
Discovery and theory data providers must remain inert. The runner clears ambient
service configuration for default execution. This is a test policy boundary, not
an operating-system network sandbox; report network-denied verification separately.

## Authoring and policy enforcement

Every C# test/helper source belongs in
`tests/Vulgata.Tests/TestInfrastructure/suites.json`. Every test has exactly one
`Trait("Suite", "...")` and matching `SuiteFact`/`SuiteTheory`; existing custom
qualification attributes inherit those guards and preserve their additional skips.
Never use bare xUnit Fact/Theory or override the guard. New helper files must be
classified even when they contain no test methods. Do not initialize services in
constructors, static fields, discovery or data generators.
The policy also runs before test-project compilation, including direct CLI/IDE
builds; PowerShell 7 is required for these repository checks.

SelfContained tests must not start native processes, access service credentials,
open external databases/listeners, or construct HTTP transports without a controlled
handler. Trace helper dependencies before classification. The source policy checks
known risky APIs and type references across helper files using bundled Roslyn
syntax trees, including executable string interpolation and combined/qualified
attributes; it is conservative source
analysis, not a semantic proof of every production call path. Controlled runtime
checks and review still matter. Only the three reviewed infrastructure helpers may
use the reserved Policy classification. Regex exemptions are rejected. The only
reviewed exception identifiers are `test-owned-keyring-environment` and
`modeled-http-handler-type`, restricted to their exact syntax in
`PreparedChatIntegrationTests.cs`, with a manifest rationale. They cannot erase
arbitrary expressions or waive a whole file.

The initial inventory conservatively assigns mixed external files as a unit,
including additional consumers of shared fixtures found during implementation.
The test infrastructure maintainer owns future separation into self-contained and
qualification files; preserve assertions when splitting them. Existing useful
SQLite/TestServer coverage remains SelfContained. Personal key rings and JDT fixture
paths have been replaced with ephemeral/owned fixtures. Streaming timing probes run
in their own nonparallel collection so CPU-heavy tests cannot consume their deadlines.

Run `eng/test-policy-regressions.ps1` for controlled policy/outcome probes and
`eng/test-guard-regressions.ps1` after a current build for fixture exclusion and its
deliberately failing enabled control. The latter script succeeds only when disabled
execution excludes the sentinel and enabled execution reaches its fixture.

The configured governed full-test command means the complete SelfContained
regression suite. It does not replace required PostgreSQL, native tool/provider or
feature-specific E2E gates. Excluded qualification is never reported as passed.

## Harness

- Tests live in `tests/Vulgata.Tests`, mirroring the .NET source structure. Use
  `{Method}_{Scenario}_{ExpectedResult}` names and the existing xunit patterns.
- Web integration uses `WebApplicationFactory<Program>` and the host's
  `public partial class Program`, with SQLite substitutions for both DbContexts
  where applicable. Those substitutions are not PostgreSQL equivalence proof.
- Post Blazor SSR forms with **all rendered hidden inputs**, not only antiforgery.
  Select by `action`/`formname`; a logout form may precede the target form.
- Assert AccessDenied redirects by path prefix where query details vary. Verify API
  401/403 behavior separately from browser redirect behavior.
- Reflection/component harnesses must supply every injected dependency, even if the
  constructor is parameterless or an earlier test needed fewer injections.
- SQLite cannot translate every PostgreSQL query, notably some `DateTimeOffset`
  ordering. Keep selection bounded and use a justified provider-compatible/client
  projection in the harness; do not weaken production semantics to make SQLite pass.
- Keep status-code-page re-execution off `/api/`; middleware can otherwise change a
  correct API response into an HTML rewrite or another status.
- Verify Chinese Identity validation messages through the actual Identity/form
  path; do not assume `ChineseIdentityErrorDescriber` translates every framework
  validation attribute automatically.

## Evidence and safety

- Behavioral changes need honest RED then minimal GREEN and relevant regression
  evidence. Documentation/metadata changes use proportionate checks, not fabricated
  RED or unnecessary claims of application qualification.
- Prepared-source Windows tests open every ancestor with native directory handles.
  A restricted execution context may allow temporary-file creation but deny an
  ancestor open (`NtCreateFile` access denied, surfaced as `RootUnavailable`).
  Compare focused and full runs through the same launcher and permission mode:
  a direct `dotnet test` pass does not establish access for children launched by
  `eng/test-native.ps1`. If that launcher reproduces the failure only in a sandbox,
  use approved execution outside it; preserve containment checks and test assertions.
  See [Story 18.1 execution diagnosis](../../specs/016-contained-source-tools/evidence/rootunavailable-execution-diagnosis-01.md).
- A no-build result can use a stale binary. Confirm the tested assembly/build
  boundary. Concurrent `dotnet` processes can lock shared outputs; serialize owned
  test work or use proven isolated outputs rather than killing unrelated processes.
- Establish explicitly owned disposable databases, containers, processes and fixture
  directories. Startup applies migrations/seeding. Never infer authority to drop a
  shared database, stop all `dotnet` processes or copy real users' hashes/secrets from
  a legacy helper recipe. Use supported fixture or application creation paths.
- Separate host/configuration/network readiness from product behavior. Report exact
  failing URLs and sanitized error classifications when applicable. Use bounded
  source-free readiness checks under task authority; do not repeatedly retry an
  unchanged expensive provider/Scan operation.
- Record the actual source/revision/configuration and whether evidence is static,
  controlled/mock, local integration or real tool/provider/UI behavior. Native MCP
  requires native MCP proof; provider SSE ingestion alone is not browser streaming.
- Old passing tests and fixed-pair fixtures are reusable evidence for their accepted
  boundary, not proof of generic one/several/standalone behavior. Test-only data must
  remain test composition, not production fallback.
- Failed/incomplete drafts are not accepted Knowledge. Valid JSON or exact citation
  identity alone does not establish explanation usefulness/faithfulness. Preserve
  current feature-specific real qualification requirements and honest gaps.
- `git diff --check` may print normal LF/CRLF conversion warnings. Distinguish those
  warnings from an actual whitespace failure; do not declare a line-ending blocker
  without a failing relevant check.

Use [development guidance](development.md) for commands. Recheck a pitfall against
source before removing it; lack of a recent failure is not evidence its cause
disappeared.
