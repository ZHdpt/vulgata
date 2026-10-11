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

## Real whole-run result (2026-10-11)

The single authorized run `staged-whole-01` used implementation
`c2272e2090c310271898275ac4515e6c13de6179`, the same approved 77-file source,
DeepSeek flash, configured low reasoning, max_tokens 393216, existing budgets
and fresh owned resources. Hard retry remained off. The grant is consumed;
no automatic whole-scan restart was performed.

- Result: Failed / Partial; 49/77 accepted, one failed and 27 unstarted.
  Publication NotRequested, zero Documents. Ordinary reader qualification was
  therefore not reached. Partial accepted data and diagnostic evidence are retained.
- Process: exit 1 after 1773.18 seconds; TimedOut=false and
  NeedsSettlementCheck=false. Generator elapsed time was 29:31.0614321.
- Terminal file: account
  `src/main/java/cn/com/yitong/actions/register/selfregist/RegistUserAuth.java`.
  Worker reports Cancelled at Transport, with no parse or budget failure detail.
  This is not a JSON retry exhaustion or demonstrated launcher timeout. The
  cancellation origin is not identified by the retained terminal report; proximity
  to the 1800-second run limit alone does not prove that limit fired.
- Usage: 80 requests, unknown aggregate input/output usage. No precise total
  tokens or cost is claimed. All 77 original source hashes matched afterward.
- Matching and Business Logic both report not-requested; no correction-log gap
  was reported.

Nine files needed JSON regeneration and all nine recovered: eight on regeneration
1, one on regeneration 3. Eleven correction requests were logged: nine Configured,
one ThinkingDisabled and one CompactedThinkingDisabled. The latter file was
account `src/main/java/cn/com/yitong/actions/domain/login/SaveVersionCacheOp.java`,
admission `68783462-98cd-4497-92b6-e5f4a8610012`, and its final coverage is Accepted.

| Candidate | Strategy | Bytes | Worker elapsed | Result |
| --- | --- | ---: | ---: | --- |
| Initial | Configured | 5100 | 11.652s | Syntax, EOF position 5100 |
| Regeneration 1 | Configured | 5000 | 18.737s | Syntax, EOF position 5000 |
| Regeneration 2 | ThinkingDisabled | 4888 | 24.165s | Syntax, EOF position 4888 |
| Regeneration 3 | CompactedThinkingDisabled | 5037 | 30.653s | Valid; Worker Completed at 30.897s |

All four responses reported Stop. Compaction removed five rejected/obsolete
messages while preserving the original evidence base. This is real evidence that
the third stage can recover a file; one observation does not establish its general
success rate or isolate compaction as the cause. Request-local restoration is
proven by the controlled actual-SDK wire tests above; subsequent real files
continued, but raw provider requests were not retained as proof of their settings.

### Retained evidence

- Owned root: `C:\Users\13728\AppData\Local\Vulgata\OctoberRuns\phone-ca0a24b79da8`
- Owned database: `vulgata_oct_pilot_37bba955b9614185a96312a72fd29c02`
- Batch: `6bc93d43-a833-440d-a710-6d5febc75d46`
- Ignored ledger: `TestResults/replay-paid/staged-whole-01.json`
- Root-relative report: `logs/generate-3c9e321e88ff4bd38a2c892045e932ff.stdout`
  (SHA256 `E6AEDC20AFF664AD5877D6EAC302E9A540EB59C152703269016F526BAA31C03E`)
- Root-relative corrections: `logs/generator/worker-json-corrections.jsonl`
  (SHA256 `36B4F785C32FDE3EE1BC9A1243799243403C7AC446B8905B5F77D553B88DE523`)
- Executed Agents DLL SHA256:
  `F16DF58FFC181E32FB1E8687A83470CC2A54CE440630DB7581D0C126C10F9C6B`
- Executed Generator DLL SHA256:
  `A2D7432263E85CBDCD2F92BDD997EB7EA46B214B9011C66C8447FEA441C2C2FB`
