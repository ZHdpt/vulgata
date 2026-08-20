# Story 12.2 Fifth Definition-Correction Authorization

- **Decision ID**: `story-12-2-definition-review-fifth-correction-authorization-01`
- **Status**: Accepted
- **Decided at**: `2026-08-20T12:33:34Z`
- **Evidence scope SHA-256**: `f42cd70d6a1e3a7a4b0e6396bbc775dba8f9e25558d25ca5dd88ee54f5e46657`
- **Evidence**: `specs/011-java-xml-prescan-manifest/reviews/definition-review/definition-review-04.json`

The user explicitly authorized one bounded fifth correction-and-review cycle for
DR4-001 through DR4-003. The authorized correction may replace the infeasible
nullable `CodeUnit` alternate-key association with an EF Core/PostgreSQL-feasible
prepared-only principal shape, reorder all required qualifications before final
verification, and refresh the quickstart provenance label. It must preserve all
previously accepted requirements, runtime pins, Story 12.3 exclusions, and the
existing planned-change authorization.

This decision does not authorize production implementation. A fresh combined
definition-review `ACCEPT` and separate implementation approval remain required.
