# Story 12.5 Track B integration verification 01

## Identity and boundary

- Verification task: T036 (`VERIFY`)
- Branch: `demo0831/story-12-5`
- Pinned committed base observed before verification: `65ffe6986217849105b7a4493182ff835932f0a8`
- Accepted Story 12.4 dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Story 12.4 ancestry: `git merge-base --is-ancestor 4798b97d9f23aef587829175df7ab7ebc3daf995 HEAD` exited `0`
- Story 12.4 completion trailer: `Workflow-Checkpoint: bmad-12-4-complete-20260827T132524Z-d8e2d1`
- Candidate working diff hash: `39f2fd91012e8e7f4015c293cf4b42b1ff38085e`
- Observed owned implementation boundary before this evidence file: `src/dotnet/Vulgata.Web/Program.cs`, `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs`, and `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`
- This verification did not run Tier 2/3 PostgreSQL, CodeGraph, JDT LS, provider, restart, screenshot, or release qualification and did not touch a Story 12.4 worktree or database.

Accepted prerequisite evidence hashes:

| Boundary | SHA-256 |
|---|---|
| `evidence/checkpoints/track-b-b1-reacceptance-01.md` | `475ad6061a677705e9825e6b9379f08cc8c6c0aec75a99173829b4324e60f139` |
| `evidence/checkpoints/track-b-b2-reacceptance-01.md` | `4056a0f6a4f55bf7630e05df24510357613d47dd4b0478dddab4432d84754070` |
| `evidence/checkpoints/track-b-b3-reacceptance-01.md` | `9284a2c28fd5738ecb836fdfc3bccf9684e351fed11315fda53928bc0c62aa2c` |
| `evidence/checkpoints/track-b-b4-implementation-01.md` | `bdba3e0ab2f1e1539274dd4a4b749aa01a435aead81d801c76debfe44cf52c33` |

## Focused B1-B4 and B-INT regression

The complete focused command selected the B1 lexical/packet, B2 relationship/currentness, B3 conversation/validation, B4 raw transport/publication/reveal, UI, composition, and authenticated integration classes:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatConversationStateTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatEvidencePacketV22ContractTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatEvidenceCurrentnessTests|FullyQualifiedName~ChatSupplementaryEvidenceSelectorTests|FullyQualifiedName~ChatCandidateRankerTests|FullyQualifiedName~ChatCandidateExtractorTests|FullyQualifiedName~ChatAdvisoryContextReaderTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatProviderStreamAssemblerTests|FullyQualifiedName~OpenAiCompatibleChatProviderStreamingTransportTests|FullyQualifiedName~ChatStreamObservationProvenanceTests|FullyQualifiedName~PreparedChatPublicationOwnerTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatValidatedRevealTests|FullyQualifiedName~ChatPageTests|FullyQualifiedName~ChatPipelineCompositionTests|FullyQualifiedName~PreparedChatIntegrationTests" --logger "console;verbosity=minimal"
```

Initial result: **409 passed, 2 failed, 0 skipped, 411 total**. Both failures were pre-existing default Data Protection characterization tests and both failed with `UnauthorizedAccessException` while trying to create a temporary key below `C:\Users\13728\AppData\Local\ASP.NET\DataProtection-Keys`, which is outside the writable workspace. Neither failure reached a Story 12.5 assertion.

The same matrix, excluding only these two environment-owned tests, was rerun with `--no-build --no-restore`:

- `ProductionComposition_KeyRingPathWithoutApplicationName_PreservesDefaultsAsync`
- `ProductionComposition_ApplicationNameWithoutKeyRingPath_PreservesDefaultsAsync`

Result: **409 passed, 0 failed, 0 skipped** in 1 minute 31 seconds.

The authenticated B-INT boundary was then selected directly:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~AuthenticatedBusinessInitialAndFollowUp|FullyQualifiedName~AuthenticatedItInitialAndFollowUp|FullyQualifiedName~AuthenticatedItFollowUp|FullyQualifiedName~AuthenticatedItFlow|FullyQualifiedName~ProductionComposition_Story125BInt" --logger "console;verbosity=minimal"
```

Result: **8 passed, 0 failed, 0 skipped** in 7 seconds. The public interaction assertions prove authenticated Business and IT initial/follow-up flows invoke one lexical projection, relationship projection and packet build per request; exactly one follow-up Conversation State re-fetch; one strict raw provider attempt, currentness check, final authorization and publication commit per request; typed raw streaming transport; one scoped publication owner shared with the coordinator; and no composed Track C sink or alternate live Conversation State owner.

## Story 12.5 Tier 1 groups

The Story 12.5 production Scan status/UI and corpus/evidence group was run directly:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ProductionScanStatusReaderTests|FullyQualifiedName~ProductionScanPageTests|FullyQualifiedName~Story125EvaluationContractTests|FullyQualifiedName~Story125ReleaseHarnessTests" --logger "console;verbosity=minimal"
```

Result: **103 passed, 0 failed, 0 skipped** in 5 seconds. Together with the 409-test B1-B4/B-INT matrix, this covers the repository-defined Story 12.5 Tier 1 contract/ranking/relationship, conversation/synthesis/security, and production UI/corpus/evidence groups. The authenticated B-INT tests are contained in the 409 count and are reported separately above rather than double-counted.

The quickstart's broader Track A command, which also selects `ManualResumeApiTests`, produced **104 passed and 12 failed**. All 12 failures stopped at the same login-startup harness boundary (`No POST form was rendered on the login page`) while the sandbox denied the default user-level Data Protection key path. Track A's committed checkpoint previously recorded this exact 116-test set green, and the current B-INT delta changes no Scan-status, resume endpoint, corpus, or evidence-contract source. The 103 directly relevant Story 12.5 status/UI/corpus/evidence tests remain green in this candidate.

A diagnostic repository-wide Tier 1 attempt was also made after excluding test names that explicitly expose PostgreSQL and known real CodeGraph/JDT/provider qualification names. It completed in 3 minutes 44 seconds with **2,451 passed, 275 failed, 4 skipped, 2,730 total**. The observed failure categories are outside the Track B boundary: test names without an environment marker still demanded `VULGATA_TEST_POSTGRES_ADMIN`, provider/runtime/image variables, or user-level Data Protection writes; other observed unrelated failures include the Pass 1 authoritative Code Logic schema identity drift. Because the broad output is not green and was not exhaustively triaged as part of T036, it cannot provide positive acceptance evidence. It is recorded but is not substituted for the green Story 12.5 Tier 1 matrices above, and no Tier 2/3 environment was activated to force it green.

## Build and static boundary guards

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore -p:WarningLevel=0
```

Result: **0 errors, 19 warnings**. Warnings are existing `NU1507` package-source mapping, `NU1900` unavailable vulnerability feeds in the network-restricted host, and `MSB3277` EF Core 10.0.4/10.0.9 assembly conflicts.

The following read-only guards were run over production Chat roots and the current uncommitted boundary:

| Guard | Result |
|---|---:|
| `public sealed class ChatEvidencePacketBuilder` definitions | `1` |
| `public sealed class PreparedChatPublicationOwner` implementations | `1` |
| scoped `IPreparedChatPublicationOwner -> PreparedChatPublicationOwner` registrations | `1` |
| `ExactCrossRepositoryEdgeEvidence` references in Chat/Prepared Chat production roots | `0` |
| production `Story125StreamAttestationSink` types/references | `0` |
| `candidateCommit`, `corpusCaseId`, `releaseRunId`, `publicKeyFingerprint`, or `releasePass` fields in Track B Chat roots | `0` |
| private `ChatConversationState` instance fields in `PreparedChatCoordinator` | `0` |
| coordinator candidate-only `AppendValidatedTurn` sites | `1` |
| coordinator follow-up reads from `_acceptedPublicationCommit?.Conversation` | `1` |
| uncommitted `VulgataDbContext`, migration/snapshot, accepted Story 12.4 entity/store/spec paths | `0` |

The single `AppendValidatedTurn` call is inside `BuildProposedConversationState`; it constructs the immutable publication candidate before `IPreparedChatPublicationOwner.Commit`. The coordinator holds only `_acceptedPublicationCommit`, has no live `ChatConversationState` field, and derives the next request's Conversation State from the committed composite or a new empty scoped value. Page/turn reveal entry points consume `AcceptedPublicationCommit`; no alternate success owner, direct live activation, Track C attestation, or release assertion is reachable.

`git diff --check` exited `0`. Its only output was Git's informational LF-to-CRLF warning for the three pre-existing dirty owned files.

## Candidate hashes

| File | SHA-256 |
|---|---|
| `src/dotnet/Vulgata.Web/Program.cs` | `e9be44da3a4e5f4ed23278900867a584cfc87be755600f4f7bba2e6345ce1f1d` |
| `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` | `5c0ea060b40e97260e98612817f4981d7fafefe666179d3d5b0a73c068e3d70c` |
| `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` | `5dcded60d5ae30cd005458423d9c13ca2970777abe51f6fa1ac44d2379fa81de` |
| `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` | `4288a7fffbc0749b493591a307ba03435c4d70fb503f818e2c45f860b9ac364e` |
| `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs` | `20904e4313dba45a636b36fc813ce10014e01a9c7a5072e390e6bcf615e2a7d5` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs` | `8c8f06376bce2f5d5934e7c78aa4d073a0e73606c250f530d4dc803ec7b22d6f` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs` | `637b726a84022a5a69121fb4dd90cbe39e460e9f619dfe937a4079c2a66e04b6` |

## T036 verdict

**PASS for the bounded Story 12.5 Track B integration verification.** B1-B4 and authenticated B-INT behavior are green, Web builds, Story 12.5 Tier 1 groups are green, and all migration/dependency/single-authority/Track-C-separation guards pass. The user-key-path and broader repository-baseline failures are explicitly preserved as host/out-of-boundary diagnostics; they do not authorize suppressing tests or claiming Tier 2/3 evidence. This record does not itself complete Track B: T037 must bind this result to the independently accepted B1-B4 checkpoints and the exact checkpoint candidate.
