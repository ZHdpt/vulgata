# Research: Retained Source Access

## Authority set and scope

Decisions use Story 13.1 in `docs/bmad/epics.md`, the active specification, PRD FR-2.4/2.5/4.9 and NFR-2.3/3.1/3.3/4.1, architecture AD-1/2/3/5/6/8/15/16/30/31/32, Constitution 2.0.0, the glossary, and current source/tests cited in `plan.md`.

These decisions cover exact source acquisition, retention, reopening, and scoped reads only. They do not settle semantic source formats, Scan work planning, Knowledge/Chat, container backup/restore, or tool installation.

The authorized compatibility prerequisite is narrower still: it repairs the existing canonical File Code Unit identity producer/validator mismatch needed to complete Story 13.1 full-suite verification. It does not adopt a historical feature plan or extend the source design below.

## R0 — Register the already-emitted semantic File Code Unit identity domain

**Decision**: Add only `vulgata/story-12.2/file-code-unit-identity/2.0` to `CanonicalEnvelope`'s literal closed domain registry. Preserve the existing 2.0 producer, field order, field 13 canonical symbol validation/binding, 1.0 producer and bytes, envelope grammar, deterministic derivation, and rejection of every other unregistered domain/version.

**Rationale**: Current source already selects the 2.0 domain in `PreparedScanIdentityBuilders.CreateSemanticFileCodeUnitIdentity` and validates its fully qualified Java symbol before creating the canonical artifact. `CanonicalEnvelope` rejects that exact domain only because its otherwise closed registry contains 1.0 but omits 2.0. One literal registration restores internal contract consistency without creating a new format or decision. It aligns with AD-7's server-derived canonical identity and Constitution II/IV's exact-history and behavioral-regression requirements.

**Alternatives considered**: Reverting the producer to 1.0 would collide semantically distinct identities and discard the bound symbol; changing 1.0 would break byte compatibility; wildcard/prefix/version-range acceptance would weaken closed validation; caller-extensible registration would move domain authority outside Core; changing profiles, schemas, Scan behavior, or historical plans would exceed the approved prerequisite. All are rejected.

**Consequences**: Focused public-seam tests must prove valid 2.0 creation/validation and determinism, different valid symbols produce different bytes/identities, invalid symbols remain rejected, literal 1.0 golden bytes/identities remain unchanged, and representative unknown domains fail through creation and validation. Then rerun the two fixtures that exposed the defect, retained-source regressions, and the full suite. Passing canonical tests are controlled compatibility evidence, not generic Scan qualification.

## R1 — Canonical source comes from Git objects

**Decision**: Fetch the permitted ref into an operation-local bare repository, resolve it once, enumerate its committed tree with NUL-delimited Git plumbing, and read blobs by object ID through a binary-safe bounded process adapter.

**Rationale**: `ExactGitRevisionSnapshotService` isolates ref/commit and protects containment, but materializes through `git archive`. Export attributes can omit/substitute committed entries; checkouts can apply filters or line-ending conversion. Story 13.1 requires committed bytes and explicit unavailable capabilities.

**Alternatives considered**:

- Reuse `git archive`: rejected because export behavior can change the committed tree projection.
- Copy a checkout: rejected because it is mutable and filter/environment dependent.
- Keep a bare clone as the read interface: rejected because ordinary consumers would gain Git/process/object-path authority and maintenance could change retention.

**Consequences**: Add one bounded binary Git adapter. Record Git object format instead of hard-coding SHA-1. Gitlinks and unsafe/non-UTF-8 paths remain explicit integrity entries with limited ordinary capabilities.

**Primary technical references**: Git documents archive attribute behavior in [`git archive`](https://git-scm.com/docs/git-archive#_attributes), raw uncompressed object output in [`git cat-file`](https://git-scm.com/docs/git-cat-file#_output), and verbatim NUL-terminated path output in [`git ls-tree`](https://git-scm.com/docs/git-ls-tree#_output_format). These support the design choice; they are not runtime qualification.

## R2 — Use a flat immutable blob/manifest object

**Decision**: Store exact blob bytes under SHA-256 content keys and keep raw Git path bytes, safe logical path, mode/type/object ID, length, digest, and capabilities in a canonical versioned manifest.

**Rationale**: A flat store avoids materialized symlinks/reparse points, case collisions, reserved names, and host normalization. Ordinary lookup never combines caller input with a canonical physical root.

**Alternatives considered**:

- Extracted directory as canonical source: rejected because host filesystems cannot represent every Git tree safely/exactly.
- Every entry/blob in PostgreSQL: rejected because it couples high-volume source I/O to relational semantic storage.
- Global cross-Repository deduplication with reference counts: rejected because deletion/accounting policy is outside this story.

**Consequences**: Trusted tools need separate disposable materialization. No reference-count deletion scheme is needed because automatic canonical pruning is absent.

## R3 — PostgreSQL commit is the visibility point

**Decision**: Fully stage and verify on one filesystem, then hold a cross-process Repository/revision identity lock across existing-state recheck, atomic rename/adoption, and unique immutable completion-row insertion. Readers require the row and matching object.

**Rationale**: PostgreSQL and filesystem rename cannot share a portable transaction. Row visibility prevents partial directories and crash staging from becoming Source Snapshots. Deterministic keys, atomic rename, and unique Repository/revision resolve capture races.

**Alternatives considered**:

- Persist `Capturing` workflow rows: rejected because durable acquisition scheduling/recovery is not needed for this source-only capability.
- Insert row before files: rejected because readers could observe partial source.
- Put all source bytes in PostgreSQL: rejected by the separate durable-source volume design and bounded source-I/O needs.
- Distributed transaction/coordinator: rejected as unavailable and disproportionate.

**Consequences**: A crash between rename and insert leaves an unreferenced sealed object. A freshly authorized retry verifies/adopts it under the identity lock. Cleanup uses the same lock, rechecks that no row exists, and requires the stale grace, so it cannot race publication/adoption. Completed source is never auto-pruned.

## R4 — Authorization is source-specific and fail closed

**Decision**: Require a verifier keyed by server-derived user subject, purpose, Repository, and Profiled Revision. Web checks live Identity role plus Repository/System ownership.

**Rationale**: Current `RepositoryAuthorizationVerifier` caps access at two Repositories under one System and cannot authorize standalone source. `RepositoryAccessScope` treats a missing verifier as allowed. Existing standalone administration is available to live Administrator/SystemOwner roles, while System Repository visibility additionally uses current ownership.

**Alternatives considered**:

- Broaden the pair-era verifier: rejected because it would change historical Knowledge/Chat paths.
- Trust endpoint authorization once: rejected because revocation must affect every operation.
- Persist bearer grants: rejected because reopening requires fresh authorization, not durable access.

**Consequences**: Identity remains in Web/Data and Core receives no ASP.NET types. Role or ownership removal denies the next operation. Standalone uses current global ManagementAccess until a later accepted requirement introduces finer ownership.

## R5 — The grant is process-local and cumulative

**Decision**: Return a server-created `SourceReadContext` whose concurrent operations share atomic operation, metadata-entry, and returned-byte counters plus a monotonic deadline and cancellation lifetime.

**Rationale**: This avoids a token registry while allowing a new process to issue a fresh context from persistent source. Reserving maximum result cost before I/O prevents partial disclosure and exact-boundary races.

**Alternatives considered**:

- Stateless per-call limits: rejected because repetition resets the budget.
- Automatic renewal: rejected because it bypasses the issued bound.
- Persist every read budget: rejected because one grant need not continue across processes.
- Refund unused reservations: rejected because concurrency becomes timing-dependent and retries amplify work.

**Consequences**: The Source Snapshot is reopenable; the grant is not. Failed attempts consume an operation reservation. A new context is an explicit freshly authorized action.

## R6 — Bytes are canonical; text is a capability

**Decision**: Exact bounded bytes are primary. Strict UTF-8 text is available only for declared entries and scalar-aligned ranges. No replacement, BOM stripping, newline/Unicode normalization, or encoding guess occurs.

**Rationale**: This preserves committed bytes and makes binary, invalid UTF-8, symlink, gitlink, and unsafe-path limits explicit.

**Alternatives considered**:

- Replacement decoding or local encoding guesses: rejected because returned content would be environment dependent.
- Omit unsupported entries: rejected by Story 13.1 and AD-5.

**Consequences**: Other encodings belong to later format analysis over exact bytes, not this source owner.

## R7 — Physical workspaces stay inside Infrastructure

**Decision**: Ordinary contracts expose no physical path. An Infrastructure-internal trusted provider may materialize a verified object into an owned Tool Workspace after trusted-purpose authorization; unsafe entries fail explicitly.

**Rationale**: AD-3 permits physical leases only to trusted adapters, and the Constitution prevents Agents gaining arbitrary filesystem authority.

**Alternatives considered**:

- `RootPath` on the Core read context: rejected because it grants every consumer filesystem authority.
- Model/tool-selected paths: rejected by AD-3/6.
- Materialize on ordinary reads: rejected as unnecessary transient state.

**Consequences**: This story exposes no Agent tool. Later CodeGraph/LSP composition retains its own approval and qualification gates.

## R8 — Bounds are portable configuration

**Decision**: Add validated `SourceStorage` options for durable root, capture/read caps, operation age, deadline, and verification buffer. Startup creates one immutable internal capture policy from them. The public capture request has no limit/deadline fields or caller override; deployment changes take effect only through new composition/restart. Read-grant requests may only lower server-owned read bounds.

**Rationale**: AD-30/32 prohibit developer-machine paths/quotas becoming product eligibility while still requiring bounded work.

**Alternatives considered**:

- Reuse `CodeGraph:DataRootPath`: rejected because retained source must survive tool-cache cleanup.
- No limits: rejected as unbounded disk/memory/work.
- Filter entries above limits: rejected because truncation would alter source silently.
- Let a capture caller request lower values and clamp them: rejected because Story 13.1 has no caller-specific capture policy, and request-dependent bounds would make one Repository/revision capture outcome vary without product value. One deployment policy is simpler and deterministic.

**Consequences**: Invalid configuration prevents capture registration before Git/storage work. Exceeding a valid server cap fails the entire capture truthfully, and callers cannot raise it. Story 13.2 owns container volume wiring; later operations stories own backup/restore.

## R9 — Legacy composition remains additive

**Decision**: Register the generic source owner beside existing exact/committed snapshot services and pair registries. Do not rewrite legacy RootPath consumers.

**Rationale**: Story scope preserves pair consumers and AD-16 requires honest versioned replacement.

**Alternatives considered**:

- Replace all snapshot/lease registrations: rejected as wholesale migration and source/tool conflation.
- Adapt immediately into pair registries: rejected because pair execution/Scan Run identity does not belong to generic retained source.

**Consequences**: Old tests keep historical meaning. New production-composition tests must call the generic route without pair identity.
