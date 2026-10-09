# OCT-CHAT validation guide and bounded K0 preparation evidence

The feature-verification scenarios below remain planned unless the scoped execution evidence at the end records an actual result. K0 preparation, controlled T002–T006 and subsequent unaffected primitives have run; integrated Chat/provider/browser/storage qualification has not. The user has now explicitly authorized controlled T011 and remaining satisfiable work through T021; earlier hold evidence below is historical. Source artifacts and common fixture identities must be pinned for each claimed boundary.

## Preconditions

1. Work in this assigned worktree on the reviewed implementation commit. Keep output/test resources separate from other lanes.
2. Integrate the Business-owned compiled K0 contracts/common fixture. Before any real provider or production user-facing C1 dispatch, also integrate Business's durable dialogue begin/fence/terminal-save/lookup/orphan implementation and schema, and pass focused PostgreSQL admission/save checks. Earlier C1 work is test-only controlled mechanics. Obtain Explore's exact reader route for C2 and integration-owner Web/profile/shared-admission composition. Do not create fallback contracts or write October migrations from Chat.
3. Use existing accounts with an ordinary permitted user, Business/IT cases and denied/revoked contributor. Setup operator credentials are not ordinary reader access.
4. Confirm explicit database/batch/publication and schema identity. The integration owner controls the schema application window; ordinary Web does not execute October migrations. Starting Web still applies existing Identity/domain migrations/seeding, so it requires an explicitly owned target.
5. For real execution, declare source consent, selected ChatCompletions endpoint/model and host configuration source, protected credentials, request/rate admission, time/byte limits and owned test database/ports. Do not print secrets or use production/shared databases. No global PATH or settings changes.

## Controlled verification

After classifying new tests/helpers in the existing suite inventory, use the repository launcher. Proposed tests should use namespace `Vulgata.Tests.October.Chat` so the following focused filter is concrete; before execution confirm discovery is nonzero.

```powershell
pwsh -NoProfile -File eng/test.ps1 -PolicyOnly
pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat'
```

Expected observations:

- Selected fixture is visibly controlled; wrong database/batch/publication is unavailable with no legacy substitution. Ordinary authorized users need no operator/management role.
- Tool transport sends native fragmented call arguments, invokes a real registered controlled function, continues the model round and yields answer text while provider completion remains deliberately blocked. Include text before a later tool finishes. UI observation must not be inferred from transport receipt alone.
- Malformed UTF-8/framing, incomplete tool JSON, conflicting IDs, invalid function, truncation and limits terminate honestly. Cancellation releases owned work and provider permits.
- Business never receives CL/title/count/path/history/footer metadata. Revoke a contributor used only in non-footer input; subsequent text/history is withheld. Authorization outage fails closed.
- Malicious retrieved instructions cannot expand scope, tools or provider settings. Raw reasoning, arguments/results, credentials and unsafe rendered links/HTML remain absent from UI/diagnostics.
- Current-turn exact reads qualify references; discovery-only, earlier-turn, invented and foreign-section handles fail. Empty report is valid. Footer readiness does not delay text.
- F3: use Business-owned fixture Documents with (a) multiple sections totaling >64 KiB and (b) one section >64 KiB. Invoke ReadWiki with the returned opaque continuation until complete within budgets. Assert fixed publication/version/section binding, complete UTF-8 across multibyte boundaries, preserved claim qualifiers and exact returned coverage. Repeat with revoked access, expired/tampered tokens and mismatched target; expect owner-safe failure without fallback. Exhaust aggregate/time/tool budgets after a prefix: report incomplete, do not credit unread suffix/sections or duplicate coverage on replay. Failed/cancelled pages add no ledger coverage.
- F4: reuse Business's lock-acquisition versus supplied business-motive fixture, including a Mixed group. Observe source/advisory attribution beside the first affected streamed prose and again in a saved-history follow-up; motive/duration must not become source proof. Exercise bounded history and chunk boundaries near the qualifier, preserving it or omitting the affected group/turn honestly. Business presentation must keep safe attribution without technical CL identifiers. No sentence-by-sentence citations, whole-answer verifier, buffering or publication gate may be added to pass this check.
- Provider admission: verify agreed policy with separate process-local Web/generator permits; Chat uses Web's pool and generator Business finalization shares Scan's pool. Do not claim a global cap or create a cross-process coordinator.
- Duplicate submit, same-key conflict, one-active-turn contention and late events never start another generation or attach text to a different conversation.
- Useful completed history supports a follow-up. Interrupted output and old IT context are excluded from new Business inference. Context budget omits whole old turns with an honest indication, not a fabricated summary.

## Persistence qualification

Use only runner-created PostgreSQL resources and guarded tests:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat'
```

Require atomic text/reference/dependency/status save, ordered history, cross-request active fence, identical replay and conflicting-payload rejection. Drop a commit acknowledgement after the database commits: outcome lookup confirms the single saved result with no provider call. An unavailable lookup leaves save-indeterminate and retry blocked. Confirm host-orphan fencing cannot steal live work; restart never dispatches old turns. Useful terminal partial text is retained; unsaved process-crash tokens are not promised.

## Real provider and browser qualification

External/E2E commands run only after explicit execution/source/provider authorization and actual classified test names exist. The filter must discover enabled tests; all-skipped/zero-selected is non-success.

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite External -WithPostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat'
pwsh -NoProfile -File eng/test.ps1 -Suite E2E -WithPostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat'
```

The runner supplies owned databases, not the frozen shared delivery dataset. Seed only through supported owner interfaces or explicitly authorized local seed inputs. Manual UI observation against the final shared dataset belongs to the integration owner's separately authorized run window.

For the real selected source-backed publication, record:

| Scenario | Required observable result |
| --- | --- |
| Cross-Repository registration/login explanation | Actual native tool round and at least two actual Document reads; narrative matches independently inspected source-backed Documents, including branches/effects and relationship meaning. |
| Narrative follow-up | Uses useful completed history plus a further wiki read; references belong to this turn's exact ledger. |
| Unknown | States a genuine source/Knowledge gap without inventing a business relationship or a footer target. |
| Live delivery | Browser first-visible text timestamp precedes final provider completion; include interleaved activity. Record total duration/limits, not an invented universal latency target. |
| Exact navigation | Footer opens Explore's exact version/section in the same publication; compare with reader/graph selection. No latest-version redirect. |
| Interruption / error | Useful partial text has interrupted/failed status, no automatic generation replay, no completed-history pollution; controlled injected failures stay labelled. |
| Access | Ordinary permitted success; Business BL-only; current revoked/denied contributor cannot leak tool/history/footer metadata. |

Separate retrieval completeness, generated-document quality, answer usefulness and reference faithfulness. Exact IDs or valid schema alone are not semantic evidence. Preserve sanitized timings/correlation IDs, result status, declared source coverage and limitations; never store credentials, hidden reasoning or raw tool payloads in evidence.

## Integration handoff and completion

Provide the integration owner the feature commit, K0/extension pins, registration entry point, required configuration source, test results and remaining limits. The integration owner performs the final combined Web build and complete SelfContained run after final fixes, plus affected runtime checks. Do not repeat broad suites absent changed behavior or an unresolved concern. No original 19.x/full PRD closure is implied.

This planning request stops before all commands above except document/Git validation. No model calls, migrations, application startup or runtime tests were executed to author it.

## Subsequent bounded K0 preparation — 2026-10-06

OCT-Integrator's later explicit dispatch authorized the initial contract import, conformance tests, exact suite classification and bounded commits. The earlier planning stop remains applicable to the other feature tasks. See [canonical symbol map and proposed W0](contracts/imported-k0-and-w0.md) for scope and ownership.

Imported integration checkpoint: **`3bcd815b09cfbac7418d6644a2a75816e0788ec0`**, containing Business candidate **`07c3fe4463e3228a6982fd106b58148ca87974bd`** and handoff **`7a189c34aec625202f74846d4907ded787ccbdca`**. Planning was preserved at `4fad5314`, then imported via merge `f8127cb9`, with no reset/history rewrite. K0 compiled read-handle/DTO/fixture conformance is acknowledged by this lane; integration owns aggregate T006/accepted-K disposition and the other lanes' acknowledgements. **W0 remains unsatisfied and does not block this K0 acknowledgement.**

Executed command, twice due to a new probe assertion correction:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat.OctoberChatKnowledgeConformanceTests|FullyQualifiedName~Vulgata.Tests.October.Knowledge'
```

- First run: `TestResults/isolation-c3535e5d1e1d401fb447f24baa4b755c/results.trx`, 60 selected, 59 passed, 1 failed, 0 skipped. The new related-result test compared whole record equality across separately allocated immutable arrays. Replaced that assertion with exact key, explanation, source-evidence and limitation comparisons. No owner code/fixture or behavior was changed; this is a test assertion correction, not product RED/GREEN evidence.
- Final current-source run: `TestResults/isolation-fac1f9c98be242fcad2914f297f89695/results.trx`, **60 selected, 60 passed, 0 failed, 0 skipped**: 10 Chat consumer cases and 50 imported Knowledge cases. Launcher discovery: 3,389; these counts do not claim the full SelfContained suite ran.
- The launcher rebuilt current sources, classified 321 source files and passed its policy/outcome/timeout/cleanup/fixture-guard checks. Build succeeded with warnings; no warning policy was changed. Build and execution logs are beside the final TRX.
- PostgreSql, LocalTools, External, E2E and HistoricalBoundary were excluded and are **not qualified**. No database/migration, provider, source/native-tool suite or Web startup was invoked. Launcher-owned compiler/test/guard processes and output directories are the only executed preparation resources.
- Tests use actual `IOctoberKnowledgeReader`/`IOctoberKnowledgeDisclosurePolicy`, Core records and Shared DTOs/validators against `OctoberKnowledgeFixture`/`ControlledKnowledgeReader`/`ControlledDisclosurePolicy`. There is no new Knowledge DTO, policy or production test-data fallback.

Task accounting after the subsequent planning-only ordering correction: **T001 is K0-only and complete from this prior preparation; W0 verification is required at T007. T002 prep-file classification done; broader future inventory/author assignments still open. T003–T038 not executed.** Controlled feature-local T003–T006 may be scheduled after K0 and separate implementation authorization without waiting for W0; integrated T007/C0 completion still requires owner-settled W0 and its real reader/composition prerequisites. The proposed W0 API remains unaccepted/unimplemented. No checkbox implies C0 completion. Added only `October/Chat/OctoberChatKnowledgeConformanceTests.cs` to suites.json as SelfContained under the earlier preparation's explicit authority; no new tests ran for the scheduling correction.

Remaining prerequisites: integration-owned W0 context/entry composition as proposed, Business production read/disclosure services, D0 dialogue declarations/implementation/schema, W1 selected Chat provider/credentials/Web permits, E0 exact reader and eventual I0 real data. Imported generator `IOctoberExecutionScope` is not Web Chat admission. Large-document/section, expiry/cryptographic cursor, Mixed streamed-answer/history and real authority/storage/streaming qualifications remain later work. Stop after this bounded preparation commit; do not begin C0/full Wave 2 or send an outbound reply.

## Authorized controlled T002–T006 execution

The user subsequently instructed: "Implement T002 setup, then controlled T003–T006". This supersedes the earlier preparation-only stop for these exact tasks. W0 remains proposed, not accepted or activated; T007 and later tasks, providers, databases/migrations, native-tool suites and real host startup remain excluded. No new skills/workflows or global changes are invoked.

T002 setup: `controlled_test_author` owns only `OctoberChatSelectionTests.cs`, `OctoberChatEntryTests.cs` and `ControlledChatFixture.cs`; `controlled_production_author` owns the task-listed Core/Shared/selection/VM/module and feature-local ChatPage bindings. `controlled_inventory_owner` is the sole local suites.json editor and has classified exactly these three new test/helper paths as SelfContained. Root owns serialized launcher execution, scoped evidence and documentation. No production author can weaken tests; both authors assess the final shape. Existing project references suffice; no package/project/shared composition edit is required.

The complete future suite proposal was submitted to and classified by the inventory owner, without inserting nonexistent entries (the repository policy rejects stale inventory): C1 Contract/WikiTool/ContentContinuation/Provider/Qualification/Turn/Disclosure/StreamingUi tests are SelfContained; PersistenceTests is PostgreSql; ProviderQualificationTests is External; BrowserQualificationTests is E2E. C2 History/Reference/SavedOutcome tests are SelfContained and reuse the latter persistence/browser suites. C3 reuses External/E2E plus affected SelfContained checks. Every future helper is classified from actual dependency/resource reachability when introduced; this setup does not authorize those tasks.

Only controlled canonical K0 reader/policy and host-input shapes are used. Temporary results belong to this checkout's unique `TestResults/isolation-*` directories. Root runs one launcher at a time, with no shared process termination. Compile-only scaffolding is not counted as RED; actual failing assertions must precede production GREEN. Actual observed results follow when available.

Initial behavioral RED: `eng/test.ps1 -Suite SelfContained` filtered to `OctoberChatSelectionTests|OctoberChatEntryTests` built successfully and ran 37 cases, 6 passed / 31 failed / 0 skipped. TRX: `TestResults/isolation-779a84b4dee4462b9c00e0e5c77ed06f/results.trx`. Failures included Ready/InvalidSelection vs fail-closed Unavailable, missing context confirmation, IT light-vs-dark binding and absent opt-in registration. Production GREEN was then authorized; test and production files remained separately owned.

Initial combined GREEN: `eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat|FullyQualifiedName~Vulgata.Tests.October.Knowledge|FullyQualifiedName~Vulgata.Tests.ChatPageTests|FullyQualifiedName~Vulgata.Tests.ChatConversationStateTests'` passed 150/150, 0 failed/skipped, at `TestResults/isolation-ba0c93be84f244f2ab968dd28b458602/results.trx`. This includes five additional controlled identity/stale-open boundary cases authored before that run; they were not part of the earlier 37-case RED claim. Independent review subsequently identified the pending page-path host-change race and entry-instance replacement/removal lifecycle gap. This initial GREEN therefore is not the final acceptance boundary; targeted regression/correction evidence follows separately.

Targeted lifecycle RED: the same isolated launcher selected `InitializeAsync_SupersededPendingRead|ChatPage_ModeButton_ConfirmsFreshContext|ChatPage_ReplacedEntryWithSameHostSelection|ChatPage_RemovedExplicitEntry`; 5 cases, 2 passed / 3 failed / 0 skipped, TRX `TestResults/isolation-f9dc11cbcc334093af248fdee6a0b0f6/results.trx`. Failures reproduced ignored pending host updates, fresh model stuck Idle and removed binding causing a null legacy EditContext. Separate production correction then tracked pending selection/operation versions, initialized replacement models regardless of unchanged selection object and retained a cleared unavailable October shell on removal. Tests were not weakened.

**Final current-source GREEN:** the combined filter above passed **154/154, 0 failed, 0 skipped**, TRX `TestResults/isolation-b9e9c73f002a4341be3a302d9771c27b/results.trx`. Counts verified from persisted XML: 22 new selection cases, 24 new entry cases, 10 prior Chat K0 probes, 50 Knowledge cases and 48 legacy Chat cases. Discovery reported 3,435; this is focused/affected SelfContained verification, not a full suite claim. Build succeeded with 0 errors and 5,568 warnings (including AD0001 analyzer failures); warning policy was unchanged. The launcher policy and guard checks passed for 324 classified source files. All other suites were excluded/not qualified.

The test author and production author assessed the final shape; retained independent reviewer accepted the corrected T002–T006 scope conditional on final combined GREEN, now satisfied. T002–T006 are complete within controlled composition; T007/integrated C0 is open. No provider, DB/migration, native-tool qualification or real Web startup occurred, and no shared Program.cs/navigation/project/package/Knowledge implementation was edited.

### Registration handoff for future owner-settled W0

`Vulgata.Web.Services.OctoberChat.OctoberChatModule.AddOctoberChat(IServiceCollection)` registers scoped `IOctoberChatSelectionService` and `OctoberChatEntryViewModel` only. Integration must supply the actual `IOctoberKnowledgeReader` / `IOctoberKnowledgeDisclosurePolicy` and trusted canonical `ReadSelectionRequest` (subject/authentication binding, explicit publication, purpose Chat, mode/scope). No fallback Knowledge implementation or proposed W0 interface is registered.

`ChatPage` accepts optional `OctoberEntry` and `OctoberSelection` parameters for controlled feature-local composition. Without October opt-in, the existing legacy path remains the default. Explicit October composition renders selection/loading/empty/unavailable state with Chinese mode themes, confirmed new contexts and a disabled send action; losing a previously supplied October binding fails closed rather than exposing legacy data. The VM clears old owner/stamp metadata immediately and never treats Active/SaveIndeterminate as settled; it does not implement durable admission or generation. Real ordinary-reader services, W0/D0/W1 and actual host/runtime qualification remain integration prerequisites. This verified boundary was subsequently committed at the user's request as `803142072cabd3606b2121ed6fc142c80aa4c881` (`feat: add controlled October Chat selection and entry`) on `oct14/chat`; the worktree was clean immediately after that commit. No push was requested or performed.

## Controlled work toward T021

The user instructed "Move on to Controlled work toward T021", then "Commit previous changes first". The commit above precedes this work. Separate test, production and inventory owners continue; root owns serialized test execution and evidence. No additional skills, shared project/composition edits, provider execution, database work or Web startup are authorized by this controlled slice.

Independent dependency assessment permits Chat-local budgets/events, canonical read mapping/continuation/read-ledger/report validation and isolated event rendering. It found a concrete K0 gap: `DisclosureRequest` requires `AllContributors`, but `SearchPage`, `ContentPage` and `RelatedPage` provide neither complete transitive input lineage nor an owner-issued disclosure-dependency handle. Visible citations, repository scope or rereading reported Documents cannot reconstruct hidden or unreported contributors. OCT-BUSINESS must supply the canonical extension; this lane will not invent one. Isolated ledger reports are consumed-read mechanics, not authorization for a footer/model/generated answer. T011 disclosure integration and T018–T019 therefore remain incomplete.

T016–T017 depend on Business-owned dialogue begin/fence/save/lookup/orphan declarations, still absent at the pinned checkpoint. W1's selected Chat profile, protected credentials and Web admission contract are also absent; generator `IOctoberExecutionScope` is not Chat admission. T012–T013 production wiring/live execution remain stopped. Read-only installed SDK inspection found streaming and invocation interception APIs, but does not prove strict framing or delivery before EOF. New October prompt resources need integration-owned Agents project registrations; legacy prompt resources are not a substitute. No current controlled result establishes genuine provider streaming, durable save, browser delivery, safe full-lineage disclosure or integrated C1 completion.

First T008–T011 behavioral RED: `eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~OctoberChatContractTests|FullyQualifiedName~OctoberWikiToolTests|FullyQualifiedName~OctoberContentContinuationTests'` built current sources and ran **59 cases: 5 passed, 54 failed, 0 skipped**, with TRX `TestResults/isolation-34150c719eee4734903c55cfe6501b3f/results.trx`. Failures were assertions against fail-closed scaffolds, including budget refusal, missing consumed-read credit and invalid-argument mapping; compilation failure is not used as RED. Inventory/policy/guard checks passed for 327 classified files. Large UTF-8 chunks and expiry errors are controlled projections over owner-fixture metadata/response types, not qualification of Business's production cursor implementation. Separate production GREEN was authorized after this observed result.

### Explicit T011 hold

Automatic approval review rejected the material `OctoberWikiTools.InvokeAsync` implementation as lacking sufficiently specific agent-tool approval. Root checked the user's actual controlled-through-T021 instruction, corrected the stale T002–T006-only authority paragraph and allowed one same-action retry with that provenance. Review again required a new explicit user decision. Root asked whether to approve the isolated SearchWiki/BrowseWiki, ReadWiki, GetRelatedDocuments and ReportAnswerDocuments facade, retaining the host/provider/disclosure stops. The user answered **"Keep T011 blocked"**. No rejected facade patch was applied and no workaround/retry follows that decision. The fail-closed scaffold and meaningful RED tests are retained; no full Chat-suite GREEN is claimed. Only unaffected primitives and isolated UI continue. Two later original-lifetime cancellation/deadline rows were added before the hold; they are not included in the earlier 59-case RED count.

Unaffected primitive/affected regression GREEN: `eng/test.ps1 -Suite SelfContained` filtered to `OctoberChatContractTests|Record_DuplicatePrefixAndMiddleOnlyRead|OctoberChatSelectionTests|OctoberChatEntryTests|OctoberChatKnowledgeConformanceTests|Vulgata.Tests.October.Knowledge|Vulgata.Tests.ChatPageTests|Vulgata.Tests.ChatConversationStateTests` passed **183/183, 0 failed/skipped**, TRX `TestResults/isolation-557edc9a09b34bf4b48abf7ef92488f9/results.trx`. This is 28 contract/budget/event cases, one direct ledger case and the prior 154-case regression selection. Facade-dependent tests are deliberately outside this filter and remain incomplete/RED. Discovery found 3,496 cases; no full-suite or external qualification is claimed. T008–T009 primitives have controlled GREEN; history limits are constants for future history enforcement, and counters are not integrated provider/SSE transport proof.

### Isolated T020–T021 presentation

`OctoberChatStreamingUiTests` uses bUnit and host-supplied typed events only. Its first compiled RED selected **14 cases, 0 passed, 14 failed, 0 skipped**, TRX `TestResults/isolation-326768cfc92840b497c1981dc0e191f1/results.trx`; every failure was an assertion against the fail-closed `Apply` scaffold. The inventory owner registered this actual file as SelfContained (328 classified sources), with no browser placeholder or new dependency.

Initial UI/affected GREEN added `OctoberChatStreamingUiTests` to the exact 183-case filter above: **197/197 passed, 0 failed/skipped**, TRX `TestResults/isolation-98690abc054a41dc8d7459038d7e2403/results.trx`. It proves visible controlled text while completion is held, independent reference/save updates, ordered sanitized activity, replacement/disposal and bound page behavior. Review then found empty interrupted/failed results misleadingly claimed retained partial text; a targeted RED and correction follow. This initial GREEN is not the final acceptance boundary.

The component renders Razor-encoded narrative text with preserved whitespace. Rich Markdown remains incomplete; no parser/package or unsafe markup is introduced. References display counts/partial-read labels only; E0 authorized titles/destinations and full-lineage disclosure checks remain absent. `TurnSaved` carries a Chat-owned presentation outcome, not a Business storage contract or durable receipt. Optional `ChatPage.OctoberTurn` renders only for the current matching conversation/context/request/selection; sending remains disabled. Real coordinator/subscription, provider/browser streaming, latency measurement and durable-result integration are not implemented or qualified by this isolated presentation slice. T020–T021 remain partial.

Empty-outcome correction RED selected `Apply_EmptyOrWhitespaceInterruptedOutcome_DoesNotClaimRetainedUsefulContent`: **4 failed, 0 passed/skipped**, TRX `TestResults/isolation-daa5bb70026e42f4b92a883b5cdd5859/results.trx`. The production owner then limited the retained-partial-content label to nonblank text; empty/whitespace interrupted or failed results say no usable content was received. Assertions were not weakened.

**Final affected GREEN:** the same 183-case primitive/regression filter plus all 18 `OctoberChatStreamingUiTests` passed **201/201, 0 failed/skipped**, TRX `TestResults/isolation-80ead151507142718b17714df3ed5bda/results.trx`. Root verified persisted counters; discovery found 3,514 cases, policy/guard checks passed for 328 classified sources, and the current-source build reported **0 errors, 32 warnings**. Warnings remain; neither warning-free compilation nor a full SelfContained pass is claimed. Test and production owners assessed the final shape; independent review accepted the controlled subset and corrected wording conditional on this final GREEN, now satisfied. T008–T009 remain partial because full routing/argument/page enforcement depends on the blocked facade. T010–T011 remain incomplete with retained RED tests; T020–T021 remain partial for the integration/rendering limitations above. T001–T006 alone remain checked.

The previous committed boundary is still `803142072cabd3606b2121ed6fc142c80aa4c881`; this subsequent work is uncommitted. No further merge/push, root-agent/feature-selection/global-setting change, shared composition/project/schema edit, provider, database, migration, native-tool qualification or Web startup occurred. PostgreSql/LocalTools/External/E2E/HistoricalBoundary remain excluded and unqualified. Stop at this controlled handoff with **T011 blocked by the user's explicit decision** and owner-contract prerequisites unresolved; do not interpret partial GREEN as permission to activate generation or advance to T022.

## Explicitly approved T011 continuation

After receiving the specific recommendation to implement the isolated SearchWiki/BrowseWiki, ReadWiki, GetRelatedDocuments and ReportAnswerDocuments facade with canonical reads, server-bound selection, strict arguments, budgets/cancellation, opaque continuations and consumed-read report validation, the user instructed **"Accept your suggestion. Proceed T011 and what remains before T021"**. This new approval supersedes the preceding T011 hold; its history is preserved. Automatic approval review accepted the newly authorized facade patch. Live registration/activation and safe answer/footer disclosure still require owner-issued contracts and qualification. No other skill is invoked.

The first continuation launcher attempt (`TestResults/isolation-cecd2e87d380474ba929c140e41760d6/build.log`) stopped at compilation: target-typed request construction was ambiguous between FluentValidation overloads. Production made the two request types explicit. This is a build correction, not behavioral RED evidence. Separate test/independent review found that owner exceptions needed truthful Unavailable classification and that the complete ReadWiki result, including metadata/qualification/escaping, needed the individual 64 KiB bound. Targeted tests precede those behavioral corrections; their actual results follow.

Corrected-source RED selected the Contract/WikiTool/ContentContinuation classes: **66 cases, 58 passed, 8 failed, 0 skipped**, TRX `TestResults/isolation-b87e474ebee44483b00ef3f5c1dc7c52/results.trx`. Five failures reproduced the reviewed defects (three owner exception types, two oversized complete projections). Three exposed test record-equality mistakes over recreated ImmutableArray backing storage; the test author replaced them with exact ordered ID/text/qualification/evidence/limitation comparisons. The deadline assertion now distinguishes BudgetExceeded from explicit Cancelled while preserving no-credit/stop/lease assertions. The >64-KiB fixture remains 72 KiB raw across two complete multibyte projections; its emoji/ASCII mixture was adjusted so each valid serialized page fits the actual 64 KiB projection cap. No whole-document requirement or qualification assertion was removed. Production then separated input parsing from owner execution and capped the complete serialized ReadWiki result before ledger credit.

**T011 continuation GREEN:** `eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.October.Chat|FullyQualifiedName~Vulgata.Tests.October.Knowledge|FullyQualifiedName~Vulgata.Tests.ChatPageTests|FullyQualifiedName~Vulgata.Tests.ChatConversationStateTests'` passed **238/238, 0 failed/skipped**, TRX `TestResults/isolation-c028f249f2fe45a9aecd6e8db4a2a65e/results.trx`; discovery 3,519. This includes all current Chat tests, 50 Knowledge cases and 48 legacy Chat regressions. Independent review accepted both corrections and **controlled T008–T011 completion**, because argument validation, server-bound routing, page limits and refusal accounting now accompany the previously verified primitives. No retained facade RED remains at this boundary. Owner cursor cryptography/production large-page compatibility, all-contributor disclosure/T019, D0/W1 and real transport/runtime remain separate obligations; a consumed-read report still grants no current disclosure authority.

### Remaining controlled SDK and prompt work

Automatic approval review rejected a proposed compile-only production `OctoberChatAgent` / `OctoberChatProviderAdapter` scaffold, interpreting new provider/agent capability as outside the isolated T011 approval. No such production files were created, and root did not retry or change write mechanisms. This is an automatic-approval restriction, not a newly invented project checkpoint. A narrower, distinct test-only installed-SDK capability probe may call existing MAF with a scripted `IChatClient` and the already approved facade; it does not implement a substitute adapter in tests. T013 production adapter work remains unperformed pending explicit resolution. T014–T015 external prompt/loader work can proceed independently with fixed manifest lookup and controlled named streams, while integration-owned resource registration remains absent.

The first combined SDK/prompt run selected **15 cases, 3 passed, 12 failed, 0 skipped**, TRX `TestResults/isolation-277003213a8d46d0a2d230324a5a1761/results.trx`. Prompt loader behavior was compiling RED (**13 cases: 2 passed, 11 failed**) against its null-returning scaffold. SDK cancellation passed; native tool continuation failed at `AIFunctionFactory.Create` while exporting the Core result's default ImmutableArray return schema, before any tool invocation. The test author changed only the probe delegate's return representation to explicit canonical-result JSON text, preserving the real facade/native-loop/early-text assertions. This is SDK integration diagnosis, not manufactured production RED. Future production wrappers must use the facade's bounded serialization representation or re-account actual emitted bytes; default serializers can expand escaping beyond the already measured size. No rejected adapter was implemented in the test helper.

**Combined GREEN:** the full affected Chat/Knowledge/legacy filter above passed **253/253, 0 failed/skipped**, TRX `TestResults/isolation-a9cc8d595a564528b10f9135304129f9/results.trx`; discovery 3,534, inventory 330 sources. Both installed-SDK probes and all 13 prompt-loader cases passed. The scripted native BrowseWiki function invoked the real approved facade once; MAF itself issued the next request, exposed typed text while completion was gated, and propagated cancellation without another request. This does not qualify an October production adapter, final-reason validation, raw SSE/framing, actual serialized SDK tool-output limits or provider/browser behavior. Installed versions remain MAF 1.13.0 / Microsoft.Extensions.AI 10.6.0 from this checkout's resolved assets.

The loader selects exact fixed manifest names, snapshots configured named resources, bounds reads to 32 KiB plus one sentinel byte, decodes strict UTF-8 without alternate-encoding fallback, disposes streams and propagates requested cancellation. Explicit controlled streams preserve the exact owned Business/IT prompt-file text. Default production lookup remains unavailable until integration adds the documented resource entries; no filesystem or historical-prompt fallback exists. Independent review accepted this source/test subset conditional on the combined GREEN, now satisfied. T012–T015 remain partial: production adapter/transport, prompt embedding, actual prompt-to-provider wiring and semantic qualification are outstanding. Root requested explicit T013 controlled-production approval after this concrete probe; pending input is not permission to create the rejected adapter.

Final inspection of that build reported **0 errors and 5,891 warnings**, including AD0001 analyzer failures; no warning policy was changed and not all warnings are claimed pre-existing. Both authors accepted the verified subset. `git diff --check` passed apart from ordinary line-ending conversion notices. New work remains uncommitted on `oct14/chat`, with `80314207` still HEAD and `fad7f11` verified included. The T013 approval question is pending; no production SDK source, live provider, host activation, shared database/migration/project edit or later T022 work was performed. Missing Business/integration semantics are itemized in contracts/imported-k0-and-w0.md; this document does not claim those owners received or accepted a message.

## Explicit controlled T013 approval — 2026-10-07

The user requested a brief of the pending T013 question, received the concrete scope (internal OctoberChatAgent/OctoberChatProviderAdapter; one bounded MAF loop with injected test client and approved wiki facade; duplicate/unknown-call rejection, text-only updates, cancellation; no live provider/credentials/Web registration/invented shared admission) and answered **"Approve"**. This new approval resolves the previous automatic-review restriction for that exact implementation. Root verified `oct14/chat`, unchanged HEAD `80314207` and ancestry of `fad7f11`; prior edits were preserved. No new skill, shared contract/project/schema edit or external runtime is authorized by this continuation.

Initial compiled RED for `OctoberChatProviderTests`: **29 selected, 7 passed, 22 failed, 0 skipped**, TRX `TestResults/isolation-12efcce6fc094139953e874aae8beebb/results.trx`. Failures exercised safe scaffolds with no streamed text, native execution, counters or lifetime behavior. Three additional cases distinguish mapping failures, already-processed informational calls and legitimate usage-only metadata after stop; focused RED was **3 failed, 0 passed/skipped**, TRX `TestResults/isolation-4faabd91b723409d84e016add000383c/results.trx`. Two local same-agent reuse cases were then added before GREEN; they are additional coverage, not claimed as part of those observed RED counts. They establish only single-instance use, not durable cross-request fencing.

The selected native tool representation preserves the original argument dictionary through a fixed-schema AIFunction and returns a cloned JsonElement from the same bounded JSON counted by the facade. This avoids reflection silently dropping unexpected arguments, the previously observed Core return-schema export failure and a second JSON-string quoting layer. String enum names in this Chat-only representation retain qualification meanings; canonical Knowledge types/schema remain unchanged. Actual raw HTTP/SSE serialization, profile selection/admission, durable save and full-lineage disclosure remain separate unresolved obligations.

Initial implementation passed the full affected filter: **285/285, 0 failed/skipped**, TRX `TestResults/isolation-41cfbfcbc1034d38b22d44ae6818048d/results.trx` (34 provider cases, discovery 3,566). Independent review then identified two uncovered typed-client defects: forwarding cancellation alone could leave a non-cooperative MoveNext pending beyond the original lifetime, and distinct calls/arguments could accumulate before round termination without preflight limits. This initial GREEN is not acceptance. Targeted tests and corrections follow; these are local adapter obligations, not issues deferred to raw HTTP qualification.

Four targeted cases reproduced those findings (**4 failed**, TRX `TestResults/isolation-e41b1421871743718c7868c133555ae5/results.trx`). Production bounded pending iteration by the original lifetime, deferred cleanup until pending iteration settles, and checked remaining call/argument bounds before retaining immutable argument snapshots. Facade accounting remains the sole tool-call charge. The affected run then passed **289/289**, TRX `TestResults/isolation-4f23e5723e344314b00fa3b2f5948ded/results.trx`.

Independent review found one remaining cleanup path: normal iterator disposal could itself ignore cancellation. Two new cases completed iteration immediately and held only disposal behind an owned gate. Both reproduced the defect as timeouts awaiting turn settlement before releasing cleanup (**2 failed**, TRX `TestResults/isolation-b03a2b40996642538d8d2ae2d3577bfe/results.trx`). Production now starts disposal once, bounds the wait with the original token and observes that same task after cancellation. Pending iteration is never disposed concurrently, no late Current is processed, and the injecting caller retains client ownership. This bounds the consumer wait; it does not forcibly terminate opaque underlying I/O. Future W1 must retain real request permits until underlying cleanup completes. The independent reviewer and both authors accepted the corrected controlled source, conditional on the final affected GREEN.

**Final controlled T013 GREEN:** the same full affected Chat/Knowledge/legacy filter passed **291/291, 0 failed/skipped**, including all **40 provider cases**, in `TestResults/isolation-ccd63f8f85234e7dba0d29a19fef51ef/results.trx`. Root verified persisted counters: 291 executed/passed, zero failures, timeouts or aborted cases; discovery 3,572 and inventory 330. The current build reported **0 errors and 5,956 warnings**, including AD0001 analyzer exceptions; warning policy was preserved and these are not all claimed pre-existing. Independent review's final GREEN condition is satisfied. The tests used scripted clients, in-memory gates and controlled clocks; PostgreSql, LocalTools, External, E2E and HistoricalBoundary were excluded, not qualified. This is an affected selection, not the complete SelfContained suite.

The controlled T013 approval is resolved and its internal typed-client agent/adapter is implemented. T012/T013 remain open overall for strict raw HTTP/SSE and selected ChatCompletions/profile, credential/retry/admission/permit integration and runtime qualification. T014/T015 still need integration-owned prompt embedding and semantic qualification. T016–T019 stop at missing Business dialogue/full-input-lineage contracts; T020/T021 remain controlled UI only. No real provider/host/database was started, no additional task beyond the approved boundary was implemented, and no new shared contract/schema/project/root-instruction edit or commit was made. HEAD remains `803142072cabd3606b2121ed6fc142c80aa4c881` on `oct14/chat`; `fad7f11` remains included. The dated project-state guidance and its ownership/test pointers were rechecked; no unrelated project-state rewrite was needed.

## Next authorized wave: actual tool progress — 2026-10-07

The user authorized independent remaining T012–T015/T020–T021 work, Business-owned contract coordination and reviewed owned commits. Checkout was clean at `f3db38db` above approved integration `082994eb`; the contract contribution was preserved and relayed. Integration/Chat transport and W0 ownership handoff is committed at `1aadbd86353ff9f05ab720e322d0290e6ec569a2` in contracts/web-provider-handoff.md. This authorization is not permission to invent D0/disclosure or execute live T023.

The internal agent now accepts optional application-owned nonblocking `Action<OctoberChatToolProgress>` after its cancellationToken argument. It emits an application-generated Guid and Started/Completed/Failed only around actual facade invocation. Rejected uninvoked native calls emit no fictional activity; no provider call IDs, tool arguments/results or hidden reasoning enter progress. Text callbacks remain immediate. A throwing sink stops safely without recursively notifying itself. The future coordinator owns UI subscription isolation and binding these notifications to authorized turn events; no durable start/save receipt is fabricated.

Separate test/production owners retained disjoint files. Nine progress cases compiled RED: **2 passed, 7 failed**, `TestResults/isolation-6cd08007b3a74edcbf2d5931731cbf0a/results.trx`. After implementation the affected selection passed 419/420; its deadline case exposed existing facade-linked cancellation classified as Cancelled instead of BudgetExceeded (`isolation-5eda135ab86f4b6db469f3a9a02d364a`). Test ownership confirmed the original caller token was untouched; production preserves original cancellation/deadline cause before cleanup cancellation in the agent's generic catch. No assertion was weakened.

Final affected command remains the Chat/Knowledge/ChatPage/ChatConversationState SelfContained filter recorded above: **420/420 passed, zero failed/skipped**, `TestResults/isolation-6abfb460971147f08fd2a4cc68614b97/results.trx`, discovery 3,856/inventory 360. The larger count includes integrated Knowledge tests now present in the approved baseline. Independent source review accepted progress and the classification correction; both authors assessed final shape. This is controlled typed-client evidence, not actual provider/browser visibility, raw transport, full-input authorization or persistence qualification. No runtime resources or new shared schema/contracts were used. Partial task statuses remain unchanged.

## Raw HTTP/SSE guard — controlled qualification, 2026-10-07

Chat implements the agreed public `OctoberChatStreamingHandler(admittedTransport, budget)` and `SealAndWaitForSettlementAsync()` with a borrowed inner transport. It shares the original budget, permits one actual send per already-admitted round, validates bounded incremental UTF-8/SSE and native fragments, and withholds only final DONE until physical EOF while forwarding earlier valid events. Deferred send/read/response cleanup remains observable after consumer cancellation; seal synchronously closes output and its stable task proves this guard's cleanup, not integration's separate permit drain. No whole-answer buffer or second tool loop is introduced. Integration owns selected client/credentials, retries and redirects off, admission and the second drain operation.

Tests exercise the installed `OpenAI.Chat.ChatClient(...).AsIChatClient()` over a fake HTTP handler, in addition to direct guard checks. Initial direct constructor use failed compilation (internal OpenAIChatClient, `isolation-5f07a69a1ab8488bac5c5e5331fc931d`); the public extension correction is not RED evidence. Compiled RED progressed from 27 cases (2 passed/25 failed, `isolation-13d9a305f9074c88b8faa5a3ef12d819`) to 28 (2/26, `isolation-76cae32dacc3453ea3d08b6b729113e0`) after correcting refused-call accounting and adding actual-SDK EOF gating. Two further actual-SDK protocol-versus-budget cases both failed against the scaffold (`isolation-dae1c30f421842dd98642c29013f9a4f`). Identifiable parser-rejected native calls consume original tool budget once; cancellation/disposal/network unwind does not double-charge typed-adapter refusals. Original transport failure cause survives SDK exception wrapping.

The first implementation passed 8/30 (`isolation-0d3b6185f0934064978b1bab81a96400`). Controlled diagnostics proved two distinct compatibility facts: installed SDK emits ToolCalls finish before its final aggregated FunctionCallContent update, and one bare .NET StreamContent response disposal invokes its wrapped stream Dispose twice. The corrected adapter narrowly accepts that one final ToolCalls aggregate while retaining all call validation and complete-iterator admission. Test cleanup assertions now independently require one original-response disposal and one actual resource cleanup transition, retaining raw invocation counts while pending and no dispose/read overlap; they do not simply accept arbitrary duplicate guard disposal. The temporary framework-only diagnostic was removed after evidence. Diagnostic compilation first needed an explicit Microsoft.Extensions.AI.ChatMessage qualification (`isolation-8a3d951afde847dab2aa8d925357ed49`); no behavioral result is claimed for that failed build.

Independent review also required recursive case-insensitive duplicate-property rejection inside decoded native arguments before SDK dictionary normalization. The nested-argument case reproduced the defect; actual-SDK duplicate rows were initially masked by the blanket post-finish rejection and are not misreported as RED. Diagnostic six-case results were 3 passed/3 failed (`isolation-7d8113162c694eeba1df687a57a15ff9`). After corrections, 33/35 passed; two deterministic new close-buffer cases failed (`isolation-7ec5c0d00a784fc8bfbf66ae982c65d0`). Production then made buffer copy/closure atomic and marked exchanges closing synchronously before seal returns, preserving deferred cancellation outside the gate.

**Final affected GREEN:** **455/455 passed, zero failed/skipped**, including all 35 raw transport cases, in `TestResults/isolation-7cd788a4de214b0d96018af3f6c1a333/results.trx`; discovery 3,891/inventory 361. Build reported zero errors and 6,609 warnings; no warning policy was changed. Independent reviewer accepted every correction and both authors assessed final shape. This proves the controlled installed-SDK path and modeled physical cleanup only; integration's actual W1 permit release, live provider/browser, canonical disclosure implementation and durable persistence remain unqualified. No paid service, host, native tool or database was started. T012/T013 remain partial overall; no T022/T023 completion is inferred.

## Canonical consumer increment — 2026-10-07

The raw guard checkpoint is `26a89712d2bc854df703a62cbf672962c6aab9c3`. Root imported Integrator-approved `2ea9cc5d3c32f0ef53f5381f0827d07f44091680` at merge `3a853603834ed89a36f702c51f90e224cf1d7364`, preserving the Chat contribution and compatibility acceptance. The affected Chat/Knowledge/legacy selection plus `FullyQualifiedName~Vulgata.Tests.October.Integration` passed **509/509, zero failed/skipped**, `TestResults/isolation-a43607e95d84450db15028dca819ad65/results.trx`. This imported Business's agreed declarations, embedded Chat prompts and Chat-purpose context composition; it did not establish D0 persistence or W1 live execution qualification.

Initial canonical disclosure tests compiled against fail-closed scaffolds: **26 selected, 3 passed, 23 failed**, zero skips, `TestResults/isolation-c0c446c3ab2e4ddcaf75121b64fe65b3/results.trx` (build zero errors, 6,727 warnings). Existing rejection-only passes are not claimed as behavioral RED. The test owner corrected an unjustified report-time proof assumption: every admitted content result needs its owner-issued consumed-content proof before model bytes return, including uncited reads and interrupted turns. The corrected cumulative-proof case and a new uncited-read case both failed against the scaffold (**0/2 passed**, `TestResults/isolation-bae024b463dc430b8d294db15f1501d0/results.trx`). No canonical consumer GREEN or durable/live completion is claimed by this RED record.

The internal session now accumulates opaque owner capsules, obtains eager consumed-content proofs and reauthorizes fresh fixed-selection identity before every provider round and released delta. Page adoption stages the complete union/proof until owner checks and exact output-byte accounting succeed. Ignored-cancellation owner calls cannot mutate state later. The canonical facade cannot fall back to legacy reads or infer empty lineage. Generic metadata-free Unavailable may continue only after reauthorizing the existing union. Separate controlled APIs remain explicitly noncanonical.

Independent review added between-round revocation and late Merge/Build completion cases (five cases, first observed after implementation, not claimed as preimplementation RED). Initial complete class run passed **31/32**, `TestResults/isolation-b2c4e57064aa4cd682efa44c0a2cc85b/results.trx`. The actual installed SDK reformatted a JsonElement into indented tool-message text, violating exact output-byte accounting. Production now strictly decodes the verified owner bytes and passes literal JSON text through the SDK text-result branch, with no second serialization. The unchanged test captures the outgoing HTTP request, requires exact UTF-8 equality, object JSON rather than quoted JSON, equal charged bytes and no capsule data. This empirical correction supersedes earlier JsonElement representation assumptions; it changes no owner schema or encoding profile.

**Final affected GREEN:** the Chat/Knowledge/legacy/October.Integration SelfContained filter passed **541/541, zero failed/skipped**, including all 32 disclosure cases, `TestResults/isolation-acc5eb43c25e4fb49ed15018d28b946b/results.trx`; discovery 4,002/inventory 366, build zero errors and 6,771 warnings. Independent review accepted the final source and representation correction; separate test/production owners assessed final shape. This qualifies consumer mechanics against scripted canonical ports and controlled HTTP only. Owner crypto/contributor completeness, actual dialogue codec/store, admitted public runner, coordinator/subscriber isolation and live provider/browser remain unqualified. The async text sink is trusted host code and must honor cancellation/context ownership. T018/T019 remain partial until their coordinator/save/history integration is verified; no T022/T023 completion or resource authority is inferred.

The reviewed canonical consumer is committed at `34aaf11071d0257c8d05f5f981dd691b6554dc18` and relayed to Integrator. Approved `705ecb799f7544e3114f66200fd79904ae029619` was then merged at `058f7b298672c4eb11037120e66f4aca3f2f27bc`; that merge changes integration documentation only. Both `fad7f11` and `f3db38db` remain ancestors on `oct14/chat`.

## Admitted runner — controlled RED, 2026-10-07

The next test group targets a public application-only runner consuming a confirmed dispatchable Business admission, canonical execution authority, exact embedded prompt/history, immutable selected profile and the same original budget. Integration accepted the narrow `ReauthorizeBeforeSendAsync` operation for its post-permit/pre-physical-send check; the operation exposes no capsule or substitute authorization DTO. The coordinator preallocates the terminal save operation identity. Generation facts are not a receipt or proof of physical settlement.

The fail-closed factory scaffold compiled against **40 tests: 19 passed, 21 failed, zero skipped**, `TestResults/isolation-18cd30bd552f4f80bbdbcc59ce2455e8/results.trx`. Positive creation paths fail at the null factory; rejection-only passes do not claim implemented validation. Tests include exact prompt/history inputs, narrowed durable/original monotonic deadlines, precise finish reasons, capacity checks through the sole owner codec, report accounting and authority changes while queued for a permit. Real dialogue persistence, host registration/client settlement and production route activation remain separate.

Initial implementation passed **40/40**, `TestResults/isolation-a64cf573878548029b1d40c0fe9e6dda/results.trx`. Source assessment identified an authorization-ordering gap at both typed dispatch and the post-permit callback: execution verification could await after an earlier Knowledge check. Two gated regressions reproduced one prohibited dispatch at each boundary (**0/2 passed**, `TestResults/isolation-66bf8b9c0cb54566a53dc48d8848eb4d/results.trx`). Both paths now await execution verification first and perform fresh full-union authorization last. Independent review accepted this correction.

The runner uses the actual embedded mode prompt and exact owner-codec history projection, never caller-supplied replacements. It merges history dependencies through the owner, only narrows the original monotonic deadline, admits one local run and returns canonical generation facts without a saved receipt or slot release. Capacity probes use the sole owner codec, actual retained identities/capsules, a valid maximum-width UTC timestamp and fixed-width measurement digest. Whole-envelope and array caps are checked before new material; these probes are never persisted. Report output is accounted before accepted footer state changes. Explicit authority loss returns sanitized facts; this does not recall already transmitted bytes.

**Final affected GREEN:** **583/583 passed, zero failed/skipped**, including all 42 runner cases, `TestResults/isolation-cc10983c70f942a399e5533ccb84e043/results.trx`; discovery 4,044/inventory 367, current build zero errors and 545 warnings. Independent review and separate owners accepted the scoped final shape. No full-suite, real codec/store, W1 registry/physical settlement or browser qualification is inferred. The actual host must bind the post-permit check, keep the same budget, settle both guard/admission owners and confirm canonical terminal persistence before declaring a saved turn. T016/T017 coordinator and T019 final save/history integration remain incomplete.

The separately reviewed mechanical bridge `007fd7d30f3ce9241fd2a8678f9b6d562139200f` exposes `OriginalCancellationToken` and `CreateDeadlineCancellation()` for Integration. Only visibility changed in that commit. Consumers link both tokens with their caller token, own/dispose the returned timer and retain budget/eligibility checks. The runner's deadline-narrowing behavior belongs to this later tested increment.

## Actual owner disclosure compatibility — controlled, 2026-10-07

Runner commit `6de2d394606d534e02a15afd747e3f7a72303469` was preserved while importing Integrator-approved `7cddddf687d252663cc03719441749b58694c4c0` at merge `9411415bc8715871c36df0104e352a1cce7a012c`. The single Budget visibility overlap was resolved with the exact tested Chat budget implementation; no source behavior changed in that resolution. The checkpoint includes Business's actual dependency protection/reader implementation and Integration's selected client session. Its pending noncooperative-readiness and durable-timer corrections remain owner work, not silently accepted runtime behavior.

Three new qualification cases use Business's existing `DisclosureFixture`: real reader, protected capsules, dependency union and consumed proofs over controlled published source/policy. They prove complete near-limit Chat wrapper bytes and genuine continuation, eager proof/report agreement, replay deduplication despite changed protected cursor bytes while charging both outputs, and uncited-search contributor denial without a guessed citation reread. Root independently checked the assertions and resource boundary. No alternate crypto, contributor reconstruction or production persistence was introduced; these tests qualify existing implementation rather than manufacture a RED phase.

The affected Chat/Knowledge/legacy/October.Integration selection passed **630/630, zero failed/skipped**, including these three cases, `TestResults/isolation-b38301cc82204b5c95dfe9e8e6bc4d33/results.trx`; discovery 4,181/inventory 377, build zero errors and 11,274 warnings. This is actual owner implementation under controlled data/authority, not live Identity, PostgreSQL dialogue, real provider/browser or complete W1 qualification. It does not authorize T023 or a separate real dataset.
## Canonical coordinator — controlled, 2026-10-07

Approved Business dialogue implementation `1b599c` was imported at `7804c520`,
then Integration client-readiness/durable-timer correction `e1f7ed0b` at
`f42f10169bfd7f517138e4abe4780052116a935e`. The original Chat contribution and
planning baseline remain ancestors. The new coordinator consumes these owner ports
and actual client session; it supplies no competing schema, codec or execution authority.

Initial fail-closed coordinator RED was **0/16 passed**,
`TestResults/isolation-0f6a2bf2e09844a997e038c1972a3fd0/results.trx`.
The first implementation passed **16/16** at
`TestResults/isolation-d39651a0646448698b1b9ce1ed53d40c/results.trx`.
Two post-drain authorization-loss cases then reproduced the retained-Running gap
(**0/2 passed**, `TestResults/isolation-70368382108041cdbc89e129514b3396/results.trx`).
Two lost-transition acknowledgement cases reproduced stale phase projection
(**0/2 passed**, `TestResults/isolation-1c5d9eb8626a46689c942f2ed7386693/results.trx`).
Corrections preserve explicit uncertainty and exact first-save recovery without
redispatch, and perform fresh complete-lineage authorization after actual drain.
Only the owner's sanctioned no-content authorization-failure closure can bypass
a denied Saving transition; missing context, unknown lineage and unsettled work cannot.

Two additional safeguard cases cover an ignored-cancellation phase operation and
a synchronously throwing save. These are post-implementation coverage, not claimed RED.
The frozen coordinator selection passed **22/22, zero failed/skipped**,
`TestResults/isolation-da1a5951642445a4b52fa39f7ebfffe8/results.trx`.
Tests use Business's actual SQLite store/codec and Integration's real client session
with controlled HTTP, clock and authority/commit doubles. They do not qualify the
root execution registry, PostgreSQL, real Identity/provider, host routes or browser.

Independent reviewer `wave_reviewer` accepted the corrections conditional on affected
GREEN. The host must still register original intent, actual execution/store tasks,
exact expected operations and settlement. Canonical missing-row lookup returns
Indeterminate, not proof of absence; attempted saves are never resubmitted.
Browser disconnect must detach presentation without cancelling the owned lifecycle.
T020/T021 remain partial, including current authorization at actual UI handoff and
clearing provisional content after a confirmed sanitized closure. T022 remains
separate from T023; no runtime or resource approval is inferred from controlled GREEN.

Final affected Chat/Knowledge/legacy-page/October.Integration verification passed
**720/720, zero failed/skipped**,
`TestResults/isolation-046b9ed63bdc469fb115b927c0f5c83f/results.trx`;
discovery 4,309/inventory 386, build zero errors and 19 warnings. This satisfies the
independent review condition. It is an affected SelfContained selection, not a full
suite or PostgreSQL/native/provider/browser qualification.
## Bounded presentation delivery — 2026-10-07

Coordinator `d99517a65a58868ad2f9836abe91ef8775f4812e` was independently reviewed
and committed after 720 affected controlled passes. Approved integration `d72d1b80`
was then merged cleanly at `4c808bb90583ce974a64ffe846a75dfd87fcca38`.
The next increment adds an isolated application delivery adapter, narrow opaque-lineage
callback overloads and authoritative terminal replacement in the existing view model.
It does not activate page sending, a route or runtime resources.

The initial eight focused cases compiled and failed against the fail-closed scaffold:
**0/8 passed**, `TestResults/isolation-08e065c8abeb4e07b6398a0fbf6f0b3d/results.trx`.
Seven failures were unavailable attachment/coordinator paths; the bUnit regression
showed an explicit sanitized terminal projection left prior prose visible.
The first implementation plus two reviewer-requested safeguards ran **9/10 passed**,
`TestResults/isolation-9cb8be71fab84bcbb8b313326df73336/results.trx`.
The remaining failure was a test setup mistake: empty history does not merge an
initial root. Correcting the expected starting capsule to the actual initial capsule
preserved all strict native-read updated-union assertions; that case then passed.
The byte-cap and native-read additions are coverage, not claimed pre-implementation RED.

Independent review found a separate real display gap: queued-delta denial detached
delivery before a later sanitized receipt could clear an already visible prefix.
The two denial/unavailability rows reproduced it (**0/2 passed**); the corrected
runner fixture passed in the same three-case run,
`TestResults/isolation-7c9308fcf141460c892e81473e4ee274/results.trx`.
The accepted correction adds an application-only content-free DisclosureSuppressed
event, bound to the original turn/context, with no durable terminal/save/idle claim.
Tests require cleared text/references, no late restoration and the actual owner-store
slot still occupied. This cannot recall previously delivered bytes or force a broken
subscriber to render; actual callback ownership and trusted UI handoff checks remain.

Final affected verification passed **795/795, zero failed/skipped**, including all
12 new presentation/lineage/VM cases,
`TestResults/isolation-d90c38a470d84f5e99363472087e653b/results.trx`;
discovery 4,424/inventory 392, build zero errors and 7,281 warnings, including AD0001
analyzer exceptions. Warning policy was preserved; these are not all claimed pre-existing.
Independent reviewer `wave_reviewer` accepted the complete increment conditional on
this GREEN, now satisfied. Separate production/test owners assessed their boundaries.
This qualifies controlled application delivery with actual SQLite owner receipts and
controlled provider transport, not PostgreSQL, live Identity/provider/browser or a full
SelfContained suite. No runtime services, migrations or provider calls were started.

Next demonstrable result is the trusted host registry/commit/death and route composition
driving this adapter against the shared qualified dataset. Integration remains its owner.
The reported Business dialogue commit/disposal race is a separate live-dialogue hold;
T022 must qualify the accepted owner fix and PostgreSQL before T023 live/provider/browser
execution. Consolidated resource/source/data/spend approval remains with Integrator;
controlled tests and integration checkpoints do not grant it. Safe Markdown, exact reader
links and actual page/browser wiring remain incomplete T020/T021 work.
## Encoded Markdown leaf — controlled, 2026-10-07

On clean `2d371f4f`, Integrator requested available bounded T020/T021 leaf work and
confirmed ownership of parent attachment/route composition. Inspection found no
installed Markdown/sanitizer package. The accepted narrow approach uses fixed Blazor
tags and encoded text only, with the supported subset and limits documented in
contracts/web-provider-handoff.md. No dependency/project/ChatPage change was needed.

Five initial component cases compiled: **1 passed, 4 failed**, with encoded HTML/URL
safety already passing and formatting selectors absent on the plain-text scaffold,
`TestResults/isolation-7529785738d54714b8a751f87ef62560/results.trx`.
A sixth coverage case checks dense syntax and oversized-input complete-text fallback;
it is not claimed as pre-implementation RED or a timing benchmark.

The final controlled Chat/page selection passed **402/402, zero failed/skipped**,
including all six renderer cases,
`TestResults/isolation-20a4b658e5374f58ac696abadc22bbcc/results.trx`;
discovery 4,430/inventory 392, build zero errors and 7,296 warnings including AD0001.
Warning policy was preserved. Independent reviewer `wave_reviewer` accepted the fixed
tag/encoded-text implementation, bounded flat parsing, incomplete-prefix visibility,
rich-DOM suppression and scoped inherited styling conditional on GREEN, now satisfied.
The broader combined owner suite was left to Integrator, avoiding duplicate qualification.

This is a conservative Markdown subset, not full CommonMark or actual browser/provider
qualification. Parent route/registry, exact authorized reader links and T022/T023
resource prerequisites remain. Integrator reports the specific Business store hold
lifted at approved checkpoint `964f530326e9d46c1a2ebeb7d6fa0d141a6d02d8`; it was not
merged into this dirty leaf increment. No migrations/services/provider calls were run.
## Saved-reference footer — released controlled slice, 2026-10-07

Chat fast-forwarded cleanly to Integrator-approved
`ba8051c4dc5759be6f0f17e9e1d5de59ffe72fdb`, preserving original `f3db38db` and
reviewed renderer `bd51e764`. After Explorer's read-only interface assessment and
Business's explicit contract/encoder/limit agreement, Integrator released the bounded
saved-projection T026/T027 adapter and leaf work. Parent page/registry/activation remain
integration-owned. The exact agreement is in contracts/web-provider-handoff.md.

The scaffold compiled against **22 controlled cases, 0 passed/22 failed/0 skipped**,
`TestResults/isolation-ed472d395e7b49cabc03f3e1568d4c5e/results.trx`.
Twenty-one failures were unavailable adapter paths and one was absent footer rendering;
these were not build or fixture failures. Tests use real Business disclosed-reader and
protected dependency fixtures for exact title/lineage/encoder behavior, separately from
actual SQLite saved-projection tests with explicitly controlled metadata ports. Their
different authority domains are not combined or represented as live integrated proof.

The accepted local display lifetime starts at Prepare and lasts ten seconds across
Handoff, with no reset. Saved-view suppression is binding-scoped and preserves durable
terminal/save-indeterminate facts; it does not reopen the completed stream sequence.
Live preterminal report projection and actual reader navigation remain outside this
initial saved-reference slice. No source/provider/browser/database service was started.

The initial implementation passed **22/22**, zero failures/skips,
`TestResults/isolation-41ac1ada124f4ca5a7e844780f3dc366/results.trx`.
Independent review then identified a seal/start ordering gap: retaining already-started
tasks protected completion accounting but did not prevent another owner/sink invocation
after sealing. Two deterministic reentrant attachment checks reproduced forbidden new
context dispatch in Prepare and Handoff (**0/2 passed**),
`TestResults/isolation-535d5e4adbe34ed59e8594f1461a6b02/results.trx`.
The later UI-sink race was source-review evidence, not claimed as separately reproduced.
The accepted correction uses one gate for seal and operation-start/retention, preserving
the original deadline and ownership of work admitted before sealing.

One additional post-implementation coverage case holds an actual footer apply callback
past the original deadline: Handoff returns, but Completion/Seal remain pending until
the actual callback settles. Repeated calls start no new work, and the trusted callback
checks cancellation/current attachment before applying, preventing late UI restoration.
This is coverage, not fabricated RED or a claim that cancellation recalls handed-off bytes.

Final controlled Chat/page verification passed **427/427, zero failed/skipped**,
including all **25 reference cases**,
`TestResults/isolation-16fc8edabd1f4cef977f06bafe1d401d/results.trx`;
discovery 4,526/inventory 399, build zero errors and 7,508 warnings including AD0001.
Warning policy was preserved. Independent reviewer `wave_reviewer` accepted the full
bounded saved-reference increment and confirmed the final coverage/GREEN conditions
satisfied, with no remaining review blocker. The broader combined owner suite remains
Integrator's responsibility; this is not full-suite, PostgreSQL or browser qualification.

Implemented: saved owner projection to exact authorized document-title/local-link rows,
complete original-plus-display lineage, bounded separate display lifetime, atomic
operation-start/seal ordering, truthful retained-task settlement, binding-scoped saved
display suppression and safe local footer rendering. Parent attachment/page wiring,
live preterminal reports with actual consumed-proof validation, and actual reader
navigation remain unfinished; no T031 or full C2 completion is claimed. No runtime
resource or source/data/provider/spend approval is inferred from these controlled tests.

## Canonical history and read-only saved display — released 2026-10-07

Before new edits, Chat fast-forwarded cleanly from `d4d0b049` to approved integration
`8a612a24c07a7e42e4f1815028bfdcc7e21ee100`. Integrator released bounded T024/T025
and the read-only saved-display portion of T028/T029, with the exact root handoff in
contracts/web-provider-handoff.md. Business selects history/transcript; root retains
initial-union/request/admission/budget preparation, Coordinator invocation, parent page,
retry/composer decisions and runtime activation. No unrelated Business/Scan barrier is
introduced and no duplicate broad combined run is implied.

The new scaffolds compiled against **17 controlled cases: 1 passed, 16 failed, zero
skipped**, `TestResults/isolation-39f8feacf85e40e4ba4c671781ae4d97/results.trx`.
The already-passing case rejected invalid saved-view bindings. Fifteen failures were
unavailable read paths and one was the missing positive saved-view initializer.
Actual Business SQLite fixtures own suffix selection, whole-turn omission and saved
projections. Consumer wrappers prohibit lifecycle/write calls. No transcript serializer
or display-to-inference conversion is introduced.

Source inspection and Integrator confirmed the exact owner no-content orphan projection:
Interrupted/OwnerLost, null dependencies and empty question/answer/references. The
read-only consumer preserves that display shape without invoking orphan cleanup,
inferring death, granting inference history or claiming conversation idle/retry authority.
Its controlled fixture performs the owner transition separately from consumer reads.

The implemented read-only consumer passed **17/17 focused cases**, zero failures/skips,
`TestResults/isolation-8edc3ac0d80a4445baaccdf8469181ba/results.trx`, then
**444/444 affected Chat/page cases**, zero failures/skips,
`TestResults/isolation-c83522fce0e5456990a44193294dcf94/results.trx`.
The latter discovered 4,708 tests, classified 410 source files and built with zero
errors / 19 warnings. The earlier focused build reported 8,084 warnings including
AD0001; no warning policy was changed or full-suite result inferred.

Implemented scope: exact canonical history/codec handoff, separate owner-selected
transcript display, complete supplied-capsule reauthorization, original ten-second
attempt, actual late-work settlement, and fresh saved VMs without stream replay.
Failed reads stay unavailable; legitimate empty history grants no admission.
Root initial-union/admission and parent-page integration, retry/composer controls,
PostgreSQL qualification and provider/browser execution remain outside this increment.

Independent reviewer `wave_reviewer` accepted the frozen source and tests with no
remaining actionable blocker and confirmed both GREEN runs satisfy review conditions.
The current project-state pointers were rechecked: their open root-composition/runtime
claims still apply; no shared project-state file or root instructions were changed.

## Application activation — 2026-10-08, in progress

The user delegated parent-page attachment and bounded feature-local execution/lifetime
composition. Exact file ownership was agreed with Integrator before edits; shared DI,
Program, provider/context configuration and Business persistence remain with their owners.
The clean assigned branch fast-forwarded to approved
`f6be1b410bd5de08394d70e5e83017c6512fec51`, preserving `f3db38db` and `fad7f11`.
See contracts/web-provider-handoff.md for the agreed boundaries and owner prerequisites.

Initial unavailable scaffolds compiled, then **0/7 passed, 7 failed, zero skipped**:
`TestResults/isolation-bd39fe5ef681471395dfadc4eb05fea0/results.trx`.
Six registry cases failed because reservation was unavailable; the parent regression
exposed the legacy textarea when an October application was explicitly supplied without
an entry. These are actual behavioral failures, not build failures. Discovery was 5,073,
inventory 428, build zero errors / 14,410 warnings including AD0001; warning policy is
unchanged. This first group exercises ownership mechanics and the unavailable parent,
not canonical database cleanup or composed conversation qualification.

Business's per-turn store disposal and Integrator's exact prompt-input digest helper
are agreed dependencies pending import. PostgreSQL execution is currently assigned
exclusively to Business; Chat has started no PostgreSQL/provider/browser/native/host
resource. Actual Generate/provider configuration remains explicitly pending with the
user. Controlled results do not authorize that execution or claim application activation.

At a frozen source boundary, Chat safely fast-forwarded to Integrator-approved
`7005f96bc425265feda0770049eaf2f06f5781ed`, importing the public exact prompt-input helper
(`5d6c42a9`) and its owner's reviewed 16-case controlled evidence. All activation edits
were preserved; only the overlapping two-entry local suite inventory delta was
temporarily stashed, reapplied cleanly and its temporary stash removed. No reset,
shared composition edit or duplicate qualification run occurred. The canonical
store-disposal implementation remains pending its owner's accepted checkpoint.

The accepted owner lifecycle was then imported through approved
`260c57068757d0f39fe790c6794c3a738328675b` with the same safe inventory preservation.
Integrator reports owner 500/500 controlled and the exclusively assigned 62/62
PostgreSQL cases; these are owner evidence, not Chat application PostgreSQL qualification.
The concrete owner now supplies observable idempotent cleanup without a schema/Core change.

The first 25-case activation launcher crashed before build/test execution in the
policy scanner with `System.ExecutionEngineException` (illegal instruction while
traversing Roslyn syntax). An unchanged retry passed all policy gates and compiled:
**19/25 passed, 6 failed, zero skipped**, discovery 5,128, inventory 432,
`TestResults/isolation-e7f7be60c4d44750a5fcd5f02dc3f2a1/results.trx`.
Build: zero errors / 14,616 warnings. No policy was bypassed.
The missing-entry parent regression passed; five composed application cases failed
on unavailable Open/IsReady scaffolds. Registry passed 18/19: the late credential
case retained actual cleanup correctly but returned an already-sealed client instead
of refusing the factory result. No unsafe send was observed. A separate reviewed
NotStarted sanitized-closure gap requires an explicit fresh owner refusal test/fix.
Application implementation is now authorized against these actual behavioral failures.

The NotStarted closure regression selected three cases: **1 passed, 2 failed**, zero
skipped, `TestResults/isolation-15abc485e4ac42e8b3298191ea14fd6a/results.trx`.
The negative fabricated denial was refused correctly; genuine Denied and
AuthorityUnavailable cases were incorrectly refused. The correction extends the
fresh original-dependency probe to an otherwise verified empty NotStarted failure.
Separate source review also required atomic readiness/client construction, registry
ownership of actual session drain, and final service-scope disposal only after all
registry/store work settles. These source findings are not mislabelled as observed RED.

During that frozen boundary, Chat fast-forwarded without overlapping files to approved
`c5e19241a5741aaa1524c1c5149a78168780007e`. Integrator reports full baseline
SelfContained 3,991/3,991; this excludes the local uncommitted activation changes and
is not their final verification. No external qualification follows from that baseline.

The subsequent approved baseline is `c8d3ef0ebac924147392ec773b45249798832158`.
A build stopped on corrupt Git metadata before executing tests; this was an
infrastructure failure, not behavioral RED. After user repair, the single already
started controlled run completed: **28/37 passed, 9 failed, zero skipped**,
`TestResults/isolation-9c99c37fe2fe448a8f1d7a09ba36f157/results.trx`.
All 24 execution-ownership cases passed. Application failures cover six invalid
locator inputs, exact existing-conversation reload, uncertain-save reconciliation,
and cancellation terminal classification. Application activation is not yet green.

Following the user's second repair, `git fsck --full --no-dangling` passed with
exit 0; Integrator independently confirmed the same and lifted the Git/build hold.
The branch remains `oct14/chat` at the approved baseline, with `f3db38db` and
`fad7f11` included. All 16 source/document backup hashes matched before resuming
owned corrections. No lane metadata repair, reset, pruning or external backup copy
was performed. Source backups remain retained. PostgreSQL and live-resource gates
are unchanged by Git recovery.

The first post-repair restricted launcher stopped during build with exit 1 and no
compiler diagnostics (`isolation-82482581b416481da0fa43acda399b5a`); no tests ran.
Git fsck remained clean. The same focused check with approved process permissions
compiled and executed: **0/1 passed, 1 failed**, zero skipped, discovery 5,200,
`TestResults/isolation-e0d3e4cf2169405cbd0f9d0c927c8ea3/results.trx`.
The regression timed out waiting for final scope cleanup after actual canonical store
disposal had settled and its observer reported a controlled disposal fault. This is
observed RED for attempting remaining cleanup while retaining failed capacity; it
is not a claim that the underlying SQLite store itself failed disposal.

Expanded frozen application/registry qualification compiled and ran **47/51 passed,
4 failed, zero skipped**, discovery 5,206,
`TestResults/isolation-1990c598291947cf97c579c8246c06ed/results.trx`.
All 25 registry cases passed, including the new cleanup regression. Previously
failing locator and uncertain-save cases passed; cancellation also passed without
an Agent classification change, so the earlier failure is not causally resolved.
Two failures were bUnit stale event-handler dispatch during input (idle originating
context and explicit new conversation). Unknown Begin did not expose reconciliation.
The renderer authorization test observed old question text but has a known test
synchronization gap to correct before attributing that observation solely to code.
The full activation boundary remains non-green; source-free checks grant no runtime
provider or PostgreSQL authorization.

After correcting the renderer test's context-switch synchronization and strengthening
actual-save assertions, the next run selected 53: **47 passed, 6 failed**, zero
skipped (`TestResults/isolation-aa6ee77ea24242708b7a78dfb8f39334/results.trx`).
Unknown-admission lookup/retention now passes. The strengthened Denied closure
receives an actual Denied save instead of a canonical successful receipt; the
originating-context test still exposes old question text; two same-page locator
cases retain the prior conversation. These are observed behavioral failures.
The AuthorityUnavailable closure case hit the same bUnit input-dispatch race and
requires fixture synchronization. Cancellation reproduced with canonical
Failed/ExecutionFailure, Unknown provider finish and six partial-answer characters;
no save had begun before explicit cancellation. This remains under causal diagnosis,
not a reason to rewrite every failure as cancellation.

The next frozen run selected 55: **53 passed, 2 failed**, zero skipped
(`TestResults/isolation-f735b61cd64f48c282204376ddc55276/results.trx`). Actual
canonical empty saves, final identity rechecks and same-page locator changes now
pass. The two failures are explicit full-stack cancellation and its new deterministic
ordering regression: the original token is requested while linked propagation is
held; a cancellation-associated exception carrying the original token is incorrectly
classified Failed. The genuine-provider-failure-before-late-cancellation control
passes. This supplies causal RED for a narrow Agent classification correction;
no whole-answer buffering or general Failed-to-Cancelled rewriting is introduced.

With the reviewed narrow Agent correction, **55/56 passed, 1 failed**, zero skipped
(`TestResults/isolation-ec133480ca0348bbbb845688bbf1634e/results.trx`). Both the
original-signal propagation regression and actual application cancellation pass;
the pre-cancellation provider-failure control remains passing. The only failure is
the new pending-locator test: after A's actual owner lookup/cleanup settles, B is
not opened. Its correction must consume only the latest requested locator after
actual settlement, with no persistence polling or generation replay.

The first broader affected boundary (`OctoberChat|OctoberWiki|ChatPageTests`) passed
**594/594**, zero failures/skips, discovery 5,211,
`TestResults/isolation-e9858088aff34c32b8b58c481f928e9e/results.trx`.
Final independent source review nevertheless identified an open-flag reentrancy
window: a navigation handler that skipped a busy open could clear the actual open's
flag and let pending navigation race its stale continuation. The narrow flag-ownership
correction is review-driven (not fabricated RED) and requires a fresh affected run.

Final independent review accepted the owned activation increment after that correction,
with no remaining actionable source findings. The exact final affected rerun passed
**594/594**, zero failures/skips, discovery 5,211,
`TestResults/isolation-9e380300c462417c9d70df23f948d282/results.trx`.
The filter was `FullyQualifiedName~OctoberChat|FullyQualifiedName~OctoberWiki|FullyQualifiedName~ChatPageTests`
intersected with SelfContained through `eng/test.ps1`; policy/inventory/guard probes
passed. No assertions were weakened and no external suite was selected.

Implemented production code now attaches parent ChatPage to the application, finite
execution registry, canonical per-turn preparation/store observation, existing genuine
stream/tool presentation, canonical saved transcript and exact references. Controlled
actual-SDK transport proves one conversation and a follow-up with exact owner history,
immediate first text before provider completion, detach versus cancel, uncertain-save
lookup without redispatch, and existing-locator reload/navigation. This is controlled
consumer conformance, not a live provider/browser result or PostgreSQL application
qualification. Shared Web DI/configuration and the three additional PostgreSQL cases
remain Integrator dependencies; live provider/browser approval remains separate.

An unresolved prior-host active turn remains unavailable without authoritative owner
recovery. Empty sanitized closure without an authentic complete generated capsule is
limited to freshly verified whole-selection refusal; document-specific uncertainty
stays fail-closed. The host registration and exact proposed PostgreSQL resource scope
are recorded in contracts/web-provider-handoff.md. Source backups remain retained.
