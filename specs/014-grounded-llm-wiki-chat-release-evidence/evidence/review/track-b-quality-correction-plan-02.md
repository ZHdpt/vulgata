# Story 12.5 Track B quality correction plan 02

## Identity and boundary

- Source verdict: `reviews/quality-review/track-b-quality-review-04.json`
- Reviewed candidate: `ce9777bff62c10edb678a2eeea2e99bcc129f392`
- Task-authoring boundary: branch `demo0831/story-12-5` at `4e9f509adc598ad6664485b47bb5bbfd74e35329`
- Verdict: `REVISE`
- Finding: `QR-TB-009` only
- Earliest owner: implementation through public behavioral RED, minimal GREEN, and invalidated downstream gate reruns
- Accepted definition: unchanged `spec.md`, `plan.md`, and AD-6/AD-7B/AD-7C
- Correction scope: append-only Phase 13 tasks T068-T077

`QR-TB-009` is an implementation-isolation defect inside the accepted bounded transient Conversation State and sole publication-owner design. The user-visible `新对话` action clears page state and cancels active work, but the next same-mode/same-scope request can still read the prior accepted commit's Conversation State. Cancellation alone also cannot prevent a pre-reset candidate from becoming owner-current after reset. The correction requires no specification, plan, PRD, BMad authority, Story 12.4 contract, EF persistence, Track A, Track C, provider-transport, or retrieval redesign.

## Bounded correction design

### One owner-current conversation generation

`IPreparedChatPublicationOwner` remains the sole accepted-publication authority and gains the minimum reset/generation behavior needed to make AD-6's new-conversation discard boundary real:

1. The owner returns one atomic read snapshot containing the current conversation generation and the current published composite, if any.
2. The coordinator derives the request's starting Conversation State only from that snapshot. Scope/mode reset semantics and the existing re-fetch/revalidation path remain unchanged.
3. Every `PreparedChatPublicationCandidate` is immutably bound to the generation captured for that request. No candidate can be constructed for live use without that binding.
4. The explicit new-conversation command enters the same owner serialization boundary used by commit, advances the generation, and clears the owner-current composite as one operation.
5. `Commit` compares the candidate generation to the owner-current generation inside that same serialization boundary and before any accepted terminal exchange. A mismatch closes the candidate as non-published using bounded cancellation/reset semantics.
6. Once reset has advanced the generation, a pre-reset candidate cannot activate or reactivate owner-current state on its first, repeated, delayed, or concurrently scheduled commit call.
7. Only a candidate from the current generation may reach the existing `AcceptedPublicationCommit`. The accepted answer, proposed Conversation State, completion summary, and reveal directive remain one atomic composite.

The owner-current generation is transient scoped server state. It is not persisted, logged as conversation content, exposed to the provider/client, added to Track B telemetry, or projected into Track C evidence.

### Page and race ordering

`ChatPage.StartNewConversationAsync` routes the explicit `新对话` action through the coordinator to the publication owner before completing the existing page-only clear/cancellation sequence. The page version still prevents old markup from reappearing, but it is presentation protection rather than Conversation State authority.

The correction must close all three observable paths:

- after one accepted turn, `新对话`, then the next same-mode/same-scope question, retrieval receives an empty Conversation State;
- when reset races a request that has captured the old generation but has not committed, the old candidate remains non-published and the next request receives empty Conversation State;
- when reset occurs during progressive reveal of already accepted content, late reveal work cannot restore page content or owner-current Conversation State, and the next request receives empty Conversation State.

Accepted-only reveal is preserved: reset may discard the current conversation by explicit user action, but provider fragments or unvalidated content never become visible. Mandatory primary/repair streaming, packet validation, final authorization/currentness/advisory reread, deterministic re-fetch, and the fixed Logical Document hint behavior are unchanged.

## Public RED and minimal GREEN trace

| Finding | Public RED | Minimal GREEN | Owned paths | Exit proof |
| --- | --- | --- | --- | --- |
| `QR-TB-009` accepted-turn reset | T068 accepts a turn, invokes `新对话`, asks again in the same mode/scope, and inspects an empty retrieval Conversation State | T072-T073 route reset through the owner generation boundary and page command | `tests/Vulgata.Tests/ChatPageTests.cs`; `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`; `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`; `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs` | T074 focused page/coordinator/publication regression |
| `QR-TB-009` in-flight and reveal races | T069-T070 hold an old-generation request/candidate or accepted progressive reveal across reset, then prove no old activation/reactivation and empty next-request Conversation State | T072 makes reset/generation/commit one owner-serialized boundary; T073 preserves page-version presentation protection while invoking it | same Web paths plus `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs` and `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs` | T074 deterministic race matrix |
| `QR-TB-009` sole authority/composition | T071 requires every candidate and current-conversation read to use the same owner generation and rejects a coordinator-owned shadow authority or alternate reset path | T072 removes the coordinator-local publication authority and binds candidate creation/current reads/reset to the scoped owner | `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`; `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`; `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs`; two Web data paths | T074 B-INT/composition and no-second-owner guards |

## Ownership and gate order

The root assigns one named **QR-TB-009 Test Owner** for T068-T071 and one named **QR-TB-009 Production Owner** for T072-T073. Because `ChatPageTests.cs`, `PreparedChatCoordinatorTests.cs`, `PreparedChatPublicationOwnerTests.cs`, `ChatPipelineCompositionTests.cs`, `ChatPage.razor.cs`, `PreparedChatCoordinator.cs`, and `PreparedChatPublicationOwner.cs` overlap semantically and structurally, T068-T073 are strictly serial and are not parallelizable. The existing B-INT Interaction Test Owner coordinates the `ChatPipelineCompositionTests.cs` write; no second worker edits it concurrently.

After GREEN, T074 records focused verification, T075 reruns convergence, T076 prepares a verdict-neutral quality input, and T077 obtains a fresh independent quality review. E2E, final Track B verification, T040/T041, and Track C remain closed until the fresh quality verdict and ordinary downstream gates permit them.

## Explicit preservation and exclusions

- preserve full evidence re-fetch and currentness/authorization/advisory revalidation on every request;
- preserve prior validated answers as context only and the fixed eligible current Logical Document hint boost;
- preserve whole-answer validation, accepted-only Unicode reveal, reduced-motion/immediate reveal, and no provider-fragment client path;
- preserve mandatory strict streaming for primary and syntax repair under the shared deadline;
- no new persistence, EF model/configuration/migration/snapshot, saved Chat session, browser storage, or durable generation;
- no Story 12.4 or `specs/013-exact-cross-repository-flow-pass2` change;
- no Track C attestation, E2E, Tier 2/3, release run, screenshot, fake Backup B, or evidence-pass claim;
- no `Program.cs`, Agents transport, Infrastructure retrieval/currentness, packet contract, spec, plan, PRD, BMad authority, workflow-state, staging, commit, or Git-history change in this authoring pass.

## Authorization disposition

No new authorization trigger applies. This is the minimum implementation correction needed to satisfy the already accepted AD-6 new-conversation discard rule and AD-7C sole publication authority. It adds no Agent tool, provider-callable capability, Scan strategy, PRD change, second publication owner, or architecture expansion. The existing accepted implementation authority remains binding; the correction must rerun convergence and independent quality review because production/test changes invalidate those gates.
