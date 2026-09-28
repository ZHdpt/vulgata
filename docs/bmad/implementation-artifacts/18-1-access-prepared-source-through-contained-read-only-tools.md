# Story 18.1 development record

Status: in-progress
Feature directory: specs/016-contained-source-tools
Workflow state: specs/016-contained-source-tools/workflow-state.json

## Accepted delivery definition

### Story 18.1: Access Prepared Source Through Contained Read-Only Tools

As a batch operator,
I want approved prepared directories exposed through bounded list, read and search operations,
So that analysis can inspect selected source without Git acquisition or arbitrary host access.

**Requirements:** O-FR01, O-FR04; O-NFR01. **Completion dependencies:** none.
**Parallel start:** immediately after the source/tool F0 contract is agreed in this plan; no database/provider prerequisite.

**Acceptance Criteria:**

**Given** an application/operator-approved batch, Repository and root binding,
**When** source access is requested,
**Then** roots are selected outside model arguments and public locations are Repository-relative,
**And** labels are not verified Git revisions; no copying, Git inspection, hashing or retention service is introduced.

**Given** files of known or unfamiliar extensions under the granted root,
**When** listing, ranged reading or text search exceeds one response,
**Then** accurate locations and explicit continuation/limit information permit further access,
**And** incomplete/unreadable results are distinct from complete empty results; listing makes no admission decision.

**Given** a foreign binding, path/symlink escape or requested write/command,
**When** the caller invokes the capability,
**Then** it is denied without access outside the root,
**And** missing inputs and cancellation return sanitized truthful outcomes and release owned resources.

**Given** small owned prepared fixtures,
**When** the production capability is invoked directly,
**Then** its normal and containment paths are observable without DB, LLM, CodeGraph or LSP,
**And** existing normal Git-bound source behavior is unchanged; no UI or management CLI is required.

## Activation and ownership

Observed 2026-09-28T03:33:29.3096814Z in the Codex-assigned managed worktree at `0c0bf0cfb05edc5b28a346d8f3a63002d1e781ef`. The worktree was clean and detached under the host's explicit native handoff; no branch switch is authorized or required. Canonical activation is `docs/bmad/implementation-artifacts/workflow-activations/18-1-access-prepared-source-through-contained-read-only-tools.json`.

The bounded accepted backlog definition supplies readiness. Only Story 18.1 is being activated. Root owns lifecycle, state, evidence, approvals, staging and commits. Workers may not delegate, stage, commit or modify lifecycle state.

## Context evidence

- No `docs/project-lessons.md` exists, so there are no recorded active lessons to apply.
- Current reusable `ISourceReadContext` and `RetainedSourceReadContext` seams provide grant-bound Repository-relative metadata, paginated listing and ranged reads with budgets, deadlines, authorization rechecks and sanitized outcomes.
- Those seams are retained Git-snapshot capabilities, not the prepared-directory contract: there is no text-search operation, live prepared-root traversal or proven filesystem symlink-escape containment.
- Existing retained-source tests provide patterns for cursors, budgets, cancellation, authorization and range results. Story 18.1 still needs direct owned-directory fixtures and a regression boundary preserving normal Git-bound reads.
- October prepared labels are not Profiled Revisions. The specification and plan must not silently add Git acquisition, copying, hashing, Source Snapshot identity or retention.
- AD-2/AD-3 and the Constitution require injected Core contracts, Infrastructure-owned filesystem authority and no arbitrary host/process authority in Agent Execution.
- The planned Worker-facing source capability triggers the repository's early plan-review and explicit planned-change authorization before tasks. No approval is requested before an independently accepted plan exists.

## Workflow Bookkeeping

Specification self-review passed with all 16 requirements-checklist items complete and no clarification marker. The actual feature mapping is `specs/016-contained-source-tools`; the optional after-specify agent-context hook was not executed because this feature has no plan yet. Lifecycle advanced `backlog -> ready-for-dev -> in-progress`. Epic 18 started with this first activation. Pending activation checkpoint: `bmad-18-1-context-20260928T034100Z-7f2ba2`. The next stage is plan with the retained definition author.

The architecture plan added the Worker Agent list/read/search surface and received one CRITICAL plan finding for missing live authorization. The retained architecture-plan author added an injected current-authority verifier with revocation/unavailability fail-closed behavior, continuation invalidation and DB-free test seams. The retained independent reviewer accepted the corrected boundary in `specs/016-contained-source-tools/evidence/plan-review-03.json`. On 2026-09-28T04:25:28.9936733Z the user explicitly authorized progression from that accepted plan to task generation with the instruction `authorize`. Decision record: `specs/016-contained-source-tools/evidence/plan-authorization-01.json`. This authorization does not permit implementation.

The retained definition author generated 26 dependency-ordered tasks covering all accepted requirements and all 19 R/I/S/A/F/L/T design IDs. The first complete-definition review accepted the semantic boundary, after which checkpoint validation found trailing whitespace in the plan. That byte-only issue invalidated the old hashes without changing content. The retained plan author removed only the whitespace; independent plan review accepted the corrected bytes, the user's earlier plan authorization was mechanically rebound to that unchanged semantic scope, and the retained task author revalidated the unchanged tasks. Read-only SpecKit analysis and the refreshed independent complete-definition review found no unresolved placeholder, unmapped task, ordering conflict or Constitution violation. `specs/016-contained-source-tools/evidence/definition-review-result-02.json` is ACCEPT. The pre-production recovery episode remains closed with its original one failed result retained in history. Pending accepted-definition checkpoint: `bmad-18-1-definition-review-20260928T043922Z-a605ed`. Production remains blocked on `18-1/implementation-approval/2`.

On 2026-09-28T05:03:16.4560797Z the user explicitly approved the accepted implementation boundary with the instruction `Approve`. Decision record: `specs/016-contained-source-tools/evidence/implementation-approval-02.json`. Governed implementation may now begin; the approval does not broaden scope or waive convergence, review, conditional E2E or final verification.

T001 and T002 completed by direct verification. Before T003 wrote a guessed test API, its test owner found that the accepted design named the trusted issuer but omitted its callable signature. Independent plan-review-05 confirmed the HIGH definition defect. The retained plan owner specified the existing intended seam (`PreparedSourceContextIssuer`, constructor-injected verifier, `IssueAsync`, typed sanitized result), and the retained task owner clarified T003/T004 without changing their behavior or order. Independent plan-review-06 and definition-review-03 accepted the corrected boundary and confirmed both existing user approvals remain semantically applicable. Root rebound their hashes on 2026-09-28T06:02:11.3091704Z; the implementation recovery episode remains open until final verification, as required for a defect found after production entry.

T003-T015 then completed their governed slices through the contained Core/Infrastructure seam. Before T016 invented an Agent factory, its test owner found that the definition fixed tool names and schemas but omitted the callable construction interface. Independent plan-review-08 confirmed this second HIGH definition defect in the same open recovery episode. The retained plan and task owners fixed only that interface (`PreparedSourceFunctions(context).Create()`, exactly three ordered borrowed-context functions). Definition-review-04 and plan-review-09 accepted the correction and confirmed unchanged scope; root rebound the existing approvals on 2026-09-28T08:23:40.6628207Z. T016 may resume without a new user decision.
