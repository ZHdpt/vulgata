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

## Authorized rerun outcome

Implementation checkpoint: `348f90e5a55252e54e65bba74190c62dfa33f194`.
Run `64a9cb86-d08d-4758-862a-4d193fbb501f`, PID 54132, started at
2026-10-13 04:40:49 UTC. BeginBusinessFromCl committed at 04:40:53 UTC and
CompleteBusinessFromCl at 04:42:02 UTC. Process exit 1 after 73.47 seconds;
no watchdog expiry and no automatic retry. This is invocation four of the five-run
grant; one remains unused.

One provider response returned with known usage: 15,472 input plus 13,864 output
= 29,336 tokens; model wait 68.0154458 seconds. Resolution then returned
`business_resolution_ineligible`; matching and Business are Unavailable,
publication NotRequested. **Zero accepted v2 outcomes/edges; zero new BL.**
The requested transport/configuration corrections are implemented and controlled-
tested, but successful real link generation is still unqualified.

This differs from the earlier 100-second no-response failure: a response and usage
were obtained. The current executor maps several post-response failures (including
parsing/validation exceptions and authorization failures) into a coarse admission
status, and the host does not preserve a specific resolution diagnostic or raw
witness on that exit. Therefore malformed JSON, invalid field shape, or a particular
authorization cause cannot be established from this run's retained evidence.
Do not weaken acceptance or guess an accepted edge from a failed response.

Post-run authenticated read-only verification confirmed the matching completion
receipt and unchanged exact membership/content projections of all seven previous
publications. The 163 original CL versions remain readable. No new ordinary-user
BL reading was possible and no temporary reader was created. No retained outcome,
receipt, launch marker or publication was deleted or replaced.

Evidence: ignored `TestResults/business-links-run-2/` contains authorization,
preflight, process/terminal report, operation log and `after-run-verification.json`.
The next bounded correction should expose safe resolution failure categories and
controlled response-validation diagnostics before spending on another attempt.
No additional paid run or diagnostic-code expansion was performed here.
