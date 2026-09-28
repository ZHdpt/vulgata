# Review and Correction Contract

## Verdicts

Independent definition and quality reviews return `ACCEPT`, `REVISE`, or `BLOCKED` against one pinned boundary. Persist the raw verdict unchanged.

- `ACCEPT`: no blocking finding; advance without duplicate user approval.
- `REVISE`: correct the cited boundary and rerun every invalidated downstream stage without routine user approval.
- `BLOCKED`: stop without consuming a revision attempt.

Critical/High findings, Constitution MUST violations, and unresolved authority cannot coexist with ACCEPT. Gate `attempt` is a raw verdict sequence, never the stopping budget. New state 3.1 uses one unresolved recovery episode across phases and changed inputs; historical 3.0 per-phase counters are not used for new dispatch.

## Recovery and Diagnosis

Route each required finding to its earliest invalid owner immediately: inadequate intent to spec, unsound design to plan, coverage/order defects with sound design to tasks, and code/test defects with sound definition to the appropriate implementation/test owner. Authority contradictions and hard blockers use the existing human gate. Multiple findings choose the earliest owner; optional polish and out-of-scope discoveries do not expand the story. If cause is uncertain, diagnose from existing evidence before inventing another task.

| Result | `failed_results` | Before the next repair |
|---|---:|---|
| Initial substantive negative result | 1 | Route to owner |
| First failed repair | 2 | Require a defensible repair hypothesis |
| Second failed repair | 3 | Record diagnosis |
| Third failed repair | 4 | Request user direction |

Count one result per correction and authoritative check, irrespective of number of findings. Duplicate delivery, re-reading a report, unchanged rechecks, expected TDD RED, technical exceptions, BLOCKED, invalidation, and intermediate ACCEPT neither increment nor reset it. Never reset for new task IDs, artifact hashes, phase changes, or author replacement. Newly found required defects join the open episode. Do not rerun an unchanged negative check without correction or qualifying new evidence.

Diagnosis is a short root-owned entry in the development record/analysis evidence: failed obligation and source, affected assumption/owner, what the next repair changes, and how it will be verified. Cite failed approaches; repeating one needs new evidence. No standing diagnostic worker or extra review is required. Diagnosis can choose local correction; it does not automatically mean replanning or Astra. See `agent-catalog.md` for evidence-backed escalation.

At result 4, an accepted bounded extension may increase the finite cumulative `pause_at`, binding episode, affected scope, and limit to a durable decision. Preserve count and episode, re-diagnose before continuing, and diagnose again after two further failed repairs. Missing, declined, stale, wrong-episode, or unbounded approval cannot unlock dispatch. An author upgrade grants no repair allowance.

Preserve independent `authority` and `blocked` holds across diagnosis and extension. The effective action is pause first, then blocked hold, authority hold, due diagnosis, or normal route. Ordinary ACCEPT cannot clear a hold; root validates explicit resolution evidence and names the resolved holds in the accepted transition. More budget never resolves an authority conflict or missing infrastructure.

An episode opened before production closes only on complete-definition ACCEPT plus required earlier plan conditions. Production entry promotes its closing condition to final verification. Episodes opened at/after implementation close only when the corrected candidate has clean convergence, accepted quality, valid E2E disposition, and final verification. Intermediate ACCEPT or convergence cannot conceal later churn. Retain the closure transition head after `recovery` becomes null.

Clearly transient technical failures get at most three root-issued invocations including the original of the same operation, with operation identity/count/result persisted in existing failure evidence referenced by `resume`. Identity binds tool/command, target, and cause, surviving renamed attempts and resume. Respect retry-after and avoid duplicating SDK retries. Success closes this technical allowance; independently evidenced external repair can establish another. Missing authority/infrastructure or fatal errors stop when retry cannot help. Product assertions use substantive recovery. A documented nondeterminism investigation uses this technical allowance without erasing established product failure.

After every substantive result, run the read-only targeted guard with `--repo`, `--story-state`, and `--result`; root persists its proposed transition. Before corrective dispatch, escalation, or resume progression, rerun with `--next-stage` and applicable `--next-profile` against the current boundary. State/evidence must agree with `transition_head`; no manual counter reset or stale-head replay. A paused state is valid, but a prohibited next dispatch returns failure. Mechanical checks do not prove semantic diagnoses or agent compliance.

## Definition Review

The definition reviewer has two modes:

- plan-only: used before tasks when AGENTS.md triggers planned-change authorization;
- definition: reviews spec, plan, tasks, BMad alignment, and speckit-analyze consistency in one pass.

Each finding declares `earliest_owner: spec|plan|tasks`. Send it unchanged to the active persistent author. After a `spec` correction, prior plan classification is invalid; a `plan` finding that challenges classification or its trigger also requires fresh classification. Before recovery takeover, use the same persistent `definition-author` for `none|bounded` or return architecture content to the retained high author, `plan-architect-high`, while its accepted trigger remains valid. `tasks` return to the ordinary author. After takeover, the recovery author owns all classification and definition writing without returning to either old author. Reconcile affected tasks after plan correction; tasks-only correction remains local. Root validates/hashes each phase before downstream follow-ups. Preserve valid text, IDs, code/tests, and evidence with unchanged required inputs; revalidate affected checks if independence is uncertain.

Reuse the same `definition-reviewer` session for plan-only, complete-definition, and correction review within a story. Every review turn receives current raw artifacts and ordered prior raw verdict paths. If the session is unavailable, a recovery replacement receives the complete current boundary and ordered raw verdict history, never only the latest verdict or an author summary.

Author self-review is evidence returned by the author, not an independent verdict and not another gate.

## User Decisions

Pause only for a repository-mandated planned-change gate, accepted-definition implementation approval, prior/higher authority contradiction, new architecture outside the accepted plan, the current recovery pause limit, a hard blocker, or another precise unresolved high-impact choice.

For actual message selection and examples, read `human-interaction.md`. Preserve the current pre-task planned-change gate and its review/authorization order; do not add a tasks walkthrough. Optional input during spec/planning is a separate nonblocking communication behavior, not another pause condition. Silence creates no decision or state event. Root relays actual replies to the active author and reconciles late changes at the earliest invalid owner before accepting affected artifacts.

A formal request must make the topic and exact decision clear in the user-visible message, including why user judgment is required, viable options, the recommendation and its main consequence, material uncertainty, and helpful design concepts. Link related files and authorities after that explanation. The record retains the exact authorized scope and scope hash, what remains unchanged, and which downstream gates still apply. Merely storing these fields in an artifact does not make the outgoing request useful. Consequential decisions use a briefing; repeated failures/blockers use root's evidence-grounded recovery report. Neither explanation nor preference feedback supplies approval.

Persist the decision once in the development record or a decision artifact and store only its ID, scope hash, status, time, and evidence path in state. Preserve accepted authority for unchanged semantic scope, explain its continued applicability, and bind corrected accepted artifact hashes. Byte changes alone do not require duplicate approval. A recovery extension also identifies failed approaches, proposed changed approach, episode, and finite limit. Model escalation does not authorize architecture or product changes.

Root checks the actual user's instruction and its context, including when importing a decision from an explicitly requested discussion task. A guide's assertion, tentative preference, or stale answer is not an applicable approval. The lifecycle approval helper checks state/evidence fields, not the actual human source or semantic currentness of the scope; root must not present that mechanical check as proof of those judgments.

### Explicit delegated approval (optional)

Human approval remains the default. Only planned-change authorization (`plan-review`, after independent plan ACCEPT and before tasks) and `implementation-approval` (after complete-definition ACCEPT and before implementation) may be supplied by a delegate explicitly designated by the user, within the user's stated scope and conditions and when consumer policy permits it. All other gates remain unchanged: no delegated recovery extension, hold resolution, prior/higher-authority contradiction, later unplanned architecture/high-impact exception, reserved epic disposition, retrospective choice, or execution permission. A scoped architecture proposal at the normal planned-change gate is eligible only if the user included it; routing an exception back to plan does not expand the delegation.

Use the delegate's stable task ID/chat link, not its title. The original user instruction must name the delegate and applicable story scope/gates; record its exact text and source. The delegate cannot appoint a replacement or transfer authority to its subagents. Root verifies actual source/context, response identity, semantic scope, and any withdrawal, expiry, or other user condition before use; uncertainty stops this approval route. No mandatory charter, registry, communications protocol, or new agent is required.

The runner owns every approval-related file write. The delegate may work in another directory and returns only a decision: approve, decline, or needs clarification, identifying the existing request/decision ID and accepted boundary with its rationale. Root supplies the decision briefing, raw accepted documents, hashes and independent verdict using accessible absolute paths or the raw material itself; a summary alone is insufficient. Exchange messages only within the user's communication authorization. Root records the actual reply as `delegated-approval`, never `human-approval`, then uses the existing state approval pointer and targeted guard. The delegate does not write runner state, evidence, or Git; code/test worker ownership is unchanged.

See [artifact-contracts.md](artifact-contracts.md#delegated-decision-evidence) for the small evidence extension. An unchanged decision ID is reused on duplicate delivery, not counted as another approval. Materially changed accepted documents/review require a newly bound delegate response before progression; routine post-implementation task checkboxes use the existing accepted-definition proof rules. Silence, preferences, an independent ACCEPT, or a delegate summary of alleged human consent is not delegated approval.

## Implementation and Convergence

Behavior corrections reuse the task group's available/current tdd-test-author for regression RED and tdd-implementer for minimal GREEN. Both assess their final affected code; correct concrete local design problems while GREEN and rerun focused and surrounding tests after the final shape. Refactoring is conditional, not limited to one cleanup; routine private decomposition stays local, while consequential public-contract, schema, migration, dependency, architecture, or authority changes return to plan. An unavailable owner is replaced from raw scope, artifacts, findings, and RED/GREEN evidence. A defective test returns to its test owner with evidence; never weaken assertions to accommodate a product defect or rewrite historical RED. Test-only coverage of already-correct behavior records no expected RED. Apply governed-tdd's verification routing by accepted behavior and risk, including for dependency/artifact/packaging/setup changes. Nonbehavioral work uses direct verification; behavior-preserving refactors use before/after regression or characterization checks. Neither requires fabricated RED or a test-author turn solely for ceremony.

A consequential interface, state lifetime, async/cancellation, retry/idempotency, cross-module, or architecture choice outside the accepted design returns to `plan` with the active definition owner. Routine private helpers and incidental language choices remain local implementation responsibilities. Retrieve relevant lessons, reconcile valid implementation with the revised design, and repeat affected definition/downstream checks. Use existing scope/architecture authority rules; go-back is not a Git rollback.

Convergence verifies prerequisites, reuses unfinished tasks already covering an obligation/behavior/defect, and appends only genuinely new traceable work. A completed-task regression remains a real correction. `tasks_reused` and `tasks_appended` are non-converged; mixed reuse/append reports the latter. `return_to_definition` routes upstream before speculative appends. Description/dependency edits belong to the definition author. Rerun convergence after the required repair, then root lookback before quality.

## Quality Review

Quality review starts only on a converged pinned boundary and covers correctness, security, edge cases, tests, concrete maintainability risks in the affected production/test units, architecture seams, regression risk, and repository standards. Distinguish evidenced risks and documented obligations from preference; do not inflate severity to make optional polish blocking. It does not repeat a separate acceptance review.

A production/test correction invalidates affected convergence, lookback, and quality. A definition correction returns to its earliest owner. A tracked E2E test invalidates quality; a product E2E failure returns to TDD. Ambiguous E2E failure requires user direction. Include refreshed lookback/project outputs in the new reviewed candidate; routine successful evidence or lifecycle bookkeeping does not invalidate the lookback section.

## Epic Findings

Epic architecture findings retain their independent close-governed-epic disposition contract. Story-level ACCEPT never waives Strong recommendations, ADR conflicts, Critical/High findings, or the exact-commit epic audit.
