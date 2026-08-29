# Implementation Plan: Grounded LLM-Wiki Chat & August 30 Release Evidence

**Branch**: `demo0831/story-12-5` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

**Input**: Story 12.5 specification content at `specs/014-grounded-llm-wiki-chat-release-evidence/spec.md`, corrected under `story-12-5-design-correction-02` at exact clean source checkpoint `719c2a5afc9b2a0161fff4c9deb7b26531ebf66d`. The specification's literal `Status: Draft` header is stale document metadata, not a new product contradiction: the correction preserves its FR/SC content and invalidates the downstream plan/tasks/reviews, not the specification intent. This plan does not edit that separately owned artifact. The immutable accepted Story 12.4 dependency commit is `4798b97d9f23aef587829175df7ab7ebc3daf995` and remains ancestral to this boundary.

## Summary

Deliver Story 12.5 as a bounded delta over Prepared Chat rather than a second Chat stack. Preserve the existing authenticated Business/IT page, server-owned two-Repository scope, opaque handles, packet fingerprint, claim/citation validation, Source Anchors, and render-time authorization/currentness barrier. At checkpoint `719c2a5a`, Track B already has exactly one typed pipeline: authorized current snapshot -> deterministic lexical primary projection -> current-v2 relationship/Flow Group expansion -> complete-unit packet packing -> streamed synthesis -> whole-answer validation. The lexical projection remains the sole primary packet authority; the relationship projector remains the sole optional expansion authority. No question text, exact Edge, Flow Group, legacy row, or relationship result may select or rebuild the primary packet.

The previous cutover deleted the literal Cross-Repository question classifier, `ExactRelationshipRow`/`ProvenanceRow` rebuild branch, projection-only early return, duplicate packet assembly, and every Story 12.5 read of `ExactCrossRepositoryEdgeEvidence` or prepared exact-profile authority. Those removals are implementation evidence to preserve, not work to repeat. Deterministic constants, closed relationship/Flow Group states, and architectural interaction tests make that single path observable. Streaming remains mandatory for primary synthesis and the single syntax repair under strict UTF-8/SSE/JSON/cap/deadline rules. The correction introduces one Web-scoped publication owner and one deadline linearization point so accepted result publication, conversation commit, and neutral completion observation cannot race one another. Track B emits only neutral content-dark runtime/completion observations. Track C alone binds those observations to candidate/case/process/signature/export attestation for release proof. Provider frames remain client-invisible until the complete answer passes every validation barrier.

Accepted Track A already supplies the production Scan-status read model, Chinese management UI, fixed question corpus and evidence-manifest contracts; this correction preserves that behavior/evidence unchanged. Track C remains closed and later runs Tier 1/2/3 from one pinned candidate, using only uniquely named `vulgata_story125_*` PostgreSQL databases and rejecting fake Backup B state. No persistence shape is required; `VulgataDbContext`, migrations, and `VulgataDbContextModelSnapshot` remain unchanged.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); Razor/Blazor Interactive Server; strict UTF-8 JSON and SHA-256 fingerprints

**Primary Dependencies**: existing EF Core/Npgsql 10.0.9 persistence; ASP.NET Core Identity/authorization; FluentValidation; Microsoft Fluent UI Blazor; Microsoft Agents AI/OpenAI-compatible Chat path; Story 12.1 durable Scan recovery; accepted Story 12.4 v2 exact-resolution/Flow Group projections

**Storage**: existing PostgreSQL 17 `vulgata` and `identity` schemas only; no new table, column, index, or EF migration. Conversation state is scoped in-memory server state and is discarded with the Blazor scope/new-conversation reset.

**Testing**: xUnit; pure contract/ranking/packet tests; SQLite relational doubles where existing harnesses are sufficient; authenticated `WebApplicationFactory<Program>` component/composition/API tests; disposable PostgreSQL integration/restart tests; opt-in Tier 3 real CodeGraph/JDT LS/LLM qualification and browser screenshots

**Target Platform**: documented Windows host for Vulgata, CodeGraph, Java 21, and Eclipse JDT LS; PostgreSQL 17 is the only required Docker service for the release run

**Project Type**: existing multi-project Blazor/ASP.NET Core application with Core contracts, Infrastructure relational projections, Agents provider dispatch/validation, Shared boundary DTOs, Web coordination/Razor, and one mirrored xUnit project

**Performance Goals**: deterministic selection for identical question/scope/mode/conversation/persisted evidence; one to five Documents, one to twenty sections, one to thirty-two Source Anchors, at most thirty-two relationships, at most twelve flow steps, and one relationship hop; primary and repair provider calls stream through one bounded transport; accepted complete render under 30 seconds using a 29-second internal absolute deadline

**Constraints**: explicit authorized System and exactly two Repositories; evidence <=64 KiB encoded UTF-8, conversation <=4 KiB, advisory context <=8 KiB; each received logical Provider Chunk <=16,384 payload bytes and their assembled received-response sum <=65,536 bytes before JSON decoding; decoded answer delta <=16,384 UTF-8 bytes and final decoded answer <=65,536 UTF-8 bytes as separate additional bounds; separate raw-read/body and SSE implementation safety caps; strict wire and final UTF-8; one terminal; ten-second decoded-data inactivity; no client fragment before complete validation; no embeddings/vector search/cross-encoder/raw CodeGraph proximity/autonomous multi-hop/unrestricted Agent tools; whole-answer rejection on any transport or validation failure; neutral Track B content-dark runtime observations plus Track C-only signed attestation; fake Backup B state is release-ineligible

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
│   ├── GlobalContext.cs / System.cs / Repository.cs # applied advisory-context authority
│   ├── PendingContextChange.cs                  # excluded until applied by existing lifecycle
│   ├── LogicalDocument.cs / DocumentVersion.cs / SourceAnchor.cs
│   ├── ScanExecution.cs / ScanJobExecution.cs / CodeUnit.cs # File Code Unit rows
│   └── ExactCrossRepositoryEdgeEvidence.cs       # legacy readable only; not v2 Chat authority
├── Vulgata.Core/DomainServices/
│   ├── DurableScanContracts.cs                   # existing recovery snapshot authority
│   └── IContextCompositionService.cs             # existing Global -> System -> Repository semantics
├── Vulgata.Infrastructure/Data/
│   ├── DurableScanRepository.cs
│   └── PreparedChatScopeResolver.cs              # reuse scope/ownership semantics, remove only legacy profile coupling if required
├── Vulgata.Web/Api/Scans/ManualResumeEndpoints.cs
└── Vulgata.Web/Components/Pages/ChatTurn.cs
```

### Existing Story 12.5 paths present at the checkpoint; preserve or adjust only through a corrected task

```text
src/dotnet/
├── Vulgata.Core/DomainServices/
│   ├── ChatContracts.cs                          # typed retrieval stages, packet v2, byte accounting and v2 relationship bindings
│   ├── IChatAgentDispatcher.cs                  # absolute deadline and content-dark attempt summary
│   ├── IChatEvidenceRetriever.cs                 # server-owned bounded conversation input
│   └── IChatEvidenceCurrentnessValidator.cs      # exact decision/Flow Group bindings
├── Vulgata.Infrastructure/Data/
│   ├── ChatEvidenceRetriever.cs                  # one snapshot -> lexical -> relationship -> packet path; old branch deleted
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

### Story 12.5 production-path inventory at the checkpoint

```text
src/dotnet/
├── Vulgata.Core/DomainServices/
│   ├── ChatConversationContracts.cs
│   ├── IChatAdvisoryContextReader.cs             # authorized applied-context snapshot/result seam
│   ├── ChatStreamingContracts.cs                # absolute deadline + release-neutral runtime observation
│   └── ProductionScanStatusContracts.cs
├── Vulgata.Agents/ChatExecution/
│   ├── IChatProviderStreamingTransport.cs
│   ├── ChatProviderStreamAssembler.cs
│   ├── Story125StreamAttestationSink.cs          # Track C-only, not present until separately authorized
│   └── OpenAiCompatibleChatProviderStreamingTransport.cs
├── Vulgata.Infrastructure/Data/
│   ├── ChatEvidencePacketBuilder.cs
│   ├── ChatAdvisoryContextReader.cs
│   ├── ChatRelationshipEvidenceProjector.cs
│   └── ProductionScanStatusReader.cs
└── Vulgata.Web/
    ├── Data/
    │   ├── ProductionScanStatusCoordinator.cs
    │   └── PreparedChatPublicationOwner.cs       # AD-7C planned correction; absent at checkpoint
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

### Story 12.5-added test, fixture, and evidence paths plus the AD-7C test seam

```text
tests/Vulgata.Tests/
├── ChatEvidencePacketBuilderTests.cs
├── ChatAdvisoryContextReaderTests.cs
├── ChatConversationStateTests.cs
├── ChatProviderStreamAssemblerTests.cs
├── OpenAiCompatibleChatProviderStreamingTransportTests.cs
├── ChatStreamObservationProvenanceTests.cs
├── PreparedChatPublicationOwnerTests.cs
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
| Implemented; preserve with architectural guards | `ChatEvidenceRetriever` constructor-composes `IChatLexicalPrimaryProjector`, `IChatRelationshipProjector`, and `ChatEvidencePacketBuilder`; `RetrieveInsideTransactionAsync` invokes them exactly once in lexical -> relationship -> assembly order | Preserve as the only production topology. Treat any classifier, legacy-authority query, alternate final-packet return, direct projector construction, or second packet build as a regression. |
| Implemented; preserve with currentness/mutation guards | `ChatRelationshipEvidenceProjector` and `ChatEvidenceCurrentnessValidator` consume current `DocumentEdge`, accepted v2 decision sets, `GENERATED_FROM`, and complete `Published` Flow Groups; packet 2.2 binds selected decision/count/set authority | Reaccept under the B2 boundary only after fixed-primary interaction and mutation tests prove optional invalid facts cannot replace primaries and selected-required facts fail closed. Do not redesign Story 12.4. |
| Implemented; preserve with regression | `ChatAgentDispatcher` and `OpenAiCompatibleChatProviderStreamingTransport` use the dedicated raw streaming seam for primary/repair, strict staged assembly, one propagated deadline, and neutral runtime observations | Preserve transport ownership and byte-domain behavior. B4 correction is the post-validation publication/telemetry linearization, not another transport rewrite. |
| Partially implemented | Pass 1 uses `RunStreamingAsync` plus inactivity helpers and Pass 2 uses a raw SSE transport with byte accounting | Reuse as source evidence for cancellation/stream plumbing only. Their keepalive liveness, terminal, aggregation, and large-size rules are not Story 12.5 authority and those paths remain unchanged. |
| Implemented; preserve with regression | scoped `ChatConversationState` keeps at most two validated turns and prior Logical Document hints; retrieval re-fetches current rows and packet 2.2 binds the bounded conversation | Reaccept under B3 with mutation/reset/eviction tests; prior prose and handles remain context only. |
| Obsolete | `ExactCrossRepositoryEdgeEvidence`, `ExactResolutionProfileCatalog`, and one prepared occurrence as final retrieval authority | Leave legacy tables/classes readable for old tests; do not join or accept them as Story 12.5 v2 evidence. |
| Obsolete | `specs/008-demo-chat-release-gate` lifecycle and `Story115ReleaseHarness` August 15 identity | Reuse only security/cleanup/polling patterns; create Story 12.5 contracts, names, topology, and evidence. |
| Obsolete | `DemoScanPage`/`DemoScanUiState` as proof | Keep isolated development behavior; exclude route/state/screenshots from all accepted evidence. |
| Partially implemented; design correction required | `PreparedChatCoordinator.CompleteGroundedAnswerAsync` prepares result/conversation, repeats live clock checks, mutates `_conversationState`, then records the final completion summary; later operations can observe time or fail after the last check | Replace only this B4 tail with AD-7C's single publication owner and named linearization point. Completion summary, next conversation, answer, and reveal directive are fully prepared before one final deadline observation and one synchronous composite commit. |
| Implemented and accepted Track A evidence | production Scan status/read UI, fixed corpus, database guard, release schemas and evidence preparation | Preserve unchanged; rerun only regression affected by shared composition. No Track A rewrite or repeat independent review unless behavior changes. |
| Missing execution, prepared contracts only | same-candidate Tier 2/3 observations, real-LLM corpus, restart continuity, screenshots and passed release receipt | Keep Track C closed until B1-B4 and the Track B integration checkpoint are accepted, then execute from a newly pinned candidate. |

## Architecture Decisions

### AD-1: Preserve Prepared Chat's orchestration and security barrier

Keep `PreparedChatCoordinator` as the sole answer orchestrator: validate caller input, create one absolute answer deadline, resolve current user and selected scope, check access, retrieve one closed packet, dispatch one bounded streamed synthesis attempt (plus the allowed streamed syntax repair), assemble and validate the complete result, revalidate currentness/authorization, then publish one complete answer. Existing provider failure and missing-equivalent outcomes remain bounded Chinese messages. No provider frame, claim, citation, handle, relationship, or Source Anchor detail reaches the client before complete validation.

**Alternative**: replace Prepared Chat with a general Chat Agent tool loop. Rejected because it duplicates accepted security behavior, violates the dated bounded-server-retrieval architecture, and creates unrestricted evidence access.

**Consequence**: most existing coordinator/page tests remain regression evidence; Track B changes inputs and packet authority rather than user-facing orchestration identity.

### AD-2: One typed retrieval pipeline with lexical projection as sole primary authority

`IChatEvidenceRetriever.RetrieveAsync` has one implementation path and these typed stage results:

```text
AuthorizedChatSnapshot
  -> ChatLexicalPrimaryProjection
  -> ChatRelationshipExpansionProjection
  -> ChatEvidenceAssembly
  -> EvidencePacket v2
```

Each stage returns exactly one of `Succeeded(value)`, `InsufficientEvidence`, `NotFoundOrUnauthorized`, or `InconsistentCurrentEvidence`; later stages cannot replace an earlier value. The authorized snapshot is one serializable relational read of current Document Versions, `IndexEntry`, `DocumentSection`, section-owned Source Anchors, and the applied advisory-context authorities in AD-2B for the selected System and exactly two Repositories. The lexical stage is the only stage that selects primary Documents or primary sections. The relationship stage accepts the immutable ordered primary projection and may append optional complete support units only. The packet builder accepts those two typed values and is the only packet construction call.

The lexical candidate window retains exactly the highest-ranked 256 distinct `StableSectionId` rows or exhaustion. The current-scope query streams eligible sections in stable-ID order; a pure bounded top-K ranker scores and deduplicates before applying the 256 cap, so a 257th or larger eligible set is not refused merely for size. Duplicate rows with the same stable ID must be byte-equivalent on all ranking/currentness fields or the snapshot is inconsistent. Normalize input to NFC. Extract distinct ordinal-sorted ASCII `[a-z0-9_]+` terms of length >=2, CJK bigrams after the existing closed ignored-term set, Repository names, paths, symbols/endpoints, and mode terms. Parse `KeySymbolsJson`, `BusinessTermsJson`, and `TechnicalTermsJson` as closed string arrays; malformed arrays make that row inconsistent, and symbol/endpoint/mode-term matches use normalized token equality rather than serialized-JSON substring matching. Score each section with these integer components, each applied at most once when any applicable term matches: Repository name 50, key symbol/endpoint 40, Repository-relative path 30, active-mode term 25 (`BusinessTerms` only in Business Mode, `TechnicalTerms` only in IT Mode), search text 20, heading 5, title 3, prose content 1, and a prior-current-Logical-Document boost of 1 total. Repository/path/search/title/heading/prose matching uses normalized ordinal-ignore-case containment. All positive-score candidates compete in the same top-K ranking; a weak metadata match never excludes a stronger prose candidate before scoring. If no positive-score candidate remains, run the same bounded ranker over current Business Logic summaries plus advisory-context terms; if that also yields none, return `InsufficientEvidence` without provider dispatch.

Rank by descending total score, then mode preference, Repository ID, Logical Document ID, section order, and stable section ID. Mode preference is Business Logic then Code Logic in Business Mode; Code Logic TechnicalDetail/Evidence, other Code Logic, then Business Logic in IT Mode. Select the strongest section of each ranked Document first, at most five Documents. Then add remaining sections belonging to those Documents in the same rank order until the exact primary-section target of 12 or exhaustion, never exceeding four sections per Document. Primary section count is therefore 1-12; the packet's 20-section cap reserves up to eight relationship-support sections. No relationship, exact decision, Flow Group, question literal, or legacy row affects primary ranking, primary selection, or fallback.

**Alternative**: keep a question-triggered exact-flow branch or let relationship evidence rebuild the packet. Rejected because source at the pinned boundary proves that this creates two primary authorities and made eight convergence passes test symptoms instead of enforcing the cutover.

### AD-2A: Mandatory single-path preservation and forbidden-topology inventory

Checkpoint `719c2a5a` no longer contains the audited alternate retrieval path. The correction therefore treats all of the following as permanently forbidden topology. B1 is unacceptable if any item is present or can regain production authority:

| Old path/shape | Required cutover |
| --- | --- |
| `ChatEvidenceRetriever.RequiresCrossRepositoryEvidence` and its literal `跨仓库` / `cross-repository` classifier | remain absent; question text is tokenized only by the lexical ranker |
| a `RetrieveInsideTransactionAsync` branch after lexical projection | remain absent; every success invokes lexical -> relationship -> packet stages |
| `ExactRelationshipRow`, `ProvenanceRow`, `RelationshipPacketRow`, or exact-row-first packet rebuild owned by the retriever | remain absent; equivalent valid facts are projected only by `ChatRelationshipEvidenceProjector` |
| projection-only early `Found` return | remain absent; even zero-expansion success passes through the relationship stage and sole packet builder |
| two `ChatEvidencePacketBuilder.Build`/`EvidencePacket.Create` call sites in retrieval | remain impossible; exactly one post-expansion call owns the packet |
| any `ExactCrossRepositoryEdgeEvidence` or `ExactResolutionProfileCatalog` query in Story 12.5 retrieval/currentness/composition | remain absent; legacy entities remain persisted/readable only for unrelated historical compatibility |
| direct construction of the relationship projector inside the retriever | remain absent; constructor composition exposes the single interaction seam |
| tests that seed a legacy row as positive Chat authority | retain only negative compatibility vectors; positive vectors seed accepted v2 decision + current Edge |

A structural composition test and mutation test fail when the classifier, legacy authority query, duplicate packet-builder call, alternate final-packet path, or bypassed projector is reintroduced. This is a single-path invariant, not coexistence, a feature flag, or pending implementation work.

### AD-2B: Server-owned applied Context is the advisory-context authority

`IChatAdvisoryContextReader.ReadAsync` accepts only the server-created `AuthorizedChatScope` produced after caller authorization; no page/API/provider request can supply advisory text or Context row identities. Its Infrastructure implementation reads the one applied `GlobalContext`, selected `System.Context`, and both selected `Repository.Context` values inside the retriever's serializable transaction. `PendingContextChange` is deliberately excluded until the existing Context lifecycle applies it. The reader rechecks System membership and the exact two Repository IDs; a hidden/missing/mismatched scope returns `NotFoundOrUnauthorized` without identities. No rows or all blank rows returns the valid bounded empty snapshot, not an error.

The typed `ChatAdvisoryContextSnapshot` contains canonical complete source units `(Global, System, Repository)` with server-known source identity, `UpdatedAt`, normalized text, ordered Repository IDs, composed provider text, extracted terms, UTF-8 byte count, and `AuthorityFingerprint`. Normalize each value to NFC; map CRLF/CR to LF; trim outer Unicode whitespace; reject disallowed controls while permitting LF; preserve internal text. Compose nonempty units once in `Global -> System -> Repository` order, with the two Repository units ordered by RFC 4122 big-endian Repository ID. Tokenization is the same closed lexical tokenizer as AD-2: distinct ordinal-sorted ASCII `[a-z0-9_]+` terms of length >=2 and CJK bigrams after the ignored-term set. The complete composed UTF-8 text, including canonical LF separators, may be 0-8,192 bytes. Exactly 8,192 succeeds; 8,193 or more returns `InconsistentCurrentEvidence` before ranking/provider dispatch. No byte slicing, scalar truncation, unit dropping, or pending-value substitution is allowed.

Question/conversation terms run the primary rank first. Advisory terms never change a positive primary score or primary identity. Only when that pass has zero positive candidates does fallback rank current Business Logic summary sections with the union of question/conversation/advisory terms. In fallback, a match contributes at most once per field: search/summary 20, heading 5, title 3, prose 1; canonical ties and the same top-256/Document/section rules apply. Empty or nonmatching advisory context adds no score; if fallback still has no positive candidate, return `InsufficientEvidence` without synthesis.

The packet carries the exact composed text and `advisoryContextAuthorityFingerprint`. Its canonical packet fingerprint binds both. Immediately before reveal, currentness reloads the same applied Global/System/two-Repository fields, rechecks authorization/membership, and recomputes the authority fingerprint; any value, `UpdatedAt`, ordering, membership, or applied/pending disposition change withholds the answer as missing/stale. This adds a read/transient seam only and no persistence or migration.

**Alternative**: call the existing per-Repository `IContextCompositionService` twice and concatenate its strings. Rejected because it duplicates Global/System text, does not carry authorization/currentness identity, and cannot prove the exact two-Repository composition. **Alternative**: accept caller advisory text or silently truncate to 8 KiB. Rejected because caller injection and unbound truncation violate server ownership and packet fingerprint integrity.

### AD-3: Use accepted v2 decisions plus current Edges as exact Cross-Repository authority

Track B reads every current `CrossRepositoryResolutionDecision` that the integrated Story 12.4 contract marks as an exact match and binds to the candidate current directed `References` Edge between current selected-scope Document Versions. Story 12.4 deliberately permits any number of distinct endpoint/consumer-entry decisions to share that document-level Edge. Revalidate each decision's System, pair/coordination, consumer/provider Repository, Profiled Revision, decision/consumer/provider/evidence fingerprints, complete contribution authority, exact Edge direction, and current endpoints. The legacy `ExactCrossRepositoryEdgeEvidence` row is never sufficient and is not required.

For one Edge, the Story 12.4 decision-slot key is exactly `(Pass1PairExecutionId, ConsumerCandidateId, ConsumerEntryStableKey, Protocol, NormalizedEndpointIdentifier)` after its consumer-entry identity/currentness validation. Order independently valid decisions by `Pass1PairExecutionId` RFC 4122 bytes, protocol enum value, persisted canonical `ConsumerEntryStableKey` UTF-8 ordinal bytes, persisted canonical endpoint UTF-8 ordinal bytes, `ConsumerCandidateId` RFC 4122 bytes, then decision ID RFC 4122 bytes. Reject only conflicting rows for that same slot or invalid bindings; multiple distinct valid slots are not a conflict. Select the first valid decision solely as the relationship's canonical `selectedExactDecisionId`.

Bind the entire current valid set through `exactDecisionCount` and `exactDecisionSetFingerprint`. The fingerprint preimage starts with the schema identity and Edge ID, then for every decision in the order above uses null-marked, length-prefixed canonical encodings of: decision ID; resolution schema identity; pair/coordination/System IDs; consumer candidate, entry identity/stable key, Document Version, Repository, Scan Run and Profiled Revision; protocol; normalized endpoint and normalization-rule identity; outcome/outcome-kind/reason; provider identity, Document Version, Repository, Scan Run and Profiled Revision; Document Edge ID; Uncertainty/resolved-Uncertainty IDs; and consumer-evidence, provider-evidence-set, decision and aggregate evidence fingerprints. GUIDs use RFC 4122 bytes, enum values fixed big-endian integers, strings persisted canonical UTF-8 bytes, and nullable values a one-byte presence marker. Before hashing, recompute and compare the complete Story 12.4 provider-contribution set and fingerprints; no contribution is discarded merely because another decision shares the Edge. Other valid decisions remain independent members of the set and can own distinct Published Flow Groups.

If the final integrated Story 12.4 names/states differ from the candidate contract, adapt Story 12.5 to the accepted shape without changing Story 12.4. If no accepted shape can express these invariants, Track B is blocked and escalates to the Story 12.4 authority.

**Alternative**: backfill or translate legacy exact-evidence rows into v2 Chat truth. Rejected because endpoint/entry multiplicity and accepted evidence cannot be reconstructed safely.

### AD-4: Current-v2 relationship projection is the sole optional expansion authority

Expansion seeds are the first `min(3, primaryDocumentCount)` Documents in lexical rank order (one when only one primary Document exists, two when two exist, otherwise exactly three). Only outgoing stored direction is traversed: `REFERENCES` source -> target and `GENERATED_FROM` Business Logic source -> Code Logic target. Cross-Repository `REFERENCES` is eligible with one or more independently valid accepted v2 decisions bound to that exact Edge; same-Repository `REFERENCES` and `GENERATED_FROM` carry no decision-set authority. Flow Group support is eligible only through a current `Published` Flow Group whose Business Logic Document Version is already a primary/added Document, whose exact decision is a member of the Edge's recomputed valid decision set, and whose exact Edge, Flow Trace, steps, fingerprints, Documents, Scan Runs, Profiled Revisions, System, and two Repositories are all current and internally consistent.

Candidate Edge order is: seed rank; mode relationship preference (`GENERATED_FROM` then `REFERENCES` in Business, `REFERENCES` then `GENERATED_FROM` in IT); same-Repository before Cross-Repository only within the same preference; target Repository ID; target Logical Document ID; target Document Version ID; Edge ID. Within a Cross-Repository Edge, decisions use AD-3 order. Flow Group order is: owning Edge rank; owning decision rank in that Edge's valid set; `GroupOrder`; Flow Group ID; trace order; step order; step ID. Deduplicate Edges by Edge ID, decision slots by Story 12.4 slot identity, related Documents by Document Version ID, Flow Groups by Flow Group ID, and steps by step ID before any `Take`. Conflicting duplicates are `present-inconsistent`, never tie-broken; distinct decisions/Flow Groups sharing an Edge remain independent. Select at most two new related Documents per seed, at most five Documents total, 32 relationships, ten Flow Group authorities, 12 flow steps, and exactly one hop. A target already selected may contribute a valid relationship without consuming the related-Document allowance.

Relationship and Flow Group candidates use the closed state tables in [data-model.md](data-model.md): `absent`, `present-valid`, `present-inconsistent`, and `selected-required`. Absent or inconsistent optional candidates do not poison unrelated lexical evidence and cannot support a claim. Ranking leaves a complete valid unit optional; only retention after all complete-unit packing drops promotes it to `selected-required`. Any later missing/stale/inconsistent Document, section, Source Anchor, decision, Edge, trace, step, or fingerprint rejects the whole assembly as `InconsistentCurrentEvidence`. Selection never substitutes a lower-ranked unit after such a failure. Business Mode can prefer a valid GENERATED_FROM unit; IT Mode can prefer a valid REFERENCES unit. No state permits an incomplete/unpublished Flow Group or legacy evidence to support expansion.

| Closed support state | Owner | Allowed transition/outcome |
| --- | --- | --- |
| `absent` | relationship projector query boundary | omit; it cannot score, cite, promote or fail unrelated lexical evidence |
| `present-valid` | relationship projector validation/ranking boundary | remain optional through ranking/packing; may be dropped only as one complete unit |
| `present-inconsistent` | relationship projector/currentness validator | omit and record only a content-dark internal reason; it cannot support a claim or be rescued by legacy/lower-authority rows |
| `selected-required` | packet builder after all whole-unit drops | include the complete unit or return `InconsistentCurrentEvidence`; no lower-ranked substitution, partial child drop or late demotion |

Flow Group persistence states map closedly: missing is `absent`; only `Published` plus complete current authority can be `present-valid`; `Incomplete`, `Complete` but unpublished, `Failed`, stale, wrong-direction, wrong-revision, incomplete trace/step, or fingerprint mismatch is `present-inconsistent`; retained packed support alone becomes `selected-required`. The projector owns pre-packing classification, the packet builder owns promotion, and the reveal currentness validator owns post-selection refusal.

**Alternative**: recursive SQL traversal or raw CodeGraph proximity. Rejected because Story 12.5 authorizes one persisted hop only and raw analysis relationships are not publication truth.

### AD-5: Versioned packet builder owns all cardinality and byte invariants

Preserve `ChatEvidencePacketBuilder` as the sole owner that assigns opaque handles after canonical selection, packs complete evidence units, and computes UTF-8 sizes before constructing `EvidencePacket` v2.2. It receives a required lexical-primary unit set, the AD-2B advisory snapshot, and ordered optional relationship/Flow Group units. One relationship unit includes every Document, section, Source Anchor, Edge, canonical selected decision, complete decision-set count/fingerprint, Flow Group authority and flow step needed to make that unit valid; no child is packed independently. Schema 2.2 is the current intentional incompatible cutover from 2.1 because complete many-decision authority and advisory-source authority cannot be represented honestly by the old relationship/context shape; composition accepts only 2.2 and has no dual-version runtime path.

Packing order is: all strongest-per-Document primary units; remaining lexical primary sections in lexical rank; ranked `present-valid` relationship/Flow Group units in AD-4 rank. Enforce five Documents, 20 sections, four sections per Document, 32 Source Anchors, 32 relationships, ten Flow Group authorities, 12 flow steps, 65,536 evidence UTF-8 bytes, 8,192 advisory-context UTF-8 bytes, and 4,096 conversation UTF-8 bytes. If a bound is exceeded, drop optional relationship/Flow Group units from lowest rank, then drop non-strongest lexical extra-section units from lowest rank. Remove support that becomes unreachable after each whole-unit drop. After all drops, every retained relationship/Flow Group unit becomes `selected-required`. Never drop a strongest-per-Document unit, split text, or partially retain selected-required support. If required units alone exceed a bound, return `InconsistentCurrentEvidence`; never truncate. The canonical fingerprint binds schema/policy versions, request/scope/mode, ordered primary identities and ranks, expansion seed identities, each selected Edge's canonical decision/count/set fingerprint, each Flow Group's owning decision, complete packed units, exact advisory text/authority fingerprint, and conversation state.

**Alternative**: rely only on item counts or string character counts. Rejected because provider-visible UTF-8 size is the safety/performance boundary and Chinese content is multibyte.

### AD-6: Conversation is bounded transient context, never durable evidence

`PreparedChatCoordinator` is already scoped; it retains a provider-independent `ChatConversationState` containing at most two prior normalized questions, two complete validated answers, the prior packet fingerprint, cited opaque handles plus stable Logical Document identities for deterministic boost, and the selected System/repository/mode binding. A new conversation, mode change, scope change, authorization failure, or disposed Blazor scope clears it. It is never written to EF, logs, browser storage, or release fixtures.

Each follow-up passes only the bounded state to ranking and synthesis. The retriever re-fetches current Documents, sections, Source Anchors, complete decision sets, Edges, Flow Groups, and the applied advisory Context snapshot; prior stable Document identities receive only a small fixed tie-break-independent score boost after current eligibility. A prior answer/handle absent from the new packet is context only and cannot support a claim.

**Alternative**: persist Chat sessions or trust prior answer citations. Rejected because saved sessions are out of scope and either option weakens currentness/authorization.

### AD-7: Dedicated strict provider stream for primary and repair under one deadline

`PreparedChatCoordinator` creates one monotonic absolute deadline with an internal maximum of 29,000 ms when it accepts a question. Retrieval, primary streaming, optional repair streaming, final UTF-8 flush, parsing, evidence/citation semantics, re-fetch/currentness/authorization, completion-summary preparation, next-conversation preparation, reveal-plan selection, and the AD-7C publication linearization consume the same budget. The provider-independent dispatch contract carries that deadline; neither attempt creates a fresh provider timeout. Repair requires at least 3,000 ms remaining.

Preserve the internal Agents-only `IChatProviderStreamingTransport`, `OpenAiCompatibleChatProviderStreamingTransport`, and pure `ChatProviderStreamAssembler` described by [chat-provider-streaming-v1.md](contracts/chat-provider-streaming-v1.md). The implemented order is raw response-header body reads -> strict incremental wire UTF-8 -> SSE fields/events across arbitrary reads -> received logical Provider Chunk byte gate -> JSON event/escape decoding -> strict UTF-8 encoding of each decoded answer delta -> final concatenated Grounded Answer assembly.

FR-054's logical Provider Chunk is one complete nonterminal SSE `data` event before JSON decoding, including empty, metadata, finish, error, or answer JSON. Its received byte count is the sum of exact wire UTF-8 octets in the event's `data` field values after SSE field-name/colon/optional-one-space removal; SSE line terminators, field syntax, and parser-inserted multi-line LF are framing and excluded. JSON syntax, escapes, and metadata are included. `[DONE]` is the separate terminal and is not a chunk. Every chunk must be <=16,384 received payload bytes, and the monotonic sum of all chunk payloads in the attempt—the assembled received provider response—must be <=65,536 bytes. Both gates execute before JSON decoding; the offending chunk is never decoded.

Keep all domains separate: physical raw read <=16,384 and raw body <=1,048,576 are implementation caps; SSE line/event <=131,072 wire bytes are framing caps; decoded answer delta <=16,384 strict UTF-8 bytes and final decoded answer <=65,536 bytes are additional defense-in-depth bounds after JSON unescape. The runtime observation records `providerChunkCount`, `maximumReceivedProviderChunkByteCount`, `assembledReceivedProviderResponseByteCount`, `answerDeltaCount`, `maximumAnswerDeltaByteCount`, and `assembledAnswerByteCount` independently. No counter is inferred from another, and no decoded limit can excuse a received-byte overflow.

Track B's transport output is a neutral immutable `ChatStreamRuntimeObservation`: opaque runtime observation ID, monotonic sequence, request ID, attempt kind, provider/config/request fingerprints, response-header/read/EOF dispositions, UTC/monotonic transport timings, per-domain counters, transport outcome, and rejection category. It contains no post-transport validation claim. The coordinator prepares a separate neutral `ChatStreamCompletionSummary` template binding the ordered runtime observation IDs, whether syntax repair was used, each attempt's validation outcome and final validation outcome. `AcceptedPublicationCommit` supplies its authoritative accepted duration; a timed-out/cancelled candidate cannot emit an accepted summary. Both are content-dark and contain no candidate commit, corpus case ID, release run ID, public-key fingerprint, signature, artifact path, or release-pass assertion. They support operations, FR-060, and Tier 1 tests but are never themselves release receipts.

The raw adapter owns response status/media/protocol, physical reads/EOF, disconnect/cancellation, and raw-body overflow; the wire decoder owns wire UTF-8; the SSE parser owns field/event grammar/caps; the received-chunk gate owns `received-provider-chunk-overflow` and `received-provider-response-overflow`; the JSON decoder owns JSON/escape/provider-event classification; the delta encoder owns `decoded-delta-overflow`; and the answer assembler owns `final-answer-overflow`, terminal/post-terminal, and final answer. Ten-second inactivity resets only on a valid non-empty decoded answer delta or `[DONE]`, never on physical reads, comments, empty/metadata/finish events. `[DONE]` is the sole success terminal; finish/error metadata must be consistent. EOF before terminal fails; after terminal the adapter reads to physical EOF and rejects duplicate/contradictory terminal or non-empty data/error.

Exactly one logical terminal is mandatory. The adapter reads through physical EOF and rejects missing/duplicate/contradictory terminal state and non-empty post-terminal data. Cancellation, disconnect, inactivity, overall deadline, any domain overflow, invalid UTF-8, terminal violation, provider error, or later validation failure disposes the read, discards/zeroes the buffer best effort, and publishes nothing. The current Pass 1/Pass 2 streaming code is evidence for plumbing only: its keepalive, terminal, aggregation, and size behavior is not composed into this contract.

Only a complete primary response that fails a closed local syntax category may invoke one streamed repair. It reuses the exact packet/fingerprint/deadline and receives the invalid bounded response plus category; no retrieval runs between attempts. There is no third call. Semantic/evidence/citation/currentness/authorization failure, unknown handles, unsupported claims, expired budget, timeout, or cancellation never repairs. The existing deterministic grounded rescue may remain as a bounded operational outcome but cannot satisfy a provider-streamed Tier 3 answer receipt.

**Alternatives**: (1) retain `agent.RunAsync` or use an SDK aggregate pretending to stream—rejected because receipt-level framing, inactivity, terminal, and raw-byte rules cannot be proven; (2) reuse the Pass 2 transport—rejected because it stops at `[DONE]`, has different liveness and size semantics, and cannot detect post-terminal data; (3) reset 30 seconds per attempt—rejected because it violates the end-to-end target; (4) repair semantics—rejected because it can invent evidence.

### AD-7B: Provider streaming and validated UI reveal are separate state machines

Provider frames remain inside Agents/Web server orchestration. `ChatPage` exposes only its generic pending/cancellable state until the coordinator has a fully assembled, syntax-valid, packet-bound, citation-valid, semantically valid, current, authorized answer. The accepted answer is immutable before it reaches the publication owner. Existing `ChatClaimReveal` may present only that accepted answer by Unicode text element. The publication candidate precomputes either `ProgressiveAccepted` with a deterministic reveal cost that fits the remaining internal budget plus margin, or `ImmediateAccepted` for reduced motion or insufficient budget. The client/page cannot turn a pending/provider buffer into revealable content.

**Alternative**: forward provider deltas or syntactically complete partial claims to Blazor. Rejected because later bytes can invalidate syntax, terminal integrity, citations, currentness, or authorization and would create an irreversible disclosure. **Alternative**: keep checking the wall clock after every reveal/update and retract on expiry. Rejected because time can advance after every check and accepted content cannot be undisclosed.

### AD-7C: One deadline-linearized publication owner

Add one Web-scoped `IPreparedChatPublicationOwner` (implemented beside `PreparedChatCoordinator`, not in Agents or Infrastructure) as the sole authority that can commit an accepted result. The coordinator, completion recorder, conversation helper, and page cannot independently publish success or mutate accepted conversation state. They prepare one preallocated `PreparedChatPublicationCandidate` containing the complete accepted `GroundedAnswer`, proposed next `ChatConversationState`, ordered runtime-observation identities, neutral completion-summary template, final authorization/currentness/advisory fingerprints, request ID, monotonic start/deadline tokens, and reveal directive. Its only unset values are the primitive publication timestamp/duration and terminal state written by the owner at linearization; readers project `ChatStreamCompletionSummary` from the committed record.

The publication state machine is closed and has one named linearization point, `AcceptedPublicationCommit`:

```text
Pending
  -> TransportComplete
  -> SyntaxAndEvidenceValidated
  -> CurrentnessAndAuthorizationValidated
  -> CompletionSummaryPrepared
  -> ConversationAndRevealPrepared
  -> PublicationCandidateReady
  -> AcceptedPublicationCommit | TimedOutBeforePublication | CancelledBeforePublication
AcceptedPublicationCommit is terminal; later consumers only read it
Any earlier state -> ClosedFailure
```

The exact owner order is:

1. finish provider assembly and whole-answer syntax/evidence/citation validation;
2. re-fetch and validate all currentness, authorization, advisory Context and selected-required support;
3. build the immutable answer and proposed conversation state without mutating live state;
4. collect immutable runtime-observation IDs and construct the neutral completion-summary candidate in memory; any lookup/preparation failure closes the attempt, and no recorder emits an accepted summary yet;
5. precompute the reveal directive and complete composite publication candidate; all allocation, serialization, logging callbacks, telemetry callbacks, async work and other fallible work ends here;
6. enter the single-writer publication critical section, sample caller cancellation once, then sample the monotonic clock exactly once;
7. if cancelled, or if the sample is greater than or equal to the absolute 29,000 ms deadline, commit no answer/conversation and return the closed cancellation/timeout outcome;
8. otherwise write the sampled primitive timestamp/duration into the preallocated candidate and perform one synchronous non-throwing atomic terminal-state exchange. That exchange is `AcceptedPublicationCommit` and simultaneously makes the accepted answer, proposed conversation, accepted completion summary and reveal directive authoritative; later telemetry consumers only project/read that already-committed summary.

The deadline requirement is defined at that linearization point: an internally accepted answer is under 29 seconds iff the authoritative monotonic sample immediately preceding the single-writer atomic exchange is strictly before the deadline. Time advancing after `AcceptedPublicationCommit` does not retroactively invalidate the commit; no later clock read is part of success acceptance. Returning the already-committed record and Blazor observing that record are reads, not second publication owners. No success telemetry, conversation append, authorization/currentness query, callback, timer decision, allocation, async continuation, or other fallible mutation may occur after the exchange.

`ImmediateAccepted` stores the complete visible answer in the committed page record. `ProgressiveAccepted` is allowed only when its deterministic Unicode-text-element schedule plus a fixed safety margin was computed before step 6 to finish within the internal deadline; the page owns presentation ticks but never content acceptance. A live page-side deadline supervisor switches the already accepted answer to immediate complete display if the schedule falls behind. This supervisor cannot turn failure/pending state into content and cannot extend the server acceptance boundary. Track C separately records the actual complete-render observation and rejects a release attempt at 30,000 ms or later; this release observation does not alter Track B runtime/completion telemetry or retroactively unpublish validated content.

**Alternative**: mutate conversation, return success, and record completion in separate statements guarded by repeated time checks. Rejected because the current checkpoint demonstrates a post-check race and no single linearization owner. **Alternative**: hold a database transaction through provider streaming/render. Rejected because conversation/publication are transient Web state and a long transaction weakens availability without making browser scheduling atomic. **Consequence**: B4 requires a small explicit Web seam and atomic composite record, but no persistence change, Story 12.4 change, provider transport rewrite, or client exposure of unvalidated content.

### AD-8: Real Scan UI uses a bounded read projector over existing durable state

Preserve accepted Track A `IProductionScanStatusReader` in Core and `ProductionScanStatusReader` in Infrastructure. It materializes one ownership-filtered view from current `ScanExecution`, its bounded `ScanJobExecution` rows, safe `FileCodeUnit` relative paths, and relevant Pass 2/coordination terminal state where the accepted integrated model requires it. It maps preparing/indexing, running, retrying, paused, resumed/prepared, completed, completed-with-warnings, integrity/security failure, and cancellation to a closed status contract with counts, earliest next retry, bounded machine-derived Chinese reason, and at most twenty failed-file summaries.

`ProductionScanStatusCoordinator` resolves the current authenticated ManagementAccess user and visible System/Repository ownership, returns missing-equivalent on hidden/missing state, and invokes the existing durable recovery service for `继续扫描`. `ScanHistoryPage` lists current real executions; `ScanExecutionPage` renders and refreshes the bounded view. Neither reads `DemoScanUiState` nor uses the demo iframe.

**Alternative**: adapt `DemoScanPage` or derive state client-side. Rejected because fake circuit state is not durable evidence and client mapping could invent progress.

### AD-9: Fixed source-traceable evaluation corpus, not desired-answer fixtures

Accepted Track A supplies exactly one schema-validated 12-20 case corpus. Every entry records stable ID, Business/IT mode, authorized System/two-Repository selector by release alias, question, follow-up parent where applicable, expected evidence themes/relationship kinds, usefulness rubric, citation/authorization expectation, and timing/result fields. It does not embed provider prose as ground truth. Authorization-negative disclosure is an automatic fail.

**Alternative**: free-form demo questions chosen after seeing results. Rejected because it permits cherry-picking and cannot establish repeatability.

### AD-10: One closed same-candidate release manifest across Tier 1/2/3

The Story 12.5 harness emits an immutable, schema-validated run envelope for every attempt. Its `status` and discriminated result distinguish `planned`, `attempted`, `passed`, `failed`, `blocked`, and `not-run`; only `passed` may contain the non-null release receipt and success metrics. Attempted/failed/blocked records preserve attempted observations, bounded reason/evidence where terminal, and the commands actually run. A correction creates a new `runId`, links `supersedesRunId`, and never overwrites the prior attempt.

Every executed command records stable ID, redacted exact command, its SHA-256, exit code, UTC timing, and hashed output artifacts. Executed evidence binds the candidate commit, integrated Story 12.4 final commit, disposable database, Repository revisions, prompt/packet/stream/schema/tool versions, and exact provider API/model plus non-secret configuration identity/fingerprint.

Track C adds a release-only `Story125StreamAttestationSink` at production composition. The raw adapter publishes each immutable Track B runtime observation directly to this sealed same-process sink; SDK aggregates and callers cannot submit observations. The coordinator supplies the matching immutable `ChatStreamCompletionSummary` through a distinct server-only binding seam; it cannot alter transport fields. At release-process start the sink creates an ephemeral ECDSA P-256 key, binds candidate/process identity and exact adapter type/version/assembly hash, and accepts a server-owned case/run context for each corpus attempt. It projects and signs a `Story125StreamAttemptAttestation` that binds the untouched runtime observation ID/sequence/counters/outcome, the distinct completion summary ID and matching coordinator-owned validation result, and candidate/run/case/request/attempt/provider/`stream=true` request/header/read/EOF identities. The private key and internal admission capability never leave process memory. Thus Track B remains release-neutral while Track C still proves that every exported observation originated at the production raw response-header adapter in the same candidate process. A passed receipt additionally requires those authenticated packet/result/stream artifacts and explicit query/observation receipts for Scan counts, exact Cross-Repository Edge, honest Uncertainty, Business Logic Documents, Cross-Repository flow, restart duplicate checks, and database preflight/ownership/cleanup.

The semantic validator first verifies every referenced artifact hash, Track C attestation signature, same-process public-key preflight, exact production DI type/version/assembly hash, immutable runtime-observation ID/sequence, canonical streaming-request fingerprint, and candidate/run/case/request/provider/header/read/EOF binding, then parses the named receipt artifacts and recomputes every derived aggregate. A perfectly consistent aggregate/SDK fake-chunk observation fails solely when the sealed raw-adapter-to-attestation admission and signed binding are absent. It rejects any mismatch between a manifest value and its source artifact, including Scan counts, ordinary-failure budget usage, Chat case/usefulness/citation/leakage/timing totals, packet fingerprints, raw-read/body, SSE, received-chunk, assembled-received, decoded-delta and final-answer counters, streamed attempt outcomes, restart duplicate/data-loss observations, and database ownership/access counts. Required inequalities and coverage are `ordinaryFailureCount <= ordinaryFailureBudget`, `usefulCount <= caseCount`, `corpusResultCount = caseCount`, and exactly one result receipt for every executed fixed-corpus case. Invoked cases have exactly one primary stream and, only when marked, one syntax-repair stream; non-invoked insufficient/authorization outcomes have no packet or stream. Accepted provider answers require a completed terminal, zero non-streaming accepted answers, an AD-7C committed completion summary, a Track C actual complete-render duration of 0-29,999 ms, and an authenticated content-dark Track C attestation artifact. The release duration is not inferred from the earlier Track B publication duration. `usefulPercent` is computed with exact nonnegative arithmetic as `floor((10000 * usefulCount / caseCount) + 0.5) / 100` (round half up to two decimal percentage points) and must equal the recorded two-decimal value and be at least 80.00. The fixed-corpus ID set, result-row ID set, and case-receipt ID set agree exactly; ordered packet fingerprints equal invoked cases in corpus order. No omitted, duplicate, foreign-candidate, additional, content-bearing, unsigned, wrong-adapter, wrong-process, or fake-chunk stream receipt is accepted.

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
| A: dependency-independent | accepted Track A checkpoint is preserved; no new A production write in this correction | existing production Scan status contracts/reader/coordinator/pages; corpus/schema; evidence manifest/schema remain unchanged | run regression only; repeat independent Track A review only if tracked A behavior changes | preserved authenticated UI/fake-state/corpus/evidence/database-guard results |
| B: dependency-sensitive | Story 12.4 accepted commit remains ancestral + fresh plan review/planned-change authorization + regenerated tasks/definition acceptance + new Track B implementation approval | reaccept B1 typed lexical projection/packet; B2 v2 relationship/currentness; B3 conversation; correct B4 dedicated primary/repair stream, neutral telemetry, deadline-linearized publication and accepted-only reveal; then run B-INT | accepted Story 12.4 entities/configurations are read-only authority; Agents `DependencyInjection.cs`, Web `Program.cs`, project/central registration files have one integration owner; no B work if contract mismatch requires dependency change | one checkpoint per B1-B4 plus one B-INT checkpoint; architectural/mutation REDs, single-path preservation, deterministic relational/pure tests, authorization/currentness barriers, packet/byte bounds, stream/framing/deadline/cancellation proof, one-hop proof, follow-up re-fetch, one repair, zero unvalidated client fragments |
| C: final verification | A and B accepted; one candidate commit pinned; Tier 1 green; disposable DB/tool/provider preflight green | Tier 2 restart run; Tier 3 real repositories/tools/streamed LLM/corpus/screenshots/evidence manifest | no product design or production-code correction inside an evidence run; failed run returns to normal TDD/review then pins a new candidate | same-candidate manifest, streamed primary/repair receipts, zero non-streaming accepted answers, 80% useful, 100% accepted technical-claim citation, zero leakage, <30s accepted answers, restart and screenshots |

Track A and B must use disjoint file ownership during parallel implementation. Track A owns production Scan UI/query and release fixtures/harness. Track B owns Chat contracts/retrieval/Agents/coordinator/page. Only the root-assigned integration owner edits `Program.cs` and any unexpectedly required shared registration/project file. No worker touches `VulgataDbContext`, migrations, model snapshot, Story 12.4 worktree/files/temp directories/database, Git index, or history.

### Track B independently verifiable boundaries

| Boundary | Required architectural RED before replacement write | Green exit |
| --- | --- | --- |
| B1 projection/packet | against the current implementation, mutation tests prove Cross-Repository literal/neutral wording equality, relationship facts cannot replace/reorder lexical primaries, and classifier/legacy query/alternate packet path/duplicate builder/bypassed projector cannot regain control; advisory Context positive/empty/unauthorized/stale/exact-8,192/+1 vectors prove server ownership and fallback | independently accepted B1 checkpoint: sole typed snapshot -> lexical -> relationship -> builder topology; authorized applied-Context reader; exact active-mode ranking/selection/packing vectors and one packet fingerprint; no B2/B3/B4 acceptance implied |
| B2 relationships/currentness | with a frozen accepted B1 projection, mutation tests exercise `absent`, `present-valid`, `present-inconsistent`, and `selected-required`; optional invalid facts support nothing without poisoning primaries, selected-required mutation refuses assembly, and two distinct decisions sharing one Edge retain two decision-owned Published Flow Groups | independently accepted B2 checkpoint: current-v2 allowlist, canonical selected decision plus complete decision-set count/fingerprint, exact ordering/dedup/bounds, closed state tables and render-time revalidation; no Story 12.4 mutation |
| B3 conversation | with accepted B1/B2 ports replaced by deterministic doubles where appropriate, follow-up tests mutate/revoke every previously cited current row and prove only eligible Logical Document hints receive the fixed +1 boost; prior prose/handles never become packet evidence | independently accepted B3 checkpoint: two-turn/4 KiB state, resets, re-fetch and revalidation on every turn; no provider/publication acceptance implied |
| B4 streaming/publication/reveal | split-wire/browser-boundary tests prove raw streaming is mandatory and unvalidated content cannot cross; publication mutation tests expire/cancel before every state and substitute throwing completion/runtime observers; a scheduler/fake-clock vector advances after the authoritative sample and proves the named commit, rather than another check, owns acceptance | independently accepted B4 checkpoint: strict primary/repair stream, neutral content-dark telemetry, one `IPreparedChatPublicationOwner`, one `AcceptedPublicationCommit`, atomic answer/conversation/summary/reveal state, and accepted-only Unicode presentation |
| B-INT composition | production DI resolves exactly the accepted B1-B4 ports; interaction tests fail when lexical primaries are replaced, relationship expansion is skipped, legacy authority is reintroduced, conversation bypasses re-fetch, transport uses aggregation, or any second publication owner appears | one Track B integration checkpoint: Business and IT initial/follow-up flows traverse B1 -> B2 -> B3 -> B4 once, shared authorization/currentness/deadline invariants hold, Web build/focused/full regression pass, and no Track C attestation is composed |

B1 precedes B2; B1+B2 precede B3; B1-B3 precede B4 composition; B1-B4 precede B-INT. Each boundary gets a distinct reviewable checkpoint and can be accepted without claiming later boundaries. A green aggregate test count does not close a boundary unless its architectural/mutation RED was first observed at the named public seam. B-INT is the only checkpoint allowed to claim Track B completion.

## Failure and State Behavior

| Condition | Closed outcome | Mutation/evidence boundary |
| --- | --- | --- |
| selected scope missing/unauthorized/revoked | missing-equivalent Chinese result | no identity, Document, Edge, citation, Scan state, or reason disclosure |
| applied advisory Context absent/blank | valid empty advisory snapshot | primary ranking unchanged; fallback uses question/conversation terms and current Business Logic summaries only |
| advisory Context hidden/mismatched/changed after snapshot | missing-equivalent or stale refusal | caller cannot distinguish missing from unauthorized; no stale Context reaches provider/reveal |
| normalized advisory Context is exactly 8,192 / at least 8,193 UTF-8 bytes | accept exact / reject whole snapshot as inconsistent | no truncation, unit drop, pending substitution, provider call, or partial fingerprint |
| no useful current section after bounded fallback | insufficient evidence | no provider call and no fabricated answer |
| applicable v2 relationship is absent | omit optional candidate; refuse only when a deterministically selected unit requires it | lexical primary packet is unchanged; legacy evidence cannot rescue it |
| applicable v2 relationship is present-inconsistent | closed exclusion; selected-required unit refuses whole assembly | unrelated stale Edge does not poison lexical evidence; inconsistent fact supports no claim |
| two or more distinct current valid exact decisions share one REFERENCES Edge | one Edge with first canonical selected decision plus complete decision-set count/fingerprint | valid multiplicity remains present-valid; each eligible Published Flow Group retains its own decision binding |
| duplicate current decision slot conflicts or decision-set fingerprint changes after selection | present-inconsistent / selected-required refusal | no arbitrary decision choice, lower-ranked substitution, or Story 12.4 mutation |
| applicable Flow Group absent/incomplete/unpublished/stale/wrong revision/trace mismatch | omit optional group; selected-required group refuses whole assembly | no flow claim or step enters packet; unrelated group does not poison lexical evidence |
| one-hop candidate creates cycle/duplicate/sixth Document/+1 bound | candidate excluded canonically | packet remains within hard bounds |
| packet byte cap cannot retain complete required support | whole packet unavailable | no partial evidence or truncated support |
| prior conversation evidence is stale/unauthorized/absent | no boost/evidence; missing-equivalent if scope revoked | prior prose/handle never proves a claim |
| syntactically malformed provider output with budget | one syntax-only repair | same packet/fingerprint; no evidence addition |
| one received logical Provider Chunk is 16,385+ bytes (`received-provider-chunk-overflow`) or assembled received response becomes 65,537+ (`received-provider-response-overflow`) | reject before JSON decoding offending chunk | includes JSON escape/metadata payload bytes; SSE framing excluded; discard all buffers; no repair/render |
| one decoded answer delta is 16,385+ bytes (`decoded-delta-overflow`) or final decoded answer becomes 65,537+ (`final-answer-overflow`) | reject at post-JSON owner | additional bounds remain separate from normative received caps; no substitution, repair, or render |
| malformed/overlong/surrogate/truncated UTF-8 | reject whole stream | strict incremental decoder; no replacement text or repair/render |
| ten seconds without non-empty data/terminal | inactivity rejection | keepalive/comment/metadata cannot reset clock; dispose stream |
| provider disconnect, missing/duplicate/contradictory terminal, or data after terminal | framing rejection | read through physical EOF; discard buffer; content-dark category only |
| caller cancellation, Blazor disconnect, or absolute deadline | cancelled/timed-out rejection | cancellation remains effective through validation/publication; no fragment |
| cancellation or monotonic deadline observed before `AcceptedPublicationCommit` | cancelled/timed-out rejection | preallocated candidate is discarded; no accepted summary, conversation append, answer or reveal directive becomes authoritative |
| clock advances after the authoritative pre-commit sample | committed result remains accepted | the named atomic commit is the linearization point; no second clock check, fallible success work or retroactive revocation exists |
| completion-summary preparation/observation lookup fails before commit | bounded failure | no accepted completion summary or answer is emitted; Track C attestation is not involved |
| second syntax failure, semantic failure, unknown handle | reject whole answer | no partial render and no further call |
| accepted complete result with reduced motion/insufficient reveal budget | `ImmediateAccepted` composite publication | full validated answer is visible state at commit; provider streaming stays server-only |
| progressive accepted presentation falls behind its precomputed schedule | page supervisor switches to full already-accepted answer | no new acceptance, evidence, telemetry or provider content; Track C measures actual complete render |
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

1. **Tier 1 / B1 projection and packet**: begin from the current single typed implementation and observe mutation/structural REDs that fail when a literal-question branch, legacy query, skipped relationship projector, direct projector construction, alternate builder/final packet, or second packet call is introduced. Then prove the preserved typed composition, exact top-256-after-ranking behavior with 257+ eligible sections, duplicate-row conflict detection, structured token equality, exact integer scoring with only the active mode's term field, strongest-per-Document, `min(3,count)` seed identity, 12-primary target, 20-total/four-per-Document, randomized-input ties, independently constructed packet-v2 fingerprints, complete-unit drop order, exact/+1 UTF-8 bounds, insufficient fallback, and no vector/raw graph path. Cross-Repository and neutral wording with the same tokens produce the same primaries; relationship fixture mutations never change primary identities. Public-seam advisory vectors prove: applied Global/System/two-Repository Context is loaded only after authorization; canonical repository ordering is stable; positive terms select a current Business Logic fallback; caller text and pending changes cannot enter; blank/nonmatching Context adds no score; value/membership/authorization mutation refuses reveal; packet/authority fingerprints change with used Context; exactly 8,192 UTF-8 bytes succeeds and 8,193 refuses before provider dispatch.
2. **Tier 1 / B2 v2 relationships and currentness**: hold B1 primary projection fixed and cover every relationship/Flow Group state-table cell. The mandatory architectural RED seeds two distinct current exact decisions sharing one current directed Edge and two distinct Published Flow Groups, then proves one Edge, canonical selected decision, count `2`, stable full-set fingerprint, both decision-owned Flow Groups in canonical order, unchanged lexical primaries, and render-time success. Randomize decision/group enumeration, mutate either decision/contribution/group after selection, and prove stable fingerprint or whole-assembly refusal. Legacy-only, non-exact, missing Edge, wrong direction, stale current pointer, wrong System/Repository/revision/fingerprint, and conflicting duplicate slots are excluded; multiple distinct valid decisions are not. Prove exact mode/order/dedup, first `min(3,count)` seeds, two additions per seed, one hop, cycle/duplicate and every +1 bound, and only complete `Published` Flow Groups/current ordered steps. Optional absent/inconsistent candidates do not poison unrelated lexical evidence; mutating any selected-required support between projection and packet/currentness refuses the whole assembly without lower-ranked substitution.
3. **Tier 1 / B3 conversation and B4 streaming/publication/reveal**: initial + two follow-ups, fourth-turn eviction, scope/mode/new-conversation reset, exact +1 prior-current-Document boost without evidence carryover, re-fetch/currentness mutation barriers, and 4 KiB cap. Split raw wire UTF-8 scalars, CR/LF/CRLF, SSE fields/events, logical chunks, JSON tokens/escapes/surrogate pairs, decoded deltas, finish metadata, terminal, and post-terminal reads at every boundary. Public raw-transport exact/+1 vectors cover received logical Provider Chunk 16,384/16,385 and assembled received response 65,536/65,537 before JSON decoding; escape/metadata-heavy vectors prove received payload counters include all JSON bytes while decoded counters reflect only answer deltas. Pure post-JSON seams cover decoded-delta 16,384/16,385 and final-answer 65,536/65,537; full-pipeline collisions prove received overflow wins first. Separate exact/+1 vectors cover the 16,384-byte read buffer, 1,048,576-byte raw-body cap, and 131,072-byte SSE line/event caps without counter substitution. Every overflow proves closed owner, offending-chunk non-decoding where applicable, whole-buffer discard, zero partial client output and content-dark runtime observation. Fake-clock tests prove only a non-empty decoded delta or `[DONE]` resets inactivity. Both primary and repair share the 29-second deadline/fingerprint/raw adapter; semantic failure cannot repair. Publication tests drive cancellation/timeout/failure at every AD-7C transition, prove the prepared candidate has no live-state effect, prove exactly one atomic commit makes answer/conversation/summary/reveal authoritative, and use a scheduler that advances time immediately after the authoritative sample to prove acceptance is linearized rather than guarded by an impossible later check. Throwing observer/preparation doubles fail before commit. Structural/mutation tests reject any direct `_conversationState` write, success result construction, completion recorder emission or page publication outside `IPreparedChatPublicationOwner`. Contract tests retain runtime/completion/release separation and prove Track C fields are absent.
   A cross-artifact consistency vector extracts the byte-domain definitions and closed transport/rejection/validation vocabulary from this plan, `data-model.md`, `chat-provider-streaming-v1.md`, `release-evidence-contract.md`, and release schema 2.2; it fails on any missing term, synonym, different cap/domain, release-field leakage into Track B telemetry, or reintroduced before-decoding acceptance rule.
4. **Tier 1 / security and UI**: authenticated and missing-equivalent access; Business/IT content/citation presentation; real production Scan mappings for every closed state; counts/earliest retry/bounded reason/failed-file summary; `继续扫描` authorization and durable outcome; fake state never enters status reader or evidence.
5. **Tier 1 / fixtures and evidence**: 12-20 corpus cases, required fields, follow-up graph acyclic/bounded, no desired answer, negative automatic fail, and database-prefix guard before connection. Schema vectors preserve honest status conditioning and append-only correction. Stream-attestation vectors require a sealed raw-adapter admission, immutable runtime observation ID/sequence, candidate/run/case/request/process binding, exact adapter type/version/assembly hash, provider API/config fingerprint, canonical `stream=true` request fingerprint, response-header status/media/protocol, first physical-read/EOF disposition, authenticated signature/artifact, one primary and at most one ordered repair, exact per-domain counters, zero non-streaming accepted answers, and content-dark fields. A fixed production-composition test proves DI resolves the exact raw adapter and Track C sink. Reject altered/foreign/unsigned provenance and a perfectly aggregate-consistent SDK fake-chunk receipt solely because it lacks authorized admission/attestation provenance. Passed-envelope vectors retain all ten mandatory checks and same-envelope command/artifact rules.
6. **Track B integration checkpoint B-INT**: production composition must invoke B1 lexical once, B2 relationship once, B3 current conversation/re-fetch once, and B4 raw stream/publication owner once for authenticated Business and IT initial/follow-up flows. Mutation doubles replace or skip each port and must make the interaction test fail. Verify no legacy authority, SDK aggregate, second packet builder, direct conversation append, second success-publication path, Track C sink or release field is reachable. Run focused B1-B4 suites, Web build, full Tier 1, `git diff --check`, and the no-migration/Story 12.4 boundary guard before declaring Track B complete.
7. **Tier 2 / local integration**: create one clean `vulgata_story125_*` PostgreSQL database; apply current migrations; use neutral Java/XML repositories and real Git/CodeGraph/JDT LS with controlled provider; force provider outage, persist pause/retry, stop and restart the Vulgata process, restore provider, invoke manual resume, and prove no duplicate Documents/Edges/publication. Record redacted exact commands, command hashes, exit codes, durations, versions, provider model/non-secret configuration identity, attempted observations, and database ownership receipts.
8. **Tier 3 / release candidate**: pin one candidate and accepted Story 12.4 commit; host-run Vulgata with PostgreSQL 17 as the sole Docker service; scan exactly two real sample Repositories and 50-150 automatic File Code Units; require all prior Scan/Chat/restart thresholds. Before corpus execution bind the running process's observation public key and exact resolved raw-adapter type/version/assembly hash to the candidate. Every provider attempt must carry a valid same-process signed observation with matching candidate/case/request/provider/streaming-request/header/read/EOF provenance; the semantic verifier recomputes all per-domain counters and rejects any SDK aggregate/fake-chunk source even when its metrics match perfectly. Every accepted case also records the actual complete-render observation; `>=30,000 ms` fails release evidence even though Track B acceptance is already linearized. All prior zero-exit command, packet/result, Scan/Edge/Uncertainty/Business Logic Document/flow/restart/database receipts, exact half-up scoring, content redaction, and five screenshot categories remain required.
9. **Final verification**: `dotnet build`, full `dotnet test`, JSON Schema and semantic aggregate/ID-reference validation, candidate/path/database/provider identity checks, receipt-to-command/artifact cross-checks, mandatory-check inventory validation, screenshot category/redaction review, hash the evidence files, and report warnings separately from errors. Negative schema vectors remove each required receipt/category/check, duplicate or mis-tier a check, introduce a non-passed mandatory check or nonzero passed command, or attach success results to a non-passed run and must fail. Tier 3 is not complete on test doubles or fake Backup B state.

## Planned-Change Authorization Determination

**Author strategy**: this correction is authored with `gpt-5.6-sol` at high reasoning. If the corrected plan is independently accepted and receives fresh planned-change authorization, regenerated tasks must also be authored with `gpt-5.6-sol` at high reasoning. Model selection does not advance, merge, or waive any gate.

Independent plan review is required because AD-2 through AD-8 and AD-10/AD-11 are architecture decisions. The repository's additional planned-change authorization has only three triggers. This plan triggers **the Agent-tool condition** and does not trigger the other two:

| Repository trigger | Determination | Exact reason and authority |
| --- | --- | --- |
| adds/exposes/materially changes a Worker Agent or other Agent tool | **YES** | AD-2/AD-2A/AD-4 preserve the material cutover already made to the Chat Agent retrieval tool contract `IChatEvidenceRetriever.RetrieveAsync`: one typed lexical-primary/current-v2-expansion packet contract replaced the historical dual prepared/exact paths. AD-7 preserves mandatory bounded raw provider streaming, while AD-7C materially changes the server-side Chat completion/publication boundary and AD-10 retains a Track C-only attestation export. Repository implementation history explicitly classifies the Core retrieval interface as a Chat Agent tool (`specs/008-demo-chat-release-gate/plan.md` lines 626-637 and `contracts/chat-agent-contract.md` lines 255-260). All seams remain server-executed and provider database/tool-blind; no provider-callable retrieval tool or open loop is added. |
| adds/materially changes a Scan strategy | NO | AD-8 is a read-only presentation of already-persisted Scan/job/recovery state and invokes the existing manual-resume behavior. It changes no parsing, filtering, scheduling, grouping, retry, or dispatch rule. |
| changes, weakens, or conflicts with current PRD | NO | AD-2 through AD-7C implement and strengthen the dated August 30 PRD overlay: database-backed non-vector section retrieval, one-hop persisted expansion, explicit bounded scope, citations, and <30 seconds (`prd.md` lines 24-50, 276-287, 380). Mandatory streaming and the named publication linearization are the specification's transport/trust mechanisms for meeting those outcomes, not weaker product requirements. Long-term typed tools, auto-scope, uploads, and vector behavior remain backlog rather than being removed. |

Therefore, after independent plan review accepts this plan, the root **must pause before task generation and request fresh explicit planned-change authorization**. Prior planned-change and Track B/conditional Track C approvals are hash-invalidated by `story-12-5-design-correction-02` and cannot be reused. The requested authorization covers only: preserving and mutation-guarding the AD-2/AD-2A typed single retrieval topology and AD-2B server-owned advisory reader; retaining lexical projection as sole primary authority and AD-4 current-v2 projection, including many decisions sharing one Edge, as sole optional expansion; retaining packet schema 2.2 and neutral Track B transport telemetry; replacing the unlinearized B4 tail with AD-7C's one Web publication owner/composite commit; and retaining Track C schema 2.2 attestation projection. Implementation intent is to reaccept current B1-B3 behavior through the named independent boundaries, write an honest B4 architectural RED against the present post-check race, implement the minimum publication-owner correction, then run B-INT. The provider remains tool-blind and mandatory raw streaming/accepted-only reveal remain unchanged.

The authorization request must cite Story 12.5 acceptance in `docs/bmad/epics.md` (deterministic lexical selection, current persisted one-hop expansion, bounded packet and Tier 3 proof); the August design sections 13.1-13.4 (non-vector section retrieval, persisted expansion, packet and conversation); dated PRD overlay lines 42-49 plus FR-11.6/FR-11.7 and NFR-1.2; specification FR-007-FR-032 and FR-053-FR-060; and AD-2 through AD-7C/AD-10 here. It authorizes progression only to regenerated tasks. Fresh definition review and mandatory implementation approval remain before any production/test write; Track C remains closed until Track B and B-INT are accepted.

## Requirements Traceability

### Checkpoint `719c2a5a` requirement-state inventory

`Implemented` means current production/test evidence exists but must be reaccepted under the corrected checkpoint boundary; it does not preserve an invalidated review or approval. `Partial` means a valid portion exists but the named acceptance behavior/evidence is incomplete. `Missing` means no release execution exists. No accepted FR/SC is obsolete or contradicted; the obsolete items are implementation shapes (`ExactCrossRepositoryEdgeEvidence` authority, the literal-question alternate packet path, packet 2.0/2.1 runtime authority, fake Backup B release proof), not product requirements.

| Requirement | State | Checkpoint evidence / remaining design work |
| --- | --- | --- |
| FR-001 | Implemented | bounded Prepared Chat delta remains the production path; B-INT must reaccept it |
| FR-002 | Implemented | authenticated explicit mode/System/two-Repository request boundary exists |
| FR-003 | Implemented | pre-retrieval and reveal-time access checks exist; preserve in B-INT |
| FR-004 | Implemented | missing-equivalent outcomes exist; mutation guards remain required |
| FR-005 | Implemented | Chinese Business Mode and ranking/prompt behavior exist |
| FR-006 | Implemented | IT Mode Source Anchor/path/citation behavior exists |
| FR-007 | Implemented | current authorized relational snapshot is the B1 input |
| FR-008 | Implemented | closed lexical/conversation token extraction exists |
| FR-009 | Implemented | deterministic integer scoring/canonical ties exist |
| FR-010 | Implemented | five-Document/primary/total/per-Document limits exist |
| FR-011 | Implemented | strongest-per-Document selection exists |
| FR-012 | Implemented | server-owned advisory Business Logic fallback/refusal exists |
| FR-013 | Implemented | forbidden vector/raw-graph/tool-loop paths are absent |
| FR-014 | Implemented | accepted v2 decision/current Edge authority replaces legacy evidence |
| FR-015 | Implemented | allowlisted current REFERENCES/GENERATED_FROM/Flow Group projection exists |
| FR-016 | Implemented | `min(3,count)`, one-hop, two-related and five-Document limits exist |
| FR-017 | Implemented | relationship/flow/cycle/dedup/canonical bounds exist |
| FR-018 | Implemented | Business/IT relationship preference exists |
| FR-019 | Implemented | relationship/currentness/advisory revalidation exists; B2 mutations reaccept it |
| FR-020 | Implemented | unpublished/inconsistent support cannot authorize a claim |
| FR-021 | Implemented | sole packet 2.2 composition and cardinalities exist |
| FR-022 | Implemented | complete-unit packing/drop/refusal exists |
| FR-023 | Implemented | evidence/conversation/advisory UTF-8 bounds exist |
| FR-024 | Implemented | canonical 2.2 fingerprint binds ordered authority/context |
| FR-025 | Implemented | provider request is bounded and tool/database blind |
| FR-026 | Implemented | material claims require packet citation/Source Anchor support |
| FR-027 | Implemented | whole-result syntax/evidence/citation/semantic barrier exists |
| FR-028 | Implemented | one syntax-only repair under the shared budget exists |
| FR-029 | Implemented | scoped two-prior-turn conversation exists |
| FR-030 | Implemented | fixed hint boost plus re-fetch/revalidation exists |
| FR-031 | Implemented | bounded Chinese failure outcomes and zero provider partials exist |
| FR-032 | Partial | 29-second budget propagates, but accepted result/conversation/summary publication lacks AD-7C linearization |
| FR-033 | Implemented | accepted Track A production Scan-state projection/UI exists |
| FR-034 | Implemented | persisted counts/retry/reason/failed-file summary exists |
| FR-035 | Implemented | authorized durable `继续扫描` exists |
| FR-036 | Implemented | fake Backup B is isolated and release-ineligible |
| FR-037 | Implemented | fixed 12-20 case corpus exists |
| FR-038 | Implemented | source-traceable case schema/results without desired prose exist |
| FR-039 | Partial | closed schemas/semantic validator exist; final same-candidate evidence run is missing |
| FR-040 | Partial | broad Tier 1 suites exist; B4 publication and B-INT acceptance are incomplete |
| FR-041 | Partial | disposable-DB guard/contracts exist; controlled Tier 2 restart execution is missing |
| FR-042 | Partial | Tier 3 harness/contracts exist; real candidate execution is missing |
| FR-043 | Partial | receipt/query rules exist; real Scan threshold evidence is missing |
| FR-044 | Partial | corpus/scoring/stream receipt rules exist; real complete-corpus results are missing |
| FR-045 | Partial | restart/duplicate-proof contract exists; candidate execution is missing |
| FR-046 | Partial | five screenshot/redaction categories exist; candidate screenshots are missing |
| FR-047 | Implemented | forbidden-target preconnection database guard exists |
| FR-048 | Implemented | A/B/C gates are explicit; all downstream authority is intentionally invalidated for this correction |
| FR-049 | Implemented | Story 12.4 contract remains read-only and accepted commit is ancestral |
| FR-050 | Implemented | no EF/model/migration delta exists or is planned |
| FR-051 | Implemented | shared integration-file ownership boundary exists |
| FR-052 | Implemented | separate-worktree/database/Git boundary remains enforced |
| FR-053 | Implemented | primary and repair use the dedicated raw streaming transport |
| FR-054 | Implemented | received chunk/assembled-response byte domains and caps exist |
| FR-055 | Implemented | strict incremental UTF-8 and closed invalid-sequence rejection exist |
| FR-056 | Partial | one deadline reaches all stages, but its final authoritative observation/commit is not implemented |
| FR-057 | Partial | transport failures discard buffers; cancellation/deadline still race final conversation/result publication |
| FR-058 | Implemented | provider/unvalidated evidence cannot cross the complete validation barrier |
| FR-059 | Partial | accepted-only Unicode reveal exists; AD-7C must bind immediate/progressive directive to the same commit |
| FR-060 | Partial | neutral runtime/completion models exist; accepted completion ordering and Track C attestation execution remain incomplete |

| Success criterion | State | Checkpoint evidence / remaining work |
| --- | --- | --- |
| SC-001 | Implemented | currentness/authorization support exists; B-INT reaccepts composition |
| SC-002 | Implemented | deterministic ordered retrieval/fingerprint suites exist |
| SC-003 | Implemented | packet cardinality/one-hop bounds exist |
| SC-004 | Implemented | whole-answer claim/citation validator exists |
| SC-005 | Partial | evidence/transport failures are closed; final publication race remains |
| SC-006 | Implemented | initial plus two follow-up state/re-fetch exists |
| SC-007 | Implemented | accepted Track A production Scan UI/resume/fake exclusion exists |
| SC-008 | Implemented | fixed corpus contract/fixture exists |
| SC-009 | Missing | requires real Tier 3 corpus results |
| SC-010 | Missing | requires real Tier 3 Scan |
| SC-011 | Missing | requires real candidate Edge/Uncertainty/Business Logic Document/flow proof |
| SC-012 | Missing | requires same-database candidate restart proof |
| SC-013 | Missing | requires final same-candidate evidence/screenshots/hashes |
| SC-014 | Implemented | database isolation guard exists; candidate execution still must obey it |
| SC-015 | Implemented | dedicated raw streaming and bound/terminal suites exist |
| SC-016 | Implemented | no unvalidated provider fragment plus accepted-only reveal exists |
| SC-017 | Partial | shared budget exists; AD-7C and actual Track C complete-render observation are missing |

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
| FR-012 | AD-2/AD-2B | server-owned advisory fallback/insufficient tests |
| FR-013 | AD-1/AD-2/AD-4 | composition/tool-declaration absence tests |
| FR-014 | AD-3 | v2 accepted/legacy-only rejected tests |
| FR-015 | AD-4 | relationship allowlist tests |
| FR-016 | AD-4 | seed/related/Document +1 tests |
| FR-017 | AD-4/AD-5 | relationship/flow/cycle canonical tests |
| FR-018 | AD-2/AD-4 | Business/IT traversal tests |
| FR-019 | AD-3/AD-4 | currentness/authorization/direction/revision/full-decision-set tests |
| FR-020 | AD-3/AD-4 | incomplete/unpublished/conflicting-slot/missing support and shared-Edge/multiple-decision tests |
| FR-021 | AD-4/AD-5 | packet-v2 selected-decision/count/set-authority contract tests |
| FR-022 | AD-5 | complete-unit packing tests |
| FR-023 | AD-2B/AD-5/AD-6 | UTF-8 evidence/conversation/advisory exact/+1 tests |
| FR-024 | AD-2B/AD-4/AD-5 | independently constructed advisory/decision-set/packet fingerprint vectors |
| FR-025 | AD-1/AD-2B/AD-5/AD-7 | provider request closed shape with server-owned advisory snapshot tests |
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
| FR-054 | AD-7; streaming contract | received logical Provider Chunk 16,384/16,385 and assembled received-response 65,536/65,537 pre-JSON vectors; separate raw/SSE and decoded-delta/final-answer exact/+1 counters |
| FR-055 | AD-7; streaming contract | incremental strict UTF-8 split vectors and malformed/overlong/surrogate/truncated rejection |
| FR-056 | AD-7; failure table | one coordinator-created 29-second deadline across retrieval/primary/repair/validation/render, ten-second decoded-data-or-terminal inactivity, and 3-second repair-window tests |
| FR-057 | AD-7; failure table | cancellation/disconnect/inactivity/deadline/overflow/UTF-8/terminal/framing vectors discard the untrusted buffer and return no partial Grounded Answer |
| FR-058 | AD-1/AD-7B/AD-7C; grounded-chat contract | mocked server/client boundary proves no fragment, claim, citation, handle, relationship, or Source Anchor is published before every validation barrier and the sole atomic commit |
| FR-059 | AD-7B/AD-7C; streaming contract | accepted-only Unicode text-element reveal, committed reveal directive and reduced-motion/remaining-budget immediate render tests |
| FR-060 | AD-7/AD-10; streaming and release contracts | neutral content-dark Track B runtime observation; Track C sealed-admission signed attestation, per-domain aggregate recomputation, zero non-streaming accepted answers, fake-chunk forgery, and leakage-negative vectors |

| Success criterion | Plan/contract coverage | Evidence |
| --- | --- | --- |
| SC-001 | AD-1/AD-2B/AD-3/AD-4 | currentness/authorization/advisory/relational tests + Tier 3 |
| SC-002 | AD-2/AD-2B/AD-4/AD-5 | randomized decision/context ordering and repeated-query fingerprints |
| SC-003 | AD-2B/AD-4/AD-5 | advisory and packet exact/+1 contract vectors |
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
| SC-015 | AD-7; streaming contract | production raw-adapter composition plus split wire/SSE/received-chunk/JSON/decoded/final/terminal tests and exact received-byte caps |
| SC-016 | AD-1/AD-7B/AD-7C | end-to-end no-fragment publication, sole-commit interaction and accepted-only Unicode/reduced-motion render tests |
| SC-017 | AD-7/AD-10 | same-budget timing proof and Tier 3 content-dark stream receipts recomputed from bound artifacts |

All 60 FRs and 17 SCs have an implementation decision and observable seam.

## Design-correction audit disposition

| Verified finding from `design-correction-02.md` and referenced audit | Disposition | Plan closure/source evidence at `719c2a5a` |
| --- | --- | --- |
| old literal-question/`ExactRelationshipRow` second packet path | **aged/resolved in current implementation** | current `ChatEvidenceRetriever` constructor-composes lexical and relationship projectors and calls one packet builder; AD-2A makes the old symbols/topology forbidden-regression evidence rather than planning their reimplementation |
| Track B bundled retrieval, relationship authority, packet, conversation, streaming, publication and UI under one acceptance boundary | **resolved in corrected delivery design** | B1, B2, B3 and B4 now have independent RED/green/checkpoint exits; B-INT alone claims combined Track B completion |
| green aggregate suites did not prove architecture adoption | **resolved in corrected test design** | mutation tests must fail when primaries are replaced, relationship projection is skipped, legacy authority regains control, conversation skips re-fetch, aggregate transport is composed, or a second publication owner appears |
| optional relationship/Flow Group outcomes and failure owners were weak | **resolved in corrected authority design** | AD-4 names four closed states, exact owners, promotion point, no-substitution behavior and complete Flow Group persistence mapping |
| repeated T079-T081 deadline checks demonstrate ambiguous publication ownership | **resolved in corrected architecture** | AD-7C names `IPreparedChatPublicationOwner`, preallocated candidate, exact work ordering and single `AcceptedPublicationCommit`; time after the authoritative sample is explicitly outside retroactive acceptance |
| Track B runtime/completion observation was confused with Track C attestation | **resolved/preserved** | AD-7 keeps content-dark release-neutral observations; AD-7C makes completion authoritative with the composite commit; AD-10 alone adds candidate/process/signature/export identity |
| stale plan status/requirement disposition hid what was already built | **resolved in plan** | input metadata distinguishes the stale spec header from unchanged product content; the checkpoint inventory classifies every FR/SC and preserves current valid B1-B3/Track A evidence |
| correction might require spec/BMad, Story 12.4 or EF changes | **rejected with current source evidence** | product intent is coherent, current rows and accepted Story 12.4 contracts express the required authority, and AD-12 prohibits dependency/persistence changes |

No verified design finding is deferred. The only planned product correction is the B4 publication owner plus acceptance/mutation seams; B1-B3 and Track A are preserved/reaccepted, while Track C execution remains closed.

## Plan-review-07 finding disposition

| Immutable finding | Disposition | Exact closure in this plan set |
| --- | --- | --- |
| `S125-PR7-001` Story 12.4 many-decisions-to-one-REFERENCES-Edge cardinality | **resolved-in-plan** | AD-3 defines the exact slot key, canonical decision order, selected decision, full count/set-fingerprint preimage and contribution revalidation; AD-4/state tables preserve distinct decision-owned Published Flow Groups; packet 2.2 carries selected/count/set plus decision-ranked groups; B2 requires two decisions/one Edge/two groups and mutation REDs. Story 12.4 is unchanged. |
| `S125-PR7-002` unowned advisory-context fallback | **resolved-in-plan** | AD-2B names applied `GlobalContext`, `System.Context`, and exact-two `Repository.Context` as the server-owned source behind `IChatAdvisoryContextReader(AuthorizedChatScope)`; excludes pending/caller text; closes authorization/currentness/normalization/tokenization/order, empty state, exact 8,192/+1 behavior, fallback scores, packet binding and positive/negative public-seam tests. |
| `S125-PR7-003` transport observation/validation conflation | **resolved-in-plan** | `ChatSynthesisAttempt.RuntimeObservationId` now names transport-only telemetry; distinct `CompletionSummaryId`/`ValidationOutcome` name coordinator-owned validation; streaming/release contracts and schema 2.2 bind both immutable IDs in Track C without moving release fields into Track B. |

No finding requires a specification, BMad story, Story 12.4 contract, EF model, migration, or snapshot change. No finding is deferred or rejected.

## Plan-review-08 finding disposition

| Immutable finding | Disposition | Exact closure in this plan set |
| --- | --- | --- |
| `S125-PR8-001` FR-054/SC-015 received-byte domain conflict | **resolved-in-plan** | AD-7 and the streaming contract define one complete nonterminal SSE `data` event as the logical Provider Chunk, count its exact received payload bytes before JSON, enforce 16,384 per chunk and 65,536 across all chunks, and give +1 failures dedicated pre-decode owners. Raw-read/body, SSE, decoded-delta and final-answer domains remain independent; escape/metadata-heavy vectors, zero-partial failure tests, neutral telemetry fields and signed Track C recomputation prove every counter. The accepted specification is not reinterpreted. |

No `plan-review-08` finding requires a specification, BMad story, Story 12.4 contract, EF model, migration, or snapshot change. Nothing is deferred.

## Inline Architecture Self-Review

| Lens | Evidence reviewed | Result |
| --- | --- | --- |
| Feasibility | Current source already supplies B1 lexical -> B2 relationship -> packet topology, B3 conversation, strict B4 raw streaming, result validation, neutral observations and accepted-only page presentation. The remaining correction is one Web-scoped preallocated candidate plus single-writer atomic state transition, implementable with existing scoped lifetime/`TimeProvider` and without holding a database transaction. | PASS |
| Repository fit | The design preserves existing specific ports and adds one narrow Web publication port beside the coordinator; no generic repository, second Chat stack, vector store, duplicate graph truth, provider-callable tool, or Infrastructure/Agents ownership leak appears. | PASS |
| Invariants | One typed retrieval path; immutable lexical primaries; exact deterministic ranking/packing; complete many-decision Edge authority; four closed relationship/Flow Group states with named owners; distinct byte domains; strict UTF-8/terminal/EOF; one propagated deadline; one preallocated publication candidate; one authoritative monotonic sample; one `AcceptedPublicationCommit`; atomic answer/conversation/summary/reveal authority; accepted-only presentation; neutral Track B telemetry and Track C-only attestation. | PASS |
| Failure behavior | Every fallible/async/currentness/authorization/telemetry-preparation operation precedes the final sample. Cancellation/deadline before commit publishes nothing; time after commit cannot retroactively fail it; no success callback or mutation follows commit. Transport/validation/support failures remain all-or-nothing and non-disclosing. Track C separately fails late physical render evidence without changing Track B authority. | PASS |
| Test seams | B1-B4 each have independent mutation-resistant public seams and checkpoints; B4 injects expiry/cancellation/throwing preparation at every transition and advances time after the authoritative sample. Structural guards fail on legacy authority, skipped relationship projection, alternate builder/transport/publication, direct conversation append or release-field leakage. B-INT proves exact production interaction order in Business/IT initial/follow-up flows. | PASS |
| Migration/compatibility | No persistence write is needed; current packet 2.2/typed retrieval/streaming behavior is preserved; legacy rows stay historical and unauthorized; transient conversation intentionally does not survive restart. The new publication record is scoped in-memory compatibility state. | PASS |
| Security/data stewardship | Provider/tool blindness, strict staged server-only decoding, complete validation before publication, content-dark runtime observations, Track C sealed admission/signed attestation, ephemeral private key/non-secret public fingerprint, current access/Source Anchor validation, bounded artifacts, screenshot redaction, and database isolation preserve Constitution V. | PASS |
| Constitution/ADR/context | All five principles pass; no ADR/context files exist; dated PRD and August architecture are followed without silently deleting long-term backlog. | PASS |
| Dependency/ownership | A/B/C gates are enforceable, accepted Story 12.4 remains read-only, shared files have one owner, B1-B4/B-INT have separate exits, and no Story 12.4 worktree/database/temp/Git state is touched. | PASS |
| Planned-change governance | Architecture review is required. The corrected plan preserves a material Chat Agent retrieval-tool cutover and adds AD-7C's material server completion boundary, so fresh planned-change authorization remains mandatory before regenerated tasks. No Scan-strategy or PRD-conflict trigger applies. | PASS |
| Placeholder/source traceability | Every FR/SC has a checkpoint state and verification seam. The specification `Draft` label is explicitly treated as stale document metadata rather than silently changing its content; no unresolved design clarification, template instruction, guessed Story 12.4 contract or historical dual-path claim remains. | PASS |

**Self-review conclusion**: **PASS**. The design correction preserves the already-implemented single typed retrieval path instead of planning its reimplementation, decomposes Track B into independently acceptable B1-B4 boundaries plus B-INT, closes optional support ownership, and replaces repeated deadline checks with one feasible `AcceptedPublicationCommit`. The authoritative sample/commit definition deliberately avoids the impossible requirement that time be rechecked forever: every fallible operation precedes it, the composite state changes once, and actual <30-second complete render remains a separate Track C observation. All 60 FRs/17 SCs are inventoried; the accepted byte domains, current-v2 authority, advisory Context, packet 2.2, mandatory raw streaming, no-unvalidated-client boundary, no migration, no Story 12.4 mutation, Track B/Track C telemetry separation, ten checks, five screenshot categories and fake-state exclusion remain intact. No specification-content, BMad, PRD, EF, or Story 12.4 change is required. Architecture trigger: **YES**. Fresh independent plan review and planned-change authorization are required before Sol/high task authoring. Status: **PLAN_READY_FOR_REVIEW**.
