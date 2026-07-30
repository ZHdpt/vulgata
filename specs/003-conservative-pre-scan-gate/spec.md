# Feature Specification: Conservative Pre-Scan Gate

**Feature Branch**: `story4.3`

**Created**: 2026-07-30

**Status**: Draft

**Input**: User description: "For Story 4.3, deterministically classify prepared RPC and HTTP communication kinds after Repository profiling, preserve conservative evidence and confidence, and publish one durable automatically approved pre-scan handoff for the August 15 sequential demonstration runner."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Classify Prepared Communication Evidence (Priority: P1)

As a SystemOwner, I want the completed Repository profile checked for prepared
RPC and HTTP communication kinds so the demonstration Scan can proceed using
deterministic, auditable evidence without an LLM or human wait.

**Why this priority**: The sequential demonstration runner cannot safely
proceed until the Repository profile has a conservative communication-kind
classification.

**Independent Test**: Run the gate against prepared Repository profiles
containing direct RPC evidence, direct HTTP evidence, import-only evidence, and
unsupported communication families. Verify that only direct supported evidence
is confirmed, weak evidence remains a hint, and unsupported families are not
guessed.

**Acceptance Scenarios**:

1. **Given** persisted Code Units and language/framework facts contain a
   supported direct RPC annotation or matched configuration, **When** the
   deterministic post-profile pass runs, **Then** it records the RPC kind as
   confirmed with high confidence and bounded provenance.
2. **Given** the prepared profile contains supported direct HTTP controller,
   client, annotation, or matched configuration evidence, **When** the pass
   runs, **Then** it records the HTTP kind as confirmed with high confidence
   and bounded provenance.
3. **Given** a communication framework appears only as an import or other weak
   evidence, **When** the pass runs, **Then** it records a conservative hint and
   does not represent the hint as a confirmed code-derived fact.
4. **Given** evidence concerns MQ, file/storage, page navigation, indirect
   object flow, Service Topology, or another unsupported family, **When** the
   pass runs, **Then** it records no confirmed kind for that family and leaves
   the family deferred.

---

### User Story 2 - Publish One Approved Pre-Scan Handoff (Priority: P1)

As a SystemOwner, I want one durable `pre_scan_approved` result for the
Repository and ScanRun so the later sequential runner receives a complete,
approved Pre-Scan Handoff without dispatch occurring in this feature.

**Why this priority**: Automatic approval is the deadline-critical contract
between completed Repository profiling and Story 11.2.

**Independent Test**: Complete the gate with confirmed kinds, hints, warnings,
and with warnings only; then retrieve the result and retry the gate for the
same ScanRun. Verify one complete approved result, no duplicate facts, and no
Worker Agent dispatch.

**Acceptance Scenarios**:

1. **Given** deterministic classification completes with any mix of confirmed
   kinds, hints, and warnings, **When** the gate completes, **Then** exactly one
   Repository- and ScanRun-scoped result transitions directly to
   `pre_scan_approved`.
2. **Given** an approved result exists, **When** an authorized consumer requests
   the handoff, **Then** it receives the result identity, Repository and ScanRun
   identity, approval state, schema version, confirmed kinds, hints, warnings,
   evidence locators, confidence, and provenance needed by the sequential
   runner.
3. **Given** the same completed profile is processed again for the same ScanRun,
   **When** the retry completes, **Then** it yields the same logical approved
   result without a second approved result or duplicate facts.
4. **Given** approval persistence fails, **When** the operation ends, **Then**
   no partial approved result or partial classification is observable.
5. **Given** an approved handoff is published, **When** this feature finishes,
   **Then** it has created no Worker Agent job and performed no dispatch.

---

### User Story 3 - Review a Safe Conservative Outcome (Priority: P2)

As an authorized SystemOwner, I want a bounded Chinese outcome summary so I can
see that pre-scan approved conservatively without exposing source, secrets, or
host details.

**Why this priority**: The demonstration needs an auditable approval outcome,
but observability must preserve Repository visibility and source isolation.

**Independent Test**: Request the same warning-bearing result as an authorized
and unauthorized user and inspect its Scan log and summary. Verify the exact
Chinese approval message for the authorized user and zero restricted details
for the unauthorized user.

**Acceptance Scenarios**:

1. **Given** a warning-bearing gate approves, **When** the outcome is recorded,
   **Then** the Scan log records
   `预扫描已批准并保留审慎警告——继续进入顺序演示运行器`.
2. **Given** an authorized user requests the outcome, **When** the summary is
   returned, **Then** all evidence is bounded and Repository-relative and no
   source content, absolute host path, secret, or unbounded diagnostic appears.
3. **Given** a user cannot view the Repository, **When** that user requests the
   handoff or summary, **Then** no result existence, evidence, warning, or
   Repository detail is disclosed.

### Edge Cases

- A completed profile contains no prepared RPC or HTTP evidence: the result
  still auto-approves with no confirmed kinds and does not invent a fact.
- An individual evidence input is malformed, unsafe, oversized, contradictory,
  or unsupported: it is isolated as a bounded warning or omitted evidence,
  independent valid inputs continue, and no confirmed fact is invented.
- Multiple inputs justify the same communication kind: the approved result
  retains one deterministic logical fact with bounded supporting evidence
  rather than duplicate facts.
- A hint and direct evidence concern the same kind: the direct evidence may
  justify confirmation, but the weaker item is not itself relabelled as
  high-confidence evidence.
- Repository profiling fails or is incomplete: no approved handoff is
  published.
- Authorization is revoked after approval: subsequent retrieval follows the
  current Repository visibility decision and discloses no stale authorized
  data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST run the pre-scan gate only after one Repository's
  Code Units, language facts, and framework facts have completed profiling for
  the target ScanRun.
- **FR-002**: The system MUST classify only the prepared demonstration's RPC and
  HTTP communication kinds from bounded Repository-local structural,
  framework, package-manifest, annotation, and configuration evidence.
- **FR-003**: Classification MUST be deterministic and MUST NOT use an LLM,
  Human-in-the-Loop input, package execution, network lookup, or source-code
  exfiltration.
- **FR-004**: Direct supported framework annotations or matched configuration
  MUST be recorded as confirmed high-confidence communication kinds.
- **FR-005**: Import-only and other weak evidence MUST be retained as
  conservative hints and MUST NOT be represented as confirmed code-derived
  facts.
- **FR-006**: Material ambiguity, malformed or unsafe individual evidence, and
  other non-confirming conditions MUST be retained only as bounded conservative
  warnings or omitted evidence and MUST NOT create a confirmed fact.
- **FR-007**: Every confirmed kind, hint, and warning MUST carry deterministic
  provenance and a bounded Repository-relative evidence locator sufficient to
  audit its classification without retaining source content.
- **FR-008**: Evidence and diagnostics exposed or retained by this feature MUST
  exclude absolute host paths, source content, secrets, attacker-controlled
  type discriminators, and unbounded payloads.
- **FR-009**: Completion of deterministic classification, including a result
  containing only hints or warnings, MUST transition directly to
  `pre_scan_approved` without an LLM or human wait.
- **FR-010**: The system MUST persist exactly one durable result per Repository
  and ScanRun containing approval state, schema version, confirmed kinds,
  hints, warnings, confidence, evidence locators, and provenance.
- **FR-011**: The approved result and all of its classification data MUST become
  observable atomically; a failure MUST leave no partially approved result.
- **FR-012**: Reprocessing the same Repository and ScanRun MUST be idempotent and
  MUST NOT create another approved result or duplicate logical facts.
- **FR-013**: An authorized consumer MUST be able to retrieve an approved
  Pre-Scan Handoff containing the information required by the Story 11.2
  sequential runner.
- **FR-014**: Handoff and summary access MUST preserve existing ManagementAccess
  and Repository visibility rules, including non-disclosure when access is
  absent or revoked.
- **FR-015**: Approval with conservative warnings MUST record the Simplified
  Chinese Scan-log message
  `预扫描已批准并保留审慎警告——继续进入顺序演示运行器`.
- **FR-016**: This feature MUST NOT identify provider or consumer roles, resolve
  endpoint targets, create Uncertainties or REFERENCES Edges, or perform
  Cross-Repository Resolution.
- **FR-017**: This feature MUST NOT create or dispatch a Worker Agent job or
  implement a Scan Coordinator, queue, lease, retry policy, concurrency,
  superstep, provider failover, or sequential runner.
- **FR-018**: This feature MUST NOT confirm or resolve MQ, file/storage,
  page-navigation, indirect object-flow, Service Topology, or other
  communication families outside the prepared RPC/HTTP acceptance path.
- **FR-019**: This feature MUST NOT implement Communication Pattern Catalog
  management, LLM question generation, manual approval, the seven-day review
  lifecycle, notifications, or other Human-in-the-Loop interactions.

### Key Entities

- **Pre-Scan Result**: The single durable Repository- and ScanRun-scoped gate
  outcome. It carries its identity, `pre_scan_approved` state, schema version,
  confirmed communication kinds, conservative hints, conservative warnings,
  evidence, confidence, and provenance.
- **Communication Kind Classification**: A normalized RPC or HTTP kind and its
  truth level: confirmed high-confidence fact, conservative hint, or
  conservative warning. A hint or warning is never a confirmed fact.
- **Evidence Locator**: A bounded Repository-relative pointer to the structural,
  framework, manifest, annotation, or configuration evidence used for a
  classification; it contains no source content or host path.
- **Pre-Scan Handoff**: The authorized, versioned view of the approved
  result consumed later by the sequential runner. It carries no dispatch
  instruction or resolved provider/consumer endpoint.

### Deferred Scope

- Detailed provider/consumer/endpoint detection, exact RPC/HTTP matching,
  REFERENCES creation, and Cross-Repository Resolution belong to Story 11.4
  and the post-deadline backlog.
- MQ, file/storage, page-navigation, indirect object-flow, Service Topology,
  LLM/Human-in-the-Loop, dispatch, and full Communication Pattern Catalog
  management are outside the August 15 acceptance boundary.
- The sequential runner and typed Worker Agent dispatch consume this feature's
  approved handoff in Story 11.2; they are not part of this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across the prepared demonstration fixtures, 100% of supported
  direct RPC and HTTP annotations or matched configurations are returned as
  confirmed high-confidence kinds without an LLM or human wait.
- **SC-002**: Across all acceptance fixtures, 100% of import-only hints,
  warnings, and unsupported-family inputs create zero confirmed
  code-derived facts.
- **SC-003**: Every successful gate run produces exactly one
  `pre_scan_approved` result for its Repository and ScanRun; any number of
  retries produces zero additional approved results and zero duplicate logical
  facts.
- **SC-004**: In all injected approval-persistence failures, zero partial
  approved results and zero partial classifications are observable.
- **SC-005**: Authorized handoff checks expose 100% of the required fields,
  while unauthorized or visibility-revoked checks expose zero result,
  Repository, evidence, warning, source, path, or secret details.
- **SC-006**: 100% of retained or exposed evidence locators are bounded,
  Repository-relative, and free of source content, absolute host paths, and
  secrets.
- **SC-007**: The complete Story 4.3 acceptance suite creates zero Worker Agent
  jobs, zero REFERENCES Edges, and zero provider/consumer endpoint
  resolutions.

## Assumptions

- Stories 4.1 and 4.2 provide a complete, atomically persisted Repository
  profile; their provider-level failure behavior remains authoritative.
- The prepared demonstration's supported RPC/HTTP evidence vocabulary is a
  closed delivery input. User-facing catalog editing and organization-wide
  extensibility are deferred.
- A Scan targets exactly one Repository, and one pre-scan result belongs to one
  Repository and one ScanRun.
- Existing ManagementAccess and Repository visibility decisions are reused for
  the handoff and summary.
- Story 11.2 is the first dispatching consumer of the
  `pre_scan_approved` handoff.
- `execution_level: expedited-mvp` governs workflow concision only; it does not
  weaken deterministic evidence, authorization, redaction, atomicity, or
  idempotency.
