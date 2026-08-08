# Feature Specification: Demo Chat, Citations, and Release Gate

**Feature Branch**: `codex/run-story-115-sprint-loop`

**Created**: 2026-08-04

**Status**: Draft

**Execution Level**: `expedited-mvp`

**Input**: Story 11.5 — prepared-scope Business/IT chat, grounded citations,
and the final August 15 demonstration release gate

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask About the Prepared Flow (Priority: P1)

As an authenticated user, I want to select the prepared System and its two
prepared Repositories and ask a question in Business Mode or IT Mode so that I
can understand the demonstrated Cross-Repository business flow at the level of
detail appropriate to me.

**Why this priority**: A useful answer over the prepared flow is the primary
user value of the demonstration.

**Independent Test**: Select each mode and the authorized prepared scope, ask
a bounded question about the known flow, and verify that the Chinese answer is
based only on current persisted knowledge from that scope and follows the
selected mode's presentation rules.

**Acceptance Scenarios**:

1. **Given** an authenticated user can access the prepared System and both
   prepared Repositories, **When** the user explicitly selects that scope,
   chooses Business Mode, and asks a bounded question, **Then** the answer is
   an understandable Chinese business narrative grounded in the current
   Documents, Document Sections, Source Anchors, flow/provenance evidence, and
   exact cross-Repository `REFERENCES` Edge in that scope.
2. **Given** the same authorized prepared scope, **When** the user chooses IT
   Mode and asks a bounded question, **Then** the Chinese answer provides
   technical detail grounded in the same current persisted evidence and may
   show Repository-relative source locators and line ranges directly.
3. **Given** the chat page is open, **When** the user prepares a question,
   **Then** explicit System and Repository selection is required and neither
   automatic scope selection nor file upload is available.

---

### User Story 2 - Verify Every Material Claim Safely (Priority: P1)

As a user, I want every material answer claim linked to persisted evidence so
that I can verify the answer without exposing information I am not authorized
to see.

**Why this priority**: Trustworthy citations and authorization are necessary
for the answer to be useful in an organization-controlled codebase.

**Independent Test**: Render answers containing multiple claims under fully
authorized, partially authorized, revoked, stale, incomplete, and malformed
evidence conditions; verify complete claim-to-evidence coverage for valid
answers and no disclosure or unsupported narrative for every invalid case.

**Acceptance Scenarios**:

1. **Given** a grounded answer has material claims, **When** it is rendered,
   **Then** every material claim maps to at least one current persisted
   Document and an available Source Anchor line range.
2. **Given** a grounded Business Mode answer, **When** citations are rendered,
   **Then** its narrative contains no inline code references, each material
   claim has a Chinese Document citation, and expanding that citation exposes
   the available persisted source-line range.
3. **Given** a grounded IT Mode answer, **When** citations are rendered,
   **Then** each material claim has a Document citation and the answer may show
   authorized Repository-relative paths and line ranges directly.
4. **Given** evidence is unauthorized, missing, stale, malformed, incomplete,
   or cannot support every material claim, **When** an answer would otherwise
   be rendered, **Then** the request fails closed with a bounded Chinese error,
   no unsupported answer claim, and no restricted existence or detail.

---

### User Story 3 - Prove the Prepared Release Path (Priority: P1)

As a demonstration owner, I want the full prepared two-Repository path proven
from a clean database and the final deployable image verified so that release
claims reflect an observed end-to-end result.

**Why this priority**: The deadline slice is complete only when the prepared
flow works through chat under release-equivalent conditions and leaves no
temporary verification state behind.

**Independent Test**: Start from a uniquely identified clean database, execute
the prepared two-Repository path through one cited Business or IT answer,
verify authorization-negative behavior and all included release checks, build
and start the final container image, then remove the disposable database and
confirm its absence.

**Acceptance Scenarios**:

1. **Given** the accepted prepared Repository revisions and a uniquely named
   clean database, **When** final verification runs, **Then** the path completes
   profiling, Pass 1, exact Cross-Repository Resolution, runtime prepared-flow
   binding, Pass 2, authorized retrieval, and one fully cited Business or IT
   answer without inferred graph truth.
2. **Given** the end-to-end path is exercised, **When** inaccessible scope is
   queried, **Then** the result is indistinguishable from missing scope and
   discloses no identity, metadata, citation, source, relationship, or business
   detail.
3. **Given** the release candidate is ready, **When** the included tests,
   authorization checks, migration checks, application build, applicable
   regression suite, final Docker image build, and container startup and
   health checks run, **Then** every check passes against the same accepted
   candidate boundary.
4. **Given** final verification has ended, whether successfully or not,
   **When** cleanup runs, **Then** the disposable database is removed and its
   absence is verified, no deliberately failing test remains, and post-demo
   limitations are reported separately from completed criteria.

### Edge Cases

- The user selects no scope, only one prepared Repository, a Repository outside
  the prepared pair, or a System that does not contain the selected pair: no
  broadening or automatic scope inference occurs, and a bounded Chinese
  guidance message is shown.
- Authorization is revoked after retrieval but before rendering: authorization
  is checked again, the answer is withheld, and the response reveals no
  restricted existence or detail.
- A current Document is superseded, a Source Anchor no longer has an available
  line range, or relationship evidence refers to a different Document Version:
  affected claims are not rendered.
- The answer contains a material claim with no citation, an unknown citation,
  a citation outside the selected scope, or a citation whose detail does not
  support the claim: the entire answer fails closed rather than rendering a
  partial narrative.
- The answer provider fails, times out, returns malformed content, or the user
  cancels: the user receives a bounded Chinese state with no raw diagnostics,
  credentials, partial answer, or stale result.
- Retrieved evidence includes an unauthorized Edge target: neither the target
  identity nor any related source, citation, or business detail is exposed.
- Release verification fails before cleanup: the failure remains visible as a
  failed gate, cleanup still occurs, and the feature is not reported complete.
- A prepared Pass 1 member predates the deterministic identity boundary, or
  its historical Worker job identity cannot be recomputed canonically from
  current product records: the path fails closed before Pass 2 dispatch or
  persistence and generates no Document.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide an authenticated Chinese chat experience
  for the prepared August 15 demonstration scope.
- **FR-002**: A user MUST explicitly select Business Mode or IT Mode, the
  prepared System, and both authorized prepared Repositories before submitting
  a question.
- **FR-003**: This feature MUST NOT provide automatic scope selection, default
  all-access scope, file upload, or scope beyond the prepared System and two
  prepared Repositories.
- **FR-004**: Retrieval MUST use only current, authorized, database-backed
  Documents, Document Sections, Source Anchors, flow/provenance evidence, and
  the exact cross-Repository `REFERENCES` Edge within the explicit scope;
  `index.md` MUST NOT be treated as canonical knowledge.
- **FR-005**: Repository authorization MUST be verified before retrieval and
  again before rendering, including visibility to both endpoints of any
  relationship evidence used in an answer.
- **FR-006**: Unauthorized or revoked scope MUST be missing-equivalent and MUST
  disclose no System, Repository, Logical Document, Document Version, Source
  Anchor, relationship, source text, citation, path, credential, or business
  detail.
- **FR-007**: Business Mode MUST return an understandable Chinese business
  narrative without inline code references.
- **FR-008**: Every material Business Mode claim MUST have at least one Chinese
  Document citation whose expandable detail exposes an authorized, available,
  persisted Source Anchor line range.
- **FR-009**: IT Mode MUST return a Chinese answer focused on technical detail;
  every material claim MUST have a Document citation, and authorized
  Repository-relative paths and Source Anchor line ranges MAY appear directly.
- **FR-010**: Every rendered material claim in either mode MUST map to at least
  one current persisted Document and an available Source Anchor line range that
  supports that claim.
- **FR-011**: Answer evidence MUST remain bound to the selected System,
  Repositories, current Document Versions, and exact relationship direction;
  narrative inference MUST NOT create or strengthen graph truth.
- **FR-012**: The complete answer and its claim-to-citation mappings MUST be
  validated before any answer claim is rendered.
- **FR-013**: Missing, stale, malformed, unsupported, out-of-scope, or
  incompletely cited answer content MUST fail closed with a bounded Chinese
  error and zero unsupported claims.
- **FR-014**: Provider failure, cancellation, and timeout outcomes MUST expose
  no partial answer, raw provider diagnostic, credential, absolute host path,
  unauthorized source, or stale result.
- **FR-015**: The final release path MUST begin from a uniquely named clean
  database and the accepted prepared revisions of the two organization-
  controlled Repositories.
- **FR-016**: The final path MUST complete profiling, Pass 1, exact
  Cross-Repository Resolution, Pass 2, authorized database-backed retrieval,
  and one fully cited Business or IT chat answer without guessed graph truth.
- **FR-017**: Final verification MUST prove that inaccessible scope is
  missing-equivalent and that no restricted evidence is retrieved or rendered.
- **FR-018**: Included tests, authorization checks, migration checks, the Web
  build, all applicable regression checks, one final Docker image build, and
  container startup and health evidence MUST pass against the accepted release
  candidate.
- **FR-019**: The disposable final-verification database MUST be removed after
  the run, including failed runs, and its absence MUST be verified.
- **FR-020**: No deliberately failing or unresolved RED test MAY be included in
  the accepted candidate.
- **FR-021**: Deferred or unsupported behavior MUST be reported as a truthful
  post-demo limitation and MUST NOT be represented as a completed acceptance
  criterion or inferred capability.
- **FR-022**: This feature MUST NOT add Scan scheduling, concurrency,
  retry/failover policy, general Cross-Repository matching, durable chat
  history, graph/dashboard behavior, live notifications, or any deferred Epic
  5–10 capability.
- **FR-023**: After the accepted exact Cross-Repository relationship is
  committed, the final path MUST bind the current authorized Pass 1 evidence
  and actual product-created identities into one server-owned runtime prepared
  Pass 2 flow. It MUST fail closed rather than use embedded or fabricated
  database identities.
- **FR-024**: Each newly dispatched ordinary Pass 1 Worker job in the prepared
  path MUST use a deterministic server-owned `JobId` bound to its actual
  Scan Execution, Scan Run, Repository, Code Unit, and accepted Profiled
  Revision through one versioned, domain-separated canonical identity rule.
  The runtime prepared-flow binding MUST independently recompute that same
  historical identity from current persisted product records and MUST fail
  closed before Pass 2 dispatch or persistence for unsupported legacy,
  substituted, drifted, malformed, or noncanonical identity evidence. This
  requirement MUST preserve the exact seven-slot Pass 2 payload and MUST NOT
  introduce a data migration or backfill, change retry policy, or expand
  general Scan scheduling, concurrency, failover, clustering, or matching.

### Key Entities

- **Prepared Chat Scope**: The explicitly selected authorized System and its
  two prepared Repositories that bound one demonstration question.
- **Grounded Answer**: A mode-specific Chinese answer whose complete set of
  material claims has passed validation against current authorized evidence.
- **Material Claim**: A factual statement about the prepared business or
  technical flow that must be supported by a citation before rendering.
- **Document Citation**: A user-visible reference from one or more material
  claims to a current persisted Document, with authorized Source Anchor detail.
- **Source Anchor**: The persisted Repository-relative source location and
  inclusive line range that supports a cited Document claim.
- **Evidence Packet**: The finite authorized set of current Documents,
  Document Sections, Source Anchors, flow/provenance evidence, and exact
  `REFERENCES` Edge facts available to ground one answer.
- **Runtime Prepared Pass 2 Flow**: The single consumer-owned flow bound from
  current authorized Pass 1 evidence, the accepted exact Cross-Repository
  relationship, and actual product-created identities, with fixed support from
  exactly the two accepted Repositories.
- **Release Verification Run**: The bounded clean-database execution of the
  prepared two-Repository path, release checks, final image verification, and
  verified cleanup.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For 100% of accepted prepared-scope questions, the rendered
  answer uses only current persisted evidence from the explicitly selected
  authorized System and two Repositories.
- **SC-002**: In both modes, 100% of rendered material claims map to at least
  one persisted current Document and one available supporting Source Anchor
  line range; 0 unsupported claims are rendered.
- **SC-003**: Business Mode displays 0 inline code references in its narrative
  while 100% of its material claims provide expandable Chinese Document
  citations with source-line detail.
- **SC-004**: Across unauthorized, revoked, stale, malformed, missing, and
  incompletely cited cases, 0 restricted facts and 0 partial or unsupported
  answer claims are rendered.
- **SC-005**: An authorized demonstration user can select the prepared scope,
  ask a bounded question, understand the Chinese answer, and open evidence for
  every material claim in one uninterrupted chat flow.
- **SC-006**: One final run from a uniquely named clean database completes the
  accepted two-Repository path through exactly one fully cited Business or IT
  answer, with 0 inferred Cross-Repository relationships.
- **SC-007**: 100% of included release checks pass against the same candidate,
  including authorization, migrations, application build, applicable
  regression, one final Docker image build, and container startup and health.
- **SC-008**: After every final verification attempt, 0 disposable databases
  from that attempt remain, and the cleanup evidence confirms their absence.
- **SC-009**: The release report lists 100% of known post-demo limitations
  separately and claims completion for 0 deferred Epic 5–10 capabilities.
- **SC-010**: The prepared-flow tests and final run resolve exactly one
  consumer-owned flow supported by exactly the two accepted Repositories; all
  missing, duplicate, stale, unauthorized, wrong-profile, wrong-direction,
  wrong-ref, and wrong-commit variants produce zero Pass 2 dispatches and zero
  generated Documents.
- **SC-011**: For 100% of prepared-path ordinary Pass 1 jobs, repeating the
  same Scan Execution, Scan Run, Repository, Code Unit, and accepted Profiled
  Revision boundary produces the same actual Worker job identity, changing any
  one bound input produces a different identity, and runtime binding
  independently reproduces the accepted identity. Every unsupported legacy,
  substituted, drifted, malformed, or noncanonical case produces zero Pass 2
  dispatches and zero generated Documents while the Pass 2 payload remains
  exactly seven slots and no migration, backfill, retry-policy, or general Scan
  expansion is introduced.

## Assumptions

- Stories 11.1 through 11.4 provide accepted current knowledge-store,
  sequential Scan, Pass 1, Pass 2, and exact Cross-Repository Resolution
  boundaries for the prepared two-Repository flow.
- The final path creates the prepared System and both prepared Repositories
  through product behavior, then binds the resulting identities only from
  current authorized Pass 1 evidence and the accepted exact relationship.
- Each required prepared-flow selector is expected to identify exactly one
  current, authorized match for the accepted profile, direction, refs, and
  commits; any missing or non-unique match prevents Pass 2 dispatch.
- Deterministic ordinary Pass 1 Worker job identity applies to newly dispatched
  work only. Existing persisted Documents are not rewritten or backfilled;
  prepared members without a canonically recomputable historical identity are
  unsupported and prevent Pass 2 dispatch.
- The two prepared Repositories, their accepted revisions, the prepared
  System, and the exact configured relationship are available and controlled
  by the organization before final verification begins.
- This feature's database-backed retrieval is a bounded Chat Agent capability
  over existing persisted knowledge. Any plan that adds or materially changes
  an agent tool remains subject to independent plan review and explicit
  planned-change authorization before task generation.
- Conversation persistence is not required for the deadline demonstration;
  switching mode may start a fresh transient interaction without implementing
  the full post-demo saved-session workflow.
- Automatic scope determination, defaulting to all accessible Systems, file
  upload, full chat-session behavior, broader retrieval tools, and all other
  deferred PRD behavior remain backlog.
- The final release report distinguishes observed acceptance evidence from
  limitations and does not treat the expedited-MVP boundary as completion of
  the full V1 product.
