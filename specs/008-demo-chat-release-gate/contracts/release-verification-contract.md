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
-> Sequential Scan Runner Pass 1 for both Repositories
-> exact configured Cross-Repository Resolution
-> one directed cross-Repository REFERENCES Edge + accepted evidence
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
