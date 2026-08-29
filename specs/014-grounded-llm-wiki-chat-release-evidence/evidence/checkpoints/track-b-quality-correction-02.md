# Story 12.5 Track B Quality Correction 02

- Starting checkpoint: `8c5ce6c6509723664754a10e41e1212c1712169c`
- Quality finding: `QR-TB-004`
- Task: `T079`
- Scope: final 29-second deadline enforcement immediately before conversation append/result publication, and deadline-aware Unicode-safe accepted-answer reveal.

## TDD evidence

- RED: `AskAsync_FinalPublicationBudgetExpiry_ReturnsTimedOutWithoutAppendingConversationTurn` returned `Succeeded` instead of `TimedOut` when the fake clock expired on the final remaining-budget read.
- RED: `ChatPage_AcceptedLongUnicodeAnswerWithBudgetBelowRevealCost_RendersImmediatelyAsync` entered progressive reveal when 1,500 ms remained but 80 Unicode text elements required 1,975 ms of configured reveal delays.
- GREEN: the coordinator computes and rejects a non-positive final budget before synchronous conversation append and performs a final cancellation check immediately before append.
- GREEN: the page compares the complete Unicode text-element delay cost with the remaining shared deadline and renders the already accepted answer immediately when progressive reveal cannot fit.
- The initial page fixture was corrected from an irrelevant repeated token to a lexically relevant 80-element claim; retrieval/ranking behavior was not weakened.

## Verification

- Exact T079 tests: 2 passed, 0 failed.
- `PreparedChatCoordinatorTests` plus `ChatPageTests`: 72 passed, 0 failed.
- Web build: 0 errors; 19 pre-existing package/version warnings.
- `git diff --check`: passed, with line-ending notices only.
- No EF migration, `VulgataDbContext`, model snapshot, Story 12.4 contract, provider-streaming trust boundary, or Track C artifact changed.

## Residual boundary

A deadline or cancellation can occur in the tiny synchronous interval after the final clock/token checks and before in-memory append/return. Eliminating that interval would require an atomic publication primitive and is outside the accepted T079 correction. The deterministic final-clock-read defect identified by `QR-TB-004` is closed.

Track B convergence and independent quality review must rerun. Track C remains closed until all Track B gates and its complete checkpoint are accepted.
