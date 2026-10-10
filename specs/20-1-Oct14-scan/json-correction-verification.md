# Bounded JSON correction after normal provider Stop

The user requested inspection of DeepSeek's Chat Completions documentation,
correction of the observed failure, then a whole-slice retry. Sources inspected:

- https://api-docs.deepseek.com/zh-cn/api/create-chat-completion/
- https://api-docs.deepseek.com/zh-cn/guides/json_mode/
- https://api-docs.deepseek.com/zh-cn/guides/thinking_mode/

The documented JSON-mode requirements are already present: json_object on the
wire, explicit JSON instructions/example and sufficient output allowance. The
last real response had Stop and identical HTTP/SDK/parser text with missing
closing delimiters. This does not establish the provider's internal cause; it
does establish that blindly increasing max_tokens or appending local brackets
is not a supported correction. Stop alone is not acceptance evidence.

The scoped change permits one corrective conversation turn for invalid JSON
syntax (including empty output) after Stop on the final source window. Generate
opts in; direct Worker callers default to no correction. The same session,
adapter, tools, source ledger, provider and budgets remain in use. Full assistant
messages including reasoning are replayed, followed by a trusted fixed prompt
requesting a complete replacement JSON object. Source/provider output remains
untrusted. No fragment concatenation, local JSON repair or relaxed parser.

The allowance is bounded to 0 or 1. Length, interrupted/filtered/unknown terminal
states, tools pending, schema/semantic failures and authorization loss do not
trigger syntax correction. Each corrective request passes normal currentness,
byte, request count, token, time and provider admission checks and settles usage.
Repeated malformed output stops. Original acceptance/publication boundaries apply.

This is authorized bounded output correction, not automatic whole-run retry or
recovery scheduling. Real success remains unproven until separately recorded.

## Controlled evidence and runtime preparation

RED `isolation-0862e65bfa2d40edbdbd90c2d764fad7`: 11 selected, 5 passed,
6 failed, zero skips. Failures covered the missing corrective turn and missing
0..1 validation; default refusal and non-eligible outcomes stayed bounded.
Independent GPT-6 Luna review accepted the scoped change without findings;
review did not run tests or claim runtime success.

Fresh target `phone-5920946edfbd`, batch
`e57b5fdb-5df9-4105-874c-4373b5daacd5`, database
`vulgata_oct_pilot_088ec4df5d2449fba4152367d7ed6636`: all 77 approved files
selected, CL-only, DeepSeek low reasoning, output cap393216. Bootstrap, prepare
and select passed with no paid call. Previous retained data is preserved; the
shared PostgreSQL container/volume is unchanged and the bootstrap host stopped.

Full SelfContained `isolation-7a74011042ff43b1b8b56e81b7eeccaf` passed
4783/4788, zero skips. All 11 new correction cases passed. Four existing runner
tests assumed two requests/only the original HTTP response sequence for malformed
JSON and required adaptation to the newly authorized third request. Their live
authority/replay/capture assertions remain required. The fifth failure is the
known exact report-shape mismatch in OctoberGeneratedClReportTests. Full-suite
GREEN is not claimed. Generator executable/test copy hash:
`239D5298D476A4F0D97DEBCEE5361B3F5EE9F1D4B4C49D95CF14524CC5AF11F8`;
Agents build/Generator/test copies agree at
`DD71F80E2D61A670724136A029E18B5D52BF2DE4AE88907170309C629D8686D2`.

Final affected SelfContained `isolation-2d6de8bc3e664c5d92e1965250d71b80`
passed **447/447**, zero skips. The actual SDK controlled HTTP test verifies
reasoning and malformed assistant-text replay, fixed correction prompt, JSON
mode and max_tokens on the corrective request. Capture and revoked-result replay
still assert no redispatch. Independent final-shape review accepted these test
updates. Focused GREEN does not replace the non-green full-suite result above.

## Real retry result

Reviewed commit `0b16d4c87008304df78ab5939ab187b396430e88` ran the whole selected
slice once. Report `generate-e44d4c376ba8417e87f7825240dcf3ed.stdout` records exit
1 after **76.84 seconds**, no timeout, `NeedsSettlementCheck=false`, empty stderr.
Inventory/selection 77/77; three provider requests, 21012 input and 16675 output
tokens, all usage known. Channel `pom.xml` was accepted; next
`mbs_channel/src/main/java/cn/com/yitong/actions/domain/common/SdkAntiFraudHandleOp.java`
failed InvalidOutput/ProposalParse/Syntax/Root after the single correction
allowance. No token/byte budget, tool or terminal-finish failure was reported.

The last response used MaxTokens=393216; HTTP and SDK both reported Stop and
DONE was seen. HTTP, SDK and parser text were identical at 6254 UTF-8 bytes;
all three JSON summaries report MissingClosingDelimiters, one open object and
one open array at EOF. HTTP and SDK reasoning usage match at 130 for that leaf.
The correction is controlled-verified but did not make this live response valid.
The exact provider-internal cause remains unknown. No local JSON repair,
additional correction, provider switch or identical whole-run retry was applied.

Execution Failed, coverage Partial, publication NotRequested; one accepted file,
zero returned Documents. All 77 source hashes remain unchanged. Owned database
and diagnostics, previous accepted data and shared container/volume are retained.
This is not complete generation, publication or ordinary-reader qualification.
