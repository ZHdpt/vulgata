# Data Model: Minimal Knowledge Store and Retrieval Projection

All tables use schema `vulgata`, PascalCase table/column names, application-
generated GUIDs, and Infrastructure Fluent configuration. UTF-8 byte limits are
enforced before attachment; relational max lengths/checks provide a second
conservative guard. No entity has a public deletion method.

## Closed types

| Type | Values |
| --- | --- |
| `DocumentType` | `CodeLogic`, `BusinessLogic` |
| `DocumentEdgeType` | `References`, `GeneratedFrom` |
| `FlowTraceStepKind` | `Document`, `Relationship`, `Termination` |
| `DocumentSectionKind` | `Overview`, `BusinessRule`, `ProcessStep`, `TechnicalDetail`, `Evidence`, `Other` |
| `KnowledgeCommitStatus` | `Committed`, `Conflict`, `Rejected` |
| `KnowledgeRejectionCode` | `InvalidBound`, `InvalidContent`, `InvalidOwnership`, `MissingEndpoint`, `NonCurrentEndpoint`, `UnauthorizedEndpoint`, `InvalidEdgeSemantic`, `InvalidAnchor`, `InvalidFlow` |

Persisted enum strings have check constraints for exactly the listed values.

## Canonical structured content

The stored JSON object permits exactly these top-level fields:

`schemaIdentity`, `documentType`, `stableKey`, `title`,
`repositoryRelativePath`, `summary`, `keySymbols`, `businessTerms`,
`technicalTerms`, and `sections`.

Each section permits exactly:

`stableSectionId`, `order`, `kind`, `heading`, `markdown`, `searchText`,
`sourceAnchorIds`, and `documentVersionIds`.

There is no `payload`, extension bag, raw source/snippet/code field, diagnostic
field, secret field, or arbitrary type string. Parser depth is capped at 4;
comments, trailing commas, duplicate/unknown fields, and non-object roots are
rejected. The materializer checks every normalized string against the exact
rules and fixtures in
[contracts/canonical-document-content.md](contracts/canonical-document-content.md).

## Logical Document

| Field | Type / bound | Invariant |
| --- | --- | --- |
| `Id` | non-empty Guid | stable identity supplied by caller |
| `RepositoryId` | non-empty Guid | owning Repository; immutable |
| `DocumentType` | closed enum | immutable |
| `StableKey` | UTF-8 1..4,096 | stable semantic slot, normalized Form C |
| `CurrentVersionId` | nullable Guid | points to the sole current version; EF concurrency token |
| `CreatedAt` | `DateTimeOffset` | set once |

Constraints/indexes:

- PK `Id`;
- alternate key `(Id, RepositoryId)`;
- unique `(RepositoryId, DocumentType, StableKey)`;
- index `(RepositoryId, DocumentType, CurrentVersionId)`;
- FK Repository `Restrict`;
- optional composite FK
  `(CurrentVersionId, Id, RepositoryId)` →
  `DocumentVersion(Id, LogicalDocumentId, RepositoryId)`, `NoAction`.

`CurrentVersionId` may be null only inside the initial commit transaction.
Retrieval never exposes a slot with no current pointer. The composite FK makes
it impossible for current to point to another Logical Document or Repository.

## Document Version

| Field | Type / bound | Invariant |
| --- | --- | --- |
| `Id` | non-empty Guid | immutable version identity |
| `LogicalDocumentId` | non-empty Guid | owning stable slot |
| `RepositoryId` | non-empty Guid | equals Logical Document/ScanRun Repository |
| `SystemId` | nullable Guid | generation-time organizational System |
| `ScanRunId` | non-empty Guid | completed Story 4.3 ScanRun |
| `ProfiledRevision` | lowercase hex, 1..64 | equals ScanRun Profiled Revision |
| `VersionNumber` | int >= 1 | predecessor + 1 |
| `Title` | UTF-8 1..4,096 | immutable |
| `RepositoryRelativePath` | UTF-8 1..2,048 | normalized, non-rooted/non-escaping |
| `SchemaIdentity` | exact value <=128 | `vulgata.knowledge-document/1.0` |
| `ContentJson` | allowlisted JSON object, UTF-8 <=1 MiB, depth <=4 | canonical structured content; PostgreSQL `jsonb` |
| `ContentMarkdown` | UTF-8 1..1 MiB | deterministic render |
| `ContentFingerprint` | lowercase SHA-256, 64 | canonical JSON + Markdown |
| `SupersededById` | nullable Guid | sole historical transition; EF concurrency token |
| `CreatedAt` | `DateTimeOffset` | immutable |

Constraints/indexes:

- PK `Id`;
- alternate keys `(Id, LogicalDocumentId, RepositoryId)`,
  `(Id, RepositoryId)`, and `(Id, RepositoryId, ScanRunId)`;
- unique `(LogicalDocumentId, VersionNumber)`;
- unique filtered `SupersededById` when non-null;
- indexes `(RepositoryId, LogicalDocumentId, VersionNumber)` and
  `(ScanRunId, RepositoryId)`;
- composite owner FK `(LogicalDocumentId, RepositoryId)` →
  `LogicalDocument(Id, RepositoryId)`;
- composite Scan ownership FK `(ScanRunId, RepositoryId)` →
  `ScanRun(Id, RepositoryId)`;
- composite successor FK
  `(SupersededById, LogicalDocumentId, RepositoryId)` →
  `DocumentVersion(Id, LogicalDocumentId, RepositoryId)`;
- all ownership/successor delete behavior `Restrict`/`NoAction`;
- checks: version positive, successor differs from self, fingerprint length,
  schema exact, closed Document type through owner.

Canonical fields have no mutation method. `SupersedeWith` may set a previously
null successor exactly once.

## Source Anchor

| Field | Type / bound | Invariant |
| --- | --- | --- |
| `Id` | non-empty Guid | child identity |
| `DocumentVersionId` | Guid | exact claiming version |
| `RepositoryId` | Guid | same as version and Code Unit |
| `ScanRunId` | Guid | same as version and Code Unit |
| `CodeUnitId` | Guid | exact durable Code Unit |
| `ProfiledRevision` | lowercase hex <=64 | equals ScanRun/version |
| `RepositoryRelativePath` | UTF-8 1..2,048 | equals Code Unit path |
| `EvidenceLocator` | UTF-8 1..4,096 | safe `path#line-range`, no source |
| `LineStart`, `LineEnd` | int >=1 | inclusive, start <= end, within Code Unit |
| `SourceHash` | lowercase SHA-256, 64 | exact source/snippet integrity |
| `CreatedAt` | `DateTimeOffset` | immutable |

Composite FKs bind:

- `(DocumentVersionId, RepositoryId, ScanRunId)` →
  `DocumentVersion(Id, RepositoryId, ScanRunId)`;
- `(CodeUnitId, ScanRunId, RepositoryId)` →
  `CodeUnit(Id, ScanRunId, RepositoryId)`;
- `(ScanRunId, RepositoryId)` → `ScanRun(Id, RepositoryId)`.

All delete behavior is `Restrict`.
Unique `(DocumentVersionId, CodeUnitId, LineStart, LineEnd, SourceHash)`.
Indexes `(DocumentVersionId)` and
`(RepositoryId, RepositoryRelativePath, LineStart, LineEnd)`.

## Edge (`DocumentEdge` CLR name)

| Field | Type | Invariant |
| --- | --- | --- |
| `Id` | Guid | immutable |
| `SourceDocumentVersionId` | Guid | exact source |
| `SourceRepositoryId` | Guid | source version Repository |
| `SourceScanRunId` | Guid | source version ScanRun |
| `TargetDocumentVersionId` | Guid | exact target |
| `TargetRepositoryId` | Guid | target version Repository |
| `TargetScanRunId` | Guid | target version ScanRun |
| `EdgeType` | closed enum | only References/GeneratedFrom |
| `CreatedAt` | `DateTimeOffset` | immutable |

Both endpoint versions must exist and be current/authorized when committed.
`GeneratedFrom` requires Business Logic source and Code Logic target.
Cross-Repository is derived by comparing Repository IDs. Constraints:

- unique `(SourceDocumentVersionId, TargetDocumentVersionId, EdgeType)`;
- source != target;
- composite endpoint FKs
  `(SourceDocumentVersionId, SourceRepositoryId, SourceScanRunId)` and
  `(TargetDocumentVersionId, TargetRepositoryId, TargetScanRunId)` →
  `DocumentVersion(Id, RepositoryId, ScanRunId)`, `Restrict`;
- indexes `(SourceDocumentVersionId, EdgeType)`,
  `(TargetDocumentVersionId, EdgeType)`, and
  `(SourceRepositoryId, SourceScanRunId)` /
  `(TargetRepositoryId, TargetScanRunId)`.

## Flow Group

| Field | Type / bound | Invariant |
| --- | --- | --- |
| `Id` | Guid | immutable |
| `BusinessLogicDocumentVersionId` | Guid | exact committed BL version |
| `RepositoryId`, `ScanRunId` | Guid | equal BL version ownership |
| `GroupOrder` | int 1..32 | contiguous within BL version |
| `Title` | UTF-8 1..4,096 | bounded label |
| `CreatedAt` | `DateTimeOffset` | immutable |

Unique `(BusinessLogicDocumentVersionId, GroupOrder)`. Composite version FK,
Repository/ScanRun indexes, and all delete behaviors are `Restrict`.

## Flow Trace

| Field | Type | Invariant |
| --- | --- | --- |
| `Id` | Guid | immutable |
| `FlowGroupId` | Guid | owning group |
| `TraceOrder` | int 1..64 | contiguous within group |
| `CreatedAt` | `DateTimeOffset` | immutable |

Unique `(FlowGroupId, TraceOrder)`; group FK `Restrict`; index `FlowGroupId`.

## Flow Trace Step

| Field | Type / bound | Invariant |
| --- | --- | --- |
| `Id` | Guid | immutable |
| `FlowTraceId` | Guid | owning trace |
| `StepOrder` | int 1..256 | contiguous within trace |
| `DocumentVersionId` | Guid | exact evidence version |
| `DocumentRepositoryId` | Guid | exact endpoint Repository |
| `DocumentScanRunId` | Guid | exact endpoint ScanRun |
| `EdgeId` | nullable Guid | required only for Relationship |
| `StepKind` | closed enum | Document/Relationship/Termination |
| `Description` | UTF-8 1..4,096 | returned only when step Repository authorized |
| `CreatedAt` | `DateTimeOffset` | immutable |

Unique `(FlowTraceId, StepOrder)`. Composite FK
`(DocumentVersionId, DocumentRepositoryId, DocumentScanRunId)` →
`DocumentVersion(Id, RepositoryId, ScanRunId)` and optional Edge FK are
`Restrict`; indexes
`(DocumentRepositoryId, DocumentScanRunId, DocumentVersionId)` and `EdgeId`.
The repository validates first-step/no-Edge and previous→current Edge
continuity because contiguity across rows is not expressible as a check.

## Index Entry

| Field | Type / bound |
| --- | --- |
| `Id` | Guid |
| `LogicalDocumentId`, `DocumentVersionId`, `RepositoryId` | Guid |
| `SystemId` | nullable Guid |
| `DocumentType` | closed enum |
| `Title`, `Summary` | UTF-8 1..4,096 each |
| `RepositoryRelativePath` | UTF-8 1..2,048 |
| `KeySymbolsJson`, `BusinessTermsJson`, `TechnicalTermsJson` | JSON arrays, <=256 values, each value <=4,096 UTF-8 |
| `SearchText` | UTF-8 1..4 MiB |
| `CreatedAt` | `DateTimeOffset` |

Exactly one row per version: unique `DocumentVersionId`. Composite
version/Repository FK `Restrict`; indexes `(RepositoryId, DocumentType)` and
`LogicalDocumentId`. It is replaceable projection truth, not canonical
content.

## Document Section

| Field | Type / bound |
| --- | --- |
| `Id` | Guid row identity |
| `LogicalDocumentId`, `DocumentVersionId`, `RepositoryId` | Guid |
| `StableSectionId` | non-empty Guid |
| `SectionOrder` | int 1..256 |
| `SectionKind` | closed enum |
| `Heading` | UTF-8 1..4,096 |
| `ContentMarkdown` | UTF-8 1..64 KiB |
| `SearchText` | UTF-8 1..128 KiB |
| `SourceAnchorIdsJson` | distinct Guid array, 0..512, serialized <=128 KiB |
| `DocumentVersionIdsJson` | distinct Guid array, 0..1,024, serialized <=128 KiB |
| `CreatedAt` | `DateTimeOffset` |

Unique `(DocumentVersionId, StableSectionId)` and
`(DocumentVersionId, SectionOrder)`; indexes
`(DocumentVersionId, SectionKind)` and `(RepositoryId, DocumentVersionId)`;
version FK `Restrict`.

## Aggregate construction and transitions

### Owner-lock protocol

For every write, pre-validation produces the distinct existing source/target
Logical Document IDs. A PostgreSQL transaction locks them in database UUID
order with a single parameterized `SELECT ... ORDER BY "Id" FOR UPDATE`.
Endpoint currentness, Repository/ScanRun ownership, types, and authorization
are re-read only after lock acquisition. Locks remain through commit.

SQLite integration uses a serializable transaction and a parameterized no-op
update of the same owner rows to acquire SQLite's database-wide write
reservation; it then runs identical post-lock validation. SQLite is not used
as proof of PostgreSQL row-lock behavior.

### Initial commit

```text
validate scope/content/endpoints/anchors/flow in memory
-> begin retryable transaction
-> lock all existing endpoint owner rows and revalidate currentness
-> insert Logical Document with null current pointer
-> insert immutable Document Version and all canonical facts/projections
-> move current pointer to new version
-> commit
```

### Supersession

```text
validate expected current
-> lock source and endpoint Logical Documents in UUID order
-> revalidate expected/current endpoints
-> insert successor aggregate
-> predecessor.SupersededById = successor
-> LogicalDocument.CurrentVersionId = successor
-> concurrency-checked commit
```

Only one contender succeeds. The loser rolls back all rows and returns
`Conflict`.

### Projection rebuild

```text
read immutable canonical JSON
-> materialize complete replacement in memory
-> begin retryable transaction
-> explicitly delete target version's IndexEntry/DocumentSections
-> insert complete replacement
-> commit, or rollback to prior complete projection
```

## Delete behavior summary

Every FK is `Restrict` or `NoAction`. Projection replacement uses explicit
version-scoped deletes; no cascade can remove a Logical Document, historical
Document Version, source anchor, Edge, or flow fact.
