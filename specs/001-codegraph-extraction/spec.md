# Feature Specification: CodeGraph Code Unit Extraction

**Feature Branch**: `001-codegraph-extraction`
**Created**: 2026-07-14
**Status**: Draft
**Input**: BMad Story `4-1-codegraph-integration-and-code-unit-extraction`: enable an authorised SystemOwner to start local pre-scan profiling for a Repository, persist traceable Code Units and same-Repository structural relationships, and show a minimal outcome without introducing Worker Agent orchestration.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start Repository Profiling (Priority: P1)

An authorised SystemOwner starts pre-scan profiling from a Repository's details. The system obtains the Repository source inside organisation-controlled infrastructure, extracts structural Code Units and same-Repository relationships, and records a traceable Scan Run with minimal parsed/total progress and outcome information.

**Why this priority**: It is the prerequisite for bounded, structural source context in every later scan stage; without it, no Code Units exist for subsequent work.

**Independent Test**: Using a local Repository fixture containing supported source files and at least one structural relationship, an authorised SystemOwner starts profiling and verifies that one Scan Run, the expected Code Units, and the expected Code Unit Edge are recorded and associated with that Repository.

**Acceptance Scenarios**:

1. **Given** a visible Repository with a validated Git URL and an authorised SystemOwner, **When** the SystemOwner selects `开始扫描` from its repository details, **Then** profiling starts and the SystemOwner can see a minimal result containing parsed and total file counts.
2. **Given** a valid local Repository contains extractable symbols and a same-Repository structural relationship, **When** profiling completes, **Then** the resulting Scan Run contains traceable Code Units and Code Unit Edges for those source structures.
3. **Given** a Repository contains more than approximately 1,000 candidate source files, **When** profiling runs, **Then** candidates are handled in approximately 1,000-file groups with a configurable per-file time limit that defaults to 30 seconds, while the chosen mechanism remains subject to the mandatory planning approval gate.
4. **Given** a Repository contains 10,000 candidate source files, **When** profiling runs, **Then** the result retains parsed and total file counts without starting Worker Agent work, document generation, or a full scan dashboard.

---

### User Story 2 - Audit Incomplete and Filtered Results (Priority: P2)

An authorised SystemOwner can rely on a Scan Run to distinguish extracted Code Units from excluded, malformed, failed, or unresolved source results, so that profiling is auditable instead of silently losing information.

**Why this priority**: Source inspection is valuable only when omissions and uncertain structural data remain visible for review.

**Independent Test**: Using a local Repository fixture containing generated, vendored, binary, minified JavaScript, generated protobuf or Thrift, `.gitignore`-excluded, malformed, and relationship-without-target cases, verify that every relevant result has an auditable outcome, that a relationship without a persisted target is recorded as a candidate rather than an incomplete Code Unit Edge, and that valid results continue to be recorded.

**Acceptance Scenarios**:

1. **Given** a source candidate matches an exclusion rule, **When** filtering occurs, **Then** the outcome is preserved as `Filtered` with its reason rather than deleted.
2. **Given** the structural provider returns a malformed entry for one candidate, **When** the result is validated, **Then** that entry is rejected with a warning containing file-path context and other valid results remain available.
3. **Given** a validated relationship has a persisted source Code Unit but no target Code Unit in the same Repository and Scan Run, **When** relationships are recorded, **Then** exactly one Unresolved Structural Relationship Candidate is persisted with its UUID, Scan Run, Repository, source Code Unit identity, relationship kind, and non-empty provider target endpoint identity; no Code Unit Edge with a missing target, Document, or Uncertainty is created, and Cross-Repository Resolution is not run.
4. **Given** 500 or more candidate source files yield zero Code Units, **When** the Scan Run completes, **Then** it is flagged for human review and a warning is recorded.

---

### User Story 3 - Receive Safe Failure Feedback (Priority: P3)

An authorised SystemOwner receives clear Simplified-Chinese feedback when source acquisition or the local structural provider cannot run, while unauthorized users cannot start or inspect profiling for a Repository they cannot manage.

**Why this priority**: Profiling handles proprietary source and external processes; failures and access controls must be safe and actionable.

**Independent Test**: Inject source-acquisition and provider-unavailable failures and attempt the same action as both an authorised and unauthorised user; verify the recorded Scan Run outcome, Chinese message, and absence of source contents or credentials in responses and logs.

**Acceptance Scenarios**:

1. **Given** the approved local CodeGraph provider is unavailable, **When** an authorised SystemOwner starts profiling, **Then** the Scan Run is aborted, an error containing `CodeGraph CLI not found` is recorded, and the user receives a clear Simplified-Chinese error.
2. **Given** source acquisition fails because of a network or authentication error, **When** profiling starts, **Then** the Scan Run is recorded as failed and the user sees `仓库克隆失败：{details}` without credentials or source contents.
3. **Given** a user cannot manage a Repository, **When** that user attempts to start or inspect its profiling outcome, **Then** the action returns the existing access-constrained outcome and creates no Scan Run.

### Edge Cases

- A source file shorter than 4,096 bytes still receives a File Identity Hash based on all available source bytes.
- A provider process hangs or crashes while handling a candidate: the affected candidate is recorded as skipped with its error reason, and eligible remaining candidates continue according to the subsequently approved scanning strategy.
- Every candidate is filtered: the Scan Run completes with auditable filtered outcomes and no non-filtered Code Unit is incorrectly marked `Pending`.
- A provider record lacks a valid path, range, symbol, or relationship endpoint: it is rejected rather than persisted as structurally valid data. A relationship without a persisted target Code Unit is eligible for an Unresolved Structural Relationship Candidate only when its provider target endpoint identity is non-empty and stable enough to audit; otherwise it is rejected as malformed.
- A source file produces a valid Code Unit but an unsupported structural relationship kind: the Code Unit remains available and the unsupported relationship is recorded as an auditable error or omission.
- Repository visibility changes before an action completes: the result must not expose source-derived details to a user who no longer has access.
- A repeated provider result would create the same Code Unit, Code Unit Edge, or Unresolved Structural Relationship Candidate twice: persistence preserves one unambiguous structural record for the Scan Run.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow an authorised SystemOwner to start pre-scan profiling for a visible Repository from its repository details using the Simplified-Chinese action `开始扫描`.
- **FR-002**: The system MUST obtain a local working copy of the Repository by cloning it or updating an existing working copy, and MUST keep the source, working copy, provider execution, and parsed data within organisation-controlled infrastructure.
- **FR-003**: The system MUST use the user-approved local CodeGraphContext `cgc` provider through a Vulgata-owned integration contract that consumes structural results without depending on the provider's internal graph-store schema.
- **FR-004**: For each profiling attempt, the system MUST create a Scan Run linked to exactly one Repository and record its progress outcome, parsed-file count, total candidate-file count, duration, extracted Code Units, resolved Code Unit Edges, Unresolved Structural Relationship Candidates, filtered or skipped results with reasons, errored results with messages, and any human-review flag.
- **FR-005**: The system MUST preserve every candidate excluded because it is generated code, in a vendored directory, binary, minified JavaScript, an auto-generated protobuf or Thrift stub, or in a `.gitignore`-excluded path as an auditable `Filtered` result rather than silently deleting it.
- **FR-006**: The system MUST persist each valid Code Unit with a UUID, its Scan Run and Repository associations, repository-relative file path, File Identity Hash, source hash, line start and end, symbol name, mapped unit type, Range Confidence, nullable language, and lifecycle status.
- **FR-007**: A Code Unit's File Identity Hash MUST be SHA-256 over the first `min(4096, file length)` source bytes for future rename detection; its source hash MUST be SHA-256 over the complete source-file contents for future change detection.
- **FR-008**: The system MUST use exactly `Exact`, `Approximate`, and `Heuristic` as Range Confidence values. It MUST NOT persist the superseded `High`, `Medium`, or `Low` vocabulary.
- **FR-009**: Every non-filtered extracted Code Unit MUST begin in `Pending`; `Filtered` is terminal, and the lifecycle vocabulary MUST support `Extracted`, `Pending`, `Dispatched`, `Completed`, `Failed`, and `Skipped` without starting dispatch in this story.
- **FR-010**: The system MUST persist a same-Repository Code Unit Edge only when both its source and target Code Units have been persisted for the same Repository and Scan Run. Each Code Unit Edge MUST have a UUID, source and target Code Unit identities, one of `Calls`, `Imports`, `Inherits`, or `Contains`, and a resolved indicator set to `true`. A validated relationship whose source Code Unit is persisted but whose target Code Unit is absent MUST instead persist exactly one Unresolved Structural Relationship Candidate with a UUID, Scan Run identity, Repository identity, source Code Unit identity, relationship kind, and a non-empty provider target endpoint identity. The provider target endpoint identity MUST be stable structural metadata supplied by the provider—its target path, symbol, range, or another provider-normalized endpoint key—and MUST NOT contain source contents or credentials. The candidate MUST have no target Code Unit identity and MUST NOT be represented as a partial Code Unit Edge. It records a later-resolution candidate only: this story MUST NOT identify a target Repository, persist an Uncertainty, create a Document or Edge, or execute Cross-Repository Resolution.
- **FR-011**: The system MUST validate provider output before persistence. It MUST reject malformed entries, record a warning with file-path context, and retain auditable skipped or errored outcomes without exposing source contents, credentials, tokens, or unbounded process diagnostics.
- **FR-012**: The system MUST flag a Scan Run for human review and record a warning when 500 or more candidate source files produce zero Code Units.
- **FR-013**: The system MUST provide a minimal authorised progress and outcome view containing parsed and total file counts. A full live scan dashboard is not required by this feature.
- **FR-014**: If the local CodeGraph provider is unavailable, the system MUST abort the Scan Run, record an error containing `CodeGraph CLI not found`, and show a clear Simplified-Chinese error to the authorised user.
- **FR-015**: If cloning or updating a Repository fails, the system MUST mark the Scan Run as failed, record the failure without secrets, and show `仓库克隆失败：{details}` to the authorised SystemOwner.
- **FR-016**: The system MUST preserve the existing Repository visibility and management-authorization rules for the profiling action, result access, and all related API boundaries. An unauthorised attempt MUST create no Scan Run and return the established access-constrained response.
- **FR-017**: This feature MUST not invoke an LLM, expose an MCP tool, add or dispatch a Worker Agent, perform document generation, persist an Uncertainty, infer language or frameworks, perform communication-pattern detection, run Cross-Repository Resolution, or implement a Scan Coordinator.
- **FR-018**: The system MUST meet the required approximately 1,000-file grouping, configurable per-file time limit with a 30-second default, isolated hang/crash outcome, and continuation behavior for eligible remaining candidates. The specification requires these observable outcomes but deliberately does not choose the scanning mechanism.

### Key Entities

- **Scan Run**: One auditable pre-scan profiling attempt for exactly one Repository, including progress, terminal outcome, duration, counts, warnings, and human-review status.
- **CodeGraph Parse Result**: The bounded structural outcome of one Scan Run: extracted Code Units, resolved Code Unit Edges, Unresolved Structural Relationship Candidates, filtered/skipped/errored source results, counts, and duration.
- **Code Unit**: A line-range-bounded structural source segment in a Repository, associated with a Scan Run and carrying source hashes, symbol metadata, Range Confidence, and lifecycle status.
- **Code Unit Edge**: A directed same-Repository structural relationship between two persisted Code Units in the same Scan Run, including relationship kind and a `true` resolved status. It cannot have a missing target Code Unit identity.
- **Unresolved Structural Relationship Candidate**: An auditable, scan-local record for one validated relationship whose source Code Unit is persisted but whose target Code Unit is absent. It has its own UUID; Scan Run, Repository, and source Code Unit identities; relationship kind; and non-empty provider target endpoint identity. It is neither a Code Unit Edge nor the glossary-defined Uncertainty, and it records a candidate only without Cross-Repository Resolution.
- **Filtered Result**: The auditable terminal record for a source candidate excluded from extraction and the reason for its exclusion.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, 100% of authorised start actions against valid local Repository fixtures create one traceable Scan Run and present parsed and total file counts without requiring a user to run a command-line tool.
- **SC-002**: In a fixture containing known symbols, one resolved same-Repository relationship, one relationship whose target Code Unit is absent, and excluded candidates, 100% of expected valid Code Units and resolved Code Unit Edges are associated with the Scan Run; the target-absent relationship produces exactly one Unresolved Structural Relationship Candidate with every FR-010 identity and endpoint field; zero Code Unit Edges lack a target Code Unit identity; and 100% of excluded candidates have an auditable `Filtered` outcome with a reason. The fixture creates zero Documents, Uncertainties, and Cross-Repository Resolution results.
- **SC-003**: In injected missing-provider, source-acquisition-failure, malformed-record, and isolated-hang/crash scenarios, 100% of outcomes record the required failure or warning state; valid unaffected candidates remain processable; and test assertions find no source contents, credentials, or tokens in user-visible error details.
- **SC-004**: In a 10,000-candidate-source-file fixture, the recorded and user-visible result includes both parsed and total file counts and creates zero Worker Agent jobs, documents, or full-dashboard work items.
- **SC-005**: In authorization acceptance tests, 100% of attempts by users lacking Repository management visibility create zero Scan Runs and receive the repository's established access-constrained response.

## Assumptions

- Existing Repository Git-URL validation, management authorization, and visible-Repository semantics remain the authority for who may start and inspect profiling.
- CodeGraphContext `cgc` is an approved local dependency; its reproducible installation and execution details will be selected in the implementation plan, not by this specification.
- A Scan Run is the Story 4.1 aggregate name and `ScanRuns` is the intended persistence name; earlier references to a conceptual `scans` store are superseded for this feature.
- Language remains nullable in Story 4.1; deterministic language and framework detection belongs to Story 4.2.
- The specification defines the required outcomes for approximately 1,000-file grouping, a configurable per-file timeout defaulting to 30 seconds, failure isolation, and continuation. It deliberately does not select the Vulgata-side mechanism because the provider exposes no documented per-file batching or timeout contract.
- The existing no-op scan-state seam remains in place. This feature does not add a Scan Coordinator, dispatch lifecycle, or worker scheduling behavior.

## Out of Scope

- LLM, MCP, Worker Agent, deterministic Orchestrator, superstep, retry, scheduling, or dispatch implementation.
- Code Unit language/framework tagging, communication-type detection, and SystemOwner question gates.
- Document generation, Document Version persistence, Uncertainty persistence, document graph visualization, and Cross-Repository Resolution.
- A full live Scan dashboard, real-time notifications, or multi-Repository orchestration.
- Re-scan change detection and rename application beyond recording the File Identity Hash required for later work.

## Mandatory Planning Approval Gate

The next plan must propose the Vulgata-side scanning strategy needed to satisfy the approximately 1,000-file grouping, configurable 30-second per-file timeout, process isolation, continuation, and provider-result mapping outcomes. CodeGraphContext `cgc` offers repository-level indexing rather than a documented per-file batching/timeout interface.

Because this is a Scan strategy under `AGENTS.md`, an independent plan review must accept the plan and the root orchestrator must then obtain explicit user approval before task generation or implementation. That approval request must identify the strategy, explain its effects, state implementation intent, and cite this specification plus the affected PRD and plan material. This feature specification does not choose that strategy and does not authorize bypassing the gate.

## Source Traceability

- BMad Story 4.1 defines start action, CodeGraph extraction, filtering, Code Unit/edge fields, failures, and progress: `docs/bmad/implementation-artifacts/4-1-codegraph-integration-and-code-unit-extraction.md` and `docs/bmad/epics.md`.
- User-authorised Story 4.1 decisions establish content-prefix File Identity Hash, `Exact|Approximate|Heuristic`, `ScanRun`/`ScanRuns`, and local CodeGraphContext `cgc`.
- The canonical glossary defines Code Unit, Scan, File Identity Hash, and Cross-Repository Resolution: `docs/UBIQUITOUS_LANGUAGE.md`.
- Existing management source establishes Repository validation, visibility, API authorisation, and the repository-details dialog: `src/dotnet/Vulgata.Core/Entities/Repository.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs`, `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, and `src/dotnet/Vulgata.Web/Components/Management/Repositories/RepositoryDetailDialog.razor`.
- The current Docker image has only a `codegraph` shim, so a reproducible `cgc` installation remains planning work: `docker/Dockerfile`.
- The worker-agent execution spike is complete but is outside this feature's execution scope: `docs/bmad/implementation-artifacts/sp-1-spike-report.md`.
