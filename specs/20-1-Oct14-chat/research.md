# OCT-CHAT research and decisions

Date: 2026-10-06. Static inspection only; no build, provider, database or runtime qualification.

## R1 — Direct feature planning and selected scope

**Decision:** Use direct speckit-specify/speckit-plan in this worktree. The October first-deliverable inputs supersede original story task breadth. Preserve compatible constitution 2.0.0, architecture, access, testing and runtime rules. No old workflow state is resumed.

**Rationale:** Explicit user instruction and October delivery execution boundary. Optional agent-context hooks and the plan's root instruction update are intentionally omitted under the same instruction.

**Alternatives considered:** Reopening Story 18.5 or invoking governed delivery would contradict the feature dispatch. Copying full 19.x acceptance matrices would reintroduce explicitly deferred work.

## R2 — New October path within existing projects

**Decision:** Introduce October-scoped Chat contracts/coordinator and adapt the existing Chat page through an opt-in feature-local component. Do not broaden historical candidate contracts in place.

**Evidence:** `src/dotnet/Vulgata.Core/DomainServices/ChatConversationContracts.cs` requires exactly two Repositories and nonempty references. `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs` buffers strict candidate JSON. `ChatProviderStreamAssembler.cs` consumes content with stop-only completion. `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` awaits `AskAsync` and then reveals text. `PreparedChatScopeResolver.cs` requires a pair; demo scope also uses names.

**Rationale:** These are behavioral incompatibilities, not presentation issues. Preserve historical tests for their original boundary; new contracts admit arbitrary permitted Repository selections, empty reports, typed deltas and durable outcomes.

**Alternatives considered:** Relaxing legacy validators alone still leaves fake streaming and legacy read selection. Replacing the whole Chat visual design adds unnecessary work.

## R3 — MAF execution with bounded native tools

**Decision:** Use installed MAF for one bounded model/tool conversation per turn. Wrap its injected wiki functions and provider boundary with turn authority, budgets and events; stream user-facing updates as produced. Qualify the installed streaming API against controlled native tool-call responses before live provider use.

**Evidence:** `Directory.Packages.props` pins Microsoft.Agents.AI and Microsoft.Agents.AI.OpenAI 1.13.0; `WorkerExecution/WorkerAgentDispatcher.cs` uses `ChatClient.AsAIAgent`/`RunAsync`, explicit endpoints, protected credentials and disabled SDK retry/logging. This is evidence of installed synchronous conversation composition, not qualified streaming tool execution. The existing transport's bounded SSE/UTF-8 logic is useful but its candidate-only assembler cannot be reused unchanged.

**Rationale:** AD-1/24 place bounded conversations in MAF. An October provider adapter must expose text, fragmented tool calls and terminal reasons without candidate JSON. Native tool-call IDs/names/arguments are assembled within limits before any execution. MAF owns model continuation; Chat owns turn lifecycle and tool authority. No second hidden function-invocation loop or unbounded SDK retry is permitted.

**Alternatives considered:** A hand-written independent agent scheduler would duplicate MAF ownership. A final-answer-only wrapper fails live-stream acceptance. A new SDK/package upgrade is not part of this plan.

**Qualification stop:** If installed MAF cannot enforce the declared pre-call budgets/cancellation or expose truthful completion and text, stop at C1 and propose a reviewed adapter correction; do not silently upgrade packages or substitute buffered behavior.

## R4 — Explicit selected provider and shared admission

**Decision:** First implementation supports the selected October Chat profile using ChatCompletions with native tools and streaming. Integration supplies one explicit Web-side configuration source, binds its profile identity to the selected batch and decrypts credentials through existing protected services. Reject other API types/configuration mismatch as unavailable before dispatch.

**Evidence:** `Infrastructure/Configuration/OctoberProviderBinder.cs` validates Worker/Chat profiles, including multiple API types; it does not execute a provider. Legacy `ChatAgentProviderResolver.cs` uses System/default-provider selection, not this selected-batch binding. Existing transport executes ChatCompletions only.

**Rationale:** Do not infer that generator secrets are available in Web or silently fall back to a default. Provider binding and provider execution are separate claims. Shared provider admission means agreed policy with separately owned process-local Web/generator permits: Chat uses Web's pool; generator Business finalization uses Scan's pool. No new cross-process coordinator or global capacity guarantee is implied.

**Alternatives considered:** Implementing Responses/Messages adapters or failover broadens the slice. Runtime provider capability failure remains an explicit prerequisite failure, not proof that empty output is valid.

## R5 — One shared Knowledge authority

**Decision:** Consume B0 interfaces and fixtures after a compiled contract checkpoint. This feature documents consumer requirements, not competing Knowledge DTO declarations or persistence tables.

**Evidence:** October delivery minimum contract; OCT-BUSINESS brief; existing `Infrastructure/Data/October/OctoberDbContext.cs` contains setup data only. `IKnowledgeStoreRepository.GetCurrentAsync` is not an exact published-version read port.

**Rationale:** Fixed publication membership simplifies October consistency but never freezes permission. Reopen an authorized turn context against that same immutable publication for each turn. Switching publication creates a new empty conversation. Later-publication refresh within one conversation remains broader AD-25 scope.

**Alternatives considered:** Operator selector authorization excludes ordinary users; direct EF/legacy current reads bypass the owner; consumer-defined schemas create drift.

## R6 — Dialogue state and interruption

**Decision:** Chat owns domain lifecycle and persistence requirements; Business implements storage and migrations. A durable one-active-turn fence precedes model dispatch. Terminal save atomically stores text, references, dependencies and status. Ambiguous acknowledgement is resolved by the same operation key/payload, never generation.

**Rationale:** AD-26/27 and the October brief require real ordered history and safe completion. Host-owned work is bounded even if the browser disconnects; there is no same-turn stream reattachment in this slice. A returning page may read saved/current status without joining the stream. Process failure leaves unsaved provisional text unavailable; existing durable history survives and orphaned work becomes interrupted. No durable token journal is promised.

**Alternatives considered:** Circuit-local history loses results. Cancelling every disconnect contradicts preserved lifecycle semantics. Transparent restart or live reattachment exceeds the October cut.

## R7 — Disclosure authority follows all inputs, not the footer

**Decision:** Maintain a conservative union of all source/advisory contributors entering inference, including reused completed history and tool metadata/content. Before model transmission, each output batch, footer and saved-history disclosure, ask Knowledge's live policy to authorize that union. On denial/unavailability cancel affected work and suppress further material; never delete a footer entry and assume prose is safe.

**Rationale:** Unreported Documents can influence prose. The exact-read ledger qualifies references, while contributor lineage qualifies disclosure; they are distinct. Denied historic turns are omitted from inference and display as a generic unavailable placeholder without text/title/count leakage. If a tool newly broadens the union, validate it before dispatching that result to the model or releasing subsequent text.

**Alternatives considered:** Checking only displayed citations, initial authentication or current mode at prompt construction violates NFR-2.3 and AD-26.

## Research provenance

### Focused cross-lane correction, 2026-10-06

Read-only reference: `C:/Users/13728/.codex/worktrees/f081/vulgata/specs/20-1-Oct14-business/contracts/knowledge-contract.md`, Claim qualifications and consumer preservation, Consumer reads. F3 adopts Business's <=64-KiB exact reads with owner-issued protected content continuation, valid UTF-8, live authority and truthful chunk coverage, including an oversized single section. F4 preserves its material claim-group qualifications in streamed prose and reused history, using its lock-action/advisory-motive fixture. These complete consumer behavior; they add no alternate types, semantic gate or buffering. Aggregate budgets and pre-live-C1 dialogue persistence remain unchanged. Explore independently owns reader UI continuation. The provider-admission clarification is process-local policy sharing only.

Parent inspected October inputs, constitution, root instructions, templates, architecture/PRD and source. Independent read-only explorer `chat_research` inspected execution/provider/UI/test seams and confirmed the incompatibilities above. No additional skills invoked. Source-grounded design does not claim MAF streaming, live provider capability, B0 delivery or real usefulness has already passed.
