# Story 12.5 Track C-MVP support verification 01

## Boundary

- Definition checkpoint: `2b60081a514cc718895aed935cd5db4b494523b6`
- Accepted scope: `6e88b42958a615399a9b4167a1c9fc27f7229ea7cedcc7f4febedafd8c775400`
- Owned executable delta: exactly the two new Track C-MVP test files and `Story125ReleaseHarness.cs`
- Production-source delta: none

## T003 compatibility characterization

The first normal-output attempt was unobserved because another process held the
shared test assembly. Once T004 was active, the project correctly stopped at the
T004 compile RED. An isolated build then exposed two test-only execution issues:
repository-root discovery from an external output directory and one assertion
whose wording was narrower than the accepted contract. The test was made
portable through working-directory/AppContext/source-path discovery and aligned
to the exact accepted Markdown wording. No contract or production artifact was
changed.

Final command (isolated output, sanitized):

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:OutDir=<temporary-output> --filter "FullyQualifiedName~Story125TrackCMvpReportContractTests|FullyQualifiedName~Story125TrackCMvpPreflightTests" --logger "console;verbosity=minimal" -p:WarningLevel=0

Result: `18 passed, 0 failed, 0 skipped`. The three report-contract tests are
honest compatibility coverage over the already-correct current Markdown authority.

## T004 RED

Command:

    dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~Story125TrackCMvpReportContractTests" -p:WarningLevel=0

Observed RED: `CS0117` at both public calls because
`Story125ReleaseHarness.CreateOwnedDatabaseConnection` did not exist. The
preconnection test covered null, empty, wrong-case, malformed, system,
development, Story 12.4, fake/non-owned targets and one generated owned target.

The earlier `CS2012` shared-assembly lock was recorded separately as environment,
not as RED and not as a product/review failure.

## T005 GREEN

The minimal harness method delegates to `IsOwnedDatabaseName`, throws one closed
non-secret `InvalidOperationException` before factory invocation for rejected
targets, and returns exactly one factory invocation after acceptance. It adds no
database access, connection parsing, provisioner, cleanup or envelope semantics.

Final focused result: all 15 preconnection cases passed within the `18/18` slice.

## T006 surrounding regression

The already-built isolated assembly was copied into a Git-ignored output directory
under the repository so historical root-discovery behavior remained unchanged
without rebuilding locked shared intermediates.

    dotnet vstest <ignored-repository-output>/Vulgata.Tests.dll --TestCaseFilter:"FullyQualifiedName~Story125TrackCMvpReportContractTests|FullyQualifiedName~Story125TrackCMvpPreflightTests|FullyQualifiedName~Story125Tier2PostgreSqlTests|FullyQualifiedName~Story125ReleaseHarnessTests|FullyQualifiedName~Story125EvaluationContractTests" --Logger:"console;verbosity=minimal"

Result: `93 passed, 0 failed, 0 skipped` in 934 ms.

Warnings were the existing NU1507 central-package-source warning and existing
MSB3277 EF version-conflict warning during isolated compilation. `git diff --check`
passed with only line-ending advisories. No historical compatibility assertion
was weakened and no historical test file was changed.

## Owned file hashes

- `Story125TrackCMvpReportContractTests.cs`: `919794697db4796df5f7ceb866aeca9a1ef5de1eb2356741438fbae88dd51fd6`
- `Story125TrackCMvpPreflightTests.cs`: `c4f9fe0d1411ec785c040278b0ce0d2b57755ef2312149d1e06503458f57f6e8`
- `Story125ReleaseHarness.cs`: `d808f1f31feaa3a11ea8443e66715f20d87e0059f25c4c818261198365e12d52`

Verdict: `PASS`.
