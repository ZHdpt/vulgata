# Feature Specification: Durable Scan Foundation & Analysis Runtime

**Feature Branch**: `demo0831/main`

**Created**: 2026-08-18

**Status**: Draft

**Input**: Story 12.1 establishes durable Scan state, restart-safe recovery, exact analysis-runtime identity, and bounded analysis access for the approved August 30 two-Repository release path.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resume Trusted Scan Work (Priority: P1)

As a SystemOwner, I can resume interrupted Scan work without losing committed Documents or repeating already committed jobs, so an application outage or planned restart does not invalidate an expensive bounded Scan.

**Why this priority**: Durable recovery is the foundation on which every later Epic 12 PreScan, Pass 1, Pass 2, and Chat result depends.

**Independent Test**: Persist a mixed set of completed, pending, running, and retry-scheduled jobs, stop and restart the application against the same database, resume manually, and verify that committed Documents are retained, expired work becomes eligible again, and no committed job creates a duplicate Document Version.

**Acceptance Scenarios**:

1. **Given** a Repository Scan has completed jobs and uncommitted jobs, **When** the application restarts and the SystemOwner resumes it, **Then** completed job results remain committed and only eligible unfinished jobs resume.
2. **Given** a running job's lease expires before recovery, **When** recovery evaluates the persisted execution, **Then** that job returns to Pending without changing its deterministic identity.
3. **Given** a job result and its completion state were committed atomically, **When** recovery runs more than once, **Then** exactly one Document Version remains associated with that job and each recovery produces the same durable outcome.
4. **Given** Repository access, the Profiled Revision, a file hash, prompt or schema identity, provider availability, or analysis-runtime identity no longer matches, **When** resume is requested, **Then** dispatch remains stopped and the mismatch is reported without altering committed Documents.

---

### User Story 2 - Trust the Analysis Runtime (Priority: P1)

As a SystemOwner, I can verify the exact local analysis runtime before Scan work consumes source or provider capacity, so the release evidence is reproducible and does not depend on an unqualified tool installation.

**Why this priority**: A durable Scan cannot be trusted if its structural-analysis identity is floating, incomplete, or tied to obsolete CodeGraphContext qualification.

**Independent Test**: Qualify the approved host runtime from its declared release identities and integrity evidence, exercise the bounded analysis capabilities on a neutral Repository revision, and prove that every mismatched or incomplete identity fails readiness before Worker dispatch.

**Acceptance Scenarios**:

1. **Given** the approved CodeGraph package, command, Windows bundle, version, and three installed-file hashes are present, **When** runtime qualification runs, **Then** CodeGraph is reported ready without building a Repository index during ordinary application startup.
2. **Given** the approved Eclipse JDT LS release and its exact distribution archive checksum, Java major version, and client identity are present, **When** runtime qualification runs, **Then** the Java analysis runtime is reported ready for an isolated Repository-revision workspace.
3. **Given** the JDT LS core bundle hash is known but the exact distribution archive checksum is absent, **When** readiness is evaluated, **Then** readiness fails and the core bundle hash is not accepted as a substitute.
4. **Given** any declared tool version, required checksum, Java major version, or workspace binding is wrong, **When** qualification runs, **Then** the affected runtime is rejected before Worker dispatch.
5. **Given** the August demonstration topology, **When** production startup dependencies are inspected, **Then** Vulgata and its analysis tools are host-run, PostgreSQL 17 is the only required Docker service, and CodeGraphContext, Python, and the historical qualification harness are not release prerequisites.

---

### User Story 3 - Use Bounded Analysis Evidence (Priority: P2)

As a SystemOwner, I can rely on Scan components receiving only revision-bound structural and source evidence through closed capabilities, so an Agent cannot turn analysis access into unrestricted process, network, database, or filesystem access.

**Why this priority**: Later Epic 12 stories depend on these boundaries to make source-derived claims trustworthy and authorization-aware.

**Independent Test**: Request every allowed CodeGraph, Java navigation, and source-reading capability through its typed boundary, then attempt raw protocol, shell, network, internal-store, cross-Repository, stale-revision, and unbounded reads and verify that they are rejected.

**Acceptance Scenarios**:

1. **Given** an authorized Repository and Profiled Revision, **When** Scan code requests an allowed structural or source operation, **Then** it receives bounded typed evidence bound to that Repository revision.
2. **Given** a caller attempts raw command execution, raw language-server messaging, arbitrary graph queries, internal CodeGraph storage access, network access, or unrestricted filesystem reads, **When** the request crosses an analysis boundary, **Then** the request is rejected.
3. **Given** Java analysis is active for one Repository revision, **When** another Repository revision is analyzed, **Then** it uses a distinct isolated workspace and cannot reuse the first revision's workspace identity.
4. **Given** a bounded analysis request is cancelled, times out, returns malformed data, or cannot shut down normally, **When** its lifecycle completes, **Then** the failure is classified, diagnostics are sanitized, resources are boundedly cleaned up, and the boundary remains usable or fails closed.

---

### User Story 4 - Preserve Existing Knowledge Through Migration (Priority: P2)

As a SystemOwner, I can adopt the durable Scan foundation without losing existing knowledge or historical demonstration data, so the August 30 recovery is additive rather than a destructive rewrite.

**Why this priority**: Existing immutable Documents, Source Anchors, Edges, Uncertainties, and prepared-demo rows are accepted historical evidence and must survive the new foundation.

**Independent Test**: Exercise the migration lifecycle against clean and legacy data, including concurrent durable updates, and verify apply, rollback, reapply, model consistency, and readability of every protected historical record type.

**Acceptance Scenarios**:

1. **Given** a clean PostgreSQL 17 database, **When** the migration is applied, rolled back, and reapplied, **Then** every step succeeds and the final model has no pending change.
2. **Given** existing immutable Document, Source Anchor, Edge, Uncertainty, and prepared-demo rows, **When** the migration is applied and later rolled back, **Then** those rows remain readable and semantically unchanged.
3. **Given** two writers attempt stale or conflicting transitions to the same durable Scan state, **When** the updates are persisted, **Then** at most one valid transition wins and no partial or contradictory state is committed.

### Edge Cases

- Recovery is invoked repeatedly for the same execution or after a previous recovery stopped part-way.
- A Running lease is still valid, has just expired, or has an invalid expiry relative to persisted timestamps.
- A completed job points to no committed Document Version, or an uncompleted job points to one; recovery must fail closed on the integrity contradiction.
- A retry is due, scheduled in the future, exhausted, cancelled, or classified as provider-wide rather than job-specific.
- One Repository in the paired release outcome fails integrity validation while the other already has valid committed Documents.
- The selected Repositories are duplicated, belong to the wrong System, number fewer or more than two, or change Profiled Revision after binding.
- A runtime installation has the correct version label but one incorrect approved file hash.
- The configured JDT LS location contains the expected core bundle but no verifiable distribution archive checksum.
- A machine installation path changes while the normalized release identity remains the same; the absolute path must not alter domain identity, prompts, or generated evidence.
- An analysis response references a path outside the authorized Repository, a different Profiled Revision, an excessive result set, or a stale workspace.
- Shutdown exceeds its bound or cancellation races with a response; cleanup must not leave a reusable workspace or orphaned trusted state.
- Legacy CodeGraphContext services or tests remain in the repository; they must not be selected by production composition or the August 30 release gate.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST persist exactly one immutable Scan Context Snapshot for each Repository Scan Run, containing the effective Global Context, System Context, Repository Context, content hash, schema identity, and capture time.
- **FR-002**: A Scan Context Snapshot MUST remain bound to its Repository, Scan Run, and Profiled Revision and MUST NOT be replaced or edited after capture.
- **FR-003**: The system MUST persist one Scan Job Execution for every prepared File Code Unit job, with a deterministic job identity, Scan Execution identity, File Code Unit identity, manifest ordinal, state, attempt count, next-attempt time, bounded failure classification and code, lease expiry, and committed Document Version identity when present.
- **FR-004**: Scan Job Execution states MUST distinguish Pending, Running, Retry Scheduled, Completed, Failed, and Cancelled outcomes without using a single in-flight slot as the durable job ledger.
- **FR-005**: The system MUST persist Scan Execution phases sufficient to distinguish ordinary progress, provider-unavailable pause, configuration/operator-correction pause, completed success, Completed With Warnings, integrity/security failure, and cancellation.
- **FR-006**: A job-result commit and the corresponding Completed job transition MUST be atomic; a committed job MUST identify exactly one committed Document Version, and an uncommitted job MUST NOT be treated as completed.
- **FR-007**: Recovery and manual resume MUST preserve committed Documents, return only expired Running leases to Pending, skip atomically completed jobs, honor scheduled retry time and retry exhaustion, and remain idempotent across repeated recovery attempts.
- **FR-008**: Before any resumed dispatch, the system MUST revalidate Repository authorization and access, the bound Profiled Revision, affected file hashes, prompt identity, schema identity, provider availability, and the normalized analysis-runtime identity.
- **FR-009**: Any failed revalidation or durable-state integrity contradiction MUST stop further dispatch with a bounded classified reason while retaining already committed valid Documents.
- **FR-010**: The system MUST provide a SystemOwner-initiated manual resume capability; automatic background resume is excluded from Story 12.1.
- **FR-011**: The system MUST persist one **Two-Repository Scan Coordination** record for an August release outcome. It MUST bind one System, exactly two distinct authorized Repositories, their Repository Scan Run and Scan Execution identities, their Profiled Revisions, the combined phase, bounded pause or failure reason, and lifecycle timestamps.
- **FR-012**: Two-Repository Scan Coordination MUST coordinate two independent Repository Scans without redefining Scan as a System-level operation, replacing the Scan Coordinator, or duplicating per-file Scan Job Execution state.
- **FR-013**: The combined coordination outcome MUST be idempotent on resume, MUST retain valid committed Documents when one Repository fails integrity validation, and MUST NOT report success while either bound Repository outcome is nonterminal or invalid.
- **FR-014**: The release runtime MUST identify CodeGraph as npm package `@colbymchenry/codegraph@1.5.0`, command `codegraph`, repository `https://github.com/colbymchenry/codegraph`, and Windows x64 bundle `@colbymchenry/codegraph-win32-x64@1.5.0`.
- **FR-015**: CodeGraph readiness MUST verify the approved SHA-256 values for `npm-shim.js` (`80419260F06862D7A422D13EF69E3B24C7A21DBEF4309BCE41DB02AD642B1877`), platform `node.exe` (`B3094D0B49F9AD602262A9921551737BB97637C05DD357A06AE98188D7290AA3`), and platform `lib/dist/bin/codegraph.js` (`03E4C791CC0DD91ED264278461BF9A56C0278AA0670D5942FC4732311C66DE03`).
- **FR-016**: The Java analysis runtime MUST identify Eclipse JDT LS as `jdt-language-server-1.61.0-202608141332`, require Java major version 21, and use the approved client identity StreamJsonRpc `2.25.29` over standard input/output.
- **FR-017**: JDT LS readiness MUST record and verify the exact installed distribution archive checksum. The checksum of `org.eclipse.jdt.ls.core_1.61.0.202608141332.jar` is supporting observation only and MUST NOT substitute for the distribution archive checksum.
- **FR-018**: Each active Repository Profiled Revision MUST receive an isolated writable JDT LS workspace; reuse across different Repository revisions MUST fail readiness.
- **FR-019**: Ordinary application startup MUST verify configured tool identity and health without building a Repository index, and MUST reject version, checksum, Java-major, workspace, malformed-response, or readiness mismatches before Worker dispatch.
- **FR-020**: The August release MUST run Vulgata, CodeGraph, Java 21, and JDT LS on the documented Windows host, with PostgreSQL 17 as its only required Docker service.
- **FR-021**: Machine-specific absolute installation paths MUST remain configuration only and MUST NOT enter domain identity, prompts, generated evidence, or persisted release identity except as the approved normalized executable/archive path component.
- **FR-022**: Production startup and August release verification MUST NOT require CodeGraphContext, its Python runtime, its private storage, or its historical qualification harness.
- **FR-023**: The system MUST expose closed typed CodeGraph capabilities limited to index/rebuild, status, symbol search, callers, callees, and related files.
- **FR-024**: The system MUST expose closed typed Java analysis capabilities limited to document symbols, definition, references, and implementations.
- **FR-025**: The system MUST expose a revision-bound source-reading capability limited to contained exact ranges and bounded excerpts from the authorized Repository and Profiled Revision.
- **FR-026**: Raw command or shell execution, raw language-server messages, arbitrary graph queries, CodeGraph internal storage, network access, unrestricted filesystem reads, and cross-Repository or stale-revision evidence MUST NOT cross the analysis capabilities.
- **FR-027**: Analysis lifecycle handling MUST bound startup, readiness, requests, cancellation, shutdown, and forced cleanup; sanitize diagnostics; and either preserve subsequent safe use or fail closed after a lifecycle error.
- **FR-028**: Durable and runtime identities MUST be expressed through bounded, versioned, normalized values so the same trusted inputs produce the same readiness and recovery decisions.
- **FR-029**: The additive persistence change MUST preserve readability and semantics of existing immutable Document Versions, Source Anchors, Edges, Uncertainties, and prepared-demo rows.
- **FR-030**: The migration lifecycle MUST support clean apply, rollback, and reapply on PostgreSQL 17, detect no pending model change afterward, and prevent partial state during concurrent transitions.
- **FR-031**: Story 12.1 MUST establish only the durable foundation and analysis-runtime boundary. It MUST NOT create the complete Java/XML PreScan, Prepared Scan Manifest, File Code Unit selection or splitting, Worker generation, `navigate_code`, Pass 1 dispatch policy, Cross-Repository Resolution, Pass 2, Chat retrieval, production progress UI, or final release evidence owned by Stories 12.2 through 12.5.
- **FR-032**: Historical Story 11.5, `specs/008-demo-chat-release-gate`, Backup B state, and the non-ancestral `d91772004cf6ad56d4fcd38b37917958733f693e` checkpoint MUST NOT define or satisfy Story 12.1 acceptance.

### Key Entities

- **Scan Context Snapshot**: The immutable context captured once for one Repository Scan Run and Profiled Revision; it carries bounded advisory context identity for later phases without becoming source evidence.
- **Scan Job Execution**: The durable lifecycle of one deterministic File Code Unit job, including retry, lease, failure, and committed Document Version association.
- **Scan Execution**: The durable per-Repository operational lifecycle that aggregates job progress and exposes paused, warning, failure, cancellation, and completion phases.
- **Two-Repository Scan Coordination**: A lightweight persisted operational record binding exactly two independent Repository Scan lifecycles into the bounded August release outcome. It is not a Scan, Scan Coordinator, job ledger, or general multi-Repository orchestration model.
- **Analysis Runtime Identity**: The normalized declared versions, required integrity values, Java major version, and Repository-revision workspace binding used to decide whether structural analysis is trustworthy. Absolute host paths are configuration rather than identity.
- **Analysis Runtime Health**: A bounded readiness result that identifies which required checks passed or failed without exposing raw process or protocol access.

### Scope Boundaries and Dependencies

- **Included**: additive durable context, per-job lifecycle, per-Repository Scan phases, Two-Repository Scan Coordination, recovery and manual resume rules, exact runtime identity and health, closed typed analysis capabilities, host-runtime dependency boundary, and migration compatibility.
- **Story 12.2 dependency**: uses these runtime and persistence foundations to implement complete Java/XML PreScan, real JDT LS readiness on a neutral Java file, safe XML analysis, File Code Units, deterministic ordering, and the atomic Prepared Scan Manifest.
- **Story 12.3 dependency**: uses the durable job foundation to implement shared bounded Worker dispatch, classified provider backoff, `navigate_code`, `code-logic/file-v2`, partial-failure budgets, and post-Pass1 reconciliation.
- **Story 12.4 dependency**: owns exact selected Cross-Repository Resolution, endpoint-centered grouping, Pass 2 retrieval, and Business Logic Documents.
- **Story 12.5 dependency**: owns grounded Chat, production progress/pause/resume UI, and final real-tool/real-LLM release evidence.
- **Excluded historical path**: Story 11.5, `specs/008-demo-chat-release-gate`, CodeGraphContext production qualification, Python runtime qualification, and Backup B/demo-only scan state.
- **Excluded broader product scope**: general multi-Repository orchestration, method-level scheduling, arbitrary protocols, automatic background recovery, vector retrieval, general 10,000-file acceptance, and application-container release proof.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across repeated restart and manual-resume trials containing every supported job state, 100% of committed Documents remain available, 0 duplicate Document Versions are created for completed jobs, and 100% of expired Running jobs return to Pending exactly once per valid transition.
- **SC-002**: In 100% of resume trials, dispatch proceeds only when Repository access, Profiled Revision, file hashes, prompt/schema identity, provider availability, and analysis-runtime identity all match; every introduced mismatch stops dispatch with a classified reason.
- **SC-003**: The persisted combined outcome accepts exactly two distinct authorized Repositories and rejects 100% of trials with duplicate, missing, extra, wrong-System, or revision-drifted Repository bindings.
- **SC-004**: Runtime qualification accepts 100% of trials with the complete approved identities and integrity evidence and rejects 100% of trials containing a version mismatch, required checksum mismatch or omission, wrong Java major version, or reused workspace.
- **SC-005**: Every allowed analysis capability succeeds on a neutral authorized Repository-revision fixture, while 100% of attempted raw protocol, shell, network, internal-store, unrestricted-filesystem, cross-Repository, and stale-revision accesses are rejected.
- **SC-006**: Clean apply, rollback, and reapply complete successfully, leave no pending model change, and preserve readability of 100% of protected legacy record fixtures.
- **SC-007**: In concurrent-transition trials, no execution reaches two contradictory states, no partial job/result commit is observable, and at most one stale competing transition succeeds.
- **SC-008**: Ordinary startup completes tool health verification without creating an analysis index and without requiring any historical CodeGraphContext or Python release component.
- **SC-009**: A SystemOwner can identify the paused reason and invoke manual resume in one bounded operator action; automatic background resume is not required.

## Assumptions

- The approved August 17 and August 18 Sprint Change Proposals and corrected August 30 design are authoritative and require no renewed product-decision approval at specification time.
- The currently supplied host installations are qualification inputs, not portable domain values: `codegraph` resolves to the approved 1.5.0 installation; Java 21 is configured at `C:/Program Files/Java/latest/jdk-21`; and JDT LS is configured at `C:/Users/13728/.local/jdt-language-server-1.61.0-202608141332`.
- The observed JDT LS core bundle SHA-256 `A58994113B7E493AA8CED51AD02DAB3E424973223E7213F7ED5B9D70861582E2` confirms a component observation only. The exact distribution archive checksum remains a hard readiness prerequisite and is not currently supplied.
- PostgreSQL 17 remains available as the durable database and only required Docker service for the August release topology.
- Existing Repository authorization, exact Git acquisition, immutable Document, Source Anchor, Edge, Uncertainty, and prepared-demo persistence provide compatible seams but do not by themselves satisfy the new durable recovery requirements.
- Exact retry windows, Worker concurrency, partial-failure budgets, and the Chinese production resume UI are introduced by later Epic 12 stories unless a value is necessary here to represent durable state without executing that policy.

## Authority Alignment

- **Approved August 30 correction**: Establishes revision-bound CodeGraph/JDT-LS analysis, durable pause/restart/manual resume, per-job persistence, and exactly-two-Repository coordination while preserving per-Repository Scan meaning.
- **Approved August 18 host-runtime correction**: Fixes the exact CodeGraph, JDT LS, Java, and StreamJsonRpc identities; replaces the blocking application-container topology with the Windows-host/PostgreSQL-only Docker boundary; and requires the JDT LS distribution archive checksum before readiness.
- **PRD overlay**: This feature implements the Story 12.1 foundation for the dated replacements to FR-4.1 through FR-4.5 and FR-4.9, and strengthens NFR-3.1 through NFR-3.3. It applies the dated NFR-4.1 exception without declaring the long-term Docker requirement complete.
- **Canonical architecture**: The dated August 30 design supersedes conflicting legacy CGC, orchestration, tool, retry, deployment, and release-gate clauses for Epic 12. Existing project boundaries, immutable Documents, authorization, and PostgreSQL persistence remain in force.
- **Terminology decision**: **Two-Repository Scan Coordination** is the proposed canonical name for the new lightweight record. The name states its exact bounded cardinality and operational purpose, preserves the rule that a Scan targets one Repository, and avoids representing the record as either a Scan or the Scan Coordinator. The Ubiquitous Language glossary must adopt this term before downstream implementation treats it as canonical.
- **Planned-change gate**: This specification does not authorize task generation or implementation. After a plan and independent plan-only review are accepted, the root orchestrator must request explicit authorization for the Worker Agent tool, Scan-strategy, and PRD replacement triggers before tasks are generated.
