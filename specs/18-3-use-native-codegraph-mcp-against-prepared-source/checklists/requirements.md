# Specification Quality Checklist: Use Native CodeGraph MCP Against Prepared Source

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-29
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details beyond the named native CodeGraph MCP capability and authority constraints
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders familiar with the named product capability
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No clarification markers remain
- [x] Requirements are testable and unambiguous at the story boundary
- [x] Success criteria are measurable
- [x] Success criteria describe observable outcomes rather than a selected code structure
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance scenarios or success criteria
- [x] User scenarios cover primary, failure, and qualification flows
- [x] Independent client/wrapper work is separated from physical integration, which requires both a verified Story 18.2 shared-root/lease checkpoint and an approved Story 18.3 CodeGraph index-placement decision
- [x] No unselected implementation structure is prescribed

## Notes

- Self-review passed on 2026-09-29. `CodeGraph`, `MCP`, and `Worker` are necessary product capability names, not a selected protocol replacement or code structure.
- Story 18.1 is integrated and verified; its exact approval tuple, live per-disclosure check and three read-only Agent functions remain unchanged. Story 18.2 owns the shared prepared-source read/root authority and lease lifetime; Story 18.3 owns CodeGraph-specific writable index placement. Pinned CodeGraph 1.5.0 places its index within the project and exposes no external index path, invalidating the prior placement assumption. Independent native client/wrapper work and evidence remain valid. Physical integration and Vulgata native qualification are held until the user resolves the architecture/authority choice from an independently reviewed plan, Story 18.2 supplies an identifiable verified checkpoint, and affected plan/tasks/review gates reconcile both. Neither the standalone probe nor this checklist authorizes a live-root write or source copy.
- Applicable `docs/project-lessons.md` precautions: define the native public callable seam, ownership/disposal and cancellation explicitly in the next plan revision before TDD; qualify native filesystem behavior through the same launcher and effective permission boundary instead of treating a sandbox ancestor-open denial as a product defect. Database and hash-bound resource lessons are not applicable to this spec correction.
- `.specify/extensions.yml` has no `before_specify` hook. Its enabled `after_specify` agent-context hook is optional and falls outside this dispatch's write boundary; root must decide whether to invoke it.
