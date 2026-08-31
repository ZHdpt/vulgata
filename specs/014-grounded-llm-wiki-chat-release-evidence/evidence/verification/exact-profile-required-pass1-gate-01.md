# T042 required exact-profile Pass 1 compatibility

## Unchanged authority

`src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs` was not edited.
Its SHA-256 at verification was:

```text
792A91E2E8BC7AE4348151E9818C30620F0896C8EF11D31527629310008EA33D
```

## PostgreSQL proof

The compatibility slice adds score-1,100 Manifest entries through the existing
canonical package/store path and exercises the unchanged direct-score `>=400`
and top-20-per-Manifest policy.

- 40 high-value seeds were counted across 50 jobs.
- One required high-value failure produced `FailedPass1`.
- `LaterPassAuthorized` remained false.
- Zero Cross-Repository Resolution Decisions and zero Pass 2 executions were
  created.

Focused PostgreSQL result: **25 passed, 0 failed, 0 skipped**.

No Worker, hosted-service, retry/reconciliation, Pass 1 policy, production,
EF/model, Story 12.4, workflow, staging, or database authority was changed.
