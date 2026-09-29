# Story 18.2 Research and Decision Record

This is repository-grounded planning, not live JDT-LS qualification. Authority: `spec.md`, October proposal prepared-input/whole-root/F0 sections, October epics O-FR06/O-AR09/O-NFR01, PRD FR-4.9/15.1–15.3, architecture AD-15, Constitution I/IV/V and current source.

## Prepared source and session binding

**Decision**: Consume the 18.1/F0 application grant for one whole prepared Repository. JDT-LS receives a bound source project root; disposable process data/configuration live elsewhere. No source copy, Git label substitution or second grant registry.

**Rationale**: The October proposal explicitly excludes managed copy/snapshot and arbitrary root selection. `JdtLsAnalysisAdapter.cs` currently offers a prepared-revision lease that verifies a Git-style revision and calls `CopySnapshotAsync`; that is not this contract. Its file lease likewise derives a directory from `ProfiledRevision`.

**Alternatives considered**: Reuse the revision lease/mirror, or let Worker supply a workspace. Both conflict with source origin and containment. If direct-root JDT-LS cannot obey no-write behavior, report the failure for architecture correction; do not quietly introduce a copy.

## Restricted profile and result truth

**Decision**: Fix no-import/no-build/no-restore configuration at the application adapter and qualify it using real definition and reference calls. Preserve zero results, missing classpath and failures as distinct observations.

**Rationale**: O-FR06/O-AR09 and PRD FR-15.1–15.2 require semantic navigation evidence and explicit limits. The current protocol initializer supplies `rootUri` and `workspaceFolders`, but inspection found no evidenced disabling settings. `ParseEvidence` currently treats a null response as succeeded with no evidence. Neither proves the October profile or absence semantics.

**Alternatives considered**: Count startup/readiness as success, assume current settings are restricted, or enable dependency restoration. Each exceeds or underproves accepted scope.

## Concurrency, lifetime and failures

**Decision**: One owned session per selected Repository, bounded queue/readiness/request/shutdown, serialized shared protocol operations until overlap is proven safe. After timeout/protocol failure, settle the owned session before a new attempt. No unrelated process termination.

**Rationale**: O-FR06 and Story 18.2 acceptance require one-session ownership, serialization and bounded cleanup. The current adapter already has timeout and owned process/lease disposal mechanisms, but they are tied to the old scope and require direct-root assessment.

**Alternatives considered**: Per-Worker processes and blind automatic replay increase contention and hide uncertain outcomes.

## F0 integration dependency

**Decision**: Define only navigation-specific semantics now. Use F0 concepts rather than inventing concrete 18.1 types. Later reconcile the integrated grant, authorization, relative path/location and file-tool failure/continuation semantics, then rerun affected definition and review gates.

**Rationale**: Story 18.1 is an active parallel implementation and is 18.2's completion dependency (October epics). The proposal allows the source/tool contract to release before all tools, but does not make an unintegrated implementation authoritative.

**Alternatives considered**: Duplicating a root/identity model in 18.2 would create competing authorities and force parallel work to reconcile after code changes.

## Qualification sample

**Decision**: Use an owned representative fixture first, then the user-prepared Repository after it is supplied and access is authorized. Record the actual source/profile/results separately.

**Rationale**: This is the user's stated planning preference. Fixture success reduces early protocol risk but does not prove useful navigation on the later source.

**Alternatives considered**: Waiting for the real Repository before any qualification delays early risk discovery; using fixture results as acceptance for the real Repository would overclaim evidence.
