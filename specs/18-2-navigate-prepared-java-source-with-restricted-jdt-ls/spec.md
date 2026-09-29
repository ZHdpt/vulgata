# Feature Specification: Navigate Prepared Java Source with Restricted JDT-LS

**Feature Branch**: `codex/18-2-plan`
**Created**: 2026-09-29
**Status**: Draft
**Input**: Story 18.2 of the accepted October release definition: give source-analysis Workers real, bounded definition and reference navigation over an application-granted prepared Java Repository under the declared restricted profile.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate a Granted Repository (Priority: P1)

As a source-analysis operator, I want Workers to find definitions and references in a prepared Java Repository so that analysis can use actual source relationships while understanding where navigation is incomplete.

**Why this priority**: Real navigation, rather than successful startup alone, is the principal tool risk and value of Story 18.2.

**Independent Test**: Grant a small prepared Repository containing representative declarations and callers, then ask the production navigation capability for definitions and references. Verify that returned locations refer to the granted Repository and that observed limitations are stated.

**Acceptance Scenarios**:

1. **Given** an application-granted prepared Java Repository and an explicitly configured local JDT-LS/Java installation, **when** a navigation session starts, **then** it covers the whole bound Repository with separate disposable tool state and automatic Maven/Gradle import, build and dependency restoration disabled.
2. **Given** representative declarations and callers in that Repository, **when** definition and reference navigation runs, **then** actual navigation results identify granted Repository-relative locations and report applicable capability or classpath limitations.
3. **Given** a missing or incomplete navigation result, **when** it is returned to a Worker, **then** it is not presented as proof that a definition or relationship is absent, and the Worker can still use the separately granted Story 18.1 file tools.

---

### User Story 2 - Keep Navigation Within Its Grant (Priority: P2)

As a source-analysis operator, I want each navigation session and result bound to the selected Repository so that Workers cannot choose another workspace or use a result to read unrelated host files.

**Why this priority**: The capability is useful only if it preserves the prepared-source grant and shared host boundary.

**Independent Test**: Exercise a valid bound Repository, a foreign-root request, and a returned location outside the grant. Confirm that the valid location is usable and foreign or escaping locations are denied or explicitly withheld.

**Acceptance Scenarios**:

1. **Given** one selected Repository, **when** a Worker requests navigation, **then** the application-selected Repository and profile determine the session; model arguments cannot choose a physical workspace, another adapter or a foreign Repository.
2. **Given** an absolute, traversing, symlink-escaping or otherwise out-of-grant location, **when** navigation input or output is handled, **then** the caller receives no outside-root source disclosure or permission to read it.
3. **Given** overlapping requests for one Repository, **when** they run, **then** at most one owned navigation session exists for that Repository and shared operations that cannot safely overlap are serialized.

---

### User Story 3 - Recover From Limited or Failed Navigation (Priority: P3)

As a source-analysis operator, I want time-bounded navigation with clear failure outcomes so that a stalled or limited language tool does not mislead analysis or disrupt unrelated work.

**Why this priority**: The restricted profile can leave classpath gaps, and failed sessions must release owned resources.

**Independent Test**: Cause readiness failure, navigation failure, cancellation and timeout in the owned session. Verify explicit outcomes, bounded completion and cleanup without stopping an unrelated process; confirm file tools remain independently usable.

**Acceptance Scenarios**:

1. **Given** failed readiness, failed navigation, cancellation or timeout, **when** the operation finishes, **then** it reports the limitation or failure explicitly and settles its owned process and disposable state without affecting unrelated services.
2. **Given** navigation is unavailable or incomplete, **when** the Worker continues source analysis, **then** separately granted Story 18.1 list/read/search remains available; this limitation does not silently waive the separate required CodeGraph capability.
3. **Given** qualification of the declared host profile, **when** evidence is recorded, **then** it distinguishes successful process initialization from actual definition and reference navigation and describes observed gaps without claiming universal Java completeness.

### Edge Cases

- The local JDT-LS or Java installation is missing, misconfigured or cannot become ready: report a configuration/readiness failure without a synthetic navigation result.
- A declaration or caller depends on unavailable classpath information: preserve the observed result and limitation; never convert an empty result into an absence claim.
- A returned location has no safe relative mapping within the granted Repository: withhold the unsafe location and report the limitation.
- Requests overlap while one is cancelled or timed out: keep the session ownership limit, settle the affected operation and avoid leaking a stale result to another request.
- Prepared inputs become unavailable or known changed during operation: stop making source-inspection claims for that binding; do not silently switch to a different checkout or Repository.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST offer definition and reference navigation for an application-granted whole prepared Java Repository using the declared restricted JDT-LS profile. (O-FR06, O-AR09)
- **FR-002**: The system MUST require an explicitly configured local JDT-LS/Java installation and keep navigation tool state separate from prepared source. Automatic Maven/Gradle import, build and dependency restoration MUST remain disabled for this profile. (O-FR06; AD-15)
- **FR-003**: The system MUST bind the selected Repository, source root and navigation profile outside Worker/model arguments. It MUST reject arbitrary adapter, physical workspace and foreign-root selection. (O-NFR01; F0 source/tool contract)
- **FR-004**: The system MUST own at most one navigation session per selected Repository and serialize shared navigation operations when overlap is unsafe. (O-FR06)
- **FR-005**: Definition/reference results MUST expose only locations safely mapped to the granted Repository-relative source. Inputs and outputs MUST NOT grant traversal, symlink escape, host-file access, source writes or shell/command access. (O-NFR01)
- **FR-006**: Each navigation outcome MUST distinguish observed locations, missing results, capability/classpath limits and failures. An empty or out-of-scope result MUST NOT be interpreted as proof of absence. (O-FR06; FR-15.2)
- **FR-007**: Readiness and navigation MUST be time-bounded, support cancellation, and settle owned processes and disposable state after failure or cancellation without terminating unrelated services. (Story 18.2 acceptance)
- **FR-008**: When navigation is limited or unavailable, the caller MUST retain access to its separately granted Story 18.1 list/read/search capability. This fallback MUST NOT be treated as satisfaction of the separate native CodeGraph requirement. (O-FR04–O-FR06)
- **FR-009**: Qualification MUST exercise real definition and reference navigation in a representative prepared source case under the declared profile and report actual results and limitations separately from initialization. (O-AR09)

### Key Entities

- **Prepared Repository grant**: The application-selected Repository identity and contained prepared source root authorized for one analysis context; its source/tool contract is owned by Story 18.1 and the agreed F0 slice.
- **Navigation session**: One owned, bounded use of the declared language capability for a selected Repository, with replaceable tool state distinct from the prepared source.
- **Navigation outcome**: Observed definition/reference locations relative to the granted Repository, plus explicit capability, classpath, failure and limit information where applicable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a representative prepared Java case with known declarations and callers, both definition and reference requests return real, correctly bound locations; evidence records the observed results, rather than startup alone. (2 navigation kinds)
- **SC-002**: All tested foreign-root, escaping and unmappable locations are denied or withheld, with no outside-root source disclosed. (100% of the bounded negative cases)
- **SC-003**: In tested overlapping requests for one Repository, no more than one owned navigation session exists, and every request ends with a result, explicit limitation, cancellation or bounded failure. (100% of exercised requests)
- **SC-004**: Every exercised readiness, navigation, timeout and cancellation failure reports a truthful outcome and settles owned resources without stopping an unrelated process. (100% of the bounded failure cases)
- **SC-005**: The qualification report states the restricted profile, at least one observed navigation limitation or its absence in the sample, and the difference between initialization and actual navigation; it makes no completeness claim beyond the tested case.

## Assumptions

- The agreed F0 source/tool slice supplies the prepared Repository identity, root grant, relative-location and session-binding semantics. This story consumes them and does not redefine Story 18.1's list/read/search contracts.
- Story 18.1 is active elsewhere and is not yet an integrated, verified baseline. This specification may guide parallel planning against F0; delivery acceptance depends on reconciling the plan and affected gates against the integrated, verified 18.1 implementation.
- The October scope uses operator-maintained prepared directories and an explicitly configured local host profile. A supplied operator label is not a verified Git revision; this story does not add a Source Snapshot, directory-copying service or exact historical-source claim.
- This story adds no other LSP, dependency-aware import/restore, Docker packaging, UI, management CLI or CodeGraph implementation. The broader architecture's Docker target remains authoritative outside this October story's host qualification.
