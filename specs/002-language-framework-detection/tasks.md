---

description: "Dependency-ordered remediation tasks for Story 4.2 language and framework detection"
---

# Tasks: Language and Framework Detection

**Stage**: `tasks`

**BMad Story**: `4-2-language-and-framework-detection` (lifecycle remains `review`; this artifact does not advance or repair lifecycle state)

**Accepted plan checkpoint**: `216985b812613ad20994d300fae072b69093e6b6` (tree `f766a5866170751b74eca8c5e5307c639cbf8ecd`, source parent `eefa1794a57227428376a6d838593485e491a79b`)

**Accepted plan diff identity**: `ad981b3329b7a6ece656e9e5c19ca3601a966a43ab82512406b5246d4f1ea350`
**Authorization**: `specs/002-language-framework-detection/evidence/planned-change-authorization-2.json` permits task generation for validated NuGet/npm lock-owner filtering; it waives no analysis, TDD, review, E2E, security, verification, or Git gate.

**Accepted artifact hashes**: `spec.md` `924d9d5d1f104e4206f145f565eb7f3e8c813c67fdd88da2abf55cb08456f956`; `plan.md` `1ff1d9cdcc715826229bcbf2981f18d221f3d067a0d03de19f1e79b50d0f18df`; `research.md` `306d52e498537b4fe9c4ed4478731dbac9569f34df0aebf8f9291337ca41dec9`; `data-model.md` `76130d3cd76e029e0e112041be05b4822dcd15e430f38ff3502b53a228b3288c`; `quickstart.md` `095b91d28c4c4517c3d8981011c8838fcafdf4b1043b71fc861164b723155664`; Story 4.2 contracts `74f71a218adca654b79d24bd1558f585b9ae7ff7ec95ca5370f5d153d7826181`, `b5503794a0b05076b441f684b50948b419bcfdd7d21736f0f5430de7eaf04d9b`, `b3b7d0c3cfe6a236d4b25b54239cf207fe546d2518956afb9d54abdae7c94dad`; Story 4.1 owning contract `ca2086166c91820e48f3e5869374ffa1c5fcb8fdddf20480e402194856238124`.

## Evidence Rules

- Checked tasks below preserve only behavior and files still proven by current Git/source/tests. They do **not** assert that historical raw RED output or checkpoint trailers exist.
- Unavailable historical raw RED output and checkpoint trailers must be recorded as unavailable under the approved Story 4.2-only exception. A current GREEN rerun is never historical RED evidence.
- Every iteration-2 behavior correction begins with an observed runnable assertion failure. Compile failures, broken fixtures, fabricated output, backdating, amended history, and production-before-RED are blockers.
- Each RED/GREEN task records command, exit code, immutable source commit, observed assertion, and raw-output path in the implementation evidence owned by the sprint loop.
- No task adds or changes a Scan Coordinator, Orchestrator, Worker Agent/tool, dispatch, scheduling, retry, LLM fact authority, Communication Pattern Catalog, Story 4.3 behavior, document generation, or downstream language-support promise.

## Phase 1: Activation and Preserved Baseline

**Purpose**: Pin the accepted task source and retain only still-valid implementation facts.

- [ ] T001 Verify `216985b812613ad20994d300fae072b69093e6b6` is an ancestor of `HEAD`, verify the accepted hashes above and `plan-review-6.json` ACCEPT plus `planned-change-authorization-2.json`, require no unrelated changes in `git status --short`, and run the pre-correction focused baseline commands in `specs/002-language-framework-detection/quickstart.md`
- [X] T002 Preserve the implemented atomic `RepositoryProfileDraft`, exact failure/cancellation recovery, historical nullable Story 4.1 language compatibility, and additive mappings in `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`; current behavior remains subject to fresh surrounding verification
- [X] T003 Preserve the implemented private `TOOL_RESULT_LIMITS={"execute_cypher_query":250001}`, reserved `truncated`/`result_limit` rejection, exact two-query lifecycle, and 250,000 combined-row cap in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs` and `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`; this does not preserve the stale final-image evidence schema claim
- [X] T004 Preserve the two additive migrations `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260720030657_AddLanguageFrameworkDetection.cs` and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260720060442_FrameworkEvidenceAuditCount.cs` plus their Designer files and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`; no third remediation migration is planned

**Independent validation**: Current source contains the accepted aggregate, provider-completeness, and two-migration baseline; T001 must re-establish its runnable state before correction work.

---

## Phase 2: User Story 1 - Trust the Language Profile (Priority: P1)

**Goal**: Preserve parser-derived Code Unit language and distinct-source-file summaries while correction work changes adjacent provider evidence.

- [X] T005 [US1] Preserve the exact seven-field Projection A language transport, open normalized parser-token grammar, per-path conflict rejection, and unchanged Projection B endpoint identity in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs` and `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`
- [X] T006 [US1] Preserve Code Unit language assignment, per-path consistency, and distinct retained-file language summaries in `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- [X] T007 [US1] Preserve the authorized ordinal language breakdown and Simplified-Chinese `语言分布` presentation in `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs`

**Independent validation**: The twelve-identifier matrix, extension/token mismatch, same-file multiple Code Units, novel valid token, and distinct-file counts pass without any extension or LLM language authority.

---

## Phase 3: User Story 2 - Use Auditable Framework Facts (Priority: P2)

**Goal**: Preserve deterministic framework facts, then correct lock ownership so a `Medium` fact requires exactly one validated direct-manifest owner.

### Preserved US2 baseline

- [X] T008 [US2] Preserve the closed embedded framework catalog and exact coordinate matching in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkCatalog.cs`, `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/Catalog/framework-catalog.v1.json`, and `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- [X] T009 [US2] Preserve committed-only, bounded, non-executing NuGet/npm/Maven/Python direct-evidence parsing in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- [X] T010 [US2] Preserve deepest applicable scope, parser-language compatibility, exact locator construction, `High` over `Medium`, deterministic tie-breaking, and deduplication in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- [X] T011 [US2] Preserve per-artifact isolation, repository-global fail-closed bounds, bounded audit sampling with complete total count, and no execution/network/LLM behavior in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- [X] T012 [US2] Preserve atomic `CodeUnitFramework` persistence and authorized Simplified-Chinese framework overview in `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`

### TDD Group R2A - Complete direct-manifest owner RED

- [X] T013 [US2] Add and observe runnable NuGet RED cases in `tests/Vulgata.Tests/FrameworkDetectionTests.cs` for a valid unique owner with and without a direct catalog match, zero owners, multiple owners, malformed XML, unsupported project shape, conditional/dynamic declarations or SDK ownership, unsafe/reparse/oversize owner, an invalid candidate beside a valid candidate, and an out-of-root/inapplicable owner; require no `Medium` fact and exactly one bounded closed lock/owner audit outcome for every invalid case
- [X] T014 [US2] Add and observe runnable npm RED cases in `tests/Vulgata.Tests/FrameworkDetectionTests.cs` for a valid unique literal top-level owner with and without a direct catalog match, zero owners, malformed JSON, unsupported/nonliteral shape, unsafe/reparse/oversize owner, multiple owner candidates where applicable, and an out-of-root/inapplicable owner; require no `Medium` fact and exactly one bounded closed lock/owner audit outcome for every invalid case

### TDD Group R2B - Validated owner GREEN

- [X] T015 [US2] Add the transient `DirectManifestOwnerCandidate` and `ValidatedDirectManifestOwner` contracts with closed read/parse/support/applicability states in `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`; rerun the contract-focused portions of T013-T014
- [X] T016 [US2] Replace tracked-file/count ownership with complete same-root NuGet owner-candidate evaluation before lock parsing in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`; require exactly one safely read, successfully parsed, supported, literal unconditional, applicable `.csproj`, do not discard invalid candidates to manufacture uniqueness, and rerun T013 GREEN
- [X] T017 [US2] Replace tracked-filename ownership with complete same-root npm owner evaluation before lock parsing in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`; require the exact `package.json` to be safely read, successfully parsed, supported, literal top-level, and applicable, then rerun T014 GREEN
- [X] T018 [US2] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~FrameworkDetection"` and the surrounding Repository profiling framework filters from `specs/002-language-framework-detection/quickstart.md`; record that direct `High` still beats eligible lock `Medium`, independent valid artifacts continue, diagnostics are bounded, and package/build/network/LLM execution remains zero

**Independent validation**: NuGet and npm valid unique owners retain eligible lock facts; malformed, unsupported, conditional/dynamic, unsafe, oversize, zero, multiple, and inapplicable owner sets create no lock-derived fact and one safe audit outcome.

---

## Phase 4: User Story 3 - Audit Unknown Languages (Priority: P3)

**Goal**: Preserve exact `Filtered/UnknownLanguage` auditing while hardening all provider-derived paths.

- [X] T019 [US3] Preserve zero-structure and absent-provider candidate reconciliation as exactly one terminal `Filtered/UnknownLanguage` with no retained Code Unit, Edge, or unresolved relationship in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- [X] T020 [US3] Preserve complete sorted Repository-relative unknown-language paths and distinct count under the accepted 10,000-path/16-MiB bound in `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs`

**Independent validation**: Unknown candidates remain visible to authorized audit and absent from every dispatchable Code Unit set.

---

## Phase 5: User Story 4 - Preserve Safe and Authorised Profiling (Priority: P4)

**Goal**: Close the captured-logger disclosure before any other provider/coordinator correction and preserve atomicity, visibility, and Chinese presentation.

### Preserved US4 baseline

- [X] T021 [US4] Preserve provider/global atomic failure, individual evidence isolation, late persistence failure/cancellation recovery, existing `ManagementAccess` and Repository visibility/redaction, and Simplified-Chinese user-visible prose in `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, and `tests/Vulgata.Tests/RepositoryApiTests.cs`

### TDD Group R4A - Captured-logger path disclosure RED

- [X] T022 [US4] Add a captured in-memory logger and observe runnable RED cases in `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs` and `tests/Vulgata.Tests/RepositoryProfilingTests.cs` using malformed Projection A and relationship rows with absolute Windows drive, UNC, POSIX-rooted, `..` escape, control-character, invalid-separator, and over-2,048-character paths; assert logs, exceptions, owned outcomes, durable state, API responses, and rendered output contain neither the raw scalar nor any host-path fragment and use only a safe Repository-relative value or `(unknown)` plus a closed reason

### TDD Group R4B - SafeProviderPath GREEN

- [X] T023 [US4] Introduce the bounded `SafeProviderPath` invariant and make malformed-row path recovery normalize or replace the raw scalar inside the parser stack frame before constructing `ProviderCandidateOutcome` in `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs` and `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`; discard the raw scalar and rerun provider portions of T022 GREEN
- [X] T024 [US4] Revalidate `SafeProviderPath` before the first log call and use the identical safe value for the log, audit key, durable `ScanCandidateOutcome`, DTO, and UI in `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`; rerun coordinator/persistence/API/UI portions of T022 GREEN
- [X] T025 [US4] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~CodeGraphContextProvider|FullyQualifiedName~RepositoryProfiling"` plus relevant API/component disclosure filters in `tests/Vulgata.Tests/RepositoryApiTests.cs` and `tests/Vulgata.Tests/DashboardPanelTests.cs`; record preserved two-query atomicity, local candidate audit, authorization, and zero raw-path disclosure

**Independent validation**: Captured logs and every downstream surface contain no absolute/rooted/escape path fragment; authorized users receive only bounded safe audit data, and unauthorized users receive no profiling detail.

---

## Phase 6: Closed Story 4.1 Evidence Grammar and Corrected Source Binding

**Purpose**: Align the owning contract, Python emitter, C# verifier, fresh image, and evidence to one exact schema and one clean source candidate.

### TDD Group C1 - Exact 19-root and nested grammar RED

- [x] T026 Add and observe runnable RED schema cases in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` proving the current runner/verifier accepts divergent root names or shapes; require exactly the 19 roots `schemaVersion`, `verdict`, `primaryFailureCategory`, `acceptedCorrectionSha256`, `claimSha256`, `sourceCommit`, `sourceTree`, `productionSourceManifestSha256`, `finalImageSha256`, `verifierBaseImageSha256`, `providerVersion`, `providerLockSha256`, `providerDistributionSha256`, `fixtureTreeSha256`, `projectionObservations`, `coverage`, `immutableSnapshot`, `cleanup`, and `forbiddenRetention`
- [x] T027 Extend the observed RED cases in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` to reject unknown, duplicate, missing, alternate-spelled, or wrong-typed root/nested fields; require counts only at `coverage.rowCounts`, five booleans only at `coverage.completeness`, projection completeness only at `projectionObservations.A.shapeComplete` and `.B.shapeComplete`, no `coverage.nodeKinds`, exact cleanup/forbidden-retention fields, and the exact raw safe-name allowlist including `parser_language`, `truncated`, and `result_limit`

### TDD Group C2 - One emitter/verifier grammar GREEN

- [x] T028 Rewrite the bounded observer/emitter in `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py` to emit only the Story 4.1 contract's exact closed 19-root schema, closed `projectionObservations`/`coverage`/`cleanup`/`forbiddenRetention` grammar, finite bounds, HMAC unknown-name handling, and 16-KiB evidence limit; preserve all-row validation while retaining only representative row shape
- [x] T029 Rewrite the closed parser/verifier in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` to validate the same exact grammar with no parallel field-name model, including duplicate-property detection, strict integer/boolean/type/range/sum/pass invariants, exact A/B alternatives, and canonical query/provider hashes; rerun T026-T027 GREEN
- [x] T030 Verify the canonical Projection A/B constants and raw property-name set remain identical across `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`, `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py`, `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`, and `specs/001-codegraph-extraction/contracts/repository-profiling-api.md`; run the non-Docker provider-contract schema tests and record GREEN

### TDD Group C3 - Source-to-image RED

- [x] T031 Add and observe runnable RED preflight cases in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` proving a dirty worktree, symlink, untracked/missing/case-colliding context entry, stale or unlabelled image, mismatched commit/tree/manifest label, reused correction namespace, caller-only image assertion, and source-manifest mismatch all fail before provider execution

### TDD Group C4 - Deterministic production-source binding GREEN

- [x] T032 Implement canonical `production-source-manifest-v1` creation and validation in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` over `docker/Dockerfile`, `.dockerignore`, and every tracked non-ignored Docker-context file, sorted by Repository-relative path with SHA-256; bind source commit, Git tree, manifest SHA-256, exact image ID, provider pin/lock/distribution, canonical query hashes, and verifier base image into the retained claim
- [x] T033 Add immutable build arguments/labels for source commit, source tree, and production-source-manifest SHA-256 in `docker/Dockerfile`, then make `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` inspect and compare all labels before constructing or running the verifier image
- [x] T034 Emit and validate the same source commit, source tree, production-source-manifest SHA-256, final image ID, and verifier base-image ID in `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py` and `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`; rerun T031 GREEN without provider execution

**Independent validation**: The runner and verifier accept only the exact 19-root contract and closed nested grammar, and no image can reach provider execution unless it is freshly source-bound to the clean immutable candidate.

---

## Phase 7: Truthful Historical Reconstruction

**Purpose**: Use the approved Story 4.2-only exception without converting missing historical process evidence into ordinary TDD satisfaction.

- [X] T035 Inspect immutable history and existing workflow records for every old TDD group using `git log --reverse --format="%H %P %s%n%b" e9824df7300634c19d815bdd7ad591b9fb5366d5..HEAD` and `git diff --name-status e9824df7300634c19d815bdd7ad591b9fb5366d5..HEAD`; create `specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json` with exact task-group identities, requirement/test mapping, earliest provable test and implementation commits or explicit null/reason, existing workflow/checkpoint identities only, and truthful `historicalRawRedAvailable`/`checkpointTrailerAvailable` booleans
- **Independent validation**: T035 creates only truthful historical discovery/scaffolding. It does not finalize current GREEN evidence; finalization and validation wait for T045-T046 after the immutable-candidate GREEN gates through T044.

---

## Phase 8: Immutable Candidate, Final Image, and Regression Gates

**Purpose**: Produce new source-bound evidence and rerun every accepted current-candidate gate after all corrections.

- [x] T036 Run all focused RED/GREEN correction filters from T013-T018, T022-T025, T026-T030, and T031-T034 against one clean candidate, persist raw outputs, and verify no production correction predates its observed RED using `tests/Vulgata.Tests/FrameworkDetectionTests.cs`, `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`, `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, and `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`
- [x] T037 Verify the accepted plan/research/data-model/quickstart/contracts hashes and the exact two-migration forward/rollback instructions from `specs/002-language-framework-detection/quickstart.md`; run `dotnet ef migrations script --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web --context VulgataDbContext` and `dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web --context VulgataDbContext`
- [x] T038 Create a new unused accepted-correction namespace and deterministic production-source manifest at the clean immutable candidate through `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`; stop on dirty status, identity drift, stale namespace, manifest mismatch, or any source path not represented according to `.dockerignore`
- [x] T039 Build a fresh final runtime image from `docker/Dockerfile` and the exact candidate context with the required source commit/tree/manifest labels, inspect its immutable image ID and labels, and reject any pre-correction, cached-without-proof, unlabelled, or mismatched image before provider execution
- [x] T040 Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName=Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract"` once against the fresh image and unused namespace; require exact 0.5.1 pin/lock/distribution, exact query hashes, both projections above 100 complete rows, empty-file `Filtered/UnknownLanguage`, atomic injected failures, cleanup, false forbidden-retention fields, and closed v2 evidence bound to the same source/image identities
- [x] T041 Rerun `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj` and `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj` at the exact same immutable candidate and record complete non-skipped results; no earlier full-suite run or pre-correction image is candidate evidence
- [x] T042 Rehearse both additive migrations and rollback at the same candidate using `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260720030657_AddLanguageFrameworkDetection.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260720060442_FrameworkEvidenceAuditCount.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`; prove historical Story 4.1 readability, fresh Story 4.2 invariants, reverse audit-count check/column first, then tables/indexes, and no historical rewrite
- [x] T043 Run `git diff --check` and `git status --short`, verify the final diff contains no forbidden FR-020 scope and no files outside the accepted correction/evidence boundary, and treat CRLF-only warnings as non-functional only when no actual whitespace defect exists
- [x] T044 Recheck authorized/unauthorized Repository overview behavior and captured-log redaction after the final image using `tests/Vulgata.Tests/RepositoryApiTests.cs`, `tests/Vulgata.Tests/DashboardPanelTests.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`; require all language/framework/unknown audit facts for authorized users, established 401/403/404 redaction for others, and zero raw host paths

### Post-GREEN historical ledger finalization

- [ ] T045 Record current immutable-candidate GREEN commands through T044, exit codes, source commit, and raw-output paths separately from historical fields in `specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json`; explicitly mark unavailable historical raw RED output and missing contemporaneous trailers unavailable, never infer/backdate/amend evidence, and fail validation if any current run is represented as historical RED
- [ ] T046 Validate the finalized reconstruction against current Git ancestry and every referenced path/hash using `specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json`, `specs/002-language-framework-detection/workflow-state.json`, and the immutable Git object database; leave T050 incomplete if the ledger is false, internally inconsistent, cites nonexistent evidence, or omits any required GREEN gate through T044
- [ ] T047 Validate claim/evidence/reconstruction JSON parseability, exact 19-root/nested schema, all referenced path/hash/image/commit/tree identities, new immutable namespace, evidence size, raw-output existence, and final-image cleanup using `TestResults/codegraph-provider-contract/<AcceptedCorrectionSha256>/claim.json`, `TestResults/codegraph-provider-contract/<AcceptedCorrectionSha256>/evidence.json`, and `specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json`
- [ ] T048 Reconcile every accepted plan obligation and each raw finding `AR2-001`, `AR2-002`, `AR2-003`, `QR2-001`, `QR2-002`, `CF-002`, `EV-002`, `WF-002`, `PL-001`, `TR-001`, and `PA-001` to concrete immutable-candidate evidence in `specs/002-language-framework-detection/reviews/acceptance-review-2.json`, `specs/002-language-framework-detection/reviews/quality-review-2.json`, `specs/002-language-framework-detection/reviews/analyze-final-2.json`, and the new evidence artifacts; unresolved or historical-unavailable items remain explicit and never become fabricated PASS claims
- [ ] T049 Freeze the final immutable-candidate identity after T036-T048, rerun hash/status checks without changing source, tests, contracts, plan artifacts, evidence, or image, and prepare the exact candidate/evidence inputs for the final audit; any mutation invalidates T038-T049 and requires a fresh namespace/image/gate
- [ ] T050 Audit the immutable candidate last against FR-001-FR-021, AC-1-AC-7, SC-001-SC-008, `specs/002-language-framework-detection/plan.md`, `specs/002-language-framework-detection/data-model.md`, `specs/002-language-framework-detection/quickstart.md`, every file under `specs/002-language-framework-detection/contracts/`, `specs/001-codegraph-extraction/contracts/repository-profiling-api.md`, all iteration-2 findings, the fresh RED/GREEN records, final source/image/evidence binding, two-migration rehearsal, forbidden-scope diff, and truthful historical reconstruction; mark complete only after the audit succeeds with no file mutation, otherwise leave `[ ]`

**Independent validation**: One clean immutable candidate owns every focused GREEN, full suite, Web build, pending-model result, source-bound fresh image, exact closed-schema evidence, migration rehearsal, redaction/authorization check, and truthful historical ledger. T050 is last and remains incomplete until that whole candidate passes.

---

## Dependencies and Execution Order

```text
T001
  -> preserved baseline T002-T012 and T019-T021
  -> US2 correction: T013 RED -> T014 RED -> T015 -> T016 -> T017 -> T018
  -> US4 correction: T022 RED -> T023 -> T024 -> T025
  -> evidence grammar: T026 RED -> T027 RED -> T028 -> T029 -> T030
  -> source binding: T031 RED -> T032 -> T033 -> T034
  -> historical discovery/scaffolding: T035
  -> immutable candidate GREEN gates: T036 -> T037 -> T038 -> T039 -> T040
  -> current GREEN gates: T041 -> T042 -> T043 -> T044
  -> historical ledger finalization: T045 -> T046
  -> evidence/reconciliation/freeze: T047 -> T048 -> T049
  -> T050 final audit
```

- T013-T018 and T022-T025 may be assigned to separate fresh TDD implementers sequentially because they own different production seams, but they both edit shared test files and therefore must not run concurrently.
- T026-T030 must finish before T031-T034 because the source-bound image may contain only the final closed evidence implementation.
- T035 may inspect history and create truthful scaffolding while correction groups run, but T045-T046 cannot finalize or validate the ledger until T036-T044 have supplied every required immutable-candidate GREEN result.
- T038-T049 are one serialized immutable-candidate lane. Any source, test, contract, evidence, or Docker-context mutation restarts that lane with a new correction namespace.
- No task that edits `CodeGraphContextProvider.cs`, `RepositoryProfilingCoordinator.cs`, `FrameworkEvidenceDetector.cs`, `runner.py`, `CodeGraphProviderContractTests.cs`, `docker/Dockerfile`, migrations, or final evidence is parallelized with another owner of the same file.

## Parallel Opportunities

- After T001, read-only historical discovery for T035 may run independently of the new test authoring, but T045-T046 wait for all current GREEN evidence through T044.
- Test execution may shard by fully qualified class only when no task is editing that class and all prerequisite GREEN changes are present.
- There are no `[P]` implementation tasks: the correction seams share test authorities or immutable final-candidate state, so parallel markers would be misleading.

## Old-to-New Task Mapping

| Previous tasks | Regenerated authority |
| --- | --- |
| T001 | T001 activation with accepted checkpoint `216985b...` and current accepted hashes |
| T002-T008 | T002-T004 preserved foundation; exact evidence-schema/final proof replaced by T026-T034 and T037-T047 |
| T009-T015 | T005-T007 preserved US1 plus final regression T036/T041/T044 |
| T016-T029 | T008-T012 preserved US2 baseline; stale lock-owner completion invalidated and replaced by unchecked T013-T018 |
| T030-T034 | T019-T020 preserved US3 plus final regression T036/T044 |
| T035-T044 | T021 preserved safe/authorized baseline; logger-disclosure claim invalidated and replaced by unchecked T022-T025 |
| T045-T047 | Stale completion-lane evidence invalidated; replaced by unchecked T026-T034 and fresh immutable-image T038-T040 |
| T048 | Replaced by current two-migration/hash rehearsal T037/T042 |
| T049 | Pre-correction verification is not final evidence; replaced by T036-T049 |
| T050 | Remains unchecked, is last, and now consumes the truthful reconstruction plus immutable-candidate audit |

## Acceptance and Finding Coverage

| Obligation/finding | Executable coverage |
| --- | --- |
| FR-001-FR-003, FR-005-FR-006; AC-1-AC-2; SC-001-SC-002 | T005-T007, T026-T030, T036, T040-T041, T044, T050 |
| FR-004, FR-007; AC-6; SC-004 | T019-T020, T036, T040-T041, T044, T050 |
| FR-008-FR-014, FR-016, FR-021; AC-3-AC-5; SC-003/SC-005 | T008-T018, T036, T041, T044, T050 |
| FR-015, FR-017; AC-7; SC-006 | T002-T003, T011, T021-T034, T036, T040-T044, T047, T050 |
| FR-018-FR-019; AC-7; SC-007-SC-008 | T007, T012, T020-T025, T041, T044, T050 |
| FR-020 forbidden scope | T043, T048-T050 |
| AR2-001 | T022 RED, T023-T025 GREEN, T044/T050 final proof |
| AR2-002 and CF-002 | T026-T030 exact contract RED/GREEN, T040/T047/T050 fresh proof |
| QR2-001 | T013-T014 RED, T015-T018 GREEN, T036/T050 final proof |
| AR2-003, QR2-002, WF-002 | T049 prerequisite freeze and T050 last/unchecked |
| EV-002 | T031-T034 source-binding RED/GREEN and T038-T043/T047 one-candidate evidence |
| PL-001 and PA-001 | Accepted checkpoint/hashes in T001, current plan-set/migration alignment in T037/T042, T050 |
| TR-001 | T035 and T045-T046 truthful reconstruction, fresh correction RED/GREEN T036, T048/T050 |

## Task-Group Partition Recommendation

Use exactly these seven bounded dispatch slots:

1. **R2 owner eligibility**: T013-T018, owned by one fresh TDD implementer.
2. **R4 path redaction**: T022-T025, owned by one fresh TDD implementer.
3. **C1/C2 evidence grammar**: T026-T030, owned by one fresh TDD implementer.
4. **C3/C4 source binding**: T031-T034, owned by one fresh TDD implementer after C1/C2.
5. **Historical reconstruction**: T035 discovery/scaffolding, then T045-T046 finalization/validation after current GREEN gates, all within the single bounded ledger.
6. **Immutable candidate verification**: T036-T044 and T047-T049, serialized under the orchestrator; no implementation edits after T038.
7. **Final immutable-candidate audit**: T050 alone, dispatched only after slot 6 succeeds and leaves the candidate unchanged.

## Completion Criteria

- All 50 tasks use checkbox, exact ID, story labels where applicable, and concrete repository paths or commands.
- Every new behavior correction has an observed runnable RED before production changes.
- All eleven raw iteration-2 findings and every accepted plan obligation map to executable tasks.
- Historical absence is disclosed, never fabricated or treated as ordinary TDD satisfaction.
- The exact Story 4.1 evidence grammar has 19 roots and one closed nested authority.
- NuGet/npm owner matrices include valid unique, zero, multiple, malformed, unsupported, conditional/dynamic where applicable, unsafe, oversize, and inapplicable cases.
- One fresh final image is bound to the exact clean candidate commit, tree, production-source manifest, image ID, claim, and evidence.
- T050 is unchecked, last, and cannot pass before T001-T049 and the immutable-candidate audit.
