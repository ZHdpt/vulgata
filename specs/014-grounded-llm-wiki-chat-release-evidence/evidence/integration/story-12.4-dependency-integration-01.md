# Story 12.4 dependency integration evidence 01

Observed: 2026-08-27T15:14:39.2789856Z

## Exact integration

- Story 12.5 pre-merge HEAD was `0bdfdf769faa078d480039ca89605013bb23db32` on attached branch `demo0831/story-12-5`, with a clean index and worktree.
- The only merged object was accepted Story 12.4 completion commit `4798b97d9f23aef587829175df7ab7ebc3daf995`.
- The explicit non-fast-forward merge commit is `8b35d28ff6eead162cfbea08c55168aa225826b9`, with parents `0bdfdf769faa078d480039ca89605013bb23db32` and `4798b97d9f23aef587829175df7ab7ebc3daf995`.
- Merge checkpoint: `bmad-12-5-implement-20260827T150156Z-63b5a8`.
- The only conflict was `docs/bmad/implementation-artifacts/sprint-status.yaml`. It was resolved to Story 12.4 `done` and Story 12.5 `in-progress` exactly as authorized.
- The staged merge contained exactly the 34 paths changed by the accepted Story 12.4 commit. Every non-sprint-status staged blob matched the accepted commit byte-for-byte; the resolved sprint-status diff changed only Story 12.4 from `in-progress` to `done`.
- `git diff --cached --check` passed, no unmerged paths remained, and the Track A checkpoint stayed ancestral.

Story 12.4's completion commit carries `Workflow-Checkpoint: bmad-12-4-complete-20260827T132524Z-d8e2d1`. Its committed schema-v3 state records accepted definition and quality reviews, converged status, and passed E2E, but it leaves the completion checkpoint at `binding_status: pending` with `commit_sha: null`. The current repository workflow validator rejects that exact committed state with `complete state requires a bound complete checkpoint`; it does not accept the state as a valid completed dependency.

The accepted Story 12.5 definition remained byte-identical after integration:

- spec: `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4`
- plan: `8dd2ba8d4f41a1c0436cc070d6b8639e6a962e6770d626b221ac3d314e998628`
- tasks: `679a9613c86dcfb2e7cccdf138f5d710969c478040b4f2539d6f1349e62ffddd`
- planned-change authorization artifact: `2ae0f24c235192a82ea5f8651ecb24afcf74d1d2c27b00b6c0abaa365c866274`
- accepted planned-change scope: `7581b411951e52fa08dd979bcc3b6a286645c1373ff60657044d851448fda05c`

## Integration test correction

The first Track A regression honestly observed one merge-caused RED: the release-boundary test rejected every Story 12.4 path, including the newly authorized exact dependency commit. The test-only correction now requires the accepted commit to be ancestral, requires its exact Story 12.4 path set, allows its coordinated Agents DI change only from that commit, and rejects later Story 12.4 or uncoordinated shared-file changes.

The corrected Track A boundary passed 117/117 checks, including all 75 Story 12.5 ReleaseGate checks. No Track A production behavior changed, so its accepted independent quality review remains current under the user's explicit rule.

## Dependency barrier failure

The required Exact Cross-Repository/Pass 2 regression did not pass:

```text
Failed: 194, Passed: 245, Skipped: 42, Total: 481
```

The common failure is `System.IO.InvalidDataException: The authoritative Business Logic schema identity drifted.` Composition cases consequently return HTTP 500 instead of the expected result.

This is a committed authority contradiction, not a stale build:

- `src/dotnet/Vulgata.Core/DomainServices/Pass2BusinessLogicSchemaContract.cs` in the exact accepted commit requires SHA-256 `758ECCC19EEF53FE9A510BD1627C6700C4FBA77BB95A72660DE6B6924131553B`.
- The committed embedded resource `specs/013-exact-cross-repository-flow-pass2/contracts/business-logic-flow-v1.schema.json` is SHA-256 `CE7985A6131A468A072EEA025EBE69DA5CB9214EB90AD148A5A88F845D386EA5`.
- That resource blob is identical at the common base and accepted Story 12.4 completion commit; the completion commit does not contain a matching schema/accepted-hash correction.

The Web build still succeeds with 0 errors and 19 warnings. `git diff --check` passes.

## Gate disposition

The Track B integration barrier is failed. `implementation-approval-track-b-01` is therefore **not recorded or consumed**, T020-T037 remain unstarted, and Track C remains closed. Correcting the authoritative schema bytes, accepted identity, or invalid completion binding would modify Story 12.4 contracts/state and is expressly outside this approval. A corrected, independently accepted Story 12.4 completion commit is required before Track B can resume.

No EF migration, `VulgataDbContext`, model snapshot, Story 12.4 working-directory file, Story 12.4 database, or Track C artifact was changed.
