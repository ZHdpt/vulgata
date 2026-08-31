# T043 exact-resolution owner-binding diagnosis

## Pinned boundary

- Branch: `demo0831/story-12-5-trackC`
- Source checkpoint before this working group: `705657c9c4fbea26724930f206fd880277038cc0`
- Accepted specification: `6da3e251b217ab8dd8190f84c4cf864b994be1e87090558a29e125f5648ec8b7`
- Accepted plan: `35766983a07db621efbdef3fa45934b2f66d86514d15deb490b5eca83ca75c43`
- Accepted tasks: `6ba9c8ed9336d3526b1d842a527e42381121be6adf1dddd936c4a529aebee041`
- Database boundary: a fresh lowercase disposable `vulgata_story125_*` database created by `PreparedScanInitiationPostgreSqlFixture`; no retained run-03 database was modified.

## Reproduction

The focused PostgreSQL vertical entered through the authenticated production
Prepared Scan endpoint, published two Manifests, and completed every required
Pass 1 job (`124/124`) to `ReadyForResolution`. The same exact committed source
snapshots were then passed to the unchanged Story 12.4 coordinator.

Sanitized command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --filter FullyQualifiedName~ReviewedExactProfileSeedPostgreSqlTests.PostPreparedScan_ThenPublicPass1 --logger console;verbosity=minimal
```

Observed result before characterization:

```text
Exact outcome: Refused/refused
Reviewed occurrence account-to-channel-cust-update-session-v1: Consumers=1, Providers=1
Persisted unsupported owner path: src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java
Persisted CodeUnit.SymbolName: src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java
Required profile owner symbol: cn.com.yitong.actions.cust.UploadOcrToOssOp
```

The focused run failed only at the expected `Applied|AlreadyApplied` assertion;
duration was 33 seconds. The diagnostic is retained as a green characterization
named `PostPreparedScan_ThenPublicPass1_ExactResolutionOwnerSymbolMismatch_IsPreciselyCharacterizedAsync`.

Checkpoint verification after characterization:

- complete `ReviewedExactProfileSeedPostgreSqlTests`: **4 passed, 0 failed, 0 skipped**;
- surrounding T037-T042 focused/compatibility set: **89 passed, 0 failed, 0 skipped**;
- Web build: **0 errors** (9 existing package/reference warnings);
- `git diff --check`: no whitespace errors (line-ending notices only).

The deliberate rejected-worker case initially exposed a test-fixture omission:
its Pass 1 revision registry had no exact snapshot roots, so the pump could not
claim jobs. Binding the same generated exact source roots made the unchanged
required-job path reach `FailedPass1`; this was fixture correction, not a
production change or a second product gap.

## Root cause

This is not a CodeGraph, source-selector, authorization, revision, profile-pair,
Pass 1, consumer-extraction, or provider-extraction failure.

`CodeUnit.CreatePreparedFileCodeUnit` assigns the Repository-relative file path
to `SymbolName`. `PreparedScanPairBuilder` asks `IPreparedTopLevelSymbolSource`
for Java ranges only when a Java file exceeds the whole-file bounds; an ordinary
small Java file therefore persists only the whole-file File Code Unit with the
path-valued symbol. The unchanged exact coordinator's unsupported-observation
mapping requires both the exact path and the profile's fully qualified semantic
symbol. Its query finds zero owners, the mapped count differs from the profile
count, and the coordinator returns its generic fail-closed `Refused` result.

## Classification

T037-T042 remain coherent and green. T043 remains incomplete because the
accepted current exact decision, REFERENCES Edge, and honest Uncertainty cannot
be published from the production Prepared Scan entrance.

The earliest correction owner is **plan**: the accepted plan promoted required
paths but did not define how the Prepared Scan/Pass 1 boundary supplies the
semantic owner identity required by the unchanged Story 12.4 contract. A fix
requires an explicit architecture choice between a narrow matched-profile
semantic Code Unit authority and a differently authorized current source-owner
mapping. It may not be inferred inside T043 verification.

No production fix, EF/model change, Story 12.4 mutation, CGC fallback, database
repair, remote LLM call, or run-04 release action was performed during diagnosis.
