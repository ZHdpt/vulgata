# Implementation Plan: Exact Cross-Repository Resolution

**Branch**: `detached@67b5eaf` | **Date**: 2026-08-03 | **Spec**: [spec.md](spec.md) | **Execution level**: `expedited-mvp`

## Summary

Add one deterministic, configuration-driven Cross-Repository Resolution path for the two prepared Repositories. The reviewed schema-versioned profile pins the observed `mbs_account` -> `mbs_channel` `cust/updateSession` occurrence at the immutable `vulgata/August15th-sample` commits, including exact Repository-relative call/XML/implementation selectors and a closed provider-`in/` normalization rule. It also pins one actual file/storage observation with an authorized owner selector. At runtime an authorized caller binds aliases to managed Repository/ScanRun identities; the profile contains no database IDs, Git URLs, mount locations, credentials, or Repository-name branches.

The resolver obtains each exact object in an isolated operation-specific Git root that cannot detach or alter the ordinary managed checkout, requires the durable Profiled Revisions and current Pass 1 Code Logic Document Versions to match those commits, extracts the reviewed `trans@name` and direct literal member invocation, and compares normalized identifiers ordinally in the configured direction. Exactly one eligible provider commits one cross-Repository `REFERENCES` Edge plus typed evidence. Inexact/ambiguous eligible evidence and the anchored unsupported observation commit bounded canonical Uncertainties; binding, authorization, and revision failures stop before source inspection or graph writes. The service runs only when two explicitly supplied prepared Pass 1 executions are durable; it is not a general scheduler.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: existing EF Core 10.0.9, Npgsql 10.0.3, ASP.NET Core DI/authorization, `System.Text.Json`, `System.Xml`, and existing Git process/snapshot services; no new package

**Storage**: PostgreSQL 17, schema `vulgata`, through `VulgataDbContext`; SQLite relational integration for fast invariants

**Testing**: xUnit in `tests/Vulgata.Tests`; real Git fixtures, SQLite integration, PostgreSQL migration apply/rollback/reapply and concurrency/idempotency checks

**Target Platform**: Vulgata single-container Linux deployment and Windows development

**Project Type**: existing Blazor Web App with Core, Infrastructure, Shared, Web, Agents, and Tests boundaries; no new public API, UI, Agent tool, or LLM call

**Performance Goals**: bounded two-Repository resolution with at most 16 allowlisted files per side, 2,048 provider candidates, 2,048 consumer candidates, and one atomic result transaction; no unbounded repository walk

**Constraints**:

- profile schema, profile identity/version, alias, occurrence, selector, normalizer, and comparison values are closed and bounded;
- aliases and IDs are Form-C normalized, 1-128 UTF-8 bytes; endpoint identifiers are 1-512 UTF-8 bytes; evidence locators are at most 4 KiB;
- exact refs are full `refs/tags/...` names and expected commits are exactly 40 lowercase hexadecimal characters;
- profile selectors are Repository-relative exact file allowlists beneath the fixed provider root or configured consumer roots; no `..`, drive, UNC, absolute, symlink/reparse traversal, or broad filesystem root;
- provider XML uses DTD-prohibited, resolver-disabled, bounded streaming parsing; only configured `trans` elements and `name` attributes are candidates;
- consumer extraction uses a bounded lexical scanner for the reviewed simple member invocation `netTools.execute` at its exact file/line/ordinal; only one direct decoded string-literal token at zero-based argument index 1 is exact;
- normalization is `custom-rpc-in-prefix-v1`: Java/XML decoding, Unicode Form C, ASCII-edge trimming, and required removal of exactly one provider-side `in/` prefix; no case folding, URL decoding, other slash rewriting, arbitrary mapping, or inference; comparison is `StringComparison.Ordinal`;
- durable Uncertainty `sub_status` is exactly canonical `cross_repo`, `unknown_target`, or `ambiguous`; all detailed distinctions use closed reason/observation-kind fields;
- runtime bindings contain managed Repository/ScanRun/ScanExecution IDs as trusted request data, and durable rows retain the internal relational PK/FK identities required for ownership, referential integrity, deterministic idempotency, and authorized retrieval; none is embedded in profile resources or production branches;
- profile bytes and textual evidence/diagnostic/log/exception fields never contain internal relational identifiers, source text, credentials, Git URLs, absolute paths, provider diagnostics, or mutable System context; unauthorized results expose only the existing metadata-free restricted marker;
- existing Document Version/Source Anchor/current-pointer invariants stay intact; all new graph writes are atomic and idempotent;
- no failing test is committed.

**Scale/Scope**: exactly the prepared two-Repository deadline profile and one configured custom-RPC occurrence per profile entry; either Repository may be consumer or provider per entry. General endpoint discovery, persistent endpoint/dispatch catalogs, MQ, storage, navigation, object flow, Service Topology MCP, deadlock policy, multi-Repository scheduling, and final Docker/two-Repository clean-database rehearsal remain out of scope.

## Authority Reconciliation

1. PRD §5.16 and the approved August 15 change are the accepted deadline overlay. They require exact configured RPC/HTTP matching and defer general communication kinds, advanced uncertainty policy, Service Topology MCP, concurrency, and the final integrated rehearsal to later work/Story 11.5.
2. PRD §5.5 FR-5.1/FR-5.14 and the story require configuration-driven extensibility. The profile therefore carries selectors and policies as data; production code contains no `mbs_account`, `mbs_channel`, Repository-name, endpoint, path, or direction conditional.
3. PRD §5.6 FR-6.2 and scanning plan §11 describe canonical provider `communication_endpoints` and consumer `dispatch_sites`. This deadline slice models those concepts as `ProviderEndpointCandidate` and `ConsumerDispatchCandidate` Core records during one resolution operation. Only a proved Edge, its typed evidence, or a bounded Uncertainty is durable. The reusable `communication_endpoints`, `dispatch_sites`, and candidate tables remain backlog; the plan does not claim them complete.
4. Story 11.1 currently permits Edge creation only as part of a new `KnowledgeCommit`, whose source is the new Document Version. Story 11.4 resolves two already-committed Pass 1 Document Versions, so it adds a specific `IExactCrossRepositoryResolutionStore` rather than weakening or overloading `IKnowledgeStoreRepository`.
5. Story 11.2 exposes durable `Pass1Completed`; Story 11.3 separately runs prepared Pass 2. This story adds a two-Repository coordinator invoked with two explicit prepared executions after both are `Pass1Completed`. It neither changes runner scheduling nor automatically enumerates Systems/Repositories.
6. `GitWorkingCopyService` currently owns one stable default-`HEAD` checkout per Repository and later uses `pull --ff-only`. Exact resolution therefore uses a separate operation-specific Git/snapshot service and never changes the ordinary checkout. Resolution also rejects any durable ScanRun, ScanExecution, Document Version, or Source Anchor that differs from the exact commit.
7. Read-only inspection of both tagged objects resolved the planning occurrence: account `UpdateChannelSessionOp.java:45` calls `netTools.execute` with literal `cust/updateSession` at argument 1; channel `disgram/in/cust/updateSession.xml:2` declares `trans@name=in/cust/updateSession`; channel `UpdateSessionOp.java` is the exact provider implementation Document selector. The reviewed profile records these facts and a fixed provider-`in/` rule; no endpoint was invented.
8. No `docs/adr/`, context-scoped ADR directory, or `CONTEXT-MAP.md` exists at the pinned boundary. No ADR conflict was found or invented.
9. The PRD and Ubiquitous Language reserve `wontfix` for a human-accepted terminal Dangling Link. The automatically observed file/storage limitation therefore remains nonterminal as `status=unresolved`, `sub_status=unknown_target`, `reason_code=unsupported_kind`, and `observation_kind=file_storage`, so later full-V1 file/storage resolution can re-evaluate, resolve, or reclassify it without reopening a human-terminal decision.

## Constitution Check

### Before design

| Gate | Result | Evidence |
| --- | --- | --- |
| I. Layered Project Boundaries | PASS | Core owns provider-independent profile/candidate/result/store contracts; Infrastructure alone owns Git/XML/source readers, EF mappings, transaction, migration, and materialization; Web owns authorization-aware runtime composition. |
| II. Domain Purity & Ubiquitous Language | PASS | No EF dependency/data annotation/generic repository/`IQueryable` escape is planned. Canonical terms are used; automatic deferred observations remain `unresolved`, `wontfix` remains human-terminal, and Uncertainty `sub_status` is closed to `cross_repo`, `unknown_target`, and `ambiguous`. |
| III. Validation & API Contract Discipline | PASS | No public API is added. Closed loaders and Core validators reject unknown/duplicate/unsafe/over-bound profile and runtime values. A later public surface must use FluentValidation and ProblemDetails. |
| IV. Test-First Integration Harness | PASS | Test groups begin with transient RED and cover profile, Git object identity, extraction, matching, authorization, atomic persistence/idempotency, retrieval redaction, migration, and current-boundary integration. |
| V. Security & Data Stewardship | PASS | Both Repositories/Systems are authorized before Git or data access; internal relational identities remain inside trusted request/database boundaries; profile/text/log/diagnostic surfaces exclude them; unauthorized reads return only the restricted marker; evidence is bounded and relative; nothing is sent to an LLM/external service. |
| Deadline exception | PASS | Plan review, planned-change authorization, migration/security checks, combined quality review, and current-boundary integration remain. Story 11.5 retains final clean-DB two-Repository E2E and Docker. |

### After design

PASS. Phase 1 preserves all gates: the reviewed profile excludes deployment and persistence identities; exact Git acquisition is isolated from ordinary `AcquireAsync`; Profiled Revision equality fails closed; source inspection is bounded to authorized committed snapshots; current Document Versions and Source Anchors are revalidated under deterministic owner locks; canonical Edge/evidence/Uncertainty writes are transactional and replay-safe; required internal PK/FK identities never become textual evidence or unauthorized output; unauthorized retrieval returns only the metadata-free restricted marker; XML/lexical parsing is local deterministic code; immutable Document and knowledge-store invariants remain unchanged. Principle II passes because the deferred automatic observation is canonical nonterminal `unresolved/unknown_target` and no noncanonical Uncertainty sub-status remains. No Constitution or ADR exception is required, so Complexity Tracking is empty.

## Design

### 1. Closed demo profile and runtime binding

Infrastructure embeds one fixed-name JSON resource under `CrossRepository/Profiles/`. `ExactResolutionProfileCatalog` loads it with duplicate-property detection, unknown-member rejection, canonical ordering, finite counts, closed enums, and a SHA-256 content fingerprint. The complete reviewed planning resource is [demo-resolution-profile.v1.json](contracts/demo-resolution-profile.v1.json), governed by [demo-resolution-profile.md](contracts/demo-resolution-profile.md).

Each Repository entry contains only an alias/stable label and expected full tag ref/commit. The exact occurrence pins account consumer file/line, simple-member receiver/member/form, endpoint argument index, channel XML file/root/attribute/line, provider implementation path/type, `custom-rpc-in-prefix-v1`, and ordinal comparison. The unsupported observation pins its identity, closed `file-storage` kind, account owner Document selector, member-invocation selector, and safe locator. Deployment Git URLs remain solely on managed Repository records.

`ExactResolutionRuntimeRequest` supplies SystemId, profile identity/version/fingerprint, and exactly one alias binding per profile Repository. Each binding contains the managed RepositoryId, ScanRunId, and ScanExecutionId selected by orchestration. Web builds it only after `ManagementAccess` and `IUserContext.ToSystemOwnerFilter()` prove the System and both Repository records visible. Duplicate/missing/extra aliases, reuse of one Repository for two aliases, cross-System membership, wrong ScanRun ownership, insufficient scope, or profile provenance mismatch returns one indistinguishable bounded refusal before Git/source/persistence.

Runtime GUIDs are normal internal database identities, not configuration constants. The store persists the relational PK/FK columns required to prove ownership, enforce referential integrity, derive stable replay identity, and perform authorized joins. Those identities are never serialized into the embedded profile, textual evidence/diagnostic/log/exception fields, or production branches. Authorized exact-relationship DTOs expose only the stable Edge identity as an opaque `relationshipIdentity` plus safe metadata; unauthorized retrieval exposes only `RestrictedRelationship` and no identity or metadata.

### 2. Exact Git object acquisition and Profiled Revision gate

Add a separate exact-object port; do not change `IGitWorkingCopyService.AcquireAsync` or its stable checkout:

```text
IExactGitRevisionSnapshotService.AcquireAsync(
  repository, scanRunId, expectedRef, expectedCommit)
  -> ICommittedRepositorySnapshotLease(profiledRevision == expectedCommit)
     | bounded failure
```

It creates a unique non-reparse private Git root beneath Vulgata-owned `.exact-revisions`, obtains the Git URL only from the authorized managed record, fetches only the explicit ref with prompts disabled, verifies `rev-parse expectedRef^{commit}` equals the configured commit, and archives/materializes only that private object. It never runs a Git/file mutation in the ordinary per-Repository managed working-copy directory, so attached-branch `AcquireAsync` can run before, during, and after exact acquisition.

The lease reuses the existing bounded archive, traversal, reparse, and identity controls and owns both the private Git root and snapshot. Success disposal, pre-return failure, cancellation, concurrent exact leases, and cleanup failure have one operation-local cleanup contract; cancellation terminates/awaits the process tree before bounded cleanup, and one lease cannot delete another. Tests prove exact-then-normal and concurrent exact/normal acquisition, including cancellation, leave the ordinary path/branch/HEAD/origin/index/status intact. Before extraction, equality is required across configured commit, isolated-object/snapshot Profiled Revision, ScanRun, ScanExecution, each selected Document Version, and Source Anchors.

### 3. Deterministic candidate extraction and direction

The provider extractor enumerates only profile-listed files that resolve beneath `src/main/resources/disgram/in/`. XML parsing prohibits DTD/external resolution, is bounded by file/aggregate/candidate limits, and emits a `ProviderEndpointCandidate` only for a configured `trans` element with one non-empty `name` attribute. It records decoded raw identifier plus a Repository-relative element locator; malformed, duplicate attributes, missing name, out-of-root, symlink/reparse, over-bound, and excluded files become closed outcome codes, not candidates.

The consumer extractor reads only the reviewed account file and requires line 45 ordinal 0 to be the simple member invocation `netTools.execute`; it accepts only one direct ordinary string literal at zero-based argument index 1. The provider extractor requires channel `cust/updateSession.xml` line 2 ordinal 0 to be root `trans@name`. Variables, concatenation/templates, computed expressions, malformed escapes/XML, ambiguous forms, and any unconfigured file/symbol/line/ordinal are non-exact.

Each candidate maps to exactly one current Pass 1 Code Logic Document Version. Consumer mapping requires the invocation line inside exactly one Source Anchor for the same Repository/ScanRun/Profiled Revision. Provider mapping requires the exact `UpdateSessionOp.java` path and `cn.com.yitong.actions.domain.login.UpdateSessionOp` type to select exactly one current Source Anchor/Code Unit. XML declaration locator and provider implementation Source Anchor remain separate evidence. Zero/multiple mappings produce no Edge.

The account consumer and channel provider aliases are part of candidate identity and never pooled across the reverse direction. `custom-rpc-in-prefix-v1` normalizes observed consumer `cust/updateSession` and provider `in/cust/updateSession` to `cust/updateSession` by requiring/removing exactly one provider `in/`; equality is ordinal. Exactly one eligible provider is an Exact Match. Roles remain occurrence-local; reverse-direction support is proven with separate synthetic contract fixtures and never reclassifies a Repository.

### 4. Post-Pass-1 integration without a scheduler

Add `ExactCrossRepositoryResolutionCoordinator` as a deterministic application service. Its single method accepts one profile request and two explicit bindings. It requires both ScanExecutions at durable `Pass1Completed` (a later `Completed` execution is accepted only when its same Pass 1 results are still current), verifies their Pass 1 Code Logic Document sets, and calls the resolver once. If either is not ready it returns `WaitingForPass1` without Git/source/write.

This coordinator is invoked explicitly by the prepared deadline orchestration after each Repository reaches Pass 1; the first call can wait and the second can resolve. It does not list a System, queue scans, select counterparts, add concurrency/retry/deadlock behavior, run Pass 2, or change `SequentialScanRunner` phases. Story 11.5 composes the final sequence and owns the clean-database two-Repository rehearsal.

Pass 1 narrative, Worker Agent evidence, LLM output, and mutable System context are never inputs to endpoint equality or selector choice. The reviewed JSON profile is the sole selector authority for this story; implementation may only validate its pinned selectors against the exact source objects and must fail closed on drift. Existing queued context semantics remain untouched.

### 5. Atomic Edge evidence, Uncertainty, and retrieval

Add a specific `IExactCrossRepositoryResolutionStore`. It accepts the bounded profile-ordered decision batch, materializes only already-authorized immutable Pass 1 data, locks every distinct Logical Document owner row in database UUID order, revalidates current pointers/ownership/Source Anchors/authorization, validates the complete batch before attachment, and commits the exact result plus unsupported limitation record in one transaction.

An Exact Match inserts the existing `DocumentEdge` as `References` and a required one-to-one `ExactCrossRepositoryEdgeEvidence` row. Its internal relational columns store both Source Anchor IDs and the owner Document Version/Repository/ScanRun tuples needed by composite FKs. Its bounded textual columns store both raw identifiers, normalized identifier, protocol, profile schema/identity/version/fingerprint, occurrence identity, `ExactConfigured` resolution kind, confidence `1.0000`, two Repository-relative evidence locators, and an evidence fingerprint; no internal GUID is copied into those text fields. Existing Edge uniqueness remains; cross-Repository status is still derived by comparing Repository IDs.

Evidence-based non-exact/ambiguous results insert an `Uncertainty` owned by the consumer Document Version. Stored values are lowercase; `sub_status` is only `cross_repo`, `unknown_target`, or `ambiguous`, while distinctions use closed `reason_code`. The reviewed file/storage observation must map to one authorized current account owner Document Version/Source Anchor and writes the nonterminal combination `status=unresolved`, `sub_status=unknown_target`, `reason_code=unsupported_kind`, `observation_kind=file_storage`, plus its safe locator/fingerprint. It remains eligible for later full-V1 file/storage resolution/reclassification; this story never assigns the human-terminal `wontfix` state. Identical replay returns `AlreadyRecorded`; drift conflicts; every unsupported path creates zero Edges. Binding/authorization/revision/owner-mapping failures inspect/persist nothing.

Stable Edge and Uncertainty IDs/fingerprints are SHA-256-derived inside the trusted boundary from the profile fingerprint, occurrence/observation identity, direction, revisions, exact owner Document Version/Source Anchor identities, safe identifier/locator, and outcome code. These internal identities are canonical hash inputs and relational values, never evidence text. Identical batch replay returns only `Already*` item results after byte-for-byte durable verification. Same key with drift returns aggregate `Conflict`; unique/FK/check violations roll back the whole batch. No partial Edge/evidence/unsupported record or duplicate diagnostic survives.

Extend the current authorized relationship projection with optional exact evidence only when both endpoint Repositories are in the current scope. Its DTO serializes the stable Edge identity only as an opaque `relationshipIdentity`, then safe endpoint/profile/locator fields; it does not serialize Repository, ScanRun, Document Version, or Source Anchor PK/FKs. Existing `RestrictedRelationship` remains the sole unauthorized marker and stays identity- and metadata-free. A dedicated resolver outcome follows the same two-endpoint rule. See [resolution-persistence-and-retrieval.md](contracts/resolution-persistence-and-retrieval.md).

### 6. Additive EF migration

The migration adds `ExactCrossRepositoryEdgeEvidence` and `Uncertainties`, their configurations, DbSets, closed check constraints, owner-safe foreign keys, and deterministic unique/index paths. It does not alter Document Version immutability, Source Anchor ownership, existing Edge type/uniqueness, or delete behavior. Edge evidence uses EdgeId as PK/FK and composite anchor foreign keys that prove the consumer anchor belongs to the Edge source Document Version/Repository/ScanRun and the provider anchor to the target. This requires an additive alternate key/index on `SourceAnchors (Id, DocumentVersionId, RepositoryId, ScanRunId)`; existing rows already satisfy it.

Rollback target is `20260801033655_AddSequentialScanRunner`. Apply/rollback/reapply must prove the two new tables and additive anchor key disappear/reappear while all Story 11.1/11.2 data remains. `dotnet ef migrations has-pending-model-changes` must exit 0. All migration/model-snapshot changes are integration-owner coordinated.

## Project Structure and Ownership

```text
specs/007-exact-cross-repo-resolution/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── demo-resolution-profile.md
    ├── demo-resolution-profile.v1.json
    ├── exact-git-revision-acquisition.md
    ├── exact-resolution-service.md
    └── resolution-persistence-and-retrieval.md

src/dotnet/
├── Vulgata.Core/
│   ├── Entities/
│   │   ├── ExactCrossRepositoryEdgeEvidence.cs
│   │   └── Uncertainty.cs
│   └── DomainServices/
│       ├── ExactResolutionContracts.cs
│       ├── IExactCrossRepositoryResolver.cs
│       └── IExactCrossRepositoryResolutionStore.cs
├── Vulgata.Infrastructure/
│   ├── CrossRepository/
│   │   ├── Profiles/                         # one embedded closed JSON profile
│   │   ├── ExactResolutionProfileCatalog.cs
│   │   ├── ExactCandidateExtractors.cs
│   │   └── ExactCrossRepositoryResolver.cs
│   ├── Git/ExactGitRevisionSnapshotService.cs # isolated exact object + lease cleanup
│   ├── Git/GitWorkingCopyService.cs           # ordinary acquisition unchanged
│   └── Data/
│       ├── ExactCrossRepositoryResolutionStore.cs
│       ├── VulgataDbContext.cs               # integration-owner coordinated
│       ├── Configurations/
│       └── Migrations/                        # integration-owner coordinated
├── Vulgata.Web/
│   ├── Data/ExactCrossRepositoryResolutionCoordinator.cs
│   └── Program.cs                             # composition only, coordinated
└── Vulgata.Shared/                            # no change unless a later public surface is authorized

tests/Vulgata.Tests/
├── ExactResolutionProfileTests.cs
├── ExactGitRevisionAcquisitionTests.cs
├── ExactResolutionExtractionTests.cs
├── ExactCrossRepositoryResolverTests.cs
├── ExactCrossRepositoryPersistenceTests.cs
├── ExactCrossRepositoryAuthorizationTests.cs
├── ExactCrossRepositoryMigrationTests.cs
└── ExactCrossRepositoryIntegrationTests.cs
```

**Ownership decision**: Core owns closed provider-independent domain contracts/entities. Infrastructure owns profile/source/Git implementations and all EF work. Web proves user/System/Repository visibility and creates runtime scope/request; it does not parse or match. Agents and Worker Agent prompts/results are unchanged. External `mbs_*` Repositories are read only through Git objects and never checked out, staged, or mutated by planning/tests against production locations.

## Test-First Implementation Groups (planning input, not tasks)

| Group | First RED observation | GREEN design predicate |
| --- | --- | --- |
| G1 profile/binding | Unknown/duplicate fields, aliases, rules, paths, versions, observations, or missing bindings can pass | Reviewed JSON profile plus invocation binding rejects every malformed/duplicate/unsafe/cross-System case before Git and contains no environment path/ID/credential |
| G2 exact Git | Shared-checkout detachment can break later normal acquisition | Isolated exact-object snapshot returns only the full commit; exact-then-normal, concurrent normal/exact, failure, cancellation, and cleanup tests prove ordinary `AcquireAsync` path/branch/HEAD/origin/index/status stay usable |
| G3 extraction | The pinned occurrence could be outside the planned syntax | Exact pinned call/XML/implementation selectors fit closed lexical/XML scanners; variable/malformed/wrong-root/over-bound/unconfigured cases fail |
| G4 match/direction | Provider `in/` namespace or direction can select the wrong target | Closed provider-prefix normalizer + ordinal comparison + occurrence direction yields the observed unique match or no Edge; reverse-direction fixtures remain occurrence-local |
| G5 persistence/idempotency | Existing store cannot attach an Edge or record the unsupported observation | Specific atomic batch store commits Edge+evidence plus canonical Uncertainty together; actual file/storage observation is owner-anchored, retry-safe, and always zero-Edge |
| G6 authorization/isolation | Internal identities or endpoint metadata can escape before both endpoints are authorized | Web/Infrastructure gates precede Git/query/write; relational PK/FKs remain internal; authorized DTO uses opaque relationship identity plus safe metadata; restricted retrieval is marker-only; profile/text/log/diagnostic scans contain no forbidden serialization |
| G7 migration | New typed evidence/Uncertainty schema is absent | SQLite model checks and PostgreSQL apply/rollback/reapply/no-pending checks prove additive keys/FKs/checks/indexes |
| G8 current-boundary integration | No two-Repository post-Pass-1 observable path exists | Two authorized prepared Pass 1 fixtures at pinned commits resolve exactly one Edge; negative matrix creates zero Edges; Story 11.5 gates remain untouched |

## Requirement → Design → Test Trace

| Requirements | Design | Proof |
| --- | --- | --- |
| FR-001, FR-019 | explicit two-execution coordinator; no context input | G1/G8 durable Pass1Completed and unchanged queued-context tests |
| FR-002–FR-006 | reviewed profile/runtime split and isolated exact Git/Profiled Revision gate | G1/G2 missing/duplicate/auth/ref/commit/revision plus exact-then-normal/concurrent-cleanup matrix |
| FR-007–FR-010, FR-016–FR-018 | pinned call/XML/implementation selectors, provider-`in/` normalizer, occurrence direction | G3/G4 pinned syntax, both-direction contract, same-name, variable, malformed, wrong-root, normalization fixtures |
| FR-011–FR-015 | exact match, atomic store, typed evidence/Uncertainty, stable replay | G5/G7 Edge/evidence completeness, currentness race, rollback, replay |
| FR-020–FR-021 | Web visibility + RepositoryAccessScope + restricted projection | G6 unauthorized-before-read/write and source-isolation scans |
| FR-022–FR-024 | owner-anchored reviewed unsupported observation; observed endpoint only; Story 11.5 boundary | G1/G5/G8 durable nonterminal `unresolved`/`unknown_target` + `unsupported_kind`/`file_storage`, future-resolution eligibility, replay, zero-Edge, and boundary assertions |
| SC-001–SC-005 | unique exact resolution and idempotency | G4/G5/G8 |
| SC-006–SC-008 | internal-relational versus serialized-identifier boundary, unsupported behavior, authorized trace | G6 verifies required PK/FKs, zero internal IDs in profile/text/log/diagnostics/unauthorized output, opaque authorized relationship identity, and marker-only unauthorized results; G8 proves nonterminal unsupported replay and authorized trace |
| Story AC-1 | exact prepared Repository/ref/full-commit binding and fail-before-read/write | G1/G2 binding, authorization, isolated Git, and Profiled Revision matrix |
| Story AC-2 | one exact custom-RPC Edge with complete evidence | G3/G4/G5/G8 reviewed selectors, unique match, atomic Edge/evidence, and authorized trace |
| Story AC-3 | zero Edge for incomplete/ambiguous evidence with safe diagnosis | G3/G4/G5 negative matrix and bounded Uncertainty assertions |
| Story AC-4 | occurrence-local roles and either configured direction | G4 reviewed direction plus reverse synthetic contract fixture |
| Story AC-5 | narrative/LLM/System context advisory only; queued context unchanged | G4/G8 no-context-truth and predecessor queued-context regression |
| Story AC-6 | both-endpoint authorization and source isolation | G6 fail-before-access/write, SC-006 serialization split, opaque authorized identity, and marker-only unauthorized retrieval |
| Story AC-7 | unsupported/deferred communication remains nonterminal and creates no relationship | G1/G5/G8 reviewed file/storage observation, `unresolved/unknown_target`, future-resolution eligibility, and zero Edge/evidence rows |

## E2E Decision

Story-level E2E is required because this story changes Git object acquisition, authorization, persistence, and Cross-Repository matching. It is a current-boundary integration using disposable Git fixtures and relational storage, not the final prepared production Repository rehearsal.

The integrated fixture creates two managed Repositories in one authorized System, two exact tag/full-commit Git fixtures, two durable Pass1Completed executions, current Code Logic Document Versions/Source Anchors, the pinned call/XML/implementation shape, and the pinned unsupported-observation shape. It proves exactly one Edge/evidence row, one canonical nonterminal `unresolved/unknown_target` unsupported Uncertainty and zero unsupported Edges, authorized retrieval through an opaque relationship identity, marker-only unauthorized retrieval, reverse-direction contract support, exact replay, and the full negative matrix. It also runs exact acquisition followed by/concurrent with normal `AcquireAsync`. Production `mbs_account`/`mbs_channel` locations are never mutated. Story 11.5 retains clean PostgreSQL setup across the actual prepared Repositories, complete pipeline/chat citations, and final Docker image verification.

## Planned-Change Authorization (mandatory pause after accepted plan review)

Independent plan review is mandatory and the orchestrator must pause before `speckit-tasks` for these exact triggers:

1. **Scan strategy — exact Profiled Revision acquisition.** The plan adds an isolated exact-ref/full-commit snapshot service instead of default `HEAD`. Effect: prepared resolution refuses moved/missing tags or revision drift without detaching/changing the ordinary managed checkout; exact/normal concurrency and cleanup are explicit. Intent: keep `IGitWorkingCopyService.AcquireAsync` unchanged and use operation-private exact Git roots. Plan citations: Design §§1–2 and [exact-git-revision-acquisition.md](contracts/exact-git-revision-acquisition.md). PRD citations: §5.4 FR-4.1/FR-4.2, §5.16 exact prepared slice; architecture scanning plan §§5 and 17; spec FR-002–FR-006.
2. **Scan strategy — post-Pass-1 two-Repository integration.** The plan adds one explicit coordinator after two durable Pass1Completed results, without a scheduler. Effect: the first prepared completion waits; the second can run exact resolution before prepared Pass 2. Intent: preserve `SequentialScanRunner` phases and require caller-supplied bindings/scope. Plan citations: Design §4 and [exact-resolution-service.md](contracts/exact-resolution-service.md). PRD citations: §5.4 FR-4.2/FR-4.4, §5.6 FR-6.2, §5.16 sequential/exact slice and concurrency deferral; spec FR-001/FR-019/FR-024.
3. **Cross-Repository matching behavior.** The plan makes the reviewed `netTools.execute` literal, `trans@name`, exact implementation selector, configured direction, closed provider-`in/` normalizer, ordinal equality, and unique-provider cardinality the only Edge truth; it also turns one pinned file/storage observation into a canonical nonterminal zero-Edge limitation record. Effect: the observed exact evidence creates one Edge; narrative/ambiguous/general cases cannot, and unsupported replay is durable/idempotent while remaining eligible for later full-V1 resolution. Intent: implement generic profile-driven extractors/resolver, not Repository-specific code. Plan citations: Design §§1, 3, 5 and profile/service contracts. PRD citations: §5.5 FR-5.1/FR-5.3/FR-5.14, §5.6 FR-6.1/FR-6.2/FR-6.4, §5.16 exact configured matching and deferrals; architecture §11; spec FR-007–FR-018/FR-022–FR-023.
4. **Authorization/security boundary.** The plan requires both Systems/Repositories visible before Git/source/query/write and exposes exact evidence only when both endpoints remain authorized. Effect: unauthorized calls are indistinguishable and persist/disclose nothing. Intent: reuse `ManagementAccess`, `IUserContext`, and `RepositoryAccessScope`; add no weaker policy. Plan citations: Design §§1, 5 and persistence/retrieval contract. PRD citations: §5.1 FR-1.4, §5.11 FR-11.4/FR-11.6, NFR-2.3; architecture §14; spec FR-020–FR-021.
5. **Migration invariant.** The plan adds typed one-to-one Edge evidence, Uncertainty, and an additive Source Anchor alternate key. Effect: migrations/model snapshot change while all existing immutable/history/Edge invariants remain. Intent: integration-owner-coordinated additive migration with rollback/reapply/no-pending proof. Plan citations: Design §§5–6 and [data-model.md](data-model.md). PRD citations: §5.16 green migration requirement and §5.6 FR-6.1/FR-6.2; Constitution I/II/IV; spec FR-011–FR-015.

No Worker Agent/Chat Agent tool is added or changed. The plan does not change or weaken a PRD requirement; it implements the already-approved §5.16 overlay while preserving deferred full-V1 requirements as backlog. Authorization does not waive later TDD, combined quality review, security, migration, integration, or verification gates.

## Risks and Fail-Closed Responses

| Risk | Response |
| --- | --- |
| Pinned evidence later drifts from the reviewed artifact | Full-commit and exact selector checks fail closed; the observed reviewed objects fit the grammar and no implementation-time endpoint selection remains |
| Exact acquisition strands ordinary profiling | Exact Git/object/snapshot roots are disjoint from the stable managed checkout; exact-then-normal and concurrent cancellation/cleanup regressions are mandatory |
| XML declaration has no unambiguous provider Document | Exact configured implementation Document selector must map to one current Source Anchor/Code Unit; otherwise Uncertainty and no Edge |
| Tag moves or object is unavailable | Fetch/peel/full-commit mismatch fails before snapshot/source/write |
| Current Document changes during resolution | Deterministic owner locks and current-pointer/revision revalidation roll back |
| Endpoint metadata is sensitive | Both-endpoint authorization on every read; restricted relationship contains no metadata; no values in logs/errors |
| Concurrent/retried resolution duplicates truth | Stable IDs/fingerprints, unique constraints, exact replay verification, and one transaction |
| Full architecture appears partially implemented | Plan explicitly treats endpoint/dispatch candidates as transient and does not claim persistent canonical catalogs, general resolution, or advanced policy |
| Migration touches shared snapshot | Integration owner coordinates migration, DbContext, Program composition, and snapshot changes; no parallel unowned edit |

## Complexity Tracking

No Constitution violations.
