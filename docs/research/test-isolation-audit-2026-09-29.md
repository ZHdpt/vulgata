# Test isolation audit — 2026-09-29

Implementation follow-through: [completed infrastructure repair and verification](../bmad/implementation-artifacts/spec-test-isolation.md).
The audit and proposal below retain the pre-change source snapshot and rationale.

## Scope and evidence

Static inspection of the assigned checkout `G:\source\repos\vulgata`, HEAD `d99f6a0f71a7c20b36b68e462efe9b0a552578c7`. No tests, application hosts, database connections, provider calls, containers, or tool qualification processes were run for this inventory. Environment values and secrets were not inspected. Findings describe execution paths in source, not observed runtime failures.

The repository has one test project, [Vulgata.Tests.csproj](../../tests/Vulgata.Tests/Vulgata.Tests.csproj), targeting .NET 10 and combining unit, EF, web integration, native tooling, and qualification tests. Central packages select xUnit 2.9.3, its Visual Studio runner 3.1.5, and Microsoft.NET.Test.Sdk 18.7.0 ([Directory.Packages.props](../../Directory.Packages.props), lines 26–32). `rg --files tests/Vulgata.Tests -g '*.cs'` found 264 C# source files; a source-line search for ordinary `[Fact]`/`[Theory]` attributes found 2,041 lines. This is **not a discovered test-case count**: theories expand, custom attributes exist, and files contain helpers and mixed test types.

There was no existing `docs/research` directory; existing historical research resides under `docs/bmad/planning-artifacts/research`. This task-scoped audit uses the requested neutral research location and does not change historical planning authority.

## Main finding

The reported blocker is real in source. Several ordinary Facts/Theories fail when external configuration is absent; others silently return success without executing qualification; still others correctly report discovery-time skips. The project has no uniform dependency classification: the only `Trait(` occurrence found in test C# source is the Story 12.4 production qualification category. Filename-based exclusion cannot safely isolate dependencies because shared fixtures introduce PostgreSQL beneath tests with ordinary feature names, and some files mix self-contained tests with external qualification.

## Database inventory

| Family and source | Configuration and absence behavior | Effects and boundary |
| --- | --- | --- |
| [KnowledgeStorePostgreSqlConcurrencyTests](../../tests/Vulgata.Tests/KnowledgeStorePostgreSqlConcurrencyTests.cs), lines 17, 89, 159, 216; helper 499–506 | Four ordinary Facts require `VULGATA_TEST_POSTGRES`; missing value throws | `ResetAndSeedAsync`, lines 448–452, calls `EnsureDeletedAsync` then `MigrateAsync` on the **supplied target database**. No generated ownership name or target guard is present in this helper. This is the highest-priority destructive boundary defect. |
| [PreparedScanAtomicPostgreSqlTests](../../tests/Vulgata.Tests/PreparedScanning/PreparedScanAtomicPostgreSqlTests.cs), 606–612; [Pass1PairStartTests](../../tests/Vulgata.Tests/PreparedPass1/Pass1PairStartTests.cs), 263–268; [Pass1PairPostgreSqlAtomicTests](../../tests/Vulgata.Tests/PreparedPass1/Pass1PairPostgreSqlAtomicTests.cs), 1256–1261; [Pass1ProductionExecutionCapacityTests](../../tests/Vulgata.Tests/PreparedPass1/Pass1ProductionExecutionCapacityTests.cs), 811–817 | Require `VULGATA_TEST_POSTGRES_ADMIN`; missing value throws | Shared [PreparedScanPostgreSqlFixture](../../tests/Vulgata.Tests/PreparedScanning/PreparedScanPostgreSqlFixture.cs), 38–47, creates an owned database then applies real migrations. Disposable ownership does not remove the service/configuration requirement. |
| [PreparedScanInitiationPostgreSqlFixture](../../tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationPostgreSqlFixture.cs), 47–100 | Requires `VULGATA_TEST_POSTGRES_ADMIN`; blank/missing value throws | Generates Story 12.5 database name, checks PostgreSQL 17, creates database; owns cleanup. Consumers include [PreparedScanInitiationPostgreSqlTests](../../tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationPostgreSqlTests.cs):348, [PreparedScanInitiationApiTests](../../tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationApiTests.cs):130 and 464, and [ReviewedExactProfileSeedPostgreSqlTests](../../tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedPostgreSqlTests.cs):45. API filename does not imply self-contained. |
| [RetainedSourcePostgreSqlDatabase](../../tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs), 239–386 | Requires `VULGATA_STORY131_POSTGRES_ADMIN`; missing value throws at 368–375 | Requires explicitly supplied port and numeric loopback host (265–286); generates owned database, verifies target, drops and verifies cleanup. Stronger isolation than older fixtures but still unguarded external dependency. |
| [RetainedSourceCaptureApplicationFixture](../../tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureApplicationFixture.cs), 61–79, 641–685 | Indirectly uses the preceding mandatory administrator configuration | Broad hidden fanout: 17 `*Tests.cs` files directly reference this application fixture, including Authorization, RangeRead, Manifest, Capture, Logging, ReadContext, CapabilityBoundary, RepositoryDeletion, and TrustedSourceWorkspace tests. Count is a source-reference count, not a count of exclusively external classes. [RetainedSourceHttpE2eTests](../../tests/Vulgata.Tests/RetainedSource/RetainedSourceHttpE2eTests.cs):21–26 adds a direct ordinary Fact using PostgreSQL. |
| [ExactCrossRepositoryMigrationTests](../../tests/Vulgata.Tests/ExactCrossRepositoryMigrationTests.cs), 369–396 | `VULGATA_TEST_POSTGRES` absent: ordinary Fact returns normally | When configured, creates separate generated database via `postgres` administration connection and executes migrations. Missing prerequisite is reported to xUnit as successful completion rather than a skip. Other tests in this file are not necessarily external. |
| [DurableScanRestartPostgreSqlTests](../../tests/Vulgata.Tests/DurableScanning/DurableScanRestartPostgreSqlTests.cs), 37–50; [DurableScanConcurrencyPostgreSqlTests](../../tests/Vulgata.Tests/Migrations/DurableScanConcurrencyPostgreSqlTests.cs), 31–41; [DisposablePostgreSqlDatabaseTests](../../tests/Vulgata.Tests/Migrations/DisposablePostgreSqlDatabaseTests.cs), 57–73 | `VULGATA_TEST_POSTGRES_ADMIN` absent: return normally | Real qualification is omitted without runner-visible skip; configured runs use disposable database helper. |
| [ExactCrossRepositoryPostgreSqlTests](../../tests/Vulgata.Tests/PreparedPass2/ExactCrossRepositoryPostgreSqlTests.cs), 14–27; [Pass2FlowPlanningPostgreSqlTests](../../tests/Vulgata.Tests/PreparedPass2/Pass2FlowPlanningPostgreSqlTests.cs), 28–35 and 256–263; [Pass2ExecutionRecoveryTests](../../tests/Vulgata.Tests/PreparedPass2/Pass2ExecutionRecoveryTests.cs), 315–323 and 368–376 | Administrator absent: writes “Qualification not executed” then returns | Output is more honest for a human, but test-runner outcome still succeeds. Recovery file mixes this with SQLite tests. |
| [Pass1DocumentPostgreSqlAtomicTests](../../tests/Vulgata.Tests/PreparedPass1/Pass1DocumentPostgreSqlAtomicTests.cs), 1054–1069 | `VULGATA_TEST_ADMIN_CONNECTION`, falling back to `VULGATA_TEST_POSTGRES_ADMIN`; absent configuration constructs and throws internal `SkipException` using reflection | Different skip mechanism; actual runner handling is unverified. The method name `GetAdministratorConnectionStringOrSkip` alone is not proof that installed runner records a skip. |
| [Pass2DocumentPostgreSqlAtomicTests](../../tests/Vulgata.Tests/PreparedPass2/Pass2DocumentPostgreSqlAtomicTests.cs), 2138–2147; [Pass2PostgreSqlConstraintTests](../../tests/Vulgata.Tests/PreparedPass2/Pass2PostgreSqlConstraintTests.cs), 836–845 | Custom Fact attribute sets `Skip` when administrator absent | Existing discovery-time skip pattern; merely inheriting configured credentials enables the external operation. |

This inventory is representative, not a complete method-level dependency graph. Additional migration and retrieval-budget files also consult administrator configuration. Before enforcing a safe default, enumerate methods and traverse shared-fixture consumers; moving only files whose names contain `PostgreSql` will miss known external calls.

### Existing owned-database helper

[DisposablePostgreSqlDatabase](../../tests/Vulgata.Tests/Migrations/DisposablePostgreSqlDatabase.cs) already centralizes unique names (33–43), protected-name rejection (11–13), connection timeouts (55–60), PostgreSQL 17 checking, creation and cleanup, and exact target verification (112–128). It provides a reusable ownership foundation. It does not require a loopback administration endpoint in the general helper (171–186); Story 13.1's separate helper does. Presence of a connection string establishes technical readiness, not independently recorded permission to use that server.

## Other dependencies that can block automation

- **Pinned native tools and personal paths:** [CodeGraphCliQualificationTests](../../tests/Vulgata.Tests/AnalysisRuntime/CodeGraphCliQualificationTests.cs):9–22 has ordinary Facts requiring a fixed `G:` installation, hashes and a real CLI; [JdtLsRealQualificationTests](../../tests/Vulgata.Tests/AnalysisRuntime/JdtLsRealQualificationTests.cs):15–22 and 70–84 require a fixed Java installation and a retained archive in a user's Downloads. Its first Fact (38–68) only checks runtime identity construction, illustrating mixed classification. [NavigateCodeRealToolQualificationTests](../../tests/Vulgata.Tests/PreparedPass1/NavigateCodeRealToolQualificationTests.cs):18–58 similarly asserts host prerequisites before invoking real processes. [PreparedScanRealQualificationTests](../../tests/Vulgata.Tests/AnalysisRuntime/PreparedScanRealQualificationTests.cs) mixes local Git qualification and native analysis qualification.
- **Git executable, local filesystem and repository history:** [ExactGitRevisionAcquisitionTests](../../tests/Vulgata.Tests/ExactGitRevisionAcquisitionTests.cs):555, [FrameworkDetectionTests](../../tests/Vulgata.Tests/FrameworkDetectionTests.cs):2878, and [RepositoryManagementTests](../../tests/Vulgata.Tests/RepositoryManagementTests.cs):666 launch local Git against fixtures. These are not inherently remote network tests, but require a declared toolchain. [Story125ReleaseHarnessTests](../../tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs):10–76 validates historical commits and current Git delta, so later feature edits or shallow history can break a historical boundary check unrelated to runtime regression.
- **Nested test processes:** [RetainedSourceSeparateProcessTests](../../tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessTests.cs):39–79 invokes `dotnet test --no-build --filter FullyQualifiedName~RetainedSourceSeparateProcessChild`, sets its child-mode contract, and removes administrator credentials from the child. Parent fixtures still need real PostgreSQL; recursion/process semantics and build freshness must remain explicit.
- **Live provider qualification:** [Pass1HostRuntimeQualificationTests](../../tests/Vulgata.Tests/PreparedPass1/Pass1HostRuntimeQualificationTests.cs):233–237 and [Pass1ProductionExecutionTests](../../tests/Vulgata.Tests/PreparedPass1/Pass1ProductionExecutionTests.cs):707–717 already use custom attribute skips. [Pass1DeepSeekIsolatedSourceQualificationTests](../../tests/Vulgata.Tests/PreparedPass1/Pass1DeepSeekIsolatedSourceQualificationTests.cs):816–827 has explicit `VULGATA_T084_ISOLATED_SOURCE_JOBS=1` plus host qualification prerequisites. [Pass2ProductionQualificationTests](../../tests/Vulgata.Tests/PreparedPass2/Pass2ProductionQualificationTests.cs):518–525 requires both production qualification and provider-content flags. Preserve separation from ordinary tests and preserve explicit source/provider authority.

Searches found no `Testcontainers` usage or literal Docker process launch in the inspected C# tests. Playwright is a package reference, but source search did not find actual browser creation/launch calls. Neither package presence nor an `HttpClient` identifier proves outside-service execution.

## Useful self-contained patterns already present

- [KnowledgeStoreTestHarness](../../tests/Vulgata.Tests/KnowledgeStoreTestHarness.cs):14–25 opens SQLite `:memory:` and creates its schema. This provides service-free relational coverage, but not PostgreSQL concurrency/constraint equivalence.
- [LoginLogoutTests](../../tests/Vulgata.Tests/LoginLogoutTests.cs):470–495 removes production registrations for both Identity and domain contexts and replaces them with SQLite, including the context factory. In-process `WebApplicationFactory` clients are not automatically external HTTP calls.
- [JdtLsAdapterTests](../../tests/Vulgata.Tests/AnalysisRuntime/JdtLsAdapterTests.cs):848 and [RecordingProcessLauncherDouble](../../tests/Vulgata.Tests/AnalysisRuntime/RecordingProcessLauncherDouble.cs):22 model process interaction without starting real native tools.
- Existing [testing guidance](../agents/testing.md) already requires explicitly owned disposable resources, distinguishes readiness from product failures, and rejects SQLite as PostgreSQL equivalence. The problem is inconsistent executable selection and prerequisite behavior, not absence of all safety guidance.

## Verification limits

No broad test run was attempted because it would enter the unsafe or unconfigured paths being investigated. No pass/fail totals, database version observations, or native runtime qualification are claimed. Counts and line references describe the inspected source snapshot and can change with subsequent edits.

## Recommended resolution (proposal; not implemented)

### Decision and scope

Keep the existing single test project. Make its routine automation suite explicitly
self-contained, and run resource-dependent qualification through separate named
suites. Preserve useful integration tests and their assertions.

This addresses two different defects: accidental selection of external tests, and
unsafe/unowned resources inside those tests. Filtering alone fixes neither fully.
Do not solve this by distributing database credentials, relaxing authorization,
catching connection errors, deleting integration coverage, or substituting SQLite
for every PostgreSQL test.

The recommendation fits Constitution IV's single-project requirement. Splitting
into separate projects remains a possible later amendment, but is unnecessary for
this repair. No Scan strategy, Worker Agent tool, or product behavior is proposed.

### Suite contract

Use one required execution-suite classification on each test, inherited from a
class where appropriate, plus optional dependency metadata. Proposed values:

| Suite | Allowed resources | When required |
| --- | --- | --- |
| SelfContained | In-process fakes, controlled clock, owned temporary files, SQLite, in-memory TestServer; no external services, credentials, Docker, browsers or separately installed tools | Every ordinary development/final verification run |
| PostgreSql | Runner-provisioned disposable PostgreSQL instance/database | Changes requiring PostgreSQL query, schema, migration, transaction or concurrency evidence |
| LocalTools | Declared local Git/JDT/CodeGraph/child-process dependencies and owned directories/processes | Changes exercising those real adapters or process boundaries |
| External | Explicitly scoped live provider/network targets | Accepted real-service qualification requirements |
| HistoricalBoundary | Retained commits/artifacts for an explicitly selected historical checkpoint | Reproducing that checkpoint; not every later feature |
| E2E | Declared combination of application, browser and other owned resources | Applicable existing E2E gates |

Unit versus component/integration is a separate descriptive distinction:
SQLite/TestServer tests may be self-contained integration tests. A directory named
Integration is not a resource declaration. Tests with multiple dependencies must
declare all of them; suite selection must not erase those prerequisites.

Use a positive selection such as `Suite=SelfContained`, not a negative list of
known dangerous class names. New/unclassified tests must fail the classification
check, rather than disappear unnoticed from the default coverage. Default test
discovery, constructors, static initializers, data providers and collection/class
fixtures must also be free of external side effects.

The installed xUnit 2.9.3 and VSTest adapter already support trait-based filtering;
this does not require a framework upgrade. Verify any custom Fact/Theory traits
and skip behavior against that installed adapter before adopting them.
[Microsoft filter documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests).

### Existing tests: repair order

1. **Contain the current hazards.** Guard every external fixture before resource
   acquisition and add explicit suite metadata, including all indirect fixture
   consumers. Give ordinary unfiltered CLI/IDE runs a safe default too: test
   project default runsettings plus pre-execution opt-in attributes/guards on
   resource-dependent tests. A filter override must not by itself authorize
   database deletion or provider calls. Verify disabled suites never initialize
   their external fixtures; do not assume an attribute alone guarantees this.
2. **Make the standard automation command safe.** Add a repository-owned runner,
   proposed as `eng/test.ps1`, with default `SelfContained` and explicit suite
   parameters. It validates classifications, builds the current tests, composes
   suite and focused-test filters, executes, and propagates failures. Reject zero
   selected tests and unexpected all-skipped results. Report the suite, discovered,
   selected, passed, failed and skipped counts; distinguish excluded qualification
   from successful execution. Point the repository's full-test command to this
   runner and update the development/testing command guidance together.
3. **Repair the destructive PostgreSQL fixture first.** Replace
   KnowledgeStorePostgreSqlConcurrencyTests' deletion of the supplied target with
   the shared disposable-resource fixture. Reuse and harden the existing owned
   database patterns rather than creating another copy. A name prefix alone is
   insufficient ownership evidence: cleanup must target an instance/database the
   current run created and recorded.
4. **Provision PostgreSQL in one place.** Prefer an ephemeral PostgreSQL 17
   container for the explicitly selected PostgreSql suite, with generated
   credentials, dynamic ports, run-specific ownership and bounded cleanup.
   Migrations/data writes are confined to that instance. Testcontainers is one
   suitable implementation, but is a proposed dependency, not an installed one.
   Alternatively, an explicitly authorized test-only server may provision a
   unique database per run with constrained credentials. Never fall back to the
   application's connection string or an existing shared database.
5. **Reduce accidental dependencies.** Move orchestration/authorization/validation
   assertions to existing fakes or SQLite/TestServer harnesses where those
   assertions do not depend on PostgreSQL semantics. Retain PostgreSQL tests for
   provider behavior, migrations, transactions and races. Consolidate safe Web
   factories so both Identity and domain contexts are replaced before startup
   migrations/seeding, and outbound clients/background work cannot fall back to
   real services.
6. **Separate historical checkpoint checks.** Route Story125ReleaseHarnessTests to
   HistoricalBoundary and bind execution to the intended retained revision. Keep
   its historical assertions intact; it must not assert that all later feature
   diffs still belong to Story 12.5.
7. **Normalize outcomes.** Replace environment-dependent early returns that appear
   as passes. Outside an explicitly requested suite, report exclusion/skip. Inside
   a required selected suite, missing configuration, tools or readiness is a
   non-success prerequisite result, not a green test. Fixed historical skips
   remain visibly skipped; do not remove them or claim their behavior is covered
   without evidence. Preserve child-process test selection and pass only the
   parent's scoped resource context to child processes.

Suggested command interface, **not available yet**:

```powershell
pwsh -File eng/test.ps1
pwsh -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~SomeFeature'
pwsh -File eng/test.ps1 -Suite PostgreSql
pwsh -File eng/test.ps1 -Suite LocalTools
```

The external-suite command requires an already authorized resource scope. An
ambient connection string is configuration, not permission. Once a disposable
runner/resource boundary is authorized, routine operations within that boundary
can execute without repeated per-test approval. Container startup remains outside
the default suite; Docker availability must not become the replacement blocker.

Microsoft explains that SQLite differs from production providers and cannot
establish their query/transaction behavior; repository fakes can isolate service
logic while database-provider tests remain valuable.
[EF Core testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy).
For container isolation, use dynamic ports and retain automatic cleanup rather
than fixed shared resource names.
[Testcontainers best practices](https://dotnet.testcontainers.org/api/best_practices/).

### Future tests: enforce the boundary

- **Authoring rule:** unit tests use injected doubles and controlled data. They
  cannot read application secrets/connection strings, access remote endpoints, or
  acquire external database/container/tool resources. Component tests may use
  SQLite/TestServer and owned temp files under the SelfContained contract.
- **Shared fixture rule:** real PostgreSQL, host/browser and tool tests enter
  through approved fixtures that validate suite selection and ownership before
  any side effect. Discovery/data generators must remain inert. Do not introduce
  a global production `IsTesting` bypass or weaken production authorization.
- **Automated policy check:** validate every discovered test's classification,
  including inherited attributes and theories; reject conflicting classifications.
  Add semantic/source checks for forbidden APIs and helpers in self-contained
  tests, following shared fixtures and helper dependencies. Start with the
  concrete risky APIs found in this audit. Regex alone is a useful warning but
  cannot prove absence of indirect network/database access.
- **Enforcement run:** execute SelfContained in a clean environment with service
  credentials absent and external network denied after dependency restore.
  Restore/build prerequisites are reported separately. This exercises indirect
  dependencies that static checks can miss. Allow only explicitly needed local
  resources; in-memory TestServer does not require a real network listener.
- **Policy regression checks:** prove a deliberately unclassified test is rejected,
  an external dependency is rejected from SelfContained, disabled suites do not
  initialize external fixtures, and an explicitly requested but unready suite
  fails clearly. These checks protect the runner's behavior.
- **Coverage preservation:** maintain a complete classification inventory and
  review test moves against it. No assertion deletion or silent coverage loss.
  Record temporary classifications with a concrete remediation owner; do not
  label unknown tests SelfContained just to make the baseline green.

Put the rules in `docs/agents/testing.md`, retain a short pointer in root AGENTS,
and update `docs/agents/development.md` and the dated project state when the repair
is implemented. A build/runner policy check provides enforcement; prose alone
does not restrict newly generated tests. No hosted CI workflow was found in this
checkout, so use the same checked-in runner locally and in any future CI job.

### Acceptance criteria for implementation

1. On a clean machine with the normal SDK/package prerequisites, no application
   secrets and no database/Docker/provider configuration, the default command runs
   the full classified self-contained suite successfully.
2. Ordinary CLI/IDE runs and focused filters cannot accidentally initialize an
   external fixture. Bogus ambient connection configuration does not cause a
   connection attempt from the default suite.
3. Explicit PostgreSql execution creates only run-owned resources; migrations,
   concurrency assertions and cleanup run there. Shared targets are rejected
   before mutation; simultaneous runs cannot delete each other's resources.
4. Classification/policy checks reject newly introduced violations and zero-test
   selections. Reporting never counts a missing-prerequisite early return as a pass.
5. Relevant real-provider/tool/E2E checks remain required by accepted behavior and
   are separately reported. SelfContained success makes no claim that those passed.
6. Validate runner selection/guards with controlled probes first; run real
   PostgreSQL/tool qualification only within its authorized disposable boundary.

### Delivery sequence and alternatives evaluated

Land one bounded test-infrastructure repair: containment and complete
classification first; runner/default command and enforcement next; shared
PostgreSQL fixture and targeted dependency reduction after that. Do not make
rewriting every integration test a prerequisite to unblocking the default suite.
Before enabling the default command, verify that the migrated classification
covers the existing test inventory and that ordinary runs are guarded.

A broad `Integration` exclusion would discard safe, valuable HTTP/SQLite tests.
A class-name blacklist misses hidden fixture consumers. A Docker requirement for
all tests simply replaces one infrastructure blocker with another. Separate test
projects offer stronger structural boundaries but conflict with the current
single-project constitution and add migration cost. The proposed single-project
suite/fixture/policy approach is the smallest compatible repair; a future project
split can be considered if these controls prove too difficult to maintain.

This report changes no test behavior, runtime permissions, workflow configuration
or lifecycle state. Implementation and runtime qualification remain to be done.

## Implementation dependency map (static follow-up)

Paths below are relative to `tests/Vulgata.Tests`. These conservative file-level
classifications include mixed files; all remaining test-bearing files require
explicit SelfContained classification and clean-run verification, not an automatic
fallback. PostgreSql, E2E and External can also need Git or child dotnet processes.

```text
PostgreSql:
KnowledgeStorePostgreSqlConcurrencyTests.cs
ExactCrossRepositoryMigrationTests.cs
DurableScanning/DurableScanRestartPostgreSqlTests.cs
Migrations/DisposablePostgreSqlDatabaseTests.cs
Migrations/DurableScanConcurrencyPostgreSqlTests.cs
Migrations/DurableScanMigrationPostgreSqlTests.cs
Migrations/PreparedScanMigrationPostgreSqlTests.cs
PreparedScanning/PreparedScanAtomicPostgreSqlTests.cs
PreparedScanning/PreparedScanInitiationApiTests.cs
PreparedScanning/PreparedScanInitiationPostgreSqlTests.cs
PreparedScanning/ReviewedExactProfileSeedPostgreSqlTests.cs
PreparedPass1/Pass1ClaimedPacketExecutionTests.cs
PreparedPass1/Pass1DocumentPostgreSqlAtomicTests.cs
PreparedPass1/Pass1PairPostgreSqlAtomicTests.cs
PreparedPass1/Pass1PairStartTests.cs
PreparedPass1/Pass1ProductionExecutionCapacityTests.cs
PreparedPass2/ExactCrossRepositoryPostgreSqlTests.cs
PreparedPass2/Pass2DocumentPostgreSqlAtomicTests.cs
PreparedPass2/Pass2ExecutionRecoveryTests.cs
PreparedPass2/Pass2FlowPlanningPostgreSqlTests.cs
PreparedPass2/Pass2PostgreSqlConstraintTests.cs
PreparedPass2/RetrieveCodeLogicPostgreSqlBudgetTests.cs
RetainedSource/RetainedSourceAuthorizationTests.cs
RetainedSource/RetainedSourceCapabilityBoundaryTests.cs
RetainedSource/RetainedSourceCaptureBoundaryTests.cs
RetainedSource/RetainedSourceCaptureResourceTests.cs
RetainedSource/RetainedSourceCaptureTests.cs
RetainedSource/RetainedSourceIdentityCancellationTests.cs
RetainedSource/RetainedSourceLoggingTests.cs
RetainedSource/RetainedSourceManifestTests.cs
RetainedSource/RetainedSourcePersistenceTests.cs
RetainedSource/RetainedSourcePostgreSqlTests.cs
RetainedSource/RetainedSourceRangeReadTests.cs
RetainedSource/RetainedSourceReadContextTests.cs
RetainedSource/RetainedSourceReadFailureTests.cs
RetainedSource/RetainedSourceReadLifecycleTests.cs
RetainedSource/RetainedSourceRepositoryDeletionTests.cs
RetainedSource/RetainedSourceRootMappingTests.cs
RetainedSource/RetainedSourceSeparateProcessTests.cs
RetainedSource/RetainedSourceSeparateProcessChild.cs
RetainedSource/TrustedSourceWorkspaceTests.cs

LocalTools:
AnalysisRuntime/CodeGraphCliQualificationTests.cs
AnalysisRuntime/JdtLsMissingArchiveQualificationTests.cs
AnalysisRuntime/JdtLsRealQualificationTests.cs
AnalysisRuntime/PreparedScanRealQualificationTests.cs
ChatEvidenceCurrentnessTests.cs
ChatEvidenceRetrievalTests.cs
DatabaseConnectionConfigTests.cs
ExactCrossRepositoryAuthorizationTests.cs
ExactCrossRepositoryIntegrationTests.cs
ExactCrossRepositoryPersistenceTests.cs
ExactCrossRepositoryTestFixtures.cs
ExactGitRevisionAcquisitionTests.cs
ExactResolutionExtractionTests.cs
FrameworkDetectionArchiveLeaseTests.cs
FrameworkDetectionTests.cs
PreScanCommunicationDetectionTests.cs
RepositoryManagementTests.cs
SequentialScanRunnerDocumentGenerationTests.cs
UserSuppliedContextTests.cs
PreparedScanning/PreparedRawArtifactTests.cs
PreparedPass1/NavigateCodeRealToolQualificationTests.cs
PreparedPass2/ExactCrossRepositoryResolutionTests.cs
PreparedPass2/Pass2DocumentCommitTests.cs
PreparedPass2/Pass2ExecutionApiTests.cs
PreparedPass2/Pass2FlowPlanningTests.cs
PreparedPass2/Pass2PartialCompletionTests.cs
PreparedPass2/Pass2ProductionCompositionTests.cs
PreparedPass2/RetrieveCodeLogicContractTests.cs
PreparedPass2/RetrieveCodeLogicSecurityTests.cs

External:
PreparedPass1/Pass1DeepSeekContractQualificationTests.cs
PreparedPass1/Pass1DeepSeekIsolatedSourceQualificationTests.cs
PreparedPass1/Pass1HostRuntimeQualificationTests.cs
PreparedPass1/Pass1ProductionExecutionTests.cs
PreparedPass1/Pass1ProductionProviderOutageTests.cs
PreparedPass2/Pass2ProductionQualificationTests.cs

HistoricalBoundary:
ReleaseGate/Story125/Story125ReleaseHarnessTests.cs
PreparedPass1/Pass1ProductionBoundaryQualificationTests.cs

E2E:
RetainedSource/RetainedSourceHttpE2eTests.cs
LlmProviderConfigTests.cs
```

False positives requiring care: Story125Tier2PostgreSqlTests and
Story125TrackCMvpPreflightTests only validate names/callbacks; DurableScanModelTests
uses Npgsql model metadata. NavigateCodeSecurityTests inspects source strings;
JdtLsAdapterTests/BoundedAnalysisEvidenceTests use doubles. DeepSeekStreaming tests
use fake transport. These need not leave SelfContained merely due to API words.

Existing custom qualification Fact/Theory attributes and RetainedSource child
operation skips must remain compatible with suite guards. Manually acquired
PostgreSQL fixtures must reject unauthorized suite execution before opening any
connection. RetainedSourceFixture starts Git before application fixture setup.
LlmProviderConfigTests owns a loopback TcpListener and is not in-memory HTTP.
