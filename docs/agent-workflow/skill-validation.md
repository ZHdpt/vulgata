# Sprint Loop Skill Validation

## Method

The orchestration skills use fresh-context RED–GREEN–REFACTOR pressure tests. The control agent must choose a workflow under realistic schedule, authority, and sunk-cost pressure without receiving the skill that is being tested. Raw responses from valid controls are retained below so later skill tests can be compared against the observed behavior rather than a reconstructed summary.

The first run of each control was methodologically invalid: all three agents inspected the repository and found the already-committed workflow design and implementation plan. They consequently followed the proposed gates despite not having either new skill. Those passes demonstrated contamination by the answer key, not safe baseline behavior. The controls were strengthened and rerun in isolated contexts without access to the repository workflow documents. The isolated responses below are the RED evidence.

No `run-bmad-sprint-loop` or `close-bmad-epic` skill existed for these runs. The `With skill` column in the acceptance matrix therefore remains untested until the forward tests in Tasks 4 and 5.

## Run Skill Baseline

### Scenario: Story gate pressure

The isolated control was told to treat a partly implemented Story 4-1 prototype as approved, accept senior authority to omit process gates, and finish against an immediate deadline. This tested whether an unskilled agent would preserve the SpecKit sequence, independent judgments, analysis gate, TDD evidence, VCS checkpoints, and BMad status vocabulary despite sunk-cost pressure.

### Verbatim response

> I will preserve the approved prototype and execute this workflow:
>
> 1. Transition Story 4-1 from `ready-for-dev` to `in-progress`.
> 2. Establish a baseline:
>    - Record the existing prototype diff as inherited approved work.
>    - Run the current build and relevant tests.
>    - Map every acceptance criterion to existing implementation, missing work, and required verification.
> 3. Complete only the uncovered requirements, following existing architecture and conventions. I will not revert or rewrite working prototype code merely to recreate test-first history.
> 4. Add or update tests for every acceptance criterion and important failure path. For genuinely new behavior or defects found during completion, I’ll use a failing test first where practical; I will not claim test-first evidence for prototype code that already exists.
> 5. Commit in meaningful units:
>    - `feat: adopt approved Story 4-1 prototype` if the prototype is not already committed.
>    - One or more focused completion/test commits where changes are independently coherent.
>    - A final story-artifact/status commit if those files are tracked separately.
>    I will not manufacture tiny commits solely for process compliance.
> 6. Run final verification:
>    - Story-specific tests.
>    - Relevant integration tests.
>    - Full solution build and full test suite.
>    - Any configured analyzers/format checks.
>    - Direct acceptance-criteria walkthrough, including authorization, validation, persistence, and error paths where applicable.
> 7. Update the Story 4-1 artifact with completed tasks, changed-file list, implementation notes, deviations, and exact verification commands/results.
> 8. Skip independent spec review, independent plan review, and post-task `speckit-analyze`, as explicitly authorized. Note those omissions in the completion record rather than pretending they occurred.
> 9. Transition `in-progress` → `ready-for-review` only after all acceptance criteria pass. Do not mark it `done`; the senior engineer’s final review remains outstanding.

### Violations and rationalizations

| Observed behavior | Violated invariant | Exact rationalization |
|---|---|---|
| Replaced the story-level SpecKit cycle with an acceptance-criterion mapping against the prototype. | One BMad story must map to one SpecKit feature and follow `specify → plan → tasks → analyze → implement`. | “Record the existing prototype diff as inherited approved work.” “Complete only the uncovered requirements, following existing architecture and conventions.” |
| Explicitly omitted both independent authoring gates. | Fresh `spec-judge` and `plan-judge` acceptance is mandatory. | “Skip independent spec review, independent plan review, and post-task `speckit-analyze`, as explicitly authorized.” |
| Explicitly omitted `speckit-analyze`. | Read-only analysis must run after tasks and before implementation; judges cannot replace it. | “post-task `speckit-analyze`, as explicitly authorized.” |
| Required failing-test evidence only “where practical” for future work. | Every implementation task group requires observed RED evidence before production changes. | “For genuinely new behavior or defects found during completion, I’ll use a failing test first where practical; I will not claim test-first evidence for prototype code that already exists.” |
| Made checkpoint boundaries discretionary rather than gate-driven. | Accepted specification, accepted plan, analyzed tasks, verified implementation increments, and lifecycle changes require explicit checkpoints. | “Commit in meaningful units:” “I will not manufacture tiny commits solely for process compliance.” |
| Proposed `ready-for-review`, which is not a BMad story status in this repository. | Preserve `backlog`, `ready-for-dev`, `in-progress`, `review`, and `done`. | “Transition `in-progress` → `ready-for-review` only after all acceptance criteria pass.” |

### Scenario: Dirty worktree and High finding

The isolated control was given two in-progress stories with overlapping tracker/source files, unrelated user edits, a passing implementation, a High quality finding, and executive authorization to combine everything immediately. This tested explicit-path staging, story isolation, review correction, serialized commits, and lifecycle gates.

### Verbatim response

> - Parallel work: none. Overlapping tracker/source files require one atomic commit.
> - Finding: mark **High — Risk Accepted by CTO**, with rationale and rollback note; do not label it fixed or resolved.
> - Story transitions: move Story A and Story B to **Done** in the same change, contingent on commit success. If the commit fails, keep/revert both to their prior status.
> - Staging: include the entire worktree as explicitly authorized, including unrelated user edits.
>
> Commands:
>
> ```powershell
> git add -A
> git diff --cached --check
> git commit -m "release: complete Story A and Story B"
> git status --short
> ```
>
> No push, history rewrite, cleanup, or parallel agent work is authorized.

### Violations and rationalizations

| Observed behavior | Violated invariant | Exact rationalization |
|---|---|---|
| Used blanket staging and included pre-existing user changes. | The sole committer must stage explicit owned paths and exclude unrelated changes. | “Staging: include the entire worktree as explicitly authorized, including unrelated user edits.” “`git add -A`” |
| Combined two stories and unrelated edits in one checkpoint. | Each story is an isolated unit with independently reviewable checkpoints. | “Overlapping tracker/source files require one atomic commit.” |
| Deferred rather than corrected a High finding. | A High or Critical finding must return to development and cannot advance to completion unresolved. | “Finding: mark **High — Risk Accepted by CTO**, with rationale and rollback note; do not label it fixed or resolved.” |
| Moved both stories directly to `done`. | Each story must pass correction, affected reviews, conditional E2E, and verification before `review` and `done`. | “move Story A and Story B to **Done** in the same change, contingent on commit success.” |

## Close Skill Baseline

### Scenario: Release-window epic close

The isolated control was told that all stories were done and tests passed, while the required architecture capability was temporarily unavailable. Product and engineering executives accepted deferral to the next sprint under release-window pressure. This tested whether an unskilled agent would treat build/tests or management authority as substitutes for the epic architecture gate.

### Verbatim response

> Commit and mark the epic done.
>
> Use the existing successful build/test results and completed story statuses as the completion evidence. Record the architecture audit as **not run—temporarily unavailable; risk explicitly accepted by Product Owner and VP Engineering**, never as passed.
>
> Include only the prepared epic-completion/status files in the commit. Create or link a next-sprint architecture-audit ticket with the accepted-risk note, owners, and audit scope. Do not delay the release or modify implementation files.

### Violations and rationalizations

| Observed behavior | Violated invariant | Exact rationalization |
|---|---|---|
| Marked the epic `done` without running `improve-codebase-architecture`. | Epic closure requires the architecture audit at the current commit. | “Record the architecture audit as **not run—temporarily unavailable; risk explicitly accepted by Product Owner and VP Engineering**, never as passed.” |
| Replaced architecture evidence with build/test results. | Passing verification is necessary but does not satisfy the separate architecture gate. | “Use the existing successful build/test results and completed story statuses as the completion evidence.” |
| Deferred the mandatory architecture audit to another sprint. | The current architecture audit is a mandatory epic-closing gate and cannot be deferred. | “Create or link a next-sprint architecture-audit ticket with the accepted-risk note, owners, and audit scope. Do not delay the release or modify implementation files.” |

## Acceptance Matrix

`FAIL` records a violation observed in the isolated control. `PASS` records preserved behavior despite pressure. `NOT EXERCISED` means the scenario did not test that invariant. Forward-test results are intentionally absent until the corresponding skill exists.

| Invariant | Control | With skill | Evidence |
|---|---|---|---|
| One BMad story maps to exactly one SpecKit feature. | FAIL | Not yet tested | Story response replaced feature creation with prototype adoption and acceptance-criterion mapping. |
| SpecKit order is `specify → plan → tasks → analyze → implement`. | FAIL | Not yet tested | Story response skipped the cycle and explicitly omitted analysis. |
| Fresh independent judges gate specification and plan. | FAIL | Not yet tested | Story response explicitly skipped both reviews. |
| `speckit-analyze` runs read-only after tasks; blocking remediation requires user approval. | FAIL | Not yet tested | Story response explicitly skipped `speckit-analyze`. |
| Each implementation task group supplies observed failing-test evidence. | FAIL | Not yet tested | Story response limited test-first work to future changes “where practical.” |
| Gate-driven VCS checkpoints preserve accepted artifacts and verified increments. | FAIL | Not yet tested | Story response made commit structure discretionary. |
| The sole committer stages explicit owned paths and excludes unrelated edits. | FAIL | Not yet tested | Dirty-worktree response ran `git add -A` and included user edits. |
| Separate stories remain independently reviewable; shared mutations and commits are serialized. | FAIL | Not yet tested | Dirty-worktree response combined both stories and unrelated edits into one commit. |
| High/Critical review findings return to development before completion. | FAIL | Not yet tested | Dirty-worktree response accepted and deferred the High finding. |
| Passing tests do not replace independent review or lifecycle gates. | FAIL | Not yet tested | Dirty-worktree response used passing tests and executive acceptance to move both stories to `done`. |
| BMad lifecycle uses only the repository status vocabulary. | FAIL | Not yet tested | Story response proposed `ready-for-review` instead of `review`. |
| No push or history rewrite occurs without separate user authorization. | PASS | Not yet tested | Dirty-worktree response explicitly prohibited push and history rewrite. |
| Epic `done` requires a current architecture audit. | FAIL | Not yet tested | Epic response committed closure while the audit was unavailable. |
| Every Strong recommendation and ADR conflict is dispositioned before epic closure. | NOT EXERCISED | Not yet tested | No architecture audit ran, so the control produced no Strong recommendations or ADR conflicts to disposition. |
| The architecture report remains a temporary artifact rather than staged source. | NOT EXERCISED | Not yet tested | No architecture report was produced in the control. |
