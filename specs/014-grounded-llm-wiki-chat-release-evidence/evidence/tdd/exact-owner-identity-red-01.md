# T044 Exact-owner identity RED

Date: 2026-08-31
Story: 12.5 Track C, T044
Classification: corrected public-seam ownership; post-correction partial-T047 verification

## Scope

This slice covers the AD-PSI-7 identity boundary after T043: server-owned
semantic symbol propagation for a matched small owner, semantic binding to
exactly one containing oversized segment, fail-closed owner ambiguity and
non-containment, and a separate semantic-v2 identity projection while the
path-valued v1 identity remains stable. The request continues to expose no
caller-selected owner authority.

## RED observation

Focused command (build and test):

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~PreparedJavaSplitRangeTests.JavaSplit_MatchedOwner|FullyQualifiedName~PreparedFileCodeUnitAndSelectionTests.MatchedOwner|FullyQualifiedName~PreparedFileCodeUnitAndSelectionTests.SemanticOwner|FullyQualifiedName~PreparedScanApiTests.PreparedScanApi_SemanticOwner|FullyQualifiedName~PreparedScanBoundaryTests.FileCodeUnitIdentity" --logger "console;verbosity=minimal"
```

Result: 7 total, 1 passed, 6 failed, 0 skipped.

The passing characterization was
`FileCodeUnitIdentity_PathValuedV1RemainsByteStable`; it verified the
path-valued v1 domain and pinned fingerprint
`21870acdebeb7d142f39f4bf7cf51cafee74e84f037ca1bef0c62aac5d48e76d`.

The original pre-T047 run recorded six expected REDs. That original behavior
level RED remains retained evidence; it is not reclassified as a new RED after
the coordinated T047 production owner began implementation.

The original six expected REDs were:

* `MatchedOwner_FileCodeUnitSeamCarriesServerOwnedSymbolName`: neither
  `PreparedFileCodeUnitInput` nor `PreparedFileCodeUnitDraft` exposes the
  required server-owned `SymbolName` seam.
* `SemanticOwner_UsesSeparateV2IdentityBuilderWithOrdinalFqnBinding`: the
  identity builder exposes only path-valued `CreateFileCodeUnitIdentity` and
  no symbol-bound semantic projection.
* `PreparedScanApi_SemanticOwnerIsServerOwnedAndInputCarriesRequiredFqn`:
  the input constructor has no required `symbolName` parameter, while the
  public request correctly has no caller owner/symbol field.
* `JavaSplit_MatchedOwner_BindsFqnToOneContainingSegmentAndKeepsSiblingsPathValued`:
  the accepted owned `PreparedJavaRangeSource` semantic-owner projection seam
  was absent before T047, so it could not bind only the unique containing
  segment or preserve path-valued siblings.
* `JavaSplit_MatchedOwner_RejectsAmbiguousOrNonContainingObservationBeforePublication`:
  the accepted owned `PreparedJavaRangeSource` owner-binding result/failure
  seam was absent before T047, so it could not reject zero/multiple,
  wrong-range, or non-containing observations before publication.
* `FileCodeUnitIdentity_SemanticOwnerUsesSeparateV2BuilderWithOrdinalFqnBinding`:
  no separate semantic-v2 builder with an ordinal FQN input is present.

Surrounding characterization command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~PreparedJavaSplitRangeTests|FullyQualifiedName~PreparedFileCodeUnitAndSelectionTests" --logger "console;verbosity=minimal"
```

Result after the seam correction and against the partially implemented T047
candidate: **11 total, 11 passed, 0 failed, 0 skipped**. The generic Java
split and selection characterizations remained green, and the corrected
matched-owner reflection seams resolved on `PreparedJavaRangeSource`.

Corrected exact matched-owner run:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~PreparedJavaSplitRangeTests.JavaSplit_MatchedOwner_BindsFqnToOneContainingSegmentAndKeepsSiblingsPathValued|FullyQualifiedName~PreparedJavaSplitRangeTests.JavaSplit_MatchedOwner_RejectsAmbiguousOrNonContainingObservationBeforePublication" --logger "console;verbosity=minimal"
```

Result: **2 total, 2 passed, 0 failed, 0 skipped**. This is a post-correction
verification against the partial T047 production candidate, not a fresh
pre-production RED. The test now names the accepted `PreparedJavaRangeSource`
owner seam; generic coordinate-range characterizations continue to use the
legacy `PreparedJavaRangeProvider` seam.

The broader API/boundary class run also encountered the existing environment
failure `jdt_distribution_archive_missing` while constructing the production
JDT test composition. It is not counted as a T044 product/review failure.

## Minimal GREEN owner handoff

T047 must add the server-owned `SymbolName` to the Core input/draft and
reconstruct the applicable identity: unchanged v1 bytes when the symbol is
the normalized relative path, and a separate v2 canonical projection whose
additional ordinal field is the validated fully qualified Java symbol. The
prepared Java owner seam must reuse one `PreparedJavaSourceResult`, validate
the matched root symbol/path/range and selector containment, assign the FQN
only to the unique containing safe segment, leave sibling segments
path-valued, and return a bounded failure before Manifest/package publication
for zero/multiple/wrong/non-containing/unavailable evidence. T047 must not
change the public request, caller authority, EF model, Story 12.4 contracts,
or generic flat coordinate splitting.

## Changed paths and checks

T044 edits are limited to:

* `tests/Vulgata.Tests/PreparedScanning/PreparedJavaSplitRangeTests.cs`
* `tests/Vulgata.Tests/PreparedScanning/PreparedFileCodeUnitAndSelectionTests.cs`
* `tests/Vulgata.Tests/PreparedScanning/PreparedScanApiTests.cs`
* `tests/Vulgata.Tests/PreparedScanning/PreparedScanBoundaryTests.cs`
* this evidence file

`git diff --check --` on the four assigned test files passed (only the
repository's existing LF/CRLF informational warnings were reported); the
evidence file has also been checked for trailing whitespace. No production,
workflow, task, state, staging, commit, or history changes were made by this
correction. Existing T043/T045 edits and `.env.example` remain untouched.
