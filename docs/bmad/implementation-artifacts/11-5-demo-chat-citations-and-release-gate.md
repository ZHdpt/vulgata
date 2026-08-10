---
story_key: 11-5-demo-chat-citations-and-release-gate
title: "Story 11.5: Demo Chat, Citations & Release Gate"
Status: in-progress
Epic: 11
Story: 5
created: 2026-08-04
execution_level: expedited-mvp
depends_on:
  - 11-1-minimal-knowledge-store-and-retrieval-projection
  - 11-2-sequential-scan-runner-and-typed-agent-dispatch
  - 11-3-pass-1-and-pass-2-demo-document-generation
  - 11-4-exact-rpc-http-cross-repository-demo-resolution
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md
  - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md
  - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md
  - docs/agent-workflow/august-15-expedited-mvp-exception.md
  - docs/UBIQUITOUS_LANGUAGE.md
---

# Story 11.5: Demo Chat, Citations & Release Gate

## Story

As a **user**,
I want to ask Business or IT questions about the prepared two-Repository flow
and verify every answer,
so that the competition demonstration proves Vulgata's end-to-end value.

## Scope and boundary

- Replace the authenticated Chinese chat placeholder with the minimum prepared
  Business/IT chat workflow.
- Require explicit selection of the prepared System and Repositories. Automatic
  scope selection and file upload remain deferred.
- Retrieve only current authorized Documents, Document Sections, Source Anchors,
  flow/provenance evidence, and the exact Cross-Repository `REFERENCES` Edge
  from the database-backed knowledge store. `index.md` is never canonical.
- Ground every material answer claim in a persisted Document citation and an
  available Source Anchor line range.
- Business Mode keeps its answer narrative free of inline code references and
  exposes a Chinese Document citation whose expandable detail contains the
  source line range. IT Mode may show Repository-relative paths and line ranges
  directly. This is the user-approved reconciliation of PRD FR-11.1 with this
  story's citation acceptance criterion.
- Reuse existing System/Repository visibility, `RepositoryAccessScope`, live
  authorization verification, encrypted LLM-provider selection, immutable
  Document Version retrieval, and exact relationship projection.
- Add no Scan scheduling, concurrency, retry/failover, general Cross-Repository
  matching, file upload, automatic scope selection, graph, dashboard, SignalR,
  or post-demo Epic 5–10 behavior.
- Run the final clean-database prepared two-Repository path and final Docker
  image build required by the August 15 demonstration slice.

## Acceptance Criteria

### AC-1: Ask a grounded question in either prepared mode

**Given** an authenticated user opens the Chinese chat page
**When** the user selects Business or IT Mode plus an authorized prepared System
and its two prepared Repositories and submits a bounded question
**Then** retrieval uses current database-backed Documents, Document Sections,
Source Anchors, flow/provenance evidence, and the exact Cross-Repository
`REFERENCES` relationship within that explicit scope
**And** Business Mode returns an understandable Chinese narrative while IT Mode
may include precise technical detail
**And** automatic scope selection and file upload remain unavailable.

### AC-2: Cite every material claim without leaking unauthorized data

**Given** the Chat Agent has retrieved an authorized evidence packet
**When** it returns and the UI renders an answer
**Then** every material claim maps to at least one persisted current Document
and an available Source Anchor line range
**And** Business Mode renders a non-code narrative with expandable Chinese
Document citations while IT Mode may render Repository-relative source locators
and line ranges directly
**And** unauthorized Systems, Repositories, Documents, Source Anchors, exact
relationship metadata, source text, absolute host paths, and credentials are
neither retrieved nor rendered
**And** missing, stale, malformed, or ungrounded model output fails closed with
a bounded Chinese error and no unsupported answer claim.

### AC-3: Pass the final release gate from a clean database

**Given** the accepted deadline slice and the two prepared Repository revisions
**When** final verification runs against a uniquely named clean PostgreSQL 17
database
**Then** migrations apply and the prepared path completes profiling, Pass 1,
exact Cross-Repository Resolution, Pass 2, retrieval, and one cited Business/IT
chat answer without guessed graph truth
**And** authorization-negative checks prove inaccessible scope is
missing-equivalent
**And** included focused tests, migration checks, Web build, full applicable
regression, one final Docker image build, and container startup/health evidence
pass
**And** the disposable database is dropped and absence is verified
**And** no deliberately failing or unresolved RED test is committed
**And** known post-demo limitations are reported separately and are not claimed
as completed acceptance criteria.

## Developer guardrails and reuse

- Start from `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`; preserve
  the existing authorized `/` route, Chinese-only UI, Fluent UI conventions,
  Business light theme, IT dark theme, keyboard behavior, and inline citation
  interaction defined by the UX artifacts.
- Reuse `IKnowledgeStoreRepository`, `CurrentKnowledge`,
  `CurrentDocumentSection`, `CurrentSourceAnchor`, authorized relationship read
  models, `IRepositoryAuthorizationVerifier`, `IUserContext`, and existing
  System/Repository repositories. Do not expose `IQueryable` or EF types.
- Agent prompts live as embedded resources in `Vulgata.Agents/Prompts/`; never
  hardcode prompts or credentials. Model output must use a closed typed contract
  and be validated before rendering.
- Core owns provider-independent contracts; Infrastructure owns database-backed
  retrieval; Agents owns Chat Agent execution/prompting; Web owns authorization,
  composition, session state, and Razor presentation.
- Prefer no migration: the deadline story does not require durable conversation
  history. Any plan that introduces a new persistent model must justify it and
  retain additive migration/apply/rollback/reapply/no-pending proof.
- Do not modify predecessor feature artifacts under `specs/004` through
  `specs/007`, predecessor BMad records, external prepared Repositories, exact
  resolver truth, or Sequential Scan Runner scheduling.

## Testing and release requirements

- Observe transient RED then GREEN for each included production behavior.
- Cover explicit authorized scope selection, Business/IT answer shaping, typed
  grounded output validation, citation completeness, stale/missing evidence,
  prompt/resource loading, provider failure, cancellation, and Chinese UI states.
- Prove authorization before retrieval and again before rendering; unauthorized
  scope must disclose no existence, identifiers, metadata, citations, or source.
- Exercise the real Web/Agents/Core/Infrastructure composition with relational
  storage; mocks alone cannot satisfy the final slice.
- The final E2E owns the prepared Repository commits documented by Story 11.4,
  a uniquely named clean PostgreSQL database, cleanup verification, and one
  source-bound final Docker build plus startup/health check.

## Tasks / Subtasks

- [x] Independent plan-review iteration 10 accepted the historical Full Option A
      correction with zero findings; the execution-level change invalidates it
      as the active plan-review evidence.
- [x] User explicitly authorized the Option A planned change before regenerated
      task execution.
- [x] The user authorized `full` -> `expedited-mvp` for Story 11.5 on
      2026-08-10 under the August 15 exception; it expires when Epic 11
      completes or at `2026-08-15T23:59:59+08:00`, whichever occurs first.
- [x] Plan-review iteration 11 accepted the expedited-mvp corrected plan with
      zero findings. The durable Option A planned-change authorization remains
      valid; no duplicate authorization is required.
- [x] Regenerated `specs/008-demo-chat-release-gate/tasks.md` and completed
      root mechanical validation: SHA-256
      `920d0f93ad148be3813387a434d62add3bdffcc373ce87907b25092b28cfab09`,
      73 continuous tasks, T001-T063 preserved complete, and T064-T073 pending.
- [ ] Execute pending T064-T071 with transient observed RED then GREEN, obtain
      the one fresh combined quality review over the pinned candidate, execute
      the sole live T072 clean PostgreSQL two-Repository/Docker release run,
      then execute T073 cleanup, absence proofs, and final verification.

## References

- Story authority: [Source: `docs/bmad/epics.md#Story-115-Demo-Chat-Citations--Release-Gate`]
- Deadline overlay: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md`]
- Chat and retrieval requirements: [Source: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md#511-Chat--Two-Modes`]
- Canonical retrieval/security design: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md#12-Retrieval-and-Chat`]
- UX behavior: [Source: `docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md#Component-Patterns`]
- Expedited workflow: [Source: `docs/agent-workflow/august-15-expedited-mvp-exception.md`]
- Canonical terminology: [Source: `docs/UBIQUITOUS_LANGUAGE.md`]

## Dev Agent Record

### Context reference

- Preparation source commit:
  `c07df7d8789e7b975dc7fb6a121dff564e8549ad`.
- Execution level: `expedited-mvp`, authorized by the user on 2026-08-10 for
  Story 11.5 only under
  `docs/agent-workflow/august-15-expedited-mvp-exception.md`; it expires when
  Epic 11 completes or at `2026-08-15T23:59:59+08:00`, whichever occurs first.
- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/11-5-demo-chat-citations-and-release-gate.json`.
- Proposed SpecKit short name: `demo-chat-release-gate`.
- SpecKit feature directory: `specs/008-demo-chat-release-gate`.
- Current workflow stage: `implement`.
- Expedited-mvp flow: `context -> specify -> plan -> conditional plan-review +
  planned-change authorization -> tasks -> implement -> combined quality-review
  -> conditional-e2e -> verify -> complete`.
- Active gates: conditional plan review and planned-change authorization remain
  required; transient RED/GREEN TDD, combined quality review, security and
  migration checks, clean-database two-Repository E2E, one final Docker image,
  cleanup, and verification remain required. `spec-review`, active
  `speckit-analyze`, and separate `acceptance-review` are invalidated at
  iteration zero under the expedited-mvp flow.
- Latest accepted-delivery checkpoint ID:
  `bmad-11-5-expedited-accepted-delivery-20260810T104750Z` (accepted task
  boundary; its checkpoint stage is `tasks`, followed by current `implement`).

### Agent Model Used

- Root sprint-loop orchestrator with direct-child context and specification
  authors.

### Completion Notes List

- Comprehensive deadline-slice context prepared from Stories 11.1–11.4,
  current source seams, PRD/architecture/UX authorities, and the dated exception.
- User approved the Business Mode citation reconciliation on 2026-08-04.
- Specification validation passed 16/16 items with no clarification markers.
- Plan-review iteration 4 accepted the original bounded design with zero
  findings; it is historical evidence only after the Option A correction.
- User authorized the new bounded Chat Agent retrieval tool and paired internal
  currentness-validation safety contract on 2026-08-04.
- Plan-review iteration 10 accepted the historical Full Option A correction
  with zero findings, followed by the distinct user-authorized Option A
  planned change. It is invalidated as active plan-review evidence by the
  2026-08-10 execution-level transition.
- Plan-review iteration 11 ACCEPT closes ANALYZE-115-TASK-001 for the current
  expedited-mvp boundary with zero findings. The durable Option A planned-change
  authorization remains valid, so no duplicate authorization is required.
- Regenerated tasks and root mechanical validation are accepted: the task
  artifact SHA-256 is
  `920d0f93ad148be3813387a434d62add3bdffcc373ce87907b25092b28cfab09`; it
  has 73 continuous test-first tasks, with T001-T063 preserved complete and
  T064-T073 pending. Validation recorded FR-001-FR-024 and SC-001-SC-011
  coverage, valid RED-before-GREEN ordering, no migration/model delta, 35/35
  workflow tests, and a clean `git diff --check`.
- The pre-Option-A T064 live correction evidence, including Repository
  profiling, zero-byte committed blobs, candidate-local provisioning storage,
  and external nonce-resource/database cleanup, is historical and invalidated
  as current execution evidence.
- The prior Full acceptance review iteration 4 ACCEPT and quality review
  iteration 6 ACCEPT are historical invalidated evidence; neither is current
  acceptance. The expedited-mvp flow requires one fresh combined quality
  review after implementation.
- Implementation has not begun for T064-T073. The retained sequence is
  transient RED/GREEN TDD for T064-T071, one fresh combined quality review over
  the pinned candidate, the sole live T072 release run, and T073 cleanup,
  absence proofs, and final verification. No live resource, implementation,
  review, E2E, cleanup, or completion evidence is presumed.

### File List

- `docs/bmad/implementation-artifacts/11-5-demo-chat-citations-and-release-gate.md`
- `specs/008-demo-chat-release-gate/`
- `specs/008-demo-chat-release-gate/tasks.md`
