# Story 12.5 Track B B1 Reacceptance 01

- Observed at: `2026-08-29T07:49:03.337Z`
- Source commit: `ec15c8a2ce6e7a7d725661ec473f046670663cc5`
- Branch: `demo0831/story-12-5`
- Tasks: T008-T013 (`EXISTING-EVIDENCE` / `VERIFY` only)
- Scope: dependency-independent B1 lexical-primary, advisory Context, packet 2.2, candidate extraction, and provider-blind synthesis boundary.

## Pinned boundary and structural guards

The production and test paths named by T008-T012 had no diff from the source commit before or after verification. Static inspection of `ChatEvidenceRetriever.cs` found:

- `_lexicalProjector.ProjectAsync(...)`: exactly 1 call;
- `_relationshipProjector.ProjectAsync(...)`: exactly 1 call;
- `_packetBuilder.Build(...)`: exactly 1 call;
- direct `new ChatEvidencePacketBuilder`: 0 occurrences;
- projection-only final-return forms (`RetrieveProjectionOnlyAsync`, `EvidencePacket projectionPacket`, or `projectionPacket.`): 0 occurrences;
- `RequiresCrossRepositoryEvidence`, `ExactRelationshipRow`, `ProvenanceRow`, and `ExactCrossRepositoryEdgeEvidence`: absent from the retriever.

The persisted `ExactCrossRepositoryEdgeEvidence` authority and its Story 12.4 fixtures remain elsewhere by design; this check rejects only a legacy or alternate Story 12.5 retriever authority/path. The focused topology test additionally proves exactly one packet build and rejects direct projector/builder construction, obsolete classifiers/rows, alternate projection packets, and legacy profile authority.

Commands:

```pwsh
git diff --exit-code ec15c8a2ce6e7a7d725661ec473f046670663cc5 -- <T008-T012 production-and-test-path manifest>
rg -n "RequiresCrossRepositoryEvidence|ExactRelationshipRow|ProvenanceRow|ExactCrossRepositoryEdgeEvidence|new ChatRelationshipEvidenceProjector|new ChatEvidencePacketBuilder" src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs
```

Results: pinned-path diff clean; forbidden-symbol search returned no matches. A counted source scan returned lexical `1`, relationship `1`, packet build `1`, direct builder `0`, and projection-only return `0`.

## Focused B1 verification

Command:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatPipelineCompositionTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatSupplementaryEvidenceSelectorTests|FullyQualifiedName~ChatAdvisoryContextReaderTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatEvidencePacketV22ContractTests|FullyQualifiedName~ChatCandidateExtractorTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatContractTests" -p:WarningLevel=0
```

Result: **216 passed, 0 failed, 0 skipped** in 1 minute 18 seconds.

### T008-T009: single topology and deterministic lexical primary

- Cross-Repository-literal and neutral-equivalent questions retain identical ordered `StableSectionId` and `DocumentVersionId` primary identity vectors.
- Adding relationship expansion preserves every existing lexical-primary `StableSectionId`; expansion cannot replace the primary authority.
- NFC normalization, token-array equality, ASCII/CJK extraction, active-mode term selection, prior-current-Document preference, full-set ranking before the 256 window, strongest-per-Document selection, twelve-primary target, five-Document/four-section bounds, and insufficient-evidence fallback are covered by the focused suites.
- Integer score components `50/40/30/25/20/5/3/1` are independently represented and applied once. Randomized input permutations retain canonical Repository/Document/section tie order. Exact 256, 256+ candidate, fifth/sixth Document, fourth/+1 section, twelve-primary, and twenty/+1 section vectors pass.

### T010: server-owned applied advisory Context

- The coordinator creates the authorization scope before reading server-owned advisory Context and before evidence retrieval.
- Applied Global, System, and exactly two Repository Context units are composed in canonical RFC 4122 Repository order; caller text and pending Context are excluded.
- Empty and nonmatching Context remain valid, advisory terms are fallback-only, and authority/currentness mutation changes the fingerprint after re-fetch.
- Exact 8,192 UTF-8 bytes succeeds without truncation; 8,193 bytes fails closed as inconsistent current evidence.

### T011: packet 2.2 and canonical fingerprint

- Runtime serialization is closed to `grounded-chat-evidence/2.2` and conforms to `contracts/chat-evidence-packet-v2.schema.json`; the obsolete 2.0 relationship shape is absent.
- Opaque Document, section, Source Anchor, relationship, primary, and expansion-seed handles remain bounded. Complete sections retain Source Anchors.
- Independent fingerprint vectors bind Document title, section heading, Flow Group step description and authority, relationship edge/current-v2 authority, selected decision count/set fingerprint, advisory Context, and conversation state. Collection ordering and shared-edge/two-decision-owned-Flow-Group permutations retain the same canonical fingerprint.
- Whole-unit packing drops optional lexical/relationship support in deterministic lowest-rank order and removes unused support. Required selected evidence or Flow Group support is never truncated or dropped; an over-bound required packet fails closed.
- Covered closed bounds include 1-5 Documents, 1-20 sections, 1-32 Source Anchors, 32 relationships, 10 Flow Groups, twelve primary sections, evidence body 65,536/+1 UTF-8 bytes, advisory Context 8,192/+1 bytes, and conversation 32,768/+1 bytes.

### T012: candidate extraction and provider blindness

- Candidate extraction consumes packet 2.2 only, preserves Unicode spans and canonical support, rejects restricted/missing/reversed/incomplete authority, and refuses over-bound spans without truncation or concatenation.
- Provider synthesis receives only the bounded schema, request ID, mode, question, evidence fingerprint, candidate handle/text, and narrative flag. Tests reject leakage of System/Document/section identities, Source Anchor/relationship handles, database access, raw Repository/source access, legacy profiles, vector search, analysis tools, or unrestricted tool loops.
- The dispatcher makes one selection-only streamed provider call for the primary attempt; raw provider content does not become grounded evidence or a client-visible answer at this B1 boundary.

## Environment notes

Build/test emitted pre-existing, non-product warnings: NU1507 for two configured package sources, NU1900 because vulnerability metadata endpoints were unavailable in the restricted environment, and MSB3277 for the existing EF Core 10.0.4/10.0.9 reference conflict. Compilation and every selected assertion completed successfully; no warning was treated as product evidence.

## Acceptance boundary

T008-T013 can be marked complete. B1 is independently reaccepted at the pinned source commit with no production or test change. This checkpoint does **not** accept or claim B2 relationship/currentness, B3 conversation, B4 streaming/publication/reveal, B-INT integration, Track C real-LLM/restart/screenshot evidence, or Story 12.5 completion.
