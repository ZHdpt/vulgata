# Story 12.3 DeepSeek Multi-Call Recovery Planned-Change Authorization 05

- Decision ID: `story-12-3-planned-change-authorization-05`
- Status: Accepted
- Decided at: `2026-08-24T20:39:28.1409486Z`
- Scope SHA-256: `82d8d005d857aa15d14eef53f123ee177e0a2591fc07367efe69a61f80ee1567`
- Plan SHA-256: `adc2414e283f39c789078f61bab8b343c85eeb78fe35bc43ac0264ec11cc2b1f`
- Accepted plan-review SHA-256: `57866f2590319569938287720b040221ea2c251cf45cb05f130e5f07ddad2f89`

Scope construction: SHA-256 of this exact UTF-8 JSON object with no trailing
newline:

```json
{"complete_turn_preflight":true,"dispatcher_executor_invocations_per_claim":1,"durable_per_callback_reservation":true,"inactivity_activity_source":"raw_successful_sse_read","provider_status_classification":"deepseek-current","reject_ignored_sampling_fields":true}
```

## Authority

The user explicitly authorized diagnosis of further DeepSeek/LLM failures,
comparison of the complete current plan and implementation against current
best practices, and the resulting bounded plan, test, implementation, and
qualification corrections without another user approval gate. System changes
remain prohibited. Independent plan review 15 accepted the exact corrected
plan with zero findings.

## Approved planned change

- Insert one complete-provider-turn, zero-effect preflight immediately below
  the pinned `FunctionInvokingChatClient`; accept only an all-`navigate_code`
  batch or a sole legal `submit_code_logic` call before any callback can run.
- Invoke accepted navigation callbacks serially in provider order. Durably
  reserve exactly one ordinal immediately before each callback, treating
  `ToolAttemptCount` as the job-wide reservation high-water across every
  exception, cancellation, crash, retry, and restart. Bind receipts without a
  second increment and repair the ordinal-nine Calls marker idempotently.
- Hydrate the exact durable remaining budget and invoke the executor once per
  durable claim, removing dispatcher-owned conversation/retry/budget state.
- Reset streaming inactivity only on successful nonzero raw SSE reads, classify
  current DeepSeek HTTP statuses precisely, and reject thinking-mode sampling
  fields that DeepSeek documents as ignored.
- Preserve the exact two raw public function declarations and projected hashes,
  complete reasoning replay, zero SDK retries, absent `tool_choice`,
  `parallel_tool_calls`, and `response_format`, local final validation, bounded
  source authorization, and the existing 25-minute Worker absolute cap.

## Preserved boundary

No table, column, migration, new durable row kind, manual provider loop,
provider retry, endpoint/model change, source-scope expansion, Story 12.4/12.5
work, CodeGraphContext use, credential exposure, host configuration, network,
firewall, service, permission, tool installation, or other system change is
authorized.

## Remaining gates

Regenerated dependency-ordered tasks; fresh combined definition review;
implementation-approval evidence binding the accepted definition; behavioral
RED/GREEN; deterministic and fake-wire regression; source-free T096/T083;
fresh disposable-database T084 3/3; T085/full T062; convergence; quality
review; conditional E2E; final verification; and root-owned workflow/checkpoint
evidence remain mandatory.

## Decision

The root may proceed directly to task generation under the user's standing
authorization. This omits another approval pause only; it does not waive any
independent review, TDD, privacy, provider, database-isolation, or final
verification gate.
