# Data Model: Language and Framework Detection

PostgreSQL schema `vulgata` is authoritative. Provider graph and manifest
models are transient. All paths are normalized Repository-relative paths.

## Transient provider models

### `ProviderCodeUnitProjection`

Extends the Story 4.1 row with:

| Field | Rule |
| --- | --- |
| `ParserLanguage` | Nullable scalar transport field. Only structural-node values are language evidence; File null is expected. |

The whole row remains closed and bounded to 4,096 bytes. A non-null value is
Form-KC normalized, trimmed, invariant lower-case, at most 64 characters, and
must match `[a-z][a-z0-9_+#.-]{0,63}`.

### `ProviderFileLanguage`

| Field | Rule |
| --- | --- |
| `RelativePath` | Required eligible normalized path. |
| `Language` | Exactly one normalized structural-node identity for the path. |
| `StructuralObservationCount` | Positive aggregate count; audit/metrics only. |

No record exists when no structural node supplies a language. More than one
distinct identity for a path rejects the provider result atomically.

### `RepositoryProfileDraft`

In-memory atomic persistence unit:

```text
CodeUnits
CodeUnitEdges
UnresolvedStructuralRelationshipCandidates
CodeUnitFrameworks
ScanRunLanguageSummaries
CandidateOutcomes
terminal ScanRun state
```

Every Code Unit path has one `ProviderFileLanguage`; no outcome path has both a
retained Code Unit and `Filtered/UnknownLanguage`.

## Persistent entities

### `CodeUnit` (extended)

`Language` is required for every Code Unit created by a completed Story 4.2
profile. It stores the normalized parser token, maximum 64 characters.

The physical column remains nullable only for compatibility with historical
Story 4.1 rows. New draft validation rejects null/empty/invalid language before
EF tracks the aggregate. All Code Units sharing `(ScanRunId, FilePath)` must
have one identical language.

Relationship:

- one Code Unit has zero to 64 `CodeUnitFramework` facts.

### `CodeUnitFramework`

| Field | Rule |
| --- | --- |
| `Id` | UUID, generated in Core. |
| `CodeUnitId` | Required Code Unit identity. |
| `ScanRunId`, `RepositoryId` | Required and identical to the Code Unit aggregate. |
| `FrameworkName` | Catalog-normalized slug, `[a-z][a-z0-9-]{0,63}`. |
| `Confidence` | Exactly `High` or `Medium`. |
| `DetectionSource` | Exact bounded Repository-relative evidence locator, maximum 4,096. |
| `CreatedAt` | UTC. |

Constraints:

- alternate/composite FK
  `(CodeUnitId, ScanRunId, RepositoryId)` to Code Unit;
- unique `(CodeUnitId, FrameworkName)`;
- index `(ScanRunId, FrameworkName)`;
- closed enum conversion and string/path length checks.

Winner selection before construction:

1. confidence `High` before `Medium`;
2. greater scope depth;
3. ordinal `DetectionSource`;
4. exact duplicates collapse.

### `ScanRunLanguageSummary`

| Field | Rule |
| --- | --- |
| `ScanRunId`, `RepositoryId` | Composite owner key/FK to ScanRun. |
| `Language` | Same normalized token grammar as CodeUnit. |
| `DistinctFileCount` | Positive count of distinct retained Code Unit paths. |

Primary/unique key: `(ScanRunId, Language)`.

Invariant: recomputing `COUNT(DISTINCT CodeUnit.FilePath)` grouped by Language
for the ScanRun exactly equals these rows.

### `ScanCandidateOutcome` (reason vocabulary extension)

Existing entity remains the bounded audit store. New closed reasons:

```text
UnknownLanguage
FrameworkEvidenceMalformed
FrameworkEvidenceUnsafePath
FrameworkEvidenceReparsePoint
FrameworkEvidenceUnsupported
FrameworkEvidenceAmbiguousScope
FrameworkEvidenceBoundExceeded
```

`UnknownLanguage` is always `Filtered`, once per eligible candidate path, and
is terminal. Framework evidence outcomes use `Skipped` or `Errored` as defined
by the closed detector mapping and never contain raw parser text, source,
absolute host paths, or arbitrary package coordinates. At most 100 detail
records are retained for evidence parsing; complete aggregate counts are kept
in the draft/result metrics.

## Transient framework models

### `FrameworkCatalogEntry`

| Field | Rule |
| --- | --- |
| `Ecosystem` | `nuget`, `npm`, `maven`, or `python-requirements`. |
| `PackageCoordinate` | Exact normalized supported coordinate. |
| `FrameworkName` | Normalized closed fact name. |
| `AllowedLanguages` | Non-empty normalized parser-token set. |
| `EvidenceKinds` | Closed direct and/or paired-lock kinds. |

Catalog version and SHA-256 are logged as safe operational metadata. Duplicate
or overlapping coordinates that can produce multiple framework names fail
startup.

### `PackageEvidence`

| Field | Rule |
| --- | --- |
| `ArtifactPath` | Repository-relative, maximum 2,048. |
| `ScopeRoot` | Normalized parent directory. |
| `Ecosystem` | Closed catalog ecosystem. |
| `PackageCoordinate` | Retained only after exact catalog match. |
| `Relationship` | `Direct` or `Transitive`. |
| `DetectionSource` | Closed exact locator, maximum 4,096. |
| `ScopeDepth` | Non-negative path-segment count. |

Direct maps to `High`; paired committed lock maps to `Medium`. An unpaired lock
does not create this model.

## State transitions

```text
local candidate audit persisted
  -> provider index + two complete projections
  -> parser languages reconciled per file
  -> missing-language candidates become Filtered/UnknownLanguage
  -> retained Code Units receive required language
  -> bounded package evidence parsed in memory
  -> catalog facts scoped, ranked, deduplicated
  -> language summaries computed
  -> RepositoryProfileDraft validates
  -> one transaction persists full draft + Completed ScanRun
```

Provider/process/envelope/truncation/language-conflict/cleanup failure:

```text
retain local pre-provider filter audit
commit zero provider-derived Code Units, Edges, unresolved relationships,
framework facts, or language summaries
terminalize ScanRun Failed/Aborted
```

Invalid individual evidence:

```text
bounded outcome for that artifact
no facts from that artifact
independent valid artifacts continue
```

Global evidence/detector failure:

```text
fail before structural transaction
retain only pre-provider local filter audit
```

Final persistence failure or cancellation:

```text
rollback final transaction
clear/detach the failed RepositoryProfileDraft and tracked Completed ScanRun
reload the durable still-Profiling ScanRun in a fresh unit of work
persist one sanitized Failed transition, or Aborted for cancellation
prove zero provider-derived rows and retained local pre-provider audit
```

This explicitly corrects the current failure seam in which the in-memory
ScanRun is marked Completed before `SaveChangesAsync` and therefore cannot be
terminalized by a catch branch that checks only the tracked status.

## Migration and rollback

Additive migration:

1. create `CodeUnitFrameworks`;
2. create `ScanRunLanguageSummaries`;
3. add non-negative `ScanRun.FrameworkEvidenceAuditTotalOutcomeCount` with a
   default of zero so complete audit totals survive the bounded 100-detail
   sample and reload;
4. add composite FKs, uniqueness, checks, and indexes;
5. leave existing `CodeUnits.Language` nulls unchanged.

The audit-total column is an accepted implementation-time storage correction:
the response contract requires a complete count even when only 100 safe detail
rows are retained. It changes no evidence parsing, filtering, or Scan
strategy.

Deployment:

1. back up database;
2. apply migration in staging through normal startup `MigrateAsync`;
3. prove historical Story 4.1 runs remain readable;
4. run a fresh Story 4.2 profile and verify relational invariants;
5. deploy the same image/migration to production.

Rollback deploys the previous application image and reverses the additive
migrations, first dropping only the audit-total check/column and then the two
new tables/indexes. It never backfills, rewrites, or deletes historical Code
Units or candidate outcomes. If new Story 4.2 facts must be retained for
audit, roll forward instead of applying the destructive down migration.
