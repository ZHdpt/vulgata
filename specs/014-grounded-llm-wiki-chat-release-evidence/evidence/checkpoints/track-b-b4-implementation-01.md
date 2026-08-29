# Story 12.5 Track B B4 Implementation 01

- Observed at: `2026-08-29T08:44:44.9159817Z`
- Source commit: `29cf72d4daa55e8c4d6eb19ba5054eea77bf91fe`
- Branch: `demo0831/story-12-5`
- Checkpoint ID: `bmad-12-5-implement-20260829T084444Z-b4`
- Tasks: T023-T032
- Scope: independently accept mandatory raw provider streaming, one deadline-linearized Web publication owner, atomic answer/Conversation State/completion-summary/reveal authority, and accepted-only Unicode presentation. B-INT and Track C remain unaccepted.

## Honest RED

The public B4 seam was tested before production mutation:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~PreparedChatPublicationOwnerTests|FullyQualifiedName~ChatValidatedRevealTests|FullyQualifiedName~PreparedChatCoordinatorTests.PreparedChatCoordinator_Story125B4|FullyQualifiedName~ChatPipelineCompositionTests.PublicationBoundary_Story125B4" --logger "console;verbosity=minimal"
```

Result: **16 total; 13 expected RED; 3 characterization passes**. The REDs proved the absence of `IPreparedChatPublicationOwner`, the immutable publication candidate, one terminal commit, coordinator delegation to the owner, and accepted-commit-only client reveal. A premature DI-resolution assertion was removed before GREEN because T035 reserves `Program.cs` composition for B-INT; its replacement asserts only the public sole-owner structure.

## Minimal GREEN and publication transition

- `PreparedChatPublicationOwner` is the single Web owner. A preallocated candidate contains the immutable Grounded Answer, proposed next Conversation State, neutral completion template, reveal directive, packet/advisory authority, and monotonic start/deadline tokens.
- The synchronous critical section observes caller cancellation, deadline cancellation, and one monotonic timestamp. Cancellation or `sample >= deadline` closes without publication. Otherwise it writes only primitive timestamp/duration values and performs one terminal transition to `AcceptedPublicationCommit`.
- Time advancing after the accepted transition cannot retroactively revoke it. No later clock read, callback, asynchronous continuation, logging, or telemetry emission participates in acceptance.
- `PreparedChatCoordinator` builds candidate-only Conversation State with `AppendValidatedTurn`, delegates once, and obtains follow-up Conversation State only from the committed composite. Failed or expired candidates do not activate live state.
- `ChatPage` and `ChatTurn` obtain content only from the committed reveal directive. Immediate and progressive display operate only on the complete accepted answer; progressive reveal remains Unicode text-element based and schedule lag/reduced motion may expose only already accepted content.
- Completion-summary preparation remains before publication. Accepted completion authority is exposed through `AcceptedPublicationCommit.CompletionSummary`; the stale test requiring direct coordinator accepted-summary ownership was corrected to the regenerated design.
- The committed completion-summary authority is also preallocated. Its content-dark fields are immutable, accepted duration is its sole directly written primitive, repeated reads are reference-identical, and no reflection, delegate construction, or post-commit summary allocation remains.

Focused GREEN after correcting the fake clock to advance both UTC and monotonic time and closing the stable-summary allocation seam: **118 passed, 0 failed** across publication owner, coordinator, page, turn, conversation, reveal, composition, and observation-provenance suites. The exact B4 public seam passed **17/17**.

## Mandatory streaming and neutral telemetry reacceptance

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ChatProviderStreamAssemblerTests|FullyQualifiedName~OpenAiCompatibleChatProviderStreamingTransportTests|FullyQualifiedName~ChatAgentDispatcherTests"
```

Result: **73 passed, 0 failed**. Primary and the single syntax repair use the raw streaming adapter with the same 29-second deadline and packet fingerprint. Strict split UTF-8/SSE/JSON, one `[DONE]` plus physical EOF, ten-second decoded-data inactivity, exact/+1 received Provider Chunk and assembled-response caps, separate raw/SSE/received/decoded/final domains, whole-buffer discard, zero-output rejection, and no aggregate accepted fallback remain green.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ChatStreamObservationProvenanceTests|FullyQualifiedName~ChatPipelineCompositionTests"
```

Result: **14 passed, 0 failed**. Runtime observations and completion summaries are immutable and content-dark; candidate/run/case/key/signature/export/content fields are absent. Production selects `OpenAiCompatibleChatProviderStreamingTransport`, accepts no SDK aggregate fallback, and composes no Track C attestation sink.

`PreparedChatIntegrationTests` produced **2 configured-key-ring passes** and **2 environment-only failures** because the default ASP.NET Data Protection provider could not write `C:\Users\13728\AppData\Local\ASP.NET\DataProtection-Keys\*.tmp` in the restricted sandbox. The paired repository-controlled key-ring vectors passed, so the host-path denial is not accepted as a B4 product failure.

## Aggregate verification

The final aggregate B1-B4 matrix passed **355/355** in 1 minute 25 seconds. It covered lexical and relationship projections, packet 2.2, advisory Context, currentness, bounded Conversation State, whole-answer validation, raw streaming, publication, reveal, and Track B telemetry.

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore -p:WarningLevel=0
```

Result: **0 errors, 19 existing warnings**. `git diff --check` is clean apart from informational LF-to-CRLF notices. Existing NU1507, NU1900, and EF assembly-version warnings are unchanged.

## Owned production/test manifest

| Path | SHA-256 |
|---|---|
| `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs` | `20904e4313dba45a636b36fc813ce10014e01a9c7a5072e390e6bcf615e2a7d5` |
| `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` | `4288a7fffbc0749b493591a307ba03435c4d70fb503f818e2c45f860b9ac364e` |
| `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` | `0c41c478a797925f3dc7256cd0aacce2ff3b1980edae0564398d21ba047eaef1` |
| `src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs` | `d7bd1bed39c893086dbba27460a748157a87a8c7fc0fae0536b4375b116b88d2` |
| `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs` | `741f91e9e75b6951aa9e9fcd9b6b6790ef74f7246080d9b3952218cc623bf6fc` |
| `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs` | `85ea55225917a38887b7f630c706873b9d7494274fc278c42a2b91a34b218a07` |
| `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` | `9620afbf5c065fada934d2f646788df7fcb2e54ff0c1a025a5b401c49320652d` |
| `tests/Vulgata.Tests/ChatValidatedRevealTests.cs` | `2c711cdea43a4763c34caf19e12b836c0b0a2d26e457247164be011fa65c08ba` |
| `tests/Vulgata.Tests/ChatStreamObservationProvenanceTests.cs` | `b380dee2501cb9addc6a1b0d8ef9b7e5cec0fa5a3e8b5dc3ce9cc8b40acc0158` |

## Acceptance boundary

T023-T032 are independently accepted. This checkpoint does not claim exact production B1-B4 DI interaction, Track B completion, convergence, quality review, E2E acceptance, Track C attestation, real-LLM qualification, restart proof, screenshots, or release evidence. T033-T037 B-INT is the next boundary. T035 remains the sole owner of `Program.cs` and any required Agents DI change.
