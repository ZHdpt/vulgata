# Chat Provider Streaming Contract v1

## Scope and sole production authority

This is the server-only transport boundary for every Story 12.5 primary synthesis request and the single permitted syntax-repair request. The production `IChatProviderStreamingTransport` registration MUST resolve directly to `Vulgata.Agents.ChatExecution.OpenAiCompatibleChatProviderStreamingTransport`. That response-header raw adapter, not an SDK aggregate response, SDK fake-chunk wrapper, dispatcher, test double, or release harness, is the sole authority allowed to originate a Track B runtime stream observation. Track C release attestation is a separate export boundary below.

```csharp
internal interface IChatProviderStreamingTransport
{
    IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
        ChatProviderStreamRequest request,
        CancellationToken cancellationToken);
}
```

`ChatProviderStreamRequest` carries the selected provider identity, bounded prompt, runtime request/attempt identities, packet fingerprint, canonical streaming-request fingerprint, and one absolute deadline. It contains no candidate commit, release run, corpus case, signature, or export identity. Signals are `AnswerDelta`, `Terminal`, or `ProviderError`; none is client-renderable.

## Ordered byte-to-answer pipeline

The following stages are ordered and have separate state, counters, and failure ownership. No stage may collapse two domains into one counter.

| Stage | Input and output domain | Bounds and counting | Owner and closed failures |
| --- | --- | --- | --- |
| 1. Raw response-header HTTP adapter | Arbitrarily segmented response-body octets from `HttpContent.ReadAsStreamAsync`; headers are recorded separately | Each `ReadAsync` requests at most 16,384 bytes. This is an implementation read-buffer safety cap, not FR-054's Provider Chunk acceptance cap. Raw response-body total <=1,048,576 bytes is an implementation anti-overhead cap. | adapter owns status/media/protocol validation, cancellation/disconnect, raw total overflow, first physical read, and physical EOF disposition |
| 2. Strict wire UTF-8 decoder | raw response-body octets -> Unicode SSE characters across arbitrary physical reads | Incremental strict decoder; incomplete scalars carry across reads. Malformed, overlong, surrogate, or truncated-at-EOF wire UTF-8 fails. No replacement. | wire decoder owns `invalid-wire-utf8` |
| 3. SSE field/event parser | decoded characters -> complete SSE fields/events across arbitrary reads, CR, LF, or CRLF boundaries | A decoded SSE line and one assembled SSE event are each <=131,072 wire UTF-8 bytes; raw event accounting uses the exact pre-decoding body slice. These are implementation grammar/overhead caps, not FR-054 acceptance caps. Comments/unknown safe fields are ignored; invalid field grammar or cap overflow fails. | SSE parser owns `invalid-sse`, `sse-line-overflow`, and `sse-event-overflow` |
| 4. Received logical Provider Chunk gate | one complete nonterminal SSE `data` event -> its exact received payload octets | One SSE `data` event, including empty, metadata, finish, error, or answer JSON, is one FR-054 **Provider Chunk**. Count the exact wire UTF-8 octets in its `data` field values after SSE field-name/colon/optional-one-space removal; exclude SSE line terminators, field syntax, and the parser-inserted multi-line LF. Count JSON syntax, escapes, and metadata. `[DONE]` is the separate terminal and is not a chunk. Before JSON decoding, require each chunk <=16,384 received bytes and the running sum of all chunks <=65,536 received bytes. | received-chunk gate owns `received-provider-chunk-overflow` and `received-provider-response-overflow`; the offending chunk is never JSON-decoded |
| 5. JSON event decoder | one already-bounded Provider Chunk -> typed provider delta, finish metadata, or error | JSON is decoded only after both received-byte gates pass. Invalid JSON, duplicate/invalid required fields, invalid JSON escapes, or unpaired escaped UTF-16 surrogates fail. | JSON decoder owns `invalid-json-event`, `invalid-json-escape`, and provider error classification |
| 6. Answer-delta encoder | decoded provider content string after JSON unescape -> strict UTF-8 answer-delta bytes | One non-empty decoded content delta is a separate defense-in-depth domain, <=16,384 bytes after strict UTF-8 re-encoding. For example received `\u4e2d` is six chunk bytes and three decoded-delta bytes. This bound does not define FR-054 and cannot excuse a received-byte overflow. | delta encoder owns `decoded-delta-overflow` and invalid decoded scalar state |
| 7. Grounded-answer assembler | ordered decoded answer-delta bytes -> one complete answer byte sequence/string | Concatenated decoded answer bytes are a separate defense-in-depth domain, <=65,536 bytes. Strict final decode/flush occurs only after valid terminal plus physical EOF. This bound does not replace the assembled received-response bound. | assembler owns `final-answer-overflow`, `missing-terminal`, `duplicate-terminal`, `contradictory-terminal`, `post-terminal-data`, and final invalid/truncated answer UTF-8 |
| 8. Grounded-result validation | complete assembled string -> parsed and accepted `GroundedAnswer` | no new byte budget; syntax, packet fingerprint, handles, claims, citations, semantic evidence, currentness, and authorization all must pass | dispatcher/coordinator own syntax-only repair decision and all validation/publication failures |

The normative and defense-in-depth counters are distinct and monotonically updated at their owning stage:

| Named domain | Exact counter | Accepted cap |
| --- | --- | --- |
| physical raw read | `maximumRawReadByteCount` | 16,384 requested/read implementation cap |
| raw response body | `rawBodyByteCount` | 1,048,576 implementation cap |
| SSE line/event | `maximumSseLineByteCount` / `maximumSseEventByteCount` | 131,072 each, implementation caps |
| received logical Provider Chunk | `providerChunkCount` / `maximumReceivedProviderChunkByteCount` | 16,384 received payload bytes per chunk, normative FR-054 |
| assembled received provider response | `assembledReceivedProviderResponseByteCount` | 65,536 sum of received Provider Chunk payload bytes, normative FR-054 |
| decoded answer delta | `answerDeltaCount` / `maximumAnswerDeltaByteCount` | 16,384 strict UTF-8 bytes, additional bound |
| final decoded answer | `assembledAnswerByteCount` | 65,536 strict UTF-8 bytes, additional bound |

The raw-body and SSE caps contain framing overhead and remain independent: a large SSE event may fail its safety cap before a Provider Chunk forms, while any formed Provider Chunk still fails at 16,385 received payload bytes even when its decoded answer is tiny. No counter is inferred from another.

## Liveness, terminal, error, and EOF

- Ten-second inactivity resets only when stage 6 emits a valid non-empty `AnswerDelta`, or stage 4 derives the single valid `[DONE]` terminal. Physical reads, SSE comments, empty `data:`, metadata-only JSON, and finish metadata do not reset it.
- `[DONE]` is the one logical successful terminal. A non-null finish reason is pending metadata: `stop` is consistent; truncation/length, content-filter, error, two different reasons, or success/error coexistence is contradictory. Provider error JSON is `ProviderError`, never a successful terminal.
- The adapter continues physical reads after `[DONE]` until EOF. A second `[DONE]` is duplicate. Any later non-empty decoded content delta or provider error is post-terminal data. Comments, empty events, and transport whitespace may follow but cannot reset liveness.
- EOF before `[DONE]` is missing terminal. EOF after exactly one consistent `[DONE]`, with no post-terminal data, permits final assembly. Disconnect, cancellation, inactivity, or deadline is not EOF success.
- Cancellation wins until the complete result has passed every acceptance check. Every failure cancels/disposes the read, discards and best-effort zeroes raw/event/delta/assembly buffers, and exposes only a closed content-dark category.

## Shared deadline and repair

`PreparedChatCoordinator` creates one monotonic deadline capped at 29,000 ms. Retrieval, primary streaming, optional repair streaming, all decoding/validation, currentness/authorization, completion-summary/next-conversation/reveal preparation, and the publication linearization consume it. Repair starts only after a closed syntax failure and with at least 3,000 ms remaining. It uses the same packet/fingerprint/deadline and the same raw adapter pipeline. Semantic/citation/currentness/authorization failures never repair; there is no third call.

One Web-scoped `IPreparedChatPublicationOwner` is the sole success owner. It receives a preallocated composite candidate after every fallible/async/telemetry-preparation operation is complete. In one single-writer critical section it samples cancellation, samples monotonic time once, and either closes without publication or performs `AcceptedPublicationCommit`, one non-throwing atomic terminal-state exchange that simultaneously makes the complete answer, proposed conversation, accepted completion summary and reveal directive authoritative. The sample must be strictly before the deadline. No later clock read, callback, telemetry emission, conversation write or publication operation may participate in success. Time advancing after the named commit does not retroactively invalidate it.

## Whole-answer browser barrier

No raw octet, SSE field, JSON event, answer delta, partial answer, claim, citation, handle, relationship, or Source Anchor detail crosses the server-to-client boundary before complete acceptance. `ChatPage` exposes only generic pending/cancel state and reads content only from `AcceptedPublicationCommit`. The committed directive is either immediate full display or a precomputed Unicode text-element schedule; reduced motion, insufficient precomputed budget, or schedule lag uses immediate full display. Track C records actual complete-render time and fails release evidence at 30,000 ms or later without moving release identity into Track B or retroactively changing accepted content. Operational deterministic rescue is not a streamed provider answer and cannot satisfy Tier 3 streaming evidence.

## Track B neutral runtime telemetry

For each attempt the raw adapter emits one immutable content-dark `ChatStreamRuntimeObservation` containing:

- opaque runtime observation ID, process-local monotonic sequence, request ID and attempt kind;
- provider API identity/non-secret configuration fingerprint and canonical request fingerprint containing `stream=true`;
- response-header disposition/status/media/protocol/timestamp;
- first physical-read and physical-EOF timestamps/dispositions;
- independent raw-read/body, SSE line/event, `providerChunkCount`, `maximumReceivedProviderChunkByteCount`, `assembledReceivedProviderResponseByteCount`, `answerDeltaCount`, `maximumAnswerDeltaByteCount`, and `assembledAnswerByteCount` counters, plus terminal/error outcome and rejection category.

The runtime record has no post-transport validation result, candidate commit, release run ID, corpus case ID, process public-key fingerprint, signature, artifact/export path, prompt, response, evidence, claim, citation, handle, relationship, Source Anchor, secret, connection, or host path. `ChatSynthesisAttempt.RuntimeObservationId` references only that transport record. The coordinator separately prepares one content-dark `ChatStreamCompletionSummary` template; `ChatSynthesisAttempt.CompletionSummaryId` references the committed summary and `ValidationOutcome` is copied only from it. The template binds ordered observation IDs, syntax-repair use and per-attempt/final validation outcome; accepted duration is derived from the primitive `AcceptedPublicationCommit` timestamp. The composite commit is the only accepted-summary emission, so timeout/cancellation cannot leave false accepted telemetry. Runtime and completion records support operations and Tier 1 verification only. Neither can be serialized directly as a Story 12.5 release receipt or claim release eligibility.

## Track C signed release attestation

Track C optionally composes a release-only `Story125StreamAttestationSink`. Its admission method is internal and receives observations directly from the production raw adapter; dispatcher, SDK aggregates, test doubles, and release-harness callers cannot submit a runtime record. At release-process start the sink creates an ephemeral ECDSA P-256 key pair and opaque process-observation ID. Preflight binds its public-key fingerprint/process ID/candidate commit to the resolved adapter type, informational version and assembly SHA-256. The private key and admission capability never leave process memory.

For each admitted runtime observation the sink combines server-owned release run/corpus case context and the matching coordinator-owned completion summary with the immutable runtime observation ID, completion summary ID, sequence/request/attempt/provider/request/header/read/EOF/counter/outcome fields and signs the canonical SHA-256. Completion binding may add validation/repair/duration facts but cannot alter transport facts. It exports one content-dark `Story125StreamAttemptAttestation`. A same-process semantic verifier authenticates the signature, exact runtime-to-attestation transport equality, and exact completion-summary identity/validation equality before export. Test adapters may emit runtime telemetry, but only the production adapter can reach the release sink.

A fixed production-composition test and Tier 3 preflight must prove DI resolves the exact raw adapter and Track C sink/assembly identity. The semantic verifier rejects a perfectly internally consistent aggregate or SDK fake-chunk receipt when it lacks sealed adapter admission, immutable runtime observation binding, exact adapter type/hash, streaming request fingerprint, response headers, physical-read/EOF provenance, process signature, or same-process candidate/run/case binding.

## Required vectors

1. Split valid and invalid wire UTF-8 scalars at every physical-read byte; split CR, LF, and CRLF at every boundary.
2. Split SSE field names, colon/value separators, multi-line `data:` events, blank event delimiters, and `[DONE]` across arbitrary reads; reject invalid grammar and exact/+1 line/event safety caps.
3. At the public raw-transport seam, accept one valid logical Provider Chunk of exactly 16,384 received payload bytes and reject 16,385 as `received-provider-chunk-overflow` before JSON decoding. Prove metadata and escape bytes count, SSE framing does not, and the offending chunk yields no decoded delta or partial answer.
4. Accept a stream whose Provider Chunk received-byte sum is exactly 65,536 and reject the chunk that would make it 65,537 as `received-provider-response-overflow` before decoding that chunk. Every individual chunk remains <=16,384. Prove `providerChunkCount`, maximum-chunk, and assembled-response counters.
5. Use escape-heavy JSON to prove received `\u4e2d` counts six Provider Chunk bytes but three decoded-delta bytes; use metadata-heavy valid chunks to prove received totals grow while decoded counters remain unchanged. Split JSON tokens/escapes across physical reads; accept valid surrogate pairs and reject invalid escapes/unpaired surrogates.
6. At the pure post-JSON stage seam, accept a decoded answer delta of exactly 16,384 bytes and reject 16,385 as `decoded-delta-overflow`; accept final decoded answer bytes of exactly 65,536 and reject 65,537 as `final-answer-overflow`. Full-pipeline tests also prove a received-byte overflow wins earlier when both could apply.
7. Prove raw-body 1,048,576 and SSE line/event 131,072 implementation caps pass and each +1 fails in its own category without fabricating or substituting received/decoded acceptance counters.
8. Empty/metadata/finish events do not reset inactivity; non-empty decoded deltas and valid `[DONE]` do. Exercise cancellation/disconnect races at every stage.
9. Reject missing/duplicate/contradictory terminal, error plus success, non-empty data/error after terminal, truncated wire UTF-8 at EOF, and post-terminal reads; permit only inert comment/whitespace before physical EOF.
10. Prove every overflow discards all untrusted buffers and no mocked client receives any pre-acceptance domain; only validated Unicode-safe reveal is available afterward.
11. Prove primary and one repair use the same deadline, packet and raw adapter; semantic failure cannot repair. Prove Track B runtime records contain every named content-dark counter and no release fields.
12. Drive cancellation, deadline and throwing preparation doubles at every pre-publication state. Prove the prepared candidate has no live effect, the sole atomic commit updates answer/conversation/completion/reveal together, and structural guards reject any direct conversation append, accepted-summary emission, success return or page publication outside `IPreparedChatPublicationOwner`. Advance the fake clock immediately after the authoritative sample and prove the commit remains the linearization point rather than adding another impossible check.
13. Accept a genuine Track C attestation over an immutable admitted runtime observation and recompute all raw/SSE/received/decoded counters. Reject alteration of any counter or binding/signature, foreign process/candidate/run/case/request/provider/assembly, missing headers/read/EOF disposition, direct caller submission, and a perfectly consistent SDK aggregate fake-chunk receipt solely because authorized adapter-to-sink provenance is absent. Record actual complete render separately and reject release evidence at 30,000 ms or later.
