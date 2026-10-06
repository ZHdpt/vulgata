# Independent OCT-SCAN plan review

Date: 2026-10-06
Decision: **ACCEPT — planning boundary only**
Reviewer: independent_plan_review (independent delegated reviewer; not the definition author).

## Reviewed boundary

Reviewed spec.md, plan.md, research.md, data-model.md, contracts/scan-boundaries.md and quickstart.md against the assigned checkout's October delivery and integration plans, Scan and Business briefs, compatible October proposal authority deltas, constitution 2.0.0, architecture gateway/spine, affected PRD requirements and testing guidance. Inspected actual generator session lifetime, dedicated JDT entry/lifecycle and CodeGraph descriptor/session limits. Verified actual branch oct14/scan and successful fad7f11 ancestry check. No other worktree, skill, runtime, provider or database was used.

## Findings and correction

**R1 — resolved before acceptance:** the first contract draft prohibited every new call after any stop, while the plan required graceful-stop Business finalization. The corrected contract explicitly separates first graceful stop (no new file workstreams; bounded active drain and post-freeze finalization within original authority/time/cost) from hard cancellation/revocation (no new calls and cancel/fence owned work). The plan matches this distinction. Include both cases in future implementation verification.

The definition author also aligned the proposed Generate selection with the actual environment-backed October:Mode parser, added the proposed October:ScanPolicyFile selector and explicit exit semantics. These are marked planned, preserve Prepare/Select and avoid inventing an existing CLI syntax.

No unresolved design finding blocks this planning boundary.

## Acceptance rationale

- The feature is the selected first deliverable. It does not resume original governed stories or claim their broader criteria. Its scope/deferred work and PRD dispositions remain explicit.
- Scan owns deterministic process-local orchestration, real MAF execution, tool integration and generator lifetime. Business retains the single Knowledge contract/store/migration writer; integration retains shared Web composition. Shared project/package/test inventory edits require their owner.
- Trusted completion binds the assembled proposal and current attempt; the common gate spans acceptance transaction and stop fencing. Replay/conflict and exact acknowledgement reconciliation do not rerun inference. Indeterminate writes block freeze; explicit terminal coverage and exact handles precede finalization.
- C0 is an honest pending dependency with concrete semantics and fixture observations, not a claimed existing API. Owner-selected implementation details can be reconciled there. Missing or incompatible C0 stops consumer implementation rather than licensing private DTOs or persistence.
- The proposed tool design addresses actual lifetime limitations: short scoped source contexts/cursors, returned native descriptor, bounded serialized same-process sessions, positively identified tool-state exclusion, contained excerpt verification and dedicated JDT windows. The optional Generate-only 120-second JDT grant is a bounded proposal requiring approval and qualification, not an existing qualified permission or global limit change.
- S1 tool composition, S2 one real accepted file, S3 connected batch and integration handoff have meaningful prerequisites and separately classified evidence. Combined runtime/usefulness claims cannot be obtained from fixture/JSON/static evidence.
- Explicit budgets, live authority, no implicit provider failover, fixed publication identity and fail-closed interruption preserve compatible integrity/security rules. First graceful stop does not grant additional budget.

## Dependencies and required later decisions

1. Business C0 integration commit, actual shared symbols, atomic acceptance/lookup/freeze/finalization and authorized exact reads. The completion/fence bridge and single-pool callback must be proved at this seam before dependent implementation.
2. Approved owned native/JDK/source profile; S1 must prove useful combined lifetime, paging, state coexistence and cleanup before provider work. Failure is a stop condition, not permission to weaken containment or drop CodeGraph.
3. Scoped provider spending and finite ceilings, owned PostgreSQL target, and live accounts/configuration. No runtime authorization follows from this review.
4. Business real publication and Explore/integration composition for ordinary-reader and complete release evidence.
5. Explicit post-plan user approval is triggered by materially changed Worker tools and Scan strategy. The approval must identify the plan's tool/lifetime changes, admission/scheduling/retry/completion/stop strategy and acknowledge the stated bounded October PRD dispositions. Any further PRD departure requires its own explicit disposition. This review does not supply that approval or task/implementation authority.

This acceptance covers the current proposed design and its explicit implementation holds. It is not C0 approval, native qualification, a runtime pass, acceptance of Knowledge content or release completion. Stop before task generation and implementation as requested.

## Reviewed artifact SHA-256
- spec.md: BF8F8AA6A75FE92D330E50972180136F09048F9C68185A458E80C94F90F781B2
- plan.md: EE9628FAAD09EEEDC44728FA06C611B8A9A7279F409946F530CBFDE9375FCC00
- research.md: ECFF187472E937E12B0258D5FE54BCD12176C66BEED84523E43299D74E42C198
- data-model.md: B68A4E052E70872C6604668703414E52F1F1E394C84597A517F7D0516D2F5F2E
- contracts/scan-boundaries.md: 361A2D2FF3B1E78F219E53BCFDCCAECA5EDFFF49FB0FE636ED60226245E9F605
- quickstart.md: D91FF6C586E84AF0F779B836CC2980FEEC50E4C00A69045800C53D59062A0E8F


## Focused correction review — 2026-10-06

Decision: **ACCEPT F5 planning correction; F1/F2 alignment holds remain open.**

This appended decision preserves the original review above verbatim and supersedes any inference that its initial acceptance establishes current cross-lane agreement. Scope: the seven local definition artifacts hashed below; read-only comparison with the explicitly named Business knowledge-contract.md and fixture-cases.md. No other skills, tasks, implementation, runtime, contract compilation or commits were performed.

### F5 assessment

The required outputStage=cl-only|cl-and-bl choice is pinned in the existing scan-policy file and rejects absent/unknown values before dispatch. It adds no competing configuration path or default downgrade. S2 has a usable complete route: real Worker analysis, settled atomic acceptance, Business B0 fixed CL publication, exact owner-returned handles and ordinary permitted IT reading. B1/B2 production services are neither resolved nor called for this route; absent-service/zero-call probes explicitly test that boundary. Shared later-stage type declarations do not imply a runtime dependency on their implementations.

The outcome distinguishes requested/completed stage, selected-scope coverage and confirmed publication. CL-only success cannot assert BL, S3 or release completion. Accepted rows without confirmed publication are not success; zero-unit-only/no-CL results are no-content. Ambiguous publication acknowledgement retains the original operation identity and uses exact lookup/replay without re-analysis, a fresh publication key or manual SQL. Business Mode remains safely empty/unavailable for CL content. S3 explicitly requests CL-and-BL, cannot silently downgrade, and returns a separate immutable publication rather than modifying B0 or automatically retargeting readers.

These choices agree with the Business counterpart's Fixed publication/F5 section and its separate-publication fixture case. Verification remains prospective: source-grounded content, PostgreSQL atomicity and ordinary-reader authorization require their stated runtime evidence. **No actionable F5 correction remains.**

### F1/F2 boundary of this decision

The local corrections correctly identify seal -> drain-expiry fence -> transaction starts as an unresolved alignment counterexample, distinguish completion evidence from settled acceptance, retain tracking of potentially committing operations and prohibit freeze while outcomes are unknown. They also require shared live execution admission/accounting/cancellation rather than copies of numeric limits, with relevant concurrency, retry, queued cancellation, admission-to-send and permit-cleanup counterexamples.

At review time the named Business knowledge-contract.md already contains newly visible F1 commit-submission-guard and F2 execution-scope proposals. This observation is not a stable checkpoint delivery, adoption, approval or consumer concurrence. The local references to the earlier Business v1 seal describe the discrepancy under correction; they must not be treated as a description of the newest owner proposal. This focused review does not choose either final mechanism or close F1/F2. The next alignment pass must bind the stable owner revision/hash, compare these counterexamples against it, and record explicit adoption or actionable objection without defining consumer replacement types.

F1 alignment remains required before S2 acceptance writes; F2 alignment remains required before dependent Business model dispatch. The common C0/CP-K1 checkpoint and its controlled conformance probes remain prerequisites under the plan's implementation holds. S2's independence from B1/B2 does not waive shared contract conformance or authorize implementation while the current holds remain.

The existing tool/Scan planned-change approval, bounded October PRD dispositions, implementation authority, owned-resource permissions and native qualification conditions remain in force. Stop before tasks and implementation.

### Focused reviewed artifact SHA-256
- spec.md: CAE95A289DA1FBC100D7305BFF260187264FCA1DFB5E1BF0BE9E78900BE299B2
- plan.md: 5614BB2E8C2F4C8991EDE45F99BBEA54640E42B643695F0A371B4341D4BAA4AA
- research.md: 834E5A586CCD78743705D73AF0439DA01B2F683D128710BD88349E820A3E31B4
- data-model.md: 6FCCDD31BB289B58E7C9320DD60D4D129062AE67934A7EC0BCB8D9C91591B11B
- contracts/scan-boundaries.md: A64DD191D0BA966D26956653C3DA4892B6D480AF81FEA8D185EF75D840CE1DB1
- quickstart.md: A79CBBA1B853F2C50791987FE416615024F856F7C909564BA006DE4F54E3D768
- checklists/requirements.md: D1ACAE91076D5702EDB6AC8F8935C6A3BC417E6611CED94A8BBD13619DFE99AB


## Focused second-pass alignment review — 2026-10-06

Decision: **ACCEPT F1/F2/F5 planning-semantic alignment. No remaining semantic objection against the pinned canonical contract.**

Canonical authority inspected read-only: C:/Users/13728/.codex/worktrees/f081/vulgata/specs/20-1-Oct14-business/contracts/knowledge-contract.md.

Verified SHA-256: F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36.

Reviewed the current seven local artifacts as one definition, with the authorized Business plan, data model, fixture cases and quickstart supporting the canonical contract. This appended decision closes the earlier F1/F2 semantic alignment holds at this exact boundary; it preserves their history and does not retrospectively change either earlier review.

### Agreement and counterexample assessment

- **F1:** the seal proves completed analysis only. Scan owns separate irreversible eligibility/fence, monotonic drain/run deadlines and hard stop. Entry validation plus the shared final commit-submission guard preserves the canonical ordering. Seal -> drain expiry/fence -> transaction entry is rejected before entry; expiry during staging prevents submission. Delayed timers do not extend eligibility. Already-submitted commits may settle after cancellation: exact lookup and confirmed settlement, not cancellation or a temporarily absent row, determine their outcome. Indeterminate operations prevent freeze/replacement/success. Fresh authority gates disclosure. New publication uses the same phase-eligible rules. The plan, contract, model, specification and prospective probes agree on these meanings.
- **Stop distinctions:** first graceful stop closes new file work without itself fencing active eligible attempts. Drain expiry fences file attempts; it does not manufacture more time/cost or cancel a separately eligible finalization phase. Hard stop and authority failure bar new calls/publication and cancel owned work, while preserving and settling already-submitted operations. A possible writer cannot be prematurely labelled a stopped gap; hard stop never resumes finalization because a pending commit later succeeds.
- **F2:** every Business provider attempt/retry uses the Scan-owned process-local shared cap, host request/rate admission, remaining budgets, monotonic deadline, cancellation and accounting scope. Grant/send checks and queued rate waits retain original bounds. Parent finalization holds no permit while children wait, avoiding cap=1 deadlock. Actual or explicit unknown usage settles exactly once; unknown retains conservative reservation or blocks further constrained calls. Permit release waits for transport settlement/confirmed disposal; unconfirmed cancellation stops dispatch without false capacity reclamation. No independent Business pool/limiter or cross-process Web permit service is introduced.
- **F5:** the required pinned existing-policy outputStage remains explicit. S2 uses B0 acceptance -> fixed CL publication -> exact handles -> ordinary IT reading with no B1/B2 runtime dependency or Business provider calls. Stage-labelled success, partial/no-content/indeterminate outcomes and publication acknowledgement recovery remain truthful. S3 requests CL-and-BL explicitly and creates a separate immutable publication; no downgrade, automatic retargeting or implicit resume is added.
- **CP-K1/C0:** one initial owner-published compiling contract/labelled fixture commit enables minimal consumer compile/conformance probes before dependent production. Completed consumer implementations are not probe prerequisites. Acceptance of the exact compiled checkpoint follows resolved probe objections. This resolves the circular-gate concern without claiming that Markdown concurrence satisfies compilation or authorizes code.

No actionable contradiction remains in this focused planning review. Proposed symbol names remain owner-published contract work, not a competing consumer API. The Business document's statement that consumer concurrence was pending records its earlier local perspective; this review supplies Scan-side planning concurrence only and does not edit or imply an updated counterpart record.

### Remaining boundaries

The shared compiled checkpoint and conformance evidence do not yet exist in this reviewed boundary. Their authorized initial contract/fixture/probe work, dependent production approval, explicit tool/Scan planned-change approval and bounded October PRD dispositions, scoped resources, native qualification and real provider/content/integration evidence remain required. This decision authorizes none of them. No new skill/workflow, tasks, implementation, runtime, tests or commits were performed. Stop at planning.

### Second-pass reviewed artifact SHA-256
- spec.md: F61DF2A3BC2844C6568727B46339FE4D4601CEF67348D04A9C9F90F14A72CDCD
- plan.md: 8D3EFE4B840715DCD76C86CED5D96DA7A07B984B0A0D91ADCB20A0C7C613B812
- research.md: 9E7EE242F87831F1D9E49D0C00AC8B2EE3ECA0D6FD20CA74A26D876DC4E45A73
- data-model.md: D15948DA76146727302178DBAD658064150E7C51446355698CC386284A536204
- contracts/scan-boundaries.md: 4DCB80F6E7CF1140E7A9292B43C1960DD3D1774395E13CBFE67FB08377C9679D
- quickstart.md: DC861F4B200334439FF8435EFFC6611D1C5052593D1BE32776B5744CCB3EBAC2
- checklists/requirements.md: 6DCA729C0DD4082BE02C2A5A0341B71887C4DAAB1DCE9E2A10EEED22588E2490

