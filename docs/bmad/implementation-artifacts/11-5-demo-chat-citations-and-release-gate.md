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
- [x] Execute T064-T071 with transient observed RED then GREEN and complete
      the required non-live Option A, migration, model, build, and static checks.
- [ ] Obtain the one fresh combined quality review over the pinned candidate, execute
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
- Current workflow stage: `quality-review` (candidate pinning in progress).
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
- T064-T071 are complete with observed transient RED then GREEN. Root reproduced
  the focused Option A contract 4/4, the direct Pass 1/Pass 2 surrounding set
  29/29, and the corrected currentness fixture 3/3. The complete non-live
  release harness is 44/44, the required combined quickstart filters are
  137/137, predecessor migration tests are 10/10, both DbContexts report no
  pending model changes, Release builds and the provisioner publish succeed,
  and the published provisioner mount allowlist contains exactly three files.
  No live Docker, PostgreSQL, provider, Repository, CodeGraph, browser, or
  Playwright resource was acquired. T072-T073 remain pending behind the fresh
  combined quality review and unchanged-candidate pin.
- Combined quality review iteration 7 returned `REVISE` with
  `QR-115-007-001` and `QR-115-007-002`: the host parser did not fully close
  and nonce/hash-bind provisioner output, and the three mounted provisioner
  files were not byte-bound to the reviewed candidate/evidence. Both findings
  were corrected test-first without live activation. Root reproduced the
  corrected complete non-live release harness at 48/48; a fresh combined
  quality review over the recovery candidate is required before T072.
- Combined quality review iteration 9 accepted the recovery candidate with zero
  findings after iteration 8 corrected the canonical source-manifest metadata.
  The first live T072 execution then failed before database creation at the
  first Repository clone because `git clone --branch` rejected the accepted
  full `refs/tags/...` name. Cleanup proved no Story image/container/database
  residue. A focused RED then GREEN correction now maps only the clone selector
  to the short tag while retaining full-ref/full-commit verification; the real
  disposable clone reaches the exact accepted commit and the complete non-live
  release harness is 49/49. T072 remains pending a fresh combined review.
- Combined quality review iteration 10 accepted the tag-clone correction with
  zero findings. The second live T072 attempt then failed cleanly before any
  Story resource acquisition because the mandatory no-cache source image build
  exceeded the harness's 30-minute subprocess limit; cleanup had no error, no
  Story container/image/network/nonce database/run directory remained, and the
  external PostgreSQL 17 Alpine container remained healthy. A focused test
  first reproduced that a 45-minute bound was rejected. The minimal harness-only
  correction permits exactly 45 minutes for the source image build while still
  rejecting any larger bound; focused 1/1 and complete non-live release harness
  50/50 are GREEN. T072-T073 remain pending a fresh combined quality review and
  unchanged-candidate pin before another live retry.
- Quality iteration 11 correctly rejected stale ignored candidate metadata;
  iteration 12 was invalidated when its manually transcribed manifest omitted
  the final hexadecimal character, which the live parser rejected before any
  acquisition. Iteration 13 independently invoked the repository helper and
  accepted the corrected 64-character identity. The following build failed
  cleanly on an external PyPI SSL/read timeout, with BuildKit logs preserving
  the exact layer and cleanup preserving zero Story residue. A connectivity-
  healthy retry built the final image and reached the same-image provisioner,
  which returned generic exit 1; the fixed diagnostic intentionally exposed no
  cause. A new RED then GREEN correction keeps that fixed stderr byte-for-byte
  and adds only closed numeric stage exit codes, allowing bounded diagnosis
  without secrets, paths, source, or raw product diagnostics. Focused 1/1 and
  the complete non-live release harness 50/50 are GREEN. T072-T073 remain
  pending quality iteration 14 over the new immutable candidate.
- Quality iteration 14 accepted the closed stage-code recovery candidate with
  zero findings. Its diagnostic live retry was interrupted before Story
  resource acquisition when the user authorized faster package transport for
  subsequent Docker builds. The correction uses Tsinghua for ordinary Ubuntu
  Noble archives and the hash-locked pip index, retains Ubuntu's official Noble
  security archive and pip `--require-hashes`, and changes no package identity,
  topology, migration/model, or T072/T073 acceptance behavior. A focused
  Dockerfile contract observed RED before the mirror change and is GREEN 3/3;
  the changed candidate requires a fresh combined quality review before T072.
- Quality iteration 15 accepted the mirror candidate with zero findings. The
  next live T072 attempt built the no-cache image in under five minutes, then
  failed cleanly at closed provisioner stage 11 before product provisioning.
  A minimized real-container diagnosis proved the image's `cgc` 0.5.1 and
  requirements-lock checks pass, but the three-file driver's dependency
  manifest intentionally omits image-owned `Vulgata.Web`, causing CLR
  resolution to fail before runtime validation entered. The test-first repair
  declares and installs an exact six-name `/app` assembly resolver before any
  typed product reference; unknown names still fail closed, image inventory
  and collision checks remain authoritative, focused contract GREEN is 1/1,
  the minimized container now advances beyond stage 11, and the complete
  non-live harness remains GREEN 50/50. T072-T073 remain pending a fresh
  combined quality review over the corrected candidate.
- Quality iteration 16 accepted the image-owned assembly-resolver candidate
  with zero findings. The next live T072 attempt completed its mirror-backed
  no-cache build in about 3m40s and reached provisioner stage 23, where product
  Repository validation failed closed. A minimized same-image read-only probe
  reproduced Git's dubious-ownership rejection for each Windows bind-mounted
  Repository working tree. The regression-first correction publishes and
  configures only `/vulgata/sources/mbs_account/.git` and
  `/vulgata/sources/mbs_channel/.git` as safe directories in the ephemeral
  provisioner user's Git protected configuration before product validation;
  it uses no wildcard, does not mutate either read-only source, and preserves
  the exact three driver mounts and one-image boundary. The probe then resolved
  both accepted tags and full commits; focused contract 1/1, complete non-live
  harness 50/50, and Dockerfile contracts 3/3 are GREEN. Cleanup left no Story
  image/container/network/database/run-root residue. T072-T073 remain pending
  a fresh combined quality review over the corrected candidate.
- Quality iteration 17 accepted the exact mounted-Repository safety correction
  with zero findings. The following live T072 attempt passed Repository
  validation and remained CPU-active in product profiling, but the provisioner
  hit its unchanged 30-minute outer timeout before Worker/provider traffic.
  Cleanup again left no Story image, container, network, nonce database, or
  nonce run-root residue, and the external PostgreSQL 17 Alpine instance stayed
  healthy. Diagnosis found a structural budget mismatch: each whole-Repository
  CodeGraph profile independently permits 1,800 seconds, while the outer
  provisioner permits only 1,800 seconds total for two sequential profiles plus
  Pass 1, exact Cross-Repository Resolution, runtime Pass 2, and result
  publication. T072-T073 remain pending a reviewed course decision; no timeout
  extension, Scan filtering, or additional live retry has been inferred.
- The user approved the timeout-only recovery and requested current web
  research before another retry. The resulting 2026-08-10 technical report
  found no official CodeGraphContext 0.5.1 average, classified public timings
  as low-confidence evidence spanning tens of minutes to hours, and combined
  them with exact 2,556/6,771-file sample measurements plus the observed
  30-minute still-active profile. The bounded correction sets 14,400 seconds
  per Repository and 510 minutes only for `provisioner-run`; every other
  subprocess retains the 45-minute maximum. Contract RED was 0/1 on the absent
  timeout field and executor RED failed to compile on the absent provisioner
  ceiling; both tracer bullets are GREEN and the complete non-live release
  harness is 51/51. No `.cgcignore`, filtering, parsing, scheduling, grouping,
  retry, dispatch, migration/model, topology, provider, or package change was
  made. T072-T073 remain pending a fresh combined quality review and immutable
  candidate pin before the next live attempt.

### File List

- `docs/bmad/implementation-artifacts/11-5-demo-chat-citations-and-release-gate.md`
- `specs/008-demo-chat-release-gate/`
- `specs/008-demo-chat-release-gate/tasks.md`
