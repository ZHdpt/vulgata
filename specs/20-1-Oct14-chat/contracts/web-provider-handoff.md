# October Chat Web/provider seam — 2026-10-07

Baseline: `f3db38db` above integration `082994eb`. The user authorized the next bounded Chat wave, coordination with OCT-Integrator `01a1109d-ec4c-7500-8b73-642a5e22f59b`, and reviewed owned commits. Integration accepted ownership of selected configuration/credentials/retries-off SDK construction and Web-local admission; Chat owns raw HTTP/SSE validation and bounds. No duplicate validator or generator admission reuse. Business dialogue/disclosure remains a separate canonical-contract prerequisite.

## Existing compiled consumer surface

- `OctoberChatModule.AddOctoberChat(IServiceCollection)` registers scoped `IOctoberChatSelectionService` and `OctoberChatEntryViewModel` only; it does not register execution.
- `IOctoberChatSelectionService.OpenAsync(ReadSelectionRequest? trustedSelection, CancellationToken cancellationToken = default)` consumes the host-derived publication, current `KnowledgeSubject`, fixed mode, `Purpose=Chat` and canonical Repository scope. It returns an ephemeral selection/conversation, not a durable admission receipt.
- `ChatPage` accepts optional `OctoberEntry`, `OctoberSelection` and `OctoberTurn` parameters. Existing entry lifecycle handles pending selection changes and requires settled work before a fresh context. Integration supplies the Chat-purpose context, notification and host binding; it must not relabel/reuse a Reader-purpose token.
- Integration confirmed public `Web.Services.OctoberChat.IOctoberChatContextProvider`, with `event Action? ContextChanged` and `Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default)`. It will register through AddOctoberWikiReader plus AddOctoberChat without route/generation activation. Freeze the explicit Wiki database/batch/publication configuration; derive current Identity at each observation; null/unavailable on missing or changing identity/configuration. Existing selection-only empty-contributor/null-kind checks do not authorize model inputs, answer text or history.
- Internal `OctoberChatAgent(IChatClient client, OctoberWikiTools tools, OctoberChatBudget budget)` requires the facade's same budget instance and rejects an already function-invoking client. `RunAsync(string question, string instructions, Action<string> onText, CancellationToken cancellationToken = default)` is the established controlled execution entry. The current wave adds an optional nonblocking progress callback after cancellationToken; it reports actual invocation lifecycle with host-generated IDs only. This is not a public executable Web factory yet.
- Internal `OctoberChatPromptCatalog.LoadAsync(KnowledgeMode mode, CancellationToken cancellationToken = default)` returns strict bounded text or null. Integration owns Agents csproj embedding under exact logical names `Vulgata.Agents.Prompts.OctoberChatBusiness.txt` and `Vulgata.Agents.Prompts.OctoberChatIT.txt`. Default loader tests can use the existing test friend assembly. Actual public execution/prompt factory should be settled with D0/W1, not by making a bare agent reachable from Web early.

## Raw guard API selected for Chat implementation

Proposed Chat-owned public type in `Vulgata.Agents.ChatExecution.October`:

```csharp
public sealed class OctoberChatStreamingHandler : DelegatingHandler
{
    public OctoberChatStreamingHandler(HttpMessageHandler admittedTransport, OctoberChatBudget budget);
    public Task SealAndWaitForSettlementAsync();
}
```

This signature is now implemented and controlled-qualified in Chat's raw-guard increment (455 affected passes; quickstart.md). Create one guard per turn; it borrows `admittedTransport`. The integration chain is SDK HttpClient -> Chat guard -> integration admission handler -> physical handler. The agent and guard share the original budget. The typed adapter alone calls TryStartProviderRound before invoking the client; guard captures that already-started round and rejects a second actual HTTP send for it. No automatic retry, concurrent alternate request, second provider-round charge or new tool invocation loop.

Guard responsibilities: POST streaming ChatCompletions shape only; bounded raw bytes via TryAddSseBytes on the captured round; successful HTTP/SSE response; strict incremental UTF-8/framing; bounded events/native fragment storage; one choice; consistent native call index/ID/name and bounded arguments; consistent terminal finish. Ordinary validated text-bearing events reach SDK immediately. Hold only the final `[DONE]` marker until strict physical EOF validates the tail. Missing/trailing/duplicate terminal data fails honestly; no full-answer buffer or candidate-only assembler. The existing typed adapter still performs complete-round tool admission and strict facade validation. Hidden reasoning is not emitted by the typed text projection.

Integration responsibilities: frozen selected provider/profile/model/ChatCompletions support; protected credential resolution; no implicit provider failover; endpoint request construction and retries-off client; request permits and actual physical I/O lifetime. Existing installed constructor pattern uses `OpenAIClientOptions` with `RetryPolicy = new ClientRetryPolicy(0)`, `Transport = new HttpClientPipelineTransport(httpClient, false, null)`, disabled distributed tracing/body logging, and selected `OpenAI.Chat.ChatClient(model, new ApiKeyCredential(apiKey), options)`. Expose it as the installed plain IChatClient adapter, not another FunctionInvokingChatClient. Integration owns SDK/client/physical-handler disposal. Chat's decorator must not dispose the borrowed admitted handler.

The compiled public adapter is `selectedChatClient.AsIChatClient()` from Microsoft.Extensions.AI; direct OpenAIChatClient construction is internal and must not be used. Integration also accepted `AllowAutoRedirect=false` for the physical handler: swallowed redirects can bypass the outer guard's one-send/selected-endpoint check. Qualify 302/307 no-follow behavior with a controlled transport; this is not supplied by the Chat guard alone. The current guard validates POST/ChatCompletions route and actual SSE response; selected request model/stream construction remains integration's responsibility, with no invented request-body cap borrowed from response-byte limits.

## W1 settlement contract — accepted operation; guard cleanup handshake

Recommended per-turn integration-owned operation:

```csharp
Task SealAndWaitForSettlementAsync();
```

Integration confirmed this operation name and no-caller-token semantics. Sealing atomically forbids additional sends. The returned task completes only after **all physical SendAsync, body-read and disposal operations have settled and their permits have been released**. Cancelling a caller's wait never completes this task or proves physical cleanup. Chat does not declare a competing admission interface, request Options key or persistence DTO.

To include Chat's deferred cleanup, the Chat handler also exposes the same seal-and-settlement operation for **its own** tracked send/read/response-stream cleanup. It seals dispatch, signals owned stop, initiates cleanup without racing pending I/O, and returns one stable task. Pending ignored-cancellation reads remain registered until they settle and their response/stream disposal completes. No borrowed inner handler is disposed. Integration starts BOTH guard sealing and admission sealing before awaiting their tasks together; it must not await admission drain before asking the guard to release response resources. Keep the guard, SDK client and admission owner alive while either task remains pending. SDK synchronous Dispose may initiate this process but never certifies it. Successful completion of both is required for normal terminal/permit settlement; an unconfirmed cleanup/release remains explicit unresolved cleanup rather than a fabricated release. A failed send/read, cancelled generation or rejected protocol CAN drain successfully once operations have terminated and resource release is confirmed; successful drain does not mean successful generation. Only unproven cleanup/release keeps settlement unresolved. Integration accepted this two-owner handshake; its factory must preserve it before W1 is usable.

Admission wait consumes the original turn deadline. Check eligibility immediately before actual send. Retain each permit until actual request transport cleanup, including a noncooperative send/read/disposal that outlives the SDK consumer. Stop-and-seal after agent completion; keep observing/owning deferred cleanup. Do not release the dialogue active slot or claim durable completed settlement based only on the provider-loop result. No Dispose/DisposeAsync may race an outstanding read; SDK synchronous disposal needs an owned deferred cleanup path. A cleanup fault with unconfirmed release is not confirmation of settlement. Usage must be actual or explicitly unknown, never invented zero.

The guard bounds consumer waits and may observe unfinished I/O after cancellation; it cannot forcibly terminate arbitrary borrowed I/O. Integration's inner lifetime tracker must retain the underlying operation regardless of the outer wrapper returning or being disposed. No source/API/credential data belongs in logs/progress. W1 acceptance and D0 canonical outcomes must reconcile this requirement before production dispatch.

## Controlled qualification and next handshake

Use real installed OpenAI ChatClient -> plain IChatClient -> October agent over a controlled HttpMessageHandler, not a scripted typed client alone. Proposed probes cover split multibyte bytes/SSE fields and native arguments, early text before tool/finish gates, interleaved calls, invalid IDs/names, usage after finish, `[DONE]` plus EOF/tail failures, original aggregate budgets, refused retries and cleanup ownership. A physical fake stream that ignores cancellation must let consumer cancellation return while settlement stays pending/permit retained until its gate releases; assert exactly-once disposal and no late callbacks. No live endpoint or new real dataset is needed for these probes.

Integrator will publish exact approved embedding/Chat-purpose context/W1 commits. Chat imports only those approved checkpoints and checks actual behavior against this seam. Business owns full-input dependency completeness and all dialogue/storage contracts; the earlier consumer contribution remains a proposal until agreed. T012–T015/T020–T021 remain partial, T016–T019 depend on canonical ports, and T022 persistence versus T023 provider/browser qualification remain distinct. This handoff does not authorize resources or activate sending.

Independent review accepted the implemented raw guard with the lifetime/framing conditions above; controlled actual-SDK evidence is in quickstart.md. The separate integration-owned client/admission and real provider boundaries remain unqualified by that evidence.

### Canonical tool-result representation correction

The canonical consumer's actual SDK request test found that the installed adapter indents JsonElement results, changing bytes after owner sizing. Chat therefore returns strictly decoded owner `OutputUtf8` as literal JSON text through the SDK text-result branch, without serializing that string again. Controlled HTTP capture now proves the decoded tool-message content is byte-for-byte identical to the owner output and parses as an object, not a quoted JSON string. This preserves the agreed output profile and exact accounting; it supersedes the earlier JsonElement implementation assumption. Host capsules remain excluded. The 541-case affected GREEN and narrower evidence limits are recorded in quickstart.md; Business owns any corresponding clarification to its shared contract document.

## Admitted runner API — compiled and controlled-qualified

`Vulgata.Agents.ChatExecution.October.OctoberChatTurnRunner.CreateAsync` takes, in order: `DialogueBeginResult`, plain `IChatClient`, the original `OctoberChatBudget`, `ReadSelection`, `DialogueHistoryResult`, selected profile ID and revision, `IOctoberKnowledgeDisclosedReader`, `IOctoberKnowledgeDependencyAuthority`, `IOctoberDialoguePayloadCodec`, `IOctoberDialogueExecutionAuthority`, fresh-selection `Func<CancellationToken, Task<ReadSelectionRequest?>>`, coordinator-preallocated terminal save `Guid`, and optional cancellation token. It returns a nullable runner. Null neither proves admission absence nor releases its durable slot. The factory accepts only confirmed dispatchable admission, loads the real embedded prompt, checks canonical history/digests and merges owner dependencies; no raw question/prompt/history override exists.

`RunAsync(Func<string, CancellationToken, ValueTask> authorizedText, CancellationToken = default, Action<OctoberChatToolProgress>? onToolProgress = null)` is single-use. The text sink must honor cancellation and context/attachment ownership; the progress sink must synchronously enqueue bounded sanitized events without blocking. `OctoberChatGenerationResult` contains canonical terminal state/reason/provider-finish, answer, exact references/consumed proofs/full dependency capsule, actual generation end time and the same save operation ID. It is **generation facts only**, never a saved receipt or physical settlement certificate.

Bind `ReauthorizeBeforeSendAsync(CancellationToken = default)` into Integration's request admission. It refuses outside active Run, verifies execution, then freshly reauthorizes the complete input union under the original lifetime. Invoke it after permit waiting and other awaited execution/profile checks immediately before physical send. A client-construction closure must refuse until the runner is assigned; construction sends no request. Keep root execution/profile fencing and both settlement owners. The public budget token/timer surfaces expose the original lifetime without resetting it. These APIs are controlled-qualified by 583 affected passes; production route, authoritative registry, final persistence and live qualification remain owner prerequisites.
## Canonical coordinator attachment boundary — 2026-10-07

`OctoberChatCoordinator` consumes the Business store/codec and the integration-owned
`OctoberChatClientSession`; it does not issue execution authority or activate a route.
The host freezes/registers the exact request, initial history dependency union,
selected profile and original budget before calling `ExecuteAsync`. Its required
generation observer attaches the actual `Task<OctoberChatGenerationResult>` before
the post-permit callback can send. A throwing observer seals and drains the session.
The original Admitted snapshot remains immutable; current phase is tracked separately.

The host retains the whole execution task through factory, generation and physical
settlement, even when a browser detaches. Canonical store calls use separate bounded
persistence attempts. A terminal generation result is not a saved receipt. Root-owned
registration, expected-operation checks and actual database-task settlement remain
required in production; controlled authority doubles do not replace them.

An uncertain transition is displayed as unknown, not the last confirmed phase or
an idle slot. Exact admission lookup may complete only the first never-attempted save
of this original lifecycle after the original phase task and owned work settle.
It never restores dispatch. Once a save is attempted, even if invocation throws,
the immutable payload is retained and recovery uses exact terminal lookup only.
Missing owner rows do not prove absence or authorize resubmission.

After physical drain, fresh full-capsule authorization precedes the first terminal
freeze. Explicit owner denial/unavailability can produce the owner's sanctioned
empty authorization-failure closure from a nonterminal phase. Missing context,
unknown lineage, exceptions or unconfirmed cleanup cannot manufacture this exception.
Provider finish and generation-end time remain actual facts. An already attempted
payload is never rewritten. The host's commit guard must preserve this exact exception.

T020/T021 still require a bounded presentation attachment. A queued delta needs live
authorization at actual disclosure; an earlier runner check is not a lasting grant.
Slow/throwing subscribers must not block owned generation. Confirmed sanitized closure
must clear provisional prose/references, not describe them as retained. Existing
controlled events do not yet supply this integration or browser qualification.
## Bounded presentation attachment — application plumbing

The Runner and Coordinator retain their existing callback overloads and add overloads
carrying `KnowledgeDependencyHandle` with authorized text and sanitized progress.
The immutable opaque snapshot is host-only, never part of an outward event. The same
runner/lifecycle executes both APIs; this adds no model tool or invocation loop.

Create `OctoberChatTurnPresentation` only inside the confirmed-Running generation
observer, using the original admission, exact binding and preallocated save identity.
Producers use nonblocking TryPublish methods. Delivery has its own cancellation and
observable Completion; disconnect, overflow and subscriber failure cannot cancel
generation or terminal persistence. Retain an actual callback that ignores cancellation
until it settles. Completion is a lifetime fact, not proof of successful display.
The default queue allows 64 waiting events with a conservative 4 MiB retained-memory
reservation that includes the active item and protected capsule strings. This is local
presentation accounting, not canonical serialized-size or generation-budget accounting.

After dequeue, the adapter obtains fresh Chat context, reauthorizes the exact opaque
union and checks current context/authentication again before application handoff.
A trusted asynchronous UI sink must also check its current attachment and supplied
token at actual apply/render time. No atomic revocation across later browser/network
delivery or recall of previously delivered bytes is claimed. Context replacement must
remove the old bound view; detached delivery cannot be used as a replay mechanism.

Only exact confirmed canonical results produce TurnSaved. Ordinary results require
matching authorized projection; sanctioned no-content authorization-failure receipts
clear prose/references explicitly. A separate content-free DisclosureSuppressed event
handles explicit denial/unavailability during queued delivery. It clears the original
bound view and prevents late content from returning, without claiming persistence,
terminal status or active-slot release. A throwing/noncooperative UI sink still cannot
be forced to display a clear; its actual lifetime remains owned.

No production page sending, host registry, route, Markdown renderer or reader-link
composition is activated by this adapter. Root alone supplies those prerequisites.
## Encoded leaf rendering subset

The October turn uses a leaf renderer with fixed application tags and encoded text
nodes only. Supported formatting is deliberately limited: paragraphs, ATX headings,
flat unordered and sequential ordered lists starting at one, block quotes, closed
triple-backtick code fences, single-backtick inline code and flat emphasis/strong spans.
This is not full Markdown/CommonMark. Unsupported links, images and HTML remain
readable text; model input never supplies a tag, attribute, class, destination or image.
Unfinished fences remain visible literal text immediately, without waiting for a
provider finish or closing delimiter. Exact code content is encoded, not interpreted.

Parsing is capped at 65,536 UTF-16 characters, 4,096 nodes and a bounded scan-work
budget. Exceeding a bound renders the entire original encoded text, without truncation
or partial syntax-tree output. These are renderer limits, not new generation budgets.
Styling is local to the turn and inherits the existing theme. Parent ChatPage, routes,
dependencies and exact authorized reader-link projection are unchanged by this leaf.
## Saved-reference footer agreement — 2026-10-07

Integrator released the bounded controlled T026/T027 slice after Business confirmed
the existing contracts and limits. Explorer has no combined authorized title/destination
API. Chat's local adapter composes canonical disclosed exact reads with the unchanged
`ExploreGraphOutputEncoder` and `ExactDocumentLinkBuilder`; neither helper grants
disclosure authority. Reader-purpose opening is not substituted for fresh Chat authority.

Initial inputs are trusted owner-issued saved `DialogueTerminalProjection` values with
their complete original turn/history lineage and exact original reference facts.
Live preterminal reference projection remains deferred behind actual consumed-proof
validation. A title read is one initial exact content page, not a metadata-only API:
charge every encoded byte and retain the whole page capsule, but do not add model
consumption, coverage/read-completeness credit or change original partial flags.
The title is the server document title, never an invented section title. Exact section
identity remains in the original handle and local destination.

The adapter verifies outer/page success, non-null exact server document, all five handle
parts, unchanged encoder profile, complete output bytes and output/page digests.
It reauthorizes the complete original lineage even for zero references, merges every
successful display-page capsule through the owner, and uses the resulting single union
for fresh final authorization and exact current-attachment/context checks at handoff.
No contributor reconstruction, missing-lineage root, stale title cache, retargeting,
new route, Core contract or schema is permitted.

One fresh display attempt allows 10 seconds total across Prepare and Handoff, serial
acquisition of at most 16 initial pages, 64 KiB per complete result and 256 KiB aggregate,
with the canonical 256-reference bound. There is no continuation, retry, cache, eviction
or renewed model budget. Owner capsule/contributor/origin bounds still apply. Ended
generation alone does not deny a fresh saved-history display attempt. Late results do
not renew the deadline or publish Ready data; actual pending owner/sink work remains
observed until settlement without holding generation or terminal persistence.

Prepared data stays private until the bounded local footer handoff. Closed
`TurnPresentation` is not reopened. A trusted UI sink rechecks the exact attachment
at actual apply; generic footer failure contains no stale titles, links or counts.
Explicit aggregate denial/unavailability suppresses the whole dependent display.
Saved-view suppression must clear content while preserving terminal/save-indeterminate
bookkeeping and reject another binding, without reusing the closed stream sequence.
Root retains parent ChatPage, registry, route and runtime activation.
