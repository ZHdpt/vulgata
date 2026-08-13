# Feature Specification: Exact Cross-Repository Resolution

**Feature Branch**: `detached@67b5eaf`

**Created**: 2026-08-03

**Status**: Draft

**Execution Level**: `expedited-mvp`

**Input**: Story 11.4 — Exact RPC/HTTP Cross-Repository Demo Resolution, narrowed to one configured custom-RPC path

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resolve One Exact Custom-RPC Path (Priority: P1)

As a SystemOwner, I want one configured custom-RPC communication occurrence resolved across the two prepared Repositories so that the August 15 demonstration can trace a real business flow across a Repository boundary without guessing graph truth.

**Why this priority**: This exact Cross-Repository relationship is the minimum trusted graph fact needed to connect the prepared demonstration flow.

**Independent Test**: Bind the two configured Repository aliases to their authorized records and exact Profiled Revisions, supply one configured `netTools*` call with a literal endpoint and one eligible `trans@name` provider declaration, and verify that exactly one cross-Repository `REFERENCES` Edge is recorded with complete evidence and provenance.

**Acceptance Scenarios**:

1. **Given** the aliases `mbs_account` and `mbs_channel` identify exactly one authorized Repository each and their acquired Profiled Revisions equal the configured full commits, **When** resolution starts after both Pass 1 result sets are durable, **Then** the configuration binds to those Repository records without using persistence identifiers or host paths.
2. **Given** an allowlisted `netTools*` invocation contains a statically exact literal endpoint and one allowed provider declaration contains a `trans@name` that produces the same normalized identifier for the configured Repository direction, **When** deterministic resolution evaluates the occurrence, **Then** exactly one cross-Repository `REFERENCES` Edge is recorded from the consumer Document Version to the provider Document Version.
3. **Given** the exact match is recorded, **When** an authorized user inspects it, **Then** the relationship preserves the raw and normalized endpoint identifiers, protocol, configuration identity and version, exact-match confidence, bounded evidence, and Source Anchors for both sides.

---

### User Story 2 - Fail Closed on Inexact or Ambiguous Evidence (Priority: P1)

As a SystemOwner, I want incomplete, ambiguous, stale, or unsupported communication evidence preserved without creating a relationship so that the demonstration never presents an inferred connection as exact truth.

**Why this priority**: A false Edge would undermine the trustworthiness of the entire Cross-Repository demonstration.

**Independent Test**: Evaluate revision mismatches, variable targets, disallowed invocations, malformed or out-of-root provider declarations, unmatched identifiers, normalization mismatches, and multiple eligible providers, then verify that no `REFERENCES` Edge is created and that eligible diagnostic cases retain bounded Repository-relative evidence.

**Acceptance Scenarios**:

1. **Given** a Repository alias is missing, duplicated, inaccessible, or bound to a Profiled Revision other than its configured full commit, **When** resolution is requested, **Then** it fails before inspecting source evidence or writing graph truth.
2. **Given** a consumer target is variable or non-literal, a call is outside the configured `netTools*` forms, a provider declaration is malformed or outside the configured provider root, identifiers differ after normalization, or zero or multiple eligible providers match, **When** resolution evaluates the occurrence, **Then** no Edge is created and a bounded warning or Uncertainty retains enough Repository-relative evidence for diagnosis.
3. **Given** narrative Pass 1 evidence, LLM interpretation, or System context suggests an endpoint, target, direction, or confidence without the configured exact source match, **When** resolution runs, **Then** the suggestion may explain or select a configured candidate but cannot create or strengthen graph truth.

---

### User Story 3 - Preserve Direction, Authorization, and Source Isolation (Priority: P2)

As a SystemOwner, I want each Repository to act as provider or consumer only for the observed occurrence and all results constrained to my authorized scope so that the same prepared pair can be resolved safely in either configured direction.

**Why this priority**: Repository roles are contextual, while access control and source isolation are mandatory for trustworthy reuse of the resolver.

**Independent Test**: Run eligible configured occurrences in both Repository directions under authorized and unauthorized scopes, then verify direction-specific exact results, no permanent provider/consumer classification, and no disclosure or persistence for callers lacking visibility to either participant.

**Acceptance Scenarios**:

1. **Given** either prepared Repository is configured as consumer and the other as provider, **When** exact eligible evidence matches in that direction, **Then** resolution can succeed without classifying either Repository as globally provider-only or consumer-only.
2. **Given** identical normalized endpoint names exist in both Repositories, **When** an occurrence is resolved, **Then** the configured Repository direction and target binding distinguish the eligible provider.
3. **Given** a caller lacks visibility to either participating System or Repository, **When** resolution or graph retrieval is attempted, **Then** no source, endpoint metadata, evidence, or cross-Repository graph data is disclosed or persisted for that caller.

### Edge Cases

- A configured alias binds to no Repository, more than one Repository, an inaccessible Repository, or the wrong Profiled Revision; resolution stops before evidence inspection and graph writes.
- The `vulgata/August15th-sample` ref does not resolve to the configured full commit; a similarly named ref or current working copy is not accepted as a substitute.
- A `netTools*` call uses a symbol, variable such as `transCode`, computed value, unsupported form, or non-literal endpoint; it is not exact consumer evidence.
- A provider declaration is malformed, lacks `trans@name`, falls outside `src/main/resources/disgram/in/**`, or is excluded by configuration; it is not eligible provider evidence.
- Normalization yields no identifier, different identifiers, or more than one eligible provider in the configured direction; no Edge is created.
- The same endpoint identifier exists in both Repositories; direction and target binding prevent an accidental self-match or reversed match.
- The same successful resolution or warning is retried; no duplicate relationship truth or duplicate diagnostic record is produced.
- Mutable System context changes during an active Scan; the existing queued-application behavior remains in force and the change cannot alter active exact-match truth.
- MQ, file/storage, page navigation, indirect object flow, general HTTP/RPC discovery, or Service Topology MCP evidence is encountered; it is recorded as unsupported or deferred without relationship inference.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST begin this Cross-Repository Resolution only after Pass 1 results for both prepared Repositories are durably available.
- **FR-002**: The version-controlled, schema-versioned demo configuration MUST identify Repository aliases, stable Git identity, expected refs and full commits, evidence selectors, normalization rules, comparison policy, permitted Repository direction, and configuration identity/version.
- **FR-003**: Configuration MUST NOT contain persistence identifiers, working-copy roots, absolute host or WSL paths, environment-specific external Repository locations, or credentials.
- **FR-004**: At runtime, each configured alias MUST bind through authorized Repository management to exactly one permitted Repository in the same authorized System scope.
- **FR-005**: `mbs_account` MUST be verified at full commit `451b3a99b179b711ae66260651eb6b12db482e0b` and `mbs_channel` at full commit `d2e11bacd157195c421065292674ab0c29260081`, each selected by the configured `vulgata/August15th-sample` ref; any mismatch MUST fail closed.
- **FR-006**: Missing, duplicate, inaccessible, or revision-mismatched Repository binding MUST fail before source evidence is inspected or graph state is written.
- **FR-007**: Eligible provider candidates MUST come only from configured declarations beneath `src/main/resources/disgram/in/**`, with `trans@name` as the candidate endpoint identifier.
- **FR-008**: Eligible consumer candidates MUST come only from configured `netTools*` invocation forms containing a statically exact literal endpoint; symbols, variables, computed values, and LLM interpretations MUST NOT constitute exact endpoint evidence.
- **FR-009**: The system MUST apply the configured deterministic normalization and ordinal exact comparison to raw provider and consumer identifiers.
- **FR-010**: An exact match MUST include the configured consumer-to-provider Repository direction and target binding; identifier equality alone MUST NOT select a provider in another direction.
- **FR-011**: The system MUST create exactly one cross-Repository `REFERENCES` Edge from the consumer Document Version to the provider Document Version only when exactly one eligible provider matches all configured exact evidence.
- **FR-012**: Every created Edge MUST preserve consumer and provider Repository identity, Document Version identity, Source Anchors, raw and normalized endpoint identifiers, protocol, configuration identity/version, exact-match confidence, and bounded evidence.
- **FR-013**: Variable, non-literal, disallowed, malformed, out-of-root, unmatched, normalization-mismatched, revision-mismatched, or multiply matched evidence MUST create no Edge.
- **FR-014**: Eligible non-match or ambiguity outcomes MUST be preserved as one bounded warning or Uncertainty with Repository-relative evidence sufficient for diagnosis and without plaintext source, credentials, absolute paths, or external-Repository locations.
- **FR-015**: Repeating the same resolution input MUST be idempotent and MUST NOT duplicate an exact relationship or its warning/Uncertainty.
- **FR-016**: Provider and consumer MUST remain roles of an observed communication occurrence; neither Repository may receive a durable provider-only or consumer-only classification.
- **FR-017**: Either prepared Repository MUST be allowed to serve as consumer or provider when an eligible configuration entry and exact evidence select that direction.
- **FR-018**: Pass 1 narrative, LLM output, and System context MAY help select or explain configured candidates but MUST NOT create endpoint identity, role, target, direction, confidence, or an Edge without the configured exact code/declaration match.
- **FR-019**: System context changes during an active Scan MUST retain the existing queued-application behavior.
- **FR-020**: Authorization MUST be verified for both participating Systems and Repositories before source evidence is accessed, resolved, persisted, or returned.
- **FR-021**: Durable and returned evidence MUST use Repository-relative locators and MUST NOT disclose plaintext source, credentials, absolute host paths, or unauthorized Repository, endpoint, route, evidence, or business details.
- **FR-022**: MQ, file/storage, page navigation, indirect object flow, general HTTP/RPC discovery, Service Topology MCP, and every non-configured communication kind MUST be recorded as unsupported or deferred and MUST NOT create a relationship.
- **FR-023**: This feature MUST NOT invent or embed a concrete business endpoint; the demonstrated literal/declaration pair MUST be selected from the pinned Repository evidence through the version-controlled allowlist.
- **FR-024**: Story 11.5 MUST remain the owner of the final clean-database two-Repository end-to-end demonstration and final Docker image release gate.

### Key Entities

- **Prepared Repository Binding**: The authorized association between a configuration alias, one managed Repository, its stable Git identity, expected ref, and exact Profiled Revision.
- **Communication Occurrence**: One direction-specific observation in which a prepared Repository acts as consumer and the other as provider without creating a permanent role classification.
- **Provider Candidate**: An eligible configured provider declaration carrying a raw endpoint identifier and Repository-relative Source Anchor.
- **Consumer Candidate**: An eligible configured invocation carrying a statically exact literal endpoint and Repository-relative Source Anchor.
- **Exact Match**: The unique outcome in which configured normalization, ordinal comparison, Repository direction, target binding, revision, and evidence rules all agree.
- **REFERENCES Edge**: The directed dependency between the consumer Document Version and provider Document Version created only from one Exact Match.
- **Uncertainty**: Bounded, non-sensitive evidence of an unresolved or ambiguous occurrence for which no Edge is justified.
- **Resolution Configuration**: The version-controlled, schema-versioned allowlist and provenance used to bind Repositories and judge exact evidence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The accepted demonstration fixture resolves its one configured exact custom-RPC occurrence into exactly 1 cross-Repository `REFERENCES` Edge and 0 inferred Edges.
- **SC-002**: For 100% of eligible configured fixtures in either Repository direction, the selected provider is the unique provider in that configured direction and neither Repository receives a permanent provider/consumer classification.
- **SC-003**: Across all accepted missing-binding, duplicate-binding, authorization, revision-mismatch, variable-target, malformed, wrong-root, unmatched, normalization-mismatch, and multiple-provider fixtures, 0 `REFERENCES` Edges are created.
- **SC-004**: 100% of created exact Edges retain both Source Anchors, both raw and normalized identifiers, protocol, configuration identity/version, exact-match confidence, and bounded evidence.
- **SC-005**: Retrying the same successful, unmatched, or ambiguous input produces 0 duplicate Edges and 0 duplicate warnings or Uncertainties.
- **SC-006**: Security inspection finds 0 plaintext source fragments, credentials, absolute host/WSL paths, external-Repository locations, or serialized persistence identifiers in version-controlled configuration, textual evidence payloads, diagnostics, logs, or unauthorized returned graph data. Internal relational primary/foreign keys MAY persist solely to enforce ownership, referential integrity, idempotency, and authorized retrieval, and MUST NOT be exposed as source evidence or to unauthorized callers.
- **SC-007**: For 100% of unsupported/deferred communication fixtures, the outcome records the limitation and creates 0 relationships.
- **SC-008**: An authorized SystemOwner can trace the resolved occurrence from the consumer Document Version to the provider Document Version using the preserved evidence without relying on LLM interpretation or System context as match truth.

## Assumptions

- Stories 11.1 through 11.3 provide the accepted knowledge-store, sequential Scan, Pass 1, Document Version, Source Anchor, and retrieval boundaries required by this feature.
- The two prepared Repositories are organization-controlled and available through Repository management; deployment-specific acquisition locations remain outside the version-controlled resolution configuration.
- The immutable planning inputs are `mbs_account` commit `451b3a99b179b711ae66260651eb6b12db482e0b` and `mbs_channel` commit `d2e11bacd157195c421065292674ab0c29260081`, both selected by `vulgata/August15th-sample`.
- Read-only planning inspection selected one real literal/`trans@name` occurrence and its exact source selectors from those pinned Git objects in the reviewed version-controlled demo profile; the endpoint was observed from source rather than invented by this specification.
- This `expedited-mvp` feature covers one configured custom-RPC path only. General HTTP/RPC discovery, other communication kinds, general multi-Repository scheduling, advanced uncertainty resolution, and post-demo Cross-Repository behavior remain backlog.
- System context is advisory and continues to follow the existing queued-application rule during an active Scan.
- Story 11.5 owns the clean-database two-Repository rehearsal and final Docker image verification; this story uses current-boundary integration and security verification.
