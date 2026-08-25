# DeepSeek V4 Flash terminal-history probe

**Date:** 2026-08-25
**Scope:** source-free, content-dark diagnostic of the T084 r25 terminal function mismatch. No Repository source, prepared context, identifiers, paths, database content, credentials, provider response content, reasoning, arguments, or tool results were retained.

## Result

The blocker is the historical tool-call transcript, not the current terminal declaration array.

All requests used `https://api.deepseek.com/beta/chat/completions`, `deepseek-v4-flash`, streaming, disabled thinking, one strict `submit_code_logic` declaration, and exact named `submit_code_logic` choice. The payload contained only fixed synthetic diagnostic text.

| Case | Historical assistant tool-call protocol | Result |
|---|---|---|
| Fresh terminal | None | HTTP 200, `[DONE]`, one `submit_code_logic` call |
| Historical terminal | Nine synthetic assistant `navigate_code` calls plus matching tool results | HTTP 200, `[DONE]`, one `navigate_code` call |
| Projected terminal, trial 1 | No assistant tool-call protocol; bounded prior results represented as server-owned user evidence | HTTP 200, `[DONE]`, one `submit_code_logic` call |
| Projected terminal, trial 2 | Same | HTTP 200, `[DONE]`, one `submit_code_logic` call |
| Projected terminal, trial 3 | Same | HTTP 200, `[DONE]`, one `submit_code_logic` call |

The historical case reproduces T084 r25 without source. It also reproduces the provider violation even though `navigate_code` is absent from the current `tools` array and the current named choice identifies only `submit_code_logic`. Removing the historical assistant function-call protocol while retaining a bounded server-owned prior-result projection restored the requested function in three of three trials.

## Current-provider and SDK implications

- DeepSeek documents Chat Completions as stateless: the client resends the full message history on every round. The full historical assistant/tool sequence is therefore active model input, not inert SDK bookkeeping.
- DeepSeek documents `tools` as the functions currently available and named `tool_choice` as forcing the selected function. The historical probe demonstrates that this route/model does not reliably enforce those request fields against a conflicting long tool-call history. Vulgata must continue to validate every complete response locally.
- DeepSeek's official tool example appends the prior assistant tool call and matching result before the next request. That is appropriate while the same tool remains legal; r25 proves it is not a safe finalization boundary after the legal capability set contracts.
- The pinned `FunctionInvokingChatClient` owns the iterative callback loop and message replay. Vulgata's existing delegating-client seam can project the messages supplied to the terminal inner request without adding a second agent, durable transcript, SDK retry, request eleven, or manual provider transport loop.
- Switching to DeepSeek's Responses API would replace the accepted adapter/loop and does not remove the documented requirement to resend full history. It is therefore a broader change with no evidence-backed advantage for this blocker.

## Recommended bounded correction

Introduce one deterministic, execution-local **Terminal Context Projection** inside the existing FICC composition.

1. Ordinary navigation-capable turns retain the complete assistant/tool continuity required by the current contract.
2. When only finalization is legal—provider turn ten or the turn after a first submission attempt—the provider request uses the existing exact submit-only declaration and named choice.
3. That request uses an externalized terminal-only instruction which contains no obsolete function identity, plus the original prepared job packet, server-owned ordered navigation result content, and, when present, bounded first-submission correction context.
4. Historical assistant messages, reasoning, free-form content, function-call names, call IDs, and tool-role protocol messages are excluded from the terminal provider context. No provider content is persisted.
5. The local FICC still owns exactly the same two functions and one Worker execution. The submission session, two-attempt limit, ten-turn/no-eleven ceiling, local parser/semantic validation, source authorization, byte budgets, five-/25-minute timeouts, zero retries, and atomic commit authority remain unchanged.
6. Projection must fail closed on an unrecognized message/result shape, missing original packet, duplicate or unordered prior result, oversized context, or any attempt to broaden the source/context envelope.

This is a provider-facing message-projection correction, not a host, network, database, or system change. It must be plan-reviewed, defined test-first, source-free qualified on a history-bearing terminal request, and then retried through T084 only on another fresh disposable database.

## Primary sources

- [DeepSeek Multi-round Conversation](https://api-docs.deepseek.com/guides/multi_round_chat/)
- [DeepSeek Tool Calls](https://api-docs.deepseek.com/guides/tool_calls/)
- [DeepSeek Chat Completions API](https://api-docs.deepseek.com/api/create-chat-completion/)
- [DeepSeek Context Caching](https://api-docs.deepseek.com/guides/kv_cache/)
- [Pinned Microsoft `FunctionInvokingChatClient`](https://github.com/dotnet/extensions/blob/c8437bc222c86c535ef6d763468d655737b1261d/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/FunctionInvokingChatClient.cs)
