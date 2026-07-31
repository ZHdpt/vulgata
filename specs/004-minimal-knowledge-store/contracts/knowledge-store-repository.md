# Internal Contract: Knowledge Store Repository

This contract is Core-owned and materialized by Infrastructure. It exposes no
route, UI, SignalR method, Agent tool, `DbSet`, `IQueryable`, or EF entity.

## Scope

```text
RepositoryAccessScope
  authorizedRepositoryIds: distinct non-empty Guid[1..256]
```

The source Repository must be present. Current authorization is supplied on
every call; the repository does not cache it.

## Commit

```text
CommitAsync(KnowledgeCommit request, RepositoryAccessScope scope)
  -> KnowledgeCommitResult

KnowledgeCommit
  operationId: non-empty Guid (stable across execution-strategy replay)
  logicalDocumentId: non-empty Guid
  expectedCurrentVersionId: Guid? (null only for initial creation)
  newDocumentVersionId: non-empty Guid
  repositoryId: non-empty Guid
  scanRunId: non-empty Guid
  profiledRevision: lowercase Git object ID <= 64
  content: CanonicalDocumentContent
  sourceAnchors: SourceAnchorDraft[0..512]
  edges: EdgeDraft[0..1024]
  flowGroups: FlowGroupDraft[0..32]
  committedAt: DateTimeOffset

KnowledgeCommitStatus
  Committed | Conflict | Rejected

KnowledgeRejectionCode
  InvalidBound | InvalidContent | InvalidOwnership | MissingEndpoint |
  NonCurrentEndpoint | UnauthorizedEndpoint | InvalidEdgeSemantic |
  InvalidAnchor | InvalidFlow
```

`Rejected` returns only a closed code. Raw database/provider/exception text is
not part of the contract. A failure makes zero new rows visible.

Initial creation requires no existing stable slot and atomically creates one
Logical Document plus one complete current version. Supersession requires the
expected current version and atomically inserts a successor, sets the
predecessor's `SupersededById`, and moves the current pointer. A stale race is
`Conflict`; the caller must re-read before retrying.

`REFERENCES` accepts exact current endpoints. `GENERATED_FROM` accepts only a
new Business Logic Document Version source and current Code Logic Document
Version targets actually used. All endpoint Repositories must be authorized.
Every `EdgeDraft` supplies source/target Document Version, Repository, and
ScanRun IDs. Every anchor supplies Document Version/Repository/ScanRun and
Code Unit/ScanRun/Repository tuples. The database composite foreign keys—not
only application validation—must accept those ownership tuples.

## Lock and currentness protocol

Each execution-strategy attempt:

1. derives the source Logical Document ID when it already exists and every
   target endpoint's Logical Document ID;
2. deduplicates and sorts them in PostgreSQL UUID order;
3. begins the transaction and executes one parameterized
   `SELECT ... ORDER BY "Id" FOR UPDATE` over those owner rows;
4. after lock acquisition, re-reads each target Logical Document and exact
   Document Version and validates current pointer, Repository, ScanRun,
   Document type, and authorization;
5. writes the new aggregate and holds every lock through commit.

If a target was superseded while waiting, the result is
`Rejected/NonCurrentEndpoint` and no new row is visible. If the source current
pointer changed, the result is `Conflict`.

SQLite integration never executes `FOR UPDATE`. It uses a serializable
transaction plus a parameterized no-op owner-row update to acquire SQLite's
database-wide write reservation, then performs the same revalidation. SQLite
proves deterministic sequential behavior and rollback; PostgreSQL two-context
tests are authoritative for wait/race semantics.

Flow groups are allowed only for a Business Logic Document Version. Group,
trace, and step orders are contiguous and 1-based. The first step has no Edge.
A `Relationship` step requires an exact persisted/same-commit Edge from the
previous step's Document Version to its Document Version. Other step kinds
have no Edge.

## Projection rebuild

```text
RebuildProjectionAsync(
  documentVersionId,
  RepositoryAccessScope scope)
  -> RebuildStatus: Rebuilt | NotFoundOrUnauthorized | Rejected
```

The method reads immutable canonical JSON, prepares one bounded index entry and
all sections, then replaces only those rows in one transaction. Failure leaves
the previous complete projection. Repeat rebuilds yield one equivalent index
entry and the same stable ordered section set.

## Current retrieval

```text
GetCurrentAsync(
  logicalDocumentId,
  RepositoryAccessScope scope,
  requestedStableSectionIds: distinct Guid[0..100])
  -> CurrentKnowledge?
```

Missing and unauthorized source Documents both return `null`. The materialized
result contains exactly one current Document Version, canonical content and
Markdown, at most 512 anchors, 1,024 authorized-or-restricted relationships,
32 flow groups, 64 traces per group, 256 steps per trace, one index entry, and
only matching requested sections in canonical order.

Authorized relationships contain exact endpoint identity. An unauthorized
target is represented only as:

```text
RestrictedRelationship
  isRestrictedRelationship: true
```

It contains no Edge ID/type or target Repository, Logical Document, Document
Version, title, path, anchor, content, source, term, or behavior. Flow steps and
section Document references apply the same rule.
