# Story 12.5 Track B Convergence Correction 14

- Starting checkpoint: `ca102fb21a7aeeb02dbd8bd931e71b8778c4797a`
- Task: `T080`
- Source finding: convergence attempt 8, `QR-TB-004` partial
- Continuation counter before the next convergence pass: 2 of 3

## TDD evidence

- RED: `AskAsync_DeadlineExpiresDuringSuccessPublication_ReturnsTimedOutWithoutAppendingConversationTurn` advanced the fake clock through the public completion-observation seam after the earlier check; production returned `Succeeded` and carried the appended turn into the next request.
- RED: `ChatPage_AcceptedUnicodeReveal_UsesLiveAbsoluteDeadlineAfterRevealStartsAsync` advanced the shared fake clock after progressive Unicode reveal began; the page remained in `revealing` state at expiry.
- GREEN: completion-observation data is prepared before the final deadline check; a proposed conversation state remains unassigned until the final live clock/token linearization point; expired publication returns `TimedOut` with no Answer or appended turn.
- GREEN: a successful `PreparedChatResult` carries the absolute deadline. Chat Page monitors that deadline with the coordinator-owned `TimeProvider`; internal expiry completes the already accepted Unicode-safe answer immediately, while caller cancellation remains distinct.
- Grounded fallback paths retain their prior single completion-summary behavior and do not repeat runtime-observation reads. Accepted-duration calculation uses the remaining budget captured at the publication point and performs no post-linearization clock read.

## Verification

- Exact T080 tests: 2 passed, 0 failed.
- `PreparedChatCoordinatorTests`, `ChatPageTests`, and Chat Turn coverage: 77 passed, 0 failed.
- Web build: 0 errors; 19 pre-existing package/version warnings.
- `git diff --check`: passed, with line-ending notices only.
- No EF migration, `VulgataDbContext`, model snapshot, Story 12.4 contract, provider-streaming trust boundary, or Track C artifact changed.

Track B convergence must rerun. If that pass is non-converged, it is the third failure under `story-12-5-convergence-continuation-03` and the workflow must pause for the next user approval.
