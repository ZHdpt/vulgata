---
story_key: 4-2-language-and-framework-detection
title: "Story 4.2: Language and Framework Detection"
Status: ready-for-dev
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
- Canonical activation: not created until the story activation checkpoint.
- SpecKit feature directory: not allocated until `speckit-specify`.
- Required post-plan human gate: Scan strategy change authorization.
- Preparation limitation: the connected CodeGraphContext code-intelligence
  transport returned `Transport closed` twice, so repository source/tests and
  official provider documentation were used for context. This is not product
  qualification evidence.

### Completion notes

- Ultimate context engine analysis completed; comprehensive developer guide
  created.
- Story preparation checkpoint:
  `bmad-4-2-prepared-20260719T111713Z-e5ad70` (pending Git binding).

### File list

- `docs/bmad/implementation-artifacts/4-2-language-and-framework-detection.md`
