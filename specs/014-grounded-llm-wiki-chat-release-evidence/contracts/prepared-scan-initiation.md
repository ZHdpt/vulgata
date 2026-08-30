# Contract: Prepared Scan Initiation

## Status and scope

This is the closed Story 12.5 contract for entering the existing exactly-two-Repository Prepared Scan path from ordinary production records. It adds no endpoint, request field, persistence shape, legacy profiling bridge or general Scan lifecycle.

## Public API

`POST /api/systems/{systemId}/prepared-scans`

The existing `PreparedScanRequest` remains unchanged:

```json
{
  "repositories": [
    {
      "repositoryId": "00000000-0000-0000-0000-000000000000",
      "gitRef": "refs/heads/example",
      "profiledRevision": "0123456789abcdef0123456789abcdef01234567"
    },
    {
      "repositoryId": "00000000-0000-0000-0000-000000000000",
      "gitRef": "refs/tags/example",
      "profiledRevision": "89abcdef0123456789abcdef0123456789abcdef"
    }
  ]
}
```

The IDs above illustrate shape only. A valid request requires two distinct non-empty Repository IDs in the route System, safe full refs, and exact lowercase 40-hex expected Profiled Revisions. Runtime, prompt, Document schema, tuple, lock and authorization identities are server-owned and are never request fields.

Successful responses remain the existing `PreparedScanResponse`:

- `201 Created` when the prepared pair package is first committed;
- `200 OK` when the same prepared pair package replays.

The response exposes prepared Manifest/handoff identities only. It does not expose initiation Scan Execution IDs, effective-authorization fingerprints, advisory-lock keys, runtime JSON, or protected existing-tuple diagnostics.

## Failure mapping

| Condition | HTTP | Problem code |
| --- | --- | --- |
| unauthenticated | 401 | existing authentication behavior |
| `ManagementAccess` denied | 403 | `prepared-scan-forbidden` |
| malformed route/request/cardinality/ref/revision | 400 | `invalid-prepared-scan-request` |
| invisible System/Repository or unauthorized effective scope | 404 | `prepared-scan-not-found` |
| existing tuple is mismatched, legacy, partial, ambiguous or incompatible | 409 | `prepared-scan-initiation-conflict` |
| configured Prepared Pass 1 contract or runtime identity is not Ready/mismatched | 503 | `prepared-scan-initiation-unavailable` |
| advisory-lock timeout, cancellation before commit or persistence failure | 503 | `prepared-scan-initiation-unavailable` |
| downstream prepared-package conflict/rejection/unavailability | existing mapping | existing prepared codes |

Denied/missing outcomes disclose no Repository, revision, tuple or authorization detail and create no initiation row.

## Application contract

`PreparedScanInitiationCoordinator` owns this sequence:

1. validate the authenticated `IUserContext` and exactly one initially visible System;
2. read exactly two distinct Repositories in that System and normalize canonical order;
3. obtain `PreparedPass1ContractIdentity` through Core `IPreparedPass1ContractIdentitySource`; accept only prompt identity `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}` and exact Document schema identity `code-logic/file-v2` from the sole Agents production source;
4. obtain one complete canonical `analysis-runtime/1` JSON/hash for each Repository/Profiled Revision through internal `IPreparedScanRuntimeIdentityResolver`; sole default `ConfiguredPreparedScanRuntimeIdentityResolver` rechecks configured tool integrity and is also used by runtime readiness;
5. after identity resolution, perform a fresh isolated-scope live authorization/ownership recheck: reload the user and roles, verify Repository membership, and require either current Administrator role or both current SystemOwner role and current SystemOwner assignment;
6. on any identity unavailability/mismatch return Unavailable before the rechecker/store; on any authorization revocation/drift return missing-equivalent NotFound with zero store calls; otherwise call `IPreparedScanInitiationStore.CreateOrReplayAsync` immediately with the newly observed effective mode;
7. outside the transaction, apply the phase-aware replay table below: call `PreparedScanCoordinator.RecoverPreparedAsync(authorizationScope, tuple, cancellationToken)` to reuse the existing handoff source/mapper without analysis, or call `PrepareInitiatedAsync` only for a new/exact retryable pre-publication tuple;
8. require `IPreparedScanPairBuilder.BuildAsync(tuple, repositories, revisionBindings, cancellationToken)` to load/validate the exact Runs/executions/coordination by ID and preserve the third live authorization recheck immediately before atomic Prepared Scan publication.

The production endpoint resolves only `PreparedScanInitiationCoordinator`. Its constructor requires `IPreparedPass1ContractIdentitySource` and MUST NOT accept `IScanRecoveryDispatchIdentitySource`. The tuple-less coordinator/builder call shape is replaced, not retained as a production fallback.

No source-acquisition, analysis, Worker, Pass, Document, Edge, Uncertainty, Chat or LLM port is available to the initiation store.

## Prepared Pass 1 contract-identity port

The Core contract is closed and non-executing:

```text
IPreparedPass1ContractIdentitySource.GetCurrent()
  -> PreparedPass1ContractIdentity? {
       promptIdentity,
       documentSchemaIdentity
     }
```

The sole production implementation is Agents-owned `EmbeddedPreparedPass1ContractIdentitySource`. It:

1. resolves exactly one embedded logical resource `Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md`;
2. decodes the resource with strict UTF-8 and rejects empty or schema-inconsistent content;
3. hashes the exact decoded string re-encoded as UTF-8 with SHA-256;
4. returns `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}` and `code-logic/file-v2`.

It does not expose prompt text and has no System, Repository, provider, dispatch, retry, scheduling, parsing, persistence, Agent-tool or public-API input. Missing, duplicate, invalid or mismatched authority maps to the existing bounded 503 before fresh authorization or store access.

`IScanRecoveryDispatchIdentitySource`/`WorkerAgentDispatchIdentitySource` is explicitly not this port. It remains unchanged for durable recovery with v1 prompt identity and `vulgata.knowledge-document/1.0`; neither initiation nor its tests may adapt that legacy value into the new-path contract.

## Core persistence port

The Core contract is specific, materialized and non-EF:

```text
IPreparedScanInitiationStore.CreateOrReplayAsync(
  PreparedScanInitiationCommand command,
  CancellationToken cancellationToken)
  -> PreparedScanInitiationResult
```

Closed outcomes: `Created`, `Replay`, `NotFound`, `Conflict`, `Rejected`, `Cancelled`, `Unavailable`.

A successful result contains an immutable tuple projection sufficient for exact builder consumption:

- System ID and effective authorization-scope fingerprint;
- common UTC initiation time;
- coordination ID/version/phase;
- whether any prepared artifact currently exists for the coordination/bound Runs;
- canonical Repository IDs;
- both Scan Run IDs and exact Profiled Revisions;
- both deterministic Scan Execution IDs, phase, prompt/schema identities, runtime JSON/hashes and empty legacy Pre-Scan Result marker.

No `DbSet`, `IQueryable`, EF type, connection or transaction escapes Infrastructure.

## Atomic store contract

The Infrastructure implementation performs, in order:

1. begin one short PostgreSQL `ReadCommitted` transaction;
2. execute `SET LOCAL lock_timeout = '5s'` and acquire `pg_advisory_xact_lock` for the versioned System-scoped signed-64-bit key;
3. revalidate the domain System/Repository ownership and canonical pair under the transaction;
4. inspect all existing System coordinations and their two Runs/executions;
5. return exact Replay, or reject conflict/legacy/partial/ambiguous state, without mutation;
6. when absent, construct two `ScanRun.Start` + `BindProfiledRevision`, two `ScanExecution.CreateNewPath`, and one `TwoRepositoryScanCoordination.Create` using one UTC initiation instant;
7. add the five rows, call `SaveChangesAsync` once, commit once;
8. return the materialized immutable tuple projection.

Any exception, cancellation, lock timeout or save failure before commit rolls back, clears tracked additions and exposes no partial tuple. The transaction ends before `PreparedScanCoordinator` is called.

`ScanRun.Start` receives the established 1,800-second compatibility value for the existing durable provider-timeout field. This is not CGC runtime configuration and must not cause `CodeGraphContextProvider`, `ICodeGraphProvider` or the historical profiling coordinator to resolve.

## Canonical lock and replay identity

Repository order is `Guid.CompareTo`, matching `TwoRepositoryScanCoordination.Create`.

The advisory key is the signed big-endian 64-bit prefix of SHA-256 over:

```text
vulgata/story-12.5/prepared-scan-initiation-system-lock/1
{system-id-lowercase-D}
```

Core `PreparedScanInitiationIdentityBuilder` owns framing through `CanonicalEnvelope` domain `vulgata/story-12.5/prepared-scan-initiation-execution/1.0`. Tags 1 through 10 bind, in closed order:

```text
vulgata/story-12.5/prepared-scan-initiation-execution/1
effective authorization user and administrator/owner mode
system ID
canonical Repository ID
safe full Git ref
exact Profiled Revision
current prompt identity
current Document schema identity
canonical analysis-runtime/1 JSON
analysis runtime SHA-256
```

SHA-256 produces a lowercase 64-hex fingerprint. `PreparedScanIdentityBuilders.CreateDeterministicIdentity` receives exact case-sensitive kind literal `PreparedScanInitiationExecution` to derive each execution ID. `TwoRepositoryScanCoordination.Create` derives the coordination ID from System and both execution IDs. An exact request reproduces the identities; any changed pair/ref/revision/runtime/prompt/schema/effective authorization scope derives a different expected identity and conflicts with the System's existing tuple. PostgreSQL lock-timeout `55P03` and caller cancellation roll back and map to `Unavailable`.

Golden Core vectors contain literal expected execution and coordination GUIDs, prove Repository reversal leaves them unchanged, and mutate each bound field independently. Expected constants are not computed by the production builder under test.

## Phase-aware replay

| Exact tuple state | Behavior |
| --- | --- |
| Created + Preparing + no prepared artifact | run external preparation |
| Replay + Preparing + no prepared artifact | retry external preparation using the same tuple after induced failure/crash |
| Replay + Preparing + prepared artifact present | validate/read existing handoff; return 200 Replay if exact, otherwise conflict; no analysis |
| Replay + phase beyond Preparing + complete exact handoff | return/recover 200 Replay; no analysis or new state |
| Replay + phase beyond Preparing + missing/incomplete handoff | conflict as corrupt/partial/incompatible; no rewind or analysis |
| mismatch, legacy, partial tuple, extra coordination or ambiguity | conflict/missing-equivalent with no disclosure or mutation |

The Infrastructure store reports artifact presence only: any Manifest, prepared-v2 Pre-Scan Result, context snapshot, Manifest Entry/audit or File Code Unit bound through the exact coordination/Run/Manifest IDs counts as present. `PreparedScanCoordinator.RecoverPreparedAsync` keeps existing `IPreparedScanHandoffSource` and response mapping as the sole complete-handoff validation path outside the transaction.

## New-path compatibility

- `ScanExecution.PreScanResultId` stays `Guid.Empty`; no legacy Pre-Scan Result is invented or attached.
- non-null prompt, Document schema and runtime identities plus `Prepared` phase identify the new path.
- both Scan Runs and executions use the common initiation instant.
- the pair builder validates and consumes the exact coordination/executions, uses each execution runtime hash in CodeGraph/JDT-LS/Manifest bindings, and uses the initiation instant for the later `ScanContextSnapshot.CapturedAt`.
- the existing Prepared Scan store alone creates current snapshots, prepared Pre-Scan Results, Prepared Scan Manifests and File Code Units.
- old v1 profiling state cannot satisfy replay or prepared handoff.

## Required verification

The public production-composition vertical begins with only an authenticated authorized user, one ordinary System, two ordinary Repositories, exact ref/revision bindings, and ready accepted CodeGraph 1.5/JDT-LS ports. It pre-seeds no Scan Run, Pre-Scan Result, Scan Execution, coordination, Prepared Scan Manifest, File Code Unit or Document.

It proves creation/handoff/publication; pre-publication retry; post-publication and advanced-phase handoff recovery without analysis; partial/corrupt conflict; mismatch conflict; revocation between initial and fresh pre-store checks with zero store calls/writes; tuple-required coordinator/builder composition; default Agents contract-identity and configured runtime resolver plus test-only replacement of those narrow seams; CodeGraph 1.5/no-CGC composition; and zero later-stage work. Identity tests independently hash the v2 embedded prompt, pin prefix/hash/schema, preserve legacy v1 recovery output, and reject null/v1/malformed-hash/wrong-schema values before recheck/store. Disposable PostgreSQL tests independently prove identical and competing concurrency, advisory timeout, rollback and complete-tuple visibility. Golden Core tests pin domain/kind/identity outputs. The focused real CodeGraph CLI 2/2 qualification is supporting tool evidence only.

## Explicit exclusions

No `CodeGraphContextProvider`, historical `RepositoryProfilingCoordinator`, `/scan-runs` repair, general/single/N-Repository lifecycle, EF/model/migration change, Story 12.4 change, accepted Track A/B change, `WorkerAgentDispatchIdentitySource` change, prompt/dispatcher/provider/tool/retry change, Worker/Pass/Chat behavior change, fake Backup B evidence, or retained-database deletion is part of this contract.
