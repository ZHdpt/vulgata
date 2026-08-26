# Tasks: Exact Cross-Repository Flow & Pass 2

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`,
`quickstart.md`, and the four contracts under
`specs/013-exact-cross-repository-flow-pass2/`.

**Boundary**: Story 12.4 only. Story 12.5 Chat, production progress UI, final
release evidence, vector retrieval, unrestricted tools, and general graph
clustering are excluded. The existing Story 12.1–12.3 seams are extended
compatibly; legacy prepared-demo rows remain readable and are not promoted by
inferred backfill.

**Execution rule**: Every executable behavior task is test-first. The named
test owner writes the public-seam test and records an honest RED (existing
correct behavior is characterization/test-only and must not fabricate RED); the
named production owner then implements the minimum change for GREEN. Tests and
production code are separate ownership boundaries. Documentation, migration
metadata, prompts, and other non-executable artifacts use direct verification.

## Phase 1: Setup (Shared Story Boundary)

**Purpose**: Establish the feature test vocabulary and bounded production
composition without changing behavior.

- [x] T001 [P] Add Story 12.4 test fixture builders, deterministic canonical ordering helpers, and content-dark assertion helpers in `tests/Vulgata.Tests/PreparedPass2/Pass2TestFixtures.cs`, `tests/Vulgata.Tests/PreparedPass2/Pass2CanonicalAssertions.cs`, and `tests/Vulgata.Tests/PreparedPass2/Pass2PrivacyAssertions.cs`.
- [x] T002 Add the external Pass 2 prompt and the one-time embedded `business-logic-flow-v1.schema.json` resource; define its exact logical resource name in `src/dotnet/Vulgata.Agents/Vulgata.Agents.csproj`, verify unique embedding/loading, SHA-256 identity against `specs/013-exact-cross-repository-flow-pass2/contracts/business-logic-flow-v1.schema.json`, and absence of hardcoded prompt/schema duplication in `src/dotnet/Vulgata.Agents/Prompts/pass2-business-logic-worker.v1.md`, `tests/Vulgata.Tests/PreparedPass2/Pass2PromptCompositionTests.cs`, and `tests/Vulgata.Tests/PreparedPass2/Pass2SchemaResourceTests.cs`.
- [x] T003 [P] Add the Story 12.4 test project references and explicit qualification opt-in switches without changing default provider behavior in `tests/Vulgata.Tests/Vulgata.Tests.csproj` and `tests/Vulgata.Tests/PreparedPass2/Pass2ProductionQualificationTests.cs`.

## Phase 2: Foundational (Contracts, Identity, and Migration Seams)

**Purpose**: Establish shared contracts and additive persistence prerequisites.
No user-story implementation may start until these tasks are green.

- [x] T004 Write RED public contract tests for v2 resolution decisions, provider contribution identity, Flow Group execution identity, cross-scan logical-flow identity, Pass 2 execution/job/retrieval/omission states, `IPreparedPass2WorkerDispatcher`, foundation entity/contract invariants, and compatibility with legacy `ExactCrossRepositoryEdgeEvidence` in `tests/Vulgata.Tests/PreparedPass2/ExactResolutionContractTests.cs`, `tests/Vulgata.Tests/PreparedPass2/Pass2ExecutionContractTests.cs`, `tests/Vulgata.Tests/PreparedPass2/Pass2WorkerDispatcherContractTests.cs`, and `tests/Vulgata.Tests/PreparedPass2/Pass2LegacyCompatibilityTests.cs`. Record the RED before any T005/T006 GREEN work.
- [x] T005 Define the v2 Core entities and value contracts, preserving immutable Documents, Version Chains, reusable tuple-unique `DocumentEdge`, decision-owned exact evidence, and consumer-owned logical-flow keys in `src/dotnet/Vulgata.Core/Entities/CrossRepositoryResolutionDecision.cs`, `src/dotnet/Vulgata.Core/Entities/ExactResolutionProviderEvidenceContribution.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2Execution.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2Omission.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2JobExecution.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2RetrievalBudget.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2RetrievalReceipt.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2RetrievalResult.cs`, `src/dotnet/Vulgata.Core/Entities/FlowGroup.cs`, `src/dotnet/Vulgata.Core/Entities/Uncertainty.cs`, and `src/dotnet/Vulgata.Core/Entities/LogicalDocument.cs`.
- [x] T006 Define public domain-service contracts, including `IPreparedPass2WorkerDispatcher`, and canonical codecs for resolution, planning, execution, retrieval, runtime binding, and atomic publication in `src/dotnet/Vulgata.Core/DomainServices/ExactResolutionContracts.cs`, `src/dotnet/Vulgata.Core/DomainServices/PreparedPass2Flow.cs`, `src/dotnet/Vulgata.Core/DomainServices/PreparedPass2RuntimeBinding.cs`, `src/dotnet/Vulgata.Core/DomainServices/Pass2CanonicalCodec.cs`, `src/dotnet/Vulgata.Core/DomainServices/Pass2ExecutionContracts.cs`, `src/dotnet/Vulgata.Core/DomainServices/Pass2RetrievalContracts.cs`, `src/dotnet/Vulgata.Core/DomainServices/IPreparedPass2WorkerDispatcher.cs`, `src/dotnet/Vulgata.Core/DomainServices/IExactCrossRepositoryResolver.cs`, `src/dotnet/Vulgata.Core/DomainServices/IExactCrossRepositoryResolutionStore.cs`, `src/dotnet/Vulgata.Core/DomainServices/IPreparedPass2Store.cs`, `src/dotnet/Vulgata.Core/DomainServices/IPass2CodeLogicRetrievalService.cs`, and `src/dotnet/Vulgata.Core/DomainServices/IPass2BusinessLogicDocumentCommitter.cs`.
- [x] T007 Write migration and relational-constraint RED tests for v2 decision/contribution, nullable prepublication Flow Group, execution/job/retrieval/omission, stable logical-flow key/current pointer, reusable Edge FK, legacy compatibility, rollback, and pending-model-change checks in `tests/Vulgata.Tests/PreparedPass2/Pass2MigrationTests.cs` and `tests/Vulgata.Tests/PreparedPass2/Pass2PostgreSqlConstraintTests.cs`.
- [x] T008 Implement one additive compatibility migration, EF configurations, model registrations, indexes, FKs, concurrency tokens, and legacy-row preservation in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/ExactCrossRepositoryResolutionDecisionConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/ExactResolutionProviderEvidenceContributionConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/FlowGroupConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/Pass2ExecutionConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/Pass2OmissionConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/Pass2JobExecutionConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/Pass2RetrievalConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_ExactCrossRepositoryFlowPass2.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_ExactCrossRepositoryFlowPass2.Designer.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`.
- [x] T009 Run the migration RED/GREEN suite against fresh and legacy fixtures, rollback/reapply with no v2 prepublication rows, and EF pending-model-change verification using `tests/Vulgata.Tests/PreparedPass2/Pass2MigrationTests.cs` and `tests/Vulgata.Tests/PreparedPass2/Pass2PostgreSqlConstraintTests.cs`.

## Phase 3: User Story 1 - Resolve Exact Cross-Repository Communication (Priority: P1)

**Goal**: Produce only deterministic exact outcomes: one reusable directed
REFERENCES Edge plus one decision when one distinct current provider Document
Version is eligible, otherwise a decision-owned honest Uncertainty.

**Independent test**: Pure and PostgreSQL tests cover exact Custom RPC and safe
HTTP, protocol mismatch, unsupported kinds, zero/multiple distinct providers,
duplicate observations, conflicting roles, stale/unauthorized evidence,
randomized order, replay, concurrent Edge reuse, and no CodeGraph/LLM effect.

### Tests first

- [ ] T010 [P] [US1] Write RED tests for current authorized candidate extraction, catalog re-evaluation, consumer-entry stable identity, provider aggregation, exact Custom RPC normalization, safe HTTP normalization, and closed rejection in `tests/Vulgata.Tests/PreparedPass2/ExactCrossRepositoryResolutionTests.cs`.
- [ ] T011 [US1] Write RED tests for protocol/key matching, zero/one/multiple distinct current provider Document Versions, duplicate locators under one provider, two endpoint/entry decisions sharing one reusable Document Edge, uncertainty reasons, ten randomized enumeration/completion orders, and graph/LLM independence in `tests/Vulgata.Tests/PreparedPass2/ExactCrossRepositoryResolutionTests.cs` after T010's RED seam; T010 and T011 are serialized because they own one file.
- [ ] T012 [P] [US1] Write RED tests for atomic decision/contribution/Edge/Uncertainty persistence, exact duplicate replay, reordered/missing/extra replay conflict, currentness and authorization drift, concurrent tuple-Edge reuse, and unchanged legacy evidence in `tests/Vulgata.Tests/PreparedPass2/ExactCrossRepositoryPostgreSqlTests.cs`.

### Implementation

- [ ] T013 [US1] Implement server-owned current candidate reconstruction and revalidation from the terminal Story 12.3 pair, manifests, current Code Logic Documents, Source Anchors, classifications, deferred observations, catalog evidence, and exact same-Repository REFERENCES Edges in `src/dotnet/Vulgata.Infrastructure/CrossRepository/CurrentCommunicationCandidateSource.cs`.
- [ ] T014 [US1] Implement the single closed protocol-aware endpoint normalizer with exact catalog-driven Custom RPC and safe queryless HTTP rules in `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactEndpointNormalizer.cs`.
- [ ] T015 [US1] Implement deterministic provider aggregation by distinct current provider XML Document Version and canonical evidence fingerprints in `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactCrossRepositoryResolver.cs` and `src/dotnet/Vulgata.Core/DomainServices/ExactResolutionContracts.cs`.
- [ ] T016 [US1] Implement v2 decision-owned exact/unresolved outcomes, reusable tuple-unique Document Edge binding, Uncertainty ownership, and additive idempotent persistence in `src/dotnet/Vulgata.Infrastructure/Data/ExactCrossRepositoryResolutionStore.cs` and `src/dotnet/Vulgata.Infrastructure/Data/Configurations/DocumentEdgeConfiguration.cs`.
- [ ] T017 [US1] Verify pure and PostgreSQL US1 suites, including randomized determinism, exact replay, concurrent Edge reuse, and legacy compatibility; record SC-001, SC-002, FR-001–FR-010 evidence in `tests/Vulgata.Tests/PreparedPass2/ExactCrossRepositoryResolutionTests.cs` and `tests/Vulgata.Tests/PreparedPass2/ExactCrossRepositoryPostgreSqlTests.cs`.

## Phase 4: User Story 2 - Plan Bounded Endpoint-Centered Flow Groups (Priority: P1)

**Goal**: Plan one deterministic pair-specific Flow Group per exact resolution
decision, with closed membership and separate cross-scan Logical Document
identity; dispatch exactly one job only for each complete group.

**Independent test**: Planning tests prove required members, one-hop same-
Repository closure, stable ordering/keys, currentness and authorization,
8-Document/16-step/10-group/2-Repository bounds, omission reasons, shared Edge
with distinct decisions, and atomic start/replay.

### Tests first

- [ ] T018 [P] [US2] Write RED pure planning tests for pair-specific Flow Group identity, cross-scan `business-flow-logical-key/1.0`, stable consumer-entry identity across Document Versions and anchor ranges, shared Edge with distinct decisions, required consumer/provider XML/provider implementation members, and canonical ordering in `tests/Vulgata.Tests/PreparedPass2/Pass2FlowPlanningTests.cs`.
- [ ] T019 [US2] Write RED pure planning tests for one-hop same-Repository references, cycles/duplicates, stale/unanchored/unauthorized/wrong-revision/foreign-System candidates, raw graph/LLM exclusion, missing/multiple implementation, and incomplete reasons in `tests/Vulgata.Tests/PreparedPass2/Pass2FlowPlanningTests.cs` after T018's RED seam; T018 and T019 are serialized because they own one file.
- [ ] T020 [P] [US2] Write RED boundary tests for two Repositories, eight distinct Documents, sixteen evidence steps, ten groups, plus-one bound exclusion, content-dark omission rows, exact repeated planning, and atomic group/job/budget start/replay in `tests/Vulgata.Tests/PreparedPass2/Pass2FlowPlanningPostgreSqlTests.cs`.

### Implementation

- [ ] T021 [US2] Implement deterministic endpoint-centered planning from exact decisions (never Uncertainties), canonical member/trace construction, one-hop validation, stable keys, and bounded omission classification in `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass2FlowPlanner.cs`, `src/dotnet/Vulgata.Core/Entities/FlowGroup.cs`, `src/dotnet/Vulgata.Core/Entities/FlowTrace.cs`, and `src/dotnet/Vulgata.Core/Entities/FlowTraceStep.cs`.
- [ ] T022 [US2] Implement durable Pass 2 execution start, atomic group/trace/omission/job/budget creation, coordination transition, exact replay, and no-job incomplete/bound-excluded behavior in `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass2Store.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2Execution.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2JobExecution.cs`, and `src/dotnet/Vulgata.Core/Entities/Pass2Omission.cs`.
- [ ] T023 [US2] Verify US2 pure/PostgreSQL planning and atomic-start suites and record SC-003, SC-004, FR-011–FR-019 evidence in `tests/Vulgata.Tests/PreparedPass2/Pass2FlowPlanningTests.cs` and `tests/Vulgata.Tests/PreparedPass2/Pass2FlowPlanningPostgreSqlTests.cs`.

## Phase 5: User Story 3 - Retrieve Closed Supporting Knowledge (Priority: P2)

**Goal**: Provide only bounded, server-scoped current Code Logic evidence through
`retrieve_code_logic`; retrieval cannot alter resolution or group membership.

**Independent test**: Every operation and inclusive boundary is exercised with
durable reservation, replay/restart accounting, opaque handles, refusal privacy,
and provider-decision immutability.

### Tests first

- [ ] T024 [P] [US3] Write RED contract tests for `Search`, `Sections`, `SourceAnchors`, `SourceExcerpt`, `RelatedDocuments`, typed arguments/results, opaque handles, and content-dark refusal behavior in `tests/Vulgata.Tests/PreparedPass2/RetrieveCodeLogicContractTests.cs`.
- [ ] T025 [P] [US3] Write RED security/currentness tests for stale, superseded, unauthorized, cross-System, mismatched Profiled Revision/hash/range, second-hop, hidden-content, and provider/group-changing requests in `tests/Vulgata.Tests/PreparedPass2/RetrieveCodeLogicSecurityTests.cs`.
- [ ] T026 [P] [US3] Write RED PostgreSQL budget/replay tests for eight calls, ten results, four additional Documents, four sections per Document, 65,536 UTF-8 bytes, duplicate results, lost receipts, exact call replay, later duplicate requests, and retry/restart persistence in `tests/Vulgata.Tests/PreparedPass2/RetrieveCodeLogicPostgreSqlBudgetTests.cs`.

### Implementation

- [ ] T027 [US3] Implement server-owned retrieval scope, deterministic lexical search, current section/anchor/source reads, one-hop related-Document traversal, and authorization/currentness validation in `src/dotnet/Vulgata.Infrastructure/Data/Pass2CodeLogicRetrievalService.cs` and `src/dotnet/Vulgata.Core/DomainServices/IPass2CodeLogicRetrievalService.cs`.
- [ ] T028 [US3] Implement durable reservation-before-I/O, cumulative call/result/Document/section/byte budgets, exact ordinal replay, lost-receipt charging, opaque handle binding, and content-dark receipts/results in `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass2Store.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2RetrievalBudget.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2RetrievalReceipt.cs`, `src/dotnet/Vulgata.Core/Entities/Pass2RetrievalResult.cs`, and `src/dotnet/Vulgata.Core/DomainServices/Pass2RetrievalContracts.cs`.
- [ ] T029 [US3] Verify US3 operation, security, and PostgreSQL budget suites and record SC-005, SC-006, SC-010, FR-020–FR-028 evidence in `tests/Vulgata.Tests/PreparedPass2/RetrieveCodeLogicContractTests.cs`, `tests/Vulgata.Tests/PreparedPass2/RetrieveCodeLogicSecurityTests.cs`, and `tests/Vulgata.Tests/PreparedPass2/RetrieveCodeLogicPostgreSqlBudgetTests.cs`.

## Phase 6: User Story 4 - Generate an Immutable Business Logic Document (Priority: P1)

**Goal**: Run the strict two-function Worker loop and publish one immutable,
source-grounded Business Logic Document Version per successful Flow Group with
all-and-only-used GENERATED_FROM provenance and cross-scan Version Chain rules.

**Independent test**: Fake-wire, validation, PostgreSQL atomicity, replay,
concurrency, changed endpoint, later scan pair, and failure-injection tests
prove no partial publication and immutable current-pointer behavior.

### Tests first: strict Worker transport

- [ ] T030 [US4] Write RED strict declaration tests proving exactly `retrieve_code_logic` and `submit_business_logic`, bounded arguments, and no evidence authority in submission in `tests/Vulgata.Tests/PreparedPass2/Pass2StrictSubmissionTests.cs`; depend on T002's embedded-schema identity tests and keep the production projector contract external-resource-only.
- [ ] T031 [P] [US4] Write RED streaming executor tests for inactivity and absolute caps, raw/request/assembled byte bounds, zero SDK retry, all-retrieve batch preflight, serial callbacks, forced request nine submission, sole correction request ten, no request eleven, and budget survival across retry/restart in `tests/Vulgata.Tests/PreparedPass2/PreparedPass2JobExecutorStreamingTests.cs`.
- [ ] T032 [US4] Write RED submission/semantic-validation tests for opaque handle use, claim support, contiguous ordinals, group/provider/Edge/key fingerprints, unsupported relationship rejection, returned-but-uncited exclusion, incomplete provenance, and bounded UTF-8 in `tests/Vulgata.Tests/PreparedPass2/Pass2StrictSubmissionTests.cs` and `tests/Vulgata.Tests/PreparedPass2/Pass2BusinessLogicValidationTests.cs` after T030's RED seam; T030 and T032 are serialized because they own `Pass2StrictSubmissionTests.cs`.

### Tests first: publication and replay

- [ ] T033 [P] [US4] Write RED pure commit tests for all-and-only-used GENERATED_FROM derivation, initial and retrieved evidence revalidation, immutable materialization, stale/unauthorized/mismatched/changed-provider rejection, and no-write rejection in `tests/Vulgata.Tests/PreparedPass2/Pass2DocumentCommitTests.cs`.
- [ ] T034 [P] [US4] Write RED PostgreSQL atomic/replay/concurrency tests for failure injection at each boundary, exact/lost-ack/restart replay, changed replay conflict, one shared Edge with another decision rejected as evidence, stable-key creation race, current-pointer locking, later-pair append/supersede, changed endpoint/new chain, and preserved old versions in `tests/Vulgata.Tests/PreparedPass2/Pass2DocumentPostgreSqlAtomicTests.cs`.

### Implementation

- [ ] T035 [US4] Implement the concrete `PreparedPass2WorkerDispatcher` against the Core `IPreparedPass2WorkerDispatcher` contract from T006, plus strict v2 tool declarations, retrieve callback, submission callback, packet projection, evidence handles, preflight, forced submission/correction, streaming caps, sanitized diagnostics, and zero-retry policy; load and verify the single embedded `business-logic-flow-v1.schema.json` resource from T002 (never duplicate it in C# or prompt text) in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedRetrieveCodeLogicFunction.cs`, `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedSubmitBusinessLogicFunction.cs`, `src/dotnet/Vulgata.Agents/WorkerExecution/Pass2StrictToolDeclarations.cs`, `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass2JobExecutor.cs`, `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass2WorkerDispatcher.cs`, and `src/dotnet/Vulgata.Agents/Prompts/pass2-business-logic-worker.v1.md`.
- [ ] T036 [US4] Implement v2 runtime packet/result binding, local schema/semantic validation, used-handle derivation, and exact group/decision/provider/Edge/logical-key fingerprint checks in `src/dotnet/Vulgata.Core/DomainServices/PreparedPass2RuntimeBinding.cs`, `src/dotnet/Vulgata.Core/DomainServices/PreparedPass2Flow.cs`, `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass2Store.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/Pass2BusinessLogicDocumentCommitter.cs`.
- [ ] T037 [US4] Implement one atomic immutable publication transaction, consumer-owned stable Logical Document creation/reuse, Version Chain successor/current-pointer publication, all-and-only-used GENERATED_FROM Edges, Flow Trace attachment, job completion, lease release, and exact replay/conflict handling in `src/dotnet/Vulgata.Infrastructure/Data/Pass2BusinessLogicDocumentCommitter.cs`, `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeDocumentMaterializer.cs`, `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/Configurations/LogicalDocumentConfiguration.cs`.
- [ ] T038 [US4] Verify strict-wire, executor, validation, atomic PostgreSQL, replay, and Version Chain suites and record SC-007–SC-010, FR-029–FR-037, and FR-042 evidence in `tests/Vulgata.Tests/PreparedPass2/Pass2StrictSubmissionTests.cs`, `tests/Vulgata.Tests/PreparedPass2/PreparedPass2JobExecutorStreamingTests.cs`, `tests/Vulgata.Tests/PreparedPass2/Pass2BusinessLogicValidationTests.cs`, and `tests/Vulgata.Tests/PreparedPass2/Pass2DocumentPostgreSqlAtomicTests.cs`. SC-012 is not owned or satisfied here.

## Phase 7: User Story 5 - Complete Remaining Groups Honestly (Priority: P1)

**Goal**: Preserve successful Business Logic Documents while failed,
incomplete, bound-excluded, provider-paused, or omitted groups remain visible
with bounded reasons and truthful terminal accounting.

**Independent test**: Recovery/API/mixed-outcome tests prove serial claims,
one active job per group, one retry, lease/restart/manual resume, sanitized
authorization, no resolution rewrite, and complete terminal counts.

### Tests first

- [ ] T039 [P] [US5] Write RED execution/recovery tests for authenticated activation, serial canonical claims, one active job, lease expiry, host restart, one durable job-local retry, manual resume, provider pause/probe reuse, and identity/budget preservation in `tests/Vulgata.Tests/PreparedPass2/Pass2ExecutionRecoveryTests.cs`.
- [ ] T040 [P] [US5] Write RED partial-completion tests for successful, failed, incomplete, bound-excluded, and omitted groups; zero-document failure; preserved successful Documents; immutable resolution outcomes; warning reason inventory; and Completed/CompletedWithWarnings terminal accounting in `tests/Vulgata.Tests/PreparedPass2/Pass2PartialCompletionTests.cs`.
- [ ] T041 [US5] Write RED public API/security tests for create/status/resume, central plural-route reachability, authenticated ownership filtering, 401/403/ownership-filtered 404 semantics, ProblemDetails conflicts, sanitized counts/warnings, caller-supplied scope rejection, and no Story 12.5 UI in `tests/Vulgata.Tests/PreparedPass2/Pass2ExecutionApiTests.cs`. This is the public API RED owner; T043 is its explicit GREEN owner.

### Implementation

- [ ] T042 [US5] Consume the Core `IPreparedPass2WorkerDispatcher` contract from T006 at the Web execution seam while implementing durable claim/lease/recovery/retry state transitions, serial canonical scheduling, provider pause/manual-resume policy reuse, terminal warning/count aggregation, and coordination completion in `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass2Store.cs`, `src/dotnet/Vulgata.Web/Data/PreparedPass2Coordinator.cs`, `src/dotnet/Vulgata.Web/Data/PreparedPass2ExecutionActivator.cs`, `src/dotnet/Vulgata.Web/Data/PreparedPass2ExecutionHostedService.cs`, and `src/dotnet/Vulgata.Web/Data/PreparedPass2JobRunner.cs`.
- [ ] T043 [US5] Implement the authenticated plural Pass 2 execution endpoints as the GREEN owner for T041, with ownership-derived scope, Chinese FluentValidation/ProblemDetails, filtered 404/403/401 behavior, sanitized state/counts/warnings, and explicit resume wake-up; register the endpoint group through the mandatory central mapper in `src/dotnet/Vulgata.Web/Api/Scans/Pass2ExecutionEndpoints.cs`, `src/dotnet/Vulgata.Shared/Scans/Pass2ExecutionContracts.cs`, `src/dotnet/Vulgata.Shared/Scans/Pass2ExecutionValidators.cs`, and `src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs`.
- [ ] T044 [US5] Compose the production DI graph and preserve source/provider data stewardship, optional disabled direct JDT-LS/CodeGraph verification, and no Chat/UI/release-evidence registration in `src/dotnet/Vulgata.Web/Program.cs`, `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs`, `src/dotnet/Vulgata.Agents/DependencyInjection.cs`, and `src/dotnet/Vulgata.Web/appsettings.json`.
- [ ] T045 [US5] Verify execution, recovery, API, mixed-outcome, privacy, and composition suites and record SC-011, FR-038–FR-043 evidence in `tests/Vulgata.Tests/PreparedPass2/Pass2ExecutionRecoveryTests.cs`, `tests/Vulgata.Tests/PreparedPass2/Pass2PartialCompletionTests.cs`, `tests/Vulgata.Tests/PreparedPass2/Pass2ExecutionApiTests.cs`, and `tests/Vulgata.Tests/PreparedPass2/Pass2ProductionCompositionTests.cs`.

## Phase 8: Production Qualification and Final Barrier

**Purpose**: Validate the complete vertical slice against the approved disposable
PostgreSQL database, two approved Repositories and exact revisions only after
all fake-wire, schema, migration, PostgreSQL, and privacy gates pass. Stop and
report provider/network/configuration/environment failures; do not weaken the
contract or change the host.

- [ ] T046 [P] Run pure and PostgreSQL focused suites for exact resolution, Flow Group planning, retrieval, strict Worker, commit/replay, execution/API, and migrations using the commands in `specs/013-exact-cross-repository-flow-pass2/quickstart.md`; record test counts and all nonblocking baseline failures in `specs/013-exact-cross-repository-flow-pass2/evidence/verification/focused-results.json`.
- [ ] T047 [P] Run authenticated `WebApplicationFactory<Program>` composition and security suites, including source-free provider capability and content-dark diagnostics, in `tests/Vulgata.Tests/PreparedPass2/Pass2ProductionCompositionTests.cs` and `tests/Vulgata.Tests/PreparedPass2/Pass2ExecutionApiTests.cs`.
- [ ] T048 Run approved two-Repository live qualification with the disposable PostgreSQL connection, exact revisions, configured provider boundary, and disabled direct Pass 2 JDT-LS/CodeGraph in `tests/Vulgata.Tests/PreparedPass2/Pass2ProductionQualificationTests.cs`; own SC-012 and retain only the durable sanitized artifact `specs/013-exact-cross-repository-flow-pass2/evidence/qualification/story-12.4-live-two-repository.json` with `schema_version`, `qualification_id`, sanitized System/Repository/revision identifiers, provider endpoint/model (no key), database target (no password), status, Flow Group/job/document/omission counts, SHA-256 hashes, byte sizes, content-dark flags, and failure category. Stop and report on environment/network/provider failure.
- [ ] T049 Run regression suites for `ExactCrossRepository`, `Pass2`, `DurableScanning`, `PreparedScanning`, `KnowledgeStore`, and `AnalysisRuntime`, build `src/dotnet/Vulgata.Web/Vulgata.Web.csproj`, and run `git diff --check`; record zero-error build separately from warnings in `specs/013-exact-cross-repository-flow-pass2/evidence/verification/final-verification.md`.

## Dependencies & Execution Order

### Phase dependencies

- Phase 1 has no story dependency; T001–T003 may run in parallel.
- Phase 2 depends on Phase 1. Run T004 RED first, then T005 and T006 sequentially for the entity/contract GREEN seams; T007 may follow T006, then T008 and T009 are sequential. No foundation RED is concurrent with its GREEN owner.
- US1 depends on Phase 2. Run T010, then T011 because both own `ExactCrossRepositoryResolutionTests.cs`; T012 may run after T010 in parallel with T011 because it owns a different file. T013–T016 are ordered by candidate → normalization → resolution → persistence; T017 is the US1 gate.
- US2 depends on US1. Run T018, then T019 because both own `Pass2FlowPlanningTests.cs`; T020 may run after T018 in parallel because it owns a different file. T021 precedes T022; T023 is the US2 gate.
- US3 depends on US2’s authoritative Flow Group/job scope. T024–T026 may run in parallel; T027 precedes T028; T029 is the US3 gate.
- US4 depends on US2 and US3. Run T030 after T002, then T032 because both own `Pass2StrictSubmissionTests.cs`; T031, T033, and T034 may run in parallel with T030/T032 because they own distinct files. T035 depends on T002, T006, T030, and T031; T036 depends on T032; T037 depends on T033–T036; T038 is the US4 gate.
- US5 depends on US2 and US4. T039–T041 may run in parallel because they own distinct files; T042 follows T006, T039, and T040, T043 follows T041, and T044 follows T042/T043 and owns composition files. T045 is the US5 gate.
- Phase 8 depends on T009, T017, T023, T029, T038, and T045. T046–T047 may run in parallel; T048 requires all preceding gates and owns SC-012 qualification evidence; T049 requires T048 and is last.

### Parallel execution examples

```text
# Foundation contracts and migration tests (strict RED -> GREEN order)
T004 Foundation contract/entity invariant RED tests
then T005 Core v2 entities -> T006 domain-service contracts/codecs -> T007 migration RED tests

# US1 test-first slice (same-file RED seams serialized)
T010 Candidate/normalizer RED tests -> T011 determinism/cardinality RED tests
T012 PostgreSQL resolution RED tests (parallel after T010; distinct file)
then T013 -> T014 -> T015 -> T016 -> T017

# US4 independent test seams
T030 strict declaration RED tests -> T032 semantic-validation RED tests (shared file)
T031 streaming tests | T033 commit tests | T034 PostgreSQL replay tests (distinct files)
then T035 -> T036 -> T037 -> T038
```

### Test and production ownership boundaries

`tests/Vulgata.Tests/PreparedPass2/` owns all RED/GREEN assertions, fake-wire
providers, PostgreSQL fixtures, qualification evidence, and test-only harness
helpers. Core owns closed contracts/entities; Infrastructure owns catalog/source
revalidation, EF configurations, migration, planning, retrieval, transactions,
and persistence; Agents owns prompt resources and strict provider transport;
Shared owns API DTOs/validators; Web owns authorization, activation, hosted
execution, and endpoints. No test task may modify production code, and no
production task may silently replace a failing test with a weaker assertion.

## Requirements and Success-Criteria Traceability

| Task groups | Requirements | Success criteria / acceptance |
| --- | --- | --- |
| T010–T017 | FR-001–FR-010 | SC-001–SC-002; AC-1 |
| T018–T023 | FR-011–FR-019 | SC-003–SC-004; AC-2 |
| T024–T029 | FR-020–FR-028 | SC-005–SC-006, SC-010; AC-3 |
| T030–T038 | FR-029–FR-037, FR-042 | SC-007–SC-010; AC-3 |
| T039–T045 | FR-038–FR-043 | SC-011; AC-4 |
| T007–T009, T046–T049 | migration, security, qualification, regression invariants | SC-012 (owned by T048 only), all acceptance scenarios and final barrier |

All forty-three functional requirements and all twelve success criteria are
covered by at least one test-first or direct-verification task. The five BMad
acceptance groups map respectively to US1, US2, US3/US4, and US5; immutable
Document/Version Chain, reusable Edge versus decision identity, provider
cardinality, retrieval budgets, strict two-function transport, atomic replay,
terminal accounting, API/host composition, security/privacy, migration
lifecycle, and approved two-Repository qualification are named at their owning
tasks. No task introduces Story 12.5 behavior.

## Implementation Strategy

1. Complete Foundation and migration compatibility.
2. Deliver US1 deterministic resolution as the first independently valuable
   vertical slice.
3. Add US2 bounded planning and atomic job creation.
4. Add US3 closed retrieval, then US4 strict Worker and immutable publication.
5. Add US5 recovery/API/partial completion and run the full qualification
   barrier.

## Self-Review Checklist

- Every task uses `- [ ] T###`, has an exact path, and uses `[P]` only where
  dependencies and ownership allow parallel work.
- Every user-story task is labeled `[US1]`–`[US5]`; setup/foundation/polish
  tasks carry no story label.
- Tests precede each observable production behavior; T004 explicitly records
  foundation RED before T005/T006 GREEN, and characterization is not presented
  as fabricated RED.
- No parallel tasks share a writable file; same-file RED seams are serialized,
  and every RED has an explicit downstream GREEN owner.
- T002 uniquely embeds and hashes the accepted schema; T035 loads that embedded
  resource and contains no hardcoded schema duplicate.
- T041 owns the public API RED and T043 owns endpoint plus central mapper GREEN;
  T048 alone owns SC-012 and its durable sanitized qualification artifact.
- Source references cover the accepted plan’s Core, Infrastructure, Agents,
  Shared, Web, test, migration, qualification, and evidence seams.
- Dependency order is explicit, including resolution before planning, planning
  before retrieval/Worker, publication before terminal accounting, and all
  gates before live qualification.
- Exact deterministic, bounded, immutable, privacy, replay, concurrency,
  migration, and Story 12.5 exclusion constraints are represented with no
  unresolved placeholder or authority conflict.
