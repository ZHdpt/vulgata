# Business transport correction and one bounded link rerun

User authorization (2026-10-13): fix the timeout, apply low reasoning to
resolution, run focused tests and independent review, then rerun one bounded
scan while preserving existing publications. Starting checkpoint is `5f3956d7`.

The accepted narrow plan changes only the nonstreaming Business client in
`OctoberWorkerClientFactory.cs`. Set SDK NetworkTimeout to infinite, matching the
existing HttpClient setting, so linked owner/lease cancellation remains
authoritative (resolution 120 seconds; synthesis 900 seconds). Keep SDK retries
disabled and await transport cleanup. Apply configured low reasoning to resolution
through cloned request options; preserve synthesis thinking-disabled strategies
and the separate output limit. CL streaming and acceptance semantics are unchanged.

Focused tests use the actual installed SDK pipeline and controlled HTTP transport:
effective network timeout, resolution wire reasoning, unchanged caller options,
owner/lease cancellation, synthesis strategies and legacy transport behavior.
Independent plan review accepted this boundary; final implementation review and
passing tests are required before the single paid rerun.

Verification complete: focused RED selected three tests, with two expected failures
(SDK message timeout 100 seconds instead of infinite; resolution wire high instead
of configured low) and one cancellation pass. Evidence:
`TestResults/isolation-7835fb1ffdb64f79be6ffc18d093edb5/results.trx`.
After correction, all 423 selected SelfContained tests passed with zero skips,
covering the file Worker, resolution, Business transports and synthesis wire
strategies: `TestResults/isolation-26c8c90ed7ef488d87cd7742b3f95350/results.trx`.
Independent implementation review passed without remaining findings. These are
controlled SDK/transport tests, not a claim of real link-generation success.

The rerun reuses exact original CL publication
`fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`, selects only `authMethods`, and uses the
retained-evidence v2 profile with its 11 corpus candidate inquiries. Limits remain
26 provider requests, 45 minutes, 20 million total tokens, DeepSeek/deepseek-flash,
393,216 output tokens and configured low reasoning. Use a fresh RunId and exclusive
launch marker. Reconfirm prior settlement and all seven immutable publications;
never delete receipts or retry an indeterminate provider operation. No migration,
CL regeneration, other-lane changes, Web restart, merge or push is authorized here.
