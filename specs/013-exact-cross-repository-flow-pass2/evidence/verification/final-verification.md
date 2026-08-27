# Story 12.4 final verification

Observed at: 2026-08-27T21:00:23+08:00

Status: GREEN

## Live two-Repository qualification

- Command boundary: the opt-in `Pass2ProductionQualificationTests.ApprovedTwoRepositoryPair_ProducesSanitizedSourceGroundedBusinessDocumentAsync` test, using the approved `https://opencode.ai/zen/go/v1/chat/completions` endpoint, `deepseek-v4-flash`, and the API key supplied only through `VULGATA_TEST_KEY`.
- Result: 1 passed, 0 failed, 0 skipped in 3 minutes 11 seconds.
- Durable result: one Published Flow Group, one Completed Pass 2 Job, one immutable Business Logic Document, three GENERATED_FROM Edges, three Source Anchors, and no omission.
- Sanitized evidence: `specs/013-exact-cross-repository-flow-pass2/evidence/qualification/story-12.4-live-two-repository.json`.
- Retained database: `localhost:5432/vulgata_story124_b21df3f92e714fd98cb3b564d8314c70`. It was not removed after qualification.

## Regression results

### Exact Cross-Repository and Pass 2

Command:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore -p:BaseOutputPath=G:\source\repos\vulgata\.tmp-t049-provider-owner-green\ --filter "(FullyQualifiedName~ExactCrossRepository|FullyQualifiedName~Pass2)&FullyQualifiedName!~Pass2ProductionQualificationTests" --logger "console;verbosity=minimal"
```

Result: 481 total, 439 passed, 0 failed, 42 skipped. The skipped cases are explicit PostgreSQL opt-in coverage; the Story 12.4 PostgreSQL barrier is separately GREEN at 87/87 in `focused-results.json`.

The final regression correction keeps the exact Provider owner on the configured XML declaration Document while the Provider implementation remains separate supporting evidence. It also performs the live authorization recheck before querying persisted v2 authority. Focused extraction, integration, authorization, and current-candidate coverage passed 76/76.

### Durable Scanning, Prepared Scanning, Knowledge Store, and Analysis Runtime

The build-enabled host-qualified run used:

- `VULGATA_TEST_POSTGRES` bound to retained disposable clone `vulgata_story124_diag_28b1e871abd549308bb475febbf9420b`;
- the verified JDT-LS installation and archive supplied by the host;
- `G:\packages\npm\prefix\codegraph.ps1`;
- unrestricted host execution for the approved local tools and PostgreSQL instance.

Result before the administrator-only sub-boundary: 443 total, 411 passed, 31 environment-gated PostgreSQL tests awaiting `VULGATA_TEST_POSTGRES_ADMIN`, and 1 intentional SQLite-only skip. Supplying the approved administrator connection and rerunning the exact missing boundary produced 31/31 passed. Effective final boundary: 442 passed, 0 failed, 1 intentional skip.

The retained regression database remains available at `localhost:5432/vulgata_story124_diag_28b1e871abd549308bb475febbf9420b`.

## Build and hygiene

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore -p:BaseOutputPath=G:\source\repos\vulgata\.tmp-t049-web-build\ -p:WarningLevel=0 -clp:ErrorsOnly
```

Result: build succeeded with 0 errors and 9 warnings. Warnings are reported separately and were not treated as errors.

`git diff --check` completed without errors after this evidence was written.

## Environment and data handling

- No provider key, source content, prompt transcript, or raw candidate is stored in durable qualification evidence.
- Bounded source-derived context was transmitted only from the two user-approved repositories to the approved OpenCode Zen endpoint.
- The ordinary `vulgata` database was not used as a disposable test target.
- Successful and diagnostic Story 12.4 databases and raw qualification outputs were retained as requested.
