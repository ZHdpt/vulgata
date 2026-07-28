---

description: "Dependency-ordered remediation tasks for Story 4.2 language and framework detection"
---

# Tasks: Language and Framework Detection

**Stage**: `tasks`

**BMad Story**: `4-2-language-and-framework-detection` (Full execution; lifecycle remains `review`; this artifact does not advance or repair lifecycle state)

**Source boundary**: pinned checkpoint `a30f95ef792903ccb43cff5c5e1f42a100e155c7`, tree `1776c6940b5ac7eed4728c8d0a4f1911cca39c99`; this task-author boundary changes only this file and is not an implementation candidate

**Accepted complete-plan verdict**: `specs/002-language-framework-detection/reviews/plan-review-10.json`, SHA-256 `dc0638af64c19545adc96a3e8462abc4b9302ac77bc13c3b267726d565ce32be`, iteration `10`, verdict `ACCEPT`, reviewed five-path binary diff SHA-256 `3d273cf81ad4321abd983c6cc72c4b7f5a2612c06ca3363fb9007978cd8834ba`

**Exact accepted five-file plan set**: `specs/002-language-framework-detection/plan.md` `3a455c1b61b0b5d3179a52ca1d28ac11e973bdcbfc787285ad528b84e1193f7e`; `specs/002-language-framework-detection/research.md` `8b0de0d30fb6ad98078e8e0af6405165d6d87f67239a5af1365538e91d22ae36`; `specs/002-language-framework-detection/data-model.md` `504a63a0d5872db386170e5353dd550c55e1fa58a5c8732820f79a760bb2509a`; `specs/002-language-framework-detection/contracts/framework-detection.md` `27a53d1413433f69213c7a141d115f32e32020c0b5d8d111643837e8fdc00d30`; `specs/002-language-framework-detection/quickstart.md` `5e876703be51cbba7e9efa06ca5e6e2d73f3a5323adb9727f0526403f76dc54e`

**QR9 planned-change authorization**: `specs/002-language-framework-detection/evidence/quality9-scan-strategy-remediation-approval.json`, SHA-256 `09a9f89d9f7c25ca523198949fd88ad82cb1e440623d25a0a3bce8418b51d459`, decision `approved`; it authorizes only QR9-001's three literal project-level NuGet owner forms and rejection of nested/execution-dependent owners after plan-review 10. It does not authorize a Worker Agent/tool, orchestration, dispatch, scheduling, grouping, retry, PRD, persistence, API, UI, migration, or later-gate waiver.

## Evidence and Ordering Rules

- The exact historical statement is **“Pre-correction T001 baseline observation: unavailable”**. The superseded T001 observation was not captured before T002-T049, is temporally unavailable, and cannot be recreated by running commands now.
- A result produced now is **Current verification** only. It must retain its actual UTC time, immutable commit/tree, command, exit code, and raw-output path and must not be called historical, pre-correction, RED, or proof of contemporaneous ordering.
- Checked tasks preserve still-valid implementation and immutable evidence. They do not manufacture unavailable historical raw RED output or missing checkpoint trailers.
- Candidate `708b7dfca397b8d155111d512c7950bc68651ea8`, `acceptance-review-9.json`, `quality-review-9.json`, and all candidate-bound downstream claims are invalidated history. The formerly checked historical T041 pin does not satisfy the replacement T041 marker below.
- Every new changed behavior uses observed runnable RED before implementation. Compile failure, broken fixture, inferred failure, fabricated output, backdating, amended history, or implementation-before-RED is a blocker.
- T001 and T067 must both complete before T068-T070. T001 binds a clean immutable post-task-checkpoint **Current verification** record to the accepted plan-review-10 checkpoint and QR9 authorization; T067 separately verifies the executable authority/invalidation boundary before implementation. T068 and T069 must each independently retain behavior-level RED, and T070 must retain the separate stale-binding RED, before T071 or T072 may mutate production/binding behavior.
- Each RED/GREEN record includes the exact command, exit code, immutable source, observed assertion, and create-new raw-output path. Current GREEN is never relabelled as historical RED.
- Any tracked candidate-boundary mutation after the replacement T041 pin invalidates T041-T050 and requires correction, pre-pin evidence regeneration/validation, one new pin, and rerun of every dependent gate.
- No task adds or changes a Scan Coordinator, Orchestrator, Worker Agent/tool, dispatch, scheduling, grouping, retry, LLM fact authority, Communication Pattern Catalog, Story 4.3 behavior, document generation, Cross-Repository Resolution, or downstream language-support promise.

## Phase 1: Current Verification and Preserved Foundation

**Purpose**: Replace the impossible old T001 prerequisite with truthful present-day verification and retain only still-valid foundation behavior.

- [x] T001 Create `specs/002-language-framework-detection/evidence/story-4-2-current-verification.json` against a root-supplied clean immutable post-task-checkpoint commit containing this regenerated `tasks.md`, its accepted fresh Full analysis, and the exact accepted five-file plan set: preserve verbatim `Pre-correction T001 baseline observation: unavailable`; record only `Current verification` with the actual UTC observation time, immutable commit/tree, commands, exit codes, and create-new raw outputs `TestResults/story-4-2-current-verification-quality9-prerequisite/T001-provider-profiling-green.txt` and `TestResults/story-4-2-current-verification-quality9-prerequisite/T001-framework-green.txt`; prove that `Workflow-Checkpoint` `bmad-4-2-plan-20260727T134009Z-b02d3f` resolves uniquely through the required four trailers to accepted-plan commit `3de522a42fc1cc77ae200da7596c8c3c5a1af421` and is its ancestor; prove exact `specs/002-language-framework-detection/reviews/plan-review-10.json` SHA-256 `dc0638af64c19545adc96a3e8462abc4b9302ac77bc13c3b267726d565ce32be`, iteration `10`, verdict `ACCEPT`, reviewed diff `3d273cf81ad4321abd983c6cc72c4b7f5a2612c06ca3363fb9007978cd8834ba`, and the complete five-file artifact-hash map in this header; prove QR9 authorization path `specs/002-language-framework-detection/evidence/quality9-scan-strategy-remediation-approval.json`, SHA-256 `09a9f89d9f7c25ca523198949fd88ad82cb1e440623d25a0a3bce8418b51d459`, decision `approved`, scope limited to QR9-001, and committed authorization boundary `a30f95ef792903ccb43cff5c5e1f42a100e155c7`; then run the two focused provider/profiling and framework commands from `specs/002-language-framework-detection/quickstart.md` GREEN without calling any current result historical, pre-correction, RED, or proof of contemporaneous ordering
- [x] T002 Preserve the implemented atomic `RepositoryProfileDraft`, rollback/detach/reload failure and cancellation recovery, historical nullable Story 4.1 language compatibility, and additive mappings in `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- [x] T003 Preserve the private `TOOL_RESULT_LIMITS={"execute_cypher_query":250001}`, reserved `truncated`/`result_limit` rejection, exact two-query lifecycle, and 250,000 combined-row cap in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs` and `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`
- [x] T004 Preserve exactly the two additive migrations `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260720030657_AddLanguageFrameworkDetection.cs` and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260720060442_FrameworkEvidenceAuditCount.cs`, their Designer files, and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`; no third remediation migration is permitted

**Independent validation**: T001 proves only the accepted/current immutable boundary and explicitly preserves the missing temporal fact. The focused current commands pass, the exact accepted plan binding is executable, and the foundation remains unchanged.

---

## Phase 2: User Story 1 - Trust the Language Profile (Priority: P1)

**Goal**: Preserve parser-derived Code Unit language and distinct-source-file summaries.

- [x] T005 [US1] Preserve the exact seven-field Projection A language transport, bounded open parser-token grammar, per-path conflict rejection, and unchanged Projection B endpoint identity in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs` and `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`
- [x] T006 [US1] Preserve Code Unit language assignment, per-path consistency, and distinct retained-file language summaries in `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- [x] T007 [US1] Preserve the authorized ordinal language breakdown and Simplified-Chinese `语言分布` presentation in `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs`

**Independent validation**: The twelve-identifier matrix, extension/token mismatch, novel valid token, same-file multiple Code Units, and distinct-file counts pass with no extension or LLM language authority.

---

## Phase 3: User Story 2 - Use Auditable Framework Facts (Priority: P2)

**Goal**: Preserve deterministic, scoped framework facts and the validated direct-manifest owner correction.

- [x] T008 [US2] Preserve the closed embedded framework catalog and exact coordinate matching in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkCatalog.cs`, `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/Catalog/framework-catalog.v1.json`, and `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- [x] T009 [US2] Preserve committed-only, bounded, non-executing NuGet/npm/Maven/Python direct-evidence parsing in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- [x] T010 [US2] Preserve deepest applicable scope, parser-language compatibility, exact locator construction, `High` over `Medium`, deterministic tie-breaking, and deduplication in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- [x] T011 [US2] Preserve per-artifact isolation, Repository-global fail-closed bounds, bounded audit sampling with complete total count, and zero execution/network/LLM behavior in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` and `tests/Vulgata.Tests/FrameworkDetectionTests.cs`
- [x] T012 [US2] Preserve atomic `CodeUnitFramework` persistence and the authorized Simplified-Chinese framework overview in `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- [x] T013 [US2] Preserve the observed NuGet RED matrix in `tests/Vulgata.Tests/FrameworkDetectionTests.cs` for valid unique, zero, multiple, malformed, unsupported, conditional/dynamic, unsafe/reparse/oversize, mixed valid-invalid, and out-of-root/inapplicable direct-manifest owners
- [x] T014 [US2] Preserve the observed npm RED matrix in `tests/Vulgata.Tests/FrameworkDetectionTests.cs` for valid unique, zero, malformed, unsupported/nonliteral, unsafe/reparse/oversize, multiple where applicable, and out-of-root/inapplicable owners
- [x] T015 [US2] Preserve `DirectManifestOwnerCandidate` and `ValidatedDirectManifestOwner` with closed read/parse/support/applicability states in `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`
- [x] T016 [US2] Preserve complete same-root NuGet owner-candidate evaluation before lock parsing in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`; exactly one safely read, successfully parsed, supported, literal unconditional, applicable `.csproj` is required and invalid candidates cannot be discarded to manufacture uniqueness
- [x] T017 [US2] Preserve complete same-root npm owner evaluation before lock parsing in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`; the exact `package.json` must be safely read, successfully parsed, supported, literal top-level, and applicable
- [x] T018 [US2] Preserve immutable GREEN evidence for the focused `FrameworkDetection` and surrounding Repository profiling filters; direct `High` still beats eligible lock `Medium`, independent valid artifacts continue, diagnostics remain bounded, and package/build/network/LLM execution remains zero

**Independent validation**: Valid unique owners retain eligible `Medium` lock facts; every invalid owner set creates no lock-derived fact and exactly one bounded closed audit outcome.

---

## Phase 4: User Story 3 - Audit Unknown Languages (Priority: P3)

**Goal**: Preserve exact `Filtered/UnknownLanguage` audit behavior and dispatch exclusion.

- [x] T019 [US3] Preserve zero-structure and absent-provider candidate reconciliation as exactly one terminal `Filtered/UnknownLanguage` with no retained Code Unit, Edge, or unresolved relationship in `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- [x] T020 [US3] Preserve complete sorted Repository-relative unknown-language paths and distinct count under the 10,000-path/16-MiB bound in `src/dotnet/Vulgata.Shared/Repositories/ScanRunSummaryDto.cs`, `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/DashboardPanelTests.cs`

**Independent validation**: Unknown candidates remain visible to authorized audit and absent from every dispatchable Code Unit set.

---

## Phase 5: User Story 4 - Preserve Safe and Authorised Profiling (Priority: P4)

**Goal**: Preserve fail-closed provider paths, atomicity, Repository visibility, and Simplified-Chinese presentation.

- [x] T021 [US4] Preserve provider/global atomic failure, individual evidence isolation, late persistence failure/cancellation recovery, existing `ManagementAccess` and Repository visibility/redaction, and Simplified-Chinese user-visible prose in `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, and `tests/Vulgata.Tests/RepositoryApiTests.cs`
- [x] T022 [US4] Preserve observed captured-logger RED cases in `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs` and `tests/Vulgata.Tests/RepositoryProfilingTests.cs` for absolute Windows, UNC, POSIX-rooted, escape, control-character, invalid-separator, and oversize provider paths
- [x] T023 [US4] Preserve the bounded `SafeProviderPath` transition before `ProviderCandidateOutcome` construction in `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs` and `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`
- [x] T024 [US4] Preserve defense-in-depth revalidation before the first log and the identical safe path for log, audit key, durable outcome, DTO, and UI in `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`
- [x] T025 [US4] Preserve immutable GREEN evidence for provider/profiling and relevant API/component disclosure tests proving two-query atomicity, local candidate audit, authorization, and zero raw-host-path disclosure

**Independent validation**: Captured logs and every downstream surface contain only a safe Repository-relative path or `(unknown)`; unauthorized users receive no profiling detail.

---

## Phase 6: Preserved Closed Evidence Grammar and Source Binding

**Purpose**: Preserve the accepted Story 4.1 v2 evidence grammar and source-to-image preflight implementation while invalidating its stale plan-review claim.

- [x] T026 Preserve observed exact-19-root RED cases in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`
- [x] T027 Preserve observed nested-schema RED cases in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` for unknown, duplicate, missing, alternate, and wrong-typed fields plus duplicate semantic authorities
- [x] T028 Preserve the exact bounded v2 observer/emitter in `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py`
- [x] T029 Preserve the exact closed v2 C# parser/verifier in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`
- [x] T030 Preserve canonical Projection A/B and safe-name equality across `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`, `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py`, `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`, and `specs/001-codegraph-extraction/contracts/repository-profiling-api.md`
- [x] T031 Preserve observed source-binding preflight RED cases in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` for dirty, symlink, untracked/missing/case-colliding, stale/unlabelled/mismatched image, reused namespace, and manifest mismatch inputs
- [x] T032 Preserve canonical `production-source-manifest-v1` creation and validation in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`
- [x] T033 Preserve immutable source commit/tree/manifest image labels in `docker/Dockerfile` and their pre-provider inspection in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`
- [x] T034 Preserve equal source/image/verifier identities in `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py` and `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`

**Independent validation**: The closed grammar and source preflight remain runnable, but no prior claim, candidate, manifest, image, namespace, review, E2E, verification, T049, or T050 result is accepted after the corrected plan bytes.

---

## Phase 7: Executable Accepted-Plan Binding TDD

**Purpose**: Observe both stale executable bindings before replacing them with the exact fresh verdict/checkpoint/diff contract.

### Binding RED

- [x] T035 Run only `HashBoundFile_WindowsCheckout_PreservesCanonicalLfBytes` before changing `CanonicalLfFiles` in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`; retain actual assertion failures for exactly `plan.md`, `research.md`, and `quickstart.md` in create-new `TestResults/story-4-2-current-binding/T035-canonical-hash-red.txt`, with command, nonzero exit code, immutable source, and no compile failure
- [x] T036 Edit only the focused assertion in `AcceptedCorrectionManifest_IsCanonicalAndArtifactBound` in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` to reject `user-approved-story-4-2-plan-2026-07-20` and require the exact fresh verdict path/hash/diff plus checkpoint ID/commit fields; run that test against the otherwise unchanged `_reviewIdentity`/`CreateAcceptedCorrectionManifest`, and retain the actual stale-claim assertion failure in create-new `TestResults/story-4-2-current-binding/T036-plan-claim-red.txt`

### Binding GREEN

- [x] T037 Observe focused RED for the stale accepted-plan checkpoint binding in create-new `TestResults/story-4-2-review3-correction/T037-plan-binding-red-attempt-0002.txt`, then bind the executable resolver to forward-only checkpoint `36bd6cccdba1fa341eddc8096a5c2269c62c3118` / `bmad-4-2-plan-binding-20260726T073452Z-d04e72`; require the exact accepted plan/verdict hashes in tree `54705660bde4e74decf8baaab32ca6046f420152`, unique checkpoint resolution, ancestry, and all four exact `BMad-Story` / `Workflow-Scope` / `Workflow-Stage` / `Workflow-Checkpoint` trailers without requiring the empty replacement checkpoint to introduce those files in its own diff; add missing/wrong negative cases for every mandatory trailer. Separately observe focused UI RED in create-new `TestResults/story-4-2-review3-correction/T037-confidence-ui-red-attempt-0002.txt`, preserve canonical API/DTO confidence values `High` / `Medium`, and localize only `RepositoryDetailDialog.razor` presentation to `高` / `中` while rejecting rendered English confidence values
- [x] T038 Rerun the focused binding tests, focused dashboard confidence tests, and surrounding non-Docker `CodeGraphProviderContractTests` plus `DashboardPanelTests`; retain commands, UTC observation times, exit codes, totals, and immutable create-new raw outputs `TestResults/story-4-2-review3-correction/T038-binding-green-attempt-0002.txt`, `TestResults/story-4-2-review3-correction/T038-dashboard-green-attempt-0002.txt`, and `TestResults/story-4-2-review3-correction/T038-surrounding-green-attempt-0002.txt`; verify the correction changes no API wire value, `FrameworkConfidence` enum, coordinator serialization, Scan strategy, Worker Agent/tool behavior, provider parsing, persistence, migration, Docker, or PRD behavior

**Independent validation**: Both stale bindings fail before T037. Focused and surrounding GREEN then bind the claim to exactly `plan-review-8.json`, its SHA-256/reviewed diff/artifact map, and the unique ancestor checkpoint commit; the static 2026-07-20 identity is absent.

---

## Phase 8: Truthful Ledger Regeneration

**Purpose**: Replace the invalidated ledger without merging unavailable historical time with current verification.

- [x] T039 Regenerate and finalize `specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json` from immutable Git and existing workflow records: preserve every still-provable historical group, earliest test/implementation commit or explicit null/reason, truthful `historicalRawRedAvailable`/`checkpointTrailerAvailable`, the exact unavailable T001 statement, and a link to `specs/002-language-framework-detection/evidence/story-4-2-current-verification.json`; add T035-T038, T055-T057, and T058-T062 as separately timed current RED/GREEN/review-remediation evidence and remove every stale accepted claim for the prior candidate, reviews, manifest, image, namespace, T049, or T050
- [x] T040 Validate the regenerated ledger against Git ancestry, referenced blobs/trailers, T001, T035-T038, T055-T057, and T058-T062 raw-output existence/hashes, and the Story 4.2-only exception; reject fabricated/backdated/amended evidence, a current run labelled historical/pre-correction, missing temporal disclosure, or any claim that the replacement final image/T049/T050 already passed

**Independent validation**: Audit 4 independently ACCEPTED the corrected QR8 ledger boundary: all authority, immutable-object, raw-evidence, checkpoint, chronology, Current-verification, invalidation, and downstream-state checks passed. T041 is permitted after T066 finalization in the same root checkpoint.

---

## Phase 8A: Quality Review 4 Corrections

**Purpose**: Correct QR4-001 through QR4-003 with current RED/GREEN evidence before pinning a replacement candidate.

- [x] T051 Observe focused RED for boundary-whitespace Windows-drive, UNC, and POSIX-rooted provider path scalars in `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs` and `tests/Vulgata.Tests/RepositoryProfilingTests.cs`; retain native output at create-new `TestResults/story-4-2-quality4-correction/T051-provider-path-red.txt`, then make `SafeProviderPath` reject the raw boundary-whitespace scalar before any owned outcome, log, durable result, DTO/API value, or UI-derived value can retain it and capture focused GREEN at `T051-provider-path-green.txt`
- [x] T052 Observe focused RED for invalid Python trailing syntax such as `django???` in `tests/Vulgata.Tests/FrameworkDetectionTests.cs`; retain native output at create-new `TestResults/story-4-2-quality4-correction/T052-python-literal-red.txt`, then require one closed full-line literal requirement before catalog matching, preserve independently valid evidence plus one bounded closed audit outcome, and capture focused GREEN at `T052-python-literal-green.txt`
- [x] T053 Observe focused RED for one catalog-matched plus one valid-unmatched same-root direct NuGet manifest in `tests/Vulgata.Tests/FrameworkDetectionTests.cs`; retain native output at create-new `TestResults/story-4-2-quality4-correction/T053-scope-ambiguity-red.txt`, then retain successfully parsed supported direct-manifest scope identities independently from matched evidence, suppress only the ambiguous same-ecosystem winning root with one bounded audit outcome, preserve colocated independent ecosystems and deeper unambiguous scopes, and capture focused GREEN at `T053-scope-ambiguity-green.txt`
- [x] T054 Run the complete surrounding `CodeGraphContextProviderTests`, `RepositoryProfilingTests`, and `FrameworkDetectionTests` suites after T051-T053; preserve the first expectation-alignment failure at create-new `TestResults/story-4-2-quality4-correction/T054-surrounding-green.txt`, retain the current final-byte GREEN at `T054-surrounding-green-attempt-0003.txt`, verify exact source/test/task scope and `git diff --check`, and leave T041-T050 incomplete until the root pins and reviews a replacement immutable candidate

**Independent validation**: Each finding has an observed behavior-level RED before its production correction, focused and surrounding GREEN use the current bytes, provider paths remain fail-closed and confidential, Python parsing remains literal and non-executing, and scope ambiguity stays ecosystem-local.

---

## Phase 8B: T046 Review-Evidence and Quality Review 6 Correction

**Purpose**: Preserve the user-approved review-evidence remediation, correct QR6-001 with observed RED/GREEN evidence, and invalidate every downstream gate bound to the superseded candidate.

- [x] T055 Preserve `specs/002-language-framework-detection/evidence/t046-review-evidence-remediation-approval.json` and the create-new iteration-6 Full verdicts without rewriting iteration 5: `acceptance-review-6.json` is contract-valid `ACCEPT`, while `quality-review-6.json` is contract-valid `REVISE` with QR6-001 proving that tracked-path inventory followed by mutable working-tree reads violates committed Repository evidence
- [x] T056 Observe behavior-level RED at `TestResults/story-4-2-quality6-correction/red-dirty-direct.txt` for a dirty tracked direct manifest producing an uncommitted fact; pin one validated `HEAD^{commit}` identity per detector pass, preserve `git ls-files --cached` inventory, read exact `<commit>:<path>` blobs through non-executing `git cat-file`, fail closed on missing/drifting committed objects, and prove dirty direct-manifest, dirty paired-lock, and HEAD-change-after-inventory cases GREEN
- [x] T057 Align only the Git fixture in `tests/Vulgata.Tests/RepositoryProfilingTests.cs` to create committed fixture state after the first 7/168 harness-only failure; preserve that failed output, pass the provider/profiling/framework boundary 168/168 in both worker and root runs, pass the complete non-final-image suite 507/507, build `Vulgata.Web` with zero errors, and verify the correction adds no Scan strategy, Worker Agent/tool, dispatch, scheduling, retry, PRD, migration, persistence, API, or UI behavior

**Independent validation**: QR6-001 has one observed behavior RED before production correction, exact committed bytes remain stable across dirty or concurrently advanced working trees, all current surrounding tests pass, and the prior T041-T045/T046 candidate lane is explicitly invalidated rather than relabelled.

---

## Phase 8C: Quality Review 7 Corrections

**Purpose**: Apply the explicitly approved QR7 Scan-strategy remediation with one observed RED before each production correction, artifact-local fail-closed behavior, and fresh surrounding evidence.

- [x] T058 Preserve `specs/002-language-framework-detection/evidence/quality7-scan-strategy-remediation-approval.json`, the unchanged iteration-7 Full verdicts, and explicit invalidation of candidate `614962322e9f3578980a6fa28bc8897d22b66c58` plus both review axes before source or test mutation
- [x] T059 Observe RED for a committed regular direct manifest and paired lock losing deterministic facts after dirty deletion or reparse substitution; derive eligibility from the pinned Git tree/object mode rather than mutable checkout metadata, then prove all cases GREEN
- [x] T060 Observe RED for duplicate relevant JSON properties producing or owning npm evidence; reject the ambiguous artifact locally with one bounded audit, no framework fact, continued independent evidence, then prove direct-manifest and paired-lock cases GREEN
- [x] T061 Observe RED for duplicate Maven `groupId` or `artifactId` children escalating to repository-wide `DetectorFailed`; make the ambiguity artifact-local, preserve one bounded audit, continue an independent valid manifest, then prove all cases GREEN
- [x] T062 Run the complete `FrameworkDetectionTests` suite, provider/profiling/framework surrounding suite, complete non-final-image test suite, and `Vulgata.Web` build; preserve raw RED/GREEN outputs, verify exact correction scope, refresh Current verification and the truthful ledger, and leave T041-T050 incomplete until a replacement candidate is pinned

**Independent validation**: Each QR7 finding has behavior-level RED before production correction, committed evidence is independent of mutable checkout metadata, ambiguous JSON/XML remains artifact-local and fail-closed, independent valid artifacts continue, and every prior candidate/review/downstream claim remains invalidated history.

---

## Phase 8D: Quality Review 8 Corrections

**Purpose**: Apply the explicitly approved QR8 Scan-strategy remediation with observed RED before production correction, namespace-safe XML evidence, bounded pinned-tree discovery, reconciled authorities, and fresh surrounding evidence.

- [x] T063 Preserve `specs/002-language-framework-detection/evidence/quality8-scan-strategy-remediation-approval.json`, the unchanged iteration-8 Full verdicts, and explicit invalidation of candidate `7c24d8da4230c5753dfe58723f7d3bf82b0c53f0` plus both review axes before source, test, contract, plan, or story mutation
- [x] T064 Observe independent RED for foreign-namespace NuGet direct evidence, NuGet lock ownership, and Maven evidence; accept only empty or supported MSBuild/Maven root namespaces, require every consumed evidence element to match the accepted root namespace, reject a relevant foreign-namespace lookalike artifact with one bounded audit and no partial fact, continue independent valid artifacts, and prove mixed legitimate/foreign plus standard namespace/no-namespace cases GREEN
- [x] T065 Observe RED for a huge irrelevant committed tree being fully materialized before supported-artifact bounds; enumerate only root and nested `*.csproj`, `pom.xml`, `package.json`, `package-lock.json`, `packages.lock.json`, and `requirements.txt` entries at the pinned commit, consume Git output incrementally under explicit byte/record/deadline/cancellation bounds, preserve tree mode/object identity, fail closed with zero partial facts, reconcile stale `git ls-files --cached` wording in the plan, framework-detection contract, and story, then prove the executable accepted-plan binding GREEN
- [x] T066 Run focused QR8, complete `FrameworkDetectionTests`, provider/profiling/framework surrounding, complete non-final-image, and `Vulgata.Web` build verification; preserve raw RED/GREEN outputs, verify exact correction scope, refresh Current verification and the truthful ledger, rerun independent T040 ledger validation, and leave T041-T050 incomplete until a replacement candidate is pinned

**Independent validation**: Foreign XML lookalikes never become framework facts; valid supported namespaces remain deterministic; irrelevant committed paths cannot consume unbounded detector memory; overflow and cancellation create zero partial facts; plan, contract, story, and executable binding describe the exact pinned-tree behavior; every iteration-8 candidate and review claim remains invalidated history.

---

## Phase 8E: Quality Review 9 Correction and Accepted-Plan Binding

**Purpose**: Apply only the explicitly authorized QR9-001 Scan XML owner-grammar correction, bind executable evidence to the exact accepted plan-review-10 bytes, and finish all mutable evidence work before candidate pinning.

- [x] T067 Verify before implementation that `specs/002-language-framework-detection/reviews/plan-review-10.json` is iteration-10 `ACCEPT` with SHA-256 `dc0638af64c19545adc96a3e8462abc4b9302ac77bc13c3b267726d565ce32be`, reviewed diff `3d273cf81ad4321abd983c6cc72c4b7f5a2612c06ca3363fb9007978cd8834ba`, and the exact five-file artifact map in this header; verify `specs/002-language-framework-detection/evidence/quality9-scan-strategy-remediation-approval.json` has SHA-256 `09a9f89d9f7c25ca523198949fd88ad82cb1e440623d25a0a3bce8418b51d459`, decision `approved`, and scope limited to QR9-001; preserve `specs/002-language-framework-detection/reviews/plan-review-9.json`, `specs/002-language-framework-detection/reviews/acceptance-review-9.json`, `specs/002-language-framework-detection/reviews/quality-review-9.json`, `specs/002-language-framework-detection/workflow-state.json`, and `specs/002-language-framework-detection/evidence/story-4-2-immutable-candidate-3.json` as immutable proof that AR9-001/QR9-001/PR9-001 invalidated candidate `708b7dfca397b8d155111d512c7950bc68651ea8` and every iteration-9 downstream claim
- [ ] T068 [US2] Add only the Target-nested owner assertion in `tests/Vulgata.Tests/FrameworkDetectionTests.cs` for a sole literal `PackageReference` beneath `Target` paired with a catalog-matched committed `packages.lock.json`; run the focused test against unchanged production bytes and retain independently observed behavior-level RED at create-new `TestResults/story-4-2-quality9-correction/T068-target-owner-red.txt`, proving the current detector creates a forbidden `Medium` framework fact rather than zero facts and one bounded closed audit
- [ ] T069 [US2] After T068 without production mutation, add the separate Choose/When-nested owner assertion in `tests/Vulgata.Tests/FrameworkDetectionTests.cs` for a sole literal `FrameworkReference` beneath `Choose/When` paired with a catalog-matched committed `packages.lock.json`; run only that focused case and retain independently observed behavior-level RED at create-new `TestResults/story-4-2-quality9-correction/T069-choose-when-owner-red.txt`, proving the same forbidden `Medium` fact while an independently valid Repository manifest remains detectable
- [ ] T070 Add focused exact-byte assertions in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` for `plan-review-10.json`, its SHA-256/verdict/reviewed diff/five-file artifact map, all five canonical plan hashes, and the QR9 approval path/SHA-256/decision/scope; run only the stale binding assertions before changing resolver/manifest bindings and retain actual assertion RED at create-new `TestResults/story-4-2-quality9-correction/T070-plan-review-10-binding-red.txt`
- [ ] T071 [US2] After T068-T070 RED integrity is accepted, minimally restrict `ValidateNuGetOwnerManifest` in `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` to exactly three literal, unconditional, same-supported-namespace project-level owner shapes: root `Project Sdk`, direct `Project/Sdk`, and `PackageReference`/`FrameworkReference` directly beneath a direct project-level `ItemGroup`; reject `Target`, `Choose/When`, import-driven, conditional/dynamic, or other execution-dependent declarations without evaluating MSBuild, producing zero `Medium` facts, exactly one bounded redacted artifact-local audit with no raw values, and continued independent valid Repository evidence
- [ ] T072 Update only the executable accepted-plan resolver/canonical claim in `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs` to require the exact T067 plan-review-10 verdict/diff/five-file hashes and QR9 approval bytes, reject plan-review-8/9 or absent/wrong QR9 bindings, and pass the focused T070 binding test without changing provider, production schema, API, persistence, migration, UI, Docker, or PRD behavior
- [ ] T073 [US2] Run focused GREEN for T068-T069 plus positive fixtures for all three valid project-level owner shapes in `tests/Vulgata.Tests/FrameworkDetectionTests.cs`; retain create-new `TestResults/story-4-2-quality9-correction/T073-owner-grammar-green.txt` and assert zero `Medium` facts for both invalid nested shapes, exactly one bounded closed redacted audit per invalid artifact, no raw package/property values, deterministic independent-evidence continuation, and unchanged valid owner facts
- [ ] T074 Run the focused T070 binding GREEN, complete `FrameworkDetectionTests`, complete `CodeGraphContextProviderTests`/`RepositoryProfilingTests` surrounding suites, the full non-final-image `tests/Vulgata.Tests/Vulgata.Tests.csproj` suite, `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`, authorization/redaction coverage, FR-020 scope audit, `git diff --check`, and status checks; retain create-new raw outputs under `TestResults/story-4-2-quality9-correction/`, require all current tests GREEN with no `Medium` fact from unsupported owner shapes, and verify no unapproved scope

**Independent validation**: T067 proves plan-review-10 and QR9 authority before implementation. T068 and T069 are separate observed behavior REDs, T070 is a separate exact-binding RED, T071 is minimal production GREEN, T072 binds exact accepted bytes, and T073-T074 prove all three valid owner shapes, bounded redaction, zero false `Medium` facts, independent continuation, and surrounding/full regression safety.

---

## Phase 8F: Final Pre-Pin Current Verification and Ledger

**Purpose**: Regenerate every tracked mutable evidence artifact and independently validate it before exactly one replacement candidate pin.

- [ ] T075 Refresh `specs/002-language-framework-detection/evidence/story-4-2-current-verification.json` at the clean post-T074 immutable source boundary; preserve **“Pre-correction T001 baseline observation: unavailable”**, append only truthfully timed `Current verification` for the exact plan-review-10/QR9/binding/QR9-correction bytes, run focused provider/profiling/framework/binding commands GREEN, and retain create-new raw outputs under `TestResults/story-4-2-current-verification-quality9-refresh/` without calling any result historical, pre-correction, RED, or contemporaneous-order proof
- [ ] T076 Regenerate `specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json` from immutable Git and existing records; preserve all still-provable completed T001-T066 history, add T067-T075 with truthful timestamps and raw hashes, explicitly record candidate `708b7dfca397b8d155111d512c7950bc68651ea8` plus iteration-9 reviews/downstream as invalidated history, cover R1-R17/AR9-001/QR9-001/PR9-001, and claim no new candidate, review, E2E, verification, manifest, namespace, image, T049, or T050
- [ ] T077 Dispatch fresh independent read-only validation of the regenerated ledger and persist the verdict at create-new `specs/002-language-framework-detection/reviews/t040-ledger-validation-5.json`; require exact Git ancestry/blob/trailer and raw-output hashes, exact plan-review-10/QR9 bindings, truthful Current-verification separation, T068/T069/T070 RED-before-GREEN chronology, complete invalidation claims, and zero fabricated/backdated/amended evidence before permitting T041

**Independent validation**: The refreshed Current verification is current-only, the ledger truthfully preserves completed history and invalidations, and a fresh independent validator accepts its exact immutable evidence before any candidate is pinned.

---

## Phase 9: Replacement Candidate, Full Reviews, E2E, and Verification

**Purpose**: Pin and independently judge exactly one candidate containing the accepted plan-review-10 binding, authorized QR9 correction, final Current verification, and independently accepted truthful ledger.

- [ ] T041 After T067-T077 and fresh Full analysis accept, have the root preserve the former checked T041 and `specs/002-language-framework-detection/evidence/story-4-2-immutable-candidate-3.json` as invalidated history and create exactly one clean immutable replacement implementation candidate/checkpoint containing only accepted Story 4.2 owned changes; record commit/tree, exact binary diff SHA-256, artifact hashes, owned paths, plan-review-10/QR9 identities, final T075/T076/T077 hashes, and clean status; any tracked implementation/test/contract/planning/ledger/current-verification/Docker-context mutation after pinning invalidates T041-T050
- [ ] T042 [P] Have the root dispatch a fresh read-only Full `acceptance-review` against the exact replacement T041 boundary and persist its returned verdict at the next create-new review path; require every BMad/SpecKit behavior, truthful T001/T075 contract, exact plan-review-10/QR9 binding, Chinese confidence presentation, QR4-001 through QR4-003, QR6-001, QR7-001 through QR7-003, QR8-001 through QR8-002, QR9-001, AR9-001, PR9-001, all three valid owner shapes, zero invalid-owner `Medium` facts, and forbidden scope to be accepted
- [ ] T043 [P] Have the root dispatch a fresh read-only Full `quality-review` against the identical replacement T041 boundary and persist its returned verdict at the next create-new review path; require correctness, localization, security, committed-evidence integrity, bounded redaction, independent-evidence continuation, test quality, edge cases, architecture seams, and regression risk to be accepted
- [ ] T044 After both fresh reviews accept, record and execute the current-boundary E2E decision in `specs/002-language-framework-detection/workflow-state.json` using `docs/agent-workflow/e2e-decision-matrix.md`; cite a complete coverage override only if current integration evidence covers every triggered API/UI/persistence/migration/image boundary, otherwise run the required E2E owner; any tracked test edit reruns T042-T044
- [ ] T045 Run pre-image verification at the unchanged T041 candidate and persist command evidence through `specs/002-language-framework-detection/workflow-state.json`: focused provider/profiling/framework/binding tests; `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName!~FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract"`; `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`; both migration script/rollback checks; `dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web --context VulgataDbContext`; authorization/redaction coverage; FR-020 diff audit; `git diff --check`; and clean status
- [ ] T046 Read-only cross-validate the finalized `specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json`, T075 Current verification, and T077 independent verdict against the exact T041-T045 candidate/review/E2E/verification identities without editing any candidate-boundary file; any contradiction, stale boundary, fabricated temporal claim, missing current evidence, or post-pin tracked mutation blocks before image work

**Independent validation**: Both Full axes accept the same candidate, E2E is passed or completely covered, verification is GREEN, and the finalized ledger remains truthful. No candidate mutation is hidden.

---

## Phase 10: Fresh Manifest, Image, T049 Freeze, and T050 Audit

**Purpose**: Regenerate every Docker-context-dependent proof and leave the final audit last.

- [ ] T047 At the exact clean T041 source candidate, create a new unused accepted-correction namespace and canonical `production-source-manifest-v1` through `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`; include the corrected plan/tasks/test/ledger Docker-context bytes, bind candidate commit/tree and every tracked non-ignored context hash, and stop before image creation on dirty status, identity drift, symlink/collision, stale namespace, or manifest mismatch
- [ ] T048 Build a fresh image from `docker/Dockerfile` with the T047 commit/tree/manifest labels, inspect its immutable ID before provider execution, run `FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract` once in the unused namespace, and rerun the full suite, Web build, migration/model checks, authorization/redaction tests, diff/scope/status checks at the same source; require exact provider 0.5.1/query/schema/accepted-plan binding, both projections above 100 complete rows, empty-file `Filtered/UnknownLanguage`, atomic failures, cleanup, false forbidden retention, and no reused image/evidence/namespace
- [ ] T049 Freeze the exact T041 source candidate plus T042-T048 verdict/E2E/verification/ledger/manifest/image/claim/evidence identities, rerun all hash/reference/status checks without changing source, tests, contracts, planning artifacts, ledger, evidence, or image, and prepare the immutable inputs for T050; any mutation or stale/missing identity invalidates T041-T049 and requires a new namespace/image/gate
- [ ] T050 Audit the immutable candidate last against FR-001-FR-021, AC-1-AC-7, SC-001-SC-008, accepted decisions R1-R17, AR9-001, QR9-001, PR9-001, all prior accepted correction findings, the exact five-file plan set and plan-review-10/QR9 approval, Story 4.1 owning provider contract, truthful T001/T075 Current verification, T068-T074 RED/GREEN, T076-T077 ledger proof, both Full reviews, E2E, verification, two-migration rehearsal, forbidden-scope diff, and fresh source/image/evidence binding; mark complete only after an unchanged audit succeeds, otherwise leave `[ ]`

**Independent validation**: One immutable source candidate owns accepted Full reviews, E2E, verification, truthful ledger, a fresh source-bound image and namespace, exact final-image evidence, and an unchanged T049 freeze. T050 is last and remains incomplete until every prerequisite passes.

---

## Dependencies and Execution Order

```text
plan-review-10 exact five-file ACCEPT + QR9 approval
  -> regenerated tasks checkpoint
  -> fresh Full analysis
  -> preserved valid completed history T002-T040 + T051-T066
  -> T001 clean immutable post-task-checkpoint Current verification
  -> T067 authority/invalidation binding
  -> T001 + T067 prerequisites satisfied
     -> T068 Target-nested RED
     -> T069 Choose/When-nested RED
     -> T070 plan-review-10 stale-binding RED
  -> T071 minimal owner-grammar GREEN
  -> T072 exact executable binding GREEN
  -> T073 focused owner-shape GREEN
  -> T074 surrounding/full GREEN
  -> T075 Current verification
  -> T076 truthful ledger regeneration
  -> T077 fresh independent ledger validation
  -> T041 one replacement immutable candidate pin
  -> T042 acceptance-review + T043 quality-review on the same boundary
  -> T044 conditional E2E
  -> T045 verification
  -> T046 read-only ledger/current-gate cross-validation
  -> T047 new manifest + namespace
  -> T048 fresh image + exact gate + full current gates
  -> T049 freeze
  -> T050 final audit
```

- Full `speckit-analyze` runs after this `tasks.md` and before T001 or T067; implementation does not start until the latest analysis has no Critical/High finding.
- T001 and T067 are both prerequisites of T068-T070. T001 creates the clean immutable post-task-checkpoint Current-verification record; T067 is the separate executable authority/invalidation check. T068 and T069 are serialized because they edit `tests/Vulgata.Tests/FrameworkDetectionTests.cs`, and both must independently fail before T071 changes `FrameworkEvidenceDetector.cs`.
- T070 is independent of the two owner-behavior RED observations but must also retain its exact stale-binding failure before T072; T071 and T072 remain serialized so the accepted boundary has one auditable GREEN transition.
- T075-T077 are serialized and precede T041 because Current verification and ledger bytes are tracked candidate inputs.
- T042 and T043 are the only `[P]` tasks. They are read-only independent axes over the identical T041 boundary and persist separate verdict paths.
- T044 waits for both reviews. A product correction returns to fresh TDD and reruns both axes; a tracked E2E test edit also reruns both axes before E2E can advance.
- T041-T049 are one immutable-candidate lane. Review/evidence records may be persisted outside the nonrecursive implementation diff, but source/test/contract/ledger/Docker-context mutation invalidates the lane.
- T047-T049 are serialized because the namespace, image, claim, evidence, and freeze identities are single-use and candidate-bound.

## Parallel Opportunities

- T042 and T043 may run concurrently against the exact same T041 boundary.
- Read-only coverage counting and referenced-hash validation may be sharded only after the owning artifact is immutable.
- No implementation task is parallelizable: T068-T074 share test/production/binding boundaries, T075-T077 share tracked pre-pin evidence, and T041-T050 share one immutable candidate or single-use namespace.

## Acceptance, Decision, and Finding Coverage

| Obligation | Executable coverage |
| --- | --- |
| FR-001-FR-003, FR-005-FR-006; AC-1-AC-2; SC-001-SC-002 | T001, T005-T007, T075, T042-T045, T048-T050 |
| FR-004, FR-007; AC-6; SC-004 | T019-T020, T042-T045, T048-T050 |
| FR-008-FR-014, FR-016, FR-021; AC-3-AC-5; SC-003/SC-005 | T008-T018, T068-T074, T042-T045, T048-T050 |
| FR-015, FR-017; AC-7; SC-006 | T002-T003, T011, T021-T034, T042-T045, T048-T050 |
| FR-018-FR-019; AC-7; SC-007-SC-008 | T007, T012, T020-T025, T042-T045, T048-T050 |
| FR-020 forbidden scope | T038, T042-T045, T048-T050 |
| R1-R14 functional/evidence design | T002-T034, T068-T074, T042-T050 |
| R15 / TR-001 truthful unavailable T001 | T001, T075-T077, T046, T049-T050 |
| R16 / T001-PLAN-001 executable accepted-plan binding | T001, T067, T070, T072, T074-T077, T041-T050 |
| R17 / QR9-001 authorized owner grammar | T067-T074, T076-T077, T041-T050 |
| AR9-001 pinned committed-tree authority | T063-T067, T074, T076-T077, T042-T050 |
| PR9-001 candidate-stable downstream ordering | T067, T075-T077, T041-T050 |
| AR2-001 | T022-T025, T042-T045, T048-T050 |
| AR2-002 / CF-002 | T026-T030, T042-T043, T048-T050 |
| QR2-001 | T013-T018, T042-T045, T048-T050 |
| AR2-003 / QR2-002 / WF-002 | T041-T050, with T049 prerequisite freeze and T050 last |
| EV-002 | T031-T034, T041, T047-T050 |
| PL-001 / PA-001 | T001, T035-T038, T045, T048-T050 |

Coverage totals: **21/21 functional requirements**, **8/8 success criteria**, **7/7 acceptance-criteria groups**, **17/17 accepted plan decisions**, with explicit executable coverage for **AR9-001**, **QR9-001**, and **PR9-001**.

## Bounded Task-Group Partition

1. **Preserved completed history**: T002-T040 and T051-T066; no relabelling, implementation, or fabricated evidence.
2. **Current authority prerequisites**: T001 and T067; clean immutable post-task-checkpoint Current verification plus separate exact plan-review-10/QR9 executable authority/invalidation verification before T068-T070.
3. **QR9 and binding RED**: T068-T070 after both T001 and T067; two independent behavior REDs plus one exact stale-binding RED.
4. **Minimal GREEN and regression**: T071-T074; one production correction, one binding correction, focused and surrounding/full GREEN.
5. **Final mutable pre-pin evidence**: T075-T077; Current verification, truthful ledger, fresh independent validation.
6. **Full candidate review/E2E/verification**: T041-T046; root/reviewer/runner-owned, one pinned nonrecursive boundary.
7. **Fresh manifest/image gate**: T047-T049; one candidate, one unused namespace, one fresh image, serialized.
8. **Final immutable-candidate audit**: T050 alone and last.

## Implementation Strategy

- Preserve completed pre-QR9 behavior/evidence as history; implement only the QR9 owner grammar and exact plan-review-10/QR9 binding after fresh Full analysis and observed RED.
- Regenerate Current verification and the ledger, then obtain fresh independent ledger acceptance before exactly one replacement candidate pin.
- Do not carry forward candidate `708b7df`, iteration-9 reviews, E2E, verification, manifest, image, namespace, T049, or T050 conclusions.
- Stop at any failed RED integrity, authority, ancestry, review, E2E, verification, ledger, image, or scope gate. Do not weaken or relabel evidence to advance.

## Completion Criteria

- All 77 task markers use checkbox, exact ID, required story labels, concrete repository paths or exact commands, and dependency-correct ordering.
- Completed pre-QR9 tasks remain truthful history; corrected T001, every QR9 task, and every downstream marker are incomplete.
- T001 and T067 both bind the exact plan-review-10 and QR9 authorization boundary before T068-T070; T001 additionally requires clean immutable post-task-checkpoint Current verification.
- T068 and T069 each retain independently observed runnable behavior RED, and T070 retains stale-binding RED, before T071-T072 GREEN.
- Every accepted requirement, success criterion, acceptance group, plan decision, and correction finding maps to executable validation.
- QR9 approval remains exact and bounded; no additional planned-change trigger beyond the authorized QR9 scope is introduced.
- Current verification and the ledger are regenerated and independently validated before one candidate pin; both Full reviews and every dependent gate then use that unchanged boundary.
- T041-T050 remain unchecked; T050 is last and cannot pass before every prerequisite and the immutable-candidate audit.
