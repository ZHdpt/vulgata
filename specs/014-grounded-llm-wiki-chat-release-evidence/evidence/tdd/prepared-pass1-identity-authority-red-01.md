# T025 Prepared Pass 1 contract identity and layering RED

- Story: `12.5-grounded-llm-wiki-chat-and-august-30-release-evidence`
- Task: `T025`
- Source commit observed: `9d626259c0d64173fb5269be6690d6fbe455dd58`
- Accepted definition scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`
- Specification SHA-256: `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`
- Plan SHA-256: `6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8`
- Tasks SHA-256: `469956462085136a1d1a6acf1b59ab895da403e0de409631c3f10b3b8f90015b`
- Implementation decision: `story-12-5-implementation-approval-prepared-pass1-identity-authority-02`
- Implementation scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`
- RED test source SHA-256: `d2038ff55a6bbac15a880d08b29cfa024ae79132ca04eea7ee3a2e153d76140d`

## Assigned seams

The RED slice observes only the public contract/composition seams accepted for
T025: the Core port and Agents source type, the initiation coordinator's
constructor dependency, the unchanged legacy recovery source, and the Web
authorization-rechecker dependency boundary. No production source was edited.

## Test command and results

Command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedPass1ContractIdentitySourceTests"
```

Result: `1 passed, 3 failed, 0 skipped`.

Expected characterization GREEN:

- `LegacyRecoveryIdentity_RemainsV1AndKnowledgeDocumentSchemaAsync` passed. The
  existing `WorkerAgentDispatchIdentitySource` still returns the literal
  v1 prompt identity prefix/hash and `vulgata.knowledge-document/1.0`.

Honest RED:

- `DefaultPreparedPass1Composition_ExposesV2PromptAndDocumentSchemaIdentity`
  failed because the dedicated Core port and Agents implementation types do
  not yet exist. This is the missing Prepared Pass 1 authority, not a test
  fixture failure.
- `InitiationCoordinator_UsesDedicatedPreparedPass1PortAndNotLegacyRecoveryPort`
  failed because the constructor still accepts
  `IScanRecoveryDispatchIdentitySource` and has no
  `IPreparedPass1ContractIdentitySource` dependency.
- `AuthorizationRechecker_UsesWebIdentityAndMaterializedRepositorySeams`
  failed because the current Web rechecker still imports
  `Microsoft.EntityFrameworkCore`, `Vulgata.Infrastructure.Data`, and
  `VulgataDbContext`; the accepted correction requires materialized Core
  repository seams while retaining Web `UserManager` role orchestration.

The test process built the solution successfully with existing analyzer and
package warnings before executing the four tests. No PostgreSQL database,
provider, external analysis, or retained Track C database was contacted.

## Minimal GREEN owners

- Core: add `PreparedPass1ContractIdentity` and
  `IPreparedPass1ContractIdentitySource` only.
- Agents: add the sole embedded v2 prompt/hash/schema implementation; leave
  `WorkerAgentDispatchIdentitySource` and both prompt contracts unchanged.
- Web: replace only the initiation coordinator's legacy identity dependency
  and remove EF/`VulgataDbContext` access from the authorization rechecker via
  existing materialized `ISystemRepository` and `IRepositoryRepository` seams.

`git diff --check` was clean for the edited test/evidence content; only the
repository's existing line-ending warnings were reported by surrounding .NET
commands.

## T026 follow-up observation

After T026 added the Core value and port as uncommitted source files, the
direct Core verification passed:

```text
dotnet build src/dotnet/Vulgata.Core/Vulgata.Core.csproj --no-restore -p:WarningLevel=0
```

The build reported `0 Error(s)` and reflection over the resulting Core
assembly found both `IPreparedPass1ContractIdentitySource` and
`PreparedPass1ContractIdentity`. A refreshed full test-project build could not
complete because the shared Web output was locked (`CS2012` for
`src/dotnet/Vulgata.Web/obj/Debug/net10.0/Vulgata.Web.dll`). The subsequent
`--no-build` T025 run therefore remained an output-refresh observation of
`1 passed, 3 failed`: the legacy characterization stayed GREEN, while the
coordinator and Web layering REDs remained unchanged; the authority-source
test still observed the older testhost dependency copy. This is an environment
/output-lock limitation, not evidence against the Core T026 source, and no
process was killed or any production file changed.

## T028 GREEN follow-up

The T025 coordinator fixture was adapted only at its accepted identity seam:
it now supplies `IPreparedPass1ContractIdentitySource` with the literal v2
prompt identity/hash and `code-logic/file-v2`, while the legacy
`WorkerAgentDispatchIdentitySource` characterization remains unchanged. No
production, workflow, task, state, staging, or history files were edited by
this adaptation.

The refreshed test-project build completed successfully (0 errors; existing
package/analyzer warnings only). The focused identity and coordinator command
was:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~PreparedPass1ContractIdentitySourceTests|FullyQualifiedName~PreparedScanInitiationCoordinatorTests" --logger "console;verbosity=minimal"
```

Result: `18 passed, 0 failed, 0 skipped`. This covers the four T025 identity
and layering tests plus the fourteen coordinator tests, including all existing
coordinator assertions.

The broader unchanged T011/API initiation filter was also run with the
identity and coordinator tests:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~PreparedScanInitiationIdentityTests|FullyQualifiedName~PreparedPass1ContractIdentitySourceTests|FullyQualifiedName~PreparedScanInitiationCoordinatorTests|FullyQualifiedName~PreparedScanInitiationApiTests" --logger "console;verbosity=minimal"
```

Result: `23 passed, 10 failed, 0 skipped` out of `33`. All ten failures were
the unchanged PostgreSQL-backed T011 tests, each stopping before test setup
with `InvalidOperationException: T011 requires VULGATA_TEST_POSTGRES_ADMIN
for an isolated PostgreSQL 17 database.` They are environmental prerequisite
failures, not product assertions. The five identity tests and eighteen
identity/coordinator tests remained green. No retained or shared database was
contacted.

`git diff --check` remained clean for the assigned test and evidence files
(with only the repository's existing CRLF informational warnings). The
dedicated identity source, coordinator dependency, and Web layering seams are
now verified green against the T028 production changes; the next production
owner may proceed without changing this T025 test boundary.

## T029 API 503 diagnosis

The requested T011/API reproduction was attempted without changing the API
tests or production code. The focused run reached the test host and compiled
cleanly, but all ten PostgreSQL-backed API cases failed at the fixture's
precondition in `PreparedScanInitiationPostgreSqlFixture.CreateAsync` (line
54): `VULGATA_TEST_POSTGRES_ADMIN` is absent. Consequently, this run did not
reach `/api/systems/{systemId}/prepared-scans`, the contract identity lookup,
the authorization rechecker, the initiation store, or the preparation
pipeline; no API 503 response or identity-authority mismatch can be attributed
from this environment.

The API fixture does not override `IPreparedPass1ContractIdentitySource` and
there is no stale legacy identity value in its factory setup. The production
composition resolves the embedded v2 source, while the direct identity,
coordinator, and layering tests remain green (`23/23` non-API tests in the
combined run). Therefore no honest test-fixture correction was identified.

The exact root command required for the remaining T011 evidence is:

```text
$env:VULGATA_TEST_POSTGRES_ADMIN = '<approved non-owned PostgreSQL 17 administration connection string>'
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedScanInitiationIdentityTests|FullyQualifiedName~PreparedPass1ContractIdentitySourceTests|FullyQualifiedName~PreparedScanInitiationCoordinatorTests|FullyQualifiedName~PreparedScanInitiationApiTests"
```

The credential must be supplied in the root execution environment and must
not be written to repository files or evidence. No retained or shared
database was contacted by this diagnosis.
