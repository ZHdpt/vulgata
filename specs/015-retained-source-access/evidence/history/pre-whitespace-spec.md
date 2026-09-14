# Feature Specification: Retained Source Access

**Feature Branch**: `dev` (no feature branch created)  
**Created**: 2026-09-14  
**Status**: Draft  
**Input**: Story 13.1: retain the exact committed source used for authorized Repository analysis and expose it through scoped, bounded, read-only access.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Capture an Exact Source Snapshot (Priority: P1)

As a System Owner, I can have an authorized Repository, including a standalone Repository, captured at one exact permitted Git commit so later analysis has a stable source identity even if a ref or working copy changes.

**Why this priority**: Every later source read depends on a trustworthy, exact, independently owned Source Snapshot.

**Independent Test**: Use an authorized local Git Repository and permitted ref, capture it, change the ref and working copy, and verify that the completed Source Snapshot remains identified by the Repository and original full commit with matching integrity information. Repeat with a standalone Repository that has neither a parent System nor a Repository pair.

**Acceptance Scenarios**:

1. **Given** an existing authorized Repository and a permitted Git ref, **When** a trusted application capability acquires a Source Snapshot, **Then** authority is checked, the ref is resolved once to one full Profiled Revision before capture, and the completed source identity is the Repository plus that exact commit with integrity information.
2. **Given** an eligible standalone Repository with no parent System or Repository pair, **When** an authorized capture is requested, **Then** the same Source Snapshot capability completes without requiring pair identity, pair execution identity, or parent System membership.
3. **Given** a capture that is cancelled or fails before verification and completion, **When** a consumer attempts to open it, **Then** it is not presented as a completed Source Snapshot and any owned disposable operation state is safely released.

---

### User Story 2 - Read Retained Source Through a Scoped Grant (Priority: P1)

As an authorized ordinary consumer, I can list retained directories and files and read bounded source content using Repository-relative locations, while the application enforces live scope, cumulative limits, and source confidentiality.

**Why this priority**: Retention provides value only when later consumers can safely read the exact captured bytes without gaining arbitrary filesystem access.

**Independent Test**: Open a server-granted read context for a completed Source Snapshot, list entries and read bounded ranges after changing the checkout, then exercise expired, revoked, mismatched, escaping, exhausted, and cancelled requests and verify exact results or explicit denials without physical-location disclosure.

**Acceptance Scenarios**:

1. **Given** a completed Source Snapshot and a valid server-granted read context, **When** an ordinary consumer lists directories or files or reads bounded content, **Then** it receives Repository-relative metadata or content from the captured committed bytes, regardless of later working-copy or remote-ref changes.
2. **Given** a captured entry for which text or another requested read capability is unavailable, **When** the consumer requests that capability, **Then** the outcome explicitly identifies the limitation rather than altering bytes, silently omitting the entry, or substituting tool-index data.
3. **Given** an expired or revoked grant, a mismatched Repository or Profiled Revision, an absolute or escaping location, or a location that resolves outside the captured source, **When** a read operation is requested, **Then** access is denied without disclosing source content, restricted metadata, credentials, or the physical storage location.
4. **Given** one server-issued cumulative budget and deadline, **When** successive operations exhaust the permitted work or cancellation occurs, **Then** operations stop with an explicit limit or cancellation outcome, owned transient resources are released, and repeated calls do not receive an automatic replacement budget.

---

### User Story 3 - Reopen Durable Source Safely (Priority: P1)

As a newly authorized consumer, I can reopen and verify the same retained Source Snapshot after the original lease and application process are gone, even when the original remote Repository is unavailable.

**Why this priority**: Process-independent reopening is the core durability benefit and prevents accepted evidence from depending on disposable runtime state.

**Independent Test**: Complete a capture in declared durable source storage, dispose the original lease, stop the original application instance, make the remote unavailable, and verify that a fresh instance with fresh authority can reopen and read byte-identical source. Exercise concurrent consumers, repeated capture publication, corruption, and storage exhaustion.

**Acceptance Scenarios**:

1. **Given** a completed capture in deployment-configured durable source storage, **When** the original lease is disposed and a fresh application instance reopens it while the remote is unavailable, **Then** a newly authorized consumer can verify and read the same bytes without the former process token, a temporary operation directory, or a new clone.
2. **Given** retries or multiple authorized consumers for the same Repository and Profiled Revision, **When** capture completion is published, a verified capture is reopened, or one consumer releases its lease, **Then** identity and integrity remain stable, no consumer observes partial publication, and one release does not invalidate another consumer or delete verified canonical source.
3. **Given** missing or corrupted retained bytes or unavailable durable storage, **When** exact capture or reopening cannot be completed, **Then** the caller receives a truthful sanitized failure with authorized Repository and revision context, no alternate source or ref is silently selected, and no failed or partial source is labelled usable.
4. **Given** a later reacquisition or retry, **When** the application attempts to restore availability, **Then** it performs a fresh authority check and verifies exact byte identity rather than trusting a new temporary location.

---

### User Story 4 - Use the Source Capability Independently (Priority: P2)

As a product owner, I can qualify exact source retention and scoped reads as a bounded source capability without implying that later analysis, Knowledge, Chat, or deployment features are complete.

**Why this priority**: Accurate capability boundaries prevent source-only evidence from being mistaken for a working end-to-end Scan.

**Independent Test**: Verify capture, bounded listing and reading, denial, cumulative limits, concurrent lifetime, and fresh-process reopening with isolated local Git fixtures and declared persistent storage, while all analysis tools and semantic stores are absent.

**Acceptance Scenarios**:

1. **Given** isolated local Git fixtures and declared persistent source storage, **When** the Story 13.1 behaviors are verified, **Then** they work without invoking CodeGraph, LSP, an LLM, or future semantic Code Unit, Document, Edge, or Logical Flow capabilities.
2. **Given** successful source-only verification, **When** results are reported, **Then** they are labelled as retained-source capability evidence and are not labelled as a completed Scan, generated wiki, qualified Chat flow, or qualified full Docker analysis stack.

### Edge Cases

- A symbolic link, nested path, normalization variant, or equivalent path representation attempts to escape the captured source boundary.
- A valid entry exists but cannot be represented as text without changing its canonical bytes.
- Authority expires or is revoked between listing and reading, or between successive range reads.
- A grant names the correct Repository but the wrong Profiled Revision, or the correct revision in a different Repository.
- Budget is exhausted exactly at an operation boundary, partway through a requested range, or across multiple individually valid calls.
- Cancellation occurs before capture verification, during publication, or during a read.
- Two consumers open the same capture while one releases its lease or cleans its disposable state.
- Repeated capture attempts for the same Repository and Profiled Revision finish concurrently.
- The retained source is missing, corrupted, only partly present, or cannot be written because configured durable storage is unavailable or exhausted.
- The remote ref moves, the working copy changes, or the remote becomes unavailable after capture.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The capability MUST authorize the Repository and requested acquisition before resolving or capturing source.
- **FR-002**: The capability MUST support an eligible standalone Repository without requiring parent System membership, a Repository pair, or pair execution identity solely for Source Snapshot capture.
- **FR-003**: The capability MUST resolve a permitted Git ref once to one full commit before capture and use that full commit as the Profiled Revision.
- **FR-004**: A completed Source Snapshot MUST be publicly identified by Repository plus Profiled Revision and MUST carry sufficient integrity information to verify its exact captured bytes; any physical storage locator MUST remain internal.
- **FR-005**: Capture completion MUST be published atomically only after the retained source is complete and verified; failed, cancelled, corrupted, or partial captures MUST NOT be exposed as completed.
- **FR-006**: A server-granted read context MUST bind every ordinary read operation to one authorized Repository, Profiled Revision, Source Snapshot scope, cumulative work budget, and deadline.
- **FR-007**: Ordinary consumers MUST be able to obtain Repository-relative directory and file metadata and perform bounded content or range reads supported by the captured entry.
- **FR-008**: Ordinary reads MUST return metadata and content from the immutable captured committed bytes and MUST NOT substitute a mutable checkout, a changed remote ref, tool-index data, or content transformed in a way that changes the canonical bytes.
- **FR-009**: When an entry or requested text/read capability is unavailable, the capability MUST return an explicit limitation or unsupported outcome rather than silently changing, excluding, or replacing the source.
- **FR-010**: Every read operation MUST recheck applicable live authority and MUST deny expired, revoked, Repository-mismatched, or Profiled-Revision-mismatched access without disclosing unauthorized source or metadata.
- **FR-011**: Ordinary access MUST accept only contained Repository-relative locations and MUST deny absolute, escaping, or outside-resolving locations, including equivalent normalized forms.
- **FR-012**: Ordinary results and failures MUST NOT expose credentials or physical source-storage locations; any physical workspace access MUST be limited to separately trusted consumers within the granted source boundary.
- **FR-013**: The read context MUST apply one cumulative budget and deadline across its operations; budget exhaustion, deadline expiry, and cancellation MUST stop work with an explicit bounded outcome and MUST NOT automatically issue a fresh budget for repeated calls.
- **FR-014**: Cancellation or failure MUST release disposable resources owned by the affected operation without invalidating verified canonical source or other authorized consumers.
- **FR-015**: Completed source MUST be retained in deployment-configured durable source storage and MUST be reopenable by a fresh, newly authorized application instance without relying on an earlier in-memory token, temporary operation directory, available remote, or new clone.
- **FR-016**: Retained canonical source MUST remain independent of CodeGraph cache, tool data, and disposable workspace lifetime.
- **FR-017**: Retries and consumers that refer to the same Repository and Profiled Revision MUST observe stable source identity and integrity, MUST NOT observe partly published source, and MUST NOT lose access merely because another consumer releases its lease.
- **FR-018**: The initial retention policy MUST NOT automatically prune verified canonical retained source; automatic cleanup is limited to safe, unretained, disposable operation state.
- **FR-019**: Authentication, transport, ref-resolution, integrity, retained-byte, or storage-capacity failures MUST return truthful sanitized outcomes with enough authorized logical Repository and revision context for the caller to respond, without silently choosing another ref or source.
- **FR-020**: Any reacquisition or retry MUST perform a fresh authority check and verify exact byte identity before making retained source available.
- **FR-021**: The retained-source capability MUST be independently usable and verifiable without CodeGraph, LSP, an LLM, semantic Code Units, Documents, Edges, Logical Flows, Chat, or full Scan activation.
- **FR-022**: Evidence and status for this capability MUST distinguish completed retained-source behavior from later Scan, Knowledge, Chat, wiki, tool, Docker-stack, backup, and restore qualification.

### Scope Boundaries

**Included**:

- Exact Source Snapshot identity, integrity, atomic capture completion, durable retention, and fresh-instance reopening.
- Generic scoped read contexts with Repository-relative metadata, listing, bounded range/content reads, cumulative budgets, deadlines, cancellation, and explicit limitations.
- Live Repository/revision authorization, contained-path enforcement, sanitized failures, concurrent consumer safety, stable retry identity, and cleanup of disposable operation state.
- Only source-owned records and deployment-configured source storage needed for this capability.

**Excluded**:

- CodeGraph or LSP installation, preparation, invocation, or readiness; LLM overview or detailed semantic analysis; file scheduling; Code Unit, Document, Edge, Logical Flow, or Cross-Repository Resolution generation.
- Chat, a new public Source Snapshot user interface, full Scan activation, full Docker analysis-stack qualification, and operator backup, restore, or cross-host migration.
- Wholesale migration of pair-era registries or all physical-path consumers. Existing legacy pair consumers remain outside this generic capability except for any later-planned minimal compatibility need.
- Automatic pruning of verified canonical retained source and claims that this slice completes every obligation in composite PRD FR-4.9 or NFR-4.1.

### Key Entities

- **Repository**: The authorized source-bearing resource, which may belong to a System or may be standalone.
- **Profiled Revision**: The full exact Git commit selected once from the permitted ref and used as the public revision identity.
- **Source Snapshot**: The verified retained committed bytes for one Repository and Profiled Revision, together with integrity information and completion state; it is not an additional public version axis.
- **Source Entry**: Repository-relative metadata for a retained directory or file, including which bounded read capabilities are available.
- **Source Read Context**: A server-issued, live-authorized grant bound to one Repository/revision/source scope with a cumulative budget and deadline.
- **Source Read Outcome**: A successful metadata/content result or an explicit denial, limitation, limit, cancellation, integrity, acquisition, or storage failure expressed without physical-path or credential disclosure.
- **Disposable Operation State**: Temporary state owned by an acquisition or read operation that may be safely released without deleting verified canonical source or invalidating another consumer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of accepted capture fixtures, a completed Source Snapshot identifies the authorized Repository and the exact full commit resolved before capture, and byte-integrity verification succeeds after the working copy and remote ref change.
- **SC-002**: Both System-member and standalone Repository fixtures complete capture through the same capability, with zero standalone cases requiring pair identity, pair execution identity, or parent System membership.
- **SC-003**: Across the declared fixture set, 100% of successful metadata, listing, and bounded content reads match the captured committed source and use Repository-relative locations; no result substitutes later checkout, ref, or tool-index content.
- **SC-004**: Across all expired, revoked, mismatched, absolute, escaping, and outside-resolving test cases, 100% are denied with zero unauthorized content, restricted metadata, credential, or physical-location disclosure.
- **SC-005**: In every budget, deadline, and cancellation test, work stops no later than the issued bound, returns a distinct actionable outcome, and leaves zero completed partial captures or automatically refreshed budgets.
- **SC-006**: A fresh application instance can reopen and byte-verify 100% of completed retention fixtures after the original lease and process are gone and the remote is unavailable.
- **SC-007**: Under concurrent and repeated-use tests, all consumers observe one stable Repository/revision identity and integrity result, no consumer observes partial publication, and releasing one lease causes zero failures for other active consumers.
- **SC-008**: Every injected acquisition, transport, ref, integrity, corruption, and storage-capacity failure is reported truthfully without alternate-source substitution; every later successful retry demonstrates fresh authority and exact-byte verification.
- **SC-009**: The full Story 13.1 acceptance suite completes using isolated local Git fixtures and declared persistent source storage with zero CodeGraph, LSP, LLM, semantic Knowledge, Chat, or full Scan dependencies.
- **SC-010**: Reviewers can classify every Story 13.1 result as source capability success, limitation, denial, cancellation, or failure from its public outcome, while zero results claim completion of a Scan, wiki, Chat flow, full Docker analysis stack, backup, or restore.

## Assumptions

- Existing Repository records, management authorization, and Git acquisition primitives remain available as prior dependencies; this feature does not redesign Repository administration or Git credential management.
- A permitted Git ref is supplied through an already authorized application boundary. Reachability at Repository registration time is best-effort, while capture must resolve and verify exact source or fail truthfully.
- Deployment provides a durable source-storage location and declared source-access limits. This feature states the required behavior without choosing storage technology, record layout, default capacities, or cleanup mechanics; those decisions belong to planning.
- Public provenance continues to use Repository, Profiled Revision, and Repository-relative source locations. Source Snapshot does not introduce another user-visible source version.
- The initial policy retains verified canonical source without automatic pruning. Broader historical retention, backup, restore, and cross-host migration policies remain separate work.
- Existing pair-era consumers and historical evidence retain their own meaning. This feature establishes the generic source contract without crediting or relabelling legacy behavior as the replacement workflow.
