# Story 12.5 Track B convergence correction checkpoint

- Checkpoint ID: `bmad-12-5-implement-20260828T031907Z-convergence`
- Baseline implementation checkpoint: `4127658dcdff6942f031c3dbd3600dc76fd507b4`
- Dependency commit: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Approval: `implementation-approval-track-b-01`
- Scope: Track B convergence tasks T051-T053 only
- Track C: closed

## Correction outcome

- T051: current authorized `IndexEntry` and `DocumentSection` projection-first lexical retrieval replaces the mandatory prepared relationship seed for non-flow questions; exact metadata outranks prose and existing deterministic bounds/fallback/no-legacy-authority coverage remains green.
- T052: bounded prior validated questions and current Logical Document hints influence selection only after re-fetch and authorization; prior evidence is never reused.
- T053: render-time `GENERATED_FROM` validation binds both persisted Edge Scan Run endpoints and their distinct Profiled Revisions.
- Consolidated Track B regression: 218 passed, 0 failed, 0 skipped.
- Web build: 0 errors; 19 existing warnings.
- Diff check: passed with checkout line-ending notices only.

## Owned-path manifest

Only these paths belong to this correction checkpoint:

- `specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-01.md`
- `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatSupplementaryEvidenceSelector.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/PreparedChatScopeResolver.cs`
- `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`
- `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`

Explicit exclusions: EF DbContext, migrations, model snapshot, Story 12.4 contracts, `Program.cs`, DI/project/central-package files, Track C tasks and release evidence.
