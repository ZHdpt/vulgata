# Chat Provider Streaming Contract v1

## Scope and sole production authority

This is the server-only transport boundary for every Story 12.5 primary synthesis request and the single permitted syntax-repair request. The production `IChatProviderStreamingTransport` registration MUST resolve directly to `Vulgata.Agents.ChatExecution.OpenAiCompatibleChatProviderStreamingTransport`. That response-header raw adapter, not an SDK aggregate response, SDK fake-chunk wrapper, dispatcher, test double, or release harness, is the sole authority allowed to originate a Tier 3 `StreamAttemptReceipt`.

```csharp
internal interface IChatProviderStreamingTransport
{
    IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
        ChatProviderStreamRequest request,
        CancellationToken cancellationToken);
}
```

`ChatProviderStreamRequest` carries the selected provider identity, bounded prompt, candidate/case/request/attempt identities, packet fingerprint, canonical streaming-request fingerprint, and one absolute deadline. Signals are `AnswerDelta`, `Terminal`, or `ProviderError`; none is client-renderable.

## Ordered byte-to-answer pipeline

The following stages are ordered and have separate state, counters, and failure ownership. No stage may collapse two domains into one counter.

| Stage | Input and output domain | Bounds and counting | Owner and closed failures |
| --- | --- | --- | --- |
| 1. Raw response-header HTTP adapter | Arbitrarily segmented response-body octets from `HttpContent.ReadAsStreamAsync`; headers are recorded separately | Each `ReadAsync` requests at most 16,384 bytes. This is an implementation read-buffer safety cap, not FR-054's Provider Chunk acceptance cap. Raw response-body total <=1,048,576 bytes is an implementation anti-overhead cap. | adapter owns status/media/protocol validation, cancellation/disconnect, raw total overflow, first physical read, and physical EOF disposition |
| 2. Strict wire UTF-8 decoder | raw response-body octets -> Unicode SSE characters across arbitrary physical reads | Incremental strict decoder; incomplete scalars carry across reads. Malformed, overlong, surrogate, or truncated-at-EOF wire UTF-8 fails. No replacement. | wire decoder owns `invalid-wire-utf8` |
| 3. SSE field/event parser | decoded characters -> complete SSE fields/events across arbitrary reads, CR, LF, or CRLF boundaries | A decoded SSE line and one assembled SSE event are each <=131,072 wire UTF-8 bytes; raw event accounting uses the exact pre-decoding body slice. These are implementation grammar/overhead caps, not FR-054 acceptance caps. Comments/unknown safe fields are ignored; invalid field grammar or cap overflow fails. | SSE parser owns `invalid-sse`, `sse-line-overflow`, and `sse-event-overflow` |
| 4. JSON event decoder | one complete `data:` event -> typed provider delta, finish metadata, error, or `[DONE]` | JSON is decoded only after event completion. Invalid JSON, duplicate/invalid required fields, invalid JSON escapes, or unpaired escaped UTF-16 surrogates fail. The raw JSON event remains within the SSE event cap. | JSON decoder owns `invalid-json-event`, `invalid-json-escape`, and provider error classification |
| 5. Answer-delta encoder | decoded provider content string after JSON unescape -> strict UTF-8 answer-delta bytes | **FR-054 Provider Chunk** is exactly one non-empty decoded content delta from one JSON event, re-encoded as strict UTF-8. It MUST be <=16,384 bytes. JSON escape expansion is counted **after** JSON decoding/unescaping: for example raw `\u4e2d` is six wire bytes and three answer-delta bytes. | delta encoder owns `chunk-overflow` and invalid decoded scalar state |
| 6. Grounded-answer assembler | ordered answer-delta bytes -> one complete provider response byte sequence/string | **FR-054 complete assembled response** is the concatenation of decoded answer-delta UTF-8 bytes and MUST be <=65,536 bytes. The exact limit passes; +1 fails. Strict final decode/flush occurs only after valid terminal plus physical EOF. | assembler owns `total-overflow`, `missing-terminal`, `duplicate-terminal`, `contradictory-terminal`, `post-terminal-data`, and final invalid/truncated answer UTF-8 |
| 7. Grounded-result validation | complete assembled string -> parsed and accepted `GroundedAnswer` | no new byte budget; syntax, packet fingerprint, handles, claims, citations, semantic evidence, currentness, and authorization all must pass | dispatcher/coordinator own syntax-only repair decision and all validation/publication failures |

The 131,072-byte line/event and 1,048,576-byte raw-body limits are explicit implementation safety caps that contain JSON/SSE escape and metadata overhead. They do not weaken or substitute for the 16,384-byte decoded Provider Chunk or 65,536-byte assembled-response acceptance caps.

## Liveness, terminal, error, and EOF

- Ten-second inactivity resets only when stage 5 emits a valid non-empty `AnswerDelta`, or stage 4 derives the single valid `[DONE]` terminal. Physical reads, SSE comments, empty `data:`, metadata-only JSON, and finish metadata do not reset it.
- `[DONE]` is the one logical successful terminal. A non-null finish reason is pending metadata: `stop` is consistent; truncation/length, content-filter, error, two different reasons, or success/error coexistence is contradictory. Provider error JSON is `ProviderError`, never a successful terminal.
- The adapter continues physical reads after `[DONE]` until EOF. A second `[DONE]` is duplicate. Any later non-empty decoded content delta or provider error is post-terminal data. Comments, empty events, and transport whitespace may follow but cannot reset liveness.
- EOF before `[DONE]` is missing terminal. EOF after exactly one consistent `[DONE]`, with no post-terminal data, permits final assembly. Disconnect, cancellation, inactivity, or deadline is not EOF success.
- Cancellation wins until the complete result has passed every acceptance check. Every failure cancels/disposes the read, discards and best-effort zeroes raw/event/delta/assembly buffers, and exposes only a closed content-dark category.

## Shared deadline and repair

`PreparedChatCoordinator` creates one monotonic deadline capped at 29,000 ms. Retrieval, primary streaming, optional repair streaming, all decoding/validation, currentness/authorization, and complete publication consume it. Repair starts only after a closed syntax failure and with at least 3,000 ms remaining. It uses the same packet/fingerprint/deadline and the same raw adapter pipeline. Semantic/citation/currentness/authorization failures never repair; there is no third call.

## Whole-answer browser barrier

No raw octet, SSE field, JSON event, answer delta, partial answer, claim, citation, handle, relationship, or Source Anchor detail crosses the server-to-client boundary before complete acceptance. `ChatPage` exposes only generic pending/cancel state. After acceptance, existing Unicode text-element reveal may present the immutable answer; reduced motion or insufficient remaining deadline renders it immediately. Operational deterministic rescue is not a streamed provider answer and cannot satisfy Tier 3 streaming evidence.

## Authenticated raw-adapter provenance

At process start, `ChatStreamObservationAuthority` creates an ephemeral ECDSA P-256 key pair and an opaque process-observation ID. The release preflight binds its public-key fingerprint, process ID, candidate commit, resolved adapter fully qualified type, adapter assembly informational version, and adapter assembly SHA-256. The private key never leaves process memory.

For each attempt, the raw adapter canonicalizes and signs a content-dark observation containing:

- candidate commit, case ID, request ID, attempt kind, observation sequence, and process-observation ID;
- exact adapter type, version, assembly SHA-256, and process public-key fingerprint;
- provider API identity and non-secret configuration fingerprint;
- canonical request fingerprint that includes `stream=true`, with `requestStreamMode: true` recorded separately;
- response-header disposition; status code, normalized media type, negotiated HTTP protocol, and headers timestamp when received;
- first physical-read timestamp/disposition and physical-EOF timestamp/disposition;
- all stage counters, terminal/error outcome, rejection category, repair/validation disposition, and total duration.

`ChatStreamObservationAuthority` signs the SHA-256 of that canonical receipt. The same-process semantic verifier validates the signature before exporting the content-dark artifact. Tier 3 then binds the artifact/signature/public-key preflight to the same candidate, case, request, provider configuration, adapter assembly, and app process. Test adapters may emit test observations, but their type/provenance is release-ineligible.

A fixed production-composition test and Tier 3 preflight must prove DI resolves the exact raw adapter and its assembly hash. The semantic verifier rejects a perfectly internally consistent aggregate or SDK fake-chunk receipt when it lacks the authorized adapter type/hash, streaming request fingerprint, response headers, physical-read/EOF provenance, process signature, or same-process binding.

## Required vectors

1. Split valid and invalid wire UTF-8 scalars at every physical-read byte; split CR, LF, and CRLF at every boundary.
2. Split SSE field names, colon/value separators, multi-line `data:` events, blank event delimiters, and `[DONE]` across arbitrary reads; reject invalid grammar and exact/+1 line/event safety caps.
3. Split JSON tokens and escape sequences across reads/events as applicable; accept valid surrogate pairs and reject invalid escapes/unpaired surrogates.
4. Prove raw `\u4e2d` counts six wire bytes but three answer-delta bytes. Accept a decoded answer delta of 16,384 bytes and reject 16,385, including an escape-heavy raw event. Accept final concatenated answer bytes of 65,536 and reject 65,537 across many deltas.
5. Prove raw-body 1,048,576 and SSE line/event 131,072 implementation caps pass and each +1 fails without changing FR-054 acceptance counters.
6. Empty/metadata/finish events do not reset inactivity; non-empty decoded deltas and valid `[DONE]` do. Exercise cancellation/disconnect races at every stage.
7. Reject missing/duplicate/contradictory terminal, error plus success, non-empty data/error after terminal, truncated wire UTF-8 at EOF, and post-terminal reads; permit only inert comment/whitespace before physical EOF.
8. Prove primary and one repair use the same deadline, packet, raw adapter, and authenticated process authority; semantic failure cannot repair.
9. Prove no mocked client receives any pre-acceptance domain and only validated Unicode-safe reveal is available afterward.
10. Accept a genuine signed raw-adapter receipt. Reject altered bindings/signature, foreign process/candidate/case/request/provider/assembly, missing headers/read/EOF disposition, and a perfectly consistent SDK aggregate fake-chunk receipt solely because authorized raw-adapter provenance is absent.
