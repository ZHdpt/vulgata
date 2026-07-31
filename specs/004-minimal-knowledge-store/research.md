# Research: Minimal Knowledge Store and Retrieval Projection

All planning questions are resolved; there are no open placeholders.

## Decision 1: Current is a Logical Document pointer

**Decision**: store `CurrentVersionId` on `LogicalDocument`; do not mutate an
`IsCurrent` flag on historical Document Versions.

**Rationale**: this leaves predecessor `SupersededById` as the sole historical
metadata transition while providing exactly one current version.

**Alternatives considered**: filtered unique `IsCurrent` rows were rejected
because supersession would mutate two historical fields; computed “latest”
queries were rejected because creation time is not a concurrency invariant.

## Decision 2: Owner-safe current and successor foreign keys

**Decision**: use `DocumentVersion(Id, LogicalDocumentId, RepositoryId)` as an
alternate key. Map current with
`LogicalDocument(CurrentVersionId, Id, RepositoryId)` and successor with
`DocumentVersion(SupersededById, LogicalDocumentId, RepositoryId)` to that key.
Use `(Id, RepositoryId, ScanRunId)` as the endpoint/anchor alternate key.

**Rationale**: single-column version FKs prove existence but allow an owner
tuple to point across Logical Document, Repository, or ScanRun boundaries.
Composite keys make ownership a database invariant.

**Alternatives considered**: application-only checks and single-column FKs
were rejected as race-prone and structurally weaker.

## Decision 3: Row lock current endpoints, then use optimistic concurrency

**Decision**: inside each retryable PostgreSQL transaction, lock all existing
source/target Logical Document rows in deterministic UUID order with
parameterized `FOR UPDATE`, then revalidate exact endpoint currentness and
ownership. Also configure current/successor values as application-managed EF
concurrency tokens.

**Rationale**: a target can be current before a transaction but superseded
before its Edge commits. Holding the owner-row lock through commit closes that
time-of-check/time-of-use gap; concurrency tokens still produce a closed
source-writer conflict. EF's concurrency behavior is documented in
[Handling Concurrency Conflicts](https://learn.microsoft.com/ef/core/saving/concurrency).

**Alternatives considered**: pre-transaction validation and optimistic tokens
alone were rejected because an Edge target row is not updated by the writer;
unordered locks were rejected for deadlock risk; PostgreSQL `xmin` was rejected
as an unnecessary provider-specific domain field.

## Decision 4: Provider-safe SQLite behavior

**Decision**: SQLite integration uses a serializable transaction and a
parameterized no-op update of the sorted owner rows to obtain SQLite's
database-wide write reservation before the same post-lock validation. It never
emits `FOR UPDATE`. PostgreSQL two-context tests are the concurrency authority.

**Rationale**: the test provider must not execute unsupported SQL or falsely
claim row-level semantics.

**Alternatives considered**: skipping locking on SQLite was rejected because
tests would exercise a different validation order; treating SQLite races as
production proof was rejected.

## Decision 5: Retry the whole explicit transaction

**Decision**: execute commit/rebuild through the context execution strategy's
`ExecuteInTransactionAsync`, using stable caller-provided IDs and a
post-commit verification query. Persist a deterministic SHA-256
`CommitFingerprint` on the immutable Document Version. It binds the stable
`OperationId` to the commit identity/current expectation/timestamp, canonical
JSON/Markdown, anchors, Edges, ordered flow evidence, and exact derived
`IndexEntry`/`DocumentSection` projections. Verification recomputes the
fingerprint and compares every durable aggregate/projection row before an
existing version identity is accepted as an exact replay.

**Rationale**: Program enables Npgsql retries. EF documents that a
user-initiated transaction must be replayed as one execution-strategy unit;
transactions make all included operations atomic. Version identity alone does
not prove that a prior attempt committed the same operation and all intended
children/projections. Fingerprint plus full durable comparison makes an
identical retry idempotent and a changed/tampered replay a closed `Conflict`.
See
[Connection resiliency](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency#execution-strategies-and-transactions)
and [Using transactions](https://learn.microsoft.com/ef/core/saving/transactions).

**Alternatives considered**: naked `BeginTransactionAsync` was rejected as
incompatible with the configured retry strategy; separate child saves were
rejected because they expose partial state; checking only the Document Version
ID or content fingerprint was rejected because neither binds `OperationId`,
provenance children, flow evidence, nor retrieval projections.

## Decision 6: Closed typed canonical content drives both projections

**Decision**: accept one closed `CanonicalDocumentContent` record with exactly
ten top-level and eight section properties, no arbitrary payload/extension
object, canonicalize it to JSON, render Markdown from ordered sections, and
derive `IndexEntry` and `DocumentSection` rows from the same value.

**Rationale**: rebuild can reproduce retrieval truth without parsing
`index.md`, and duplicate/reordered caller maps cannot change the output.

**Alternatives considered**: an arbitrary JSON payload and independently
supplied projection rows were
rejected because they can drift; parsing Markdown was rejected because JSON is
canonical; filesystem export was rejected as non-canonical.

## Decision 7: Deterministic FR-019 rejection boundary

**Decision**: remove source/secret/diagnostic fields structurally and scan every
normalized canonical string for the closed credential, host-path,
raw-source-carrier, and raw-diagnostic markers listed in the
canonical-content contract. Reuse the same Core-owned guard for source-anchor
evidence locators, Flow Group titles, and Flow Trace Step descriptions before
persistence. Reject unknown/duplicate properties, forbidden controls, unsafe
evidence text, and exact boundary overflow with closed `InvalidContent`.

**Rationale**: “detect every possible secret or source fragment” is not a
testable claim. A typed allowlist plus explicit marker fixtures is finite,
deterministic, fail-closed, and directly reviewable. Evidence locators, Flow
Group titles, and step descriptions cross the same retrieval boundary as
canonical prose, so leaving them outside the guard would reopen the prohibited
secret/source/path/diagnostic channel.

**Alternatives considered**: arbitrary JSON key blacklists were rejected
because unknown fields remain open; guarding only canonical JSON was rejected
because noncanonical evidence text is still persisted and returned;
entropy/semantic secret classifiers were rejected as nondeterministic; an
unqualified universal detection claim was rejected as unverifiable.

## Decision 8: Composite provenance ownership

**Decision**: duplicate Repository/Scan identity on Document Versions and
source anchors and enforce composite foreign keys to `ScanRun` and `CodeUnit`.

**Rationale**: an ordinary single-column FK proves existence but not that an
anchor belongs to the same Repository, ScanRun, and Profiled Revision.

**Alternatives considered**: application-only ownership checks were rejected
as insufficient; adding source text was rejected for security and scope.

## Decision 9: Exact Edge endpoints with no cross-repository discriminator

**Decision**: store exact source/target Document Version plus their Repository
IDs and only `References`/`GeneratedFrom`. Require current, authorized endpoints
at commit time; historical endpoints remain exact after later supersession.

**Rationale**: Cross-Repository status is a comparison of endpoint Repository
identity, not another Edge type.

**Alternatives considered**: Logical Document endpoints, narrative inference,
and a stored Cross-Repository flag were rejected.

## Decision 10: Minimal flow vocabulary

**Decision**: persist only Flow Group, ordered Flow Trace, and ordered Flow
Trace Step with closed step kinds `Document`, `Relationship`, `Termination`.

**Rationale**: this is sufficient to preserve prepared flow evidence and makes
candidate/dispatch/endpoint binding tables impossible to create accidentally.

**Alternatives considered**: copying the complete July Story 5.3 trace schema
was rejected as deadline scope expansion; JSON-only traces were rejected
because order and exact references need relational constraints.

## Decision 11: Repository scope is an input to every read/write

**Decision**: the specific Core repository takes a bounded set of currently
authorized Repository IDs. Hidden source reads return the same `null` as
missing. Unauthorized relationship targets collapse to a marker with no type,
ID, title, path, content, anchor, or behavior. If an Edge, flow step, or
section Document reference targets a Repository outside current scope,
retrieval also clears canonical JSON/Markdown, index summary/terms/search
text, and section heading/Markdown/search text because those projections may
contain target-derived prose without carrying an explicit target ID.

**Rationale**: Repository authorization is the outer boundary, and returning a
partially populated target would leak identity. Removing identifiers alone is
not sufficient: target-derived business text can disclose the same restricted
behavior. Empty bounded projection shapes plus the restricted marker are the
deterministic fail-closed response after revocation.

**Alternatives considered**: loading then filtering in Web, caching ownership,
revealing target IDs/types, and attempting selective prose scrubbing were
rejected. Selective scrubbing has no complete provenance map and could miss a
derived disclosure. No public API or new policy is needed.

## Decision 12: Restrictive history with explicit projection replacement

**Decision**: use restrictive/no-action delete behavior for every canonical,
history, graph, and flow FK. Rebuild explicitly removes only the target
version's replaceable projections inside its transaction.

**Rationale**: deletion cannot cascade through immutable evidence, while
projection replacement remains atomic and narrowly owned.

**Alternatives considered**: aggregate-wide cascades were rejected because
historical facts must never disappear; soft-delete flags were rejected because
there is no deletion requirement.

## Decision 13: Additive migration and protected predecessor compatibility

**Decision**: generate `AddMinimalKnowledgeStore` after plan acceptance, with
Story 4.3 migration `20260730083207_AddConservativePreScanGate` as exact
rollback target. Test clean apply, rollback, reapply, and no pending model
changes. EF documents the pending-model command in
[Managing migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing#checking-for-pending-model-changes).
The migration includes a non-null fixed-length `CommitFingerprint` column with
`CK_DocumentVersions_CommitFingerprint_Length` and an explicit
`CK_IndexEntries_DocumentType_Closed` constraint allowing only `CodeLogic` and
`BusinessLogic`.
Separately, after explicit user exception, the integration owner changes only
`tests/Vulgata.Tests/MigrationTests.cs:142` from latest migration to the exact
Story 4.3 migration. Story 11.1 implementers never edit that file.

**Rationale**: the existing Story 4.3 test asserts that only its two tables are
added; `Migrate()` becomes invalid as soon as a later migration exists. Pinning
the migration is the only change that preserves the test's intended boundary,
but its completed-story ownership requires a narrow user exception. The
fingerprint check proves its digest shape, while the explicit index-entry check
prevents an undefined converted enum string from entering retrieval truth.
Task generation remains blocked until authorized.

**Alternatives considered**: editing/combining Story 4.3 migration, manual SQL
without snapshot evolution, weakening/removing the Story 4.3 assertion, letting
Story 11.1 implementers touch the file, or skipping rollback were rejected.

## Decision 14: No ADR/context substitute

**Decision**: record that no ADR directories and no `CONTEXT-MAP.md` exist at
the pinned boundary; rely on the accepted specification, Constitution,
Ubiquitous Language, PRD, and July architecture.

**Rationale**: inventing an ADR or context authority is outside this plan.

**Alternatives considered**: none compliant.
