# Data Model: CodeGraph Code Unit Extraction

All persistent identifiers are UUIDs. PostgreSQL schema `vulgata` is
authoritative; the private CodeGraphContext Kuzu graph is transient provider
state.

## Transient provider projection model

These records are owned parser models, not EF entities and not direct provider
DTOs.

Until the mandatory final-image real-provider contract gate passes,
`ProviderProjectionEnvelope` and the coordinate source-property bindings are
candidate grammar. Fake/controlled projection records cannot transition them
to accepted provider grammar.

### `ProviderProjectionEnvelope`

| Field | Rule |
| --- | --- |
| `Success` | Required `true`. |
| `Error` | Required `null` on success. |
| `Data.Success` | Required `true`. |
| `Data.Query` | Exact echo of the request query. |
| `Data.RecordCount` | Non-negative and exactly equals parsed result rows. |
| `Data.Results` | Array under the shared body/row bounds. |

An invalid envelope rejects the whole provider result.

### `ProviderContractExecutionClaimV1`

Immutable
`TestResults/codegraph-provider-contract/<AcceptedCorrectionSha256>/claim.json`
created before image/provider reachability:

| Field | Rule |
| --- | --- |
| `SchemaVersion` | Exactly `cgc-provider-contract-claim-v1`. |
| `AcceptedCorrectionSha256` | Exactly the 64-lowercase-hex directory name. |
| `ExecutionOrdinal` | Exactly `1`. |
| `ProjectionARequestSha256`, `ProjectionBRequestSha256` | Exact unchanged production hashes. |
| `ExpectedFinalImageSha256` | Pre-supplied final image identity. |
| `ProviderVersion`, `ProviderLockSha256`, `ProviderDistributionSha256` | Exact current pins. |
| `EvidenceSchemaVersion` | Exactly `cgc-provider-contract-evidence-v2`. |

Atomic no-replace creation of the correction directory is the durable claim.
The parent directory is synced before claim-file publication. `claim.json`
uses temporary write, file flush, no-replace rename, directory sync, reread,
and closed-schema validation. Any existing/partial/corrupt/raced directory is
already consumed and is never repaired or reused.

### `ProviderContractEvidenceV2` (ephemeral test artifact)

Closed schema `cgc-provider-contract-evidence-v2`, maximum 16 KiB, published
for both pass and fail:

| Field | Rule |
| --- | --- |
| `SchemaVersion`, `Verdict` | Exact schema ID; verdict is `pass|fail`. Missing evidence and skip are failures. |
| `PrimaryFailureCategory` | `none|preflight|provider-lifecycle|http-status|invalid-json|envelope-mismatch|query-rejected|projection-mismatch|observation-bound-exceeded|cleanup-failure|publication-failure`; `none` only on pass. |
| `AcceptedCorrectionSha256` | Hash of the independently accepted one-execution correction boundary. |
| `ClaimSha256` | Hash of the exact immutable `claim.json` bytes. |
| `FinalImageSha256`, `VerifierBaseImageSha256` | Required equal image identities. |
| `ProviderVersion`, `ProviderLockSha256`, `ProviderDistributionSha256` | Exactly `0.5.1`; lock `10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95`; distribution `3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658`. |
| `FixtureTreeSha256` | Hash only; no fixture path, filename, symbol, or source. |
| `ProjectionARequestSha256`, `ProjectionBRequestSha256` | Equal the canonical production query hashes. |
| `ProjectionObservations` | Exactly A/B: attempted, nullable HTTP status, byte count/hash, request hash, optional echo hash, root JSON kind, and `ShapeComplete`. |
| `JsonShape` | Maximum depth 4, 32 containers, 128 property observations, and 32 array summaries. A safe name may be raw; an unknown name is only `{HmacSha256, Utf8Length, ValueKind, ContainerOrdinal, FirstSeenOrdinal}`. |
| `AliasNames`, `CoordinateBindings` | Raw only when exactly in the fixed safe envelope/production-alias allowlist; otherwise use the unknown-name representation and fail. |
| `ProjectionBQueryEchoSha256` | Required equal to `ProjectionBRequestSha256` when the observed envelope contains the query echo. |
| `EnvelopeGrammarId` | Bounded safe enum, no provider value. |
| `Coverage` | Counts/booleans for isolated node kinds, four relationship kinds, filtered-target unresolved-only behavior, and Projection A/B `cgc-v1` equality. |
| `ImmutableSnapshot` | Booleans/hashes proving no index/watcher/mutation between projections and immutable Projection B text. |
| `Cleanup` | Gateway/Context/home/container/tag booleans plus `ProviderStateKind=absent|regular-file|directory|link-or-reparse|special|unknown`, removal boolean, and failure count. |
| `ForbiddenRetention` | All false for scalar values, source, rows, raw JSON/query, paths, symbols, credentials, tokens, endpoint values, and diagnostics. |

The raw-name allowlist is exactly:
`status,message,error,data,success,query,record_count,results,record_kind,provider_kind,path,name,start_line,end_line,relationship_kind,source_provider_kind,source_path,source_name,source_start_line,source_end_line,target_provider_kind,target_path,target_name,target_start_line,target_end_line`.

The exact 0.5.1 correction uses one heterogeneous node/relationship match per
projection with closed label allowlists. Missing coordinates remain JSON null
without requiring Kuzu UNION column coercion.

Bounded evidence observes one representative row-object shape per array while
the closed parser validates every row; repeated schema copies are not retained.

Unknown names use HMAC-SHA256 with a fresh memory-only per-execution 256-bit
CSPRNG key and domain-separated input
`"vulgata-cgc-unknown-property-v1" || NUL || uint64be(length) || NUL || name`.
Unkeyed hashes, raw unknown names, and key persistence/logging are forbidden.
No JSON scalar value is retained. Any unknown or oversize name fails. On
observation overflow, stop collecting names, retain counts/hashes, set
`ShapeComplete=false`, publish `observation-bound-exceeded`, and remain blocked.

This artifact is not domain or qualification state. After cleanup, exactly one
record is closed-schema validated and create-new published as
`<AcceptedCorrectionSha256>/evidence.json` with file flush, atomic no-replace
rename, and directory sync. The correction directory/claim/evidence/temp files
are immutable history. A crash after directory creation consumes the
correction; missing evidence is failure, never resume authority.

### `ProviderCodeUnitProjection`

| Field | Rule |
| --- | --- |
| `RecordKind` | Exactly `code_unit`. |
| `ProviderKind` | `File`, `Module`, `Class`, or `Function`. |
| `Path` | Nullable in transport; normalized repository-relative path when present. |
| `Name` | Nullable in transport; required for Module/Class/Function. |
| `StartLine`, `EndLine` | Nullable in transport; positive/inclusive/ordered when present. |

Mapping:

- File: path required; name may be derived from path; range is exact
  `1..local-file-line-count`, with `RangeConfidence.Exact`.
- Module: name required; persisted only with eligible path and valid range;
  a provider single-line range maps to `RangeConfidence.Approximate`;
  otherwise it is retained only as an endpoint tuple.
- Class/Function: eligible path, name, and valid range required; directly
  observed ranges map to `RangeConfidence.Exact`.
- filtered paths never produce persistent Code Units.

### `ProviderRelationshipProjection`

| Field | Rule |
| --- | --- |
| `RecordKind` | Exactly `relationship`. |
| `RelationshipKind` | `CONTAINS`, `IMPORTS`, `CALLS`, or `INHERITS`. |
| `SourceKind/Path/Name/StartLine/EndLine` | Source endpoint tuple. |
| `TargetKind/Path/Name/StartLine/EndLine` | Target endpoint tuple. |

Relationship rows never create Code Units. They map only after all valid Code
Unit rows are known.

### `ProviderEndpointIdentity`

Normalize `(kind, path, name, start_line, end_line)`, serialize it as
NUL-delimited UTF-8, hash with SHA-256, and prefix lowercase hex with
`cgc-v1:`.

- exact duplicate tuple/identity pairs deduplicate;
- the same identity with different normalized fields rejects the provider
  result atomically;
- a pathless Module can have a valid identity and serve as an unresolved
  relationship target without becoming a Code Unit;
- identities contain no source, credential, token, absolute checkout path, or
  Kuzu internal ID.

### `CodeGraphProviderResult`

| Field | Rule |
| --- | --- |
| `Succeeded` | True only after both projections and provider cleanup succeed. |
| `CodeUnits` | Valid independently enumerated provider Code Units. |
| `Relationships` | Valid endpoint-key relationships. |
| `CandidateOutcomes` | Bounded malformed/skipped/errored provider outcomes. |
| `ParsedFileCount` | Distinct eligible paths with at least one valid persistent Code Unit. |
| `Duration` | Whole provider operation duration. |
| `Error` | Bounded safe terminal error on failure. |

Failure returns no partial provider Code Units, relationships, or provider
outcomes. Pre-provider filtered audit rows are outside this transient result and
remain durable.

## `ScanRun` aggregate (`ScanRuns`)

| Field | Rule |
| --- | --- |
| `Id` | UUID generated in Core. |
| `RepositoryId` | Required FK to exactly one Repository. |
| `Status` | `Profiling`, `Completed`, `Failed`, or `Aborted`; no queue/dispatch state. |
| `TotalCandidateFileCount` | Non-negative candidate count after local filtering classification. |
| `ParsedFileCount` | Non-negative distinct parsed-file count, never above total. |
| `ProviderProcessTimeoutSeconds` | Positive deployment value; default 1,800; never per-file. |
| `StartedAt`, `CompletedAt`, `Duration` | UTC timing; terminal runs have completion/duration. |
| `NeedsHumanReview` | True when at least 500 candidates yield zero Code Units after successful projections. |
| `TerminalMessage` | Bounded/sanitized; no source, secret, raw JSON, or private path. |

`ScanRun` owns candidate outcomes and its provider-derived structural records.

## `ScanCandidateOutcome`

One auditable result per repository-relative candidate/reason occurrence:

```text
Id, ScanRunId, FilePath, Outcome, ReasonOrCategory,
AuditGroupOrdinal, CreatedAt
```

- `AuditGroupOrdinal` is deterministic metadata for groups of at most
  approximately 1,000 paths. It never partitions provider input.
- filtered outcomes are persisted before provider execution and survive
  provider failure;
- malformed-row details are sampled to at most 100 safe warnings while
  aggregate rejection counts remain accurate;
- provider process/top-level failure is represented on `ScanRun`, not
  fabricated as one error per file.

## `CodeUnit`

| Field | Rule |
| --- | --- |
| `Id` | UUID. |
| `ScanRunId`, `RepositoryId` | Required and mutually consistent. |
| `FilePath` | Eligible normalized repository-relative path. |
| `FileIdentityHash` | SHA-256 of first `min(4096, file length)` source bytes. |
| `SourceHash` | SHA-256 of complete local file contents. |
| `LineStart`, `LineEnd` | Positive inclusive ordered source range. |
| `SymbolName`, `UnitType` | Required mapped structural identity. |
| `ProviderEndpointIdentity` | Required `cgc-v1:` key, unique per ScanRun. |
| `RangeConfidence` | Exactly `Exact`, `Approximate`, or `Heuristic`. |
| `Language` | Nullable in Story 4.1. |
| `LifecycleStatus` | Extracted then Pending; later vocabulary includes Dispatched, Completed, Failed, Skipped. |

Uniqueness:

- `(ScanRunId, ProviderEndpointIdentity)`;
- structural duplicate protection over
  `(ScanRunId, FilePath, LineStart, LineEnd, SymbolName, UnitType)`.

An isolated Code Unit is valid and persists with no Code Unit Edge.

## `CodeUnitEdge`

| Field | Rule |
| --- | --- |
| `Id` | UUID. |
| `ScanRunId`, `RepositoryId` | Same as both endpoints. |
| `SourceCodeUnitId`, `TargetCodeUnitId` | Required persisted Code Units in same ScanRun/Repository. |
| `RelationshipKind` | `Calls`, `Imports`, `Inherits`, or `Contains`. |
| `IsResolved` | Always true. |

Unique on `(ScanRunId, SourceCodeUnitId, TargetCodeUnitId,
RelationshipKind)`. There is no target-null Edge.

## `UnresolvedStructuralRelationshipCandidate`

| Field | Rule |
| --- | --- |
| `Id` | UUID. |
| `ScanRunId`, `RepositoryId` | Required. |
| `SourceCodeUnitId` | Required persisted source in same ScanRun/Repository. |
| `RelationshipKind` | Mapped structural kind. |
| `ProviderTargetEndpointIdentity` | Required bounded `cgc-v1:` identity. |

Unique on `(ScanRunId, SourceCodeUnitId, RelationshipKind,
ProviderTargetEndpointIdentity)`.

It has no target Code Unit, target Repository, Document, Edge, or Uncertainty
identity. It is a later Cross-Repository Resolution candidate only; Story 4.1
does not run resolution.

## Validation and persistence transitions

```text
Created ScanRun
  -> local candidate/filter audit persisted
  -> one whole-Repository index
  -> Code Unit projection validated in memory
  -> relationship projection validated in memory
  -> endpoint identities and mapping validated
  -> provider cleanup succeeds
  -> one transaction persists provider structure + terminal ScanRun
```

If index, gateway, either envelope, a bound, endpoint collision, timeout,
process, or cleanup fails, the transition skips provider persistence and writes
terminal failed/aborted `ScanRun` state only. Existing filtered audit rows
remain.

Story lifecycle may transition to complete only after a separate transient
`ProviderContractEvidenceV2` validates as `pass` for the just-built final image
and both exact production projection hashes. The evidence test does not mutate
`ScanRun` data or any qualification record.

Cleanup transitions after Context deletion:

```text
absent -> success
regular file (including graph.kuzu) -> unlink
directory -> bounded recursive delete without following links
link/reparse/special -> do not follow; cleanup failure
then publish pass/fail evidence with separate primary and cleanup facts
```

Malformed independent rows after valid envelopes are excluded with bounded
outcomes; other valid rows continue. Relationships with absent source are
rejected. Relationships with persisted source and absent but valid target
identity create unresolved candidates.

## Historical harness model

The append-only qualification attempt ledger is workflow evidence, not domain
state. `attempt-0001`, `attempt-0002`, and `attempt-0003` remain immutable.
`attempt-0003` is permanently `failed / provider-contract-failure` under
boundary SHA-256
`b9b5487b844d1c4ce89e2f03636e8b0c64f99f4dcbf6723a4f1e152ef138853b`.
No domain mapping, migration, or production state depends on changing or
retrying that record.
