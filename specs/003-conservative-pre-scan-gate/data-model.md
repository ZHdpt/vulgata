# Data Model: Conservative Pre-Scan Gate

## Remediation impact

The July 31 Epic 4 architecture correction changes no domain entity, field,
relationship, enum, EF configuration, table, constraint, index, migration, or
transport projection described below. The existing model remains the accepted
Story 4.3 model. The correction removes service-layer paths that could bypass
it:

- framework detection receives the Profiled Revision-bound
  `ICommittedRepositorySnapshotLease`;
- every profiling coordinator construction requires the snapshot and real
  Pre-Scan detector services; and
- the sole public final persistence input is
  `RepositoryProfileCommitDraft(profile + result)`.

## Closed types

| Type | Values | Rule |
| --- | --- | --- |
| `PreScanApprovalState` | `PreScanApproved` | Story 4.3 has no waiting/manual state. |
| `CommunicationKind` | `Rpc`, `Http` | No arbitrary or deferred family. |
| `ClassificationLevel` | `Confirmed`, `Hint`, `Warning` | A hint/warning is never projected as confirmed. |
| `ConfidenceBand` | `High`, `Conservative`, `None` | Confirmed=High; Hint=Conservative; Warning=None. |
| `EvidenceProvenance` | `StructuralAnnotation`, `FrameworkFact`, `PackageManifest`, `MatchedConfiguration`, `Import`, `Detector` | Closed persistence value. |
| `EvidenceRule` | `RpcDirectAnnotation`, `RpcMatchedConfiguration`, `HttpControllerAnnotation`, `HttpClientAnnotation`, `HttpMatchedConfiguration`, `RpcImportOnly`, `HttpImportOnly`, `MalformedEvidence`, `UnsafePath`, `BoundExceeded`, `ContradictoryEvidence`, `UnsupportedFamily` | Closed detector decision/reason. |

## `PreScanResult` aggregate root

| Field | Type / bound | Invariant |
| --- | --- | --- |
| `Id` | `Guid` | Equals `ScanRunId`; never generated independently. |
| `RepositoryId` | `Guid` | Non-empty; equals owning ScanRun Repository. |
| `ScanRunId` | `Guid` | Non-empty; one result per ScanRun. |
| `ProfiledRevision` | lowercase Git object ID, max 64 | Equals `ScanRun.ProfiledRevision`; immutable commit used for profiling and detection. |
| `State` | `PreScanApprovalState` | Exactly `PreScanApproved`. |
| `SchemaVersion` | string, max 16 | Exactly `1.0` for this contract. |
| `ApprovedAt` | `DateTimeOffset` | Not before ScanRun start; set once. |
| `Fingerprint` | lowercase SHA-256 hex, length 64 | Canonical aggregate content excluding timestamps and database IDs. |
| `ApprovalMessage` | string, max 256 | Exact warning message when warnings exist; bounded Chinese approved message otherwise. |
| `ConfirmedCommunicationKinds` | closed flags | Rpc/Http truth derived before truncation; never reconstructed from samples. |
| `TotalConfirmedEvidenceCount` | int, 0..250,000 | Count before sample truncation. |
| `TotalHintCount` | int, 0..250,000 | Count before sample truncation. |
| `TotalWarningCount` | int, 0..250,000 | Count before sample truncation. |
| `EvidenceTruncated` | bool | True when canonical evidence exceeds the 100-sample persistence cap. |
| `Classifications` | collection, max 100 | Bounded evidence samples; same aggregate identity and canonical order. |

Relationships:

- required one-to-one `ScanRun` → `PreScanResult`;
- required one-to-many `PreScanResult` →
  `CommunicationKindClassification`;
- result identity includes Repository/ScanRun ownership; no cross-Repository
  child is permitted.

## `CommunicationKindClassification` child

| Field | Type / bound | Invariant |
| --- | --- | --- |
| `Id` | `Guid` | Stable only within the committed aggregate. |
| `PreScanResultId` | `Guid` | Equals owning result/ScanRun ID. |
| `Kind` | nullable `CommunicationKind` | Required for Confirmed/Hint; nullable only for a general Warning. |
| `Level` | `ClassificationLevel` | Closed value. |
| `Confidence` | `ConfidenceBand` | Fixed mapping from Level. |
| `Rule` | `EvidenceRule` | Compatible with Kind and Level. |
| `Provenance` | `EvidenceProvenance` | Compatible with Rule. |
| `RelativePath` | string, 1..2,048 | Normalized Repository-relative path or closed `(unknown)` warning value; never rooted/escaping/control-bearing. |
| `Locator` | string, 1..4,096 | Repository-relative `path#selector`; no source content, secret, or host path. |
| `OccurrenceCount` | int, 1..250,000 | Count of exact duplicates collapsed into this row. |
| `Fingerprint` | lowercase SHA-256 hex, length 64 | Hash of closed fields and safe locator; unique per result. |

Compatibility invariants:

- `Confirmed` requires non-null Kind, `High`, direct annotation/client/
  controller/configuration rule, and direct provenance.
- `Hint` requires non-null Kind, `Conservative`, import/package/framework
  evidence, and an import-only rule.
- `Warning` requires `None`; it can name a kind only when the bounded anomaly
  is kind-specific.
- `UnsupportedFamily` can only be Warning and never supplies a confirmed kind.
- Direct evidence does not mutate or remove an independent hint row.
- Confirmed sample rows must agree with `ConfirmedCommunicationKinds`, but the
  flags are the authoritative truth and are never derived from retained rows.

## Aggregate construction

1. Require a completed ScanRun, complete identity-consistent profile, captured
   Profiled Revision, and the same committed-snapshot lease used by framework
   and communication detection; verify the lease identity.
2. Normalize every path and selector before constructing a candidate child.
3. Reject unknown enum values and incompatible level/confidence/rule tuples.
4. Group exact canonical child keys and increment `OccurrenceCount`.
5. Derive closed confirmed-kind flags and total confirmed/hint/warning counts
   from the complete normalized set.
6. Stream the Profiled Revision, flags, counts, and every normalized locator
   record in ordinal order into the aggregate SHA-256 before truncation.
7. Sort by Level (`Confirmed`, `Hint`, `Warning`), Kind, Rule, path, selector.
8. Retain at most 100 samples. Reserve one direct Confirmed sample per
   confirmed kind and one `BoundExceeded` Warning when truncated, then fill
   remaining slots deterministically without duplicates.
9. Compute retained child fingerprints and construct `PreScanApproved`; no
   intermediate durable state exists.

## State transitions

```text
completed profile-only RepositoryProfileDraft + Profiled Revision
  -> deterministic classification
  -> RepositoryProfileCommitDraft(profile + result)
  -> PreScanApproved (in memory)
  -> sole IScanRunRepository final operation
  -> profile + approved result atomically committed
```

There is no `Waiting`, `PendingApproval`, `Expired`, `Rejected`, or manual
approval transition in Story 4.3. A detector-level individual anomaly becomes
a Warning. A detector infrastructure failure or persistence failure publishes
no result. A same-fingerprint retry returns the committed result; a different-
fingerprint retry fails closed. No public service operation may transition a
completed `RepositoryProfileDraft` to durable state without its
`PreScanResultDraft`.

## Existing database constraints and indexes

- `PreScanResults` PK/FK `Id` uses the ScanRun ID; unique
  `(RepositoryId, ScanRunId)`. Domain construction and repository
  materialization verify that Repository identity matches the owning ScanRun.
- check State=`PreScanApproved`, schema version length/value, fingerprint
  length, message length.
- `CommunicationKindClassifications` PK `Id`; FK to result with cascade;
  unique `(PreScanResultId, Fingerprint)`.
- checks for closed persisted strings, level/confidence compatibility,
  Profiled Revision equality, confirmed-kind flags/counts, sample
  path/locator/count/fingerprint bounds, and nullable Kind rules.
- index `(PreScanResultId, Level, Kind)` for deterministic materialization.

The remediation adds no constraint or index. Validation must confirm the
existing model has no pending migration change.

## Transport projection

The DTO is a read-only materialized projection:

- result identity, Repository identity, ScanRun identity, Profiled Revision;
- state, schema version, approved time, approval message;
- distinct ordered confirmed kinds derived from the persisted flags;
- total evidence/hint/warning counts and the evidence-truncated indicator;
- bounded ordered confirmed evidence, hints, and warnings;
- each classification's closed level/confidence/rule/provenance,
  Repository-relative locator, and occurrence count.

Entity fingerprints and internal database IDs are not required by Story 11.2
and are not exposed.
