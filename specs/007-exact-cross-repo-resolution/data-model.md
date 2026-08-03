# Data Model: Exact Cross-Repository Resolution

## Overview

The feature adds two durable entities and several transient domain records. Existing Logical Document, Document Version, Source Anchor, DocumentEdge, ScanRun, and ScanExecution identities remain authoritative. Their Guid PK/FK values are internal relational identities: they are required for ownership, referential integrity, deterministic idempotency, and authorized joins, but are not textual evidence and are never copied into profile bytes, evidence/diagnostic/log/exception strings, or unauthorized output. Cross-Repository is derived from differing Edge endpoint Repository IDs; it is not a new Edge type.

## Existing entities used unchanged

### Repository / ScanRun / ScanExecution

- Runtime alias bindings point to managed `Repository.Id`, `ScanRun.Id`, and `ScanExecution.Id` only for the life of a request.
- Both Repositories must belong to the same authorized System selected by the request.
- `ScanRun.ProfiledRevision` and `ScanExecution.ProfiledRevision` must equal the profile expected full commit.
- ScanExecution must be `Pass1Completed`, or `Completed` with unchanged current Pass 1 artifacts.

### LogicalDocument / DocumentVersion

- Exact matches connect two current Code Logic Document Versions.
- Consumer version is Edge source; provider version is Edge target.
- Both owner Logical Documents are locked and current pointers revalidated before commit.
- No Document body/version/projection is mutated.

### SourceAnchor

Existing fields and required CodeUnit ownership stay unchanged. Add an alternate key/index:

```text
(Id, DocumentVersionId, RepositoryId, ScanRunId)
```

It supports owner-safe composite foreign keys from exact Edge evidence. Existing rows already satisfy it because `Id` is the primary key.

### DocumentEdge

Existing shape and unique constraint stay unchanged. Exact resolution creates:

```text
EdgeType = References
SourceRepositoryId != TargetRepositoryId
```

Every exact-resolution Edge must have exactly one evidence row; other REFERENCES and GENERATED_FROM Edges have none.

## Durable new entity: ExactCrossRepositoryEdgeEvidence

One-to-one typed provenance for an exact configured cross-Repository Edge.

Guid fields below are internal relational PK/FK components. They persist in typed columns and participate in constraints/replay; no Guid is interpolated into any textual evidence field. Authorized serialization exposes only the Edge identity as opaque `relationshipIdentity`, not the owner Repository/ScanRun/Document Version/Source Anchor keys.

| Field | Type / bound | Rule |
| --- | --- | --- |
| EdgeId | Guid, PK/FK | Existing `DocumentEdge.Id`; Edge must be `References` and cross-Repository (validated in store) |
| ConsumerSourceAnchorId | Guid | Composite FK proves membership in Edge source Document Version/Repository/ScanRun |
| ProviderSourceAnchorId | Guid | Composite FK proves membership in Edge target Document Version/Repository/ScanRun |
| ConsumerDocumentVersionId | Guid | Duplicated ownership component for composite FK; equals Edge source |
| ConsumerRepositoryId | Guid | Equals Edge source Repository |
| ConsumerScanRunId | Guid | Equals Edge source ScanRun |
| ProviderDocumentVersionId | Guid | Equals Edge target |
| ProviderRepositoryId | Guid | Equals Edge target Repository |
| ProviderScanRunId | Guid | Equals Edge target ScanRun |
| ConsumerRawIdentifier | string, 1-512 UTF-8 | Decoded literal, Form C; safe endpoint metadata, not source text |
| ProviderRawIdentifier | string, 1-512 UTF-8 | Decoded XML attribute, Form C |
| NormalizedEndpointIdentifier | string, 1-512 UTF-8 | Result of the configured normalizer; ordinal-equal for both candidates |
| Protocol | closed string, max 32 | `CustomRpc` for this slice |
| ResolutionKind | closed string, max 32 | `ExactConfigured` only |
| Confidence | decimal(5,4) | Exactly `1.0000`; not LLM-derived |
| ProfileSchemaVersion | closed string, max 16 | `1.0` |
| ProfileId | closed string, 1-128 UTF-8 | Stable profile identity |
| ProfileVersion | closed string, 1-32 UTF-8 | Version provenance |
| ProfileFingerprint | char(64) | Lowercase SHA-256 of canonical profile bytes |
| OccurrenceId | closed string, 1-128 UTF-8 | Direction-specific occurrence identity |
| ConsumerEvidenceLocator | string, 1-4096 UTF-8 | Repository-relative file#line:column plus rule ID; no source |
| ProviderEvidenceLocator | string, 1-4096 UTF-8 | Repository-relative XML#line:column plus rule ID; no source |
| EvidenceFingerprint | char(64), unique | Canonical exact replay fingerprint |
| CreatedAt | DateTimeOffset | First committed timestamp; unchanged on replay |

### Relationships and constraints

- PK/FK `EdgeId -> DocumentEdges.Id`, delete restricted/no action.
- Consumer composite FK to `SourceAnchors(Id, DocumentVersionId, RepositoryId, ScanRunId)`.
- Provider composite FK to the same key.
- Unique `EvidenceFingerprint`.
- Checks close protocol/resolution/confidence/schema/hash lengths and require consumer/provider Repository IDs differ.
- Repository/store validation proves evidence endpoint tuples equal the referenced DocumentEdge endpoint tuples in the same locked transaction; a database trigger is not introduced.

## Durable new entity: Uncertainty

First-class bounded known-unknown owned by a consumer Document Version.

Its Guid owner/target/anchor fields are likewise internal relational columns, not evidence payload. Unauthorized retrieval returns no Uncertainty identity or metadata.

| Field | Type / bound | Rule |
| --- | --- | --- |
| Id | Guid, PK | Stable deterministic ID |
| DocumentVersionId | Guid | Consumer/source Document Version |
| RepositoryId | Guid | Consumer Repository |
| ScanRunId | Guid | Consumer ScanRun |
| SourceAnchorId | Guid? | Required when a safe exact consumer location exists; composite owner FK |
| Type | closed lowercase string, max 32 | `cross_repository` or `unsupported_communication` |
| Status | canonical lowercase string, max 16 | `unresolved` for every automatic result in this story; `resolved` reserved for later resolution and `wontfix` reserved for an explicit human-accepted Dangling Link |
| SubStatus | canonical lowercase string, max 32 | exactly `cross_repo`, `unknown_target`, or `ambiguous` |
| ReasonCode | closed lowercase string, max 64 | `variable_target`, `disallowed_invocation`, `malformed_provider`, `provider_out_of_root`, `unmatched`, `multiple_providers`, `normalization_mismatch`, or `unsupported_kind` |
| ObservationKind | closed lowercase string? max 32 | `file_storage` for the reviewed unsupported observation; null for exact/non-kind outcomes |
| EndpointIdentifier | string? <=512 UTF-8 | Safe raw consumer identifier only when exact and authorized |
| EndpointIdentifierNormalized | string? <=512 UTF-8 | Safe normalized value when available |
| TargetRepositoryId | Guid? | Known, authorized configured target only |
| ProfileSchemaVersion | string max 16 | Profile provenance |
| ProfileId | string max 128 | Profile provenance |
| ProfileVersion | string max 32 | Profile provenance |
| ProfileFingerprint | char(64) | Profile provenance |
| OccurrenceId | string max 128 | Exact occurrence identity or unsupported observation identity |
| EvidenceLocator | string, 1-4096 UTF-8 | Repository-relative locator/rule, never snippet |
| EvidenceFingerprint | char(64), unique | Idempotency key/fingerprint |
| CreatedAt | DateTimeOffset | Immutable creation time |
| ResolvedAt | DateTimeOffset? | Reserved for later general resolution; null in this story |
| ResolvedByDocumentVersionId | Guid? | Reserved; null in this story |

### Relationships and constraints

- Composite owner FK `(DocumentVersionId, RepositoryId, ScanRunId)` to DocumentVersion.
- Optional composite Source Anchor FK proves anchor belongs to owner Document Version.
- Target Repository FK is restricted; value is written only when caller is authorized and target is configured/known.
- Unique `EvidenceFingerprint` and index `(RepositoryId, Status, SubStatus)`.
- Closed checks for type/status/canonical sub-status/reason/observation kind; this story's automatic write validator requires `Status=unresolved`; endpoint raw/normalized nullability must agree with reason. No `sub_status` outside `cross_repo`, `unknown_target`, or `ambiguous` is representable.
- No description/context snippet/source content column is added.

## Transient profile and runtime records

### ExactResolutionProfile

```text
schemaVersion
profileId
profileVersion
profileFingerprint
repositories[2]
occurrences[1..16]
unsupportedObservations[1..16]
```

### ProfileRepository

```text
alias
stableGitIdentity
expectedRef
expectedCommit
```

No RepositoryId, Git URL, host/mount path, or credentials.

### ResolutionOccurrence

```text
occurrenceId
protocol = CustomRpc
consumerAlias
providerAlias
consumerSelector
providerSelector
providerDocumentSelector
normalizerId = custom-rpc-in-prefix-v1
comparison = Ordinal
```

The reviewed entry is `account-to-channel-cust-update-session-v1`: account `UpdateChannelSessionOp.java:45`, simple member invocation `netTools.execute`, endpoint argument index 1; channel `cust/updateSession.xml:2`, `trans@name`; implementation Document `UpdateSessionOp.java` with exact type symbol; normalizer `custom-rpc-in-prefix-v1`. Consumer raw `cust/updateSession` and provider raw `in/cust/updateSession` normalize to the same observed value.

### UnsupportedObservation

```text
observationId = account-file-storage-oss-upload-v1
kind = file-storage
ownerAlias = mbs_account
ownerDocument = exact Repository-relative path + exact type symbol
selector = simple-member-invocation + receiver/member/line/ordinal
evidenceLocator = bounded Repository-relative locator + rule
```

The runtime-mapped record adds authorized owner Repository/ScanRun/Profiled Revision/Document Version/Source Anchor identities. It is valid only when line 66 of the reviewed `UploadOcrToOssOp` selector falls inside exactly one current owner Source Anchor.

Roles are occurrence-local; the Repository aggregate is unchanged.

### ExactResolutionRuntimeRequest

```text
systemId
profileId/profileVersion/profileFingerprint
bindings[2]: alias, stableGitIdentity, repositoryId, scanRunId, scanExecutionId
```

Runtime IDs are never copied into profile resources or textual evidence/diagnostic/log/exception fields. The validated decision batch carries them into the store, which persists the required typed relational PK/FK columns and uses them for authorized joins and deterministic replay.

### ProviderEndpointCandidate

Transient canonical provider-side `communication_endpoint` concept:

```text
occurrenceId
repositoryId/scanRunId/profiledRevision
rawIdentifier
normalizedIdentifier
declarationEvidenceLocator
providerDocumentVersionId
providerSourceAnchorId
```

### ConsumerDispatchCandidate

Transient canonical consumer-side `dispatch_site` concept:

```text
occurrenceId
repositoryId/scanRunId/profiledRevision
callForm/receiverSymbol/memberName/argumentIndex/line/ordinal
rawIdentifier
normalizedIdentifier
invocationEvidenceLocator
consumerDocumentVersionId
consumerSourceAnchorId
```

These records are not EF entities in this slice.

### ExactResolutionDecisionBatch

```text
profileId/profileVersion/profileFingerprint
decisions[2..32] in canonical profile identity order
  exact occurrence decision[occurrences.Count]
  unsupported observation decision[unsupportedObservations.Count]
```

All decisions carry complete authorized immutable owner identities and safe bounded values before the store is called. Count/order drift, a duplicate/missing profile identity, or any invalid member rejects the batch before attachment. Exact Edge/evidence and limitation Uncertainty persist in one transaction.

### ExactResolutionOutcome

Closed outcomes:

```text
WaitingForPass1
Applied
AlreadyApplied
UncertaintyRecorded
AlreadyRecorded
Refused
Conflict
Cancelled
Failed
```

`Refused` carries only a bounded closed code, never alias existence, endpoint metadata, path, Git diagnostic, or provider details.

### ExactResolutionRelationshipReadModel

Authorized only after both endpoint Repositories are rechecked:

```text
relationshipIdentity  # opaque stable serialization of the Edge identity
edgeType
exactEvidence:
  raw/normalized identifiers, protocol, profile/occurrence provenance
  exact confidence, consumer/provider safe Evidence Locators
```

It does not serialize Repository, ScanRun, Document Version, or Source Anchor PK/FK values; Source Anchor provenance is projected through the two safe Evidence Locators. When either endpoint is unauthorized, the only result is the existing identity-free, metadata-free `RestrictedRelationship` marker. Uncertainty retrieval returns null/empty indistinguishably when its owner or known target is outside scope.

## State transitions

### Resolution operation

```text
runtime request
  -> validate profile + complete binding + authorization
  -> verify two durable Pass 1 boundaries
  -> acquire/verify isolated exact commits and snapshots
  -> extract/map exact candidate and unsupported observation
  -> normalize + direction-qualified cardinality
  -> lock/revalidate current Document Versions
  -> per-item Applied/UncertaintyRecorded/Already* | aggregate Conflict/Refused
```

Any failure before candidate inspection writes nothing. Cancellation never fabricates a result.

### Uncertainty

This story automatically creates only canonical nonterminal combinations:

```text
unresolved/{cross_repo|unknown_target|ambiguous}
```

Detailed distinctions never become sub-statuses. They are closed `ReasonCode` values. The reviewed unsupported observation creates `unresolved/unknown_target` with `reason_code=unsupported_kind` and `observation_kind=file_storage`, or writes nothing when owner authorization/currentness/Source Anchor mapping fails. It remains eligible for future full-V1 file/storage resolution or reclassification; automatic Story 11.4 processing cannot transition it to human-terminal `wontfix`.

General later resolution to `resolved`, reclassification to a more precise unresolved state, and explicit human transition to `wontfix` are reserved and not implemented.

## Idempotency identity

The canonical fingerprint material includes:

1. profile schema/id/version/fingerprint;
2. occurrence ID and consumer/provider aliases;
3. both exact-match endpoint identities, or the unsupported observation's authorized owner Repository, ScanRun, Document Version, Source Anchor, and Profiled Revision identities;
4. raw and normalized identifiers when safe/available;
5. protocol/normalizer/comparison/direction for an exact occurrence, or closed observation kind/selector for an unsupported observation;
6. both evidence locators; and
7. exact outcome/reason code.

Collections are sorted ordinally/UUID-wise before hashing. Edge/Uncertainty UUIDs are derived from the first 16 bytes of the SHA-256 with deterministic UUID variant/version bits. Durable replay must match every stored value, not merely the ID.

These trusted canonical inputs and derived UUIDs remain internal relational/idempotency material. They are not rendered into Evidence Locators, diagnostics, logs, exceptions, or unauthorized output.

## Migration lifecycle

- New migration follows `20260801033655_AddSequentialScanRunner`.
- Apply adds two tables plus SourceAnchor alternate key/index.
- Rollback removes only Story 11.4 tables/key and preserves all prior rows/schema.
- Reapply recreates identical constraints/indexes.
- `dotnet ef migrations has-pending-model-changes` must return exit 0.
- Integration owner coordinates migration, designer, model snapshot, DbContext, and Program composition.
