# Contract: Prepared Scan Initiation

## Status and scope

This is the closed Story 12.5 contract for entering the existing exactly-two-Repository Prepared Scan path from ordinary production records and, only for a complete reviewed-profile identity match, retaining its required sources as mandatory high-value seeds. It adds no endpoint, request field, persistence shape, legacy profiling bridge or general Scan lifecycle.

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

Profile ID/version/fingerprint, Repository alias/stable identity, required path, role, score, semantic symbol and selector line are also never request fields.

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

## Reviewed exact-profile seed authority

`IReviewedExactProfileSeedAuthority` is a Core read-only value port. Default production DI maps exactly one instance to Infrastructure `ReviewedExactProfileSeedAuthority`, which reads the existing validated `ExactResolutionProfileCatalog`. The builder supplies exactly two server-observed bindings from the initiated tuple and loaded Repository records:

```text
RepositoryId
Repository.Name as exact ordinal StableGitIdentity
RequestedRef
ProfiledRevision
```

Only a complete one-to-one match to both profile repositories, expected full refs and expected commits returns `Matched`. A complete nonmatch returns `NonMatching` and an empty source set; it changes no evidence, score, candidate, audit, selection or fingerprint. Catalog integrity failure returns `Unavailable`. No caller, provider, LLM, environment file or external profile may supply or alter the values.

For `Matched`, the adapter returns the canonical union of profile consumer files, provider declaration files, provider implementation Document paths and unsupported-observation owner Document paths, each bound to the matched Repository ID and one or more closed roles. Only an `UnsupportedObservationOwner` source may also carry one internal profile-owned fully qualified Java symbol and one positive one-based selector line. Distinct duplicate owner bindings on one path, invalid qualified-symbol/line state, or a semantic owner on a non-Java path makes the authority `Unavailable`. It returns no public sample value.

The existing safe inventory/split path must then prove:

1. exactly one inventory file for every required Repository/path;
2. eligibility, containment, hash/raw-byte integrity and safe Java/XML processing;
3. at least one exact bounded candidate segment per required path;
4. every segment for that path has `PreparedBusinessEvidence.ReviewedExactResolutionProfile`, direct score 1,100 and existing `DirectBusinessEvidence` reason;
5. no Repository has more than 20 required segments;
6. the existing seed and final selection contain every required segment inside the unchanged 20/150 bounds.

Failure of any matched check returns `prepared-scan-reviewed-profile-seed-incomplete` as a rejected preparation before either Manifest commits. Authority unavailability returns `prepared-scan-reviewed-profile-unavailable`. Existing Prepared Pass 1 policy remains unchanged: direct score `>=400` and top 20 per Manifest are mandatory, so any required-seed job failure reaches `FailedPass1` and exact resolution/Pass 2 cannot start. AD-PSI-7 changes only typed document-symbol projection, File Code Unit identity construction/reconstruction, and server-only propagation/revalidation of that identity through existing Prepared Pass 1 assignment/packet seams.

## Matched semantic-owner binding

The semantic owner is a joint server-owned authority. The embedded profile supplies the fully qualified Java symbol and selector line. The revision-bound JDT-LS document-symbol path supplies the current same-path top-level Type name and exact zero-based, end-exclusive UTF-16 range. Neither alone may bind `CodeUnit.SymbolName`.

`textDocument/documentSymbol` MUST use a dedicated typed protocol projection with explicit Core hierarchy provenance. Only homogeneous array-root `DocumentSymbol[]` may authorize the semantic owner. Every root requires nonempty name, integer Class=5/Enum=10/Interface=11/Struct=23 kind, direct exact range, direct exact selectionRange wholly contained by range, and array-valued children when present; children are not flattened. Valid root non-Type kinds reject as `jdt_document_symbol_kind_unsupported`; missing/non-integer/unknown kind rejects as `jdt_document_symbol_kind_malformed`; mixed/non-array/malformed name/path/range/selectionRange/children or uncontained selectionRange rejects as `jdt_document_symbol_response_malformed`.

Homogeneous `SymbolInformation[]` is protocol-valid flat coordinate evidence only. It is marked `FlatSymbolInformation`; `containerName` MUST NOT be used to infer hierarchy. Its valid exact coordinates may continue supporting unrelated generic splitting, but it never supplies semantic TypeName. A matched owner receiving flat-only or null evidence returns `prepared-scan-reviewed-owner-symbol-unavailable` before Manifest publication. Definition, references and implementation parsing remain unchanged.

For the one matched semantic-owner source:

1. obtain at most one JDT document-symbol result per exact Repository ID/Profiled Revision/path/file-hash build key, even when the file fits whole-file bounds;
2. require exactly one same-path top-level Type whose ordinal name equals the terminal segment of the profile symbol and whose UTF-16 line interval contains `selectorLine - 1`;
3. keep a bounded source as one whole-file `CodeUnitType.File`; for oversized Java, reuse the same JDT result for the existing safe split and require exactly one segment containing the selector line;
4. bind the profile FQN only to that owning File Code Unit's `SymbolName`; every other required segment remains path-valued while retaining score 1,100;
5. complete the binding before CodeGraph expansion, selection, package fingerprints or atomic Manifest publication.

Missing, duplicate, stale, malformed, wrong-name/path/range, unavailable or ambiguous JDT evidence returns `prepared-scan-reviewed-owner-symbol-unavailable`. No unique owning safe range returns `prepared-scan-reviewed-owner-segment-incomplete`. Both publish zero Manifests. Cancellation retains the existing cancellation result.

Path-valued units preserve exact canonical domain `vulgata/story-12.2/file-code-unit-identity/1.0` and tags 1-12. A semantic unit uses `vulgata/story-12.2/file-code-unit-identity/2.0`, preserves tags 1-12 exactly and binds the ordinal FQN as tag 13. The identity fingerprint derives the File Code Unit ID and existing selection/Manifest/package fingerprints. Persistence, handoff and recovery readers reconstruct v1 only when `SymbolName == FilePath`; otherwise they validate/reconstruct v2 and reject any path/symbol/range/ID/fingerprint disagreement.

`PreparedPass1FileCodeUnitAssignment` and `PreparedPass1WorkerPacket` MUST carry required server-only `SymbolName`. `PreparedPass1ClaimedPacketSource` validates v1/v2 before exact source read and after tracking-clear/re-fetch. Web `PreparedPass1JobRunner.CreateDispatchRequest` MUST forward hydrated `packet.SymbolName` unchanged into the assignment; no defaulting or inference is allowed. The dispatcher validates before executor invocation; the executor validates after authorization and before provider resolution. Omission, mutation or mismatch uses existing integrity failure with zero executor/provider continuation. `SymbolName` MUST NOT enter provider JSON, prompts, tools, navigate/submit schemas or results. Path-valued v1 and semantic FQN/v2 both round-trip through packet -> runner -> assignment -> dispatcher packet. Job state, scheduling, retry and failure policy remain unchanged. The Pass 1 commit and Source Anchor retain the same Code Unit/range.

No database transaction spans the profile/JDT observation or any other external analysis. The initiation transaction has already closed, and the existing atomic publication transaction starts only after the complete symbol-bound package exists.

## Required verification

The public production-composition vertical begins with only an authenticated authorized user, one ordinary System, two ordinary Repositories, exact ref/revision bindings, and ready accepted CodeGraph 1.5/JDT-LS ports. It pre-seeds no Scan Run, Pre-Scan Result, Scan Execution, coordination, Prepared Scan Manifest, File Code Unit or Document.

It proves creation/handoff/publication, replay/conflict/authorization/atomicity, CodeGraph 1.5/no-CGC composition and zero later-stage work. The corrected path additionally proves root DocumentSymbol supported Type kinds, mandatory contained selectionRange, flat SymbolInformation non-authority with generic coordinate compatibility, malformed closure and unrelated navigation; whole-file/oversized binding; v1/v2 identity; semantic/path hydrated packet -> job runner -> assignment -> dispatcher packet SymbolName round-trip; omission/mutation rejection before executor/provider; unchanged provider shape; current Source Anchor and unchanged exact Edge/Uncertainty without seeding or repair. Existing identity, PostgreSQL concurrency and CodeGraph CLI evidence remain required.

## Explicit exclusions

No caller/provider symbol input, containerName hierarchy inference, sample literal, bound increase, profile parser/resource change, CGC/legacy path, EF/model/migration, Story 12.4, Track A/B, recovery identity, prompt/provider JSON/tool/job-state/retry/scheduling/result-policy, Pass 1 policy, Pass 2/Chat, fake evidence, run-03 repair or retained-database deletion is allowed. Internal assignment/packet/claimed-source/job-runner bridge/dispatcher/executor may change only to carry/reconstruct SymbolName.
