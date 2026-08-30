# Run-03 Pass 1 claim retry-strategy integration correction 02

## Observed production failure

After the accepted Pass 1 pair and its 150 jobs were durably created, the
run-03 hosted dispatcher left every job pending. The sanitized host log showed
that `PreparedPass1Store.ClaimNextAsync` began a user transaction and then
executed its locking query outside the configured
`NpgsqlRetryingExecutionStrategy`. No provider request was sent and the durable
queue remained intact.

## RED

Public store seam: `IPreparedPass1Store.ClaimNextAsync` with PostgreSQL 17 and
`EnableRetryOnFailure(3)` after an accepted prepared pair is started.

Test:
`PostgreSql17_RetryingExecutionStrategy_ClaimsPreparedJobWithoutTransactionRejectionAsync`.

Result before the production change: `1` failed, `0` passed. Expected
`Claimed`; actual `Failed`.

## GREEN

`PreparedPass1Store.ClaimNextAsync` now executes its existing read-committed
claim transaction through `Database.CreateExecutionStrategy().ExecuteAsync`.
The locking, eligibility, lease recovery, deterministic ordering, concurrency,
state transition, cancellation and result behavior are unchanged.

- exact regression: `1/1` passed;
- surrounding `Pass1PairPostgreSqlAtomicTests`: `20/20` passed;
- no EF migration, DbContext model, model snapshot, Story 12.4 contract, Scan
  strategy, provider contract or public endpoint change;
- the final run-03 database was not repaired or directly mutated.

The verified host was stopped before the correction. The same accepted Pass 1
pair and its intact 150-job queue remain the production resume boundary.
