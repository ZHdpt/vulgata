# T012 Core Prepared Scan initiation identity observation

Status: **GREEN after test-vector correction**

## Public contract under test

`PreparedScanInitiationIdentityTests` pins independent literal vectors for the
Core-owned execution identity:

- CanonicalEnvelope domain
  `vulgata/story-12.5/prepared-scan-initiation-execution/1.0`;
- tags 1 through 10 in the accepted order (user, effective authorization mode,
  System, Repository, exact full ref, Profiled Revision, prompt identity,
  Document schema identity, canonical runtime JSON, runtime hash);
- literal 40-ASCII-character Profiled Revisions, literal execution IDs
  `e71c720d-1e9e-8a78-8b26-34788d6f67cb` and
  `c9074fec-6011-8b92-b902-551c2ebfbd2a`;
- literal coordination ID `444950ba-c4f5-832d-acbd-71995d284230`;
- reversal-stable pair ordering and independent one-field mutations for every
  replay-bound dimension.

The expected envelope, SHA-256 fingerprint, and identifiers remain independent
literals in the test. The test does not recompute expected values with the
production builder or use private reflection.

## Test-vector correction

The first post-implementation observation was 1 pass / 2 fail. The failures
were test-fixture defects: the expected tag-6 envelope length encoded 32 bytes
while the accepted Profiled Revision is 40 ASCII characters, and the copied
prompt literal contained one extra `a` byte. The expected coordination literal
was also recalculated independently with the accepted
`two-repository-scan-coordination/1.0` domain and LF-delimited execution IDs.
Only this assigned test file was corrected; no production behavior or contract
was changed.

## Focused and surrounding verification

Focused command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName~PreparedScanInitiationIdentityTests -p:WarningLevel=0 --logger "console;verbosity=minimal"
```

Result: **3 passed, 0 failed, 0 skipped**.

Surrounding Core command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~PreparedScanInitiationIdentityTests|FullyQualifiedName~PreparedCanonicalEnvelopeTests|FullyQualifiedName~PreparedCanonicalEnvelopeGoldenTests|FullyQualifiedName~Pass1CanonicalEnvelopeTests|FullyQualifiedName~AnalysisRuntimeIdentityTests|FullyQualifiedName~PreparedScanProductionLiteralQualificationTests" --logger "console;verbosity=minimal"
```

Result: **23 passed, 0 failed, 0 skipped**. Existing NU1507 and MSB3277
dependency warnings were emitted; there were no test or compile errors.

## Ownership and boundaries

The Core identity implementation is owned by T013/T014. This T012 correction
changed only independent test vectors and this evidence record. No EF migration,
DbContext/model-snapshot, Infrastructure, workflow, staging, or Git-history
change was made by this test slice.

## Root T014 verification

Root removed the temporary substring workaround left by the vector correction,
stored the corrected envelope as one independent literal, and added the explicit
Core dependency guard required by T014. A fresh-artifact rerun of
`PreparedScanInitiationIdentityTests`, both existing canonical-envelope suites,
and `Story12_2BoundaryTests` passed **13/13** with no failures or skips. The new
guard confirms the Core assembly and initiation port expose no EF,
`IQueryable`, or `DbSet` dependency. Existing NU1507, NU1900, and MSB3277
warnings were non-fatal.

Verified SHA-256 values:

- `IPreparedScanInitiationStore.cs`: `8bfd877d28745628080d3aa34d8cbfd36aa1b832c74f723eadba9ab12e26e4aa`
- `PreparedScanInitiationIdentityBuilder.cs`: `8b7d1a5d02406cd9c4e488f8e5d232dcd0433163795d370a49060eb15c5a9e5b`
- `CanonicalEnvelope.cs`: `7d415386f23baf5d269077b895642b16694995988f675c2cedd46297950a84e2`
- `PreparedScanInitiationIdentityTests.cs`: `9fe2bcbc9afb07c392cd2c7cef9530cb380fb16fe0f74a8e6dd819a144e76d49`

`git diff --check` passed with line-ending advisories only.
