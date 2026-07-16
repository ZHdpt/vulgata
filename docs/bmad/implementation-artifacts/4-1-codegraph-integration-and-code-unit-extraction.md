---
story_key: 4-1-codegraph-integration-and-code-unit-extraction
title: Story 4.1: CodeGraph Integration and Code Unit Extraction
Status: in-progress
Epic: 4
Story: 1
created: 2026-07-14
depends_on:
  - sp-1-worker-agent-execution-adapter-validation-spike
  - 2-3-repository-management
references:
  - docs/bmad/epics.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - docs/bmad/implementation-artifacts/sp-1-spike-report.md
  - docs/bmad/planning-artifacts/document-graph-anchordb-plus-plan.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-10.md
  - src/dotnet/Vulgata.Core/Entities/Repository.cs
  - src/dotnet/Vulgata.Core/DomainServices/IScanStateService.cs
  - src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs
  - src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs
  - src/dotnet/Vulgata.Web/Components/Pages/Management/RepoDetail.razor
  - docker/Dockerfile
  - tests/Vulgata.Tests/RepositoryManagementTests.cs
---

# Story 4.1: CodeGraph Integration and Code Unit Extraction

## User story

As a `SystemOwner`, I want to start pre-scan profiling for a Repository from its
details page. The system clones or updates its source, obtains structural Code
Units and their call-graph edges through a local CodeGraph tool, and persists a
traceable parse result. Later Worker Agents therefore receive bounded source
segments and structural context rather than raw files.

## Scope and boundary

- Add the Vulgata-owned domain, persistence, Git working-copy, CodeGraph CLI
  integration, authorized start action, and minimal user-visible outcome needed
  by Story 4.1.
- Use the locally runnable **CodeGraphContext** `cgc` CLI as the CodeGraph
  provider. Vulgata must translate provider output through an owned boundary;
  it must not depend directly on the provider's internal graph database schema.
- Keep source, the working copy, provider execution, and parsed data inside
  organisation-controlled infrastructure. This story adds no LLM call, MCP
  tool, Worker Agent, orchestration dispatch, document generation, framework
  inference, or full scan dashboard.
- `IScanStateService` remains the existing Epic 5 seam. Do not introduce a
  `ScanCoordinator` or Worker dispatch lifecycle here.
- The details-page action and every API boundary must preserve existing
  Repository visibility and management authorization rules. All user-visible
  text added by this story is Simplified Chinese.

## Authoritative decisions — user-authorized 2026-07-14

These decisions resolve conflicts among the Epic, planning artifacts, and
implementation baseline. They are binding for this story and its downstream
SpecKit artifacts.

1. `file_identity_hash` is SHA-256 over the first `min(4096, file length)`
   source bytes, not a canonical-path hash. Its purpose is rename detection;
   `source_hash` remains the full source-content SHA-256 for change detection.
2. `RangeConfidence` has exactly `Exact`, `Approximate`, and `Heuristic`.
   Older `High`/`Medium`/`Low` wording is not a storage contract.
3. The domain aggregate is `ScanRun`; its database table is `ScanRuns`, using
   the repository's established PascalCase database naming. References to a
   conceptual `scans` store in earlier planning material must be reconciled in
   the plan, not implemented as a competing model.
4. CodeGraph is an approved external local dependency. The implementation
   target is CodeGraphContext `cgc`; its exact reproducible installation,
   invocation, and version pin belong in the plan and Docker change.

## Acceptance criteria

### AC-1: Start and capture a CodeGraph parse

**Given** a Repository has a validated Git URL and an authorised SystemOwner
**When** the owner selects `开始扫描` from RepoDetail
**Then** the system clones or updates a local working copy using a Git service
**And** invokes CodeGraph in approximately 1,000-file batches with a
configurable per-file timeout whose default is 30 seconds
**And** captures a `CodeGraphParseResult` containing extracted Code Units,
skipped files and reasons, errored files and messages, parse duration, and total
files parsed.

### AC-2: Validate and isolate parse failures

**Given** CodeGraph output is received
**When** Vulgata validates it
**Then** malformed entries are rejected and logged at Warning level with file
path context
**And** hangs or crashes are isolated as skipped files with an error reason so
remaining work continues
**And** zero Code Units from 500 or more candidate source files logs a warning
and flags the ScanRun for human review.

### AC-3: Preserve filtered files for audit

**Given** a source candidate is generated, vendored, binary, minified
JavaScript, an auto-generated protobuf/Thrift stub, or excluded by `.gitignore`
**When** filtering is applied
**Then** it is persisted as `Filtered`, with audit information, rather than
silently deleted.

### AC-4: Persist extracted Code Units

**Given** a valid Code Unit is extracted
**When** its result is stored
**Then** it has a UUID id, `ScanRun` and Repository foreign keys, file path,
file identity hash, source hash, line start/end, symbol name, mapped unit type,
`RangeConfidence`, nullable language for Story 4.2, and lifecycle status
`Extracted -> Pending -> Dispatched -> Completed | Failed | Skipped`
**And** non-filtered Code Units begin in `Pending` while `Filtered` is terminal.

### AC-5: Persist structural edges

**Given** CodeGraph produces same-Repository structural relationships
**When** they are stored
**Then** each Code Unit Edge has a UUID id, source and target Code Unit keys,
`Calls | Imports | Inherits | Contains`, and `IsResolved`
**And** unresolved relationships remain available as later Cross-Repository
resolution candidates.

### AC-6: Large-repository observability

**Given** a Repository has 10,000 source files
**When** profiling runs
**Then** extraction does not block the future scan pipeline
**And** a minimal progress outcome exposes parsed and total file counts; Epic 8
owns the full live dashboard.

### AC-7: Missing CodeGraph executable

**Given** CodeGraph is unavailable in the application container
**When** an authorised user starts profiling
**Then** Vulgata logs an Error containing `CodeGraph CLI not found`, aborts the
ScanRun, and shows a clear Simplified-Chinese error.

### AC-8: Repository clone failure

**Given** cloning or updating the Repository fails due to authentication or
network failure
**When** profiling starts
**Then** Vulgata logs the failure, marks the ScanRun as failed, and displays
`仓库克隆失败：{details}` to the SystemOwner.

## Required planning decision and approval gate

The selected provider exposes repository-level indexing, not a documented
per-file batch or timeout contract. The implementation plan must therefore
state, test, and justify the proposed Vulgata-side scanning strategy for:

- forming approximately 1,000-file batches without changing source semantics;
- enforcing the 30-second default timeout and terminating an isolated hung
  process safely;
- continuing after a failed batch or malformed provider result; and
- mapping provider graph nodes and relationships into Vulgata contracts.

This is a **scanning-strategy change** under `AGENTS.md`. After plan review
accepts that strategy, the workflow must explain its intent and effects to the
user and obtain explicit approval before task generation or implementation.

## Implementation guardrails

- Keep Core free of EF Core and infrastructure references. Use aggregate-specific
  repositories; do not expose `IQueryable` or `DbSet` outside Infrastructure.
- Add one EF configuration per aggregate in Infrastructure and preserve separate
  Identity and domain migration histories.
- Validate all external/process and API input with FluentValidation. Use
  `ProblemDetails` for API errors and do not weaken existing API auth handling.
- Never log source contents, credentials, or tokens. Process diagnostics must
  be bounded and redact secrets.
- All async methods use the `Async` suffix. Tests mirror source structure and
  use the existing WebApplicationFactory SQLite dual-context harness.

## Existing integration context

- `Repository` already validates its Git URL; the current Git remote validator
  uses `git ls-remote` and is not a clone/update implementation.
- `RepositoryManagementCoordinator`, its API surface, and RepoDetail are the
  existing management path. Reuse their authorization and visibility behavior.
- `VulgataDbContext` currently has no `ScanRun`, Code Unit, or Code Unit Edge
  set. The planned model is additive and must be migration-backed.
- `docker/Dockerfile` currently contains a deterministic `codegraph` shim;
  replace it only with the approved reproducible `cgc` installation selected
  in the plan.

## Story completion evidence

- Unit and integration tests cover the domain lifecycle, provider-result
  mapping, filtering, process failure handling, Git failure, authorization,
  persistence constraints, and Chinese user-visible errors.
- A deterministic end-to-end test uses a local Git fixture and a controlled
  CodeGraph process fixture; it proves the authorised RepoDetail/API start path
  persists a ScanRun, Code Units, and Code Unit Edges without a network or LLM.
- The final quality gate runs the project build and the full test project, and
  records the exact commands and results in the implementation evidence.

## Decision log

| Date | Decision | Authority | Effect |
| --- | --- | --- | --- |
| 2026-07-14 | Treat SP-1 as complete for Epic 4 entry. | User direction and committed SP-1 report/status | No further pre-epic spike is required for Story 4.1. |
| 2026-07-14 | Use content-prefix file identity, Exact/Approximate/Heuristic, and ScanRun/ScanRuns. | User direction | Resolves conflicting Epic and planning wording. |
| 2026-07-14 | Add CodeGraphContext `cgc` as the local CodeGraph dependency. | User approval | Requires an approved, reproducible execution plan. |

## Dev Agent Record

### Context reference

- Canonical activation: `docs/bmad/implementation-artifacts/workflow-activations/4-1-codegraph-integration-and-code-unit-extraction.json`
- SpecKit feature directory: `specs/001-codegraph-extraction`.
- Current workflow stage: `tasks`; the complete plan regenerated from corrected FR-018 has fresh independent acceptance, and task regeneration is next.
- Latest workflow checkpoint: `bmad-4-1-plan-review-20260716T090000Z-c3d912` (pending local commit binding).
- Authorization: analysis finding I1 remediation is accepted through specification and plan review. The canonical PRD remains unchanged; implementation stays blocked until fresh task generation and read-only analysis accept the regenerated boundary.

### Completion notes

- Not started.
