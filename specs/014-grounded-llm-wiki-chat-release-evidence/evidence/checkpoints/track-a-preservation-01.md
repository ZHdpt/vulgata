# Story 12.5 Track A preservation checkpoint 01

- Observed at: `2026-08-29T07:42:47Z`
- Source commit: `ba45c59065db8beebf2fe95f11fb518387bc18a4`
- Branch: `demo0831/story-12-5`
- Story 12.4 accepted dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995` (verified as the merge base with `HEAD`)
- Classification: T005 and T006 are `EXISTING-EVIDENCE`; T007 is `VERIFY`. No behavioral RED was fabricated and no product or test file was changed.

## Exact focused commands and results

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ProductionScanStatusReaderTests|FullyQualifiedName~ProductionScanPageTests|FullyQualifiedName~ManualResumeApiTests" --logger "console;verbosity=minimal"
```

Result: **42 passed, 0 failed, 0 skipped**, exit code 0, duration reported by the test runner: 14 seconds.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --no-build --filter "FullyQualifiedName~Story125EvaluationContractTests|FullyQualifiedName~Story125ReleaseHarnessTests" --logger "console;verbosity=minimal"
```

Result: **74 passed, 0 failed, 0 skipped**, exit code 0, duration reported by the test runner: 1 second.

Combined focused result: **116 passed, 0 failed, 0 skipped**.

## Reaccepted Track A invariants

T005 reaccepts the unchanged production Scan boundary:

- production composition resolves the persisted status reader and management coordinator;
- every persisted Scan phase maps to the closed Chinese display state, with persisted counts, earliest retry time, bounded failure reason, and the bounded twenty-file failure summary;
- hidden and unauthorized executions are missing-equivalent and reveal neither protected details nor the resume action;
- only a recoverable current paused execution can resume, resume re-reads durable persisted status, and replay remains bounded and idempotent;
- the manual-resume API preserves 401/403/400/404 behavior, ownership filtering, bounded Chinese `ProblemDetails`, and rejects caller-supplied state/runtime fields;
- resume does not dispatch a Worker Agent or initialize analysis runtime;
- fake Backup B state is never eligible for production status or release evidence.

T006 reaccepts the unchanged evaluation and release-evidence boundary:

- the schema-valid corpus contains 16 stable cases: 9 Business mode and 7 IT mode; authorization categories are 14 authorized, 1 missing-equivalent, and 1 no-restricted-disclosure; relationship categories cover REFERENCES, GENERATED_FROM, exact Cross-Repository evidence, Flow Groups, and no-relationship cases;
- follow-ups reference existing cases, are acyclic, and remain at most three questions per chain;
- corpus cases contain no `expectedAnswer`, `providerAnswer`, or `expectedResponse` field;
- any authorization disclosure fails automatically, accepted technical-claim citation coverage must be 100%, and useful percentage uses exact nonnegative half-up scoring;
- a passed result requires its release receipt; planned, attempted, or failed evidence cannot claim passed-only receipts;
- the mandatory ten-check inventory remains unique, complete, correctly tiered, and ordered;
- run supersession remains append-only: no self, missing, or later target is accepted;
- release evidence binds commands, attempted observations, artifacts, corpus/result/receipt case sets, packet fingerprints, hashes, and the closed streaming vocabulary;
- database guards accept only the disposable `vulgata_story125_*` boundary and reject the main database, Story 12.4 databases, and fake Backup B state;
- migration, `VulgataDbContext`, model snapshot, Story 12.4, and uncoordinated shared-integration boundaries remain protected.

## Pinned input hashes

| Input | SHA-256 |
|---|---|
| `tests/Vulgata.Tests/ProductionScanStatusReaderTests.cs` | `fe4263be509e85be71ec255e1452970b841e2f5be389f5cbba11119603c0f820` |
| `tests/Vulgata.Tests/ProductionScanPageTests.cs` | `4c8d5d20355ed0f621b8d74c1ea9a9761022707a90b73262f141e48218123070` |
| `tests/Vulgata.Tests/DurableScanning/ManualResumeApiTests.cs` | `1cc109670b66213dfd3d7c3c34dd56d5dc57a4289b7e48487736ecbd32cc55dd` |
| `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs` | `60feb253a1b4a88afad5f646778d5978078fe4e901150c0270fc1f5347e3cb76` |
| `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs` | `e6ac40e9eee4292e2dce90ef4497673e61e919b42d9f16d6646a4ed61911610f` |
| `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json` | `7a06a291855113ef9231a1c252f7f224d6e7d5f7c576e0e8f65c8fdb55a3e371` |

## Environment classification and scope statement

The build emitted existing analyzer/compiler warnings plus `NU1507` package-source-mapping warnings and `NU1900` vulnerability-feed reachability warnings for `nuget.org` and `dnceng`. The run used `--no-restore`; both focused suites completed successfully, so these warnings are environmental/non-product evidence and not Track A failures.

T005-T007 can be marked complete. This checkpoint reaccepts Track A only. It claims no Track B B1-B4/B-INT acceptance and no Track C release qualification, restart, screenshot, or real-LLM evidence.
