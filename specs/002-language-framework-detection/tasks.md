---

description: "Dependency-ordered remediation tasks for Story 4.2 language and framework detection"
---

# Tasks: Language and Framework Detection

**Stage**: `tasks`

**BMad Story**: `4-2-language-and-framework-detection` (Full execution; lifecycle remains `review`; this artifact does not advance or repair lifecycle state)

**Source boundary**: commit `fe08df102957276f4376b9ca62b5921d19113003`, tree `6db827214d282961e4ded4e3c94bc0b975faa827`, empty candidate diff SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`

**Accepted plan checkpoint**: ID `bmad-4-2-plan-20260726T041520Z-3fbfd5`, uniquely trailer-resolved commit `fe08df102957276f4376b9ca62b5921d19113003`

**Fresh accepted verdict**: `specs/002-language-framework-detection/reviews/plan-review-8.json`, SHA-256 `fcfd3fefc3914d5d01d3021869c2856cba6e6b7139106e4821deb6d554371087`, verdict `ACCEPT`, reviewed diff SHA-256 `b9e98e34ec19b6c495a3478de9811974222001a3931f2ccffbf279d6b3e4eb79`

**Complete reviewed artifact-hash map**: `specs/002-language-framework-detection/plan.md` `2cc2a9cb0bf25f31d03e56066043916f91a607267e660e4caa0fd4c1fe948600`; `specs/002-language-framework-detection/research.md` `e75d65e261bedb7417d0aa4682096308970cb07ed01b1fe078285d44b31427c4`; `specs/002-language-framework-detection/quickstart.md` `4565c5a3a5404f72018d00c103247ae84db8f1bce22febcd87bff6137da21115`

**Unchanged accepted inputs**: `spec.md` `924d9d5d1f104e4206f145f565eb7f3e8c813c67fdd88da2abf55cb08456f956`; `data-model.md` `76130d3cd76e029e0e112041be05b4822dcd15e430f38ff3502b53a228b3288c`; Story 4.2 contracts `b5503794a0b05076b441f684b50948b419bcfdd7d21736f0f5430de7eaf04d9b`, `74f71a218adca654b79d24bd1558f585b9ae7ff7ec95ca5370f5d153d7826181`, and `b3b7d0c3cfe6a236d4b25b54239cf207fe546d2518956afb9d54abdae7c94dad`; Story 4.1 owning provider contract `ca2086166c91820e48f3e5869374ffa1c5fcb8fdddf20480e402194856238124`

**Authorization**: `specs/002-language-framework-detection/evidence/planned-change-authorization-2.json`, SHA-256 `33c926b9125fb35628489f13971172d8d083b0ab5d8c8a432815d463731ab24f`, authorizes only the unchanged validated NuGet/npm lock-owner filtering design. It does not cover a new Worker Agent/tool, Scan strategy, or PRD change and waives no analysis, TDD, review, E2E, security, verification, or Git gate.

## Evidence and Ordering Rules

- The exact historical statement is **“Pre-correction T001 baseline observation: unavailable”**. The superseded T001 observation was not captured before T002-T049, is temporally unavailable, and cannot be recreated by running commands now.
- A result produced now is **Current verification** only. It must retain its actual UTC time, immutable commit/tree, command, exit code, and raw-output path and must not be called historical, pre-correction, RED, or proof of contemporaneous ordering.
- Checked tasks preserve still-valid implementation and immutable evidence. They do not manufacture unavailable historical raw RED output or missing checkpoint trailers.
- Every new changed behavior uses observed runnable RED before implementation. Compile failure, broken fixture, inferred failure, fabricated output, backdating, amended history, or implementation-before-RED is a blocker.
- The only permitted edit before the second accepted-plan-binding RED is the focused assertion added by T036. T035 changes no file. T037 may not start until both T035 and T036 retain actual failing output.
- Each RED/GREEN record includes the exact command, exit code, immutable source, observed assertion, and create-new raw-output path. Current GREEN is never relabelled as historical RED.
- No task adds or changes a Scan Coordinator, Orchestrator, Worker Agent/tool, dispatch, scheduling, grouping, retry, LLM fact authority, Communication Pattern Catalog, Story 4.3 behavior, document generation, Cross-Repository Resolution, or downstream language-support promise.

## Phase 1: Current Verification and Preserved Foundation

**Purpose**: Replace the impossible old T001 prerequisite with truthful present-day verification and retain only still-valid foundation behavior.

- [x] T001 Create `specs/002-language-framework-detection/evidence/story-4-2-current-verification.json` against a root-supplied clean immutable commit containing the accepted plan set and this regenerated `tasks.md`: preserve the exact unavailable-baseline statement above; record `Current verification`, actual UTC observation time, commit/tree, commands, exit codes, and create-new raw outputs `TestResults/story-4-2-current-verification/T001-provider-profiling-green.txt` and `TestResults/story-4-2-current-verification/T001-framework-green.txt`; prove the checkpoint ID/commit and ancestor relationship above, the exact `plan-review-8.json` path/hash/verdict/diff/artifact map, and the unchanged authorization path/hash/scope; then run the two focused provider/profiling and framework commands from `specs/002-language-framework-detection/quickstart.md` GREEN without any historical/pre-correction/RED/contemporaneous-ordering claim
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

**Independent validation**: The ledger is internally consistent, discloses missing history, distinguishes `Current verification`, contains no stale completion identity, and is ready to be included in the new reviewed candidate.

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

## Phase 9: New Candidate, Full Reviews, E2E, and Verification

**Purpose**: Pin and independently judge one candidate containing T001, the accepted-plan binding GREEN, and the truthful regenerated ledger.

- [ ] T041 After T001, T035-T040, T051-T054, T055-T057, and T058-T062, have the root create one clean immutable implementation candidate/checkpoint containing only accepted Story 4.2 owned changes; record commit/tree, exact binary diff SHA-256, artifact hashes, owned paths, and clean status; any implementation/test/ledger mutation after pinning invalidates T041-T049
- [ ] T042 [P] Have the root dispatch a fresh read-only Full `acceptance-review` against the exact refreshed T041 boundary and persist its returned verdict at the next create-new review path; require every BMad/SpecKit behavior, corrected T001 truth contract, replacement binding RED/GREEN, Chinese confidence presentation, QR4-001 through QR4-003, QR6-001, QR7-001 through QR7-003 corrections, and forbidden scope to be accepted
- [ ] T043 [P] Have the root dispatch a fresh read-only Full `quality-review` against the identical refreshed T041 boundary and persist its returned verdict at the next create-new review path; require correctness, localization, security, committed-evidence integrity, test quality, edge cases, architecture seams, and regression risk to be accepted
- [ ] T044 After both fresh reviews accept, record and execute the current-boundary E2E decision in `specs/002-language-framework-detection/workflow-state.json` using `docs/agent-workflow/e2e-decision-matrix.md`; cite a complete coverage override only if current integration evidence covers every triggered API/UI/persistence/migration/image boundary, otherwise run the required E2E owner; any tracked test edit reruns T042-T044
- [ ] T045 Run pre-image verification at the unchanged T041 candidate and persist command evidence through `specs/002-language-framework-detection/workflow-state.json`: focused provider/profiling/framework/binding tests; `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName!~FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract"`; `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`; both migration script/rollback checks; `dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web --context VulgataDbContext`; authorization/redaction coverage; FR-020 diff audit; `git diff --check`; and clean status
- [ ] T046 Read-only cross-validate the finalized `specs/002-language-framework-detection/evidence/story-4-2-historical-tdd-reconstruction.json` against the exact T041-T045 candidate, review, E2E, and verification identities without editing the ledger or any Docker-context file; any contradiction, stale boundary, fabricated temporal claim, or missing current evidence blocks before image work

**Independent validation**: Both Full axes accept the same candidate, E2E is passed or completely covered, verification is GREEN, and the finalized ledger remains truthful. No candidate mutation is hidden.

---

## Phase 10: Fresh Manifest, Image, T049 Freeze, and T050 Audit

**Purpose**: Regenerate every Docker-context-dependent proof and leave the final audit last.

- [ ] T047 At the exact clean T041 source candidate, create a new unused accepted-correction namespace and canonical `production-source-manifest-v1` through `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`; include the corrected plan/tasks/test/ledger Docker-context bytes, bind candidate commit/tree and every tracked non-ignored context hash, and stop before image creation on dirty status, identity drift, symlink/collision, stale namespace, or manifest mismatch
- [ ] T048 Build a fresh image from `docker/Dockerfile` with the T047 commit/tree/manifest labels, inspect its immutable ID before provider execution, run `FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract` once in the unused namespace, and rerun the full suite, Web build, migration/model checks, authorization/redaction tests, diff/scope/status checks at the same source; require exact provider 0.5.1/query/schema/accepted-plan binding, both projections above 100 complete rows, empty-file `Filtered/UnknownLanguage`, atomic failures, cleanup, false forbidden retention, and no reused image/evidence/namespace
- [ ] T049 Freeze the exact T041 source candidate plus T042-T048 verdict/E2E/verification/ledger/manifest/image/claim/evidence identities, rerun all hash/reference/status checks without changing source, tests, contracts, planning artifacts, ledger, evidence, or image, and prepare the immutable inputs for T050; any mutation or stale/missing identity invalidates T041-T049 and requires a new namespace/image/gate
- [ ] T050 Audit the immutable candidate last against FR-001-FR-021, AC-1-AC-7, SC-001-SC-008, all 16 accepted plan decisions R1-R16, all 11 iteration-2 findings plus `T001-PLAN-001`, the exact plan/data-model/quickstart/contracts, Story 4.1 owning provider contract, T001 current verification, T035-T038 RED/GREEN, both Full reviews, E2E, verification, two-migration rehearsal, forbidden-scope diff, truthful ledger, and fresh source/image/evidence binding; mark complete only after an unchanged audit succeeds, otherwise leave `[ ]`

**Independent validation**: One immutable source candidate owns accepted Full reviews, E2E, verification, truthful ledger, a fresh source-bound image and namespace, exact final-image evidence, and an unchanged T049 freeze. T050 is last and remains incomplete until every prerequisite passes.

---

## Dependencies and Execution Order

```text
accepted plan checkpoint fe08df1...
  -> regenerated tasks checkpoint + clean immutable current-verification commit
  -> T001 Current verification
  -> preserved valid work T002-T034
  -> T035 canonical-hash RED
  -> T036 stale-claim RED
  -> T037 binding GREEN
  -> T038 surrounding GREEN
  -> T039 ledger regeneration
  -> T040 ledger validation
  -> T041 immutable candidate pin
  -> T042 acceptance-review + T043 quality-review on the same boundary
  -> T044 conditional E2E
  -> T045 verification
  -> T046 read-only ledger/current-gate cross-validation
  -> T047 new manifest + namespace
  -> T048 fresh image + exact gate + full current gates
  -> T049 freeze
  -> T050 final audit
```

- Full `speckit-analyze` runs after this `tasks.md` and before T001; implementation does not start until the latest analysis has no Critical/High finding.
- T001 requires the root's post-task accepted checkpoint/clean immutable commit; it cannot run against this uncommitted task-author boundary.
- T035 and T036 are serialized because both use `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`; only T036's focused assertion edit is allowed before its observed RED.
- T042 and T043 are the only `[P]` tasks. They are read-only independent axes over the identical T041 boundary and persist separate verdict paths.
- T044 waits for both reviews. A product correction returns to fresh TDD and reruns both axes; a tracked E2E test edit also reruns both axes before E2E can advance.
- T041-T049 are one immutable-candidate lane. Review/evidence records may be persisted outside the nonrecursive implementation diff, but source/test/contract/ledger/Docker-context mutation invalidates the lane.
- T047-T049 are serialized because the namespace, image, claim, evidence, and freeze identities are single-use and candidate-bound.

## Parallel Opportunities

- T042 and T043 may run concurrently against the exact same T041 boundary.
- Read-only coverage counting and referenced-hash validation may be sharded only after the owning artifact is immutable.
- No implementation task is parallelizable: T035-T040 share one test/evidence boundary, and T041-T050 share one immutable candidate or single-use namespace.

## Acceptance, Decision, and Finding Coverage

| Obligation | Executable coverage |
| --- | --- |
| FR-001-FR-003, FR-005-FR-006; AC-1-AC-2; SC-001-SC-002 | T001, T005-T007, T035-T038, T042-T045, T048-T050 |
| FR-004, FR-007; AC-6; SC-004 | T019-T020, T042-T045, T048-T050 |
| FR-008-FR-014, FR-016, FR-021; AC-3-AC-5; SC-003/SC-005 | T008-T018, T042-T045, T048-T050 |
| FR-015, FR-017; AC-7; SC-006 | T002-T003, T011, T021-T034, T042-T045, T048-T050 |
| FR-018-FR-019; AC-7; SC-007-SC-008 | T007, T012, T020-T025, T042-T045, T048-T050 |
| FR-020 forbidden scope | T038, T042-T045, T048-T050 |
| R1-R14 functional/evidence design | T002-T034, T042-T050 |
| R15 / TR-001 truthful unavailable T001 | T001, T039-T040, T046, T049-T050 |
| R16 / T001-PLAN-001 executable accepted-plan binding | T035-T038, T041-T050 |
| AR2-001 | T022-T025, T042-T045, T048-T050 |
| AR2-002 / CF-002 | T026-T030, T042-T043, T048-T050 |
| QR2-001 | T013-T018, T042-T045, T048-T050 |
| AR2-003 / QR2-002 / WF-002 | T041-T050, with T049 prerequisite freeze and T050 last |
| EV-002 | T031-T034, T041, T047-T050 |
| PL-001 / PA-001 | T001, T035-T038, T045, T048-T050 |

Coverage totals: **21/21 functional requirements**, **8/8 success criteria**, **7/7 acceptance-criteria groups**, **16/16 accepted plan decisions**, and **12/12 correction findings** (the 11 iteration-2 findings plus `T001-PLAN-001`).

## Bounded Task-Group Partition

1. **Current verification**: T001 only; evidence write, no product/test change.
2. **Preserved implementation**: T002-T034; no relabelling of historical evidence and no new implementation.
3. **Accepted-plan binding TDD**: T035-T038; one test file, two observed REDs, minimal binding GREEN, surrounding GREEN.
4. **Truthful ledger**: T039-T040 regeneration/validation, then T046 read-only cross-validation after current gates; one durable ledger plus the separate T001 record.
5. **Full candidate review/E2E/verification**: T041-T045; root/reviewer/runner-owned, one pinned nonrecursive boundary.
6. **Fresh manifest/image gate**: T047-T049; one candidate, one unused namespace, one fresh image, serialized.
7. **Final immutable-candidate audit**: T050 alone and last.

## Implementation Strategy

- Preserve the completed language/framework behavior; the only new tracked implementation work is the evidence-contract test binding in T035-T038.
- Complete T001 and obtain a clean Full analysis before dispatching the binding group.
- Do not carry forward the old ledger, candidate, reviews, E2E, verification, manifest, image, namespace, T049, or T050 conclusion.
- Stop at any failed RED integrity, authority, ancestry, review, E2E, verification, ledger, image, or scope gate. Do not weaken or relabel evidence to advance.

## Completion Criteria

- All 50 tasks use checkbox, exact ID, required story labels, concrete repository paths or exact commands, and dependency-correct ordering.
- T001 truthfully records current GREEN without converting it into unavailable historical evidence.
- T035 and T036 each retain observed runnable RED before T037 changes the stale bindings.
- Every accepted requirement, success criterion, acceptance group, plan decision, and correction finding maps to executable validation.
- Prior planned-change authorization remains scoped only to unchanged lock-owner filtering; no new trigger is introduced.
- The ledger, candidate, both Full reviews, E2E, verification, manifest, image, namespace, T049, and T050 are regenerated rather than carried forward.
- T049 and T050 remain unchecked; T050 is last and cannot pass before T001-T049 and the immutable-candidate audit.
