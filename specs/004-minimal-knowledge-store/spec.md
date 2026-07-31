# Feature Specification: Minimal Knowledge Store and Retrieval Projection

**Feature Branch**: `codex/story-11-1-knowledge-store`

**Created**: 2026-07-30

**Status**: Draft

**Input**: User description: "For Story 11.1, durably store the prepared-demo
minimum of Logical Documents, immutable Document Versions, source anchors,
exact REFERENCES and GENERATED_FROM Edges, narrow flow evidence, and
database-backed retrieval projections so an authorized SystemOwner can retrieve
a traceable current Cross-Repository business flow."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Commit Traceable Knowledge Atomically (Priority: P1)

As a SystemOwner, I want a generated Code Logic Document or Business Logic
Document committed with its traceability facts as one durable unit so the
prepared demonstration never observes partial or misleading knowledge.

**Why this priority**: Durable, internally consistent knowledge is the minimum
foundation for every later retrieval and demonstration behavior.

**Independent Test**: Commit a bounded generated Document with source anchors,
permitted Edges, narrow flow evidence, an index entry, and ordered sections;
then inject a failure at each validation and persistence boundary. Verify one
complete initial version is visible after success and no new knowledge-store
fact is visible after any failure.

**Acceptance Scenarios**:

1. **Given** a valid bounded Code Logic Document commit, **When** it is
   accepted, **Then** one Logical Document, one immutable current Document
   Version, its content, source anchors, permitted Edges, index entry, and
   ordered sections become visible atomically.
2. **Given** a valid bounded Business Logic Document commit for the prepared
   flow, **When** it is accepted, **Then** its exact GENERATED_FROM Edges and
   its flow group, traces, and ordered trace steps become visible in the same
   atomic result.
3. **Given** any invariant violation, missing Edge endpoint, ownership
   mismatch, oversized value, excessive collection, or persistence failure,
   **When** the commit ends, **Then** none of its new Logical Document,
   Document Version, anchor, Edge, flow, index-entry, or section facts are
   visible.

---

### User Story 2 - Retrieve Current Authorized Knowledge (Priority: P1)

As an authorized SystemOwner, I want the current version of a Logical Document
with bounded traceability and retrieval sections so I can inspect the prepared
Cross-Repository business flow without reading an export as canonical state.

**Why this priority**: The August 15 demonstration depends on retrieving
grounded, current knowledge while preserving Repository visibility.

**Independent Test**: Retrieve the same Logical Document under authorized,
partially authorized, unauthorized, and revoked Repository scopes. Verify the
current version and requested bounded details are returned only as permitted,
and unauthorized Edge targets disclose no restricted identity or content.

**Acceptance Scenarios**:

1. **Given** an authorized Repository scope and a Logical Document with
   history, **When** current knowledge is requested, **Then** exactly its
   current Document Version is materialized with content, source anchors,
   permitted Edges, narrow flow evidence, its index entry, and only the
   requested ordered sections.
2. **Given** a REFERENCES Edge whose target Repository is not authorized,
   **When** the source Document is retrieved, **Then** the relationship may be
   represented as redacted but no target Repository, Logical Document,
   Document Version, title, path, source anchor, content, or business behavior
   is disclosed.
3. **Given** the source Repository is unauthorized or authorization was
   revoked, **When** current knowledge is requested, **Then** no Document
   existence, version, content, anchor, Edge, flow, index, section, path,
   source text, secret, or diagnostic is disclosed.
4. **Given** a retrieval request, **When** results are materialized, **Then**
   every result remains Repository-scoped, finite, and free of absolute host
   paths, secrets, source text outside an explicitly accepted source anchor,
   and unbounded diagnostics.

---

### User Story 3 - Preserve History and Rebuild Retrieval Truth (Priority: P2)

As a SystemOwner, I want regenerated knowledge to preserve its Version Chain
and allow retrieval projections to be rebuilt so current answers remain
searchable without rewriting historical evidence.

**Why this priority**: The demonstration must distinguish current knowledge
from retained history and recover derived retrieval data safely.

**Independent Test**: Commit a second version for an existing Logical Document,
retrieve the current version, inspect both historical versions and their
version-to-version facts, and rebuild the newer version's projections twice,
including one injected failure.

**Acceptance Scenarios**:

1. **Given** a Logical Document with a current Document Version, **When** a new
   version is committed, **Then** the new immutable version becomes current,
   the previous version remains intact, and the two are linked through the
   Version Chain.
2. **Given** historical source anchors, Edges, and flow evidence, **When** a
   later version is committed, **Then** the historical facts retain their exact
   original Document Version endpoints and are neither deleted nor rewritten.
3. **Given** a committed Document Version, **When** its retrieval projection is
   rebuilt, **Then** only that version's `index_entries` and
   `document_sections` projection is replaced from canonical Document content,
   without reading or parsing `index.md`.
4. **Given** projection rebuilding fails, **When** the operation ends, **Then**
   the last complete projection remains observable and no partial replacement
   is visible.

### Edge Cases

- A commit refers to a nonexistent, non-current, or unauthorized Edge endpoint:
  the commit is rejected atomically; endpoints are never inferred from
  narrative content.
- A REFERENCES Edge crosses Repositories: it remains a REFERENCES Edge, and
  Cross-Repository status is derived from endpoint Repository identity rather
  than stored as another Edge type.
- A GENERATED_FROM Edge points anywhere except from a Business Logic Document
  Version to a Code Logic Document Version actually used in synthesis: the
  commit is rejected.
- A flow-trace step has a duplicate, missing, or non-contiguous order, or refers
  to a Document Version or Edge outside the committed trace evidence: the
  commit is rejected atomically.
- A new version races another new version for the same Logical Document: at
  most one becomes current; the losing commit leaves no partial facts and must
  retry against the observed current version.
- Structured content, Markdown, a section, an evidence locator, or a collection
  is empty where required, malformed, or beyond its finite acceptance limit:
  the request is rejected before any new fact becomes visible.
- A rebuild is repeated without content changes: it produces one equivalent
  complete index entry and the same ordered section set, with no duplicates.
- A requested section does not exist or the request exceeds the retrieval
  limit: no unrelated section is returned, and an over-limit request is
  rejected rather than silently expanded.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST keep stable Logical Document identity separate
  from immutable Document Version identity for both Code Logic Documents and
  Business Logic Documents.
- **FR-002**: An initial commit MUST make one Logical Document, one current
  Document Version, canonical structured content, deterministic Markdown
  content, schema identity, source anchors, permitted Edges, permitted narrow
  flow evidence, one index entry, and ordered sections visible atomically.
- **FR-003**: A failed validation or persistence operation MUST leave zero
  partial facts from the attempted initial commit or new-version commit.
- **FR-004**: Every Document Version MUST be immutable after commit. A later
  generation MUST insert a new version, preserve the old version and its
  attached facts, link the old version to the new version through
  `superseded_by_id`, and leave exactly one current version for the Logical
  Document. Updating the predecessor's Version Chain link is the sole permitted
  historical metadata transition; its content and evidence remain immutable.
- **FR-005**: Every source anchor MUST identify the Repository, scanned commit,
  Repository-relative file path at scan time, Code Unit, inclusive line range,
  source or snippet hash, and exact Document Version making the claim.
- **FR-006**: A REFERENCES Edge MUST connect the exact source and target
  Document Versions that hold the dependency fact; Cross-Repository status
  MUST be derived from their Repository identities.
- **FR-007**: A GENERATED_FROM Edge MUST connect the exact Business Logic
  Document Version to each Code Logic Document Version actually used in its
  synthesis.
- **FR-008**: The system MUST reject an Edge whose endpoints do not exist,
  whose endpoint types violate its Edge semantics, or whose ownership cannot
  be validated within the authorized commit scope.
- **FR-009**: Flow evidence in this feature MUST be limited to `flow_groups`,
  `flow_traces`, and ordered `flow_trace_steps` for the prepared demonstration
  flow.
- **FR-010**: Each flow-trace step MUST have one unambiguous contiguous order
  within its trace and MUST reference the exact Document Version and, when the
  step follows a persisted relationship, the exact permitted Edge.
- **FR-011**: The system MUST materialize exactly one bounded document-level
  `index_entries` projection per Document Version from that version's metadata
  and canonical structured content.
- **FR-012**: The index entry MUST preserve the Document, Document Version,
  Repository, System, Document type, title, Repository-relative path, bounded
  summary, bounded key symbols, bounded business and technical terms, and
  bounded searchable text needed for later retrieval.
- **FR-013**: The system MUST materialize `document_sections` from the canonical
  ordered sections of a Document Version, preserving stable section identity,
  order, kind, heading, bounded Markdown, bounded search text, and bounded
  references to source anchors and Documents.
- **FR-014**: Projection rebuilding MUST replace only the index entry and
  sections for the specified immutable Document Version, MUST NOT mutate its
  canonical content or historical facts, and MUST NOT read or parse `index.md`.
- **FR-015**: Projection replacement MUST be atomic; failure MUST preserve the
  last complete projection and expose no partial or duplicate projection.
- **FR-016**: Current-version retrieval MUST return a materialized, finite
  result containing exactly one current Document Version and only the requested
  authorized source anchors, Edges, narrow flow evidence, index entry, and
  sections.
- **FR-017**: Repository authorization MUST be the outer retrieval boundary.
  Document access MUST inherit its Repository visibility, and Edge target
  details MUST be disclosed only when both endpoint Repositories are
  authorized.
- **FR-018**: When an Edge target is unauthorized, the system MUST redact every
  target identity and behavioral detail while preserving only a non-sensitive
  indication that a restricted relationship exists when the caller is allowed
  to view the source.
- **FR-019**: Stored and returned values MUST exclude absolute host paths,
  secrets, attacker-selected open-ended type values, source text outside an
  explicitly accepted authorized anchor, and unbounded diagnostics.
- **FR-020**: A single commit request MUST accept no more than 1 MiB of
  structured Document content, 1 MiB of Markdown, 256 sections, 64 KiB of
  Markdown per section, 512 source anchors, 1,024 Edges, 32 flow groups, 64
  traces per flow group, and 256 steps per trace; all text is measured as
  UTF-8 bytes and over-limit input MUST be rejected atomically.
- **FR-021**: Each source-anchor evidence locator, title, heading, summary,
  symbol, business term, and technical term MUST be capped at 4 KiB of UTF-8
  text, and each collection of symbols or terms MUST contain no more than 256
  items.
- **FR-022**: One retrieval request MUST return no more than 100 explicitly
  requested sections, 512 anchors, 1,024 authorized-or-redacted Edges, 32 flow
  groups, 64 traces per group, and 256 steps per trace; an over-limit request
  MUST be rejected rather than returning an unbounded or silently truncated
  result.
- **FR-023**: Candidate selections, dispatch bindings, endpoint bindings, full
  flow clustering, Uncertainty infrastructure, reverse-index impact analysis,
  virtual-tree behavior, and general export behavior MUST NOT be added by this
  feature.
- **FR-024**: This feature MUST NOT add or change a public retrieval API, Chat
  Agent tool, Worker Agent tool, Scan strategy, parsing, filtering,
  scheduling, grouping, retry, dispatch behavior, provider behavior, UI, or
  Story 4.3 behavior.
- **FR-025**: This feature MUST preserve the exact Story 4.3 integration
  boundary and add no claim that deferred Story 5.3, Story 5.4, or Epic 5-10
  behavior is complete.

### Key Entities

- **Logical Document**: The stable semantic slot for one Code Logic Document or
  Business Logic Document across generations; it identifies which immutable
  Document Version is current.
- **Document Version**: An immutable generated snapshot with canonical
  structured content, deterministic Markdown, schema identity, Repository and
  Scan identity, and one place in the Version Chain.
- **Source Anchor**: Exact traceability from a claim in one Document Version to
  a Repository, scanned commit, Repository-relative file, Code Unit, inclusive
  line range, and integrity hash.
- **Edge**: A typed directed relationship between exact Document Versions,
  limited here to REFERENCES and GENERATED_FROM.
- **Flow Group**: The prepared Business Logic Document's bounded grouping of
  persisted flow evidence.
- **Flow Trace**: One bounded path of evidence within a Flow Group.
- **Flow Trace Step**: One ordered step that points to an exact Document
  Version and, when applicable, an exact permitted Edge.
- **Index Entry**: The canonical bounded document-level searchable projection
  for one Document Version.
- **Document Section**: One ordered bounded retrieval unit materialized from a
  Document Version's canonical sections.

### Deferred Scope

- Candidate selection, dispatch-site binding, endpoint binding, general flow
  clustering, and the remaining full Story 5.3 behavior are post-deadline work.
- Uncertainty persistence and resolution, reverse-index impact analysis,
  virtual-tree display, `index.md` or other exports, and full Story 5.4
  behavior are not delivered here.
- User-facing retrieval, grounded chat, citations, Agent tools, public APIs,
  Worker Agent execution, Scan orchestration, exact RPC/HTTP endpoint matching,
  and the final two-Repository demonstration belong to later Epic 11 stories.
- The final clean-database two-Repository end-to-end rehearsal and final
  container verification remain Story 11.5 responsibilities.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every valid initial acceptance fixture, exactly one complete
  Logical Document and current Document Version are observable with 100% of
  supplied permitted anchors, Edges, flow evidence, index entry, and sections.
- **SC-002**: Across all injected validation and persistence failures, zero new
  partial Logical Documents, Document Versions, anchors, Edges, flow facts,
  index entries, or sections are observable.
- **SC-003**: After any number of accepted regenerations, 100% of prior
  Document Versions and their original content and evidence remain retrievable
  as history, while exactly one version per Logical Document is current.
- **SC-004**: In all provenance fixtures, 100% of REFERENCES and
  GENERATED_FROM Edges and ordered flow steps point to the exact expected
  Document Versions, with zero narrative-inferred or wrong-version endpoints.
- **SC-005**: Rebuilding a projection one or more times yields exactly one
  equivalent index entry and one complete ordered section set for the target
  Document Version, reads zero facts from `index.md`, and changes zero
  canonical or historical facts.
- **SC-006**: Authorized current-version retrieval returns 100% of requested
  in-limit materialized facts; unauthorized source access returns zero
  knowledge details, and unauthorized Edge targets expose zero target identity
  or behavioral details.
- **SC-007**: Every accepted or returned payload complies with all FR-020
  through FR-022 limits; every over-limit acceptance fixture is rejected with
  zero partial state.
- **SC-008**: Acceptance verification creates zero candidate-selection,
  dispatch-binding, endpoint-binding, Uncertainty, reverse-index,
  virtual-tree/export, public API, Agent-tool, Scan-strategy, or Story 4.3
  behavior.

## Assumptions

- Story 4.3 is complete at
  `b851ba165d6ebe560227bfdc3b206a814280e9b2` and supplies the unchanged
  Repository, ScanRun, and completed profiling boundary consumed by this
  feature.
- The prepared demonstration provides already generated Code Logic Documents,
  one Business Logic Document, and validated exact relationship and flow
  evidence; generation and candidate discovery are outside this feature.
- Existing Repository ownership and visibility decisions are authoritative;
  this feature preserves the Repository identity required to apply them and
  introduces no new authorization policy.
- The finite limits in FR-020 through FR-022 are conservative deadline defaults
  for safe, deterministic acceptance. Later accepted plans may lower them but
  may not make any input or output unbounded.
- `index_entries` and `document_sections` are retrieval truth. `index.md` is at
  most a later export artifact and is never an input to this feature.
- `execution_level: expedited-mvp` shortens workflow ceremony only; it does not
  weaken immutability, authorization, redaction, migration safety, atomicity,
  deterministic provenance, or the prohibition on failing acceptance tests.
