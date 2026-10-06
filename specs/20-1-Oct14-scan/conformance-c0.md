# OCT-SCAN initial contract import and consumer conformance

## Imported boundary

- Assigned branch: `oct14/scan` in `C:/Users/13728/.codex/worktrees/f8dc/vulgata`.
- Accepted planning checkpoint: `23ca0883`.
- Exact requested integration checkpoint: `3bcd815b09cfbac7418d6644a2a75816e0788ec0`.
- Business compiled candidate: `07c3fe4463e3228a6982fd106b58148ca87974bd`; documentation handoff: `7a189c34aec625202f74846d4907ded787ccbdca`.
- Local merge: `5d98ee54` (ordinary merge; no reset/rebase/history rewrite). Requested integration and `fad7f11` ancestry verified.
- Imported owner Markdown contract SHA-256: `719C5AF610E5A84EEA99CA03A6487015DEE79A052D94B0E0902185BA363D4568`.
- Actual imported symbol/ownership mapping: [contracts/scan-boundaries.md](contracts/scan-boundaries.md).

## Task and evidence status

| Task | Status | Evidence boundary |
| --- | --- | --- |
| T001 | Complete | Exact merge/ancestry and read-only Core/Shared/fixture inspection; concrete symbols recorded |
| T002 | Complete | Seven compiled Scan contract cases in `OctoberScanContractProbes.cs`; exact additive SelfContained classification |
| T003 | Complete | Twelve compiled Scan boundary cases in `OctoberScanBoundaryProbes.cs`; exact additive SelfContained classification |
| T004 | Complete — Scan candidate acknowledgement | 69/69 filtered SelfContained cases pass (19 Scan + 50 imported Business); independent test-code review accepted; global CP-K1 acceptance remains integration-owned |
| T005 onward | Not started | Explicit stop boundary remains |

Historical authorization blocker: automatic approval review rejected the first test-file write because the earlier direct stop-before-implementation instruction controlled over the coordinator dispatch. No bypass was attempted. The user subsequently explicitly superseded that instruction for T002–T004, including test authoring/classification, SelfContained verification, acknowledgement and commit, with a hard stop before T005 and no provider/native-tool/database/migration/host execution. This direct confirmation resolves the earlier blocker only within that scope.

## Executed verification and acknowledgement

**Scan acknowledges the initial compiled consumer contract/fixture candidate imported at `3bcd815b09cfbac7418d6644a2a75816e0788ec0`**, with no blocking compile or supported controlled-conformance objection. This is T004 consumer acknowledgement for integration's waiting coordination task, not acceptance on behalf of other lanes, production qualification or permission to begin T005.

Command, from the assigned worktree:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.OctoberScan|FullyQualifiedName~Vulgata.Tests.October.Knowledge'
```

Launcher exit code **0**. Current sources were built; policy checked **322 classified source files**, with policy/outcome/timeout/cleanup regressions and fixture guards passing. Discovery reported **3,398** tests; selection executed **69**, passed **69**, failed **0**, skipped **0**, errors/timeouts/aborts **0**. Breakdown: **7** Scan contract cases, **12** Scan boundary cases, **50** imported Business cases. This is the targeted SelfContained selection, not the full SelfContained suite.

- Actual evidence: `TestResults/isolation-d0963a3b5ccb4ed4bfd371a577a4ff36/results.trx`, with `build.log`, `discovery.log`, `execution.log` and `guard-probes.log` alongside it.
- TRX SHA-256: `CC836675EA6F06E432ED607F1992A862B2D39D9CE0003B16343C26D2054890C9`.
- Build: **0 errors, 9,190 warnings**, including AD0001 analyzer exceptions. No analyzer severity/configuration was changed; build success is not a warning-free claim.
- The guard log includes its intentional failing enabled sentinel followed by `PASS: disabled fixture excluded; enabled control reached fixture`; this verifies the guard and is distinct from the 69 passing selected cases.
- No provider, native analysis tool, database, migration or application host execution was performed. PostgreSql, LocalTools, External, E2E and HistoricalBoundary suites were excluded and are not qualified.
- No contrived RED is claimed for these compile/conformance probes against an already imported contract/fixture. Product behavior is unchanged. Separate test author authored the probes; root reviewed the exact diff and ran verification; the retained independent reviewer accepted the 19 cases as bounded consumer probes with no blocking issue.

| Verified file | SHA-256 |
| --- | --- |
| `tests/Vulgata.Tests/OctoberScan/OctoberScanContractProbes.cs` | `C2D248CFB38047A765ECDB7FBE334F45C1B78BC4EEEC605836A86B844A0CA37F` |
| `tests/Vulgata.Tests/OctoberScan/OctoberScanBoundaryProbes.cs` | `26A4CB4DAA87905C46114225ED8AB8376D642B77015D9F6189EA95F8D89A0FC8` |
| Imported `src/dotnet/Vulgata.Core/DomainServices/OctoberKnowledgeContracts.cs` | `369F804929274E72AA46BFCD6132DA24939614DFDE0CB337DF9855D9CC668FF0` |
| Imported `tests/Vulgata.Tests/October/Knowledge/OctoberKnowledgeFixture.cs` | `300904F67D989CF4C84B268A2D3F660198866C84BDC0E5ACB02CADBF7DFF79D1` |

Only the two lane probe files and their two suite entries changed in test code. Imported production contracts, validators and Business tests/fixture remain identical to the integration checkpoint. Scope freezes here before T005; final integration acknowledgement/other consumers and later actual producer/host implementations remain prerequisites.

## Controlled assessment and retained qualification limits

The imported types preserve accepted F1/F2/F5 responsibilities. F1's `Func<CancellationToken, SubmittedCommit>` requires synchronous commit invocation under the shared guard before returning the settlement task, rather than merely invoking a deferred callback. F2's owner lease exposes `CheckBeforeSendAsync`, `SettleAsync`, cancellation and asynchronous disposal. F5 uses `IOctoberKnowledgeWriter` acceptance/freeze/publication plus exact owner reader types without B1/B2.

The owner fixture is expressly a controlled scripted model:

- `ControlledKnowledgeWriter` validates proof matching and models duplicate operations/zero-unit acceptance, but does not validate completed-proposal structure/schema/source anchors. Public Shared validators validate read requests, not producer proposals. Do not assert malformed producer rejection from this fixture.
- `FreezeCorpusAsync`/`PublishSelectionAsync` are scripted. Exact handles and CL-only consumer calls can be checked; real publication replay, transaction atomicity, complete membership enforcement and publication guard equivalence are not established by these methods.
- `ControlledCommitGuard` models entry/staging/submission/deadline/fence and settlement; it does not prove production synchronization or database rollback. Temporary absent results while pending remain indeterminate in the fixture.
- `ControlledExecutionScope` models cap=1, rate flags, hard-stop queue denial and actual/unknown accounting. Its caller cancellation token is not observed by queued acquisition; arbitrary caller-driven queued cancellation is unproven. No real limiter/provider/transport or release-after-disposal implementation is qualified.
- Reader/disclosure fixtures demonstrate scripted exact membership and live mutable denial, not actual Identity, persisted publication, secure cursor or Web composition.

These limitations are bounded qualification gaps, not permission to add alternate lane-owned Knowledge policies or edit Business fixtures. The compiled probes use the actual imported types/fixtures for supported interactions and leave unsupported producer-schema validation, arbitrary queued caller cancellation and transactional publication equivalence unclaimed. The CL-only probe checks scripted owner publication/reader handles and absence of a Business call path; fixed fixture membership and its constant ProviderCalls value do not prove an actual persisted source-to-publication pipeline. Producer persistence/validation/live authority, actual Scan guard/scope/transport disposal, native tool composition, B0 ordinary reader and later B1/B2 remain production prerequisites. Integration may use this acknowledgement to resolve its Scan-consumer dependency while retaining those future obligations and other consumers' independent acknowledgements.
