# Phase 0 Research: Language and Framework Detection

All planning clarifications are resolved. Decisions below are bounded to Story
4.2 and the approved provider remediation.

## R1. Parser identity source

**Decision**: project `lang` from provider structural nodes
(`Module`, `Class`, `Function`) and reconcile it by normalized
Repository-relative path. `File` is not an authority.

**Rationale**: exact wheel `0.5.1` defines no `lang` property on `File`, drops
the parsed file-level value when merging File, and persists parser `lang` on
structural nodes. This is observable without an extension guess and preserves
the pinned stock provider.

**Alternatives considered**:

- `File.language`: rejected; absent in the pinned schema/merge.
- file extension: rejected; violates FR-002 and cannot distinguish provider
  support from generic File ingestion.
- provider fork/upgrade: rejected by approved remediation.
- LLM classification: rejected as Story 4.2 fact authority; a later separate
  hypothesis/human-review aid is allowed but cannot change dispatchability.

## R2. Zero-structure and conflicting files

**Decision**: no observable structural `lang` means exactly one
`Filtered/UnknownLanguage` candidate outcome and no Code Unit. Multiple
distinct valid identities for one path are an atomic provider-contract
failure.

**Rationale**: an empty or declaration-free supported file and a generic File
are indistinguishable in the pinned graph. Absence is safer than fabricated
language; conflict means the provider snapshot cannot supply one authoritative
file fact.

**Alternatives considered**:

- retain File Code Unit with `Unknown`: rejected because it would be
  dispatchable without parser identity and lacks a genuine structural range.
- choose first/majority language: rejected as order-dependent guessing.
- treat conflict as an isolated row: rejected because every Code Unit on the
  path would remain ambiguous.

## R3. Language normalization

**Decision**: normalize Form KC, trim, invariant lower-case, and accept the
bounded open grammar `[a-z][a-z0-9_+#.-]{0,63}`.

**Rationale**: it retains pinned provider tokens (`c_sharp`, `cpp`, `tsx`) and
future safe provider identifiers without a language allowlist while preventing
unbounded/control/path-like values.

**Alternatives considered**:

- friendly-name allowlist (`C#`, `C++`): rejected because every new provider
  identifier would require feature code.
- arbitrary Unicode/string: rejected because it weakens persistence and UI
  bounds.

## R4. Provider completeness

**Decision**: set only the private gateway environment
`TOOL_RESULT_LIMITS={"execute_cypher_query":250001}`; reserve and fail closed
on `data.truncated`/`data.result_limit`; retain the combined 250,000-row cap and
two queries.

**Rationale**: the pinned handler defaults to 100, slices before reporting
`record_count`, and adds truncation metadata. A ceiling one above Vulgata's
cap lets Vulgata detect 250,001 directly while every larger response is
provider-marked incomplete.

**Alternatives considered**:

- deterministic pagination: rejected because it changes the accepted
  two-query immutable-snapshot lifecycle.
- unlimited provider results: rejected as unsafe.
- ignore additive metadata: rejected because it silently accepts partial
  structure.

## R5. Framework catalog and supported artifacts

**Decision**: use a closed, embedded, version-controlled JSON catalog and
closed parsers for `.csproj` + `packages.lock.json`, `package.json` +
`package-lock.json` v2/v3, direct Maven `pom.xml`, and direct Python
`requirements.txt`. Evidence inventory comes only from
`git ls-files --cached`; dynamic/conditional constructs, Maven inheritance,
requirements includes/URLs/markers, and untracked files are unsupported.
Initial facts are `aspnet-core`, `blazor`,
`spring-boot`, `react`, `express`, and `django`.

**Rationale**: these formats cover the story examples and both direct and
committed-lock evidence while remaining non-executing. Catalog startup
validation prevents ambiguous coordinates.

**Alternatives considered**:

- infer from imports/symbols/files: rejected by FR-009/014.
- evaluate MSBuild/Gradle/package scripts or restore: rejected for execution,
  security, and determinism.
- support every ecosystem immediately: rejected as unbounded; unsupported
  formats create no fact.
- mutable database catalog: rejected; this story requires version-controlled
  deterministic mappings, not user hypotheses.

## R6. Scope, confidence, and tie-breaking

**Decision**: manifest parent is scope root; select the deepest ancestor scope
per ecosystem, reject same-root ecosystem ambiguity, require catalog language
compatibility, and pair a lock only after proving exactly one successfully
parsed, supported, applicable same-root direct-manifest owner. Direct is
`High`; eligible lock-only evidence is `Medium`. Group by
CodeUnit/framework, then order confidence descending, scope depth descending,
and evidence locator ordinal ascending.

**Rationale**: applicability is explainable from Repository structure without
evaluating build logic. The tie-break is stable and preserves strongest
evidence.

**Alternatives considered**:

- apply every root manifest to every Code Unit: rejected as cross-project
  leakage.
- authorize a lock from tracked filenames or owner count alone: rejected
  because malformed, unsupported, conditional, or unsafe direct manifests do
  not prove ownership.
- ignore an invalid same-root owner candidate and use the remaining valid one:
  rejected because doing so manufactures uniqueness from incomplete evidence.
- require the owner manifest itself to contain a catalog-matched direct
  framework: rejected because a valid supported direct manifest may own a
  committed lock whose transitive entry is the only catalog match.
- persist all duplicate sources: rejected by the one-fact entity contract.
- last-write/encounter order: rejected as nondeterministic.

## R7. Bounds and failure ownership

**Decision**: per artifact 8 MiB, combined 64 MiB, 1,000 supported artifacts,
100,000 parsed package entries, nesting depth 64, path 2,048, locator 4,096,
250,000 candidate framework facts, 64 final facts per Code Unit, and 100 audit
details. Individual invalid/unsafe/unsupported evidence is isolated; global
overflow or detector fault fails before structural commit.

**Rationale**: attacker-controlled Repository evidence must not exhaust
resources or create partial misleading facts. Existing local candidate-filter
audit remains durable; provider-derived structure and framework facts stay
atomic.

**Alternatives considered**:

- fail the whole profile for one malformed manifest: rejected by FR-016.
- continue after global cap with partial facts: rejected because absence would
  be indistinguishable from a complete negative result.
- log raw parse diagnostics: rejected because they may contain source or host
  paths.

## R8. Persistence and migration

**Decision**: retain the two current additive migrations:
`20260720030657_AddLanguageFrameworkDetection` adds
`CodeUnitFramework` and `ScanRunLanguageSummary` tables with composite
aggregate FKs and unique constraints; and
`20260720060442_FrameworkEvidenceAuditCount` adds the non-negative
`ScanRun.FrameworkEvidenceAuditTotalOutcomeCount` column/check. Keep
historical nullable Story 4.1 `CodeUnit.Language` rows unchanged; enforce
non-null language for new completed Story 4.2 drafts in
Core/coordinator/repository. Iteration-2 remediation needs no migration.

**Rationale**: guessing a backfill would violate parser authority. Relational
summary rows make the recorded distinct-file breakdown auditable and queryable.

**Alternatives considered**:

- backfill `Unknown`: rejected as fabricated fact.
- JSON summary on ScanRun: rejected as weaker relational invariants.
- derive all summary values on every API request: rejected because FR-005 says
  the Scan Run summary records the result.

## R9. Authorization and presentation

**Decision**: extend the existing authorized scan-run response and
`RepositoryDetailDialog`; retain `ManagementAccess`, the current System
visibility checks, standalone Repository behavior, completion-time visibility
recheck, and 404 redaction. All new prose labels/errors are Simplified Chinese.

**Rationale**: a parallel endpoint or authorization model could disclose
profiling details or drift from Story 4.1.

**Alternatives considered**:

- public summary endpoint: rejected.
- new role/policy: rejected as scope and behavior change.
- render raw diagnostics: rejected for security and Chinese-only UI.

## R10. Final-image qualification

**Decision**: extend the mandatory final-image gate, not the historical
qualification ledger, to prove exact pin/image/query aliases, empty
supported-language behavior, a result above 100 rows, private 250001 limit,
absence of truncation, atomic rejection of truncation metadata, cleanup, and
forbidden retention. Use the single closed
`cgc-provider-contract-evidence-v2` grammar owned by the Story 4.1 contract;
the Python runner emits it and the C# verifier rejects any alternate root,
nested field, safe-name set, or cleanup shape. The completion lane treats
missing environment/evidence as failure.

**Rationale**: controlled JSON tests cannot prove the unpublished pinned
provider runtime. The original test's early return was not a completion proof;
the remediation planning source already requires completion-lane inputs, and
the iteration-2 correction now closes its schema and source-image binding.
The three old row counts have one destination under `coverage.rowCounts`.
Five non-projection completeness booleans have one destination under
`coverage.completeness`; the two old projection truncation-absence facts are
subsumed only by the stricter `projectionObservations.A.shapeComplete` and
`.B.shapeComplete` fields. Keeping both names would create duplicate semantic
authorities and is rejected by the closed grammar.

**Alternatives considered**:

- rely only on fake responses: rejected.
- modify/retry historical attempt-0003: rejected; it remains immutable and
  non-authoritative for this correction.
- retain the runner's current `schema`/`failureCategory`/`observations` shape
  beside the contract's
  `schemaVersion`/`primaryFailureCategory`/`projectionObservations`: rejected
  because two grammars cannot supply exact contract evidence.

## R11. Provider path disclosure boundary

**Decision**: normalize a provider-derived path to a bounded safe
Repository-relative value, or replace it with `(unknown)`, before constructing
an owned provider outcome and before the first log call. The coordinator
normalizes again as defense in depth and uses the same safe value for logging,
deduplication, durable audit, response, and UI.

**Rationale**: normalizing only immediately before persistence still lets an
absolute host path escape through structured logging. Making safe-path state an
owned-model invariant removes ordering-dependent disclosure.

**Alternatives considered**:

- log raw then normalize for persistence: rejected by AC-7, FR-015, and
  SC-006.
- rely on logger configuration or `NullLogger` tests: rejected because logging
  sinks are deployment-specific and the value has already crossed the
  boundary.
- sanitize only known Windows drive paths: rejected because UNC, POSIX-rooted,
  path-escape, control-character, and oversized forms require the same
  fail-closed rule.

## R12. Lock owner eligibility

**Decision**: before parsing lock entries into facts, enumerate the complete
committed same-root owner-candidate set. NuGet requires exactly one `*.csproj`;
npm requires the exact same-root `package.json`. That sole candidate must be
safely read, successfully parsed, supported by the closed direct-manifest
parser, unconditional/literal where the ecosystem permits dynamic constructs,
and applicable to the lock scope. Any zero/multiple, malformed, unsupported,
conditional/dynamic, unsafe, oversize, or inapplicable owner makes the lock
ineligible and yields one bounded audit outcome.

**Rationale**: a tracked filename proves only presence. It does not prove that
the lock has one valid owner or that the owner can establish an applicable
project/package scope.

**Alternatives considered**:

- count tracked `.csproj` or look for `package.json`: rejected; this is the
  current defect.
- accept a valid lock when its sole owner is malformed: rejected because the
  relationship to an applicable scope is unproven.
- treat an invalid owner as absent when another owner is valid: rejected
  because incomplete evidence cannot prove unique ownership.

## R13. Corrected source-to-image binding

**Decision**: build the final image freshly from one clean immutable correction
candidate. Generate a deterministic manifest of the candidate commit, Git
tree, Dockerfile, `.dockerignore`, and every tracked Docker-context file not
excluded by `.dockerignore`, with Repository-relative paths and SHA-256
content hashes. Label the image with candidate commit, tree, and manifest
SHA-256; inspect those labels before execution; bind the same values and image
ID into the accepted-correction claim and closed v2 evidence. The evidence
retains only the source-manifest SHA-256, not its path list.

**Rationale**: an image ID proves immutable bytes but not which corrected
source produced them. A clean Git identity plus complete consumed-context
manifest and inspected image labels closes that provenance gap.

**Alternatives considered**:

- reuse the earlier passing image: rejected because application source changed
  after that build.
- bind only a supplied image ID: rejected because it is caller assertion, not
  source provenance.
- bind only the Git commit: rejected because an unclean Docker build context
  can differ from the commit.

## R14. Story 4.2 historical workflow-evidence exception and T050

**Decision**: use the user-approved Story 4.2-only exception to reconstruct
what immutable Git history, existing workflow records, current source/tests,
and fresh current-candidate GREEN runs can prove. The reconstruction explicitly
records missing historical raw RED output and missing contemporaneous
checkpoint trailers as unavailable. It cannot create, infer, backdate, amend,
or relabel evidence. Fresh iteration-2 corrections still require ordinary
runnable RED-GREEN evidence. T050 remains incomplete until the corrected
immutable-candidate audit and reconstruction both pass.

**Rationale**: unavailable historical process output cannot be recreated
truthfully. A bounded disclosure preserves audit honesty while requiring full
current correctness and fresh correction evidence.

**Alternatives considered**:

- fabricate representative RED logs: rejected as false evidence.
- amend old commits to add trailers: rejected because it rewrites history and
  still would not prove contemporaneous execution.
- treat current GREEN as historical RED/GREEN: rejected because it proves only
  current behavior.
- block forever on unavailable historical raw output: rejected by the explicit
  Story 4.2 exception, provided the absence is disclosed and every current gate
  passes.

## Gate result

No `NEEDS CLARIFICATION` remains. Constitution pre-design and post-design
checks pass. No ADR or context authority exists in the Repository for this
area, and no conflict was found. The stricter lock-owner eligibility rule is a
material Scan evidence-filtering change, so fresh planned-change approval
remains mandatory after independent plan acceptance. The prior approval and
the iteration-2 remediation approval do not substitute for that post-review
gate.
