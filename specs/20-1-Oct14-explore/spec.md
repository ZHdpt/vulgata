# Feature Specification: OCT-EXPLORE — Exact reader and bounded Knowledge graph

**Feature Branch**: `oct14/explore`

**Created**: 2026-10-06

**Status**: Validated for planning; implementation not authorized

**Input**: New October first-deliverable feature using direct SpecKit. Sources: [delivery plan](../../docs/bmad/planning-artifacts/october-delivery-plan-2026-09-30.md), [integration plan](../../docs/bmad/planning-artifacts/october-integration-plan-2026-10-06.md), [lane brief](../../docs/bmad/planning-artifacts/october-delivery/reader-graph-feature.md). Stories 18.6, 20.1 and 20.2 provide traceability, not inherited full execution scope or completion claims.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Read exact published Documents as an ordinary user (Priority: P1)

An authorized ordinary user browses a small selected publication or follows an exact link, without management permissions. IT reads CL/BL; Business reads BL only. The reader explains the exact version, structured sections and material qualifications.

**Why this priority**: The first real CL must be useful before graph or Chat completion.

**Independent Test**: Open two exact versions and their sections using an ordinary IT account against a labelled fixture; then repeat with actual published CL. Fixture wiring is not generation proof.

**Acceptance Scenarios**:

1. **Given** a permitted published CL, **when** an ordinary IT user browses and opens it, **then** its exact title, type, content and permitted qualifications appear without management authority.
2. **Given** an exact version/section link and a newer version, **when** opened, **then** the referenced version is displayed and its section focused.
3. **Given** an unknown, unpublished, withdrawn, wrong-publication or unavailable target, or missing section, **when** opened, **then** the reader shows a safe explicit failure without substituting another batch, version or source checkout.
4. **Given** no published BL, **when** Business browses, **then** it sees a truthful empty state with no CL metadata; once BL is available it can read permitted BL.
5. **Given** access to any source/advisory contributor is revoked, **when** another disclosure is requested, **then** content and metadata are withheld and affected display state is cleared when access loss is learned. Mode choice grants no authority.
6. **Given** a Document or one exact section exceeds one permitted response, **when** opened, **then** the reader shows an explicitly partial first portion and a keyboard-accessible next-portion action. Following it preserves the exact publication/version/section, narrative order and visible qualifications until the owner reports completion; no text is silently dropped or newer version fetched.
7. **Given** a next-portion request is cancelled, denied, expired or overtaken by a context change, **when** its response arrives, **then** no earlier or foreign portions are restored or mixed. A safe failure permits an explicit restart of the same exact target under fresh authority, never an automatic alternate selection.

### User Story 2 — Inspect accepted Knowledge relationships (Priority: P2)

A user opens a bounded graph of the same publication, inspects permitted nodes and directed relationships, then opens a Document through the shared reader.

**Why this priority**: Relationships make the working scenario navigable after exact reading works.

**Independent Test**: A labelled fixture supplies CL, BL, REFERENCES and GENERATED_FROM Edges, an Uncertainty and a disallowed contributor. Compare output in both modes; later inspect a real neighborhood against accepted evidence.

**Acceptance Scenarios**:

1. **Given** permitted Documents and accepted Edges, **when** the graph opens, **then** exact endpoints, type, meaning and direction are preserved.
2. **Given** a selected node, **when** opened, **then** it uses the same exact reader destination as a corresponding Chat reference.
3. **Given** shared helpers or missing targets, **when** projected, **then** no BL-to-BL dependency, runtime order or invented Edge appears; permitted Uncertainties remain visible.
4. **Given** Business Mode or disallowed contributors, **when** output is requested, **then** restricted nodes, Edges, titles, counts, topology, identifiers and tooltips never reach the user. Sparse/empty graphs are valid.
5. **Given** retrieval/display limits are reached, **when** results appear, **then** a visible incomplete indication states permitted scope and limits without restricted totals or a completeness claim.

### User Story 3 — Move and refresh a coherent view (Priority: P3)

A user pans, zooms, fits, selects and opens nodes with pointer or keyboard controls, and manually refreshes with clear loading/retry feedback.

**Independent Test**: Exercise movement and keyboard-only navigation plus overlapping refreshes, mode changes and failures; obsolete responses must never overwrite the active selection.

**Acceptance Scenarios**:

1. **Given** a graph, **when** layout or viewport changes, **then** domain Edges remain unchanged.
2. **Given** refresh succeeds, **when** shown, **then** nodes, Edges and qualifications replace the prior view together from one publication; failed refresh never mixes selections.
3. **Given** mode/scope change or access failure, **when** an older response arrives, **then** it is discarded without restoring prohibited display state.
4. **Given** a recoverable error, **when** retry is chosen, **then** the same explicit target is requested under live authorization with Chinese feedback and no implicit fallback.

### Edge Cases

- Malformed/forged selection, version, section, mode or excessive limits fail before protected metadata disclosure.
- A BL with one disallowed contributor is withheld as a whole; permitted fragments do not authorize composite prose.
- Cycles, self-references and multiple typed Edges terminate within declared bounds without changing meaning.
- Unknown content format fails visibly; retrieved markup and links cannot execute instructions or scripts.
- Missing Knowledge deployment leaves ordinary login/administration available and this feature unavailable, without fixture or legacy fallback.
- Refresh retains the explicit publication. A different publication requires explicit integration-owned selection change; old exact links remain exact where authorized membership remains available.
- Source unavailability is a qualification separate from Document availability. No source fetch repairs it.
- A section larger than one response is still the exact navigation target: focus its first loaded portion, label the section incomplete and continue within it. A missing section is an error, not a reason to show the whole Document instead. Invalid/tampered/expired continuation must not imply content completion.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Provide authenticated ordinary-user browse and exact version/section reads independent of management permissions (Story 1).
- **FR-002**: Bind discovery, graph and exact reads to explicit batch/publication membership; prohibit latest-version, previous-batch or current-source substitution (Stories 1–3).
- **FR-003**: Enforce live subject, purpose, mode and every source/advisory contributor before content/metadata disclosure; authorization outages fail closed (Stories 1–3).
- **FR-004**: Business is BL-only across all content, links, graph topology, metadata, counts and errors; IT may read permitted CL/BL (Stories 1–2).
- **FR-005**: Render immutable structured content, safe title/type, exact sections, permitted provenance and material gaps, Uncertainties, stale/partial/source-unavailable qualifications (Story 1).
- **FR-006**: Supply one exact reader destination for graph and Chat; validate that any section belongs to that exact version (Stories 1–2).
- **FR-007**: Project only accepted directed REFERENCES/GENERATED_FROM Edges over exact permitted endpoints from one publication. Shared helpers, similarity and layout never create relationships (Story 2).
- **FR-008**: Bound discovery, retrieval and displayed graph results; show limitations and displayed permitted counts without hidden-data totals (Story 2).
- **FR-009**: Provide pan/zoom/fit, selection/open, direction/type legends, keyboard alternatives and manual refresh; coherent replacement ignores obsolete responses (Story 3).
- **FR-010**: Preserve Chinese Fluent/Library Oak, Business/light and IT/dark, accessible loading/empty/unavailable/error/retry states (Stories 1–3).
- **FR-011**: Consume Knowledge-owned reads only; do not accept Documents, modify relationships, run Scans/source tools or create another Knowledge store (Stories 1–3).
- **FR-012**: Clearly label controlled fixtures and exclude them from production fallback. Final integration uses the same real selected publication as Chat (Stories 1–3).
- **FR-013**: Provide bounded exact-content continuation for oversized Documents and sections, with explicit next-portion/loading/completion states, preserved ordering and adjacent qualifications, live authorization per portion, and no silent truncation or context mixing (Story 1).

### Key Entities *(include if feature involves data)*

- **Knowledge Publication / Knowledge Read Context**: Explicit membership and version-bound authorized selection owned by Knowledge, not a wiki copy.
- **Document Version / Section**: Immutable CL/BL and its exact section identity.
- **Edge**: Accepted directed exact-version relationship with selected relationship evidence.
- **Uncertainty**: Unresolved permitted claim or limitation; never an invented Edge.
- **Graph view**: Bounded ephemeral presentation, without semantic acceptance or persistence authority.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All exact-link cases preserve requested version/section or fail explicitly; zero substitutions.
- **SC-002**: Both ordinary-user reader journeys succeed without management roles; every denial case exposes zero restricted content or metadata.
- **SC-003**: Every displayed Edge in the real case maps to accepted evidence with matching endpoints/type/direction; all corresponding graph/Chat links open the same exact Document.
- **SC-004**: Every bound/incomplete case displays its limitation; refresh tests show zero mixed-publication or obsolete-mode results.
- **SC-005**: Users can browse, inspect and open the small case by keyboard alone, with readable Chinese states in both themes.
- **SC-006**: Combined evidence includes a real cross-Repository case with readable CL/BL, an accepted relationship and a genuine Uncertainty. Fixture evidence is separate.
- **SC-007**: For a Document and an individual section larger than the response limit, ordered portions reproduce all permitted narrative text without gaps or duplicate narrative fragments. Applicable qualification context remains beside each fragment, intentionally repeated when a claim spans portions. Every context-switch/cancel/denial case shows zero foreign or previously cleared portions. Partial output is never presented as complete.

## Assumptions

- OCT-BUSINESS supplies compiled shared contracts, labelled fixtures, publication, persistence and live ordinary-reader authorization; these are dependencies, not claims of existing APIs. Existing Identity is reused.
- Integration owns selected publication configuration/shared Web composition. OCT-CHAT consumes the reader destination. Repository names/counts are not eligibility rules.
- Initial presentation caps: 40 Documents, 80 Edges, 20 browse items/page, lowered if shared limits are smaller. Caps never define accepted coverage or reveal hidden totals.
- Full version-history browsing, source viewer, advanced search/filters, interactive expansion, large graphs and live refresh are outside this first deliverable. PRD FR-9.4, FR-11.2/11.7 and FR-12.6 remain broader obligations; this feature does not amend them or claim their full completion.
- Planning approval does not authorize tasks, implementation, database/runtime operations or deployment.
