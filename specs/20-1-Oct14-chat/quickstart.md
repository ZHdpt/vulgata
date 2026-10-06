# OCT-CHAT validation guide and bounded K0 preparation evidence

The feature-verification scenarios below remain planned, not executed. Only the explicitly authorized initial K0 consumer preparation recorded at the end has run; it is not Chat implementation/runtime qualification. Source artifacts and common fixture identities must be pinned for each claimed boundary.

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

`ChatPage` accepts optional `OctoberEntry` and `OctoberSelection` parameters for controlled feature-local composition. Without October opt-in, the existing legacy path remains the default. Explicit October composition renders selection/loading/empty/unavailable state with Chinese mode themes, confirmed new contexts and a disabled send action; losing a previously supplied October binding fails closed rather than exposing legacy data. The VM clears old owner/stamp metadata immediately and never treats Active/SaveIndeterminate as settled; it does not implement durable admission or generation. Real ordinary-reader services, W0/D0/W1 and actual host/runtime qualification remain integration prerequisites. Implementation is left uncommitted pending further instruction; no merge/push or subsequent task was performed.
