# Data Model: Retained Source Access

## SourceSnapshot aggregate

One immutable source-owned aggregate records that exact bytes for one Repository and Profiled Revision crossed the publication boundary.

| Field | Type / bound | Rule |
| --- | --- | --- |
| `Id` | `Guid` | Internal identifier from the domain builder; not a public source version. |
| `RepositoryId` | `Guid` | Required Repository reference with delete restriction. |
| `ProfiledRevision` | lower-case hex, 40 or 64 chars | Full commit resolved once. Unique with Repository. |
| `GitObjectFormat` | `sha1` or `sha256` | Must agree with revision length and captured repository. |
| `ManifestSchemaIdentity` | `retained-source-manifest/1.0` | Fixed for this story. |
| `ManifestSha256` | 64 lower-case hex | Digest of exact canonical manifest bytes. |
| `EntryCount` | non-negative `long` | Exact committed leaf count represented. |
| `TotalBlobBytes` | non-negative `long` | Sum of retained blob lengths; gitlinks add zero. |
| `InternalStorageKey` | bounded relative opaque string | Infrastructure-only, contained under configured source root, unique, never logged/exposed. |
| `CompletedAt` | UTC `DateTimeOffset` | Successful row-commit time. |

Database invariants:

- unique `(RepositoryId, ProfiledRevision)` and unique `InternalStorageKey`;
- fixed manifest schema/SHA-256 shape and object-format/revision-length agreement;
- non-negative counts;
- immutable after insertion; deletion is absent from the story repository contract;
- Repository relationship uses `Restrict` so deleting a Repository cannot implicitly remove retained source. Existing management routes map this specific conflict to sanitized Chinese `409`.

No ScanRunId, pair, PairExecutionId, SystemId, CodeGraph, Code Unit, Document, Edge, flow, provider, or tool field belongs here. System membership is reloaded only for authorization.

## RetainedSourceManifest

Canonical JSON conforms to `contracts/retained-source-manifest-v1.schema.json`. Serialization is UTF-8 without BOM with deterministic property order, lower-case hex, base64 raw paths, invariant numbers, and entries sorted by raw path bytes. PostgreSQL stores SHA-256 over these exact bytes.

| Field | Rule |
| --- | --- |
| `schemaIdentity` | `retained-source-manifest/1.0` |
| `repositoryId` / `profiledRevision` | Equal the aggregate identity. |
| `gitObjectFormat` | `sha1` or `sha256`. |
| `entryCount` | Equals entry-array length. |
| `totalBlobBytes` | Sum of retained blob lengths. |
| `entries` | Exact leaf inventory in raw-path order. |

### RetainedSourceManifestEntry

| Field | Rule |
| --- | --- |
| `ordinal` | Zero-based manifest order; stable only within this manifest. |
| `pathBytesBase64` | Exact Git path bytes; internal integrity field. |
| `relativePath` | Exact strict-UTF-8 `/` path only when safe for ordinary lookup; otherwise null. |
| `entryKind` | `regularFile`, `executableFile`, `symbolicLink`, or `gitlink`. |
| `gitMode` / `gitObjectId` | Exact supported Git tree mode and full object ID. |
| `byteLength` / `contentSha256` / `storageBlobKey` | Blob integrity/location; null for gitlink. |
| `capabilities` | Explicit subset of `metadata`, `byteRange`, `strictUtf8Text`. |
| `limitationCode` | Required when ordinary path/content/text is unavailable. |

Directories are derived from safe paths, not persisted. Unsafe/non-UTF-8 paths remain counted and integrity-bound. Authorized root listing yields bounded limitation summaries by ordinal without unsafe path interpretation.

## SourceReadContext

Ephemeral server capability; never persisted.

| Field | Rule |
| --- | --- |
| `ContextId` | Opaque process-local correlation identity; not accepted from caller. |
| `AuthorizationSubject` / `Purpose` | Server-derived subject and fixed source purpose. |
| Repository/revision | Exact public source binding. |
| Snapshot/manifest IDs | Internal binding loaded from completed row. |
| UTC issue/expiry | Diagnostic only. |
| Monotonic issued/deadline timestamps | Enforcement through `TimeProvider`. |
| `Budget` | Shared atomic counters and per-operation caps. |
| lifetime cancellation | Server-owned and linked to every call. |

Disposal clears only context-owned transient state; it never changes canonical source.

## SourceAccessBudget

| Counter | Reservation |
| --- | --- |
| operations | One before each metadata/list/read attempt, including failed/denied attempts. |
| metadata entries | Requested page limit before list I/O; no refund. |
| returned bytes | Requested maximum before byte/text I/O; no refund. |

Requests above a per-operation cap fail before cumulative reservation. Reservation of all applicable counters is atomic across concurrent calls. Failure performs no storage I/O and no method refreshes the budget.

## Ordinary projections and outcomes

`SourceEntry` contains only exact safe Repository-relative path, logical kind, byte length/digest where applicable, capabilities, and stable limitation. It excludes raw unsafe path bytes, origin/credentials, object/storage keys, and physical paths.

`SourceReadOutcome` is discriminated:

| Status | Disclosure |
| --- | --- |
| `Succeeded` | Scoped payload plus Repository/revision and manifest digest receipt. |
| `Denied` | No source existence, count, hash, or entry metadata. |
| `NotFoundOrUnavailable` | Authorized logical identity only. |
| `Unsupported` | Safe capability/limitation only. |
| `BudgetExceeded` | Public exhausted dimension; no partial payload. |
| `DeadlineExceeded` / `Cancelled` | No payload. |
| `IntegrityFailure` | Authorized logical identity and correlation ID only. |
| `AcquisitionFailure` / `StorageFailure` | Sanitized classification and authorized logical request context. |

## Capture transitions and cross-process coordination

```text
Authorized
  -> RefResolved
  -> ObjectsCaptured
  -> VerifiedStaging
  -> [acquire Repository/revision identity lock]
  -> SealedObject
  -> CompletedRecord
```

- Staging owns an exclusive operation lease and is removed on controlled cancellation/failure.
- Seal/adopt/cleanup all use the same identity lock under `.locks`.
- Under the lock, an actor rechecks the completion row and object integrity.
- `CompletedRecord` is the visibility point and has no mutation/delete transition here.
- A crash before row commit may leave a sealed unreferenced object. Fresh retry adopts it under the lock after verification.
- Cleanup may remove that object only under the lock, after row recheck and configured stale grace. It cannot race capture/adoption.
- Same identity/digest yields replay; same Repository/revision with different digest yields integrity failure.

## Compatibility and migration

The migration adds only `SourceSnapshots`; it does not alter/populate prepared/pair tables or adopt disposable/archive-derived directories. New composition is additive. System Repository deletion gains only the retained-source conflict mapping in its actual Web endpoint/coordinator paths; no Shared DTO/schema or broader deletion override is added.
