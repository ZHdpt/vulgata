# Quickstart: Validate the Minimal Knowledge Store

Run from repository root after implementation and independent plan acceptance.
These checks validate Story 11.1 only; they do not generate tasks, run Worker
Agents, change Scan behavior, or perform Story 11.5's final demonstration.

## Prerequisites

- .NET 10 SDK;
- PostgreSQL 17 development database for migration commands;
- implementation based on
  `b851ba165d6ebe560227bfdc3b206a814280e9b2`;
- generated migration suffix `AddMinimalKnowledgeStore`;
- explicit user exception authorizing the integration owner—not a Story 11.1
  implementer—to pin `tests/Vulgata.Tests/MigrationTests.cs:142` to
  `20260730083207_AddConservativePreScanGate`;
- no other edit to completed Story 4.3 source, migration, or tests.

Task generation and implementation must not begin until that narrow exception
is recorded.

## 1. Build

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
```

Expected: exit 0 with analyzers and code style enforced.

## 2. Test-first focused groups

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStoreDomainTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStorePersistenceTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStoreRetrievalTests"
```

Expected after recorded transient RED→GREEN:

- every UTF-8/count/path/type/order bound is exact;
- canonical JSON rejects unknown/duplicate fields and every required
  secret/source-text/host-path/raw-diagnostic fixture while accepting the safe
  counterexamples;
- the same closed sensitive-text fixtures are rejected from source-anchor
  evidence locators, Flow Group titles, and Flow Trace Step descriptions with
  only `InvalidContent` returned;
- initial commit is all-or-nothing under injected failures;
- an identical execution-strategy replay returns `Committed`, while a changed
  `OperationId`, anchor, Edge, flow value, or durable projection returns
  `Conflict` because `CommitFingerprint` and the complete aggregate no longer
  match;
- supersession preserves old content/evidence and one current pointer;
- SQLite uses its supported write-reservation path and never emits
  `FOR UPDATE`;
- `REFERENCES`, `GENERATED_FROM`, anchors, and flow steps use exact versions;
- rebuild is repeatable, atomic, and reads no `index.md`;
- source authorization is non-disclosing and target revocation contains no
  restricted identity/behavior or target-derived canonical, index, or section
  text;
- only explicitly requested sections are returned.

## 3. PostgreSQL locking/currentness group

Run the PostgreSQL-tagged two-context integration fixtures against a disposable
PostgreSQL database:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStorePostgreSqlConcurrencyTests"
```

Expected:

- two successor writers start from the same current version; owner rows are
  locked in UUID order, exactly one commits, the other returns `Conflict`, and
  no losing child/projection row remains;
- one transaction holds a target owner lock and supersedes it while another
  commit waits; the waiter revalidates after lock acquisition, returns
  `NonCurrentEndpoint`, and persists no Edge to the stale version;
- composite current/successor and endpoint/anchor ownership constraints reject
  cross-Logical-Document, cross-Repository, and cross-Scan tuples.

## 4. Migration model/operation group

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStoreMigrationTests"
```

Expected: only the nine Story 11.1 table families are added; FK/delete,
uniqueness, check, concurrency, and Repository-query indexes match
[data-model.md](data-model.md), including the fixed 64-character
`CommitFingerprint` and
`CK_IndexEntries_DocumentType_Closed`; Story 4.3 tables remain unchanged.

The full suite also verifies the separately authorized integration-owner pin:

```text
tests/Vulgata.Tests/MigrationTests.cs:142
migrator.Migrate("20260730083207_AddConservativePreScanGate");
```

The Story 11.1 implementer must treat this as read-only.

## 5. PostgreSQL clean apply, rollback, reapply, pending-model check

Use a disposable/clean development database:

```pwsh
dotnet ef database update --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
dotnet ef database update 20260730083207_AddConservativePreScanGate --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
dotnet ef database update --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web
```

Expected: clean apply; rollback removes all nine Story 11.1 tables but preserves
Story 4.3 schema; reapply succeeds; pending-model command exits 0.

## 6. Full regression

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
```

Expected: all tests pass, including Story 4.3.

## 7. E2E disposition

Authorization, persistence integration, EF migration, and observable Version
Chain lifecycle predicates are triggered, so the E2E gate starts required.
The `e2e-runner` executes sections 2–5 against the pinned reviewed boundary.
A `skipped-covered` override is valid only if current-boundary passing evidence
from those exact integration tests covers all three predicates. The final clean
two-Repository flow and Docker build remain Story 11.5.
