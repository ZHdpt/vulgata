# Contract: Story 11.5 Release Verification

## Immutable inputs

- one clean accepted Vulgata candidate with source commit, tree, and canonical
  `production-source-manifest-v1` hash;
- PostgreSQL 17 administrator connection supplied outside source control;
- prepared `mbs_account` ref `refs/tags/vulgata/August15th-sample` at full
  commit `19c60cd3f3976b7697c487c7bafd96142e3a7625`;
- prepared `mbs_channel` ref `refs/tags/vulgata/August15th-sample` at full
  commit `47f6b627a7598f862873610413eec1e1e7bf46bf`;
- one gate-owned Data Protection key directory/application discriminator and
  deterministic isolated Worker/Chat provider namespace;
- one authorized demo user and one authorization-negative user/scope.

Any ref/commit, source identity, provider, or profile mismatch fails before an
acceptance claim.

## Database lifecycle

The harness creates `vulgata_story115_<random>` using a cryptographically random
suffix. It must reject shared/default/production-looking names. The run:

1. proves the name is absent;
2. creates the database and applies all Identity/domain migrations;
3. runs migration/no-pending-model checks;
4. executes feature gates;
5. keeps the database alive through final-container Playwright proof;
6. in `finally`, removes the app/provider containers, terminates only
   connections to the exact disposable database, drops only that database,
   queries the catalog to prove absence, then removes its PostgreSQL
   container/volume, network, key directory, and remaining nonce-owned state.

Primary failure and cleanup failure are both retained. Cleanup failure makes the
release gate fail. No credentials or connection string is written to evidence.

## Prepared vertical slice

The observed order is:

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

The harness creates one nonce-owned internal Docker network and chooses three
distinct available ports: `workerPort`, `chatPort`, and `appHostPort`; neither
provider port is `8080`. The stdlib fixture in the Dockerfile-pinned Python
image is invoked with `python /fixture/loopback-openai-provider.py --host
0.0.0.0 --worker-port <workerPort> --chat-port <chatPort>`. The provider
container has exactly these loopback-host publications:

- `--publish 127.0.0.1:<workerPort>:<workerPort>`;
- `--publish 127.0.0.1:<chatPort>:<chatPort>`; and
- `--publish 127.0.0.1:<appHostPort>:8080`.

The host-side profiling/Scan path and provisioner store and call
`http://127.0.0.1:<workerPort>/v1`; Chat stores
`http://127.0.0.1:<chatPort>/v1`. Equal host/container provider port numbers
are mandatory. The final app uses `--network container:<providerContainer>`,
`--env-file <gateEnvFile>`, and
`--mount type=bind,source=<keyRingPath>,target=/var/lib/vulgata-dpkeys,readonly`,
with no app-side `--publish`. It therefore reaches the identical stored
provider URLs on shared namespace loopback, while the provider container's
third mapping exposes app port `8080` only as
`127.0.0.1:<appHostPort>` for Playwright. The provider container's internal
network attachment also gives the app access to the still-live PostgreSQL
container. Binding the fixture only to container `127.0.0.1`, using unequal
provider host/container ports, or combining `--publish` with
`--network container:` fails the gate.

## Candidate checks

The same candidate must pass:

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

Exactly one accepted final runtime image is built after all code/test gates:

1. require clean immutable Git status;
2. create/validate the existing canonical production-source manifest;
3. build `docker/Dockerfile` with exact
   `VULGATA_SOURCE_COMMIT`, `VULGATA_SOURCE_TREE`, and
   `VULGATA_PRODUCTION_SOURCE_MANIFEST_SHA256` arguments;
4. record image ID and inspect the three matching labels;
5. start the image against the still-live prepared PostgreSQL 17 database,
   mounted key ring, and isolated provider fixture;
6. configure `docker run` health as `curl --fail --silent --show-error
   http://127.0.0.1:8080/Account/Login` with bounded polling (`curl` is in the
   runtime image);
7. use Microsoft.Playwright 1.61.0 with matching Chromium to authenticate via
   the real Identity form, wait for the interactive Blazor circuit, operate
   selectors/send/citation controls, and assert positive claims/citations;
8. use a second real Identity context and a post-selection ownership
   revocation to prove missing-equivalent behavior and zero leaked/stale
   claim/citation content;
9. remove only nonce-prefixed gate resources and record every cleanup outcome.

A previously built or predecessor image is not acceptable. An unlabelled,
dirty-source, mismatched-label, unhealthy, or cleanup-failed image fails the
gate.

Static SSR markup, `HttpClient`, direct service calls, or bUnit do not satisfy
steps 7-8. The provider fixture must be isolated from external providers and
return Worker content plus Chat candidate selections only.

## Evidence output

The release evidence bundle records only sanitized facts and hashes:

- candidate commit/tree/manifest and image ID/labels;
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
- cleanup outcomes and truthful known post-demo limitations.

The bundle never contains credentials, plaintext provider key, absolute private
Repository paths, proprietary source, raw model prompt/response, or
unauthorized metadata.

## Known limitations reported separately

Automatic scope determination, file upload, saved chat history, iterative/full
V1 retrieval tools, retry/failover, concurrent scanning, generalized matching,
graph/dashboard/SignalR, and deferred Epic 5-10 behavior are not release claims.
