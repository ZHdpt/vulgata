# Contract: Result Commit and Scan Lifecycle

## Port

```text
CommitAsync(packet, validResult, repositoryAccessScope, cancellationToken)
  -> Committed | Conflict | Rejected | Cancelled | Failed
```

The port accepts only a valid envelope whose variant and every identity match
the dispatched packet. Infrastructure rechecks scope and materializes exactly
one existing `KnowledgeCommit`; `IKnowledgeStoreRepository` remains the sole
atomic Document write boundary.

For Pass 1, Source Anchors must stay inside the dispatched slice and reference
targets must be same-Repository allowlist members. For Pass 2, the
`GENERATED_FROM` target set must equal the exact Code Logic Document Versions
used for synthesis, and prepared flow evidence must match the manifest. Pass 2
cannot materialize a base Code Logic `REFERENCES` Edge.

## Ordering

```text
dispatch valid
-> await KnowledgeCommit outcome
-> only Committed permits durable valid-result/progress update
```

Conflict, rejection, cancellation, or failure records no valid job/progress and
cannot advance a phase. Identical replay uses stable identifiers and the
existing idempotent knowledge-store behavior. If knowledge commit succeeds but
progress persistence fails, re-entry repeats the identical commit before
retrying progress; no duplicate logical truth is created.

## F003 gate

Before production composition, SQLite must reserve its relational write lock
inside a serializable transaction with the accepted parameterized no-op owner-
row update before reading/revalidating state. It never emits `FOR UPDATE`.
PostgreSQL retains row locking. A real two-context SQLite regression is a
production-DI precondition.
