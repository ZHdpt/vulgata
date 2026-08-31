# T043 Exact Owner Protocol RED 01

Date: 2026-08-31
Story: 12.5 Track C-MVP, T043
Classification: honest product RED

## Boundary and ownership

This slice tests the existing public reviewed-profile authority result, the raw
`textDocument/documentSymbol` protocol client, and the Core prepared-scan
boundary contract. Only the assigned test files were changed; production,
workflow state, task artifacts, staging, and Git history were not changed.

Changed paths:

- `tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedAuthorityTests.cs`
- `tests/Vulgata.Tests/AnalysisRuntime/JdtLsAdapterTests.cs`
- `tests/Vulgata.Tests/PreparedScanning/PreparedScanBoundaryTests.cs`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/exact-owner-protocol-red-01.md`

The pre-existing user-owned `.env.example` remains untouched.

## Commands and results

Initial focused build/test command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~JdtLsAdapterTests|FullyQualifiedName~ReviewedExactProfileSeedAuthorityTests|FullyQualifiedName~PreparedScanBoundaryTests" --logger "console;verbosity=minimal"
```

The first observation reached test compilation but exposed two test-source
diagnostics in this slice: `PropertyInfo` needed `System.Reflection`, and the
installed xUnit version has no two-argument `Assert.NotNull` overload. Those
test-only diagnostics were corrected without touching production.

Corrected focused command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~JdtLsAdapterTests.StreamJsonRpcJdtLsClient_DocumentSymbolRoot_PreservesAuthorityProvenanceAndTypeNameAsync|FullyQualifiedName~ReviewedExactProfileSeedAuthorityTests.MatchedUnsupportedOwner_ExposesProfileOwnedQualifiedSymbolAndSelectorLine|FullyQualifiedName~PreparedScanBoundaryTests.DocumentSymbolEvidence_DeclaresRootAndFlatProvenanceAndServerTypeName" --logger "console;verbosity=minimal"
```

Build completed successfully and the test run reported **3 failed, 0 passed,
0 skipped, total 3**. The failures are expected product REDs:

1. `PreparedScanBoundaryTests.DocumentSymbolEvidence_DeclaresRootAndFlatProvenanceAndServerTypeName` failed because `AnalysisSymbolEvidence` has no public `Provenance` property. The accepted contract requires `RootDocumentSymbol`, `FlatSymbolInformation`, and `Unspecified` provenance, plus a server-observed `PreparedJdtSymbolSpan.TypeName`.
2. `ReviewedExactProfileSeedAuthorityTests.MatchedUnsupportedOwner_ExposesProfileOwnedQualifiedSymbolAndSelectorLine` expected the profile-owned FQN `cn.com.yitong.actions.cust.UploadOcrToOssOp`, but the current required source exposes `null` for `FullyQualifiedSymbol`. `SelectorLine` is likewise not currently exposed.
3. `JdtLsAdapterTests.StreamJsonRpcJdtLsClient_DocumentSymbolRoot_PreservesAuthorityProvenanceAndTypeNameAsync` sent a valid homogeneous root `DocumentSymbol[]` (`kind=5`, exact `range`, contained `selectionRange`, empty `children`) through the raw JSON-RPC seam, then failed because the parsed evidence has no `Provenance` property. The current parser also has no `TypeName` projection on `PreparedJdtSymbolSpan`.

The current parser accepts the root response as ordinary `AnalysisSymbolEvidence`
and does not establish the required root-vs-flat authority distinction. No
production change was made to make the tests pass.

## Expected production owner handoff

The minimal GREEN owner is the single coordinated T047 owner listed by the
accepted plan: Core analysis protocol/provenance and profile-owner values;
Infrastructure `JdtLsAnalysisAdapter` typed root/flat document-symbol parsing
and reviewed authority projection; and the enumerated prepared builder and
identity/packet seams. The owner must preserve definition, references,
implementation and generic flat-coordinate behavior, while rejecting malformed,
mixed, unsupported, ambiguous, non-contained, or flat-only semantic-owner
evidence before publication. No caller/provider symbol input, `containerName`
hierarchy inference, CGC/legacy path, EF/model/migration change, or Track A/B
change is implied by this RED.

## Hygiene

```text
git diff --check -- tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedAuthorityTests.cs tests/Vulgata.Tests/AnalysisRuntime/JdtLsAdapterTests.cs tests/Vulgata.Tests/PreparedScanning/PreparedScanBoundaryTests.cs
```

Passed. Git reports the pre-existing `.env.example`, this slice's three assigned
test files and evidence file, plus separately owned T045 test/evidence files
already present in the shared worktree. Those other-owner paths were not
modified, cleaned, staged, or reverted; nothing is staged.
