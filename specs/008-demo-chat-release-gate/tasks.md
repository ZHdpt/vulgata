# Tasks: Demo Chat, Citations, and Release Gate

**Input**: Accepted design documents from `specs/008-demo-chat-release-gate/`

**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/chat-agent-contract.md`, `contracts/release-verification-contract.md`, accepted `reviews/plan-review/plan-review-iteration-4.json`, and recorded planned-change authorization in `workflow-state.json`

**Pinned boundary**: Source commit `c07df7d8789e7b975dc7fb6a121dff564e8549ad`; accepted plan-review iteration 4; planned-change authorization `2026-08-04T12:42:33Z` (`user: Approve`)

**Frozen design hashes**: `plan.md` `80123c7ac60f65775e7a3214e13264bdf16997917c0cb3fbeeb8fe2da6ff262a`; `research.md` `85428e46a538183ef3ac9eb5d51f6435d2b990fdb3121028bc92cd520168d6c2`; `data-model.md` `1ad29fc52b86e00634341f224ffa99370f24d0759e55bc83357bdd85c3fdf2f9`; `quickstart.md` `75eac1c541809bfd7320a93cdbc49f9fa9732a494ad2581486788c4cd0fc25f5`; chat contract `13d1d1ce8c96333cabd3c054f8c98648639b411db4bc6027ee983748ce276b19`; release contract `40a8b2e8adbba3941ece7b4be27296bd9e297fce45e496b773a3ae9485ee09a8`

**Tests**: Mandatory observed RED -> GREEN -> REFACTOR. Each production group below starts with a test-writing task, then a distinct task that runs the focused filter and records the expected failure, then implementation, then focused GREEN/regression. RED is transient and no deliberately failing test may enter the accepted candidate.

**Ownership boundary**: Core owns provider-independent contracts only; Infrastructure owns EF reads; Agents owns candidate selection, provider resolution, embedded prompts, dispatch, and closed result validation; Web owns user authorization, call ordering, transient UI, citations, and composition; `Story115ReleaseHarness` solely owns final clean-database/Docker/browser resources and evidence. No generic repository, EF surface in Core, migration/model change, Scan or matching expansion, retry/failover, durable chat history, or deferred Epic 5-10 behavior is permitted.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run concurrently because it changes different files and has no dependency on an incomplete task.
- **[Story]**: Maps the task to `[US1]`, `[US2]`, or `[US3]` from `spec.md`.
- Every checklist item names exact repository-relative paths.

---

## Phase 1: Setup and Boundary Confirmation

**Purpose**: Confirm the accepted inputs and establish a reproducible green baseline without changing implementation.

- [ ] T001 Verify `git rev-parse HEAD`, the six frozen SHA-256 values, accepted iteration-4 verdict, and recorded `user: Approve` authorization against `specs/008-demo-chat-release-gate/plan.md`, `specs/008-demo-chat-release-gate/research.md`, `specs/008-demo-chat-release-gate/data-model.md`, `specs/008-demo-chat-release-gate/quickstart.md`, `specs/008-demo-chat-release-gate/contracts/chat-agent-contract.md`, `specs/008-demo-chat-release-gate/contracts/release-verification-contract.md`, `specs/008-demo-chat-release-gate/reviews/plan-review/plan-review-iteration-4.json`, and `specs/008-demo-chat-release-gate/workflow-state.json`; stop on any mismatch.
- [ ] T002 Restore and run the pre-change Web build plus full test baseline for `src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and `tests/Vulgata.Tests/Vulgata.Tests.csproj`, recording command, exit code, source boundary, timestamp, and concise result under `tests/Vulgata.Tests/TestResults/story-11-5-baseline/`.

**Checkpoint**: The accepted boundary is unchanged and the repository baseline is known before any RED is introduced.

---

## Phase 2: Foundational Provider-Independent Contracts (Blocking)

**Purpose**: Establish the closed Core types and service boundaries required by every story without EF, provider SDK, or Web dependencies.

**Independent validation**: Contract tests prove closed modes/outcomes, normalized 1-2,000-character questions, exactly two canonical Repository IDs, packet and selection maxima, deterministic fingerprints/support signatures, positive safe Source Anchors, fixed claim-to-citation mappings, and provider-independent retrieval/currentness/dispatcher interfaces.

- [ ] T003 Write the foundational contract RED for valid and invalid `ChatQuestionRequest`, `EvidencePacket`, `CandidateClaim`, `ChatCurrentnessValidationRequest`, `ChatSynthesisResult`, `MaterialClaim`, `DocumentCitation`, `GroundedAnswer`, closed outcomes, canonical ordering, hashes, bounds, and failure codes in `tests/Vulgata.Tests/ChatContractTests.cs`.
- [ ] T004 Run `FullyQualifiedName~ChatContractTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent Core chat contracts and interfaces in `tests/Vulgata.Tests/TestResults/story-11-5-red/core-contracts.txt`.
- [ ] T005 Implement provider-independent records, enums, invariant validation, canonical ordering/fingerprinting, and bounded failure results in `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`, plus the specific interfaces in `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceRetriever.cs`, `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceCurrentnessValidator.cs`, and `src/dotnet/Vulgata.Core/DomainServices/IChatAgentDispatcher.cs`, with no EF, SDK, `DbSet`, `IQueryable`, or generic repository exposure.
- [ ] T006 Run `FullyQualifiedName~ChatContractTests` GREEN, refactor duplicate invariant/canonicalization code only within `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`, rerun the focused tests, and run the Core project boundary/analyzer build for `src/dotnet/Vulgata.Core/Vulgata.Core.csproj`.

**Checkpoint**: All later work consumes a stable, provider-independent, bounded Core contract.

---

## Phase 3: User Story 1 - Ask About the Prepared Flow (Priority: P1) 🎯 MVP

**Goal**: An authenticated user explicitly selects Business or IT Mode, one prepared System, and exactly two authorized prepared Repositories and receives a bounded Chinese answer grounded only in the current persisted prepared flow.

**Independent Test**: With current prepared evidence and authorized scope, run both modes through the coordinator and interactive component; verify one bounded retrieval, mode-eligible exact persisted spans, one selection-only synthesis call, a complete Chinese answer, explicit scope controls, no automatic scope/file upload, and no transaction spanning provider latency. Currentness and adversarial citation security are strengthened independently in US2.

### Prepared evidence retrieval

- [ ] T007 [P] [US1] Write relational retrieval RED cases for current Document Versions, bounded canonically ordered Sections/Source Anchors/flow/provenance, the exact accepted-profile directed cross-Repository `REFERENCES` Edge and `GENERATED_FROM`, both-endpoint live authorization, deterministic handles/fingerprint, hard maxima, and missing-equivalent stale/restricted/out-of-scope rejection in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`.
- [ ] T008 [US1] Run `FullyQualifiedName~ChatEvidenceRetrievalTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent bounded relational retriever in `tests/Vulgata.Tests/TestResults/story-11-5-red/evidence-retrieval.txt`.
- [ ] T009 [US1] Implement the single current, authorized, read-only Evidence Packet query with live `RepositoryAccessScope`, no `index.md`, no partial packet, and no public EF/query surface in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`.
- [ ] T010 [US1] Run `FullyQualifiedName~ChatEvidenceRetrievalTests` GREEN, refactor query/canonicalization code within `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`, rerun the focused tests, and verify no transaction remains open when `RetrieveAsync` returns.

### Exact candidate extraction

- [ ] T011 [P] [US1] Write candidate-extraction RED vectors covering exact Unicode paragraph/list-item offsets, canonical order, BL `Overview`/`ProcessStep` eligibility, CL `Overview`/`ProcessStep`/`TechnicalDetail`/`Evidence` eligibility, safe-text/no-code-locator filtering, fixed Anchor/relationship support signatures, and exclusion of fenced code, tables, raw HTML, links, restricted references, missing support, and over-bound spans in `tests/Vulgata.Tests/ChatCandidateExtractorTests.cs`.
- [ ] T012 [US1] Run `FullyQualifiedName~ChatCandidateExtractorTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent exact persisted-span extractor in `tests/Vulgata.Tests/TestResults/story-11-5-red/candidate-extractor.txt`.
- [ ] T013 [US1] Implement model-independent exact persisted-span extraction and immutable SHA-256 support signatures, without paraphrase, normalization, concatenation, locator invention, or EF access, in `src/dotnet/Vulgata.Agents/ChatExecution/ChatCandidateExtractor.cs`.
- [ ] T014 [US1] Run `FullyQualifiedName~ChatCandidateExtractorTests` GREEN, refactor parsing while preserving byte-for-byte offsets in `src/dotnet/Vulgata.Agents/ChatExecution/ChatCandidateExtractor.cs`, and rerun the focused test set.

### Chat provider, prompts, and one-call dispatch

- [ ] T015 [P] [US1] Write dispatcher RED cases for exact System `AgentType.Chat` override precedence, ordered global Chat default, Chat Completions/endpoint/model/encrypted-key validation, dispatch-time decryption, one call, 30-second linked timeout, cancellation, provider failure, no Worker fallback, no retry/failover, prompt resource mode mapping, and selection-only request content in `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs`.
- [ ] T016 [US1] Run `FullyQualifiedName~ChatAgentDispatcherTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by absent Chat resolution/prompts/dispatch in `tests/Vulgata.Tests/TestResults/story-11-5-red/chat-dispatcher.txt`.
- [ ] T017 [US1] Implement `AgentType.Chat` assignment resolution in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentProviderResolver.cs`, embedded-resource loading in `src/dotnet/Vulgata.Agents/ChatExecution/ChatPromptCatalog.cs`, one OpenAI-compatible selection-only call in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs`, versioned prompts in `src/dotnet/Vulgata.Agents/Prompts/chat-business.v1.md` and `src/dotnet/Vulgata.Agents/Prompts/chat-it.v1.md`, and exact logical resource names in `src/dotnet/Vulgata.Agents/Vulgata.Agents.csproj`.
- [ ] T018 [US1] Run `FullyQualifiedName~ChatAgentDispatcherTests` GREEN, inspect captured provider requests for only mode-eligible handles/exact text and no credentials/locators/internal IDs, refactor within `src/dotnet/Vulgata.Agents/ChatExecution/`, and rerun the focused tests.

### Prepared chat coordinator happy path

- [ ] T019 [US1] Write coordinator RED cases for authorized prepared-scope listing, explicit mode/System/exactly-two distinct Repository validation, normalized question bounds, same-System preauthorization, one retrieval/extraction/dispatch call, server-owned exact claims/citations, cancellation, mode/scope reset, bounded Chinese failures, and no prior/partial answer publication in `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`.
- [ ] T020 [US1] Run `FullyQualifiedName~PreparedChatCoordinatorTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent coordinator in `tests/Vulgata.Tests/TestResults/story-11-5-red/prepared-chat-coordinator.txt`.
- [ ] T021 [US1] Implement `ListScopesAsync` and the bounded transient `AskAsync` orchestration with `AuthenticationStateProvider`/`UserContext`, explicit scope, server-retained claims, and fixed Chinese outcomes in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`; use faked successful currentness/final authorization at this increment boundary without weakening the production ordering seam completed in US2.
- [ ] T022 [US1] Run `FullyQualifiedName~PreparedChatCoordinatorTests` GREEN, refactor orchestration without adding asynchronous work between security stages in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, and rerun retrieval, dispatcher, and coordinator focused tests from `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

### Chinese prepared chat UI

- [ ] T023 [US1] Write bUnit RED cases for `[Authorize]`, Business/IT explicit selection, prepared System and exactly-two Repository selectors, question validation, Chinese `LoadState` transitions, mode-locked Library Oak theme, keyboard send/cancel, clearing on mode/scope/new request/failure, atomic answer display, and absence of upload/automatic-scope/history/graph/live-notification controls in `tests/Vulgata.Tests/ChatPageTests.cs`.
- [ ] T024 [US1] Run `FullyQualifiedName~ChatPageTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED against the placeholder page in `tests/Vulgata.Tests/TestResults/story-11-5-red/chat-page.txt`.
- [ ] T025 [US1] Replace the authenticated placeholder with the Simplified-Chinese Fluent UI flow in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, implement transient circuit state, `LoadState`, cancellation, and identity acquisition in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`, and apply only existing Library Oak tokens/classes in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.css` and `src/dotnet/Vulgata.Web/wwwroot/css/brand.css`.
- [ ] T026 [US1] Run `FullyQualifiedName~ChatPageTests` GREEN, refactor the Razor/state split without introducing a reusable ViewModel or persistence in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor` and `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`, and rerun the focused component tests.

### Real layer composition

- [ ] T027 [US1] Write composition RED cases resolving the specific Core/Infrastructure/Agents/Web chat services from a real Web host, relational contexts, existing shared-namespace provider dependencies, and scoped circuit-safe authorization in `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`.
- [ ] T028 [US1] Run `FullyQualifiedName~PreparedChatIntegrationTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by absent production registrations in `tests/Vulgata.Tests/TestResults/story-11-5-red/chat-composition.txt`.
- [ ] T029 [US1] Register only the specific retriever, currentness validator, extractor, Chat resolver/dispatcher/validator, and coordinator lifetimes in `src/dotnet/Vulgata.Web/Program.cs`, preserving default Data Protection and existing Worker/Scan composition.
- [ ] T030 [US1] Run `FullyQualifiedName~PreparedChatIntegrationTests` GREEN and rerun `FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatCandidateExtractorTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatPageTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

**Checkpoint**: US1 is demonstrable in both modes over an authorized prepared current scope using one bounded retrieval and one selection-only provider call.

---

## Phase 4: User Story 2 - Verify Every Material Claim Safely (Priority: P1)

**Goal**: Every rendered Material Claim is bound to current authorized persisted support, with Business/IT citation rules and whole-answer fail-closed behavior for every invalid or revoked condition.

**Independent Test**: Exercise fully authorized, partially authorized, post-retrieval revoked, superseded, Anchor-loss, relationship-loss, malformed, foreign, contradictory-decoy, support-swapped, timeout, and cancellation cases; valid answers have complete server-derived citations and every invalid case returns one bounded Chinese state with zero claim/citation/restricted detail.

### Post-provider database currentness

- [ ] T031 [P] [US2] Write relational and barrier-controlled RED cases for one serializable `ValidateCurrentAsync` read proving exact Logical Document current pointers, non-superseded Document Versions, Stable Section span offsets/text SHA-256, fixed safe same-version positive-range Source Anchors, required directed current `REFERENCES`/`GENERATED_FROM` Edges, exact evidence profile/direction, both authorized endpoints, and whole-answer rejection after successor, Anchor-loss, or relationship-loss mutation strictly between retrieval/provider dispatch and validation in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`.
- [ ] T032 [US2] Run `FullyQualifiedName~ChatEvidenceCurrentnessTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent serializable currentness implementation in `tests/Vulgata.Tests/TestResults/story-11-5-red/evidence-currentness.txt`.
- [ ] T033 [US2] Implement the bounded provider-independent `Current|MissingOrStale` predicate in one serializable EF read with live `RepositoryAccessScope`, no partial outcome, no test fault-injection hook, and no transaction across provider latency in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`.
- [ ] T034 [US2] Run `FullyQualifiedName~ChatEvidenceCurrentnessTests` GREEN, verify the barriers prove committed mutation before validation and zero claims/citations, refactor query code in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`, and rerun retrieval/currentness focused tests.

### Closed selection-only validation

- [ ] T035 [P] [US2] Write adversarial RED cases for exact closed JSON, unknown/missing/duplicate properties, wrong schema/request/mode/fingerprint, non-contiguous or out-of-bound order, duplicate/foreign/unrelated/contradictory-decoy/out-of-allowlist handles, swapped support, changed retained span/hash/signature, restricted evidence, Business locator text, and all-or-nothing materialization in `tests/Vulgata.Tests/ChatAgentResultValidatorTests.cs`.
- [ ] T036 [US2] Run `FullyQualifiedName~ChatAgentResultValidatorTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent whole-result validator in `tests/Vulgata.Tests/TestResults/story-11-5-red/result-validator.txt`.
- [ ] T037 [US2] Implement strict selection-only deserialization, retained-memory hash/support recomputation, Business safe-text revalidation, and server-owned Material Claim/fixed-support mapping with no repair or partial acceptance in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentResultValidator.cs`.
- [ ] T038 [US2] Run `FullyQualifiedName~ChatAgentResultValidatorTests` GREEN, inspect that provider output cannot author prose/citations/locators/diagnostics, refactor within `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentResultValidator.cs`, and rerun dispatcher/validator focused tests.

### Security ordering and atomic citation materialization

- [ ] T039 [US2] Extend coordinator RED cases to assert the exact order selection validation -> one Infrastructure currentness validation -> immediate final authorization -> fixed citation mapping -> atomic publication, with no unrelated asynchronous work, plus missing-equivalent behavior for precheck failure, live retrieval/currentness denial, final revocation, stale/malformed/unsupported/provider failure/timeout/cancellation, and zero restricted identity/path/source/relationship/raw diagnostic leakage in `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`.
- [ ] T040 [US2] Run `FullyQualifiedName~PreparedChatCoordinatorTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED for currentness/final-authorization ordering and fail-closed publication in `tests/Vulgata.Tests/TestResults/story-11-5-red/coordinator-security.txt`.
- [ ] T041 [US2] Complete the exact ten-step Web composition, build `ChatCurrentnessValidationRequest` only from retained server state, call currentness once, immediately reauthorize the same two Repositories, derive every Document Citation from fixed authorized Anchors, and publish only a complete `GroundedAnswer` in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`.
- [ ] T042 [US2] Run `FullyQualifiedName~PreparedChatCoordinatorTests` GREEN, refactor without widening failure distinctions in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, and rerun coordinator plus barrier-controlled currentness tests.

### Mode-specific citation rendering and leak prevention

- [ ] T043 [US2] Extend component RED cases so every claim has a Chinese Document citation, Business narrative has zero inline locators and expandable authorized Repository-relative positive line ranges, IT may show the same server-owned locator inline, and every invalid/revoked state clears all prior claims/citations and exposes no forbidden detail in `tests/Vulgata.Tests/ChatPageTests.cs`.
- [ ] T044 [US2] Run `FullyQualifiedName~ChatPageTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED for citation detail, mode presentation, and stale-answer clearing in `tests/Vulgata.Tests/TestResults/story-11-5-red/chat-citations.txt`.
- [ ] T045 [US2] Implement complete mode-specific claim/citation rendering and bounded Chinese fail-closed states in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, with only presentation/state support in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` and `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.css`.
- [ ] T046 [US2] Run `FullyQualifiedName~ChatPageTests` GREEN and rerun `FullyQualifiedName~ChatEvidenceCurrentnessTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~PreparedChatIntegrationTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

**Checkpoint**: US2 independently proves complete current claim-to-evidence coverage and zero partial or restricted rendering across every accepted invalid condition.

---

## Phase 5: User Story 3 - Prove the Prepared Release Path (Priority: P1)

**Goal**: Prove the accepted two-Repository flow from one unique clean PostgreSQL 17 database through the one accepted runtime image, real Identity, interactive Blazor, cited chat, negative authorization, and verified cleanup.

**Independent Test**: Run only `DemoChatReleaseGatePostgreSqlTests` from a clean candidate; require accepted full refs/commits, production-service provisioning, profiling -> Pass 1 -> exact Cross-Repository Resolution -> Pass 2 -> retrieval -> selection -> currentness -> final authorization -> one cited answer, then the single source-labelled Docker build, curl health, real Playwright circuit positive/negative flows, and finally database/resource absence even after failure.

### Shared Data Protection runtime branch

- [ ] T047 [P] [US3] Extend production-composition RED cases for the optional all-or-nothing `DataProtection:KeyRingPath` plus `DataProtection:ApplicationName` branch, identical provisioner/runtime decryption, unchanged defaults when either value is absent, and no key/plaintext evidence in `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`.
- [ ] T048 [US3] Run the Data Protection cases under `FullyQualifiedName~PreparedChatIntegrationTests` and record the observed RED against unconditional default Data Protection in `tests/Vulgata.Tests/TestResults/story-11-5-red/data-protection-composition.txt`.
- [ ] T049 [US3] Implement the optional paired key-ring path/application discriminator configuration while preserving default deployments in `src/dotnet/Vulgata.Web/Program.cs`.
- [ ] T050 [US3] Run `FullyQualifiedName~PreparedChatIntegrationTests` GREEN, refactor the configuration branch in `src/dotnet/Vulgata.Web/Program.cs`, and rerun `FullyQualifiedName~SecretEncryptionServiceTests|FullyQualifiedName~WorkerAgentProductionRegistrationTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

### Sole clean-database, provider, image, browser, and cleanup harness

- [ ] T051 [US3] Write the release-harness RED entry test for cryptographically random `vulgata_story115_` database ownership, rejected unsafe names, absent/create/migrate/keep-live/drop/catalog-absence lifecycle, accepted `mbs_account` and `mbs_channel` refs/full commits, product-service-only provisioning, exact predecessor path, inaccessible-scope proof, one source-bound image, label inspection, curl health, real Identity/interactive-circuit controls, revocation, primary-plus-cleanup failure retention, sanitized evidence, and nonce-only cleanup in `tests/Vulgata.Tests/DemoChatReleaseGatePostgreSqlTests.cs`.
- [ ] T052 [US3] Run `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent release harness, provider fixture, Playwright dependency, and executable final path in `tests/Vulgata.Tests/TestResults/story-11-5-red/release-harness.txt`.
- [ ] T053 [US3] Pin test-only `Microsoft.Playwright` 1.61.0 in `Directory.Packages.props` and reference it in `tests/Vulgata.Tests/Vulgata.Tests.csproj`; implement the sole resource owner and sanitized `tests/Vulgata.Tests/TestResults/story-11-5-release-evidence.json` output in `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs`, and implement the isolated stdlib Worker/Chat selection fixture in `tests/Vulgata.Tests/ReleaseGate/loopback-openai-provider.py` without changing `docker/Dockerfile` or provisioning through fixture SQL/`DbSet`.
- [ ] T054 [US3] Run fast harness contract/lifecycle cases under `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` GREEN without performing a second candidate-image build, refactor bounded polling/port/resource cleanup in `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs`, and rerun those fast cases.

**Checkpoint**: US3 has one executable harness that owns the live database through accepted-image browser proof and always verifies exact cleanup.

---

## Phase 6: Final Verification, Security, and Evidence

**Purpose**: Apply every retained expedited-MVP gate to one unchanged clean candidate; this phase performs no feature expansion and builds the final runtime image exactly once.

- [ ] T055 Run all Story 11.5 focused GREEN filters from `specs/008-demo-chat-release-gate/quickstart.md` against `tests/Vulgata.Tests/Vulgata.Tests.csproj`, confirm no unresolved RED, and retain raw output only for failures or provider/runtime/security contracts under `tests/Vulgata.Tests/TestResults/story-11-5-focused/`.
- [ ] T056 Run predecessor lifecycle filters `KnowledgeStoreMigrationTests|SequentialScanRunnerMigrationTests|ExactCrossRepositoryMigrationTests` and `dotnet ef migrations has-pending-model-changes` for `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj` with startup `src/dotnet/Vulgata.Web/Vulgata.Web.csproj`; require no migration or `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs` delta, and return to correct-course/plan review instead of generating a migration if a model delta appears.
- [ ] T057 Run authorization/security/static boundary checks over `src/dotnet/Vulgata.Core/`, `src/dotnet/Vulgata.Infrastructure/`, `src/dotnet/Vulgata.Agents/`, `src/dotnet/Vulgata.Web/`, `docker/Dockerfile`, and `tests/Vulgata.Tests/` for EF leakage from Infrastructure, generic repositories, automatic/all-access scope, file upload, `index.md` knowledge, retry/failover, Scan/matching changes, durable chat state, graph/dashboard/SignalR expansion, credentials, absolute paths, raw diagnostics, fixture SQL/`DbSet` provisioning, placeholder markers, and deliberately failing tests.
- [ ] T058 Restore `tests/Vulgata.Tests/Vulgata.Tests.csproj`, build `src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and `tests/Vulgata.Tests/Vulgata.Tests.csproj` in Release with analyzers, then install the matching Chromium only from `tests/Vulgata.Tests/bin/Release/net10.0/playwright.ps1` as required by `specs/008-demo-chat-release-gate/quickstart.md`.
- [ ] T059 Run the complete applicable regression suite from `tests/Vulgata.Tests/Vulgata.Tests.csproj`, including PostgreSQL-tagged groups, against the same candidate; preserve concise command/exit/boundary/timestamp results and raw output only for failures in `tests/Vulgata.Tests/TestResults/story-11-5-regression/`.
- [ ] T060 From the unchanged clean candidate, run `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` once in Release so `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs` performs the unique PostgreSQL 17 two-Repository predecessor path, final `docker/Dockerfile` build with exact commit/tree/production-manifest labels, health polling, shared Data Protection, real Identity/Playwright positive cited answer, negative user plus post-selection revocation, and `finally` cleanup; require `tests/Vulgata.Tests/TestResults/story-11-5-release-evidence.json` to contain only the sanitized contract fields.
- [ ] T061 Root/orchestrator verifies database catalog absence and removal of every nonce-owned container, volume, network, env file, and key directory recorded by `tests/Vulgata.Tests/TestResults/story-11-5-release-evidence.json`, confirms truthful limitations from `specs/008-demo-chat-release-gate/contracts/release-verification-contract.md`, pins the clean candidate for the independent combined quality review, and rejects completion on any primary, cleanup, security, migration, regression, Docker, health, Identity, circuit, citation, or authorization failure.

**Checkpoint**: One unchanged candidate has complete focused, migration, security, build, regression, clean PostgreSQL, accepted-image Docker, Identity/Blazor, authorization-negative, citation, and cleanup evidence and is ready for the retained combined quality-review and workflow verification gates.

---

## Requirement and Contract Coverage

| Authority | Executable task coverage |
|---|---|
| FR-001; FR-002; FR-003; SC-005 | T019-T030: authenticated Chinese UI, explicit mode/System/exact pair, and negative absence of automatic scope/file upload |
| FR-004; FR-011; SC-001 | T007-T014, T031-T034: bounded current database evidence, exact directed Edge/profile facts, deterministic span support, and no `index.md` knowledge |
| FR-005; FR-006; FR-017; SC-004 | T007-T010, T031-T034, T039-T046, T051-T061: live both-endpoint checks, final reauthorization, missing-equivalent behavior, and authorization-negative release proof |
| FR-007; FR-008; FR-009; FR-010; SC-002; SC-003 | T011-T014, T035-T046: exact mode-eligible persisted claims, complete server-owned Document citations, Business locator hiding/expansion, and IT locator allowance |
| FR-012; FR-013; FR-014 | T015-T018, T031-T046: whole-result validation, post-provider currentness, atomic publication, cancellation/timeout/provider/malformed fail-closed behavior |
| FR-015; FR-016; SC-006 | T051-T054, T060: accepted revisions and unique clean PostgreSQL path through profiling, Pass 1, exact Cross-Repository Resolution, Pass 2, retrieval, currentness, final authorization, and one cited answer |
| FR-018; SC-007 | T055-T060: focused/security/migration/build/regression, exactly one source-bound Docker image, curl health, real Identity, and interactive Blazor proof |
| FR-019; SC-008 | T051-T054, T060-T061: unconditional exact-database drop, catalog absence, nonce-owned cleanup, and cleanup-failure rejection |
| FR-020 | T003-T006 and every story RED/GREEN quartet, then T055-T057: transient observed RED only and zero failing RED in the candidate |
| FR-021; SC-009 | T053, T060-T061: sanitized release evidence and truthful separate post-demo limitations |
| FR-022 | T057 and T061: negative boundary for Scan/matching/retry/history/graph/dashboard/SignalR and deferred Epic 5-10 behavior |
| Core and evidence request/packet contracts | T003-T014 |
| Provider resolution, embedded prompt, and closed JSON contracts | T015-T018, T035-T038 |
| Post-provider currentness and Web ordering contracts | T031-T042 |
| Render contract | T023-T026, T043-T046 |
| Release verification contract | T047-T061 |
| No-migration decision | T056-T057; any unexpected model delta stops for correct-course/plan review |

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** has no dependency and must confirm the pinned inputs before any RED.
- **Phase 2 (Foundational)** depends on Phase 1 and blocks every user story.
- **US1 / Phase 3** depends on Phase 2 and supplies the minimal prepared chat slice.
- **US2 / Phase 4** depends on the US1 retrieval/candidate/coordinator seams, then independently proves currentness, complete citations, and fail-closed security.
- **US3 / Phase 5** depends on US1 and US2 because its vertical slice must exercise their accepted behavior through production services and the real UI.
- **Phase 6 (Final verification)** depends on all selected story tasks and must run sequentially on one unchanged candidate; T060 is the only final image build and T061 always verifies cleanup.

### User story dependency graph

```text
Setup -> Foundational -> US1 (prepared chat MVP)
                              |
                              v
                    US2 (current citations/security)
                              |
                              v
                    US3 (clean release path)
                              |
                              v
                       Final verification
```

US1 remains independently testable with current authorized prepared evidence and controlled success fakes for the later security seams. US2 is independently testable at its contract boundary with relational/barrier fixtures but integrates the US1 types and coordinator. US3 is inherently the final vertical integration of both earlier stories and the accepted predecessors.

### Within every production group

1. Write the named focused test in the named test file.
2. Run only its named filter and observe the stated product-meaningful RED.
3. Make the bounded production change in the named ownership paths.
4. Run focused GREEN, refactor without behavior expansion, and rerun the focused regression set.
5. Remove no valid regression and never carry a deliberately failing test to the next checkpoint.

---

## Parallel Opportunities

- After Phase 2, T007 retrieval tests, T011 candidate-extraction tests, and T015 dispatcher tests may be authored concurrently because they initially touch separate test files and distinct production owners; each RED observation and GREEN remains paired with its own group.
- After US1, T031 currentness tests and T035 selection-validator tests may be authored concurrently because Infrastructure currentness and Agents selection validation do not share files or database transactions.
- After US2, T047 Data Protection composition tests and T051 release-harness tests may be authored concurrently; production work remains ordered so the harness consumes the accepted composition.
- No task that edits `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, `src/dotnet/Vulgata.Web/Program.cs`, `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`, `tests/Vulgata.Tests/ChatPageTests.cs`, or `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` may overlap another task editing the same file.
- T055-T061 are intentionally sequential. The unique database, candidate identity, final image, browser proof, and cleanup evidence must not be split across owners.

### Parallel example: US1

```text
T007: tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs
T011: tests/Vulgata.Tests/ChatCandidateExtractorTests.cs
T015: tests/Vulgata.Tests/ChatAgentDispatcherTests.cs
```

### Parallel example: US2

```text
T031: tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs
T035: tests/Vulgata.Tests/ChatAgentResultValidatorTests.cs
```

---

## Implementation Strategy

### MVP first

1. Complete Setup and Foundational contracts.
2. Complete US1 through T030.
3. Validate US1 independently in Business and IT modes over the explicit prepared scope.
4. Treat this as the smallest functional MVP increment, not as a releasable Story 11.5 candidate; citations/currentness security and the release gate remain mandatory.

### Incremental delivery

1. **US1** adds explicit prepared-scope Chinese chat with bounded current evidence and one selection-only call.
2. **US2** adds deterministic post-provider database currentness, complete server-owned citations, final reauthorization, and whole-answer fail-closed behavior.
3. **US3** adds the optional shared Data Protection configuration and sole clean PostgreSQL/Docker/Identity/interactive-browser release harness.
4. **Final verification** applies all retained expedited-MVP gates to one source-bound candidate and hands that boundary to independent combined quality review.

### Stop conditions

- Stop on frozen input or source-boundary mismatch, missing planned-change authorization, an authority conflict, or an implementation need outside the accepted paths.
- Stop and return to correct-course/plan review if durable state, an EF model delta/migration, a new provider-visible tool field, Scan strategy, Cross-Repository matching behavior, retry/failover, or a PRD change becomes necessary.
- Stop acceptance on any unresolved RED, missing/partial citation, restricted disclosure, inferred graph truth, dirty/mismatched image label, unavailable prerequisite, failed cleanup, or database residue.

## Notes

- `.specify/extensions.yml` contains no `hooks.before_tasks` or `hooks.after_tasks`; no task hook applies.
- The expedited-MVP exception omits separate analysis and acceptance-review gates but retains observed transient TDD, one independent combined quality review, security, migration, regression, clean-database two-Repository E2E, final Docker, verification, and cleanup.
- `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs` is the sole authority for disposable database and nonce-owned Docker/browser resources; root/orchestrator alone owns final candidate pinning, lifecycle advancement, staging, and commit.
