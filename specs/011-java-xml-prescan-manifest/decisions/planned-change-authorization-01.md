# Story 12.2 Planned-Change Authorization

**Decision ID:** `story-12-2-planned-change-authorization-01`
**Status:** Accepted
**Requested at:** 2026-08-20T10:20:19Z
**Decided at:** 2026-08-20T10:33:28Z
**Accepted definition scope SHA-256:** `a77c1118e1c4a33ab43094b554745fa2b1297c749a7853e63d9834e43a53e150`
**Independent plan review:** `ACCEPT`, iteration 3, `reviews/plan-review/plan-review-03.json`

## Decision requested

Authorize the root orchestrator to proceed from the independently accepted
Story 12.2 plan to `speckit-tasks`. This authorization covers only the accepted
definition scope identified above. It does not authorize production
implementation and does not waive any later gate.

## Repository-policy triggers and effects

| Trigger | Proposed effect and implementation intent | Plan sections | PRD clauses |
| --- | --- | --- | --- |
| Material Scan parsing/filtering | Add exact Java/XML enumeration, contained path/filter grammar, raw-byte hashes, safe XML/catalog parsing, and explicit whole-file skips. | Summary; AD-1, AD-2, AD-4; Failure and Replay Invariants | FR-4.1; FR-5.8; NFR-2.3 |
| Material Scan ordering/splitting | Create exact bounded File Code Units, safe JDT/XML splits, mandatory high-value seeds, lowercase UUID-text tie-breaks, the 50-150 pair cap, dual ordinal domains, and the sole post-readiness expansion fallback. | AD-3, AD-5, AD-6 | FR-4.1; FR-4.2; FR-4.9; dated NFR-1.1 scope |
| Material Scan handoff/persistence | Add versioned Prepared Scan Manifest/result/handoff data, canonical proof/manifest identities, pair-wide atomic commit, and exact replay/conflict; do not dispatch jobs. | AD-7, AD-8, AD-9; Persistence and Migration | FR-4.2-FR-4.5; FR-4.12; NFR-3.1-NFR-3.3 |
| Material analysis-tool use | Enrich closed CodeGraph completeness/related-file operations and JDT readiness/split evidence while keeping raw protocols, arbitrary queries, and Worker tools prohibited. | AD-2, AD-3, AD-6; safe-analysis contract | FR-4.9; FR-15.1-FR-15.3; NFR-2.3 |
| Dated PRD replacement | Use File Code Units rather than method units, retain relevant XML configuration, and defer Worker-embedded communication, Documents, Edges, and Uncertainties to later stories. | Summary; AD-1, AD-4, AD-5, AD-8; Requirement Traceability | FR-4.1; FR-4.3-FR-4.5; FR-4.12; FR-5.8; FR-6.1/FR-6.2 |

## Preserved architecture and boundary

- Vulgata, CodeGraph, Java 21, and JDT LS run on the Windows host.
- PostgreSQL 17 is the only required Docker service.
- CodeGraph is `@colbymchenry/codegraph@1.5.0`.
- JDT LS is `jdt-language-server-1.61.0-202608141332` over stdio through
  StreamJsonRpc `2.25.29`.
- CodeGraphContext remains excluded.
- Story 12.3 Worker execution, `navigate_code`, retry/lease/dispatch policy, and
  Document generation remain excluded.

## Options and recommendation

1. **Authorize (recommended):** proceed to dependency-ordered task generation
   against the accepted artifact hashes. Consequence: no implementation starts;
   the complete definition must still pass independent definition review and
   the separate implementation-approval gate.
2. **Defer:** keep Story 12.2 paused at plan-review with all current artifacts
   and evidence intact.
3. **Request plan revision:** state the required change. This invalidates the
   accepted scope hash and returns to the earliest affected definition owner.

## Remaining gates after authorization

Task authoring/self-review; fresh combined definition review; explicit
implementation approval; public-seam TDD RED/GREEN; convergence; independent
quality review; conditional E2E with real PostgreSQL/Git/CodeGraph/JDT-LS;
security and migration verification; pinned final verification; and root-owned
checkpoint/state/commit binding.

## Decision

The user explicitly approved this accepted scope on 2026-08-20. The root may
proceed to task generation. This decision does not authorize production
implementation and remains valid only while the accepted scope SHA-256 is
unchanged.
