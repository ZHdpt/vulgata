# Implementation Plan: Grounded LLM-Wiki Chat & August 30 Release Evidence

**Branch**: `demo0831/story-12-5` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Revised fresh Story 12.5 specification at `specs/014-grounded-llm-wiki-chat-release-evidence/spec.md` (SHA-256 `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4`), authored from source authority `0d835c5f65f70a3c6a9561c6ff600069bb93e832` and inspected at clean committed activation boundary `ec3c68613ea6d9afdbebfb72bb3ef57395fd751e` plus the root/spec-author-owned Story 12.5 definition artifacts.

## Summary

Deliver Story 12.5 as a bounded delta over Prepared Chat rather than a second Chat stack. Preserve the existing authenticated Business/IT page, server-owned two-Repository scope, opaque handles, packet fingerprint, claim/citation validation, Source Anchors, and render-time authorization/currentness barrier. In Track B, after Story 12.4 has an accepted final checkpoint and its final commit is integrated, replace the retriever's embedded-profile/`ExactCrossRepositoryEdgeEvidence` prerequisite with deterministic current `IndexEntry`/`DocumentSection` ranking and a closed one-hop projector over current `DocumentEdge` rows, exact-match `CrossRepositoryResolutionDecision` rows bound to those Edges, `GENERATED_FROM` Edges, and `Published` Flow Groups. Extend the packet to enforce encoded byte bounds and bounded transient conversation. Replace the current non-streaming Chat provider call with one dedicated server-only streaming seam used by both primary synthesis and the single syntax repair: strict incremental UTF-8, 16 KiB per data chunk, 64 KiB total, ten-second data inactivity, exact terminal framing, cancellation, and one 29-second end-to-end deadline. Provider frames remain untrusted and client-invisible until complete syntax, evidence, citation, authorization, currentness, and semantic validation; only then may the existing Unicode-safe UI reveal run.

Track A independently adds a production Scan-status read model and Chinese management UI over existing durable `ScanExecution`/`ScanJobExecution`/manual-resume authorities, plus a fixed question corpus and evidence-manifest contracts. Track C runs Tier 1/2/3 from one pinned candidate, using only uniquely named `vulgata_story125_*` PostgreSQL databases and rejecting fake Backup B state. No persistence shape is required; `VulgataDbContext`, migrations, and `VulgataDbContextModelSnapshot` remain unchanged.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); Razor/Blazor Interactive Server; strict UTF-8 JSON and SHA-256 fingerprints

**Primary Dependencies**: existing EF Core/Npgsql 10.0.9 persistence; ASP.NET Core Identity/authorization; FluentValidation; Microsoft Fluent UI Blazor; Microsoft Agents AI/OpenAI-compatible Chat path; Story 12.1 durable Scan recovery; accepted Story 12.4 v2 exact-resolution/Flow Group projections

**Storage**: existing PostgreSQL 17 `vulgata` and `identity` schemas only; no new table, column, index, or EF migration. Conversation state is scoped in-memory server state and is discarded with the Blazor scope/new-conversation reset.

**Testing**: xUnit; pure contract/ranking/packet tests; SQLite relational doubles where existing harnesses are sufficient; authenticated `WebApplicationFactory<Program>` component/composition/API tests; disposable PostgreSQL integration/restart tests; opt-in Tier 3 real CodeGraph/JDT LS/LLM qualification and browser screenshots

**Target Platform**: documented Windows host for Vulgata, CodeGraph, Java 21, and Eclipse JDT LS; PostgreSQL 17 is the only required Docker service for the release run

**Project Type**: existing multi-project Blazor/ASP.NET Core application with Core contracts, Infrastructure relational projections, Agents provider dispatch/validation, Shared boundary DTOs, Web coordination/Razor, and one mirrored xUnit project

**Performance Goals**: deterministic selection for identical question/scope/mode/conversation/persisted evidence; one to five Documents, one to twenty sections, one to thirty-two Source Anchors, at most thirty-two relationships, at most twelve flow steps, and one relationship hop; primary and repair provider calls stream through one bounded transport; accepted complete render under 30 seconds using a 29-second internal absolute deadline

**Constraints**: explicit authorized System and exactly two Repositories; evidence <=64 KiB encoded UTF-8, conversation <=4 KiB, advisory context <=8 KiB; decoded provider answer delta <=16,384 UTF-8 bytes and concatenated decoded response <=65,536 UTF-8 bytes; separate raw-wire/SSE implementation safety caps; strict wire and final UTF-8; one terminal; ten-second decoded-data inactivity; no client fragment before complete validation; no embeddings/vector search/cross-encoder/raw CodeGraph proximity/autonomous multi-hop/unrestricted Agent tools; whole-answer rejection on any transport or validation failure; raw-adapter-authenticated content-dark observations only; fake Backup B state is release-ineligible

**Scale/Scope**: 12-20 fixed Chat evaluation cases; final candidate covers 50-150 automatically selected File Code Units across exactly two real sample Repositories, at least one exact Cross-Repository REFERENCES Edge, one honest Uncertainty, and at least three Business Logic Documents including one Cross-Repository flow

No clarification remains. Research decisions are closed in [research.md](research.md).

## Constitution Check

### Pre-design gate

| Principle | Plan evidence | Result |
| --- | --- | --- |
| I. Layered Project Boundaries | Core owns provider-independent Chat/Scan read contracts; Infrastructure alone queries EF; Agents owns prompt/transport/result parsing; Shared retains validated caller DTOs; Web owns current-user coordination and Razor. No `DbSet` or `IQueryable` crosses Infrastructure. | PASS |
| II. Domain Purity & Ubiquitous Language | Uses System, Repository, Scan, Document Version, Source Anchor, REFERENCES Edge, GENERATED_FROM Edge, Cross-Repository Resolution, Uncertainty, Flow Group, and Profiled Revision exactly. Existing immutable Document Versions and Edge types are reused. | PASS |
| III. Validation & API Contract Discipline | The existing validated Chat request and manual-resume API remain authoritative. New production Scan UI uses bounded Chinese server read models and missing-equivalent ownership behavior. Any shared registration change is coordinated through existing `Program.cs`; no second endpoint-registration path is introduced. | PASS |
| IV. Test-First Integration Harness | Every delta has a pure or provider-independent seam, including byte-level provider framing/assembly and fake-clock deadline tests, authenticated component/composition coverage, relational currentness coverage, PostgreSQL restart coverage, and a same-candidate release manifest. | PASS |
| V. Security & Data Stewardship | Retrieval and rendering recheck authorization/currentness; provider receives only bounded packet content and opaque handles; provider frames remain untrusted server-side data until complete validation; conversation is context, not evidence; content-dark release artifacts redact secrets/absolute paths; external providers receive only selected evidence. | PASS |

No Constitution exception is requested. The repository contains no `CONTEXT-MAP.md`, `CONTEXT.md`, system ADR directory, or context-scoped ADR at the pinned boundary. The August 30 architecture and dated PRD overlay are therefore the controlling architecture decisions; no ADR/context conflict exists to waive.

### Post-design gate

The completed contracts preserve all five principles. `data-model.md` adds only provider-independent transient/view models; the dedicated provider transport remains internal to Agents. The relational projectors use existing accepted tables and current-pointer invariants, provider frames cannot bypass the existing publication barrier, the UI cannot infer Scan truth from fake state, and the release harness fails before database access when the database name is outside `vulgata_story125_*`. Result: **PASS**.

## Project Structure

### Documentation (this feature)

```text
specs/014-grounded-llm-wiki-chat-release-evidence/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── spec.md
└── contracts/
    ├── chat-evidence-packet-v2.schema.json
    ├── chat-provider-streaming-v1.md
    ├── grounded-chat-v2.md
    ├── production-scan-ui.md
    ├── release-evidence-contract.md
    ├── story125-evaluation-case.schema.json
    └── story125-release-evidence.schema.json
```

`tasks.md` is deliberately not authored by this plan dispatch.

### Existing paths reused unchanged unless a later RED proves a listed modification necessary

```text
src/dotnet/
├── Vulgata.Core/Entities/
│   ├── CrossRepositoryResolutionDecision.cs
│   ├── DocumentEdge.cs
│   ├── DocumentSection.cs
│   ├── IndexEntry.cs
│   ├── FlowGroup.cs / FlowTrace.cs / FlowTraceStep.cs
│   ├── LogicalDocument.cs / DocumentVersion.cs / SourceAnchor.cs
│   ├── ScanExecution.cs / ScanJobExecution.cs / CodeUnit.cs # File Code Unit rows
│   └── ExactCrossRepositoryEdgeEvidence.cs       # legacy readable only; not v2 Chat authority
├── Vulgata.Core/DomainServices/
│   └── DurableScanContracts.cs                   # existing recovery snapshot authority
├── Vulgata.Infrastructure/Data/
│   ├── DurableScanRepository.cs
│   └── PreparedChatScopeResolver.cs              # reuse scope/ownership semantics, remove only legacy profile coupling if required
├── Vulgata.Web/Api/Scans/ManualResumeEndpoints.cs
└── Vulgata.Web/Components/Pages/ChatTurn.cs
```

### Existing paths modified

```text
src/dotnet/
├── Vulgata.Core/DomainServices/
│   ├── ChatContracts.cs                          # packet v2, byte accounting, conversation and v2 relationship bindings
│   ├── IChatAgentDispatcher.cs                  # absolute deadline and content-dark attempt summary
│   ├── IChatEvidenceRetriever.cs                 # server-owned bounded conversation input
│   └── IChatEvidenceCurrentnessValidator.cs      # exact decision/Flow Group bindings
├── Vulgata.Infrastructure/Data/
│   ├── ChatEvidenceRetriever.cs                  # current lexical projection + one-hop expansion
│   ├── ChatSupplementaryEvidenceSelector.cs      # generalized stable section scoring
│   └── ChatEvidenceCurrentnessValidator.cs       # v2 decisions, Edges, Flow Groups, sections/anchors
├── Vulgata.Agents/ChatExecution/
│   ├── ChatAgentDispatcher.cs                    # dedicated provider streaming + shared deadline + repair
│   ├── ChatAgentResultValidator.cs               # whole-result semantic/evidence barrier
│   ├── ChatCandidateExtractor.cs
│   ├── ChatCandidateRanker.cs
│   └── ChatPromptCatalog.cs
├── Vulgata.Agents/DependencyInjection.cs         # shared Agents DI registration; coordinated owner only
├── Vulgata.Agents/Prompts/
│   ├── chat-business.v1.md
│   └── chat-it.v1.md
├── Vulgata.Web/Data/PreparedChatCoordinator.cs    # transient two-turn state and re-fetch hints
├── Vulgata.Web/Components/Pages/
│   ├── ChatPage.razor
│   ├── ChatPage.razor.cs
│   └── ChatTurn.cs                               # accepted-output Unicode reveal + immediate mode
├── Vulgata.Web/Components/Pages/Management/ScanHistoryPage.razor
└── Vulgata.Web/Program.cs                         # shared DI integration; coordinated owner only
```

`src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs`, all project files, central package files, and other registration files are shared integration paths. This plan does not require changing them. If implementation discovers a registration or project-file delta, the root must assign one integration owner before the write.

### New production paths

```text
src/dotnet/
├── Vulgata.Core/DomainServices/
│   ├── ChatConversationContracts.cs
│   ├── ChatStreamingContracts.cs                # absolute deadline + content-dark attempt summary
│   └── ProductionScanStatusContracts.cs
├── Vulgata.Agents/ChatExecution/
│   ├── IChatProviderStreamingTransport.cs
│   ├── ChatProviderStreamAssembler.cs
│   ├── ChatStreamObservationAuthority.cs
│   └── OpenAiCompatibleChatProviderStreamingTransport.cs
├── Vulgata.Infrastructure/Data/
│   ├── ChatEvidencePacketBuilder.cs
│   ├── ChatRelationshipEvidenceProjector.cs
│   └── ProductionScanStatusReader.cs
└── Vulgata.Web/
    ├── Data/ProductionScanStatusCoordinator.cs
    └── Components/Pages/Management/
        ├── ScanExecutionPage.razor
        └── ScanExecutionPage.razor.css
```

### Existing tests retained and modified as regression evidence

```text
tests/Vulgata.Tests/
├── ChatContractTests.cs
├── ChatEvidenceRetrievalTests.cs
├── ChatEvidenceCurrentnessTests.cs
├── ChatSupplementaryEvidenceSelectorTests.cs
├── ChatCandidateRankerTests.cs
├── ChatAgentDispatcherTests.cs
├── ChatAgentResultValidatorTests.cs
├── PreparedChatCoordinatorTests.cs
├── PreparedChatIntegrationTests.cs
├── ChatPageTests.cs
└── DurableScanning/
    ├── ManualResumeApiTests.cs
    └── DurableScanRestartPostgreSqlTests.cs
```

### New test, fixture, and evidence paths

```text
tests/Vulgata.Tests/
├── ChatEvidencePacketBuilderTests.cs
├── ChatConversationStateTests.cs
├── ChatProviderStreamAssemblerTests.cs
├── OpenAiCompatibleChatProviderStreamingTransportTests.cs
├── ChatStreamObservationProvenanceTests.cs
├── ChatValidatedRevealTests.cs
├── ProductionScanStatusReaderTests.cs
├── ProductionScanPageTests.cs
└── ReleaseGate/Story125/
    ├── Story125EvaluationContractTests.cs
    ├── Story125ReleaseHarnessTests.cs
    ├── Story125Tier2PostgreSqlTests.cs
    ├── Story125Tier3QualificationTests.cs
    ├── Story125ReleaseHarness.cs
    └── Fixtures/story125-question-corpus.json

specs/014-grounded-llm-wiki-chat-release-evidence/evidence/
├── tier-1/
├── tier-2/
└── tier-3/
```

Evidence directories are created only by later authorized execution. Track A may add schemas/templates and the fixed corpus; Track C alone records observed Tier 3 results/screenshots.

**Structure Decision**: deepen the current interfaces and composition rather than introduce a generalized RAG or new persistence layer. Split deterministic ranking, relationship projection, packet packing, and Scan-status reading into testable Infrastructure modules; keep final user/access ordering in the existing Web coordinator; keep provider transport and result repair in Agents.

## Repository Fit and Existing-Implementation Disposition

| Disposition | Concrete source evidence | Planned treatment |
| --- | --- | --- |
| Already implemented | `ChatPage.razor(.cs/.css/.js)`, `PreparedChatCoordinator`, `PreparedChatQuestionRequest`, authentication and explicit Business/IT + System/two-Repository selection | Preserve UI and request boundary; add transient follow-up continuity, not saved sessions or auto-scope. |
| Already implemented | `EvidencePacket`, opaque handles, SHA-256 fingerprint, 1-5 Documents, 1-20 sections, 1-32 anchors, <=32 relationships, <=12 flow steps | Version the packet contract and add encoded byte/advisory/conversation limits; retain handle/canonicalization rules. |
| Already implemented | `ChatAgentResultValidator`, `ChatEvidenceCurrentnessValidator`, `PreparedChatCoordinator.CompleteGroundedAnswerAsync` | Preserve whole-answer validation and post-provider currentness/authorization ordering; adapt relationship authority. |
| Already implemented | `ChatTurn`/`ChatClaimReveal` enumerate Unicode text elements and `ChatPage` already has reduced-motion/immediate presentation behavior | Preserve as post-validation presentation only; characterize that no provider frame can reach it and force immediate complete render when the shared deadline is too close. |
| Already implemented | `ChatSupplementaryEvidenceSelector` and `ChatCandidateRanker` | Generalize deterministic lexical scoring to all eligible current projections and mode rules. |
| Already implemented | `ManualResumeEndpoints`, `IDurableScanStore.ReadRecoverySnapshotAsync`, durable retry/pause/restart state | Reuse as operational authority; add a bounded read projection and real UI. |
| Partially implemented | `ChatEvidenceRetriever` starts with one embedded profile occurrence, one legacy exact-edge row, attached provenance, then up to two supplementary Code Logic sections | Replace the start authority in Track B; retain access/currentness transaction and packet materialization patterns. |
| Partially implemented | `FlowGroup` rows are read when a Business Logic version exists, but the retriever does not require v2 `State == Published`, exact decision binding, or accepted Profiled Revisions | Require published current v2 group/trace/decision/Edge consistency before expansion or claim support. |
| Partially implemented | `ChatAgentDispatcher` calls `agent.RunAsync` with a fresh 30-second provider timeout and parses only after the non-streaming response | Replace only this provider-call seam in Track B. Primary and repair must use a dedicated server-only stream, one coordinator-created 29-second absolute deadline, strict framing/UTF-8/bounds, and the existing complete-result validators. |
| Partially implemented | Pass 1 uses `RunStreamingAsync` plus inactivity helpers and Pass 2 uses a raw SSE transport with byte accounting | Reuse as source evidence for cancellation/stream plumbing only. Their keepalive liveness, terminal, aggregation, and large-size rules are not Story 12.5 authority and those paths remain unchanged. |
| Partially implemented | `ChatPage` renders multiple turns, but every `AskAsync` is stateless and earlier handles are not re-fetched as hints | Keep at most two prior validated turns server-side; rebuild every packet from current rows. |
| Obsolete | `ExactCrossRepositoryEdgeEvidence`, `ExactResolutionProfileCatalog`, and one prepared occurrence as final retrieval authority | Leave legacy tables/classes readable for old tests; do not join or accept them as Story 12.5 v2 evidence. |
| Obsolete | `specs/008-demo-chat-release-gate` lifecycle and `Story115ReleaseHarness` August 15 identity | Reuse only security/cleanup/polling patterns; create Story 12.5 contracts, names, topology, and evidence. |
| Obsolete | `DemoScanPage`/`DemoScanUiState` as proof | Keep isolated development behavior; exclude route/state/screenshots from all accepted evidence. |
| Missing | current projection-first retrieval, v2 exact-decision validation, closed one-hop expansion, byte packing, bounded conversation, dedicated Chat provider streaming, strict assembly, and streamed syntax repair | Implement only in gated Track B. |
| Missing | production Scan status/read UI, fixed corpus, evidence manifest | Implement/prepare in Track A after implementation approval. |
| Missing | same-candidate Tier 1/2/3 observations, restart and screenshots | Execute in Track C after accepted Track B and candidate pin. |

## Architecture Decisions

### AD-1: Preserve Prepared Chat's orchestration and security barrier

Keep `PreparedChatCoordinator` as the sole answer orchestrator: validate caller input, create one absolute answer deadline, resolve current user and selected scope, check access, retrieve one closed packet, dispatch one bounded streamed synthesis attempt (plus the allowed streamed syntax repair), assemble and validate the complete result, revalidate currentness/authorization, then publish one complete answer. Existing provider failure and missing-equivalent outcomes remain bounded Chinese messages. No provider frame, claim, citation, handle, relationship, or Source Anchor detail reaches the client before complete validation.

**Alternative**: replace Prepared Chat with a general Chat Agent tool loop. Rejected because it duplicates accepted security behavior, violates the dated bounded-server-retrieval architecture, and creates unrestricted evidence access.

**Consequence**: most existing coordinator/page tests remain regression evidence; Track B changes inputs and packet authority rather than user-facing orchestration identity.

### AD-2: Start retrieval from current authorized sections, not a prepared exact Edge

Within one consistent relational read, load only current Document Versions for the selected System/two Repositories, their `IndexEntry` and `DocumentSection` projections, and safe section-owned Source Anchors. `ChatSupplementaryEvidenceSelector` becomes the deterministic section ranker. It extracts exact symbols/endpoints/Repository names/paths, normalized ASCII tokens, CJK bigrams, and mode terms from the question plus bounded conversation context. Exact identifier/path/title/heading matches outrank content matches. Stable ties are resolved by score class, mode preference, Repository ID, Logical Document ID, section order, and stable section ID.

Take the strongest eligible section from each selected Document before extras; select <=5 Documents, approximately 12 primary sections, <=20 total, and <=4 per Document. A bounded fallback uses current Business Logic summaries and advisory context terms; otherwise return insufficient evidence.

**Alternative**: keep the legacy exact Edge as the mandatory seed. Rejected because it makes unrelated or non-flow questions impossible and treats stale prepared-profile metadata as current v2 authority.

### AD-3: Use accepted v2 decisions plus current Edges as exact Cross-Repository authority

Track B reads `CrossRepositoryResolutionDecision` only when the integrated Story 12.4 contract marks an exact match, binds one non-null `DocumentEdgeId`, and the Edge is a current directed `References` Edge between current selected-scope Document Versions. Revalidate the decision's System, consumer/provider Repository, Profiled Revision, decision/consumer/provider evidence fingerprints, exact Edge direction, and current endpoints. The legacy `ExactCrossRepositoryEdgeEvidence` row is never sufficient and is not required.

If the final integrated Story 12.4 names/states differ from the candidate contract, adapt Story 12.5 to the accepted shape without changing Story 12.4. If no accepted shape can express these invariants, Track B is blocked and escalates to the Story 12.4 authority.

**Alternative**: backfill or translate legacy exact-evidence rows into v2 Chat truth. Rejected because endpoint/entry multiplicity and accepted evidence cannot be reconstructed safely.

### AD-4: Closed one-hop expansion with independent relationship proofs

Expand only from the top two or three primary Documents. For each seed, consider current directed `REFERENCES` and `GENERATED_FROM` Edges, accepted exact v2 decision/Edge pairs for Cross-Repository REFERENCES, and `Published` Flow Groups whose bound Business Logic Document Version, exact decision, exact Edge, Flow Trace, steps, and Profiled Revisions remain current and internally consistent. Add at most two related Documents per seed and never exceed five Documents, thirty-two relationships, twelve ordered flow steps, or one hop. Canonical ordering precedes every truncation; duplicate/cyclic/back-to-seed candidates are rejected.

Business Mode prefers Business Logic Documents and can traverse GENERATED_FROM to supporting Code Logic Documents. IT Mode prefers Code Logic Documents, Source Anchors, and REFERENCES. A relationship-required Document/section/anchor is all-or-nothing; it is never partly truncated.

**Alternative**: recursive SQL traversal or raw CodeGraph proximity. Rejected because Story 12.5 authorizes one persisted hop only and raw analysis relationships are not publication truth.

### AD-5: Versioned packet builder owns all cardinality and byte invariants

Create `ChatEvidencePacketBuilder` to assign opaque handles after canonical selection, pack complete evidence units, and compute UTF-8 sizes before constructing `EvidencePacket` v2. The evidence body is <=65,536 bytes, advisory context <=8,192 bytes, and conversation <=4,096 bytes. When a cap is approached, drop the lowest-ranked optional whole section first; never truncate text, required relationship support, or Source Anchors. A canonical SHA-256 binds schema, request/scope/mode, ordered evidence, relationship authorities, advisory context, and conversation state.

**Alternative**: rely only on item counts or string character counts. Rejected because provider-visible UTF-8 size is the safety/performance boundary and Chinese content is multibyte.

### AD-6: Conversation is bounded transient context, never durable evidence

`PreparedChatCoordinator` is already scoped; it retains a provider-independent `ChatConversationState` containing at most two prior normalized questions, two complete validated answers, the prior packet fingerprint, cited opaque handles plus stable Logical Document identities for deterministic boost, and the selected System/repository/mode binding. A new conversation, mode change, scope change, authorization failure, or disposed Blazor scope clears it. It is never written to EF, logs, browser storage, or release fixtures.

Each follow-up passes only the bounded state to ranking and synthesis. The retriever re-fetches current Documents, sections, Source Anchors, decisions, Edges, and Flow Groups; prior stable Document identities receive only a small fixed tie-break-independent score boost after current eligibility. A prior answer/handle absent from the new packet is context only and cannot support a claim.

**Alternative**: persist Chat sessions or trust prior answer citations. Rejected because saved sessions are out of scope and either option weakens currentness/authorization.

### AD-7: Dedicated strict provider stream for primary and repair under one deadline

`PreparedChatCoordinator` creates one monotonic absolute deadline with an internal maximum of 29,000 ms when it accepts a question. Retrieval, primary streaming, optional repair streaming, final UTF-8 flush, parsing, evidence/citation semantics, re-fetch/currentness/authorization, and publication consume the same budget. The provider-independent dispatch contract carries that deadline; neither attempt creates a fresh provider timeout. Repair requires at least 3,000 ms remaining.

Add the internal Agents-only `IChatProviderStreamingTransport`, `OpenAiCompatibleChatProviderStreamingTransport`, and pure `ChatProviderStreamAssembler` described by [chat-provider-streaming-v1.md](contracts/chat-provider-streaming-v1.md). The implementable order is raw response-header body reads -> strict incremental wire UTF-8 -> SSE fields/events across arbitrary reads -> JSON event/escape decoding -> strict UTF-8 encoding of each decoded answer delta -> final concatenated Grounded Answer assembly. FR-054's Provider Chunk is one decoded answer delta after JSON unescape/re-encoding (<=16,384 bytes); the complete response is concatenated decoded answer bytes (<=65,536). Raw reads (16,384-byte requested buffer), raw body (1,048,576 bytes), and SSE line/event (131,072 wire bytes) have separate implementation safety caps and counters. JSON escapes are counted after unescape for acceptance, while their lexical bytes count only toward raw/SSE safety caps.

The raw adapter owns response status/media/protocol, physical reads/EOF, disconnect/cancellation, and raw-body overflow; the wire decoder owns wire UTF-8; the SSE parser owns field/event grammar/caps; the JSON decoder owns JSON/escape/provider-event classification; the delta encoder/assembler own 16 KiB/64 KiB acceptance, terminal/post-terminal, and final answer. Ten-second inactivity resets only on a valid non-empty decoded answer delta or `[DONE]`, never on physical reads, comments, empty/metadata/finish events. `[DONE]` is the sole success terminal; finish/error metadata must be consistent. EOF before terminal fails; after terminal the adapter reads to physical EOF and rejects duplicate/contradictory terminal or non-empty data/error.

Exactly one logical terminal is mandatory. The adapter reads through physical EOF and rejects missing/duplicate/contradictory terminal state and non-empty post-terminal data. Cancellation, disconnect, inactivity, overall deadline, either size overflow, invalid UTF-8, terminal violation, provider error, or later validation failure disposes the read, discards/zeroes the buffer best effort, and publishes nothing. The current Pass 1/Pass 2 streaming code is evidence for plumbing only: its keepalive, terminal, aggregation, and size behavior is not composed into this contract.

Only a complete primary response that fails a closed local syntax category may invoke one streamed repair. It reuses the exact packet/fingerprint/deadline and receives the invalid bounded response plus category; no retrieval runs between attempts. There is no third call. Semantic/evidence/citation/currentness/authorization failure, unknown handles, unsupported claims, expired budget, timeout, or cancellation never repairs. The existing deterministic grounded rescue may remain as a bounded operational outcome but cannot satisfy a provider-streamed Tier 3 answer receipt.

**Alternatives**: (1) retain `agent.RunAsync` or use an SDK aggregate pretending to stream—rejected because receipt-level framing, inactivity, terminal, and raw-byte rules cannot be proven; (2) reuse the Pass 2 transport—rejected because it stops at `[DONE]`, has different liveness and size semantics, and cannot detect post-terminal data; (3) reset 30 seconds per attempt—rejected because it violates the end-to-end target; (4) repair semantics—rejected because it can invent evidence.

### AD-7B: Provider streaming and validated UI reveal are separate state machines

Provider frames remain inside Agents/Web server orchestration. `ChatPage` exposes only its generic pending/cancellable state until the coordinator has a fully assembled, syntax-valid, packet-bound, citation-valid, semantically valid, current, authorized answer. After that barrier, existing `ChatClaimReveal` may reveal the accepted complete claims by Unicode text element. Reduced motion or insufficient remaining shared deadline renders the already accepted answer immediately; reveal never extends the request beyond 29 seconds.

**Alternative**: forward provider deltas or syntactically complete partial claims to Blazor. Rejected because later bytes can invalidate syntax, terminal integrity, citations, currentness, or authorization and would create an irreversible disclosure.

### AD-8: Real Scan UI uses a bounded read projector over existing durable state

Add `IProductionScanStatusReader` in Core and `ProductionScanStatusReader` in Infrastructure. It materializes one ownership-filtered view from current `ScanExecution`, its bounded `ScanJobExecution` rows, safe `FileCodeUnit` relative paths, and relevant Pass 2/coordination terminal state where the accepted integrated model requires it. It maps preparing/indexing, running, retrying, paused, resumed/prepared, completed, completed-with-warnings, integrity/security failure, and cancellation to a closed status contract with counts, earliest next retry, bounded machine-derived Chinese reason, and at most twenty failed-file summaries.

`ProductionScanStatusCoordinator` resolves the current authenticated ManagementAccess user and visible System/Repository ownership, returns missing-equivalent on hidden/missing state, and invokes the existing durable recovery service for `继续扫描`. `ScanHistoryPage` lists current real executions; `ScanExecutionPage` renders and refreshes the bounded view. Neither reads `DemoScanUiState` nor uses the demo iframe.

**Alternative**: adapt `DemoScanPage` or derive state client-side. Rejected because fake circuit state is not durable evidence and client mapping could invent progress.

### AD-9: Fixed source-traceable evaluation corpus, not desired-answer fixtures

Track A creates exactly one schema-validated 12-20 case corpus. Every entry records stable ID, Business/IT mode, authorized System/two-Repository selector by release alias, question, follow-up parent where applicable, expected evidence themes/relationship kinds, usefulness rubric, citation/authorization expectation, and timing/result fields. It does not embed provider prose as ground truth. Authorization-negative disclosure is an automatic fail.

**Alternative**: free-form demo questions chosen after seeing results. Rejected because it permits cherry-picking and cannot establish repeatability.

### AD-10: One closed same-candidate release manifest across Tier 1/2/3

The Story 12.5 harness emits an immutable, schema-validated run envelope for every attempt. Its `status` and discriminated result distinguish `planned`, `attempted`, `passed`, `failed`, `blocked`, and `not-run`; only `passed` may contain the non-null release receipt and success metrics. Attempted/failed/blocked records preserve attempted observations, bounded reason/evidence where terminal, and the commands actually run. A correction creates a new `runId`, links `supersedesRunId`, and never overwrites the prior attempt.

Every executed command records stable ID, redacted exact command, its SHA-256, exit code, UTC timing, and hashed output artifacts. Executed evidence binds the candidate commit, integrated Story 12.4 final commit, disposable database, Repository revisions, prompt/packet/stream/schema/tool versions, and exact provider API/model plus non-secret configuration identity/fingerprint. The production raw response-header adapter is the sole stream-observation authority. At process start an ephemeral ECDSA P-256 authority binds candidate/process identity, exact adapter type/version/assembly hash, and a public-key fingerprint; every content-dark attempt receipt is signed in that process and additionally binds case/request, provider config, canonical `stream=true` request fingerprint, response status/media/protocol, first physical read, and physical EOF disposition. A passed receipt additionally requires those authenticated packet/result/stream artifacts and explicit query/observation receipts for Scan counts, exact Cross-Repository Edge, honest Uncertainty, Business Logic Documents, Cross-Repository flow, restart duplicate checks, and database preflight/ownership/cleanup.

The semantic validator first verifies every referenced artifact hash, raw-adapter signature, same-process public-key preflight, exact production DI type/version/assembly hash, canonical streaming-request fingerprint, and candidate/case/request/provider/header/read/EOF binding, then parses the named receipt artifacts and recomputes every derived aggregate. A perfectly consistent aggregate/SDK fake-chunk observation fails solely when authorized raw-adapter provenance is absent. It rejects any mismatch between a manifest value and its source artifact, including Scan counts, ordinary-failure budget usage, Chat case/usefulness/citation/leakage/timing totals, packet fingerprints, raw-wire/SSE/delta/final counters, streamed attempt outcomes, restart duplicate/data-loss observations, and database ownership/access counts. Required inequalities and coverage are `ordinaryFailureCount <= ordinaryFailureBudget`, `usefulCount <= caseCount`, `corpusResultCount = caseCount`, and exactly one result receipt for every executed fixed-corpus case. Invoked cases have exactly one primary stream and, only when marked, one syntax-repair stream; non-invoked insufficient/authorization outcomes have no packet or stream. Accepted provider answers require a completed terminal, zero non-streaming accepted answers, total duration 0-29,999 ms, and an authenticated content-dark raw-adapter observation artifact. `usefulPercent` is computed with exact nonnegative arithmetic as `floor((10000 * usefulCount / caseCount) + 0.5) / 100` (round half up to two decimal percentage points) and must equal the recorded two-decimal value and be at least 80.00. The fixed-corpus ID set, result-row ID set, and case-receipt ID set agree exactly; ordered packet fingerprints equal invoked cases in corpus order. No omitted, duplicate, foreign-candidate, additional, content-bearing, unsigned, wrong-adapter, wrong-process, or fake-chunk stream receipt is accepted.

A passed envelope has exactly these ten mandatory checks, each once, with the fixed tier: Tier 1 `tier1-contract-ranking-relationship`, `tier1-conversation-synthesis-security`, `tier1-production-ui-corpus-evidence`, `tier1-build-full-tests`; Tier 2 `tier2-postgresql-restart-resume`; Tier 3 `tier3-candidate-tool-database-preflight`, `tier3-real-scan`, `tier3-grounded-chat-corpus`, `tier3-restart-continuity`, `tier3-screenshot-release-integrity`. JSON Schema closes ID/tier/status/cardinality; semantic validation requires every check command ID to resolve to a zero-exit same-envelope command and every check artifact to equal a hashed same-envelope command output or attempted observation. Omission, duplication, wrong tier, non-passed state, dangling binding, or cross-candidate artifact rejects the passed envelope.

Screenshots are passed-only and require at least one candidate-bound, hashed, redaction-passed production-state view in each category: real Scan state, grounded Business answer, grounded IT answer, restart result, and release summary. Duplicate views cannot substitute for a missing category. No screenshot may contain fake-state markers, secrets, database connections, credentials, absolute host paths, another candidate, or unrelated output.

**Alternative**: prose-only release notes or reusing Story 11.5 receipts. Rejected because they cannot prove candidate identity, real topology, or absence of fake state.

### AD-11: Database guard fails before product access

Every Tier 2/3 entry validates a generated unique database name against the exact `vulgata_story125_` prefix and a lowercase random suffix, then independently rejects the configured development database and Story 12.4 qualification database identities before opening a product connection. The harness creates, migrates, uses, and cleans only its proven-owned database. Ambiguous create/drop behavior follows the existing Story 11.5 ownership-proof pattern but uses Story 12.5 names and host-run topology.

**Alternative**: allow any operator-supplied connection string with a warning. Rejected because one typo could mutate another story's qualification or development evidence.

### AD-12: No migration and no Story 12.4 contract mutation

All required authority is already represented by current Documents/sections/indexes/anchors, v2 decisions/Edges/Flow Groups, durable Scan execution/jobs, and existing manual resume. New models are view/transient contracts. `VulgataDbContext`, configurations, migrations, and snapshot remain excluded.

If implementation discovers a missing persisted fact, it must first prove why it cannot be derived from the accepted Story 12.4/Scan authorities, stop Track B/A as applicable, and return for a revised plan plus explicit ownership coordination. It must not modify an unaccepted Story 12.4 contract.

## Track, Dependency, and Ownership Matrix

| Track | Entry gate | Owned work | Shared/blocked seams | Exit evidence |
| --- | --- | --- | --- | --- |
| A: dependency-independent | accepted definition + mandatory implementation approval | production Scan status contracts/reader/coordinator/pages; corpus/schema; evidence manifest/schema; Tier 1/2 harness preparation; characterization of existing post-validation Unicode/reduced-motion reveal | may test that generic pending UI exposes no fragments, but cannot add provider streaming or count fake streaming; `Program.cs` DI is shared and gets one coordinated integration owner; no Story 12.4 path/database/worktree | focused RED/GREEN, authenticated UI states, fake-state exclusion, corpus/schema/stream-receipt validation, database-name preflight |
| B: dependency-sensitive | Story 12.4 accepted final checkpoint + final commit integrated into this branch + implementation approval still valid for accepted plan | current lexical retrieval; v2 decision/Edge/Flow Group projection; packet v2; conversation; dedicated primary/repair provider stream; strict assembler; shared deadline; currentness; Chat UI follow-ups/reveal barrier | accepted Story 12.4 entities/configurations are read-only authority; Agents `DependencyInjection.cs`, Web `Program.cs`, project/central registration files have one integration owner; no B work if contract mismatch requires dependency change | deterministic relational/pure tests, authorization/currentness barriers, packet/byte bounds, stream/framing/deadline/cancellation proof, one-hop proof, follow-up re-fetch, one streamed-repair proof, zero client fragments |
| C: final verification | A and B accepted; one candidate commit pinned; Tier 1 green; disposable DB/tool/provider preflight green | Tier 2 restart run; Tier 3 real repositories/tools/streamed LLM/corpus/screenshots/evidence manifest | no product design or production-code correction inside an evidence run; failed run returns to normal TDD/review then pins a new candidate | same-candidate manifest, streamed primary/repair receipts, zero non-streaming accepted answers, 80% useful, 100% accepted technical-claim citation, zero leakage, <30s accepted answers, restart and screenshots |

Track A and B must use disjoint file ownership during parallel implementation. Track A owns production Scan UI/query and release fixtures/harness. Track B owns Chat contracts/retrieval/Agents/coordinator/page. Only the root-assigned integration owner edits `Program.cs` and any unexpectedly required shared registration/project file. No worker touches `VulgataDbContext`, migrations, model snapshot, Story 12.4 worktree/files/temp directories/database, Git index, or history.

## Failure and State Behavior

| Condition | Closed outcome | Mutation/evidence boundary |
| --- | --- | --- |
| selected scope missing/unauthorized/revoked | missing-equivalent Chinese result | no identity, Document, Edge, citation, Scan state, or reason disclosure |
| no useful current section after bounded fallback | insufficient evidence | no provider call and no fabricated answer |
| v2 exact decision missing/non-exact/stale/direction mismatch/no current Edge | candidate excluded; required support causes whole packet refusal | legacy exact-evidence row cannot rescue it |
| Flow Group incomplete/unpublished/stale/wrong revision/trace mismatch | excluded; required support causes refusal | no flow claim or step enters packet |
| one-hop candidate creates cycle/duplicate/sixth Document/+1 bound | candidate excluded canonically | packet remains within hard bounds |
| packet byte cap cannot retain complete required support | whole packet unavailable | no partial evidence or truncated support |
| prior conversation evidence is stale/unauthorized/absent | no boost/evidence; missing-equivalent if scope revoked | prior prose/handle never proves a claim |
| syntactically malformed provider output with budget | one syntax-only repair | same packet/fingerprint; no evidence addition |
| one post-JSON-unescape strict-UTF-8 Provider Chunk >16,384 bytes (`chunk-overflow`) or concatenated decoded Provider Chunk bytes >65,536 (`total-overflow`) | reject whole stream | acceptance counters are decoded-delta/final bytes; raw-body and SSE safety-cap failures remain separate; discard buffer; no repair/render |
| malformed/overlong/surrogate/truncated UTF-8 | reject whole stream | strict incremental decoder; no replacement text or repair/render |
| ten seconds without non-empty data/terminal | inactivity rejection | keepalive/comment/metadata cannot reset clock; dispose stream |
| provider disconnect, missing/duplicate/contradictory terminal, or data after terminal | framing rejection | read through physical EOF; discard buffer; content-dark category only |
| caller cancellation, Blazor disconnect, or absolute deadline | cancelled/timed-out rejection | cancellation remains effective through validation/publication; no fragment |
| second syntax failure, semantic failure, unknown handle | reject whole answer | no partial render and no further call |
| accepted complete result, reduced motion, or insufficient reveal time | immediate complete validated render | provider streaming stays server-only; UI reveal never extends deadline |
| Scan hidden/missing | identical not-found UI state | no protected status/reason/count disclosure |
| Scan retry/pause | show bounded persisted status/count/earliest retry/reason | no inferred progress; resume only through authorized durable service |
| completed with warnings | visible warning plus <=20 safe failed-file summaries | never reported as full success |
| fake Backup B route/state/screenshot | release evidence rejection | development state remains isolated |
| invalid Tier 2/3 database name/identity | preflight failure | no product database connection, migration, read, or write |
| evidence run failure/block | immutable `failed`/`blocked` envelope with attempted observations, bounded reason and artifacts; `releaseReceipt: null` | no fabricated success metrics, no selective replacement; correction uses a new `runId` linked by `supersedesRunId` |

## Migration and Compatibility Decision

**Decision**: no EF migration. Do not edit `VulgataDbContext`, any configuration, any migration, or `VulgataDbContextModelSnapshot`.

Legacy `ExactCrossRepositoryEdgeEvidence` and prepared-profile behavior remain readable for Story 11.5 regression tests but are not accepted Story 12.5 relationship authority. Packet v1 tests remain characterization; production composition moves to packet v2 only after Track B. Existing manual-resume API behavior and fake Backup B state remain compatible but separately named and release-ineligible. Conversation is ephemeral, so restart intentionally discards conversational continuity while preserving wiki/Chat capability and persisted Documents.

## Test and Qualification Strategy

1. **Tier 1 / contracts and ranking**: independently constructed packet-v2 fingerprints; exact/+1 counts and UTF-8 byte bounds; complete-unit dropping; symbols/endpoints/paths/titles/headings above prose; CJK/ASCII/mode terms; strongest-per-Document before extras; canonical ties across randomized input order; insufficient fallback; no vector/raw graph path.
2. **Tier 1 / v2 relationships**: exact decision + current directed Edge accepted; legacy-only evidence rejected; non-exact/missing Edge/wrong direction/stale current pointer/wrong System or Repository/revision/fingerprint rejected; current REFERENCES/GENERATED_FROM; top 2-3 seeds, two related per seed, one hop, cycle/duplicate and every +1 bound; only complete `Published` Flow Groups and current ordered steps.
3. **Tier 1 / conversation, streaming, and synthesis**: initial + two follow-ups, fourth-turn eviction, scope/mode/new-conversation reset, prior-citation boost without evidence carryover, re-fetch/currentness mutation barriers, and 4 KiB cap. Split raw wire UTF-8 scalars, CR/LF/CRLF, SSE fields/events, JSON tokens/escapes/surrogate pairs, decoded deltas, finish metadata, terminal, and post-terminal reads at every boundary. Exact/+1 vectors independently cover the 16,384-byte read buffer, 1,048,576-byte raw-body safety cap, 131,072-byte SSE line/event safety caps, 16,384-byte decoded Provider Chunk acceptance cap, and 65,536-byte final-answer acceptance cap. Escape-heavy vectors prove lexical `\u4e2d` counts six raw bytes but three decoded-delta bytes. Fake-clock tests prove only a non-empty decoded delta or `[DONE]` resets inactivity and every stage has a closed failure owner. Both primary and repair share the 29-second deadline/fingerprint/raw adapter; semantic failure cannot repair. Mocked browser tests prove zero pre-acceptance output and Unicode reveal only afterward.
   A cross-artifact consistency vector extracts the byte-domain definitions and closed transport/rejection/validation vocabulary from this plan, `data-model.md`, `chat-provider-streaming-v1.md`, `release-evidence-contract.md`, and schema 2.1; it fails on any missing term, synonym, different cap/domain, or reintroduced before-decoding acceptance rule.
4. **Tier 1 / security and UI**: authenticated and missing-equivalent access; Business/IT content/citation presentation; real production Scan mappings for every closed state; counts/earliest retry/bounded reason/failed-file summary; `继续扫描` authorization and durable outcome; fake state never enters status reader or evidence.
5. **Tier 1 / fixtures and evidence**: 12-20 corpus cases, required fields, follow-up graph acyclic/bounded, no desired answer, negative automatic fail, and database-prefix guard before connection. Schema vectors preserve honest status conditioning and append-only correction. Stream-receipt vectors require raw-adapter observation authority, candidate/case/request/process binding, exact adapter type/version/assembly hash, provider API/config fingerprint, canonical `stream=true` request fingerprint, response-header status/media/protocol, first physical-read/EOF disposition, authenticated signature/artifact, one primary and at most one ordered repair, exact per-domain counters, zero non-streaming accepted answers, and content-dark fields. A fixed production-composition test proves DI resolves the exact raw adapter. Reject altered/foreign/unsigned provenance and a perfectly aggregate-consistent SDK fake-chunk receipt solely because it lacks authorized raw-adapter provenance. Passed-envelope vectors retain all ten mandatory checks and same-envelope command/artifact rules.
6. **Tier 2 / local integration**: create one clean `vulgata_story125_*` PostgreSQL database; apply current migrations; use neutral Java/XML repositories and real Git/CodeGraph/JDT LS with controlled provider; force provider outage, persist pause/retry, stop and restart the Vulgata process, restore provider, invoke manual resume, and prove no duplicate Documents/Edges/publication. Record redacted exact commands, command hashes, exit codes, durations, versions, provider model/non-secret configuration identity, attempted observations, and database ownership receipts.
7. **Tier 3 / release candidate**: pin one candidate and accepted Story 12.4 commit; host-run Vulgata with PostgreSQL 17 as the sole Docker service; scan exactly two real sample Repositories and 50-150 automatic File Code Units; require all prior Scan/Chat/restart thresholds. Before corpus execution bind the running process's observation public key and exact resolved raw-adapter type/version/assembly hash to the candidate. Every provider attempt must carry a valid same-process signed observation with matching candidate/case/request/provider/streaming-request/header/read/EOF provenance; the semantic verifier recomputes all per-domain counters and rejects any SDK aggregate/fake-chunk source even when its metrics match perfectly. All prior zero-exit command, packet/result, Scan/Edge/Uncertainty/Business Logic Document/flow/restart/database receipts, exact half-up scoring, content redaction, and five screenshot categories remain required.
8. **Final verification**: `dotnet build`, full `dotnet test`, JSON Schema and semantic aggregate/ID-reference validation, candidate/path/database/provider identity checks, receipt-to-command/artifact cross-checks, mandatory-check inventory validation, screenshot category/redaction review, hash the evidence files, and report warnings separately from errors. Negative schema vectors remove each required receipt/category/check, duplicate or mis-tier a check, introduce a non-passed mandatory check or nonzero passed command, or attach success results to a non-passed run and must fail. Tier 3 is not complete on test doubles or fake Backup B state.

## Planned-Change Authorization Determination

Independent plan review is required because AD-2 through AD-8 and AD-10/AD-11 are architecture decisions. The repository's additional planned-change authorization has only three triggers. This plan triggers **the Agent-tool condition** and does not trigger the other two:

| Repository trigger | Determination | Exact reason and authority |
| --- | --- | --- |
| adds/exposes/materially changes a Worker Agent or other Agent tool | **YES** | AD-2 through AD-6 materially change the existing Chat Agent retrieval tool contract `IChatEvidenceRetriever.RetrieveAsync`: it no longer requires one prepared legacy exact Edge and instead returns current section-ranked, v2-decision/Edge/Flow Group-expanded, conversation-bound packet v2 evidence. AD-7 also materially changes the Chat Agent's provider-dispatch contract from one aggregate `RunAsync` call to mandatory bounded streaming for primary and repair. Repository implementation history explicitly classifies retrieval as a Chat Agent tool (`specs/008-demo-chat-release-gate/plan.md` lines 626-637 and `contracts/chat-agent-contract.md` lines 255-260). Both seams remain server-executed and provider database/tool-blind; no provider-callable retrieval tool or open loop is added. |
| adds/materially changes a Scan strategy | NO | AD-8 is a read-only presentation of already-persisted Scan/job/recovery state and invokes the existing manual-resume behavior. It changes no parsing, filtering, scheduling, grouping, retry, or dispatch rule. |
| changes, weakens, or conflicts with current PRD | NO | AD-2 through AD-7B implement and strengthen the dated August 30 PRD overlay: database-backed non-vector section retrieval, one-hop persisted expansion, explicit bounded scope, citations, and <30 seconds (`prd.md` lines 24-50, 276-287, 380). Mandatory streaming is the revised spec's transport/trust mechanism for meeting those outcomes, not a weaker product requirement. Long-term typed tools, auto-scope, uploads, and vector behavior remain backlog rather than being removed. |

Therefore, after independent plan review accepts this plan, the root **must pause before task generation and request explicit planned-change authorization** for this exact retrieval-tool and Chat Agent dispatch delta. The request must cite AD-2 through AD-7B, explain that implementation will change `IChatEvidenceRetriever`/packet/currentness authority and make provider streaming mandatory for primary/repair while keeping retrieval server-only and the provider tool-blind, and cite the dated PRD overlay (`prd.md` lines 42-44, 285, and 380) plus revised spec FR-053 through FR-060. The user's parallel split approval is not this authorization. Any approval permits progression only to task generation; mandatory implementation approval still applies before the first production write, and Track B/C dependency gates remain mandatory.

## Requirements Traceability

| Requirement | Plan/contract coverage | Verification seam |
| --- | --- | --- |
| FR-001 | AD-1; Prepared Chat disposition | regression composition + coordinator tests |
| FR-002 | AD-1; grounded-chat contract scope | request/authorization component tests |
| FR-003 | AD-1/AD-3/AD-4 | pre-retrieval + render-time barrier tests |
| FR-004 | AD-1/AD-8; failure table | unauthorized/revoked/missing equivalence tests |
| FR-005 | AD-2/AD-4; mode ranking | Business UI/candidate tests |
| FR-006 | AD-2/AD-4; mode ranking | IT citations/path/line tests |
| FR-007 | AD-2/AD-3/AD-4 | relational eligible-source tests |
| FR-008 | AD-2/AD-6 | token extraction/conversation tests |
| FR-009 | AD-2 | randomized-order ranking tests |
| FR-010 | AD-2/AD-5 | exact/+1 packet selection tests |
| FR-011 | AD-2 | strongest-per-Document tests |
| FR-012 | AD-2 | bounded fallback/insufficient tests |
| FR-013 | AD-1/AD-2/AD-4 | composition/tool-declaration absence tests |
| FR-014 | AD-3 | v2 accepted/legacy-only rejected tests |
| FR-015 | AD-4 | relationship allowlist tests |
| FR-016 | AD-4 | seed/related/Document +1 tests |
| FR-017 | AD-4/AD-5 | relationship/flow/cycle canonical tests |
| FR-018 | AD-2/AD-4 | Business/IT traversal tests |
| FR-019 | AD-3/AD-4 | currentness/authorization/direction/revision tests |
| FR-020 | AD-3/AD-4 | incomplete/unpublished/nonunique/missing support tests |
| FR-021 | AD-5 | packet-v2 contract tests |
| FR-022 | AD-5 | complete-unit packing tests |
| FR-023 | AD-5/AD-6 | UTF-8 evidence/conversation/context +1 tests |
| FR-024 | AD-5 | independently constructed fingerprint vectors |
| FR-025 | AD-1/AD-5/AD-7 | provider request closed-shape tests |
| FR-026 | AD-1; result validator | claim/handle/anchor semantic tests |
| FR-027 | AD-1/AD-7 | whole-result render barrier tests |
| FR-028 | AD-7 | one-repair/no-semantic-repair/deadline tests |
| FR-029 | AD-6 | two-turn/eviction/byte tests |
| FR-030 | AD-6 | boost plus re-fetch mutation tests |
| FR-031 | AD-1/AD-7; failure table | bounded Chinese outcome/no diagnostic tests |
| FR-032 | AD-7/AD-10 | fake-clock unit + Tier 3 timing evidence |
| FR-033 | AD-8 | closed state mapping/component tests |
| FR-034 | AD-8 | counts/retry/reason/summary bounds tests |
| FR-035 | AD-8 | authorized resume/replay/stale tests |
| FR-036 | AD-8/AD-10 | fake-source/screenshot rejection tests |
| FR-037 | AD-9 | corpus schema/count/category tests |
| FR-038 | AD-9 | required fields/no desired prose tests |
| FR-039 | AD-10; evidence contract | status-conditioned envelope/result tests, passed-only receipt, append-only supersession |
| FR-040 | AD-10; test strategy 1-5 | four fixed mandatory Tier 1 checks and complete schema/semantic vectors |
| FR-041 | AD-10/AD-11; test strategy 6 | fixed Tier 2 PostgreSQL restart/resume check and receipts |
| FR-042 | AD-10/AD-11; test strategy 7 | five fixed Tier 3 topology/tool/Scan/Chat/restart/release checks |
| FR-043 | AD-10; test strategy 7 | explicit Scan-count/Edge/Uncertainty/Business Logic Document/Cross-Repository-flow receipts with recomputation |
| FR-044 | AD-9/AD-10; test strategy 7 | exact case coverage, half-up useful percentage, and recomputed packet/result/citation/leakage/timing receipts |
| FR-045 | AD-10; test strategy 6-7 | same-DB restart artifacts, duplicate query receipt, and zero-exit commands |
| FR-046 | AD-10; evidence contract | one candidate-bound screenshot in each of five mandatory categories plus redaction review |
| FR-047 | AD-11 | preconnection database guard tests |
| FR-048 | Track matrix | gate evidence before A/B/C writes |
| FR-049 | AD-3/AD-12 | integrated-contract comparison; block on mismatch |
| FR-050 | AD-12; migration decision | Git diff denies context/migration/snapshot changes |
| FR-051 | project structure/track matrix | explicit integration owner evidence |
| FR-052 | track matrix/AD-11 | path/database/worktree preflight evidence |
| FR-053 | AD-7; streaming contract | composition tests prove primary and optional repair use the dedicated provider stream; non-streaming path cannot yield an accepted provider answer |
| FR-054 | AD-7; streaming contract | decoded-delta 16,384 and assembled-answer 65,536 exact/+1 vectors; distinct raw/SSE safety-cap counters and JSON-unescape accounting |
| FR-055 | AD-7; streaming contract | incremental strict UTF-8 split vectors and malformed/overlong/surrogate/truncated rejection |
| FR-056 | AD-7; failure table | fake-clock cancellation/disconnect/inactivity/deadline/terminal/framing vectors discard all bytes |
| FR-057 | AD-1/AD-7B; grounded-chat contract | mocked server/client boundary proves no fragment, claim, citation, handle, relationship, or Source Anchor is published before every validation barrier |
| FR-058 | AD-7B; streaming contract | accepted-only Unicode text-element reveal and reduced-motion/remaining-budget immediate render tests |
| FR-059 | AD-7; test strategy 3 | one coordinator-created 29-second deadline across retrieval/primary/repair/validation/render, ten-second data-only inactivity, and 3-second repair-window tests |
| FR-060 | AD-10; release schema 2.1 | signed raw-adapter per-attempt provenance, per-domain aggregate recomputation, zero non-streaming accepted answers, fake-chunk forgery, and leakage-negative vectors |

| Success criterion | Plan/contract coverage | Evidence |
| --- | --- | --- |
| SC-001 | AD-1/AD-3/AD-4 | currentness/authorization relational tests + Tier 3 |
| SC-002 | AD-2/AD-4/AD-5 | randomized ordering and repeated-query fingerprints |
| SC-003 | AD-4/AD-5 | exact/+1 packet contract vectors |
| SC-004 | AD-1/AD-7 | semantic validator and citation coverage manifest |
| SC-005 | failure table | adversarial authorization/currentness/shape suite |
| SC-006 | AD-6 | initial + two follow-up integration flow |
| SC-007 | AD-8 | real state UI/resume tests + screenshots |
| SC-008 | AD-9 | corpus schema and count check |
| SC-009 | AD-9/AD-10 | artifact-derived corpus totals and exact half-up aggregate scoring |
| SC-010 | AD-10 | passed-only Tier 3 Scan receipt with recomputed counts/budget and query artifacts |
| SC-011 | AD-3/AD-4/AD-10 | exact Edge, Uncertainty, Business Logic Document and flow query receipts |
| SC-012 | AD-10/AD-11 | same-DB restart/duplicate/database-ownership receipts |
| SC-013 | AD-10 | closed candidate/provider/command/artifact manifest, five screenshot categories, and hashes |
| SC-014 | AD-11 | zero-access preflight and accepted DB names |
| SC-015 | AD-7; streaming contract | production raw-adapter composition plus split wire/SSE/JSON/delta/final/terminal tests and exact domain caps |
| SC-016 | AD-1/AD-7B | end-to-end no-fragment publication plus accepted-only Unicode/reduced-motion render tests |
| SC-017 | AD-7/AD-10 | same-budget timing proof and Tier 3 content-dark stream receipts recomputed from bound artifacts |

All 60 FRs and 17 SCs have an implementation decision and observable seam.

## Inline Architecture Self-Review

| Lens | Evidence reviewed | Result |
| --- | --- | --- |
| Feasibility | Existing packet/handles/fingerprint, candidate extraction/ranking, provider dispatcher/result validator, raw HTTP/SSE patterns, .NET strict incremental UTF-8/JSON/ECDSA primitives, Unicode-safe reveal, currentness barrier, durable Scan recovery, and release harness patterns provide concrete seams. Each streaming stage is independently implementable/testable and the ephemeral signer adds no persistence. | PASS |
| Repository fit | The design deepens existing specific interfaces and project ownership; no generic repository, second Chat stack, vector store, or duplicate graph truth. | PASS |
| Invariants | Explicit selected scope/current v2 truth, deterministic one-hop packet bounds, raw/wire/SSE/JSON/delta/final domain separation, post-unescape 16 KiB Provider Chunk, 64 KiB final answer, strict UTF-8, one terminal plus physical EOF, decoded-data-only inactivity, one 29-second deadline, accepted-only reveal, signed same-process raw-adapter provenance, aggregate equality, half-up scoring, ten checks, and same-candidate evidence are stated at exact owners. | PASS |
| Failure behavior | Missing-equivalent authorization, insufficient evidence, stale/mismatched relationship exclusion, all-or-nothing packet/result, cancellation/disconnect/inactivity/deadline/overflow/UTF-8/terminal violations, one streamed syntax repair only, no partial client output, real Scan warning/failure states, database preflight, and status-conditioned failed/blocked/not-run evidence are closed and non-disclosing; non-passed runs cannot claim success. | PASS |
| Test seams | Ranker/relationship/packet/conversation seams plus arbitrary-read split vectors for wire scalars, CR/LF, SSE fields, JSON tokens/escapes, deltas, terminal and EOF; exact/+1 per-domain caps; fake-clock liveness; production DI resolution; signature/binding tamper vectors; and the aggregate-consistent SDK fake-chunk forgery are observable alongside all prior security/UI/schema/PostgreSQL/release checks. | PASS |
| Migration/compatibility | No persistence write is needed; legacy prepared Chat remains characterized but loses authority only in Story 12.5 composition; transient conversation intentionally does not survive restart. | PASS |
| Security/data stewardship | Provider/tool blindness, strict staged server-only decoding, complete validation before publication, content-dark signed raw-adapter receipts, ephemeral private key/non-secret public fingerprint, current access/Source Anchor validation, bounded artifacts, screenshot redaction, and database isolation preserve Constitution V. | PASS |
| Constitution/ADR/context | All five principles pass; no ADR/context files exist; dated PRD and August architecture are followed without silently deleting long-term backlog. | PASS |
| Dependency/ownership | A/B/C gates are enforceable, Story 12.4 is read-only until accepted integration, shared files have one owner, and no Story 12.4 worktree/database/temp/Git state is touched. | PASS |
| Planned-change governance | Architecture review is required. Existing repository history classifies `IChatEvidenceRetriever` as a Chat Agent retrieval tool; AD-2 through AD-6 materially change it and AD-7 changes Chat Agent provider dispatch, so the explicit planned-change pause before tasks is required. No Scan-strategy or PRD-conflict trigger applies. | PASS |
| Placeholder/source traceability | Every decision cites an observed code seam and repository authority; no unresolved marker, template instruction, or guessed accepted Story 12.4 contract remains. | PASS |

**Self-review conclusion**: **PASS**. The plan remains a bounded Prepared Chat delta with all 60 FRs/17 SCs, A/B/C gates, v2 authority, no migration, ten checks, and five screenshot categories intact. PR4-001 is closed by an ordered raw-wire -> strict UTF-8 -> SSE -> JSON/escape -> decoded-delta -> final-answer pipeline with explicit acceptance versus safety caps, failure ownership, liveness/terminal/EOF behavior, and split/exact/+1 vectors. PR4-002 is closed by sole-authority production raw-adapter observations signed by an ephemeral same-process authority and bound to candidate/case/request/adapter/provider/stream-request/header/read/EOF identities; the aggregate-consistent SDK fake-chunk forgery must fail solely on provenance. No spec, PRD, migration, or Story 12.4 change is required. Status: **PLAN_READY_FOR_REVIEW**.
