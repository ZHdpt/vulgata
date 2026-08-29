# Story 12.5 Track B quality correction plan 01

## Identity and boundary

- Source verdict: `reviews/quality-review/track-b-quality-review-03.json`
- Reviewed candidate: `a0eda652f01cb7b410a270783128d1eaefbd0ec1`
- Verdict: `REVISE`
- Accepted definition: unchanged `spec.md`, `plan.md`, and accepted architecture through AD-7B/AD-7C
- Correction scope: append-only Phase 11 tasks T055-T066 for `QR-TB-005` through `QR-TB-008`

The findings are implementation and behavioral-test gaps inside the accepted publication/reveal/composition design. They require no specification, plan, PRD, BMad authority, Story 12.4 contract, EF persistence, Track A, or Track C change.

## Verdict-to-task trace

| Finding | RED | Minimal GREEN | Owned paths | Exit proof |
| --- | --- | --- | --- | --- |
| `QR-TB-005` | T055 cancels after accepted commit before/during reveal and requires the visible answer, citations, and follow-up Conversation State to remain committed | T056 makes post-commit cancellation complete only the already accepted reveal; pre-commit cancellation remains closed | `tests/Vulgata.Tests/ChatPageTests.cs`; `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` | T063 focused page/publication regression |
| `QR-TB-006` | T057 covers throwing elapsed-time, exact deadline, cancellation, post-sample scheduling, concurrent terminal exchange, and owner-current composite authority | T058 removes fallible post-sample work and coordinator post-commit state mutation; the owner exchange activates the composite | `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`; `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`; `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`; `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs`; `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` | T063 owner/coordinator/integration regression |
| `QR-TB-007` | T059 proves prebound Unicode schedule cost, a fixed non-zero safety margin, equality/+margin refusal, and lag completion | T060 binds the directive before commit and lets the page consume it only for already accepted content | `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`; `tests/Vulgata.Tests/ChatPageTests.cs`; `tests/Vulgata.Tests/ChatValidatedRevealTests.cs`; `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs`; `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`; `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` | T063 reveal-budget regression |
| `QR-TB-008` | T061 requires typed non-optional recorder/owner dependencies and same-scope production identity | T062 removes object/null/default/direct-owner fallbacks and updates existing direct-construction fixtures | `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`; `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`; `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs`; `tests/Vulgata.Tests/ChatPageTests.cs`; `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`; `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` | T063 composition/constructor regression; `Program.cs` verified unchanged |

## Gate order and ownership

T055-T062 are strictly serial because their test and Web paths overlap. One named quality-correction test owner coordinates the existing B-INT-owned integration/composition test files; one named quality-correction production owner owns the Web corrections. `Program.cs` remains with the sole Integration Owner and is not an authorized correction write because the typed scoped registrations already exist.

After implementation, T063 records focused RED/GREEN and boundary evidence, T064 reruns independent convergence, T065 prepares a verdict-neutral candidate-bound quality input, and T066 obtains a fresh independent quality verdict. T040/T041 and Track C remain closed until the ordinary accepted downstream gates.

## Explicit exclusions

- no provider transport rewrite or non-streaming fallback
- no packet/retrieval/current-v2 relationship redesign
- no Track C attestation, E2E, Tier 2/3, release run, or screenshot work
- no `VulgataDbContext`, configuration, migration, or model-snapshot change
- no Story 12.4 or `specs/013-exact-cross-repository-flow-pass2` change
- no workflow-state, review-verdict, staging, commit, or Git-history mutation by this task-authoring pass
