# Sprint Change Proposal: Epic 4 Framework Detector Lease-Only Correction

**Date:** 2026-07-31
**Trigger:** Epic 4 architecture gate finding
`epic4-framework-detector-path-entrypoint-bypass`
**Mode:** Batch
**Scope classification:** Minor direct adjustment within Story 4.3
**Execution level:** `expedited-mvp`
**User decision:** Approved the recommended narrow remediation on 2026-07-31.

## Issue

The accepted Story 4.3 plan requires `IFrameworkEvidenceDetector` to consume
the verified `ICommittedRepositorySnapshotLease` and states that path-based Git
snapshot discovery is not a production or test entry point. The concrete
`FrameworkEvidenceDetector` nevertheless retains public `string
workingCopyPath` overloads, an alternate Git snapshot source, and legacy tests
that exercise those paths directly or through reflection.

This is implementation drift from the accepted plan. It is not a new product
requirement or Scan strategy.

## Approved correction

1. Add a public-interface contract test proving that framework detection has no
   public path-based entry point.
2. Convert affected behavioral tests to acquire a verified committed Repository
   snapshot lease and call `IFrameworkEvidenceDetector`.
3. Remove the concrete detector's public string-path overloads, alternate Git
   snapshot-source construction path, and obsolete Git inventory helpers.
4. Preserve framework evidence, bounds, cancellation, failure, redaction, and
   Profiled Revision behavior.
5. Run focused and full tests, Web build, migration-neutral verification, the
   required Story 4.3 integration boundary, Docker build, one independent
   combined quality review, and a fresh Epic 4 architecture audit.

## Impact and authority

- **PRD:** unchanged.
- **Architecture and plan:** unchanged; implementation is brought into
  conformance with `specs/003-conservative-pre-scan-gate/plan.md:120-128`.
- **Database/migrations:** unchanged.
- **Worker Agent tools:** unchanged.
- **Scan strategy:** unchanged; parsing, filtering, scheduling, grouping,
  retry, and dispatch behavior are not modified.
- **Lifecycle:** Story 4.3 returns from `done` to `in-progress` until the
  correction passes review and verification. Epic 4 remains `in-progress`.

The approval authorizes this correction only. It does not waive transient
RED-GREEN TDD, the combined quality review, integration/security/migration
checks, Docker verification, or the fresh Epic 4 architecture gate.

## Completion condition

The correction is complete only when the public detector surface is
lease-only, affected tests use the committed snapshot lease, no existing
behavior regresses, and a fresh exact-source Epic 4 audit no longer reports
`epic4-framework-detector-path-entrypoint-bypass`.
