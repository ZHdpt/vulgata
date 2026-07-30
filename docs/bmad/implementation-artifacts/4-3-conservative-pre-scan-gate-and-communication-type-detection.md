---
story_key: 4-3-conservative-pre-scan-gate-and-communication-type-detection
title: "Story 4.3: Conservative Pre-Scan Gate and Communication Type Detection"
Status: in-progress
Epic: 4
Story: 3
created: 2026-07-30
execution_level: expedited-mvp
depends_on:
  - 4-1-codegraph-integration-and-code-unit-extraction
  - 4-2-language-and-framework-detection
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-story-4-3-pre-scan-gate.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md
  - docs/agent-workflow/august-15-expedited-mvp-exception.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - specs/002-language-framework-detection/plan.md
---

# Story 4.3: Conservative Pre-Scan Gate and Communication Type Detection

## Story

As a **SystemOwner**,
I want Vulgata to detect communication kinds deterministically after Repository
profiling and persist a conservative automatically approved pre-scan handoff,
so that the August 15 Scan can proceed without treating weak evidence as fact
or waiting for an LLM or Human-in-the-Loop cycle.

## Scope and boundary

- Extend the completed Story 4.1/4.2 Repository profiling path with a
  deterministic post-profile pass and a durable Pre-Scan Handoff.
- Use only bounded Repository-local structural, framework, package-manifest,
  annotation, and configuration evidence. Do not call an LLM.
- The August 15 acceptance path covers prepared RPC and HTTP evidence only.
  Message queue, file/storage, page-navigation, object-flow, Service Topology,
  and general Cross-Repository Resolution remain documented post-deadline
  limitations.
- Preserve three truth levels: confirmed high-confidence communication kinds,
  conservative hints, and conservative warnings. A hint or warning must never
  be promoted to a confirmed code-derived fact.
- Persist and expose `pre_scan_approved` for the later sequential runner. Do not
  implement a Scan Coordinator, queue, Worker Agent dispatch, lease, retry,
  concurrent superstep, or provider failover.
- Do not implement pre-scan LLM questions, manual approval, the seven-day
  lifecycle, `等待回答`, the bell badge, or the full notification surface.
- Do not implement detailed provider/consumer endpoint detection. Story 11.4
  owns the prepared exact RPC/HTTP match; Epic 7 owns the general subsystem.

## Acceptance Criteria

### AC-1: Detect prepared RPC and HTTP communication kinds deterministically

**Given** Code Units, language facts, and framework facts have been persisted for
a Repository
**When** the Story 4.3 post-profile pass runs
**Then** it inspects bounded CodeGraphContext structural evidence, supported
framework facts, package manifests, annotations, and configuration
**And** it detects the prepared demonstration's RPC and HTTP communication kinds
without an LLM, package execution, network lookup, or source-code exfiltration
**And** unsupported communication families are left for their owning
post-deadline stories rather than guessed.

### AC-2: Preserve evidence, provenance, and conservative confidence

**Given** deterministic communication evidence is found
**When** the result is classified
**Then** direct supported framework annotations or matched configuration are
recorded as confirmed high-confidence kinds
**And** import-only or otherwise weak evidence is retained as a conservative hint
**And** material ambiguity is retained as a conservative warning
**And** every item carries a bounded Repository-relative evidence locator and
deterministic provenance
**And** neither hints nor warnings are represented as confirmed code-derived
facts.

### AC-3: Persist an automatically approved pre-scan result

**Given** the deterministic pass completes, including when it produces only
hints or warnings
**When** the pre-scan gate completes
**Then** one durable Repository- and ScanRun-scoped result transitions directly
to `pre_scan_approved`
**And** confirmed kinds, hints, warnings, evidence locators, provenance, and the
schema/version needed by a Pre-Scan Handoff consumer are committed atomically
**And** a retry cannot create a second approved result or duplicate facts for
the same ScanRun.

### AC-4: Publish a Pre-Scan Handoff without dispatch

**Given** an approved result exists
**When** an authorized application service or API requests the pre-scan handoff
**Then** it receives the approved Pre-Scan Handoff needed by Story 11.2
**And** the response contains no absolute host path, source content, secret, or
unbounded diagnostic payload
**And** this story does not create or dispatch a Worker Agent job.

### AC-5: Record bounded Chinese observability

**Given** the pre-scan gate approves a result
**When** the outcome is recorded
**Then** the Scan log records the Simplified-Chinese equivalent of
“Pre-scan approved with conservative warnings — proceeding to the sequential
demo runner”
**And** any exposed warning/evidence summary is bounded and authorized through
the existing Repository visibility rules.

### AC-6: Preserve atomic failure and rerun behavior

**Given** an evidence input is malformed, unsafe, oversized, or unsupported
**When** detection processes that input
**Then** the individual input is isolated as bounded conservative evidence or a
warning without inventing a confirmed fact
**And** a persistence failure leaves no partially approved pre-scan result
**And** a provider-level profiling failure remains governed by Story 4.1/4.2
atomicity and does not publish an approved handoff.

## Context resolutions

1. **The August 15 PRD boundary is the current acceptance overlay.** Although the
   full Story 4.3 wording enumerates RPC, HTTP, message queues, file/storage, and
   page navigation, PRD section 5.16 and the approved July 30 demonstration
   slice explicitly defer every family except the prepared RPC/HTTP path.
2. **Pre-scan hypotheses are not full Cross-Repository Communication
   Detection.** Story 4.3 classifies communication kinds and preserves evidence;
   it does not infer provider/consumer roles, resolve a target Repository, or
   create a REFERENCES edge.
3. **Catalog-shaped seeds do not establish catalog-management ownership.** The
   approved handoff may carry confirmed kinds suitable for later Communication
   Pattern Catalog consumption, but this story must not implement the
   SystemOwner catalog editor, versioning, prompt injection, or Epic 7
   management behavior.
4. **The accepted deadline correction supersedes the earlier question gate.**
   Blocking ambiguity becomes a warning and the result auto-approves. Story 8.7
   preserves the post-deadline LLM/Human-in-the-Loop lifecycle.
5. **Durability is required, but its minimum shape is a planning decision.** The
   current source has no pre-scan result aggregate. The plan must choose the
   smallest additive Core/Infrastructure model that preserves type-safe,
   queryable, atomic, idempotent handoff semantics and must coordinate shared EF
   files with Story 11.1.

## Existing implementation seams

- `RepositoryProfilingCoordinator` currently completes the post-CodeGraphContext
  profiling flow and builds `RepositoryProfileDraft`.
- `IScanRunRepository.PersistCompletedProfileAsync` and `ScanRunRepository`
  atomically persist Code Units, structural records, framework facts, language
  summaries, and candidate outcomes.
- `ScanRun` currently exposes only `Profiling`, `Completed`, `Failed`, and
  `Aborted`; it has no approved pre-scan result.
- `CodeUnitEdge` and `UnresolvedStructuralRelationshipCandidate` preserve
  structural relationships but do not represent communication kinds,
  confidence levels, warnings, or approval.
- `RepositoryProfilingTests` and `FrameworkDetectionTests` are the focused
  deterministic and persistence test seams.

## Implementation guardrails

- Keep Core free of EF Core and infrastructure dependencies.
- Use specific domain service contracts; do not add a generic repository or
  expose `DbSet`, `IQueryable`, or provider-specific response shapes.
- Keep detection closed, deterministic, non-executing, path-contained, and
  bounded by explicit input/evidence limits.
- Reuse already persisted structural/framework facts where sufficient. Read
  additional files only through a Repository-relative, containment-checked
  boundary.
- Normalize communication-kind and confidence values through closed domain
  types; never persist attacker-controlled text as a type discriminator.
- Preserve existing `ManagementAccess` and Repository visibility behavior.
- Any additive migration must pass clean-database apply, rollback, and pending
  model checks and must be serialized with Story 11.1 migration work.
- This story materially changes Scan parsing/classification and gate behavior.
  After independent plan review accepts, the sprint loop must obtain explicit
  planned-change authorization before task generation.

## Testing requirements

- Observe transient RED then GREEN for every included production behavior; no
  failing test may be committed.
- Cover high-confidence RPC/HTTP evidence, weak-hint non-promotion, warning
  auto-approval, unsupported/deferred families, bounded evidence, path
  containment, duplicate suppression, rerun idempotency, and atomic failure.
- Cover Pre-Scan Handoff authorization and redaction.
- If persistence changes, cover clean migration, rollback, and no pending model
  changes.
- Treat the post-profile persistence/API path as integration-triggering for the
  conditional E2E decision.

## Tasks / Subtasks

Task generation is intentionally paused. Tasks will be generated only after an
independent plan review accepts and the user explicitly authorizes the
Scan-strategy change.

## References

- Story authority: [Source: `docs/bmad/epics.md#Story-43-Conservative-Pre-Scan-Gate--Communication-Type-Detection`]
- Deadline PRD boundary: [Source: `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md#516-August-15-Demonstration-MVP-Boundary`]
- Approved deadline slice: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md`]
- Earlier ownership correction: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-story-4-3-pre-scan-gate.md`]
- Canonical terminology: [Source: `docs/UBIQUITOUS_LANGUAGE.md`]
- Inherited profiling design: [Source: `specs/002-language-framework-detection/plan.md`]

## Dev Agent Record

### Context reference

- Preparation and activation source commit:
  `e7597082bf6acd4e7dfa05a56d393deae787a3d4`.
- Execution level: `expedited-mvp`.
- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/4-3-conservative-pre-scan-gate-and-communication-type-detection.json`.
- SpecKit feature directory:
  `specs/003-conservative-pre-scan-gate`.
- Current workflow stage: `implement` (accepted delivery definition pending
  checkpoint binding).
- Latest workflow checkpoint ID:
  `bmad-4-3-tasks-20260730T044037Z-c7a41e`.
- First expedited checkpoint remains pending until story, specification,
  accepted plan review, planned-change authorization, and tasks form one
  accepted delivery definition.

### Agent Model Used

- Root sprint-loop orchestrator
- Story context synthesizer

### Debug Log References

- None.

### Completion Notes List

- Story context prepared and activated from the accepted August 15 scope.
- Independent plan review accepted the pinned Story 4.3 plan on iteration 3.
- User explicitly approved the Scan strategy; task generation is active.
- Task generation produced T001-T036 and read-only analysis accepted with no
  findings; the first expedited checkpoint is pending binding.

### File List

- `docs/bmad/implementation-artifacts/4-3-conservative-pre-scan-gate-and-communication-type-detection.md`
- `docs/bmad/implementation-artifacts/sprint-status.yaml`
- `docs/bmad/implementation-artifacts/workflow-activations/4-3-conservative-pre-scan-gate-and-communication-type-detection.json`

## Change Log

- 2026-07-30: Prepared and activated Story 4.3 under the expedited-mvp
  exception; no tasks or implementation generated.
- 2026-07-30: Plan review accepted after correction; paused before task
  generation for mandatory Scan-strategy authorization.
- 2026-07-30: User approved the accepted Scan strategy; task generation
  resumed without waiving later gates.
- 2026-07-30: Tasks and read-only analysis accepted; assembled the first
  expedited accepted-delivery-definition checkpoint.
