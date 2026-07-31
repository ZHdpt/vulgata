# Implementation Plan: Minimal Knowledge Store and Retrieval Projection

**Branch**: `codex/story-11-1-knowledge-store` | **Date**: 2026-07-30 |
**Spec**: [spec.md](spec.md) | **Execution level**: `expedited-mvp`

## Summary

Add a specific Core knowledge-store contract and an Infrastructure-owned,
PostgreSQL-backed aggregate for Logical Documents, immutable Document Versions,
source anchors, exact `REFERENCES` and `GENERATED_FROM` Edges, the deadline-only
flow-group/trace/step evidence, and database-backed `IndexEntry` and
`DocumentSection` projections. Commits and projection replacement run as
retry-safe EF transactions with deterministic owner-row locking. Current
retrieval accepts an explicit authorized
Repository scope, returns only bounded materialized Core read models, and
fully redacts unauthorized Edge and flow-step targets. Retry verification binds
the stable `OperationId` to the complete canonical aggregate and its derived
projections with a deterministic `CommitFingerprint`; current retrieval clears
all potentially target-derived text when any referenced target is no longer
authorized.

This story adds no public API, UI, Agent tool, Scan behavior, generator,
Cross-Repository matcher, Story 4.3 change, or deferred Story 5.3 binding
family.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: existing EF Core 10.0.9, Npgsql EF provider 10.0.3,
ASP.NET Core DI, and `System.Text.Json`; no new package

**Storage**: PostgreSQL 17, schema `vulgata`, through `VulgataDbContext`

**Testing**: xUnit in `tests/Vulgata.Tests`; SQLite relational integration for
fast invariant/atomicity tests plus PostgreSQL migration commands

**Target Platform**: Vulgata single-container Linux deployment and Windows
development

**Project Type**: existing Blazor Web App with Core, Infrastructure, Shared,
Web, and Tests boundaries

**Performance Goals**: one bounded aggregate commit or projection rebuild per
transaction; one Repository-filtered current retrieval with no lazy loading or
unbounded expansion

**Constraints**:

- 1 MiB canonical structured JSON and 1 MiB deterministic Markdown per commit;
- 256 sections, 64 KiB Markdown per section, 512 source anchors, 1,024 Edges,
  32 flow groups, 64 traces per group, and 256 ordered steps per trace;
- title, heading, summary, symbol, business term, technical term, and evidence
  locator are each at most 4 KiB UTF-8; each symbol/term collection has at most
  256 items;
- Repository-relative paths are at most 2,048 UTF-8 bytes; schema/stable keys
  are closed-format and at most 128/4,096 UTF-8 bytes respectively;
- derived index search text is at most 4 MiB UTF-8; section search text and
  serialized reference lists are each at most 128 KiB UTF-8;
- retrieval accepts at most 100 explicit section IDs and returns no more than
  the accepted anchor/Edge/flow bounds; over-limit input is rejected;
- authorized Repository scope is a deduplicated finite set of at most 256
  non-empty IDs and must contain the source Repository;
- canonical JSON has only the exact allowlisted common and section fields in
  [canonical-document-content.md](contracts/canonical-document-content.md);
  unknown/duplicate fields, raw-source carriers, closed credential markers,
  closed host-path markers, closed raw-diagnostic markers, forbidden controls,
  and over-bound values are rejected deterministically;
- the same closed sensitive-text guard applies to source-anchor evidence
  locators, Flow Group titles, and Flow Trace Step descriptions before any
  entity is attached;
- no failing test is committed.

**Scale/Scope**: prepared two-Repository demonstration data; one Logical
Document commit/rebuild/retrieval operation at a time; historical versions are
retained without a deletion path

## Authority Reconciliation

1. PRD section 5.16 and Epic 11 are the accepted deadline overlay. Full Epic
   5 storage remains backlog and is not claimed complete.
2. The July data contracts remain the architectural direction, but Story 11.1
   implements only Logical Document/Document Version storage, anchors,
   `REFERENCES`/`GENERATED_FROM`, flow groups/traces/steps, `index_entries`, and
   `document_sections`.
3. Candidate selections, dispatch bindings, endpoint bindings,
   communication endpoints, Uncertainty, reverse index, stale notices,
   virtual tree/export, retrieval tools, and `index.md` parsing are excluded.
4. Story 4.3 is immutable at commit
   `b851ba165d6ebe560227bfdc3b206a814280e9b2`; its migration
   `20260730083207_AddConservativePreScanGate` is the rollback baseline.
5. The completed `PreScanMigrationTests` contains one latest-migration call at
   `tests/Vulgata.Tests/MigrationTests.cs:142`. Once Story 11.1 adds a later
   migration, that test must target
   `20260730083207_AddConservativePreScanGate` explicitly or its Story 4.3-only
   table assertion becomes unsound. Because that is a protected Story 4.3
   file, only the integration owner may make the one-line change after a
   narrow explicit user exception. Story 11.1 implementers must not edit it,
   and task generation is blocked until the exception is authorized.
6. No `docs/adr/`, context-scoped ADR directory, or `CONTEXT-MAP.md` exists at
   this boundary. No ADR/context decision is invented; the accepted spec,
   Constitution, glossary, PRD, and July architecture are sufficient.

## Constitution Check

### Before design

| Gate | Result | Evidence |
| --- | --- | --- |
| I. Layered boundaries | PASS | Core owns entities/contracts; Infrastructure alone owns EF, transactions, mappings, migration, and materialization; Web only registers the specific repository. |
| II. Domain purity/language | PASS | No annotations, generic repository, `IQueryable`, or `DbSet` escape; exact Logical Document, Document Version, Edge, Version Chain, and Cross-Repository terms are used. |
| III. Validation/contracts | PASS | No public/API DTO is introduced. The internal typed contract is a closed allowlist and validates UTF-8/count/path plus explicit sensitive-content rejection rules for canonical content and evidence-bearing text before persistence; a later public input must add FluentValidation outside this story. |
| IV. Test-first integration | PASS, dependency noted | Six transient RED→GREEN groups cover domain, commit, PostgreSQL row-lock races, rebuild, authorization/redaction, and migration. The protected Story 4.3 migration-test compatibility edit is integration-owner work requiring user exception before tasks. |
| V. Security/stewardship | PASS | Repository scope is outermost; hidden source results are indistinguishable from missing; unauthorized targets disclose only a restricted marker and cause potentially target-derived text projections to be cleared; the closed schema has no source/secret/diagnostic field and deterministic lexical guards reject the accepted prohibited fixtures across canonical and evidence-bearing text. |
| Immutable traceable truth | PASS | Canonical content/evidence are insert-only; only predecessor `SupersededById` and Logical Document current pointer transition. |
| Deadline exception | PASS | Migration, authorization, security, test, review, E2E-decision, and verification gates remain; deferred Epic 5 families are absent. |

### After design

PASS. Phase 1 keeps all EF work in Infrastructure, adds one specific repository,
uses owner-safe composite foreign keys, deterministic PostgreSQL row locks, an
aggregate `CommitFingerprint`, and optimistic concurrency for exactly one
current Document Version, makes every write/rebuild atomic, fails closed on
restricted target-derived text, and keeps the public/API, Agent-tool, Scan, and
deferred binding boundaries unchanged. The `IndexEntry.DocumentType` check
closes the projection discriminator independently of its owning Logical
Document. Implementation remains blocked before task
generation on the separately authorized, integration-owner-only Story 4.3
test compatibility line. The Complexity Tracking table is unnecessary.

## Design

### 1. Stable identity and immutable Version Chain

`LogicalDocument` owns Repository identity, closed `DocumentType`, a bounded
stable semantic key, and nullable `CurrentVersionId`. It has alternate key
`(Id, RepositoryId)`. `DocumentVersion` owns
all generated content and generation-time Repository/System/Scan identity.
It has alternate keys `(Id, LogicalDocumentId, RepositoryId)` and
`(Id, RepositoryId, ScanRunId)`.
Initial creation inserts the Logical Document with no visible current pointer,
inserts its complete version aggregate, then sets the pointer inside the same
transaction.

The current pointer FK is
`LogicalDocument(CurrentVersionId, Id, RepositoryId)` →
`DocumentVersion(Id, LogicalDocumentId, RepositoryId)`. The successor FK is
`DocumentVersion(SupersededById, LogicalDocumentId, RepositoryId)` → the same
alternate key. The database therefore cannot point current/successor to a
version owned by another Logical Document or Repository.

Supersession requires `ExpectedCurrentVersionId`. The repository loads that
exact current version, inserts version N+1, sets only the predecessor's
`SupersededById`, and moves the Logical Document pointer. Both
`CurrentVersionId` and predecessor `SupersededById` are EF concurrency tokens.
A competing writer receives the closed `Conflict` result; its transaction is
rolled back, so no losing version/evidence/projection row survives. Callers
retry only after re-reading the observed current version.

### 2. Canonical content and finite materialization

The internal `CanonicalDocumentContent` contract is the only accepted content
shape. It contains exactly the allowlisted metadata fields and ordered typed
sections; there is no arbitrary payload or extension object. Infrastructure
canonicalizes it with
ordinal property/list ordering, serializes it to PostgreSQL `jsonb`, and
renders deterministic Markdown from title plus ordered section headings and
Markdown. The complete UTF-8 JSON/Markdown is measured before entities are
attached.

Every canonical string is Form-C normalized, rejects forbidden controls, and
is checked against the closed credential, host-path, raw-source-carrier, and
raw-diagnostic rules in the contract. The same Core-owned guard is applied to
source-anchor evidence locators, Flow Group titles, and Flow Trace Step
descriptions before persistence. Unknown or duplicate JSON properties and
document-type, section-kind, or schema values are rejected. Unsafe
noncanonical evidence text returns only closed `InvalidContent`; the rejected
value and provider diagnostics do not cross the contract. This is the testable
FR-019 boundary; the plan does not claim semantic detection of every possible
secret or source fragment.

`KnowledgeDocumentMaterializer` derives exactly one `IndexEntry` and the full
ordered `DocumentSection` set from this canonical content. Rebuild reads only
the immutable Document Version JSON, prepares and validates the replacement
in memory, then deletes/reinserts projection rows for that version in one
transaction. It never reads `index.md`; rollback preserves the old complete
projection.

### 3. Exact ownership and provenance

Source anchors carry Document Version, Repository, ScanRun, Profiled Revision,
Repository-relative path, Code Unit, inclusive lines, and SHA-256. Composite
foreign keys bind
`(DocumentVersionId, RepositoryId, ScanRunId)` to the version alternate key,
`(CodeUnitId, ScanRunId, RepositoryId)` to the Code Unit alternate key, and
`(ScanRunId, RepositoryId)` to the ScanRun alternate key. Commit validation
also requires path/range and
Profiled Revision agreement with the durable Code Unit/ScanRun.

Edges persist exact source/target Document Version, Repository, and ScanRun
IDs. Each endpoint FK uses `(DocumentVersionId, RepositoryId, ScanRunId)`, so
the database rejects cross-owner endpoint tuples.
`GENERATED_FROM` permits only Business Logic source → Code Logic target;
`REFERENCES` uses the exact supplied current endpoints. Both endpoints must
exist, be current after locking inside the committing transaction, and be in
the authorized Repository scope. Cross-Repository status is derived by
comparing endpoint Repository IDs; there is no stored type/flag.

Flow evidence is only `FlowGroup` → ordered `FlowTrace` → ordered
`FlowTraceStep`. Every group belongs to the committed Business Logic Document
Version. Orders are 1-based, unique, and contiguous. The first step has no
Edge; a `Relationship` step requires an Edge from the immediately preceding
step's Document Version to the current one; other steps cannot attach an Edge.
No candidate, dispatch, endpoint, or resolution-state row/JSON is accepted.

### 4. Retry-safe atomic writes and endpoint currentness

`KnowledgeStoreRepository` pre-validates pure bounds and canonical content,
then uses `Database.CreateExecutionStrategy().ExecuteInTransactionAsync` so the
explicit multi-save transaction is compatible with configured Npgsql retries.
Inside each PostgreSQL attempt it collects the source Logical Document (when
existing) and every target endpoint's Logical Document ID, sorts the distinct
UUIDs in database UUID order, and executes one parameterized
`SELECT ... ORDER BY "Id" FOR UPDATE`. Only after all owner rows are locked
does it re-read and validate each target's `CurrentVersionId`, Repository,
ScanRun, type, and authorization. The locks remain held through version/Edge/
flow/projection writes and commit. A target superseded while an attempt waited
is rejected as `NonCurrentEndpoint`; it cannot produce an Edge to stale truth.

For SQLite integration tests, the repository must not emit unsupported
`FOR UPDATE`. It begins a serializable transaction, acquires SQLite's
database-wide write reservation with a parameterized no-op owner-row update,
then performs the same post-lock validation. SQLite tests prove deterministic
sequential behavior and rollback only; a real PostgreSQL two-context test is
the authority for row-lock waiting and currentness races.

The caller supplies a non-empty `OperationId` that remains stable across every
execution-strategy attempt. Before insertion, Infrastructure computes a
lowercase SHA-256 `CommitFingerprint` over that `OperationId`, the commit
identity/current expectation/timestamp, canonical JSON and Markdown, source
anchors, Edges, ordered flow evidence, and the exact derived `IndexEntry` and
ordered `DocumentSection` rows. Unordered child families are sorted by their
stable IDs; ordered flow and section families retain their accepted order with
stable-ID tie breakers. The 64-character fingerprint is stored on the immutable
Document Version and protected by a relational length check.

The post-commit verification delegate does not treat the version ID alone as
proof. It re-materializes the expected canonical content and projections,
recomputes `CommitFingerprint`, and compares the durable Document Version,
anchors, Edges, Flow Groups/Traces/Steps, `IndexEntry`, ordered
`DocumentSection` rows, timestamp, current pointer, and predecessor link. Only
an exact replay returns `Committed`. Reusing the new Document Version identity
with another `OperationId`, changed aggregate input, changed projection, or
tampered durable row returns closed `Conflict`; replay never repairs or accepts
drift. Validation, unique/FK failures, injected `SaveChanges` failures, and
concurrency conflicts return closed outcomes and clear the change tracker
without leaking provider diagnostics.

No production failure hook is added. Tests inject persistence faults with an
EF interceptor.

### 5. Repository-scoped retrieval and redaction

`GetCurrentAsync` accepts `LogicalDocumentId`, finite authorized Repository IDs,
and 0–100 explicit stable section IDs. The query applies source Repository
authorization before materializing content. Missing and unauthorized source
documents both return `null` with no differentiated diagnostic.

Authorized results are immutable Core read models. Anchors contain safe
locators and hashes, never source text. For an Edge or flow step whose target
Repository is unauthorized, the result contains only
`IsRestrictedRelationship=true`; it omits Edge ID/type, target
Repository/Logical Document/Document Version, title, path, anchor, content,
and behavior. Section Document references use the same rule: authorized IDs
only plus a restricted-reference indicator.

Canonical JSON/Markdown, index summary/terms/search text, and section
heading/Markdown/search text can contain target-derived prose even when they
carry no machine-readable target ID. Therefore retrieval fails closed on
textual projection: when any Edge, flow step, or section Document reference
targets a Repository outside the current scope, it returns empty canonical
content/Markdown, an empty `CurrentIndexEntry`, and empty section text
projections. A section with a restricted Document reference also returns no
source-anchor or Document Version IDs and sets only its restricted-reference
indicator. Current scope is reevaluated on every call; no tracked entity or
cached authorization result can preserve access after revocation. The exact
materialized contract is in
[knowledge-retrieval-projection.md](contracts/knowledge-retrieval-projection.md).

### 6. Relational invariants and deletion policy

The migration adds only:

`LogicalDocuments`, `DocumentVersions`, `SourceAnchors`, `DocumentEdges`,
`FlowGroups`, `FlowTraces`, `FlowTraceSteps`, `IndexEntries`, and
`DocumentSections`.

All history/graph/canonical foreign keys use `Restrict`/`NoAction`; there is no
domain delete method. Projection rows are deleted explicitly only by rebuild.
Owner-safe alternate/composite keys enforce current/successor ownership and
Repository/ScanRun identity for anchors, Edge endpoints, and flow-step version
endpoints. Unique constraints enforce stable slots, version numbers, single
predecessor/successor links, exact Edge uniqueness, one index entry per
version, stable section ID/order, and contiguous-order preconditions backed by
repository validation. `IndexEntry.DocumentType` has its own closed relational
constraint, `CK_IndexEntries_DocumentType_Closed`, permitting only `CodeLogic`
or `BusinessLogic`; conversion to a string is not treated as sufficient
closure. Repository-scoped current, Edge endpoint, anchor, flow, index, and
section query paths are indexed.

Migration verification must:

1. apply from a clean database;
2. roll back exactly to
   `20260730083207_AddConservativePreScanGate`;
3. prove all nine Story 11.1 tables disappear while Story 4.3 tables/column
   remain;
4. reapply and prove constraints/indexes;
5. run `dotnet ef migrations has-pending-model-changes` with exit 0.

The existing Story 4.3 test also requires, before Story 11.1 tasks, the
integration-owner-only compatibility edit:

```text
tests/Vulgata.Tests/MigrationTests.cs:142
- migrator.Migrate();
+ migrator.Migrate("20260730083207_AddConservativePreScanGate");
```

This keeps the Story 4.3 test pinned to the schema it asserts. It is not owned
by a Story 11.1 implementer and may occur only after the user's narrow
read-only-boundary exception.

## Project Structure

### Documentation

```text
specs/004-minimal-knowledge-store/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── canonical-document-content.md
│   ├── knowledge-store-repository.md
│   └── knowledge-retrieval-projection.md
├── checklists/requirements.md
└── spec.md
```

### Planned source boundary

```text
src/dotnet/
├── Vulgata.Core/
│   ├── Entities/
│   │   ├── KnowledgeDocumentTypes.cs
│   │   ├── LogicalDocument.cs
│   │   ├── DocumentVersion.cs
│   │   ├── SourceAnchor.cs
│   │   ├── DocumentEdge.cs
│   │   ├── FlowGroup.cs
│   │   ├── FlowTrace.cs
│   │   ├── FlowTraceStep.cs
│   │   ├── IndexEntry.cs
│   │   └── DocumentSection.cs
│   └── DomainServices/
│       ├── IKnowledgeStoreRepository.cs
│       └── KnowledgeStoreContracts.cs
├── Vulgata.Infrastructure/Data/
│   ├── Configurations/
│   │   ├── LogicalDocumentConfiguration.cs
│   │   ├── DocumentVersionConfiguration.cs
│   │   ├── SourceAnchorConfiguration.cs
│   │   ├── DocumentEdgeConfiguration.cs
│   │   ├── FlowGroupConfiguration.cs
│   │   ├── FlowTraceConfiguration.cs
│   │   ├── FlowTraceStepConfiguration.cs
│   │   ├── IndexEntryConfiguration.cs
│   │   └── DocumentSectionConfiguration.cs
│   ├── KnowledgeDocumentMaterializer.cs
│   ├── KnowledgeStoreRepository.cs
│   ├── VulgataDbContext.cs
│   └── Migrations/
│       ├── *_AddMinimalKnowledgeStore.cs
│       ├── *_AddMinimalKnowledgeStore.Designer.cs
│       └── VulgataDbContextModelSnapshot.cs
└── Vulgata.Web/Program.cs

tests/Vulgata.Tests/
├── KnowledgeStoreDomainTests.cs
├── KnowledgeStorePersistenceTests.cs
├── KnowledgeStoreRetrievalTests.cs
├── KnowledgeStoreMigrationTests.cs
└── KnowledgeStorePostgreSqlConcurrencyTests.cs

integration-owner dependency (not Story 11.1 implementer ownership):
└── tests/Vulgata.Tests/MigrationTests.cs  # authorized one-line pin only
```

**Structure Decision**: extend the existing Core/Infrastructure repository
pattern and composition root. Story 11.1 implementers do not modify Story 4.3
entities, services, migration, tests, or workflow artifacts. The protected
`MigrationTests.cs:142` pin is a separately authorized integration-owner
dependency. `VulgataDbContext`, its next migration/snapshot, and the one-line
DI registration are the only existing production files updated by Story 11.1.

## Production and Test Ownership

| Group | Production ownership | Test ownership / required RED |
| --- | --- | --- |
| G0 protected predecessor compatibility | none | Integration owner only, after user exception: pin `MigrationTests.cs:142` to Story 4.3 migration; Story 11.1 task generation cannot start before this dependency is authorized |
| G1 closed content/bounds | Core types/entities; `KnowledgeDocumentMaterializer` | `KnowledgeStoreDomainTests`: exact JSON allowlist/duplicate rejection; UTF-8 limits; closed secret/source/host/diagnostic fixtures across canonical content, anchor locators, Flow Group titles, and Flow Trace Step descriptions; collection/order rules |
| G2 atomic initial commit | `IKnowledgeStoreRepository`, `KnowledgeStoreRepository`, configurations | `KnowledgeStorePersistenceTests`: success plus interceptor failure at validation/each save boundary leaves zero rows; exact replay succeeds while changed `OperationId`, aggregate child, flow, or durable projection conflicts |
| G3 Version Chain/currentness | owner-safe configurations and repository row-lock/supersession | SQLite sequential/rollback tests plus PostgreSQL two-context barriers: one successor, one conflict; waiting stale-endpoint commit rejects after lock; no losing/stale Edge rows |
| G4 projection/rebuild | materializer, `IndexEntry`, `DocumentSection`, repository rebuild | `KnowledgeStorePersistenceTests`: repeat/rebuild/failure, one equivalent index, stable ordered sections, zero canonical mutation, no `index.md` read |
| G5 retrieval/redaction | Core read models and repository materialized query | `KnowledgeStoreRetrievalTests`: authorized/partial/hidden/revoked scopes, restricted Edge/flow/section refs, target-derived text projection clearing, requested-section and output bounds |
| G6 migration | nine configurations, DbContext, migration, snapshot | `KnowledgeStoreMigrationTests`: operation allowlist, FK/delete/index/check shape including commit-fingerprint length and closed `IndexEntry.DocumentType`, clean apply, rollback baseline, reapply; PostgreSQL commands in quickstart |

Every group records transient failing test evidence before its production
change and reaches GREEN before the next group. Full regression and Web build
follow the focused groups.

## Requirement-to-Design/Test Trace

| Requirements | Design | Tests |
| --- | --- | --- |
| FR-001–FR-004 | split identity, current pointer, immutable version, aggregate `CommitFingerprint`, exact replay verification, optimistic supersession | G1, G2, G3 |
| FR-005–FR-010 | composite anchor ownership, exact typed Edges, minimal ordered flow | G1, G2, G5 |
| FR-011–FR-015 | canonical materializer and transactional projection replacement | G2, G4 |
| FR-016–FR-019 | finite materialized read model, sensitive evidence guard, Repository-first redaction, and fail-closed restricted target-derived text | G1, G5 |
| FR-020–FR-022 | exact UTF-8/count limits in contract, entities, and retrieval | G1, G2, G5 |
| FR-023–FR-025 | table/type/path allowlists and Story 4.3 regression | G6 plus full suite |

## E2E Decision

Initial predicates:

| Predicate | Triggered | Decision / owner |
| --- | --- | --- |
| User-visible workflow | No | no UI/API behavior |
| Authentication/authorization | Yes | Required: Repository scope and target redaction; `e2e-runner` |
| API/SignalR/persistence/multi-module | Yes | Required: Core→Infrastructure→PostgreSQL persistence; `e2e-runner` |
| EF migration/observable lifecycle | Yes | Required: additive migration, supersession, rollback/reapply; `e2e-runner` |
| Existing complete integration coverage | No at plan boundary | no coverage override may be claimed before current-boundary passing evidence |

The economical E2E is the focused persistence, retrieval/redaction, and
migration integration groups in [quickstart.md](quickstart.md), not a browser
flow. A later coverage override is allowed only if those tests cover every
triggered predicate at the pinned reviewed boundary. The clean-database
two-Repository demonstration and Docker image remain Story 11.5.

## Plan Review and Planned-Change Authorization

**Plan review: required.** The expedited exception requires independent review
because this plan establishes authorization/redaction and migration invariants.

**Repository planned-change authorization: not triggered.** The plan adds no Worker Agent
or other Agent tool, no Scan parsing/filtering/scheduling/grouping/retry/
dispatch strategy, and does not change or weaken a PRD requirement.

**Integration-owner exception: required; tasks BLOCKED.** The one sound
compatibility change is the exact `MigrationTests.cs:142` pin shown above.
That file belongs to completed Story 4.3 and is outside Story 11.1 implementer
ownership. The root must obtain a narrow explicit user exception authorizing
only that integration-owner edit before task generation. The exception does
not authorize any other Story 4.3 change and does not waive TDD, migration,
review, E2E, or verification gates.

## Complexity Tracking

No Constitution violation or additional architectural layer is introduced.
