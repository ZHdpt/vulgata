# Run-03 Pass 1 document commit retry-strategy correction 07

## Observed production failure

The retained run-03 pair resumed after the navigation-reservation correction.
Real provider streaming completed two tool attempts and persisted one bounded
navigation receipt for the newly claimed job.  The accepted result then reached
`Pass1CodeLogicDocumentCommitter`, whose atomic publication transaction was
opened outside the configured `NpgsqlRetryingExecutionStrategy`.

PostgreSQL rejected the first locked pair query with the execution-strategy
user-transaction exception.  The pair failed closed as `PausedInspection`; no
Document, Source Anchor, Document Section, Index Entry, Edge, or completed-job
state was partially published.

## RED

Public seam: `IPass1CodeLogicDocumentCommitter.CommitAsync` with a complete
authorized Pass 1 packet, a PostgreSQL 17 context configured with
`EnableRetryOnFailure(3)`, and the exact durable pair/job/lease bindings.

Test:
`PostgreSql17_RetryingExecutionStrategy_CommitsPass1DocumentWithoutTransactionRejectionAsync`.

Result before the production change: `1` failed, `0` passed.  The exception was
the retrying execution strategy rejecting the user-initiated transaction at the
first `FOR UPDATE` query in `CommitAsync`.

## GREEN

The complete existing document-publication transaction now executes through
`Database.CreateExecutionStrategy().ExecuteAsync`.  Its locks, authorization,
currentness, immutable identity checks, canonical projection, idempotent replay,
conflict behavior, graph writes, job completion, rollback behavior, and atomic
commit remain one retriable unit.

- exact regression: `1/1` passed;
- complete Pass 1 PostgreSQL document atomicity class: `71/71` passed;
- Infrastructure build: succeeded with `0` errors;
- `git diff --check`: clean apart from line-ending notices;
- no EF migration, DbContext model, model snapshot, Story 12.4 contract,
  provider contract, public endpoint, or Scan strategy change.

The next action is to restart the bounded host and invoke the authenticated
recovery endpoint against the retained run-03 database and pair.
