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
- [x] Independent client/wrapper work is separated from in-place physical integration, which requires owner-verified Story 18.2 write authority, Story 18.3's bounded positively bound reader repair and affected review
- [x] No unselected implementation structure is prescribed

## Notes

- Self-review passed on 2026-09-29. `CodeGraph`, `MCP`, and `Worker` are necessary product capability names, not a selected protocol replacement or code structure.
- The user selected in-place preparation in an explicitly dedicated Vulgata-managed prepared workspace and assigned the bounded generated-state reader/traversal/continuation repair to Story 18.3. Story 18.1's exact approval and three read-only Agent functions remain unchanged; Story 18.2 alone owns the minimal bound tool-state-write lease adjustment, and its current read lease is not a write grant. Story 18.3 owns CodeGraph index preparation, positive app-owned state binding, narrow reader correction, pre-existing-state validation, one index owner and session sharing. A foreign or unbound `.codegraph` name is not silently hidden. Completed independent client/wrapper work remains valid. Physical integration and native Vulgata qualification wait for the verified 18.2 capability, affected plan/tasks/reviews and concrete directory/process authority. The standalone probe is transport evidence only; this checklist authorizes no real-root write, Git hook, project-config edit or extra source copy.
- The user-authorized O-NFR01/AD-3 exception applies only to the trusted Infrastructure-launched pinned CodeGraph direct child in this story's dedicated workspace. It inherits host filesystem rights, so the spec does not claim preventive operating-system write denial. Observed effects outside declared `.codegraph` tool state stop native readiness; prepared source, project configuration and Git hooks remain unchanged obligations. Model-visible Worker tools keep arbitrary-path, write and command denials. Owned-fixture observations cannot prove that the child lacks out-of-footprint write capability or qualify the Vulgata application-bound route.
- Applicable `docs/project-lessons.md` precautions: define the native public callable seam, ownership/disposal and cancellation explicitly in the next plan revision before TDD; qualify native filesystem behavior through the same launcher and effective permission boundary instead of treating a sandbox ancestor-open denial as a product defect. The direct-child exception means that qualification must report observed effects without claiming an OS write barrier. Database and hash-bound resource lessons are not applicable to this spec correction.
- `.specify/extensions.yml` has no `before_specify` hook. Its enabled `after_specify` agent-context hook is optional and falls outside this dispatch's write boundary; root must decide whether to invoke it.
