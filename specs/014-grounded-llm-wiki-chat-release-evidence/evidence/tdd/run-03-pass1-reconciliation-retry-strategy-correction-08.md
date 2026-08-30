# Run 03 Pass 1 reconciliation retry-strategy correction 08

- Story: 12.5
- Stage: implement / T036 real Pass 1 continuation
- Decision: `story-12-5-run-03-all-remaining-tasks-approval-02`
- Observed at: `2026-08-30T21:01:36.4213634Z`
- Retained pair: `8a5b8a31-bd93-8558-9cb4-efc137db4ada`
- Guarded database: `vulgata_story125_b527027ad0dc4e359070f26498817c3c`

## Observed production failure

The retained production pair reached a fresh terminal exhaustion with 150
durable jobs: 148 completed Documents, two bounded ordinary `JobTransient`
failures, and no pending, running, or retry-scheduled job. The pair nevertheless
remained `Running` at state version 10. An authenticated replay returned the same
terminal counts and did not advance the phase.

Source inspection confirmed that the runner intentionally uses
`IPreparedPass1Store.ReconcileAsync` as the sole normal terminal transaction.
That transaction creates the `Pass1Outcome`, advances the two child executions
and coordination, and publishes `ReadyForResolution`. It opened its serializable
transaction directly, outside EF Core's configured retrying execution strategy.
The resulting `InvalidOperationException` was converted to the public
`Failed` reconciliation outcome, leaving all prior durable work intact but the
pair unable to advance.

## Honest RED

Added the public PostgreSQL regression
`PostgreSql17_RetryingExecutionStrategy_ReconcilesTerminalPairWithoutTransactionRejectionAsync`.
It creates a strict disposable PostgreSQL database, prepares a terminal pair,
constructs the concrete store with `EnableRetryOnFailure(3)`, and invokes the
public reconciliation seam.

Observed result before the production change:

- expected `Reconciled`;
- actual `Failed`;
- 1 test failed, 0 passed.

One earlier surrounding-suite invocation used the wrong documented
administrator variable and produced 19 setup failures before any product
behavior ran. It was recorded as an operator-environment error, corrected by
setting the suite's exact `VULGATA_TEST_POSTGRES_ADMIN` variable, and is not a
product or convergence failure.

## Minimal GREEN

`PreparedPass1Store.ReconcileAsync` now executes its existing complete
serializable transaction through `Database.CreateExecutionStrategy()`. The
transaction contents, authorization, currentness, canonical reconciliation,
failure budget, outcome, child execution, coordination, Edge, and atomic
publication rules are unchanged. No second completion path was introduced.

Verification:

- exact retry-strategy regression: 1 passed, 0 failed;
- complete `Pass1DocumentPostgreSqlAtomicTests`: 72 passed, 0 failed;
- `Pass1ProductionExecutionCapacityTests`: 19 passed, 0 failed;
- `git diff --check`: clean (line-ending notices only).

## Boundary confirmation

- no EF migration, `VulgataDbContext`, configuration, or model-snapshot change;
- no Story 12.4 contract or accepted Track A/Track B behavior change;
- no source, provider body, secret, connection string, or retained database
  content was recorded;
- no direct database repair was performed;
- the next action is to restart the guarded host and replay the authenticated
  production activation so this same terminal pair can reconcile normally.
