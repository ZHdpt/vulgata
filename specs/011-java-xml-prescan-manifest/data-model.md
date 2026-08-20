# Data Model: Lightweight Java/XML PreScan & Prepared Scan Manifest

All Story 12.2 persistence is additive. `Prepared Scan Manifest`, `File Code Unit`, and `Scan Context Snapshot` use their current authoritative glossary meanings. Existing v1 Pre-Scan Results, v1 handoffs, historical Code Units, Documents, Source Anchors, Edges, Uncertainties, Scan Executions, and prepared-demo rows remain readable and semantically unchanged.

## 1. Prepared Scan Manifest

One immutable manifest per `(RepositoryId, ScanRunId)` for the Story 12.2 path.

| Field | Shape | Invariant |
| --- | --- | --- |
| `Id` | deterministic UUID v8 | Derived only after `ManifestFingerprint` through `canonical-envelope/1.0`; never a fingerprint input |
| `RepositoryId`, `ScanRunId` | `Guid` | Composite binding to one exact Scan Run |
| `CoordinationId` | `Guid` | Same exactly-two coordination for both manifests |
| `ProfiledRevision` | fixed 40-char lowercase hex | Equals Scan Run and every child |
| `RequestedRef` | bounded canonical full ref | `refs/heads/...` or `refs/tags/...`; no refspec/options/pseudoref |
| `SchemaIdentity` | string | Exactly `prepared-scan-manifest/1.0` |
| `SnapshotIdentity` | SHA-256 | Complete canonical Git archive inventory and bytes; no host path |
| `RuntimeIdentityHash` | SHA-256 | Story 12.1 canonical host runtime identity |
| `CodeGraphProofIdentityHash` | SHA-256 | Digest of the typed `codegraph-completeness-proof/1.0` envelope; all stable proof fields are persisted for independent reconstruction |
| `JavaReadinessIdentityHash` | SHA-256 | Digest of the typed `java-readiness/1.0` envelope; `Ready` or XML-only `NotApplicable` stable fields are independently reconstructable |
| `CodeGraphObservedDurationMilliseconds`, `JavaReadinessObservedDurationMilliseconds` | bounded non-negative longs | First-commit audit metadata only; excluded from all fingerprints, UUIDs, replay equality, and API identity projections |
| `CatalogSchemaIdentity`, `CatalogHash` | bounded string + SHA-256 | Closed catalog resource identity |
| `Candidate/Eligible/Filtered/Skipped/Selected` | non-negative counts | Exactly equal ordered child/audit totals |
| `Classification totals` | non-negative counts | Complete Confirmed/Hint/Warning totals, not only retained rows |
| `EvidenceTruncated` | bool | True exactly when observations exceeded 256 |
| `ContextSnapshotId` | `Guid` FK | Legacy Story 12.1 time-derived relational identity; excluded from every Story 12.2 canonical identity/replay comparison |
| `PreparedScanContextIdentityHash`, `ContextContentHash` | SHA-256 + SHA-256 | Stable envelope digest plus content hash for exact Repository/Scan Run/Profiled Revision/schema/content binding |
| `Fingerprint` | SHA-256 | Length-prefixed canonical identity over all ordered package values |
| `ApprovalState` | enum string | `Approved`; no draft row is persisted |
| `CreatedAt`, `ApprovedAt` | UTC timestamps | Audit metadata excluded from replay identity |

Every entry carries contiguous Repository-local `ManifestOrdinal` and distinct pair-global `PairSelectionOrdinal`. Unique constraints: `(RepositoryId, ScanRunId)`, `Fingerprint`, `(CoordinationId, RepositoryId)`, `(ManifestId, ManifestOrdinal)`, and `(CoordinationId, PairSelectionOrdinal)`. Deletes are Restrict/NoAction. Exact bytes, field order, exclusions, and UUID derivation are defined only by [canonical-envelope.md](contracts/canonical-envelope.md).

### Prepared Scan Manifest Entry (prepared-only principal)

One required row per selected File Code Unit; historical Code Units have no entry row.

| Field | Shape | Invariant |
| --- | --- | --- |
| `FileCodeUnitId` | non-null `Guid`, PK/FK | Required one-to-one FK to `CodeUnit.Id`; also the entry identity used by selected audits |
| `PreparedScanManifestId` | non-null `Guid` FK | Required manifest owner; this is the selected unit's manifest association |
| `CoordinationId` | non-null `Guid` | Equals manifest coordination and supports pair-global uniqueness |
| `FileCodeUnitIdentityFingerprint` | non-null SHA-256 | Equals the referenced File Code Unit's computed/persisted fingerprint |
| `DirectBusinessScore`, `FinalBusinessScore` | non-null ints | Closed score table; final never below direct |
| `SelectionReason` | non-null closed enum | Direct, expanded relationship/hop, or production fallback |
| `ManifestOrdinal` | non-null positive int | Contiguous and unique within manifest |
| `PairSelectionOrdinal` | non-null positive int | Contiguous and unique within coordination |

The table has valid non-null principal constraints: PK `(FileCodeUnitId)`; composite FK `(PreparedScanManifestId, CoordinationId)` to the manifest binding; unique `(PreparedScanManifestId, ManifestOrdinal)`; unique `(PreparedScanManifestId, FileCodeUnitIdentityFingerprint)`; unique `(CoordinationId, PairSelectionOrdinal)`; and unique audit-principal key `(PreparedScanManifestId, FileCodeUnitId, FileCodeUnitIdentityFingerprint, PairSelectionOrdinal)`. `PreparedScanManifestEntryConfiguration` maps these keys/FKs/checks. No alternate key is placed on nullable `CodeUnit` columns. The store recomputes the referenced Code Unit fingerprint and rejects an entry whose scalar differs; PostgreSQL mutation tests prove this cross-row invariant because it cannot be expressed as an FK without recreating the nullable-key defect.

## 2. Prepared Scan Audit Outcome

Immutable ordered child of one manifest. It records every candidate/filter/skip/selection decision without source content.

| Field | Shape | Invariant |
| --- | --- | --- |
| `Id` | deterministic UUID v8 | Derived from `DetailFingerprint`; derived ID/fingerprint and AuditOrdinal are excluded from audit identity |
| `PreparedScanManifestId` | `Guid` | Required parent FK |
| `Ordinal` | positive int | Contiguous canonical order per manifest |
| `RelativePath` | max 2,048 | Canonical contained `/` path or closed unknown-warning sentinel |
| `Outcome` | closed enum | `Eligible`, `Filtered`, `Skipped`, `Selected`, `Warning` |
| `Reason` | closed enum/string | Exact filter/safety/encoding/bound/analysis/cap reason |
| `FileHash` | nullable SHA-256 | Required when safe complete raw bytes were accepted |
| `RawByteCount`, `LogicalLineCount` | nullable non-negative | Present when measured safely |
| `FileCodeUnitIdentityFingerprint` | nullable SHA-256 | Canonical-envelope tag 9 persisted verbatim; distinguishes multiple segments on one path |
| `PairSelectionOrdinal` | nullable positive int | Canonical-envelope tag 10 persisted verbatim; non-null only for `Selected` |
| `PreparedScanManifestEntryId` | nullable `Guid` FK component | Equals the selected entry's non-null `FileCodeUnitId`; excluded from audit envelope and absent for nonselected rows |
| `DetailFingerprint` | SHA-256 | Canonical audit identity; no raw source or host path |

Conditional invariants:

- `Selected`: `FileCodeUnitIdentityFingerprint`, `PairSelectionOrdinal`, and `PreparedScanManifestEntryId` are all non-null. Composite FK `(PreparedScanManifestId, PreparedScanManifestEntryId, FileCodeUnitIdentityFingerprint, PairSelectionOrdinal)` targets the prepared-only entry principal key, proving same-manifest identity and ordinal equality. Unique filtered indexes enforce one selected audit per entry/fingerprint and pair ordinal.
- `Eligible`: `FileCodeUnitIdentityFingerprint` is non-null because the candidate identity was safely constructed; `PairSelectionOrdinal` and `PreparedScanManifestEntryId` are null. This preserves an excluded-over-cap candidate even though no manifest entry/File Code Unit row is persisted.
- `Filtered`, `Skipped`, or `Warning`: all three fields are null; no File Code Unit identity or pair position is asserted.
- `PairSelectionOrdinal` may never be present without the fingerprint and selected FK. Path alone is never an association because one path may yield multiple segments.

Unique `(ManifestId, Ordinal)` and `(ManifestId, RelativePath, Outcome, Reason, DetailFingerprint)`. The additive migration plus `PreparedScanManifestEntryConfiguration` and `PreparedScanAuditConfiguration` own the prepared principal, nullable dependent columns, composite FK, filtered indexes, and outcome checks. `IPreparedScanStore` recomputes `DetailFingerprint` from every persisted preimage before insert and independently from PostgreSQL rows on replay; it never treats the stored digest as its own proof.

Canonical audit construction has one public Core seam and one production owner: `PreparedScanAuditFactory`. Before that factory exists, `PreparedScanAuditContractTests` must be RED for every candidate/filter/skip/selected outcome, canonical ordering, closed reason, both nullable preimage fields, exact DetailFingerprint envelope/digest, deterministic UUID, and mutation-sensitive replay projection. Enumerators supply closed decision inputs; they do not construct fingerprints/UUIDs independently. Manifest/store consumers depend on this factory's focused and surrounding-suite GREEN.

## 3. File Code Unit (additive `CodeUnit` new-path shape)

Historical fields remain. A Story 12.2 File Code Unit additionally requires:

| Field | Shape | Invariant |
| --- | --- | --- |
| `FileCodeUnitIdentityFingerprint` | nullable SHA-256 | Required for new prepared File units but not a key; historical rows remain null |
| `FileHash` | nullable SHA-256 | Complete raw parent-file bytes; never the legacy prefix hash |
| `ByteStart`, `ByteEndExclusive` | nullable long | `0 <= start < end <= file length` |
| `LineStart`, `LineEnd` | existing ints | Exact one-based inclusive logical lines |
| `SegmentOrdinal`, `SegmentCount` | nullable ints | `1 <= ordinal <= count`; unsplit is `1/1` |
| Existing `UnitType/RangeConfidence/Status` | enums | Exactly `File` / `Exact` / `Pending` |
| Existing `SourceHash` | SHA-256 | Exact assigned raw byte interval; equals FileHash for unsplit |

The new factory computes `FileCodeUnitIdentityFingerprint` from exactly Repository, Scan Run, Profiled Revision, path, FileHash, byte/line range, segment ordinal/count, and SourceHash, then derives UUID v8. Scores, selection reason, and both ordinals live on the non-null prepared-only manifest entry and remain excluded from File Code Unit identity. For XML, construction is unreachable until the exact safe range chain is GREEN. Historical constructors/rows remain compatible; nullable CodeUnit columns are never principal-key properties.

## 4. CodeGraph completeness proof (value object)

Persist the exact stable typed fields in canonical-envelope §4.2: Repository, Scan Run, Profiled Revision, snapshot identity/root-binding hash, runtime identity hash, index identity, proof schema, initialized/state, all pending counts, worktree/reindex predicates, indexed-file count, logical project/index bindings, all binding-match predicates, and trusted-path predicates. `Complete` is derived and true only when every approved-state invariant passes. `CodeGraphProofIdentityHash` is the canonical-envelope digest and is the only CodeGraph proof reference in manifest identity. Measured duration and volatile process/status telemetry are separately bounded first-commit audit metadata and never replay identity. Failure in index/rebuild, status, runtime, identity, binding, trusted path, completeness, or readiness rejects approval; only the later related-file expansion may fall back.

## 5. Java readiness outcome (value object)

Persist the exact stable typed fields in canonical-envelope §4.3: `Ready|NotApplicable`, Repository/Scan Run/Profiled Revision/snapshot/runtime/workspace bindings, fixed JDT-LS release/archive hash, Java major, StreamJsonRpc version, stdio transport, workspace exclusivity, nullable readiness file, initialize/initialized/import-state/document-symbol predicates, and every binding-match predicate. `JavaReadinessIdentityHash` is the canonical-envelope digest and is the only Java proof reference in manifest identity. XML-only is the sole legal `NotApplicable` case with the contract's explicit Null/false/zero defaults. `Failed` produces no approved proof envelope. Duration, timestamps, diagnostics, compilation observations, raw LSP/process data, host paths, workspace tokens, and failure detail remain bounded audit metadata or rejected-attempt diagnostics and are excluded from identity/replay.

## 6. Communication Pattern Catalog (immutable resource/value model)

Root: schema identity, catalog ID/version, rules, raw resource SHA-256. Each rule has a case-sensitive contained path glob; one-to-eight namespace/local-name element steps; optional exact ancestor chain; zero-to-eight exact attribute tests; one extraction choice; fixed prefix removal; Communication Kind; classification rule/provenance. Loader rejects unknown properties and all non-grammar features.

## 7. Communication Kind Classification (additive v2 shape)

Existing v1 rows remain. Story 12.2 rows add/bind catalog rule identity and use the v2 closed compatibility matrix. A retained row contains kind when known, level, confidence, rule, provenance, canonical path, exact Evidence Locator, occurrence count, and fingerprint. Maximum retained rows per v2 result is 256; over-bound representation is first 255 plus one deterministic Bound Exceeded Warning.

## 8. Scan Context Snapshot

Reuse the immutable Story 12.1 entity and schema `scan-context-snapshot/1.0`. Story 12.2 inserts it through the pair transaction, not the separate `CreateContextSnapshotAsync` call, and additively persists nullable `PreparedIdentityHash` only for new Story 12.2 rows. The stable `PreparedScanContextIdentityHash` is the canonical-envelope digest over Repository, Scan Run, Profiled Revision, schema identity, and content hash. Manifest identity contains this stable hash and content hash, never the legacy entity `Id`. The existing time-derived `Id` remains the relational FK; `CapturedAt` remains required audit metadata. Both are excluded from fingerprints/replay, so different capture instants with identical stable bindings replay without mutation. Historical Story 12.1 rows remain readable with null prepared identity and receive no backfill or reinterpretation. Context content is untrusted advisory context and never classification evidence.

## 9. Pre-Scan Result and approved prepared handoff

Keep v1 shape legal. Add a v2 factory/conditional relational shape requiring manifest ID/fingerprint, relational ContextSnapshotId, stable PreparedScanContextIdentityHash/content hash, Profiled Revision, result/handoff schema identities, complete classification totals, and approval time. The old handoff source returns only v1. A new prepared-handoff source returns only a complete v2 package and grants no dispatch authority.

## 10. Pair aggregate and transaction state

`PreparedScanPairPackage` is an in-memory command containing exactly two distinct Repository packages bound to one `TwoRepositoryScanCoordination`, expected row versions, `PairSelectionFingerprint`, `PairPackageFingerprint`, and merged selection count 50-150. High-value candidates have direct score >=400; only the first at most 20 per Repository by corrected FR-029 order are mandatory seeds. Repository comparison is lowercase UUID `D` text with ordinal comparison. No draft table exists. Durable outcomes are `Committed`, `Replay`, `Conflict`, `Rejected`, `Cancelled`, or `Failed`; only `Committed/Replay` can expose prepared handoffs.

## 11. Canonical dependency graph

```text
source scalars -> FileCodeUnitIdentityFingerprint -> File Code Unit UUID
stable CodeGraph predicates/bindings -> CodeGraphProofIdentityHash
stable Java readiness predicates/bindings -> JavaReadinessIdentityHash
stable Repository/ScanRun/revision/context-schema/content bindings -> PreparedScanContextIdentityHash
classification scalars -> ClassificationFingerprint -> Classification UUID
audit scalars -> DetailFingerprint -> Audit UUID
selected entries + both ordinals -> PairSelectionFingerprint
manifest scalars + both proof hashes + prepared context hash + pair fingerprint + entries/audits/classifications -> ManifestFingerprint -> Manifest UUID
pair fingerprint + ordered manifest fingerprints -> PairPackageFingerprint
```

There is no reverse dependency and no derived ID is included in its source fingerprint. Observed proof durations/telemetry, `CapturedAt`, and the legacy time-derived ContextSnapshotId have no edge into this graph. All identity values use the exact tagged grammar and artifact field tables in `contracts/canonical-envelope.md`.

### Commit transition

```text
absent pair + all predicates pass -> both Approved packages (one transaction)
exact Approved pair + exact retry -> Replay (zero mutation)
any existing mismatch -> Conflict (zero mutation)
any validation/persistence/cancellation before commit -> no approved package
Approved -> immutable (no update/delete transition)
```

Story 12.2 creates no `ScanJobExecution`, starts no Worker, advances no Pass 1 dispatch state, and creates no Document/Edge/Uncertainty.
