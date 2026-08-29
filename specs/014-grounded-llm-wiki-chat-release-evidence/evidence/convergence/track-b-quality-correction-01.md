# Story 12.5 Track B quality-correction convergence 01

## Verdict

**NOT_CONVERGED**. One source-traceable partial gap was found and appended as T067. No quality-review, E2E, final-verification, or Track C gate is advanced by this report.

This is the first fresh post-correction convergence pass. The user-approved next-approval rule therefore remains open: another two non-converged passes are required before requesting the next user approval.

## Candidate and authority

- Candidate inspected: `363c7179fcae7c9f767db9e757088fdb7e069384`
- Branch: `demo0831/story-12-5`
- Worktree: clean before the convergence evidence/task writes
- Story 12.4 dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995` is ancestral
- Governing artifacts: accepted Story 12.5 `spec.md`, `plan.md`, append-only `tasks.md` through T063, `.specify/memory/constitution.md`, and `track-b-quality-correction-03.md`
- Accepted artifact hashes before this convergence append:
  - `spec.md`: `A7367B1F64FCFC2CF099D7FD204575F314E35994204CCB0CD7B7F2DACF6F7DD4`
  - `plan.md`: `FAD42DCD3A3F393809E95500EA6D6C7607CE4A16787D8B16D054B415CE533318`
  - `tasks.md` before T067: `9CAC0CE82EDE8DA736B817E839FCF9246EBFE058F1A2447AAB950D1C3F30C512`
  - `constitution.md`: `1B6C1C1BF13D0A398A4C1DC5A3572DEFD350E8470766BB1FB6C111ECB534C1E5`
  - `track-b-quality-correction-03.md`: `6C90CE9641DFF4BDD3E2E9DA8EC9C3572F3E3B498D7CC81AFEB7361B2D9EFDC8`

## Assessment scope

The assessment checked the Track B intent inventory: FR-001 through FR-032 and FR-053 through FR-060; SC-001 through SC-006 and SC-015 through SC-017; B1-B4 and B-INT task obligations T008-T037; correction obligations T055-T063; and the bounded architecture decisions AD-1 through AD-7C. The applicable Constitution MUST constraints were checked for bounded retrieval, authorization/currentness, test-first ownership, no migration, Story 12.4 read-only dependency use, and no unvalidated client output.

The accepted T063 evidence and current source inspection continue to support the corrected boundaries: one typed lexical/relationship/packet path; current-v2 decision, DocumentEdge, `GENERATED_FROM`, and Published Flow Group authority; bounded transient conversation with re-fetch/revalidation; mandatory raw primary/repair streaming with strict UTF-8/SSE/byte/terminal/deadline rules; one Web publication owner and accepted-only reveal; content-dark Track B observations; and no EF, Story 12.4, vector/autonomous-loop, fake-state, or Track C production path.

## Finding

### F1 — stale constructor composition characterization

- Classification: `partial`
- Severity: `HIGH` for the current Track B verification boundary (test gate failure; no production defect observed)
- Source references: T061/T062, B-INT, FR-053, FR-060, SC-015-SC-017, AD-7C
- Evidence: `tests/Vulgata.Tests/ChatStreamObservationProvenanceTests.cs:200-219` still asserts `ownerParameter.HasDefaultValue` for `IPreparedChatPublicationOwner`. The current accepted T062 production constructor deliberately requires non-optional typed `IChatStreamCompletionSummaryRecorder` and `IPreparedChatPublicationOwner` dependencies and rejects fallback construction. The provenance test therefore fails at line 215 even though the implementation satisfies the corrected authority boundary. The same test class also predates the corrected typed-recorder expectation, so the current test boundary does not agree with T061/T062.
- Reproduction: `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore -p:WarningLevel=0 --filter "FullyQualifiedName~ChatStreamObservationProvenanceTests" --logger "console;verbosity=minimal"` => 4 passed, 1 failed. Failure: `PreparedChatCoordinator_ComposesPublicationOwnerForCompletionAuthority`, `Assert.True` at line 215.
- Impact: the full bounded Track B verification cannot be green while a current composition characterization requires the superseded optional/fallback binding. This is an omitted test-boundary correction, not a request to weaken the accepted typed composition contract.
- Required correction: T067 updates this stale characterization to assert the required non-optional typed owner and recorder dependencies and reruns the provenance suite plus the bounded Track B matrix. No production, specification, plan, Story 12.4, persistence, or Track C change is needed.

## Focused verification observed

- T063's six affected suites remain recorded as 127/127 passed on the corrected candidate, with only the two documented default ASP.NET Data Protection key-path characterizations excluded for the managed sandbox.
- Fresh streaming/transport probe: `ChatAgentDispatcherTests`, `ChatProviderStreamAssemblerTests`, and `OpenAiCompatibleChatProviderStreamingTransportTests` passed 73/73.
- Fresh correction/publication/reveal probe: `PreparedChatPublicationOwnerTests`, `PreparedChatCoordinatorTests`, `ChatPipelineCompositionTests`, `ChatPageTests`, and `ChatValidatedRevealTests` passed 120/120.
- Fresh provenance probe: 4/5 passed, with only F1 failing.
- Web build and prior T063 `git diff --check` evidence remain green; no new production or workflow-state mutation occurred in this convergence pass.
- A broader fresh filter was attempted but did not produce a terminal result within the bounded observation window and was interrupted; it is not counted as a pass or failure. The prior T063 and accepted convergence evidence remain the source for the already-covered B1-B4 matrix until T067 reruns it.

## Appended work

- Outcome: `tasks_appended`
- Appended section: `## Phase 12: Convergence`
- Appended task: `T067`
- Task path: `specs/014-grounded-llm-wiki-chat-release-evidence/tasks.md`
- Post-append tasks hash: `546C83D7C3FE16FEEE1A5C13B9812358DB49F07FB703274F9C928BBC5B416A82`

## Authority and scope result

No specification, plan, Constitution, PRD, planned-change authorization, Story 12.4 contract, production code, test code, lifecycle state, stage, commit, or Git history was changed. The only writes are this traceable convergence report and the required append-only T067 task. No intent or authority defect requires return to definition; the actionable defect is a stale test characterization omitted from the correction task scope.
