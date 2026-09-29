# Feature Specification: Contained Prepared Source Tools

**Feature Branch**: `detached-native-handoff` (feature selected by directory, not branch)
**Feature Directory**: `specs/016-contained-source-tools`
**Created**: 2026-09-28
**Status**: Draft
**Input**: Story 18.1, “Access Prepared Source Through Contained Read-Only Tools”; October requirements O-FR01, O-FR04 and O-NFR01

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inspect an approved Repository (Priority: P1)

As a batch operator, I need analysis to inspect only a Repository directory that the application and operator approved for the current batch, so that selected source can be understood without exposing unrelated host files.

**Why this priority**: Every source operation depends on a trustworthy Repository and root binding.

**Independent Test**: Grant one prepared directory containing a small source tree; list and read its files through the production capability, then request another Repository, a sibling directory and a write. Only permitted read results should be returned.

**Acceptance Scenarios**:

1. **Given** an approved batch, Repository and prepared root, **when** a caller lists the root and reads a file, **then** it receives only that Repository's entries and content with Repository-relative locations, and no physical host path is exposed.
2. **Given** a caller supplies a different Repository, batch, root, physical path or other foreign binding, **when** it requests source access, **then** access is denied without revealing that target's entries or content.
3. **Given** a path that traverses outside the root, a symlink that reaches outside it, or a requested write or command, **when** the caller attempts the operation, **then** the capability denies the attempt without accessing or changing the outside target.

---

### User Story 2 - Continue bounded discovery (Priority: P2)

As a Worker Agent examining approved source, I need bounded listing, ranged file reading and text search to reveal their limits and locations, so that I can continue inspection without mistaking a partial answer for complete coverage.

**Why this priority**: Whole-root source access must remain useful when files or result sets exceed a single response or other analysis tools have limited coverage.

**Independent Test**: Use a prepared tree with known and unfamiliar extensions, a long file and more list/search matches than fit in one response; follow returned continuations and ranges until completion, and compare the reported locations and completion states with the fixture.

**Acceptance Scenarios**:

1. **Given** entries of known and unfamiliar extensions, **when** the caller lists the granted root, **then** both kinds can be discovered with accurate Repository-relative locations; listing does not label any entry as admitted, supporting or excluded analysis work.
2. **Given** a file longer than one permitted response, **when** the caller reads a range, **then** it receives the requested bounded portion, its accurate file location and range, and explicit remaining-range or limit information sufficient to request another permitted portion.
3. **Given** matching text beyond one permitted response, **when** the caller searches the granted root, **then** matches carry accurate Repository-relative file and text locations, and the response identifies its limit and continuation so the caller can obtain further permitted matches.
4. **Given** a directory or search with no entries or matches, **when** the operation completes its permitted scope, **then** it explicitly reports complete empty; an incomplete, limited, unreadable or missing result is never represented as complete empty.

---

### User Story 3 - Understand interruption and source limits (Priority: P3)

As a batch operator, I need source operations to report denial, missing inputs, unreadable content and cancellation truthfully, so that analysis can distinguish evidence gaps from absence of source.

**Why this priority**: A false empty or silently truncated answer can mislead later analysis.

**Independent Test**: Exercise missing, unreadable, unsupported-text, changing and cancelled prepared inputs; inspect the public result and confirm that no out-of-root content or unsanitized host detail is returned and owned activity settles.

**Acceptance Scenarios**:

1. **Given** a missing or unreadable permitted entry, **when** listing, reading or searching encounters it, **then** the result identifies the applicable unavailable or incomplete condition without asserting that the entry or matches do not exist.
2. **Given** content that cannot be safely treated as searchable text, **when** text search or text reading reaches it, **then** the result reports the limitation and does not silently treat that content as a complete no-match result.
3. **Given** a caller cancellation or expired operation limit, **when** a source operation stops, **then** it returns a distinct sanitized interrupted or limited outcome, discloses no subsequent content, and releases its owned activity.

### Edge Cases

- An empty directory, an empty file at its end, and a completed no-match search are successful complete-empty outcomes for their respective scopes.
- A response stopped by page size, byte or work budget, deadline, unreadable entry or changing prepared input identifies the affected scope and is not complete.
- Requests with malformed relative paths, invalid ranges, absolute paths, traversal segments, symlinks or path aliases that escape containment cannot disclose outside content.
- A symlink may appear as a limited entry when safely identifiable, but it cannot become a route to outside content.
- A cursor or continuation from another batch, Repository, root or request scope is rejected rather than retargeted.
- Read and search remain available for an unfamiliar filename extension; text that cannot be decoded or safely searched is reported as limited or unsupported.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST expose source access only for an application/operator-approved batch, Repository and prepared directory binding; callers and Worker Agents MUST NOT select or replace the physical root through operation arguments. (O-FR01, O-NFR01)
- **FR-002**: Every public entry, match and read location MUST identify its Repository and path relative to the granted root, with accurate range or text-match location where applicable; physical host paths MUST NOT appear in public results. (O-FR01, O-FR04)
- **FR-003**: The capability MUST support bounded directory listing, ranged file reading and text search across the granted prepared tree, including files with unfamiliar extensions, independently of CodeGraph or LSP availability. (O-FR04)
- **FR-004**: Every list, read and search response MUST state whether its requested scope is complete, empty, limited or incomplete, and MUST provide an applicable continuation, remaining range or explicit stopping limit when more permitted inspection is possible. No bounded response may imply complete coverage without reaching it. (O-FR04, O-NFR01)
- **FR-005**: Listing MUST describe discoverable source entries without deciding which files are admitted, supporting or excluded from later analysis. (O-FR04)
- **FR-006**: A missing input, unreadable entry, unsupported text, invalid range and changed or unavailable prepared input MUST produce a truthful, distinguishable outcome for the affected operation or scope; none may be converted to a complete-empty answer. (O-FR04, O-NFR01)
- **FR-007**: A foreign batch, Repository, root or continuation binding, an out-of-root path or symlink traversal, and any requested write or command MUST be denied without disclosing or changing content outside the approved root. (O-NFR01)
- **FR-008**: Cancellation and operation or work limits MUST stop further disclosure, return a sanitized distinct outcome and release activity owned by the request. (O-NFR01)
- **FR-009**: Public failures and diagnostics MUST omit physical paths, outside-root metadata and sensitive source content beyond the caller's grant. (O-NFR01)
- **FR-010**: Prepared input labels MUST remain operator labels, not verified Profiled Revisions or Source Snapshots. This feature MUST NOT acquire source through Git, copy or hash the prepared tree, or create a source-retention service. (O-FR01)
- **FR-011**: Existing normal Git-bound source capture and read behavior MUST continue to operate as before for its existing callers. (Story 18.1 regression boundary)

### Key Entities

- **Prepared source grant**: The approved batch, Repository and prepared-root relationship and its read/work limits. The physical root is application-held authority, not a caller-selected public location.
- **Prepared input label**: Operator-provided identification for the prepared source. It does not prove a Git commit, exact Source Snapshot or retained version.
- **Source location**: A Repository-relative path plus an applicable file range or text-match position, never a host path.
- **Source access result**: The requested scope's content or entries, its completion/limit state, and any continuation or sanitized limitation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In the owned prepared-source fixture, 100% of permitted listed entries, read ranges and text matches report the correct Repository-relative location, including at least one unfamiliar extension.
- **SC-002**: For fixture lists, files and searches that exceed one response, following the advertised continuations or ranges reaches every permitted item or reports an explicit stopping limit; zero partial results are marked complete.
- **SC-003**: In the fixture matrix covering empty, missing, unreadable, unsupported-text, cancelled and limited operations, 100% of results distinguish complete emptiness from lack of complete inspection.
- **SC-004**: In the fixture matrix covering foreign bindings, traversal, escaping symlinks, writes and commands, zero attempts disclose or change content outside the approved root.
- **SC-005**: Existing normal Git-bound source read scenarios continue to pass without changed public behavior; the prepared-source scenarios run against small owned directories without a database, model provider, CodeGraph or LSP.

## Assumptions

- The operator supplies and keeps each approved prepared directory available and unchanged during its batch. If availability or contents change, the affected operation reports a truthful limitation; this feature does not claim stable historical source identity.
- A caller receives a grant through an application-owned boundary. This story supplies the contained source capability for that boundary; later batch setup and Worker integration stories may provide their own orchestration.
- Response sizes, work limits, continuation details and supported text encodings will be set during planning, while preserving the complete-versus-limited behavior above.
- This story adds no batch-management UI or CLI, database, LLM, CodeGraph, LSP, source copying, hashing, Git inspection or retention service.
