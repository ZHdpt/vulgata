# Run-03 Pass 1 navigation reservation retry-strategy correction 06

## Observed production failure

After authenticated recovery re-leased the four expired run-03 jobs, real
provider execution reached the durable `navigate_code` reservation boundary.
`PreparedPass1Store.ReserveNavigationAttemptAsync` opened its serializable
transaction outside the configured `NpgsqlRetryingExecutionStrategy`.
PostgreSQL rejected the first locking query, the reservation remained absent,
and the accepted fail-closed path paused the pair as `PausedInspection`.  No
navigation receipt or partial document was published.

The same durable workflow immediately consumes
`CommitReservedNavigationReceiptAsync` after a successful reservation.  Both
halves of this single reservation/receipt persistence boundary therefore need
the same retry-strategy transaction ownership.

## RED

Public store seams: `ReserveNavigationAttemptAsync` followed by
`CommitReservedNavigationReceiptAsync` with PostgreSQL 17,
`EnableRetryOnFailure(3)`, one current authorized claimed job, and its exact
pair/job/lease binding.

Test:
`PostgreSql17_RetryingExecutionStrategy_ReservesAndCommitsNavigationReceiptWithoutTransactionRejectionAsync`.

Result before the production change: `1` failed, `0` passed.  The exact
exception was the retrying execution strategy rejecting the user-initiated
transaction at `LockReservationOwnersAsync`.

## GREEN

The reservation and reserved-receipt transactions now execute through
`Database.CreateExecutionStrategy().ExecuteAsync`.  Their existing locks,
authorization/currentness checks, ordinal and cursor binding, budget updates,
canonical receipt construction, idempotent replay, rollback behavior, and
serializable isolation are unchanged.

- exact regression: `1/1` passed;
- surrounding retry-strategy and reservation qualification: `7/7` passed;
- Infrastructure build: succeeded with `0` errors;
- `git diff --check`: clean apart from line-ending notices;
- no EF migration, DbContext model, model snapshot, Story 12.4 contract,
  provider contract, public endpoint, or Scan strategy change.

The next action is to restart the bounded host and invoke the same authenticated
production recovery endpoint against the retained run-03 database.
