# Feature Specification: Owned October Batch Setup

**Feature Branch**: `detached-managed-worktree` (assigned worktree; feature path is `specs/016-october-batch-setup`)

**Created**: 2026-09-28

**Status**: Draft — definition and planning only; Story 18.1 integration remains a completion dependency

**Input**: October Story 18.4, “Prepare an Owned October Batch with Valid Context and Provider Bindings,” and the user's direction to plan independently decidable setup while withholding delivery until Story 18.1 is integrated and verified.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a valid owned batch (Priority: P1)

As a batch operator, I can prepare one October batch against an explicitly authorized, owned database and my real operator identity. The resulting System, Repository, advisory context and batch records are valid for both the generator and October Web without invented administration or Scan history.

**Why this priority**: A false or incomplete setup could give later analysis and readers the wrong authority or source identity.

**Independent Test**: With an owned database and real permitted operator, prepare a batch and inspect its valid setup; repeat with an unauthorized operator or incomplete input and observe rejection with no partial valid batch.

**Acceptance Scenarios**:

1. **Given** an owned database target, real permitted operator and valid System/Repository inputs, **when** setup is requested, **then** one selected batch has valid operator, System, Repository and context bindings available to both October hosts.
2. **Given** a standalone Repository or a Repository in a System, **when** setup is requested, **then** both valid arrangements are supported and System membership alone creates no communication evidence.
3. **Given** missing authority, an invalid Repository binding or an inconsistent setup input, **when** setup is requested, **then** setup fails visibly and no usable batch is published.

---

### User Story 2 - Select exactly one batch and database (Priority: P1)

As an operator starting the generator or October Web, I select the exact database and batch that the host will use. I can tell when either selection is absent or inconsistent before any batch read or write occurs.

**Why this priority**: A previous-batch or cross-database fallback could expose or alter unrelated data.

**Independent Test**: Prepare two owned databases with the same fixed schema names and distinct batch IDs. Start each host with every valid and invalid pairing and observe exact selection, rejection and restart-only switching.

**Acceptance Scenarios**:

1. **Given** two distinct owned batch databases with identical fixed schemas, **when** the generator and October Web start with the same valid database and batch ID, **then** both select that batch and neither reads nor writes the other database.
2. **Given** a missing database, missing batch ID or mismatched pair, **when** either host starts, **then** it fails visibly without selecting an earlier, latest or other database's batch.
3. **Given** a running host bound to a batch, **when** a different database or batch is requested, **then** the host requires a restart before using the new selection.

---

### User Story 3 - Bind context and provider roles explicitly (Priority: P2)

As a batch operator, I can see which valid Global, System and Repository advisory context was selected and where each value came from. I can also declare the provider configuration and role profiles that the host will use without hidden discovery or failover.

**Why this priority**: Later interpretation needs traceable advisory inputs, and provider ambiguity risks wrong credentials, cost or data destination.

**Independent Test**: Select differing context at all applicable scopes and declare a provider configuration source and role profiles; verify retained values, origins, conflicts and effective binding. Omit or conflict a required provider value and observe a visible configuration failure.

**Acceptance Scenarios**:

1. **Given** valid Global, System and Repository context with a disagreement, **when** a batch is bound, **then** selected values and scope/origin are retained and the conflict remains visible rather than silently merged into one asserted fact.
2. **Given** a standalone Repository, **when** context is selected, **then** applicable Global and Repository values are retained without inventing a System value.
3. **Given** an explicitly declared provider configuration source, endpoint, model, credential, API type and Worker/Chat role profiles, **when** a host binds its batch, **then** the effective configuration and precedence are explicit; sharing a profile occurs only when deliberately selected.
4. **Given** an incomplete or conflicting provider binding, **when** a host starts, **then** it reports a sanitized configuration failure without borrowing credentials, mixing fields silently, discovering an unspecified environment file or switching providers automatically.

---

### User Story 4 - Preserve October and ordinary isolation (Priority: P2)

As an operator, I can prepare October data without changing ordinary startup behavior or the ordinary data model. Setup diagnostics tell me what was selected and what remains unready without exposing sensitive material.

**Why this priority**: October setup must be additive and must not make normal operation depend on incomplete October work.

**Independent Test**: Compare ordinary and October-enabled startup against owned test targets; verify only October mode uses October setup, and inspect sanitized success/failure diagnostics.

**Acceptance Scenarios**:

1. **Given** ordinary startup, **when** registration and migrations are evaluated, **then** October mappings and migration history are not applied and ordinary routes, model and Git/pair validation behavior remain unchanged.
2. **Given** October-enabled startup for an owned target, **when** its setup is evaluated, **then** October persistence uses its separate context, fixed schema, migration history and model snapshot without adding October mappings to the ordinary model.
3. **Given** a setup outcome, **when** diagnostics are emitted, **then** they identify selected batch/configuration state and failure class without credentials, raw source or misleading claims that tools, paid providers or Knowledge generation are ready.

### Edge Cases

- Reject an exact batch ID that exists only in a different owned database, even when both databases have the same schema names.
- Reject an operator label that cannot be tied to a real permitted identity; do not fabricate an Administrator.
- Keep a context conflict qualified when values from different scopes disagree; advisory text is not source proof or an instruction to a Worker Agent.
- Treat a missing or incompatible protected-credential configuration as a binding failure. Copy protected rows only if key, application identity and purpose compatibility is established for that chosen route.
- A failed setup must leave no selectable partially initialized October batch or implicit fallback to legacy Knowledge.
- Ordinary startup must not apply October migrations, even if October files are present in the application.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Setup MUST require an explicitly authorized owned database target and a real permitted operator identity before creating a selectable October batch. (O-FR18)
- **FR-002**: Setup MUST validate and retain the selected batch ID, System/Repository membership and valid Global/System/Repository advisory context through shared rules usable by the generator and October Web; a Repository MAY be standalone. (O-FR01, O-FR02, O-FR18)
- **FR-003**: Setup MUST retain each selected context value with its applicable scope and origin, preserving material conflicts and distinguishing advisory interpretation from source facts. (O-FR02)
- **FR-004**: Setup MUST create only records and mappings owned by this setup scope; it MUST NOT create dummy pair or FlowGroup records, force ScanRun completion, import raw legacy content or create a new management CRUD UI as a prerequisite. (O-FR18, O-AR01–O-AR04)
- **FR-005**: Each October host MUST bind an exact database and batch ID at startup; absent, invalid or mismatched selection MUST fail visibly before batch data access, without earlier/latest-batch or other-database fallback. (O-FR17)
- **FR-006**: Two owned batch databases MUST use identical fixed October schemas/models while remaining isolated for all reads and writes; changing a host's selected database or batch MUST require restart. (O-FR17, O-AR03)
- **FR-007**: Each host MUST declare the source and precedence for provider endpoint, model, credential, API type and Worker/Chat role profiles; any shared role profile MUST be deliberate. Incomplete or conflicting effective configuration MUST fail visibly without silent field mixing, unspecified environment-file discovery, borrowed credentials or automatic provider failover. (O-FR19, O-NFR03)
- **FR-008**: Provider and database credentials MUST remain protected and absent from prompts, logs and diagnostics. If protected rows are copied as the selected configuration route, setup MUST establish compatible protection keys, application identity and purpose before use. (O-NFR03)
- **FR-009**: October persistence MUST have a separate context, fixed schema, migration history and model snapshot. Ordinary startup MUST neither register nor migrate October persistence, and its existing model, source validation and routes MUST remain unchanged. (O-AR01–O-AR03)
- **FR-010**: Setup MUST provide sanitized selection/configuration outcomes and failure classifications without claiming source-tool readiness, paid-provider readiness or completed Knowledge generation. (Story 18.4 acceptance)
- **FR-011**: The 18.1-dependent source binding MUST consume the integrated, accepted 18.1 Repository/root/source identity and contained read-only tool contracts when available. It MUST NOT substitute a newly invented identity, physical-path authority, fake Git revision, source copy or tool grant. Its final validation and detailed interface remain pending plan reconciliation with a verified integrated 18.1 baseline. (O-FR01; Story 18.1 dependency)
- **FR-012**: The generator and October Web MUST use the same selected setup identity and compatible shared data rules; this story MUST NOT create a separate acceptance/publication/read implementation or overwrite ordinary provider administration. (O-FR18, O-FR19, O-AR04)

### Key Entities

- **October Batch**: The exact batch identifier and owning operator selection within one owned database; its selection cannot move to another database by inference.
- **Owned Batch Database**: The explicitly authorized database target containing one batch's fixed October schema; separate targets remain isolated.
- **System and Repository Binding**: Valid organizational membership for selected Repositories, including standalone Repositories; it does not assert communication or grant source-tool access by itself.
- **Selected Advisory Context**: Batch-bound Global/System/Repository values, scopes, origins and conflicts; interpretation guidance rather than source evidence.
- **Provider Binding**: The declared configuration source/precedence and effective endpoint, model, credential reference, API type and Worker/Chat role profiles for a host process.
- **Prepared Source Binding**: The batch's approved Repository/root/source access association. Its exact identity, grant and tool representation are owned by Story 18.1 and are pending integration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In controlled setup cases for one System member and one standalone Repository, both hosts identify the same selected batch and all applicable context origins; no fake operator, pair or FlowGroup is needed.
- **SC-002**: Across two owned databases with identical schema names, 100% of valid exact database/batch selections use only the selected target, and 100% of missing or mismatched selections fail without fallback or cross-target reads/writes.
- **SC-003**: In controlled conflicting-context and incomplete-provider cases, 100% retain the context disagreement or report a sanitized configuration failure; none silently substitutes credentials, fields or a provider.
- **SC-004**: In controlled ordinary startup, October persistence causes zero October migrations or model additions and leaves existing ordinary behavior unchanged.
- **SC-005**: Every emitted setup diagnostic in controlled success/failure cases omits credentials and raw source and distinguishes setup selection from later tool, provider and Knowledge readiness.
- **SC-006**: Before downstream delivery resumes, a verified integrated Story 18.1 baseline is present, each 18.1-dependent source-binding assumption is reconciled against its accepted contracts, and the affected definition receives the required independent review. Until then, dependency satisfaction is 0% claimed.

## Assumptions

- The operator supplies and is authorized to use each owned batch database; this definition does not grant database access or authorize running migrations against any actual target.
- Context already exists through valid project management rules. This feature selects it for a batch and does not add context authoring screens or a human-maintained counterpart map.
- The operator explicitly declares which provider configuration source a process uses. This definition requires visible precedence and complete binding but leaves concrete input names and selection mechanics to planning.
- The October host generator and October Web are distinct consumers of one selected setup. Their later generation, publication, Chat and reader behavior belongs to their owning stories.
- Story 18.1 is still being implemented elsewhere. Its accepted integrated source identity, Repository/root binding and contained read-only tool contracts are unknown here. FR-011 is a dependency obligation, not an implementation choice or claim of contract availability.
- Planning may resolve independent batch, database, context, provider and isolation decisions now. Task generation and later delivery remain held until the integrated verified Story 18.1 baseline is present in this worktree and the specification/plan are reconciled against its actual contracts and reviewed as required.
