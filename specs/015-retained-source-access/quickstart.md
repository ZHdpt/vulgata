# Quickstart: Validate Retained Source Access

This evidence boundary is source-only. No CodeGraph, LSP, LLM, semantic Knowledge, Chat, full Scan, Docker-stack, backup, or restore result is required or credited.

## Prerequisites

- .NET 10 SDK and repository package versions
- Git CLI available to the test process
- isolated owned temporary directories
- explicitly owned PostgreSQL test database
- `SourceStorage:RootPath` set to an isolated persistent directory outside `CodeGraph:DataRootPath`
- capture/read configuration recorded with results

Do not use shared repositories, source roots, databases, or another process's workspace. Web startup applies migrations and role seeding.

## Static contract checks

```powershell
rg -n ": NEEDS CLARIFICATION$|\[FEATURE\]|\[###-feature-name\]|\[DATE\]" specs/015-retained-source-access/plan.md specs/015-retained-source-access/research.md specs/015-retained-source-access/data-model.md specs/015-retained-source-access/contracts
Get-Content -Raw specs/015-retained-source-access/contracts/retained-source-manifest-v1.schema.json | ConvertFrom-Json | Out-Null
git diff --check -- specs/015-retained-source-access
```

Expected: no placeholder matches; JSON parse and diff check succeed.

## Focused controlled tests

After approved implementation:

```powershell
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~RetainedSource"
```

### Exact committed bytes

Create isolated Git fixtures with ordinary, executable, empty, binary, invalid-UTF-8, and mixed-newline blobs; export/filter-sensitive `.gitattributes`; symlink/gitlink entries; and case/Unicode-distinct paths where supported. Capture a ref, then change ref/working copy.

Verify every committed leaf is integrity-accounted, regular/executable reads equal `git cat-file` bytes, unavailable capabilities are explicit, and no alternate source is selected.

### Authorization and containment

Exercise Administrator and assigned System Owner for a System Repository; current Administrator/SystemOwner ManagementAccess for standalone; removed role/assignment, unauthenticated subject, moved/deleted Repository, wrong Repository/revision, and missing verifier.

Exercise absolute/UNC/drive, backslash, dot/empty/control segment, normalization variant, wrong-cursor scope, symlink, and gitlink requests. Every denial returns no source bytes, existence/count/hash metadata, credential, storage key, or physical path.

### Budget, deadline, cancellation

Run exact-boundary and concurrent operations against one context. Atomic reservations must not exceed operation/entry/byte budgets; no method auto-refreshes; documented failed attempts consume an operation; insufficient capacity returns no partial payload. Advance fake `TimeProvider` across auth/storage awaits to prove post-I/O suppression. Cancel before capture verification, at final publication check, and during reads; assert no completed partial row and only owned cleanup.

For capture, verify the public request/signature contains no entry/blob/aggregate/deadline value or overload. Invalid `SourceStorage` capture configuration must fail application composition before Git or filesystem activity. Compose with deliberately small server caps and prove no caller can raise them; cap exhaustion returns `source_capture_limit_exceeded`, deadline exhaustion returns `source_deadline_exceeded`, and changing values requires a newly composed application instance.

### Publication, concurrency, integrity

Inject barriers at staging verification, identity-lock acquisition, atomic rename, and row insertion. Verify:

- concurrent same-identity captures produce one matching row/object and completion/replay;
- process failure after rename/before insert yields no readable Source Snapshot, then fresh authorized retry adopts under the identity lock;
- cleanup cannot acquire the identity lock during publication/adoption and, after acquiring it, rechecks row plus stale grace before deletion;
- same Repository/revision with different digest is an integrity conflict;
- storage full/permission/root failures and blob/manifest corruption are sanitized;
- releasing one context/workspace cannot delete canonical source or invalidate another reader.

## True separate-process reopening

Use a purpose-built test helper executable or a test-host mode launched as a separate OS process. Do not substitute a second DI provider in the same process.

1. Process A uses production composition to capture and complete a Source Snapshot in the owned PostgreSQL database/source root.
2. Process A writes only logical result evidence, disposes the grant/services, and exits.
3. The harness makes the local Git remote unavailable and changes/removes any checkout.
4. Process B starts production composition with the same database/source root and a newly authenticated/authorized subject.
5. Process B opens Repository plus Profiled Revision, performs full verification, lists, and reads exact bytes.

Expected: Process B succeeds without Process A memory/token, new clone, remote, CodeGraph root, cache, or Tool Workspace. Corrupting a blob before Process B yields `IntegrityFailure`, never automatic reacquisition.

## PostgreSQL migration and compatibility

Use the owned PostgreSQL fixture to prove migration/check constraints, unique Repository/revision and storage key, immutable row behavior, and restrictive Repository deletion. Verify the existing System-Repository endpoint/coordinator maps only this retained-source conflict to sanitized Chinese `409`, with no EF/path detail. SQLite is not evidence for these behaviors.

## Surrounding verification

```powershell
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
```

Report static/schema inspection, controlled Git/filesystem, owned PostgreSQL, and separate-process persistence independently. Passing proves only retained-source capture/reopen/scoped reads, never completed Scan/wiki/Chat, native MCP/LSP, full Docker stack, backup, restore, or universal capacity.
