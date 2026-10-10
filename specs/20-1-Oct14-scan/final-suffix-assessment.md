# Final JSON suffix assessment

2026-10-10. OCT-Integrator reported its single approved diagnostic after integrating
capture as `1f4f4ecf`. Its four-class controlled verification passed 321/321
(`bdf45ce41cd2482a9a668eabe69b4449`). Scan starts clean at
`9f1ee796a89e6354cee1ec169b82bcbabc173988`; affected executor/factory production
matches that integrated checkpoint. No reset or production change is required.

## Finite real observation supplied by Integrator

The 52.59-second run ended `InvalidOutput / ProposalParse / Syntax / Root`, with
`captureStatus: Captured`, three requests, 11,599 input and 3,367 output tokens,
zero accepted files and no timeout or cleanup doubt. Integrator's protected local
inspection found 5,412 strict UTF-8 bytes: an object start, no Markdown fence,
failure at EOF (line 0, byte 5412) at both depth 32 and 128, no unterminated string
or mismatched closing delimiter, and one object plus one array left open.
Appending only their closing delimiters in a diagnostic memory buffer made valid
JSON. Nothing repaired was accepted or persisted. Integrator deleted the raw file
and verified absence. This lane neither requested nor accessed it.

This establishes incomplete final JSON and rules out depth as the observed parser
failure. It does not establish why the final delimiters were absent, whether
additional semantic items were intended, or that the repaired shape would meet
the proposal schema or acceptance policy. Aggregate token usage does not identify
the final leaf's output cap. Accepted Stop is not proof of token-limit termination.

## Current application path

The Worker factory forwards each mapped SDK update without trimming text. The
executor appends complete TextContent fragments before setting the finish reason
on the same update. It continues MoveNext until the SDK iterator completes, then
validates the terminal state and saves LastText after settlement. MAF receives the
aggregate downstream; proposal parsing consumes LastText directly rather than a
selected MAF message or a reconstruction from conversation history.

Post-finish content is rejected rather than silently discarded by the Worker.
Its narrow post-ToolCalls aggregate exception applies to executable calls and
usage only, not trailing proposal text. Output-byte, update-count and token/lease
guards reject; they do not slice off a suffix. No application stop sequences are
configured. The existing actual-SDK wire controls establish JSON mode initially
and after tools, but not remote-provider compliance with that requested format.

## Smallest proposed next change, subject to a separate decision

No SDK/adapter patch is justified without a reproduced loss. Retain strict
rejection, original budgets and the current source/acceptance guards. In
particular, appending closers would manufacture a response: syntactic completion
does not establish that omitted claims, units or uncertainties were unnecessary.

If Integrator elects an application-side mitigation, the smallest candidate is
an explicit final-window instruction in the existing Worker prompt:

> When `finalWindow` is true, after any necessary allowed tool calls, return one
> complete JSON object in the exact proposal format. Include all closing array
> and object delimiters. Do not return a partial object or continuation fragment.

This addresses the prompt's currently implicit mapping from finalWindow to the
final proposal request. It is an instruction clarification and an empirical
mitigation, not a demonstrated causal repair. It must not reduce file coverage,
omit required fields/evidence, convert incomplete output to acceptance, or add an
automatic retry. No prompt edit is made here. Any later real trial needs its own
scoped decision; this assessment authorizes neither another paid run nor capture.

Risks: a model or intermediary may still return malformed/truncated output with
Stop; valid JSON still may fail the closed schema or semantic acceptance. Raising
the token/depth limits, switching transports or changing SDK versions has no
demonstrated justification in the finite observation. Hidden stop configuration,
upstream graceful truncation and misreported termination remain possibilities,
not established causes.

## Controlled SDK boundary observations

The new controls use the existing synthetic HTTP handler, actual installed SDK,
Worker factory and MAF execution. They independently concatenate mapped SDK text
before checking the Worker result. No real requests/responses are used.

| Synthetic delivery | Observed result |
| --- | --- |
| `]` and `}` in separate deltas, followed by empty Stop | SDK preserves both; Worker completes the controlled proposal. |
| Final `}` or `]}` shares the Stop delta | SDK and Worker preserve the complete text. |
| Real UTF-8 SSE bytes split into one-byte reads | Complete Unicode proposal survives. |
| Incomplete JSON followed by Stop | Exact incomplete text reaches capture callback; strict Syntax/Root rejection. |
| Closing deltas after Stop but before DONE | SDK exposes them; Worker rejects ContentAfterFinish before parsing/capture. |
| Closing deltas after DONE | SDK ignores post-DONE data; incomplete text is rejected as Syntax/Root. |
| Incomplete JSON with Length | StreamTerminal/UnsupportedFinish, not ProposalParse; no capture. |
| Complete JSON and Stop followed by clean EOF without DONE | SDK completes and Worker accepts the controlled proposal. |

Therefore no suffix loss is reproduced for validly ordered delivery, including
network-byte fragmentation. The SDK end boundary matters: data after DONE is not
part of its response, and clean EOF need not establish receipt of DONE. This is
not evidence that either condition occurred in the real run. Its retained
observation is final text, not raw SSE framing; app-level Stop cannot prove which
wire chunks or end marker existed upstream. Do not "fix" this by accepting
post-terminal text or appending missing delimiters.

The first run, `5554090dbe09480c8d59c65a96d6530d`, passed 329/330 with zero skips.
The sole failure was a new test's usage expectation: Length is rejected after the
stream is drained, so already observed usage remains known. ContentAfterFinish
rejects earlier, before the fixture's usage event. The test correction follows
that observed boundary; no production behavior or assertion on strict rejection
is relaxed. The final tests also pin the observed post-Stop SDK behavior rather
than accepting either mapping outcome.

Final GREEN `d28736c5a26947b7a0d7024dd373f2fc`: **330/330 passed**, zero failures
or skips, build zero errors. The complete Worker, GenerateRunner, EntryPoint and
ExecutionScope classes ran through `eng/test.ps1 -Suite SelfContained` with their
standard fully qualified class filter. Existing analyzer warnings remain.

The final synthetic incomplete fixture is exactly 5,412 UTF-8 bytes, built solely
from the existing committed semantic fixture plus JSON whitespace inside its last
array; the complete control is 5,414 bytes. No real text was reconstructed. All
complete controls preserve 5,414 bytes; incomplete Stop and post-DONE controls
preserve 5,412 bytes. The real-UTF-8 network-fragment control recorded 12,090
fragmented read calls and preserved the complete proposal. No configured stop
parameter appears in the actual controlled Worker HTTP requests.

The test owner and independent reviewer accepted this controlled boundary and
the assessment's evidence limits. Only the existing test file and this assessment
change; production, prompt, schema, budgets and retry policy are unchanged.
`git diff --check` passed. Controlled evidence does not constitute another real
generation, provider or reader qualification.

No production edit, paid/native/PostgreSQL operation, new capture or real source
inspection is authorized in this assessment.
