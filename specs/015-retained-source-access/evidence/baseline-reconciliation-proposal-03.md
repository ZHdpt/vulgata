# Remaining baseline fixture and opt-in proposal

The approved proposal02 repairs are being verified. Clearing the shared schema/hash failures exposed these additional controlled-test and legacy-runtime boundaries. No production implementation change, historical feature activation, external runtime installation, or gate waiver is proposed.

## Exact additional scope

1. In tests/Vulgata.Tests/PreparedScanning/PreparedScanReleaseQualificationTests.cs and the directly shared reviewed-profile fixture used by ReviewedExactProfileSeedPostgreSqlTests, align synthetic source/range data with the already accepted profile-required src/main/java paths. Preserve oversized synthetic release-file scenarios and all deliberately missing, unsafe, filtered, duplicate, ambiguous, non-contained, empty-hash and invalid-split rejection assertions. Do not change the profile or production PreparedScanPairBuilder.
2. Isolate Pass1DeepSeekPromptCompositionTests.ProductionWebScope_ResolvesPreparedExecutorAndBothClosedFunctionFactoriesAsync from an unconfigured real JDT archive by supplying a deterministic test-owned dependency through the existing test factory. Keep assertions for the actual production executor and closed function factories. No tool execution or claim of real JDT readiness.
3. Make Pass1HostRuntimeQualificationTests.PinnedHostTools_AreExactAndJdtLsUsesStdioFactory explicitly opt-in at discovery, consistent with the neighboring historical host-qualification cases. Preserve its complete pinned-version/hash/stdin-stdout assertions when opted in. Leave external flags disabled and report the ordinary-run skip explicitly; do not claim real-tool qualification.
4. Archive exact originals before new edits, verify these focused tests and affected neighbors, then run retained-source and full-suite gates. Any genuine production defect or other test retirement returns separately.

## Current evidence

The old full-suite log contains the profile fixture's unconditional synthetic A-LargeEntry.java expectation while production requests the existing accepted profile path UploadOcrToOssOp.java. The post-resource-repair run passes297 tests with2 opt-in skips and only jdt_distribution_archive_missing in the production-composition test. The historical T046 host test currently fails immediately on absent VULGATA_T046_CODEGRAPH_COMMAND; it has no discovery-time opt-in attribute despite its neighboring host tests having one.

These are additional named files/behaviors beyond proposal02's exact resource, SQL and T048 repair list. Approval authorizes only this bounded test maintenance. Full-suite success, convergence, independent quality review, E2E decision and final verification remain required.
