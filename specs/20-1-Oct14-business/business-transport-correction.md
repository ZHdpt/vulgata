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

## One-hour diagnosed retry window (October 13)

The user extended paid BL retry authority for one hour, from 04:50:39 through
05:50:39 UTC. This supersedes the previous five-run count only within that window.
Each attempt remains bounded, uses a fresh launch marker and RunId, and requires
confirmed previous settlement. Stop at first verified success or the deadline;
indeterminate execution never authorizes another launch.

The correction distinguishes JSON syntax, typed witness shape, incomplete/output-
limited responses, transport failures and live authority failures in safe host
report fields. Only fixed enum values and allowlisted DTO labels are exposed;
no response content, exception text or arbitrary JSON paths are logged. Parser
failure produces no witness and no accepted relationship. The resolution prompt
clarifies the existing exact JSON object/enum/evidence shapes. Acceptance policy,
independent store revalidation and transport retry count remain unchanged.

Independent static review passed. Focused RED: 28 selected, 14 passed/14 failed,
from isolation-1b8a83689f0a4d20a1a5b674efd20ca8. GREEN: 157/157 passed (isolation-67a0ce501c4c44c7a037dc2d9542a64d), plus the current-attempt diagnostic retry regression 1/1 (isolation-28e9a70b8a3347f09dca16e3927c1598).
Read-only preflight reconfirmed 163 CL versions, 11 eligible v2 inquiries,
18 settled receipts and all seven existing publications before the next attempt.


### First run in the one-hour window

Checkpoint `bb1152f18aedd66d50b585c1338d45909bdbcbfb`; RunId
`e62bf7b3-1c9c-46e1-8081-524a0bd31744`, 05:05:42–05:10:37 UTC.
Five requests, 109,113 input + 58,527 output = 167,640 known tokens; exit 1,
no watchdog timeout. Four outcomes were durably Unresolved; the fifth produced
no witness, preventing synthesis/publication. All seven prior publications remain
unchanged, completion is confirmed, and all four stored outcomes independently
revalidate (complete contributor sets compared in canonical order).

Actual CLI output exposed a projection omission: internal resolution diagnostics
were created, but Program's manual JSON projection dropped the two new fields.
Correct the projection with defined enums and literal field allowlisting. The real
CLI regression RED selected 15: 3 passed/12 failed, isolation-996293fdf9f34976bdbdde5325c94182.
Prompt wording additionally clarifies exact copied observation fields, required
v2 BindingToken, and source-qualified opposition versus advisory/runtime caveats.
No accepted result is changed. A subsequent canonical continuation must reuse the
four retained Unresolved results and evaluate only the seven remaining inquiries.
No successful link or BL claim is made for this run. Evidence: ignored
`TestResults/business-links-run-3/`.

CLI projection GREEN: 148/148, isolation-0a08b92c2a3044c4a7a7e2c2978fb604; independent static review PASS. Next run limited to 25 minutes plus 60-second owned watchdog, within the same one-hour authority.

### Second run in the one-hour window

Checkpoint `ed8e90ec4873f0d6e80fa5e2cf41034874928201`; RunId
`0e5d5aeb-dcfb-449a-996c-7f13861f2e1d`, 05:17:31–05:19:19 UTC.
Two requests: 56,454 input + 19,814 output = 76,268 known tokens. One more
Unresolved outcome committed; the next response failed typed deserialization
(`WitnessShape`). Exit 1; no timeout, no new publication. All five stored outcomes
independently revalidate and all seven original publications remain unchanged.
The witness on the newly unresolved password reference used a wrong SectionId,
so exact claim lookup properly failed. These records are preserved, not repaired
by hand. Add safe fixed diagnostic labels for remaining witness fields, including
case-insensitive matching of known JSON property names, to diagnose the unprocessed
response shape without exposing raw values or paths. No schema/policy change.

Field-category RED: 13 selected, 9 passed/4 failed (isolation-3a19adc7d1c64d4e996795257a92345b). GREEN: 26/26 (isolation-35513d71a77e4906b4d0e36df898c1f5). Independent review PASS. Third window attempt is bounded to20minutes plus60secondwatchdog; preserves/reuses5existingUnresolved outcomes,6inquiriesremain.
