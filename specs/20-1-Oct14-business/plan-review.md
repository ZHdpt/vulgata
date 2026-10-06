# Independent plan review — OCT-BUSINESS

Date: 2026-10-06. Reviewer: independent subagent `/root/independent_plan_review`.

## Round 1 — REVISE

Reviewed the eight feature artifacts, October delivery/integration/Business brief, compatible October proposal, relevant PRD/architecture/constitution and OctoberDbContext. Independently verified actual branch `oct14/business` and `git merge-base --is-ancestor fad7f11 HEAD` exit 0 in the assigned worktree. No other worktree or skill was used. This is a static design review, not compilation, provider, database or runtime evidence.

1. **Preserve material qualifications at the content boundary.** `spec.md:67,75` and `contracts/knowledge-contract.md:10,19` retain provenance and advisory status but do not require section/statement-group qualifications to survive synthesis and consumer projections. The compatible October proposal `docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md:285-291` explicitly requires this through CL -> BL -> Chat. Add structured source/advisory/hypothesis qualification plus exact references at the relevant section/group, validation and a controlled case proving indirect advisory input cannot become an unqualified source fact. Do not add a sentence-level truth engine.
2. **Make graceful stop an explicit finalization path.** `contracts/knowledge-contract.md:32`, `data-model.md:23` and `plan.md:122` block unfinished roots without explicitly distinguishing operator-stopped inventory from genuinely unsettled/paused work. The proposal at lines 95-103 and 329 requires settling active writes and freezing the accepted partial corpus with unprocessed gaps after operator stop, without an extra threshold. Specify Scan's terminal unprocessed/cancelled dispositions, no late acceptance after freeze, and a stop-with-unprocessed-files case. Still reject incomplete file manifests and indeterminate active writes.
3. **Remove the initial contract/dialogue dependency contradiction.** `plan.md:80` makes Chat dialogue acknowledgement prerequisite to CP-K1, which blocks B0a; `plan.md:94` says missing dialogue agreement does not block B0. Initial CP-K1 should require Chat's shared Knowledge-read/handle acknowledgement; dialogue schema agreement gates only its additive contribution.

No further design blocker identified. The proposed single Knowledge/schema owner, integration-owned shared Web composition, separate live ordinary-reader policy, immutable exact publication, whole-file finality/replay, bounded evidence-based resolution and rooted document-first BL fit the selected first-deliverable scope. Existing coarse roles plus live account/contributor checks are compatible with PRD FR-1.4; per-user Repository entitlement would require a separate policy decision.

## Approval and dependency distinction

After corrections, independent acceptance remains planning acceptance only. The bounded candidate/inquiry/grouping/finalization strategy conservatively triggers explicit Scan-strategy approval before tasks (plan approval assessment; PRD FR-6.2/6.3). No new exposed agent tool or PRD amendment is proposed. New tools or weakened PRD/security requirements would require reassessment. CP-K1 consumer adoption, Scan's trusted completion/inventory, integration's composition, Chat's later schema agreement and owned runtime resources remain implementation dependencies, not reasons to resume original governed stories. No tasks or implementation are authorized by this review.

## Round 2 — PASS (accepted planning boundary)

All three Round 1 findings are resolved in the current eight-artifact boundary:

- `spec.md` FR-015, contract "Claim qualifications and consumer preservation", `QualifiedClaimGroup` and the qualifications fixture preserve material section/group source/advisory/hypothesis distinctions through CL, BL and consumer projections. Structural/reference validation is distinguished from genuine semantic usefulness; no sentence-level truth engine is required.
- FR-016, contract "Fixed publication", CorpusSelection/state rules and graceful-stop fixture explicitly close dispatch, settle commits/ambiguous replies, record undispatched/cancelled gaps and freeze the accepted subset. Paused/unsettled/indeterminate work still blocks freeze; whole-run abort or lost authority does not authorize provider continuation.
- CP-K1 now requires Chat/Explore shared read/handle semantics, while dialogue-specific agreement gates only its additive schema contribution. B0 has no dialogue-schema dependency.

The specification and plan are feasible and sufficiently bounded for subsequent authorized work. No remaining blocking design finding. Ownership, shared interfaces/semantics, small usable increments, verification, deferred work and exact stop conditions are explicit. This accepts the documented approach, not a compiled contract, deployed schema, real provider result or integrated release. No tests or runtime resources were exercised by this review.

Before tasks, explicit user authority and the reported Scan-strategy approval remain required. No new agent-tool exposure or PRD amendment is proposed by this boundary. Shared contract adoption, Scan completion/freeze adapters, ordinary-reader live Identity composition, Explore destination, later Chat schema agreement and explicitly owned runtime targets remain future dependencies. Stop now before task generation and implementation as requested.

### Reviewed artifact SHA-256

Paths below are relative to `specs/20-1-Oct14-business`; `plan-review.md` is excluded. Any later substantive artifact change requires corresponding review before relying on this acceptance.

| Artifact | SHA-256 |
| --- | --- |
| spec.md | 0F6E1820C1493973FFA370A2AECF865BF513692FE539A573A286FCE59F72D4FB |
| plan.md | 6D5F092DB5D91A564E63E36E2D810AFC0C036064BFD060E9B40CDC10DEF688D8 |
| research.md | 83C78F1D614262B9E22610AAD6FA24A485240DFF9F00677381DDC8D96D521D0E |
| data-model.md | 1573F78609F004B02E3423E7E50E23E299DA6C72F35F3E7F4463576ADD5F4C80 |
| quickstart.md | 2355109BCF2A0C982B1A29BFF73D82D1F2EFF7ADB2F1FC787BD97F0213460024 |
| contracts/knowledge-contract.md | 370A4F5DC8393C0BDBED2D286694EF4B0CD7540A91E5E867248472D1D98AA687 |
| contracts/fixture-cases.md | FC9854BA9E45285A547AA9EC5081D04A2BF99E0F6018EB9D5AF22D324C1850BB |
| checklists/requirements.md | B114ECD0034EE907CE325EF2716F282ADCBC75574C54DC7E48FAFAE827EEF0BB |

## Round 3 — PASS: focused F1/F2/F5 planning corrections

Date: 2026-10-06. Same independent reviewer: `/root/independent_plan_review`. Rounds 1/2 and their historical hash table remain unchanged. This round accepts the corrected eight-artifact Business planning boundary below; it does not claim Scan concurrence, CP-K1/C0 completion, implementation or runtime proof.

Reviewed all eight Business artifacts and read only the specifically named Scan counterpart `C:/Users/13728/.codex/worktrees/f8dc/vulgata/specs/20-1-Oct14-scan/contracts/scan-boundaries.md`. Scan currently labels F1/F2 as alignment holds (lines 5, 13-27); its acknowledgement must remain pending. No other checkout was edited. No skills, tasks, production/test code, runtime resources or dialogue expansion were performed.

### Findings and resolution

- **F1 accepted:** contract "Producer-only acceptance and completion" and "F1 — Canonical stop/acceptance ordering and Scan adoption" distinguish immutable completion proof from revocable write eligibility. Seal -> drain expiry/fence -> transaction entry rejects before starting a new write. A fence/deadline during staging rejects at the final shared guard and rolls back without submitting commit. The rule is serialized commit submission, not irrevocable seal issuance. Already-submitted commits may still physically commit after cancellation/observed authority loss; exact settlement/lookup governs the outcome, current authority governs disclosure, and temporary row absence cannot release freeze. Authority outage before submission denies the write; hard stop bars new publication/finalization. The outcome table and deterministic-barrier cases preserve these distinctions.
- **F2 accepted:** contract "Host-supplied execution/admission/accounting scope" requires every provider attempt/retry to consume the same host cap, provider request/rate admission, remaining budgets/deadline and hard-stop signal. There is no outer orchestration permit while awaiting child calls, avoiding cap=1 deadlock. Active/queued hard-stop probes, late-response rejection, exactly-once actual-or-unknown accounting and settlement-before-release are explicit. Unknown usage remains conservatively reserved or blocks further constrained calls. Controls remain process-local; no independent Business pool or cross-process Web permit service is added.
- **F2 clarification resolved during this round:** the first corrected draft omitted explicit host rate admission present in Scan's contract line 25. The final eight artifacts now require rate eligibility at grant/pre-send, original-deadline rate waits, no send after rate denial/expiry and no reset/local Business limiter. The budget/rate fixture and quickstart carry the controlled queued/rate-denied case. No remaining objection on this point.
- **F5 accepted:** "Fixed publication", FR-004 and the publication fixture allow a settled nonempty CL-only selection with empty relationship/flow/BL sets, absent B1/B2 services and zero Business provider calls. Scan owns selecting its early Generate path; zero-unit-only coverage cannot claim a readable CL milestone. Later BL publication has a separate immutable identity. This matches the named Scan CL-only path without claiming its implementation.
- **Checkpoint sequence accepted:** the CP-K1 section and increment allow an initial owner-published contract/fixture commit followed by minimal consumer compilation/conformance probes before dependent production. Completed consumer implementations are not prerequisites. This resolves the circular dependency without authorizing contract/probe code in this planning session; Chat dialogue agreement remains separate.

No remaining blocking local design finding in this focused review. Scan must explicitly adopt the submission-versus-settlement distinction and acknowledge F1/F2/F5 against the stable owner contract/fixture checkpoint; this reviewer does not provide that concurrence. Existing planned Scan-strategy approval and implementation/resource authorization remain separate. No new tool exposure or PRD amendment is proposed by these corrections. Controlled probes, actual PostgreSQL behavior and real provider/source/integrated evidence remain future verification, not results of this review.

### Round 3 reviewed artifact SHA-256

Paths are relative to `specs/20-1-Oct14-business`; the review file is excluded. These hashes identify this corrected acceptance boundary; the Round 2 hashes identify the earlier boundary only.

| Artifact | SHA-256 |
| --- | --- |
| spec.md | 4DE6C3CDDF8BE8932485998D4D75DAB9500A21B4D97A358E130668E3462E8079 |
| plan.md | 40F1B07798105C3D251116ECAC24AB1D75106E7D1CE732D7B29B4B8C32242BDA |
| research.md | F4769A707EF4E6665C0F7E7D34F162E3EA43387490608DF66C853ED10FAA124F |
| data-model.md | 118CD2D926CA25E12C47E783E4C12299CD8D1C5A31CE5A977637004CDD20E1BE |
| quickstart.md | 1AD6D55793E5BCBC32E68C493AFC9CE06853BB7F5EC3F413C020E2421536B54A |
| contracts/knowledge-contract.md | F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36 |
| contracts/fixture-cases.md | C698F6B6C966B06D3C1539D8852A2855B3CE52A0C918CC094C23B4BDE6E28A55 |
| checklists/requirements.md | D27A84AF1DE0C54EFCF46A16DB20FCE39E8C10DB2E84EE2AFFF7A4E1F1B29666 |

## Coordination addendum — Scan planning concurrence received (2026-10-06)

Read-only verification of Scan's `contracts/scan-boundaries.md` (concurrence at line 5) and `plan-review.md` (focused second-pass acceptance at line 82), under `C:/Users/13728/.codex/worktrees/f8dc/vulgata/specs/20-1-Oct14-scan`, confirms explicit F1/F2/F5 planning-semantic adoption and retained independent-review acceptance with no remaining semantic objection. Verified Scan contract SHA-256: `4DCB80F6E7CF1140E7A9292B43C1960DD3D1774395E13CBFE67FB08377C9679D`.

Scan concurs with the unchanged canonical Business contract SHA-256 `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36`: F1 separates completion proof from commit-submission eligibility and settles ambiguous writes before freeze; F2 uses the shared process-local execution/admission/accounting scope; F5 retains the early CL-only path without B1/B2. No substantive contradiction was found. This addendum supersedes earlier pending-concurrence statements as coordination status only; historical reviews, the canonical contract and all eight Round 3 reviewed artifact hashes remain unchanged.

Coordinator chat `01a0fc13-c69b-7543-9e6b-b6ddb3bb015f` also reports its narrow Chat/Explore F3 and Chat F4 checks passed; that is coordinator-reported evidence, not a new Business-side review or change to read/qualification semantics. CP-K1/C0 compiled conformance, planned-change/implementation approval and scoped runtime/resource authorization remain pending. This status-only closure introduces no new review cycle, tasks, code, runtime work, commits or implementation authorization.
