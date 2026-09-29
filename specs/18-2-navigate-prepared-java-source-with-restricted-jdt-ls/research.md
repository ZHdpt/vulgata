# Story 18.2 Research and Decision Record

This is repository-grounded planning, not live JDT-LS qualification. Authority: `spec.md`, October proposal prepared-input/whole-root/F0 sections, October epics O-FR06/O-AR09/O-NFR01, PRD FR-4.9/15.1–15.3, architecture AD-15, Constitution I/IV/V and current source.

## Prepared source and session binding

**Decision**: Preserve integrated 18.1's `PreparedSourceContextIssuer` and `IPreparedSourceReadContext` exactly as Worker list/read/search. Story 18.2 owns a separate small trusted physical-workspace issuer/lease for JDT-LS and Story 18.3, bound to the same exact `PreparedSourceApproval` tuple plus explicit application-selected tool purpose. JDT-LS receives the selected source project root through Infrastructure only; disposable process data/configuration live elsewhere. No source copy, Git label substitution or second source registry.

**Rationale**: Story 18.1's public context has only `ListAsync`, `ReadTextRangeAsync` and `SearchTextAsync`; physical root and handle are private to Infrastructure. The accepted verifier checks batch, Repository, subject, `WorkerSourceRead`, approval/root-binding identities and operator label at issuance/use/disclosure. `WorkerSourceRead` alone does not authorize a JDT-LS or CodeGraph subprocess, so a separate current tool-purpose authorization is required. The October proposal excludes managed copy/snapshot and arbitrary roots. `JdtLsAnalysisAdapter.cs` currently offers a prepared-revision lease that verifies Git-style identity and calls `CopySnapshotAsync`; it is not the October contract.

**Alternatives considered**: Reuse the revision lease/mirror, lend the 18.1 read context's private root, or let Worker supply a workspace. All conflict with source origin, 18.1's closed interface or containment. A path lease alone does not constrain subprocess reads/writes. If the host cannot enforce a read-only source and least-privilege process profile, report the failure for architecture correction rather than quietly copying source.

## Restricted profile and result truth

**Decision**: Select the executable from application config files and fix no-import/no-build/no-restore settings at the adapter. Only real JDT-LS tests may load `JDT_LS_BIN` from their local `.env`; no ordinary `.env` discovery. Qualify actual definition and reference calls, source no-write and least-privilege process behavior. Preserve zero results, missing classpath and failures as distinct observations.

**Rationale**: O-FR06/O-AR09 and PRD FR-15.1–15.2 require semantic navigation evidence and explicit limits. The current protocol initializer supplies `rootUri` and `workspaceFolders`, but inspection found no evidenced disabling settings. `ParseEvidence` currently treats a null response as succeeded with no evidence. Neither proves the October profile or absence semantics.

**Alternatives considered**: Count startup/readiness as success, assume current settings are restricted, or enable dependency restoration. Each exceeds or underproves accepted scope.

## Concurrency, lifetime and failures

**Decision**: One owned session per selected Repository, bounded issuance/queue/readiness/request/authority check/shutdown, serialized shared protocol operations until overlap is proven safe. Check exact current source approval and selected tool purpose at issuance, before and during use, and immediately before disclosure. Authority loss latches the lease closed, cancels owned activity and discards buffered results from every concurrent request. After timeout/protocol failure, settle the owned session before a new attempt. No unrelated process termination.

**Rationale**: O-FR06 and Story 18.2 acceptance require one-session ownership, serialization and bounded cleanup. The current adapter already has timeout and owned process/lease disposal mechanisms, but they are tied to the old scope and require direct-root assessment.

**Alternatives considered**: Per-Worker processes and blind automatic replay increase contention and hide uncertain outcomes.

## F0 integration dependency

**Decision**: Bind the shared trusted-workspace seam to integrated 18.1 `PreparedSourceApproval`, `IPreparedSourceCurrentAuthorityVerifier` and `PreparedSourceLocation` semantics without altering 18.1's issuer/context/functions or their failure/continuation semantics. Story 18.2 owns its small JDT-independent Infrastructure issuer/lease and focused tests; Story 18.3 consumes the identifiable verified checkpoint only after separate authorization. Navigation-specific context/functions and JDT-LS implementation remain 18.2-owned.

**Rationale**: Story 18.1 is integrated and verified; its read context cannot provide a physical-root lease. The Parallel Development coordination decision assigns the shared trusted workspace to 18.2 so 18.3 can use it without JDT dependencies. This is ownership, not plan/implementation approval. The current project lesson requires a callable public construction/method/result/disposal/cancellation shape before behavioral TDD; `contracts/navigation-tool.md` supplies one.

**Alternatives considered**: Duplicating 18.1's root/identity model or extending its Worker context with a physical path would create competing authority or broaden the closed Agent interface. Letting 18.3 independently create a root lease would duplicate the shared seam.

## Qualification sample

**Decision**: Use an owned representative fixture first, then the user-prepared Repository after it is supplied and access is authorized. Record the actual source/profile/results separately.

**Rationale**: This is the user's stated planning preference. Fixture success reduces early protocol risk but does not prove useful navigation on the later source.

**Alternatives considered**: Waiting for the real Repository before any qualification delays early risk discovery; using fixture results as acceptance for the real Repository would overclaim evidence.

## Applicable project lesson

The public-seam lesson in `docs/project-lessons.md` applies: new callable boundaries need explicit constructor, call, result, ownership, cancellation and permitted arguments before behavioral TDD. The native filesystem lesson applies when qualification uses Windows handle-relative traversal under a sandboxed launcher: compare effective permission modes and native status before calling a root-open failure a product defect. Neither lesson waives the required real JDT-LS and containment evidence.
