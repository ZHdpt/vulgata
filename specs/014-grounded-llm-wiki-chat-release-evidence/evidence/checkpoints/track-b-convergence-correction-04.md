# Story 12.5 Track B convergence correction checkpoint 04

- Checkpoint ID: `bmad-12-5-implement-20260828T052745Z-413249`
- Baseline correction checkpoint: `22dc0e5184445369dc52dd4ca80b661fb6c54c21`
- Dependency commit: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Approval: `implementation-approval-track-b-01`
- Scope: Track B convergence tasks T060-T064 only
- Track C: closed

## Correction outcome

- Projection-first lexical selection binds authorized Repository names, selects the strongest eligible section from each selected Document before extras, caps each Document at four sections, and preserves canonical ties.
- Currentness requires completed Scan Runs, matching Profiled Revisions, requested-System v2 decisions, and Source Anchor identity before a provider packet can be constructed.
- Bounded one-hop expansion supports same-Repository `REFERENCES` without fabricated exact authority, rejects duplicate related targets/Edges, and loads and validates every selected published Flow Group with its decision, Edge, Flow Trace, and ordered steps.
- Content-dark signed stream observations bind candidate/case/provider configuration and lifecycle metrics; strict streaming counts comments and unknown SSE fields, preserves post-terminal provider-error classification, and converts physical disconnects into one bounded signed rejection.

Original test owners verified evidence authority 73/73 and dispatcher/raw streaming 45/45. Consolidated Track B verification passed 252 tests with 0 failures and 0 skips. Web build passed with 0 errors and existing package/analyzer warnings. Diff check passed with checkout line-ending notices only.

## Owned-path manifest

- `specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/workflow-state.json`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/track-b-implementation.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-convergence-correction-04.md`
- `src/dotnet/Vulgata.Agents/ChatExecution/ChatProviderStreamAssembler.cs`
- `src/dotnet/Vulgata.Agents/ChatExecution/ChatStreamObservationAuthority.cs`
- `src/dotnet/Vulgata.Agents/ChatExecution/OpenAiCompatibleChatProviderStreamingTransport.cs`
- `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs`
- `src/dotnet/Vulgata.Infrastructure/Data/ChatSupplementaryEvidenceSelector.cs`
- `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`
- `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`
- `tests/Vulgata.Tests/ChatProviderStreamAssemblerTests.cs`
- `tests/Vulgata.Tests/ChatStreamObservationProvenanceTests.cs`
- `tests/Vulgata.Tests/ChatSupplementaryEvidenceSelectorTests.cs`
- `tests/Vulgata.Tests/OpenAiCompatibleChatProviderStreamingTransportTests.cs`

Explicit exclusions: EF DbContext, migrations, model snapshot, Story 12.4 contracts, `Program.cs`, project/central-package files, Track C tasks and release evidence.
