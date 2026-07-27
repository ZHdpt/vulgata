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

### `SafeProviderPath`

Value object used by every owned provider outcome, log, and durable audit:

| Field | Rule |
| --- | --- |
| `Value` | Normalized forward-slash Repository-relative path, maximum 2,048, or the literal `(unknown)`. |
| `IsKnown` | `true` only when strict normalization, containment, and segment checks passed. |

The raw provider scalar is never stored on this model. Absolute Windows,
UNC, POSIX-rooted, escape, reparse, control-character, invalid-separator, or
oversize inputs produce `(unknown)` before `ProviderCandidateOutcome` is
constructed. Logging and persistence accept `SafeProviderPath`, not a raw
string.

### `ProviderCandidateOutcome` (hardened transient contract)

| Field | Rule |
| --- | --- |
| `RelativePath` | Required `SafeProviderPath`; never a raw provider scalar. |
| `Outcome` | Closed provider outcome enum. |
| `Reason` | Closed bounded reason; no parser text or exception message. |

The coordinator revalidates `RelativePath` before its first log call and uses
the identical safe value for its audit key and `ScanCandidateOutcome`.

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

### `CommittedEvidenceInventoryEntry`

Transient identity for one retained supported artifact from the exact pinned
Repository tree:

| Field | Rule |
| --- | --- |
| `SourceCommit` | Exact immutable commit passed to `git ls-tree`; never mutable `HEAD` after resolution. |
| `TreeMode` | Exact mode from the NUL-delimited tree record. |
| `ObjectType` | Must be exactly `blob`; another type fails the Repository detector result. |
| `ObjectId` | Exact committed blob object ID used to read the artifact. |
| `ArtifactPath` | Safe Repository-relative path, maximum 2,048 characters. |
| `ArtifactKind` | Exactly one of `CsProject`, `MavenPom`, `NpmManifest`, `NpmLock`, `NuGetLock`, or `PythonRequirements`. |

Entries come only from incremental parsing of
`git ls-tree -r -z --full-tree <commit> --`. The stream is limited to 16 MiB,
100,000 input records, 1,000 retained entries, a 30-second deadline, and caller
cancellation. Only root or nested `*.csproj`, `pom.xml`, `package.json`,
`package-lock.json`, `packages.lock.json`, and `requirements.txt` paths are
retained. A malformed record, bound/deadline/cancellation failure, non-blob
retained entry, or mode/type/object identity drift returns zero framework
facts; no partial inventory survives.

The earlier `git ls-files --cached` wording is superseded. The mutable index,
working tree, untracked files, ignored files, and dirty files cannot create this
model or supply framework evidence.

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

### `DirectManifestOwnerCandidate`

Transient precondition record evaluated before any NuGet/npm lock entry may
create `PackageEvidence`:

| Field | Rule |
| --- | --- |
| `ArtifactPath` | Safe committed Repository-relative `.csproj` or exact `package.json`. |
| `ScopeRoot` | Same normalized parent directory as the candidate lock. |
| `Ecosystem` | `nuget` or `npm`; must match the lock. |
| `ReadStatus` | `SafeAndBounded` or a closed failure reason. |
| `ParseStatus` | `ParsedSupported` or a closed malformed/unsupported/conditional reason. |
| `Applicable` | True only when the supported direct-manifest shape can own the same-root lock scope. |

### `ValidatedDirectManifestOwner`

Created only when the complete same-root owner-candidate set has exactly one
entry and that entry is `SafeAndBounded`, `ParsedSupported`, and applicable.
For NuGet, `ParsedSupported` is closed to the same project-level grammar as
direct evidence, resolving `QR9-001`:

- a literal root `Project` `Sdk` attribute;
- a literal direct `Project/Sdk` child; or
- a literal `PackageReference` or `FrameworkReference` directly beneath a
  direct project-level `ItemGroup`.

Conditions or dynamic values on these shapes reject ownership. Declarations
beneath `Target`, `Choose`, `When`, import-driven/evaluated content, or any
other nested or execution-dependent construct never prove ownership. If one of
those is the sole apparent declaration, the candidate is `Unsupported`, the
matched lock creates no `PackageEvidence`/`Medium` fact, and exactly one bounded
closed audit outcome is retained for the artifact-local lock/owner rejection.
The outcome contains no raw declaration value, source content, or absolute host
path, and independent valid Repository evidence continues.

npm manifests outside the supported literal top-level package shape likewise
do not create this model. A catalog-matched direct framework declaration is not
required: ownership and catalog fact matching are separate.

`PackageEvidence(Relationship=Transitive)` requires one
`ValidatedDirectManifestOwner`. If there are zero or multiple candidates, or
the sole candidate is malformed, unsupported, conditional, unsafe, oversize,
or inapplicable, the lock produces no `PackageEvidence` and one bounded audit
outcome.

## Verification-only evidence models

These are workflow artifacts and image metadata, not PostgreSQL entities.

### `ProductionSourceManifest`

| Field | Rule |
| --- | --- |
| `SourceCommit` | Exact immutable correction-candidate commit. |
| `SourceTree` | Git tree for that commit. |
| `Entries` | Ordinally sorted Dockerfile, `.dockerignore`, and every tracked Docker-context file not excluded by `.dockerignore`; each entry is Repository-relative path plus SHA-256. |
| `ManifestSha256` | SHA-256 of the canonical manifest bytes. |

The final image carries inspected labels for `SourceCommit`, `SourceTree`, and
`ManifestSha256`. The accepted-correction claim retains the manifest; the
bounded provider evidence retains only these identities and never the entry
paths.

### `ProviderContractEvidenceV2`

One closed JSON model owned by the Story 4.1 contract and shared verbatim by
the Python runner and C# verifier. Its canonical root names are exactly
`schemaVersion`, `verdict`, `primaryFailureCategory`,
`acceptedCorrectionSha256`, `claimSha256`, `sourceCommit`, `sourceTree`,
`productionSourceManifestSha256`, `finalImageSha256`,
`verifierBaseImageSha256`, `providerVersion`, `providerLockSha256`,
`providerDistributionSha256`, `fixtureTreeSha256`,
`projectionObservations`, `coverage`, `immutableSnapshot`, `cleanup`, and
`forbiddenRetention`. The contract-owned nested shape is exact:

- `coverage.rowCounts` contains required bounded integers `projectionA`,
  `projectionB`, and `combined`;
- `coverage.completeness` contains required strict booleans
  `privateLimitExact`, `injectedTruncationRejected`, `queryDriftRejected`,
  `languageConflictRejected`, and `combinedBoundRejected`;
- `projectionObservations.A.shapeComplete` and
  `projectionObservations.B.shapeComplete` are the sole, stricter
  replacements for the old projection-A/projection-B truncation-metadata-
  absent booleans.

`rowCounts` and `completeness` are not root members. The two old
projection-truncation fields are not nested under `coverage.completeness`
because that would create duplicate semantic authorities. All remaining
`coverage` member names, observation members, requiredness, alternatives, and
integer bounds are reproduced only by the owning Story 4.1 contract and must
be consumed verbatim; Story 4.2 defines no parallel model.
Unknown, duplicate, missing, wrong-typed, or alternate-spelled fields fail.
The safe property-name set includes `parser_language`, `truncated`, and
`result_limit`; completeness metadata is observable by name but its presence
still forces failure.

### `HistoricalTddReconstruction` (Story 4.2 only)

| Field | Rule |
| --- | --- |
| `TaskGroup` | Exact Story 4.2 task-group identity. |
| `RequirementAndTests` | Existing requirement/test mapping. |
| `EarliestTestCommit`, `EarliestImplementationCommit` | Git-derived identities when provable; otherwise explicit null plus reason. |
| `WorkflowRecords`, `CheckpointTrailers` | Existing identities only; no amended or invented records. |
| `CurrentGreenEvidence` | Command, immutable candidate, result, and raw-output path for the current rerun. |
| `HistoricalRawRedAvailable` | Truthful boolean; false is permitted by the approved exception. |
| `CheckpointTrailerAvailable` | Truthful boolean; false is permitted by the approved exception. |

This model never represents a current rerun as historical evidence. It is an
input to T050, not evidence that T050 already passed.

## State transitions

```text
local candidate audit persisted
  -> provider index + two complete projections
  -> parser languages reconciled per file
  -> missing-language candidates become Filtered/UnknownLanguage
  -> retained Code Units receive required language
  -> resolve one exact committed tree
  -> incrementally parse git ls-tree -r -z --full-tree <commit> --
  -> retain only six supported blob shapes with exact mode/type/object identity
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

Lock with unproven owner:

```text
enumerate complete same-root direct-manifest owner candidates
  -> candidate count != 1: bounded ambiguous/unsupported outcome
  -> sole candidate read/parse/support/applicability fails: bounded outcome
  -> Target/Choose/When/import-driven declaration alone: unsupported
  -> no lock-derived PackageEvidence or framework fact
  -> exactly one bounded closed lock/owner audit without raw values
  -> independent valid artifacts continue
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

This preserves the implemented correction to the original failure seam in
which the in-memory ScanRun was marked Completed before `SaveChangesAsync` and
therefore could not be terminalized by a catch branch that checked only the
tracked status.

## Migration and rollback

Current additive migrations:

1. `20260720030657_AddLanguageFrameworkDetection` creates
   `CodeUnitFrameworks`;
2. the same migration creates `ScanRunLanguageSummaries`;
3. `20260720060442_FrameworkEvidenceAuditCount` adds non-negative
   `ScanRun.FrameworkEvidenceAuditTotalOutcomeCount` with a
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

Rollback deploys the previous application image and reverses
`20260720060442_FrameworkEvidenceAuditCount` first, dropping only the
audit-total check/column, then reverses
`20260720030657_AddLanguageFrameworkDetection`, dropping only the two new
tables/indexes. It never backfills, rewrites, or deletes historical Code Units
or candidate outcomes. If new Story 4.2 facts must be retained for audit, roll
forward instead of applying the destructive down migration. Iteration-2
remediation introduces no third migration.
