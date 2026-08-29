# Story 12.5 Track B B2 Reacceptance 01

- Observed at: `2026-08-29T07:57:52.1756662Z`
- Source commit: `e5d50047831043b47e4baffd7c8c5a6f3dacfe48`
- Branch: `demo0831/story-12-5`
- Accepted dependency: Story 12.4 completion commit `4798b97d9f23aef587829175df7ab7ebc3daf995` (ancestral)
- Tasks: T014-T018 (`EXISTING-EVIDENCE` / `VERIFY` only)
- Scope: independently reaccept current-v2 relationship projection, exact decision-set authority, decision-owned Published Flow Groups, and post-selection currentness. B1 remains a prerequisite; B3, B4, B-INT, and Track C are not accepted here.

## Pinned boundary and evidence identity

Before and after verification, Git reported no tracked or untracked changes. The B2 production/test manifest had no diff from the source commit, and the Story 12.4/specs/013, EF model, migration, and `VulgataDbContext` guard manifest also had no diff. The accepted Story 12.4 completion commit is ancestral to this candidate (`git merge-base --is-ancestor` exit `0`).

The ordered B2 evidence-set SHA-256 is `89835af312c3d12dbfeb82b626cf15e0808729e253081e80a49d201d57b3c1d0`. It is SHA-256 over UTF-8 lines `path=lowercase-file-sha256` for this ordered manifest:

| Path | SHA-256 |
|---|---|
| `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs` | `513beeb16de9eb7a8b3b1ef393792b52ff260c7d8eea09778c5e8773bce5ede4` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatExactDecisionSetAuthority.cs` | `96b6059953f0c82e88e71d52a0009f141424d7ee9901197d4d06f326d988f5ee` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs` | `1e1796e82a0f1b123ec8385e189dcefab8b3ef1010923959de3b2236798c2120` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs` | `637b726a84022a5a69121fb4dd90cbe39e460e9f619dfe937a4079c2a66e04b6` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs` | `16f55b8a6293d1131ec5758d8c60c638b2a36aa2b2657f3a193aede41e5f60c2` |
| `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs` | `8c8f06376bce2f5d5934e7c78aa4d073a0e73606c250f530d4dc803ec7b22d6f` |
| `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs` | `c95c711e52dc23b25fa405e034f442d5263245215e0285968a23ccaa64744481` |
| `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` | `1ffb8e87035f6488a775008a98ee42b4ac13422c9e11ba1823b969d97cd0297a` |
| `tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs` | `9ab625ac1c5cf43b96dbedb7c6a64ec8e2a414d1383cc62532083e4219956c25` |
| `tests/Vulgata.Tests/ChatEvidencePacketV22ContractTests.cs` | `68e41cd4a90a80ea4c8f31c1649ffd97a0aaa0e8a2da4dbe36f386e164a6bd17` |

## Commands and results

Aggregate B1+B2 verification:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatPipelineCompositionTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatSupplementaryEvidenceSelectorTests|FullyQualifiedName~ChatAdvisoryContextReaderTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatEvidencePacketV22ContractTests|FullyQualifiedName~ChatCandidateExtractorTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatEvidenceCurrentnessTests" -p:WarningLevel=0
```

Result: **265 passed, 0 failed, 0 skipped** in 1 minute 24 seconds.

Focused relationship/currentness/packet verification against the built candidate:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~ChatEvidenceCurrentnessTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatEvidencePacketBuilderTests" -p:WarningLevel=0
```

Result: **124 passed, 0 failed, 0 skipped** in 1 minute 17 seconds.

Static boundary guards:

```pwsh
git merge-base --is-ancestor 4798b97d9f23aef587829175df7ab7ebc3daf995 HEAD
git diff --name-only e5d50047831043b47e4baffd7c8c5a6f3dacfe48 -- <B2 production-and-test manifest>
git diff --name-only e5d50047831043b47e4baffd7c8c5a6f3dacfe48 -- specs/013-exact-cross-repository-flow-pass2 src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs src/dotnet/Vulgata.Infrastructure/Data/Migrations src/dotnet/Vulgata.Infrastructure/Migrations
rg -n "RequiresCrossRepositoryEvidence|ExactRelationshipRow|ProvenanceRow|ExactCrossRepositoryEdgeEvidence|new ChatRelationshipEvidenceProjector|new ChatEvidencePacketBuilder" src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs
rg -n "ExactCrossRepositoryEdgeEvidence|ExactRelationshipRow|RequiresCrossRepositoryEvidence|ProvenanceRow" src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs src/dotnet/Vulgata.Infrastructure/Data/ChatExactDecisionSetAuthority.cs src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs
```

Results: ancestry passed; both diff manifests were empty; both forbidden-authority searches returned no matches. `ChatEvidenceRetriever` contains one injected `IChatRelationshipProjector` call and no concrete-projector/builder fallback.

## T014: current-v2 many-decision authority

- `ChatExactDecisionSetAuthority` rejects duplicate decision IDs/slots, recomputes every decision's complete ordered provider contribution authority, then orders independently valid decisions by the accepted Story 12.4 slot sequence and RFC 4122 byte comparison. It binds the first canonical decision, full count, and a length-framed set fingerprint.
- `RetrieveAsync_SharedEdgeWithTwoCurrentDecisions_PreservesDecisionSetAndOwnedFlowGroupsAsync` proves one current Cross-Repository REFERENCES Edge can retain two independently valid decisions, `ExactDecisionCount == 2`, a non-null set fingerprint, and two distinct decision-owned Flow Groups.
- Counts 11 and 256 remain current; 257 on one Edge fails closed. Two Edges with independent 128/129 sets remain current. Adding a valid decision after selection changes the complete set and returns `MissingOrStale`.
- Canonicality is independent of input enumeration: decision authority explicitly sorts the unordered persisted collection; packet tests reverse collection order and prove stable fingerprint/handles; the shared-Edge/two-group packet vector reverses the groups and proves the same fingerprint with canonical group/decision/step order. The aggregate suite also executes randomized lexical/supplementary tie permutations before B2 expansion.

## T015: closed relationship and Flow Group states

| State | Optional projection/currentness result | Selected-required result |
|---|---|---|
| `absent` | supports no relationship/flow claim; unrelated lexical evidence remains valid/current | not applicable until selected |
| `present-valid` | accepted only from exact current-v2 authority and a complete `Published` Flow Group | retained unit becomes required |
| `present-inconsistent` | supports nothing; inconsistent selected snapshot returns `MissingOrStale` | entire assembly withheld |
| `selected-required` | not substitutable after packet packing | any later drift returns `MissingOrStale`; no lower-ranked replacement |

The vectors cover legacy-only/non-exact authority, missing or reversed authority, wrong profile/System/Repository/revision/direction/fingerprint, conflicting slot, duplicate related target, mismatched decision/group binding, empty group, and persisted `Incomplete`, `Complete`, or `Failed` group state. Only `Published` is eligible. Optional missing/unrelated stale facts do not poison valid lexical primaries; an additional empty/inconsistent selected group refuses the whole packet.

## T016: deterministic expansion, preference, and bounds

- Expansion seeds are the first `min(3, primaryDocumentCount)` lexical Documents; each seed contributes at most two additions.
- Traversal preserves stored direction and depth one. A second hop is excluded; duplicate/cycle targets are deduplicated or fail closed when authority conflicts.
- The complete eligible relationship set is ranked before bounds. Business mode prefers GENERATED_FROM business explanation; IT mode prefers REFERENCES technical evidence, with canonical Repository/Logical Document/Document Version/Edge tie order.
- Verified closed bounds remain five Documents, 32 relationships, ten Flow Group authorities, twelve ordered Flow Steps, twenty total sections, and four sections per Document. The test seam asserts relationship `1..32`, per-seed additions `1..2`, and flow-step `0..12`; packet/builder vectors cover whole-unit removal and group authority.
- Published Flow Groups retain canonical decision rank, group order/ID, and ordered trace steps. Multiple groups on one relationship remain distinct; the exercised two-group fixture retains all four steps.

## T017: post-selection currentness and whole-answer withholding

The currentness public seam re-fetches and checks every selected Document/current version/title, section/stable identity/content, Source Anchor/Repository/path/line/fingerprint, Edge/current endpoints/direction, exact decision and complete contribution set, selected count/set fingerprint, Published Flow Group, trace, ordered step/description/Document/Edge/decision binding, System, Repository, completed Scan Run, and Profiled Revision.

Mutation vectors return `MissingOrStale` for Document title/content/current-version drift; Source Anchor Scan Run/Profiled Revision drift; Edge endpoint/revision/direction changes; exact-decision canonical fingerprint or decision-set membership changes; Flow Group identity/state/Document/decision/Edge/System/Repository/Scan Run changes; trace identity/order changes; step identity/order/content/Document/Edge/decision changes; selected Scan Run status; provider or step Profiled Revision drift; Cross-System authority; authorization loss; and database/cancellation failure. No partial selected-required support or answer is returned. Legacy evidence mutation does not affect accepted current-v2 currentness.

## Environment notes

Compilation emitted the existing `NU1507` two-package-source warning, `NU1900` vulnerability-feed reachability warnings in the restricted environment, and `MSB3277` EF Core 10.0.4/10.0.9 reference-conflict warnings. The projects built, test execution completed, and every selected assertion passed. Git also reported that the user-level ignore file was inaccessible in the sandbox; repository porcelain and explicit diff manifests still completed successfully.

## Acceptance boundary

T014-T018 can be marked complete. B2 is independently reaccepted at the pinned source commit without production or test changes. This evidence preserves B1 and proves B2 only; it does **not** accept or claim B3 conversation, B4 streaming/publication/reveal, B-INT integration, Track C real-LLM/restart/screenshot evidence, or Story 12.5 completion.
