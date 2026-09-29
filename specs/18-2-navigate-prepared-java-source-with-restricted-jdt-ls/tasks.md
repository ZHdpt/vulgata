---
description: "Dependency-ordered Story 18.2 implementation tasks"
---

# Tasks: Navigate Prepared Java Source with Restricted JDT-LS

**Input**: `spec.md`, accepted `plan.md`, `research.md`, `data-model.md`, `contracts/navigation-tool.md`, and `quickstart.md` in this feature directory. The integrated Story 18.1 contract at `specs/016-contained-source-tools/contracts/prepared-source-tools.md` remains unchanged.

**Prerequisites**: Independent complete-definition review and separate implementation approval before production work. Use governed RED/GREEN for changed behavior, with separate test and production owners. Root owns checkpoint evidence and Git.

**Tests**: Behavioral tests are required for new source/tool authority and navigation behavior. Direct real-tool verification is separately required; a fake protocol or successful initialization does not qualify JDT-LS.

**Organization**: Foundation delivers the JDT-independent shared trusted-workspace seam. User Story 1 delivers navigation; User Story 2 protects its authority and location boundary; User Story 3 settles failure and concurrency. Final qualification exercises the real restricted profile and later prepared Repository.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run in parallel only when files and prerequisites do not overlap.
- **[Story]**: User story from `spec.md`.
- Every checklist line names the accepted design IDs it implements or verifies.

## Path Conventions

- Production: `src/dotnet/`.
- Tests: `tests/Vulgata.Tests/` with `tests/Vulgata.Tests/TestInfrastructure/suites.json` suite registration and guarded attributes.
- Feature validation instructions: `specs/18-2-navigate-prepared-java-source-with-restricted-jdt-ls/quickstart.md`.

## Phase 1: Setup (Existing Baseline)

**Purpose**: Confirm the integrated 18.1 boundary and owned test resources before behavioral work. No new project, package, schema or source copy is needed.

- [x] T001 Directly inspect `specs/016-contained-source-tools/contracts/prepared-source-tools.md`, `src/dotnet/Vulgata.Core/DomainServices/PreparedSourceContracts.cs` and `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs` against the accepted 18.2 contract; report the existing read-only list/read/search boundary, exact approval tuple and callable seam to root before RED authoring (direct verification; ID-L02, ID-I04).
- [x] T002 [P] Prepare owned fixture directories and inert test fixtures in `tests/Vulgata.Tests/AnalysisRuntime/PreparedJavaNavigationFixture.cs`, with a small Java declaration/caller Repository, a foreign root, a symlink/reparse escape and separate disposable tool state; classify the helper in `tests/Vulgata.Tests/TestInfrastructure/suites.json` (direct fixture verification; ID-T01, ID-T02).

---

## Phase 2: Foundational (Shared Trusted Workspace Checkpoint)

**Purpose**: Deliver Story 18.2's smallest JDT-independent Infrastructure workspace authority before either tool consumes it. The trusted caller, never a Worker argument, supplies the existing exact `PreparedSourceApproval` (batch, Repository, subject, `WorkerSourceRead` purpose, approval/root-binding identities, operator label), physical root, one selected `JdtNavigation` or `CodeGraphPreparation` purpose and deadline. `WorkerSourceRead` alone cannot authorize either tool.

- [x] T003 Write behavioral RED public-seam tests in `tests/Vulgata.Tests/PreparedSource/PreparedToolWorkspaceIssuerTests.cs` for the `PreparedToolWorkspaceIssuer` construction/`IssueAsync` status-plus-nullable-lease shape, mandatory current source/tool-purpose verifiers, exact tuple and selected purpose, owned held-root identity, trusted-only root access, deadline/cancellation and disposal; register the suite in `tests/Vulgata.Tests/TestInfrastructure/suites.json` (RED; ID-R01, ID-I04, ID-S03, ID-T03).
- [x] T004 Implement minimal GREEN `PreparedToolWorkspaceIssuer`, grant/status/lease and tool-purpose authorizer contracts in `src/dotnet/Vulgata.Infrastructure/Source/PreparedToolWorkspaceIssuer.cs`, preserving 18.1 `PreparedSourceContextIssuer`, `IPreparedSourceReadContext` and the three file functions unchanged; keep the root handle/path Infrastructure-only and no JDT dependency in the shared seam (GREEN; ID-R01, ID-I04, ID-S03, ID-L02).
- [x] T005 Extend RED tests in `tests/Vulgata.Tests/PreparedSource/PreparedToolWorkspaceIssuerTests.cs` for issuance, mid-use and pre-disclosure revocation/mismatch/unavailable source or purpose authority, concurrent buffered work, observed root change and terminal lease behavior: all affected calls release zero payload, close the lease and require new issuance (RED; ID-S03, ID-F01, ID-T02, ID-T03).
- [x] T006 Implement minimal GREEN current-authority checks, terminal closure/cancellation and bounded `CheckCurrentAsync` in `src/dotnet/Vulgata.Infrastructure/Source/PreparedToolWorkspaceIssuer.cs`; do not cache allow decisions or let a `CodeGraphPreparation` lease borrow `JdtNavigation` permission/lifetime (GREEN; ID-S03, ID-F01, ID-L02).
- [x] T007 Run focused public-seam tests and final-shape assessment for `tests/Vulgata.Tests/PreparedSource/PreparedToolWorkspaceIssuerTests.cs` and `src/dotnet/Vulgata.Infrastructure/Source/PreparedToolWorkspaceIssuer.cs`; directly verify the JDT-independent contract and report an identifiable verified checkpoint to root before any separately authorized Story 18.3 consumption (GREEN/checkpoint verification; ID-I04, ID-L02, ID-T03).

**Checkpoint**: Root may identify the shared seam for a later separately authorized 18.3 integration only after T003–T007 pass and are reviewed. This checkpoint does not qualify CodeGraph or JDT-LS subprocess containment.

---

## Phase 3: User Story 1 - Navigate a Granted Repository (P1 MVP)

**Goal**: A Worker obtains real definition/reference observations from one approved whole prepared Java Repository with explicit limitations, never an absence inference.

**Independent Test**: Under an owned declaration/caller fixture, invoke both navigation functions and observe contained relative locations and honest no-result/limited outcomes. Real JDT-LS qualification remains T024.

- [ ] T008 [P] [US1] Write behavioral RED contract tests in `tests/Vulgata.Tests/AnalysisRuntime/PreparedJavaNavigationFunctionsTests.cs` for `IPreparedJavaNavigationContext`, `PreparedJavaNavigationFunctions.Create()` returning exactly `navigate_java_definition` and `navigate_java_references`, canonical relative Java path, one-based line/character, bounded `MaximumLocations`, forwarded cancellation, borrowed-context disposal and sanitized result fields; register the suite in `tests/Vulgata.Tests/TestInfrastructure/suites.json` (RED; ID-R02, ID-I01, ID-I02, ID-T02).
- [ ] T009 [US1] Implement minimal GREEN request/result/operation/outcome/limitation types and context contract in `src/dotnet/Vulgata.Core/DomainServices/PreparedJavaNavigationContracts.cs`, plus the two closed Agent functions in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedJavaNavigationFunctions.cs`; no grant issuance, physical path, executable or adapter selector may enter Worker arguments (GREEN; ID-R02, ID-I01, ID-I02, ID-L02).
- [ ] T010 [P] [US1] Write behavioral RED adapter tests in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsPreparedNavigationTests.cs` for one whole prepared-root `rootUri`, separate disposable `-data`/configuration, explicit config-file Java/JDT installation, fixed disabled Maven/Gradle import/build/restore settings, missing-config failure, actual definition/reference mapping, no-result versus capped-result classification and classpath/capability limitations (RED; ID-I02, ID-I03, ID-S02, ID-L01, ID-T01).
- [ ] T011 [US1] Implement minimal GREEN prepared-root JDT-LS adapter and result normalization in `src/dotnet/Vulgata.Infrastructure/Analysis/Java/JdtLsAnalysisAdapter.cs` with `src/dotnet/Vulgata.Infrastructure/Analysis/Java/PreparedJdtLsNavigationContext.cs`; reuse stdio/protocol mechanics only after direct-root assessment, and reject the legacy Git-style `AnalysisScope`/prepared-revision copy as an October grant (GREEN; ID-R02, ID-I02, ID-I03, ID-S02, ID-L01).
- [ ] T012 [US1] Write behavioral RED composition tests in `tests/Vulgata.Tests/AnalysisRuntime/PreparedJavaNavigationCompositionTests.cs` for trusted application-selected approval, tool purpose, fixed profile and issued lease, plus independently usable 18.1 file functions when JDT navigation is limited; register the suite in `tests/Vulgata.Tests/TestInfrastructure/suites.json` (RED; ID-R01, ID-I01, ID-F02, ID-L02).
- [ ] T013 [US1] Implement minimal GREEN October navigation composition in `src/dotnet/Vulgata.Web/Program.cs` and `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedJavaNavigationFunctions.cs`, selecting executable through application config files and failing closed when absent; do not discover ordinary `.env` files or change 18.1 file-tool behavior (GREEN; ID-R01, ID-I01, ID-I03, ID-F02, ID-L02).

**Checkpoint**: US1 controlled tests pass through the public navigation and composition seams; record that real restricted-profile behavior remains unqualified until the final phase.

---

## Phase 4: User Story 2 - Keep Navigation Within Its Grant (P2)

**Goal**: All inputs, outputs and session reuse remain within the live application grant and selected Repository.

**Independent Test**: Foreign Repository/root, traversal, symlink escape, unmappable/outside URI and authority-loss cases disclose no outside location or buffered detail.

- [ ] T014 [US2] Write behavioral RED adversarial tests in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsPreparedNavigationTests.cs` for absolute/foreign/traversing/symlink-escaping input, unmappable/outside-root returned URI, changed prepared input, conflicting root/profile reuse and invalid Worker selectors; assert zero physical-path/source disclosure and sanitized limitations (RED; ID-R01, ID-I01, ID-I02, ID-S01, ID-T02).
- [ ] T015 [US2] Implement minimal GREEN contained input validation, held-root-relative output mapping and binding-key checks in `src/dotnet/Vulgata.Infrastructure/Analysis/Java/PreparedJdtLsNavigationContext.cs` and `src/dotnet/Vulgata.Infrastructure/Analysis/Java/JdtLsAnalysisAdapter.cs`; withhold unsafe URIs rather than turning them into missing relationship evidence (GREEN; ID-R02, ID-I02, ID-S01, ID-L01).
- [ ] T016 [US2] Write behavioral RED multi-request authority tests in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsPreparedNavigationTests.cs` for issuance, mid-use and immediately pre-disclosure source/purpose revocation, mismatch, verifier unavailability, expiry and disposal; every affected concurrent response must discard buffered locations/counts/limits and a restored approval must require new issuance (RED; ID-S03, ID-F01, ID-T02).
- [ ] T017 [US2] Implement minimal GREEN exact approval-plus-`JdtNavigation` rechecks, latch closure and payload discard in `src/dotnet/Vulgata.Infrastructure/Analysis/Java/PreparedJdtLsNavigationContext.cs`; sanitize denied/authority-unavailable outcomes and settle owned activity without exposing subject, approval identity, host path or outside metadata (GREEN; ID-R01, ID-S03, ID-F01, ID-L02).

**Checkpoint**: US2 controlled adversarial cases pass. A trusted path lease and filtered URI still do not prove subprocess confinement.

---

## Phase 5: User Story 3 - Recover From Limited or Failed Navigation (P3)

**Goal**: One bounded owned session per Repository, serialized unsafe protocol work and truthful terminal behavior under cancellation, failure and limitation.

**Independent Test**: Concurrent and failing requests finish with explicit outcomes; owned process/tool state settles, unrelated processes remain untouched, and 18.1 file tools remain independently usable.

- [ ] T018 [US3] Write behavioral RED session tests in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsPreparedNavigationTests.cs` for overlapping Workers, one session per selected Repository, serialized unsafe calls, bounded acquisition/queue/readiness/request/authority/shutdown, waiting and active cancellation, timeout, late reply and unhealthy-session retirement (RED; ID-S01, ID-A01, ID-T02).
- [ ] T019 [US3] Implement minimal GREEN bounded session owner, serialized protocol queue and terminal cleanup in `src/dotnet/Vulgata.Infrastructure/Analysis/Java/PreparedJdtLsNavigationContext.cs` and `src/dotnet/Vulgata.Infrastructure/Analysis/Java/JdtLsAnalysisAdapter.cs`; a timed-out or unhealthy session must settle before another attempt and must not terminate unrelated processes (GREEN; ID-S01, ID-A01, ID-F02, ID-L01).
- [ ] T020 [US3] Write behavioral RED failure tests in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsPreparedNavigationTests.cs` for missing installation, readiness/protocol failure, incomplete classpath, cancellation and deadline outcomes; verify no-result is not absence, no automatic root/adapter switch, no dependency restore and no uncertain-result replay (RED; ID-R02, ID-I02, ID-F02, ID-T02).
- [ ] T021 [US3] Implement minimal GREEN explicit failure/limitation classification and settled same-grant new-attempt policy in `src/dotnet/Vulgata.Infrastructure/Analysis/Java/PreparedJdtLsNavigationContext.cs`; keep the separately granted 18.1 file tools available and native CodeGraph independently required (GREEN; ID-R02, ID-I02, ID-F02, ID-L01).

**Checkpoint**: US1–US3 controlled behavior passes; proceed to direct real-tool qualification before claiming Story 18.2 acceptance.

---

## Phase 6: Real Tool Qualification and Final Shape

**Purpose**: Establish actual JDT-LS behavior and host protection on owned source, then on the user-prepared registration/login Repository when it is supplied and authorized.

- [ ] T022 Write behavioral RED launch-boundary tests in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsPreparedNavigationTests.cs` for rejecting an unsafe process profile, selecting prepared source read-only, using separate writable tool state and withholding unrelated host files except declared Java/JDT runtime inputs (RED; ID-S02, ID-F03, ID-T02, ID-T03).
- [ ] T023 Implement minimal GREEN launch enforcement in `src/dotnet/Vulgata.Infrastructure/Analysis/Java/JdtLsAnalysisAdapter.cs` and its owned process-launch helper after T022's failing observation; do not launch when the declared host cannot enforce source read-only and least-privilege process access (GREEN; ID-S02, ID-F03, ID-T03).
- [ ] T024 Qualify the real restricted profile in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsRealQualificationTests.cs` against the owned declaration/caller fixture from `tests/Vulgata.Tests/AnalysisRuntime/PreparedJavaNavigationFixture.cs`: load `JDT_LS_BIN` only in this real test from its local `.env`, exercise actual definition and reference calls, inspect effective no-import/no-build/no-restore behavior, compare source before/after for writes, and probe unrelated-file reads and child-process access under the declared host boundary; record actual source/profile/results separately from initialization. If useful navigation, no-write or subprocess containment fails, return a design finding rather than copying or modifying source (real-tool qualification, not simulated GREEN; ID-I03, ID-S02, ID-F03, ID-T01, ID-T03).
- [ ] T025 When the user-prepared registration/login Repository is actually supplied and access authorized, repeat real definition/reference navigation and restricted-profile/no-write/process checks via `tests/Vulgata.Tests/AnalysisRuntime/JdtLsRealQualificationTests.cs` and `specs/18-2-navigate-prepared-java-source-with-restricted-jdt-ls/quickstart.md`; report fixture and prepared-Repository outcomes separately. Until supplied, record this as an explicit dependent qualification, not a pass (real-tool qualification; ID-T01, ID-T03).
- [ ] T026 Run focused affected suites, configured SelfContained regression suite and direct final-shape review of `src/dotnet/Vulgata.Infrastructure/Source/PreparedToolWorkspaceIssuer.cs`, `src/dotnet/Vulgata.Infrastructure/Analysis/Java/PreparedJdtLsNavigationContext.cs`, `src/dotnet/Vulgata.Infrastructure/Analysis/Java/JdtLsAnalysisAdapter.cs`, `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedJavaNavigationFunctions.cs` and their tests; classify executed versus excluded qualification, preserve historical 18.1 behavior, and investigate Windows native root-open failures under the same launcher/permission mode before changing containment (final GREEN/direct verification; ID-L01, ID-L02, ID-T02, ID-T03).

---

## Dependencies & Execution Order

### Phase Dependencies

1. T001–T002 confirm current 18.1/fixture boundaries.
2. T003–T007 deliver and verify the JDT-independent shared workspace seam. Root identifies the checkpoint; Story 18.3 consumption requires separate authorization and its own integration/containment qualification.
3. T008–T013 deliver US1 navigation on the shared seam. T014–T017 harden the same session for US2; T018–T021 settle US3 failures and concurrency.
4. T022–T024 qualify actual restricted fixture behavior before any claim of useful JDT navigation. T025 waits for the user-prepared Repository and access authorization. T026 assesses final shape and applicable regressions after affected code and required available qualification settle.

### User Story Dependencies

- **US1 (P1)**: Starts after the shared workspace checkpoint and can be tested with the owned fixture; real JDT acceptance depends on T024.
- **US2 (P2)**: Uses US1's issued navigation context and mapping; adversarial tests are independently observable through that public seam.
- **US3 (P3)**: Uses the same accepted context/session; concurrent and terminal cases are independently observable without a database, provider or CodeGraph.

### Within Each Behavioral Slice

- The test owner records an honest failing RED at the accepted public seam before the production owner changes behavior, then the production owner reaches minimal GREEN. Both assess final affected units; refactor only when a concrete issue warrants it and retain relevant GREEN evidence.
- T001–T002, T007, T024–T026 use direct or real-tool verification rather than invented RED. Existing adequate coverage is reused. Tests and production code have separate owners under the governed route.
- Every C# test/helper added under `tests/Vulgata.Tests/` must be suite-classified in `tests/Vulgata.Tests/TestInfrastructure/suites.json` with guarded attributes; real JDT/native process tests remain qualification, not SelfContained.

### Parallel Opportunities

- T001 authority review and T002 owned fixture preparation may proceed independently before RED groups; T003–T007 must remain ordered because the shared seam and its verified checkpoint are one ownership boundary.
- Test authors may prepare US1 and US2 scenario fixtures in separate files, but production edits to `JdtLsAnalysisAdapter.cs` and `PreparedJdtLsNavigationContext.cs` are sequential to avoid conflicting shared session behavior.
- The prepared registration/login Repository may be arranged in parallel with controlled implementation, but T025 cannot claim a result until its input and authorization exist.

## Parallel Example: User Story 1

```text
After T007, the test owner may prepare T008 in PreparedJavaNavigationFunctionsTests.cs
and T010 in JdtLsPreparedNavigationTests.cs in parallel, then record each honest RED.
The production owner applies T009 and T011 only after the matching RED and keeps
their disjoint Core/Agents and Infrastructure file ownership clear.
```

## Implementation Strategy

### MVP First (US1)

Complete T001–T013, including the verified shared workspace checkpoint, then exercise US1 through the public navigation interface. This is a controlled MVP; real restricted-profile acceptance still requires T022–T024.

### Incremental Delivery

Complete US2 containment (T014–T017) and US3 failure/concurrency (T018–T021) before real-tool claims. Qualify the fixture and declared host boundary (T022–T024), then the user-prepared Repository only when available and authorized (T025). Finish with final-shape and regression evidence (T026). A failed no-write, process-containment or useful-navigation check returns to definition/architecture and affected gates.

## Notes

- No task changes Story 18.1's accepted issuer/context/functions, implements CodeGraph, starts Scan Control work or adds a source copy, Git identity, Knowledge schema, Docker package or general workspace framework.
- The accepted plan's public callable seam is the test target; a missing or materially different interface returns through root for definition correction before RED authoring.
- `docs/project-lessons.md` applies to callable seams and Windows native traversal: define construction/result/disposal/cancellation/arguments before tests, and compare native status plus effective launcher permissions before diagnosing a root-open failure.
