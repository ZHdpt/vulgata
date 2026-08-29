# Story 12.5 Track B conditional E2E run 01

- Result: **passed**
- Candidate under test: `43ddc8687153a72001a5a64bad921db94a8e9f24`
- Branch: `demo0831/story-12-5`
- Decision input: `track-b-decision-input-01.md`
- Execution owner: `e2e-runner`
- Tracked test edits: none
- Production edits: none

## Exact execution

Command:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatPageTests|FullyQualifiedName~PreparedChatPublicationOwnerTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatPipelineCompositionTests|FullyQualifiedName~ChatValidatedRevealTests|FullyQualifiedName~PreparedChatIntegrationTests|FullyQualifiedName~ChatStreamObservationProvenanceTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatEvidenceCurrentnessTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatEvidencePacketV22ContractTests|FullyQualifiedName~ChatCandidateExtractorTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatProviderStreamAssemblerTests|FullyQualifiedName~OpenAiCompatibleChatProviderStreamingTransportTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatConversationStateTests"
```

Observed terminal result:

```text
Passed!  - Failed:     0, Passed:   399, Skipped:     0, Total:   399, Duration: 1 m 28 s - Vulgata.Tests.dll (net10.0)
```

The focused two-Repository authorization execution was also run without a
rebuild:

```text
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --filter "FullyQualifiedName~ExactCrossRepositoryAuthorizationTests"
```

Observed terminal result:

```text
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 7 s - Vulgata.Tests.dll (net10.0)
```

Across the two commands, 405 tests passed, with no failures or skips.

The run emitted existing package-source, vulnerability-feed, compiler, analyzer,
and line-ending warnings only. No test failure, skipped test, or test-process
error was observed.

## Predicate mapping

| Decision predicate | Evidence in this run |
| --- | --- |
| `user-visible-workflow` | `ChatPageTests` covers authenticated Business and IT Chinese rendering, accepted-only citation/answer reveal, pending/cancellation behavior, explicit `新对话`, in-flight/reveal reset races, and empty identical-scope follow-up Conversation State. `ChatValidatedRevealTests` and `ChatConversationStateTests` cover accepted composite publication and conversation binding. |
| `authentication-authorization` | `PreparedChatIntegrationTests`, `ChatEvidenceCurrentnessTests`, `ChatEvidenceRetrievalTests`, and the focused `ExactCrossRepositoryAuthorizationTests` execution cover authorized scope, current-v2 evidence, direction/cardinality constraints, and two-Repository Cross-Repository boundaries. |
| `api-persistence-multi-module` | `PreparedChatIntegrationTests` and `ChatPipelineCompositionTests` exercise the production typed coordinator/DI graph, advisory/retrieval/relationship/packet seams, strict streaming transport, validation, sole publication owner, and follow-up re-fetch. Packet, candidate, dispatcher, provider-stream, and validator suites cover the downstream contracts. |
| `ef-migration-data-lifecycle` | Not triggered by the decision input; no migration or model lifecycle claim is made here. |
| `existing-integration-coverage` | Remains a may-skip row only; no coverage override is claimed. |

The selected suites include the two-Repository fixtures and typed production
composition, while keeping the run bounded to the Track B/B-INT chat boundary.
The tests do not use fake Backup B state, deterministic rescue output as Tier 3
evidence, Track C attestation, a real provider, restart qualification,
screenshots, or release-evidence claims.

## Classification and boundary

Classification: **passed**. There were no failed-product, failed-test, or
blocked/ambiguous results. Because no tracked E2E/API/integration test was
added or corrected, the accepted Track B quality review remains valid and no
review invalidation is required.

Preflight after execution:

```text
## demo0831/story-12-5
43ddc8687153a72001a5a64bad921db94a8e9f24
```

The worktree was clean before and after the run except for this newly written
execution-evidence file. No workflow state, decision input, production code,
EF artifact, Story 12.4 artifact, Track C artifact, Git stage, or Git history
was changed.
