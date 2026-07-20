---

description: "Dependency-ordered RED-first tasks for Story 4.2 language and framework detection"
---

# Tasks: Language and Framework Detection

**Input**: Accepted design documents from `specs/002-language-framework-detection/`

**Pinned boundary**: immutable accepted task source commit `366f25824b38627d3e840ebe5de4533ec44e84cf` must be an ancestor of implementation `HEAD`; corrected specification SHA-256 `924d9d5d1f104e4206f145f565eb7f3e8c813c67fdd88da2abf55cb08456f956`; corrected plan SHA-256 `e5d9cb5593d0ed60cee59c76503c5ae222af6dd3b9d8afbc4cebd18850b2d306`; corrected quickstart SHA-256 `d5e099233187b19484cc5f883f7dae528c7790157623517f17341f0cd5eeef9f`; planned Scan-strategy authorization recorded in `specs/002-language-framework-detection/workflow-state.json`.

**Tests**: Mandatory. Every TDD group starts with a runnable behavior-specific RED assertion, not a compile failure or broken fixture. Each following GREEN task reruns the named focused tests and records the original RED assertion plus the GREEN command in the implementation evidence.

**Organization**: Shared persistence and provider-completeness blockers are foundational. The four prioritized user stories remain independently reviewable increments. Tasks touching `CodeGraphContextProvider.cs`, `RepositoryProfilingCoordinator.cs`, `ScanRunRepository.cs`, shared DTOs, or the Repository dialog are serialized; they are never blanket-parallelized.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run concurrently only after its stated dependencies are complete and only when it owns different files.
- **[Story]**: Maps the task to one of the four user stories in `spec.md`.
- Every task names its exact owned file path or validation input path.

## Phase 1: Setup (Accepted Boundary)

**Purpose**: Establish reproducible evidence without changing production behavior.

- [X] T001 Verify with `git merge-base --is-ancestor 366f25824b38627d3e840ebe5de4533ec44e84cf HEAD` that the immutable accepted task source is an ancestor of implementation `HEAD`, require a clean worktree at implementation activation, verify specification SHA-256 `924d9d5d1f104e4206f145f565eb7f3e8c813c67fdd88da2abf55cb08456f956`, plan SHA-256 `e5d9cb5593d0ed60cee59c76503c5ae222af6dd3b9d8afbc4cebd18850b2d306`, and quickstart SHA-256 `d5e099233187b19484cc5f883f7dae528c7790157623517f17341f0cd5eeef9f`, then run the pre-change focused baseline from `specs/002-language-framework-detection/quickstart.md`, `specs/002-language-framework-detection/workflow-state.json`, `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/DashboardPanelTests.cs`, and `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`; allow descendant workflow checkpoints but stop on ancestry, cleanliness, or artifact-hash failure

**Checkpoint**: The accepted, authorized source boundary and existing test baseline are reproducible.

---

## Phase 2: Foundational (Shared Blocking Prerequisites)

**Purpose**: Establish the atomic aggregate, additive storage, historical compatibility, and provider completeness correction required by every story.

**Critical sequencing**: Complete T002-T008 in order. T002 must be observed RED before T003-T005. T007 must be observed RED before T008. No user-story coordinator work starts before this phase is GREEN.

### TDD Group F1 - Atomic persistence model and exact recovery

- [X] T002 Add runnable reflection/SQLite and injected-final-commit RED tests in `tests/Vulgata.Tests/RepositoryProfilingTests.cs` proving the absent `CodeUnitFramework`/`ScanRunLanguageSummary` constraints, incomplete `RepositoryProfileDraft` rejection, historical nullable Story 4.1 language readability, additive rollback boundary, and the current tracked-Completed recovery defect; assert that a failed final commit leaves zero provider-derived Code Units/Edges/unresolved relationships/frameworks/summaries, preserves local candidate-filter audit, reloads the durable still-`Profiling` ScanRun in a fresh unit of work, and persists one sanitized `Failed` transition
- [X] T003 Implement the minimum Core aggregate contract needed to turn T002 GREEN: required normalized language for new completed drafts, `High|Medium` framework confidence, bounded `CodeUnitFramework`, `ScanRunLanguageSummary`, closed candidate-outcome reasons, complete `RepositoryProfileDraft`, and aggregate navigation/invariants in `src/dotnet/Vulgata.Core/Entities/CodeUnit.cs`, `src/dotnet/Vulgata.Core/Entities/CodeUnitFramework.cs`, `src/dotnet/Vulgata.Core/Entities/ScanRun.cs`, `src/dotnet/Vulgata.Core/Entities/ScanRunLanguageSummary.cs`, `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`, and `src/dotnet/Vulgata.Core/DomainServices/IScanRunRepository.cs`; rerun the Core/draft tests from T002 and record GREEN evidence
- [X] T004 Implement one atomic complete-draft write plus rollback, ChangeTracker clear/detach, fresh-unit-of-work reload, and separate sanitized failure persistence in `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, and `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`; do not use the failed tracked ScanRun status to decide recovery, and rerun the injected-final-commit cases from T002 to GREEN
- [X] T005 Create the mappings in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/CodeUnitFrameworkConfiguration.cs` and `src/dotnet/Vulgata.Infrastructure/Data/Configurations/ScanRunLanguageSummaryConfiguration.cs`, then run `dotnet ef migrations add AddLanguageFrameworkDetection --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web --context VulgataDbContext`; own the uniquely generated `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddLanguageFrameworkDetection.cs`, matching Designer file, and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`; enforce composite aggregate FKs, unique `(CodeUnitId, FrameworkName)`, summary key, checks and indexes, leave historical `CodeUnits.Language` nulls untouched, and make `Down` remove only the two Story 4.2 tables/indexes before rerunning T002 GREEN
- [X] T006 Refactor the F1 implementation without changing behavior, then run the focused persistence tests in `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, locate exactly one generated `*_AddLanguageFrameworkDetection.cs` migration by suffix/metadata under `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`, and inspect its generated migration script; record that the complete draft is atomic, exact recovery works, and historical Story 4.1 rows remain readable

### TDD Group F2 - Story 4.1 provider-completeness correction

- [X] T007 Add runnable RED cases in `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs` proving the private gateway currently lacks process-local `TOOL_RESULT_LIMITS={"execute_cypher_query":250001}`, a default-100 shortened response can be accepted, and any present, duplicate, wrong-typed, or inconsistent `data.truncated`/`data.result_limit` metadata is not yet rejected atomically; retain exact two-query and combined-250,000-row expectations
- [X] T008 Implement the private gateway environment and fail-closed reserved completeness-envelope parsing in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`, and correct the owned contract in `specs/001-codegraph-extraction/contracts/repository-profiling-api.md`; set no global/other tool limit, add no pagination/retry/third query, reject 250,001 combined rows, and rerun every T007 case to GREEN

**Checkpoint**: Atomic persistence, exact recovery, additive schema, historical compatibility, and complete provider envelopes are GREEN and block no user story.

---

## Phase 3: User Story 1 - Trust the Language Profile (Priority: P1) MVP

**Goal**: Attribute every retained Code Unit from one normalized structural parser identity per file and expose a distinct-source-file language breakdown.

**Independent Test**: Profile a mixed-language fixture with multiple Code Units per file, extension/token mismatch, all eleven named identifiers plus at least one additional valid token, and verify every retained Code Unit plus the authorized overview without using extensions.

### TDD Group 1A - Provider language projection

- [X] T009 [US1] Add runnable JSON/reflection RED cases in `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs` for the exact nullable `parser_language` Projection A alias and the explicit fixture matrix C#→`c_sharp`, Java→`java`, Python→`python`, JavaScript→`javascript`, TypeScript→`typescript`, Go→`go`, Kotlin→`kotlin`, Swift→`swift`, Rust→`rust`, C→`c`, C++→`cpp`, plus additional valid token `tsx`; also prove Form-KC/trim/invariant-lower normalization, open grammar `[a-z][a-z0-9_+#.-]{0,63}`, exact duplicate observations, wrong-type/invalid-token atomic rejection, same-path conflict atomic rejection, and unchanged Projection B/`cgc-v1` identity; ensure failures are assertion failures against current behavior
- [X] T010 [US1] Extend only Projection A, the closed row grammar, provider transport models, and per-path structural-language reconciliation in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs` and `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`; keep `File` null valid transport, forbid extensions/frameworks/LLMs as language authority, and rerun T009 to GREEN

### TDD Group 1B - Language assignment and summaries

- [X] T011 [US1] Add runnable RED integration cases in `tests/Vulgata.Tests/RepositoryProfilingTests.cs` proving one parser token applies to every Code Unit on a path, extension/token mismatch follows the parser, a novel valid token persists, language summaries count distinct retained paths rather than Code Units, an incomplete/conflicting provider result persists no provider-derived aggregate, and every new completed draft rejects null/empty/invalid or per-path-inconsistent language
- [X] T012 [US1] Implement language assignment, distinct-path summary construction, complete-draft validation, and mapping in `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Core/Entities/CodeUnit.cs`, `src/dotnet/Vulgata.Core/Entities/ScanRun.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`; retain the one-Repository/one-ScanRun/1,800-second lifecycle and rerun T011 to GREEN

### TDD Group 1C - Authorized language overview

- [X] T013 [US1] Add runnable RED response/component cases in `tests/Vulgata.Tests/RepositoryApiTests.cs` and `tests/Vulgata.Tests/DashboardPanelTests.cs` proving the existing authorized scan-run shape/dialog lacks every sorted language and distinct source-file count, while preserving the established scan-run routes, Repository visibility, not-found redaction, and all injected component services
- [X] T014 [US1] Extend the existing response and dialog only in `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, and `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`; render `语言分布` and ordinally sorted technical tokens/counts in Simplified Chinese without a parallel endpoint/policy, then rerun T013 to GREEN
- [X] T015 [US1] Refactor US1 without altering contracts and run the exact twelve-identifier matrix from T009 plus the US1 filters from `specs/002-language-framework-detection/quickstart.md` against `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`, `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs`; record RED-to-GREEN evidence for FR-001-FR-003, FR-005-FR-006 and SC-001-SC-002

**Checkpoint**: US1 is independently demonstrable as the MVP.

---

## Phase 4: User Story 2 - Use Auditable Framework Facts (Priority: P2)

**Goal**: Produce deterministic, scoped, deduplicated framework facts with exact bounded evidence and no execution, network, package-manager, or LLM authority.

**Independent Test**: Profile committed NuGet, npm, Maven, and Python fixtures containing direct, paired-lock, nested, duplicate, unsupported, missing, ambiguous, and out-of-scope evidence and verify exact facts, confidence, source, and intentional absence.

### TDD Group 2A - Closed catalog

- [X] T016 [P] [US2] Add runnable RED catalog cases in `tests/Vulgata.Tests/FrameworkDetectionTests.cs` for closed-schema startup validation, unknown fields, duplicate/ambiguous coordinates, invalid framework/language/evidence kinds, exact matching only, and all fixed initial coordinates for `aspnet-core`, `blazor`, `spring-boot`, `react`, `express`, and `django`
- [X] T017 [US2] Implement the embedded versioned catalog and startup validation in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkCatalog.cs`, `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/Catalog/framework-catalog.v1.json`, `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj`, and `src/dotnet/Vulgata.Web/Program.cs`; log only catalog version/SHA-256 safe metadata and rerun T016 to GREEN

### TDD Group 2B - Bounded deterministic evidence parsers

- [X] T018 [US2] Extend `tests/Vulgata.Tests/FrameworkDetectionTests.cs` with runnable RED fixtures proving inventory uses only `git ls-files --cached`; NuGet literal unconditional `.csproj` plus paired `packages.lock.json`, npm literal dependency maps plus paired lock v2/v3, direct project-level Maven, and literal Python requirements map exactly; untracked/ignored evidence, unpaired locks, dynamic/conditional constructs, Maven inheritance/profiles/properties/dependencyManagement, includes/URLs/editables/markers, Gradle/MSBuild/build/restore/install/scripts/imports/symbols/files/external services/LLMs create no fact
- [X] T019 [US2] Implement the smallest closed non-executing evidence inventory/parsers and exact catalog matching in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`; retain arbitrary package text only after exact catalog match and rerun T018 to GREEN

### TDD Group 2C - Scope, language, and ambiguity

- [X] T020 [US2] Extend `tests/Vulgata.Tests/FrameworkDetectionTests.cs` with runnable RED cases for deepest ancestor scope per ecosystem, parser-language compatibility, same-ecosystem same-root ambiguity, independent co-located ecosystems, and unprovable or out-of-scope evidence producing no fact
- [X] T021 [US2] Implement only scope-root selection, parser-language compatibility, and ambiguity handling in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`, then rerun T020 to GREEN

### TDD Group 2D - Ranking, deduplication, and safe locators

- [X] T022 [US2] Extend `tests/Vulgata.Tests/FrameworkDetectionTests.cs` with runnable RED cases for direct `High` over paired-lock `Medium`, deeper scope then ordinal evidence-locator tie-breaking, exact duplicate collapse, the three closed locator forms, 4,096-character locator and 2,048-character path bounds, and persistence of no arbitrary package or source text
- [X] T023 [US2] Implement only deterministic ranking/deduplication and safe bounded locator construction in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`, then rerun T022 to GREEN

### TDD Group 2E - Per-artifact safety and isolation

- [X] T024 [US2] Extend `tests/Vulgata.Tests/FrameworkDetectionTests.cs` with runnable RED cases for the 8 MiB per-artifact and depth-64 bounds, malformed/unsupported evidence, Repository-relative escape, reparse-point traversal, bounded Chinese diagnostics, and isolation that lets independent valid artifacts continue without raw line/value/content/absolute-path disclosure
- [X] T025 [US2] Implement only per-artifact bounds, path/reparse safety, closed audit outcomes, and isolated continuation in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`, then rerun T024 to GREEN

### TDD Group 2F - Repository-global fail-closed limits

- [X] T026 [US2] Extend `tests/Vulgata.Tests/FrameworkDetectionTests.cs` with runnable RED cases for the 64 MiB aggregate, 1,000 supported artifacts, 100,000 parsed entries, 250,000 candidate facts, 64 final facts per Code Unit, 100 retained details, and unexpected detector failure; assert every global overflow/fault stops before structural persistence instead of silently producing partial facts
- [X] T027 [US2] Implement only Repository-global accounting and fail-closed behavior in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`, then rerun T026 to GREEN

### TDD Group 2G - Framework persistence and authorized overview

- [X] T028 [US2] Add runnable RED aggregate/integration cases in `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs` proving absent scoped `CodeUnitFramework` persistence, composite FK/uniqueness, strongest confidence/source selection, language-compatible one-pass fan-out, durable complete framework-audit counts with bounded shared samples, and authorized Chinese framework evidence rendering
- [X] T029 [US2] Integrate the detector and framework facts into the same complete draft and existing authorized overview in `src/dotnet/Vulgata.Core/Entities/ScanRun.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/ScanRunConfiguration.cs`, an additive EF migration and model snapshot, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, and `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`; persist the nonnegative complete framework-audit outcome count, perform one bounded Repository evidence pass followed by pure deterministic Code Unit fan-out, render `框架`, `置信度`, `检测依据`, and `框架证据警告`, share the 100-detail sample budget, sort deterministically, rerun T028 to GREEN, and record FR-008-FR-014/FR-021 plus SC-003/SC-005 evidence

**Checkpoint**: US2 facts are deterministic, scoped, auditable, bounded, and independently testable.

---

## Phase 5: User Story 3 - Audit Unknown Languages (Priority: P3)

**Goal**: Turn every otherwise eligible candidate with no valid structural parser identity into exactly one terminal `Filtered/UnknownLanguage` audit outcome and no fabricated or dispatchable Code Unit.

**Independent Test**: Profile candidates with and without structural language observations, including a supported empty file, and verify retained units, exact terminal outcomes, complete distinct counts, and sorted Repository-relative paths.

### TDD Group 3A - Reconciliation and persistence

- [X] T030 [US3] Add runnable RED cases in `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs` and `tests/Vulgata.Tests/RepositoryProfilingTests.cs` with two explicit current baselines: a supported zero-structure provider result can currently create a synthetic File Code Unit, while a candidate absent from provider output currently becomes `Skipped/NoCodeUnitExtracted`; assert the desired result for both is exactly one terminal `Filtered/UnknownLanguage`, zero retained Code Units, Edge endpoints, or unresolved relationships for that path, and therefore no Code Unit available to any later dispatch stage
- [X] T031 [US3] Implement candidate-to-provider reconciliation and terminal outcome construction in `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`; reserve `UnknownLanguage` for true absence in an otherwise valid complete response, remove all structure for that path before mapping, deduplicate once per eligible path, and rerun T030 to GREEN

### TDD Group 3B - Complete authorized audit overview

- [X] T032 [US3] Add runnable RED response/component cases in `tests/Vulgata.Tests/RepositoryApiTests.cs` and `tests/Vulgata.Tests/DashboardPanelTests.cs` for the exact distinct unknown count and all ordinally sorted unique Repository-relative paths, with no sampling up to 10,000 paths/16 MiB and fail-before-profile overflow rather than silent omission
- [X] T033 [US3] Extend the existing summary and dialog in `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, and `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`; render `未知语言文件` and complete safe paths under the accepted bound, add no parallel overview, and rerun T032 to GREEN
- [X] T034 [US3] Refactor and run the independent US3 cases in `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`, `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs`; record RED-to-GREEN evidence for FR-004, FR-007, SC-004, and the explicit absence of any retained Code Unit for an unknown candidate

**Checkpoint**: US3 exposes every bounded unknown-language gap without inventing source structure.

---

## Phase 6: User Story 4 - Preserve Safe and Authorised Profiling (Priority: P4)

**Goal**: Isolate invalid individual evidence, fail atomically on provider/global failures, disclose no unsafe content, preserve existing Repository authorization/redaction, and keep all new user-visible prose Simplified Chinese.

**Independent Test**: Exercise malformed provider rows, truncation, conflicts, unsafe/reparse/invalid artifacts, global bounds, injected final persistence failure, authorized/hidden/revoked users, and inspect all durable/user-visible outputs for exact atomicity and redaction.

### TDD Group 4A - Provider and Repository-global atomicity

- [X] T035 [US4] Add runnable RED integration cases in `tests/Vulgata.Tests/RepositoryProfilingTests.cs` for malformed/wrong-typed provider language, language conflict, truncation, provider process/query/cleanup failure, and Repository-global detector overflow/fault; assert each retains only local pre-provider candidate audit, commits zero provider-derived structure/framework/summary rows, and terminalizes the ScanRun safely
- [X] T036 [US4] Harden only provider/global failure ownership and atomic structural boundaries in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`, `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, and `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, then rerun T035 to GREEN

### TDD Group 4B - Individual evidence isolation and redaction

- [X] T037 [US4] Add runnable RED integration cases in `tests/Vulgata.Tests/RepositoryProfilingTests.cs` for malformed, unsupported, ambiguous, escape, reparse, and oversize individual evidence; assert independent valid artifacts continue, the invalid artifact creates only its bounded closed outcome, and no source content, absolute host path, arbitrary package value, raw parse diagnostic, credential, or token survives
- [X] T038 [US4] Implement only individual-evidence isolation and bounded closed Chinese outcomes in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, then rerun T037 to GREEN

**Final-commit recovery ownership**: Foundational T002-T004 is the sole RED/GREEN implementation cycle for injected final-commit rollback, detach, fresh-unit reload, and sanitized durable terminalization. US4 reruns that focused GREEN evidence in T044; it does not attempt a second artificial RED.

### TDD Group 4C - Durable and user-visible disclosure

- [X] T039 [US4] Add runnable RED persistence/response/component cases in `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs` inspecting all durable and rendered failure details for raw provider rows/queries, source text, absolute host paths, arbitrary package values, credentials, tokens, unbounded diagnostics, unsafe timing detail, or non-Chinese prose
- [X] T040 [US4] Remove only the durable/user-visible disclosures found by T039 using safe closed reason mapping in `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, and `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`, then rerun T039 to GREEN

### TDD Group 4D - Existing authorization and objective Chinese presentation

- [X] T041 [US4] Add runnable RED authorization and representative-overview cases in `tests/Vulgata.Tests/RepositoryApiTests.cs` and `tests/Vulgata.Tests/DashboardPanelTests.cs` for unauthenticated, non-`ManagementAccess`, hidden System Repository, hidden standalone Repository, and visibility revoked during profiling/read; assert the established 401/403 or not-found-redacted outcome as applicable and zero profiling disclosure, while representative authorized fixtures expose 100% of every recorded language/distinct-file count, every framework with strongest retained confidence and safe evidence source, every bounded `UnknownLanguage` path, and all required Simplified-Chinese labels with zero omission or developer interpretation
- [X] T042 [US4] Preserve existing route groups, `ManagementAccess`, `GetVisibleByIdAsync`, standalone visibility, completion-time visibility recheck, and not-found redaction while implementing the complete objective overview contract from T041 in `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, and `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`; introduce no role/policy/endpoint and rerun T041 to GREEN
- [X] T043 [US4] Verify the bounded Story 4.2 source diff contains zero Scan Coordinator, Orchestrator, Worker Agent/tool, dispatch, scheduling, retry, LLM language authority, Communication Pattern Catalog, Story 4.3 question-gate, document-generation, or downstream language-support changes by reviewing `src/dotnet`, `tests/Vulgata.Tests`, `docker`, and `specs/001-codegraph-extraction/contracts/repository-profiling-api.md`; treat any such path or behavior as a blocking FR-020 failure
- [X] T044 [US4] Rerun the foundational injected-final-commit recovery cases from T002-T004 plus the independent US4 focused filters from `specs/002-language-framework-detection/quickstart.md` against `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs`; record GREEN recovery evidence and RED-to-GREEN evidence for FR-015-FR-020 and SC-006-SC-008, including the exact objective SC-008 fixture assertions, with no waived authorization, security, atomicity, disclosure, or Chinese-UI assertion

**Checkpoint**: US4 preserves atomicity, bounded diagnostics, established authorization, and Chinese-only presentation.

---

## Phase 7: Polish and Mandatory Cross-Cutting Gates

**Purpose**: Prove the exact final provider image, additive deployment/rollback, full regression suite, and scope/traceability boundary. These tasks do not waive earlier RED evidence.

### TDD Group P1 - Mandatory exact final-image gate

- [ ] T045 Add the completion-lane RED assertions and fixtures in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`, `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py`, `tests/Vulgata.Tests/CodeGraphProviderContract/ProviderFixture/empty_file.py`, `tests/Vulgata.Tests/CodeGraphProviderContract/ProviderFixture/main.py`, and `tests/Vulgata.Tests/CodeGraphProviderContract/ProviderFixture/many_rows.py`; first observe failure when required accepted-correction/image inputs are absent, then observe failure against the old 100-row limit/old empty-file expectation while requiring exact pin/lock/wheel/query hashes, both projections above 100 complete rows, parser-language bindings, immutable two-query snapshot, atomic drift rejection, cleanup, and forbidden-retention evidence
- [ ] T046 Update the final-image verifier and canonical production/test constants in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`, `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py`, `tests/Vulgata.Tests/CodeGraphProviderContract/ProviderFixture/empty_file.py`, `tests/Vulgata.Tests/CodeGraphProviderContract/ProviderFixture/main.py`, `tests/Vulgata.Tests/CodeGraphProviderContract/ProviderFixture/many_rows.py`, and `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`; require, never early-return on, completion-lane inputs and prove the exact private `250001` environment, no truncation metadata, `parser_language` aliases, per-file identities, one empty-file `Filtered/UnknownLanguage`, 101+ rows per projection, atomic completeness rejection, cleanup, and zero forbidden retention without modifying or retrying historical qualification attempt-0003
- [ ] T047 Build the exact final runtime image from `docker/Dockerfile`, set the required image/accepted-correction inputs, and run only `Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract` from `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`; completion is blocked unless the final-image gate is GREEN with exact evidence

### Deployment and regression gates

- [ ] T048 Locate exactly one generated `*_AddLanguageFrameworkDetection.cs` migration and matching Designer by suffix/metadata under `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`, then rehearse additive upgrade and rollback with that migration and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`: inspect generated SQL, back up/stage, prove a historical Story 4.1 ScanRun remains readable, run a fresh Story 4.2 profile and verify relational invariants, and confirm `Down` drops only Story 4.2 tables/indexes without backfill/rewrite/delete
- [ ] T049 Run `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`, the full `tests/Vulgata.Tests/Vulgata.Tests.csproj`, `git diff --check`, and inspect `git status --short` from `specs/002-language-framework-detection/quickstart.md`; fix regressions only through a new behavior-specific RED assertion before production changes
- [ ] T050 Audit the final diff against FR-001-FR-021, AC-1-AC-7, SC-001-SC-008, `specs/002-language-framework-detection/plan.md`, `specs/002-language-framework-detection/data-model.md`, all files under `specs/002-language-framework-detection/contracts`, and the approved boundary in `specs/002-language-framework-detection/workflow-state.json`; block completion for any missing RED/GREEN evidence, non-additive migration, escaped path, weakened final-image/authorization gate, forbidden scope, unresolved item, or undocumented accepted-design deviation

**Checkpoint**: Exact final image, migration/rollback, full suite, scope, and traceability gates are all GREEN.

---

## Dependencies and Execution Order

### Phase dependency graph

```text
T001
  -> T002 RED -> T003 -> T004 -> T005 -> T006
  -> T007 RED -> T008
  -> US1: T009 RED -> T010 -> T011 RED -> T012 -> T013 RED -> T014 -> T015
  -> US2: T016 RED -> T017 -> T018 RED -> T019
          -> T020 RED -> T021 -> T022 RED -> T023
          -> T024 RED -> T025 -> T026 RED -> T027
          -> T028 RED -> T029
  -> US3: T030 RED -> T031 -> T032 RED -> T033 -> T034
  -> US4: T035 RED -> T036 -> T037 RED -> T038
          -> T039 RED -> T040 -> T041 RED -> T042
          -> T043 -> T044
  -> T045 RED -> T046 -> T047 -> T048 -> T049 -> T050
```

### User story dependencies

- **US1 (P1)** starts after T008 and is the MVP.
- **US2 (P2)** catalog/detector work may begin after T006 while US1 is active, but T028-T029 wait for T015 because they share persistence/coordinator/DTO/API/UI seams.
- **US3 (P3)** depends on US1 provider-language reconciliation and starts after T015.
- **US4 (P4)** validates the integrated language/framework/unknown result and starts after T029 and T034.
- **Final gates** require every selected story complete; the final-image gate is mandatory and cannot be skipped for missing environment.

### Non-parallel seams

- Tasks modifying `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs` run in ID order.
- Tasks modifying `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, or the final migration run in ID order.
- Tasks modifying `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, or `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor` run in ID order.
- RED tests sharing `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, or `tests/Vulgata.Tests/DashboardPanelTests.cs` are serialized with their corresponding GREEN task.

## Parallel Opportunities

- After T006, isolated catalog/detector T016-T027 can run in parallel with US1 tasks through T014 because they own `FrameworkDetectionTests.cs` and `Infrastructure/FrameworkDetection`; their coordinator/persistence integration T028-T029 remains blocked on T015.
- Test execution may shard by fully qualified class only when no task is editing that class.
- No persistence/provider/coordinator/API/UI/final-image work is marked parallel because these seams share state, canonical constants, or observable contracts.

## Implementation Strategy

### MVP first

1. Complete T001-T008.
2. Complete T009-T015 for US1.
3. Stop and validate the mixed-language distinct-file overview independently.

### Incremental delivery

1. Foundation: atomic draft, exact recovery, migration, provider completeness.
2. US1: authoritative language profile and breakdown.
3. US2: deterministic framework facts and evidence through bounded TDD groups T016-T029.
4. US3: complete unknown-language audit in T030-T034.
5. US4: atomicity, isolation, recovery, disclosure, authorization, and Chinese-UI boundaries in T035-T044.
6. Mandatory exact final-image, migration/rollback, regression, and traceability gates.

## Requirement and Design Coverage

All functional requirements are explicitly covered: FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-007, FR-008, FR-009, FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, FR-016, FR-017, FR-018, FR-019, FR-020, and FR-021. All accepted criteria are explicitly covered: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, and AC-7.

| Accepted behavior | Task coverage |
| --- | --- |
| FR-001-FR-003; AC-1; SC-001 | T007-T012, T015, T045-T047 |
| FR-004, FR-007; AC-6; SC-004 | T030-T034, T045-T047 |
| FR-005-FR-006; AC-2; SC-002/SC-008 | T002-T006, T011-T015 |
| FR-008-FR-014, FR-021; AC-3-AC-5; SC-003/SC-005 | T016-T029 |
| FR-015-FR-017; AC-7; SC-006 | T002-T008, T024-T027, T035-T040, T045-T048 |
| FR-018-FR-019; AC-7; SC-007/SC-008 | T013-T014, T028-T029, T032-T033, T039-T044 |
| FR-020 forbidden scope | T043, T050 |
| Story 4.1 completeness correction | T007-T010, T045-T047 |
| Additive migration and rollback | T002-T006, T048 |
| Exact atomic final-persistence recovery | T002-T004 |
| Final-image completion gate | T045-T047 |

## Task Count

| Phase | Count |
| --- | ---: |
| Setup | 1 |
| Foundational | 7 |
| US1 | 7 |
| US2 | 14 |
| US3 | 5 |
| US4 | 10 |
| Polish/gates | 6 |
| **Total** | **50** |

## Notes

- Every implementation task follows an observable RED task and includes focused GREEN evidence; refactoring is performed only while those tests remain GREEN.
- Technical provider language tokens, framework names, and exact safe evidence locators remain untranslated facts; all prose labels/warnings/errors are Simplified Chinese.
- No task authorizes Worker Agent tooling, orchestration, dispatch, retry, LLM fact authority, Story 4.3, or a weaker authorization/final-image gate.
