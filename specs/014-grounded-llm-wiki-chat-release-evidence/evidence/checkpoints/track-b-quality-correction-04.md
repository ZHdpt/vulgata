# Story 12.5 Track B quality correction checkpoint 04

- Observed at: `2026-08-29T12:59:32.3862591Z`
- Branch: `demo0831/story-12-5`
- Parent checkpoint: `1513747666ba8c5f8f05b1c99e340251f9ab2781`
- Tasks: T068-T074
- Finding: QR-TB-009

## RED evidence

The sole test owner recorded the following serialized public/owner REDs before production mutation:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ChatPage_NewConversationAfterGroundedTurn_ResetsBackendConversationForIdenticalScope" --logger "console;verbosity=minimal"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ChatPage_NewConversationDuringInFlightCommit_RejectsStalePublicationAndNextRequestIsEmpty|FullyQualifiedName~Commit_CandidateCapturedBeforeReset_CannotPublishOrReactivate" --logger "console;verbosity=minimal"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ChatPage_NewConversationDuringProgressiveReveal_DropsLateRevealAndCommittedConversationContext" --logger "console;verbosity=minimal"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~Commit_PreResetCandidate_FirstRepeatedAndConcurrentCallsRemainClosedAndNonCurrent|FullyQualifiedName~PublicationCandidate_BindsImmutableConversationGeneration|FullyQualifiedName~PreparedChatCoordinator_UsesPublicationOwnerForConversationAuthorityWithoutLocalShadowState|FullyQualifiedName~ProductionComposition_Story125T071_UsesOneOwnerGenerationResetBoundary" --logger "console;verbosity=minimal"
```

| Task | RED result |
|---|---|
| T068 | 1 failed: the next identical-scope retrieval retained the prior validated turn and packet fingerprint. |
| T069 | 2 failed: a pre-reset candidate published after reset, and the next retrieval retained the prior turn. |
| T070 | 1 failed: late reveal stayed visually suppressed, but the next retrieval retained the pre-reset turn. |
| T071 | 4 failed: stale concurrent commit published; candidate lacked generation binding; coordinator retained shadow authority; owner lacked atomic snapshot/reset. |

## GREEN implementation

`PreparedChatPublicationOwner` is now the sole scoped conversation/publication authority:

- `CaptureSnapshot()` atomically returns generation plus the current published composite.
- `Reset()` serializes generation advance and current-clear on the same gate as commit.
- every candidate is immutably bound to the captured owner snapshot/generation;
- first, repeated, delayed, or concurrent commits bound to an old generation fail closed;
- production commit rejects a candidate bound to another owner authority;
- the interface requires `CaptureSnapshot`, `Reset`, and `Commit`; no default compatibility authority or `object` fallback exists.

`PreparedChatCoordinator` derives Conversation State only from the owner snapshot, resets the owner when scope changes, binds candidates to the captured generation, exposes the owner-backed explicit reset seam, and owns no `PreparedChatPublicationAuthority` field. `ChatPage.StartNewConversationAsync` calls that reset seam before page-version advance, cancellation, delayed clearing, notice, and focus.

## Verification

- focused reset/owner/page slice after sole-owner hardening: **18/18 passed**;
- seven affected suites excluding only two sandbox default-key-path tests: **140/140 passed**;
- full bounded Track B/B-INT/mandatory-streaming matrix with the same two exclusions: **436/436 passed**, 0 skipped, in 1 minute 34 seconds;
- Web build: **0 errors**, 19 existing NU1507/NU1900/MSB3277 warnings;
- `git diff --check`: clean apart from informational LF-to-CRLF notices;
- the two excluded Data Protection characterizations remain environment-only `UnauthorizedAccessException` failures under `C:\Users\13728\AppData\Local\ASP.NET\DataProtection-Keys`; controlled key-ring vectors pass.

No `Program.cs`, project file, EF model/configuration/migration/snapshot, Infrastructure retrieval/currentness, Agents transport, Story 12.4 contract/artifact, sprint status, or Track C path changed.

## Owned-path hashes

| Path | SHA-256 |
|---|---|
| `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` | `9e73079c94d7b481414e5ac5751abc8f310a90428eb6e36374517a828dfe5b3e` |
| `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` | `88be8cebc123a2418da355c721833da5db019e02172abe2a3fb1c988268ac8bc` |
| `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs` | `afc276b4b5516e270b8f53f5d70730e5f2acc4251bb7b9d69de8f03b0ce89ddb` |
| `tests/Vulgata.Tests/ChatPageTests.cs` | `b26ce5e1f43378a2992a450948a8784edb280d821f3f3d9c1dd7df3fdc6dda64` |
| `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` | `0c9c22292e7158b7a6c6e2018d0bfca5c9fec2d592abd9bfc57703effab457d3` |
| `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs` | `5b55edf3ecad4905f2448845c07896d2e43f33d2e5cd1617a97d3a273d3805f2` |
| `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` | `90f923133ac1d2f5dfc2cc5927be03b42d3c2808949ccfacce1e56568c9557f4` |
| `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs` | `5689d249121c36a3cc3ec1c1fb7fa968d06124d0a3cf5b25b12cf70eda0f2395` |

## Verdict

T068-T074 are complete. QR-TB-009 is correction-ready for mandatory independent convergence T075. This checkpoint does not claim convergence, quality acceptance, E2E acceptance, final Track B verification, Track C readiness, or Story completion.
