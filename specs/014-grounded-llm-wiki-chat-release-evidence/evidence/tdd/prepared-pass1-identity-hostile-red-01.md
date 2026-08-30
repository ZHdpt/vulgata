# QR-PSI-001 hostile Prepared Pass 1 identity RED

Date: 2026-08-30

## Scope

This slice covers the accepted Prepared Scan initiation identity authority. It
adds executable tests only; no production, workflow, task, stage, or Git
history changes are included. The coordinator vectors use the public
`IPreparedPass1ContractIdentitySource` seam and assert that invalid identity
cannot reach runtime resolution, fresh authorization, initiation persistence,
or prepared-pair preparation.

## Observed result

Focused command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter
"FullyQualifiedName~PreparedPass1ContractIdentitySourceTests|FullyQualifiedName~PreparedScanInitiationCoordinatorTests"
```

Result: 23 passed, 6 failed, 0 skipped (29 total).

The six failures are the intentional RED for the missing internal resource
provider seam. Each hostile-resource case (`missing`, `duplicate`, `empty`,
`invalid-utf8`, and `schema-inconsistent`) and the exact decoded-text SHA-256
binding test fails at reflection lookup because
`EmbeddedPreparedPass1ContractIdentitySource` does not yet expose the narrow
testable constructor/delegate seam. The failure is reported as
`Assert.NotNull() Failure: Value is null`.

The five coordinator hostile-identity vectors are already GREEN:

- null identity;
- legacy v1 prompt/schema identity;
- malformed v2 hash;
- wrong document schema;
- throwing identity source.

For each, the result is `Unavailable` with
`prepared_scan_identity_unavailable`, and runtime-resolution,
fresh-authorization, initiation-store, pair-builder, and preparation work
remain at zero. Existing valid identity and ordering characterization coverage
also remains GREEN.

## Required GREEN

Add only the smallest internal resource-provider/delegate seam needed to supply
resource names and byte streams to the embedded source. Preserve the default
parameterless production composition. The seam must reject missing or
duplicate resources, empty content, invalid UTF-8, and schema-inconsistent
content, and must compute the v2 prompt identity from the exact decoded UTF-8
text. Then rerun this focused filter and retain the coordinator zero-call
assertions.

`git diff --check` for the assigned test and evidence paths: clean.

## GREEN follow-up

The minimal Agents-owned correction added one internal resource-provider
constructor while preserving the public parameterless production path. It
validates the exact resource set and strict UTF-8/schema/hash authority without
changing the prompt, public Core port, Web coordinator or Scan behavior.

Focused rerun:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter
"FullyQualifiedName~PreparedPass1ContractIdentitySourceTests|FullyQualifiedName~PreparedScanInitiationCoordinatorTests"
```

Result: `29 passed, 0 failed, 0 skipped`. The Agents build completed with zero
errors and existing package/version warnings only.
