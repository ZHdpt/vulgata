# Contract: Closed Analysis Runtime Ports

These are semantic Core-facing contracts. `System.Diagnostics`, StreamJsonRpc, JSON-RPC/LSP, CLI parsers, EF, and host configuration types do not cross the ports.

## Shared binding

Every request carries a trusted `AnalysisScope`: authorized `RepositoryId`; exact `ScanRunId` and `ProfiledRevision`; verified working-copy binding token; normalized runtime identity hash; deadline/cancellation; and operation-specific finite bounds. A caller cannot provide an executable, raw argument list, absolute path, environment variable, protocol method, graph query, database path, or workspace path.

## `ICodeGraphAnalysisPort`

| Operation | Inputs | Fixed CLI mapping | Typed output |
| --- | --- | --- | --- |
| `IndexAsync` | scope | `init <bound-root>` | index identity/duration/file counts |
| `RebuildAsync` | scope | `index --quiet <bound-root>` | index identity/duration/file counts |
| `GetStatusAsync` | scope | `status <bound-root> --json` | ready/stale/counts |
| `SearchSymbolsAsync` | scope, normalized search atom, kind allowlist, limit | `query ... --path <bound-root> --limit N --json` | bounded symbols |
| `GetCallersAsync` | scope, exact symbol, limit | `callers ... --path <bound-root> --limit N --json` | bounded relationships |
| `GetCalleesAsync` | scope, exact symbol, limit | `callees ... --path <bound-root> --limit N --json` | bounded relationships |
| `GetRelatedFilesAsync` | scope, exact symbol, limit | `impact ... --path <bound-root> --depth 1 --json` | unique bounded Repository-relative files |

All paths are contained canonical Repository-relative paths and all results are rebound to Repository/Profiled Revision. Parsers reject unknown/duplicate required fields, excess bytes/items/depth, control characters, malformed UTF-8/JSON, path escape, stale index identity, and timeout.

Not exposed: `explore`, `node`, `files`, daemon/lock management, install/uninstall/upgrade/telemetry, arbitrary options, raw graph query, shell, process object, `.codegraph` storage, or network access.

## `IJavaAnalysisSessionFactory` / `IJavaAnalysisSession`

The factory accepts only an `AnalysisScope` and opens one session under an exclusive Repository/Profiled Revision workspace lease. A session exposes:

- `GetDocumentSymbolsAsync(relativePath, limit)`;
- `GetDefinitionAsync(relativePath, position, limit)`;
- `GetReferencesAsync(relativePath, position, limit)`;
- `GetImplementationsAsync(relativePath, position, limit)`.

Each path must be contained, revision-current, and eligible. Positions are one-based at the Core boundary. Results contain typed symbols/locations, canonical relative paths, exact ranges, and the request binding; no raw JSON, external URI, diagnostic stream, or process handle.

Lifecycle:

1. Verify the pinned archive checksum, extracted release identity, Java major 21, StreamJsonRpc 2.25.29 composition, and exclusive workspace binding.
2. Launch the exact contained Equinox launcher through configured Java with fixed JDT-LS properties, `config_win`, stdio, and the leased workspace.
3. Complete initialize/initialized within a bound; explicit qualification later verifies neutral-fixture import/document symbols.
4. Correlate requests, enforce time/item/byte bounds, and map cancellation internally.
5. On dispose, send shutdown then exit, close streams, wait boundedly, and kill the entire child process tree if needed.
6. A malformed response, framing error, workspace mismatch, or failed cleanup makes the session unusable and fail-closed.

Raw LSP methods/notifications, arbitrary initialization options, TCP/network service, workspace edits/commands, and unrestricted diagnostics are not exposed.

## `IRevisionBoundSourceReader`

Request: scope, canonical Repository-relative path, exact one-based inclusive line range, expected source SHA-256, and requested byte cap within the global maximum.

Response: binding, path, actual range, source hash, UTF-8 excerpt, and byte count. The reader rejects rather than silently truncating/clamping. It resolves under the verified working-copy root, rejects traversal/reparse-point escape/stale root, verifies the expected hash, and returns no absolute path. No enumeration, glob, whole-drive access, file handle, arbitrary encoding, write, or network operation exists.

## `IAnalysisRuntimeHealthProbe`

`CheckConfiguredIntegrityAsync` verifies CodeGraph package/command/bundle/version/three hashes; exact JDT-LS release and retained distribution archive checksum; Java major 21; StreamJsonRpc 2.25.29 composition; and workspace-root containment. Ordinary startup must not call CodeGraph `init/index`, create `.codegraph`, launch JDT-LS against a Repository, or import a project.

Missing JDT archive/checksum returns `NotReady/JdtDistributionArchiveMissing` or `JdtDistributionArchiveChecksumMissing`. The core JAR hash may be only a sanitized observation.

## Failure result

Ports expose only closed outcomes: `NotReady`, `Rejected`, `TimedOut`, `Cancelled`, `MalformedResponse`, `IntegrityMismatch`, `WorkspaceConflict`, `ProcessFailed`, `CleanupFailed`, or `Unavailable`, plus a bounded machine code. Raw stdout/stderr, host paths, environment, and secrets never cross the boundary.
