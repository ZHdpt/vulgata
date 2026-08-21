# Story 12.2 Third-Convergence Revision Authorization

- **Decision ID**: `story-12-2-convergence-third-revision-authorization-01`
- **Status**: Accepted
- **Decided at**: `2026-08-20T23:36:41Z`
- **Evidence scope SHA-256**: `f8d4ab6d51d8bb7935c56660bcbbb124b6a7ba50bc938cd6532f012ab1c01941`
- **Evidence**: `specs/011-java-xml-prescan-manifest/reviews/convergence/convergence-03.md`

The user explicitly authorized T057 after convergence iteration 3 identified
F5: one oversized, binary, invalidly encoded, or unreadable eligible artifact
can currently abort the whole exact snapshot inventory instead of producing one
closed per-file outcome and allowing safe files to continue.

The authorization permits the bounded TDD correction described by T057:
preserve one deterministic `Skipped`/bounded `Warning` inventory record with a
closed `PreparedScanAuditReason`, retain complete candidate/outcome totals,
continue safe files, and prove the rejected artifact cannot become a File Code
Unit. It does not authorize Story 12.3 Worker execution, Document generation,
new analysis tools, or any change to the approved host-runtime architecture.
