# Story 12.5 Track B B3 Reacceptance 01

- Observed at: `2026-08-29T08:05:19.8917156Z`
- Source commit: `f4ff035c84e011354d295e48b4827d7328698bf2`
- Branch: `demo0831/story-12-5`
- Tasks: T019-T022 (`EXISTING-EVIDENCE` / `VERIFY` only)
- Scope: independently reaccept bounded transient Conversation State, per-turn re-fetch and revalidation, and whole-answer semantic/evidence validation. B1 and B2 remain accepted prerequisites; B4 provider streaming/publication/reveal, B-INT, and Track C are not accepted here.

## Pinned boundary and evidence identity

The worktree was clean before verification. After all runs, the B3 production/test manifest had no diff from the pinned source commit; no production or test file was edited. The ordered B3 evidence-set SHA-256 is `1ee827611aefe8408113841cac5c0894f5c118f1320c9b004f3988ba9d80287b`. It is SHA-256 over UTF-8 lines `path=lowercase-file-sha256` for this ordered manifest:

| Path | SHA-256 |
|---|---|
| `src/dotnet/Vulgata.Core/DomainServices/ChatConversationContracts.cs` | `de593132988a2a8e8a24c485c5621d9d34154e50ba87e04268a04353a45690d8` |
| `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs` | `513beeb16de9eb7a8b3b1ef393792b52ff260c7d8eea09778c5e8773bce5ede4` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs` | `637b726a84022a5a69121fb4dd90cbe39e460e9f619dfe937a4079c2a66e04b6` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatSupplementaryEvidenceSelector.cs` | `23f14e1b330b66ea52b7c8f1840a442733c75bdcd9dea79004d372bad6c5590d` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs` | `8c8f06376bce2f5d5934e7c78aa4d073a0e73606c250f530d4dc803ec7b22d6f` |
| `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentResultValidator.cs` | `d6b7d0d46051bdb9b424ff696b90fe1ff0688df04ea3efa998240c47391cd751` |
| `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` | `5076c6e964d743b2583c38037c29cb29583729d2cc970b94af9de87a48c2224e` |
| `tests/Vulgata.Tests/ChatConversationStateTests.cs` | `93b3227aad2ef47a5e673e2ba314c2a961975baa1e2f5ab9d4fa44ccc51ac394` |
| `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs` | `1e51e1da25621915059edf7d9d161484987946568537a744c4d8f1598e727ca7` |
| `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` | `1ffb8e87035f6488a775008a98ee42b4ac13422c9e11ba1823b969d97cd0297a` |
| `tests/Vulgata.Tests/ChatAgentResultValidatorTests.cs` | `61b7a83cb17b87a4df9c101af70f91c3729e7c1c8be794f84eba84436080485e` |
| `tests/Vulgata.Tests/ChatContractTests.cs` | `a8d4eda792b695d8b615d3db8875275ef5ff021b9ba88826e987f92669bd948a` |
| `tests/Vulgata.Tests/ChatSupplementaryEvidenceSelectorTests.cs` | `6c5fadf656077fa41453ba5679dd961f69dbcec1e0a3f738ffc1af166b317bb6` |
| `tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs` | `9ab625ac1c5cf43b96dbedb7c6a64ec8e2a414d1383cc62532083e4219956c25` |
| `tests/Vulgata.Tests/ChatEvidencePacketV22ContractTests.cs` | `68e41cd4a90a80ea4c8f31c1649ffd97a0aaa0e8a2da4dbe36f386e164a6bd17` |
| `tests/Vulgata.Tests/ChatPageTests.cs` | `24f0d0e6acdc6b7d62e5b0e127a348726e79adb9085258dd8cc54a81148f7c6d` |

## Commands and results

Aggregate B1-B3 verification:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatPipelineCompositionTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatSupplementaryEvidenceSelectorTests|FullyQualifiedName~ChatAdvisoryContextReaderTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatEvidencePacketV22ContractTests|FullyQualifiedName~ChatCandidateExtractorTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatEvidenceCurrentnessTests|FullyQualifiedName~ChatConversationStateTests|FullyQualifiedName~ChatAgentResultValidatorTests" -p:WarningLevel=0
```

Result: **286 passed, 0 failed, 0 skipped** in 1 minute 23 seconds.

B3 conversation/retrieval/coordinator/validator verification against the built candidate:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~ChatConversationStateTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~ChatContractTests" -p:WarningLevel=0
```

Result: **133 passed, 0 failed, 0 skipped** in 1 minute 11 seconds.

Bounded-state and complete-result contract verification:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~ChatConversationStateTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~ChatContractTests" -p:WarningLevel=0
```

Result: **37 passed, 0 failed, 0 skipped** in 106 milliseconds.

Existing UI new-conversation reset regression:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~ChatPage_NewConversation" -p:WarningLevel=0
```

Result: **3 passed, 0 failed, 0 skipped** in 950 milliseconds.

Pinned drift and non-persistence guards:

```pwsh
git diff --exit-code f4ff035c84e011354d295e48b4827d7328698bf2 -- <ordered B3 production-and-test manifest>
rg -n "ChatConversationState" src/dotnet/Vulgata.Infrastructure/Data/Configurations src/dotnet/Vulgata.Infrastructure/Data/Migrations src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs -g "*.cs"
rg -n "AddScoped<PreparedChatCoordinator>|PreparedChatCoordinator" src/dotnet/Vulgata.Web/Program.cs src/dotnet/Vulgata.Web -g "*.cs"
```

Results: manifest diff empty; no EF configuration, migration, model, or `VulgataDbContext` mapping for Conversation State; production DI registers `PreparedChatCoordinator` as scoped and the scoped owner implements disposal reset.

## T019: bounded transient Conversation State

- Initial question plus two follow-ups retain only the last two complete validated turns in order; a fourth accepted turn evicts the oldest and never exceeds two turns.
- The canonical serialized state accepts exactly 4,096 UTF-8 bytes. The 4,097-byte case and a multibyte over-bound answer reject without truncation or mutation of the prior state.
- Every retained turn binds normalized question, complete validated answer, packet fingerprint, citation handles, and stable Logical Document identities. Retrieval context exposes only questions, handles, and Logical Document hints; prior answer prose and prior evidence bodies are absent.
- Explicit reset, mode change, Repository scope change, authorization loss, partial authorization loss, and partial current-version loss clear all retained turns. Existing page tests prove a new conversation clears UI/transient state and prevents late request/reveal content from reappearing. Scoped coordinator disposal calls the same conversation reset.
- Conversation State is an in-memory scoped Core/Web object only. It has no entity configuration, migration, `DbSet`, browser-storage, release-fixture, or persistence mapping.

## T020: every turn re-fetches and revalidates

- The initial question and two follow-ups each execute one evidence retrieval, one provider dispatch, and one post-selection currentness validation: observed totals are exactly three of each.
- The success security-order vector proves one pre-authorization, retrieval, dispatch, currentness validation, one final authorization, and only then citation materialization. The server-owned advisory vector proves exactly one initial advisory read and one reveal-time reread, ordered around retrieval/currentness.
- Conversation hints enter `ChatSupplementaryEvidenceSelector` only after `BuildRetrievalContext` rechecks every prior Logical Document against the current authorized/current sets. An eligible preferred Logical Document receives the fixed `+1` score; ineligible or removed prior evidence clears the complete state and returns the missing-equivalent `NotFoundOrUnauthorized` result with no packet.
- Retrieval binds the bounded conversation into packet 2.2 and changes the canonical fingerprint. A follow-up can use the prior validated question and currently eligible Logical Document identity for lexical selection, but removal of that current evidence causes missing-equivalent refusal rather than evidence carryover.
- Advisory mismatch, authorization/currentness loss, provider failure, semantic rejection, cancellation, and deadline failures return no answer and do not activate or replace live Conversation State. Subsequent requests prove failed questions are absent; changed mode/scope failures expose neither prior nor partial answers.

## T021: complete-result semantic and evidence barrier

- The validator binds request ID, mode, packet fingerprint, exact candidate handle, retained candidate identity/content/span, support signature, Source Anchor handles, order, and exact ordered equivalence between raw JSON selections and the typed result.
- Unknown, duplicate, missing, reordered, foreign-packet, mismatched-fingerprint, altered-content/span/support, restricted-support, locator-leaking Business text, mixed-valid/invalid, and extra-field selections reject the entire result. Rejection materializes zero claims and zero selected candidates.
- Accepted claims use only retained server candidate text and fixed citation/Source Anchor support. Prior answer prose is synthesis context only and cannot validate or supply a current claim.
- Coordinator coverage confirms semantic failure performs one dispatch only: it does not invoke syntax repair, publish a partial result, validate currentness as if accepted, or append a failed turn.

## Environment notes

The aggregate build emitted the existing `NU1507` warning for two configured package sources, `NU1900` warnings because vulnerability metadata endpoints were unavailable in the restricted environment, and `MSB3277` warnings for the existing EF Core 10.0.4/10.0.9 reference conflict. Compilation and every selected assertion completed successfully. Git also reported that the sandbox could not read the user-level ignore file; repository porcelain and explicit path diffs still completed.

## Acceptance boundary

T019-T022 can be marked complete. B3 is independently reaccepted at the pinned source commit without production or test drift. This checkpoint proves bounded transient Conversation State, per-turn re-fetch/revalidation, missing-equivalent revocation, complete-result validation, and failed-turn non-activation. It does **not** accept or claim B4 raw provider streaming, deadline-linearized publication, accepted-only reveal, B-INT composition, Track C real-LLM/restart/screenshot evidence, or Story 12.5 completion.
