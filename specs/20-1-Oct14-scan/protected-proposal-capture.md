# One-run protected final proposal capture

2026-10-10. OCT-Integrator (`01a1109d-ec4c-7500-8b73-642a5e22f59b`)
reported the human's explicit approval of one diagnostic run: capture only the
failing final proposal, at most 256 KiB, in protected local storage outside Git;
Integrator inspects it, retains only a sanitized diagnosis and deletes the raw
capture afterward. Reasoning, requests, credentials and tool payloads are excluded.
This is not approval for a paid retry series or output repair.

The lane starts clean on `oct14/scan`,
`3a17cd1ae9bb0e67611231a828a0f8f1bc6ad82f`. Its affected production matches
approved main `bcf2e891`; no reset or replacement is required. Implementation is
limited to the Scan executor and Generator composition/sink/report seam, controlled
tests and this record. Web, shared Knowledge contracts, schemas and migrations
remain outside this change. Earlier partial CL remains preserved.

The feature is default-off. Agent execution receives only an injected callback;
it receives no filesystem authority. The trusted Generator binds one configured
diagnostic operation to the exact original run/session/target/batch. Capture is
eligible only when parsing the exact final LastText fails while the phase remains
ProposalParse. Success, later proposal validation, transport, tools and terminal
failures are excluded. Strict UTF-8 must fit 262,144 bytes; invalid encoding and
oversize input are rejected without truncation. Empty final text remains meaningful
diagnostic evidence and must not fall back to previous text.

Capture requires fresh source-ledger verification of all observed supporting
sources and contributor authority, within the original lifetime and cancellation
boundary. It cannot turn a rejected or revoked response into accepted Knowledge.
Storage is a newly created protected local directory outside Git, with pinned
non-reparse ancestry and a fixed CreateNew file. No existing foreign object may
be adopted, overwritten or deleted. The one-attempt allowance is atomic; capture
failure must preserve the original Worker outcome and report only a finite status.
Cleanup uncertainty must be distinguishable from confirmed absence.

The lane will exercise synthetic text and owned temporary paths only. Integrator
owns the single approved real run, local inspection and verified raw deletion.
No real provider/native/PostgreSQL/source-root/capture operation is performed here.

## Opt-in contract

The explicit configuration section is `October:ParseFailureCapture`:
`Enabled=true`, `TargetId`, `BatchId`, `SubjectId`, `ManagementAuthorityId` and
`Directory`. There is no shared SessionId to invent: runtime binding uses the
actual session object in addition to those existing identities, and the new
original RunId. Absence disables the feature; a section containing only
`Enabled=false` also disables it. Partial, conflicting or unknown fields fail
configuration without echoing their values.

The Generator executable reads environment configuration. Integrator supplies
these six keys only to its single owned Generator child process, using the actual
authorized values (the placeholders below are not runnable bindings):

```text
October__ParseFailureCapture__Enabled=true
October__ParseFailureCapture__TargetId=<exact target>
October__ParseFailureCapture__BatchId=<exact batch UUID in D format>
October__ParseFailureCapture__SubjectId=<actual operator subject>
October__ParseFailureCapture__ManagementAuthorityId=<actual management authority>
October__ParseFailureCapture__Directory=<fresh absolute local directory>
```

Do not persist these variables or carry them into another run. All existing
source/provider/Identity/budget configuration remains necessary and unchanged.
The original internally generated RunId is bound once; configuration cannot
replace it. A different live session or run cannot reuse the sink.

`Directory` names a fresh nonexistent child beneath the operator's owned local
run directory, not an existing directory to adopt. The sink creates and protects
it; the only capture filename is `final-proposal.txt`. Successful capture remains
outside Git for Integrator's one local inspection and subsequent deletion. Never
print its contents through stdout/stderr, a report, chat/tool output or a tracked
artifact. Retain only the authorized sanitized diagnosis.

The additive safe report field is `workerDiagnostics.captureStatus`. Its finite
values are `Captured`, `TooLarge`, `Unencodable`, `Suppressed`, `Failed`,
`AlreadyAttempted`, `CleanupUnconfirmed`; null means disabled, inapplicable or
unknown, not confirmed capture. Existing disclosure fences can clear the entire
Worker diagnostics object. `Captured` is diagnostic storage only, never accepted
Knowledge or successful generation. `CleanupUnconfirmed` requires inspecting and
cleaning only the owned destination before any later authorized operation.

Extract only the safe result from the existing Generate report, for example
`(Get-Content -LiteralPath $ownedStdout -Raw | ConvertFrom-Json).workerDiagnostics.captureStatus`.
The report contains no capture path or captured text. On `Captured`, the raw file
is still untrusted model data; inspect locally without executing it or echoing it
into retained tool/chat output. Integrator verifies deletion after diagnosis.

The storage implementation is Windows-only. Destination and exclusion syntax must
be explicit local-drive paths; ambiguous mappings, reparses and Git markers are
rejected. Configured source, tool state/read/install, key, log and operator/config
resources are excluded. Successful storage uses a protected DACL granting only
the current process user, checked on the owned directory and file. It grants no
Agent filesystem or new tool authority.

The initial Worker-only RED used inert enum/callback/result scaffolding, without
capture behavior. Standard SelfContained FileWorker class run
`e20816c4456d45e7bee5e330d68b2833`: 157 selected, 152 passed, five failed, zero
skipped; build zero errors. Failures were the missing callback/status, byte-bound
outcome and callback ownership. Existing outcomes and excluded boundaries passed.

Generator RED `5592d5e3cb894e6e9bd5061525536d9d` selected the complete Worker,
GenerateRunner and EntryPoint classes: 272 selected, 244 passed, 28 failed, zero
skipped; build zero errors. Worker behavior and its fresh-revocation, unknown-status
and original ProviderFailed controls passed. Failures concerned the inert Generator
binding/configuration/storage/forwarding and the additive safe report field.

Initial affected GREEN `10d10d6f1d9e47b188dd7ed4a44ffbd8`: 280/280 passed, zero
failures/skips. This includes actual owned-temp Windows ACL, reparse, Git marker,
CreateNew collision and concurrent/late-cancellation controls. It is not provider,
native-tool or PostgreSQL qualification. Independent review then required local
syntax validation of exclusions before native opens and compatibility with JSON
configuration's boolean representation; final evidence follows after correction.

Correction RED `ab2a3a98125c4c4595451034ac956d3d`: 85 EntryPoint cases selected,
78 passed, seven failed, zero skipped, build zero errors. Actual JSON true/false
and equivalent layered booleans reproduced the configuration defect; four
syntax-only UNC/device/relative exclusion cases reproduced missing preflight.
Those cases did not open their supplied paths. Contradictory booleans and a local
drive-root exclusion retained their expected behavior.

Final GREEN `12e1980563fd41ee97fb3f3d5c9fd96f`: **321/321 passed**, zero
failures/skips, build zero errors, using the standard SelfContained launcher and
all four affected classes:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~OctoberScanFileWorkerTests|FullyQualifiedName~OctoberScanGenerateRunnerTests|FullyQualifiedName~OctoberScanEntryPointTests|FullyQualifiedName~OctoberScanExecutionScopeTests' -TestTimeoutSeconds 300 -CommandTimeoutSeconds 900
```

The separate test and production owners assessed the final boundary. Independent
review accepted both corrections and the complete implementation; no findings
remain open. Existing analyzer warnings remain, and `git diff --check` passed.
The tests reuse existing classified sources and controlled transports/fixtures.
This is focused controlled verification, not a claim of full-suite, PostgreSQL,
native-tool, provider or real-capture qualification. No real-run resources or
real payloads were accessed by this lane. The original strict parser, rejection,
retry, acceptance and publication rules remain unchanged.
