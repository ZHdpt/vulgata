# Feature Specification: OCT-BUSINESS — October Knowledge and Business Logic

**Feature Branch**: `oct14/business`
**Created**: 2026-10-06
**Status**: Validated specification; planning only
**Input**: New October first-deliverable feature using direct SpecKit. Deliver shared Knowledge, source-supported relationships and rooted Business Logic. Stop after independent plan acceptance, before tasks or implementation.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Open accepted Code Logic reliably (Priority: P1)

An operator finishes a selected file. An ordinary permitted IT user opens its explicitly published CL and exact sections with source provenance and coverage limitations.

**Why this priority**: Shared acceptance and reads unblock Scan, Chat and Explore.
**Independent Test**: Submit a labelled whole-file fixture, publish its exact membership and read through shared ports without a provider, Chat or graph.

**Acceptance Scenarios**:

1. **Given** an admitted file with trusted successful completion and two valid Code Units, **when** accepted, **then** its units, CL versions, observations and coverage are stored atomically; ordinary reads expose none before explicit publication.
2. **Given** identical replay or lost acknowledgement, **when** the exact operation is resolved, **then** the same handles return without duplicate content or generation; changed payload under the same key is rejected.
3. **Given** unfinished analysis, a stop fence or deadline winning before commit submission (even after completed analysis), invalid anchors or only a model completion assertion, **when** submitted, **then** no partial Knowledge becomes visible. A genuinely complete zero-unit result records coverage without inventing a Document.
4. **Given** an ordinary permitted reader, **when** a published CL is opened in IT Mode, **then** access succeeds without operator authority; Business Mode exposes no CL content or metadata.

### User Story 2 — Understand relationships and unknowns (Priority: P2)

A reader sees which accepted Documents are connected, why the connection is supported and what remains unknown.

**Why this priority**: Cross-Repository explanations need evidence rather than plausible links.
**Independent Test**: Resolve a fixed labelled corpus with a supported pair, an ambiguous candidate and a cycle; verify directed relationships and Uncertainties without BL generation.

**Acceptance Scenarios**:

1. **Given** a fixed authorized corpus, **when** bounded resolution completes, **then** each selected matching item has an outcome with exact inputs and supporting/opposing evidence.
2. **Given** a lone candidate, shared name, advisory assertion or confidence without decisive evidence, **when** evaluated, **then** the result remains uncertain and asserts neither deployed routing nor runtime execution.
3. **Given** a supported cross-Repository relationship, **when** read in its publication, **then** direction, meaning and exact endpoints are preserved without rewriting caller CL.

### User Story 3 — Read useful, distinct Business Logic (Priority: P3)

A Business user reads a source-backed explanation of conditions, alternatives, effects and gaps, and follows permitted BL references.

**Why this priority**: This supplies the first business-facing outcome from the foundation.
**Independent Test**: Pin Documents/relationships for two roots sharing a helper, synthesize BL and verify distinct Logical Flows and coherent publication. Qualify real-provider usefulness separately.

**Acceptance Scenarios**:

1. **Given** distinct source-backed roots sharing helpers or cycles, **when** flows are planned, **then** roots remain distinct, local-only flows qualify and traversal terminates.
2. **Given** document-first synthesis, **when** published, **then** BL content, exact dependencies and relationship/Uncertainty selection become visible atomically; failed drafts remain invisible.
3. **Given** a Business reader, **when** searching, opening or following references, **then** only permitted BL and business explanations appear; CL titles, identifiers, paths and counts do not leak.
4. **Given** advisory context supplies a business motive that source does not establish, **when** CL becomes BL and a consumer excerpt, **then** the affected statement remains visibly advisory; mixed source/advisory claims are split or qualified.
5. **Given** a contributor is revoked or authority cannot be checked, **when** any content, metadata or continuation is requested, **then** disclosure fails closed without another batch/publication fallback.

### Edge Cases

- Wrong database/batch/publication/version/section: unavailable, never latest/legacy fallback.
- Concurrent replay/conflict or unavailable commit lookup: exact reconciliation or indeterminate, never regenerate. A submitted commit may settle after cancellation; absence during an in-flight transaction cannot prove noncommit. Freeze waits for known outcomes.
- Seal issued, then drain expires, then write attempted: reject even though analysis completed. Cap=1 with concurrent inquiries permits one active call; hard stop cancels active work and denies queued/retry calls without misreporting unknown usage as zero.
- Nested units/shared anchors are valid; ambiguous semantic keys and out-of-file anchors are invalid.
- Access lost during materialization/paging: final recheck; no partially authorized response.
- Failed root files remain coverage gaps. A graceful operator stop closes admission, settles active writes and explicitly accounts for unprocessed/cancelled files before freezing the accepted subset; paused, unsettled or commit-indeterminate work is not finalizable.
- Missing deployment bindings, exhausted budgets and incomplete tools remain unknowns, not negative proof.
- Supplemental accepted Documents actually used enter dependencies even when not cited.
- Log failure cannot erase committed Knowledge or turn an ambiguous write into a known failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Accept a complete admitted file atomically using trusted application admission/completion bound to its exact proposal and current write eligibility at commit submission; completed analysis alone cannot override a fence, deadline or authority loss. Model output grants no authority.
- **FR-002**: Knowledge derives semantic Code Unit/Document identities, validates Source Anchors and retains immutable structured content, exact provenance, observations, Uncertainties and truthful coverage, including zero-unit outcomes.
- **FR-003**: Mutations have scoped unique operation keys. Identical replay returns committed results; conflicting replay fails. Resolve ambiguous acknowledgement by exact lookup without regeneration; unavailable lookup is indeterminate.
- **FR-004**: Publish explicit immutable membership. A settled nonempty CL-only selection is usable before relationship resolution or BL generation, with truthful coverage; no Business provider call or B1/B2 prerequisite is required. Unpublished and failed drafts stay invisible. Never infer latest/current selection, fabricate legacy origins or substitute another batch.
- **FR-005**: Supply shared bounded browse/search, exact Document/section and accepted-related reads with stable publication, provenance and limitations. Totals/cursors cannot reveal hidden records.
- **FR-006**: Enforce live subject, purpose, mode and every source/advisory contributor before applicable acceptance/publication and every disclosure. Ordinary reading is independent of operator management authority; denial/outage fails closed.
- **FR-007**: Business Mode exposes BL only, including titles, counts, references and related targets. Internal CL dependencies participate in authorization without becoming Business metadata.
- **FR-008**: Resolve finite pinned accepted evidence with bounded inquiry. Every matching/synthesis provider attempt consumes the host-supplied shared capacity, provider request/rate limits and remaining budget/deadline, respects hard stop, and reports actual or explicitly unknown usage; no independent multiplied pool is allowed. Retain inputs, supporting/opposing evidence and outcomes; lone candidates, names, confidence or context cannot alone establish relationships/deployment facts.
- **FR-009**: Preserve distinct source-backed Logical Flow roots and bounded directed membership, branches, cycles and uncertain frontiers. Shared helpers/connectivity do not merge business purposes.
- **FR-010**: Generate BL from accepted Documents/relationships and selected advisory context; explain conditions, alternatives, effects and gaps; track all used evidence. Synthesis cannot rewrite CL, create authoritative Edges or schedule scans.
- **FR-011**: Publish each flow's BL, exact dependencies and relationship/Uncertainty selection atomically. Preserve immutable history without retargeting dependencies.
- **FR-012**: Consumers share one Knowledge contract/store. The Knowledge owner serializes October persistence/migrations including Chat dialogue contributions; Chat retains conversation behavior ownership.
- **FR-013**: Preserve sanitized append-only operation/target/timestamp logs. Exclude secrets, raw source and hidden reasoning. Treat Documents/source/context as evidence, not instructions.
- **FR-014**: Distinguish labelled fixtures, real-provider usefulness and integrated real-source delivery. Structural success cannot claim generated Knowledge or release completion.
- **FR-015**: Preserve material source-supported, advisory, hypothesis and Uncertainty qualifications at each affected section or statement group through CL, BL and all consumer projections. Acceptance never upgrades an advisory assertion into a source fact; materially mixed claims are split or explicitly qualified.
- **FR-016**: On natural analysis finish or graceful operator stop, settle active writes and record every selected input disposition before freezing the accepted corpus. Unprocessed/cancelled/failed files remain visible coverage gaps. Paused, unresolved in-flight or commit-indeterminate work blocks finalization, without exposing drafts.

### Key Entities

- **Accepted File Manifest**: complete exact file result with Code Units, CL, observations and coverage.
- **Code Unit / Logical Document / Document Version**: semantic identity, enduring explanation identity and immutable content respectively.
- **Knowledge Publication**: fixed explicit content, relationship and dependency selection.
- **Edge / Uncertainty**: accepted directed evidence or an explicitly unresolved claim.
- **Logical Flow / Flow Group Version**: source-backed purpose and pinned bounded membership.
- **Read selection**: fixed publication, scope, purpose and mode, never a lasting permission grant.
- **Conversation/turn outcome**: Chat-owned ordered dialogue and idempotent result persisted through the sole October schema owner.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All controlled acceptance/replay/conflict/zero-unit/unfinished and sealed-before-fence scenarios produce expected results with zero duplicated acceptance and zero exposed drafts.
- **SC-002**: All wrong-selection, revoked-contributor and Business CL probes disclose zero forbidden content/metadata; an ordinary permitted non-operator reads the wiki.
- **SC-003**: One coherent real case across two Repositories yields useful CL, at least one supported cross-Repository relationship, one honest Uncertainty and rooted BL. Source review verifies conditions, alternatives and effects; missing facts remain unknown.
- **SC-004**: Two roots sharing a helper remain two Logical Flows; cycles terminate within declared bounds and each selected matching item has a disposition. With configured provider capacity one, concurrent inquiry/synthesis attempts never overlap; hard stop admits zero queued/new calls and usage remains actual or explicitly unknown.
- **SC-005**: Reader, Chat references and graph endpoints resolve identical permitted exact versions; failed publication exposes no mixed content/dependency set.
- **SC-006**: Handoff records actual coverage, qualification boundaries and dependencies without claiming full original-story or generic-product completion.

## Assumptions

- Local October delivery/integration/Business inputs define the subset. Original 18.5/18.9–18.11 provide provenance only; compatible integrity/security/runtime rules remain.
- Real Identity/System/Repository records and authorized source/provider/database targets are runtime prerequisites, not supplied or authorized by planning.
- Scan owns admission/completion and generator execution; Explore owns the shared reader; Chat owns tools/events/history; integration owns shared Web composition.
- Two Repositories are the minimum demonstration, never an eligibility rule.
- Deferred: durable restart, cross-batch lineage, mutable-head competition, visibility epochs, universal matching, advanced graph/history, source-tool Pass 2, recursive scans and external operational tools.
- This session ends after independent plan acceptance with no tasks, implementation or agent-context edits.



