# T041 exact-profile seed selection verification

## Candidate and scope

- Candidate before the uncommitted T037-T042 group: `705657c9c4fbea26724930f206fd880277038cc0`.
- Production delta is limited to the closed Core seed authority/value, the
  Infrastructure adapter over the existing embedded exact profile, matched
  candidate promotion/validation in `PreparedScanPairBuilder`, and one DI
  registration.
- No EF model, migration, Story 12.4, exact profile resource/parser/catalog,
  Pass 1 store/policy, CGC, legacy profiling, caller DTO, or bound changed.

## Verification

The focused T037/T038 and surrounding selection/public/composition suite ran
with the approved process-local PostgreSQL administrator mapping:

```text
ReviewedExactProfileSeedAuthorityTests
PreparedSelectionPlannerTests
PreparedScanReleaseQualificationTests
PreparedScanProductionLiteralQualificationTests
PreparedScanBoundaryTests
PreparedScanInitiationApiTests
PreparedFileCodeUnitAndSelectionTests
PreparedScanAtomicReplayTests
```

The combined run observed 63 passes and one stale source-location assertion.
That assertion still looked for the CodeGraph package and Java-major pins in
`Program.cs`, although the accepted runtime authorities had moved to
`AnalysisRuntimeIdentity.cs` and `PreparedScanRuntimeIdentityResolver.cs`.
The test-only assertion was corrected to inspect those actual authorities and
its isolated rerun passed. Effective result: **64 passed, 0 product failures,
0 skipped**.

The suite covers complete matched activation, Repository/candidate reversal,
all required segments at direct score 1,100, stable Manifest/audit/package
fingerprints, byte-identical nonmatch neutrality, missing/unsafe/ambiguous/
split-invalid/over-bound fail-closed cases before commit, unchanged 20/150
bounds, one default server-owned authority, no caller profile/path/score seam,
and no reviewed sample literal in production source.

## Integrity

`git diff --check` reports no whitespace errors (only line-ending notices).
The tracked production-literal inspection output is treated as generated
verification evidence and will be intentionally regenerated or excluded at
the root-owned checkpoint; `.env.example` remains user-owned and unstaged.
