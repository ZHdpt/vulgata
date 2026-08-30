# Run-03 Pass 1 failure-recording retry-strategy correction 03

## Observed production failure

The first four run-03 jobs were claimed, but durable packet hydration rejected
them because the freshly bootstrapped System had no ordinary System Owner
assignment. When the hosted dispatcher attempted to record that fail-closed
outcome, `PreparedPass1Store.RecordFailureAsync` returned `Failed`: its locking
transaction was outside the configured `NpgsqlRetryingExecutionStrategy`.
No provider request or source disclosure occurred. The four leases remain
durable and recoverable; no database repair was performed.

## RED

Public store seam: `IPreparedPass1Store.RecordFailureAsync` with PostgreSQL 17
and `EnableRetryOnFailure(3)` for a currently claimed job.

Test:
`PostgreSql17_RetryingExecutionStrategy_RecordsClaimFailureWithoutTransactionRejectionAsync`.

Result before the production change: `1` failed, `0` passed. Expected
`Recorded`; actual `Failed`.

## GREEN

`PreparedPass1Store.RecordFailureAsync` now executes its existing serializable
failure transaction through `Database.CreateExecutionStrategy().ExecuteAsync`.
Failure classification, lease/version checks, retry policy, circuit behavior,
ordinary-failure budget and fail-closed transitions are unchanged.

- exact regression: `1/1` passed;
- surrounding `Pass1PairPostgreSqlAtomicTests`: `21/21` passed;
- no EF migration, DbContext model, model snapshot, Story 12.4 contract,
  provider contract, public endpoint or Scan strategy change.

The missing ordinary System Owner is a production-bootstrap prerequisite, not
a database or source mutation. It will be created and assigned through the
existing authenticated registration and System ownership behavior before the
same durable pair is resumed after lease expiry.
