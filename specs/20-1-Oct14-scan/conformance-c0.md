# OCT-SCAN initial contract import — blocked consumer preparation

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
| T002 | Blocked before test-file creation | No lane conformance tests or suite registrations written |
| T003 | Blocked before test-file creation | No lane boundary probes written |
| T004 | Not complete | No local compiled consumer acknowledgement; no launcher run in this preparation |
| T005 onward | Not started | Explicit stop boundary remains |

Automatic approval review rejected the separate test author's initial test-file write: it judged the earlier direct human stop-before-implementation instruction controlling over the coordinator's bounded preparation dispatch. The test author stopped without another tool/path attempt. Root did not bypass that rejection. Direct human confirmation is needed before the rejected test-authoring action and its dependent test execution/acknowledgement proceed.

**This preparation's test counts: 0 executed; no PASS claimed.** No build, SelfContained launcher, native tool, provider, database, migration or host startup was run. Imported Business evidence remains its own previously recorded boundary, not a new Scan consumer result. Final shared CP-K1 acceptance remains integration-owned; neither import nor static concurrence closes it.

## Read-only assessment and limits for the future probes

The imported types preserve accepted F1/F2/F5 responsibilities. F1's `Func<CancellationToken, SubmittedCommit>` requires synchronous commit invocation under the shared guard before returning the settlement task, rather than merely invoking a deferred callback. F2's owner lease exposes `CheckBeforeSendAsync`, `SettleAsync`, cancellation and asynchronous disposal. F5 uses `IOctoberKnowledgeWriter` acceptance/freeze/publication plus exact owner reader types without B1/B2.

The owner fixture is expressly a controlled scripted model:

- `ControlledKnowledgeWriter` validates proof matching and models duplicate operations/zero-unit acceptance, but does not validate completed-proposal structure/schema/source anchors. Public Shared validators validate read requests, not producer proposals. Do not assert malformed producer rejection from this fixture.
- `FreezeCorpusAsync`/`PublishSelectionAsync` are scripted. Exact handles and CL-only consumer calls can be checked; real publication replay, transaction atomicity, complete membership enforcement and publication guard equivalence are not established by these methods.
- `ControlledCommitGuard` models entry/staging/submission/deadline/fence and settlement; it does not prove production synchronization or database rollback. Temporary absent results while pending remain indeterminate in the fixture.
- `ControlledExecutionScope` models cap=1, rate flags, hard-stop queue denial and actual/unknown accounting. Its caller cancellation token is not observed by queued acquisition; arbitrary caller-driven queued cancellation is unproven. No real limiter/provider/transport or release-after-disposal implementation is qualified.
- Reader/disclosure fixtures demonstrate scripted exact membership and live mutable denial, not actual Identity, persisted publication, secure cursor or Web composition.

These limitations are bounded qualification gaps, not permission to add alternate lane-owned Knowledge policies or edit Business fixtures. After authorization, use actual imported types/fixtures for supported interactions and distinguish unproven assertions from passing conformance. Producer persistence/validation/live authority, actual Scan guard/scope, native tool composition, B0 ordinary reader and later B1/B2 remain production prerequisites. The coordinator must receive a real compiled consumer result before treating its awaiting-acknowledgement task as complete.
