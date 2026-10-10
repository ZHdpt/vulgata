# Final proposal JSON wire assessment

2026-10-09. OCT-Integrator (`01a1109d-ec4c-7500-8b73-642a5e22f59b`)
dispatched source-free assessment after the real run reported
`InvalidOutput / ProposalParse / Syntax / Root`, retry `None`. The reported run
took 54.94 seconds and three requests, with 15,195 input and 3,696 output tokens
known, 77 inventory files and zero accepted files. These are report facts, not
evidence of the response's text or of provider JSON-mode compliance. The raw
response was not retained. No source, response fragments, arguments or secrets
were inspected or printed in this assessment.

Scan started clean on `oct14/scan` at
`268c5aea050866dc2e05366af70fdf1305d959dc`. Its executor, prompt and Worker client
factory match approved integration `52fa489b`; no replacement or reset was needed.
Integrator's later `faa3cd88` records the failed run. This checkpoint changes only
an existing controlled test file and this assessment, preserving production.

## Actual SDK wire controls

The tests execute `OctoberWorkerClientFactory` and `OctoberFileWorkerExecutor`
through the installed MAF and OpenAI SDK with the existing controlled HTTP handler.
They inspect only synthetic request JSON in memory. They do not capture headers,
real requests, native output or provider traffic. Assertions run after execution,
so a failed wire assertion cannot be swallowed as a Worker transport failure.

| Control | Observed behavior |
| --- | --- |
| Initial request and request after a tool result | Both carry exactly `response_format: {"type":"json_object"}`; the strict proposal prompt and final-window metadata remain present. The second includes tool history. |
| Two application source windows | Both carry JSON mode; the exact synthetic source reconstructs from the windows, with ordinals 1/2 and `finalWindow` false/true. |
| Initial tool leaf contains prose, final leaf contains a valid proposal | Only the final proposal is parsed; expected controlled unit and source binding are retained. |
| Initial tool leaf contains a valid proposal, final leaf is empty | The earlier proposal is not reused. Rejected as `Syntax/Root`, with no proposal. |
| Final leaf contains fenced JSON or prose | Both are strictly rejected as `Syntax/Root`, with no proposal. |
| JSON nesting depth 32/33, using an unknown synthetic property | Depth 32 reaches the closed-field guard; depth 33 fails as `Syntax/Root`. Neither is an admissible proposal. |

All tool-continuation cases assert known usage and exactly-once settlement/disposal
for both leaves. The multiwindow control deliberately returns non-JSON `continue`
at the first Stop leaf: this demonstrates parsing occurs after the final window;
it does not demonstrate that a real provider honors JSON mode on intermediate
windows. No new fixture, dataset, test source or suite inventory entry is needed.

Static inspection agrees with the wire evidence: the bounded Worker client clones
ChatOptions and changes MaxOutputTokens without removing ResponseFormat. The
factory forwards those options to the actual SDK. JSON mode requests generic JSON,
not the application's closed proposal schema. The deployed provider's compliance
is not established by serialization controls.

## Prompt and final text selection

The prompt defines the exact closed schema, case sensitivity, explicit nullable
members, integer rules, primary-file anchors and JSON without fences/prose. It
requests a whole-file proposal when the application requests the final proposal,
and says not to substitute a final proposal for unread windows. Every user window
contains `finalWindow`, window ordinal/count and the source coordinates. The prompt
does not explicitly equate `finalWindow: true` with that final request, and there
is no separate final natural-language instruction. This is a clarity opportunity,
not an established cause of the live syntax rejection. No prompt change is made.

Each provider leaf starts a fresh text buffer. TextContent contributes to LastText;
reasoning content is separately accounted and does not contribute to that buffer.
The settled leaf replaces LastText even when empty. Only the last leaf after all
source windows is passed to the proposal parser; whole history and initial tool
prose are not concatenated into it. Empty Stop with no pending calls can therefore
reach the JSON parser and receive Syntax/Root. Existing terminal guards continue
to reject unsettled, length-limited or pending-call completions. Their absence in
the live failure does not prove the actual text was complete JSON.

The parser uses MaxDepth 32 before root/schema guards. Syntax/Root thus includes
empty input, malformed JSON and excessive nesting. It cannot establish fences,
prose, truncation, depth overflow or a provider defect on its own. There is no
demonstrated need to raise the depth limit or repair/normalize model output.

## Consolidated decision proposal (not implemented)

No controlled SDK wire or final-text assembly defect was reproduced. Do not spend
another full batch to obtain the same coarse rejection or apply a speculative
prompt/schema/normalization fix. There is no retained raw response to replay.

If Integrator chooses one further observation, approve it as one bounded operation
with all needed content-free discriminators together:

- Final leaf text empty/whitespace-only flags and strict UTF-8 byte count (nullable
  if encoding fails), bound to the actual leaf passed to ParseProposal.
- Fixed parser outcome: syntax invalid, proven depth limit, parsed object, parsed
  array or parsed scalar. A depth label requires observing the depth boundary;
  otherwise report depth indeterminate. Do not classify from exception messages.
- Preserve the already available terminal, budget, tool and parser-stage facts;
  no response fragments, dynamic keys, paths, indices, exception text or reasoning.

A diagnostic-only bounded token walk on the rejected text could establish the
33rd nesting open without changing acceptance. This is a proposed mechanism that
needs its own reviewed controls, not existing capability or authority. If that
observation cannot be established safely, retain indeterminate depth rather than
parse with relaxed settings and accept the result. Empty output would focus the
next assessment on final-leaf/provider behavior; proven excessive depth on the
emitted structure; nonempty syntax failure would still require a separately
authorized bounded inspection to distinguish malformed shapes. Counts alone do
not establish fences or truncation.

Any privileged local observation must be explicitly scoped by Integrator to the
approved source/provider/spend and owned resource lifetime, with only the agreed
fixed summary retained. Reuse its existing qualification fixture/resources and
preserve the real execution path; do not create a second dataset, bypass acceptance
or silently replace the batch with a new scheduling mode. This assessment grants
no instrumentation, paid retry, native/host, source-root or PostgreSQL authority.
Earlier retained partial CL remains untouched. The original run/session/provider,
explicit roots, budget, trusted acceptance and reader authorization stay intact.

## Verification and review

Standard launcher, current build:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~OctoberScanFileWorkerTests|FullyQualifiedName~OctoberScanExecutionScopeTests|FullyQualifiedName~OctoberScanGenerateRunnerTests' -TestTimeoutSeconds 300 -CommandTimeoutSeconds 900
```

Isolation `713d966f101f4845b6839990ac68f82f`: **199 selected, 199 passed, zero
failed/skipped**, exit zero. Seven added characterization cases pass against
unchanged production. Test policy and launcher regressions passed. This is
controlled evidence only; PostgreSQL, native, provider and ordinary-reader
qualification are not claimed. The separate test owner accepted the boundary.
Independent review accepted the tests and assessment with no actionable findings;
it confirmed the distinction between serialized requests, provider compliance and
live-cause attribution. Build completed with zero errors; existing analyzer
warnings remain. `git diff --check` passed. No production change was made.

## Output-cap field assessment (2026-10-10)

Integrator reported exactly two newly authorized source-free native SDK probes:
streaming and non-streaming both returned the expected tiny JSON object, HTTP 200
and Stop, with equal HTTP/SDK text. Their serialized cap was
`max_completion_tokens=8192`. Tiny outputs establish neither alias support nor cap
enforcement. They do not identify the cause of the preceding real proposal rejection.

The October Worker sets `MaxOutputTokens` to the smaller of 8192 and its remaining
per-request allowance after measured input. `OctoberWorkerClientFactory` uses
`ChatClient.AsIChatClient()` and forwards requests through its lease guard without
rewriting the body. Resolved local assets contain OpenAI 2.10.0 and
Microsoft.Extensions.AI.OpenAI 10.6.0. The existing controlled wire test now checks
the cap field/value on initial and post-tool requests, alongside JSON mode, with
no new fixture or production change.

DeepSeek's official [integration guide](https://api-docs.deepseek.com/quick_start/agent_integrations/oh_my_pi/)
specifies `max_tokens` rather than `max_completion_tokens`; its
[Chat Completions reference](https://api-docs.deepseek.com/api/create-chat-completion/)
documents `max_tokens`. These statements were available in indexed official-page
results; direct page fetches timed out. No documented alias guarantee was found.
This identifies a request-contract compatibility concern, not proof of how the
deployed endpoint handles an unknown field or why real output was malformed.

The separate historical `PreparedPass1JobExecutor` explicitly rewrites that field
to `max_tokens`. October does not use its specialized handler. That handler also
changes other provider semantics and must not be copied wholesale into this path.
No cap, thinking, transport, parser or production policy change is made here.
No paid/provider/native/PostgreSQL operation or new raw capture was performed.

Controlled verification `3170cac576484c6497260b8129bc2349` selected and passed all
four existing theory cases, zero failures/skips, current build zero errors, via
the standard SelfContained launcher. Every initial and post-tool request carried
`max_completion_tokens=8192`; `max_tokens`, `thinking`, `reasoning_effort` and
`stop` were absent. This proves local factory/SDK/MAF serialization only. The first
restricted attempt `ecee15f68ac34c8ab8733c0b3963e7a6` stopped at build exit 1 with
no compiler diagnostics and no tests; the single approved escalated retry supplied
the passing evidence above. Existing assertions remain intact.

## Authorized compatibility adapter (2026-10-10)

The user subsequently requested a simple compatibility adapter and a test of
whether `max_tokens` improves the outcome. Integrator verified that direct
instruction and confirmed the selected endpoint as `https://api.deepseek.com`
(default HTTPS port, root base path), model `deepseek-flash`. Its approved main
checkpoint `3ddde7b3` matches this lane's affected factory/executor production
files. No reset, replacement implementation or unrelated synchronization is needed.

The bounded design changes only the outgoing cap property for that exact parsed
endpoint. The existing admitted numeric limit is preserved; no cap is invented or
increased. Other providers and endpoint variants retain their existing behavior.
Ambiguous duplicate/conflicting cap properties fail before dispatch. The body
transformation precedes the final lease/cancellation check, preserves other request
semantics and applies to both existing Worker streaming and shared Business
nonstreaming requests. It changes neither thinking mode nor response format,
tools, retries, output parsing, publication or source/reader authority.

Independent design review accepted this boundary, with final implementation review
and controlled RED/GREEN required. Integrator owns a separately recorded fresh
live experiment; earlier two-probe and Generate grants were exhausted. The lane
does not acquire secrets or perform provider/native/PostgreSQL operations. A
successful synthetic cap comparison would establish only its tested provider
behavior, not real-source generation, accepted Knowledge or ordinary-reader success.

Initial controlled RED `9f3a217ca81a4813ad98450fa2ff3046` selected 12 cases:
7 passed, 5 failed, zero skipped, build zero errors. All five failures were the
missing `max_tokens` assertion for the exact endpoint (Worker initial/tool
continuation, nondefault cap and Business nonstreaming); negative endpoint and
final lease-denial controls passed. Earlier `94000630f0b2461988a1bb16b012ef00`
stopped at the restricted build without tests. `5651aadcfb884dcc99714ea542307f8d`
then exposed a test-construction compile error (record syntax on an immutable
class), corrected through its existing constructor before the genuine RED run.
Neither earlier attempt is behavioral RED evidence.

Final GREEN `578da4dac41f409d817cfb0921322c05`: **342/342 passed**, zero failures
or skips, current build zero errors, through the standard SelfContained launcher
for the complete Worker, GenerateRunner, EntryPoint and ExecutionScope classes.
All 12 new cases passed. Actual SDK/MAF initial/post-tool bodies differ from the
generic endpoint controls only in the cap key; the direct and Business cases
preserve the nondefault value 137. Six negative endpoint cases retain generic
behavior, and final Business lease denial produces zero dispatch. Independent
implementation review accepted the boundary pending this now-passing GREEN.
Duplicate/conflicting cap rejection and nonmatching body nonaccess were reviewed
statically; no public-SDK malformed-key test is claimed. Existing generic wire
characterization remains valid and unchanged. `git diff --check` passed.

This checkpoint establishes the controlled compatibility behavior. Whether the
deployed provider enforces the key, or real generation improves, remains pending
Integrator's fresh runtime experiment. No live result is inferred from this GREEN.
