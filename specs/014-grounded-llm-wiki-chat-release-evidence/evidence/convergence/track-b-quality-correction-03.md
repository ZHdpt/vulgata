# Story 12.5 Track B quality-correction convergence 03

- Outcome: **CONVERGED**
- Candidate: `9766f8aa740532d3d754c97d64ea65c474f0c836`
- Branch: `demo0831/story-12-5`
- Feature: `specs/014-grounded-llm-wiki-chat-release-evidence`
- Scope: accepted specification, plan, tasks through T074, Constitution, and the corrected Track B implementation.
- Dirty paths at observation: none.

## Authority set

The unchanged accepted artifacts were checked against the implementation:

| Artifact | SHA-256 |
| --- | --- |
| `spec.md` | `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4` |
| `plan.md` | `fad42dcd3a3f393809e95500ea6d6c7607ce4a16787d8b16d054b415ce533318` |
| `tasks.md` (T001-T077, T075 pending) | `cf0eaad100f7639dd9ef04ada049230cfcb1abb66a9f9fda4bbbc3ac3ff54cf2` |
| `evidence/checkpoints/track-b-quality-correction-04.md` | `5b67e4c3cda0a17c7fc82844323334d2600708973c7587fea9baf182bcc675fc` |

The tasks hash was copied from the observed `Get-FileHash` result and is retained verbatim for traceability.

## Findings summary

| ID | Gap type | Severity | Source | Result |
| --- | --- | --- | --- | --- |
| — | — | — | FR-001-FR-060, SC-001-SC-017, AD-1 through AD-12, Constitution I-V, T001-T074 | No actionable gap found. |

Checked 60 functional requirements, 17 buildable success criteria, all five user-story acceptance sets and edge cases, the accepted plan decisions and constraints, Constitution principles I-V, and every completed task through T074. No `missing`, `partial`, `contradicts`, or `unrequested` finding remains. No intent or authority defect requires return to specification, plan, or task definition.

## QR-TB-009 correction proof

- `PreparedChatPublicationOwner` is the sole scoped authority. Its required `CaptureSnapshot`, `Reset`, and `Commit` operations share one serialized authority and expose one `(generation, current published composite)` snapshot (`src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs:401-419`).
- A candidate carries an immutable `ConversationGeneration` and the captured authority (`.../PreparedChatPublicationOwner.cs:333-350`). `Commit` rejects a candidate from another authority or a stale generation before any accepted transition (`.../PreparedChatPublicationOwner.cs:431-434`).
- `Reset` advances generation and clears current state under the same owner gate (`.../PreparedChatPublicationOwner.cs:210-218`), so first, repeated, delayed, and concurrent pre-reset commits cannot publish or reactivate. The focused owner tests cover these cases (`tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs:176-222`).
- `PreparedChatCoordinator` reads and resets only through the injected owner, captures the snapshot used for retrieval and candidate construction, and has no coordinator-local `PreparedChatPublicationAuthority` or live `ChatConversationState` field (`src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs:222-231, 631-646`; `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs:24-86`; `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs:220-254`).
- `ChatPage.StartNewConversationAsync` calls `ResetConversation` before page-version/cancellation/clear/notice/focus work (`src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs:163-184`). The page tests prove accepted-turn reset, in-flight stale publication rejection, progressive-reveal suppression, and empty next-request state (`tests/Vulgata.Tests/ChatPageTests.cs:153-254, 890-934`).

## Preservation proof

The T074 checkpoint records the required RED-to-GREEN evidence and the corrected implementation boundary. It reports 18/18 reset/owner/page tests, 140/140 affected suites with only the two controlled Data Protection exclusions, and 436/436 for the bounded Track B/B-INT/mandatory-streaming matrix. Those results cover current-v2 relationship/Flow Group authority, deterministic packet/fingerprint and citation validation, re-fetch/currentness/authorization, strict raw streaming and failure caps, accepted-only reveal, typed composition, and no second owner or Track C sink.

Independent rerun on this candidate:

```text
dotnet test ... --filter "ChatPageTests|PreparedChatPublicationOwnerTests|PreparedChatCoordinatorTests|ChatPipelineCompositionTests|ChatValidatedRevealTests|PreparedChatIntegrationTests|ChatStreamObservationProvenanceTests|ChatEvidenceRetrievalTests|ChatEvidenceCurrentnessTests|ChatEvidencePacketBuilderTests|ChatEvidencePacketV22ContractTests|ChatCandidateExtractorTests|ChatAgentDispatcherTests|ChatProviderStreamAssemblerTests|OpenAiCompatibleChatProviderStreamingTransportTests|ChatAgentResultValidatorTests|ChatContractTests|ChatConversationStateTests"
Result: 397 passed, 0 skipped, 2 failed.
```

The two failures are `ProductionComposition_KeyRingPathWithoutApplicationName_PreservesDefaultsAsync` and `ProductionComposition_ApplicationNameWithoutKeyRingPath_PreservesDefaultsAsync`; both fail only because this sandbox denies writes to `C:\Users\13728\AppData\Local\ASP.NET\DataProtection-Keys`. They are environment-only failures documented by T074; controlled key-ring vectors pass and neither failure implicates Track B behavior.

Additional verification: `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore -p:WarningLevel=0` completed with 0 errors and 19 existing NU1507/NU1900/MSB3277 warnings; `git diff --check` passed; `validate_workflow.py --repo .` reported workflow configuration and contracts valid. No EF/model/migration/snapshot, Program/DI, Story 12.4, Track A, or Track C changes are present in the candidate.

## Handoff

Convergence is clean. T075 requires no appended tasks. The candidate may proceed to the existing independent quality-review input (T076); this report does not grant quality, E2E, final-verification, or Track C acceptance.
