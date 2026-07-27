# Implementation Plan: Language and Framework Detection

**Source boundary**: Full review iteration-9 correction source commit `fab80f9334b7f2132bc270c9ce99e433b3d77513` (tree `90b569cd7934c44a084ab51b3da86c64a1add6f2`) | **Date**: 2026-07-27 | **Spec**: [spec.md](spec.md)
**Story**: `4-2-language-and-framework-detection`
**Provider boundary**: stock exact-pinned `codegraphcontext==0.5.1`

## Summary

Continue Story 4.1's one-Repository profiling transaction by projecting the
parser `lang` property that the pinned provider persists on structural nodes,
reconciling exactly one normalized parser identity per Repository-relative
source file, and applying it to every retained Code Unit in that file. A
candidate with no observable structural-node identity, including a supported
zero-structure file, produces exactly one terminal
`Filtered/UnknownLanguage` outcome and no Code Unit. Conflicting or malformed
provider identities, an incomplete response, or provider-reported truncation
fails the provider result atomically.

After language reconciliation, Infrastructure reads a closed set of committed
package/project artifacts without executing them. A version-controlled catalog
maps exact package coordinates to normalized framework names. Scoped direct
declarations produce `High` confidence; deterministic committed lock entries
produce `Medium`. Facts are deduplicated by Code Unit/framework, retain the
strongest confidence and a deterministic exact evidence locator, and are
persisted in the same structural transaction as Code Units, edges,
relationships, language summaries, and bounded evidence outcomes.

The plan also corrects Story 4.1's technical provider contract: the private
gateway is started with `TOOL_RESULT_LIMITS` setting
`execute_cypher_query` to `250001`; both projection envelopes must reject
`truncated`/`result_limit` instead of silently accepting a partial result; and
the mandatory final-image gate proves an empty supported-language file and
more than 100 returned rows. The two-query lifecycle and 250,000-row Vulgata
cap remain unchanged.

Iteration-2 remediation closes four proof and safety gaps without expanding
product scope. Every provider-derived path is normalized to a bounded safe
Repository-relative value before it can be logged or persisted. NuGet and npm
lock evidence is eligible only when its complete same-root owner-candidate set
proves exactly one successfully parsed, supported, applicable direct manifest.
The Story 4.1 contract, Python observer, and C# verifier implement one closed
final-image evidence schema and bind a freshly built image to the exact clean
candidate source. T050 remains incomplete until those corrections and the
immutable-candidate audit pass.

The user-approved Story 4.2 historical workflow-evidence exception permits a
truthful reconstruction from Git and current runnable evidence when historical
raw RED output or missing checkpoint trailers no longer exist. It never
authorizes fabricated RED output, backdated evidence, amended history, or a
claim that a current GREEN rerun is a historical RED observation.

The same truth rule applies to current T001. Its required pre-correction
baseline was not observed before correction work and is now temporally
unavailable. Regenerated tasks must preserve that absence explicitly and
replace the impossible historical prerequisite with a present-day immutable
Git and current-GREEN verification record. That record proves only the
accepted/current boundary; it does not prove or imply that the commands ran
before the corrections.

Full review iteration 9 adds two bounded corrections. `AR9-001` reconciles R5
with the already approved QR8 committed-tree inventory: exact pinned
`git ls-tree`, closed six-shape retention, incremental bounds, and retained
mode/type/blob identity explicitly supersede mutable-index
`git ls-files --cached`. `QR9-001` makes NuGet lock ownership use exactly the
supported project-level direct-manifest grammar. Nested or
execution-dependent MSBuild declarations never prove ownership; rejection is
artifact-local, creates zero lock-derived facts and one bounded closed audit,
and does not suppress independent valid evidence.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`); Python only inside the existing
final-image verifier; exact `codegraphcontext==0.5.1` in the Linux image.

**Primary Dependencies**: ASP.NET Core, EF Core 10/Npgsql, Fluent UI Blazor,
system `git`, the unpublished loopback `cgc api` gateway, and built-in bounded
JSON/XML/TOML/text readers. No LLM, package-manager, build, restore, script,
external registry, or provider MCP/tool route participates.

**Storage**: PostgreSQL 17, schema `vulgata`; SQLite integration override.
CodeGraphContext Kuzu state and manifest parse models are transient.

**Testing**: xunit in `tests/Vulgata.Tests`, SQLite
`WebApplicationFactory<Program>` integration coverage, and the mandatory
final-image provider-contract E2E.

**Target Platform**: the existing single Linux application container plus
PostgreSQL; local Windows development remains supported.

**Project Type**: .NET Blazor web application with Core, Infrastructure,
Shared, Web, and Tests boundaries.

**Performance Goals**: preserve Story 4.1's one 1,800-second Repository
profiling deadline; process at most 64 MiB of committed package evidence,
250,000 provider rows, and 250,000 candidate framework facts before
deduplication; calculate language counts by distinct path without per-Code-Unit
rescans.

**Constraints**:

- one Repository and one ScanRun; no queue, Orchestrator, Worker Agent,
  dispatch, scheduling, retry, or Story 4.3 behavior;
- stock pinned provider; structural-node `lang` is the only authoritative
  language signal and extensions are never language evidence;
- private gateway limit `250001`; provider `truncated` or `result_limit`,
  combined rows above 250,000, language conflict, response drift, or cleanup
  failure is atomic;
- closed non-executing framework catalog and bounded Repository-relative
  evidence; no raw source, absolute host path, credential, raw provider row, or
  unbounded diagnostic is retained;
- existing `ManagementAccess`, Repository visibility, not-found redaction, and
  Simplified-Chinese user-visible text remain exact.
- every provider-derived path is normalized or replaced with `(unknown)` before
  any structured log, exception, durable outcome, DTO, or UI path can observe
  it; a raw rejected path never enters an owned outcome model;
- lock-derived framework evidence requires one proven direct-manifest owner,
  not merely a tracked filename, same-root file count, or catalog-matched lock
  entry;
- committed evidence inventory is only the bounded incremental output of
  `git ls-tree -r -z --full-tree <commit> --`, filtered to the closed six
  supported artifact shapes with exact tree mode/type/blob identity;
  `git ls-files --cached` is superseded and is not an evidence authority;
- NuGet ownership can be proven only by a literal root `Project` `Sdk`
  attribute, literal direct `Project/Sdk`, or literal
  `PackageReference`/`FrameworkReference` directly beneath a project-level
  `ItemGroup`; nested `Target`/`Choose`/`When`, import-driven, and other
  execution-dependent constructs never prove ownership;
- final-image evidence is accepted only from one closed schema and a fresh
  image whose inspected source labels match the clean immutable candidate and
  its deterministic production-source manifest;
- historical reconstruction is Story 4.2-only and cannot weaken fresh
  RED-GREEN requirements for any iteration-2 correction;
- the missing ordered pre-correction T001 observation remains explicitly
  unavailable. A present-day GREEN verification may satisfy the corrected
  current-boundary prerequisite only when it is labelled with its actual
  observation time and immutable commit; it can never be called historical or
  pre-correction evidence.

**Scale/Scope**: one Repository with approximately 10,000 candidate files;
at most 1,000 supported evidence artifacts, 64 MiB combined evidence bytes,
100,000 parsed package entries, 250,000 candidate framework facts, 64
framework facts per Code Unit, 100 retained audit/evidence details, 10,000
unknown-language paths with at most 16 MiB combined UTF-8 path data, and
complete aggregate counts.

**Unresolved clarifications**: 0.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1.*

| Gate | Plan response | Pre | Post |
| --- | --- | --- | --- |
| I. Layered Project Boundaries | Core owns entities and service contracts; Infrastructure owns provider/manifest parsing, EF, catalog loading, and persistence; Shared owns DTOs; Web owns authorization/facade/UI. | Pass | Pass |
| II. Domain Purity & Ubiquitous Language | Uses Repository, Scan, Code Unit, Worker Agent, and Cross-Repository exactly; no EF/data annotations/generic repository/query leakage in Core. | Pass | Pass |
| III. Validation & API Contract Discipline | Existing plural minimal APIs, `ProblemDetails`, `ManagementAccess`, Repository visibility, and Simplified-Chinese UI are extended, not replaced. No new public input is added. | Pass | Pass |
| IV. Test-First Integration Harness | Every iteration-2 behavior correction retains its named runnable RED/GREEN evidence. QR9-001 adds fresh behavior-level RED/GREEN for Target-nested and Choose/When-nested owners plus all three valid project-level owner shapes. Existing integration coverage is rerun at the corrected candidate. The bounded historical exception records unavailable earlier raw RED evidence and the missing ordered T001 observation as unavailable; present-day GREEN is separately identified and never relabelled as historical. | Pass | Pass |
| V. Security & Data Stewardship | All analysis stays local and non-executing; provider gateway remains loopback-only; provider paths are fail-closed before logging/persistence; evidence, diagnostics, and response sizes are bounded and redacted. | Pass | Pass |

No Constitution exception or ADR conflict was found. The July architecture's
sampled LLM language/framework output is a separate later hypothesis or
human-review aid only; it cannot create or replace a Story 4.2 fact or make an
`UnknownLanguage` candidate dispatchable.

## Authority and Planned-Change Disposition

- **Accepted intent**: the original provider remediation in
  `evidence/plan-blocker-1.json` remains authoritative. The user has also
  explicitly approved the iteration-2 remediation findings
  `AR2-001..003`, `QR2-001..002`, `CF-002`, `EV-002`, `WF-002`, `PL-001`,
  `TR-001`, and `PA-001`, including the bounded Story 4.2 historical
  workflow-evidence exception. Neither approval changes specification or PRD
  scope.
- **PRD**: no requirement changes. PRD FR-4.1 still requires deterministic
  language/framework profiling before Worker Agent dispatch. Retaining new
  valid provider identifiers does not promise Worker Agent support beyond the
  V1 demo languages and therefore does not weaken PRD out-of-scope language
  coverage.
- **Previously satisfied planned-change trigger**: the accepted functional plan
  materially changed Scan evidence filtering by requiring one successfully
  parsed, supported, applicable direct-manifest owner for lock-derived facts.
  Independent `plan-review-6.json` accepted that plan and
  `evidence/planned-change-authorization-2.json` contains the subsequent
  explicit authorization. Regenerated tasks must verify both immutable records
  and their accepted artifact boundary.
- **AR9-001 disposition**: R5 now explicitly supersedes mutable-index
  `git ls-files --cached` with the pinned committed-tree mechanism already
  authorized for QR8: exact
  `git ls-tree -r -z --full-tree <commit> --`, bounded incremental parsing,
  closed six-shape retention, exact mode/type/blob identity, and zero partial
  facts. This reconciliation does not expand the approved QR8 scope.
- **QR9-001 introduces a new planned-change trigger**: restricting NuGet
  lock-owner validation to the supported project-level direct-manifest grammar
  materially changes Scan XML parsing and lock-owner filtering. The prior QR8
  approval covers only its stated inventory/namespace remediation and does not
  pre-authorize QR9. After a fresh independent plan-review `ACCEPT`, the root
  must pause before task generation for explicit user authorization naming the
  trigger, exact owner-grammar correction and effects, implementation intent,
  this plan, and PRD FR-4.1. That approval waives no TDD, Full reviews, E2E,
  security, verification, or Git gate.
- **Scope**: no PRD requirement, Worker Agent/tool, scheduling, grouping,
  retry, dispatch, or product intent changes. The accepted outcome remains
  deterministic framework facts before Worker Agent dispatch, with unsupported
  evidence preserved as absence.

## Provider and Profiling Design

### Language projection and reconciliation

1. Keep the exact two read-only projection lifecycle. Add
   `node.lang AS parser_language` to Projection A only. `File` rows legitimately
   return null; `Module`, `Class`, and `Function` rows may expose the parser
   identity. Projection B and endpoint identity remain structural and do not
   duplicate language authority.
2. Extend the closed Projection A row grammar with exactly one nullable scalar
   `parser_language`. Normalize a present value by Unicode Form KC, trim,
   invariant lower-case, and require
   `[a-z][a-z0-9_+#.-]{0,63}`. This preserves provider tokens such as
   `c_sharp`, `cpp`, `typescript`, and future valid identifiers without a
   feature-specific allowlist.
3. Group valid non-empty structural identities by normalized Repository path.
   One distinct value is the file language and applies to all provider Code
   Units on that path. Exact duplicates are harmless. More than one distinct
   value is a provider-contract conflict and fails the whole provider result.
4. Reconcile every locally eligible candidate. A path with no observable
   identity produces exactly one `Filtered/UnknownLanguage` outcome. Remove
   every projected Code Unit and relationship endpoint for that path before
   mapping; do not construct a File Code Unit, symbol, range, Edge, unresolved
   relationship, or dispatchable unit.
5. Build `ScanRunLanguageSummary` rows from distinct retained Code Unit paths,
   not Code Unit count. Every Code Unit on one path must have the same
   non-empty language before the persistence transaction begins.

### Fail-closed provider path handling

All provider-derived paths have a single safe-state transition:

```text
raw provider scalar
  -> strict Repository-relative normalization and containment validation
  -> bounded SafeProviderPath | literal "(unknown)"
  -> owned outcome/log/persistence/response
```

The raw scalar may be used only inside the validating stack frame and is then
discarded. Absolute paths, rooted drive/UNC forms, path escapes, reparse
traversal, control characters, invalid separators, and values over 2,048
characters become `(unknown)`. Provider parsing constructs
`ProviderCandidateOutcome` only with the safe value. The coordinator
normalizes again before its first log call and uses that same safe value for
the audit key and durable `ScanCandidateOutcome`; it never logs first and
normalizes later. The same ordering applies to rejected provider Code Units,
relationships, exceptions, and any future provider outcome.

Captured-logger RED/GREEN tests inject absolute Windows, UNC, POSIX-rooted, and
`..` escape paths into malformed provider rows. They assert that logs,
exceptions, durable state, API responses, and rendered output contain neither
the raw value nor any host-path fragment and contain only `(unknown)` plus a
closed reason.

### Story 4.1 technical-contract correction

The pinned provider's private HTTP handler defaults
`execute_cypher_query` to 100 rows and, when truncating, reports the shortened
`record_count` plus additive `result_limit` and `truncated` fields. The
original Story 4.1 adapter ignored those additive envelope fields, so a partial
response could look complete. The implementation at the remediation planning
source already contains the accepted limit/completeness correction; this plan
keeps it as a required invariant while closing the iteration-2 evidence-schema
and source-image proof gaps.

The correction is:

- launch only the private gateway with
  `TOOL_RESULT_LIMITS={"execute_cypher_query":250001}`; do not change global
  process environment or any other tool limit;
- parse `data.truncated` and `data.result_limit` as reserved completeness
  metadata: absent is normal; if either is present, both must have the exact
  provider types, and **any presence or true/value state rejects the entire
  provider result**. They are never ignored as generic additive metadata;
- keep Vulgata's combined 250,000-row cap. A response of 250,001 rows crosses
  the Vulgata cap; a larger provider result is marked truncated and fails.
  No silent truncation and no pagination is accepted;
- update the Story 4.1 contract text and shared production/test constants
  during implementation, but do not edit Story 4.1 artifacts in this planning
  stage;
- extend the final-image verifier to assert the gateway environment, no
  truncation metadata for complete queries, a fixture producing more than 100
  rows, the empty supported-language behavior, exact language aliases, and
  unchanged two-query snapshot/cleanup/forbidden-retention evidence.

### One closed final-image evidence and source binding

`specs/001-codegraph-extraction/contracts/repository-profiling-api.md` owns the
only `cgc-provider-contract-evidence-v2` grammar. The Python runner emits it,
and the C# verifier validates that exact closed root and every nested field;
neither component defines an alternate field-name set. The exact root uses
`schemaVersion`, `primaryFailureCategory`, `projectionObservations`, the
documented cleanup provider-state fields, and the documented
`forbiddenRetention` booleans. The raw safe-name allowlist includes
`parser_language`, `truncated`, and `result_limit`. The last two names are safe
to observe as property names but their presence in a purportedly complete
response still forces failure. Unknown root or nested fields, omitted required
fields, duplicate fields, wrong types, or alternate spellings fail.

The closed nested mapping is not left to runner/verifier interpretation.
`coverage.rowCounts` has exactly the bounded required integers `projectionA`,
`projectionB`, and `combined`. `coverage.completeness` has exactly the strict
required booleans `privateLimitExact`, `injectedTruncationRejected`,
`queryDriftRejected`, `languageConflictRejected`, and
`combinedBoundRejected`. The prior projection-specific truncation-absence
facts map only to `projectionObservations.A.shapeComplete` and
`projectionObservations.B.shapeComplete`: each `shapeComplete` is the stronger
conjunction of in-bound complete observation, exact closed envelope/row
validation, no unknown/duplicate/missing/wrong-typed field, and absence of
both reserved completeness fields. The old root `rowCounts`/`completeness`,
the old projection-truncation boolean names, and runner-era duplicate
`coverage.nodeKinds` are unknown fields. The Story 4.1 contract enumerates
every other observation/coverage member, alternative, bound, and pass
invariant; implementation copies that grammar rather than defining another
schema.

The final-image gate runs only at a clean immutable correction candidate. It
creates a deterministic production-source manifest containing the candidate
commit, Git tree, Dockerfile and `.dockerignore`, and every tracked file
included by the Docker build context after `.dockerignore`, each with a
Repository-relative path and SHA-256. The manifest itself is retained in the
claim; bounded provider evidence retains only its SHA-256. The image is built
fresh from that exact clean context with inspected labels for source commit,
source tree, and production-source-manifest SHA-256. The accepted-correction
claim and v2 evidence bind those three values plus the resulting image ID,
provider pin/lock/distribution, canonical query hashes, and verifier base image.
Reusing a pre-correction image or supplying an unlabelled/unmatched image
fails before provider execution.

Each accepted correction consumes a new immutable namespace. After a fresh
build, the C# verifier copies the canonical production queries and the Python
runner from the same candidate, runs the verifier image once, validates the
closed v2 evidence, and then reruns the full test project, Web build,
migration/model checks, diff check, and status check at that same candidate.
No stale image, evidence hash, or earlier accepted-boundary claim is completion
evidence.

### Framework detection

Infrastructure owns an embedded, version-controlled
`framework-catalog.v1.json` with closed schema and startup validation. Initial
normalized facts are `aspnet-core`, `blazor`, `spring-boot`, `react`,
`express`, and `django`. Entries contain exact ecosystem, package coordinate,
allowed parser-language tokens, normalized framework name, and supported
direct/lock evidence kinds. Exact initial coordinates are fixed in
[contracts/framework-detection.md](contracts/framework-detection.md);
matching is exact after ecosystem name normalization, never a wildcard or
substring. Ambiguous or duplicate catalog mappings fail startup.

Supported V1 evidence is deliberately closed:

- resolve one exact committed `HEAD` identity, stream
  `git ls-tree -r -z --full-tree <commit> --` incrementally, and retain only
  root or nested `*.csproj`, `pom.xml`, `package.json`, `package-lock.json`,
  `packages.lock.json`, and `requirements.txt` blobs; untracked, ignored, and
  dirty working-copy files are never evidence;
- bound the Git inventory before materialization to 16 MiB, 100,000 input
  records, 1,000 retained supported artifacts, 2,048 path characters, and a
  30-second deadline with caller cancellation; preserve each retained entry's
  committed object ID and mode, and fail closed with zero facts on an
  inventory bound;
- NuGet: literal unconditional `.csproj`
  `PackageReference`/`FrameworkReference`/SDK declarations
  and eligible committed `packages.lock.json`;
- npm: literal `package.json` `dependencies`, `devDependencies`, and
  `optionalDependencies` maps and eligible committed
  `package-lock.json` v2/v3;
- Maven: literal project-level `pom.xml` direct dependency coordinates only;
  profiles, parent/property interpolation, and `dependencyManagement` are
  unsupported;
- Python: literal package requirements in `requirements.txt` only; includes,
  URLs, editable entries, and environment markers are unsupported.

No Gradle script, generated `project.assets.json`, arbitrary MSBuild target,
unpaired lock, import, symbol, filename resemblance, or unsupported manifest
format is interpreted. More formats require a future catalog/parser change and
tests.

The retained Git record grammar is the exact NUL-delimited `ls-tree` record:
tree mode, object type, object ID, and Repository-relative path. A retained
artifact must be a blob and must be read by that exact object ID. Malformed
records, object-type/mode/ID drift, byte/record/artifact/path bounds, deadline,
or cancellation fail the Repository detector result with zero framework facts.
The former `git ls-files --cached` inventory is explicitly superseded and must
not be retained as an alternate or fallback evidence path.

XML evidence is namespace-closed. A `.csproj` root may use no namespace or the
legacy MSBuild namespace; a Maven root may use no namespace or the standard
Maven POM namespace. Every consumed relevant element must use its accepted root
namespace. A relevant foreign-namespace lookalike rejects only that artifact
with one bounded audit outcome and no fact; independent valid artifacts remain
eligible.

Each successfully parsed supported direct manifest establishes a scope at its
parent directory. A Code Unit is eligible only when its path is under that
scope and its language matches the catalog entry. The most-specific ancestor
scope for an ecosystem wins. Different ecosystems may coexist at one root and
apply only to matching languages.

Lock ownership is proven before any lock entry is parsed into
`PackageEvidence`:

1. enumerate the complete committed same-root owner-candidate set
   (`*.csproj` for NuGet; the exact `package.json` for npm);
2. require that candidate set to contain exactly one file;
3. safely read and successfully parse that file within the normal artifact
   bounds;
4. require the direct-manifest shape used to establish ownership to be
   supported and applicable. For NuGet, only a literal root `Project` `Sdk`
   attribute, literal direct `Project/Sdk` child, or literal
   `PackageReference`/`FrameworkReference` directly beneath a direct
   project-level `ItemGroup` can prove ownership. Declarations beneath
   `Target`, `Choose`, `When`, imported/evaluated content, or any other nested
   or execution-dependent construct never prove ownership. npm ownership uses
   the supported literal top-level direct dependency maps and supported package
   shape;
5. require the manifest and lock to share ecosystem and scope root, and apply
   eventual facts only to descendant Code Units with a catalog-compatible
   parser language.

No catalog match is required in the owner manifest: a valid literal direct
manifest may own a lock whose supported transitive entry supplies the only
framework fact. However, zero or multiple owner candidates, malformed JSON/XML,
an unsupported manifest shape/version, a conditional/dynamic NuGet owner, an
unsafe/oversize owner, or an owner that is not applicable to the scope makes
the lock ineligible. It emits one bounded closed audit outcome and no
lock-derived fact. A tracked filename or count alone never proves ownership,
and an invalid candidate is never silently discarded to manufacture a unique
owner.

For an owner whose only apparent declaration is nested or execution-dependent,
validation returns unsupported before lock entries can create evidence. The
matched lock creates zero `Medium` facts and exactly one bounded closed audit
outcome for the artifact-local lock/owner rejection; it retains no raw value,
source content, or absolute host path. Other valid Repository artifacts remain
eligible. A valid supported project-level owner remains valid even when it has
no direct catalog match, because ownership proof and framework matching are
separate.

The owner-grammar outcomes are testable as a closed matrix:

| Sole apparent NuGet ownership evidence | Owner result | Matched lock result |
| --- | --- | --- |
| literal root `Project` `Sdk` attribute | supported | eligible for normal lock matching |
| literal direct `Project/Sdk` child | supported | eligible for normal lock matching |
| literal `PackageReference` or `FrameworkReference` directly under a direct project-level `ItemGroup` | supported | eligible for normal lock matching |
| any allowed shape with a condition or dynamic value | conditional/dynamic | zero `Medium` facts; one bounded closed audit |
| declaration below `Target` | unsupported | zero `Medium` facts; one bounded closed audit |
| declaration below `Choose/When` | unsupported | zero `Medium` facts; one bounded closed audit |
| import-driven or any other execution-dependent declaration | unsupported | zero `Medium` facts; one bounded closed audit |

Every rejected row also proves that independent valid evidence in the same
Repository continues and that the audit retains no raw declaration value.

Candidate facts group by `(CodeUnitId, FrameworkName)`. `High` outranks
`Medium`; then deeper scope outranks shallower scope; then the ordinal
Repository-relative evidence locator sorts ascending. Persist the first.
This supplies one stable winner and preserves direct evidence when direct and
transitive evidence both justify the same fact.

Evidence locators contain only a normalized Repository-relative path plus a
closed locator:

```text
<path>#json:<pointer>
<path>#xml:<closed-coordinate>
<path>#line:<line>:<normalized-package-coordinate>
```

Paths are at most 2,048 characters and locators 4,096. Package coordinates are
retained only when they exactly match a catalog coordinate; arbitrary package
text and source content are not persisted.

An unsafe, malformed, unsupported, oversize, or ambiguous individual artifact
adds a sampled `ScanCandidateOutcome` reason under the closed
`FrameworkEvidence*` vocabulary and contributes to complete aggregate counts;
other valid artifacts continue. Repository-wide byte/artifact/entry/fact cap
overflow or an unexpected detector failure fails before the structural
transaction, retaining only already-persisted local candidate-filter audit.

## Data and Transaction Design

See [data-model.md](data-model.md) and [contracts](contracts/).

One in-memory `RepositoryProfileDraft` contains language-reconciled Code Units,
resolved Code Unit Edges, Unresolved Structural Relationship Candidates,
CodeUnitFramework facts, language summaries, and bounded candidate/evidence
outcomes. `IScanRunRepository` persists that complete draft and terminal
ScanRun in one EF transaction after provider cleanup and framework detection
succeed. Local pre-provider filter outcomes remain the only prior durable
records. If the final transaction fails after the tracked ScanRun was marked
Completed, the repository must roll back, clear/detach the failed draft, reload
the still-`Profiling` ScanRun in a fresh unit of work, and persist one safe
terminal failure separately, or one `Aborted` transition when cancellation
caused the rollback. The current tracked-state check cannot be used, because
its in-memory status may be Completed even though the transaction never
committed. Tests inject non-EF failure and cancellation both after mapping and
during final persistence, and prove no provider-derived row survives, local
filter audit remains, and the durable ScanRun reaches the correct terminal
state.

The existing additive migration
`20260720030657_AddLanguageFrameworkDetection` creates
`CodeUnitFrameworks` and `ScanRunLanguageSummaries`, their composite foreign
keys, unique constraints, checks, and indexes. The second existing additive
migration `20260720060442_FrameworkEvidenceAuditCount` adds the non-negative
`ScanRun.FrameworkEvidenceAuditTotalOutcomeCount` column so the complete count
required by the response contract survives reload when details are capped at
100; this changes no parsing or filtering behavior. Iteration-2 remediation
requires no schema change. Existing nullable `CodeUnit.Language` rows from
Story 4.1 are not guessed or backfilled.
New completed Story 4.2 runs enforce non-null language in Core, draft
validation, and persistence tests. Rollback drops only that added
check/column and the two new tables/indexes; it does not mutate historical Code
Units or filtered audit records. Deployment validation must back up the
database, apply the migrations in staging, run compatibility reads for
historical runs, then run a fresh Story 4.2 profile before production rollout.

## Implementation Ownership

```text
docker/codegraph/
└── provider gateway environment wiring                         # UPDATE

src/dotnet/Vulgata.Core/
├── Entities/
│   ├── CodeUnit.cs                                             # UPDATE language invariant/navigation
│   ├── CodeUnitFramework.cs                                    # NEW
│   ├── ScanRun.cs                                              # UPDATE summary/evidence ownership
│   └── ScanRunLanguageSummary.cs                               # NEW
└── DomainServices/
    ├── RepositoryProfilingContracts.cs                         # UPDATE language/evidence/detector draft
    └── IScanRunRepository.cs                                   # UPDATE atomic draft/failure recovery seam

src/dotnet/Vulgata.Infrastructure/
├── CodeGraph/CodeGraphContextProvider.cs                       # UPDATE limit, projection, parser
├── FrameworkDetection/
│   ├── FrameworkCatalog.cs                                     # NEW closed loader
│   ├── FrameworkEvidenceDetector.cs                            # NEW bounded detector
│   └── Catalog/framework-catalog.v1.json                       # NEW embedded catalog
├── Data/
│   ├── VulgataDbContext.cs                                     # UPDATE DbSets
│   ├── ScanRunRepository.cs                                    # UPDATE atomic aggregate write/read
│   ├── Configurations/
│   │   ├── CodeUnitFrameworkConfiguration.cs                   # NEW
│   │   └── ScanRunLanguageSummaryConfiguration.cs              # NEW
│   └── Migrations/
│       ├── 20260720030657_AddLanguageFrameworkDetection.*      # RETAIN
│       └── 20260720060442_FrameworkEvidenceAuditCount.*        # RETAIN
└── Vulgata.Infrastructure.csproj                               # UPDATE embedded resource

src/dotnet/Vulgata.Shared/Repositories/
└── ScanRunSummaryDto.cs                                        # UPDATE language/framework/audit DTOs

src/dotnet/Vulgata.Web/
├── Data/RepositoryProfilingCoordinator.cs                      # UPDATE reconcile/detect/summary
├── Api/Repositories/RepositoryApiEndpoints.cs                  # UPDATE existing result shape only
├── Components/Management/Repositories/RepositoryDetailDialog.razor # UPDATE Chinese overview
└── Program.cs                                                  # UPDATE detector/catalog DI only

tests/Vulgata.Tests/
├── CodeGraphContextProviderTests.cs or focused existing seam   # UPDATE
├── FrameworkDetectionTests.cs                                  # NEW
├── RepositoryProfilingTests.cs                                 # UPDATE
├── RepositoryApiTests.cs                                       # UPDATE
├── DashboardPanelTests.cs                                      # UPDATE dialog/component coverage
├── CodeGraphProviderContractTests.cs                           # UPDATE mandatory gate
└── CodeGraphProviderContract/{runner.py,ProviderFixture/}       # UPDATE final-image proof
```

No file above is authorized for implementation by this plan; task generation
must wait for independent plan acceptance and fresh QR9 planned-change
authorization.

## RED-First Verification Strategy

Groups 1-6 are historical execution groups. Their behavior remains required,
but unavailable raw observations are handled only through the Story 4.2
reconstruction exception below; they are not rerun and relabelled as
historical RED.

1. **Historical provider group**: the original baseline lacked Projection A
   language and could accept a provider-default 100-row truncation. Its GREEN
   behavior is the exact alias, per-file reconciliation, private `250001`
   environment, and fail-closed completeness metadata.
2. **Historical language group**: the original baseline persisted null
   languages and could retain a supported zero-structure file as a File Code
   Unit. Its GREEN behavior requires per-file parser tokens, distinct-file
   summaries, conflict atomicity, and exactly one
   `Filtered/UnknownLanguage`.
3. **Historical catalog/detector group**: direct, paired-lock transitive, nested scope,
   duplicate, strongest-confidence, ambiguous, unsupported, unsafe path,
   reparse, size/entry/fact cap, and non-execution cases preceded the closed
   parser/catalog behavior.
4. **Historical persistence group**: the original baseline lacked
   uniqueness/composite FK/summary storage, accepted an incomplete draft, and
   held a tracked Completed state after an injected final SaveChanges failure.
   Its GREEN behavior is the two current migrations, one atomic aggregate
   write, rollback/detach/reload, and a separate durable safe failure
   transition.
5. **Historical API/UI/authorization group**: the original overview lacked
   language, framework evidence, and unknown paths. Its GREEN behavior uses representative authorised
   API and component fixtures to assert exact equivalence with the accepted
   profile: every recorded language and distinct-file count, every framework
   with its strongest retained confidence and safe evidence source, every
   retained bounded `UnknownLanguage` path, and every required Simplified-
   Chinese label is present, with zero omission or developer interpretation
   needed to identify an accepted item. It also preserves unchanged 404
   redaction and verifies visibility revocation.
6. **Historical final-image group**: the original gate had to fail, not return
   early, when completion inputs were absent and had to reject the old 100-row
   limit/empty-file expectation. Its still-required GREEN behavior proves the
   exact pin/query hashes, more than 100 rows, empty supported-file behavior,
   parser-language bindings, atomic completeness, cleanup, and forbidden
   retention. Iteration-2 group 9 adds the corrected schema/source binding.
7. **Iteration-2 safety RED**: captured logging proves a raw absolute/escape
   provider path currently appears before normalization. GREEN makes the owned
   safe path the only value available to logs and persistence.
8. **Iteration-2 lock-owner RED**: NuGet and npm fixtures with one malformed,
   unsupported, unsafe, or conditional owner plus a matched lock currently can
   create `Medium` evidence. GREEN requires the exact validated-owner proof
   above while preserving a unique valid owner with lock-only catalog evidence.
9. **Iteration-2 evidence RED**: the current runner/C# names do not validate
   against the owned v2 schema, and an image lacking corrected source labels is
   rejected. GREEN uses one closed schema, a fresh consumed namespace, and
   exact source-manifest/image binding.
10. **Iteration-9 QR9 owner-grammar RED/GREEN**: after fresh plan acceptance
    and QR9 planned-change authorization, add assertions before production
    changes for a matched lock whose sole apparent owner is a
    `PackageReference` beneath `Target`, and another whose sole apparent owner
    is a `FrameworkReference` beneath `Choose/When`. Observe those assertions
    RED against the current descendant-search validator: they require no
    `Medium` fact, exactly one bounded closed audit without raw values, and
    continued independent valid evidence. GREEN restricts validation to the
    same three project-level shapes as direct parsing and proves both negative
    cases plus positive root-`Sdk`, direct-`Project/Sdk`, and direct
    project-level-`ItemGroup` owners. Rerun the complete framework detector and
    surrounding profiling suites. Compile failure, inferred failure, or tests
    added after production code do not satisfy RED.

### Corrected T001 current-verification contract

Regenerated `tasks.md` must replace the impossible ordered pre-correction
baseline clause in current T001 with one bounded present-day verification
record. T001 is complete only when that record:

1. states verbatim that the pre-correction baseline observation required by the
   superseded T001 was not captured before T002-T049, is temporally unavailable,
   and cannot be recreated by running commands now;
2. records an actual UTC observation time and one clean immutable Git commit and
   tree containing the accepted corrected plan set and regenerated tasks;
3. proves the accepted plan checkpoint is an ancestor of that commit, every
   planning-artifact SHA-256 equals the fresh accepted plan-review boundary, the
   fresh plan verdict is `ACCEPT`, and
   `evidence/planned-change-authorization-2.json` remains historical authority
   only for its accepted pre-QR9 owner/inventory scope, and the new post-review
   QR9 planned-change authorization is present before regenerated task
   generation or implementation;
4. runs the focused provider/profiling and framework-detection commands from
   `quickstart.md` against that exact commit and records their exit codes, raw
   output paths, and GREEN results as **current verification**;
5. contains no `historical`, `pre-correction`, `RED`, or contemporaneous-ordering
   claim for those current command results and does not amend/backdate any prior
   record; and
6. remains a prerequisite for regenerated dependent validation tasks and T050.

The accepted-plan proof in item 3 is executable, not a prose reference.
Regenerated tasks must carry the exact fresh verdict path and SHA-256, its
`reviewed_boundary.diff_sha256` and complete reviewed artifact-hash map, and
the stable plan-accepted `Workflow-Checkpoint` ID. The plan checkpoint is
assembled with a null commit SHA or pending binding, committed by the root with
that checkpoint trailer, and only then resolved to the unique immutable commit
and checked for ancestry. Neither this plan nor a precommit checkpoint record
may guess or embed the commit that will contain itself.

The accepted code candidate
`7b9ce590bd161b2cf4ed473a40ab3e9045654975` (tree
`29cf2469d6fd1a24fddd4353a7c4c7af618ce786`) and its GREEN evidence are
immutable prior inputs to reconstruction, not the future corrected-plan
candidate. They may be cited for what they currently prove, but they cannot
satisfy item 2 after these plan artifacts change and cannot be relabelled as the
missing pre-correction observation.

### Executable accepted-plan binding RED/GREEN

The corrected plan bytes make three executable constants stale today:
`CanonicalLfFiles` in
`tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` still contains the
prior accepted SHA-256 values for `plan.md`, `research.md`, and
`quickstart.md`. The same file still uses
`user-approved-story-4-2-plan-2026-07-20` as `_reviewIdentity`, and
`CreateAcceptedCorrectionManifest` publishes it into a new accepted-correction
claim. A fresh image alone cannot repair either stale binding.

Regenerated tasks must create one evidence-only TDD group in this order:

1. **Focused RED**: before changing the three `CanonicalLfFiles` values or the
   accepted-correction binding, run the canonical-LF hash theory and retain its
   actual failures for the three changed plan artifacts. Add a focused claim
   assertion that rejects the static 2026-07-20 identity and requires the exact
   fresh accepted plan verdict/checkpoint/diff binding, then observe that
   assertion fail against the unchanged manifest construction. The assertion
   edit is the only allowed pre-RED change; compile failure or an inferred
   failure is not RED.
2. **Binding GREEN**: replace exactly those three stale canonical hashes with
   the corresponding hashes from the fresh accepted plan verdict. Replace the
   static review identity in the accepted-correction claim with explicit
   `planReviewVerdictPath`, `planReviewVerdictSha256`,
   `planReviewDiffSha256`, `planCheckpointId`, and
   `planCheckpointCommit` fields. The tests must parse the named verdict,
   require `gate = plan-review`, `verdict = ACCEPT`, exact reviewed artifact
   hashes and diff identity, resolve the checkpoint ID through exactly one
   `Workflow-Checkpoint` trailer, require the resolved commit to contain the
   accepted plan/verdict checkpoint and be an ancestor of the new candidate,
   and reject the old identity, an alternate verdict, hash/diff drift, an
   unresolved/duplicate trailer, or ancestry failure.
3. **Surrounding GREEN**: rerun the focused canonical-hash/claim tests and the
   non-Docker `CodeGraphProviderContractTests` contract suite before pinning a
   new candidate. Preserve command, exit code, immutable source, observed
   assertion, and raw-output path for RED and GREEN separately.
4. **Final boundary**: because this is a tracked test/evidence-contract change,
   finish the current-verification record, regenerate the truthful
   reconstruction ledger, and independently validate both before pinning one
   new clean candidate. After that single pin, run both Full implementation
   review axes, the conditional E2E decision/result, verification, the
   production-source manifest, accepted-correction namespace, fresh image and
   exact final-image/full-suite gate, T049, and T050 without changing any
   tracked candidate-boundary byte.

The verdict binding is intentionally nonrecursive. A judge reviewing the
uncommitted three-file correction identifies it with the pinned base commit,
`candidate_commit: null`, exact binary diff SHA-256, reviewed artifact hashes,
and owned paths; the later verdict file and plan checkpoint are outside that
reviewed diff identity. The later test/evidence change may safely bind the
already-existing verdict hash and trailer-resolved plan-checkpoint commit. It
must not invent a future candidate or checkpoint commit.

### Story 4.2 historical workflow-evidence exception

The exception is a narrow evidence-disclosure rule for the already executed
Story 4.2 task groups. Task regeneration must define one durable reconstruction
ledger at
`specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json`.
For every historical task group it records the requirement/test mapping,
earliest Git commit containing the relevant tests, earliest commit containing
the production behavior, available workflow records and checkpoint trailers,
current immutable-candidate GREEN command/result, and explicit booleans for
whether original raw RED output and a valid contemporaneous checkpoint are
available.

The ledger may state `historicalRawRedAvailable: false` or
`checkpointTrailerAvailable: false`; it must not infer a RED result from a
later diff, synthesize output, backdate a file, amend a commit, or relabel a
current rerun. The exception does not waive fresh runnable RED-GREEN evidence
for AR2-001, QR2-001, AR2-002/CF-002, or EV-002, and it does not generalize to
Story 4.1 or any later story.

The regenerated ledger must also preserve the superseded T001 temporal fact
using the exact statement **“Pre-correction T001 baseline observation:
unavailable”**, with the superseded task reference and the T050 blocker
evidence. It links, but does not merge, the new current-verification record.
The current record has its own observation time, commit/tree, commands, results,
and raw outputs and is explicitly labelled **“Current verification”**.

T050 remains unchecked and incomplete while this plan is in review. After
fresh plan acceptance, task regeneration, current T001 verification, dependent
revalidation, and regeneration plus independent validation of the truthful
reconstruction ledger, the root pins one new immutable candidate. Both Full
reviews, conditional E2E, verification, source-manifest/image qualification,
T049, and T050 then run without changing that candidate boundary. T050 may pass
with the disclosed unavailable historical T001 observation and other
unavailable historical raw evidence under this exception, but it blocks on
fabricated claims, a current rerun labelled historical, missing current
correction RED/GREEN, schema/source-image mismatch, scope drift, or any other
failed audit item. Only then may a later workflow authority mark T050 complete
and create a new ready-for-review boundary.

### Downstream invalidation and regeneration

Editing `plan.md` or `research.md` invalidates the plan stage and every
dependent stage. AR9-001 reconciles authority bytes; QR9-001 also changes
planned Scan parsing/filtering behavior:

| Changed boundary | Invalidated evidence | Required next owner/action |
| --- | --- | --- |
| Corrected `plan.md`/`research.md` | stale `data-model.md`, `contracts/framework-detection.md`, and `quickstart.md` detail/authorization wording plus prior plan verdict/hash boundary | regenerate those dependent plan artifacts to carry the exact owner grammar, QR9 regressions, and fresh authorization gate, then pin the complete unchanged-specification plan set for a fresh `plan-judge`; do not change specification scope |
| Fresh complete-plan `ACCEPT` | task generation remains blocked | root obtains the mandatory QR9 planned-change authorization described above |
| Accepted plan set plus fresh QR9 authorization | current `tasks.md` and task markers | fresh `task-author` regenerates dependency-ordered tasks, including corrected T001 and QR9 behavior-level RED/GREEN |
| Regenerated tasks | prior analyze reports | fresh read-only `artifact-analyst` reruns `speckit-analyze` |
| QR9 owner-validator production/test correction | prior ledger, candidate, reviews, E2E, verification, manifest/image/namespace, T049/T050 | fresh TDD implementer observes RED, implements minimal GREEN, reruns surrounding tests, and regenerates every boundary-dependent claim |
| Corrected T001/process contract | current reconstruction ledger and T049/T050 conclusions | later evidence owner records current verification, regenerates/revalidates the ledger, and reruns its strict audit |
| `CodeGraphProviderContractTests.cs` canonical hashes/claim binding | prior implementation reviews, E2E decision/result, final verification, reconstruction ledger, candidate identity | observe focused stale-binding RED, implement binding GREEN, rerun surrounding tests, then pin and review the new boundary |
| Any new committed candidate | prior acceptance and quality verdicts, E2E decision/result, final verification | rerun both Full implementation reviews, conditional E2E decision/result, and verification against one pinned boundary |
| Plan/research/quickstart and tracked test/evidence bytes in Docker context | prior production-source manifest, image labels/ID, accepted-correction namespace, final-image evidence, T049 freeze | create a new clean committed candidate, manifest, fresh image and namespace, then rerun the mandatory final-image gate, full suite, and T049 |
| New T049 freeze | blocked T050 | fresh T050 audits T001 through T049 and the new immutable boundary |

The mandatory downstream sequence is:

1. obtain a fresh independent complete-plan `ACCEPT`;
2. obtain explicit QR9 planned-change authorization before task generation;
3. regenerate dependency-ordered tasks;
4. run fresh read-only `speckit-analyze`;
5. observe the QR9 owner-grammar RED and focused stale-binding RED, implement
   minimal GREEN, and rerun surrounding contract tests;
6. regenerate the **Current verification** record and truthful reconstruction
   ledger, then complete independent ledger validation, with every tracked
   source, test, contract, planning, and candidate-bound evidence byte final;
7. pin exactly one clean immutable candidate boundary;
8. run both fresh Full implementation review axes against that identical
   boundary;
9. run the current-boundary conditional E2E decision/result;
10. run final verification and read-only ledger/current-gate cross-validation;
11. create the production-source manifest and unused correction namespace,
    build and inspect the fresh image, and run the exact final-image gate plus
    full suite;
12. run T049 without changing the candidate boundary; and
13. run T050 last against the unchanged T049 inputs.

After step 7, any change to a tracked candidate-boundary file—including the
current-verification record, reconstruction ledger, implementation, test,
contract, planning, or Docker-context evidence bytes—invalidates the candidate,
both Full review verdicts, conditional E2E decision/result, verification,
source manifest, namespace/image/final-image evidence, T049, and T050. Complete
the correction and all candidate-bound evidence writes, repin at step 7, and
rerun steps 8-13. Post-pin review, E2E, verification, manifest/image, T049, and
T050 work must not mutate tracked candidate-boundary bytes.

No replay from a pre-correction worktree is required by this corrected evidence
contract. No prior result is silently carried forward: reusable immutable
evidence is cited as historical input, while every boundary-dependent verdict,
hash, manifest, image, and audit is regenerated.

Required commands and expected observations are in [quickstart.md](quickstart.md).

## Requirement / Design / Test Traceability

| Requirements | Design owner | RED-first evidence |
| --- | --- | --- |
| FR-001..003, SC-001 | Provider projection + language normalizer + CodeUnit invariant | exact token/open-identifier/mismatch fixtures |
| FR-004, FR-007, SC-004 | coordinator candidate reconciliation + existing outcome audit | zero-structure/missing identity/exactly-one/no-Code-Unit tests |
| FR-005..006, SC-002 | `ScanRunLanguageSummary`, DTO, existing dialog | multi-unit-per-file/API/component tests |
| FR-008..014, FR-021, SC-003/005 | closed catalog, scoped detector, confidence/tie-break | direct/transitive/scope/duplicate/ambiguity/no-LLM tests |
| FR-015..017, SC-006 | evidence bounds + repository draft transaction | escape/reparse/cap/provider failure/rollback tests |
| FR-018..019, SC-007 | existing API groups/coordinator visibility/Chinese UI | role, hidden Repository, revocation, Chinese rendering tests |
| FR-006..007, FR-012..013, FR-019, SC-008 | authorised summary DTO + existing dialog | representative authorised API/component fixtures assert 100% of languages/counts, strongest framework confidence/safe evidence, all bounded unknown paths, and required Chinese labels, with zero omissions or developer interpretation |
| FR-020 | explicit ownership exclusion | architectural source-diff test/review: zero orchestration/tool/dispatch changes |
| Story 4.1 completeness correction | gateway env + envelope parser + final-image verifier | 101+ rows, truncation metadata, exact two-query and cleanup tests |
| AR2-001 | safe provider path invariant before logger/outcome construction | captured logger + durable/API absence of raw absolute/escape paths |
| QR2-001 | validated direct-manifest owner model before lock parsing | malformed/unsupported/conditional/unsafe NuGet and npm owner fixtures |
| AR9-001 | pinned committed-tree inventory authority | exact `git ls-tree -r -z --full-tree <commit> --`; six retained shapes; incremental bounds; mode/type/blob identity; explicit `git ls-files --cached` supersession |
| QR9-001 | NuGet project-level lock-owner grammar | RED for Target and Choose/When nested owners; GREEN for zero Medium fact/one bounded audit/independent continuation and all three valid project-level shapes |
| AR2-002, CF-002 | one closed v2 evidence grammar across contract/runner/verifier | schema-name/safe-name/unknown-field and fresh namespace tests |
| EV-002 | candidate commit/tree/source-manifest labels bound to fresh image/evidence | clean candidate build, inspected labels, fresh exact-image gate, full suite |
| AR2-003, QR2-002, WF-002 | T050 remains incomplete until immutable-candidate audit | corrected T001 current-verification record plus regenerated reconstruction ledger and T049/T050 |
| PL-001, PA-001, T001-PLAN-001 | fresh plan-review and regenerated dependent artifacts | focused stale-binding RED; fresh canonical hashes; exact verdict/checkpoint/diff-bound claim; no stale accepted-boundary claims |
| TR-001 | Story 4.2-only truthful historical reconstruction exception | explicit unavailable T001/historical flags; separately timed current verification; no fabricated/backdated/amended evidence |

## Project Structure

### Documentation (this feature)

```text
specs/002-language-framework-detection/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── provider-language-projection.md
    ├── framework-detection.md
    └── repository-scan-overview.md
```

### Source Code

The existing multi-project web application is retained. Core defines domain
truth; Infrastructure owns provider, framework detection, EF, and migrations;
Shared owns transport DTOs; Web composes the authorized profiling UI/API; the
single Tests project mirrors these seams.

## Complexity Tracking

No Constitution violation requires justification. AR9-001 restores one
committed-tree inventory authority and QR9-001 narrows ownership proof to
non-executing project-level declarations with artifact-local failure. The
historical
workflow-evidence exception changes only what can be truthfully claimed about
unavailable past workflow evidence. The corrected T001 substitutes
current-boundary verification for an impossible temporal prerequisite without
calling it historical; it does not weaken product tests, authorization,
security, current correction TDD, source/image binding, or any Constitution
gate.
