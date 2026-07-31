---
story_key: 11-1-minimal-knowledge-store-and-retrieval-projection
title: "Story 11.1: Minimal Knowledge Store and Retrieval Projection"
Status: in-progress
Epic: 11
Story: 1
created: 2026-07-30
execution_level: expedited-mvp
depends_on:
  - 4-3-conservative-pre-scan-gate-and-communication-type-detection
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md
  - docs/agent-workflow/august-15-expedited-mvp-exception.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - specs/003-conservative-pre-scan-gate/plan.md
---

# Story 11.1: Minimal Knowledge Store and Retrieval Projection

## Story

As a **SystemOwner**,
I want generated Documents, source anchors, essential references, and retrieval
sections stored durably,
so that the August 15 demonstration can persist and retrieve a traceable
Cross-Repository business flow.

## Scope and boundary

- Add the prepared-demo minimum persistence model for Logical Documents,
  immutable Document Versions, source anchors, `REFERENCES` and
  `GENERATED_FROM` Edges, flow evidence, `index_entries`, and
  `document_sections`.
- Keep Logical Document identity separate from immutable Document Version
  identity. Generated Edges target Document Versions.
- Treat PostgreSQL-backed `index_entries` and `document_sections` as retrieval
  truth. `index.md` is never canonical state.
- Limit flow evidence to `flow_groups`, `flow_traces`, and ordered
  `flow_trace_steps` for the one prepared demonstration flow.
- Defer candidate-selection, dispatch-binding, endpoint-binding, full flow
  clustering, uncertainty infrastructure, reverse-index impact analysis,
  virtual-tree/export behavior, and the rest of post-deadline Story 5.3/5.4.
- Reuse existing Repository ownership and visibility boundaries. Do not add an
  authorization policy, public retrieval API, Chat Agent tool, Worker Agent
  tool, Scan strategy, dispatch behavior, or Story 4.3 behavior.
- Story 4.3 is complete at
  `b851ba165d6ebe560227bfdc3b206a814280e9b2`. This story owns the next additive
  `VulgataDbContext`, migration, and model-snapshot evolution from that exact
  checkpoint and must not modify Story 4.3 implementation or workflow files.

## Acceptance Criteria

### AC-1: Add the minimum durable knowledge-store schema

**Given** `VulgataDbContext` is migrated from the completed Story 4.3 boundary
**When** the Story 11.1 migration is applied
**Then** it creates the minimum Logical Document, immutable Document Version,
source-anchor, `REFERENCES`/`GENERATED_FROM` Edge, flow-group, flow-trace,
flow-trace-step, `index_entries`, and `document_sections` shapes
**And** all entity configuration remains in Infrastructure
**And** all foreign keys and Repository-scoped lookup paths are indexed
**And** a clean PostgreSQL database applies the migration successfully
**And** EF reports no pending model changes.

### AC-2: Commit immutable Document Versions atomically

**Given** a prepared Code Logic Document or Business Logic Document commit
contains content, source anchors, essential Edges, and flow evidence
**When** the knowledge-store repository commits it
**Then** one Logical Document and one immutable current Document Version are
persisted with the supplied content and schema identity
**And** its source anchors, permitted Edges, flow evidence, index entry, and
sections commit atomically
**And** a failed invariant or persistence operation leaves no partial
Document, Edge, flow, or retrieval projection rows.

### AC-3: Rebuild database-backed retrieval projections

**Given** a Document Version is committed
**When** its retrieval projection is built or rebuilt
**Then** `index_entries` contains the bounded document-level searchable entry
for that Document Version
**And** `document_sections` contains the bounded ordered sections materialized
from the accepted Document content
**And** rebuilding replaces only projection rows for that immutable Document
Version without reading or parsing `index.md`.

### AC-4: Retrieve current authorized knowledge

**Given** an authorized Repository scope and a Logical Document
**When** current knowledge is retrieved through the Core repository contract
**Then** the materialized result contains the current Document Version,
Markdown/content payload, source anchors, essential Edges, narrow flow
evidence, index entry, and requested sections
**And** no `DbSet`, `IQueryable`, EF entity, absolute host path, source text
outside an accepted anchor, secret, or unbounded diagnostic crosses the
Infrastructure boundary
**And** Repository identity remains available for the application layer to
enforce visibility and redact unauthorized Edge targets.

### AC-5: Preserve the Version Chain

**Given** a Logical Document already has a current Document Version
**When** a new version is committed for that Logical Document
**Then** the new version is inserted rather than updating the old content
**And** the previous version is retained and linked through
`superseded_by_id`
**And** only the new version is current
**And** no old Document Version, source anchor, Edge, or flow evidence is
deleted or mutated.

### AC-6: Preserve exact Edge and flow provenance

**Given** Code Logic Document Versions and one prepared Business Logic Document
Version exist
**When** essential relationships and flow evidence are committed
**Then** `REFERENCES` connects the exact source and target Document Versions
**And** `GENERATED_FROM` connects the Business Logic Document Version to the
Code Logic Document Versions actually used
**And** ordered flow-trace steps reference exact Document Versions and
permitted Edges
**And** Cross-Repository status is derived from endpoint Repository identity,
not stored as a separate Edge type.

## Context resolutions

1. **Story 4.3 dependency is satisfied.** Its completed-story checkpoint is the
   source boundary for this branch; its pre-scan entities, detector, handoff,
   migration, and tests remain unchanged.
2. **The flow-evidence cut is explicit.** Story 11.1 implements only
   `flow_groups`, `flow_traces`, and `flow_trace_steps`. The candidate,
   dispatch, and endpoint binding families remain Story 5.3 work.
3. **Retrieval is database-backed.** `index_entries` and
   `document_sections` are canonical projections. `index.md` is an optional
   rendered/export artifact and is not read by the repository.
4. **Authorization is a data-boundary requirement, not a new UI/API.** Core and
   Infrastructure preserve Repository identity and materialize bounded results;
   later Web/Chat work owns user-facing retrieval and citation behavior.
5. **The deadline model is additive, not a claim of full Epic 5 completion.**
   Reverse index, Uncertainty, dispatch-site, endpoint-binding, impact-analysis,
   virtual-tree, and general export behaviors remain explicit limitations.

## Existing implementation seams

- `VulgataDbContext` is the shared domain context and now includes Story 4.3
  pre-scan entities; it has no knowledge-store `DbSet`s.
- `ScanRun` and `Repository` provide the existing Repository-scoped foreign-key
  anchors.
- Infrastructure already registers one `IEntityTypeConfiguration<T>` per
  entity by assembly scan.
- Story 4.3 `MigrationTests` provide the clean apply, rollback, reapply, schema
  constraint, and pending-model-change patterns to extend.
- Current profiling persistence is not a generic knowledge-store repository;
  Story 11.1 must add a specific Core contract and Infrastructure
  implementation rather than overload `IScanRunRepository`.

## Implementation guardrails

- Keep Core free of EF Core and infrastructure dependencies.
- Use closed domain types and a specific repository contract; do not add data
  annotations, a generic repository, `IQueryable`, or exposed `DbSet`.
- Store Document Version content immutably. Rebuild only replaceable projection
  rows for the same immutable version.
- Bound JSON, Markdown, section text, evidence locators, and collection counts
  in the accepted plan before implementation.
- Validate same-Repository/Cross-Repository ownership and Edge endpoint
  existence before persistence. Never expose unauthorized target details.
- Use restrictive delete behavior for immutable history and graph truth unless
  the accepted plan proves a safe aggregate-owned cascade for newly inserted
  child rows.
- Add no Story 4.3, Worker Agent, Chat Agent, Scan strategy, dispatch,
  scheduling, retry, provider, UI, API-route, or container behavior.
- Generate the EF migration only after an independent plan review accepts the
  migration invariants. Commit no failing test.

## Testing requirements

- Observe transient RED then GREEN for every included production behavior.
- Cover Core invariants, atomic initial commit, projection rebuild,
  Version Chain supersession, exact `REFERENCES`/`GENERATED_FROM` endpoints,
  ordered flow steps, cross-Repository ownership, bounds, and rollback.
- Cover materialized Repository-scoped retrieval and unauthorized-target
  redaction at the repository/application seam without adding a new public API.
- Extend PostgreSQL migration validation with clean apply, rollback to
  `20260730083207_AddConservativePreScanGate`, reapply, required tables,
  constraints/indexes, and no pending model changes.
- Treat migration, persistence, and observable data lifecycle as requiring
  current-boundary integration coverage. The final clean-database
  two-Repository demonstration and final Docker image remain Story 11.5
  responsibilities unless this story changes container/runtime behavior.
- Run the Web build and full non-final-image test suite before completion.

## Tasks / Subtasks

- [x] Tasks are generated from the accepted SpecKit plan and recorded in the
      mapped `tasks.md`.

## References

- Story authority: [Source: `docs/bmad/epics.md#Story-111-Minimal-Knowledge-Store--Retrieval-Projection`]
- Canonical storage contracts: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md`]
- Canonical scanning plan: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md`]
- Deadline slice: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md`]
- Expedited workflow: [Source: `docs/agent-workflow/august-15-expedited-mvp-exception.md`]
- Canonical terminology: [Source: `docs/UBIQUITOUS_LANGUAGE.md`]
- Integration predecessor: [Source: `specs/003-conservative-pre-scan-gate/plan.md`]

## Dev Agent Record

### Context reference

- Preparation and activation source commit:
  `b851ba165d6ebe560227bfdc3b206a814280e9b2`.
- Execution level: `expedited-mvp`.
- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/11-1-minimal-knowledge-store-and-retrieval-projection.json`.
- Proposed SpecKit short name: `minimal-knowledge-store`.
- SpecKit feature directory: `specs/004-minimal-knowledge-store`.
- Current workflow stage: `review`.
- Latest workflow checkpoint ID:
  `bmad-11-1-candidate-20260731T034532Z-c8e2b4`.
- Latest workflow checkpoint SHA: pending commit-trailer binding.

### Agent Model Used

- Root sprint-loop orchestrator
- Story context synthesizer
- Spec author
- Plan architect
- Independent plan judge
- Task author
- EF integration owner

### Debug Log References

- None.

### Completion Notes List

- Story 4.3 completion and shared EF ownership transfer were verified at the
  exact source boundary.
- The operator directed the sprint loop to continue Story 11.1.
- The deadline flow-evidence subset is fixed to flow groups, flow traces, and
  ordered flow-trace steps.
- The independent plan review required one correction cycle and accepted the
  corrected migration, Version Chain, endpoint-currentness, and closed-content
  design.
- The operator granted a narrow exception for the EF integration owner to pin
  the Story 4.3 migration regression test to
  `20260730083207_AddConservativePreScanGate`; no other Story 4.3 edit is
  authorized.
- The accepted delivery definition contains 36 dependency-ordered tasks with
  transient RED, GREEN, and REFACTOR evidence required for every production
  group.
- TDD groups G1, G2, G5, G6, and G3/G4 are complete. The corrected candidate
  passed SpecKit analysis with 33 of 33 requirements covered and 29 of 29
  implementation tasks aligned.
- The EF integration owner generated
  `20260731034018_AddMinimalKnowledgeStore`, verified rollback to Story 4.3,
  reapply, fourteen accepted check constraints, unchanged prior migrations,
  and no pending model changes.
- The candidate has independent PostgreSQL currentness-race evidence, a
  619-test non-provider/non-PostgreSQL regression pass, and all focused
  knowledge-store suites green. The artifact-bound Epic 4.1 provider-contract
  class remains outside this story boundary.

### File List

- `docs/bmad/implementation-artifacts/11-1-minimal-knowledge-store-and-retrieval-projection.md`
- `docs/bmad/implementation-artifacts/sprint-status.yaml`
- `docs/bmad/implementation-artifacts/workflow-activations/11-1-minimal-knowledge-store-and-retrieval-projection.json`
- `specs/004-minimal-knowledge-store/`
- `tests/Vulgata.Tests/MigrationTests.cs`
