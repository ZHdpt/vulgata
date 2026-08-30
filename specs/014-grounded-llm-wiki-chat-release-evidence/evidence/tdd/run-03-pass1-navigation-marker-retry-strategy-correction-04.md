# Run-03 Pass 1 navigation-marker retry-strategy correction 04

## Observed production failure

Run-03 reached the real configured provider and exercised the bounded navigation
tool loop.  When an in-flight job reached the eight-call limit, the mandatory
durable calls-bound marker attempted a serializable transaction outside the
configured `NpgsqlRetryingExecutionStrategy`.  PostgreSQL rejected the first
query in that transaction.  The hosted runner correctly classified the
unexpected infrastructure exception as `unclassified-systemic` and moved the
pair to `PausedInspection`; no invalid document or provider content was
published.

This is distinct from the earlier ordinal-one `JobTransient` provider-result
rejection.  The marker transaction failure is a production persistence defect;
the provider-result rejection remains honest run evidence.

## RED

Public store seam:
`IPass1NavigationCallsMarkerRepairStore.EnsureNavigationCallsMarkerAsync` with
PostgreSQL 17, `EnableRetryOnFailure(3)`, a current claimed job, and a durable
navigation budget at exactly eight attempts.

Test:
`PostgreSql17_RetryingExecutionStrategy_PersistsNavigationCallsMarkerWithoutTransactionRejectionAsync`.

Result before the production change: `1` failed, `0` passed with the exact
`NpgsqlRetryingExecutionStrategy` user-initiated transaction exception observed
in run-03.

## GREEN

`PreparedPass1Store.EnsureNavigationCallsMarkerAsync` now executes its existing
serializable marker transaction through
`Database.CreateExecutionStrategy().ExecuteAsync`.  Authorization, lease and
state-version checks, the eight-call bound, canonical marker identity,
idempotent replay, and fail-closed behavior are unchanged.

- exact regression: `1/1` passed;
- surrounding `Pass1PairPostgreSqlAtomicTests`: `22/22` passed;
- no EF migration, DbContext model, model snapshot, Story 12.4 contract,
  provider contract, public endpoint, navigation bound, or Scan strategy change.

The exact run-03 database remains durable.  Resume uses the existing production
recovery path; no database row is edited by an operator or test harness.
