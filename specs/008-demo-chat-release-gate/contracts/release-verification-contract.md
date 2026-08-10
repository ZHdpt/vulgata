# Contract: Story 11.5 Release Verification

## Immutable inputs

- one clean accepted Vulgata candidate with source commit, tree, and canonical
  `production-source-manifest-v1` hash;
- one Windows Docker Desktop host in Linux-container mode; this T072 contract
  does not claim native-Linux-host support or define a fallback topology;
- the existing running PostgreSQL 17 Alpine administrator endpoint, supplied
  outside source control (observed as `localhost:5432`, administrator database
  `vulgata`), with the server/container treated as external non-owned
  infrastructure;
- prepared `mbs_account` ref `refs/tags/vulgata/August15th-sample` at full
  commit `19c60cd3f3976b7697c487c7bafd96142e3a7625`;
- prepared `mbs_channel` ref `refs/tags/vulgata/August15th-sample` at full
  commit `47f6b627a7598f862873610413eec1e1e7bf46bf`;
- one gate-owned Data Protection key directory/application discriminator and
  deterministic isolated Worker/Chat provider namespace;
- one prebuilt candidate-bound test-owned .NET provisioner entry whose mounted
  inventory contains no duplicate/colliding `Vulgata.*` product assembly;
- one authorized demo user and one authorization-negative user/scope.

Any ref/commit, source identity, provider, or profile mismatch fails before an
acceptance claim.

## Database lifecycle

The harness creates `vulgata_story115_<random>` using a cryptographically random
suffix. It must reject shared/default/production-looking names. Before step 1,
it validates the host-side PostgreSQL 17 administrator endpoint and preflights
Docker Desktop's built-in `host.docker.internal` DNS and TCP reachability from
the provider container's exact network namespace. Failure stops before nonce
database creation or product provisioning and enters bounded
cleanup. The run:

1. proves the name is absent;
2. before product provisioning, builds the final application image exactly
   once, inspects its source labels, and records its immutable image ID;
3. creates the database, then starts a temporary provisioner container from
   that image ID in the provider network namespace;
4. inside the provisioner applies all Identity/domain migrations, runs
   migration/model checks and the complete prepared product path, writes a
   closed sanitized result, and exits;
5. verifies the provisioner `.Image`, removes it and proves absence, then starts
   the app from the identical image ID;
6. keeps the database alive through final-container Playwright proof;
7. in `finally`, removes the provisioner/app/provider containers, terminates only
   connections to the exact disposable database, drops only that database,
   queries the catalog through the supplied administrator database to prove
   absence, then removes the final image, publish-capable provider/app network,
   source/config/output/work/env/key/browser state, and exact nonce root.

The harness never creates, stops, removes, reconfigures, or claims ownership of
the supplied PostgreSQL server/container. It never selects, migrates, uses for
product data, terminates connections to, or drops the supplied administrator
database or another development database. Server validation is limited to the
required PostgreSQL major version and connectivity needed for the nonce
database lifecycle.

Primary failure and cleanup failure are both retained. Cleanup failure makes the
release gate fail. No credentials or connection string is written to evidence.

## Prepared vertical slice

The observed order inside the temporary provisioner is:

```text
users/roles + prepared management + authorization + encrypted providers
-> exact Git ref/full-commit acquisition for both Repositories
-> deterministic profiling and accepted Pre-Scan Result
-> Sequential Scan Runner derives and dispatches canonical actual ordinary
   Pass 1 Worker Job IDs for both Repositories
-> exact configured Cross-Repository Resolution
-> one directed cross-Repository REFERENCES Edge + accepted evidence
-> bounded runtime prepared-flow binding from current product-created identities
-> runner validates the runtime flow/support fingerprint
-> exactly one unchanged seven-slot Pass 2 Worker dispatch
-> Pass 2 Business Logic Document and GENERATED_FROM provenance
-> current database-backed Chat evidence retrieval
-> one closed validated selection-only Business or IT result
-> bounded serializable database currentness validation
-> final Repository authorization recheck
-> fully cited rendered Grounded Answer
```

Assertions include exact Profiled Revision continuity, immutable/current
Document Versions, available Source Anchors, exact Edge direction, zero guessed
Edge, exact retained Candidate Claim text, immutable support signatures, and
complete claim-to-citation coverage. Retrieval ends before provider dispatch;
the separate post-provider read proves the same current Document Versions,
Stable Sections/exact spans, safe same-version positive-range Source Anchors,
and required current authorized `REFERENCES`/`GENERATED_FROM` support before the
final Repository authorization and render. No transaction spans provider
latency. The inaccessible-scope run is
missing-equivalent and exposes zero identity, metadata, citation, source,
relationship, or business detail.

Provisioning uses `UserManager<ApplicationUser>`, `RoleSeeder`,
`IAdministratorRoleCoordinator`, `ISystemOwnershipCoordinator`, management
repositories/coordinators, the production profiling/Sequential Scan Runner/
exact resolver/Pass 2 services, `ILlmProviderManagementCoordinator`, and
`ISystemLlmProviderOverrideCoordinator`. Fixture SQL or `DbSet` row insertion
is prohibited. Real `SecretEncryptionService` uses the gate key ring. The
provisioner and runtime must receive identical optional
`DataProtection:KeyRingPath` and `DataProtection:ApplicationName` values.
Repository origins are exact accepted ref/full-commit clean checkouts mounted
read-only at `/vulgata/sources/mbs_account` and
`/vulgata/sources/mbs_channel`; product Git services clone them into the
provisioner's writable nonce work mount. Profiling MUST resolve the final
image's hash-pinned Linux CodeGraphContext 0.5.1 runtime. Host `cgc`, host
Python/pip, and fallback resolution are prohibited.

### Ordinary Pass 1 Job Identity contract

Every newly dispatched ordinary Pass 1 packet uses an actual server-derived
Worker `JobId`; no random or planner-created member identity is accepted. The
sequential runner derives it immediately before dispatch from exactly:

```text
ScanExecutionId
ScanRunId
RepositoryId
CodeUnitId
ProfiledRevision identity/fingerprint
```

The derivation reuses the canonical binary mechanics of
`vulgata.prepared-pass2-binding-c14n/1` under the distinct ASCII magic
`VULGATA-ORDINARY-PASS1-JOB` and version byte `0x01`. The only fields are tags
`0x0001` through `0x0005` in the order above. UUID values are 16 RFC-network-
order bytes. Profiled Revision is one full lowercase 40- or 64-character Git
object ID decoded to 20 or 32 raw bytes. Each field is
`tag:u16be || length:u32be || value`; unknown, missing, duplicate, reordered,
or empty fields and any other version are invalid.

Job ID is UUIDv8-SHA256 of the complete envelope: take SHA-256 bytes 0-15, set
the version-8 and RFC-variant bits, and preserve RFC network order. UUID text,
JSON, platform-native/mixed-endian Guid bytes, and trimmed, uppercased,
abbreviated, odd-length, or malformed revision text are rejected without
repair. The normative inputs, all 142 canonical bytes, full digest, and
expected Job UUID `6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd` are fixed in
[data-model.md](../data-model.md).

The runtime planner accepts no supplied member Job ID. It independently reads
the current Scan execution, ScanRun, Repository, Code Unit, and Profiled
Revision, recomputes the ID, and requires the current ordinary Code Logic
Document Version ID to equal the existing committer projection
`CreateStableId("pass1-document-version", jobId.ToString("D"))`. This forward
projection check is mandatory because a valid Pass 1 result clears
`ScanExecution.InFlightJobId`; it proves that the current Document Version was
created for the same actual deterministic Worker job without recovering or
persisting the old transient value.

The identity is repeatable for the same logical boundary, but this contract
does not add or change retry scheduling, duplicate-dispatch permission, or
commit idempotency. Tests independently construct the golden bytes; repeat
the same boundary; mutate every input separately; prove tuple framing and
Pass 1/Manifest/Flow domain separation; and reject collision/duplicate,
legacy/random, substitution, projection mismatch, drift, malformed,
noncanonical, and mixed-endian cases before Pass 2 dispatch. Existing random-
ID Documents are not migrated or backfilled and are unsupported for this
prepared flow. The final accepted run begins from a clean database.

### Runtime Pass 2 binding contract

After exact resolution has committed the accepted directed
`mbs_account` -> `mbs_channel` `REFERENCES` Edge, the production
`PreparedDemoPass2FlowPlanner` performs one authorized bounded relational read.
The reviewed exact-resolution profile is the only selector authority. Runtime
System, Repository, Scan execution/ScanRun, Worker job, Code Unit, Logical
Document, Document Version, Source Anchor, and relationship identities must
come from current product-created records; no embedded or fabricated database
identity is accepted.

The planner requires:

- exactly one common visible System and exactly the two profile-bound
  Repositories;
- the consumer Repository as Business Logic Document owner;
- exactly-two-Repository support in consumer/provider order;
- exact profile ID/version/fingerprint, direction, refs, full commits, and
  Profiled Revisions;
- exactly one current authorized mapping for every required consumer/provider
  selector and the directed relationship; and
- every Code Logic Document Version and Source Anchor actually used in the
  prepared evidence.

The planner alone creates the payload identity slots. The schema slot is
the only closed mapping from accepted exact-profile schema/version `1.0` to
`vulgata.prepared-pass2-flow/1.0`; other versions are refused. `ManifestId` is
UUIDv8-SHA256 over the version-1 manifest envelope of schema identity plus
accepted profile ID/version/raw fingerprint. `FlowId` is UUIDv8-SHA256 over the
version-1 flow envelope of derived Manifest ID, accepted occurrence/direction,
actual common System, ordered consumer/provider bindings (aliases, stable Git
identities, Repository/ScanRun/Scan execution identities, refs, raw commits,
raw Profiled Revisions), and accepted relationship identity. Callers cannot
provide any of these three values.

All derivation and support bytes use
`vulgata.prepared-pass2-binding-c14n/1`: the normative envelope magics, numeric
tags, nested record tags, and field order are defined in
[data-model.md](../data-model.md). Encoding is ASCII magic + NUL + version 1,
then closed unique increasing `u16be` tags, `u32be` lengths, RFC-network-order
UUIDs, strict UTF-8 already-NFC text, raw-decoded exact lowercase hex, and
explicit count/length-delimited lists. UUIDv8-SHA256 takes SHA-256 bytes 0-15,
sets version 8 and RFC variant bits, and preserves RFC network order.

The support fingerprint is lowercase hexadecimal SHA-256 over the exact
support envelope. The runner recomputes the identities and raw digest
immediately before dispatch. The committer re-reads current support and repeats
the same recomputation immediately before commit, then creates
`GENERATED_FROM` provenance to every Code Logic Document Version actually
used. Neither boundary accepts a caller-supplied or previously trusted
identity/fingerprint. Any missing, duplicate, stale, unauthorized,
wrong-System, wrong-profile, wrong-direction, wrong-ref, wrong-commit,
wrong-Profiled-Revision, identity, encoding, or support mismatch yields zero
Pass 2 dispatches or zero generated Documents at its boundary.

The Worker dispatch contract remains exactly these seven top-level slots:

```text
PreparedFlowSchemaIdentity
ManifestId
FlowId
SystemId
RepositoryId                  # consumer owner
TraceEvidence
CodeLogicDocumentVersions     # exact ordered used set
```

No eighth slot is permitted. The support fingerprint and all additional
runtime binding identities remain server-side and are never serialized to the
Worker. The fixed order/inventory manifest is not production authority; it may
remain only as a non-production fixture. Fixture SQL, direct `DbSet`,
reflection, fabricated identifiers, an alternate exact profile, general flow
discovery/clustering, scheduling, concurrency, retry, and multiple Pass 2 jobs
are prohibited.

Identity/codec verification requires an independently constructed hard-coded
golden vector for canonical bytes, Manifest UUID, Flow UUID, and lowercase
support fingerprint. Rejection before dispatch and before commit covers the
old fixed-manifest IDs; random/substituted/empty Manifest or Flow IDs; changed
profile, binding, relationship, member, Anchor, or trace fields; uppercase,
prefixed, spaced, or wrong-length fingerprints; duplicate/reordered/unknown
tags and list items; non-NFC or invalid UTF-8 text; unsupported envelope
version; and .NET mixed-endian UUID bytes. Rejection never repairs,
normalizes, or falls back to the fixture.

## Provider/app network contract

The harness creates one cryptographically nonce-owned publish-capable Docker
network and chooses three
distinct available ports: `workerPort`, `chatPort`, and `appHostPort`; neither
provider port is `8080`. The stdlib fixture in the Dockerfile-pinned Python
image is invoked with `python /fixture/loopback-openai-provider.py --host
0.0.0.0 --worker-port <workerPort> --chat-port <chatPort>`. The provider
container has exactly these loopback-host publications. `--internal` is
forbidden because Docker Desktop suppresses published-port access on that
topology:

- `--publish 127.0.0.1:<workerPort>:<workerPort>`;
- `--publish 127.0.0.1:<chatPort>:<chatPort>`; and
- `--publish 127.0.0.1:<appHostPort>:8080`.

The provisioner joins `--network container:<providerContainer>` and stores/calls
`http://127.0.0.1:<workerPort>/v1`; Chat stores
`http://127.0.0.1:<chatPort>/v1`. Equal host/container provider port numbers
are mandatory. The final app uses `--network container:<providerContainer>`,
`--env-file <gateEnvFile>`, and
`--mount type=bind,source=<keyRingPath>,target=/var/lib/vulgata-dpkeys,readonly`,
with no app-side `--publish`. It therefore reaches the identical stored
provider URLs on shared namespace loopback, while the provider container's
third mapping exposes app port `8080` only as
`127.0.0.1:<appHostPort>` for Playwright. Binding the fixture only to container `127.0.0.1`, using unequal
provider host/container ports, or combining `--publish` with
`--network container:` fails the gate.

The external PostgreSQL server/container is not a member of this network. The
provisioner and final-app connection strings retain the nonce database name and credentials
but uses Windows Docker Desktop's built-in `host.docker.internal` to reach the
host-published PostgreSQL endpoint. No explicit host mapping is passed to the
provider, provisioner, or app container. Before database creation/product provisioning, the provider
container runs a bounded Python `socket.getaddrinfo` plus
`socket.create_connection` probe to `host.docker.internal:5432`; DNS or TCP
failure fails closed. This topology adds no environment variable and exposes
every harness-published port only on host `127.0.0.1`.

## Provisioner container contract

The provisioner command names the recorded immutable image ID and overrides
only the entrypoint to `dotnet /app/Vulgata.Story115.Provisioner.dll`. Exactly
three unique candidate-bound driver files (`.dll`, `.deps.json`,
`.runtimeconfig.json`) are individually mounted read-only into `/app`. Before
container creation, compare their names against the inspected `/app` inventory;
reject collisions and any mounted `Vulgata.Core`, `Vulgata.Infrastructure`,
`Vulgata.Shared`, `Vulgata.Agents`, or `Vulgata.Web` assembly. Product
assemblies and the `cgc` runtime must resolve from the image only.

Required mounts:

- accepted `mbs_account` and `mbs_channel` origins read-only at the fixed
  `/vulgata/sources/<alias>` paths;
- permission-restricted input read-only at `/vulgata/run/config/input.json`;
- nonce output writable at `/vulgata/run/output`;
- nonce Git/CodeGraph work writable at `/var/lib/vulgata-codegraph`;
- nonce Data Protection ring writable at `/var/lib/vulgata-dpkeys` during
  provisioning and mounted read-only for the later app.

The input contains secrets and is never evidence. The result is atomically
renamed to `result.json`, closed-schema and size-bounded, and rejects unknown
fields, wrong nonce/schema, credentials, connection strings, provider keys,
source/Document text, raw prompts/responses/diagnostics, absolute host paths,
or unauthorized metadata. The provisioner is a temporary container instance,
not an image/deployment artifact, and must be removed/proved absent before app
creation.

## Candidate checks

The Chat tool and both prior material Scan changes are already covered by the
durable planned-change authorizations in the Story record and
`workflow-state.json`. Plan-review iteration 10 accepted the exact Option A
topology, and the user then granted the distinct Option A plan-to-tasks
authorization at `2026-08-10T01:12:29Z`. Relocation of the release Scan remains
a conditional plan-review trigger, so this corrected `expedited-mvp` plan MUST
receive a fresh independent plan `ACCEPT` before task regeneration; the
unchanged Option A authorization is not requested again.

The active gate/task sequence is closed: the root regenerates expedited tasks
and mechanically validates requirement/task/test-first/security/migration
coverage; T064-T071 perform only non-live Option A RED/GREEN and verification;
one fresh combined acceptance-and-quality review accepts the pinned candidate;
T072 alone runs the live same-image Windows Docker Desktop/PostgreSQL/provider/
Repository/Playwright proof; and T073 independently verifies database/resource
cleanup, absence, retained checks, unchanged boundary, and final completion
eligibility. `spec-review`, active `speckit-analyze`, and separate
`acceptance-review` are omitted at iteration zero. The prior Chat/Pass 1/Pass 2
approvals are not duplicated, and no retained TDD, security, migration,
clean-database E2E, Docker, cleanup, or verification gate is weakened.

The same candidate must pass:

- release-harness command/lifecycle tests proving one build, immutable-ID use
  and equality for provisioner/app, driver collision rejection, exact mount
  access, provider namespace, built-in PostgreSQL preflight, source ref/commit
  continuity, sanitized result rejection cases, provisioner removal before app,
  and unconditional cleanup after failure at every acquired-resource phase;
- Story 11.5 contract, retrieval, agent, coordinator, UI, and integration tests;
- ordinary Pass 1 Job Identity tests proving independent canonical bytes and
  the pinned UUIDv8 vector, same-boundary repeat identity, mutation of every
  bound input, tuple/domain collision separation, runner dispatch, planner
  independent recomputation and Document Version projection verification, and
  fail-closed legacy/random/duplicate/substituted/drifted/noncanonical cases;
- runtime prepared-flow planner/runner/committer tests proving product-created
  identities, independent canonical-byte/UUID/fingerprint golden vectors, the
  exact seven-slot payload, one valid dispatch/commit, and zero dispatches/
  Documents for every fixed-manifest, substituted, drifted, noncanonical, and
  other fail-closed variant;
- barrier-controlled currentness tests that mutate Document Version and fixed
  Source Anchor/relationship support after retrieval/provider dispatch but
  before validation and require whole-answer missing/stale rejection with zero
  claims/citations;
- relevant authorization/security/static scans;
- predecessor migration lifecycle plus no pending EF model changes;
- Web Release build with analyzers;
- explicit `dotnet restore tests/Vulgata.Tests/Vulgata.Tests.csproj`, then
  `dotnet build tests/Vulgata.Tests/Vulgata.Tests.csproj --configuration Release`,
  then the generated
  `tests/Vulgata.Tests/bin/Release/net10.0/playwright.ps1 install chromium`;
- all applicable regression tests, including PostgreSQL-tagged groups;
- zero unresolved/failing RED test and zero placeholder/template marker.

## Final Docker proof

In T072, exactly one accepted final runtime image is built after T064-T071,
root coverage validation, candidate pinning, and the fresh combined quality
`ACCEPT`, and before nonce database creation/product provisioning:

1. require clean immutable Git status;
2. create/validate the existing canonical production-source manifest;
3. build `docker/Dockerfile` with exact
   `VULGATA_SOURCE_COMMIT`, `VULGATA_SOURCE_TREE`, and
   `VULGATA_PRODUCTION_SOURCE_MANIFEST_SHA256` arguments; the Dockerfile MUST
   use the user-authorized Tsinghua mirror for ordinary Ubuntu Noble,
   Noble updates/backports, and the hash-locked pip index, MUST retain the
   official Ubuntu Noble security archive, and MUST retain pip
   `--require-hashes` plus bounded retry/timeout behavior;
4. record immutable image ID and inspect the three matching labels;
5. start a temporary provisioner with `--entrypoint dotnet` from that exact ID,
   in the provider namespace, with only candidate-bound driver/source/config
   read-only mounts and nonce output/work/key writable mounts;
6. require migrations/product profiling/Scan to complete inside it, validate
   its closed sanitized output, inspect `.Image` equal to the recorded ID, then
   remove/prove absence before app start;
7. start the app from the same ID against the still-live nonce database through
   preflighted built-in `host.docker.internal`, the same key ring, and provider;
8. inspect app `.Image` equal to the recorded ID and configure health as `curl --fail --silent --show-error
   http://127.0.0.1:8080/Account/Login` with bounded polling (`curl` is in the
   runtime image);
9. use Microsoft.Playwright 1.61.0 with matching Chromium to authenticate via
   the real Identity form, wait for the interactive Blazor circuit, operate
   selectors/send/citation controls, and assert positive claims/citations;
10. use a second real Identity context and a post-selection ownership
   revocation to prove missing-equivalent behavior and zero leaked/stale
   claim/citation content;
11. remove only the nonce database and harness-owned nonce-prefixed provider/
   provisioner/app/image/network/source/config/output/work/env/key/browser
   resources, leaving the supplied PostgreSQL
   server/container and administrator database untouched, and record every
   cleanup outcome.

A second application/provisioner image, previously built or predecessor image
is not acceptable. An unlabelled,
dirty-source, mismatched-label, unhealthy, or cleanup-failed image fails the
gate.

Static SSR markup, `HttpClient`, direct service calls, or bUnit do not satisfy
steps 9-10. The provider fixture must be isolated from external providers and
return Worker content plus Chat candidate selections only.

## Evidence output

The release evidence bundle records only sanitized facts and hashes:

- candidate commit/tree/manifest and image ID/labels;
- exactly-one-build count, provisioner/app `.Image` equality, proof the
  provisioner was removed before app start, and absence of a provisioner image;
- candidate-bound driver inventory, fixed source/config/output/work/key mount
  identities, provider-namespace/PostgreSQL preflight, in-image
  CodeGraphContext 0.5.1 identity, and sanitized provisioner-result hash;
- prepared aliases/refs/commits and exact profile fingerprint;
- runtime Pass 2 support fingerprint plus sanitized proof of consumer
  ownership, exactly-two-Repository support, exact seven-slot dispatch, and one
  Pass 2 dispatch/commit;
- test/build/migration/E2E/Docker command, exit status, timestamp, and bounded
  log hash/location;
- positive answer mode, claim count, citation count, and validation outcome
  without proprietary answer/source text;
- negative authorization outcome;
- disposable database name, drop result, and absence observation;
- per-resource cleanup/absence outcomes and truthful known post-demo limitations.

The bundle never contains credentials, plaintext provider key, absolute private
Repository paths, proprietary source, raw model prompt/response, or
unauthorized metadata.

## Known limitations reported separately

Automatic scope determination, file upload, saved chat history, iterative/full
V1 retrieval tools, retry/failover, concurrent scanning, generalized matching,
graph/dashboard/SignalR, and deferred Epic 5-10 behavior are not release claims.
