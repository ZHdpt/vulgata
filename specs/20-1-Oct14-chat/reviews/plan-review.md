# Independent OCT-CHAT plan review

Date: 2026-10-06 (Asia/Shanghai)
Reviewer: independent_plan_review subagent (separate from definition author)
Decision: REVISE (round 1)

## Scope and authority

Reviewed spec.md, plan.md, research.md, data-model.md, contracts/consumer-boundaries.md, quickstart.md and checklists/requirements.md against the assigned worktree's October delivery/integration plans and streaming-chat brief, constitution 2.0.0, architecture gateway and AD-24–29, and relevant PRD FR-11/13.3, NFR-2.3/3.2/3.3. Actual branch is oct14/chat; fad7f11 is an ancestor of HEAD (Git exit 0). No other worktree, skill, application/test/runtime/provider or database was used. This is design review, not runtime qualification or implementation approval.

## Blocking finding R1 — Durable admission must precede C1 live dispatch

plan.md's C1 offers a live UI/tool conversation and an authorized real-provider round, but lists only C0, tool approval, provider/MAF and shared-admission prerequisites. Business dialogue implementation/migration is first a C2 prerequisite. consumer-boundaries.md also permits dialogue extensions to arrive before C2. In contrast, execution design step 1 and data-model.md require durable begin/fence ownership before *any* model dispatch. The current increment boundary therefore leaves two incompatible implementation instructions and could permit an unfenced, unsaved real conversation.

Correction: make Business-owned durable begin/fence/outcome/lookup implementation and its migration a prerequisite for C1 real/provider/user dispatch, with persistence qualification proportionate to that first use; or make the pre-persistence C1 path explicitly test-only controlled mechanics and disable real user/provider dispatch until that dependency arrives. Preserve the same shared contracts, no in-memory production fallback or duplicate schema. C2 can still add follow-up/history and reader integration.

## Other assessment

- First-deliverable scope, fixed publication and omitted reattachment are explicit October dispositions, not claims of full PRD completion.
- Shared Knowledge, migration and Web composition ownership are preserved. K0 correctly remains undelivered and blocking for dependent implementation; conceptual consumer needs are not competing published DTOs.
- Authorization covers all inference contributors, including unreported metadata and history, with Business exclusion and safe failure; exact-read credit is separate from access lineage.
- Streaming preserves immediate text, native tools, bounded rounds and truthful final completion. Installed MAF streaming is an explicit compile/control qualification dependency, not an asserted existing capability.
- Fenced idempotent outcomes, uncertain acknowledgements, no automatic regeneration, host continuation and orphan handling are adequately specified apart from R1 sequencing.
- Controlled, PostgreSQL, real-provider, browser-visible timing and useful source-backed evidence are separated. Exact reader and final combined integration remain named external dependencies.
- Explicit stop after accepted review is correct. Future agent-tool approval is required; no Scan strategy change is proposed. Additional PRD departures require approval. This review grants none of those permissions.

## Reviewed artifact SHA-256 (round 1)

| File | SHA-256 |
| --- | --- |
| spec.md | DF4690DDE4297AC2A4F99D20619FA869DAE99DFCA70832B22F3C9D30662CA174 |
| plan.md | FE54B535393681B3FFA951678D9AA96D10E2EE556E0A684176936B4D596A33A3 |
| research.md | 10BE4FF46AA9C79DBD49830A9958A675C4D85E0F47025B09AEE35A6329133395 |
| data-model.md | 58115AE829D9472E6B2CF9600F2DDFF810B69B9C202E00E58F20465CAD938EF2 |
| contracts/consumer-boundaries.md | C57C106C04974F4ABF3836134762EBC4D4872526D4A83CE4472DC151953B5D67 |
| quickstart.md | BC134FC25CB7C6C8715A737D7AA7F591E6C3624D0CFDD9BB5B8A74C328F66AD3 |
| checklists/requirements.md | 6A498AC57BB8D4D14F08F8F2715B08B00F6012309ADFECDE57C35A40F2AD8D0C |

## Round 2 — ACCEPT

Date: 2026-10-06 (Asia/Shanghai). Final independent disposition: **ACCEPT for planning only**. No blocking findings remain.

R1 is resolved consistently in plan.md's C1 prerequisites, C2 scope, increment note and dependency stops; consumer-boundaries.md's dialogue checkpoint timing; and quickstart.md's preconditions. Before any real-provider or production user-facing turn dispatch, Business must supply durable begin/fence/terminal-save/lookup/orphan operations and schema, with focused PostgreSQL admission/save checks. Earlier C1 is expressly test-only controlled mechanics. C2 now adds history/reader behavior to the durable C1 foundation. No production in-memory fallback or competing schema is permitted.

The unchanged artifacts retain the round-1 assessment. K0/shared fixture, Business persistence, Explore exact reader, integration-owned Web/provider/admission and actual MAF/runtime qualification remain explicit implementation dependencies, not blockers to accepting this dependency-aware plan. Acceptance does not claim those dependencies exist or their owners approved the requested shape.

Stop now before task generation or implementation. Future wiki/report agent-tool changes still require explicit planned-change authorization; no Scan change is currently proposed, and additional PRD departures require approval. No tests, application startup, database/provider work or additional skills were run for this re-review.

### Accepted artifact SHA-256 (round 2)

| File | SHA-256 |
| --- | --- |
| spec.md | DF4690DDE4297AC2A4F99D20619FA869DAE99DFCA70832B22F3C9D30662CA174 |
| plan.md | 83F80A644E89051CB618BB51C9E9A61CBF171A8DA1BBAC605819389F10B2C1D7 |
| research.md | 10BE4FF46AA9C79DBD49830A9958A675C4D85E0F47025B09AEE35A6329133395 |
| data-model.md | 58115AE829D9472E6B2CF9600F2DDFF810B69B9C202E00E58F20465CAD938EF2 |
| contracts/consumer-boundaries.md | 8247F3F423D19B7AFEFD46467EF57F22FCB16DFF7EAB27D6D4F874ECEA87FA9C |
| quickstart.md | 3BFD16FCC71E6006365CAD6E32A7100210C99252960FBFB615581432E25CB7C5 |
| checklists/requirements.md | 6A498AC57BB8D4D14F08F8F2715B08B00F6012309ADFECDE57C35A40F2AD8D0C |

## Round 3 — Focused F3/F4 and provider admission review — ACCEPT

Date: 2026-10-06 (Asia/Shanghai). Independent reviewer: independent_plan_review. Decision: **ACCEPT for the focused planning corrections**; no blocking findings in the requested scope. Earlier rounds remain retained above.

Scope: seven current OCT-CHAT planning artifacts, limited to F3 content continuation, F4 material claim-group qualification preservation, provider admission clarification, and continued pre-live-C1 durable persistence stop. Read the explicitly authorized counterpart `C:/Users/13728/.codex/worktrees/f081/vulgata/specs/20-1-Oct14-business/contracts/knowledge-contract.md` as reference only (Claim qualifications and consumer preservation, Consumer reads, F2 execution/admission scope and checkpoint). No counterpart edit or approval on another lane's behalf.

- **F3 accepted:** ReadWiki passes Business-issued opaque continuation through the owner port, with fixed database/batch/publication, exact version/section, subject/purpose/mode/scope and live authority. Complete UTF-8, qualifier context and actual returned coverage survive chunk boundaries. Only successful returned chunks enter the turn ledger; partial references remain explicitly partial and never credit unseen sections or suffixes. Existing aggregate/time/tool limits remain unchanged. Conformance includes both a multi-section Document over 64 KiB and a single section over 64 KiB, multibyte boundaries, tampering/expiry/mismatch/revocation, cancellation and exhausted budgets. Explore retains reader UI continuation ownership. Owner-issued compiled types/coverage semantics remain a future checkpoint dependency, not a delivered contract claim.
- **F4 accepted:** The owner qualification vocabulary and ClaimGroupId/evidence provenance remain canonical. Material SourceSupported/Advisory/Hypothesis/Uncertain/Mixed distinctions and mixed-clause attribution are preserved in prompts, first affected streamed prose, saved text and reused completed history. Business-safe attribution avoids technical identifiers. The shared lock-action/supplied-motive fixture tests unsupported motive/duration promotion and qualifier preservation at content/history boundaries. Whole affected groups/turns are omitted if qualifications cannot fit. No per-sentence citation obligation, semantic verifier, full-answer buffering or new publication gate is introduced; preservation remains a prompt/consumer behavior requiring the declared qualification evidence.
- **Admission clarification accepted:** Shared policy is explicitly separated from capacity enforcement. Chat uses Web's process-local pool; Business finalization uses the generator Scan pool. The plan makes no cross-process/global cap claim and adds no coordinator or competing Business pool. Original per-turn budgets and provider-attempt permit scope remain intact.
- **Prior R1 stop preserved:** plan.md, consumer-boundaries.md and quickstart.md still require Business's implemented durable begin/fence/terminal-save/lookup/orphan operations, schema and focused PostgreSQL admission/save checks before any real-provider or production user-facing C1 dispatch. Earlier C1 remains controlled test mechanics only. The dialogue contract remains a separate dependency; these corrections do not deliver it or make it a prerequisite for Business's initial Knowledge checkpoint.

This is a focused independent document review, not full implementation requalification or runtime proof. No skills, tasks, implementation, tests/runtime/provider calls, commits or outbound chat messages were performed. Existing planning-only stop and future agent-tool approval remain effective; no task generation or implementation authority is supplied by this acceptance.

### Reviewed artifact SHA-256 (round 3)

| File | SHA-256 |
| --- | --- |
| spec.md | 952783C59D6C0419245AA88B119F6BE4B5D485073B9F5AAA64814EE2B3DA25DF |
| plan.md | 73F4BFAD8730BF20C6D1305875424519E00E0735CC8374359A2B88DC80C36BF2 |
| research.md | EFD0D9CF596C268611CB9CBB604157035C12CE044A2D81449B115CA41EF2BB6A |
| data-model.md | 745B36D695A607F3A6C179A12B265829B9C4F954AD0F3C6F0889F4793D9C482B |
| contracts/consumer-boundaries.md | 98D9585BC11708A693C614DF01FADA8D8A9C46C87AD3A063941909CEC0726765 |
| quickstart.md | 8BE07EE2AD3D257A52B7B612713ABC28F27AE8EFB92AB2B2418BD329CFE4FA77 |
| checklists/requirements.md | 21E8267FC52F170B01520220B162BD1C6B393930BEAA0D97BDF89C97948C93EF |
