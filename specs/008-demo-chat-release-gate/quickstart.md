# Quickstart: Validate Demo Chat and the Final Release Gate

This guide validates Story 11.5 after implementation. It does not authorize
task generation or replace independent plan/quality review.

## Prerequisites

- .NET 10 SDK, Git, Docker, and PostgreSQL 17.
- Accepted prepared refs/commits from
  `specs/007-exact-cross-repo-resolution/contracts/demo-resolution-profile.v1.json`.
- Organization-controlled Repository endpoints and LLM-provider secrets supplied
  through the release environment, never source control or command evidence.
- A clean immutable candidate. Stop if `git status --short` is non-empty.
- Explicit planned-change approval for the new Chat evidence tool recorded by
  the root orchestrator after independent plan acceptance.

## 1. Static scope and contract checks

```pwsh
git status --short
git diff --check
$templateTokens = @('NEEDS' + ' CLARIFICATION', '[' + 'FEATURE' + ']', '[' + 'DATE' + ']', 'ACTION' + ' REQUIRED', 'REMOVE' + ' IF UNUSED', 'TO' + 'DO', 'T' + 'BD')
rg -n ($templateTokens -join '|') specs/008-demo-chat-release-gate
rg -n "index\.md|automatic scope|file upload|retry|failover|IQueryable|DbSet" src/dotnet/Vulgata.Core src/dotnet/Vulgata.Agents src/dotnet/Vulgata.Web src/dotnet/Vulgata.Infrastructure
```

Expected: clean candidate, no whitespace/template marker, no canonical
`index.md` retrieval, no automatic/file-upload/retry/failover implementation,
and no EF/query types crossing Infrastructure. Review findings must distinguish
existing unrelated uses from Story 11.5 changes.

## 2. Test-first focused groups

Run each production group after observing its transient failing test and then
its GREEN result:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~ChatContractTests
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~ChatEvidenceRetrievalTests
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~ChatEvidenceCurrentnessTests
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatAgentResultValidatorTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatPageTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~PreparedChatIntegrationTests
```

Required outcomes:

- explicit Business/IT + one System + exactly two Repository selection;
- current bounded database evidence and exact Edge direction;
- authorization before retrieval, live during retrieval/currentness validation,
  and again after currentness validation immediately before render;
- all claims structurally bound to known current Document/Section/Source Anchor
  evidence by exact retained span/support signature; the provider returns only
  selection/order handles; unrelated/contradictory decoy, swapped, changed,
  unknown, stale, or incomplete output rejects the entire answer;
- one bounded retrieval read ends before provider dispatch; after selection-only
  validation, one separate bounded serializable currentness read proves the same
  current Document Versions, Stable Sections/exact spans, safe same-version
  positive-range Source Anchors, and current authorized directed
  `REFERENCES`/`GENERATED_FROM` support; no transaction crosses provider latency;
- barrier-controlled tests supersede a Document Version and independently make
  required Source Anchor/relationship support unavailable after retrieval and
  provider dispatch but before currentness validation; each produces the same
  whole-answer missing/stale result with zero claims/citations;
- Business narrative has no inline code locator and expandable Chinese
  Document citations expose persisted line ranges; IT may show locators;
- provider failure/timeout/cancellation/malformed JSON produces a bounded
  Chinese error and no partial/stale answer;
- prompt resources load exactly once by mode and production DI resolves every
  specific service.

## 3. Migration and build gates

Story 11.5 plans no migration, but predecessor state and model parity remain
release gates:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~KnowledgeStoreMigrationTests|FullyQualifiedName~SequentialScanRunnerMigrationTests|FullyQualifiedName~ExactCrossRepositoryMigrationTests"
dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore -c Release
```

Expected: all tests pass, no pending model change, zero build warnings/errors,
and no new migration/snapshot diff.

## 4. Install the pinned interactive driver

Explicitly restore and build the test project in Release, then install the
matching Chromium binary from the generated Release script:

```pwsh
dotnet restore tests/Vulgata.Tests/Vulgata.Tests.csproj
dotnet build tests/Vulgata.Tests/Vulgata.Tests.csproj --configuration Release
pwsh tests/Vulgata.Tests/bin/Release/net10.0/playwright.ps1 install chromium
```

This is a gate prerequisite. Do not fall back to static SSR, `HttpClient`,
bUnit, or direct service invocation if Chromium cannot start.

## 5. Sole clean PostgreSQL/Docker/browser E2E

Set the harness's documented environment variables for the PostgreSQL
administrator connection, prepared Repository endpoints, and encrypted/fixture
LLM providers. Do not print them. Then run:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests --logger "trx;LogFileName=story-11-5-release-gate.trx"
```

`Story115ReleaseHarness` is the sole owner. It creates nonce-prefixed
PostgreSQL 17/container/network/volume/key-ring resources and one isolated
Worker/Chat provider namespace, then proves this exact observed path:

```text
clean migrations
-> mbs_account @ 19c60cd3f3976b7697c487c7bafd96142e3a7625
-> mbs_channel @ 47f6b627a7598f862873610413eec1e1e7bf46bf
-> profiling and accepted Pre-Scan Result
-> Pass 1 for each Repository
-> exact account-to-channel Cross-Repository Resolution
-> one directed REFERENCES Edge and exact evidence
-> Pass 2 Business Logic Document + GENERATED_FROM provenance
-> bounded authorized retrieval
-> exact Candidate Claim extraction + selection-only provider result
-> bounded serializable post-provider currentness validation
-> final Repository authorization recheck
-> keep the same database alive and build the final image exactly once
-> curl health on /Account/Login
-> real Identity login + interactive Blazor positive claim/citation proof
-> negative Identity context + post-selection revocation missing-equivalent proof
-> terminate/drop/prove database absence and remove all nonce-owned resources
```

Provisioning must call `UserManager`, role/ownership coordinators, management
repositories/coordinators, Scan services, provider coordinators, and real
`SecretEncryptionService`; fixture SQL/`DbSet` insertion is forbidden. The
provisioner and final container share the exact gate-owned Data Protection ring
and application discriminator. The test fails on ref/commit drift, guessed
truth, provider-authored factual prose, invalid support, incomplete citations,
unauthorized disclosure, noninteractive browser proof, cleanup failure, or a
remaining database/resource. `finally` always runs.

The provider/app topology is part of the harness contract, not a schematic.
The harness creates an internal nonce-owned network and chooses three distinct
available host ports; `workerPort` and `chatPort` are also the provider's
container ports, and neither may be `8080`. With all names/paths held as
harness variables, its provider invocation is equivalent to:

```pwsh
$pythonFixtureImage = 'python:3.12-bookworm@sha256:9bed8554e926c07c6f908841d5ee88c33e8df9236b191526bbce81a9062ab43a'
docker network create --internal $gateNetwork
docker run --detach --name $providerContainer --network $gateNetwork `
  --publish "127.0.0.1:${workerPort}:${workerPort}" `
  --publish "127.0.0.1:${chatPort}:${chatPort}" `
  --publish "127.0.0.1:${appHostPort}:8080" `
  --mount "type=bind,source=$fixturePath,target=/fixture/loopback-openai-provider.py,readonly" `
  $pythonFixtureImage python /fixture/loopback-openai-provider.py `
  --host 0.0.0.0 --worker-port $workerPort --chat-port $chatPort
```

The PostgreSQL container is attached to `$gateNetwork`. Product-service
provisioning and host-side profiling/Scan store and call
`http://127.0.0.1:${workerPort}/v1`; the Chat assignment stores
`http://127.0.0.1:${chatPort}/v1`. The fixture listens on `0.0.0.0`, while all
three published mappings are restricted to host loopback. Equal
host/container provider port numbers are mandatory so the identical stored
URLs work after the final app joins this namespace.

## 6. Applicable regression

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj -c Release --no-build --no-restore
```

Expected: all applicable tests pass. If the environment requires the existing
one-shot source-bound CodeGraph final-image lane to run separately, record the
explicit filter split and prove the union covers the full suite exactly once.
No deliberately failing RED test may remain.

## 7. Harness-owned final Docker mechanics

Use the existing CodeGraph provider-contract source-manifest verifier to create
and validate the canonical `production-source-manifest-v1` for the clean
candidate. Capture its source commit, source tree, and manifest SHA-256 without
editing the candidate, then perform exactly one accepted final build:

```pwsh
docker build --no-cache --progress=plain --file docker/Dockerfile --tag vulgata-story115-final --build-arg VULGATA_SOURCE_COMMIT=<candidate-commit> --build-arg VULGATA_SOURCE_TREE=<candidate-tree> --build-arg VULGATA_PRODUCTION_SOURCE_MANIFEST_SHA256=<manifest-sha256> .
docker image inspect vulgata-story115-final
```

Expected: the image ID is recorded and labels
`org.vulgata.source-commit`, `org.vulgata.source-tree`, and
`org.vulgata.production-source-manifest-sha256` exactly equal the verified
candidate values. A dirty candidate or mismatched label stops the gate.

The harness writes a permission-restricted nonce-owned `$gateEnvFile` containing
exactly `ASPNETCORE_ENVIRONMENT=Production`,
`ASPNETCORE_URLS=http://+:8080`, the still-live disposable
`ConnectionStrings__DefaultConnection`,
`DataProtection__KeyRingPath=/var/lib/vulgata-dpkeys`, and the same
`DataProtection__ApplicationName` used by the provisioner. The file is never
logged and is removed in `finally`. It starts the image in the provider's
network namespace, with no app-side `--publish`, and supplies a health check
using the binary actually installed in the runtime image:

```pwsh
docker run --detach --name $appContainer `
  --network "container:$providerContainer" `
  --env-file $gateEnvFile `
  --mount "type=bind,source=$keyRingPath,target=/var/lib/vulgata-dpkeys,readonly" `
  --health-cmd "curl --fail --silent --show-error http://127.0.0.1:8080/Account/Login" `
  --health-interval 2s --health-timeout 5s --health-retries 30 `
  --health-start-period 15s vulgata-story115-final
docker inspect --format "{{.State.Health.Status}}" $appContainer
```

The provider container already owns
`127.0.0.1:${appHostPort}:8080`; combining `--publish` with
`--network container:` is forbidden. Expected: `healthy`; Playwright targets
`http://127.0.0.1:${appHostPort}`, authenticates through `/Account/Login`, waits
for the Blazor interactive circuit, uses the Fluent controls, and verifies the
prepared answer/citations plus missing-equivalent negative path. Compose's
`wget` check and an image-configured `HEALTHCHECK` are not assumed. Remove only
nonce-owned resources. Do not reuse a predecessor image.

## 8. Evidence and limitations

Record sanitized command exits/timestamps/hashes, candidate and image identity,
positive claim/citation counts, authorization-negative result, database drop
and absence, and cleanup outcomes. Do not retain credentials, proprietary
source, raw prompts/responses, absolute private paths, or unauthorized details.

Report these as limitations, not acceptance claims: automatic scope selection,
file upload, saved sessions, full iterative retrieval tools, retry/failover,
concurrent Scan behavior, generalized Cross-Repository matching,
graph/dashboard/SignalR, and deferred Epic 5-10 capabilities.

## Optional extension hook disposition

`.specify/extensions.yml` registers optional
`speckit.agent-context.update` after planning. This planning run does not wait
for or execute the optional hook. The mandatory SpecKit context action is
satisfied directly by updating the `AGENTS.md` SPECKIT marker to this plan.
