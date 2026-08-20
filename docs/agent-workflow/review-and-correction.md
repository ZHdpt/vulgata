# Review and Correction Contract

## Verdicts

Independent definition and quality reviews return `ACCEPT`, `REVISE`, or `BLOCKED` against one pinned boundary. Persist the raw verdict unchanged.

- `ACCEPT`: no blocking finding; advance without duplicate user approval.
- `REVISE`: correct the cited boundary and rerun every invalidated downstream stage without routine user approval.
- `BLOCKED`: stop without consuming a revision attempt.

Critical/High findings, Constitution MUST violations, and unresolved authority cannot coexist with ACCEPT. The first three consecutive REVISE outcomes at one phase route automatically to correction; pause on the fourth.

Increment only the affected phase's consecutive counter. Reset it after `ACCEPT`/`converged` or when an upstream correction replaces that phase's input boundary. Gate `attempt` remains the latest verdict sequence; the top-level `attempts` counters exist only to detect a repeated current-boundary loop.

## Definition Review

The definition reviewer has two modes:

- plan-only: used before tasks when AGENTS.md triggers planned-change authorization;
- definition: reviews spec, plan, tasks, BMad alignment, and speckit-analyze consistency in one pass.

Each finding declares `earliest_owner: spec|plan|tasks`. Send the unchanged finding to that author. Regenerate downstream artifacts: spec implies plan and tasks; plan implies tasks; tasks is local. Severity determines blocking status, not whether safe correction needs user ceremony.

Author self-review is evidence returned by the author, not an independent verdict and not another gate.

## User Decisions

Pause only for a repository-mandated planned-change gate, the accepted-definition implementation approval, contradiction with an earlier user decision or higher authority, a new architecture decision outside the accepted plan, the fourth consecutive REVISE/non-converged result in one phase, a hard blocker, or another unresolved high-impact choice the root can state precisely.

Every request contains:

1. topic and exact decision requested;
2. brief, current stage, and why user judgment is required;
3. related repository-relative files and the controlling prior/higher-level material;
4. viable options, the root's recommendation, and consequences;
5. design concepts or architecture impact when material;
6. exact authorized scope and scope hash;
7. what remains unchanged and which downstream gates still apply.

Persist the decision once in the development record or a dedicated decision artifact and store only its ID, scope hash, status, time, and evidence path in workflow state. Reuse an accepted decision while its scope hash is unchanged. Implementation approval covers internal seams and architecture choices already disclosed in the accepted definition; it is not repeated for each implementation detail.

## Implementation and Convergence

Behavior corrections use tdd-test-author for regression RED and tdd-implementer for minimal GREEN. Test-only coverage of already-correct behavior records no expected RED. Documentation and non-executable metadata use direct verification. Refactoring occurs after GREEN during review-stage correction.

After implementation, convergence either reports clean or appends traceable tasks. Appended behavior work returns through TDD; nonbehavioral work uses bounded normal execution. An intent/authority defect returns to definition. Rerun convergence after every appended-task implementation.

## Quality Review

Quality review starts only on a converged pinned boundary and covers correctness, security, edge cases, tests, architecture seams, regression risk, and repository standards. It does not repeat a separate acceptance review.

A production/test correction invalidates convergence and quality. A definition correction returns to its earliest owner. A tracked E2E test invalidates quality; a product E2E failure returns to TDD. Ambiguous E2E failure requires user direction.

## Epic Findings

Epic architecture findings retain their independent close-bmad-epic disposition contract. Story-level ACCEPT never waives Strong recommendations, ADR conflicts, Critical/High findings, or the exact-commit epic audit.
