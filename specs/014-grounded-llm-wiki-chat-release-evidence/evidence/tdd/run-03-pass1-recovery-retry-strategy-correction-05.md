# Run-03 Pass 1 recovery retry-strategy correction 05

## Observed production failure

After the navigation calls-bound marker correction, the authenticated production
resume endpoint attempted to recover the exact durable run-03 pair from
`PausedInspection`.  `PreparedPass1Store.RecoverAsync` opened its serializable
transaction outside the configured `NpgsqlRetryingExecutionStrategy`; PostgreSQL
rejected its first locking query and the endpoint returned the existing
sanitized `503 pass1-execution-unavailable` response.  The guarded database and
paused pair were unchanged.

## RED

Public store seam: `IPreparedPass1Store.RecoverAsync` with PostgreSQL 17,
`EnableRetryOnFailure(3)`, a current authorized exactly-two-Repository pair, and
provider availability confirmed.

Test:
`PostgreSql17_RetryingExecutionStrategy_RecoversPreparedPairWithoutTransactionRejectionAsync`.

Result before the production change: `1` failed, `0` passed.  Expected an
accepted recovery outcome; actual `Failed`, matching the production resume
failure.

## GREEN

`PreparedPass1Store.RecoverAsync` now executes its existing serializable
recovery transaction through `Database.CreateExecutionStrategy().ExecuteAsync`.
Pair reconstruction, authorization, exact binding checks, expired-lease and
due-retry recovery, manual pause/resume semantics, provider circuit behavior,
and fail-closed integrity transitions are unchanged.

- exact regression: `1/1` passed;
- surrounding `Pass1PairPostgreSqlAtomicTests`: `23/23` passed;
- Infrastructure build: succeeded with `0` errors;
- no EF migration, DbContext model, model snapshot, Story 12.4 contract,
  provider contract, public endpoint, or Scan strategy change.

The next action is to restart the bounded host and invoke the same authenticated
production resume endpoint against the retained run-03 database.
