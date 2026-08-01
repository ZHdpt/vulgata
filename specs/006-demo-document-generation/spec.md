# Feature Specification: Demo Document Generation

**Feature Branch**: `codex/story-11-3`
**Created**: 2026-08-01
**Status**: Draft
**Execution Level**: `expedited-mvp`
**Input**: Story 11.3 — Pass 1 & Pass 2 Demo Document Generation

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Source-Grounded Code Logic Documents (Priority: P1)

As a SystemOwner, I want each prepared Code Unit processed by a bounded Worker Agent so that the demonstration produces trustworthy technical explanations linked to the exact source that was scanned.

**Why this priority**: The prepared flow cannot be explained or synthesized until its Code Logic Documents exist as durable, source-grounded truth.

**Independent Test**: Dispatch one authorized Pass 1 job for a prepared Code Unit and verify that a valid, bounded result is committed as one immutable Code Logic Document Version with exact Source Anchors, permitted same-Repository `REFERENCES` Edges, communication evidence, and a retrieval projection before job progress advances.

**Acceptance Scenarios**:

1. **Given** a Pass 1 job bound to an exact Repository, ScanRun, Code Unit, Profiled Revision, repository-relative source slice, and hashes, **When** the authorized source and context match those identities, **Then** the Worker Agent returns a schema-valid Code Logic Document result containing bounded technical content, source claims, Source Anchors, essential same-Repository references, and communication evidence.
2. **Given** a schema-valid Pass 1 result, **When** it is accepted, **Then** its complete immutable knowledge aggregate is committed atomically and the job is recorded valid only after the commit succeeds.
3. **Given** the same valid operation is submitted again, **When** it reaches the knowledge-store boundary, **Then** it produces no duplicate logical truth and preserves every superseded Document Version in the Version Chain.

---

### User Story 2 - Fail Closed on Unsafe or Invalid Results (Priority: P1)

As a SystemOwner, I want invalid Worker Agent output rejected without partial persistence or data exposure so that the demonstration cannot present fabricated, mismatched, or sensitive evidence as knowledge.

**Why this priority**: Source isolation, authorization, and atomicity are required trust boundaries for every generated Document.

**Independent Test**: Submit malformed, unbounded, identity-mismatched, unauthorized, sensitive, stale-hash, absolute-path, or out-of-range input/output fixtures and verify a bounded failure, zero knowledge writes, no valid progress record, and no raw provider diagnostic exposure.

**Acceptance Scenarios**:

1. **Given** source hydration does not match the dispatched Repository, Profiled Revision, path, line range, source hash, or File Identity Hash, **When** the job is prepared for provider execution, **Then** execution fails closed before any source is transmitted or Document data is committed.
2. **Given** a provider response is malformed, schema-invalid, unbounded, identity-mismatched, unauthorized, or contains a secret, absolute host path, raw-source carrier, or unsafe diagnostic, **When** it is validated, **Then** the Scan stops with a bounded Chinese failure result and zero rows from that result become visible.
3. **Given** a valid result whose knowledge commit conflicts, is rejected, or fails, **When** the runner handles the outcome, **Then** it does not record the job valid or advance completed progress.

---

### User Story 3 - Generate One Prepared Business Logic Document (Priority: P2)

As a SystemOwner, I want the completed Pass 1 evidence synthesized for one prepared flow so that a non-technical reviewer can understand the demonstrated business process and inspect its exact provenance.

**Why this priority**: This is the bounded business-facing outcome of the August 15 demonstration slice, built on the P1 technical evidence.

**Independent Test**: Start with all prepared Pass 1 results durably committed, dispatch exactly one prepared Pass 2 job, and verify one Business Logic Document Version, exact flow evidence and `GENERATED_FROM` Edges are committed before the Scan transitions from `Pass1Completed` to `Completed`.

**Acceptance Scenarios**:

1. **Given** all prepared Pass 1 results are durably committed, **When** the Scan reaches `Pass1Completed`, **Then** exactly one closed Pass 2 job is constructed from the version-controlled prepared flow and dispatched with only the required trace evidence and exact Code Logic Document Versions.
2. **Given** the prepared Pass 2 result is valid, **When** it is committed, **Then** one immutable Business Logic Document Version explains the business process for a non-technical reviewer and atomically stores its sections, Source Anchors, flow evidence, retrieval projection, and `GENERATED_FROM` Edges to exactly the Code Logic Document Versions used.
3. **Given** Pass 2 succeeds, **When** the knowledge commit completes, **Then** the Scan transitions to `Completed`; if dispatch, validation, or commit fails, it does not transition to `Completed`.
4. **Given** narrative text mentions a dependency, **When** Pass 2 is materialized, **Then** it does not create a new base Code Logic `REFERENCES` Edge from narrative interpretation.

### Edge Cases

- The authorized source exists but its content or identity hash differs from the dispatched packet; the job fails before provider execution.
- A response is structurally valid but repeats identifiers, uses unknown fields or values, has nondeterministic ordering, or exceeds a finite content bound; it is rejected as non-valid.
- A caller supplies access scope for another Repository or a prepared flow names a Document Version outside the authorized Repository set; no knowledge or progress is committed.
- A knowledge commit is replayed identically, conflicts with a newer current version, or is rejected; replay remains idempotent and conflict/rejection never advances progress.
- Cancellation occurs during hydration, dispatch, validation, or commit; no later lifecycle transition is fabricated.
- Zero prepared Pass 1 Code Units or more than one prepared Pass 2 flow is supplied; the Scan fails closed rather than claiming completion.
- Pass 1 completes but the single prepared Pass 2 job fails; the Scan never reports `Completed`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST execute each prepared Pass 1 job through a bounded Worker Agent contract and accept only a closed, schema-valid Code Logic Document result.
- **FR-002**: The system MUST bind every Pass 1 result to the dispatched Job, Repository, System, ScanRun, Code Unit, Profiled Revision, repository-relative path, line range, source hash, and File Identity Hash.
- **FR-003**: The system MUST hydrate only the exact authorized, revision-bound source slice and job context, verify its identities and hashes before provider execution, and transmit only the source and context required for that job.
- **FR-004**: The system MUST reject malformed, unbounded, unknown, duplicate, out-of-order, identity-mismatched, unauthorized, sensitive, absolute-host-path, raw-source-carrier, or unsafe-diagnostic input and output through bounded non-valid outcomes that do not echo raw diagnostics.
- **FR-005**: A valid Pass 1 result MUST materialize exactly one authorization-scoped atomic knowledge commit containing an immutable Code Logic Document Version, bounded canonical content, Source Anchors, permitted same-Repository `REFERENCES` Edges, communication evidence, and its retrieval projection.
- **FR-006**: The system MUST complete the knowledge commit before recording the Pass 1 job valid, clearing its in-flight state, or incrementing completed progress.
- **FR-007**: A validation failure, authorization failure, commit rejection, conflict, cancellation, or execution error MUST leave the affected result with zero visible knowledge writes and MUST NOT record valid progress.
- **FR-008**: Repeating the same accepted commit MUST be idempotent, while supersession MUST preserve prior immutable Document Versions through the Version Chain.
- **FR-009**: After all prepared Pass 1 results commit, the Scan MUST remain at the nonterminal `Pass1Completed` boundary until Pass 2 completes successfully.
- **FR-010**: The system MUST construct and dispatch exactly one Pass 2 job from one finite, version-controlled prepared-flow input; it MUST NOT discover, traverse, cluster, or schedule general flows.
- **FR-011**: The Pass 2 job MUST contain only the authorized prepared trace evidence and exact current Code Logic Document Versions required for the flow.
- **FR-012**: The system MUST accept only a closed, schema-valid Business Logic Document result that explains the prepared business process in language suitable for a non-technical reviewer.
- **FR-013**: A valid Pass 2 result MUST atomically commit one immutable Business Logic Document Version with bounded sections, Source Anchors, prepared flow evidence, retrieval projection, and `GENERATED_FROM` Edges targeting exactly the Code Logic Document Versions used for synthesis.
- **FR-014**: Pass 2 narrative content MUST NOT create or infer new base Code Logic `REFERENCES` Edges.
- **FR-015**: The Scan MUST transition from `Pass1Completed` to `Completed` only after the valid Pass 2 knowledge commit succeeds; every Pass 2 failure MUST prevent `Completed`.
- **FR-016**: Provider selection MUST use the existing authorized System or global Worker Agent provider assignment, keep credentials outside packets, prompts, results, logs, exceptions, and durable evidence, and MUST NOT introduce external fallback or provider failover.
- **FR-017**: When the selected provider is self-hosted, source code and generated Documents MUST remain within the organization-controlled provider boundary.
- **FR-018**: All knowledge writes and reads MUST enforce caller-supplied Repository access scope; a Worker Agent or provider response MUST NOT select a different Repository or System.
- **FR-019**: Pass 1 and Pass 2 prompts MUST be externalized versioned resources rather than mutable runtime input or hardcoded execution text.
- **FR-020**: The production execution path MUST reuse the existing sequential job dispatch, durable Scan execution, provider configuration, and knowledge-store boundaries without adding a public API, UI, Chat Agent tool, migration, retry/failover policy, or multi-Repository scheduler.

### Key Entities

- **Worker Agent Job**: A closed assignment for either one prepared Pass 1 Code Unit or the single prepared Pass 2 flow, bound to exact Scan and source identities.
- **Worker Agent Result**: A bounded validation outcome carrying a closed Code Logic or Business Logic payload only when valid.
- **Code Logic Document Version**: An immutable technical explanation grounded in exact Code Units and Source Anchors, with permitted same-Repository relationships and communication evidence.
- **Business Logic Document Version**: An immutable non-technical flow explanation synthesized from exact Code Logic Document Versions and prepared trace evidence.
- **Knowledge Commit**: The authorization-scoped atomic unit that persists one complete Document Version aggregate and its retrieval projection before progress advances.
- **Prepared Flow**: The one finite, version-controlled Pass 2 input for the demonstration; it is supplied rather than discovered or clustered.
- **Scan Execution**: Durable lifecycle state that progresses through Pass 1, `Pass1Completed`, the single Pass 2 job, and `Completed` only after successful commits.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For 100% of prepared Pass 1 jobs in the demonstration fixture, each recorded-valid job has exactly one complete, source-grounded Code Logic Document Version committed before its progress increment.
- **SC-002**: Across all accepted invalid, unsafe, mismatched, unauthorized, cancelled, conflict, and rejected fixtures, 0 affected Document Versions, Source Anchors, Edges, flow records, or retrieval projections become visible and 0 jobs are recorded valid.
- **SC-003**: The prepared demonstration Scan dispatches exactly one Pass 2 job after all Pass 1 commits and reaches `Completed` only after exactly one Business Logic Document Version is durably committed.
- **SC-004**: 100% of `GENERATED_FROM` Edges on the prepared Business Logic Document target exactly the Code Logic Document Versions supplied to synthesis, with 0 narrative-created base `REFERENCES` Edges.
- **SC-005**: A non-technical reviewer can identify the prepared flow's purpose, main steps, and outcome from the Business Logic Document without reading source code, while every material explanation remains traceable to persisted evidence.
- **SC-006**: Security verification finds 0 plaintext credentials, raw provider diagnostics, unauthorized source slices, or absolute host paths in transmitted job context, generated results, logs, exceptions, or durable evidence.

## Assumptions

- Stories 11.1 and 11.2 provide the accepted immutable knowledge-store, retrieval, typed job, deterministic sequencing, and durable Scan execution boundaries.
- The prepared Repository source, Code Units, Profiled Revision, hashes, provider assignment, and one version-controlled Pass 2 flow are available and authorized before execution.
- The inherited safe transactional write-reservation requirement for durable Scan execution is corrected before production reachability.
- This `expedited-mvp` feature covers only the August 15 prepared demonstration path. Bounded refinement, cross-verification waves, partial-failure continuation, retries, failover, concurrent supersteps, general Pass 2 trace planning and flow clustering, general Cross-Repository Resolution, public UI/API work, final clean-database two-Repository E2E, and final Docker verification remain explicitly deferred to their owning stories.
- Deferred Epic 5–10 behavior remains backlog and is neither implemented nor claimed complete by this feature.
