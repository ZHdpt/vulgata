# Quickstart: Validate Story 12.3

This guide is the accepted validation target for later implementation. It does not authorize task generation or production work.

## 1. Prerequisites

- Windows host checkout on a non-detached Story 12.3 branch.
- .NET 10 SDK.
- PostgreSQL 17 as the only required Docker service.
- CodeGraph `@colbymchenry/codegraph@1.5.0` and its accepted Windows x64 hashes.
- Java 21.
- `jdt-language-server-1.61.0-202608141332` with its accepted archive SHA-256.
- StreamJsonRpc 2.25.29 from central package management.
- two authorized neutral Repositories with an exact approved Story 12.2 pair of 50-150 File Code Units.
- a controllable OpenAI-compatible Worker provider for deterministic outage/function/stream/wire qualification;
- for live T062 gates only, configured DeepSeek endpoint `https://api.deepseek.com/beta`, model `deepseek-v4-flash`, and encrypted credential; the runtime composes but never rewrites this configured endpoint, and credentials are never printed.

Confirm topology without exposing paths or secrets:

```pwsh
docker ps --format "table {{.Names}}\t{{.Image}}\t{{.Status}}"
dotnet --info
java -version
codegraph --version
```

Expected: PostgreSQL 17 is the sole required Docker service; Vulgata, CodeGraph, Java, and JDT LS are host processes; CodeGraph reports 1.5.0 and Java reports major 21.

## 2. Static contract checks

Validate the strict schema parses and contains no open object shapes:

```pwsh
Get-Content -Raw specs/012-resumable-file-pass1-code-logic/contracts/code-logic-file-v2.schema.json |
  ConvertFrom-Json | Out-Null
rg -n '"additionalProperties": true|CodeGraphContext|retrieve_code_logic|Pass2BusinessLogic|Business Logic Document' specs/012-resumable-file-pass1-code-logic
```

Expected: JSON parsing succeeds; no `additionalProperties: true`; CodeGraphContext/Story 12.4 terms appear only in explicit exclusions/forbidden descriptions.

The later Agents project must link this exact specs schema as one MSBuild `EmbeddedResource`, not copy it. `Pass1StrictSubmissionSchemaTests` must prove assembly/source SHA-256 and byte equality; the Web/Docker build must fail if the linked root path is absent.

## 3. Canonical identity and projection

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1CanonicalEnvelopeTests|FullyQualifiedName~CodeLogicFileV2SchemaTests|FullyQualifiedName~CodeLogicFileV2ProjectionTests"
```

Expected:

- literal envelope/digest/UUID vectors pass against production and independent codecs;
- smallest/multi-item/Unicode/asymmetric-UUID vectors pass;
- every unknown/duplicate/unsafe/over-byte/depth/order/range/reference mutation rejects;
- every changed communication kind/level/confidence/rule/provenance/classification fingerprint/Catalog Rule Identity/Evidence Locator and every mixed-kind binding rejects;
- canonical ContentJson, Markdown, at most nine Sections, Index Entry, anchor/Edge IDs and commit fingerprint are byte-identical across repeated runs;
- no projection contains a claim absent from JSON.

## 4. Pair start, shared concurrency, and order

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1PairStartTests|FullyQualifiedName~Pass1ConcurrencyAndOrderingTests|FullyQualifiedName~Pass1ConcurrencyUpdateTests"
```

Expected:

- exactly two complete approved manifests start;
- all missing/partial/stale/mismatched/hidden packages create zero jobs;
- every File Code Unit has exactly one accepted Story 12.1 deterministic job;
- default capacity is two; 1/2/3/4 work; barrier-controlled Workers prove actual simultaneous execution reaches two and four when enough jobs are eligible, while 0/5/noninteger reject before dispatch;
- authorized versioned 4->1 and 1->4 updates affect only later claims, preserve valid leases, and stop claims while active leases meet/exceed a decreased limit;
- with four Workers held, 4->1 revokes none and launches no replacement until active durable leases fall below one; with one Worker held, 1->4 launches three additional Workers before the first completes;
- current/equal update is a zero-write no-op; exact lost-ack update replay is zero-write; stale version, changed actor/target, hidden ownership, reconciling, and terminal updates reject;
- original Start replay compares the immutable initial limit and returns the current effective limit; it cannot mutate concurrency;
- reversed inputs and at least ten completion schedules keep job/Document/display identities identical;
- pair order uses Pair Selection Ordinal and Repository-local order uses Manifest Ordinal.

## 5. Navigation and strict two-function Agent loop

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~NavigateCodeContractTests|FullyQualifiedName~NavigateCodeSecurityTests|FullyQualifiedName~PreparedPass1StrictSubmissionTests|FullyQualifiedName~Pass1StrictSubmissionSchemaTests|FullyQualifiedName~PreparedPass1JobExecutorStreamingTests"
```

Expected:

- all eight operations return only authorized current Document or exact source evidence;
- current lease/version succeeds; expired/replaced lease, paused/probing circuit, Completed job, terminal pair, and authorization change between analysis and receipt commit disclose no evidence and write no receipt;
- current-Document and source-fallback evidence share stable target/observation identity;
- unavailable and unauthorized shapes are indistinguishable;
- 8 calls, 20 results, 65,536 cumulative bytes, one hop and depth 3 are hard limits with no partial evidence;
- first depth-four, over-results, over-bytes, and accepted-call/tool-attempt-eight exhaustion persist metadata-only receipts/marker that release no evidence but permit a schema-valid `BoundExceeded` deferred observation;
- BoundExceeded receipts cannot support a positive relationship or Communication Kind claim, and no ninth tool attempt occurs;
- claimed-packet hydration loads `ToolAttemptCount` and sets exact `RemainingNavigationBudget = 8 - ToolAttemptCount`; retry/restart never receives default eight unless high-water is zero. Remaining zero is valid, rejects navigation, and still permits a sole submission;
- exactly `navigate_code` and `submit_code_logic` are declared, both strict; navigation is the only evidence access and has eight attempts/calls, while submission has at most two invocations within ten service turns;
- only one outer `document` object `JsonElement.GetRawText()` enters the full local validator; nested duplicate/unknown members survive adapter parsing and reject; parser typed failure maps exhaustively to the four closed categories without exception-message matching;
- the prompt asks for exactly one call and keeps submission sole, but enforcement preflights the complete assistant call set before any callback: a one-through-eight all-declared-`navigate_code` response is legal and serial even when it exceeds current remaining capacity; unknown, mixed, any multi-submit, post-submit, malformed-ID, above-eight, or turn-ineligible responses reject with zero callback/receipt/candidate/later request;
- illegal complete batches reserve nothing. For an eligible all-navigation response, each provider-order call within the remaining-capacity prefix is preceded by one committed lease/version-conditional reservation of the next `ToolAttemptCount` ordinal; every excess sibling receives constant `navigation-budget-exhausted` under its original call ID with no reservation/callback/receipt/evidence/candidate effect, the next sibling uses only the latest cursor returned by reservation or receipt commit, and reservation failure stops before that and later callbacks;
- exact initial zero remaining capacity bypasses no provider work: it enters the normal strict two-function conversation, closes every at-most-eight navigation call with matching no-effect results, and preserves a direct or later sole submission with zero navigation effect;
- map the pinned SDK's zero-based callback value as `providerTurn = FunctionInvocationContext.Iteration + 1`; navigation-capable turns one through eight use generic required selection with both strict declarations, while every finalization-only request (forced turn nine, its optional turn-ten correction, or the request after one earlier rejected first submission) uses the one-element unchanged submit declaration and names `submit_code_logic`; turn nine admits the sole first submission and continues only when it is locally invalid, turn ten admits only that sole second submission and then terminates regardless of validity, any ignored named choice is rejected before callback, any mapped post-ten callback is fail-closed, and no request eleven occurs;
- `Guid.Empty`, local rejection, exception, cancellation, lost response, lease expiry, and simulated crash after reservation leave the ordinal consumed. An ordinary receipt binds only to that reserved ordinal, uses the same receipt ordinal, and never increments attempts twice; missing receipt ordinals carry no evidence;
- reservation eight creates marker nine after the callback's closed outcome; hydration/lease recovery and terminal/reconciliation paths idempotently repair a crash-window missing marker before further provider or terminal visibility. Across normal attempt plus JobTransient retry/restart, released callbacks total at most eight;
- the result-only dispatcher calls the executor once per durable claim and contains no provider-turn/retry/budget `ConversationState`;
- first invalid sole submit allows only one corrected sole submit and disables navigation; valid capture terminates; second invalid, third, later navigation, free text, unknown call, or exhaustion rejects;
- the latest legal eight-navigation-bearing-turn + invalid-submit + valid-submit sequence uses provider turns one through eight for navigation, provider turn nine for the first submission, and provider turn ten for the sole correction; SDK maximum eleven only retains declarations and fake transport proves turn ten is never a first-submission fallback and no request eleven occurs;
- deterministic fully inlined transport schema matches every representable authoritative property/required/closed-object/type/enum/pattern/item/numeric bound; every allowed omission/transform is ledgered and full local validation retains unsupported/unlisted constraints;
- the only local-only conditional rows are `/$defs/communicationEvidence/anyOf` (`DEA6F2555EAD89D35C1E9C0909F676B335F8648B07EA3069C4849785854FDDD9`) and `/$defs/deferredObservation/anyOf` (`EBF64EBB6DDEC0B52D01CA493CE906165E69FF8CBCCA44E7AD3598517CE647A5`), each additionally fixed to `anyOf`, two ordered branches, and its named semantic identity;
- projection omits those complete conditional nodes and emits no vacuous/partial-object approximation; pointer, branch reorder, property/value, kind/count/hash changes and any new unclassified conditional fail projection;
- the unchanged full local validator rejects both-empty communication evidence and both-null deferred targets, while valid one-sided controls pass, regardless of provider strict acceptance;
- every response streams with a five-minute timer since the last successful nonzero raw response read and a 25-minute nonresetting absolute cap. Comment-only SSE bytes reset inactivity without entering assembled content; zero-byte reads, headers, parsed-buffer replay, and local tool work do not. Exactly 16,777,216 raw SSE bytes under the conservative Vulgata calculation 32,768 output tokens x 512 serialized SSE bytes/token and exactly 16,777,216 assembled bytes remain, with unchanged reasoning continuity, zero SDK retries, and no sensitive logging; no byte cap or 512 ratio is claimed as a DeepSeek/SDK guarantee;
- raw SSE is read and counted incrementally without materializing a complete 16 MiB response or retaining a cumulative raw transcript; public execution proves exact 16,777,216 passes and byte 16,777,217 cancels transport/no-sends with sanitized `RawResponse`, the original assembled payload above 131,072 remains legal below raw, and `RawResponse` has precedence when equal raw/assembled caps both would overflow; `AssembledResponse` remains defensive for a decoded/serialized expansion that reaches it first;
- the ten-turn attempt can consume at most 167,772,160 raw transport bytes cumulatively, but the 25-minute cap, zero SDK retries, and per-turn discard keep that exposure bounded and only assembled contributions may enter later requests;
- every finally serialized assistant replay message is <=16,777,216 bytes and all prior assistant messages together are <=16,777,216 bytes across at most nine messages; public multi-turn execution proves complete practical replay and multiple individually legal messages whose aggregate reaches plus one send no later request;
- exact projected-schema/projected-declaration/outgoing policy is 65,536/81,920/17,416,192 serialized bytes; raw public declaration inputs are independently measured before projection. Outgoing math is 131,072 prepared initial + 81,920 projected declarations + 16,777,216 assistant aggregate + 393,216 tool results + 32,768 envelope. A deterministic checked UTF-8 accounting test proves each inclusive cap and plus one; public tests assert every actual body is <=17,416,192 and any reachable final-wire plus one no-sends, without requiring an unconstructible exact 17,416,192/17,416,193 transcript;
- the single `pass1-code-logic-worker.v2.md` resource has exactly one ordered, nonnested terminal preamble marker pair and one terminal common marker pair. The derived LF-only terminal instruction contains `submit_code_logic` and `code-logic/file-v2`, contains no `navigate_code`, shares the existing Prompt Identity, and rejects every missing/duplicate/reordered/nested/empty/UTF-8/CR/literal/bound mutation before source send;
- an execution-local `pass1-terminal-context/1` projection contains exactly system terminal instruction, user exact original packet, and user canonical context with zero through 64 contiguous ordered server-owned results (at most eight on each of at most eight navigation-capable turns) plus null/one closed first-rejection category and its deterministic repair code. Fake wire proves all exact category/code pairs, mismatch rejection before send, zero/one/64 acceptance, 65 rejection, byte-equal packet reuse, exact roles/order, early-correction plus forced-turn-nine/reserved-turn-ten triggers, no historical assistant/function/tool/reasoning/prose/argument/candidate/validator/path/schema-fragment content, unchanged ordinary continuity, existing failure mappings, and zero persistence/model delta;
- immediately before transport, fake wire proves the SDK body is provisionally capped at 17,416,192, duplicate-aware parsed, optionally token-renamed once, independently disabled-thinking-inserted once, and navigation-capable required choice inserted/replaced once or exact finalization-only named-submit choice preserved, then deterministically reserialized and fully remeasured; final wire contains exactly `max_tokens: 32768`, no `max_completion_tokens`, `thinking:{"type":"disabled"}`, string `tool_choice:"required"` with both strict functions on navigation-capable requests, exact `tool_choice:{"type":"function","function":{"name":"submit_code_logic"}}` with only unchanged submit and the projected three messages on finalization-only requests, `stream: true`, no forbidden compatibility/sampling field, and no request eleven, while invalid source/projection shapes, zero-call responses, forbidden controls, or oversize no-send;
- no scheduling, raw protocol/storage, CodeGraphContext, shell, network, unrestricted filesystem, host path, other Repository or stale-revision evidence is reachable.

## 6. Atomic v2 Document commit and replay

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1DocumentCommitTests"
```

Expected:

- one valid job commits one immutable Code Logic Document Version and derived graph;
- every single identity/authorization/path/hash/range/schema/bound/reference/classification/receipt/anchor mutation writes zero graph rows and leaves the job non-Completed;
- exact lost-ack replay returns the same Document Version with zero mutation;
- changed/reordered/renormalized/partial replay conflicts;
- removing, changing, reordering, or duplicating any Source Anchor, Document Section, Index Entry, or direct REFERENCES Edge field/cardinality conflicts or fails integrity with zero writes;
- exact replay after successful local reconciliation independently reconstructs and accepts only the exact direct-plus-reconciled Edge union;
- Version Chain owner remains Repository/path/segment and older immutable version remains preserved.

## 7. Failure circuit, retry, pause, and resume

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1FailurePolicyTests|FullyQualifiedName~Pass1ProviderCircuitTests|FullyQualifiedName~Pass1RecoveryAndResumeApiTests"
```

Expected:

- provider transient permits exactly five ordered 10/30/60/120/300-second nominal windows with deterministic jitter and bounded Retry-After;
- every window performs one fixed source-free Chat Completions request with no tools/Repository/System/Scan/source/context/Document content, zero SDK retry, bounded bytes/timeout, discarded response text, and closed availability classification;
- no new job is claimed while probing;
- fifth failure persists pair/child provider-unavailable pause and `继续扫描`; advancing time or restarting the host performs no sixth/background probe;
- success reopens only remaining valid work;
- isolated JobTransient attempts the same job at most twice across restart;
- 401/402/403 and invalid assignment pause as nonretryable provider configuration; 400/422 pause as nonretryable request/capability rejection without credential wording; 408/429/5xx use the transient circuit path;
- integrity/security stop new dispatch and fail closed;
- restart/manual resume revalidates both child Scans and exact pair, skips completed jobs, recovers only expired/due eligible work, and never duplicates Documents;
- unauthenticated/policy/ownership-filtered API behavior remains 401/403/indistinguishable 404.

## 8. Partial completion and local reconciliation

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1ReconciliationTests|FullyQualifiedName~Pass1FailurePolicyTests"
```

Expected:

- denominator counts prepared File Code Units once, never attempts/probes/replays;
- zero, exact, and one-over `min(5,floor(total/10))` cases have exact outcomes;
- every accepted high-value seed is mandatory regardless of budget;
- clean child is Completed; child with bounded ordinary failures is CompletedWithWarnings;
- same authoritative inputs in ten completion orders yield the same canonically ordered warning inventory and Edge set;
- only exact current same-Repository/same-Scan-Run/revision/hash targets create REFERENCES Edges;
- ambiguous/stale/unsupported/cross-Repository/self/duplicate targets create no Edge;
- reconciliation mutates zero Document Versions and only advances the coordination to Story 12.4's `Resolving` handoff.

## 9. PostgreSQL 17 atomicity and migration

Use a disposable dedicated database; do not point these tests at user data.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1PairPostgreSqlAtomicTests|FullyQualifiedName~Pass1DocumentPostgreSqlAtomicTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1MigrationTests"
```

Expected:

- pair start and Document/job/reconciliation expose full commit or none at every injected boundary;
- competing claims never exceed capacity;
- stale writers, lost acknowledgements, cancellation and forced exceptions leave no partial rows;
- canonical receipts/outcomes/replay reconstruct from PostgreSQL scalar rows rather than trusting digests;
- rejected batch writes no reservation; every released callback has a prior committed ordinal; no-receipt/cancellation/crash reservations survive lease recovery and restart; receipts cannot double-increment `ToolAttemptCount`;
- receipt gaps, marker-nine final snapshot/repair, same-fingerprint replay, conflicting receipt rejection, exact retry hydration, and combined job-wide callback count at most eight are deterministic;
- concurrency-change audit records and exact update replay survive competing claims/restart without changing existing leases or deterministic scan/content identities;
- migration clean apply/rollback/reapply passes;
- historical v1 Documents/anchors/jobs remain readable;
- v1 and v2 schema identities coexist; v2 byte anchors are required by the new path;
- EF reports no pending model changes.
- strict result submission, schema projection, transcript, and capability observation produce zero EF model/migration delta.

## 10. Production-composed Start-to-terminal execution

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1ProductionExecutionTests|FullyQualifiedName~Pass1ProductionProviderOutageTests"
```

Expected:

- the authenticated Start API accepts exactly two approved Prepared Scan Manifests and returns after durable atomic start;
- the production `Program` DI graph resolves the activator/hosted service, claimed-packet source, job executor, dispatcher, explicit function invoker, lease-bound navigation, execution-local submit/session/parser, strict schema projector, committer, unchanged active health request/probe driver, and reconciler;
- without a test calling store methods directly, the pair reaches claims, one Worker job per File Code Unit, immutable canonical Documents, durable ordinary retry/failure policy, deterministic reconciliation, and truthful terminal status;
- barrier-controlled production Workers fail a sequential coordinator by requiring two and four independently scoped runners to be simultaneously inside execution before release; observed concurrency reaches the effective limit when enough jobs are eligible and never exceeds it;
- with four production runners held, 4->1 preserves all leases, stops new launches, drains them, and only refills below the new limit; with one runner held, 1->4 fills three additional slots without waiting for that runner;
- limits 1/2/4, activation replay/deduplication, and at least ten forward/reverse/permuted barrier-release schedules preserve job/Document/Edge/reconciliation/display identity and order;
- a circuit/pause/failure/cancellation transition stops launches immediately, in-flight runners close durably, and reconciliation occurs only after the local runner set is drained plus fresh durable terminal exhaustion;
- fake-time provider outage uses the same production composition, makes exactly five active requests at durable eligibility, stops claims, persists `PausedProviderUnavailable`/`继续扫描`, and never self-reactivates;
- stopping the host after durable Start loses no job/Document state; no dormant pair is discovered automatically; authenticated Resume revalidates and explicitly reactivates the remaining pair.

## 11. Fake-wire and live DeepSeek pre-gates

Run in this exact order:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreparedPass1StrictWireTests|FullyQualifiedName~PreparedPass1JobExecutorStreamingTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1DeepSeekContractQualificationTests"
```

Expected:

- fake wire plus disposable PostgreSQL proves configured endpoint composition without rewrite; immutable/projected declarations; exact prompt/context projection; token/disabled-thinking/capability-appropriate choice/compatibility/sampling policy; streaming/raw activity; exactly two local strict functions; zero-call and whole-response illegal-batch zero effect; durable reserve-before-each-serial-callback; no-receipt/crash consumption; exact hydration; reserved-only receipts and marker repair; combined callbacks <=8; one executor call per claim; full ordinary content/all-call/all-result replay; exact finalization-only three-message history exclusion; status mapping; privacy; and no request eleven. The fake source-free harness evidence remains byte/hash-bound to the same production declaration and terminal-context helpers;
- sibling fake transports prove the body construction hook is absent from every other Chat/Worker client, the QR-002 health request, provider configuration, and host behavior;
- the first live test is a two-request source-free strict-capability gate carrying exact production disabled-thinking/streaming policy. The ordinary request uses required choice/two declarations. The finalization request starts from fixed synthetic historical navigation protocol, runs through the same production Terminal Context Projection with one fixed server-owned result, and uses named submit/one declaration; fake observation proves history absent/result present before send. Both requests observe at least one call and `[DONE]`, the named request transiently verifies the fixed function, neither invokes a returned function or sends a follow-up result, neither carries real source/packet/identity/Document content, and neither causes durable effect;
- handshake evidence contains only sanitized route/model/classification, declaration hashes/sizes/ledger identity, Terminal Context Projection algorithm plus derived terminal-prompt/context hashes/sizes, exact-three-message/history-absent/result-present Booleans, final request hashes/sizes, strict/stream/disabled-thinking/choice/call/done/name-match flags, and timestamps. Every scalar is copied from production helpers except final request identities; no schema, ledger, request, prompt, source, result, tool argument, response, credential, header, or host-path bytes are retained. Missing/inconsistent evidence, projection failure, missing call, or incomplete stream returns `ProviderConfiguration` with empty identities or a closed capability failure and no source send;
- only after handshake success, three isolated representative source-bearing jobs run and all 3/3 must finish through locally valid captured submissions with privacy checks.

The AD-28 correction uses fake transport only until its history-bearing projected source-free live request. Any environment, configuration, endpoint/model, authentication, network, provider, strict-schema, terminal-projection, mandatory-call/`[DONE]`, evidence-identity, or size failure stops here and is reported. Do not modify the host, weaken the schema, rewrite the endpoint, reuse the QR-002 provider health probe, or send further source.

## 12. Full T062 real tool and host qualification

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~NavigateCodeRealToolQualificationTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~NavigateCodeLeaseSecurityCorrectionTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1ProductionBoundaryQualificationTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1HostRuntimeQualificationTests"
```

Expected:

- real CodeGraph/JDT-LS/source operations are revision-bound and sanitized;
- all supported neutral Java operations succeed; XML/source and unsupported combinations return the closed bounded outcome;
- exact pinned runtime identities/hashes are recorded in the evidence artifact, not Documents;
- deterministic typed-host tests make real JDT-LS `Implementations` and exact revision-bound XML source calls, lease-facade tests prove the same closed dispatch, and production-boundary tests prove those concrete adapters are in the real DI graph;
- one authenticated approved two-Repository Pass 1 resolves the production DI graph, runs real DeepSeek Workers, commits Java/XML Documents and Source Anchors, validates every provider-selected receipt against the closed binding/bounds contract, and finishes at concurrency 1/2/4 with identical identities/order/Edges; optional provider operation choice is observational and is never retried until a desired topology appears;
- PostgreSQL 17 is the only required Docker service;
- zero production dependency on CodeGraphContext.
- T085/full T062 runs only after fake wire, source-free handshake, and fresh T084 isolated 3/3 pass; it preserves exactly one durable Worker retry and the unchanged provider circuit.

This is Story 12.3 qualification only. Do not start Cross-Repository Resolution, Pass 2, Business Logic Documents, Chat, evaluation questions, or final release run.

Runtime/version/hash construction without this Start-to-terminal path is supporting evidence only, not host execution proof.

## 13. Security/composition qualification

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1ProductionBoundaryQualificationTests"
```

Expected artifact: `artifacts/qualification/story-12.3/production-boundary-inspection.json`, recording inspected production assemblies/resources, denylist hash, tool-registration inventory, schema/prompt/runtime identities, the complete required DI registration/call-edge inventory from authenticated Start to terminal reconciliation/provider pause, and zero forbidden matches. Test assemblies/fixtures are not production scan targets.

Any match routes to a requirement-backed behavioral regression test; qualification does not edit production to hide it.

## 14. Final verification barrier

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreparedPass1"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~DurableScanning|FullyQualifiedName~PreparedScanning|FullyQualifiedName~KnowledgeStore|FullyQualifiedName~AnalysisRuntime"
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
```

Report focused and full results separately. Report a zero-error build separately from warnings. A known unrelated full-suite baseline failure must be named exactly and must not be presented as Story 12.3 success without the workflow's accepted verification treatment.

Completion evidence must bind the reviewed production/test boundary, migration and PostgreSQL results, real-tool/host evidence, convergence, quality review, E2E decision/result, and the root-owned checkpoint. Clean up only the disposable Story 12.3 database/container; leave existing PostgreSQL/user services untouched.
