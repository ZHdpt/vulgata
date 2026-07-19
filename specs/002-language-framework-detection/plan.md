# Implementation Plan: Language and Framework Detection

**Branch**: detached at `492008ebd787ff71d45054db89b5e0303997c61f` | **Date**: 2026-07-19 | **Spec**: [spec.md](spec.md)
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
| IV. Test-First Integration Harness | Every behavior begins with a named failing test; integration tests cover persistence/API/UI/authorization and the exact final image proves the provider correction. | Pass | Pass |
| V. Security & Data Stewardship | All analysis stays local and non-executing; provider gateway remains loopback-only; paths, evidence, diagnostics, and response sizes are bounded and redacted. | Pass | Pass |

No Constitution exception or ADR conflict was found. The July architecture's
sampled LLM language/framework output is a separate later hypothesis or
human-review aid only; it cannot create or replace a Story 4.2 fact or make an
`UnknownLanguage` candidate dispatchable.

## Authority and Planned-Change Disposition

- **Accepted intent**: the remediation recorded in
  `evidence/plan-blocker-1.json` is authoritative and resolves the pinned
  provider feasibility issue without changing provider version or fork.
- **PRD**: no requirement changes. PRD FR-4.1 still requires deterministic
  language/framework profiling before Worker Agent dispatch. Retaining new
  valid provider identifiers does not promise Worker Agent support beyond the
  V1 demo languages and therefore does not weaken PRD out-of-scope language
  coverage.
- **Planned-change trigger**: **yes**. This plan materially changes Scan parsing
  and filtering by adding structural-node parser-language reconciliation and
  filtering candidates with no observable identity. After independent plan
  acceptance, the orchestrator must pause for the explicit `AGENTS.md`
  planned-change authorization before `/speckit-tasks`.
- **No other trigger**: no Worker Agent tool is added or changed, and no PRD
  requirement is changed, weakened, or contradicted.

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

### Story 4.1 technical-contract correction

The current private HTTP handler defaults `execute_cypher_query` to 100 rows
and, when truncating, reports the shortened `record_count` plus additive
`result_limit` and `truncated` fields. Story 4.1 currently ignores additive
envelope fields, so a partial response can look complete.

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

- inventory only paths returned by `git ls-files --cached`; untracked or
  ignored working-copy files are never evidence;
- NuGet: literal unconditional `.csproj`
  `PackageReference`/`FrameworkReference`/SDK declarations
  and paired committed `packages.lock.json`;
- npm: literal `package.json` `dependencies`, `devDependencies`, and
  `optionalDependencies` maps and paired committed
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

Each manifest establishes a scope at its parent directory. A Code Unit is
eligible only when its path is under that scope and its language matches the
catalog entry. The most-specific ancestor scope for an ecosystem wins. Two
same-ecosystem manifests at the same winning root, an unpaired lock, or an
unprovable scope is ambiguous and creates no fact. Different ecosystems may
coexist at one root and apply only to matching languages.

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
terminal failure separately. The current tracked-state check cannot be used,
because its in-memory status may be Completed even though the transaction
never committed. Tests inject final-commit failure and prove no provider-derived
row survives, local filter audit remains, and the durable ScanRun is Failed.

The additive migration creates `CodeUnitFrameworks` and
`ScanRunLanguageSummaries`, their composite foreign keys, unique constraints,
checks, and indexes. Existing nullable `CodeUnit.Language` rows from Story 4.1
are not guessed or backfilled. New completed Story 4.2 runs enforce non-null
language in Core, draft validation, and persistence tests. Rollback drops only
the two new tables/indexes; it does not mutate historical Code Units or
filtered audit records. Deployment validation must back up the database,
apply the migration in staging, run compatibility reads for historical runs,
then run a fresh Story 4.2 profile before production rollout.

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
│   └── Migrations/<additive Story 4.2 migration>               # NEW
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
must wait for independent plan acceptance and planned-change approval.

## RED-First Verification Strategy

1. **Provider RED**: prove current Projection A has no language and current
   parser accepts a truncated 100-row response. GREEN adds the exact alias,
   per-file reconciliation, private `250001` environment, and fail-closed
   completeness metadata.
2. **Language RED**: prove a mixed-language provider result currently persists
   null languages and a zero-structure supported file currently becomes a File
   Code Unit. GREEN requires per-file parser tokens, distinct-file summaries,
   conflict atomicity, and exactly one `Filtered/UnknownLanguage`.
3. **Catalog/detector RED**: direct, paired-lock transitive, nested scope,
   duplicate, strongest-confidence, ambiguous, unsupported, unsafe path,
   reparse, size/entry/fact cap, and non-execution cases fail before detector
   implementation. GREEN introduces the smallest closed parsers/catalog.
4. **Persistence RED**: prove absent uniqueness/composite FK/summary storage,
   incomplete draft acceptance, and the current tracked Completed state after
   an injected final SaveChanges failure. GREEN adds migration/configurations,
   one atomic aggregate write, rollback/detach/reload, and a separate durable
   safe failure transition.
5. **API/UI/authorization RED**: prove the existing overview lacks language,
   framework evidence, and unknown paths. GREEN adds authorized DTO/UI output,
   Chinese labels, unchanged 404 redaction, and visibility-revocation checks.
6. **Final-image RED**: the gate must fail, not return early, when its required
   environment is absent in the completion lane; it must fail with the old
   100-row limit and old empty-file expectation. GREEN proves exact final image,
   exact pin/query hashes, more than 100 rows, empty supported file behavior,
   parser-language bindings, atomic completeness, cleanup, and forbidden
   retention.

Required commands and expected observations are in [quickstart.md](quickstart.md).

## Requirement / Design / Test Traceability

| Requirements | Design owner | RED-first evidence |
| --- | --- | --- |
| FR-001..003, SC-001 | Provider projection + language normalizer + CodeUnit invariant | exact token/open-identifier/mismatch fixtures |
| FR-004, FR-007, SC-004 | coordinator candidate reconciliation + existing outcome audit | zero-structure/missing identity/exactly-one/no-Code-Unit tests |
| FR-005..006, SC-002/008 | `ScanRunLanguageSummary`, DTO, existing dialog | multi-unit-per-file/API/component tests |
| FR-008..014, FR-021, SC-003/005 | closed catalog, scoped detector, confidence/tie-break | direct/transitive/scope/duplicate/ambiguity/no-LLM tests |
| FR-015..017, SC-006 | evidence bounds + repository draft transaction | escape/reparse/cap/provider failure/rollback tests |
| FR-018..019, SC-007/008 | existing API groups/coordinator visibility/Chinese UI | role, hidden Repository, revocation, Chinese rendering tests |
| FR-020 | explicit ownership exclusion | architectural source-diff test/review: zero orchestration/tool/dispatch changes |
| Story 4.1 completeness correction | gateway env + envelope parser + final-image verifier | 101+ rows, truncation metadata, exact two-query and cleanup tests |

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

No Constitution violation requires justification.
