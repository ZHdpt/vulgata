# T045 exact-owner Prepared Pass 1 integrity RED

Date: 2026-08-31 (Asia/Shanghai)

## Scope

T045 adds test-only coverage for the server-owned `SymbolName` boundary. The
assigned tests cover both identity forms: path-v1 (`RepositoryRelativePath`)
and semantic-v2 (the reviewed fully qualified owner symbol). They assert that
the value is carried through the prepared handoff, claimed-source hydration
and exact re-fetch, the Web job-runner dispatch bridge, dispatcher assignment
and packet validation, and executor authorization/integrity boundaries.

The prompt/tool/provider checks are characterization coverage: the current
provider request already uses strict streaming and does not include
`SymbolName`; T047 must preserve that provider-visible shape while adding the
server-only packet checks. No RED is expected from those characterization
checks.

## RED observation

Focused command attempted:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~PreparedHandoffProjectionTests|FullyQualifiedName~Pass1ProductionCompositionTests|FullyQualifiedName~PreparedPass1WorkerDispatcherTests|FullyQualifiedName~Pass1ClaimedPacketExecutionTests|FullyQualifiedName~PreparedPass1JobExecutorStreamingTests|FullyQualifiedName~Pass1DeepSeekPromptCompositionTests" --logger "console;verbosity=minimal"
```

The first attempt observed **0 executed; UNOBSERVED** because the shared test
output was locked by another `dotnet`/test process (`CS2012`, unable to open
`tests/Vulgata.Tests/obj/Debug/net10.0/Vulgata.Tests.dll`). After that process
completed, the focused build succeeded (`0 errors`, existing package/version
warnings only) and the six-test focused run produced:

```text
Failed: 4, Passed: 2, Skipped: 0, Total: 6
```

The output lock was an environment condition and is not counted as a product
RED or review failure.

The follow-up claimed-source-only run produced:

```text
Failed: 1, Passed: 0, Skipped: 0, Total: 1
```

Therefore the six-vector classification is **4 product RED, 2 GREEN
characterization, 0 environment-blocked**. The six-test aggregate was not
repeated after the isolated-database rerun; its earlier `4 failed / 2 passed`
total remains numerically consistent after replacing the one environment
failure with the observed product failure.

The first compile attempt also reported unrelated concurrent T043 test-file
diagnostics in `AnalysisRuntime/JdtLsAdapterTests.cs` (missing
`PropertyInfo` using) and `PreparedScanning/PreparedScanBoundaryTests.cs`
(`Assert.NotNull` overload). Those files are outside T045 ownership. The
subsequent clean build reported no compiler diagnostic in the six T045 files.

Observed product REDs are the absent public `SymbolName` on
`PreparedPass1FileCodeUnitAssignment` and `PreparedPass1WorkerPacket`; the
three contract tests failed at those public seams before T047 production work.
The claimed-source test was then rerun through the approved process-local
isolated PostgreSQL mapping. Its fixture lifecycle completed without printing
or persisting credentials or database identifiers, and the test produced an
honest product RED: `CurrentClaim_HydratesServerOwnedPathOrSemanticSymbolNameAfterExactRefetchAsync`
failed at line 64 because the claimed packet lacks the revalidated
server-owned `SymbolName`.

Expected-vs-actual at the code boundary:

| Boundary | Expected after T047 | Current source observation |
| --- | --- | --- |
| Prepared assignment | Required `string SymbolName` | **RED:** property absent |
| Prepared worker packet | Required `string SymbolName` | **RED:** property absent |
| Claimed-source re-fetch | Same symbol before/after exact source read | **RED:** fixture completed; claimed packet lacks `SymbolName` |
| Job-runner bridge | Forward without defaulting/inference | **RED:** packet/assignment contract has no symbol value |
| Dispatcher validation | Reject omitted/mutated/drifted symbol before executor | **RED:** packet/assignment contract has no symbol binding |
| Executor provider boundary | Reject integrity drift before provider resolution | **GREEN characterization:** request shape remains symbol-free |

## Characterization expectations

The T045 prompt and executor tests separately preserve already-correct behavior:

- the embedded prompt exposes only the closed `navigate_code` and
  `submit_code_logic` tools and contains no `SymbolName` marker;
- executor requests remain strict streaming requests and contain no
  `SymbolName` JSON member;
- no provider request, prompt, tool argument, submit payload, or navigate
  payload is permitted to receive the server-only symbol.

The clean focused run observed both characterization tests as GREEN. The
claimed-source vector is now confirmed as product RED with the isolated
PostgreSQL prerequisite satisfied.

## Files changed

Only these six assigned test files were modified:

- `tests/Vulgata.Tests/PreparedScanning/PreparedHandoffProjectionTests.cs`
- `tests/Vulgata.Tests/PreparedPass1/Pass1ProductionCompositionTests.cs`
- `tests/Vulgata.Tests/PreparedPass1/PreparedPass1WorkerDispatcherTests.cs`
- `tests/Vulgata.Tests/PreparedPass1/Pass1ClaimedPacketExecutionTests.cs`
- `tests/Vulgata.Tests/PreparedPass1/PreparedPass1JobExecutorStreamingTests.cs`
- `tests/Vulgata.Tests/PreparedPass1/Pass1DeepSeekPromptCompositionTests.cs`

Minimal T047 production owners remain the accepted Core, Infrastructure,
Agents, and Web identity seams listed in `tasks.md`; no production file was
changed by T045. `git diff --check` passed for all six files. The pre-existing
user-owned `.env.example` and concurrent T043 test changes were preserved.
