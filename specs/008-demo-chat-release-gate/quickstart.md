# Quickstart: Validate Demo Chat and the Final Release Gate

This guide validates Story 11.5 after implementation. It does not replace the
fresh Full plan review, read-only analysis, separate acceptance/quality
reviews, or final verification.

## Prerequisites

- .NET 10 SDK, Git, Windows Docker Desktop in Linux-container mode, and the
  already-running PostgreSQL 17 Alpine
  administrator endpoint at `localhost:5432` (administrator database
  `vulgata` at the observed boundary).
- Accepted prepared refs/commits from
  `specs/007-exact-cross-repo-resolution/contracts/demo-resolution-profile.v1.json`.
- Organization-controlled Repository endpoints and LLM-provider secrets supplied
  through the release environment, never source control or command evidence.
- A clean immutable candidate. Stop if `git status --short` is non-empty.
- Existing durable planned-change authorizations: the 2026-08-04 Chat-tool and
  the two 2026-08-07 Pass 2/Pass 1 Scan entries in `workflow-state.json`, plus
  the Story development record and plan-review iteration 7. The current user
  correction dispatch authorizes the Windows Docker Desktop T064 topology.
  After fresh validated Full plan `ACCEPT`, advance directly to regenerated
  Full tasks, read-only `speckit-analyze`, T064/T065, separate acceptance and
  quality reviews, and verification; do not request duplicate approval.

T064 supports this Windows Docker Desktop host only. It does not claim a
native-Linux-host release proof and defines no fallback topology.

## 1. Static scope and contract checks

```pwsh
git status --short
git diff --check
$templateTokens = @('NEEDS' + ' CLARIFICATION', '[' + 'FEATURE' + ']', '[' + 'DATE' + ']', 'ACTION' + ' REQUIRED', 'REMOVE' + ' IF UNUSED', 'TO' + 'DO', 'T' + 'BD')
rg -n ($templateTokens -join '|') specs/008-demo-chat-release-gate
rg -n "index\.md|automatic scope|file upload|retry|failover|IQueryable|DbSet" src/dotnet/Vulgata.Core src/dotnet/Vulgata.Agents src/dotnet/Vulgata.Web src/dotnet/Vulgata.Infrastructure
rg -n "PreparedFlowSchemaIdentity|ManifestId|FlowId|SystemId|RepositoryId|TraceEvidence|CodeLogicDocumentVersions" src/dotnet/Vulgata.Core/DomainServices/WorkerAgentJobPacket.cs
rg -n "prepared-pass2-binding-c14n/1|UUIDv8|RFC-network-order|VULGATA-PREPARED-PASS2-(MANIFEST|FLOW|SUPPORT)" specs/008-demo-chat-release-gate
rg -n "VULGATA-ORDINARY-PASS1-JOB|6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd|pass1-document-version" specs/008-demo-chat-release-gate
```

Expected: clean candidate, no whitespace/template marker, no canonical
`index.md` retrieval, no automatic/file-upload/retry/failover implementation,
no EF/query types crossing Infrastructure, and the Pass 2 Worker payload still
has exactly the accepted seven top-level slots with no runtime-binding or
support-fingerprint eighth slot. Review findings must distinguish existing
unrelated uses from Story 11.5 changes.

## 2. Test-first focused groups

Run each production group after observing its transient failing test and then
its GREEN result:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedDemoPass2FlowPlannerTests|FullyQualifiedName~PreparedPass2RuntimeDispatchTests|FullyQualifiedName~PreparedPass2FlowTests|FullyQualifiedName~SequentialScanRunnerDocumentGenerationTests|FullyQualifiedName~WorkerAgentResultCommitterTests|FullyQualifiedName~WorkerAgentProductionRegistrationTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~ChatContractTests
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~ChatEvidenceRetrievalTests
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~ChatEvidenceCurrentnessTests
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatAgentResultValidatorTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatPageTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~PreparedChatIntegrationTests
```

Required outcomes:

- ordinary Pass 1 derives the actual Worker Job ID immediately before dispatch
  from canonical version-1 TLV bytes for the Scan execution, ScanRun,
  Repository, Code Unit, and full lowercase Profiled Revision; the distinct
  `VULGATA-ORDINARY-PASS1-JOB` magic and RFC-network-order UUIDv8-SHA256 rules
  match [data-model.md](data-model.md);
- an independent oracle pins all 142 bytes, the full SHA-256 digest, and Job
  UUID `6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd`; repeated input is equal, each
  changed input differs, delimiter-ambiguous tuples and other identity domains
  remain distinct, and malformed/mixed-endian encodings are rejected;
- the runtime planner accepts no member Job ID, independently recomputes each
  actual ordinary Pass 1 identity from current product rows, and requires the
  current Document Version ID to match the existing `pass1-document-version`
  one-way projection; legacy/random, substituted, collided/duplicate, or
  drifted identity produces zero Pass 2 dispatches and Documents;
- deterministic identity adds no retry scheduling or duplicate-commit
  permission, no legacy backfill or migration, no synthetic member identity,
  and no eighth Pass 2 slot;
- after the accepted exact directed relationship, one bounded Infrastructure
  read binds only current product-created System, two Repository, Scan
  execution/ScanRun, Worker job, Code Unit, Logical Document, Document Version,
  Source Anchor, and relationship identities;
- the exact profile remains sole selector authority; each required selector is
  unique and authorized with exact direction/ref/full-commit/Profiled Revision;
  missing, duplicate, stale, unauthorized, wrong-profile, wrong-direction,
  wrong-ref, or wrong-commit variants produce zero Pass 2 dispatches and zero
  generated Documents;
- exactly one consumer-owned runtime flow has exactly the two prepared
  supporting Repositories, a canonical server-only support fingerprint, and
  one dispatch/commit; the Worker payload remains exactly the seven accepted
  slots and `GENERATED_FROM` targets every Code Logic Document Version actually
  used;
- the planner creates schema identity exactly as
  `vulgata.prepared-pass2-flow/1.0`, derives Manifest ID from the accepted
  profile and Flow ID from that manifest plus the current ordered runtime
  binding using versioned canonical bytes and UUIDv8-SHA256, and never accepts
  those values from the caller or fixed fixture;
- an independently constructed golden vector pins the complete canonical
  bytes, derived Manifest/Flow UUIDs, and lowercase SHA-256 support
  fingerprint; UUID bytes use RFC network order, tags/lengths/counts are big-
  endian, and text is strict UTF-8 already in NFC;
- runner and committer independently recompute before dispatch and before
  commit; old fixed-manifest, empty/random/substituted, drifted, uppercase,
  malformed, reordered, duplicate, unknown-version/tag, non-NFC, and .NET
  mixed-endian variants fail without normalization, dispatch, or persistence;
- the fixed order/inventory manifest is not production authority; no fixture
  SQL, direct `DbSet`, reflection, fabricated identifier, alternate profile,
  general discovery/clustering, scheduling, concurrency, retry, or multiple
  Pass 2 job exists;
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

The harness accepts exactly this closed environment contract and validates every
value before reserving a port or creating any PostgreSQL/Docker/browser resource:

| Variable | Secret | Validation | Purpose |
|---|---:|---|---|
| `VULGATA_STORY115_RUN_FINAL` | No | Exactly `1` | Explicitly activates the unique Release-only live lane. |
| `VULGATA_STORY115_POSTGRES_ADMIN_CONNECTION` | Yes | Non-empty PostgreSQL administrator connection containing `Host` and an administrator `Database`; the database must differ from the generated nonce database and the value is never logged or written to evidence | Supplies the existing PostgreSQL 17 administrator endpoint used only to prove absence, create, terminate connections to, drop, and re-prove absence of the harness-owned nonce database. The supplied administrator/development database is never migrated, selected for product use, terminated, or dropped. |
| `VULGATA_STORY115_ACCOUNT_REPOSITORY_URL` | No | Absolute `https`, `ssh`, or `file` URI | Acquires `mbs_account` at the fixed accepted ref/full commit. |
| `VULGATA_STORY115_CHANNEL_REPOSITORY_URL` | No | Absolute `https`, `ssh`, or `file` URI | Acquires `mbs_channel` at the fixed accepted ref/full commit. |
| `VULGATA_STORY115_CANDIDATE_COMMIT` | No | Exactly 40 lowercase hexadecimal characters | Binds the final image source-commit label. |
| `VULGATA_STORY115_CANDIDATE_TREE` | No | Exactly 40 lowercase hexadecimal characters | Binds the final image source-tree label. |
| `VULGATA_STORY115_PRODUCTION_MANIFEST_SHA256` | No | Exactly 64 lowercase hexadecimal characters | Binds the canonical production-source-manifest label. |

The accepted Repository refs and full commits, provider image, fixture API key,
demo-user credentials, resource nonce, Data Protection application name, and
ports are harness constants or cryptographically generated run-local values;
they are not additional environment inputs.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests --logger "trx;LogFileName=story-11-5-release-gate.trx"
```

`Story115ReleaseHarness` is the sole owner of the generated nonce database and
nonce-prefixed provider/app image/network/env/key-ring resources. It does not
own the existing PostgreSQL server or container. It proves this exact observed
path:

```text
clean migrations
-> mbs_account @ 19c60cd3f3976b7697c487c7bafd96142e3a7625
-> mbs_channel @ 47f6b627a7598f862873610413eec1e1e7bf46bf
-> profiling and accepted Pre-Scan Result
-> derive canonical actual ordinary Pass 1 Worker Job IDs and dispatch Pass 1
   for each Repository
-> exact account-to-channel Cross-Repository Resolution
-> one directed REFERENCES Edge and exact evidence
-> bounded runtime prepared-flow binding from product-created identities
-> validate one consumer-owned flow + exactly-two-Repository support fingerprint
-> exactly one unchanged seven-slot Pass 2 Worker dispatch
-> Pass 2 Business Logic Document + GENERATED_FROM provenance to every used version
-> bounded authorized retrieval
-> exact Candidate Claim extraction + selection-only provider result
-> bounded serializable post-provider currentness validation
-> final Repository authorization recheck
-> keep the same database alive and build the final image exactly once
-> curl health on /Account/Login
-> real Identity login + interactive Blazor positive claim/citation proof
-> negative Identity context + post-selection revocation missing-equivalent proof
-> terminate exact-database connections, drop only that database, prove absence,
   and remove all other harness-owned nonce resources
```

Provisioning must call `UserManager`, role/ownership coordinators, management
repositories/coordinators, Scan services, provider coordinators, and real
`SecretEncryptionService`; fixture SQL/`DbSet` insertion is forbidden. The
provisioner and final container share the exact gate-owned Data Protection ring
and application discriminator. The test fails on ref/commit drift, guessed
truth, ambiguous/stale/unauthorized runtime binding, any eighth Pass 2 dispatch
slot, provider-authored factual prose, invalid support, incomplete citations,
unauthorized disclosure, noninteractive browser proof, cleanup failure, or a
remaining database/resource. `finally` always runs.

The provider/app topology is part of the harness contract, not a schematic.
The harness creates a cryptographically nonce-owned publish-capable network
(without `--internal`) and chooses three distinct
available host ports; `workerPort` and `chatPort` are also the provider's
container ports, and neither may be `8080`. With all names/paths held as
harness variables, its provider invocation is equivalent to:

```pwsh
$pythonFixtureImage = 'python:3.12-bookworm@sha256:9bed8554e926c07c6f908841d5ee88c33e8df9236b191526bbce81a9062ab43a'
docker network create $gateNetwork
docker run --detach --name $providerContainer --network $gateNetwork `
  --publish "127.0.0.1:${workerPort}:${workerPort}" `
  --publish "127.0.0.1:${chatPort}:${chatPort}" `
  --publish "127.0.0.1:${appHostPort}:8080" `
  --mount "type=bind,source=$fixturePath,target=/fixture/loopback-openai-provider.py,readonly" `
  $pythonFixtureImage python /fixture/loopback-openai-provider.py `
  --host 0.0.0.0 --worker-port $workerPort --chat-port $chatPort
```

The existing PostgreSQL server/container is external infrastructure and is
not a member of or managed by `$gateNetwork`. Windows Docker Desktop supplies
`host.docker.internal` as a built-in hostname; no explicit host mapping is
used. After host-side administrator connectivity and PostgreSQL-major-version
validation, and before nonce database creation, migrations, or product
provisioning, preflight resolution and TCP reachability from the provider's
exact network namespace with the equivalent bounded command:

```pwsh
docker exec $providerContainer python -c "import socket; socket.getaddrinfo('host.docker.internal', 5432); s=socket.create_connection(('host.docker.internal', 5432), 5); s.close()"
```

Expected: exit code zero without printing credentials. A name-resolution or
TCP failure stops before product provisioning and enters cleanup; there is no
alternate host topology. Product-service provisioning and
host-side profiling/Scan store and call
`http://127.0.0.1:${workerPort}/v1`; the Chat assignment stores
`http://127.0.0.1:${chatPort}/v1`. The fixture listens on `0.0.0.0`, while all
three published mappings are restricted to host loopback. Equal
host/container provider port numbers are mandatory so the identical stored
URLs work after the final app joins this namespace.
The final app connection string targets only the nonce database and replaces
the host portion with Docker Desktop's built-in `host.docker.internal` for
container execution. No new environment variable is introduced.

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
`ConnectionStrings__DefaultConnection` targeting the nonce database through
`host.docker.internal`,
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
the nonce database and harness-owned provider/app/image/network/env/key
resources. Do not remove or stop the supplied PostgreSQL server/container or
touch its administrator database. Do not reuse a predecessor image.

## 8. Evidence and limitations

Record sanitized command exits/timestamps/hashes, candidate and image identity,
exact-profile/runtime-support fingerprints, independent ordinary Pass 1
canonical-byte/digest/Job UUID proof and persisted projection verification,
derived schema/Manifest/Flow identity validation, proof of the exact seven-slot single Pass 2
dispatch/commit, positive claim/citation counts,
authorization-negative result, database drop and absence, and cleanup
outcomes. Do not retain credentials, proprietary source, raw prompts/responses,
absolute private paths, internal runtime identities, or unauthorized details.

Report these as limitations, not acceptance claims: automatic scope selection,
file upload, saved sessions, full iterative retrieval tools, retry/failover,
concurrent Scan behavior, generalized Cross-Repository matching,
graph/dashboard/SignalR, and deferred Epic 5-10 capabilities.

## Optional extension hook disposition

`.specify/extensions.yml` registers optional
`speckit.agent-context.update` after planning. This planning run does not wait
for or execute the optional hook. The mandatory SpecKit context action is
satisfied directly by updating the `AGENTS.md` SPECKIT marker to this plan.
