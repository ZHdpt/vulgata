# Story 12.5 Track B convergence correction checkpoint 12

- Checkpoint ID: `bmad-12-5-implement-20260829T021558Z-t073`
- Starting commit: `ae8f97dbfe40e6d286c934b24b67f2bda74b46ec`
- Approval: `story-12-5-convergence-continuation-03`
- Scope: Track B convergence task T073 only

## Delivered boundary

T073 enforces the accepted runtime `AuthorizedChatSnapshot` -> `ChatLexicalPrimaryProjection` -> `ChatRelationshipExpansionProjection` -> `ChatEvidenceAssembly` path. The authorized snapshot materializes current Document Versions, eligible sections plus `IndexEntry` metadata, every referenced section Source Anchor, advisory Context, exact two-Repository scope, and bounded conversation state inside the existing serializable transaction. Lexical selection is pure over that immutable snapshot. Relationship expansion consumes the snapshot and lexical projection, and the sole packet builder consumes only typed assembly.

The initial typed-wrapper attempt was not accepted because it constructed an empty snapshot and retained EF access inside the lexical projector. The final implementation removes that nominal bypass. Unrelated stale optional evidence does not poison lexical primary retrieval; selected malformed support still fails closed.

## TDD and verification

- Architectural RED: **0/3**, covering missing snapshot lexical seam, raw relationship arguments, and raw packet-build input.
- Architectural GREEN: **3/3**.
- Composition: **7/7**.
- Typed relationship focus: **4/4**.
- Retrieval: **48/48**.
- Currentness: **47/47**.
- Packet builder/schema 2.2: **31/31**.
- Root combined verification: **133 passed, 0 failed, 0 skipped**.
- Web build: **0 errors** with 19 existing warnings.
- `git diff --check`: clean; informational line-ending notices only.

No EF/model/migration, project, central package, Story 12.4 contract, or Track C change is included. `Program.cs` adds only the coordinated scoped lexical-projector registration.

## Explicit owned-path manifest

```text
specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md
specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-12.md
src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs
src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceRetriever.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatLexicalEvidenceProjector.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs
src/dotnet/Vulgata.Web/Program.cs
tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs
tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs
tests/Vulgata.Tests/ChatEvidencePacketV22ContractTests.cs
tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs
tests/Vulgata.Tests/ChatPipelineCompositionTests.cs
```
