---
story_key: 4-2-language-and-framework-detection
title: "Story 4.2: Language and Framework Detection"
Status: review
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
- Current workflow stage: `implement`. The user-approved T001 evidence correction
  preserves the unavailable historical pre-correction observation and requires
  a separately timed current immutable GREEN verification. Fresh plan review 8
  accepted the executable stale-binding RED/GREEN and checkpoint-bound review
  identity contract. Regenerated tasks passed Full read-only analysis iteration
  7 with zero Critical/High findings; implementation resumes at corrected T001.
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
- User Story 3 T030-T034 is GREEN: an eligible path without accepted parser
  identity in an otherwise complete provider response yields exactly one
  terminal `Filtered/UnknownLanguage` outcome and retains no Code Unit, Edge
  endpoint, unresolved relationship, or dispatchable structure.
- The existing overview now renders `未知语言文件` with all distinct ordinally
  sorted Repository-relative paths. Exact acceptance boundaries of 10,000
  paths and 16,777,216 combined UTF-8 bytes remain complete and unsampled;
  overflow rejects before provider or framework-detector invocation.
- Independent provider, profiling, API, and component verification passes
  85/85 with the established 403/404 redaction behavior unchanged.
- User Story 4 T035-T044 is GREEN: provider and Repository-global failures
  retain only local pre-provider audit and commit no provider-derived
  structure, framework facts, or language summaries; malformed individual
  evidence remains isolated behind bounded closed outcomes.
- Durable Scan failure messages, API `ProblemDetails`, framework-audit labels,
  and dialog errors now use closed Simplified-Chinese mappings. Raw provider
  rows, queries, host paths, package values, credentials, tokens, diagnostics,
  and timing details are not disclosed.
- System Repository visibility is rechecked after loading a ScanRun and before
  mapping its overview, so visibility revoked during a read returns the
  established redacted 404. Existing unauthenticated, non-management, hidden
  System Repository, hidden standalone Repository, and completion-time
  revocation behavior remains unchanged.
- The shared 100-detail overview budget now reserves one deterministic
  strongest-confidence safe evidence source for every recorded framework,
  then includes bounded framework-audit details and deterministic additional
  facts. The representative objective overview no longer lets one framework
  hide all evidence for a later framework.
- Independent US4 verification passes 77/77 affected tests, the accepted
  quickstart filter passes 69/69, and focused final-commit recovery passes
  2/2. The forbidden-scope audit found no Scan Coordinator, Orchestrator,
  Worker Agent/tool, dispatch, scheduling, retry, LLM language authority,
  Communication Pattern Catalog, Story 4.3, document-generation, or downstream
  language-support change.
- Completion-lane T045-T047 is GREEN on exact image
  `sha256:7968d5b45ab2f420c593610dfd2ae09bb84db40c1a80599f63582379f6a6e54c`.
  Projection A/B return 139/138 complete rows with the private exact 250001
  tool limit, no truncation metadata, immutable two-query snapshot, atomic
  completeness rejection, complete cleanup, and zero forbidden retention.
- The pinned CodeGraphContext 0.5.1 runtime deterministically synthesizes a
  line-1 Python Function for a zero-byte `.py` file, while zero-byte `.cs`,
  `.java`, `.js`, `.ts`, `.go`, `.kt`, `.swift`, `.rs`, `.c`, and `.cpp`
  files expose only `File(null)`. The user approved replacing only the owned
  completion fixture `empty_file.py` with zero-byte `empty_file.cs`; Python
  remains the 101+ row/parser-language fixture. No production query, parsing,
  filtering, or Scan behavior changed.
- Root-independent final-image evidence namespace
  `8e8d3333adb3b5845f19740b3d330718ac685bacfb253caffd5b0f5951915b92`
  passed with evidence SHA-256
  `AC4F8C4CCD5AC8C6EA056834DE35B82CFD3DA4C2C8B15382801B98ADA14D36BC`.
  The complete test suite passes 370/370 and the final Web build has zero
  errors.
- Migration rehearsal found exactly one
  `AddLanguageFrameworkDetection` migration/Designer pair, no pending model
  changes, additive-only upgrade SQL, and rollback SQL limited to the
  Story 4.2 audit-count check/column plus the two Story 4.2 tables. Historical
  nullable Code Unit language and fresh language/framework relational
  invariants pass 3/3.
- Final review iteration 1 returned `REVISE`. The accepted remediation aligned
  the canonical Story 4.1 Projection A contract with `parser_language`,
  documented the additive audit-count migration, and added RED/GREEN coverage
  for cancellation after provider-derived attachment, cancellation during
  final persistence, non-EF final persistence failure, and ambiguous
  same-root NuGet lock ownership.
- The correction is GREEN: the four new regressions pass 4/4, existing
  final-commit recovery passes 2/2, affected profiling/framework suites pass
  87/87, and the Web build succeeds with zero errors. Durable recovery now
  clears tracked draft state, reloads the still-`Profiling` Scan in a fresh
  unit of work, and persists `Aborted` or sanitized `Failed` with no
  provider-derived rows; ambiguous same-root lock evidence creates one bounded
  outcome and no framework fact.
- Ready-for-review candidate `71a2db8664ed53f4ee592635c920605909b4e218`
  is immutable and bound by checkpoint
  `bmad-4-2-implement-20260720T104752Z-c7a3e1`. Post-commit verification passes
  87/87 affected tests and a zero-error Web build. The exact pinned final-image
  gate also passes against the corrected artifact manifest
  `ca106f80f53442b067d925f516595b29172671ebbb11d78c8d1503bf02462486`
  with evidence SHA-256
  `7d0c4ab90d891d25a38aa9802f57ca518403130500de82798d8be3f7bf0d2332`.
- Review iteration 2 returned `REVISE`. New High findings identify an
  absolute-host-path logging disclosure before audit-path normalization,
  lock-derived framework evidence paired to a single tracked but
  invalid/unsupported direct manifest, divergence between the canonical
  Story 4.1 safe-name/evidence schema and the verifier, a stale pre-correction
  runtime image/full-suite binding, and premature review entry while T050
  remains unchecked. The final cross-artifact audit also requires the amended
  plan set to pass its dependent plan-review/tasks/analyze gates and requires
  explicit user approval before those analyzed artifacts are edited.
- The user approved the bounded iteration-2 remediation and the
  Story 4.2-specific historical-evidence exception. The plan set now requires
  provider paths to become `SafeProviderPath` before any outcome or log use;
  validates exactly one safely read, successfully parsed, supported,
  applicable same-root direct-manifest owner before NuGet/npm lock evidence can
  create a framework fact; closes the Story 4.1 final-image evidence grammar;
  binds corrected source/tree/manifest/image/evidence identities; and keeps
  T050 open until the final immutable-candidate audit.
- Fresh plan reviews 4 and 5 returned bounded `REVISE` findings for the
  provider-evidence schema. The final correction preserves exactly 19 required
  root members and fully closes and bounds `projectionObservations` and
  `coverage`, with one semantic authority for every retained row-count and
  completeness fact. Fresh plan review 6 returned `ACCEPT` with no
  Constitution, ADR, context, or PRD conflict.
- After that independent acceptance, the user explicitly authorized the
  material Scan evidence-filtering change to proceed to task generation.
  This authorization changes no Worker Agent tool or PRD requirement and
  waives none of the remaining task analysis, TDD, review, E2E, security,
  verification, or Git gates.
- Story status remains `review`; the internal workflow has returned to
  `tasks`. The prior implementation-review verdicts are invalidated by the
  corrected plan boundary and will be rerun after fresh task analysis and
  implementation correction.
- T050 exposed that no contemporaneous pre-correction T001 observation was
  recorded and that recreating one would fabricate history. The user approved
  the bounded correction: retain that historical observation as unavailable,
  require a separately labeled current immutable GREEN verification, and
  invalidate tasks, analysis, ledger, both Full reviews, E2E, verification,
  candidate, manifest, image, namespace, T049, and T050.
- Plan review 7 returned one High testability/source-binding finding because
  the executable provider-contract test still hard-coded the prior plan hashes
  and static review identity. The corrected plan requires an observed stale
  binding RED, binds the replacement to plan review 8 plus this plan
  checkpoint, and rejects stale, missing, duplicate, drifted, or non-ancestor
  bindings. Fresh plan review 8 returned `ACCEPT`.
- Regenerated `tasks.md` preserves 33 valid completed tasks and reopens 17
  evidence-chain tasks. Analysis iteration 7 accepted all 50 tasks with 21/21
  functional requirements, 8/8 success criteria, 7/7 acceptance groups, 16/16
  plan decisions, and 12/12 correction findings covered. The graph is acyclic:
  corrected T001 precedes stale-binding RED/GREEN, ledger regeneration, a new
  immutable candidate, Full reviews, E2E, verification, manifest/image gates,
  T049, and final T050.
- Corrected T001 current verification is complete at immutable commit
  `c04a6b594f4e1bc89baa111d664ff355ffcc57be` (tree
  `777bd88701f6443cc7195d3a37a09c955544a820`). The first provider/profiling
  attempt failed before discovery because a generated Core reference assembly
  was transiently corrupted; that failure is preserved and is not RED. The
  unchanged retry passed 107/107 and framework detection passed 48/48 with no
  skipped tests or blocked network/proxy URL. The separate historical statement
  remains `Pre-correction T001 baseline observation: unavailable`.
- Regenerated tasks initially passed complete requirements/finding coverage but
  analysis iteration 5 found one High dependency cycle: current-candidate
  ledger finalization/validation preceded the GREEN evidence it required. The
  user approved a task-only correction. T035 now performs early historical
  discovery; T036-T044 produce the immutable-candidate GREEN evidence; T045
  finalizes the ledger; T046 validates it; T047 validates evidence; T048
  reconciles obligations; T049 freezes the candidate; and T050 remains the
  final audit.
- Fresh analysis iteration 6 returned `ACCEPT`: all 21 functional
  requirements, 8 success criteria, 7 acceptance-criteria groups, 14 accepted
  plan decisions, and 11 iteration-2 findings map to an acyclic 50-task graph.
  Fourteen historical baseline tasks remain checked, 36 correction/verification
  tasks remain pending, all changed behavior retains RED-before-GREEN ordering,
  and the historical-evidence exception cannot relabel current GREEN evidence.
- Implementation slot 1 completed T013-T018 with observed RED cases for both
  NuGet and npm invalid owners. Lock-derived `Medium` evidence now requires one
  complete, safely read, successfully parsed, supported, applicable same-root
  direct-manifest owner; invalid candidates cannot be discarded to manufacture
  uniqueness, one bounded closed audit is emitted, and independent valid
  artifacts continue.
- Root verification passes 48/48 FrameworkDetection tests and 49/49 surrounding
  RepositoryProfiling tests. `git diff --check` reports only the repository's
  known LF-to-CRLF warnings.
- Implementation slot 2 completed T022-T025. Captured-logger and parser RED
  evidence produced 19 failures across the 21-case unsafe-path matrix: raw
  Windows, UNC, POSIX-rooted, escape, control-character, and invalid-separator
  values reached provider outcomes, logs, or durable audit state. A second RED
  proved that revalidating `(unknown)` incorrectly made it known.
- `SafeProviderPath` now closes the transition inside the provider parser and
  is revalidated before the coordinator's first log. Every downstream surface
  receives the identical bounded Repository-relative value or literal
  `(unknown)`; raw provider scalars are not retained.
- Root verification passes 105/105 CodeGraph provider and RepositoryProfiling
  tests plus 33/33 Repository API and dashboard component tests. Two-query
  atomicity, local candidate auditing, authorization, and visibility redaction
  remain intact.
- Implementation slot 3 completed T026-T030. The legacy-root RED failed 1/1
  because the verifier accepted alternate root names and parallel counts; the
  divergent-field RED failed 13/13 because unknown, duplicate, missing,
  alternate, wrong-typed, and legacy nested shapes were not rejected.
- The Python emitter and C# verifier now share one exact closed 19-root grammar
  with strict duplicate/type/range/sum/pass invariants, exact A/B alternatives,
  HMAC-redacted unknown names, exhaustive row validation, representative shape
  retention, and the 16-KiB evidence bound. The representative two-row evidence
  is 2,404 bytes.
- Root verification passes 33/33 non-Docker provider-contract tests and a
  non-writing Python syntax compile. Production, runner, verifier, and Story 4.1
  contract retain identical Projection A/B hashes and exact raw safe-name sets.
- Implementation slot 4 completed T031-T034. Thirteen preflight cases first
  failed because dirty/context/image/namespace mismatches could reach the
  provider sentinel. They now reject before provider execution.
- `production-source-manifest-v1` binds the clean Git commit/tree and sorted
  tracked non-ignored context hashes. Docker labels, the retained claim, Python
  emitter, and C# verifier bind the same manifest, image, verifier-base,
  provider, and query identities.
- Root verification passes 49/49 non-Docker provider-contract tests. No Docker
  build, image inspection, or provider execution was used in this slot; those
  remain in the immutable-candidate lane.
- The internal workflow remains `implement`; the next bounded work begins with
  T035 historical-evidence discovery before immutable-candidate verification.
- Source-binding implementation checkpoint:
  `bmad-4-2-implement-20260722T084438Z-a8d5c3` (pending Git binding).
- T035 historical discovery completed against the immutable range
  `e9824df7300634c19d815bdd7ad591b9fb5366d5..93b99a49a8f8b9974e00d4274fd4c5ba64c467d6`.
  Six execution groups and all 19 legacy TDD groups are mapped to provable Git
  objects and durable workflow records. No retained historical raw RED output
  is claimed; missing trailers and unprovable test-before-production order are
  explicit. Current T036-T044 evidence remains empty pending T045 finalization.
- Historical-discovery checkpoint:
  `bmad-4-2-implement-20260723T013412Z-b7e2d4` (pending Git binding).
- The first immutable-candidate run passed T036 (202/202 focused tests) and
  T037 (all accepted hashes, migration script, and no pending model changes)
  but stopped at T038 before image/provider execution because tracked agent
  aliases `.claude/skills` and `.trae/skills` were non-ignored symlinks. No
  T036-T044 marker advanced and the failed preflight remains failed evidence.
- A focused RED reproduced the alias failure. `.dockerignore` now excludes only
  `.claude/` and `.trae/`, and manifest evaluation applies ordered ignore rules
  before physical validation for ignored mode-120000 entries. Non-ignored
  tracked symlinks remain fail-closed. Root verification passes 50/50
  non-Docker provider-contract tests.
- Docker-context correction checkpoint:
  `bmad-4-2-implement-20260723T024403Z-c9f1a6` (pending Git binding).
- On the next immutable candidate, T036 passed 203/203 focused tests with zero
  skips and T037 matched all nine accepted hashes, generated the two-migration
  script, and reported no pending model changes. T038 then stopped before the
  Docker build because tracked root `CLAUDE.md -> AGENTS.md` was another
  non-ignored mode-120000 alias. The fresh namespace remained unconsumed, the
  failed preflight remains failed evidence, and T038-T044 stayed unchecked.
- A focused RED reproduced the root-alias failure. `.dockerignore` now excludes
  only `CLAUDE.md`; the existing ordered-ignore manifest behavior is unchanged.
  The focused case passes, all 5 non-ignored-symlink controls remain fail-closed,
  and root verification passes 51/51 non-final provider-contract tests.
- Root-alias Docker-context correction checkpoint:
  `bmad-4-2-implement-20260723T062734Z-f1c8a2` (pending Git binding).
- The following candidate passed T036 (204/204), T037, and deterministic T038
  manifest generation, but T039 repeatedly stopped before image creation. The
  Docker engine was first unavailable; after startup, the active local proxy
  intermittently returned HTTP 502 for Ubuntu Noble indexes. Command-construction
  failures and infrastructure failures remain failed attempt evidence; no
  namespace was consumed and no T039-T044 marker advanced.
- A focused RED proved the runtime apt layer had no bounded acquisition policy.
  Both existing apt commands now use five retries and 30-second HTTP/HTTPS
  timeouts. No proxy, mirror, secret, package, base image, provider pin, or
  runtime-content change was introduced. Root verification passes 52/52
  non-final provider-contract tests with zero skips.
- Docker apt-resilience correction checkpoint:
  `bmad-4-2-implement-20260723T073700Z-e4d7c1` (pending Git binding).
- T001 current-verification implementation checkpoint:
  `bmad-4-2-implement-20260726T052236Z-89bc56` (pending Git binding).
- Accepted-plan binding TDD completed against source commit
  `92829f9216eb322bf738d598e0717130151c818e`: T035 failed exactly the
  three stale canonical hashes, T036 failed the stale static review identity,
  and the preserved first T038 GREEN attempt exposed implicit Git stdout
  decoding for non-ASCII checkpoint content. Pinning UTF-8 decoding then passed
  17/17 focused and 80/80 surrounding non-Docker provider-contract tests.
  Existing RED and failed-GREEN outputs remain immutable; the successful retry
  uses a distinct create-new path.
- Accepted-plan binding implementation checkpoint:
  `bmad-4-2-implement-20260726T061644Z-4c8e71` (pending Git binding).
- The T039-T040 reconstruction ledger now preserves 19 legacy groups without
  inventing unavailable RED or ordering evidence, links the separate T001
  `Current verification`, records T035-T038 at their actual current times, and
  removes all stale candidate, review, E2E, verification, manifest, image,
  namespace, T049, and T050 identities. Root validation passed 33 linear
  commits, 32 immutable blob references, 8 unique ancestor checkpoints, and 8
  current raw files. One recoverable CRLF working-tree hash mismatch was
  corrected by hashing the pinned Git blob bytes.
- Truthful-ledger implementation checkpoint:
  `bmad-4-2-implement-20260726T065347Z-7d1a63` (pending Git binding).
- T041 pinned clean immutable candidate
  `6199c0be5cb607950fa3e8f8d276ff0735ae2f13` at tree
  `72f9e28e8e55a99b8d8824697c6f2ea30e118abf`. Its empty checkpoint
  commit preserves the accepted implementation tree; the exact 1,303,606-byte
  binary diff over 69 Story-owned paths has SHA-256
  `ae7a742e724e41ec9199c6e0a04c2d54539f16bbb98904d81fd2cb6ec0f6fbc0`.
  Later review/evidence records are outside this nonrecursive candidate diff.
- Immutable-candidate checkpoint:
  `bmad-4-2-candidate-20260726T065619Z-5f2b90` (bound to
  `6199c0be5cb607950fa3e8f8d276ff0735ae2f13`).
- Full acceptance review iteration 3 returned `REVISE` at the exact T041
  boundary. `AR3-001` found that both the accepted-plan and candidate commits
  carry only `Workflow-Checkpoint`, omitting mandatory `BMad-Story`,
  `Workflow-Scope`, and `Workflow-Stage` trailers. All 21 FRs, 7 AC groups,
  8 SCs, R1-R16, and 12 correction intents otherwise passed acceptance
  inspection. Forward-only replacement checkpoints are required; history will
  not be amended.
- Acceptance-review checkpoint:
  `bmad-4-2-acceptance-review-20260726T073117Z-a31c84` (pending Git binding).
- Full quality review iteration 3 returned `REVISE` at the identical T041
  boundary. `QR3-001` found that the Repository detail UI renders canonical
  confidence values `High` and `Medium` directly, violating FR-019, AC-7, and
  the Simplified-Chinese overview contract. The API wire values remain
  canonical; remediation is limited to presentation mapping and UI regression
  coverage.
- Quality-review checkpoint:
  `bmad-4-2-quality-review-20260726T073255Z-c82f16` (pending Git binding).
- Review-3 corrections are now GREEN. The accepted-plan resolver binds the
  forward-only checkpoint
  `36bd6cccdba1fa341eddc8096a5c2269c62c3118`, verifies its exact plan and
  verdict bytes in tree `54705660bde4e74decf8baaab32ca6046f420152`,
  requires unique ancestry and all four mandatory trailers, and no longer
  requires the empty replacement checkpoint to introduce the accepted files
  in its own diff. The Repository detail dialog localizes only the presentation
  of canonical confidence values (`High` to `高`, `Medium` to `中`).
- T037 retained two genuine RED observations (1/1 binding failure and 2/2
  confidence-presentation failures). T038 then passed 18/18 binding tests, 2/2
  focused dashboard tests, and 104/104 surrounding provider-contract/dashboard
  tests. An independent root rerun also passed 104/104. The first five
  transcript-only evidence files remain immutable failed-capture attempts; the
  complete native-output evidence uses create-new `attempt-0002` paths.
- The former T039-T041 ledger/candidate and T042-T043 review lane remain
  invalidated. They must be regenerated from this correction checkpoint before
  the next Full review boundary.
- Review-3 correction checkpoint:
  `bmad-4-2-review3-correction-20260726T075812Z-91c3af`
  (pending Git binding).
- T001 Current verification was refreshed at clean immutable correction commit
  `80335ff4c6e405cf5b7e5824b2d080c17b65e3b8`, tree
  `02f07501de07ac2470b88a291cc0e8c1a0da32df`. Provider/profiling passed
  107/107 and framework detection passed 48/48. The record now binds the
  replacement accepted-plan checkpoint and preserves all earlier attempts plus
  the exact unavailable-baseline disclosure. No network, URL, Clash, or proxy
  failure occurred.
- T001 refresh checkpoint:
  `bmad-4-2-t001-refresh-20260726T081132Z-6e7a20`
  (pending Git binding).
- T039-T040 regenerated and independently validated the truthful ledger at
  source `d20db9fd25c26e0041197e53d3f77f5e22d687ec`, tree
  `5541e14640a907e2d6cedfcad0c1b28a3a9a2ca5`: 41 linear commits,
  19 legacy groups, 41 immutable Git references, 11 unique checkpoints, and
  20 raw files. Five transcript-only files are retained as capture failures
  and excluded from RED/GREEN claims; the prior candidate and review-3 lane
  remain invalidated history only. No downstream candidate, E2E, image,
  namespace, T049, or T050 completion is claimed.
- Truthful-ledger checkpoint:
  `bmad-4-2-ledger-refresh-20260726T083030Z-48a1de`
  (pending Git binding).
- T041 pinned clean immutable candidate
  `1ce8140d1644eb4f52a5f1cc9f4781b57d04b37c`, tree
  `ac002fa795614a3eaf752922a95a1e7df20a37ef`, as an empty checkpoint
  carrying all four mandatory trailers. Its exact binary diff from
  `e9824df7300634c19d815bdd7ad591b9fb5366d5` is 1,382,640 bytes with
  SHA-256 `e953af5793272a0dfdc3de7a29a01b8d6be9d9cfb9ae8af298166517e25a583f`
  over 72 Story-owned paths. The candidate record is intentionally persisted
  outside the nonrecursive candidate boundary.
- Immutable-candidate checkpoint:
  `bmad-4-2-candidate-20260726T083143Z-a2d6c9`
  (Git-bound to `1ce8140d1644eb4f52a5f1cc9f4781b57d04b37c`).
- Candidate-record checkpoint:
  `bmad-4-2-candidate-record-20260726T083632Z-f41b8e`
  (pending Git binding).
- Full acceptance review iteration 4 returned `ACCEPT` with zero findings at
  candidate `1ce8140d1644eb4f52a5f1cc9f4781b57d04b37c`. It passed
  FR 21/21, AC 7/7, SC 8/8, R1-R16, corrected T001/T035-T038 evidence,
  both four-trailer bindings, Chinese confidence presentation, and forbidden
  scope. This acceptance axis does not substitute for the independent quality
  axis.
- Acceptance-review-4 checkpoint:
  `bmad-4-2-acceptance-review-20260726T085649Z-7c30d5`
  (pending Git binding).
- Full quality review iteration 4 returned `REVISE` at the identical candidate
  with three High findings. `QR4-001` found that boundary whitespace can
  bypass rooted provider-path rejection. `QR4-002` found that invalid Python
  requirement suffixes such as `django???` can create deterministic framework
  facts. `QR4-003` found that same-root direct-scope ambiguity counts only
  catalog-matched manifests and can ignore a valid unmatched peer. Candidate
  identity, evidence integrity, AR3-001, and QR3-001 otherwise passed.
- Quality-review-4 checkpoint:
  `bmad-4-2-quality-review-20260726T085941Z-1f93ac`
  (pending Git binding).
- T051-T054 corrected all three quality-review-4 findings with observed TDD.
  Boundary-whitespace provider paths failed 2/30 before rejection and passed
  30/30 after it. Invalid Python trailing syntax and matched-plus-unmatched
  direct-manifest ambiguity each failed 1/1 before their minimal corrections
  and passed 1/1 afterward. The first surrounding run retained two superseded
  audit-count expectations as failed evidence; the final-byte surrounding
  suites passed 165/165, and root independently repeated 165/165. T041-T050
  are invalidated and unchecked pending a replacement candidate.
- Quality-review-4 correction checkpoint:
  `bmad-4-2-quality4-correction-20260726T092125Z-b7814d`
  (pending Git binding).
- T001 Current verification was refreshed again at correction commit
  `3140978f83f03004b9b356ff14441614303a8380`, tree
  `600f60d97c69003a89158d2e0cf233fcff681352`: provider/profiling passed
  116/116 and framework detection passed 50/50. Prior attempts and the exact
  unavailable-baseline disclosure remain preserved; no network, URL, Clash,
  or proxy failure occurred.
- Post-quality4 T001 checkpoint:
  `bmad-4-2-t001-quality4-refresh-20260726T092740Z-9ae52c`
  (pending Git binding).
- The post-quality4 truthful ledger passed full validation at
  `b57e6088002ea57974d3063cee153852206ded63`: 48 linear commits,
  12 authorities, 51 immutable references, 18 unique checkpoints, 19 legacy
  groups, and 31 raw files. Twenty-six are valid results and five are
  capture-only; the prior candidate/review-4 lane is invalidated and no
  downstream completion is claimed.
- Post-quality4 ledger checkpoint:
  `bmad-4-2-ledger-quality4-refresh-20260726T093944Z-6bc20f`
  (pending Git binding).
- T041 pinned empty fully-trailered replacement candidate
  `14ed1fd2c2c31350f3a9d70852e2d806ed2e1522`, tree
  `d7e1329d1a431dcf8ce6c4875a0b33d5fd533cb0`. Its 1,468,107-byte
  binary diff has SHA-256
  `25a13c76b50dd95ab80a0d19a09c4b929e1186cb4e06acc73d3b8b1fe3b320cc`
  over 74 Story-owned paths.
- Replacement candidate-record checkpoint:
  `bmad-4-2-candidate-record-20260726T094834Z-3d6f90`
  (pending Git binding).
- Final Full acceptance review 5 returned `ACCEPT` with zero findings and
  complete FR 21/21, AC 7/7, SC 8/8, and R1-R16 coverage. Final Full quality
  review 5 also returned `ACCEPT` with zero findings; QR4-001 through QR4-003
  are closed and all reviewed quality axes passed at candidate `14ed1fd2`.
- Final Full-reviews checkpoint:
  `bmad-4-2-full-reviews-20260726T095959Z-83a6e1`
  (pending Git binding).
- T044 E2E was required rather than skipped: the matrix triggered on the
  user-visible Repository dialog, API/persistence/multi-module integration,
  and observable data lifecycle. The combined provider, profiling, framework,
  API, and dashboard integration run passed 198/198 at the unchanged candidate.
  PostgreSQL migration scripts and final Docker image identity remain assigned
  to T045 and T047-T048.
- E2E checkpoint:
  `bmad-4-2-e2e-20260727T004131Z-a46c91`
  (pending Git binding).
- T045 pre-image verification is GREEN at the unchanged T041 product/test
  boundary: provider/profiling 116/116, framework 50/50, executable
  accepted-plan binding 17/17, the complete non-final-image suite 504/504,
  and Repository API/dashboard authorization-redaction coverage 33/33.
  The Web build completed with zero errors; forward and reverse migration
  scripts preserve the accepted two-migration order; the model has no pending
  changes. The 74-path FR-020 audit found no forbidden Scan Coordinator,
  orchestration, Worker Agent/tool, dispatch, or retry change. The sole generic
  filename match is the accepted `RepositoryProfilingCoordinator` language and
  framework persistence seam, not a Scan Coordinator.
- Verification checkpoint:
  `bmad-4-2-verification-20260727T004912Z-d2f47a`
  (pending Git binding).
- T046 cross-validation accepted the historical ledger and the prior
  candidate/E2E/verification byte identities, but rejected the iteration-5
  review artifacts because their persisted schemas and reviewer identity were
  not contract-valid. The user approved create-new review evidence without
  rewriting history in
  `specs/002-language-framework-detection/evidence/t046-review-evidence-remediation-approval.json`.
- Fresh Full acceptance review 6 returned `ACCEPT`. Fresh Full quality review
  6 returned `REVISE` with QR6-001: framework artifacts were inventoried as
  tracked but then read from mutable working-tree bytes, allowing dirty or
  concurrently replaced manifest/lock content to become deterministic facts.
- QR6-001 now has observed behavior-level RED and GREEN evidence. The detector
  pins one validated `HEAD^{commit}` identity, preserves the closed tracked
  inventory, and reads exact committed blobs through non-executing
  `git cat-file`. Dirty direct manifests, dirty paired locks, and a HEAD change
  after inventory cannot alter facts from the pinned Repository state.
- The initial surrounding run failed 7/168 only because the profiling test
  fixture staged files without creating a commit. After the fixture was aligned
  to the immutable Repository contract, worker and root runs passed 168/168,
  the complete non-final-image suite passed 507/507, and the Web build
  completed with zero errors.
- The source/test mutation invalidates the prior T001, T039-T046 candidate
  lane. The old candidate, reviews, E2E, and verification remain historical
  evidence and are not relabelled; a clean correction checkpoint, refreshed
  Current verification and ledger, replacement candidate, both fresh Full
  reviews, E2E, and verification are required before image work.
- QR6 correction checkpoint:
  `bmad-4-2-implement-20260727T014847Z-qr6001`
  (pending Git binding).
- Post-QR6 T001 Current verification is GREEN at clean immutable correction
  commit `38bff96b7a3b4a1dc442e347d26a5c7afbb62b55`, tree
  `e42e961cb401ec7719c96da5047756fa63810035`: provider/profiling
  passed 116/116 and the framework filter passed 53/53. The exact statement
  `Pre-correction T001 baseline observation: unavailable` remains unchanged;
  no current result is labelled historical, pre-correction, RED, or
  contemporaneous ordering.
- Post-QR6 Current-verification checkpoint:
  `bmad-4-2-implement-20260727T020318Z-t001qr6`
  (pending Git binding).
- The post-QR6 truthful ledger now covers 56 linear commits, 15 authorities,
  26 observed checkpoint trailers, 64 immutable blob references, and 43 raw
  references. It preserves T001 attempts 1-9, the QR6 behavior RED/GREEN,
  fixture-alignment failure, user approval, review-6 verdicts, and explicit
  invalidation of candidate `14ed1fd2` plus its reviews, E2E, T045, and T046.
  Root validation passed JSON/source/tree/counts, authority hashes, blob OIDs,
  every raw hash/length, all 26 checkpoint identities/trailers, the exact
  unavailable statement, and the absence of a current review-6/T046 claim.
  Earlier validator-script property mistakes are preserved as failed attempts.
- Post-QR6 ledger checkpoint:
  `bmad-4-2-implement-20260727T022310Z-ledgerqr6`
  is historical-only: its `Workflow-Scope` and checkpoint suffix violate the
  checkpoint contract, and its ledger prematurely claimed T040 acceptance.
- T040 independent audit returned REVISE on three ledger bookkeeping defects:
  three checkout-normalized authority hashes, the three malformed checkpoint
  trailers, and the premature T040 completion claim. No product-code or
  behavior defect was found.
- Forward-only replacements bind the unchanged QR6 correction and Current
  verification bytes without rewriting history:
  `bmad-4-2-implement-20260727T025500Z-a61c3e` at
  `2d1ce00574a38b6bbe082000d17a04a17ee3c8c3`, and
  `bmad-4-2-verification-20260727T025501Z-b72d4f` at
  `d307a27b7502ec96a405a2b5d814beb826ca47cc`.
- The ledger now uses exact Git-blob SHA-256 values, marks all three malformed
  commits historical-only, and leaves T040 pending for a fresh independent
  audit. Correction checkpoint
  `bmad-4-2-implement-20260727T023616Z-c83e5a` is pending Git binding.
- T040 independently ACCEPTED the corrected ledger at focused state boundary
  `dcaca0e2da62ccf7b4f31a36a9abde396c9fc74c`. All 15 authority hashes,
  64 immutable blob references, 43 current raw references, two retained
  historical-green artifacts, temporal disclosures, and forward-only
  checkpoints passed. The final focused rerun also verified exact
  `committed_paths` and the allowed `artifact-hash` evidence kind. T041 is now
  the next permitted task.
- T041 pinned clean immutable candidate
  `614962322e9f3578980a6fa28bc8897d22b66c58`, tree
  `29206ebcab35a8d53d4323f23602c559909a857f`, through checkpoint
  `bmad-4-2-candidate-20260727T030148Z-f15b2c`. The empty checkpoint has the
  same tree as parent `fb3eca2680cbd2a66384005bbea1cac6527bd84d`.
  Its exact 1,616,158-byte binary diff from the Story base has SHA-256
  `907256b4e4a88cf0c15ec1c2d23de2eee73f7b408189713fdf760c8ce3aef2ae`
  over 80 owned paths. Every non-deleted owned path is bound to its immutable
  candidate Git-blob SHA-256, and the initial candidate status is clean.
- Evidence-grammar implementation checkpoint:
  `bmad-4-2-implement-20260722T080736Z-e6c4b1` (pending Git binding).
- Provider-path redaction implementation checkpoint:
  `bmad-4-2-implement-20260722T073424Z-f3a7c2` (pending Git binding).
- Owner-eligibility implementation checkpoint:
  `bmad-4-2-implement-20260722T061843Z-d9b4e2` (pending Git binding).
- Analyzed-tasks checkpoint:
  `bmad-4-2-analyze-20260726T045251Z-83fcc2` (pending Git binding).
- Accepted plan checkpoint:
  `bmad-4-2-plan-20260726T041520Z-3fbfd5` (pending Git binding).
- Implementation checkpoint:
  `bmad-4-2-implement-20260720T104752Z-c7a3e1`
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
- `specs/002-language-framework-detection/reviews/plan-review-4.json`
- `specs/002-language-framework-detection/reviews/plan-review-5.json`
- `specs/002-language-framework-detection/reviews/plan-review-6.json`
- `specs/002-language-framework-detection/reviews/plan-review-7.json`
- `specs/002-language-framework-detection/reviews/plan-review-8.json`
- `specs/002-language-framework-detection/evidence/planned-change-authorization-2.json`
- `specs/002-language-framework-detection/reviews/analyze-5.json`
- `specs/002-language-framework-detection/reviews/analyze-6.json`
- `specs/002-language-framework-detection/reviews/analyze-7.json`
- `specs/002-language-framework-detection/evidence/analyze-remediation-approval-2.json`
- `specs/002-language-framework-detection/evidence/story-4-2-current-verification.json`
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
- `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`
- `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`
- `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`
- `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`
- `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`
- `src/dotnet/Vulgata.Web/Program.cs`
- `tests/Vulgata.Tests/DashboardPanelTests.cs`
- `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`
- `tests/Vulgata.Tests/RepositoryApiTests.cs`
- `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
