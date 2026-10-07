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

## Bounded K0 preparation review — accepted 2026-10-06

Root record of the retained independent reviewer's read-only assessment of the imported-contract preparation. This is not a new feature implementation approval or a claim that earlier planning hashes describe the later import annotations.

Reviewer found no actionable findings in the Chat-owned conformance tests, one exact suite classification and lane-local K0/W0 mapping/proposal. Tests consume actual canonical Core/Shared types/validators and the imported Business fixture, with no duplicate contract/policy. The documented limit to controlled conformance is accurate. K0 can be acknowledged independently of W0; T001 remains open overall. W0 stays integration-owned and unimplemented, and D0/W1 continue to block live C1.

Acceptance was conditional on the corrected SelfContained run passing. Root verified the persisted final TRX at `TestResults/isolation-fac1f9c98be242fcad2914f297f89695/results.trx`: 60 selected, 60 passed, 0 failed/skipped, including 10 Chat and 50 Knowledge cases. That condition is satisfied. The first run's record-equality assertion correction is explained in quickstart.md; no product RED/GREEN is claimed. Reviewer made no file changes or runtime calls.

Verified test-file SHA-256: `41EE10241BD0CEB1E0D604605F6BB5252CF7F85076B545E97D6FD2FF8259298E` for `tests/Vulgata.Tests/October/Chat/OctoberChatKnowledgeConformanceTests.cs`. Imported integration source: `3bcd815b09cfbac7418d6644a2a75816e0788ec0`. Historical review rounds are retained verbatim. No other lane or aggregate accepted-K decision is made by this review.

## Controlled T002–T006 quality review — accepted 2026-10-06

Root records the retained independent reviewer's read-only source/test assessment under the user's explicit controlled implementation instruction. Separate test/production owners and a sole inventory editor retained their write boundaries. No additional skill/workflow or external runtime was invoked.

Initial assessment required correction of (1) a host update ignored while the initial read was pending and (2) optional entry-instance replacement/removal failing to initialize or safely retain the chosen October path. The test author reproduced all three affected cases through the isolated launcher (5 selected, 2 passed, 3 failed); production corrected the pending-selection generation guard and page binding lifecycle without weakening tests. The reviewer then found both issues closed with no remaining actionable findings in the scoped change. Canonical policy ownership, default legacy initialization and W0/D0/provider boundaries remain intact.

Reviewer acceptance was conditional on final combined GREEN. Root verified `TestResults/isolation-b9e9c73f002a4341be3a302d9771c27b/results.trx`: **154 passed, 0 failed/skipped** (46 newly implemented selection/entry tests, 10 prior Chat conformance, 50 Knowledge and 48 legacy Chat). Both authors also accepted final shape. This satisfies the review condition, not T007/integrated C0 or real authorization/storage/streaming qualification. Full RED/GREEN and registration handoff are in quickstart.md. Earlier hashes/reviews retain their historical meaning.

## Controlled work toward T021 — scoped quality review, 2026-10-06

Independent reviewer `chat_research` inspected the new budget/event/read-ledger primitives, isolated turn VM/component/page bindings and controlled tests without editing files or running resources. No unrelated skill or workflow was invoked. Scope expressly excludes the T011 facade: automatic approval review rejected its patch, the user subsequently chose "Keep T011 blocked", and its fail-closed scaffold/meaningful RED tests remain unchanged. Missing Business full-lineage/dialogue contracts and integration W0/W1 are dependencies, not substitutes this lane may invent.

The test author identified a misleading retained-partial-content label on empty/whitespace interrupted or failed outcomes. Four added tests reproduced it (4 failed, 0 passed/skipped); production corrected the label. The independent reviewer rechecked that correction and accepted the scoped shape with no further actionable findings, conditional on final affected GREEN. Root verified **201 passed, 0 failed/skipped** in `TestResults/isolation-80ead151507142718b17714df3ed5bda/results.trx`; that condition is satisfied. Both authors also assessed their final affected boundaries. Detailed RED/GREEN, warnings and exclusions remain in quickstart.md.

Reviewer explicitly recommended leaving **T008–T009, T010–T011 and T020–T021 unchecked/partial**: primitive counters and constants do not complete server-bound routing/argument/page enforcement, and controlled component events do not prove actual provider/browser/coordinator behavior or full rendering scope. T001–T006 remain the only completed tasks. This acceptance establishes the verified subset's quality, not whole-Chat GREEN, disclosure authority, durable completion or runtime qualification. No new human gate was introduced for the unaffected work.

## Explicitly approved T011 and remaining controlled work — 2026-10-06

The user subsequently approved the specific controlled-facade recommendation and instructed "Proceed T011 and what remains before T021". Independent reviewer `chat_research` found two T011 defects: an owner exception could be classified as invalid model input, and the individual byte cap omitted projection metadata/escaping. Five targeted cases reproduced those defects; three additional failures were independently diagnosed as incorrect ImmutableArray record-equality assertions and corrected to exact memberwise comparisons. Production isolated input parsing and capped the complete serialized result before ledger credit. The reviewer accepted both corrections and controlled **T008–T011 completion**, conditional on affected GREEN; root verified **238/238 passed** at `TestResults/isolation-c028f249f2fe45a9aecd6e8db4a2a65e/results.trx`.

A distinct later review accepted fixed-resource strict UTF-8 prompt loading, external October prompt text and test-only installed-SDK probes, conditional on affected GREEN. Root verified **253/253 passed, no failures/skips**, at `TestResults/isolation-a9cc8d595a564528b10f9135304129f9/results.trx`; both authors accepted their scoped final shape. Native tool result schema inference required an explicit JSON-string representation in the probe; this is a recorded integration limitation, not production adapter qualification. Automatic approval review rejected the proposed production SDK scaffold; it was not applied or recreated in tests. T012–T015 remain partial, and D0/W1/full-lineage, actual prompt embedding, raw transport and live integration remain unresolved. No shared contract/schema/project or root agent instructions were edited.

## Explicitly approved controlled T013 — accepted 2026-10-07

After the user approved the concrete controlled agent/adapter question, independent reviewer `chat_research` reviewed the internal typed-client MAF path and separate test/production authors assessed final shape. Initial review identified unbounded waits on non-cooperative pending iteration and preterminal call/argument accumulation. Four tests reproduced those defects before correction. A final review identified unbounded normal disposal; two further tests reproduced cancellation/deadline settlement failures before the single-start, bounded disposal correction. All prior findings are closed, with no remaining actionable findings in the controlled scope.

The reviewer accepted source conditional on final affected GREEN. Root verified **291/291 passed, 0 failed/skipped**, including 40 provider cases, in `TestResults/isolation-ccd63f8f85234e7dba0d29a19fef51ef/results.trx`; this condition is satisfied. Original budgets, strict facade arguments, typed text-only updates, native duplicate/unknown-call rejection, terminal classification and borrowed-client ownership remain intact. Full RED/GREEN and build-warning evidence is in quickstart.md.

This accepts the explicitly authorized controlled subset, not all T012/T013 or integrated C1. Raw transport, selected profile/credentials/retries, W1 admission and permit retention through deferred underlying cleanup, Business D0/full-lineage disclosure and actual prompt/host composition remain prerequisites. No new human approval gate is introduced, no live resources were used, and no owner contract or shared schema was invented. Historical review hashes and earlier holds retain their original scope.

## Actual invocation progress increment — accepted 2026-10-07

Independent reviewer `wave_reviewer` found no blocking findings in the agent/adapter progress changes or nine new ProviderTests cases. Notifications contain only application-generated operation identity and fixed lifecycle kind, preserve immediate text and report real facade results; predispatch refusals and throwing sinks do not fabricate success or recursively notify. A subsequent affected-test deadline failure was corrected by preserving the original cancellation cause before cleanup, independently rechecked without weakening tests. Final source acceptance was conditional on affected GREEN; root verified **420/420 passed, zero failures/skips**, at `TestResults/isolation-6abfb460971147f08fd2a4cc68614b97/results.trx`. Both owners accepted final shape. Actual coordinator/UI, raw HTTP/SSE, disclosure and durable completion remain outside this increment.

## Raw guard increment — accepted controlled boundary, 2026-10-07

`wave_reviewer` independently reviewed the borrowed HTTP handler, incremental validator, original-budget rejection accounting/cause and actual-SDK tests. Decoded argument duplicate properties were a blocking finding; nested-argument RED preceded the recursive case-insensitive correction. Actual SDK diagnostics justified one narrow final ToolCalls aggregation allowance. The reviewer confirmed a later buffered-output-after-close gap; two deterministic RED cases preceded synchronous exchange closure and atomic copy/close checks. Final review found no remaining blocking finding, conditional on affected GREEN; root verified **455/455 passed, no failures/skips**, in `TestResults/isolation-7cd788a4de214b0d96018af3f6c1a333/results.trx`. Both authors accepted final shape. Quickstart records all diagnostic/test corrections without treating compilation or framework probes as product RED. Actual W1 admission, owner disclosure/storage and live provider/browser qualification remain separate.

## Canonical consumer increment — accepted controlled boundary, 2026-10-07

`wave_reviewer` independently accepted the opaque-union session, eager owner content proofs, atomic output admission, fresh selection/round/delta checks and legacy bypass refusal. Review requested between-round revocation and late ignored-cancellation Merge/Build coverage; the test owner added five cases. An actual-SDK byte mismatch then disproved JsonElement preservation. The reviewer accepted the narrow literal-JSON-text correction conditional on the unchanged exact outgoing-byte assertions. Root verified **541/541 affected passes, zero failures/skips**, `TestResults/isolation-acc5eb43c25e4fb49ed15018d28b946b/results.trx`, satisfying that condition. This does not accept owner crypto/storage implementation, public dispatch, subscriber isolation or complete T019; later report/save checks and trusted cancellation-aware host sinks remain required.

## Admitted runner — accepted controlled boundary, 2026-10-07

`wave_reviewer` accepted the confirmed-admission factory, exact embedded prompt/owner history inputs, only-narrowing lifetime, owner-codec capacity measurements, atomic reported references and truthful generation facts. Capacity review required a maximum-width measurement timestamp, separate from the actual generation end time. Production assessment identified stale Knowledge authorization during awaited execution checks; two RED cases preceded the final authorization ordering at typed and post-permit boundaries. The reviewer accepted both corrections conditional on GREEN; root verified **583/583 affected passes, zero failures/skips**, `TestResults/isolation-cc10983c70f942a399e5533ccb84e043/results.trx`. The independent two-modifier visibility review for `007fd7d3` is separate from this behavioral acceptance. Host execution registration, actual physical settlement, canonical persistence and subscriber/browser integration remain outside the accepted boundary.
## Canonical coordinator — accepted controlled boundary, 2026-10-07

`wave_reviewer` independently accepted the actual owner-store/client coordinator
after RED-backed corrections for authorization loss during drain and lost phase
acknowledgements. Recovery retains exact original identities/timestamps and waits
for actual operations to settle; it cannot redispatch or resubmit an attempted save.
Sanitized closure is limited to fresh explicit owner denial/unavailability before
payload freeze. Additional tests cover unsettled timed-out phase operations and
synchronous save throws. No remaining blocking finding was reported.

Acceptance was conditional on GREEN; root verified **22/22 focused** and
**720/720 affected**, zero failed/skipped, with the final TRX at
`TestResults/isolation-046b9ed63bdc469fb115b927c0f5c83f/results.trx`.
Root registration/commit/death authority, PostgreSQL and current UI disclosure remain
separate integration requirements. Cached terminal projection is host state, not
lasting permission to display it. No full T016–T023 completion is claimed.
## Bounded presentation adapter — accepted controlled boundary, 2026-10-07

`wave_reviewer` independently accepted the entire adapter, opaque-lineage callback
overloads, append-only suppression event, view-model terminal replacement and 12
targeted cases. Review added active-item/capsule byte exhaustion and actual native-read
snapshot checks. A fixture's incorrect initial-root expectation was corrected separately
from production findings. Two observed RED rows reproduced queued-denial display
retention; sequential content-free suppression now clears the bound view and rejects
late restoration without fabricating a saved outcome or active-slot release.

No remaining source blocker was reported, conditional on affected GREEN. Root verified
**795/795, zero failed/skipped**, at
`TestResults/isolation-d90c38a470d84f5e99363472087e653b/results.trx`.
Callback cancellation and queue retention are bounded, honestly owned lifetimes;
application handoff is not atomic network revocation. No route, browser or live provider
qualification is accepted by this review.
## Encoded Markdown leaf — accepted controlled boundary, 2026-10-07

`wave_reviewer` accepted the conservative rendering subset with no blocking findings:
fixed elements, encoded model text, no model attributes/navigation/images, bounded flat
parsing and complete-text fallback, immediately visible unfinished syntax, suppression
removing previous rich DOM, and local inherited styling. Six focused cases cover these
boundaries; this is not full Markdown/CommonMark or a performance/browser qualification.
Root satisfied the review's GREEN condition with **402/402 selected Chat/page passes**,
zero failures/skips, `TestResults/isolation-20a4b658e5374f58ac696abadc22bbcc/results.trx`.
## Saved-reference adapter and leaf — accepted controlled boundary, 2026-10-07

After Business confirmed the existing full-page encoder/lineage/display limits and
Integrator released bounded T026/T027, `wave_reviewer` independently reviewed the
adapter, leaf renderer, direct bound saved-view suppression and controlled tests.
The only source blocker was post-seal operation dispatch; two observed RED rows
preceded the atomic start/retention gate shared with Seal. Original deadline checks,
exact handle/profile/byte/digest validation, full lineage retention, stable original
reference facts and no new schema/owner API were preserved.

The reviewer accepted the correction and requested one honest ignored-apply coverage
case for the non-generic Handoff lifetime. Root verified **427/427 controlled Chat/page
passes**, zero failures/skips, including all 25 reference cases at
`TestResults/isolation-16fc8edabd1f4cef977f06bafe1d401d/results.trx`.
The reviewer read that case and confirmed all conditions satisfied with no remaining
blocker. Acceptance excludes parent-page/runtime activation, live preterminal report
validation, real browser navigation and provider qualification.

## Canonical history and read-only saved display — accepted, 2026-10-07

`wave_reviewer` independently accepted the bounded T024/T025 and saved-display
T028/T029 increment with no remaining actionable blocker. Review covered unchanged
owner result/codec handoff, no consumer history repair or transcript serializer,
complete supplied lineage at fresh handoff, original Prepare-anchored deadline,
atomic operation start/seal and retained actual task settlement, exact empty
OwnerLost/authorization closure shapes, and fresh saved VMs without stream replay.
Actual owner projections and orphan persistence are exercised separately from the
read-only consumer, whose lifecycle/write calls remain forbidden by its test wrapper.

Root verified **17/17 focused** and **444/444 affected Chat/page** tests with zero
failures/skips; the reviewer confirmed these satisfy the acceptance condition.
Evidence is in quickstart.md. This accepts consumer/leaf behavior only: root
initial-union/admission, Coordinator callsite/parent-page composition, retry controls,
PostgreSQL, actual navigation and live provider execution remain outside the increment.
