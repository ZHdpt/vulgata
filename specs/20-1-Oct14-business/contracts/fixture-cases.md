# Shared labelled fixture cases (design only)

All records carry `ControlledFixture`, never generated-source provenance. Future fixture/probe code belongs under the single test project and must be classified in suites.json. Minimal compile/conformance probes validate an initial owner-published contract/fixture commit before dependent production is enabled; completed consumer implementations are not required. This Markdown catalogue does not satisfy CP-K1 compilation or runtime verification.

Representative graph: CL-A (root operation in Repository A) REFERENCES CL-B (provider in Repository B), supported by exact source-shaped binding evidence; CL-A also has an unresolved target U1. BL-A explains that rooted flow and pins CL-A/CL-B and the selected evidence. CL-C in Repository C is disallowed in the A/B read scope; BL-hidden cites CL-A publicly but its prose/input ledger also depends on CL-C. A second root A2 shares CL-B but owns BL-A2. Distinct roots must not merge. Names are illustrative, never eligibility constants. Add standalone/local-only and three-Repository variants.

| Case | Input | Required outcome |
| --- | --- | --- |
| Full acceptance | Two units, nested/shared valid anchors, observations and coverage in a trusted completed proposal | One whole manifest, exact derived handles, invisible before publication. |
| Zero units | Trusted complete file with explicit no-unit disposition | Accepted coverage, no fake Document. |
| Forgery/partial/stop | Worker Complete flag, mismatched digest, pending tool, effective fence/deadline before commit submission, including after completion seal | No accepted Knowledge. |
| Replay/race | Identical operation concurrently, then same key with changed payload | One durable result; replay same handles; changed payload Conflict. |
| Acknowledgement loss | Commit succeeds and reply is lost; lookup succeeds/fails | Exact result / CommitIndeterminate; no provider work. |
| Publication / F5 | Settled nonempty CL-only set, empty relationship/flow/BL sets, no B1/B2 services or Business provider; then separate CL+BL selection | Early exact readable CL publication succeeds with honest coverage and zero Business provider calls. Later publication is a different immutable ID; no latest fallback or exposed drafts. Zero-unit-only cannot claim readable CL. |
| BL atomicity | Failure between dependency/content/membership writes | No partial BL publication. Prior publication intact. |
| Ordinary reader | Live User, not operator, exact configured selection | Permitted IT/BL reads succeed without management gate. |
| Business exclusion | CL-only corpus; mixed corpus; related GENERATED_FROM; exact CL handle | Empty CL-only page with no cursor/count, BL-only safe metadata, unavailable CL handle. |
| Composite denial | BL-hidden public citation is allowed, uncited CL-C input is denied | Entire dependent BL hidden including titles/counts/footer/graph metadata. |
| Revocation/outage | Role removed, lockout/stamp changed, contributor deleted/moved, authority unavailable between calls or during materialization | Subsequent disclosure fails closed; no partially authorized response. |
| Exact identity | Wrong DB/batch/publication/section; unpublished version | No fallback or foreign metadata. |
| Pagination | Multiple pages/chunks with matching/tampered/expired tokens, hidden items interleaved | Bounded exact results; valid UTF-8; no hidden totals or hidden-only continuation. |
| Resolution | Supported pair, lone candidate, ambiguous namespace, opposing evidence, unknown mechanism | Only positive-evidence qualified REFERENCES accepted; rest explicit Uncertainty/limitations. |
| Flow/BL | Two roots share helper, local-only root, directed cycle, supplementary accepted CL input | Distinct Logical Flows, finite slices/frontiers, all actually supplied evidence in dependencies. |
| Qualifications | CL source proves lock acquisition; advisory text claims a motive/duration; BL and search excerpt mix both | Source fact stays distinct from attributed advisory/hypothesis; split/qualify mixed groups through BL and consumer results. No upgrade by acceptance. |
| Graceful stop / F1 | Accepted file, still-eligible active file inside drain, cancelled active file and undispatched file | First stop closes admission but allows bounded drain; expiry fences attempts. Settle submitted writes, record gaps, freeze exact subset. Paused/indeterminate prevents freeze. |
| Seal-expiry counterexample / F1 | Seal completion; expire drain/install fence; then call writer | Reject before transaction, zero accepted rows. Separately stage transaction before expiry, then release commit barrier after expiry: roll back and submit no commit. Delayed timer callback cannot bypass clock check. |
| Submitted commit / F1 | Hold acknowledgement after commit submission; fence/hard-stop/revoke; vary commit/rollback and lookup availability | Do not presume rollback or disclose after authority loss. Exact settled result or CommitIndeterminate; temporarily absent row during pending commit cannot release freeze. Preserve known committed history. |
| Scope cap / F2 | Host cap=1, two concurrent matching inquiries (repeat inquiry+synthesis), controlled delayed provider | Exactly one active attempt, second queued; orchestration holds no outer permit; shared allowance decreases, each attempt accounts/releases once. No independent Business pool. |
| Scope hard stop / F2 | One active and one queued call; hard stop, late response, attempted retry and unknown usage | Active cancellation signalled, queued/new/retry calls never dispatch, late proposal cannot newly publish. Report actual/unknown, no zero substitution; no permit release until transport settled/disposed. |
| Scope budget/rate / F2 | Missing scope, exhausted/deadline-denied scope, host rate-delayed/denied call, retry after transient failure | No provider send before host rate admission; queued rate wait retains original deadline and rate denial/expiry sends nothing. No dispatch without grant; retry reacquires from same remaining budget/rate allowance, no local/reset limiter. Conservative reservation for unknown usage or block further constrained calls; no budget reset. |
| Chat storage | Duplicate/conflicting outcome, active-turn race, partial result and exact references | Atomic idempotent save/lookup; one active turn; partial is not completed history. |
| Logs | Concurrent append, failed append, redaction-sensitive inputs | Sanitized serialized log.md; LogUnavailable doesn't erase committed result. |

Real qualification replaces these labelled records with real admitted file output and source-checked evidence. Assertions remain the same; claims change only when actual runtime evidence exists.



