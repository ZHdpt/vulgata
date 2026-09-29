# Feature Specification: Use Native CodeGraph MCP Against Prepared Source

**Feature Branch**: `codex/story-18-3-plan`

**Created**: 2026-09-29

**Status**: Draft — definition and planning only

**Input**: Story 18.3 in the October release epics, using the agreed source/tool F0 boundary. Story 18.1 is under implementation elsewhere; its integrated, verified source contract remains a completion dependency.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Explore the correct prepared Repository (Priority: P1)

As a Worker-tool integrator, I want a Worker to discover and invoke CodeGraph's native MCP tools for an approved prepared Repository, so that it can explore real indexed source without a custom graph-query substitute or access to another root.

**Why this priority**: Genuine native tool use and application-controlled source authority are the core value and safety boundary of this story.

**Independent Test**: With a small prepared fixture and an approved Repository binding, prepare its index, discover the native tools through the application's agent-tool integration, invoke a useful tool, and compare its returned source location/content with the fixture. Repeat with a foreign-root attempt and observe denial.

**Acceptance Scenarios**:

1. **Given** an application-approved prepared Repository root and a declared CodeGraph installation, **when** preparation and native MCP connection succeed, **then** the Worker-facing integration can discover and invoke granted native CodeGraph tools against that Repository.
2. **Given** a small indexed fixture, **when** a granted native tool is invoked, **then** its genuine result is observable with the correct Repository binding and usable source location or content; a CLI-only wrapper or CodeGraphContext result does not satisfy this outcome.
3. **Given** a caller attempts to select a foreign Repository, physical path, or different project through tool arguments, **when** discovery or invocation occurs, **then** the request is denied or confined to the application-selected prepared root, without disclosing outside-root source.

---

### User Story 2 - Know whether CodeGraph is usable (Priority: P2)

As an analysis operator, I want truthful readiness and failure outcomes, so that required CodeGraph problems are visible and analysis does not appear complete using only file tools.

**Why this priority**: Preparation, connection, and invocation can fail independently; hiding any required failure would misstate analysis capability.

**Independent Test**: Exercise each boundary with a valid fixture and controlled preparation, connection, invocation, and cancellation failures; observe distinct outcomes and independently usable Story 18.1 file tools.

**Acceptance Scenarios**:

1. **Given** preparation has not completed or fails, **when** readiness is queried, **then** CodeGraph is not reported ready, with an explicit bounded failure or limitation.
2. **Given** preparation succeeds but native connection or invocation fails, **when** the Worker requests a tool, **then** the failure is reported honestly and cannot silently become source-only analysis.
3. **Given** cancellation or a bounded failure during preparation, connection, or invocation, **when** work stops, **then** resources owned for that request are settled without stopping unrelated services; separately authorized file tools remain usable.

---

### User Story 3 - Qualify the selected host capability (Priority: P3)

As a delivery reviewer, I want evidence of the actual CodeGraph version and one native MCP smoke result in the selected host composition, so that readiness claims reflect real tool use.

**Why this priority**: Existing CLI/JSON behavior does not demonstrate native MCP connectivity.

**Independent Test**: On an owned small prepared fixture in the selected host composition, record the installed version, successful native discovery, one invocation/result, root binding, and any declared limitation, without a model/provider call.

**Acceptance Scenarios**:

1. **Given** the selected host composition and an owned fixture, **when** qualification runs, **then** evidence identifies the actual CodeGraph version and records native discovery and a useful invocation result.
2. **Given** only CLI preparation, a simulated MCP response, or an existing unit test, **when** capability is assessed, **then** it is not reported as a passing native smoke result.

### Edge Cases

- An approved binding points to missing, changed, or unreadable prepared source before or during preparation or invocation.
- An index is incomplete or a native tool returns no match; neither state proves source absence or complete semantic coverage.
- A native result includes an absolute or outside-root location, or embeds source beyond the granted binding and response limit.
- A caller reuses a stale tool handle after the application-selected Repository changes or access is revoked.
- A process exits unexpectedly, a request times out, or cancellation arrives while a tool is starting or returning output.
- Multiple requests for different prepared Repositories occur concurrently; one request must not inherit another's project binding or output.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST prepare the application-approved prepared Repository with the declared CodeGraph installation and make native MCP discovery and invocation available through the application's Worker agent-tool integration. (O-FR05; Story 18.3)
- **FR-002**: The system MUST bind each native CodeGraph operation to the application-selected approved Repository/root outside model-controlled arguments; a caller MUST NOT gain a different project or physical source path through tool input. (O-NFR01; AD-3/AD-6)
- **FR-003**: The system MUST expose only the granted native CodeGraph tool surface for the bound Worker purpose and constrain each response/work unit; native source results MUST obey the same source authority as the bound prepared root. (O-NFR01; AD-6)
- **FR-004**: The system MUST distinguish preparation, native connection, discovery, invocation, limitation, cancellation, and unavailable outcomes in readiness and tool results. A successful preparation or connection alone MUST NOT be reported as proven native invocation. (Story 18.3)
- **FR-005**: Required CodeGraph failure MUST remain explicit and MUST NOT silently select source-only analysis. Independently authorized Story 18.1 list/read/search capabilities MAY remain usable, but they do not satisfy CodeGraph readiness. (O-FR05; FR-4.9)
- **FR-006**: On failure, timeout, or cancellation, the system MUST settle resources it owns for the request and MUST NOT terminate unrelated services or leave a foreign-root binding usable. (Story 18.3; O-NFR01)
- **FR-007**: The system MUST provide reviewable evidence for the selected host composition that identifies the actual CodeGraph version and demonstrates native discovery, a useful invocation/result, and correct-root/foreign-root behavior against a small prepared fixture. (Story 18.3)
- **FR-008**: CodeGraph results MUST be treated as bounded source evidence with explicit limitations, not as exhaustive coverage or permanent Code Unit identity. (FR-4.9; AD-4/AD-6)

### Key Entities

- **Prepared Repository binding**: The application-approved Repository and root selected for this request under the source/tool F0 contract; it is not a model-selected physical path.
- **CodeGraph readiness**: The separately observable preparation and native connection/discovery capability for that binding, including limitations or failures.
- **Native tool grant**: The subset of CodeGraph's native MCP surface authorized for the Worker purpose and prepared binding.
- **Invocation outcome**: The bounded native result or explicit failure, with its binding and source location/provenance where supplied by the tool.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For one owned small prepared fixture, reviewers can observe one real native tool discovery and one useful native invocation/result against the approved Repository, with the actual installed CodeGraph version recorded.
- **SC-002**: All tested foreign-root, physical-path override, and stale-binding attempts are denied or confined to the approved root; none discloses outside-root source.
- **SC-003**: Controlled preparation, connection, invocation, timeout, and cancellation cases each produce an explicit outcome; none is recorded as ready after an unsuccessful required step or silently converted to source-only analysis.
- **SC-004**: For controlled failure/cancellation cases, owned request resources are settled and an independent authorized file-tool request still succeeds.
- **SC-005**: A reviewer can distinguish native MCP qualification evidence from existing CLI/JSON tests, simulated responses, and model/provider behavior without relying on inference.

## Assumptions

- The October proposal and agreed source/tool F0 contract authorize Story 18.3 definition and planning against a prepared Repository. Story 18.1 owns the approved prepared-root identity, binding, and contained list/read/search contract; this story consumes those contracts without defining a duplicate source service or changing 18.1's work.
- Story 18.1 is active elsewhere and has not been integrated or verified in this checkout. This specification does not claim its implementation or the native MCP capability is complete. Before later delivery, reconcile this spec and plan with the integrated, verified 18.1 baseline and rerun affected content and independent review gates.
- The selected host composition and declared CodeGraph installation are configuration/qualification inputs, not proof of availability. An actual version and native smoke result are required during later delivery; no runtime or provider qualification is claimed by this planning document.
- Story 18.3 does not add general registered-MCP product management, arbitrary external tools, JDT-LS, Knowledge storage, real model generation, or a new research/protocol framework. Those have separate ownership or backlog dispositions.
- Existing Git-bound Source Snapshot and CodeGraph CLI/JSON behavior remain historical/current implementation context. Prepared origin is a distinct October input, and CodeGraphContext or a CLI-only response wrapper cannot satisfy this story.
