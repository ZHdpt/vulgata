# Review and Correction Contract

## Verdicts

Every independent gate returns exactly one verdict:

| Verdict | Meaning | Orchestrator action |
|---|---|---|
| `ACCEPT` | No Critical/High finding, no violated Constitution MUST, and the gate's done conditions are evidenced | Persist verdict and advance |
| `REVISE` | The responsible author or implementer can correct one or more findings inside the approved scope | Return raw findings to a fresh responsible agent, verify correction, and rerun affected gates |
| `BLOCKED` | Missing external authority/information, unsafe action, unavailable required infrastructure, or irreconcilable source conflict prevents safe correction | Persist evidence, do not advance, and ask the user for direction |

Passing tests cannot convert a review into `ACCEPT`. In Full, a judge cannot replace `speckit-analyze`, and one review axis cannot replace another. Standard intentionally omits analysis and the separate acceptance axis only after recorded eligibility; its one quality verdict uses the documented combined acceptance-and-quality contract.

`ACCEPT` is also the stage's approval: after validating and persisting it, the orchestrator does not ask the user to approve that same specification, plan, acceptance review, or quality review again. This removes duplicate confirmation, not independent judgment. Human direction is still required for blocking analysis remediation; unresolved intent/scope or three failed revision outcomes; Strong architecture recommendations or ADR conflicts; divergent/destructive VCS recovery; inseparable ownership; unavailable required infrastructure; unsafe external actions; and ambiguous E2E failures.

## Severities

| Severity | Definition | Gate effect |
|---|---|---|
| `CRITICAL` | Safety, security, data-loss, authority, or fundamental correctness failure with severe impact | Blocking; correction and affected reruns required |
| `HIGH` | Required behavior, architecture invariant, authorization, major regression, or testability failure likely to make the result unsafe or materially incomplete | Blocking; correction and affected reruns required |
| `MEDIUM` | Real nonblocking defect or maintainability/test gap with bounded impact | Record and disposition; may advance only when the gate's contract permits and no MUST is violated |
| `LOW` | Minor clarity, consistency, or polish issue | Record; may advance with rationale |

Severity is evidence-based and never downgraded to escape a loop. Every finding includes a stable ID, category, artifact/location, affected rule, concise evidence, and concrete correction outcome.

## Gate-Specific Review

- `spec-review`: scope, completeness, ambiguity, testability, BMad acceptance alignment, and preservation of technology-agnostic what/why.
- `plan-review`: feasibility, architectural fit, Constitution/ADR compliance, contracts/data model, failure behavior, and test strategy.
- `analyze`: read-only consistency and coverage across `spec.md`, `plan.md`, and `tasks.md` immediately after task generation.
- `acceptance-review`: missing, partial, incorrect, and out-of-scope behavior against BMad and SpecKit.
- `quality-review`: standards, correctness, security, edge cases, test quality, architecture seams, and regression risk.
- Standard combined `quality-review`: all ordinary quality concerns plus missing, partial, incorrect, and out-of-scope behavior against the BMad story and concise SpecKit artifacts.
- Epic architecture review: Strong recommendations and ADR conflicts at the current commit.

## Raw-Finding Handoff

The orchestrator persists the complete verdict and passes each raw finding unchanged to the fresh correcting agent, together with its pinned artifact/commit evidence. It may add routing metadata but cannot summarize away severity, evidence, affected rule, or correction outcome.

Authors never receive a judge's private reasoning transcript. Judges receive raw artifacts rather than author rationales. The exception is a correction dispatch, where the raw finding is necessary input and the agent's bounded objective is to correct it.

## Correction Loops

1. Persist the rejected verdict and increment that gate's iteration count.
2. Route specification findings to a fresh `spec-author`, planning findings to a fresh `plan-architect`, task findings to a fresh `task-author`, and implementation findings to a fresh `tdd-implementer`. If correction would choose or expand product intent/scope rather than restore the approved contract, stop for explicit user direction first.
3. When behavior changes, require a failing regression test before production correction, then targeted green and surrounding verification.
4. Regenerate every dependent downstream artifact invalidated by an upstream correction.
5. Rerun the same gate with a fresh judge/reviewer. Full reruns both implementation review axes when the diff boundary changes. Standard reruns its combined `quality-review`.
6. Do not stage or commit rejected/failing work. After a blocking implementation finding passes fresh review, create a focused follow-up checkpoint rather than silently squashing it.

After three `REVISE` outcomes at the same gate, the issue is unresolved and the workflow stops for user direction. The count is per gate and includes repeated findings or new blocking findings caused by correction. `analyze` uses the same rule: its third `REVISE` outcome escalates. `BLOCKED` stops immediately, records its cause, and does not count as a revision attempt. The orchestrator does not attempt a fourth correction, lower severity, or mark the stage complete.

## Mandatory Approval for Analysis Remediation

`speckit-analyze` is non-destructive and read-only. If its report has any Critical or High finding:

1. Persist the untouched report and coverage metrics.
2. Present findings and the proposed artifact-specific remediation to the user.
3. Obtain explicit user approval before editing `spec.md`, plan artifacts, or `tasks.md`.
4. If approval is withheld, record `BLOCKED` and remain at `analyze`.
5. If approved, dispatch the responsible fresh author, regenerate downstream artifacts, and rerun `speckit-analyze`.
6. Do not dispatch implementation until the latest report has no Critical/High finding.

Approval is scoped to the presented remediation. It does not authorize product-scope expansion, ADR changes, source-code implementation, or unrelated cleanup.

## E2E Failure Routing

- A product failure returns raw evidence to a fresh `tdd-implementer`; changed production code reruns affected reviews and the E2E gate.
- A defective E2E test returns to `e2e-runner`, which may edit tests only; the E2E gate reruns.
- An ambiguous failure is `BLOCKED` until classified without changing production code speculatively.

Every tracked test edit changes the reviewed diff identity. Whether the runner adds coverage or repairs a defective test, Full reruns both acceptance and quality reviews; Standard reruns its combined quality review before the E2E result can be accepted. “Test-only” is not a review bypass.

## Epic Disposition

The root runs the architecture skill's documented epic-close audit profile. Its read-only independent Explore/classification judge returns the verdict, severity, strength, applicable categories, and verifiable citations; the root preserves the raw response and cannot weaken classifications. Multi-category findings require a subdecision satisfying every category, with Critical/High/MUST precedence. Critical/High and MUST require corrective work plus a fresh clearing audit or an evidence-backed user-approved false-positive. Nonsevere Strong permits load-bearing rejection, external tracked deferral, or known-debt acceptance. ADR conflicts may conform, use an already committed superseding decision plus fresh audit, or block.

The close skill does not directly remediate any tracked source or authority. Production, tests, documentation, and ADR changes go through BMad correct-course plus `$run-bmad-sprint-loop` or an explicit ADR decision workflow; the epic remains `in-progress`, and closure restarts at Step 1 on the new `HEAD`. Only an exact-source audit with verified citations and field-equal raw/embedded/visible/durable findings may advance. A schema-valid independent `ACCEPT` with no reserved finding advances without duplicate approval.
