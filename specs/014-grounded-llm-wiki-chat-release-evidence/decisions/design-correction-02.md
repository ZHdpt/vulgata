# Story 12.5 Design Correction 02

- Decision ID: `story-12-5-design-correction-02`
- Decision: return Story 12.5 from implementation/convergence to the plan owner.
- User direction: continuation-04 is not approved; use the findings from Codex task `01a04783-c4ca-7b13-b94c-5842f2ae2685` to correct the delivery design.
- Source checkpoint: `cdec21ceeb81170ed24e709bf7be3634d34b15ea`
- Track C: closed.

## Current-boundary disposition

The audit correctly classified the repeated convergence churn as a delivery-design defect rather than an isolated missing implementation. Its original central cutover finding has partially aged: the current `ChatEvidenceRetriever` no longer contains `RequiresCrossRepositoryEvidence`, `ExactRelationshipRow`, or a wording-selected second packet path, and now composes the typed lexical projection, relationship expansion, and single packet builder. Those corrections remain production evidence and are not reverted.

The plan-level defect remains current:

- Story 12.5 and Track B combine retrieval, relationship/currentness authority, packet packing, conversation, strict provider streaming, completion observation, coordinator publication, accepted-only reveal, Scan-state UI, evaluation, and release evidence under one acceptance boundary.
- The plan describes desired components but does not define sufficiently strong subsystem cutover/acceptance checkpoints, explicit publication linearization ownership, or mutation-resistant interaction invariants.
- Convergence tasks T079 through T081 repeatedly refine the same 29-second publication boundary, demonstrating that AD-7/AD-7B do not yet provide an implementable owner model.
- Green aggregate suites do not by themselves prove architectural adoption; the corrected plan must name forbidden legacy topology, single-path interactions, negative/mutation tests, closed optional-support outcomes, and the precise deadline/publication state machine.
- Neutral Track B runtime/completion observations must remain separate from Track C candidate/process/signature/release attestation.

## Authorized plan work

This decision authorizes plan-phase correction and self-review only. The corrected plan must:

1. preserve the accepted product intent and already-valid Track A/Track B implementation evidence;
2. inventory requirements as implemented, partial, missing, obsolete, or contradicted at the current checkpoint;
3. define independently acceptable B1 retrieval/packet, B2 relationship/currentness, B3 conversation, and B4 streaming/publication/reveal boundaries with one integration checkpoint;
4. specify the single allowed production topology and forbidden legacy/alternate paths;
5. define closed optional relationship/Flow Group support states and exact failure ownership;
6. define one implementable absolute-deadline publication linearization model, including completion telemetry ordering and live UI reveal behavior;
7. add interaction and mutation-test requirements that fail if lexical primaries are replaced, relationship expansion is skipped, legacy authority regains control, or post-check work extends publication beyond the deadline;
8. correct any remaining traceability/status inconsistencies and explicitly separate Track B runtime behavior from Track C release proof;
9. preserve no-migration, no Story 12.4 contract mutation, no vector/autonomous loop, authorization/currentness, bounded evidence, Source Anchor, Chinese Business/IT, and mandatory raw-streaming boundaries.

Author model strategy is explicitly escalated: the plan author and, after the later authorization gate, the task author use `gpt-5.6-sol` with high reasoning. This model choice does not advance or waive a workflow gate.

## Invalidated downstream authority

Plan review, task generation, definition review, implementation approval, convergence, quality review, E2E decision, verification, and Track B/Track C completion are invalidated for the corrected plan boundary. Existing approvals and verdicts remain historical evidence but cannot authorize new tasks or production writes when the plan/tasks hashes change.

After plan self-review, the architecture-triggered independent plan review must run. If accepted, the workflow must pause for fresh planned-change authorization before task regeneration. This decision is not planned-change authorization, task-generation approval, or implementation approval.
