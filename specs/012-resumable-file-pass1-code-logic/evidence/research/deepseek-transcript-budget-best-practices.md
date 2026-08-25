# DeepSeek transcript-budget best practices for Story 12.3

**Research date:** 2026-08-25 (Asia/Shanghai)
**Source policy:** official DeepSeek documentation/policy and first-party Microsoft/OpenAI .NET documentation or source only. No provider, source-repository, database, or host qualification activity was run.

## Decision

Keep raw, assembled, replay, and request-body caps, but name them explicitly as **Vulgata safety policy**, not DeepSeek or SDK limits. DeepSeek specifies token/context limits and protocol shapes; it does not specify independent UTF-8 byte caps or a 512-wire-bytes-per-token guarantee. The current correction should therefore:

1. preserve complete `reasoning_content`, assistant tool calls, and matching tool-result turns;
2. assert the final serialized DeepSeek request shape at the injected HTTP transport, including `stream`, `strict`, the token field, and thinking mode;
3. enforce inclusive local bounds with checked arithmetic and cancellation/no-send behavior; and
4. split **logical inclusive-bound tests** from **reachable public-transport tests**. Do not require the public executor to synthesize byte totals that its own earlier caps or fixed reserve make unreachable.

This distinction is necessary before T091/T092: the intended safety model is sound, but several exact public-seam vectors are not independently observable as currently specified.

## Provider requirements versus Vulgata policy

| Area | Provider/SDK requirement or fact | Vulgata safety policy |
| --- | --- | --- |
| Streaming | `stream: true` returns data-only SSE events and terminates with `data: [DONE]`. [`Create Chat Completion`](https://api-docs.deepseek.com/api/create-chat-completion/) | Count transport bytes incrementally, discard consumed raw bytes, cap one response, apply inactivity and absolute deadlines, and cancel on overflow. |
| Thinking + tools | For requests carrying tools, current DeepSeek thinking-mode guidance requires complete prior assistant `reasoning_content` to be passed back in later requests; omission causes HTTP 400. [`Thinking Mode`](https://api-docs.deepseek.com/guides/thinking_mode/) | Preserve reasoning, content, tool calls, order, and tool results unchanged; never truncate or summarize to meet a cap. |
| Tool protocol | Append the assistant tool-call message, execute locally, then append a `tool` message whose `tool_call_id` matches the call. Model arguments still require application validation. [`Tool Calls`](https://api-docs.deepseek.com/guides/tool_calls/), [`Chat Completions schema`](https://api-docs.deepseek.com/api/create-chat-completion/) | Permit only `navigate_code` and `submit_code_logic`; run the full local `code-logic/file-v2` validator as the acceptance authority. |
| Strict functions | Strict mode is a beta feature requiring the beta base URL, `strict: true` on **all** functions, every object property in `required`, and `additionalProperties: false`; unsupported schema shapes are rejected by the server. [`Tool Calls - strict mode`](https://api-docs.deepseek.com/guides/tool_calls/#strict-mode-beta) | Deterministically project the provider subset, cap schema/declaration bytes, and fail closed on drift. Provider strict acceptance is only an early shape check. |
| Output control | DeepSeek Chat Completions defines `max_tokens`; input plus generated tokens must fit the context. Current V4 model documentation advertises a maximum output of 384K tokens. [`Chat Completions`](https://api-docs.deepseek.com/api/create-chat-completion/), [`Models & Pricing`](https://api-docs.deepseek.com/quick_start/pricing/) | Retain 32,768 generated tokens as a smaller cost/time/resource bound. It is not the provider maximum and does not imply a byte cap. |
| Retry | DeepSeek recommends retrying transient server/overload errors after a wait. [`Error Codes`](https://api-docs.deepseek.com/quick_start/error_codes/) The OpenAI .NET SDK retries 408, 429, and selected 5xx responses up to three additional times by default. [`openai-dotnet README`](https://github.com/openai/openai-dotnet#automatically-retrying-errors) | Configure zero SDK retries so one durable Worker retry remains the only retry authority. |
| Privacy | DeepSeek's current policy says Services may collect prompts/inputs and generate outputs, and describes retention/processing; it is not a no-retention/no-training guarantee for submitted source. [`DeepSeek Privacy Policy`](https://cdn.deepseek.com/policies/en-US/deepseek-privacy-policy.html) | Never log, persist, or emit source, prompts, reasoning, tool arguments/results, provider bodies, credentials, or validator details. Source transmission still requires the repository's provider/privacy authorization. |

## Complete affected flow

### 1. Stream consumption and semantic assembly

The official OpenAI .NET client creates a non-buffered streaming request, serializes options with `ToBinaryContent()`, and exposes an async SSE enumerable with cancellation. [`ChatClient.CompleteChatStreamingAsync`](https://github.com/openai/openai-dotnet/blob/main/OpenAI/src/Custom/Chat/ChatClient.cs#L1410-L1444) `Microsoft.Extensions.AI.OpenAI` converts content and reasoning deltas to `ChatResponseUpdate` values, accumulates fragmented tool-call ID/name/arguments, and emits the complete `FunctionCallContent` after the response stream. [`OpenAIChatClient.FromOpenAIStreamingChatCompletionAsync`](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L2520-L2753)

Consequences:

- A raw counter belongs below the SDK SSE parser, on successful transport reads. It limits wire/network work and must not retain a whole raw response.
- An assembled counter belongs above parsing and must measure the final replayable assistant representation: `reasoning_content`, ordinary content, call IDs/names/arguments, and structural JSON after the same serializer/escaping rules used for replay.
- Raw and assembled caps address different resources, so separate concepts are useful. Their exact public-executor failure categories are not automatically independently reachable when both thresholds are identical; wire framing normally makes a large valid SSE response hit `RawResponse` first. A special valid fixture whose decoded value expands during final JSON serialization may exercise `AssembledResponse`, but ordinary ASCII padding cannot be assumed to do so.
- Inactivity must reflect actual inbound progress. A public fake-SSE test should prove reasoning, content, empty/metadata, and fragmented tool-call updates keep the request alive through the pinned adapters. If any adapter buffers an update kind, reset activity at the raw-read/SSE layer instead of assuming outer `AgentResponseUpdate` visibility.

### 2. Reasoning replay, functions, and tool results

DeepSeek's requirement is protocol continuity, not merely retaining reasoning in memory: every later tool-bearing request must carry the full assistant message, followed by corresponding tool results. [`Thinking Mode`](https://api-docs.deepseek.com/guides/thinking_mode/#tool-calls) `FunctionInvokingChatClient` is designed to append assistant function calls and `FunctionResultContent` and repeat the inner request until a stop condition/iteration limit. [`FunctionInvokingChatClient`](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/FunctionInvokingChatClient.cs#L2235-L2280)

The current Microsoft OpenAI adapter reads non-standard `reasoning_content`, but its ordinary assistant-message reconstruction handles text and tool calls, not `TextReasoningContent`; using an explicitly patched raw OpenAI assistant message is therefore justified and must remain wire-tested. [`OpenAIChatClient` assistant conversion](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L2194-L2383) Tool results are serialized as strings or JSON and emitted as `ToolChatMessage` with the matching call ID. [same first-party source](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L2248-L2287)

### 3. Final request serialization and output-token field

The effective authority is the bytes produced by the pinned adapter/SDK immediately before transport, not an estimated object graph. `Microsoft.Extensions.AI.OpenAI` maps `ChatOptions.MaxOutputTokens` to `ChatCompletionOptions.MaxOutputTokenCount`; its tool converter also maps the `strict` additional property to the OpenAI tool declaration. [`OpenAIChatClient.ToOpenAIOptions/ToOpenAIChatTool`](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L2171-L2191), [options mapping](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L2944-L3043)

There is a compatibility risk requiring a repository fix or explicit wire proof: DeepSeek documents `max_tokens`, whereas the official OpenAI .NET SDK changed `MaxOutputTokenCount` to the OpenAI `max_completion_tokens` field. [`openai-dotnet changelog`](https://github.com/openai/openai-dotnet/blob/main/CHANGELOG.md#200-beta12-2024-09-20) The source-free handshake manually writes `max_tokens`, but production uses `ChatOptions.MaxOutputTokens`. Capture the production body and require exactly the DeepSeek-supported field; patch the request through a pinned, tested serializer hook if it is `max_completion_tokens`. Do not accept a live success as proof that the 32,768 limit was enforced.

DeepSeek V4 currently defaults thinking mode to enabled and supports explicit `thinking`/`reasoning_effort`. [`Thinking Mode`](https://api-docs.deepseek.com/guides/thinking_mode/#thinking-mode-toggle-and-effort-control) Relying on a mutable provider default is weaker than the plan's reasoning-continuity invariant. The outgoing-wire contract should explicitly set or assert the intended thinking mode and should fail the source-free capability gate if the configured model/endpoint cannot honor it.

### 4. Cancellation, timeouts, and retries

Pass the caller token through the SDK enumeration and transport, use `HttpClient.Timeout = InfiniteTimeSpan`, and enforce Vulgata's five-minute inactivity plus non-resetting 25-minute attempt deadline with linked cancellation. `System.ClientModel` documents `NetworkTimeout` as a configurable network-operation timeout and defaults to three exponential retries unless replaced. [`System.ClientModel configuration`](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/System.ClientModel/samples/Configuration.md)

The five/25-minute values and zero retries are Vulgata policy, not DeepSeek requirements. Preserve first-failure classification while cancellation unwinds; caller cancellation should remain cancellation, while inactivity/absolute expiry should map to the existing closed provider-unavailable path. Overflow must cancel the current transport and prohibit a later tool-loop request.

### 5. Privacy and logging

The SDK boundary must stay content-dark. Microsoft telemetry excludes raw messages, function arguments, and results by default unless sensitive-data capture is enabled, but an environment variable can change that default. [`OpenTelemetryChatClient.EnableSensitiveData`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.opentelemetrychatclient.enablesensitivedata?view=net-10.0-pp) Explicitly disable SDK message/content logging and distributed tracing for this executor, do not attach a sensitive-data-enabled client, and test only closed categories/counters. The OpenAI SDK also sends generic runtime telemetry headers by default; these contain platform/runtime metadata rather than prompt content. [`openai-dotnet telemetry and privacy`](https://github.com/openai/openai-dotnet#telemetry-and-privacy)

This protects Vulgata's logs and durable state; it does not change DeepSeek's provider-side processing. The report/test evidence must say “not retained or logged by Vulgata,” never “not retained by DeepSeek.”

## Are all five exact caps meaningful and public-seam testable?

| Cap | Meaningful? | Honest proof boundary |
| --- | --- | --- |
| Raw response | Yes: bounds network/parser work independently of decoded content. | Public executor + injected HTTP handler can stream a valid exact-size SSE body and plus one; assert incremental reads, cancellation, `RawResponse`, and no next request. |
| Assembled response | Yes: bounds execution-local decoded/replay memory. | Test the inclusive serialized-message counter directly, then add a **reachable** public-SSE precedence case. Do not require a 16 MiB ASCII public fixture to report `AssembledResponse` when the equal raw cap necessarily fires first. |
| Per-assistant replay | Limited independent value. With the same 16 MiB aggregate and non-negative contributions, aggregate validation mathematically subsumes the per-message value. | Retain as a defensive invariant/unit assertion, but a single-message public test proves both simultaneously and one `OutgoingRequest` diagnostic cannot distinguish them. |
| Aggregate assistant replay | Yes: prevents up to nine individually legal messages multiplying request/memory authority. | Public multi-turn fake transport should capture the actual later request and prove the sum of final serialized assistant message elements is inclusive at the largest **reachable** value and blocks plus one before send. |
| Complete body | Yes as an absolute final-wire guard. | Capture every real body after SDK serialization and assert `body.Length <= 17,416,192`; unit-test the cap's inclusive comparison separately. The exact 17,416,192/pass and 17,416,193/fail public vectors are generally unreachable because 32,768 bytes are a reserve for envelope overhead, not a controllable contribution, while every variable component is already capped. |

Therefore the public seam should prove observable end-to-end behavior and category precedence; a deterministic serializer/accounting seam should prove exact arithmetic. Calling every cap “independent” overstates what the single executor outcome can distinguish.

## Current plan/implementation mismatches and unsupported assumptions

1. [`PreparedPass1JobExecutor.cs`](../../../../src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1JobExecutor.cs) still sets assembled/per-assistant to 131,072 bytes and computes the old 1,818,624-byte body cap (`9 * 131,072`); [`plan.md` AD-17](../../plan.md) and T091/T092 require 16,777,216 assembled/per-message/aggregate and 17,416,192 body bytes.
2. `TryAddAssembled` currently sums streamed update payloads plus ad hoc constants (`+24`, `+16`, `+64`), not the final replay serializer bytes required by the plan. Outgoing validation measures individual serialized assistant message elements but has no aggregate assistant sum.
3. The 16,777,216 raw cap is justified as `32,768 * 512`; DeepSeek documents neither a 512-byte token expansion nor a raw-byte maximum. This is a conservative Vulgata policy assumption and must not be called provider-derived.
4. The equal raw/assembled threshold makes the planned exact public `AssembledResponse` vectors fixture-dependent and commonly shadowed by `RawResponse`. Test category precedence instead of forcing an impossible ordinary fixture.
5. The body's 32,768 fixed margin makes the formula a safe ceiling, not an attainable “largest legal transcript.” Exact cap/+1 assertions belong at the accounting seam unless the public request shape gains an authorized, production-realistic way to consume that reserve.
6. Production does not yet prove that 32,768 is serialized as DeepSeek's `max_tokens`, nor does it explicitly bind thinking mode. Both are wire-contract gaps; fix/assert them before another source-bearing qualification.
7. The plan's non-buffering statement is only partly testable from the public result. An adversarial stream can prove incremental reads and early cancellation, but proving “no complete raw buffer anywhere” also requires source inspection/allocation instrumentation of the pinned SDK path.

## Recommended correction

- Keep the approved numeric caps as local policy unless governance chooses new values; do not weaken reasoning replay or the local schema validator.
- Implement one checked, deterministic accounting component over final UTF-8 JSON elements and the complete captured SDK body. Preserve a transport-level raw counter separately.
- Rewrite T091 proof language into two layers: exact inclusive comparisons at the accounting seam, plus reachable public-executor fake-SSE/fake-wire tests for raw overflow, assembled-vs-raw precedence, aggregate no-send, final-body no-send, cancellation, and privacy.
- Add exact outgoing assertions for `max_tokens: 32768`, explicit thinking intent, `stream: true`, both and only strict functions, assistant `reasoning_content`/tool calls, and matching tool-result turns. If the pinned SDK emits another token field, patch or replace that serialization path before provider traffic.
- Retain zero SDK retries and the five/25-minute local deadlines; verify activity at the lowest layer that actually observes all SSE progress.

This correction preserves the safety goal while making the tests honest, provider-compatible, and stable against SDK serialization details.
