# Tasks: Demo Chat, Citations, and Release Gate

**Input**: Accepted design documents from `specs/008-demo-chat-release-gate/`

**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/chat-agent-contract.md`, `contracts/release-verification-contract.md`, the user-approved `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-09-story-11-5-containerized-provisioning.md`, accepted `reviews/plan-review/plan-review-iteration-11.json`, and the durable Option A plan-to-tasks authorization recorded at `2026-08-10T01:12:29Z` in `workflow-state.json`

**Pinned boundary**: Plan-review base commit `d96eb66525b40dd1a3320aa5c0557b82de87827f`; accepted plan-review iteration 11; reviewed dirty-boundary digest `5f5135fb8416f84a8e658de7b07f1dbf7a0a9c0088fa19df065dc34ac85eab68`; plan-review artifact SHA-256 `5e1fc6f71a6a8a0bfd1eb19b144e6645abc103945dd45589f28816e2a1763e76`

**Current accepted design hashes (iteration 11)**: `spec.md` `8af86b17497f9c724f27970c3e80f288b8841475c2b1c1694aaff814b4b40ae0`; `plan.md` `56375f1816dfb3844c71d75d4d39b89cda6f4d75bd9861c6a989671d84b2df3f`; `research.md` `6f026645a715a9dc7980f43496ac2f2951143f8530cbe940cdba698d7f902393`; `data-model.md` `95bce74ab7ac07948a0ea66287a7af61009edc3da20857ed59d28369bf5fd48d`; `quickstart.md` `e2cb9e3630ee943d79d83a4c8ec9fae5629fa63e29b4ab1cc2160c9b9c963912`; chat contract `13d1d1ce8c96333cabd3c054f8c98648639b411db4bc6027ee983748ce276b19`; release contract `d85bdb91d89581dc74c47e9b2b90fed78563c13fff850b8a3c3aaaf7ec374e41`; Option A proposal `7b049c783a7b6a7b7d08628d84ef957435e8cc0eb112f938a0106959454900ba`

**Correction boundary**: T001-T063 remain completed historical work with their recorded semantics and truthful checkboxes. Plan-review iteration 11 plus the distinct durable Option A planned-change authorization govern only the regenerated unchecked T064-T073 boundary. The prior Chat, runtime Pass 2, and deterministic ordinary Pass 1 approvals are not repeated. Under `expedited-mvp`, root mechanical requirement/task/test-first/security/migration coverage validation precedes implementation; one fresh combined acceptance-and-quality review over the pinned candidate precedes the sole live task. `spec-review`, active `speckit-analyze`, and the separate `acceptance-review` remain omitted at iteration zero; no verdict or evidence is fabricated.

**Tests**: Mandatory observed RED -> GREEN -> REFACTOR. Each production group below starts with a test-writing task, then a distinct task that runs the focused filter and records the expected failure, then implementation, then focused GREEN/regression. RED is transient and no deliberately failing test may enter the accepted candidate.

**Ownership boundary**: Core owns provider-independent contracts only; Infrastructure owns EF reads; Agents owns candidate selection, provider resolution, embedded prompts, dispatch, and closed result validation; Web owns user authorization, call ordering, transient UI, citations, and composition; `Story115ReleaseHarness` solely owns final clean-database/Docker/browser resources and evidence. No generic repository, EF surface in Core, migration/model change, Scan or matching expansion, retry/failover, durable chat history, or deferred Epic 5-10 behavior is permitted.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run concurrently because it changes different files and has no dependency on an incomplete task.
- **[Story]**: Maps the task to `[US1]`, `[US2]`, or `[US3]` from `spec.md`.
- Every checklist item names exact repository-relative paths.

---

## Phase 1: Setup and Boundary Confirmation

**Purpose**: Confirm the accepted inputs and establish a reproducible green baseline without changing implementation.

- [x] T001 Verify `git rev-parse HEAD`, the six frozen SHA-256 values, accepted iteration-4 verdict, and recorded `user: Approve` authorization against `specs/008-demo-chat-release-gate/plan.md`, `specs/008-demo-chat-release-gate/research.md`, `specs/008-demo-chat-release-gate/data-model.md`, `specs/008-demo-chat-release-gate/quickstart.md`, `specs/008-demo-chat-release-gate/contracts/chat-agent-contract.md`, `specs/008-demo-chat-release-gate/contracts/release-verification-contract.md`, `specs/008-demo-chat-release-gate/reviews/plan-review/plan-review-iteration-4.json`, and `specs/008-demo-chat-release-gate/workflow-state.json`; stop on any mismatch.
- [x] T002 Restore and run the pre-change Web build plus full test baseline for `src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and `tests/Vulgata.Tests/Vulgata.Tests.csproj`, recording command, exit code, source boundary, timestamp, and concise result under `tests/Vulgata.Tests/TestResults/story-11-5-baseline/`.

**Checkpoint**: The accepted boundary is unchanged and the repository baseline is known before any RED is introduced.

---

## Phase 2: Foundational Provider-Independent Contracts (Blocking)

**Purpose**: Establish the closed Core types and service boundaries required by every story without EF, provider SDK, or Web dependencies.

**Independent validation**: Contract tests prove closed modes/outcomes, normalized 1-2,000-character questions, exactly two canonical Repository IDs, packet and selection maxima, deterministic fingerprints/support signatures, positive safe Source Anchors, fixed claim-to-citation mappings, and provider-independent retrieval/currentness/dispatcher interfaces.

- [x] T003 Write the foundational contract RED for valid and invalid `ChatQuestionRequest`, `EvidencePacket`, `CandidateClaim`, `ChatCurrentnessValidationRequest`, `ChatSynthesisResult`, `MaterialClaim`, `DocumentCitation`, `GroundedAnswer`, closed outcomes, canonical ordering, hashes, bounds, and failure codes in `tests/Vulgata.Tests/ChatContractTests.cs`.
- [x] T004 Run `FullyQualifiedName~ChatContractTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent Core chat contracts and interfaces in `tests/Vulgata.Tests/TestResults/story-11-5-red/core-contracts.txt`.
- [x] T005 Implement provider-independent records, enums, invariant validation, canonical ordering/fingerprinting, and bounded failure results in `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`, plus the specific interfaces in `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceRetriever.cs`, `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceCurrentnessValidator.cs`, and `src/dotnet/Vulgata.Core/DomainServices/IChatAgentDispatcher.cs`, with no EF, SDK, `DbSet`, `IQueryable`, or generic repository exposure.
- [x] T006 Run `FullyQualifiedName~ChatContractTests` GREEN, refactor duplicate invariant/canonicalization code only within `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`, rerun the focused tests, and run the Core project boundary/analyzer build for `src/dotnet/Vulgata.Core/Vulgata.Core.csproj`.

**Checkpoint**: All later work consumes a stable, provider-independent, bounded Core contract.

---

## Phase 3: User Story 1 - Ask About the Prepared Flow (Priority: P1) 🎯 MVP

**Goal**: An authenticated user explicitly selects Business or IT Mode, one prepared System, and exactly two authorized prepared Repositories and receives a bounded Chinese answer grounded only in the current persisted prepared flow.

**Independent Test**: With current prepared evidence and authorized scope, run both modes through the coordinator and interactive component; verify one bounded retrieval, mode-eligible exact persisted spans, one selection-only synthesis call, a complete Chinese answer, explicit scope controls, no automatic scope/file upload, and no transaction spanning provider latency. Currentness and adversarial citation security are strengthened independently in US2.

### Prepared evidence retrieval

- [x] T007 [P] [US1] Write relational retrieval RED cases for current Document Versions, bounded canonically ordered Sections/Source Anchors/flow/provenance, the exact accepted-profile directed cross-Repository `REFERENCES` Edge and `GENERATED_FROM`, both-endpoint live authorization, deterministic handles/fingerprint, hard maxima, and missing-equivalent stale/restricted/out-of-scope rejection in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`.
- [x] T008 [US1] Run `FullyQualifiedName~ChatEvidenceRetrievalTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent bounded relational retriever in `tests/Vulgata.Tests/TestResults/story-11-5-red/evidence-retrieval.txt`.
- [x] T009 [US1] Implement the single current, authorized, read-only Evidence Packet query with live `RepositoryAccessScope`, no `index.md`, no partial packet, and no public EF/query surface in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`.
- [x] T010 [US1] Run `FullyQualifiedName~ChatEvidenceRetrievalTests` GREEN, refactor query/canonicalization code within `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`, rerun the focused tests, and verify no transaction remains open when `RetrieveAsync` returns.

### Exact candidate extraction

- [x] T011 [P] [US1] Write candidate-extraction RED vectors covering exact Unicode paragraph/list-item offsets, canonical order, BL `Overview`/`ProcessStep` eligibility, CL `Overview`/`ProcessStep`/`TechnicalDetail`/`Evidence` eligibility, safe-text/no-code-locator filtering, fixed Anchor/relationship support signatures, and exclusion of fenced code, tables, raw HTML, links, restricted references, missing support, and over-bound spans in `tests/Vulgata.Tests/ChatCandidateExtractorTests.cs`.
- [x] T012 [US1] Run `FullyQualifiedName~ChatCandidateExtractorTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent exact persisted-span extractor in `tests/Vulgata.Tests/TestResults/story-11-5-red/candidate-extractor.txt`.
- [x] T013 [US1] Implement model-independent exact persisted-span extraction and immutable SHA-256 support signatures, without paraphrase, normalization, concatenation, locator invention, or EF access, in `src/dotnet/Vulgata.Agents/ChatExecution/ChatCandidateExtractor.cs`.
- [x] T014 [US1] Run `FullyQualifiedName~ChatCandidateExtractorTests` GREEN, refactor parsing while preserving byte-for-byte offsets in `src/dotnet/Vulgata.Agents/ChatExecution/ChatCandidateExtractor.cs`, and rerun the focused test set.

### Chat provider, prompts, and one-call dispatch

- [x] T015 [P] [US1] Write dispatcher RED cases for exact System `AgentType.Chat` override precedence, ordered global Chat default, Chat Completions/endpoint/model/encrypted-key validation, dispatch-time decryption, one call, 30-second linked timeout, cancellation, provider failure, no Worker fallback, no retry/failover, prompt resource mode mapping, and selection-only request content in `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs`.
- [x] T016 [US1] Run `FullyQualifiedName~ChatAgentDispatcherTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by absent Chat resolution/prompts/dispatch in `tests/Vulgata.Tests/TestResults/story-11-5-red/chat-dispatcher.txt`.
- [x] T017 [US1] Implement `AgentType.Chat` assignment resolution in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentProviderResolver.cs`, embedded-resource loading in `src/dotnet/Vulgata.Agents/ChatExecution/ChatPromptCatalog.cs`, one OpenAI-compatible selection-only call in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs`, versioned prompts in `src/dotnet/Vulgata.Agents/Prompts/chat-business.v1.md` and `src/dotnet/Vulgata.Agents/Prompts/chat-it.v1.md`, and exact logical resource names in `src/dotnet/Vulgata.Agents/Vulgata.Agents.csproj`.
- [x] T018 [US1] Run `FullyQualifiedName~ChatAgentDispatcherTests` GREEN, inspect captured provider requests for only mode-eligible handles/exact text and no credentials/locators/internal IDs, refactor within `src/dotnet/Vulgata.Agents/ChatExecution/`, and rerun the focused tests.

### Prepared chat coordinator happy path

- [x] T019 [US1] Write coordinator RED cases for authorized prepared-scope listing, explicit mode/System/exactly-two distinct Repository validation, normalized question bounds, same-System preauthorization, one retrieval/extraction/dispatch call, server-owned exact claims/citations, cancellation, mode/scope reset, bounded Chinese failures, and no prior/partial answer publication in `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`.
- [x] T020 [US1] Run `FullyQualifiedName~PreparedChatCoordinatorTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent coordinator in `tests/Vulgata.Tests/TestResults/story-11-5-red/prepared-chat-coordinator.txt`.
- [x] T021 [US1] Implement `ListScopesAsync` and the bounded transient `AskAsync` orchestration with `AuthenticationStateProvider`/`UserContext`, explicit scope, server-retained claims, and fixed Chinese outcomes in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`; use faked successful currentness/final authorization at this increment boundary without weakening the production ordering seam completed in US2.
- [x] T022 [US1] Run `FullyQualifiedName~PreparedChatCoordinatorTests` GREEN, refactor orchestration without adding asynchronous work between security stages in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, and rerun retrieval, dispatcher, and coordinator focused tests from `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

### Chinese prepared chat UI

- [x] T023 [US1] Write bUnit RED cases for `[Authorize]`, Business/IT explicit selection, prepared System and exactly-two Repository selectors, question validation, Chinese `LoadState` transitions, mode-locked Library Oak theme, keyboard send/cancel, clearing on mode/scope/new request/failure, atomic answer display, and absence of upload/automatic-scope/history/graph/live-notification controls in `tests/Vulgata.Tests/ChatPageTests.cs`.
- [x] T024 [US1] Run `FullyQualifiedName~ChatPageTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED against the placeholder page in `tests/Vulgata.Tests/TestResults/story-11-5-red/chat-page.txt`.
- [x] T025 [US1] Replace the authenticated placeholder with the Simplified-Chinese Fluent UI flow in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, implement transient circuit state, `LoadState`, cancellation, and identity acquisition in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`, and apply only existing Library Oak tokens/classes in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.css` and `src/dotnet/Vulgata.Web/wwwroot/css/brand.css`.
- [x] T026 [US1] Run `FullyQualifiedName~ChatPageTests` GREEN, refactor the Razor/state split without introducing a reusable ViewModel or persistence in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor` and `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`, and rerun the focused component tests.

### Real layer composition

- [x] T027 [US1] Write composition RED cases resolving the specific Core/Infrastructure/Agents/Web chat services from a real Web host, relational contexts, existing shared-namespace provider dependencies, and scoped circuit-safe authorization in `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`.
- [x] T028 [US1] Run `FullyQualifiedName~PreparedChatIntegrationTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by absent production registrations in `tests/Vulgata.Tests/TestResults/story-11-5-red/chat-composition.txt`.
- [x] T029 [US1] Register only the specific retriever, currentness validator, extractor, Chat resolver/dispatcher/validator, and coordinator lifetimes in `src/dotnet/Vulgata.Web/Program.cs`, preserving default Data Protection and existing Worker/Scan composition.
- [x] T030 [US1] Run `FullyQualifiedName~PreparedChatIntegrationTests` GREEN and rerun `FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatCandidateExtractorTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatPageTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

**Checkpoint**: US1 is demonstrable in both modes over an authorized prepared current scope using one bounded retrieval and one selection-only provider call.

---

## Phase 4: User Story 2 - Verify Every Material Claim Safely (Priority: P1)

**Goal**: Every rendered Material Claim is bound to current authorized persisted support, with Business/IT citation rules and whole-answer fail-closed behavior for every invalid or revoked condition.

**Independent Test**: Exercise fully authorized, partially authorized, post-retrieval revoked, superseded, Anchor-loss, relationship-loss, malformed, foreign, contradictory-decoy, support-swapped, timeout, and cancellation cases; valid answers have complete server-derived citations and every invalid case returns one bounded Chinese state with zero claim/citation/restricted detail.

### Post-provider database currentness

- [x] T031 [P] [US2] Write relational and barrier-controlled RED cases for one serializable `ValidateCurrentAsync` read proving exact Logical Document current pointers, non-superseded Document Versions, Stable Section span offsets/text SHA-256, fixed safe same-version positive-range Source Anchors, required directed current `REFERENCES`/`GENERATED_FROM` Edges, exact evidence profile/direction, both authorized endpoints, and whole-answer rejection after successor, Anchor-loss, or relationship-loss mutation strictly between retrieval/provider dispatch and validation in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`.
- [x] T032 [US2] Run `FullyQualifiedName~ChatEvidenceCurrentnessTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent serializable currentness implementation in `tests/Vulgata.Tests/TestResults/story-11-5-red/evidence-currentness.txt`.
- [x] T033 [US2] Implement the bounded provider-independent `Current|MissingOrStale` predicate in one serializable EF read with live `RepositoryAccessScope`, no partial outcome, no test fault-injection hook, and no transaction across provider latency in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`.
- [x] T034 [US2] Run `FullyQualifiedName~ChatEvidenceCurrentnessTests` GREEN, verify the barriers prove committed mutation before validation and zero claims/citations, refactor query code in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`, and rerun retrieval/currentness focused tests.

### Closed selection-only validation

- [x] T035 [P] [US2] Write adversarial RED cases for exact closed JSON, unknown/missing/duplicate properties, wrong schema/request/mode/fingerprint, non-contiguous or out-of-bound order, duplicate/foreign/unrelated/contradictory-decoy/out-of-allowlist handles, swapped support, changed retained span/hash/signature, restricted evidence, Business locator text, and all-or-nothing materialization in `tests/Vulgata.Tests/ChatAgentResultValidatorTests.cs`.
- [x] T036 [US2] Run `FullyQualifiedName~ChatAgentResultValidatorTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent whole-result validator in `tests/Vulgata.Tests/TestResults/story-11-5-red/result-validator.txt`.
- [x] T037 [US2] Implement strict selection-only deserialization, retained-memory hash/support recomputation, Business safe-text revalidation, and server-owned Material Claim/fixed-support mapping with no repair or partial acceptance in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentResultValidator.cs`.
- [x] T038 [US2] Run `FullyQualifiedName~ChatAgentResultValidatorTests` GREEN, inspect that provider output cannot author prose/citations/locators/diagnostics, refactor within `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentResultValidator.cs`, and rerun dispatcher/validator focused tests.

### Security ordering and atomic citation materialization

- [x] T039 [US2] Extend coordinator RED cases to assert the exact order selection validation -> one Infrastructure currentness validation -> immediate final authorization -> fixed citation mapping -> atomic publication, with no unrelated asynchronous work, plus missing-equivalent behavior for precheck failure, live retrieval/currentness denial, final revocation, stale/malformed/unsupported/provider failure/timeout/cancellation, and zero restricted identity/path/source/relationship/raw diagnostic leakage in `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`.
- [x] T040 [US2] Run `FullyQualifiedName~PreparedChatCoordinatorTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED for currentness/final-authorization ordering and fail-closed publication in `tests/Vulgata.Tests/TestResults/story-11-5-red/coordinator-security.txt`.
- [x] T041 [US2] Complete the exact ten-step Web composition, build `ChatCurrentnessValidationRequest` only from retained server state, call currentness once, immediately reauthorize the same two Repositories, derive every Document Citation from fixed authorized Anchors, and publish only a complete `GroundedAnswer` in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`.
- [x] T042 [US2] Run `FullyQualifiedName~PreparedChatCoordinatorTests` GREEN, refactor without widening failure distinctions in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, and rerun coordinator plus barrier-controlled currentness tests.

### Mode-specific citation rendering and leak prevention

- [x] T043 [US2] Extend component RED cases so every claim has a Chinese Document citation, Business narrative has zero inline locators and expandable authorized Repository-relative positive line ranges, IT may show the same server-owned locator inline, and every invalid/revoked state clears all prior claims/citations and exposes no forbidden detail in `tests/Vulgata.Tests/ChatPageTests.cs`.
- [x] T044 [US2] Run `FullyQualifiedName~ChatPageTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED for citation detail, mode presentation, and stale-answer clearing in `tests/Vulgata.Tests/TestResults/story-11-5-red/chat-citations.txt`.
- [x] T045 [US2] Implement complete mode-specific claim/citation rendering and bounded Chinese fail-closed states in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, with only presentation/state support in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` and `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.css`.
- [x] T046 [US2] Run `FullyQualifiedName~ChatPageTests` GREEN and rerun `FullyQualifiedName~ChatEvidenceCurrentnessTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~PreparedChatIntegrationTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

**Checkpoint**: US2 independently proves complete current claim-to-evidence coverage and zero partial or restricted rendering across every accepted invalid condition.

---

## Phase 5: User Story 3 - Prove the Prepared Release Path (Priority: P1)

**Goal**: Prove the accepted two-Repository flow from one unique clean PostgreSQL 17 database through the one accepted runtime image, real Identity, interactive Blazor, cited chat, negative authorization, and verified cleanup.

**Independent Test**: First prove that every newly dispatched ordinary Pass 1 job uses the deterministic canonical identity for its actual Scan Execution/Scan Run/Repository/Code Unit/Profiled Revision boundary, and that the planner independently recomputes it and verifies the existing Document Version projection before binding current product-created identities into exactly one consumer-owned runtime flow. Require the canonical schema/Manifest/Flow/support identities, exact seven-slot Worker payload, and fail-closed behavior before dispatch or commit for every invalid identity, selector, or recomputation. Then run only `DemoChatReleaseGatePostgreSqlTests` from a clean candidate; require accepted full refs/commits, production-service provisioning, profiling -> deterministic ordinary Pass 1 dispatch -> exact Cross-Repository Resolution -> runtime prepared-flow binding -> Pass 2 -> retrieval -> selection -> currentness -> final authorization -> one cited answer, then the single source-labelled Docker build, curl health, real Playwright circuit positive/negative flows, and finally database/resource absence even after failure.

### Shared Data Protection runtime branch

- [x] T047 [P] [US3] Extend production-composition RED cases for the optional all-or-nothing `DataProtection:KeyRingPath` plus `DataProtection:ApplicationName` branch, identical provisioner/runtime decryption, unchanged defaults when either value is absent, and no key/plaintext evidence in `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`.
- [x] T048 [US3] Run the Data Protection cases under `FullyQualifiedName~PreparedChatIntegrationTests` and record the observed RED against unconditional default Data Protection in `tests/Vulgata.Tests/TestResults/story-11-5-red/data-protection-composition.txt`.
- [x] T049 [US3] Implement the optional paired key-ring path/application discriminator configuration while preserving default deployments in `src/dotnet/Vulgata.Web/Program.cs`.
- [x] T050 [US3] Run `FullyQualifiedName~PreparedChatIntegrationTests` GREEN, refactor the configuration branch in `src/dotnet/Vulgata.Web/Program.cs`, and rerun `FullyQualifiedName~SecretEncryptionServiceTests|FullyQualifiedName~WorkerAgentProductionRegistrationTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

### Deterministic ordinary Pass 1 identity, runtime prepared Pass 2 binding, and exact seven-slot dispatch

- [x] T051 [US3] Write the deterministic ordinary Pass 1 identity and runtime-binding RED in `tests/Vulgata.Tests/Pass1JobIdentityTests.cs`, `tests/Vulgata.Tests/PreparedDemoPass2FlowPlannerTests.cs`, and `tests/Vulgata.Tests/PreparedPass2RuntimeDispatchTests.cs`: independently hard-code the complete 142-byte `VULGATA-ORDINARY-PASS1-JOB` version-1 TLV envelope, digest `6b9362f7193b38d94d9cb2a21ecfe2fd5ffe6a3aaaa00bd8c787e8efccdc71da`, Job UUID `6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd`, and complete manifest/flow/support byte, UUIDv8-SHA256, and lowercase-fingerprint vectors; prove same-boundary repeat equality, mutation of each Scan Execution/Scan Run/Repository/Code Unit/full lowercase 40-or-64-hex Profiled Revision input, tuple-framing and Pass1/Manifest/Flow collision-domain separation, ordinary runner dispatch of the derived Job ID, planner-independent recomputation from current product records, and the existing `CreateStableId("pass1-document-version", jobId.ToString("D"))` Document Version projection; retain closed schema/TLV/RFC-network-order/big-endian/strict-UTF-8-NFC/raw-lowercase-hex rules, exact seven payload slots, consumer ownership, exactly-two-Repository support, every-used-version `GENERATED_FROM`, and the complete missing/duplicate/collision, legacy/random/substituted, projection-mismatch/drift, stale/unauthorized/wrong-System/profile/direction/ref/commit/Profiled-Revision, changed binding/relationship/member/Anchor/trace, malformed/noncanonical/reordered/unknown-version-or-tag/list, invalid-UTF-8, and mixed-endian fail-closed matrix with zero Pass 2 dispatches and zero generated Documents.
- [x] T052 [US3] Run `FullyQualifiedName~Pass1JobIdentityTests|FullyQualifiedName~PreparedDemoPass2FlowPlannerTests|FullyQualifiedName~PreparedPass2RuntimeDispatchTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed product-meaningful RED caused by absent deterministic ordinary Pass 1 derivation/dispatch, planner-independent historical Job ID and Document Version projection verification, runtime binding contract, canonical identity codec, and runner/committer recomputation in `tests/Vulgata.Tests/TestResults/story-11-5-red/runtime-pass1-pass2-binding.txt`; do not add production code until this failure is observed.
- [x] T053 [US3] Implement the domain-separated canonical ordinary Pass 1 Job ID plus server-owned runtime binding and schema/Manifest/Flow/support derivation in `src/dotnet/Vulgata.Core/DomainServices/PreparedPass2RuntimeBinding.cs` and `src/dotnet/Vulgata.Core/DomainServices/IPreparedDemoPass2FlowPlanner.cs`; update `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs` to derive the actual ordinary Pass 1 Job ID immediately before packet creation/dispatch without granting retry or duplicate-commit permission; implement the single authorized bounded relational read and independent Job ID plus `pass1-document-version` projection verification in `src/dotnet/Vulgata.Infrastructure/Data/PreparedDemoPass2FlowPlanner.cs`, runtime resolution/dispatch validation in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass2FlowResolver.cs`, current-support recomputation and provenance validation in `src/dotnet/Vulgata.Infrastructure/Data/WorkerAgentResultCommitter.cs`, and specific composition in `src/dotnet/Vulgata.Web/Program.cs`; derive identities only from accepted current product records, fail closed for legacy/random/colliding/substituted/drifted/noncanonical evidence, compare the raw support digest in fixed time before the unchanged seven-slot single Pass 2 dispatch and again before commit, retain `PreparedPass2FlowCatalog` only as a non-production fixture, and add no migration/backfill, EF model field, general discovery/clustering, scheduling, concurrency, retry/failover, or eighth Worker slot.
- [x] T054 [US3] Run `FullyQualifiedName~Pass1JobIdentityTests|FullyQualifiedName~PreparedDemoPass2FlowPlannerTests|FullyQualifiedName~PreparedPass2RuntimeDispatchTests|FullyQualifiedName~PreparedPass2FlowTests|FullyQualifiedName~SequentialScanRunnerDispatchTests|FullyQualifiedName~SequentialScanRunnerDocumentGenerationTests|FullyQualifiedName~WorkerAgentResultCommitterTests|FullyQualifiedName~WorkerAgentProductionRegistrationTests` GREEN from `tests/Vulgata.Tests/Vulgata.Tests.csproj`; independently inspect the Pass 1 142-byte/digest/UUID oracle, repeat/per-input mutation and collision/domain-separation results, planner recomputation/Document Version projection, manifest/flow/support vectors, and exact seven-slot production composition; refactor only the bounded identity/runtime-binding validation paths, rerun the same filter, and require no change under `src/dotnet/Vulgata.Infrastructure/Data/Migrations/` or `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`.

**Checkpoint**: Ordinary Pass 1 dispatch uses one deterministic actual Job ID per canonical execution boundary; the planner independently reproduces it and the existing Document Version projection before current authorized product-created identities bind to one deterministic consumer-owned flow with exactly two supporting Repositories. Runner and committer reject every legacy/random, collided, stale, ambiguous, substituted, drifted, or noncanonical variant before Pass 2 dispatch or persistence while the Worker contract remains exactly seven slots.

### Sole clean-database, provider, image, browser, and cleanup harness

- [x] T055 [US3] Write the release-harness RED entry test for cryptographically random `vulgata_story115_` database ownership, rejected unsafe names, absent/create/migrate/keep-live/drop/catalog-absence lifecycle, accepted `mbs_account` and `mbs_channel` refs/full commits, product-service-only provisioning, exact predecessor path including deterministic ordinary Pass 1 Job IDs, planner-independent identity/Document Version projection verification, runtime prepared-flow binding, and exact seven-slot Pass 2 dispatch, inaccessible-scope proof, one source-bound image, label inspection, curl health, real Identity/interactive-circuit controls, revocation, primary-plus-cleanup failure retention, sanitized evidence, and nonce-only cleanup in `tests/Vulgata.Tests/DemoChatReleaseGatePostgreSqlTests.cs`.
- [x] T056 [US3] Run `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed RED caused by the absent release harness, provider fixture, Playwright dependency, and executable final path in `tests/Vulgata.Tests/TestResults/story-11-5-red/release-harness.txt`.
- [x] T057 [US3] Pin test-only `Microsoft.Playwright` 1.61.0 in `Directory.Packages.props` and reference it in `tests/Vulgata.Tests/Vulgata.Tests.csproj`; implement the sole resource owner and sanitized `tests/Vulgata.Tests/TestResults/story-11-5-release-evidence.json` output in `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs`, and implement the isolated stdlib Worker/Chat selection fixture in `tests/Vulgata.Tests/ReleaseGate/loopback-openai-provider.py` without changing `docker/Dockerfile` or provisioning through fixture SQL/`DbSet`.
- [x] T058 [US3] Run fast harness contract/lifecycle cases under `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` GREEN without performing a second candidate-image build, refactor bounded polling/port/resource cleanup in `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs`, and rerun those fast cases.

**Checkpoint**: US3 has one executable harness that owns the live database through accepted-image browser proof and always verifies exact cleanup.

---

## Phase 6: Final Verification, Security, and Evidence

**Purpose**: Correct the invalid host-side provisioning topology under observed RED/GREEN, complete non-live verification and the expedited retained gates, then execute exactly one live release E2E and independent cleanup/completion verification against one unchanged clean candidate.

**Independent validation**: Before T064, the root/orchestrator must mechanically validate `specs/008-demo-chat-release-gate/spec.md`, the complete accepted plan set, both contracts, and this `tasks.md` for continuous T001-T073 IDs; truthful T001-T063 history; complete FR-001-FR-024 and SC-001-SC-011 mapping; observable RED before each implementation task; exact paths, story labels, dependencies, and independent criteria; authorization/missing-equivalent, secret/source/path sanitization, external PostgreSQL nonce-only ownership, read-only mounts, same-image/no-host-`cgc`/no-second-image, and exact-seven-slot security boundaries; predecessor migration lifecycle, no pending model changes, and zero migration/model-snapshot/`VulgataDbContext` delta; and zero unresolved placeholder. This root validation creates no `speckit-analyze` verdict. After T064-T071 are GREEN, root pins one clean candidate and one fresh `quality-reviewer` must return combined acceptance-and-quality `ACCEPT` over every included criterion, security boundary, migration check, regression risk, test, and known limitation before T072 may acquire a live resource.

- [x] T059 Run all Story 11.5 focused GREEN filters from `specs/008-demo-chat-release-gate/quickstart.md` against `tests/Vulgata.Tests/Vulgata.Tests.csproj`, confirm no unresolved RED, and retain raw output only for failures or provider/runtime/security contracts under `tests/Vulgata.Tests/TestResults/story-11-5-focused/`.
- [x] T060 Run predecessor lifecycle filters `KnowledgeStoreMigrationTests|SequentialScanRunnerMigrationTests|ExactCrossRepositoryMigrationTests` and `dotnet ef migrations has-pending-model-changes` for `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj` with startup `src/dotnet/Vulgata.Web/Vulgata.Web.csproj`; require no migration or `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs` delta, and return to correct-course/plan review instead of generating a migration if a model delta appears.
- [x] T061 Run authorization/security/static boundary checks over `src/dotnet/Vulgata.Core/`, `src/dotnet/Vulgata.Infrastructure/`, `src/dotnet/Vulgata.Agents/`, `src/dotnet/Vulgata.Web/`, `docker/Dockerfile`, and `tests/Vulgata.Tests/` for EF leakage from Infrastructure, generic repositories, automatic/all-access scope, file upload, `index.md` knowledge, retry/failover or duplicate-commit permission, general Scan discovery/clustering/scheduling/concurrency or matching changes, random/caller/planner-supplied ordinary Pass 1 Job IDs, legacy backfill, any eighth `Pass2BusinessLogicJobPayload` slot, caller/fixed-manifest runtime identity authority, durable chat state, graph/dashboard/SignalR expansion, credentials, absolute paths, raw diagnostics, fixture SQL/`DbSet` provisioning, placeholder markers, deliberately failing tests, and changes under `src/dotnet/Vulgata.Infrastructure/Data/Migrations/` or `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`.
- [x] T062 Restore `tests/Vulgata.Tests/Vulgata.Tests.csproj`, build `src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and `tests/Vulgata.Tests/Vulgata.Tests.csproj` in Release with analyzers, then install the matching Chromium only from `tests/Vulgata.Tests/bin/Release/net10.0/playwright.ps1` as required by `specs/008-demo-chat-release-gate/quickstart.md`.
- [x] T063 Run the complete applicable regression suite from `tests/Vulgata.Tests/Vulgata.Tests.csproj`, including PostgreSQL-tagged groups, against the same candidate; preserve concise command/exit/boundary/timestamp results and raw output only for failures in `tests/Vulgata.Tests/TestResults/story-11-5-regression/`.
- [x] T064 [US3] Extend `tests/Vulgata.Tests/DemoChatReleaseGatePostgreSqlTests.cs` with the Option A provisioner-contract RED: reject the old host-side profiling/Scan order and every host `cgc`/Python/pip fallback; require a prebuilt framework-dependent `tests/Vulgata.Tests/ReleaseGate/Provisioner/Vulgata.Story115.Provisioner.csproj` entry with exactly the unique mounted `Vulgata.Story115.Provisioner.dll`, `.deps.json`, and `.runtimeconfig.json`; reject any mounted/colliding `Vulgata.Core`, `Vulgata.Infrastructure`, `Vulgata.Shared`, `Vulgata.Agents`, or `Vulgata.Web` assembly; pin closed bounded input, atomic sanitized result, image-owned product assemblies/CodeGraphContext 0.5.1, real public product-service composition, and no fixture SQL, direct `DbSet`, reflection, fabricated identity, migration/model change, second image, or shipped provisioner artifact.
- [x] T065 [US3] Run the non-live Option A provisioner-contract cases under `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` from `tests/Vulgata.Tests/Vulgata.Tests.csproj` and record the observed product-meaningful RED caused by the absent container provisioner entry and in-image product composition in `tests/Vulgata.Tests/TestResults/story-11-5-red/option-a-provisioner.txt`; acquire no Docker, PostgreSQL, provider, browser, or Repository resource.
- [x] T066 [US3] Implement the minimal GREEN test-owned driver in `tests/Vulgata.Tests/ReleaseGate/Provisioner/Vulgata.Story115.Provisioner.csproj`, `tests/Vulgata.Tests/ReleaseGate/Provisioner/Program.cs`, and `tests/Vulgata.Tests/ReleaseGate/Provisioner/Story115Provisioner.cs`, plus only the reusable public composition seam required in `src/dotnet/Vulgata.Web/Program.cs`: read the permission-restricted closed input, use image `/app` product assemblies and public services to apply both migration sets and provision Identity/System/Repository/encrypted Worker+Chat providers, profile the two stable mounted Repository origins into nonce work with the image-owned hash-locked `cgc`, run deterministic ordinary Pass 1, exact Cross-Repository Resolution, runtime prepared-flow binding, and one unchanged exact seven-slot Pass 2 dispatch/commit, then atomically rename one closed size-bounded sanitized result; add no production command mode, Dockerfile/package/EF model/migration/snapshot change, fixture SQL, direct `DbSet`, reflection, fabricated identity, retry/failover, or general Scan behavior.
- [x] T067 [US3] Publish `tests/Vulgata.Tests/ReleaseGate/Provisioner/Vulgata.Story115.Provisioner.csproj` in Release to `tests/Vulgata.Tests/bin/Release/net10.0/story115-provisioner`, run the T064 provisioner-contract cases GREEN, and inspect the mount allowlist/result-schema tests for exactly three candidate-bound driver files, zero duplicate/colliding product assemblies, product assembly resolution under image `/app`, in-image CodeGraphContext 0.5.1 identity, exact refs/commits/Profiled Revisions, deterministic ordinary Pass 1 identity/projection, one consumer-owned exactly-two-Repository runtime flow, one exact seven-slot Pass 2 dispatch/commit, forbidden secret/source/raw-diagnostic/absolute-host-path rejection, and atomic result completion; record only non-live bounded output in `tests/Vulgata.Tests/TestResults/story-11-5-focused/option-a-provisioner.txt`.
- [x] T068 [US3] Extend `tests/Vulgata.Tests/DemoChatReleaseGatePostgreSqlTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs` with the Option A orchestration RED matrix: one `docker/Dockerfile` build and one immutable `sha256:` image ID; same-ID provisioner then app inspection; read-only stable `/vulgata/sources/mbs_account`, `/vulgata/sources/mbs_channel`, three driver files, and `/vulgata/run/config/input.json`; nonce-writable output/work/key ring with the app key ring read-only; publish-capable provider namespace sharing with identical stored `127.0.0.1` Worker/Chat endpoints; built-in `host.docker.internal` PostgreSQL preflight before nonce database creation; provisioner removal/absence before app start; closed sanitized output; and failure injection after every acquisition proving dependency-ordered cleanup, bounded read-only/leased-pack retry, exact nonce database termination/drop/catalog absence, external PostgreSQL non-ownership, and separate retention of the primary plus every cleanup failure.
- [x] T069 [US3] Run the Option A orchestration/lifecycle cases under `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` and record the observed RED against the old host-side provisioning order in `tests/Vulgata.Tests/TestResults/story-11-5-red/option-a-harness.txt`; prove the test lane uses faked command/process/database/browser seams and acquires no live Docker, PostgreSQL, provider, Repository, or Playwright resource.
- [x] T070 [US3] Implement the minimal GREEN single-image lifecycle only in `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs` and its assertions only in `tests/Vulgata.Tests/DemoChatReleaseGatePostgreSqlTests.cs`: validate the closed environment/candidate/production manifest/Repository refs/driver inventory/Windows Docker Desktop Linux mode/external PostgreSQL 17 endpoint before acquisition; build once and record the immutable ID; start the loopback-published provider; preflight built-in `host.docker.internal` from its namespace; create only a cryptographically nonce-named external-server database; run the same-ID provisioner with exact mounts and `--network container:<provider>`; validate sanitized atomic output and remove/prove provisioner absence; start/inspect the same-ID app with the identical provider endpoints and read-only key ring; preserve curl/Identity/interactive Playwright citation, inaccessible-scope, and revocation proof; and unconditionally aggregate cleanup/absence outcomes without `--add-host`, `host-gateway`, `--internal`, app-side `--publish`, a new environment variable, a second image/build/pull/commit, host `cgc`, or ownership/change of the external PostgreSQL server/container, administrator database, or other databases.
- [x] T071 [US3] Run all non-live `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` contract, command-shape, sanitization, failure-injection, read-only/leased-pack cleanup, and lifecycle cases GREEN; rerun the focused Story 11.5 filters from `specs/008-demo-chat-release-gate/quickstart.md`, publish/inventory the Release provisioner, build `src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and `tests/Vulgata.Tests/Vulgata.Tests.csproj` in Release with analyzers, run predecessor migration tests plus `dotnet ef migrations has-pending-model-changes`, run applicable non-live regression, and statically reject a host `cgc`, second/provisioner image, duplicate mounted product assembly, fixture SQL/`DbSet`/reflection, eighth Pass 2 slot, general Scan/retry expansion, secret/path leak, deliberate RED, or any delta under `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`, `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, `docker/Dockerfile`, or package files; record bounded results under `tests/Vulgata.Tests/TestResults/story-11-5-option-a-non-live/` without live resource acquisition.
- [ ] T072 After T064-T071 are GREEN, require the pre-implementation root mechanical validation above to remain satisfied, root-only pinning of the unchanged clean commit, tree, canonical `production-source-manifest-v1` hash, owned-path/test-evidence/combined-verdict hashes, complete FR-001-FR-024/SC-001-SC-011 coverage, and zero-dirty-status observation in `tests/Vulgata.Tests/TestResults/story-11-5-candidate/candidate-metadata.json`, plus one fresh combined acceptance-and-quality `ACCEPT` with zero unresolved findings over that identical boundary in `specs/008-demo-chat-release-gate/reviews/quality-review/quality-review-iteration-7.json`. Only from that unchanged reviewed candidate, run the sole live Release `FullyQualifiedName~DemoChatReleaseGatePostgreSqlTests` final entry once on Windows Docker Desktop in Linux-container mode, requiring one source-bound image build and immutable ID for both provisioner and app, exact read-only source/driver/config origins cloned only into nonce work, provider namespace sharing, built-in `host.docker.internal` access to only the external PostgreSQL 17 nonce database, image-owned CodeGraphContext 0.5.1 profiling and the full predecessor path, deterministic ordinary Pass 1 identity/projection, one consumer-owned exactly-two-Repository runtime flow, one unchanged exact seven-slot Pass 2 dispatch/commit, authorized current Chat retrieval, complete citations, curl health, real Identity/interactive Blazor positive plus inaccessible/revoked proofs, atomic sanitized result/evidence, provisioner removal before app, and unconditional cleanup with primary and every cleanup failure preserved in `tests/Vulgata.Tests/TestResults/story-11-5-release-evidence.json`; never touch the external server/container, administrator database, or other databases.
- [ ] T073 Root/orchestrator independently verify through the supplied administrator database that the exact T072 nonce database is absent and verify absence of every evidence-recorded harness-owned provisioner/app/provider container, final image, publish-capable network, source/config/output/work/env/key/browser/nonce-root resource while leaving the external PostgreSQL server/container and administrator/other databases untouched; reject completion on residue, cleanup masking, secret/path/source/raw-diagnostic leakage, image/label/driver-inventory mismatch, host `cgc`, second image, migration/model delta, citation/authorization/currentness failure, noninteractive proof, identity/projection/runtime-binding drift, or an eighth Pass 2 slot. Against the unchanged combined-reviewed candidate, confirm final focused/applicable regression, Release builds, migration/no-pending-model/static checks, zero unresolved RED, `git diff --check`, repository workflow validation, FR-001-FR-024/SC-001-SC-011 coverage, and truthful separate limitations from `specs/008-demo-chat-release-gate/contracts/release-verification-contract.md`; only after T001-T072, the root mechanical delivery-definition validation, the fresh combined quality `ACCEPT`, and the clean-candidate prerequisite remain accepted may the root/orchestrator mark T073 complete and perform the authorized lifecycle transition/checkpoint.

**Checkpoint**: Root mechanical requirement/task/test-first/security/migration validation gates T064; T064-T071 prove the Option A correction without live acquisition; clean-candidate pinning plus one fresh combined quality `ACCEPT` gate T072 as the sole live run; T073 independently verifies exact cleanup and final workflow completion.

---

## Requirement and Contract Coverage

| Authority | Executable task coverage |
|---|---|
| FR-001; FR-002; FR-003; SC-005 | T019-T030: authenticated Chinese UI, explicit mode/System/exact pair, and negative absence of automatic scope/file upload |
| FR-004; FR-011; SC-001 | T007-T014, T031-T034: bounded current database evidence, exact directed Edge/profile facts, deterministic span support, and no `index.md` knowledge |
| FR-005; FR-006; FR-017; SC-004 | T007-T010, T031-T034, T039-T046, T051-T058, T061, T064-T073: live both-endpoint checks, runtime-binding authorization, final reauthorization, missing-equivalent behavior, and authorization-negative release proof |
| FR-007; FR-008; FR-009; FR-010; SC-002; SC-003 | T011-T014, T035-T046: exact mode-eligible persisted claims, complete server-owned Document citations, Business locator hiding/expansion, and IT locator allowance |
| FR-012; FR-013; FR-014 | T015-T018, T031-T046: whole-result validation, post-provider currentness, atomic publication, cancellation/timeout/provider/malformed fail-closed behavior |
| FR-015; FR-016; FR-023; FR-024; SC-006; SC-010; SC-011 | T051-T058, T064-T073: accepted revisions; deterministic ordinary Pass 1 Job IDs with repeat/per-input mutation/collision separation, actual runner dispatch, planner-independent recomputation, and existing Document Version projection; fail-closed legacy/random/substituted/drifted evidence; unique current authorized product-created runtime bindings; canonical schema/Manifest/Flow/support identities; exact seven-slot single dispatch/commit; and the unique clean PostgreSQL path through in-image profiling, Pass 1, exact Cross-Repository Resolution, Pass 2, retrieval, currentness, final authorization, and one cited answer |
| FR-018; SC-007 | T059-T073: focused/security/migration/build/regression, root mechanical coverage validation, one fresh combined quality review, one source-bound Docker image ID used by provisioner and app, curl health, real Identity, and interactive Blazor proof |
| FR-019; SC-008 | T055-T058, T068-T073: unconditional provisioner/resource cleanup, exact-database termination/drop/catalog absence, nonce-only ownership, external PostgreSQL non-ownership, and primary-plus-cleanup failure retention |
| FR-020 | T003-T006 and every story RED/GREEN quartet, then T059-T061: transient observed RED only and zero failing RED in the candidate |
| FR-021; SC-009 | T057, T064-T073: closed sanitized provisioner/candidate/release evidence and truthful separate post-demo limitations |
| FR-022 | T051-T054, T061, and T064-T073: negative boundary for host `cgc`, second/provisioner image, retry/duplicate-commit permission, legacy backfill, general Scan discovery/clustering/scheduling/concurrency, matching, history/graph/dashboard/SignalR, an eighth Worker slot, and deferred Epic 5-10 behavior |
| Core and evidence request/packet contracts | T003-T014 |
| Provider resolution, embedded prompt, and closed JSON contracts | T015-T018, T035-T038 |
| Post-provider currentness and Web ordering contracts | T031-T042 |
| Render contract | T023-T026, T043-T046 |
| Release verification contract | T047-T073 |
| Ordinary Pass 1 Job Identity and runtime Pass 2 binding contracts | T051-T055, T059, T061, T064-T073 |
| No-migration/no-backfill decision | T051, T053-T054, T060-T061; any unexpected model delta stops for correct-course/plan review |

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** has no dependency and must confirm the pinned inputs before any RED.
- **Phase 2 (Foundational)** depends on Phase 1 and blocks every user story.
- **US1 / Phase 3** depends on Phase 2 and supplies the minimal prepared chat slice.
- **US2 / Phase 4** depends on the US1 retrieval/candidate/coordinator seams, then independently proves currentness, complete citations, and fail-closed security.
- **US3 / Phase 5** depends on US1 and US2; T051-T054 establish deterministic ordinary Pass 1 dispatch identity, planner-independent historical identity/Document Version projection verification, and runtime prepared-flow binding before T055-T058 build the release harness that exercises them through production services and the real UI.
- **Phase 6 (Option A correction and final verification)** depends on completed T001-T063, accepted iteration 11, the durable Option A authorization, and successful pre-implementation root mechanical requirement/task/test-first/security/migration coverage validation. T064-T067 form the provisioner RED/GREEN group; T068-T071 form the harness-orchestration RED/GREEN plus non-live verification group. After T071, root pins the clean candidate and one fresh combined acceptance-and-quality review must `ACCEPT` that identical boundary. T072 is the sole live final image/browser/database run and T073 independently verifies cleanup and completes only if the identical candidate survives every gate.

### User story dependency graph

```text
Setup -> Foundational -> US1 (prepared chat MVP)
                              |
                              v
                    US2 (current citations/security)
                              |
                              v
                    US3 (clean release path + Option A correction)
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
- After US2, T047 Data Protection composition tests and T051 runtime-binding tests may be authored concurrently because they initially touch separate test and production ownership paths; T055 release-harness work depends on accepted T050 and T054 behavior.
- No task that edits `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, `src/dotnet/Vulgata.Web/Program.cs`, `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`, `tests/Vulgata.Tests/ChatPageTests.cs`, or `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` may overlap another task editing the same file.
- T064-T071 are intentionally sequential because both RED/GREEN groups touch the two preserved dirty release-harness files and the second group depends on the provisioner contract. After T071, candidate pinning is root-only and the single fresh combined quality review must finish before T072. T072-T073 are strictly sequential, and the unique database, candidate identity, final image, browser proof, and cleanup evidence must not be split across owners.

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
3. **US3** adds deterministic ordinary Pass 1 dispatch identity, planner-independent identity/Document Version projection verification, runtime prepared-flow binding with exact seven-slot Pass 2 validation, the optional shared Data Protection configuration, and the sole clean PostgreSQL/Docker/Identity/interactive-browser release harness.
4. **Option A correction** uses T064-T071 for observed provisioner and harness RED/GREEN plus non-live verification, with no live resource acquisition.
5. **Expedited completion** requires successful root mechanical delivery-definition validation, one root-pinned clean candidate, and one fresh combined quality `ACCEPT` before T072 performs the sole live Docker/PostgreSQL/Playwright run; T073 then performs independent cleanup plus final workflow verification.

### Stop conditions

- Stop on frozen input or source-boundary mismatch, missing planned-change authorization, an authority conflict, or an implementation need outside the accepted paths.
- Stop and return to correct-course/plan review if durable state, an EF model delta/migration, a new provider-visible tool field, Scan strategy, Cross-Repository matching behavior, retry/failover, or a PRD change becomes necessary.
- Stop acceptance on any unresolved RED, missing/partial citation, restricted disclosure, inferred graph truth, dirty/mismatched image label, unavailable prerequisite, failed cleanup, or database residue.

## Notes

- `.specify/extensions.yml` contains no `hooks.before_tasks` or `hooks.after_tasks`; no task hook applies.
- Execution level is `expedited-mvp`: root mechanical requirement/task/test-first/security/migration coverage validation and one fresh combined acceptance-and-quality review are mandatory; `spec-review`, active `speckit-analyze`, and the separate `acceptance-review` remain unused at iteration zero.
- T001-T063 are preserved completed history. T064-T071 are completed with
  observed RED/GREEN and non-live verification; T072-T073 remain unchecked and
  presume no successful live run, cleanup verification, or completion evidence.
- `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs` is the sole authority for the nonce database and nonce-owned Docker/browser resources; it never owns the external PostgreSQL server/container or administrator/other databases. Root/orchestrator alone owns final candidate pinning, lifecycle advancement, staging, and commit.
- T071 truthfully completed against its then-current boundary with no
  `docker/Dockerfile` delta. After repeated live build latency and a transient
  PyPI timeout, the user explicitly authorized a mirror-only correction on
  2026-08-10: Tsinghua serves ordinary Ubuntu Noble archives and the hash-locked
  pip index, while Ubuntu's official Noble security archive remains authoritative.
  This invalidates and reruns the candidate review/E2E boundary only; it does
  not reopen T064-T071 behavior, change dependencies or package identities, or
  mark T072/T073 complete.
