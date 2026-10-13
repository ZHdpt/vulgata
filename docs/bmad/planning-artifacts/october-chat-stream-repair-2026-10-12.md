# October Chat stream diagnosis and repair — 2026-10-12

Subsequent reference-visibility repair: the existing nine exact-version source
links were below the long answer. Added a source-count shortcut above each
document-backed answer and a distinct source-list card. Shortcut URLs preserve
both conversation and context IDs (fragment-only links resolve against the app's
base URL and would otherwise drop them). Browser verified all nine links and
successfully opened Document version `217b7ece-ef71-4209-959f-419229bd0cd7`.
Focused source/projection/application/UI tests: 88/88, zero failures/skips;
`TestResults/isolation-6cfebc5076f84737b35faff9022107ea/results.trx`.
Screenshot: `.tmp/chat-references-verified.png`. No new provider call was needed.
After this verification the task-owned Web host was stopped at the user's request,
and port 15075 was confirmed closed; the earlier running-host statement below
describes the preceding streaming qualification only.

Final ordinary-user replay succeeded on the uninstrumented repaired host:
**61.41 seconds, 9,638 saved Markdown characters, nine citations, ValidStop**.
Reload preserved all 8,714 rendered characters and showed `回答已保存。` with the
composer enabled. This qualifies this exact question/selection, not every query.

Direct user-authorized debugging in the current workspace; no story activation.
Existing work and retained publications are preserved. Limits remain 30 provider
rounds, 64 tool calls and 540 seconds; no automatic provider retry was added.

## Instrumented baseline

Ordinary account, IT mode, two selected repositories, question
“手机银行登录是怎么做的”, retained database
`vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`.
Conversation `04867868-b726-4c6b-a225-838e4311c2d6`, turn
`8ebdda41-8b06-47f6-a799-25f86851fc75`.

- Saved after 342.77 seconds: Failed / ExecutionFailure / Unknown provider finish,
  with 4,109 Markdown characters retained.
- Exception: `System.IO.IOException` while reading the provider TLS stream through
  `SslStream`, HTTP chunked response, and the SSE reader. No valid final completion
  was observed. The remote/network reason for the broken connection is unknown.
- At failure: 7 provider rounds, 17 tool calls, 7,017 output bytes; no transport
  budget rejection, disclosure failure or adapter failure was recorded.
- At 2,000 text updates, 296.15 seconds had been spent in live authorization alone.
  Rechecking overlapping contributor groups repeated database work per tiny delta.
- Browser answer remained empty while the server had already processed over 2,000
  characters. The bounded presentation queue repeats large dependency metadata per
  item and detaches on overflow; a controlled slow-subscriber/tiny-delta regression
  reproduced this mechanism. The live queue detach itself was not instrumented.

Sanitized temporary evidence: `.tmp/chat-failure-diag/events.json`; temporary
instrumentation was removed before repaired-host qualification. No provider
payloads, credentials, prompt content or hidden reasoning were logged by it.

## Changes

- Dependency authority checks the complete union of contributors once per Document
  kind, retaining fresh subject checks before and after. Sealed provenance remains
  unchanged; no permission caching or authorization bypass.
- Presentation combines only adjacent queued answer deltas with identical
  dependency proofs. Active items, tool/status boundaries and different proofs
  cannot merge. Memory/text bounds and fresh authorization at delivery remain.
- Provider-side batching combines already-buffered text chunks before repeated
  disclosure checks. The first text is immediate; batches flush at 128 characters,
  metadata/tool boundaries, end-of-stream, or before awaiting the next provider
  update. An in-flight read retains its cleanup owner if the consumer stops.
  Each delivered batch still receives live authorization; no permission caching.
- Provider stream `IOException` is classified as Interrupted / Truncated, preserving
  partial text without retrying or claiming successful completion.
- Citation registration retains existing answer text; it emits no retrieval-start
  event. Tool accounting and reference authorization are unchanged.
- Tool schemas advertise the effective maximum page size. Prompts explain the
  actual AND-substring search semantics, discourage repeated broad discovery, and
  direct citation registration before final synthesis. All hard limits remain.

## Additional replay findings

The first repaired replay, turn `acd4e7fe-4ad0-4f51-af70-05fce59ee80b`,
hit BudgetExceeded at 41.35 seconds with nine consumed targets and no final text.
That uninstrumented turn does not retain the exact exhausted counter. A subsequent
counter-instrumented replay did not reproduce it; limits were not increased.

That subsequent turn, `694ce816-6707-4b3f-b62f-1c06b2eec453`, completed with
ValidStop and four citations in 151.06 seconds. The browser visibly streamed 650
then 1,779 characters. However, reference registration after an already-written
answer was incorrectly treated as a retrieval boundary and discarded that answer;
only a 466-character recap was saved. This is a separate answer-retention defect,
not evidence that the recap represented the full generated answer. A regression
and correction cover citation bookkeeping without clearing answer text.

A later replay, turn `a389d78e-7177-4d59-9d10-832de5dc2c71`, failed with
ProtocolFailure / Invalid at 13.79 seconds, after two consumed targets. Further
sanitized diagnostics first narrowed this to `SearchWiki` / InvalidArguments,
then reproduced `BrowseWiki` / InvalidArguments with `pageSize: 30`, query length
2, no continuation and no unknown fields. The admitted Chat maximum is 20, but
the tool schema advertised only `minimum: 1`. The schema now declares the lower
of the admitted page-size limit and Knowledge's contract maximum. Server-side
validation and refusal remain in force; this does not enlarge permissions or limits.

After the schema correction, turn `e6abfb40-d70d-412e-86b6-1af1121b6c8b`
still hit BudgetExceeded at 47.40 seconds. Its 709,255-byte saved terminal contained
a 664,411-byte encrypted inference capsule and 44,012 bytes of consumed receipts.
This is close to the encrypted size implied by the 512-KiB canonical evidence
limit; the exact exhausted counter was not retained, so that attribution remains
an inference. Search semantics and focused-retrieval prompt guidance were added
without raising that limit. A subsequent pre-batching run still streamed slowly
and ended interrupted, motivating batching before expensive per-delta disclosure
checks rather than only coalescing the downstream display queue.

## Controlled verification

The union and queue regressions each failed before their respective fixes. The
disconnect regression also failed before its classification fix. The final affected
authorization/Chat SelfContained suite passed **625/625**, zero failures/skips,
with current-source build zero errors (existing analyzer warnings remain).
Command: `pwsh -NoProfile -File eng/test.ps1 -Filter
'FullyQualifiedName~October.Chat|FullyQualifiedName~OctoberChat|FullyQualifiedName~OctoberDisclosureTests'`.
TRX: `TestResults/isolation-124b171e7fd549c3907f6a70615543ef/results.trx`.
This is not full-suite or PostgreSQL-suite qualification.

## Final live qualification

Question “手机银行登录是怎么做的”, same ordinary account, IT mode, two selected
repositories and retained database. Conversation
`a37e0925-a871-4d89-987e-497a0e06ef05`, turn
`2b39eb9f-e9de-4885-a3ff-3ee19a01c426`.

- Completed / None / ValidStop, nine consumed targets and nine references.
- Admitted-to-committed time: 61.41 seconds; full saved answer: 9,638 characters.
- Reloaded browser: 8,714 rendered characters, `回答已保存。`, enabled composer,
  and a scrollable response area. Screenshot:
  `.tmp/chat-failure-diag/repaired-answer.png`.
- Running normal Debug Web host on port 15075; loaded Agents DLL contains no
  temporary `CHAT_DIAG` marker. Current source has no temporary instrumentation.
- Comparison with the instrumented baseline is observational: the provider's
  retrieval choices and output differ. It is not a controlled provider benchmark.
