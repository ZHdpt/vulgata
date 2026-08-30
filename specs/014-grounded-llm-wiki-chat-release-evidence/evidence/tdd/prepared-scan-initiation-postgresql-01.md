# T015-T017 Prepared Scan initiation PostgreSQL evidence

Status: **GREEN**

## Boundary

The suite uses only `PreparedScanInitiationPostgreSqlFixture`. Every executable
case creates a fresh lowercase `vulgata_story125_*` PostgreSQL database, applies
the existing migrations, and begins with one ordinary System and ordinary
Repositories. SQLite was not used. The retained Track C run-02 database
`vulgata_story125_93fd44c9b3044fee84f2d032ac3af8a2` was neither connected to
nor modified.

The administrator connection was recovered process-locally from the approved
development configuration with `NpgsqlConnectionStringBuilder`, changing only
`Database=postgres` and `Pooling=false`. No connection string or secret was
printed.

## RED chronology

1. The initial 14-test contract compiled through the new Core port and failed
   with the single expected `CS0246` because
   `PreparedScanInitiationStore` did not exist. No database was created.
2. The first implementation run separated production and test-fixture issues:
   normal persistence still returned `Unavailable`; two hostile seeds were
   impossible under existing alternate/composite keys; and record equality over
   independently materialized `IReadOnlyList` values was reference-based.
3. Test-only corrections made the ambiguous seed schema-valid, recorded the
   existing composite foreign key's rejection of an impossible cross-pair, and
   compared replay tuples structurally. The normal-create defect was the static
   record sentinel selecting a record copy constructor for `new(null)`; the
   production implementation corrected it with an explicit nullable outcome.
4. The resulting suite passed 23/23, after which a public-seam test persisted an
   exact bound `ScanContextSnapshot.CapturePrepared`. It produced an honest RED:
   23 passed and 1 failed, with expected `Present` versus actual `None`.
5. Persisted artifact detection was added for exact bound prepared snapshots,
   Manifests, prepared-v2 Pre-Scan Results, and prepared File Code Units.
   Manifest entries and audits are transitively covered by their required
   Manifest foreign key.

## Final GREEN

Focused PostgreSQL result:

```text
Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24, Duration: 24 s
```

The final suite proves:

- absent state atomically creates two Scan Runs, two new-path Scan Executions,
  and one Preparing Two-Repository coordination;
- exact and reversed-order requests replay one structural tuple;
- changed ref, Profiled Revision, runtime, prompt, schema, or effective
  authorization scope conflicts without writes;
- historical uncoordinated Runs do not satisfy the new path;
- legacy, partial, extra, ambiguous, or impossible cross-paired durable state is
  rejected by the store or existing database constraints;
- current phase and prepared-artifact presence are materialized;
- independent identical requests converge to one `Created` and one `Replay`;
- competing pair/revision requests yield at most one winner;
- advisory-lock timeout, cancellation, injected save failure, and injected
  pre-commit failure expose no partial tuple;
- the create path calls SaveChanges and commit once, and an independent observer
  sees all five rows only after commit;
- ambiguous post-commit acknowledgement maps to `Unavailable`, while an exact
  retry replays one canonical tuple without duplication.

All test-owned disposable databases were cleaned. The final post-run database
inventory contained only the retained run-02 database named above.

## File identities

- `PreparedScanInitiationStore.cs`: `38533e17dc62c516e15bfc1d47e4a9434ac71f124809468ca979e9ebd68e0531`
- `PreparedScanInitiationPostgreSqlTests.cs`: `176307384f135433c0afa3b52a33530d11c0288662a127b91203eed67e6b1c68`
- `PreparedScanInitiationPostgreSqlFixture.cs`: `27a4ce341bcaaa3da34f780afd749f7ba43ce5e11a66e07b45336e4f8eb85286`

`git diff --check` passed with line-ending advisories only. No EF migration,
DbContext/model/configuration/snapshot, Story 12.4, retained database, external
analysis, Worker, Pass, Document, Edge, Uncertainty, Chat, or LLM path changed.
