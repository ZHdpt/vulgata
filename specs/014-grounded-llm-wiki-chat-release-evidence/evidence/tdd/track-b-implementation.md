# Story 12.5 Track B implementation evidence

- Evidence ID: `12-5/implement/track-b-01`
- Kind: `test-red` and `test-green`
- Stage: `implement`
- Source commit: `1b7526f9c57b7226b561a9708f4d94f09f1fd046`
- Dependency commit: `4798b97d9f23aef587829175df7ab7ebc3daf995`
- Approval: `implementation-approval-track-b-01`
- Tasks: T020-T037 only
- Observed through: `2026-08-28T02:28:09.1564291Z`

## Behavioral RED/GREEN record

The Track B slices were executed at public seams with test and production ownership separated.

| Boundary | Observed RED | GREEN evidence |
| --- | --- | --- |
| deterministic lexical retrieval/ranking | heading/title/mode/tie/document-cap and no-fallback vectors exposed legacy ranking behavior | retrieval, supplementary selector and candidate ranker suites passed |
| accepted v2 relationship authority | non-Published Flow Groups initially returned `Found`; 13 post-retrieval Flow Group/trace/step mutations returned `Current`; a current directed one-hop `REFERENCES` related unit was omitted | Published-only retrieval, exact Story 12.4 decision/Edge/contribution authority, bounded one-hop projector, and render-time Flow Group authority revalidation passed |
| Evidence Packet v2 | title, heading and flow description were absent from the fingerprint; the dedicated builder contract was missing; contextual retrieval returned an empty conversation | canonical packet binding, 8 KiB advisory and 4 KiB conversation bounds, 64 KiB complete-unit packing, Flow Group authority binding, and retriever-builder integration passed |
| bounded conversation | follow-up eviction/reset/re-fetch/currentness cases exposed missing scoped transient state | two-turn server-owned state, scope/mode/access/reset behavior and re-fetch-only context passed |
| synthesis/citation/security | raw-versus-typed selection and whole-answer validation vectors exposed incomplete containment | claim/handle/Source Anchor/currentness/final-authorization barriers passed with no semantic repair |
| provider streaming | arbitrary split UTF-8/SSE/JSON/terminal vectors and liveness vectors exposed aggregate buffering and missing strict ownership | strict incremental assembler, raw OpenAI-compatible streaming adapter, signed content-dark observation authority, one syntax repair, shared 29-second deadline and ten-second decoded-signal inactivity passed |
| accepted-only reveal | deadline/cancellation/reveal vectors exposed stale fixtures and delayed accepted rendering | zero provider fragments before acceptance, immediate accepted render with insufficient remaining budget, Unicode reveal and render-timed scrolling passed |

Test-only fixture corrections were limited to stale lexical questions, stale retriever signature characterization, accepted Story 12.4 Flow Trace decision binding, and truthful security-order instrumentation. They did not weaken production behavior.

## Consolidated verification

Command:

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter "FullyQualifiedName~ChatContractTests|FullyQualifiedName~ChatConversationStateTests|FullyQualifiedName~ChatEvidencePacketBuilderTests|FullyQualifiedName~ChatEvidenceRetrievalTests|FullyQualifiedName~ChatEvidenceCurrentnessTests|FullyQualifiedName~ChatSupplementaryEvidenceSelectorTests|FullyQualifiedName~ChatCandidateRankerTests|FullyQualifiedName~ChatCandidateExtractorTests|FullyQualifiedName~ChatAgentResultValidatorTests|FullyQualifiedName~ChatAgentDispatcherTests|FullyQualifiedName~ChatProviderStreamAssemblerTests|FullyQualifiedName~OpenAiCompatibleChatProviderStreamingTransportTests|FullyQualifiedName~ChatStreamObservationProvenanceTests|FullyQualifiedName~PreparedChatCoordinatorTests|FullyQualifiedName~ChatValidatedRevealTests|FullyQualifiedName~ChatPageTests"
```

Result: 208 passed, 0 failed, 0 skipped.

## Convergence correction cycle

Independent convergence against checkpoint `4127658dcdff6942f031c3dbd3600dc76fd507b4` identified three partial Track B boundaries and appended T051-T053. Test and production ownership remained separate.

| Task | Honest RED | Corrected GREEN |
| --- | --- | --- |
| T051 | projection-only non-flow retrieval returned `IncompleteOrStale`; four current `IndexEntry` symbol/path/endpoint/mode metadata cases ranked a prose-only competitor | current authorized `IndexEntry` plus `DocumentSection` projection-first ranking, exact metadata precedence, deterministic strongest-per-Document/limits/fallback, and legacy non-authority passed |
| T052 | a generic follow-up omitted current evidence selected by the bounded prior validated question | prior questions and current Logical Document hints participate only after re-fetch/authorization as terms and a small post-eligibility boost; no evidence is carried over |
| T053 | four post-retrieval `GENERATED_FROM` mutations—source/target Edge Scan Run and source/target Profiled Revision—returned `Current` | render-time validation binds both persisted Edge endpoints to current Scan Runs and their distinct Profiled Revisions; all four mutation cases return missing/stale |

The original test owners verified GREEN: T051/T052 focused suite 46 passed; T053 currentness suite 26 passed. The consolidated Track B filter after correction passed 218 tests with 0 failures and 0 skips. No EF, migration, model-snapshot, Story 12.4 contract, shared composition, or Track C path changed.

A fresh convergence pass then identified T054: exact-v2 presence still selected the old prepared trunk. Three honest RED cases proved that accepted-v2 scope bypassed projection-first selection and returned `Found` for unrelated/all-stop-word input. Minimal GREEN moved the current authorized lexical eligibility gate ahead of exact-v2 lookup for every request; exact relationships remain available only for explicit Cross-Repository bounded expansion. The original test owner verified all three cases, all 22 retrieval tests, and both exact relationship/Flow Group expansion regressions GREEN. The consolidated Track B filter after T054 passed 221 tests with 0 failures and 0 skips.

The third fresh convergence pass appended T055-T059. Corrections retained projection-primary evidence during accepted-v2 expansion; made optional GENERATED_FROM/Flow Group support non-mandatory while failing closed on inconsistent persisted support; added IT TechnicalDetail/Evidence and canonical mode/tie ordering; revalidated every packet item with completed Scan Run and explicit persisted Edge/decision/Flow Group authority; aligned packet-v2 relationship and conversation wire fields/fingerprint binding; and removed the last direct-construction `application/json` aggregate acceptance path. Original test-owner verification passed retrieval 43/43, currentness 33/33, packet contract/builder 33/33, and dispatcher/raw-streaming 40/40. The consolidated Track B filter passed 236 tests with 0 failures and 0 skips; the initially stale Chat page relationship fixture was updated to explicit v2 authority and its render suite passed 27/27.

The fourth fresh convergence pass appended T060-T064. Corrections added authorized Repository-name scoring and strongest-per-Document/max-four section selection; enforced completed Scan Run/Profiled Revision/System and Source Anchor identity before packet construction; supported same-Repository REFERENCES with nullable exact authority while rejecting duplicate targets/edges and validating selected related Flow Groups; extended signed content-dark stream receipts with bounded lifecycle bindings/metrics; counted SSE comments/unknown fields within event limits; and converted physical disconnects into one signed bounded rejection. Original test-owner verification passed evidence authority 73/73 and dispatcher/raw-streaming 45/45.

After the user reset the convergence allowance, the fifth fresh convergence pass appended T065-T069. Honest RED proved that multiple accepted v2 Edges still hit a singleton guard, Scan Run/Profiled Revision drift passed currentness, stream requests lacked explicit candidate/case/provider-configuration binding, syntax repair used a noncanonical attempt name, unregistered direct construction could be accepted, and final semantic rejection did not update the signed observation. Minimal GREEN removed the singleton authority, bound Document revisions at render time, introduced strict runtime release bindings and schema-v3 content-dark counters/vocabulary, made direct construction fail closed for accepted attempts, and added a narrow validation-disposition capability from `PreparedChatCoordinator` to the signed observation authority. Multiple published Flow Groups and the aggregate twelve/+1 step bound were already GREEN and received test-only evidence. Original test owners verified retrieval/currentness 68/68 and dispatcher/streaming/coordinator 78/78.

The sixth fresh convergence pass appended T070-T074. Honest RED showed that a Cross-Repository question with no optional exact relationship still returned stale, the primary/repair lifecycle recorded no attempt-specific dispositions, and an unterminated SSE event left its maximum line counter at zero. Minimal GREEN returns the independently authorized projection packet when exact expansion is absent while revalidating any persisted Cross-Repository endpoints, records `syntax-repair-required` and `rejected-syntax` against the actual attempts, and finalizes unterminated line/event counters before signing rejection. Flow-step Document revision drift, extra-group decision mismatch, and split LF/CR/CRLF byte accounting were already GREEN and received test-only evidence. A stale legacy-security fixture was narrowed to unrelated evidence so it still proves legacy evidence cannot authorize a packet. Original test owners verified retrieval/currentness 71/71 and dispatcher/streaming/coordinator 83/83.

The seventh fresh convergence pass appended T075-T079. Honest RED showed that an unrelated stale Cross-Repository Edge poisoned projection retrieval, an empty selected Published Flow Group was silently omitted, CRLF event counters undercounted two wire bytes, raw-body +1 overflow collapsed to provider failure, and an unsupported non-answer retained an accepted receipt disposition. Minimal GREEN scopes missing-exact checks to current Repository projection authority, rejects any selected group without complete rows, counts both CRLF bytes across split reads, classifies only adapter-owned overflow as `raw-body-overflow`, and records `rejected-semantic` on the actual attempt before unsupported rescue/mapping. Original test owners verified retrieval/currentness 73/73 and dispatcher/streaming/coordinator 86/86.

Additional checks:

- `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore`: succeeded with 0 errors and existing package/analyzer warnings.
- `git diff --check`: passed; only checkout line-ending notices were emitted.
- No change under `VulgataDbContext`, migrations, `VulgataDbContextModelSnapshot`, project files, central packages, or `Program.cs`.
- No Story 12.4 entity, contract, or worktree file was modified.
- No provider fragment, unvalidated claim, citation, handle, relationship, or Source Anchor crosses the client boundary.
- Track C T038-T045 remains closed and no Tier 3 release claim was attempted.

## Owned implementation boundary

The checkpoint owns only the Story 12.5 Track B Chat production/test paths, this evidence record, the T020-T037 task markers, and compact workflow state. Staging uses an explicit path manifest; unrelated and Story 12.4 working-directory content remains excluded.
