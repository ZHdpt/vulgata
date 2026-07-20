---
story_key: 4-2-language-and-framework-detection
title: "Story 4.2: Language and Framework Detection"
Status: in-progress
Epic: 4
Story: 2
created: 2026-07-19
execution_level: full
depends_on:
  - 4-1-codegraph-integration-and-code-unit-extraction
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - docs/bmad/implementation-artifacts/4-1-codegraph-integration-and-code-unit-extraction.md
  - specs/001-codegraph-extraction/plan.md
  - specs/001-codegraph-extraction/contracts/repository-profiling-api.md
---

# Story 4.2: Language and Framework Detection

## Story

As a **SystemOwner**,
I want pre-scan profiling to identify each Code Unit's programming language and
deterministically detect frameworks from Repository package evidence,
so that later Worker Agents receive accurate language and framework context
without guesses or an LLM classification pass.

## Scope and boundary

- Extend the completed Story 4.1 Repository profiling path; do not introduce a
  Scan Coordinator, Orchestrator, Worker Agent, dispatch policy, retry policy,
  Communication Pattern Catalog, or Story 4.3 question gate.
- Language attribution must originate from CodeGraphContext's tree-sitter parser
  identity. File-extension-only classification is not sufficient evidence.
- Framework detection is deterministic and Repository-local. It may inspect
  committed package/project manifests, lock files, and their declared package
  relationships, but it must not call an LLM, query an external package service,
  execute package-manager install/restore scripts, or guess from a framework-like
  symbol name.
- Persist detected frameworks as facts attached to Code Units. Unknown or
  ambiguous frameworks produce no framework fact.
- Preserve existing Repository visibility and management authorization. Any new
  user-visible text is Simplified Chinese.

## Acceptance Criteria

### AC-1: Attribute language from CodeGraph

**Given** Code Units have been extracted from a Repository
**When** deterministic profiling continues
**Then** every retained Code Unit has a normalized language attributed from the
CodeGraphContext parser identity associated with its source file
**And** the implementation does not use file extension as the authoritative
language signal
**And** it accepts C#, Java, Python, JavaScript, TypeScript, Go, Kotlin, Swift,
Rust, C, C++, and other language identifiers returned by the pinned
CodeGraphContext provider without requiring a code change for every identifier.

### AC-2: Expose a language breakdown

**Given** a Repository contains Code Units in one or more languages
**When** profiling completes
**Then** the ScanRun summary contains a deterministic language breakdown
**And** the Repository scan overview displays each language and its distinct
source-file count, not its Code Unit count.

### AC-3: Detect frameworks deterministically

**Given** language-tagged Code Units and Repository package/project evidence
**When** framework profiling runs
**Then** supported package declarations are mapped through a closed,
version-controlled framework catalog
**And** each applicable Code Unit receives the detected framework facts for its
own project/package scope
**And** direct dependency declarations are recorded with `High` confidence
**And** transitive detections are recorded with `Medium` confidence only when a
committed lock/resolution artifact provides deterministic evidence
**And** no LLM or external package lookup participates.

### AC-4: Persist auditable framework facts

**Given** a framework has deterministic evidence
**When** profiling results are persisted
**Then** `CodeUnitFramework` records contain the Code Unit key, normalized
framework name, Repository-relative detection source, and confidence
**And** duplicate framework facts for the same Code Unit are prevented
**And** the detection source identifies the exact manifest property, package
coordinate, or lock entry that justified the fact.

### AC-5: Preserve ambiguity as absence

**Given** package evidence is missing, unsupported, or ambiguous
**When** framework profiling completes
**Then** no framework fact is created for that evidence
**And** the system does not infer a framework from file names, source symbols,
or an LLM response.

### AC-6: Filter and audit unknown languages

**Given** a source candidate has no parser identity in the completed
CodeGraphContext profile
**When** profiling reconciles provider output with candidate files
**Then** the candidate is recorded as `Filtered` with reason
`UnknownLanguage`
**And** it is not retained as a dispatchable Code Unit
**And** the ScanRun summary includes the distinct unknown-file count and
Repository-relative paths for audit.

### AC-7: Preserve failure atomicity and existing authorization

**Given** language or framework profiling receives malformed provider data,
unsafe paths, or an invalid manifest
**When** the result is processed
**Then** source contents and absolute host paths are not logged or returned
**And** invalid individual manifest evidence is isolated as bounded audit
evidence without inventing framework facts
**And** provider-level failure retains Story 4.1's atomic structural-record
behavior
**And** the existing ManagementAccess/Repository visibility boundary remains
unchanged.

## Context resolutions

1. **PRD language scope remains unchanged.** This story makes the profiling
   contract tolerant of language identifiers supported by CodeGraphContext. It
   does not promise complete downstream Worker Agent scanning for languages
   outside the V1 demo Repositories.
2. **Deterministic facts and later hypotheses are separate.** This story stores
   only CodeGraph/manifest-derived facts. The July architecture's sampled LLM
   hypotheses, where still applicable, belong to a later review/question stage
   and cannot overwrite these facts.
3. **Unknown files are audit records, not fabricated Code Units.** A file with
   no CodeGraph parser identity is represented by the existing filtered
   candidate outcome with `UnknownLanguage`; no fake symbol or line range is
   created.
4. **Provider language transport is an owned projection.** CodeGraphContext
   0.5.1 documents `File.language`; the plan must extend Vulgata's closed,
   bounded HTTP JSON projection so structural rows carry the associated file
   parser identity without depending on the provider's internal database schema
   outside the pinned query contract.

## Implementation guardrails

- Keep Core free of EF Core and infrastructure references. Put the framework
  fact entity in Core, its configuration and detector implementation in
  Infrastructure, shared response contracts in Shared, and UI composition in
  Web.
- Use one `IEntityTypeConfiguration<T>` per entity and an additive migration.
  Do not expose `DbSet` or `IQueryable` outside Infrastructure.
- Keep package parsing non-executing and bounded: reject Repository-relative
  path escapes and reparse points, bound manifest size and diagnostic detail,
  and never evaluate build targets or package scripts.
- Normalize framework names through a closed catalog; do not store raw
  attacker-controlled package text as a framework name.
- Count distinct files in summaries even when one file contains many Code
  Units. Framework evidence may fan out only to Code Units within the justified
  project/package scope.
- Preserve Story 4.1's provider envelope, row/byte limits, private context
  cleanup, atomic persistence, and existing Simplified-Chinese error behavior.
- This story materially changes Scan parsing/filtering behavior. After an
  independent plan review accepts, the sprint loop must obtain the planned-change
  authorization required by `AGENTS.md` before task generation.

## Existing implementation seams

- `CodeUnit.Language` already exists and is nullable; Story 4.1 currently
  constructs all Code Units with `language: null`.
- `CodeGraphContextProvider` owns the pinned local loopback HTTP JSON gateway and
  closed projection parser. Its current structural query returns no language.
- `RepositoryProfilingCoordinator` already reconciles provider Code Units with
  eligible source candidates and owns atomic persistence.
- `ScanCandidateOutcome` already preserves filtered/skipped/errored file audit
  facts and is the correct boundary for `UnknownLanguage`.
- `ScanRunSummaryDto` and `RepositoryDetailDialog` currently expose only
  parsed/total counts and human-review state; extend them rather than creating a
  parallel overview.
- `RepositoryProfilingTests` provides the existing SQLite integration harness
  and controlled provider/candidate doubles.

## Likely owned paths for planning

- UPDATE `src/dotnet/Vulgata.Core/Entities/CodeUnit.cs`
- NEW `src/dotnet/Vulgata.Core/Entities/CodeUnitFramework.cs`
- UPDATE `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`
- UPDATE `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`
- NEW deterministic manifest detector/catalog under
  `src/dotnet/Vulgata.Infrastructure/`
- UPDATE `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`
- UPDATE `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`
- NEW `IEntityTypeConfiguration<CodeUnitFramework>` and additive migration
- UPDATE `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`
- UPDATE `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs` to
  carry the provider parser identity into each Code Unit, map candidates with no
  parser identity to `Filtered/UnknownLanguage`, invoke deterministic framework
  detection, and build the language/unknown-file summary
- UPDATE `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`
- UPDATE
  `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`
  to render the new summary fields
- UPDATE focused provider, persistence, coordinator, API, and component tests

## Testing requirements

- Observe RED before each changed production behavior.
- Provider contract tests must prove parser identity is sourced from
  CodeGraphContext `File.language`, normalized, bounded, and associated with
  every projected structural row.
- Detector tests must cover direct and lock-backed transitive evidence, project
  scoping, duplicate suppression, unsupported/ambiguous manifests, path
  containment, size bounds, and non-execution.
- Persistence tests must cover framework FK/uniqueness/confidence/source
  constraints and language values.
- Coordinator/API/UI integration tests must cover multi-language distinct-file
  counts, unknown-language audit paths/count, authorized visibility, and
  Simplified-Chinese rendering.
- The E2E decision must treat provider/API/persistence/UI integration as
  triggered unless current-boundary integration evidence proves every observable
  boundary.

## References

- Story requirements: [Source: `docs/bmad/epics.md#Story-42-Language--Framework-Detection`]
- PRD scan and scope contract: [Source: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md#54-Scan-Pipeline`]
- July pre-scan architecture: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md#5-Pre-Scan-Discovery-and-Review`]
- Project boundaries and testing: [Source: `.specify/memory/constitution.md#Core-Principles`]
- Canonical terminology: [Source: `docs/UBIQUITOUS_LANGUAGE.md#Scanning--Agents`]
- Previous story seam: [Source: `docs/bmad/implementation-artifacts/4-1-codegraph-integration-and-code-unit-extraction.md#Existing-integration-context`]
- Provider contract: [Source: `specs/001-codegraph-extraction/contracts/repository-profiling-api.md`]
- CodeGraphContext language property: [Source: `https://codegraphcontext.github.io/concepts/graph-model/`]
- CodeGraphContext supported languages/version: [Source: `https://github.com/CodeGraphContext/CodeGraphContext`]

## Dev Agent Record

### Context reference

- Preparation source commit:
  `b796e8623a456d9b2abe1797d1e40ee1d74bb264`.
- Execution level: Full (default because the user did not request Standard).
- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/4-2-language-and-framework-detection.json`.
- SpecKit feature directory:
  `specs/002-language-framework-detection`.
- Current workflow stage: `implement`; the corrected specification, plan, and
  50 dependency-ordered tasks passed independent analysis iteration 3 with
  zero Critical or High findings and complete FR/SC/AC coverage.
- Planning is recoverably blocked at Phase 0 by pinned-provider evidence:
  CodeGraphContext 0.5.1 does not persist parser identity on `File` nodes, so a
  parser-supported file with no structural declarations cannot be distinguished
  without guessing; its HTTP query handler also truncates at 100 rows by
  default while Story 4.1 assumes a complete unpaginated projection.
- The user approved the recommended remediation: retain the stock pinned
  provider; classify zero-structure candidates without observable parser
  identity as `Filtered/UnknownLanguage`; raise the private gateway result
  ceiling to `250001`; fail atomically on reported truncation; and qualify the
  final image with empty-file and greater-than-100-row cases. Any LLM language
  output remains a separate non-authoritative hypothesis and cannot make an
  unknown candidate dispatchable.
- Activation checkpoint:
  `bmad-4-2-context-20260719T112236Z-6a56bc` (pending Git binding).
- Required post-plan human gate: Scan strategy change authorization.
- Preparation limitation: the connected CodeGraphContext code-intelligence
  transport returned `Transport closed` twice, so repository source/tests and
  official provider documentation were used for context. This is not product
  qualification evidence.

### Completion notes

- Ultimate context engine analysis completed; comprehensive developer guide
  created.
- SpecKit specification and requirements checklist created at the pinned
  activation boundary with all seven acceptance-criteria groups mapped.
- Independent specification review accepted at
  `specs/002-language-framework-detection/reviews/spec-review-1.json`.
- Planning blocker and exact remediation boundary recorded at
  `specs/002-language-framework-detection/evidence/plan-blocker-1.json`.
- Complete technical plan and Phase 0/1 design artifacts produced under
  `specs/002-language-framework-detection/`.
- Plan review iteration 1 required atomic malformed-language handling and a
  consistent evidence-locator bound; both were corrected.
- Independent plan review iteration 2 accepted the corrected plan with no new
  findings.
- The user separately granted the required post-plan Scan-strategy
  authorization. This advances task generation only and does not waive
  analysis, TDD, review, E2E, security, or verification gates.
- Analysis iteration 1 identified seven High findings; the user approved the
  complete remediation. Focused specification and plan reviews accepted the
  corrected language matrix, objective UI criterion, immutable source
  boundary, and activation hash rule.
- Analysis iteration 2 confirmed the seven original findings were resolved and
  found two remaining task-boundary defects: a stale specification hash and
  duplicate final-commit recovery RED/GREEN ownership. Both were corrected.
- Independent analysis iteration 3 accepted T001-T050 with 21/21 functional
  requirements, 8/8 success criteria, and 7/7 acceptance groups covered.
- Implementation foundation T001-T008 is GREEN: the accepted boundary passed
  24/24 baseline tests; atomic complete-draft persistence and recovery passed
  16/16 profiling tests; the private CodeGraphContext completeness envelope
  passed the combined 25/25 provider/profiling tests.
- EF migration `20260720030657_AddLanguageFrameworkDetection` adds only
  `CodeUnitFrameworks` and `ScanRunLanguageSummaries`, preserves historical
  nullable Code Unit language rows, and leaves no pending model changes.
- The accepted T005 command required the repository-specific mechanical
  correction `--context VulgataDbContext` because the startup project owns two
  DbContexts; this changes no schema intent or protected Scan behavior.
- User Story 1 T009-T015 is GREEN: Projection A carries normalized structural
  `parser_language`, complete drafts enforce one language per path, language
  summaries count distinct retained paths, and the existing authorized
  Repository overview renders sorted language counts under `语言分布`.
- The exact twelve-identifier matrix passes 12/12 and the combined provider,
  profiling, API, and component evidence passes 71/71 while hidden Repository
  redaction remains 404 and non-management access remains 403.
- User Story 2 detector foundation T016-T027 is GREEN: the versioned closed
  catalog, exact committed-manifest parsers, parser-language-compatible scope
  selection, ambiguity suppression, deterministic ranking, and bounded safe
  locators pass 37/37 focused framework tests.
- Framework detection remains non-executing and uses only
  `git ls-files --cached -z --` for Repository inventory. It performs no
  build, restore, install, script, network, LLM, import, or symbol scan.
- Per-artifact failures are isolated with fixed bounded Chinese diagnostics;
  Repository-global limit exhaustion or unexpected detector faults return a
  typed fail-closed result with zero framework evidence. Transaction-order and
  zero-persistence verification remain owned by T028 and T035-T036.
- Production limits are fixed at 8 MiB per artifact, 64 MiB aggregate, 1,000
  supported artifacts, 100,000 parsed entries, 250,000 candidate facts, 64
  final framework facts per Code Unit, and 100 retained audit details.
- User Story 2 T028-T029 is GREEN: one bounded Repository detector pass feeds
  pure parser-language/scope-compatible Code Unit fan-out; framework facts and
  the complete framework-audit count persist in the same atomic draft and
  project through the authorized Repository overview.
- The accepted response contract exposed a storage omission: sampled framework
  audit details could not reconstruct a complete count after reload. The
  minimal remediation adds only
  `ScanRun.FrameworkEvidenceAuditTotalOutcomeCount`, its nonnegative constraint,
  and migration `20260720060442_FrameworkEvidenceAuditCount`. This changes no
  approved parsing/filtering behavior or PRD requirement.
- Combined framework, profiling, API, and component verification passes 80/80;
  the overview uses the shared 100-detail budget and renders `框架`, `置信度`,
  `检测依据`, and `框架证据警告`. EF reports no pending model changes.
- Implementation checkpoint:
  `bmad-4-2-tasks-accepted-20260720T024508Z-6603fe`
  (pending Git binding).
- Story preparation checkpoint:
  `bmad-4-2-prepared-20260719T111713Z-e5ad70` (pending Git binding).

### File list

- `docs/bmad/implementation-artifacts/4-2-language-and-framework-detection.md`
- `docs/bmad/implementation-artifacts/workflow-activations/4-2-language-and-framework-detection.json`
- `.specify/feature.json`
- `specs/002-language-framework-detection/spec.md`
- `specs/002-language-framework-detection/checklists/requirements.md`
- `specs/002-language-framework-detection/reviews/spec-review-1.json`
- `specs/002-language-framework-detection/evidence/plan-blocker-1.json`
- `specs/002-language-framework-detection/reviews/plan-review-1.json`
- `specs/002-language-framework-detection/reviews/plan-review-2.json`
- `specs/002-language-framework-detection/reviews/spec-review-2.json`
- `specs/002-language-framework-detection/reviews/spec-review-3.json`
- `specs/002-language-framework-detection/reviews/plan-review-3.json`
- `specs/002-language-framework-detection/tasks.md`
- `specs/002-language-framework-detection/reviews/analyze-1.json`
- `specs/002-language-framework-detection/reviews/analyze-2.json`
- `specs/002-language-framework-detection/reviews/analyze-3.json`
- `specs/002-language-framework-detection/plan.md`
- `specs/002-language-framework-detection/research.md`
- `specs/002-language-framework-detection/data-model.md`
- `specs/002-language-framework-detection/quickstart.md`
- `specs/002-language-framework-detection/contracts/provider-language-projection.md`
- `specs/002-language-framework-detection/contracts/framework-detection.md`
- `specs/002-language-framework-detection/contracts/repository-scan-overview.md`
- `specs/002-language-framework-detection/workflow-state.json`
- `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`
- `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/Catalog/framework-catalog.v1.json`
- `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkCatalog.cs`
- `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`
- `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj`
- `src/dotnet/Vulgata.Core/Entities/ScanRun.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Configurations/ScanRunConfiguration.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260720060442_FrameworkEvidenceAuditCount.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260720060442_FrameworkEvidenceAuditCount.Designer.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`
- `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`
- `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`
- `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`
- `src/dotnet/Vulgata.Web/Program.cs`
- `tests/Vulgata.Tests/DashboardPanelTests.cs`
- `tests/Vulgata.Tests/RepositoryApiTests.cs`
- `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
