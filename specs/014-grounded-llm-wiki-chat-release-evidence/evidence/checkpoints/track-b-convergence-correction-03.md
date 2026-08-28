# Story 12.5 Track B convergence correction checkpoint 03

- Checkpoint ID: `bmad-12-5-implement-20260828T043341Z-convergence-03`
- Baseline correction checkpoint: `4b8366e16a7d50ae0c33f402de83101cbe1b5c42`
- Dependency commit: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Approval: `implementation-approval-track-b-01`
- Scope: Track B convergence tasks T055-T059 only
- Track C: closed

## Correction outcome

- Projection-first lexical Documents/sections remain primary through accepted-v2 Cross-Repository expansion; GENERATED_FROM and published Flow Group support are optional, while inconsistent persisted support fails closed.
- IT mode includes current TechnicalDetail/Evidence sections with deterministic mode, Document type, Repository, Logical Document, section-order and stable-ID ties.
- Render-time currentness carries and validates every packet item, completed Scan Runs, Profiled Revisions, selected scope, and explicit persisted Edge/decision/Flow Group authority.
- Packet-v2 wire serialization and fingerprint bind actual `edgeId`, depth, `exactDecisionId`, `flowGroupId`, and schema conversation fields.
- Primary and syntax-repair paths reject non-SSE aggregate responses; only raw `text/event-stream` transport may yield accepted provider deltas.

Original test owners verified retrieval 43/43, currentness 33/33, packet/contract 33/33, and dispatcher/raw streaming 40/40. Consolidated Track B verification passed 236 tests with 0 failures and 0 skips. Web build passed with 0 errors and existing warnings. Diff check passed with checkout line-ending notices only.

## Owned-path manifest

- `specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-03.md`
- `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs`
- `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatSupplementaryEvidenceSelector.cs`
- `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`
- `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs`
- `tests/Vulgata.Tests/ChatContractTests.cs`
- `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`
- `tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs`
- `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`
- `tests/Vulgata.Tests/ChatPageTests.cs`
- `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`

Explicit exclusions: EF DbContext, migrations, model snapshot, Story 12.4 contracts, `Program.cs`, project/central-package files, Track C tasks and release evidence.
