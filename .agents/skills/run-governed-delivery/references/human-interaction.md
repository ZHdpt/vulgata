# Human Interaction

Read when selecting optional drafting questions or preparing a real user decision. Root owns user communication and approval records. The author owns its current definition phase; reviewers judge raw artifacts. This reference changes presentation and feedback handling, not the gates or budgets in `review-and-correction.md`.

## Choose the interaction by its purpose

| Situation | Root behavior | Effect of no response |
|---|---|---|
| A useful preference during spec/plan with a defensible authorized provisional approach | Optional nonblocking question; drafting continues | No new instruction or state event |
| A consequential choice at an existing approval gate | Decision briefing; optional deeper explanation | Existing gate remains unsatisfied |
| Repeated failed repairs, a hard blocker, or another recovery decision requiring human direction | Evidence-grounded recovery brief | Existing pause/hold remains in force |

Keep the pre-task planned-change review/authorization order and its existing request design. Do not add a walkthrough of tasks. The distinction above never permits bypassing a required gate.

## Optional drafting input

Ask only when the answer could materially improve a still-forming specification or plan and existing requirements do not settle it. Select by user impact and uncertainty, not document length or technical novelty. Avoid routine implementation choices, question quotas, repeated nudges, and overlapping questions. Include the proposed assumption, its main consequence, and the affected topic; a recommendation or preselected option is not an answer.

The active definition author sends root a concise question and provisional approach as an intermediate message, then continues currently authorized work. It does not wait for root or the user, switch authoring phases, or open another user channel. Root surfaces useful questions through `request_user_input_async` or an equivalent genuinely nonblocking tool when available; otherwise use a concise commentary question and continue. Do not use a blocking ask tool, change collaboration mode, or assume every host/model exposes an asynchronous capability.

Silence, dismissal, or unavailable optional-input UI means no new user instruction. It creates no approval, failure, retry, synthetic default answer, or question ledger. Keep material assumptions in the ordinary spec/plan so the proposal is understandable. If no defensible authorized provisional approach exists, report the blocker through the existing policy instead of presenting missing authority as optional input.

Relay the user's actual response, its topic, and original context to the currently authorized author. Tentative preferences and exploration remain tentative; answering a preference does not satisfy a later approval gate. Never redispatch a superseded author or apply an answer to an obsolete alternative without checking it.

Before accepting an author's return, reconcile relevant replies that arrived during writing. A material late reply follows existing earliest-owner invalidation and affected review: spec changes return to spec even if recovery currently belongs to plan. Preserve valid work, counts, pauses, and holds. No response is retroactive authorization for implementation or changed scope.

Illustrative contrast:

- Unhelpful: "Choose a retry strategy before I continue."
- Useful: "Optional input; planning continues. I propose manual retry after a failed import because automatic retry could repeat external effects. Would you prefer automatic retry with extra safeguards? Without a response, manual retry remains the draft proposal, not an approved decision."

## Consequential decision briefing

Help the user evaluate the choice, not certify that they read every document or accept the author's judgment. Lead with the exact decision and what changes for users/operators. Explain the recommendation and its principal downside, expose material uncertainty or disagreement, and state what approval permits and what it does not. Include consequential defaults already selected in the proposal, not just open questions. Distinguish what remains unchanged or delegated to authors/reviewers.

Put source links to specific supporting sections/evidence after the explanation. Material drawbacks, irreversible effects, and unresolved constraints must be visible in the initial message, not hidden behind those links. Group genuinely related decisions for clarity without bundling distinct permissions into a blanket approval. Detail should be proportional to the choice; document length is not a reason to produce a longer briefing.

Offer deeper explanation around the user's questions: why this option, what an alternative changes, or what remains unverified. Do not require a quiz, artificial delay, repeated confirmation, or a declaration that every line was reviewed. Selecting an alternative changes the proposal; the active definition author and affected independent review must reconcile it before progression.

Illustrative contrast:

- Unhelpful: "Spec, plan, and tasks are complete. Please read them and approve."
- Useful: "The proposal keeps successfully imported items when cancellation occurs. This avoids undoing useful work, but users must distinguish partial completion from success. I recommend keeping partial results with an explicit status. The alternative is all-or-nothing import, with different storage and recovery requirements. The requested approval covers implementing the reviewed partial-result behavior; it does not authorize a new retention policy."

### Optional deeper discussion elsewhere

Ordinary guidance stays in root. Existing optional read-only context synthesis may collect raw citations for a substantial source set, but it is not another reviewer and cannot invent preferences or grant approval. No new installed guide role is required.

Create a separate user-facing discussion task only on an explicit user request and when supported by the host. Supply the relevant candidate snapshot and read-only scope: explain, do not edit product/workflow artifacts, change Git, or accept gates. Do not claim isolation that the runtime cannot provide; stay in root if the capability is unavailable. Existing root work must still respect its pending gate.

On return, root reads the user's actual instruction and source context, not just the guide's assertion that approval occurred. Check it against current documents and prior authority, recording a real applicable decision once. A hypothetical or tentative preference is not consent. Reconcile a materially changed proposal through its owner and review; do not request the same unchanged approval again solely because it came from another conversation.

## Delegated decisions

For the explicitly delegable purposes only, apply [the optional delegated-approval rule](review-and-correction.md#explicit-delegated-approval-optional). Address review-gate briefings to the user-designated task with raw accepted inputs and the independent verdict. When `recovery-extension` is explicitly delegated, send the recovery brief below with failures, current episode/count/limit, proposed changed approach/new limit, and the user's cumulative ceiling. It is still a recovery decision, not a document walkthrough. The runner records the actual user delegation and delegate response; different working directories never transfer approval-file ownership. Hold resolution, changed authority and other reserved decisions remain human-only.

## Recovery brief

Root reconciles worker evidence and reports why development is stuck. This is execution context, not a teaching task or a new standing agent. Lead with the failure and impact, then explain materially different repairs and their observed outcomes. Distinguish the initial failure, failed repairs, genuinely new defects, duplicate reports, expected RED, and technical retries.

Separate what is observed, what has been ruled out, what remains a hypothesis, and what is unknown. Explain why prior repairs were insufficient without inventing a confident cause or merely blaming a worker/model. Cite the actual evidence. A plausible cause is not a demonstrated cause.

Recommend a changed approach or, when necessary, a bounded investigation that can distinguish remaining explanations. Identify valid work retained, consequences, how the next step will be checked, and the stopping condition. State the precise decision required: extra finite effort, changed scope/architecture, access to a resource, or stopping/deferring. Do not invent cost estimates or promise that a stronger model will resolve the problem. More attempts alone is not a recovery strategy, and more allowance cannot clear independent authority/blocker holds.

Illustrative contrast (not a report of an actual incident):

- Unhelpful: "Retry limit reached. Approve continuing?"
- Useful: "The initial failure and three repairs still leave cancellation incorrect. The repairs changed worker cancellation, but the latest trace shows publication continuing after the request ends. Operation lifetime is the current hypothesis, not yet a confirmed cause. I recommend revisiting that plan boundary while retaining the passing parser work. This changes the previously approved shutdown behavior; your decision is whether to permit that revision or narrow/defer the affected behavior. The bounded next step and verification evidence follow."

## Before sending

Check the actual outgoing message: can the user understand the choice, its consequences, supporting evidence and uncertainty, and whether work is paused without reconstructing all linked documents? A list of fields stored elsewhere is not an adequate request. Keep durable decision metadata in existing records; do not paste scope hashes or raw logs into the opening explanation when a supporting reference suffices.

Formal decisions still bind their real user source and applicable scope under `review-and-correction.md`. Silence, viewing a document, an explanation, or a preference answer grants no authority. Existing validators check mechanical fields, boundaries, and budgets; they cannot prove semantic scope applicability, the truth of a causal explanation, or that a briefing is useful. Root must perform those checks rather than claim a schema-valid record establishes them.
