# Story 12.5 Track B convergence correction checkpoint 07

- Checkpoint ID: `bmad-12-5-implement-20260828T081608Z-08a69a`
- Baseline correction checkpoint: `f17cd6ec474e0d759b879fce242ec30c8fe5c2ad`
- Dependency commit: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Approval: `implementation-approval-track-b-01`
- Convergence reset: `story-12-5-convergence-reset-01`
- Scope: Track B convergence tasks T075-T079 only
- Track C: closed pending accepted Track B

## Correction outcome

- Projection retrieval no longer lets an unrelated stale Cross-Repository Edge poison an otherwise authorized packet; exact authority is checked against the current per-Repository projection boundary.
- Every selected Published Flow Group must materialize a complete trace/step set before packet construction; empty selected groups fail closed.
- Split CRLF SSE terminators count both raw wire bytes while preserving the established LF and CR behavior.
- The adapter-owned raw-body cap overflow has its closed `raw-body-overflow` classification, while ordinary I/O failures remain provider disconnects.
- A parsed but unsupported non-answer records `rejected-semantic` against the actual stream attempt before deterministic rescue or unsupported mapping.

Original test owners verified retrieval/currentness 73/73 and dispatcher/streaming/coordinator 86/86. Consolidated Track B verification passed 273 tests with 0 failures and 0 skips. Web build passed with 0 errors and existing package/analyzer warnings. Diff check passed with checkout line-ending notices only.

## Owned-path manifest

- `specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-07.md`
- `src/dotnet/Vulgata.Agents/ChatExecution/OpenAiCompatibleChatProviderStreamingTransport.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`
- `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`
- `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`
- `tests/Vulgata.Tests/OpenAiCompatibleChatProviderStreamingTransportTests.cs`
- `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`

Explicit exclusions: EF DbContext, migrations, model snapshot, Story 12.4 contracts, `Program.cs`, project/central-package files, Track C tasks and release evidence.
