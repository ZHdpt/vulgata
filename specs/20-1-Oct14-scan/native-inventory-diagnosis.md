# Bare Java inventory failure diagnosis — October 8

## Scope and boundary

OCT-Integrator relayed the user's authorization to diagnose the additional failed B run `ea77060cab984234a9a5de09ec6b2324`. That Generate report had zero provider requests/tokens, no accepted files, and an inventory gap `source-authority-or-identity-lost`. This lane was authorized only for source-free native diagnosis using owned temporary paths and the pinned tools. Production edits require a concrete diagnosis and agreed ownership. No provider, PostgreSQL, real source, global environment change, or unrelated process cleanup was performed here.

The assigned `oct14/scan` checkout was clean at `1bb1f2e7849e62cf904a0d2b1cc6a1b2e3fc5d36`. Relevant Generator/CodeGraph/source production files matched approved `ec493fcf`. The exact synthetic `AccountPolicy.java` text and bare-root shape were read from that commit's `OctoberHostSelectionTests.cs`, `TryInstallApprovedOperationBInputsAsync`; no other checkout was inspected.

## Test and observations

The additive LocalTools diagnostic is `OctoberRuntime_BareAccountPolicy_InventoryFirst_PreparesAndSelectsExactOwnedFileAsync` in the existing `OctoberScanToolRuntimeQualificationTests.cs`. It creates a fresh child directory within an owned prepared-source fixture, containing only that Java file. Its first operation is `OctoberScanInventory.BuildAsync` → `OctoberToolRuntime.ListAsync` → actual CodeGraph initialization. There is no prior Prepare call, C# beacon, project metadata, JDT invocation or provider. Authority is explicitly controlled Current, isolating native preparation from real Identity/persistence.

All runs used the repository `eng/test.ps1 -Suite LocalTools` launcher, exact method filter, 300-second test and 900-second command limits. Source assertions require complete inventory, exactly the selected file, no gaps, a prepared descriptor and verified owned cleanup. Diagnostic exception output is bounded and scoped to the invocation, with type/HResult/throw-site and allowlisted codes only. Process output is measured by byte count, not disclosed.

| Run | Changed condition | Result |
| --- | --- | --- |
| `3d8488f038e4427ab14cebbb51f4833a` | Public factory, ordinary testhost environment | 1/1 PASS, 0.997 s; complete inventory, one entry, no limitations/exceptions, descriptor present; cleanup verified |
| `7ece1dd28e194985a24827a90b681989` | Paired ordinary environment / native child environment reduced to B's available keys | 1 passed, 1 failed, zero skipped. Ordinary row passed in 0.958 s. Reduced row failed in 0.563 s: native init exit 0, no stdout/stderr, but database absent at the factory check; `CodeGraph preparation did not complete` was caught and mapped to AuthorityUnavailable; zero entries and no descriptor |
| `b62fa986d5354e04b641713930cf1915` | Reduced native child environment plus **only PATHEXT** | 1/1 PASS, 0.979 s; native exit 0, stdout 560 bytes, stderr 0, complete one-file inventory, no exceptions/limitations, descriptor present; cleanup verified |

The final run selected only the changed-condition row with `FullyQualifiedName~OctoberRuntime_BareAccountPolicy_InventoryFirst_PreparesAndSelectsExactOwnedFileAsync&DisplayName~reduced-plus-PATHEXT`. The earlier known failing condition was not silently reclassified as success.

The reduced native child contained exactly these key names: `CODEGRAPH_NO_DAEMON`, `CODEGRAPH_NO_DOWNLOAD`, `CODEGRAPH_NO_UPDATE_CHECK`, `CODEGRAPH_NO_WATCH`, `CODEGRAPH_TELEMETRY`, `DO_NOT_TRACK`, `GIT_CONFIG_GLOBAL`, `PATH`, `SYSTEMROOT`, `TEMP`. The successful changed condition added only `PATHEXT`. Other incoming SDK defaults, including profile/application-data keys, were not restored. The test's delegating runner preserves the actual pinned preflight and native runner, arguments, working directory, output bounds, cancellation and deadlines. It changes neither the testhost environment nor persistent settings.

The reduced row also failed fixture deletion because the source directory was in use. A later read-only check found `.codegraph/codegraph.db` had appeared after the outer launcher had reported exit. The installed PowerShell shim invokes `node.exe`, whose installed npm shim synchronously launches the bundled Node. The single-key experiment establishes the missing-PATHEXT condition as the cause of the reproduced early-return/readiness failure; it does not require attributing it to an unverified internal PowerShell implementation detail.

## Cleanup and disposition

The failed diagnostic retained a partially cleaned fixture at `C:\Users\13728\AppData\Local\Temp\vulgata-prepared-source-6e66a22ef8584d00bcfe1b31c20de1fe`. Integrator explicitly authorized its later owned cleanup. Read-only checks found no matching live child (excluding the diagnostic query), verified the exact temporary parent/name and absence of reparse points, and obtained exclusive access to remaining files. Only that literal fixture path was removed; final absence was verified. No process was killed.

Independent review accepted the diagnostic design and unchanged safety assertions. The RED result is intentionally retained as evidence, not a claim that the whole diagnostic selection passed. The successful single-key row is native inventory qualification only, not full Generator, JDT, Identity, PostgreSQL, provider or ordinary-reader qualification.

Integrator explicitly directed that the final routine LocalTools theory retain only the two declared valid profiles, `full` and `reduced-plus-PATHEXT`. The intentionally invalid reduced profile is not a permanently failing routine test; no readiness, inventory or cleanup assertion is weakened. Its exact historical reproduction delta is to add `[InlineData("reduced")]` alongside those two rows in the same theory. That invokes the retained filter without restoring PATHEXT and reproduces the recorded missing-database/early-return condition. This is a reproduction recipe, not authority to run it or abandon its owned cleanup.

The reduced-only row was removed from routine selection as directed. Final current-source LocalTools regression `cd45c26426cb4b2795be8bc1b73009a8` selected and passed both valid-profile rows, zero failed/skipped, with successful current-source build and verified cleanup. This **2/2 GREEN** is distinct from the historical paired RED and the single-key experiment. Independent final review accepted the test/evidence boundary contingent on this now-passing verification. All production files remain unchanged.

The smallest demonstrated correction belongs to Integrator: preserve the required `PATHEXT` in `StartGenerator`'s explicit child-environment allowlist. The production native adapter already forwards the SDK default when its parent supplies it. No Scan production change or broad environment inheritance is justified by this evidence. Integrator separately reported real authority issuance/verification and real authority-to-contained-listing checks passing; those are owner evidence, not runs performed by Scan. Further Generate/provider execution remains coordinated by Integrator under the applicable user grant.
