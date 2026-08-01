---
story_key: 11-3-pass-1-and-pass-2-demo-document-generation
title: "Story 11.3: Pass 1 & Pass 2 Demo Document Generation"
Status: review
Epic: 11
Story: 3
created: 2026-08-01
execution_level: expedited-mvp
depends_on:
  - 11-1-minimal-knowledge-store-and-retrieval-projection
  - 11-2-sequential-scan-runner-and-typed-agent-dispatch
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md
  - docs/agent-workflow/august-15-expedited-mvp-exception.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - specs/004-minimal-knowledge-store/plan.md
  - specs/005-sequential-scan-runner/plan.md
---

# Story 11.3: Pass 1 & Pass 2 Demo Document Generation

## Story

As a **SystemOwner**,
I want bounded Worker Agents to generate technical and business explanations
for the prepared flow,
so that the August 15 demonstration produces source-grounded Code Logic
Documents and an understandable Business Logic Document.

## Scope and boundary

- Supply the production Worker Agent execution adapter behind the existing
  `IWorkerAgentDispatcher` port. The Sequential Scan Runner remains
  deterministic application code and continues to dispatch one job at a time.
- Add a narrow provider-independent Worker Agent result committer seam. The
  Sequential Scan Runner invokes it for a valid result before recording job
  progress; the committer materializes one authorization-scoped
  `KnowledgeCommit` and delegates to `IKnowledgeStoreRepository`.
- Reopen the closed `WorkerAgentResultPayload` allowlist deliberately for
  schema-validated Pass 1 Code Logic Document and Pass 2 Business Logic
  Document results. Do not introduce arbitrary payloads or diagnostics.
- Hydrate only the bounded source and context authorized by the packet's exact
  Repository, ScanRun, Code Unit, Profiled Revision, repository-relative path,
  line range, and hashes. Do not widen `WorkerAgentJobPacket` into an arbitrary
  raw-source carrier.
- Commit valid results through the existing `IKnowledgeStoreRepository` atomic
  boundary so immutable Document Versions, Source Anchors, permitted Edges,
  flow evidence, and retrieval projections remain consistent.
- Add exactly one prepared Pass 2 flow path for the demonstration. After all
  Pass 1 results are durably committed, the Sequential Scan Runner leaves the
  Scan at the existing nonterminal `Pass1Completed` boundary, constructs one
  closed Pass 2 job for an exact prepared-flow input, dispatches it through the
  same adapter, commits its valid result, and only then transitions the Scan to
  `Completed`. It does not implement general graph traversal or flow-group
  clustering.
- Persist `GENERATED_FROM` Edges from the new Business Logic Document Version
  to the exact Code Logic Document Versions used. Pass 2 must not invent base
  Code Logic `REFERENCES` Edges from narrative interpretation.
- Keep prompts externalized as embedded resources under
  `Vulgata.Agents/Prompts/`. Send only the source/context required for the job.
- Register the production dispatcher and Sequential Scan Runner through the
  existing composition root after its security and provider configuration
  boundaries are accepted.
- Defer bounded refinement, cross-verification waves, partial-failure
  continuation, retry/failover, concurrent supersteps, general Pass 2 trace
  planning and clustering, and general Cross-Repository Resolution. These are
  known limitations, not failing tests or completed Epic 5 requirements.

## Acceptance Criteria

### AC-1: Generate a source-grounded Code Logic Document in Pass 1

**Given** the Sequential Scan Runner dispatches a Pass 1 job for one prepared
Code Unit
**When** the Worker Agent adapter hydrates the packet's authorized bounded
source/context and executes the embedded Pass 1 prompt
**Then** it returns a closed, schema-validated Code Logic Document payload with
bounded content, source claims, Source Anchors, essential same-Repository
references, and communication evidence
**And** every identity, path, line range, source hash, File Identity Hash, and
Profiled Revision remains bound to the dispatched packet
**And** only the source/context required for that job crosses the provider
boundary.

### AC-2: Reject unsafe or invalid Worker Agent output atomically

**Given** a provider response is malformed, schema-invalid, unbounded,
identity-mismatched, contains a secret or absolute host path, cites source
outside the authorized packet, or cannot be mapped to the closed payload
**When** the adapter validates the response
**Then** it returns the corresponding bounded non-valid result without
exposing raw provider diagnostics
**And** the Sequential Scan Runner stops with its durable Chinese failure
behavior
**And** no Logical Document, Document Version, Source Anchor, Edge, flow
evidence, or retrieval projection from that invalid result is committed.

### AC-3: Commit valid Pass 1 Documents through the knowledge store

**Given** a Pass 1 result is schema-valid and bound to its Code Unit packet
**When** the result is accepted
**Then** one immutable Code Logic Document Version and its bounded content,
Source Anchors, permitted local `REFERENCES` Edges, communication evidence, and
retrieval projection are committed atomically through
`IKnowledgeStoreRepository`
**And** the Sequential Scan Runner invokes a narrow result committer with the
dispatched packet, validated result, and caller-supplied Repository access
scope, and records the job valid only after the commit succeeds
**And** retrying the same operation cannot create duplicate logical truth
**And** superseded Document Versions are preserved rather than mutated or
deleted.

### AC-4: Generate the one prepared Business Logic Document in Pass 2

**Given** the prepared Pass 1 Documents and one exact version-controlled flow
packet exist
**When** every Pass 1 result has committed and the Scan reaches the existing
nonterminal `Pass1Completed` boundary
**Then** the embedded Pass 2 prompt receives only the prepared trace evidence
and exact Code Logic Document Versions required for that flow
**And** the Sequential Scan Runner constructs and dispatches exactly one closed
Pass 2 Worker Agent job through `IWorkerAgentDispatcher`
**And** the closed, schema-validated Business Logic Document payload explains
the business process in language suitable for a non-technical reviewer
**And** the Scan transitions to `Completed` only after that result is committed
**And** it does not perform general flow discovery, path-state traversal, or
flow-group clustering.

### AC-5: Persist exact Pass 2 provenance and flow evidence

**Given** the Pass 2 payload is valid
**When** the Business Logic Document is committed
**Then** its immutable Document Version, sections, Source Anchors, prepared
flow evidence, and retrieval projection are committed atomically
**And** `GENERATED_FROM` Edges target exactly the Code Logic Document Versions
used by synthesis
**And** no new base Code Logic `REFERENCES` Edge is inferred from narrative
text.

### AC-6: Activate the accepted production execution path

**Given** the production dispatcher, bounded context hydration, result
materialization, and persistence path are registered
**When** the composition root resolves the Sequential Scan Runner
**Then** it resolves a production `IWorkerAgentDispatcher`, the existing
runner dependencies, and the existing knowledge-store boundary
**And** provider selection reuses the configured System/global LLM Provider
rules without logging or persisting plaintext API keys
**And** no public API, UI, Chat Agent tool, migration, retry/failover policy,
or multi-Repository scheduler is added by this story.

## Context resolutions and inherited constraints

1. **The August 15 replan is the active deadline authority.** The approved
   sprint change includes one prepared Pass 1/Pass 2 path and explicitly
   defers concurrency and advanced failure policies. Full V1 retry and
   partial-failure continuation remain post-demo requirements; this story does
   not silently delete or claim them complete.
2. **Story 11.1 is the only Document commit boundary.** Reuse
   `IKnowledgeStoreRepository`, `KnowledgeCommit`, and the existing materializer
   and validators. Do not create a second Document repository or mutate the
   Story 11.1 migration. A narrow Core result-committer port owns conversion of
   a validated result into `KnowledgeCommit`; its implementation uses the
   caller-supplied Repository access scope and completes before the runner
   records valid progress.
3. **Story 11.2 owns deterministic sequencing.** Reuse
   `SequentialScanRunner`, `IWorkerAgentDispatcher`, the packet/envelope types,
   and durable Scan execution. Do not introduce concurrency, leases, replay,
   retry, failover, or multi-Repository scheduling.
4. **Bounded source hydration is required.** The existing packet intentionally
   carries source identities and hashes rather than raw source. The production
   adapter must re-read only the exact revision-bound slice from a trusted
   Repository source seam and verify its hashes before provider execution.
5. **Pass 2 input is prepared, not discovered.** This story must add a closed
   `Pass2BusinessLogicDocument` job kind and a finite version-controlled
   prepared-flow input contract. The Sequential Scan Runner owns the bounded
   lifecycle extension: Pass 1 commits finish at `Pass1Completed`, one Pass 2
   job is dispatched and committed, then the Scan reaches `Completed`. General
   graph-first path-state planning and flow-group clustering stay in Story 5.8.
6. **Story 11.2 quality finding F003 is a precondition.** Before production DI
   makes `ScanExecutionRepository` reachable, its SQLite-safe transactional
   write reservation must match the accepted Story 11.1 pattern and receive
   focused regression coverage.
7. **No ADR conflict was found.** `docs/adr/` is absent at the pinned boundary;
   Constitution, AGENTS.md, the approved deadline change, and canonical
   scanning documents remain binding.

## Architecture and security guardrails

- Keep Core free of EF Core, provider SDK, and Infrastructure dependencies.
- Keep provider execution, bounded source hydration, schema parsing, and
  embedded prompt loading in `Vulgata.Agents`; keep persistence implementations
  in Infrastructure and composition in Web.
- Use the centrally pinned Microsoft Agent Framework/OpenAI-compatible stack;
  add no unreviewed provider abstraction or hardcoded prompt string.
- Use structured output or a strict equivalent closed schema. Reject unknown
  fields, invalid enum values, duplicate identifiers, out-of-order collections,
  oversized UTF-8 text, unsafe Markdown/source carriers, and identity drift.
- Resolve the exact System/global Worker Agent LLM Provider assignment through
  existing provider configuration. Decrypt credentials only at the execution
  boundary; never include them in packets, prompts, results, logs, exceptions,
  or durable evidence.
- Source code and generated Documents must not leave organization
  infrastructure when the selected provider is self-hosted. Do not add an
  external fallback, telemetry export, retry, or provider failover.
- Preserve Repository-scoped authorization on all knowledge-store writes and
  reads. The Worker Agent cannot choose another Repository or System.
- Keep every committed test green; deferred behavior is recorded here and in
  SpecKit limitations rather than represented by a failing test.

## Likely implementation seams for planning

- `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentJobKind.cs`
- `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentResultPayload.cs`
- `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs`
- a new narrow Core result-committer port and closed prepared-flow/job contracts
  where provider-independent identity, authorization scope, and prepared-flow
  data cross a boundary
- `src/dotnet/Vulgata.Agents/` production dispatcher, bounded hydration,
  response schema/materialization, and `Prompts/` embedded resources
- `src/dotnet/Vulgata.Infrastructure/Data/ScanExecutionRepository.cs` for the
  inherited SQLite reservation correction only
- `src/dotnet/Vulgata.Web/Program.cs` for production DI
- focused tests under `tests/Vulgata.Tests/`

The accepted plan must refine this list. It must not modify Story 11.1/11.2
workflow evidence or their completed lifecycle records.

## Testing requirements

- Observe transient RED then GREEN for every included production behavior.
- Cover embedded prompt/resource discovery and absence of hardcoded prompts.
- Cover exact bounded source hydration at the Profiled Revision, including
  stale hash, wrong Repository, wrong line range, absolute path, secret, and
  oversized-content rejection before provider execution.
- Cover strict Pass 1 and Pass 2 schema validation, identity matching, finite
  bounds, deterministic ordering, safe diagnostic mapping, and cancellation.
- Cover valid Pass 1 atomic persistence and invalid-result zero-write behavior.
- Cover commit-before-progress ordering, commit conflict/rejection failure, and
  authorization-scope mismatch without recording a valid runner result.
- Cover the one prepared Pass 2 flow, exact flow evidence, exact
  `GENERATED_FROM` targets, the `Pass1Completed` to single Pass 2 to `Completed`
  lifecycle, and the prohibition on narrative-created base `REFERENCES` Edges.
- Cover production DI for `IWorkerAgentDispatcher` and
  `SequentialScanRunner`, including existing provider configuration and
  encrypted-key boundaries without using a live provider.
- Add a real SQLite relational test for the inherited
  `ScanExecutionRepository` write-reservation precondition.
- Treat persistence and multi-module integration as E2E predicates. Current-
  boundary integration tests may satisfy them only if they cover every
  observable boundary. The final clean-database two-Repository E2E and final
  Docker build remain Story 11.5 responsibilities because this story does not
  change container/runtime behavior.
- Run the focused Story 11.3 suite, Web build, and full non-final-image test
  suite before completion.

## Tasks / Subtasks

- [x] Tasks are generated only after independent plan-review acceptance and
      explicit planned-change authorization, then recorded in the mapped
      SpecKit `tasks.md`.

## References

- Story authority: [Source: `docs/bmad/epics.md#Story-113-Pass-1--Pass-2-Demo-Document-Generation`]
- Deadline replan: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md`]
- PRD Worker Agent and Pass 1/Pass 2 requirements: [Source: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md#Functional-Requirements`]
- Canonical Scan design: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md`]
- Canonical persistence contracts: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md`]
- Expedited workflow: [Source: `docs/agent-workflow/august-15-expedited-mvp-exception.md`]
- Canonical terminology: [Source: `docs/UBIQUITOUS_LANGUAGE.md`]
- Knowledge-store predecessor: [Source: `specs/004-minimal-knowledge-store/plan.md`]
- Sequential-runner predecessor: [Source: `specs/005-sequential-scan-runner/plan.md`]

## Dev Agent Record

### Context reference

- Preparation source commit:
  `c59171b2e5fb726f0ca40e15dcdeea2749744d01`.
- Execution level: `expedited-mvp`.
- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/11-3-pass-1-and-pass-2-demo-document-generation.json`.
- Proposed SpecKit short name: `demo-document-generation`.
- SpecKit feature directory: `specs/006-demo-document-generation`.
- Current workflow stage: `implement` (reviewed candidate).
- Latest workflow checkpoint ID:
  `bmad-11-3-implement-20260801T160718Z-5e73d4`.

### Agent Model Used

- Root sprint-loop orchestrator with direct-child `task-author`, sequential
  `tdd-implementer` groups, and pending combined `quality-reviewer`.

### Debug Log References

- Story 11.2 combined quality review F003:
  `specs/005-sequential-scan-runner/reviews/quality-review/quality-review-9f1a6fb.json`.
- Story 11.3 plan review ACCEPT:
  `specs/006-demo-document-generation/reviews/plan-review/plan-review-iteration-2.json`.
- Story 11.3 implementation evidence is recorded in
  `specs/006-demo-document-generation/workflow-state.json`.

### Completion Notes List

- Stories 11.1 and 11.2 are complete at the pinned source boundary.
- The approved August 15 replan resolves the ordinary retry/continuation
  tension by deferring advanced failure policy without marking it complete.
- The user explicitly authorized the accepted production Worker Agent adapter
  and prepared Pass 2 Scan strategy before task generation.
- G0-G7 completed with observed transient RED then GREEN evidence. The focused
  Story 11.3 lane passes 166/166, Web builds with zero errors, and the
  non-final-image regression lane passes 944/944.
- The current-boundary relational scenario proves exact committed source,
  Pass 1 commit-before-progress, one prepared Pass 2, exact `GENERATED_FROM`,
  zero narrative `REFERENCES`, and completion only after the Business Logic
  Document commit.
- No migration, model snapshot, API, UI, Chat, or Docker artifact changed.
  PostgreSQL/final-image environment lanes remain conditional and Story 11.5
  retains clean-database two-Repository and Docker verification.

### File List

- `docs/bmad/implementation-artifacts/11-3-pass-1-and-pass-2-demo-document-generation.md`
- `docs/bmad/implementation-artifacts/sprint-status.yaml`
- `docs/bmad/implementation-artifacts/workflow-activations/11-3-pass-1-and-pass-2-demo-document-generation.json`
- `specs/006-demo-document-generation/`
- `src/dotnet/Vulgata.Core/DomainServices/`
- `src/dotnet/Vulgata.Core/Entities/ScanExecution.cs`
- `src/dotnet/Vulgata.Agents/WorkerExecution/`
- `src/dotnet/Vulgata.Agents/Prompts/`
- `src/dotnet/Vulgata.Agents/PreparedFlows/`
- `src/dotnet/Vulgata.Infrastructure/Data/ScanExecutionRepository.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/WorkerAgentResultCommitter.cs`
- `src/dotnet/Vulgata.Web/Program.cs`
- `tests/Vulgata.Tests/`
