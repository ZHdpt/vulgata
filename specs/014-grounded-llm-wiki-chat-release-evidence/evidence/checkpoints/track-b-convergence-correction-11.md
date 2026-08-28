# Story 12.5 Track B convergence correction checkpoint 11

- Checkpoint ID: `bmad-12-5-implement-20260828T235230Z-t071t072`
- Starting commit: `12f721cdcb3971108b7c0c356f32d8c7292ff6c4`
- Approval: `story-12-5-convergence-continuation-02`
- Scope: Track B convergence tasks T071-T072 only

## Delivered boundary

- T071 carries explicit optional-unit evidence rank into packet construction. Complete relationship/Flow Group units are dropped from lowest evidence rank first, followed by explicitly ranked non-strongest lexical extras; required evidence is never truncated and unreachable support is pruned.
- T072 re-reads the complete server-owned applied Global/System/two-Repository advisory Context after persisted-evidence currentness and before reveal. Value, `UpdatedAt`, membership, order, authority fingerprint, and applied/pending changes fail closed before any answer or citation is materialized.

## TDD and verification

- T071 RED: two public builder vectors showed handle-order rather than evidence-rank packing. GREEN packet suites passed **31/31** and retrieval/currentness suites passed **95/95**.
- T072 RED: six mutation vectors each observed only one advisory read. GREEN coordinator suite passed **39/39** and reveal/UI surroundings passed **30/30**.
- Root combined packet/retrieval/currentness/composition/coordinator/reveal/UI verification: **199 passed, 0 failed, 0 skipped**.
- Web build: **0 errors** with 19 existing warnings.
- `git diff --check`: clean; informational line-ending notices only.

No EF/model/migration, project, central package, `Program.cs`, or Story 12.4 contract change is included. Track C remains closed pending the one authorized fresh convergence pass and the remaining Track B review/E2E/verification gates.

## Explicit owned-path manifest

```text
specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md
specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-11.md
src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs
src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs
tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs
tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs
```
