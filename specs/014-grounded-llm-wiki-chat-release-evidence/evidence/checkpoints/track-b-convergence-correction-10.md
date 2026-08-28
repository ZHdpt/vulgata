# Story 12.5 Track B convergence correction checkpoint 10

- Checkpoint ID: `bmad-12-5-implement-20260828T232555Z-388814`
- Starting commit: `ea8fadadbdca5f55133d1119ce2cd8ad1b0092e2`
- Approval: `story-12-5-convergence-continuation-02`
- Scope: Track B convergence tasks T067-T070 only

## Delivered boundary

- T067 removes the retriever-owned projector/builder fallback and composes exactly one typed relationship projector and packet builder through scoped DI.
- T068 adds `ChatMode` to the typed projector seam and ranks/deduplicates the complete eligible Edge set by AD-4 before applying relationship and per-seed bounds.
- T069 validates complete accepted exact-decision sets from 1 through 256, including up to 65,536 contributions, while retaining strict selected Flow Group ownership/currentness without inventing one group per decision.
- T070 applies the same bounded server-owned Business Logic summary/advisory fallback when either Business or IT mode has zero positive primaries.

## TDD and verification

- T067 RED: two public retriever constructors exposed the fallback; GREEN composition/retrieval/currentness passed 94/94 after test-fixture migration.
- T068 RED: a higher-ranked Edge beyond the old cap lost and the projector lacked a mode parameter; GREEN exact tests passed 2/2 and retrieval/currentness passed 92/92.
- T069 RED: valid 11- and 256-decision sets returned missing/stale; GREEN exact tests passed 2/2 and retrieval/currentness passed 94/94.
- T070 RED: IT zero-positive primary with valid server advisory evidence returned no packet; GREEN focused passed 1/1 and retrieval/currentness passed 95/95.
- Root combined composition/retrieval/currentness verification: **99 passed, 0 failed, 0 skipped**.
- Web build: **0 errors** with 19 existing warnings.
- `git diff --check`: clean; informational line-ending notices only.

No EF/model/migration, project, central package, or Story 12.4 contract change is included. `Program.cs` changes only the coordinated scoped `ChatEvidencePacketBuilder` registration. Track C remains closed; T071-T072 are next.

## Explicit owned-path manifest

```text
specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md
specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-10.md
src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceRetriever.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs
src/dotnet/Vulgata.Web/Program.cs
tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs
tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs
tests/Vulgata.Tests/ChatPipelineCompositionTests.cs
```
