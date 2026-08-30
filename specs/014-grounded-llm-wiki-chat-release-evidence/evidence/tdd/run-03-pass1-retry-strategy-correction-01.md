# Run-03 Pass 1 retry-strategy integration correction 01

## Observed production failure

The first authenticated run-03 `POST /api/pass1-executions` returned sanitized
HTTP `503` before any Pass 1 pair or job was created. The host log identified
`InvalidOperationException`: the configured `NpgsqlRetryingExecutionStrategy`
does not permit a user-initiated transaction unless the complete transaction is
executed through the context execution strategy.

Provider configuration, encrypted-key presence, System assignment, the exact
two Prepared Scan Manifests, and the absence of a partial Pass 1 execution were
verified independently. No source-bearing provider job was dispatched by the
failed start.

## RED

Public store seam:
`IPreparedPass1Store.StartAsync` with PostgreSQL 17 and
`EnableRetryOnFailure(3)`.

Test:
`PostgreSql17_RetryingExecutionStrategy_StartsPreparedPairWithoutTransactionRejectionAsync`.

Result before the production change: `1` failed, `0` passed. Expected
`Created`; actual `Failed`.

## GREEN

`PreparedPass1Store.StartAsync` now executes its existing serializable start
transaction through `Database.CreateExecutionStrategy().ExecuteAsync`. The
transaction body, authorization scope, handoff validation, canonical identity,
queue construction, state transitions, conflict behavior and cancellation
behavior are unchanged.

- exact regression: `1/1` passed;
- surrounding `Pass1PairPostgreSqlAtomicTests`: `19/19` passed;
- no EF migration, DbContext model, model snapshot, Story 12.4 contract, Scan
  strategy, provider contract or public endpoint change;
- the final run-03 database was not repaired or directly mutated.

The verified run-03 host was stopped only to release build outputs. The
Prepared Scan tuple and two Manifests remain the next production resume input.
