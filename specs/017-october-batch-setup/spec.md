# Feature Specification: Owned October Batch Setup

**Feature Branch**: `oct14/story-18-4` (assigned worktree; feature path is `specs/017-october-batch-setup`)

**Created**: 2026-09-28

**Status**: Draft correction — ordinary Web startup decision reconciled; affected planning and independent review pending

**Input**: October Story 18.4, “Prepare an Owned October Batch with Valid Context and Provider Bindings,” the integrated and verified Story 18.1 baseline, and the user's [ordinary Web decision](evidence/ordinary-web-user-decision-01.json): the batch runs independently with Web stopped, and Web uses ordinary startup with only its database configuration changed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a valid owned batch (Priority: P1)

As a batch operator, I can prepare one October batch with Web stopped against an explicitly authorized, owned database and my real operator identity. The resulting System, Repository, advisory context and batch records follow shared data rules without invented administration or Scan history. Ordinary Web can subsequently use the configured database without repeating generator setup at startup.

**Why this priority**: A false or incomplete setup could give later analysis and readers the wrong authority or source identity.

**Independent Test**: With Web stopped, an owned database and real permitted operator, prepare a batch and inspect its valid setup; repeat with an unauthorized operator or incomplete input and observe rejection with no partial valid batch.

**Acceptance Scenarios**:

1. **Given** an owned database target, real permitted operator and valid System/Repository inputs with Web stopped, **when** setup is requested, **then** one selected batch has valid operator, System, Repository and context bindings under the shared data rules without requiring a running Web host.
2. **Given** a standalone Repository or a Repository in a System, **when** setup is requested, **then** both valid arrangements are supported and System membership alone creates no communication evidence.
3. **Given** missing authority, an invalid Repository binding or an inconsistent setup input, **when** setup is requested, **then** setup fails visibly and no usable batch is published.

---

### User Story 2 - Select the generator batch and ordinary Web database (Priority: P1)

As an operator, I start the independent generator with an exact database and batch identity. Separately, I start ordinary Web with only its database configuration changed. Web requires no October mode, batch ID, setup-operator credentials or generator provider configuration to start.

**Why this priority**: A previous-batch or cross-database fallback could expose or alter unrelated data.

**Independent Test**: Prepare two owned databases with the same fixed schema names and distinct batch IDs. With Web stopped, exercise valid and invalid generator database/batch pairings. Start ordinary Web against A, then restart against B by changing only its database configuration; verify that ordinary operations use the configured target and that no October startup inputs are required.

**Acceptance Scenarios**:

1. **Given** two distinct owned batch databases with identical fixed schemas, **when** the generator binds an exact valid database/batch pair with Web stopped, **then** it uses only that setup; **when** ordinary Web is separately started with one database configured, **then** its ordinary data operations use that database without an October mode, batch ID, setup-operator credentials or generator provider configuration.
2. **Given** a missing or invalid generator database/batch selection, a nonexistent batch requested for selection, or a mismatched pair, **when** the generator attempts setup or selection, **then** it fails visibly without selecting an earlier, latest or other database's batch. Authorized preparation may create the explicitly requested new batch; existing-batch selection may not fabricate one.
3. **Given** a running generator or Web host, **when** its configured database changes, **then** the change requires restart and no live switch or cross-target access occurs. Changing the generator's batch selection also requires restart.
4. **Given** a database valid for ordinary Web use without any October batch, **when** Web starts with its ordinary configuration, **then** absence of October setup does not prevent startup; ordinary migrations, role seeding, browser Identity authorization and provider administration retain their existing behavior.

---

### User Story 3 - Bind context and provider roles explicitly (Priority: P2)

As a batch operator, I can see which valid Global, System and Repository advisory context was selected and where each value came from. I can also declare the provider configuration and role profiles that the generator will use without hidden discovery or failover. Ordinary Web retains its existing provider administration and does not consume this generator configuration as a startup prerequisite.

**Why this priority**: Later interpretation needs traceable advisory inputs, and provider ambiguity risks wrong credentials, cost or data destination.

**Independent Test**: Select differing context at all applicable scopes and declare a provider configuration source and role profiles; verify retained values, origins, conflicts and effective binding. Omit or conflict a required provider value and observe a visible configuration failure.

**Acceptance Scenarios**:

1. **Given** valid Global, System and Repository context with a disagreement, **when** a batch is bound, **then** selected values and scope/origin are retained and the conflict remains visible rather than silently merged into one asserted fact.
2. **Given** a standalone Repository, **when** context is selected, **then** applicable Global and Repository values are retained without inventing a System value.
3. **Given** an explicitly declared provider configuration source, endpoint, model, credential, API type and Worker/Chat role profiles, **when** the generator binds its batch, **then** the effective configuration and precedence are explicit; sharing a profile occurs only when deliberately selected.
4. **Given** an incomplete or conflicting generator provider binding, **when** the generator starts, **then** it reports a sanitized configuration failure without borrowing credentials, mixing fields silently, discovering an unspecified environment file or switching providers automatically. This does not impose generator provider configuration on ordinary Web startup.

---

### User Story 4 - Preserve October and ordinary isolation (Priority: P2)

As an operator, I can prepare October data without changing ordinary startup behavior or the ordinary data model. Setup diagnostics tell me what was selected and what remains unready without exposing sensitive material.

**Why this priority**: October setup must be additive and must not make normal operation depend on incomplete October work.

**Independent Test**: Compare independent generator setup and ordinary Web startup against owned test targets; verify that only generator setup introduces October persistence, ordinary Web retains its migrations/seeding and browser authorization, and setup diagnostics remain sanitized.

**Acceptance Scenarios**:

1. **Given** ordinary Web startup against its configured database, **when** registration and migrations are evaluated, **then** October mappings and migration history are not applied, ordinary migrations and role seeding still run, and ordinary routes, model, Git/pair validation, browser Identity authorization and provider administration retain their existing behavior.
2. **Given** authorized independent generator setup for an owned target, **when** its setup is evaluated, **then** October persistence uses its separate context, fixed schema, migration history and model snapshot without adding October mappings to the ordinary model.
3. **Given** a setup outcome, **when** diagnostics are emitted, **then** they identify selected batch/configuration state and failure class without credentials, raw source or misleading claims that tools, paid providers or Knowledge generation are ready.

### Edge Cases

- Generator selection rejects an exact batch ID that exists only in a different owned database, even when both databases have the same schema names.
- Reject an operator label that cannot be tied to a real permitted identity; do not fabricate an Administrator.
- Keep a context conflict qualified when values from different scopes disagree; advisory text is not source proof or an instruction to a Worker Agent.
- Treat a missing or incompatible protected-credential configuration as a binding failure. Copy protected rows only if key, application identity and purpose compatibility is established for that chosen route.
- A failed setup must leave no selectable partially initialized October batch or implicit fallback to legacy Knowledge.
- Ordinary Web startup must not apply October migrations or require an October batch, even if October files or batch rows are present. Its own migrations and role seeding remain enabled.
- Missing generator credentials or provider configuration cannot become a Web startup failure; browser users still require their own ordinary Identity authorization.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Generator setup MUST run independently with Web stopped and require an explicitly authorized owned database target and a real permitted operator identity before creating a selectable October batch. (O-FR18)
- **FR-002**: Setup MUST validate and retain the selected batch ID, System/Repository membership and valid Global/System/Repository advisory context through shared rules usable by the generator and October Web; a Repository MAY be standalone. (O-FR01, O-FR02, O-FR18)
- **FR-003**: Setup MUST retain each selected context value with its applicable scope and origin, preserving material conflicts and distinguishing advisory interpretation from source facts. (O-FR02)
- **FR-004**: Setup MUST create only records and mappings owned by this setup scope; it MUST NOT create dummy pair or FlowGroup records, force ScanRun completion, import raw legacy content or create a new management CRUD UI as a prerequisite. (O-FR18, O-AR01–O-AR04)
- **FR-005**: The generator MUST bind an exact database and batch ID; absent, invalid or mismatched selection MUST fail visibly before batch data access, without earlier/latest-batch or other-database fallback. Existing-batch selection MUST reject a nonexistent batch; authorized preparation may create only its explicitly selected new batch. Web MUST use ordinary startup with only its existing database configuration changed, without requiring an October mode, batch ID, setup-operator credentials or generator provider configuration. (O-FR17; user decision 2026-10-02)
- **FR-006**: Two owned batch databases MUST use identical fixed October schemas/models while remaining isolated for all reads and writes. Changing either host's configured database, or the generator's exact batch selection, MUST require restart. Ordinary Web MUST use its configured database without retaining access to the previous target or introducing runtime database switching. (O-FR17, O-AR03)
- **FR-007**: The generator MUST declare the source and precedence for provider endpoint, model, credential, API type and Worker/Chat role profiles; any shared role profile MUST be deliberate. Incomplete or conflicting effective generator configuration MUST fail visibly without silent field mixing, unspecified environment-file discovery, borrowed credentials or automatic provider failover. Ordinary Web MUST retain its existing provider administration and MUST NOT require the generator's provider configuration or binding to start. (O-FR19, O-NFR03; user decision 2026-10-02)
- **FR-008**: Provider and database credentials MUST remain protected and absent from prompts, logs and diagnostics. If protected rows are copied as the selected configuration route, setup MUST establish compatible protection keys, application identity and purpose before use. (O-NFR03)
- **FR-009**: October persistence MUST have a separate context, fixed schema, migration history and model snapshot. Ordinary Web startup MUST neither register nor migrate October persistence, and MUST preserve its existing migrations, role seeding, model, source validation, routes, browser Identity authorization and provider administration when its database configuration changes. (O-AR01–O-AR03)
- **FR-010**: Setup MUST provide sanitized selection/configuration outcomes and failure classifications without claiming source-tool readiness, paid-provider readiness or completed Knowledge generation. (Story 18.4 acceptance)
- **FR-011**: The 18.1-dependent source binding MUST consume the integrated, accepted Story 18.1 Repository/root/source approval and contained read-only tool contracts, with current authority checked before source disclosure. It MUST NOT substitute a newly invented identity, physical-path authority, fake Git revision, source copy or tool grant. Story 18.4's adaptation of those contracts remains subject to deterministic validation and independent review before task generation. (O-FR01; Story 18.1 dependency)
- **FR-012**: Generator setup MUST retain exact batch identity and compatible shared Identity/System/Repository/context data rules in the configured database. Ordinary Web database selection MUST NOT be treated as a batch-selection operation or as setup-operator/source authority. This story MUST NOT infer a latest batch, define later Knowledge read selection, create a separate acceptance/publication/read implementation or overwrite ordinary provider administration. (O-FR18, O-FR19, O-AR04; user decision 2026-10-02)

### Key Entities

- **October Batch**: The exact batch identifier and owning operator selection within one owned database; its selection cannot move to another database by inference.
- **Owned Batch Database**: The explicitly authorized database target containing one batch's fixed October schema; separate targets remain isolated.
- **System and Repository Binding**: Valid organizational membership for selected Repositories, including standalone Repositories; it does not assert communication or grant source-tool access by itself.
- **Selected Advisory Context**: Batch-bound Global/System/Repository values, scopes, origins and conflicts; interpretation guidance rather than source evidence.
- **Provider Binding**: The declared configuration source/precedence and effective endpoint, model, credential reference, API type and Worker/Chat role profiles for the generator process; ordinary Web retains its existing provider administration.
- **Prepared Source Binding**: The batch's approved Repository/root/source access association. Story 18.1 owns the accepted identity, grant and contained tool representation; Story 18.4 must bind its selected batch to that existing authority without creating another source-access contract.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With Web stopped, controlled setup cases for one System member and one standalone Repository retain the exact selected batch and all applicable context origins under shared data rules; no fake operator, pair or FlowGroup is needed.
- **SC-002**: Across two owned databases with identical schema names, 100% of valid exact generator database/batch selections use only the selected target, and 100% of missing or mismatched selections fail without fallback or cross-target reads/writes. Ordinary Web starts against each database by changing only its database configuration between restarts, requires none of the four October startup inputs excluded by FR-005, and its ordinary data operations do not access the other target.
- **SC-003**: In controlled conflicting-context and incomplete-generator-provider cases, 100% retain the context disagreement or report a sanitized configuration failure; none silently substitutes credentials, fields or a provider.
- **SC-004**: In controlled ordinary Web startup, October persistence causes zero October migrations or model additions. Existing migrations, role seeding, browser authorization and provider administration retain their ordinary behavior, including when no October batch exists.
- **SC-005**: Every emitted setup diagnostic in controlled success/failure cases omits credentials and raw source and distinguishes setup selection from later tool, provider and Knowledge readiness.
- **SC-006**: The integrated Story 18.1 baseline is verified. Before task generation, 100% of Story 18.4's source-binding assumptions are reconciled against its accepted contracts, deterministic definition validation passes, and the affected definition receives the required independent review. Baseline availability alone does not satisfy this criterion.

## Assumptions

- The operator supplies and is authorized to use each owned batch database; this definition does not grant database access or authorize running migrations against any actual target.
- Context already exists through valid project management rules. This feature selects it for a batch and does not add context authoring screens or a human-maintained counterpart map.
- The operator explicitly declares the generator provider configuration source. This definition requires visible precedence and complete binding but leaves concrete generator input names and selection mechanics to planning; ordinary Web provider administration is unchanged.
- The independent generator prepares/selects an exact batch. Web starts ordinarily against its configured database, including databases without October setup, and changing that database takes effect only after restart. No Web batch selector, inferred latest batch or replacement read contract follows from removing the special startup path. Later generation, publication, Chat and reader behavior belongs to the owning stories.
- Story 18.1 is complete and its verified baseline is integrated in this worktree. Its accepted approval, issuer, current-authority verifier, read-context and three contained Worker functions are available as the source-access boundary. FR-011 requires Story 18.4 to adapt to that boundary; integration alone does not validate this story's binding.
- The independently decidable batch, database, context, provider and isolation requirements remain in scope. Task generation follows reconciliation of this specification and plan with the actual Story 18.1 contracts, deterministic validation and independent review, plus any applicable planned-change authorization.
