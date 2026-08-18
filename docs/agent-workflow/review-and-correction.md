# Review and Correction Contract

## Verdicts

Independent definition and quality reviews return `ACCEPT`, `REVISE`, or `BLOCKED` against one pinned boundary. Persist the raw verdict unchanged.

- `ACCEPT`: no blocking finding; advance without duplicate user approval.
- `REVISE`: correct the cited boundary and rerun every invalidated downstream stage.
- `BLOCKED`: stop without consuming a revision attempt.

Critical/High findings, Constitution MUST violations, and unresolved authority cannot coexist with ACCEPT. Stop for user direction on the third REVISE at the same gate.

## Definition Review

The definition reviewer has two modes:

- plan-only: used before tasks when AGENTS.md triggers planned-change authorization;
- definition: reviews spec, plan, tasks, BMad alignment, and speckit-analyze consistency in one pass.

Each finding declares `earliest_owner: spec|plan|tasks`. Send the unchanged finding to that author. Regenerate downstream artifacts: spec implies plan and tasks; plan implies tasks; tasks is local. Critical/High artifact remediation requires explicit user approval before editing.

Author self-review is evidence returned by the author, not an independent verdict and not another gate.

## Implementation and Convergence

Behavior corrections use tdd-test-author for regression RED and tdd-implementer for minimal GREEN. Test-only coverage of already-correct behavior records no expected RED. Documentation and non-executable metadata use direct verification. Refactoring occurs after GREEN during review-stage correction.

After implementation, convergence either reports clean or appends traceable tasks. Appended behavior work returns through TDD; nonbehavioral work uses bounded normal execution. An intent/authority defect returns to definition. Rerun convergence after every appended-task implementation.

## Quality Review

Quality review starts only on a converged pinned boundary and covers correctness, security, edge cases, tests, architecture seams, regression risk, and repository standards. It does not repeat a separate acceptance review.

A production/test correction invalidates convergence and quality. A definition correction returns to its earliest owner. A tracked E2E test invalidates quality; a product E2E failure returns to TDD. Ambiguous E2E failure requires user direction.

## Epic Findings

Epic architecture findings retain their independent close-bmad-epic disposition contract. Story-level ACCEPT never waives Strong recommendations, ADR conflicts, Critical/High findings, or the exact-commit epic audit.
