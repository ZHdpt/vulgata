# Implementation Plan: Lightweight Java/XML PreScan & Prepared Scan Manifest

**Branch**: `demo0831/story-12-2` | **Date**: 2026-08-20 | **Spec**: [spec.md](spec.md)

**Input boundary**: source commit `6fe7d4a88ff344b1d71329d19fdb9d6d6d64b7e6`; specification SHA-256 `c15d03b2b10e4eaa452a59e3b96acafdbb04160de556ca93c28380117a085ad3`; checklist SHA-256 `070affb70472c8d264cdf06ec5e06412e0ef06119bb7be388ce385351a5d8150`; authorized fifth correction source `story-12-2-definition-review-fifth-correction-authorization-01`; raw definition-review attempt 4 verdict `REVISE`.

## Summary

Story 12.2 adds a versioned, deterministic preparation path for exactly two authorized Java/XML Repositories. It acquires private exact Git snapshots, enumerates and hashes only safe contained Java/XML bytes, builds fresh revision-bound CodeGraph indexes in separate disposable analysis workspaces, proves pinned JDT-LS readiness where Java exists, extracts conservative communication evidence through one safe bounded XML projection and a closed catalog, and plans exact pending File Code Units using mandatory high-value seeds, a canonical lowercase UUID-text Repository comparator, and distinct per-manifest and pair-global ordinals.

After both Repository drafts are complete, one PostgreSQL transaction persists both Prepared Scan Manifests, ordered audits and File Code Units, Communication Kind Classifications and totals, Scan Context Snapshots, v2 Pre-Scan Results, and approved prepared-handoff eligibility. Exact retry is mutation-free; any changed value conflicts; any failure exposes no prepared package. No LLM, Worker, `navigate_code`, Scan Job Execution, dispatch policy, Document, Edge, Uncertainty, Pass 2, or Chat behavior is added.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: EF Core 10.0.9, Npgsql 10.0.3, ASP.NET Core minimal APIs, FluentValidation, `System.Xml`, Story 12.1 closed ports, `@colbymchenry/codegraph@1.5.0`, Eclipse JDT LS `jdt-language-server-1.61.0-202608141332`, Java 21, StreamJsonRpc 2.25.29

**Storage**: PostgreSQL 17 `vulgata` schema; additive EF migration; configured contained Git/CodeGraph/JDT workspaces are rebuildable and not domain state

**Testing**: xUnit in `tests/Vulgata.Tests`; public-seam doubles; neutral Java/XML fixtures; disposable PostgreSQL 17; real exact Git/CodeGraph/JDT-LS qualification

**Target Platform**: Windows host for Vulgata and analysis tools; PostgreSQL 17 is the only required Docker service

**Project Type**: Layered .NET web application with Core domain/contracts, Infrastructure persistence/tool/file adapters, Shared API contracts, and Web orchestration/API composition

**Performance Goals**: CodeGraph complete proof <30 seconds/Repository; JDT readiness <60 seconds when applicable; PreScan <2 minutes/Repository; paired retained File Code Units 50-150

**Constraints**: exact raw-byte identity; stable prepared-context identity excludes legacy time-derived ID/CapturedAt; 4 MiB XML hard audit; 65,536-byte/2,000-line units; 256 observations; at most 20 mandatory high-value seeds per Repository, two hops, 200 expanded files, five seconds; hard analysis readiness fails closed; only post-readiness related-file expansion may fall back; versioned canonical envelopes govern fingerprints/replay/derived UUIDs; no source/host path in evidence; no LLM or Story 12.3 behavior

**Scale/Scope**: exactly two release Repositories and one immutable prepared package per Repository/Scan Run; general 10,000-file acceptance remains deferred

## Constitution Check

*GATE: passed before research and rechecked after design.*

| Principle | Design response | Result |
| --- | --- | --- |
| I. Layered Project Boundaries | Core owns entities, pure planning, and narrow ports; Infrastructure owns EF/Git/XML/process/workspaces; Shared owns validated API DTOs; Web owns authorized orchestration/API. | PASS |
| II. Domain Purity & Ubiquitous Language | Uses Profiled Revision, Prepared Scan Manifest, File Code Unit, Scan Context Snapshot, Pre-Scan Result/Handoff, Communication Kind Classification, and Evidence Locator verbatim. EF mapping remains separate; no Document mutation occurs. | PASS |
| III. Validation & API Contract Discipline | The one plural minimal API accepts closed exact inputs through FluentValidation and returns ProblemDetails; no data annotations/envelope. | PASS |
| IV. Test-First Integration Harness | Every decision has a public behavioral seam; PostgreSQL, real tools, neutral fixtures, authorization, replay, and failure injection are explicit. | PASS |
| V. Security & Data Stewardship | All source remains local; XML execution/network capabilities are prohibited; raw tool protocols/paths/source do not cross ports; no LLM call occurs. | PASS |

The dated August 18 host topology changes only the August release gate and retains single-container NFR-4.1 as post-deadline V1 work, matching the accepted Story 12.1 reconciliation; no Constitution exception is required. `Prepared Scan Manifest`, `File Code Unit`, and `Scan Context Snapshot` are acknowledged current authoritative glossary entries and are used without local adoption caveats. No `docs/adr/`, context-scoped ADR, `CONTEXT.md`, or `CONTEXT-MAP.md` exists in the pinned tree, so no ADR/context conflict can be evaluated.

## Project Structure

### Planning artifacts

```text
specs/011-java-xml-prescan-manifest/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── preparation-api.md
    ├── canonical-envelope.md
    ├── prepared-scan-contracts.md
    └── safe-analysis-contracts.md
```

### Intended production and test ownership

```text
src/dotnet/
├── Vulgata.Core/
│   ├── Entities/                  # manifest/audit and additive File Code Unit/result/classification shape
│   └── DomainServices/            # preparation planner/store/handoff and enriched closed analysis contracts
├── Vulgata.Infrastructure/
│   ├── Git/                       # full-ref exact acquisition + Story 12.2 enumerator/raw artifacts
│   ├── Analysis/CodeGraph/        # revision workspace, complete proof, bounded typed expansion
│   ├── Analysis/Java/             # readiness/import proof and exact symbol ranges
│   ├── CommunicationDetection/    # strict catalog + safe XML evidence extraction
│   └── Data/                      # prepared store/configurations/migration/model snapshot
├── Vulgata.Shared/                # paired preparation request/response + FluentValidation
└── Vulgata.Web/
    ├── Api/Scans/                 # prepared-scans endpoint
    ├── Data/                      # authorized paired preparation coordinator
    └── Program.cs                 # DI only; no CGC

tests/Vulgata.Tests/
├── PreparedScanning/              # deterministic plan, safety, split, ordering, atomicity/replay/auth
├── AnalysisRuntime/               # enriched CodeGraph/JDT contract and real-tool qualification
├── Migrations/                    # PostgreSQL lifecycle/compatibility/concurrency
└── Fixtures/PreparedScanning/     # neutral Java/XML/catalog/git fixtures
```

**Structure Decision:** Deepen existing projects and Story 12.1 ports. Do not add a project, generic repository, agent tool, or parallel scheduling model.

## Architecture Decisions

### AD-1: Separate Story 12.2 preparation from historical profiling

Add a new paired preparation coordinator, enumerator, manifest store, and v2 handoff projection. Reuse exact Git, analysis runtime, Scan Context Snapshot, and durable transaction disciplines; retain the existing broad `RepositoryProfilingCoordinator`, v1 Pre-Scan Result, and v1 handoff for historical data. Consequence: compatibility is additive and no incomplete v1 row can satisfy Story 12.2.

### AD-2: Immutable source snapshot plus disposable mutable CodeGraph workspace

Hold exact Git leases through commit. Copy verified source to a contained analysis root; CodeGraph may mutate only `.codegraph`. Verify all non-index bytes against the snapshot before/after every required phase. Consequence: CodeGraph private state is rebuildable and excluded from the manifest while its proof remains revision-bound.

### AD-3: Closed readiness proof, not coarse health

Enrich the CodeGraph status result with every completeness predicate and JDT-LS with typed initialize/initialized/import/document-symbol proof. Each approved proof persists exact stable typed fields and hashes its own versioned canonical envelope; the manifest references only `CodeGraphProofIdentityHash` and `JavaReadinessIdentityHash`. Measured duration and volatile telemetry remain bounded first-commit audit metadata outside identity/replay. Compilation diagnostics are nonfatal. Any index/rebuild/status/runtime/identity/binding/path/completeness/readiness failure or timeout rejects preparation; XML-only records the explicit Java `NotApplicable` identity. Only a related-file expansion failure occurring after hard readiness has passed may produce one bounded Warning and deterministic direct-order fallback. Consequence: repeated executions may differ operationally while stable readiness identity remains byte-identical.

### AD-4: Safe XML projection and closed catalog

Use strict encoding detection, bounded `XmlReader`, no resolver/DTD/validation, explicit XInclude rejection, one immutable node projection, and a byte-mapped non-evaluating range index. The safe XML owner alone produces an immutable file/hash-bound `SafeXmlDirectChildRangeSet` after parse/index agreement. Load a versioned embedded catalog using only exact allowed grammar. Consequence: rules cannot execute Repository input or network activity; unsafe/ambiguous files become Warning/skip rather than Confirmed; XML split planning cannot precede or duplicate safe range construction.

### AD-5: Exact File Code Units through additive CodeUnit fields

Use `CodeUnitType.File`, exact ranges and deterministic IDs. Preserve historical CodeUnit fields/rows; new nullable fields become conditionally required for manifest-owned units. Java splitting consumes exact JDT ranges after Java readiness; XML splitting consumes only a completed `SafeXmlDirectChildRangeSet`, creating an explicit safe-XML-before-XML-split dependency. File Code Unit identity is derived from the canonical identity envelope and excludes both `ManifestOrdinal` and `PairSelectionOrdinal`; both ordinals remain required persisted selection coordinates. Entire oversized files are skipped when all exact safe segments cannot be proven. Consequence: no unsafe placeholder/duplicate XML range parser is possible, and order changes do not rename identical File Code Units.

### AD-6: Pure deterministic business ordering

Core pure logic assigns direct scores first. Per Repository, direct score `>= 400` defines high-value candidates and the first at most 20 by descending direct score, exact path, then segment are mandatory high-value seeds; only those seeds drive bounded expansion. Pair ordering is descending final score, then Repository ID rendered as canonical lowercase 36-character UUID `D` text using ordinal comparison, then exact path and segment. Retain every mandatory seed, fill remaining capacity to 150 from nonmandatory pair order, and reject fewer than 50. Each manifest assigns contiguous `ManifestOrdinal` `1..N`; the pair assigns contiguous `PairSelectionOrdinal` `1..M`. Consequence: scheduling/database timing cannot affect selection; the corrected cross-Repository and over-cap golden cases are normative tests.

### AD-7: Pair-wide one-transaction approval

Persist both Repository packages in one PostgreSQL transaction after both exact leases pass final verification and ownership is rechecked. Each selected File Code Unit receives one non-null prepared-only Manifest Entry row; selected audits use its valid composite principal key, while historical CodeUnit rows retain nullable new fields and no alternate-key obligation. Persist every audit envelope preimage and recompute digests from columns on insert/replay. Consequence: the schema is EF/PostgreSQL-feasible, same-manifest association is exact, and corrupted digests cannot satisfy replay.

### AD-8: Prepared approval grants no dispatch

The new v2 read source is not registered with the historical sequential runner and Story 12.2 creates no Scan Job Execution. Story 12.3 must later explicitly consume the accepted manifest and define job/lease/retry/dispatch behavior. Consequence: an approved handoff certifies input only.

### AD-9: Versioned canonical envelopes close identity and replay

All CodeGraph completeness proofs, Java readiness outcomes, Prepared Scan Context Snapshot identities, manifests, pair-selection/packages, classifications, audits, File Code Units, fingerprints, and deterministic-UUID bytes use [contracts/canonical-envelope.md](contracts/canonical-envelope.md). Stable context identity contains Repository/Scan Run/Profiled Revision/schema/content hash and excludes legacy ContextSnapshotId/CapturedAt. The dependency graph is acyclic: stable proof/context envelopes produce hashes referenced by a manifest; a manifest contains the pair-selection fingerprint but not the pair package; the pair package contains ordered manifests. Consequence: independent reconstruction can prove equality without production serializers, database row order, execution timing, or capture timing.

## Persistence and Migration

Use one cohesive additive migration under one later integration owner. Add `PreparedScanManifestEntries` with non-null selection fields and valid keys; audits gain nullable fingerprint, pair ordinal, and `PreparedScanManifestEntryId`. `PreparedScanManifestEntryConfiguration` owns the principal constraints; `PreparedScanAuditConfiguration` owns dependent shapes/composite FK. CodeUnit nullable fields remain non-key and historical rows need no backfill. PostgreSQL lifecycle proves create/rollback/reapply/model snapshot/legacy null compatibility; atomic store tests prove entry/audit insertion, reconstruction, mutation, and rollback.

## Failure and Replay Invariants

- No source, analysis, XML, split, selection, authorization, or persistence failure returns an approved handoff.
- Every CodeGraph/JDT-LS hard-readiness failure rejects the pair; only post-readiness related-file expansion failure records a deterministic Warning and uses direct order.
- Every hard file failure creates one bounded audit/Warning and no partial segments.
- The pair transaction is the only prepared visibility boundary.
- Canonical equality includes every ordered audit preimage and recomputed digest. `Selected` audit requires fingerprint+pair ordinal+matching selected FK; `Eligible` requires fingerprint with null ordinal/FK; `Filtered|Skipped|Warning` require all three null. Path alone never associates a segmented File Code Unit.
- Existing v1 handoffs remain readable but never satisfy v2.
- Zero LLM calls and zero Worker/Document/Edge/Uncertainty mutations are structural tests, not operational assumptions.

## Requirement and Source Traceability

| Plan area | Spec coverage | Primary authority |
| --- | --- | --- |
| AD-1/2; exact acquisition/inventory/hash | FR-001-FR-009; SC-001/002 | Story 12.2 AC-1; August design §§2-4; Story 12.1 exact Git seam; PRD FR-4.1 dated override |
| AD-2/3/9; CodeGraph/JDT stable proof identity | FR-010-FR-015, FR-035, FR-040, FR-042; SC-001-004/008 | August design §§3-5; August 18 proposal §§3-5; Story 12.1 typed ports; PRD FR-4.9, FR-15.1-15.3, NFR-4.1 exception |
| AD-4; XML/catalog/classification | FR-016-FR-024; SC-006/007 | August design §§4.2, 5; PRD FR-5.8 dated override, NFR-2.3 |
| AD-5/6; units/splits/scores/order | FR-025-FR-035; SC-001/005/006 | August design §§4.1/4.3; corrected spec golden cases; PRD FR-4.1/4.9 dated override |
| AD-7/8/9; manifest/context/result/handoff/replay | FR-022, FR-033-FR-041; SC-001/002/008 | August design §§4, 8-10; Story 12.1 durable contracts plus additive stable context identity; canonical-envelope contract; PRD NFR-3.3 |
| Explicit exclusions | FR-041-FR-043; SC-009 | Epic 12 Stories 12.2/12.3; PRD FR-4.2-4.5, FR-4.12, FR-6.1/6.2, FR-11.6 reserved for later stories |

All 43 FRs and nine SCs are covered. PRD NFR-1.1 general 10,000-file scale remains deferred by the approved dated overlay; NFR-3.1/3.2 Worker retry/provider pause are Story 12.3; NFR-3.3 durable prepared-state atomicity is implemented here; NFR-4.1 remains post-deadline.

## Exact Production/Test Ownership Forecast

| Ownership group | Planned paths | Behavioral proof |
| --- | --- | --- |
| Core manifest model/planner | `Entities/PreparedScanManifest.cs`, `Entities/CodeUnit.cs`, `Entities/PreScanResult.cs`, `Entities/CommunicationKindClassification.cs`, `DomainServices/PreparedScanContracts.cs`, `DomainServices/IPreparedScanStore.cs`, `DomainServices/AnalysisRuntimeContracts.cs`, canonical-envelope writer/validator | independent envelope bytes/digests/UUIDs, dual ordinals, bounds, score/ties, 50-150, v1/v2 separation |
| Exact source | `Infrastructure/Git/ExactGitRevisionSnapshotService.cs`, new `PreparedScanCandidateEnumerator.cs` | moved ref, checkout isolation, collisions/escapes/reparse, exact raw hashes, repeatability |
| Prepared entries and canonical audits | `PreparedScanManifestEntry` + configuration; `PreparedScanAuditFactory` + configuration; migration/store | non-null EF principal feasibility, historical nullable CodeUnit compatibility, all audit preimages, same-manifest composite FK, lifecycle and atomic reconstruction/mutation |
| Analysis | `Core/DomainServices/AnalysisRuntimeContracts.cs`, `Infrastructure/Analysis/CodeGraph/*`, `Infrastructure/Analysis/Java/JdtLsAnalysisAdapter.cs` | typed stable proof fields/hashes, literal envelopes, real complete status, workspace binding, import/symbol readiness, duration variance, stable mutation, timeout/malformed/fallback |
| XML/catalog and XML range owner | new files/resource under `Infrastructure/CommunicationDetection/` and its csproj resource entry | safe projection/range-index GREEN produces `SafeXmlDirectChildRangeSet` before XML split consumption; every grammar/prohibition/bound, exact locator/range, sample-literal absence |
| Persistence | new configurations/store plus `VulgataDbContext.cs`, one migration/designer/model snapshot | `PreparedScanAtomicReplayTests` owns exact replay semantics; separately mandatory `PreparedScanAtomicPostgreSqlTests` proves relational reconstruction/atomicity; migration suite stays separate |
| Public seam | Shared request/validator/response; `Web/Data/PreparedScanCoordinator.cs`; `Web/Api/Scans/PreparedScanEndpoints.cs`; `Program.cs` | 400/401/403/404/409/422/503, ownership recheck, no dispatch/LLM |
| Tests/fixtures | focused PreparedScanning/AnalysisRuntime/Migrations additions and neutral fixtures | release qualification plus `PreparedScanProductionLiteralQualificationTests.cs`; evidence record at `artifacts/qualification/story-12.2/production-literal-inspection.json`; qualification failures route to a new behavioral regression RED/GREEN |

## Planned-Change Authorization Trigger Matrix

After independent plan-review `ACCEPT`, pause before tasks.

| Trigger | Proposed effect and implementation intent | Affected plan headings | Affected PRD clauses |
| --- | --- | --- | --- |
| Material Scan parsing/filtering | New exact Java/XML enumerator, strict path/filter grammar, raw-byte hashing, safe XML/catalog pipeline, whole-file skip rules | AD-1, AD-2, AD-4, Failure Invariants | FR-4.1; FR-5.8; NFR-2.3 |
| Material Scan ordering/splitting | File Code Units, exact JDT/XML splits, mandatory high-value seeds, lowercase UUID-text tie-break, pair cap, dual ordinals, and the sole post-readiness expansion fallback | AD-3, AD-5, AD-6 | FR-4.1, FR-4.2, FR-4.9; NFR-1.1 dated scope |
| Material Scan handoff/persistence | v2 canonical manifest/result/handoff, pair-wide transaction, envelope-based exact replay/conflict; no job dispatch | AD-7, AD-8, AD-9, Persistence | FR-4.2-4.5, FR-4.12; NFR-3.1-3.3 |
| Analysis-tool material change | Enriched CodeGraph proof/typed expansion and JDT readiness/split/implementation evidence; no raw/general/Worker tool | AD-2, AD-3, AD-6 | FR-4.9, FR-15.1-15.3; NFR-2.3 |
| Dated PRD replacement | File rather than method Code Units; retain XML configuration; no Worker-embedded communication or immediate Edge/Uncertainty/Document work | Summary, AD-1/4/5/8, Traceability | FR-4.1, FR-4.3-4.5, FR-4.12, FR-5.8, FR-6.1/6.2 |

Authorization intent: implement exactly this Story 12.2 preparation boundary after approval, through test-first tasks, without importing Story 12.3. Remaining gates are task self-review, definition review, honest TDD RED/GREEN, convergence, quality review, conditional economical E2E with real PostgreSQL/Git/CodeGraph/JDT-LS, final verification, and root-owned checkpoint/state/commit.

## Definition-Review Correction Seams

| Finding owner | Exact downstream seam fixed by this plan |
| --- | --- |
| DR-001 plan | Canonical prepared-context domain/tags, nullable additive prepared hash, manifest hash reference, legacy ID/CapturedAt exclusions, independent reconstruction, and capture-time-variance golden/replay tests. |
| DR-002 tasks | Safe XML owner must reach behavioral GREEN and return `SafeXmlDirectChildRangeSet` before XML split RED/GREEN; the split planner only consumes that type. Java split remains independently ordered after JDT readiness. |
| DR-003 tasks | `tests/Vulgata.Tests/PreparedScanning/PreparedScanAtomicPostgreSqlTests.cs` with filter `FullyQualifiedName~PreparedScanAtomicPostgreSqlTests` must use disposable PostgreSQL 17 and cover every forced insert boundary, cancellation rollback, stale/concurrent writers, replay/conflict, and zero partial visibility. Migration lifecycle remains a separate suite. |
| DR-004 tasks | `tests/Vulgata.Tests/PreparedScanning/PreparedScanReleaseQualificationTests.cs` must execute three complete paired preparations, one with both Repository and candidate inputs reversed, compare every SC-001 output, and measure full PreScan below two minutes for each Repository on the documented host while reporting CodeGraph/JDT sub-timings separately. |
| DR-005 tasks | Workflow/pin verification and post-GREEN boundary/security scans are nonbehavioral qualification with no expected RED. Any actual failure creates a new requirement-backed regression RED and named production GREEN owner. |

The task graph must encode these as edges, not prose-only notes:

```text
safe XML public-seam RED
  -> safe XML projection/range-index GREEN
  -> XML split public-seam RED
  -> XML split GREEN consuming SafeXmlDirectChildRangeSet
  -> every US4 File Code Unit/planner/manifest/store/coordinator/API consumer
  -> every XML-observing qualification, including three-run release and final quickstart

canonical audit public-seam RED
  -> PreparedScanAuditFactory production GREEN
  -> audit entity/configuration/migration persists every envelope preimage
  -> PreparedScanAtomicReplayTests contract replay GREEN
  -> separately mandatory PreparedScanAtomicPostgreSqlTests reconstruction GREEN
  -> manifest/store/replay consumers and SC-001/SC-008 qualification
```

No task on the right side of either chain is eligible for phase placement, parallel execution, or qualification until its named predecessor is GREEN.

Post-GREEN FR-024/SC-006 qualification has one concrete owner: `tests/Vulgata.Tests/PreparedScanning/PreparedScanProductionLiteralQualificationTests.cs`. It inspects production assemblies, embedded resources/catalogs, and public-seam output using a denylist derived from neutral/release fixture Repository names, canonical paths, endpoint literals, and fixture line-number literals; test assemblies/fixtures are out of scan scope. It writes `artifacts/qualification/story-12.2/production-literal-inspection.json` with inspected artifacts, denylist hash, matches, and zero-match verdict. A match is not repaired inside qualification: it creates `PreparedScanProductionLiteralRegressionTests.cs` as a requirement-backed behavioral RED and names the exact production owner that removes the special case while preserving neutral behavior.

The final phase has one barrier and no later qualification:

```text
all behavioral GREEN + three-run release qualification
  -> { production-literal qualification evidence;
       PostgreSQL migration + atomic replay/reconstruction evidence;
       workflow/pin + boundary/security qualification evidence }
  -> ONE final verification task (final quickstart + build + full test + evidence-count check)
```

The three qualification owners may run in parallel only after all behavioral/release prerequisites, but the one final verification task is physically last and depends explicitly on every evidence artifact/test count. No security record, PostgreSQL test, production-literal scan, or other qualification may follow or race final verification.

## Inline Self-Review

| Lens | Finding | Result |
| --- | --- | --- |
| Feasibility/repository fit | Existing exact Git, typed analysis, snapshot, CodeUnit, pre-scan, and transaction seams are reusable; gaps are additive and named. | PASS |
| Invariants | Exact source, closed readiness, safe XML-before-XML-split ownership, all-or-none splits, mandatory seeds, dual ordinals, stable prepared-context identity, atomic visibility, envelope equality, and no dispatch are explicit. | PASS |
| Failure behavior | Every hard/nonhard failure has a closed outcome; only post-readiness related-file expansion fallback is nonfatal analysis behavior. | PASS |
| Test seams | Pure planner, typed adapters, store, coordinator, API, migrations, neutral fixtures, and real-tool suites are independently testable. | PASS |
| Migration/compatibility | One additive reversible migration leaves historical Story 12.1 snapshot IDs/CapturedAt intact, adds nullable prepared identity only for the new path, and avoids backfill/reinterpretation. | PASS |
| Security | No external source transfer, raw tool protocol, XML resolver/execution, host path, sample special case, or LLM. | PASS |
| Authority/Constitution | Dated overlay and host correction preserved; canonical terms used; no ADR/context exists; no Constitution exception. | PASS |
| Story boundary | No Worker, `navigate_code`, job lease/retry/concurrency/dispatch, Document generation, Edge/Uncertainty, Pass 2, Chat, CGC, or Backup B. | PASS |
| Canonical replay | Proof/context domain tables, explicit encodings/exclusions, acyclic dependencies, literal vectors, and independent reconstruction are explicit. Duration and capture-time/legacy-ID variance preserve identity; stable proof/context mutation conflicts. | PASS |
| Audit reconstruction | Every audit tag is persisted; selected/nonselected shapes, same-manifest association, migration/configuration/store ownership, PostgreSQL recomputation, and digest/preimage mutation behavior are explicit. | PASS |
| EF/PostgreSQL schema feasibility | Prepared-only entry principal uses non-null key members; audit is optional dependent; historical CodeUnit nullable fields are not alternate keys and require no backfill. | PASS |
| Definition task seams | XML producer/split/consumer/qualification total order, canonical-audit RED/GREEN ownership, PostgreSQL store ownership, three-run/full-timing qualification, and honest nonbehavioral labels are exact enough for regenerated tasks without authorizing production. | PASS |
| Qualification integrity | Exact replay filters have named test owners; PostgreSQL remains separately mandatory; production-literal inspection covers Repository names, paths, endpoints, and line numbers with regression routing. | PASS |
| Placeholder/coverage | No unresolved template token or clarification remains; all 43 FRs, nine SCs, and authorization triggers map to a decision/test surface. | PASS |

## Plan-Review Attempt 1 Closure

| Finding | Regenerated-plan closure | Verification seam |
| --- | --- | --- |
| PR-001 | AD-3 and Failure/Replay distinguish hard CodeGraph/JDT-LS readiness rejection from the only permitted post-readiness related-file expansion Warning/fallback. | Failure injection proves no hard-readiness timeout can approve; the same timeout at expansion yields the one deterministic audit/fallback. |
| PR-002 | AD-5/6, data model, API, persistence, and replay define `ManifestOrdinal` per Repository and `PairSelectionOrdinal` pair-wide; File Code Unit identity excludes both while manifest/pair envelopes include both. | Cross-Repository interleaving, contiguous-domain, ordinal-mutation conflict, and reversed-input tests. |
| PR-003 | AD-9 and the canonical-envelope contract define exact versioned bytes, domain separators, field tags/order, all scalar/nested/list encodings, validation normalization, collection counts/order, derived exclusions, an acyclic dependency graph, SHA-256, and deterministic UUIDv8 bits. | Literal golden vectors plus an independent test-only reconstructor and persisted replay/mutation tests. |
| PR-004 | AD-6 defines score `>=400`, first-at-most-20 mandatory seeds per Repository, expansion only from those seeds, lowercase UUID `D` ordinal comparator, retain-all-mandatory/fill-to-150 semantics, and all corrected golden cases. | UUID tie, cross-Repository interleave, reversed input, over-cap 25/55-per-Repository, and readiness-versus-expansion cases. |

## Plan-Review Attempt 2 Closure

| Finding | Regenerated-plan closure | Verification seam |
| --- | --- | --- |
| PR2-001 | AD-3/9 and canonical-envelope §§4.2/4.3 define separate versioned CodeGraph and Java proof identities with exact stable field tags/types/invariants, explicit Java `NotApplicable` Null/defaults, and exhaustive volatile exclusions. Manifest tags 9/10 reference their deterministic hashes. Duration/telemetry are bounded first-commit audit metadata, never identity or replay equality. Store reconstruction reads persisted typed fields rather than trusting opaque hashes. | Literal proof-envelope hex/digest vectors; independent relational reconstruction; repeated preparation with different durations produces identical proof/manifest bytes and mutation-free replay; every stable predicate/binding mutation conflicts; excluded telemetry mutation leaves identity unchanged. |

## Definition-Review Attempt 1 Closure

| Finding | Regenerated-plan closure | Verification seam |
| --- | --- | --- |
| DR-001 | Canonical-envelope §4.4 defines `PreparedScanContextIdentityHash` over RepositoryId, ScanRunId, ProfiledRevision, schema, and ContentHash. Manifest tag 13 references that hash. Legacy time-derived ContextSnapshotId and CapturedAt remain relational/audit metadata and are excluded. Data/migration design is additive and historical rows are untouched. | Literal context envelope vector; two captures with different times/legacy IDs produce identical context/manifest/pair bytes and replay; changed stable binding/schema/content conflicts; independent PostgreSQL reconstruction. |

## Definition-Review Attempt 2 Plan Seams

| Finding | Plan-set disposition | Required task-generation evidence |
| --- | --- | --- |
| DR2-001 | Safe XML GREEN → XML split RED/GREEN → all US4 consumers → all XML-observing qualifications is now one explicit nonparallelizable chain in AD-4/5, contracts, quickstart, and this plan. | Task phase/IDs must follow the chain physically; every consumer and three-run/final-quickstart task names XML split GREEN as transitive prerequisite; no XML split task appears in an earlier parallel set. |
| DR2-002 | `tests/Vulgata.Tests/PreparedScanning/PreparedScanAuditContractTests.cs` is the required public-seam RED for canonical outcomes/order/reasons/DetailFingerprint/UUID/replay mutation; `src/dotnet/Vulgata.Core/DomainServices/PreparedScanAuditFactory.cs` is its sole named production GREEN owner. | RED evidence precedes factory work; focused test plus surrounding PreparedScanning suite prove GREEN before manifest/store consumers. |
| DR2-003 | Corrected spec now acknowledges authoritative glossary entries; plan/research/model/contracts do the same without changing semantics. | Terminology scan finds no stale pending-adoption statement. |

## Definition-Review Attempt 3 Closure

| Finding | Plan-set disposition | Required evidence |
| --- | --- | --- |
| DR3-001 | Audit persistence now stores all canonical tags 1-10, including nullable FileCodeUnitIdentityFingerprint and PairSelectionOrdinal, plus a selected-only same-manifest FK. Outcome-specific nullability, migration/configuration/store owners, filtered uniqueness, and digest recomputation are explicit. | `PreparedScanAtomicReplayTests` plus mandatory PostgreSQL reconstruction mutate every preimage, null shape, selected association, and stored digest; any mismatch conflicts without mutation. |
| DR3-002 | Exact post-GREEN owner, inspected production surfaces, four literal categories, JSON qualification artifact, and regression routing are defined. | `PreparedScanProductionLiteralQualificationTests.cs` produces a zero-match evidence record; any match creates a new behavioral regression RED/GREEN rather than an ad hoc qualification edit. |
| DR3-003 | Quickstart uses task-owned `FullyQualifiedName~PreparedScanAtomicReplayTests`; PostgreSQL reconstruction remains a separate mandatory command/filter. | Test discovery/count evidence shows both suites ran; neither OR-filter can mask absence of the PostgreSQL suite. |
| DR3-004 | Plan continues to classify only workflow/pin and post-GREEN inspections as nonbehavioral. Prepared-context/handoff projection remains behavioral RED → production GREEN. | Regenerated tasks must not list that behavioral RED among non-RED qualifications. |

## Definition-Review Attempt 4 / Authorized Fifth Correction

| Finding | Plan-set closure | Required evidence |
| --- | --- | --- |
| DR4-001 | Replaced the infeasible nullable CodeUnit alternate key with prepared-only `PreparedScanManifestEntry`. All principal-key fields are non-null; audit's composite FK is optional and selected-only; CodeUnit historical nullable fields remain non-key. | EF model builds; PostgreSQL apply/rollback/reapply succeeds; legacy null CodeUnits remain readable; entry/audit FK rejection, reconstruction, mutation, atomic rollback, and no-pending-model tests pass. |
| DR4-002 | Production-literal, PostgreSQL migration/atomic replay, and workflow/security qualifications all feed one physically last verification barrier. | Final task declares each evidence owner/count/artifact as prerequisite; nothing follows final quickstart/build/full-test verification. |
| DR4-003 | Plan and quickstart provenance identify the accepted fifth-correction authorization and raw attempt-4 source. | Provenance inspection matches the authorization decision and current artifact hashes. |

**Self-review result:** READY FOR REGENERATED TASKS AND THE AUTHORIZED FRESH COMBINED DEFINITION REVIEW. DR4-001/003 are closed in the plan set and DR4-002 has one exact final barrier seam. All earlier closures, runtime pins, 43 FR/9 SC, and Story 12.3 exclusion remain preserved. Production implementation remains unauthorized.
