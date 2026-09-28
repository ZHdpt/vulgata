# Data Model: Story 18.4 Setup Ownership

These are logical fields and invariants for this story, not a migration or a claim that Story 18.1's identities are known. Exact EF types, table names and the prepared-source association must be checked during reconciliation before tasks.

## Owned batch selection

An **Owned Batch Database** is a deployment-operator-allowlisted PostgreSQL target outside the model's choosing. Its opaque target ID resolves through trusted host configuration to an exact connection descriptor and canonical database identity; both hosts must agree on that identity. A caller cannot substitute a connection string. One database carries one selected October batch in this release. Every database uses identical fixed October schema and migration history. A target authorization check happens before migration/setup; the application cannot infer ownership from reachability.

An **October Batch** has an exact batch ID, server-derived real operator subject, creation/selection metadata, and setup state `NotSelectable` or `Selectable`. The subject originates in the Web Identity-backed activation grant, not a request field. `Selectable` is reached only by an atomic commit of valid setup-owned records. The batch ID is unique within its database; the process selector is the pair `(allowlisted database target, batch ID)`. A duplicate identical setup request can observe the committed result; conflicting input for that pair is rejected. Database identity is never inferred from a batch ID alone.

## Organizational and advisory selection

A **Batch Repository Selection** identifies a valid Repository and optionally its System membership; membership must match the chosen organizational authority. Standalone Repository has no System row or fabricated System context. The selected Repository/root/source identity and grant representation belong to 18.1 and are intentionally undecided. A membership row conveys no communication edge or source access.

A **Selected Advisory Context** is an immutable batch selection of applicable Global, System and Repository values, each with scope, originating authority/record and selection time. Empty/nonapplicable scopes are explicit. When scopes disagree, retain their values and a conflict classification; never overwrite one with another or promote any advisory text to source fact. Later mutable management context cannot silently rewrite this batch's selected inputs.

## Provider binding

A **Host Provider Binding** is process-lifetime configuration: declared source and precedence, API type, endpoint, model, protected credential reference, and Worker/Chat role-to-profile selection. Each effective profile must be complete from the declared source. Deliberate shared-profile selection is recorded explicitly. Binding is not an October database secret copy, provider availability proof or a model tool grant. If a future route selects protected database rows, key ring/application identity/protector purpose compatibility becomes a prerequisite before use.

An **Activation Grant** is a short-lived, purpose-bound protected payload issued by the ordinary Web Identity/management authority for an exact operator, target ID, batch ID and Repository selection. It is sensitive process input, not a persistent October permission or Story 18.1 source grant. Web validates it locally; the generator asks Web to validate it and recheck live authority. Expiry, revocation, unlisted target or unavailable authority denies migration/setup and protected use; renewal requires a newly authenticated/authorized operator action.

## State transitions and constraints

`Unconfigured -> Validating -> Selectable` is allowed only after grant, allowlisted target, operator permission, organizational consistency, context provenance and exact batch identity validate before migration/setup, then all setup-owned rows commit in one October database transaction. The ordinary authority and batch database do not share an atomic transaction or foreign keys; recheck live grant/permission/membership/target at protected use and deny on change. `Validating -> Failed` or cancellation rolls back and leaves no selectable batch. A selected host cannot transition to another database/batch/provider binding while running; it must restart with fresh authorization. A missing/mismatched pair fails before October data access. Source-readiness remains dependent on 18.1 even when the independent setup metadata is valid.

The context's separate `october` model/history/snapshot must contain setup-owned records only in this story. File/CL/read records, publication and one Knowledge writer belong to 18.5 and later stories. Neither Identity rows nor legacy `ScanRun`, pair, FlowGroup or raw Knowledge data are copied to make setup appear valid.
