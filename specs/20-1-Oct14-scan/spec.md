# Feature Specification: OCT-SCAN — First real Scan batch

**Feature Branch**: `oct14/scan`

**Created**: 2026-10-06

**Status**: Validated specification; planning only

**Input**: New October first-deliverable feature using direct SpecKit. Analyze a declared registration/login slice across two prepared Repositories, accept complete-file results and invoke Business finalization with honest coverage. This is not a resumption of the original governed stories.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Generate one useful accepted file (Priority: P1)

An authorized operator selects a prepared batch and uses Generate to produce useful Code Logic Documents. A permitted reader opens their exact published Document Versions.

The operator explicitly selects CL-only output for this early milestone. It requires acceptance, fixed CL publication and exact reads only; matching and BL generation are not prerequisites. Success means the requested CL stage completed, not that BL or the connected business scenario completed.

**Why this priority**: Prove the smallest real source-to-Knowledge path before increasing breadth.

**Independent Test**: Analyze one real registration/login file with the selected provider and required tools, accept the complete result, publish through Knowledge and open exact versions. Inspect key rules, branches/effects and unknowns against source independently of structural validity.

**Acceptance Scenarios**:

1. **Given** valid selection, live authority and stable approved source, **when** Generate completes an admitted file, **then** semantic units, structured CL, anchors and observations are accepted together and exact published versions are readable by a permitted user.
2. **Given** unfinished/truncated/invalid/failed analysis, **when** the attempt ends, **then** no partial draft becomes Knowledge and its gap remains visible.
3. **Given** lost acceptance acknowledgement, **when** the original operation is looked up, **then** the same committed result is returned or acceptance remains indeterminate; analysis is not regenerated to recover a storage reply.
4. **Given** a valid complete zero-unit file, **when** accepted, **then** its disposition is recorded without fabricated Documents.
5. **Given** CL-only selection and unavailable matching/BL services, **when** the real file is accepted and its fixed CL selection is published, **then** a permitted ordinary IT reader opens the returned exact handle; the command reports CL-only success and BL not requested, without invoking matching/BL or editing database rows manually.

### User Story 2 — Run a bounded multi-file batch (Priority: P2)

An operator generates the configured slice with real progress, explicit admission reasons and controlled resource use.

**Why this priority**: Connected behavior spans files and requires useful tool lifetimes.

**Independent Test**: Exercise real source tools, JDT definition/reference and native CodeGraph over an owned multi-file fixture; measure the selected real batch sequentially before qualifying cap 2 or the configured value.

**Acceptance Scenarios**:

1. **Given** deterministic inventory and declared policy, **when** dispatch runs, **then** one pool cap and one active writer per file are enforced; ranking never silently removes admitted files.
2. **Given** a file needing continued windows, **when** analysis continues, **then** one logical file identity and full required coverage are retained and only the whole assembled proposal can be accepted.
3. **Given** transient provider failure, **when** classified bounded retries exhaust, **then** capacity is released and failure/interruption is reported without automatic provider switching.
4. **Given** expiry/revocation or required CodeGraph failure, **when** a tool is called, **then** it fails closed without unauthorized disclosure. Actual JDT limitations may use existing file tools with gaps; CodeGraph has no source-only substitute.

### User Story 3 — Stop safely and finalize a fixed corpus (Priority: P3)

The operator lets the batch finish or requests graceful stop. Scan settles/fences writes, fixes the accepted corpus and invokes Business without manual intermediate starts.

**Why this priority**: Useful partial publication must accurately represent completed work.

**Independent Test**: Race stop against acceptance; verify finalization receives only settled accepted handles and terminal coverage, then open its exact publication through the ordinary reader.

**Acceptance Scenarios**:

1. **Given** natural finish or graceful stop, **when** all admitted files have terminal outcomes and acceptance is resolved, **then** Business receives the fixed corpus; publication success requires confirmed finalization.
2. **Given** a late fenced Worker, **when** it submits, **then** it cannot change the frozen corpus/publication.
3. **Given** abrupt failure, hard cancellation, revoked authority or indeterminate storage, **when** execution ends, **then** interruption/failure/indeterminacy remains explicit; no implicit restart or older wiki substitution occurs.
   An already-submitted acceptance/publication may still commit after cancellation. Preserve that history, settle its exact operation and recheck disclosure authority; cancellation or a temporary missing row cannot prove rollback or permit freeze.
4. **Given** the two-Repository qualification case, **when** Business finalizes, **then** useful CL/BL, a supported Cross-Repository link and a real Uncertainty are available with explicit limitations. Business owns those semantic decisions.

### Edge Cases

- Empty admission, unknown formats and exclusions remain explicit dispositions, not analyzed coverage.
- Legitimate unowned `.codegraph` source is not hidden by name. Positively identified application tool state is excluded from source evidence/counts and denied to ordinary reads, including later pages.
- Preexisting unowned tool state blocks unsafe preparation; no automatic adoption, deletion or overwrite.
- Source/root changes, traversal and link escapes fail contained validation. Unchanged observations alone do not prove preventive isolation.
- Expiry, second-session attempts, cancellation, incomplete cleanup and paging restarts cannot silently lose coverage.
- Identical submissions replay; conflicting payloads under one operation identity reject; unresolved acceptance blocks finalization.
- Denied/revoked source or advisory contributors cannot leak through content, titles, counts, references or logs.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Generate MUST retain exact selected batch/database, live operator session, approved root-version bindings, context provenance and explicit provider binding. No legacy-pair route or old-wiki fallback.
- **FR-002**: Inventory MUST be deterministic. An explicit configuration file MUST declare admission, priority and limits. Every source file has selected/supporting/excluded/unsupported status and reasons. Names, Repository count, unfamiliar extensions and index omission are not automatic eligibility rules.
- **FR-003**: Scan Control MUST own scheduling, one pool cap, file ownership, bounded classified retries and stop. Measure sequential execution before qualification at cap 2 or the configured value.
- **FR-004**: Workers MUST use real bounded provider execution and externalized prompts. Continued windows retain a logical file workstream; Workers cannot choose permanent identities, admission or domain writes.
- **FR-005**: Source/tool use MUST bind approved Repository/root/lifetime and recheck live authority before use and disclosure. Tools expose closed relative-path/query operations, never arbitrary paths, credentials or commands.
- **FR-006**: Required native CodeGraph and applicable JDT MUST operate within a bounded qualified multi-file lifetime. JDT limitations permit existing file-tool fallback with explicit gaps; readiness never means semantic completeness.
- **FR-007**: Prepared source MUST remain stable/read-only. Positively identified application-owned tool state MUST be excluded before evidence/admission/counting and denied to ordinary reads/search. No Git acquisition, source copy or repository-wide hashing prerequisite.
- **FR-008**: Scan MUST establish trusted admission and analysis completion for the exact assembled proposal: valid provider finish, drained tools, full required coverage and source validation. Completion does not grant irrevocable write permission. Scan owns live eligibility/fence/deadline; acceptance MUST check current authority and eligibility at transaction entry and serialized commit submission shared with fencing. Expired/fenced work cannot newly submit even after completion; a model flag is insufficient.
- **FR-009**: Knowledge MUST atomically accept whole-file results, derive semantic identities and return exact handles through its one shared contract. Failed drafts remain invisible; zero-unit completion is explicit.
- **FR-010**: Acceptance/publication MUST support stable operation identity, identical replay, conflicting-payload rejection and exact committed-result lookup. Unavailable lookup or temporarily absent data while an original write may still commit remains indeterminate. Cancellation is not rollback proof. Block replacement writes, freeze and success until exact commitment or confirmed noncommit is established; no regeneration. Result disclosure requires fresh authority.
- **FR-011**: Natural finish and graceful stop MUST close admission, settle/fence writes, record terminal gaps and freeze accepted handles before Business finalization. Accepted history survives interruption.
- **FR-012**: Generate MUST explicitly select CL-only or CL-and-BL output before dispatch. Both settle accepted work and publish through Business-owned ports. CL-only MUST publish the fixed accepted CL selection without matching/BL services or provider calls for those stages; CL-and-BL MUST invoke Business finalization with exact handles, context/provenance, limits and terminal coverage. Scan MUST NOT duplicate relationship, BL, persistence or publication rules. A later CL-and-BL publication is a separate immutable selection, never an implicit upgrade of the earlier CL publication.
- **FR-013**: Reports MUST separate coverage, execution and publication status, with real counts, queue/tool/model/store waits, duration, usage, failures and gaps. Unknown usage is not zero. Retain sanitized append-only `log.md` operation/target/timestamp output.
- **FR-014**: Handoff MUST include exact batch/publication binding and start/select/Generate/stop/fresh-batch instructions. Ordinary-reader authorization stays separate from operator setup; Business Mode is BL-only with live all-contributor checks.
- **FR-015**: Qualification MUST separate controlled, native-tool, PostgreSQL and real provider/content evidence. Runtime resources, prepared inputs and provider spending require scoped authorization.
- **FR-016**: Every Business provider attempt/retry MUST share Scan's host-owned concurrency, request/rate admission, remaining budgets, monotonic deadline and cancellation/accounting scope with file Workers. No separate pool, renewed allowance or outer orchestration permit while waiting for child calls. Settle actual/unknown usage exactly once; release capacity only after transport settles or disposal is confirmed. Unknown usage is never zero; unconfirmed cancellation stops further dispatch.

### Key Entities

- **Prepared batch/source origin**: Trusted batch, Repository and root-version provenance; never fabricated Git identity.
- **File workstream**: One admitted file, windows, attempt ownership, terminal outcome and coverage.
- **Completed-file proposal**: Semantic units, structured CL, observations, anchors and advisory qualifications; trusted completion is separate from model content.
- **Accepted corpus**: Knowledge-owned exact accepted file/Document Version handles plus terminal coverage, fixed before finalization.
- **Publication**: Knowledge-owned explicit membership and exact dependencies for consumers.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A multi-file native fixture exercises list/read/search, JDT definition/reference and CodeGraph beyond startup, including expiry, second sessions, cancellation and owned-state coexistence.
- **SC-002**: At least one real registration/login file yields useful accepted CL readable at exact published versions; source inspection confirms key rules, branches/effects and unknowns.
- **SC-003**: The selected two-Repository slice yields useful CL/BL, at least one supported Cross-Repository link and one real Uncertainty, without pair eligibility or complete-Repository claims.
- **SC-004**: Every inventoried source has a disposition and every admitted file a terminal outcome at freeze. Active Workers never exceed the cap; no file has simultaneous valid writers.
- **SC-005**: Stop races, replay/conflict and acknowledgement-loss checks yield no partial/duplicate Knowledge or late corpus mutation. Unresolved acceptance remains indeterminate.
- **SC-006**: Denied/revoked checks disclose no protected source, Knowledge or metadata; normal finish, graceful partial stop and abrupt interruption are distinct.

## Assumptions

- Authority: October delivery plan dated 2026-09-30 (updated October 6), integration plan dated 2026-10-06 and scan-batch-feature.md under `docs/bmad/planning-artifacts`. Preserve compatible October proposal, PRD, architecture and constitution requirements.
- OCT-BUSINESS supplies compiled shared contracts, labelled fixture, persistence, exact reads and finalization. Integration owns shared Web composition and Explore the reader. These are dependencies, not delivered APIs.
- Existing setup/authentication is reused. Actual inputs, accounts, provider configuration, compatible JDK/host policy and budgets are later runtime prerequisites.
- Durable restart, automatic updates, broader language/framework generality, cross-process borrowing/reclamation and generic deployment/backup are deferred. Abrupt failure needs an explicitly requested fresh batch.
- Original 18.5 and original task matrices are not this feature's completion criteria. No historical-story or generic PRD completion is claimed. This request stops after accepted planning, before tasks/implementation/runtime.
- F1/F2/F5 planning concurrence is established against Business canonical contract SHA-256 `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36`: serialized commit submission, shared host execution scope and B0-only early CL. After separate authorization the initial owner contract/fixture enables minimal compile probes, then accepted CP-K1/C0 gates dependent production. No compiled contract, probe or runtime qualification is claimed here; prior review records remain historical.
