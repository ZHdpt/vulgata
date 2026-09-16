# Implementation Plan: Retained Source Access

**Branch**: `dev` (no branch switch; logical feature directory `015-retained-source-access`) | **Date**: 2026-09-16 | **Spec**: [spec.md](spec.md)

**Input**: Story 13.1 and `specs/015-retained-source-access/spec.md`

## Summary

Create one generic retained-source owner for a Repository at one full Git commit. A trusted application service checks current Repository authority, resolves a permitted ref once, reads the committed Git tree as raw objects, writes an immutable flat blob/manifest object under deployment-configured durable source storage, and publishes exactly one source-owned PostgreSQL record after the object is complete and verified. Ordinary consumers receive only a live-authorized, Repository/revision-bound read context with relative metadata/list/exact-byte/strict-text operations, cumulative limits, and a monotonic deadline. Physical paths remain inside an Infrastructure-only trusted workspace seam.

The design deliberately does not reuse `git archive` as the canonical byte source: existing exact snapshot services are process-local leases and archive materialization can apply committed export attributes. It also does not reuse `RepositoryAuthorizationVerifier`, whose current contract is constrained to one or two Repositories under one System, or `RepositoryAccessScope`, which permits operation without a live verifier. Legacy pair consumers remain composed as they are.

The authorized compatibility prerequisite repairs one pre-existing closed-registry mismatch exposed by full-suite verification: `PreparedScanIdentityBuilders.CreateSemanticFileCodeUnitIdentity` already emits `vulgata/story-12.2/file-code-unit-identity/2.0` with the validated canonical symbol in field 13, while `CanonicalEnvelope` accepts only the corresponding 1.0 domain. The correction adds exactly that emitted 2.0 domain to the closed allowlist. It does not change the envelope grammar, the 1.0 builder or bytes, symbol validation, identity derivation, profiles, source retention, persistence, or Scan behavior.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: ASP.NET Core/Blazor composition; EF Core 10.0.9; Npgsql EF Core provider 10.0.3; installed Git CLI; BCL process, cryptography, JSON, and filesystem APIs. No new package is required.

**Storage**: PostgreSQL source-owned completion records plus a deployment-configured durable filesystem object store, separate from CodeGraph data and disposable Tool Workspaces

**Testing**: xUnit 2.9.3 in `tests/Vulgata.Tests`; isolated local Git fixtures; controlled persistent-directory restart fixtures; PostgreSQL integration for uniqueness/publication/migration behavior; public Core canonical-envelope/identity regressions for the bounded compatibility prerequisite

**Target Platform**: Current .NET 10 Web host on Windows development and the adopted Linux container target; same-volume atomic directory rename is a configured storage prerequisite

**Project Type**: Modular monolith / server-side web application with in-process application contracts

**Performance Goals**: Stream capture and verification in bounded buffers with O(entries + committed blob bytes) work and no whole-snapshot buffering; bound every list/read result before I/O; allow concurrent contexts without a global Repository lock

**Constraints**: Exact committed bytes; no export filters, checkout filters, decoding replacement, mutable checkout fallback, tool-index authority, physical-path disclosure, null live verifier, automatic budget refresh, or canonical-source pruning. Configuration exhaustion is explicit and cannot become coverage semantics.

**Scale/Scope**: One source identity per `(RepositoryId, ProfiledRevision)`. Capture defaults retain the current 10,000-entry / 8 MiB-entry / 64 MiB-aggregate baseline but are deployment-overridable. Ordinary grant defaults are 64 operations, 4,096 returned metadata entries, 4 MiB returned bytes, 60 seconds, and per-operation caps of 512 entries / 1 MiB; deployments may change them without changing source identity.

**Compatibility amendment**: One literal closed-domain registration for the already-emitted semantic File Code Unit identity version 2.0. Version 1.0 remains byte-for-byte stable; all other unregistered domains remain rejected. No data migration, schema, configuration, package, public API, or runtime resource is added.

No `NEEDS CLARIFICATION` items remain. Choices and alternatives are in [research.md](research.md).

## Constitution Check

*GATE before research: PASS. Re-evaluated after design: PASS, subject to the approval gates below.*

| Principle / authority | Design response | Result |
| --- | --- | --- |
| I. Layered Project Boundaries; AD-2 | Core owns source identity, outcomes, and specific contracts. Infrastructure owns Git, filesystem, EF, integrity, and trusted materialization. Web owns live Identity/role composition. Agents receive only injected ordinary contracts. | PASS |
| II. Domain Purity; AD-1/3/16 | Uses canonical terms. One source-owned aggregate is added only for this story; no semantic Knowledge schema or public snapshot version is introduced. | PASS |
| III. Contract Discipline | No public endpoint/UI is added. Contract validation is in Core; surfaced product text remains sanitized Simplified Chinese. No entity data annotations. | PASS |
| IV. Test-First Harness; AD-32 | Public contract seams support honest RED/GREEN. PostgreSQL and fresh-instance evidence remain distinct from unit/static checks; no tool/provider/Scan claim is made. | PASS |
| V. Security; NFR-2.3 | Server-derived subject/purpose are mandatory, authority is live at issue and every operation, paths are logical, and source/credentials/physical locators are excluded from outcomes/logs. | PASS |
| Runtime/storage; AD-15/30/31 | Source has its own configured durable root and immutable published objects; operation state is separately cleanable; no completed-source pruning. Story 13.2 still owns container volume wiring and backup/restore qualification. | PASS |
| Compatibility prerequisite; Constitution II/IV; AD-7/32 | The existing semantic identity producer and closed validator are made internally consistent by registering one literal 2.0 domain. Golden 1.0 bytes, deterministic identities, symbol validation/binding, and unknown-domain rejection remain executable test obligations. No semantic owner, profile, Scan strategy, or accepted Knowledge contract changes. | PASS |

## Project Structure

### Documentation (this feature)

```text
specs/015-retained-source-access/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── retained-source-application-contract.md
    └── retained-source-manifest-v1.schema.json
```

`tasks.md` contains the accepted retained-source work completed through T040. It remains unchanged during this plan update and will be regenerated only for the bounded compatibility prerequisite after plan acceptance, before the updated delivery definition receives independent review.

### Source Code (repository root)

```text
src/dotnet/
├── Vulgata.Core/
│   ├── Entities/SourceSnapshot.cs
│   └── DomainServices/{IRetainedSourceRepository,RetainedSourceContracts,
│                      CanonicalEnvelope,PreparedScanContracts}.cs
├── Vulgata.Infrastructure/
│   ├── Data/{Configurations/SourceSnapshotConfiguration,RetainedSourceRepository,VulgataDbContext}.cs
│   ├── Data/Migrations/<generated retained-source migration>.cs
│   └── Source/{GitCommittedObjectReader,DurableRetainedSourceStore,
│              RetainedSourceCaptureService,RetainedSourceReadContext,
│              TrustedSourceWorkspaceProvider}.cs
└── Vulgata.Web/
    ├── Api/Repositories/RepositoryApiEndpoints.cs
    ├── Data/{RepositoryManagementCoordinator,SourceRepositoryAuthorizationVerifier}.cs
    └── Program.cs

tests/Vulgata.Tests/RetainedSource/
├── RetainedSourceCaptureTests.cs
├── RetainedSourceReadContextTests.cs
├── RetainedSourceAuthorizationTests.cs
├── RetainedSourcePersistenceTests.cs
└── RetainedSourcePostgreSqlTests.cs

tests/Vulgata.Tests/PreparedScanning/
├── PreparedCanonicalEnvelopeTests.cs
├── PreparedCanonicalEnvelopeGoldenTests.cs
└── PreparedFileCodeUnitAndSelectionTests.cs
```

**Structure Decision**: Keep the modular monolith. Core exposes no EF, ASP.NET, process, filesystem, or physical-path type. Infrastructure is the single retained-source writer. Web derives the authorization subject and checks current Identity/Repository authority. The compatibility amendment remains inside the existing Core canonical registry and its existing public identity/envelope test seams. No changes are planned in Agents, Shared, Infrastructure, Web, persistence, CodeGraph, LSP, Knowledge, Chat, profiles, or legacy pair contracts for that amendment. File names may consolidate during task generation, but these responsibilities and dependency directions are fixed.

## Architecture Decisions

### 1. Source-owned identity and persistence

`SourceSnapshot` is immutable with an internal identifier and unique `(RepositoryId, ProfiledRevision)`. Public identity remains Repository plus full Git commit. PostgreSQL stores only manifest identity, bounded counts, object format, internal relative storage key, and completion time. Entry/blob details live in the integrity-protected source object, avoiding future Code Unit, Document, Edge, flow, or tool schema.

The Repository foreign key uses `Restrict`. Once a completed Source Snapshot exists, Repository deletion cannot implicitly remove its source record. This is the source-retention consequence of an explicit Repository delete, not automatic pruning policy. The existing System-Repository delete route in `RepositoryApiEndpoints.cs` and coordinator route in `RepositoryManagementCoordinator.cs` translate this specific conflict into a sanitized Chinese `409`; no new DTO, Shared change, or Snapshot UI is introduced. A broader deletion/retention override is outside Story 13.1.

### 2. Exact Git capture without archive/checkout transformations

Acquisition authorizes first, creates an owned bare temporary Git directory, fetches only the permitted full ref into an operation-local ref, and resolves that ref once to a full commit. It records Git object format and enumerates the commit recursively with NUL-delimited Git plumbing. Blob bytes are read by object ID, never from a checkout or `git archive`; `.gitattributes`, filters, line-ending conversion, later ref movement, and working-copy changes cannot transform them.

The canonical store is flat. Each leaf records raw Git path bytes, a safe strict-UTF-8 relative path when representable, Git mode/type/object ID, byte length, and SHA-256. Regular/executable blobs are stored byte-for-byte under content digests. Symlink blobs are retained but ordinary file/text access is unsupported; gitlinks retain the committed object ID and report unavailable content. Unsafe/non-UTF-8 paths remain integrity-accounted and surface as authorized limitation entries without a traversable path.

### 3. Publication and retry linearization

Capture writes below the configured source root, enforces count/size bounds while streaming, writes the canonical manifest last, rereads/verifies every blob and manifest digest, and rechecks cancellation, deadline, authority, Repository binding, and ref/commit binding.

After staging verification, seal/adoption/cleanup for one Repository/revision is serialized by an exclusive cross-process identity lock under a dedicated `.locks` directory. While holding it, the owner rechecks for an existing completed row/object, atomically renames the operation directory on the same filesystem to a deterministic sealed key derived from Repository, Profiled Revision, and manifest SHA-256, and inserts the completed `SourceSnapshots` row. That PostgreSQL commit is the sole reader-visible publication point: readers require both row and matching object. No fallible cleanup or remote call follows success.

Cancellation is honored through the final pre-publication check. After it, the short row insertion is non-cancellable so the caller gets `Completed`/`Replay` exactly when the linearization point is reached. Cancellation before it returns `Cancelled` with no completed row. A crash after rename but before insert leaves an unreferenced sealed object; a later freshly authorized retry re-verifies/adopts it. Atomic rename and DB uniqueness yield one matching completion/replay. Any same-identity digest disagreement is an integrity conflict.

Operation directories hold exclusive lease markers. Disposal removes owned staging/bare Git state. A pre-row sealed object can be adopted or cleaned only while holding the same identity lock: the actor rechecks PostgreSQL under the lock; cleanup additionally requires the configured stale grace to have elapsed. Therefore cleanup cannot delete during seal/insert or race an adopter. Completed objects are never cleaned automatically.

### 4. Live Repository authorization

A required source-specific verifier replaces neither legacy verifier. Web derives the subject from `IUserContext`; the verifier reloads user/roles and Repository ownership on every check:

- Administrator: current Administrator role and existing Repository.
- System Owner / System Repository: current SystemOwner role and current assignment for the Repository's current System.
- Standalone Repository: current Administrator or SystemOwner role, matching the existing ManagementAccess boundary under which standalone Repositories are listed/created.

The initial check returns an authorized Repository descriptor including its current Git origin and System membership; the pre-publication recheck requires those acquisition bindings still match. Capture checks before Git resolution and before publication. Grant issue checks before existence/integrity disclosure. Every list/read checks before lookup and after awaited I/O. Missing verifier/subject, removed role/assignment, moved/deleted Repository, changed origin/System binding, or Repository/revision mismatch yields sanitized denial without existence or path disclosure.

### 5. Grant, budget, deadline, and cancellation

`SourceReadContext` is a server-created in-process capability bound to subject, purpose, Repository, Profiled Revision, Source Snapshot, manifest digest, cancellation lifetime, and one budget. It is not a caller DTO or durable token registry. A fresh process issues a fresh context only after fresh authority and full retained-object verification.

The read budget has cumulative operation, returned-metadata-entry, and returned-byte counters plus per-operation caps. A read-grant request may only lower these server values. Operations atomically reserve one attempt and requested maximum result cost before I/O; insufficient capacity returns `BudgetExceeded` without partial payload/refund. Concurrent calls share counters. Failed/denied/cancelled attempts consume the operation reservation. Methods never auto-create a replacement context.

Deadline enforcement uses `TimeProvider.GetTimestamp()`; wall-clock `ExpiresAt` is diagnostic. Calls check cancellation/deadline before and after authorization and I/O. Crossing the bound discards payload and returns `DeadlineExceeded` or `Cancelled`.

### 6. Ordinary and trusted interfaces

The ordinary Core contract exposes capture/reopen by logical identity, grant opening, relative metadata/listing, exact byte ranges, and strict UTF-8 text. Outcomes distinguish `Completed`, `Replay`, `Denied`, `NotFoundOrUnavailable`, `Unsupported`, `BudgetExceeded`, `DeadlineExceeded`, `Cancelled`, `IntegrityFailure`, `AcquisitionFailure`, and `StorageFailure`.

No result contains RootPath, credentials, origin URL, operation path, or storage key. Lookup uses exact ordinal `/` paths; absolute, empty/noncanonical/dot segments, backslashes, NUL/control characters, and unsupported-entry traversal are rejected before storage. Cursors are opaque and scope/manifest-bound.

The physical-workspace seam is internal to Infrastructure and is not registered as an ordinary/Agent contract. It materializes a verified Source Snapshot into a contained disposable Tool Workspace only after trusted-purpose authorization. Symlink/gitlink/unsafe/colliding paths make workspace acquisition explicitly unsupported. Releasing a workspace removes only its directory.

### 7. Verification strategy

Behavioral TDD seams are the public application interfaces. Focused suites prove:

- System and standalone capture, ref pinning, raw bytes despite export/filter/autocrlf fixtures, changed checkout/ref, and explicit symlink/gitlink/path/encoding limits;
- the public capture contract and production DI expose no caller limit/deadline field or overload; invalid server configuration fails before Git/storage, and a deliberately small server policy cannot be raised by a caller;
- cancellation before verification/at publication, Git/auth/storage/capacity/integrity failures, no completed partial row, owned cleanup, sanitized outcomes;
- concurrent same-identity rename/DB races, crash between rename/insert, retry adoption, digest conflict, concurrent read/release, no canonical pruning;
- exact ordinal containment, path variants, byte/UTF-8 boundaries, cursor scope, atomic counters, exact-boundary exhaustion, monotonic deadline, revocation, Repository move/delete/revision mismatch, no path disclosure;
- a true separate helper process reopening through production composition while sharing only PostgreSQL/source root and with the remote unavailable, with no old token, CodeGraph, LSP, LLM, semantic table, or cache;
- migration, uniqueness, immutable-record behavior, Repository delete restriction/Chinese conflict mapping, and fail-closed production DI.

SQLite/unit tests may cover contract logic. PostgreSQL publication/migration/FK and fresh-instance persistent storage require owned integration fixtures. Story 13.1 requires no provider, CodeGraph, LSP, Docker-stack, backup/restore, or browser E2E execution; the later E2E gate still applies its matrix.

### 8. Deployment-overridable configuration

| Key | Default / rule |
| --- | --- |
| `SourceStorage:RootPath` | Required explicit durable path; resolved once. It must not equal or sit below `CodeGraph:DataRootPath` or the disposable workspace root. |
| `SourceStorage:MaximumEntries` | `10000`, positive |
| `SourceStorage:MaximumBlobBytes` | `8388608` (8 MiB), positive and no greater than aggregate |
| `SourceStorage:MaximumAggregateBlobBytes` | `67108864` (64 MiB), positive |
| `SourceStorage:CaptureDeadlineSeconds` | `600`, positive; applied monotonically to ref resolution/capture/publication |
| `SourceStorage:OperationStaleAfterMinutes` | `1440`, positive; cleanup still requires operation/identity lock and DB recheck |
| `SourceStorage:VerificationBufferBytes` | `65536`, bounded to 4 KiB–1 MiB |
| `SourceStorage:Read:MaximumOperations` | `64`, positive |
| `SourceStorage:Read:MaximumMetadataEntries` | `4096`, positive |
| `SourceStorage:Read:MaximumReturnedBytes` | `4194304` (4 MiB), positive |
| `SourceStorage:Read:MaximumEntriesPerOperation` | `512`, positive and no greater than cumulative entries |
| `SourceStorage:Read:MaximumBytesPerOperation` | `1048576` (1 MiB), positive and no greater than cumulative bytes |
| `SourceStorage:Read:DeadlineSeconds` | `60`, positive and enforced monotonically |

These keys are the sole capture-limit/deadline authority. Startup validation produces one immutable internal `SourceCapturePolicy`; `AcquireAsync` accepts no limit/deadline input, and an in-flight capture retains the policy snapshot with which it started. Changing deployment values requires new application composition/restart. Invalid/missing required configuration fails source-capability registration/startup with `source_configuration_invalid`; it never falls back to CodeGraph storage, a temporary directory, or an unbounded value. Crossing a valid configured cap returns `source_capture_limit_exceeded`, and crossing the configured monotonic capture deadline returns `source_deadline_exceeded`. These defaults are safety/admission values, not supported-Repository eligibility or proof of capacity.

## Compatibility Correction Plan

This is implementation planning for an already-established producer contract, not a ninth architecture decision. The producer already chose the semantic identity domain, canonical field shape, symbol binding, and validation rule. The correction closes the observed implementation mismatch at the existing validator registry.

The technical authority is the current source in `src/dotnet/Vulgata.Core/DomainServices/PreparedScanContracts.cs` and `CanonicalEnvelope.cs`, constrained by AD-7's server-derived canonical identity rule and the Constitution's exact-history/test-first requirements. The current PRD's FR-2.4, FR-2.5, FR-4.9, NFR-3.1, NFR-3.3, and NFR-4.1 retained-source obligations remain unchanged; its section 5 compatible-version qualification boundary is served by executable compatibility evidence rather than a new product requirement.

`CanonicalEnvelope` remains a closed literal registry. Add only `vulgata/story-12.2/file-code-unit-identity/2.0`, matching the domain already emitted by `PreparedScanIdentityBuilders.CreateSemanticFileCodeUnitIdentity`. Do not add prefix, wildcard, version-range, caller-supplied registration, fallback, or version coercion.

The existing 2.0 producer remains the contract owner for its canonical fields. Field 13 continues to contain `RequireJavaSymbol(symbolName)`, so changing only a valid symbol changes canonical bytes and the derived identity, while invalid symbols fail before an artifact is returned. The 1.0 producer and grammar remain untouched; literal 1.0 golden bytes and deterministic identity values are regression invariants. `CanonicalEnvelope.Create` and `CanonicalEnvelope.Validate` must still reject every unregistered domain/version.

Behavioral TDD starts at the current public Core seams. The first RED proves that a valid semantic 2.0 artifact can be created and validates, while the same fixture remains deterministic. Companion tests prove symbol differentiation, invalid-symbol rejection, exact 1.0 golden compatibility, and representative unknown-domain rejection through both creation and validation. The minimal GREEN is the single allowlist entry unless the RED exposes a separately classified defect. The two pending full-suite fixture edits remain test-maintenance work and are not production authority.

## Planned-Change and Approval Gates

1. **Architecture-decision trigger**: this plan settles cross-resource publication, raw Git-object capture, source-specific authorization, cumulative grants, and ordinary/trusted boundaries under AD-2/3/30/31. A plan-only independent review must accept the raw plan set.
2. **Repository planned-change trigger — material Scan strategy change (`AGENTS.md`)**: the generic acquisition/retry route replaces archive/process-lease semantics for new Scan inputs, supports standalone source without pair/System identity, and changes capture/reopen/retry behavior. It affects PRD FR-2.4, FR-2.5, FR-4.9 and source portions of NFR-3.1/3.3/4.1. After plan acceptance, the root must obtain explicit user authorization before `speckit-tasks`.

No Worker Agent/Agent tool is added or exposed and no PRD requirement is weakened. This approval covers only progression from accepted plan hashes to task generation. Implementation still requires accepted complete definition and the separate implementation approval, then TDD, convergence, quality, E2E decision, security/migration checks, and pinned verification.

**Compatibility-delta gate classification**: no new architecture decision is required. The delta makes an existing producer and its existing closed validator agree on the producer's already-selected literal version; it introduces no new identity algorithm, schema grammar, ownership seam, persistence model, or runtime topology. The original retained-source architecture classification and its accepted material Scan-strategy plan-change approval remain intact and are not inherited as a waiver. The compatibility delta itself triggers none of the additional `AGENTS.md` planned-change gates: it adds no Agent tool, changes no parsing/filtering/scheduling/grouping/retry/dispatch or other Scan strategy, and does not change, weaken, or conflict with a current PRD requirement. After plan acceptance, ordinary progression to regenerated affected tasks is appropriate; independent definition review and the preserved implementation approval gate still follow before implementation.

## Repository Fit and Source Evidence

| Evidence | Consequence |
| --- | --- |
| `src/dotnet/Vulgata.Infrastructure/Git/ExactGitRevisionSnapshotService.cs` | Reuse containment/process ideas; replace archive materialization and disposable lease ownership for the generic route. |
| `src/dotnet/Vulgata.Infrastructure/Git/CommittedRepositorySnapshotService.cs` | Current bounded values seed overridable defaults; lease cleanup cannot own canonical source. |
| `src/dotnet/Vulgata.Infrastructure/Git/GitWorkingCopyService.cs` | Reuse safe ArgumentList/no-prompt/bounded diagnostics/process-tree cancellation; add binary-safe bounded object reads. |
| `src/dotnet/Vulgata.Web/Data/RepositoryAuthorizationVerifier.cs` | Maximum-two/common-System semantics remain legacy-only. |
| `src/dotnet/Vulgata.Core/DomainServices/KnowledgeStoreContracts.cs` | Nullable live verification is unsuitable for source; new contexts fail closed. |
| `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs` and `ManagementAccessRequirement.cs` | Establish current standalone authority and System-owner boundary. |
| `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs` | Owns the existing System-Repository delete response that must sanitize a retained-source conflict. |
| `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs` and configurations | Add one source-owned DbSet/configuration/migration with specific repository and PostgreSQL constraints. |
| Existing exact-Git, revision-reader, and Repository-management tests | Reuse harness mechanics without crediting them as generic behavior. |
| `src/dotnet/Vulgata.Core/DomainServices/CanonicalEnvelope.cs` | The literal closed domain registry accepts File Code Unit identity 1.0 but omits the semantic producer's exact 2.0 domain; preserve closed rejection and the byte grammar. |
| `src/dotnet/Vulgata.Core/DomainServices/PreparedScanContracts.cs` | `CreateSemanticFileCodeUnitIdentity` already emits the exact 2.0 domain and binds a validated canonical symbol at field 13; do not redesign the producer. |
| `tests/Vulgata.Tests/PreparedScanning/PreparedCanonicalEnvelopeTests.cs`, `PreparedCanonicalEnvelopeGoldenTests.cs`, and `PreparedFileCodeUnitAndSelectionTests.cs` | Extend current public seams with executable 2.0 acceptance/determinism/symbol tests, 1.0 byte/identity compatibility, invalid-symbol rejection, and closed unknown-domain rejection. |
| `tests/Vulgata.Tests/PreparedScanning/PreparedScanReleaseQualificationTests.cs` and `ReviewedExactProfileSeedPostgreSqlTests.cs` | These are the current full-suite fixtures that exposed the mismatch. Re-run them as affected regression evidence after focused Core tests, without treating profile/Scan execution as changed scope. |

## Failure and Recovery Behavior

Unauthorized/not-visible details are coalesced. Authorized logical context may include Repository ID and requested/resolved revision; diagnostics exclude origin credentials, source, internal objects, and physical paths.

Capture never selects another ref/checkout/source/remote. Auth/transport/ref failure is `AcquisitionFailure`; entry/aggregate/disk capacity is `StorageFailure`; malformed Git output or digest disagreement is `IntegrityFailure`; cancellation is `Cancelled`. Every retry begins with fresh authority. Replay verifies manifest and bytes.

Reopen never fetches. Missing/corrupt retained state fails; it is not automatically repaired. Reacquisition is separate and must resolve an authorized ref to the same Profiled Revision and verify the same manifest digest.

## Complexity Tracking

No Constitution violation is requested. PostgreSQL and durable files cannot share a portable transaction, so the plan uses a DB visibility point and treats pre-row sealed objects as unretained retry state. Rejected simpler alternatives and consequences are explicit in `research.md`.

## Inline Plan Self-Review

**Evidence kind**: self-review
**Stage**: plan
**Observed at**: 2026-09-14T09:50:10Z
**Pinned source**: `cd82c8994063fa522253264f59c13b1cbc08a3f2` (activation metadata; application source inherited from `bff5409`)
**Pinned specification**: SHA-256 `6400232780739B7574760AFD74B0DCBA02C19B17D19383F3B0954B85BEE88BCA`

| Review lens | Result | Evidence / consequence checked |
| --- | --- | --- |
| Feasibility and repository fit | PASS | .NET 10/BCL/Git/EF/Npgsql already exist. Current Git services prove safe process/cancellation/containment patterns and current tests prove isolated Git/PostgreSQL harness seams. The only new low-level need is a bounded binary stdout adapter; no package or remote service is assumed. |
| Invariants and ownership | PASS | One immutable source owner; public identity remains Repository+Profiled Revision; raw committed blobs and manifest are separate from tool state; ordinary interfaces have no physical path; legacy pair consumers and semantic tables remain untouched. |
| Atomicity, concurrency, and cancellation | PASS | DB commit is the visibility point. Cross-process identity lock covers row recheck, seal/adopt, and insert; cleanup uses the same lock plus row recheck/stale grace. Cancellation before final gate cannot publish; after gate the bounded insert returns a deterministic completed/replay result. |
| Integrity, paths, encoding, unsupported entries | PASS | Raw path bytes and all leaves enter the manifest; blobs are SHA-256 verified; safe logical paths use ordinal lookup; symlink/gitlink/unsafe/non-UTF-8 cases are retained and explicit, never materialized/traversed/omitted; text decoding is strict and byte-receipted. |
| Live authorization and disclosure | PASS | Source verifier is mandatory and separate from pair/null-verifier contracts. Role/assignment/Repository/origin/System bindings are checked before capture, before publication, at grant issue, and around reads. Denials coalesce existence and exclude sensitive paths/source. |
| Grant and failure behavior | PASS | Shared atomic reservations prevent concurrent overspend/automatic refresh. Deadline is monotonic. Every Git, capacity, integrity, unavailable, unsupported, denial, deadline, and cancellation branch has a stable sanitized outcome and no partial payload. |
| PR-13.1-001 capture-limit ownership | PASS | Capture accepts only Repository/ref/cancellation. One startup-validated immutable deployment policy owns entry/blob/aggregate/deadline bounds; callers cannot raise or replace it, invalid configuration prevents capture, and runtime cap/deadline failures have distinct stable codes and contract tests. |
| Test seams and evidence limits | PASS | Public capture/open/list/read contracts support honest behavioral RED. True separate OS-process reopen, PostgreSQL publication/migration, local Git byte fixtures, concurrency barriers, and path/auth adversaries are explicit. No source-only result is credited as Scan/wiki/tool/provider/Docker qualification. |
| Migration and compatibility | PASS | Adds only SourceSnapshots and additive composition. No historical data is auto-adopted. Restrict preserves retained source; only the actual Web Repository delete endpoint/coordinator gain a Chinese 409 mapping. No Shared schema or wholesale pair migration is needed. |
| Constitution / ADR / PRD alignment | PASS | Checks I–V and AD-1/2/3/5/6/8/15/16/30/31/32. The plan implements, rather than weakens, cited PRD source portions. No authority contradiction or Constitution exception was found. |
| Alternatives and consequences | PASS | `research.md` states choices, rationale, rejected alternatives, and consequences for acquisition, storage, publication, authorization, grants, text, trusted paths, configuration, and compatibility. |
| Workflow gates | PASS | Architecture and material Scan-strategy triggers are explicit. Plan-only independent acceptance and named user plan authorization precede tasks; separate implementation approval remains later. |

**Artifact checks performed**: selected plan template filled; companion artifacts present; manifest JSON parsed by PowerShell; targeted placeholder search found no unresolved template/clarification value; `git diff --check -- specs/015-retained-source-access` returned no error. Schema cross-field rules that JSON Schema cannot express (entry count/sum, sort order, object-format/revision-length agreement, canonical byte encoding) are explicitly owned by the Infrastructure manifest codec/validator and covered at the public capture/open seams.

**Finding disposition**: `PR-13.1-001` corrected at the earliest owner. The caller-controlled `SourceCaptureLimits` parameter was removed and application contract, configuration ownership, failures, and planned tests now use one fail-closed server policy.

**Self-review verdict**: PASS. The corrected plan set is coherent and reviewable with no unresolved clarification, authority contradiction, or hidden implementation waiver. It is ready for independent plan-only re-review, not task generation or implementation.

## Compatibility-Amendment Self-Review

**Evidence kind**: self-review
**Stage**: plan update
**Observed at**: 2026-09-16
**Pinned source**: `b9517a01a07f171107d253d4b62287e373abf49c`
**Pinned specification**: SHA-256 `7DCE86D54A67B925EB11FC51662077260BE2D98A018C68DC8F6770941E767D14`

| Review lens | Result | Evidence / consequence checked |
| --- | --- | --- |
| Feasibility | PASS | The implementation is one literal addition to the existing `HashSet<string>`; no package, migration, configuration, service, or runtime resource is needed. |
| Repository fit and ownership | PASS | Both the closed validator and semantic identity producer are existing Core domain services. Tests use existing public Core seams in the single test project. No project dependency direction changes. |
| Failure behavior | PASS | Known 2.0 becomes valid. Invalid symbols still fail in `RequireJavaSymbol`; unknown domains still fail in `RequireDomain`/return false from validation. No fallback or open-ended matching is introduced. |
| Public test seams | PASS | Public `CreateSemanticFileCodeUnitIdentity`, `CanonicalEnvelope.Create`, and `CanonicalEnvelope.Validate` cover valid 2.0, determinism, symbol binding, invalid symbols, 1.0 goldens, and unknown rejection. The two failing full-suite fixtures and retained-source regressions provide surrounding evidence. |
| Constitution / ADR alignment | PASS | Preserves domain purity, deterministic canonical identity, exact history, test-first delivery, and truthful evidence under Constitution II/IV and AD-7/32. It makes no new architecture decision. |
| PRD / planned-change gates | PASS | The delta neither changes nor weakens the PRD and changes no Agent tool or Scan strategy. The original retained-source plan-change approval remains separately binding; no waiver is inherited. |
| Scope preservation | PASS | Source T001-T040 and their design remain unchanged. No historical feature is activated, no profile/schema/public API is changed, and no broader semantic or Scan qualification is claimed. |

**Amendment verdict**: PASS. No clarification or new architecture decision is required. The amended plan set is ready for regeneration of only the affected compatibility tasks and subsequent independent definition review; it is not implementation authority.
