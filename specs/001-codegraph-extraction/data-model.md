# Data Model: CodeGraph Code Unit Extraction

This target model implements the accepted whole-Repository specification only
after provider qualification and a follow-up plan accept an exact projection
grammar. All identifiers are UUIDs. PostgreSQL is authoritative;
CodeGraphContext's transient Kuzu directory is not Vulgata persistence truth.
Tables use the repository's PascalCase EF naming convention.

## Provider qualification precondition

This target model has no dependency on a provider graph-store schema. Its only
current provider version assumption is the user-authorised, exact executable
qualification pin `codegraphcontext==0.5.1`. The prior 0.5.0 pin was unavailable
from the configured package index and remains historical failure evidence only.
All model mappings remain contingent on the 0.5.1 non-root qualification
capturing an exact, stable projection grammar. The observed public documentation
is labelled 0.5.0, so it cannot be treated as proof of 0.5.1 mapping behavior;
a version change does not itself permit mapping, persistence, or API/UI
implementation.

## `ScanRun` aggregate (`ScanRuns`)

| Field | Rule |
| --- | --- |
| `Id` | UUID primary key generated in Core. |
| `RepositoryId` | Required FK to exactly one Repository; a run cannot span Repositories. |
| `Status` | `Profiling`, `Completed`, `Failed`, or `Aborted`. It models no queue, dispatch, or Worker Agent state. |
| `TotalCandidateFileCount` | Non-negative count of eligible source candidates after filtering. |
| `ParsedFileCount` | Non-negative count of qualified provider file records mapped from a successful whole graph; never exceeds total. It is zero for a provider-process failure. |
| `ProviderProcessTimeoutSeconds` | Positive whole-process limit copied from validated deployment configuration for audit. It is not a per-file setting. |
| `StartedAt` / `CompletedAt` / `Duration` | UTC timestamps; terminal runs have end/duration. |
| `NeedsHumanReview` | True when >=500 eligible candidates yield zero persisted Code Units after a successful provider result. |
| `TerminalMessage` | Bounded/sanitized operator text; no source, credentials, tokens, working-copy path, or raw provider output. |

`ScanRun` owns warnings and `ScanCandidateOutcome` rows. It has dependent Code
Units and Unresolved Structural Relationship Candidates. Repository deletion is
restricted while this history exists unless a later lifecycle policy says
otherwise.

## `ScanCandidateOutcome`

One auditable result per repository-relative candidate/reason occurrence:
`Id`, `ScanRunId`, `FilePath`, `Outcome`, bounded `ReasonOrCategory`,
`AuditGroupOrdinal`, and timestamp.

- `AuditGroupOrdinal` is deterministic ordinal-path grouping of no more than
  1,000 candidates. It is audit metadata, never a provider invocation or
  timeout boundary.
- A filtered path is always persisted as terminal `Filtered` before provider
  execution.
- After a successful qualified graph, an eligible file may be `Parsed`,
  `Skipped`, or `Errored` only when the completed projection provides a
  per-file mapping/validation reason.
- A whole provider-process timeout, crash, or non-zero exit is stored on
  `ScanRun`; it does **not** fabricate per-file `Skipped`/`Errored` outcomes
  and it writes no provider-derived structural records.

A partial unique key on `ScanRunId`, normalized path, outcome, and category
prevents duplicate audit records while allowing a distinct mapping warning.

## `CodeUnit` (`CodeUnits`)

| Field | Rule |
| --- | --- |
| `Id`, `ScanRunId`, `RepositoryId` | UUID identity and required FKs; Repository equals `ScanRun.RepositoryId`. |
| `FilePath` | Normalized repository-relative path, never an absolute host path. |
| `FileIdentityHash` | SHA-256 of first `min(4096, file length)` bytes. |
| `SourceHash` | SHA-256 of complete local file contents. |
| `LineStart`, `LineEnd` | Positive, inclusive, ordered; `Exact` only when the qualified provider projection directly supplies verified coordinates. |
| `SymbolName`, `UnitType` | Required structural identity. Initial supported types: Function, Class, Method, Namespace, Module, File. |
| `RangeConfidence` | Exactly `Exact`, `Approximate`, or `Heuristic`; never High/Medium/Low. |
| `Language` | Nullable in Story 4.1 pending user-approved PRD allocation to Story 4.2. |
| `Status` | New extracted units are `Pending`; vocabulary supports Extracted, Pending, Dispatched, Completed, Failed, Skipped, and terminal Filtered. Story 4.1 does not dispatch. |

Unique structural identity per Scan Run is normalized path, line range, symbol,
and unit type. Repeated provider output is idempotent.

## `CodeUnitEdge` (`CodeUnitEdges`)

`Id`, `ScanRunId`, `RepositoryId`, `SourceCodeUnitId`, `TargetCodeUnitId`,
`EdgeType`, and `IsResolved`. Both endpoints are required Code Units of the
same Scan Run and Repository. `IsResolved` is always true in Story 4.1;
`EdgeType` is Calls, Imports, Inherits, or Contains. A unique
source/target/type constraint makes repeated output idempotent.

The one whole-Repository provider graph is an explicit correctness boundary:
a qualified source/target relation is mapped only after both endpoints are
known in the complete structural snapshot. No audit group can make an endpoint
appear absent.

## `UnresolvedStructuralRelationshipCandidate`

This is not a Code Unit Edge and not the glossary-defined Uncertainty. Required
fields: `Id`, `ScanRunId`, `RepositoryId`, `SourceCodeUnitId`,
`RelationshipKind`, `ProviderTargetEndpointIdentity`, and timestamps. The
endpoint identity is non-empty stable structural metadata proven by
qualification (provider path/symbol/range or a Vulgata-normalized key derived
from those provider fields); it never contains source, credentials, or a target
Repository. It has no target Code Unit, Document, or Uncertainty field.

Unique source/relationship/endpoint identity prevents duplicates. It is written
only after a completed whole graph and a valid source mapping; it is not a
substitute for missing data caused by a timeout/crash.

## Transaction boundaries

1. Create `ScanRun` first so clone/CLI failures remain auditable. After the
   working-copy enumeration succeeds, persist deterministic Filtered audit rows
   before provider execution.
2. Execute exactly one provider graph build/projection for the whole working
   copy. On provider-process failure, persist terminal ScanRun state only; do
   not persist partial provider-derived results.
3. For a successful qualified projection, validate all records in memory;
   malformed independent records become bounded warnings/outcomes.
4. Atomically write valid Code Units, outcomes, edges/candidates, terminal
   counts/status, duration, and human-review flag. No Code Unit Edge can point
   to a missing target.
