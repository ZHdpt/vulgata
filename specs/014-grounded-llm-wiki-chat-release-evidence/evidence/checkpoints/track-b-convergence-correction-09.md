# Story 12.5 Track B convergence correction checkpoint 09

- Checkpoint ID: `bmad-12-5-implement-20260828T160816Z-5e73a6`
- Starting commit: `c1c584172db32fb45a6712c02c8d275a4e3876b6`
- Approved boundary: Track B implementation approval 02
- Scope: Track B convergence tasks T064-T066 only
- Convergence source: `track-b-redesign-02.md`

## Delivered boundary

- T064 scores and deduplicates the full current eligible lexical set, then retains the deterministic highest-ranked 256-section candidate window; the stale 257-row refusal is removed.
- T065 cuts the runtime packet to the single accepted `grounded-chat-evidence/2.2` shape, including lexical primary/seed identities, server-owned advisory Context authority, selected exact-decision set authority, decision-owned Flow Group bindings/ranks, grouped Flow Steps, prior Logical Document IDs, canonical fingerprinting, complete-unit packing, and direct JSON Schema conformance.
- T066 replaces Track B candidate/case/signature coupling with release-neutral content-dark runtime observations and a separate coordinator-owned completion summary. Candidate-bound sealed signed attestation remains absent and Track C-only.
- No EF model/migration, Story 12.4 contract, project file, central package, or `Program.cs` change is included.

## TDD and compatibility evidence

- T064 RED: a late highest-ranked section beyond the old 256 query boundary returned `IncompleteOrStale`; GREEN retrieval regression passed 45/45.
- T065 RED: runtime packet remained schema 2.0 and omitted accepted 2.2 identities; six direct contract vectors turned GREEN, the complete serialized runtime packet validates against `chat-evidence-packet-v2.schema.json`, and packet/candidate regression passed 44/44.
- T065 surrounding RED: reversed equivalent Flow Group input changed handles/fingerprint; deterministic Edge/decision/trace/group ordering closed it. Invalid legacy fixtures with ungrouped Flow Steps or non-decision-owned same-Repository Flow Groups were migrated to valid 2.2 authority without weakening assertions.
- T066 RED: five composition/leakage checks exposed candidate/case fields, absent neutral runtime/completion types, missing coordinator composition, and the legacy signed Track B authority; migrated production and surrounding tests passed 104/104.

## Verification

- Corrected combined retrieval/packet/currentness/streaming/coordinator/UI suite: **269 passed, 0 failed, 0 skipped**.
- Full accepted Track B matrix: **337 passed, 0 failed, 0 skipped**.
- Web build: **0 errors**; 19 known NU1507/NU1900/MSB3277 warnings remain.
- Workflow validator: passed before checkpoint state refresh.
- `git diff --check`: clean; informational LF-to-CRLF notices only.

Track C T054-T062 remains closed. A fresh convergence pass is next; quality review, E2E decision, pinned verification, and the bound complete Track B checkpoint remain mandatory before conditional Track C approval activates.

## Explicit owned-path manifest

```text
specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md
specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/convergence/track-b-redesign-02.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-09.md
specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md
src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs
src/dotnet/Vulgata.Core/DomainServices/ChatStreamingContracts.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs
src/dotnet/Vulgata.Infrastructure/Data/ChatSupplementaryEvidenceSelector.cs
src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs
src/dotnet/Vulgata.Agents/ChatExecution/ChatStreamObservationAuthority.cs
src/dotnet/Vulgata.Agents/ChatExecution/OpenAiCompatibleChatProviderStreamingTransport.cs
src/dotnet/Vulgata.Agents/DependencyInjection.cs
src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs
tests/Vulgata.Tests/ChatAgentDispatcherTests.cs
tests/Vulgata.Tests/ChatAgentResultValidatorTests.cs
tests/Vulgata.Tests/ChatCandidateExtractorTests.cs
tests/Vulgata.Tests/ChatContractTests.cs
tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs
tests/Vulgata.Tests/ChatEvidencePacketV22ContractTests.cs
tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs
tests/Vulgata.Tests/ChatPageTests.cs
tests/Vulgata.Tests/ChatStreamObservationProvenanceTests.cs
tests/Vulgata.Tests/OpenAiCompatibleChatProviderStreamingTransportTests.cs
tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs
```
