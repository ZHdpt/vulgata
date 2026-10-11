# Staged final-JSON correction strategy

## User authority and accepted approach

The user requested thinking off on the second regeneration, compacted context
and thinking off on the third, restoration after success, and one whole scan
after implementation. Regeneration numbers exclude the initial proposal. The
three-correction allowance and all existing budgets remain unchanged.

Independent plan review accepted a request-local typed strategy, restricted to
the already qualified DeepSeek endpoint. Initial generation and regeneration 1
use the configured reasoning behavior. Regenerations 2/3 explicitly send
`thinking: {"type":"disabled"}` and omit conflicting reasoning effort. No client
setting is mutated, so the next file/call returns to its configured behavior.
Business calls and other providers retain their existing behavior.

Regeneration 3 deterministically prunes rejected final proposals and obsolete
correction prompts from its request history. The preserved base contains the
system schema, all original source windows/advisory inputs and completed tool
call/result pairs, including protocol-required reasoning. Only the newest safe
parser feedback is appended. This is not an LLM summary or local JSON repair;
no extra provider call, evidence substitution, source omission or tool invocation
is authorized by compaction. Cumulative input accounting is not reset.

DeepSeek's [Thinking Mode documentation](https://api-docs.deepseek.com/guides/thinking_mode/)
documents the OpenAI-format thinking toggle separately from reasoning_effort.
It also requires reasoning_content replay when requests carry tools. Therefore
the existing tool exchange/reasoning replay is preserved even with tool choice
none. JSON-object output, tool-disabled enforcement, strict parsing, semantic and
source checks, authorization, acceptance and settlement guards remain intact.
The [Chat Completions reference](https://api-docs.deepseek.com/api/create-chat-completion/)
also documents the explicit enabled/disabled toggle and max_tokens up to 393216;
the existing explicit output cap is retained when thinking is disabled.

## Verification and real-run boundary

Controlled RED: `isolation-34b07957691f42d0b14541c316134ee1`, one selected,
zero passed, one failed, zero skipped. The actual SDK wire test failed because
regeneration 2 still sent reasoning_effort instead of disabling thinking. Build
and execution completed; this was a behavioral failure.

Full SelfContained `isolation-444341f5e9a24220855e59a714abcb39`: 6000 discovered,
4882 selected, 4879 passed, three failed, zero skipped. One failure is the known
unrelated `OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`.
The other two were owned runner-wire fixture rows that expected low reasoning
without configuring it. The fixture now explicitly selects low and asserts it
on the initial request. Production correctly retained the configured behavior.

Final affected GREEN `isolation-ae8c0e19bdae4dcbb7fbf57ac8b91696`: 383 selected,
383 passed, zero failures/skips. It covers Worker and GenerateRunner tests,
including actual SDK wire low/off/off, both source windows and advisory retention,
complete tool/reasoning replay, latest feedback only, same-client restoration
after success and exhaustion, non-DeepSeek unchanged behavior, log strategy/counts,
and the existing retry, budget, authority and acceptance guards. The full suite
was not rerun after the test-only fixture correction; only the known report-parser
failure remains unresolved. No full-suite GREEN claim is made.

The earlier elevated build attempt `isolation-3b616e86931941bd9f7eb73ec3bbe394`
failed before tests due to an ambiguous ChatResponseFormat reference. It was
corrected by explicitly selecting Microsoft.Extensions.AI.ChatResponseFormat;
this is not behavioral RED. Independent implementation review accepted the final
scoped change after affected GREEN. No production edits followed that review.

The newly authorized single whole-run result will be recorded below. The preceding
run remains the separately recorded failed 15/77 result; it is not resumed or
relabelled. The new run will use the same approved 77-file source, provider/model,
limits and owned resource pattern, with a fresh owned batch and no automatic
whole-scan restart. No additional run series is inferred.
