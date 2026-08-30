# Prepared Scan initiation identity-authority verification

- Story: 12.5
- Tasks: T025-T029
- Candidate base: `9d626259c0d64173fb5269be6690d6fbe455dd58`
- Accepted scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`
- Implementation approval: `story-12-5-implementation-approval-prepared-pass1-identity-authority-02`

## Contract identity

The sole Prepared Pass 1 identity source reads embedded resource
`Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md` with strict UTF-8 and publishes:

- prompt identity: `pass1-code-logic-worker.v2/sha256/5b1e48e50b83c5d12b7916d72d1973f4984db8bf60d1fa62df5cb9f4bcd27e7b`
- Document schema identity: `code-logic/file-v2`

Hostile missing, duplicate, empty, invalid-UTF8, malformed-hash, legacy-identity and schema-inconsistent authorities fail closed before fresh authorization, durable creation or external analysis. The existing `WorkerAgentDispatchIdentitySource` remains unchanged at its v1 recovery identity and `vulgata.knowledge-document/1.0` schema.

## RED/GREEN chronology

1. T025 observed `1 passed / 3 expected RED`: the dedicated Core port and Agents source were absent, the initiation coordinator used the legacy recovery identity, and the Web rechecker imported EF/`VulgataDbContext`.
2. T026-T028 added the dependency-free Core contract, the sole Agents embedded source, the scoped production registration, and materialized Core authorization reads. Focused identity/coordinator verification became `18 passed / 0 failed`.
3. The PostgreSQL public seam then exposed three latent integration defects: EF retry execution strategies wrapped user transactions incorrectly; PostgreSQL microsecond timestamp normalization made the durable tuple appear stale; and Manifest publication assumed a pre-existing legacy `PreScanResult`. Minimal GREEN changes put each complete transaction inside its execution strategy, canonicalized the initiation instant, and let the accepted publication owner create the prepared-v2 result when absent while retaining the compatibility-upgrade path.
4. Two stale public-test fixtures were corrected: the ready-runtime scenario now installs its deterministic runtime identity, and the retry scenario injects transient `AnalysisNotReady` rather than a correctly non-retryable source-identity conflict. Durable assertions now distinguish the stable initiation tuple from later Manifest/File Code Unit publication.

## Verification

The exact public identical-request case passed `1/1`. The corrected ready-runtime and transient retry cases passed `2/2`. The combined identity, coordinator, runtime-resolver, public API, PostgreSQL initiation, atomic publication and store regression filter completed:

`100 passed, 1 environment-dependent skip, 0 failed (101 total)`.

The public cases prove ordinary-record entry, exact two-Repository binding, two Scan Runs, two Scan Executions, one coordination, two prepared-v2 `PreScanResult`s, two Prepared Scan Manifests, 120 selected File Code Units, identical replay without duplicates, mismatched pair/revision rejection, initial and fresh authorization denial with zero writes, transient phase recovery, and no Logical Document or later-stage action.

Every PostgreSQL test used a fresh lowercase `vulgata_story125_<32 hex>` database created from the approved local PostgreSQL 17 administrative authority and disposed by the test fixture. No retained Track C database was contacted. Credentials and connection strings were process-local and were not printed or persisted.

## Composition and guards

- `PreparedScanInitiationAuthorizationRechecker.cs` contains no `Microsoft.EntityFrameworkCore` or `VulgataDbContext` reference; it resolves materialized `ISystemRepository` and `IRepositoryRepository` seams together with Web Identity role checks.
- Default DI binds `IPreparedPass1ContractIdentitySource` to `EmbeddedPreparedPass1ContractIdentitySource` exactly once.
- The entrance retains the existing CodeGraph 1.5/JDT-LS/safe-XML Prepared Scan ports. It neither resolves CGC nor restores the historical profiling coordinator.
- No EF migration, model snapshot, `VulgataDbContext` model shape, Story 12.4 contract, Worker dispatch source, Agent tool, general Scan lifecycle or later-stage behavior changed.

Verdict: T025-T029 are verified GREEN for the accepted bounded correction.

## QR-PSI-001 correction verification

Quality review attempt 01 correctly found that the earlier hostile-identity
claim exceeded the executable evidence. The corrected suite now directly
executes null, legacy-v1, malformed-hash, wrong-schema and throwing identity
sources and proves zero runtime-resolution, fresh-authorization,
initiation-store and pair-builder/preparation calls. It also executes missing,
duplicate, empty, invalid-UTF8 and schema-inconsistent embedded resources and
binds the exact decoded-text SHA-256.

The focused hostile/ordering matrix passed `29/29`. The complete corrected
identity, coordinator, runtime-resolver, public API, PostgreSQL initiation,
atomic publication and store filter passed `111`, skipped one explicitly
environment-dependent concurrent-store case, and failed zero (`112 total`).
All PostgreSQL databases remained fresh disposable lowercase
`vulgata_story125_<32 hex>` instances.

## QR-PSI-002 correction verification

Quality review attempt 02 found that strict embedded-resource validation ran
during scoped DI activation, before the coordinator could map failure to the
accepted sanitized 503 contract. A public authenticated endpoint RED observed
HTTP 500 with zero side effects. The minimal correction defers the unchanged
strict validation to `GetCurrent`; hostile resource failures now traverse the
coordinator's existing fail-closed mapping.

The source/coordinator/public boundary passed `41/41`. The broader identity,
initiation, PostgreSQL, atomic, store and runtime-resolver boundary passed
`116`, skipped one known environment-dependent concurrency case, and failed
zero (`117 total`).
