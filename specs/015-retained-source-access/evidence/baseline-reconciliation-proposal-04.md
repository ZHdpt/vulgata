# Bounded capacity-test observation repair

## Decision requested

Approve one additional test-only repair in `tests/Vulgata.Tests/PreparedPass1/Pass1ProductionExecutionCapacityTests.cs`, method `TerminalWorkerResults_StopLaunchAndDrainLocallyBeforeFreshDurableExhaustion`. The exact proposed edit is in `baseline-reconciliation-proposal-04.patch`; it has not been applied.

Replace the fixed 250 ms sleep and one status read with the existing `WaitForDrainedStatusAsync` helper, using its neighboring test's 8-second bound. Preserve the existing 8-second worker-launch bound, dispatched/started counts and `ReadyForResolution` assertion. This increases the terminal-observation allowance from 250 ms to at most 8 seconds; it does not relax the required terminal state or production deadline. No production changes, removed assertions or skips are proposed.

## Evidence and uncertainty

The fresh full suite passed 3127 tests, failed 7 and skipped 15. Four failures were PostgreSQL cleanup timeouts; two were streaming timing assertions. All six passed in the isolated seven-test rerun. Capacity still failed, and a one-test run with collection parallelism disabled again returned `Running` after every worker launched. Raw evidence: `full-suite-failures-03.json`, `full-suite-isolated-diagnosis-03.json`, `capacity-observation-diagnosis-01.json`.

Worker StartedCount increments before it returns its result. Durable reconciliation refuses while a job remains Running and only later commits ReadyForResolution. The adjacent FullyDrainedPair test already polls the same status helper. The failing test and PreparedPass1Store are byte-identical to activation base bff540980800eb67254170a0c82e38678b63576d. These facts support an observation race; they do not yet prove the proposed repair passes or rule out a deeper production issue.

## Scope and gates

Prior proposal03 authorized three named repairs and did not include this capacity test. The current run-bmad-sprint-loop skill requires renewed approval when approved scope changes. Approval here covers only this proposed test edit, focused/neighbor verification and another owned full-suite run. If terminal state still fails after bounded polling, stop this repair and report the actual product or runtime evidence; do not change production under this approval.

Recommend this repair over retrying unchanged or weakening the phase assertion. Declining it leaves full-suite verification incomplete. Convergence, independent quality review, required HTTP E2E, final verification and completion remain mandatory. Story13.2 and historical feature plans remain inactive.
