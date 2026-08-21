# Story 12.2 Fourth-Convergence Revision Authorization

- **Decision ID**: `story-12-2-convergence-fourth-revision-authorization-01`
- **Status**: Accepted
- **Decided at**: `2026-08-21T01:33:35Z`
- **Evidence scope SHA-256**: `843915314cc81eafcc0bf49832ff5fd8081fea0022844eb28770872a073a04f0`
- **Evidence**: `specs/011-java-xml-prescan-manifest/reviews/convergence/convergence-04.md`

The user explicitly authorized T058 and T059 after convergence iteration 4
identified two remaining HIGH partial implementation gaps: the production
paired path does not yet bind the acquired exact revision to a fresh real
CodeGraph index/rebuild/status proof, and the deterministic JDT-LS readiness
candidate set does not yet exclude empty eligible Java files.

The authorization permits the bounded TDD corrections described by T058 and
T059: compose the pinned Windows-host CodeGraph 1.5.0 runtime through the exact
snapshot lease and hard completeness gate, and select the ordinal-first
nonempty bounded Java readiness file while preserving XML-only `NotApplicable`.
It does not authorize Story 12.3 Worker execution, Document generation,
CodeGraphContext, a new analysis tool, or any change to the approved host-runtime
architecture.
