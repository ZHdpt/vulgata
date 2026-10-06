# Story 18.3 direct-process authority decision — proposed, not approved

## Decision required

Independent `plan-review-08` found that the revised direct CodeGraph process plan does not meet October **O-NFR01** as written: it requires denial of arbitrary host files and writes, while the child inherits host filesystem rights. The user's direct-process instruction authorized the controlled fixture run and rejected AppContainer/Windows-specific checked-in tests. It did not expressly change O-NFR01. The recovery episode is at failed result 3 of 4 with an authority hold; no further plan repair or application-bound execution should proceed until that conflict is resolved.

The Git and no-Git fixture run did establish pinned CodeGraph 1.5.0 CLI initialization and native MCP `codegraph_explore`, with observed fixture changes confined to `.codegraph` and unchanged fixture source/config/hooks. It did not establish denial of external writes, outbound activity or app-bound authority. The CLI offered optional Git-hook installation even with `--no-watch` and even without Git; closed stdin prevented acceptance. See `codegraph-direct-fixture-01.json` and raw fixture logs under the owned `TestResults/T015-direct-20260930-01` directory.

## Options

1. **Retain preventive O-NFR01 denial.** Keep the direct fixture result as limited evidence and require a demonstrable host file/write boundary for the production child without AppContainer or Windows-specific checked-in test code. This preserves the requirement but leaves native application integration held until a feasible mechanism is defined and qualified. An ordinary direct child under the current identity does not satisfy this option.
2. **Authorize a narrow trusted-child exception.** Amend October O-NFR01 and affected architecture/Story 18.3 definition to distinguish the model-visible Worker tool boundary from the trusted pinned CodeGraph child. Proposed scope text: "Worker/agent tool interfaces deny model-selected host paths, path/symlink escapes, source writes and shell/command operations. For Story 18.3 only, the trusted Infrastructure adapter may launch pinned CodeGraph 1.5.0 directly against one application-selected dedicated prepared workspace. The child inherits host process rights; no preventive OS filesystem-write denial is claimed. Only `.codegraph` state writes are intended, and source/config/hooks must remain unchanged. Any observed out-of-footprint write or unapproved setup effect makes native readiness unavailable and requires renewed authority." This accepts residual risk that the child has the technical ability to write other host files; fixture observation and a wrapper do not remove that ability. No arbitrary real repository, credential, database, provider, general command tool or model-selected project becomes authorized.

For the user's expressed preference for a direct process, option 2 is the smallest consistent scope change. It must be an explicit product/architecture decision, not inferred from a test result. If the user does not accept that residual capability, option 1 applies. Either path retains Story 18.2's shared lease/positive binding/contained-read dependency, Story 18.3's source-reader/native-result repair, live authorization, cancellation and owned cleanup. Affected plan/definition reviews, planned-change authorization, implementation approval and downstream verification must rerun before changed product work.

## Authorities

- `docs/bmad/planning-artifacts/october-release-epics.md` O-NFR01 (containment): deny path/symlink escapes, arbitrary host files, writes and shell/command tools.
- Current Story 18.3 `spec.md` FR-010 and `plan.md` process design; `docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md` AD-3; `AGENTS.md` planned-change rule.
- `evidence/plan-review-08.raw.json` finding PR-18-3-03 and `evidence/direct-process-authority-diagnosis-01.json`.
