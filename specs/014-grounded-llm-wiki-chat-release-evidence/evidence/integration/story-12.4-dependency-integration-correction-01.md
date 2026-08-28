# Story 12.4 dependency integration correction 01

Observed: 2026-08-28T00:07:47.1621558Z

## Authorized metadata corrections

- `.gitattributes` fixes the authoritative Business Logic schema resource to
  `text eol=lf`. The committed schema blob and the accepted runtime identity are
  unchanged; a fresh Windows worktree now materializes the same LF bytes and
  SHA-256 `758ECCC19EEF53FE9A510BD1627C6700C4FBA77BB95A72660DE6B6924131553B`.
- Story 12.4's compact schema-v3 state now binds completion checkpoint
  `bmad-12-4-complete-20260827T132524Z-d8e2d1` to accepted completion commit
  `4798b97d9f23aef587829175df7ab7ebc3daf995` and records its clean completion
  preflight. No specification, plan, task, production contract, EF model, or
  migration changed.

## Test-fixture correction

The focused integrated suite exposed six `Sequence contains no elements`
failures in `AddSharedV2DecisionAsync`. The helper selected the earliest
resolution decision without requiring `ExactMatch`; the earliest decision was
an Uncertainty and correctly had no provider contribution. The test-only helper
now selects the earliest exact decision before cloning its contribution. The
production `ExactCrossRepositoryResolutionStore` already persists v2 provider
contributions correctly and was not changed.

The Story 12.5 release-boundary test permits only the explicitly authorized
post-integration Story 12.4 metadata path
`specs/013-exact-cross-repository-flow-pass2/workflow-state.json`. Every other
post-integration Story 12.4 path and all migration, DbContext, production, and
uncoordinated shared-integration paths remain rejected.

## Verification

- Exact Cross-Repository/Pass 2 focused regression: 439 passed, 42 skipped,
  0 failed; 481 total.
- Track A release harness: 75 passed, 0 failed.
- Track A production Scan status reader: 22 passed, 0 failed.
- Track A production Scan page: 7 passed, 0 failed.
- Track A manual-resume API: 13 passed, 0 failed.
- Web build: 0 errors, 19 known package-source/audit and EF assembly-version
  warnings.
- Workflow configuration and contracts: valid.
- `git diff --check`: passed.

A combined parallel Track A invocation initially reported the newly tightened
boundary RED plus 12 login-fixture failures. After the boundary correction, the
four accepted Track A groups were rerun in their isolated established groups;
all 117 checks passed. No Track A production behavior changed, so its accepted
independent quality review remains current.

## Definition and dependency identity

- Story 12.4 accepted completion commit remains ancestral:
  `4798b97d9f23aef587829175df7ab7ebc3daf995`.
- Story 12.5 spec:
  `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4`.
- Story 12.5 plan:
  `8dd2ba8d4f41a1c0436cc070d6b8639e6a962e6770d626b221ac3d314e998628`.
- Story 12.5 tasks:
  `679a9613c86dcfb2e7cccdf138f5d710969c478040b4f2539d6f1349e62ffddd`.
- Planned-change authorization artifact:
  `2ae0f24c235192a82ea5f8651ecb24afcf74d1d2c27b00b6c0abaa365c866274`.
- Accepted planned-change scope:
  `7581b411951e52fa08dd979bcc3b6a286645c1373ff60657044d851448fda05c`.

The dependency barrier is corrected without changing accepted Story 12.5
scope. Track B approval may therefore be bound to this correction checkpoint;
Track C remains closed.
