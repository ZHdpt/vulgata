# Data Model: Story 18.4 Setup Ownership

These fields retain Story 18.4's existing persisted model. The independent-host correction changes authority composition, not Story 18.1 identity or the canonical v2 context digest.

## Database and immutable batch

One owned selected PostgreSQL database contains one October batch in identical fixed october schema/model/history. Default deployment co-locates existing identity/vulgata schemas; only Identity may use an explicitly configured separate database. Management data remains in the selected dataset. Context/schema/migration ownership does not change.

An October Batch retains exact BatchId, TargetId, CanonicalDatabaseIdentity, authenticated real SubjectId, immutable ManagementAuthorityId, SelectionDigest, SelectedAtUtc and NotSelectable/Selectable state. Unique selection slot enforces one batch per database. Atomic setup commit makes all owned records selectable together; failures do not publish partial setup. Exact identical replay returns original identity/time; conflicting input cannot overwrite.

AuthorityId names the actual configured management/Identity combination, not a host instance or request label. Current authority must match persisted identity exactly before historical context/capability disclosure. Different authority with colliding IDs denies. Switching Web DefaultConnection changes the data target only after restart and matching target/batch/authority configuration; it cannot alias an old authority by inference.

## Organization and advisory context

A Batch Repository Selection records RepositoryId, optional SystemId, immutable opaque RootBindingIdentity version and descriptive OperatorLabel. Membership is live-validated; a standalone Repository has no fabricated System. Exactly one binding per (BatchId, RepositoryId). Physical directories remain deployment configuration only, never durable rows/Agent arguments.

Selected Advisory Context retains three entries per Repository: Global, System and Repository, each with Selected/NotSelected/NotApplicable, originating record/value where selected and AdvisoryNotSourceProof. Standalone System is NotApplicable. Values preserve null versus empty. Differing selected scopes are retained as ordered pairs rather than flattened or promoted to source facts.

The trusted adapter captures current management values, not caller text. Existing OctoberSelectedAdvisoryContext and v2 digest cover exact membership/root/reference/value/authority content; the writer adds first-commit UTC SelectedAtUtc outside the digest. Identical retry preserves time. Same-authority read-only binding returns stored historical content even when current text changes, after current stable-selection access checks. New prepare with changed semantic content conflicts.

## Source approval and authentication lifetime

Current Prepared Source Approval remains an October row keyed by opaque random ApprovalIdentity: exact BatchId, RepositoryId, real SubjectId, WorkerSourceRead purpose, RootBindingIdentity, label, UTC expiry and IsRevoked. It contains no activation grant/password/factor/security stamp/physical path/live context. Multiple historical approvals may exist, but an exact current row is necessary and insufficient without fresh identity/membership/target/root authority.

IOctoberOperatorSession is ephemeral, minted only by the production headless Identity adapter after actual authentication. Private implementation binds its authority instance, subject, original security stamp, expiry and closed state; no caller constructor or serialization can create accepted authority. It holds no password after authentication. Every protected operation checks current account/stamp/eligibility/roles; expiry/disposal/authority loss closes it. It is never a batch row or a Web browser principal.

Generator prepare/select owns and disposes this session within ExecuteAsync; Web owns and disposes its startup session before serving. Their returned setup metadata does not retain source authority. The controlled source-adapter integration authenticates a separate caller-owned session and keeps it alive through issuance, settled borrowed function calls and asynchronous disposal of every context, then disposes the session. No production generation caller or durable session is introduced by this setup story.

Approval issuance requires a current authenticated session and exact bound setup. Maximum approval lifetime is ten minutes, bounded by session expiry. Direct revoke is idempotent for an authorized subject/current Administrator. Fresh verifier calls read the current row and live identity/management data, so revocation works with Web stopped. Restored permission cannot reactivate a latched closed 18.1 context.

## Host configuration and provider binding

Host selection is immutable process configuration: exact target/batch/data connection, AuthorityId, optional explicit Identity connection, provider source/profiles and trusted root map. Defaults are defined in the shared contract, not inferred from an available server or previous target. Configuration reload never retargets a process.

Host Provider Binding retains one declared source/precedence, complete API type/endpoint/model/protected credential reference and explicit Worker/Chat profile selection. No provider secret is copied into October. Authentication credentials use a separate purpose/input path, are never provider credentials and are not stored in batch records.

## State transitions

Configured -> Authenticated -> AuthorizedSelection -> Selectable is valid only after real Identity authentication, configured/connected target verification, live organization/context access and atomic October commit. Existing-batch selection validates the exact committed batch before exposing its capability. Authentication/authorization/cancellation failure yields no session or selection payload as appropriate; failed prepare leaves no selectable batch. Authority reads may access identity/vulgata in the same selected database before October migration.

Generator prepare does not create databases, authority schemas/users/roles or seed/export/import real data. Valid authority records use their existing owner and actual permission. Qualification fixtures use supported Identity/domain creation APIs on owned resources. No online Web process or bootstrap is necessary.

Issued -> AuthorityClosed/ExpiredOrCancelled/Disposed source context transitions remain Story 18.1-owned. Current authority loss/outage invalidates continuations and discards buffered source. Caller settles all borrowed function calls and disposes the context; handles/cursors never persist.

October model/history/snapshot remains setup-owned only. No legacy ScanRun/pair/FlowGroup/Knowledge import and no later-file/CL/publication tables are introduced by this correction.
