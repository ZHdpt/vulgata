# T038 Exact-Profile Seed Builder RED

**Task:** T038
**Classification:** RED (builder/public/composition)
**Date:** 2026-08-31

## Boundary

This slice adds only test assertions and this evidence record. It does not
change production code, workflow state, task definitions, the embedded exact
profile, the database, or any retained run evidence.

The tests pin the accepted public seams:

- `PreparedScanPairBuilder` must require exactly one non-optional,
  server-owned `IReviewedExactProfileSeedAuthority`.
- `PreparedScanRequest` must remain free of profile, path, score, or selector
  authority.
- `PreparedBusinessEvidence.ReviewedExactResolutionProfile` must score 1,100,
  use the existing direct-evidence selection reason, and keep all required
  segments inside the existing 20-per-Repository and 150-pair bounds.
- matched selection must be independent of Repository/candidate input order.
- the Infrastructure adapter must contain no reviewed sample Repository,
  path, endpoint, symbol, line, or revision literals.

The matched builder failure matrix remains owned by the authenticated builder
and PostgreSQL vertical owners: missing, filtered, unsafe, non-contained,
empty/hash-invalid, duplicate/ambiguous, split-invalid, over-20, and final
selection omission must reject before either Manifest is committed. A
catalog-unavailable result must remain distinct from a complete nonmatch, and
nonmatching output must remain byte-identical to the pre-correction generic
path. Those vectors are exercised through one existing builder harness without
creating a second production seam or a caller-controlled profile input.

## RED observation

Focused command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedScanReleaseQualificationTests.ProductionPairBuilder_MatchedReviewedProfile|FullyQualifiedName~PreparedScanReleaseQualificationTests.ProductionPairBuilder_NonMatchingPair_IsByteIdenticalToGenericSelection|FullyQualifiedName~PreparedScanReleaseQualificationTests.ReviewedProfileEvidence_IsScore1100AndAllRequiredSegmentsRemainMandatory|FullyQualifiedName~PreparedScanReleaseQualificationTests.ReviewedProfileSelection_IsIndependentOfRepositoryAndCandidateOrder"
```

After correcting the Core assembly lookup and using a valid reviewed XML
document, the reflection/public-seam rerun compiled and passed **5/5**. The
builder/public focused rerun then compiled and passed **15/15, 0 skipped**
against the current production. This includes matched promotion of all four
required score-1100 units, the complete missing/filtered/unsafe/
non-contained/empty-hash/duplicate/ambiguous/split-invalid/over-20 matrix,
catalog-unavailable mapping, nonmatching byte-identical characterization,
candidate-order invariance, and the overflow characterization proving required
segments are preserved. The prior 14-pass/1-fail run was the pre-correction
binary whose final-selection vector incorrectly expected rejection; that vector
is now a positive overflow characterization. The prior compile lock and
unrelated `ApprovedRuntimeAndWorkflowPins` failure remain
environmental/parallel-owner evidence and are not counted in this rerun.

No production GREEN is authorized by this test-only slice. After the owner
implements the authority and builder behavior, rerun this command and the
surrounding Prepared Scanning qualification classes.

`git diff --check` for all four assigned test files passed (only Git's normal
LF-to-CRLF informational warnings were emitted).
