# Retained Source Application Contract

This is the in-process source contract for Story 13.1, not an HTTP API or Worker Agent tool.

## Trust boundary

Ordinary callers supply only Repository/ref/revision and Repository-relative read arguments. Web derives the authenticated subject; application composition fixes purpose and policy bounds. Capture bounds and its deadline come exclusively from one immutable `SourceCapturePolicy` built from validated deployment configuration at startup. No capture request, overload, or caller DTO contains a limit or deadline. Callers cannot supply physical roots, credentials, storage keys, object IDs, budget state, or deadline extensions.

Results use stable status/codes and sanitized logical context. Product messages mapped from them are Simplified Chinese. Logs may include correlation ID, Repository, Profiled Revision, counts, duration, and classification, but never source, credentials, origin URL, raw unsafe path, storage key, or physical path.

## Capture

Conceptual public signature:

```csharp
Task<SourceCaptureResult> AcquireAsync(
    Guid repositoryId,
    string permittedRef,
    CancellationToken cancellationToken);
```

The facade supplies the server-derived subject and `capture` purpose. The capture service receives the validated immutable `SourceCapturePolicy` through composition; changing deployment bounds requires new application composition/restart and cannot affect an in-flight capture.

Preconditions: successful startup validation of every configured capture bound/deadline; authenticated current subject with source authority; existing Repository including standalone; permitted full Git ref (no revision expression/similar-ref fallback/path).

Success/replay exposes Repository, full Profiled Revision/object format, manifest SHA-256, entry/byte counts, completion time, and `Completed` or `Replay`.

Protocol:

1. Require the startup-validated immutable capture policy, then check live authority before Git.
2. Fetch the permitted ref and resolve the local fetched ref once.
3. Capture every committed leaf or fail; unavailable ordinary capabilities remain entries.
4. Verify retained blobs and canonical manifest.
5. Recheck cancellation, deadline, authority, Repository, and revision.
6. Hold the cross-process Repository/revision identity lock across state recheck, atomic seal/adoption, and completion-row insert.
7. Return success only after row commit and perform no fallible cleanup afterward.

Cancellation is honored until the final check. The bounded insert is then non-cancellable; success means the row committed. Invalid configuration fails startup/registration as `source_configuration_invalid` before a capture is callable. At runtime, crossing the server-owned entry/blob/aggregate cap is `source_capture_limit_exceeded`; crossing its monotonic deadline is `source_deadline_exceeded`. Other stable failures include `source_denied`, `source_ref_invalid`, `source_acquisition_failed`, `source_entry_unsupported`, `source_integrity_failed`, `source_storage_unavailable`, and `source_cancelled`.

## Reopen and grant issue

```csharp
Task<OpenSourceReadContextResult> OpenReadContextAsync(
    Guid repositoryId,
    string profiledRevision,
    SourceReadGrantRequest requestedGrant,
    CancellationToken cancellationToken);
```

`requestedGrant` may lower deployment bounds, never raise them. Subject/purpose are server-derived. Issue checks authority before existence disclosure; loads the unique row; validates the contained key; verifies manifest binding/counts/digest and every referenced blob; then returns a process-local context with a fresh cumulative budget and monotonic deadline.

Reopen never contacts Git, checkout, CodeGraph, LSP, an LLM, or an old token registry. Missing/corrupt source yields no context. Reacquisition is a separate explicitly authorized operation.

## Ordinary operations

```csharp
Task<SourceEntryResult> GetEntryAsync(string relativePath, CancellationToken cancellationToken);
Task<SourceListResult> ListAsync(
    string relativeDirectory, int limit, string? cursor, CancellationToken cancellationToken);
Task<SourceBytesResult> ReadBytesAsync(
    string relativePath, long byteOffset, int maximumBytes, CancellationToken cancellationToken);
Task<SourceTextResult> ReadTextAsync(
    string relativePath, long byteOffset, int maximumBytes, CancellationToken cancellationToken);
```

Common order:

1. reject disposed context, invalid bounds, or noncanonical logical path;
2. atomically reserve attempt and requested maximum result cost;
3. check linked cancellation and monotonic deadline;
4. check live subject/purpose/Repository/revision authority;
5. revalidate row/manifest binding and resolve manifest entry;
6. perform bounded I/O and verify returned blob/range;
7. recheck cancellation, deadline, and authority after awaited I/O;
8. return payload, or discard it and return the applicable status.

`ReadBytesAsync` returns the exact stored slice, offset/length, full entry length/SHA-256, and manifest SHA-256. Past-EOF is invalid; a range reaching EOF may return fewer bytes while consuming the reserved maximum.

`ReadTextAsync` requires a regular/executable entry declared strict UTF-8 and scalar-aligned range. It performs strict decoding without replacement, BOM stripping, newline conversion, or normalization and returns the exact byte-slice receipt. Binary/invalid encoding is `Unsupported`; malformed scalar boundaries are `InvalidRange`.

`ListAsync` returns immediate children in ordinal relative-path order. The opaque cursor is integrity-bound to Repository, revision, manifest, directory, and position. A cursor from another scope yields no metadata. Root listing includes bounded limitation summaries for retained leaves without safe paths.

## Path contract

A normal relative path is a strict Unicode string using `/`, not absolute/drive/UNC rooted, with no empty, dot, backslash, colon, NUL, or control segment. Lookup uses ordinal equality without case/Unicode normalization and manifest indexes, never `Path.Combine` over caller input. Alternate spellings are not aliases. Symlinks/gitlinks are not traversable.

## Budget, concurrency, and lifetime

Each attempt reserves all counters before source I/O. Concurrent calls cannot exceed the issued operations, entries, or bytes. Reservations are not refunded; methods never issue a fresh context. If cancellation/deadline occurs after read, payload is discarded. Disposal blocks new calls and cleans only context-owned buffers, not Source Snapshot, Tool Workspace, or other readers.

## Failure/disclosure matrix

| Condition | Status | Payload |
| --- | --- | --- |
| Missing/expired subject, removed role/ownership, wrong Repository/revision | `Denied` | No source metadata |
| Absolute/escaping/noncanonical path | `Denied` | No entry metadata |
| Authorized missing source/entry | `NotFoundOrUnavailable` | Logical identity only |
| Binary text, symlink, gitlink, unsafe path | `Unsupported` | Safe capability/limitation |
| Per-operation/cumulative limit | `BudgetExceeded` | Exhausted public dimension |
| Monotonic deadline | `DeadlineExceeded` | None |
| Linked cancellation | `Cancelled` | None |
| Manifest/blob mismatch | `IntegrityFailure` | Logical identity + correlation |
| Git auth/transport/ref | `AcquisitionFailure` | Authorized logical request |
| Server-owned configured capture bound | `StorageFailure` / `source_capture_limit_exceeded` | Sanitized exhausted dimension |
| Invalid capture configuration | Startup failure / `source_configuration_invalid` | Sanitized key classification; no capture callable |
| Disk full/permission/unavailable durable root | `StorageFailure` | Sanitized storage-unavailable code |

## Trusted workspace

The provider is internal to Infrastructure, not exported as an ordinary Core/Agent tool. A server-created trusted request binds an already verified Source Snapshot, subject, purpose, and configured workspace root. It materializes regular/executable files byte-for-byte into a new owned Tool Workspace and re-verifies them.

Required symlink, gitlink, unsafe/non-UTF-8 path, case collision, or host-unrepresentable entry makes workspace creation `Unsupported`; no links/substitutes are created. Disposal removes only that workspace. Ordinary Story 13.1 qualification does not require a workspace, but this seam prevents later consumers adding a public RootPath bypass.

## Sealed-object cleanup/adoption

Capture, retry adoption, and cleanup acquire the same cross-process Repository/revision identity lock. While holding it they requery PostgreSQL. Cleanup may delete a sealed object only when no row references it and its stale grace elapsed. Capture holds the lock through row commit; adoption holds it through verify/insert. Therefore cleanup cannot delete an object being published/adopted, and ordinary readers never see a pre-row object.
