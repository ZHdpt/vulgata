# Story 18.1 RootUnavailable: execution-permission diagnosis

Date: 2026-09-29
Source checkpoint: `90e34ace483f639b1beca245f057e44325cf9553`
Assigned worktree: `C:\Users\13728\.codex\worktrees\7658\vulgata`

## Outcome

The unchanged production and test sources pass the complete configured SelfContained suite: **1975 selected, 1975 passed, 0 failed, 0 skipped**. The blocker is the restricted execution context inherited through the repository native-process launcher, not full-suite interference. Run this filesystem qualification with approved execution outside that sandbox (or an ordinary authorized PowerShell session).

## Authority and scope

The user requested diagnosis and resolution in this worktree and explicitly allowed working outside the story workflow restrictions. This investigation does not change workflow-state.json, recovery counters, approvals, story lifecycle, or historical failed evidence. The responsible story root can reconcile this new execution evidence separately. No production behavior, test assertions, suite selections, or collection settings were changed.

## Controlled comparison

All tests below used the current assembly built from the unchanged source checkpoint by the full configured runner. The focused comparisons reused that assembly without building.

| Execution | Selected | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Full `pwsh -NoProfile -File eng/test.ps1`, approved outside sandbox | 1975 | 1975 | 0 | 0 |
| Focused direct `dotnet test` tool invocation | 119 | 119 | 0 | 0 |
| Focused via `eng/test-native.ps1` / `Invoke-TestNative`, restricted shell | 119 | 31 | 88 | 0 |
| Same focused launcher command, approved outside sandbox | 119 | 119 | 0 | 0 |

The restricted launcher reproduces exactly the 88 failing test names in `TestResults/isolation-750f6fde15084641a45b2b0ceb8e60a1/results.trx`: 87 expected Issued and one expected Denied, all actual RootUnavailable. No other test collection is needed to trigger them. This rules out full-suite size or cross-collection parallelism as a necessary cause.

A temporary in-memory compilation of the actual PreparedSourceTraversal source added native-status output without editing production files. In the restricted shell, NtCreateFile failed with **0xC0000022 (access denied)** at the second ancestor component below the volume root. The identical probe outside the sandbox opened every component and returned Opened. Creating the unique temporary fixture itself succeeded in both contexts. The adapter requires handle-relative opens of ancestors; permission to create the leaf fixture alone does not establish that those opens are permitted. The issuer intentionally sanitizes the failure to RootUnavailable.

The direct test invocation and the wrapper did not have equivalent effective access in this environment. A direct focused pass therefore cannot establish the wrapper's execution readiness. Use the same launcher and permission mode for focused and complete verification.

## Reproduction

The small reproducer uses the repository's actual launcher (run from the assigned worktree):

```powershell
. ./eng/test-native.ps1
$probe = Invoke-TestNative dotnet @(
    'test', 'tests/Vulgata.Tests/bin/Debug/net10.0/Vulgata.Tests.dll',
    '--filter', 'FullyQualifiedName~Vulgata.Tests.PreparedSource',
    '--verbosity', 'quiet'
) 60
$probe.ExitCode
$probe.Output
```

It produced 88 failures in restricted execution and 119 passes with approved execution outside the sandbox. The operational resolution is to run the standard command in the latter execution context:

```powershell
pwsh -NoProfile -File eng/test.ps1
```

Do not weaken containment, bypass ancestor checks, suppress failures, or reclassify the tests to hide this environment prerequisite. No administrator-token requirement was established; approval here refers to execution outside the Codex filesystem sandbox.

## Evidence

- Passing configured run: `TestResults/isolation-1f9b96fbdc0e491ea0a82004d0a8ec93/results.trx`
- Same run's build, policy/fixture probes, discovery, and execution logs are in that directory. Build: zero errors, 7205 warnings. Runner exit: 0.
- Focused restricted launcher TRX: `%TEMP%/vulgata-181-diagnosis/restricted-wrapper.trx`
- Focused approved launcher TRX: `%TEMP%/vulgata-181-diagnosis/approved-wrapper.trx`
- Direct focused TRX: `%TEMP%/vulgata-181-diagnosis/restricted.trx` (filename identifies invocation request, not proof of the child process's effective restrictions).
- Machine-readable durable counts, error grouping, matched failure names and artifact hashes: `rootunavailable-execution-diagnosis-01.json` beside this report.

Raw TestResults and temporary files are local, disposable evidence; the adjacent JSON retains the comparison. Temporary probe instrumentation was never written to production and was removed after diagnosis.

This qualifies the complete SelfContained regression boundary only. PostgreSql, LocalTools, External, E2E and HistoricalBoundary were excluded by the standard runner and are not newly qualified. Story completion and workflow resumption are not claimed.
