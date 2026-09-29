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
- [x] Independent client/wrapper work is separated from physical integration, which requires an identifiable verified Story 18.2 shared-workspace checkpoint and affected review
- [x] No unselected implementation structure is prescribed

## Notes

- Self-review passed on 2026-09-29. `CodeGraph`, `MCP`, and `Worker` are necessary product capability names, not a selected protocol replacement or code structure.
- Story 18.1 is integrated and verified. This correction aligns the spec to its exact approval tuple, bounded read-context lifetime, live per-disclosure authority check and three read-only Agent functions. Its public Agent seam does not expose a trusted physical workspace lease. The integration coordinator assigned Story 18.2 ownership of the single shared trusted physical-workspace contract, implementation and focused tests. Under ordinary review and approval gates, Story 18.3 may task and implement independent native client/wrapper slices before Story 18.2's checkpoint. Physical integration and Vulgata native qualification remain held until that checkpoint is verified and affected plan/tasks are independently reviewed. The standalone probe is transport evidence only.
- Applicable `docs/project-lessons.md` precautions: define the native public callable seam, ownership/disposal and cancellation explicitly in the next plan revision before TDD; qualify native filesystem behavior through the same launcher and effective permission boundary instead of treating a sandbox ancestor-open denial as a product defect. Database and hash-bound resource lessons are not applicable to this spec correction.
- `.specify/extensions.yml` has no `before_specify` hook. Its enabled `after_specify` agent-context hook is optional and falls outside this dispatch's write boundary; root must decide whether to invoke it.
