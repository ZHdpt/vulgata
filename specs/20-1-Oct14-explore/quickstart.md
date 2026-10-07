# OCT-EXPLORE future validation guide

These are planned checks, not executed results or runtime permission. Implementation must first be authorized, B0 checkpoint supplied and feature code/tests delivered. All commands run from this assigned worktree. Never run the Web host against ambient/shared databases: startup migrates existing Identity/domain schemas.

The guide below remains prospective for E0–E3; the bounded T001–T003 execution record at the end is actual controlled conformance evidence and overrides none of the later runtime prerequisites.

## Prerequisites

- Same compiled B0 contracts/fixture consumed by all lanes; Business real store/live policy for real-data checks.
- Integration-owned Web composition and explicit permitted batch/publication, existing ordinary IT/Business accounts, declared loopback port and owned database resource. Business owns October schema application; Web must not migrate October.
- Feature test files classified in existing `tests/Vulgata.Tests/TestInfrastructure/suites.json`. Proposed test names below must exist and select nonzero tests before interpreting results.
- Real source-backed publication from Scan/Business for combined proof; same publication in Chat, graph and reader. Provider/native tool execution remains with owning lanes and requires its own authority.

## Controlled verification

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Explore'
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
```

Expected cases: ordinary non-manager exact CL/BL reading; older exact version and section preserved; invalid/missing section and unknown/wrong/unpublished target never substitute; Business exposes no CL; contributor revocation/outage clears safe state; untrusted markup cannot execute; coherent refresh and obsolete response suppression; no-wiki feature remains unavailable.

F3 continuation checks (planned, not run): use the Business owner's shared types and labelled fixtures for a Document >64 KiB and an individual section >64 KiB, plus boundary-sized projections around 65,536 UTF-8 bytes including multibyte characters. Each response must respect the owner's cap and preserve valid UTF-8/structured chunk semantics; following explicit next actions to the owner's terminal result must reproduce the full authorized narrative text in order, without gaps or duplicate narrative fragments. Intentionally repeated qualifier context beside continued claims is required, not duplicate narrative traversal. Count bytes of the owner-defined text/JSON projection, not characters; framing/qualification metadata must not justify exceeding that declared projection bound.

Open the oversized exact section directly and confirm first-portion focus, visible `本节尚未加载完`, keyboard-accessible `下一部分`, preserved adjacent qualifications on continued claims and section-scoped terminal feedback. The whole-Document path must likewise remain visibly partial until terminal. Verify one request per action, one displayed chunk only, no auto-drain, and duplicate-click/late-response suppression. Make a newer version available mid-read: every next request still addresses the original exact publication/version/section.

Between chunks, exercise mode/scope/subject/purpose/selection/version/section change, cancel, contributor revocation, authority outage, timeout and tampered/expired/wrong-operation cursors. Expected: no partial cancelled page, old display/qualification/cursor cleared, late results discarded, no mixed retained chunks or completion claim. Explicit restart uses a fresh authorized selection at the first chunk of the same exact target; no old cursor or latest-version fallback. Repeat persisted continuation behavior under the owned PostgreSQL qualification below after implementation. Initial shared-contract compile/conformance probes may establish B0 before dependent production wiring when separately authorized; this document correction runs none.

Graph tests cover 39/40/41 nodes, 79/80/81 Edges, empty/sparse data, cycles/self-loops, bounded acquisition and cancellation. Every displayed Edge preserves accepted identity/endpoints/type/direction. Compare paired fixtures differing only in denied records: visible payload, counts, continuation/incomplete signal and empty-state text must be identical. Unknown permitted coverage is labelled incomplete, not repaired by invented Edges.

After final feature correction, run the full SelfContained suite once through `eng/test.ps1`. Further repeats require changed behavior or unresolved failures. Controlled tests do not prove provider usefulness, actual authorization data binding or real PostgreSQL behavior.

## Owned persisted-read qualification

After runtime authorization and integration prerequisites:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~Explore'
pwsh -NoProfile -File eng/test.ps1 -Suite E2E -WithPostgreSql -Filter 'FullyQualifiedName~Explore'
```

The runner creates owned resources; do not supply a shared production connection or run cleanup against ambient targets. Zero selected/all-skipped tests, setup failure or missing TRX outcomes are non-success. If the future E2E implementation has a different classified name, use that verified filter and report it rather than pretending these proposed tests ran.

## Browser and combined case

Use integration's approved host start/configuration command only after confirming its database/port/process boundaries. No guessed production launch command is provided here.

1. Log in as a permitted ordinary non-manager. Navigate from the integration-provided entry to `/knowledge/documents`; IT opens real published CL and its exact section.
2. Open real BL in Business/light. Attempt an exact CL link without switching mode: no CL metadata appears. Verify no-BL empty state on a controlled selected publication.
3. Open `/knowledge/graph`; inspect real accepted direction/meaning and genuine Uncertainty. Open a node, then the matching Chat footer reference. Compare exact publication/version/section identities, not only similar titles.
4. Pan/zoom/fit, select and open with keyboard controls; verify focus, readable Chinese loading/error messages, light/dark boundaries and the accessible list alternative.
5. Refresh and switch scope/mode while delayed responses arrive. Verify a coherent selected publication and no stale protected results. Test unknown publication, authorization outage/revocation and missing data using owned fixtures/resources; restore only the owned state changed by the check.
6. Show a bounded result with visible limits. Business graph may be sparse or edge-free: confirm no hidden CL structure or manufactured BL-to-BL links.

Record code checkpoint, contract checkpoint, explicit publication, safe exact handles, permitted account roles, source-slice coverage, accepted-edge comparison and known limits. Keep credentials/raw source out of evidence. Distinguish controlled layout/policy tests, persisted reads and real generated cross-Repository usefulness. Integration owns final combined build/SelfContained and affected runtime checks; do not rerun unrelated native tools from Explore.

## Initial contract import and acknowledgement — T001–T003

Bounded preparation authorized by OCT-Integrator dispatch from chat `01a1109d-ec4c-7500-8b73-642a5e22f59b`: checkpoint planning, merge the exact integration commit, add initial lane conformance tests/inventory, run the isolated SelfContained launcher, commit and stop before T004. No full Wave 2 execution or other workflow/skill was invoked.

- Planning checkpoint: `a4ab3157d8e7a2d25e121b274daa411109b6e97b` preserves the accepted specification/plan/F3 review/tasks as they stood before import.
- Exact imported integration checkpoint: `3bcd815b09cfbac7418d6644a2a75816e0788ec0`; Business candidate `07c3fe4463e3228a6982fd106b58148ca87974bd`, documentation handoff `7a189c34aec625202f74846d4907ded787ccbdca`. Local merge: `8bc1903c5c88b7fcb76b8cb61432c25d270029b4`. Ancestry check returned 0, on `oct14/explore`; no reset/history rewrite or counterpart edit.
- Actual symbols/limits are recorded in [consumer-boundary.md](contracts/consumer-boundary.md#imported-symbol-binding--initial-t001t003-preparation). Local links preserve all four Guid fields including LogicalDocumentId and string SectionId. Chat/integration route adoption remains pending, not inferred concurrence.
- Separate `contract_test_author` authored only `tests/Vulgata.Tests/Explore/KnowledgeReadContractTests.cs` and its exact SelfContained inventory entry. Root owns documents, launcher and Git. No production-code owner was activated. No T004 ExploreFixtureHost or presentation code exists from this dispatch.

### Exact verification

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.Explore.KnowledgeReadContractTests|FullyQualifiedName~Vulgata.Tests.October.Knowledge'
```

First attempt stopped during compilation with CS0121 at the new test's target-typed constructor in a validator call. `TestResults/isolation-e613651373fb406fa3318d4e45fdf7d6/build.log` records this failure; no tests ran and it is not behavioral RED. The test author corrected that call to explicit `RelatedKnowledgeRequest`, without changing imported production code or assertions.

Corrected run: **SelfContained discovered 3,389; selected 60; passed 60; failed 0; skipped 0**. TRX inspection independently separated **10 Explore consumer cases** from **50 imported Business cases**. Evidence: `TestResults/isolation-e21f53e5007c4d4db3876da43971f8a0/results.trx`, `execution.log`, `discovery.log`, `build.log` and `guard-probes.log` in this worktree. Build reported 0 errors and 5,102 warnings, including an AD0001 analyzer exception; successful compilation is not a claim of a clean/full analyzer run. No analyzer configuration was relaxed. Repository policy checked 321 classified files; launcher outcome/timeout/cleanup/fixture guards passed. This is a focused selection, not a full SelfContained run.

Corrected test SHA-256: `BE4AD15A69B3E2B03FA440354EC81BDD75D5E1F79994E75EF65F79E625D31B0F`. Independent reviewer `independent_plan_review` accepted the bounded T001–T003 changes conditional on the corrected run passing; that condition is satisfied. It confirmed canonical-only consumer types, meaningful ten-case coverage, exact single inventory addition and no T004/production implementation. These are conformance tests over existing imported behavior; no artificial RED/GREEN production cycle is claimed.

### Inventory and ownership for later work

Only the first row is added now; all future entries below are proposals for their owning task, not created code or advance permission. All paths are relative to `tests/Vulgata.Tests/`.

| Path | Suite / disposition |
| --- | --- |
| `Explore/KnowledgeReadContractTests.cs` | SelfContained — added and verified now |
| `Explore/ExploreFixtureHost.cs` | SelfContained — T004, not started |
| `Explore/ExactReaderTests.cs` | SelfContained — T005 |
| `Explore/ExactContentContinuationTests.cs` | SelfContained — T006 |
| `Explore/ReaderComponentTests.cs` | SelfContained — T010 |
| `Explore/ExploreCompositionTests.cs` | SelfContained — T012 |
| `Explore/ExplorePersistedReaderTests.cs` | PostgreSql — T015 |
| `Explore/KnowledgeGraphProjectionTests.cs` | SelfContained — T018 |
| `Explore/KnowledgeGraphAuthorizationTests.cs` | SelfContained — T019 |
| `Explore/KnowledgeGraphViewModelTests.cs` | SelfContained — T023 |
| `Explore/KnowledgeGraphComponentTests.cs` | SelfContained — T024 |
| `Explore/ExplorePublicationE2ETests.cs` | E2E, with owned PostgreSQL — T029 |

### Consumer acknowledgement and remaining prerequisites

**Explore acknowledges the imported candidate is usable for initial controlled E0 contract consumption.** This is not production B0, full E0 completion or global accepted-K designation; integration owns collecting all consumers' acknowledgements. Business real persistence/Identity policy, section hierarchy and large-content budget/splitting, protected cursor expiry/cancellation, GENERATED_FROM fixture coverage and graph-wide accumulated disclosure remain unqualified dependencies. The fixture has two small scripted chunks and a REFERENCES relationship; its mutable policy is not real Identity. UI cancellation/races and Chat footer wiring were not implemented or tested. No competing DTO/policy/schema compensates for missing seams.

Integration still owns ordinary Web registration/navigation/selection and final runtime resources; Business owns the real store and all October migrations; Chat owns footer adoption. No database, migrations, host startup, provider calls, source Scan or native analysis tools ran. The standard SelfContained launcher's own local process/timeout guard probes ran as part of the approved launcher. No user/machine PATH/global settings or unrelated process was changed.

Pause **before T004**. Only T001–T003's initial preparation is complete; the remainder stays unstarted. No push is performed. The final preparation commit identifies this record and its exact scoped changes; no self-referential commit hash is embedded here.

## Controlled E0 implementation — T004–T013

The later user instruction **"Implement Controlled E0: T004–T013"** supersedes the initial-preparation pause above for E0 only. This worktree fast-forwarded from `aa63ca157dc20f0047999b8ae53663425a0fb504` to accepted common CP-K1 `12d92d80339ea622cd26c593ebdb74da59401f91` before implementation. T004–T013 are complete against controlled data; **stop before T014/E1**. No graph, real B0/store, provider/native analysis or shared Web composition is included.

Direct `speckit-implement` was restricted to those task IDs with the absolute feature-directory override. Requirements checklist: 16 checked, zero unchecked; not edited. No before/after-implement hooks are configured; agent-context hooks/root instructions were not changed. Existing .gitignore/.dockerignore already cover the generated .NET/test outputs.

### Delivered surface and ownership

- Shared presentation wrappers/validator, `IExplorePresentationService`, exact reader/browse facade, opaque single-use instance/context-bound continuations, bounded current-chunk ViewModels and feature-local DI.
- Ordinary `[Authorize]` routes `/knowledge/documents` and `/knowledge/documents/read`; safe query parsing, all four Guid identities and optional string section, no query-based mode elevation, Chinese Fluent controls and Library Oak light/dark styling.
- Encoded content/qualified claims/limitations/coverage, Chinese Document type, IT-only safe relative source provenance and exact-section focus. Explicit next/reopen/cancel states; context/authentication events clear protected content and invalidate in-flight results. No accumulation or auto-drain.
- Missing module/dependencies render unavailable; no automatic fixture, legacy wiki, current-version or source fallback. No source viewer. Integration registration and trusted context/event requirements are specified in [consumer-boundary.md](contracts/consumer-boundary.md#implemented-e0-composition-handoff-t012).

`contract_test_author` exclusively owned five new test/helper files and their exact SelfContained inventory entries. `e0_production` owned the Shared/Explore, Web.ViewModels/Explore, Web/Explore and reader/browse/component files. Root owned orchestration, Git import, launcher runs and documentation. `independent_plan_review` reviewed the final code read-only. No production or test owner edited the other's files. Shared Core/Infrastructure/Agents, Program/Routes/layouts/packages and Business contracts were unchanged.

### Honest RED/GREEN and verification

Focused command: `pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.Explore'`.

| Boundary | Actual result | Evidence directory under TestResults |
| --- | --- | --- |
| Initial behavioral RED against compile-only skeletons | Build succeeded; 43 selected: 10 existing probes passed, 33 new cases failed, 0 skipped. Failures were missing behavior/DI/rendered controls, not compilation errors. | `isolation-28296ab27ab648c9bcfc79f18413923b` |
| Review regression RED after original implementation | Build succeeded; 45 selected: 41 passed, 4 failed, 0 skipped. Failures: two malformed-Unicode tests and two mode-specific type/provenance assertions. | `isolation-18ae02286c7747a8a585d7add7d1fd8b` |
| Final focused GREEN | 45 selected, **45 passed, 0 failed, 0 skipped**. | `isolation-5cf8852f60774165bd85eac5fd4cf166` |
| Full SelfContained regression | `pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained`: 3,453 discovered, 2,405 selected; **2,405 passed, 0 failed, 0 skipped**, launcher exit 0. Qualification suites excluded, not passed. | `isolation-be981bf4400945ab9cb92ddd9c604987` |
| Explicit Web build | `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`: exit 0, **0 errors, 15 dependency warnings**. No package/analyzer settings changed. | `isolation-be981bf4400945ab9cb92ddd9c604987/web-build.log` |

Each test directory contains `results.trx`, discovery/build/execution logs and guard evidence. Root checked persisted counters; repository policy covered 329 classified files. Standard launcher policy/outcome/timeout/cleanup/fixture-exclusion checks passed. Initial test-count estimates were corrected to actual discovery (33 initial new cases, then two added regression cases, plus ten prior conformance cases = 45). The full run's 2,405 selected tests are the actual SelfContained population; 3,453 discovered is not a passed count.

The independent review required rejecting malformed Unicode rather than JSON/UTF-8 replacement and preserving Document type/provenance. Separate regression tests failed before their production correction. Final review found no remaining code blockers and accepted controlled E0 conditional on focused GREEN, full regression and Web build; all three conditions now passed. The concrete T012 handoff is recorded. No subsequent production/test changes were made after these passing checks.

### Practical evidence limits and next increment

The harness uses the imported labelled fixture plus scripted response/delay probes. Tests demonstrate >64-KiB total Document/section traversal through locally bounded supplied chunks, exact handle/qualification preservation, encoding safety, denied outputs, cancellation/context races, components and DI. They do not implement or qualify Business splitting, cryptographic cursor expiry, real live Identity policy, production persistence, actual browser keyboard/theme behavior or real source-backed generation. Component tests exercise controls/markup and focus calls with bUnit; no browser or application host was launched.

CP-K1 has no canonical section catalogue/schema discriminator; E0 renders the supplied Content/qualified Claims and does not invent a competing schema. The conservative full-ContentPage default-JSON 64-KiB guard can reject chunks a producer counts differently. Agree the canonical byte-budget representation with Business before real E1 and qualify actual content; no claim of exact production size interoperability is made. Graph-wide final disclosure and GENERATED_FROM graph coverage remain outside E0.

Integration must supply trusted `IExploreContextProvider` with reliable ContextChanged notifications, the canonical reader and existing authentication provider, opt-in registration/navigation, and Chat link adoption. Real store/Identity/publication data and owned runtime resources are E1 prerequisites. At E0 completion, **T014–T032 remained unchecked/unstarted**. No database, migration, provider, source tool, host startup, global setting or unrelated process was used or changed. The standard isolated launcher's own local process guard probes are included in its authorized execution. E0 was subsequently committed on user instruction as recorded below; no push was performed.

## Controlled E2 prerequisite assessment — T018–T027

On 2026-10-06 the user instructed: **"Move on to T018–T027: controlled graph. Commit previous changes first"**. The verified E0 changes were committed first as `50be57826cf803e506e0442aa4ed6c556244f771` on actual branch `oct14/explore`; planning baseline `fad7f11` remains an ancestor. The absolute `SPECIFY_FEATURE_DIRECTORY` was supplied to the direct SpecKit prerequisite helper. The requirements checklist remains 16/16 checked; no implementation hooks are registered, and no agent-context hook was run.

Independent read-only prerequisite review by `independent_plan_review` at that commit found **CP-K1 cannot satisfy the accepted final accumulated graph-disclosure requirement**. Individual reads do not revalidate previously acquired nodes/relationships/Uncertainties. Their results carry neither the full contributor union for `IOctoberKnowledgeDisclosurePolicy` nor exact relationship-version identity. A repeated bounded read pass only moves the revocation window. Reconstructing contributors, inventing a verifier or silently dropping final authorization would violate the accepted plan and owner boundary.

This activates existing `plan.md` final-disclosure/stop conditions; it is a missing dependency, **not a fresh human approval gate**. The required OCT-BUSINESS amendment and compatible shared-checkpoint acceptance cases are recorded in [consumer-boundary.md](contracts/consumer-boundary.md#controlled-e2-prerequisite-review-2026-10-06). No tool, Scan strategy or PRD change is proposed. No cross-chat message or shared-owner file modification was made.

The separate test author added `GetRelatedAsync_LaterUnrelatedSuccess_DoesNotRevalidatePreviouslyAccumulatedMaterialAsync` to the already classified `Explore/KnowledgeReadContractTests.cs`. It acquires permitted seeds/relationship, revokes Repository A, then observes successful unrelated Repository B reads while A's exact read now returns metadata-free Unavailable. Earlier returned values still exist in consumer memory. This is a limitation probe against the unchanged owner fixture, **not behavioral RED or completion of T019**, and does not claim a failing owner per-call contract.

Verification: `pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.Explore.KnowledgeReadContractTests'` exited 0; 3,454 discovered, **11 selected/11 passed, 0 failed, 0 skipped**. Evidence: `TestResults/isolation-4c6da5f4a1aa46a3b4006478376ec341/results.trx` and adjacent build/discovery/guard logs. Excluded suites were not qualified. Independent review accepted the scoped probe and amendment conditional on this launcher result; that condition is satisfied. The executable probe demonstrates its specific interleaving; the general reread limitation is a contract-analysis finding. E0 production is unchanged, so its recorded full-suite/Web-build evidence was not rerun for this test/documentation-only update.

T018–T027 remain unchecked pending the shared owner capability. E1 and E3 remain outside this dispatch. No graph source/UI, competing Knowledge DTO/schema, fixture runtime fallback, host/database/provider/native tool or shared Web composition was added.

## Resumed E1 qualification — 2026-10-07

The user explicitly resumed T014–T017 and graph T018–T027 once the Business-owned aggregate contract is agreed/available, authorized messages to OCT-Integrator `01a1109d-ec4c-7500-8b73-642a5e22f59b`, and required scoped reviewed commits. This supersedes the earlier pause. No skills were loaded. Verified assigned worktree/branch `oct14/explore` initially at `50be5782` with exactly the four assessed pending files. Reviewed unchanged probe/docs and preserved them in `422afc86`; their prior 11/11 result and independent acceptance remain recorded above. No reset, blanket staging or lost work.

### T014 code and resource checkpoint

Approved integration `082994eb0a6ee0084efff0a21357c59e86b60fb5` was merged without conflict as `0e565bac58efa4f100ea8ab0975ed8436c9f87f6`. It contains Business B0a/B0b through T018 at `b65213ad5b5f5f26eeffc5d7d9c09db6b608bf55` and reader composition `c0cf972f2b5743f8017f462101a4ba8d4dd022f2`; `fad7f11` remains an ancestor. The canonical handoff is [October reader integration](../../docs/bmad/planning-artifacts/october-reader-integration-2026-10-07.md).

Verified `Program.cs` registers `AddOctoberWikiReader` with the frozen DefaultConnectionSnapshot. `OctoberWikiRegistration` configures the actual reader, live owner policy and scoped authenticated Explore context from explicit `October:Wiki` publication/mode/repository scope. Missing/malformed configuration fails closed; no current-version/fixture fallback, operator credentials or producer/provider registration is added. October migration remains explicit owner work, absent from ordinary Web startup. Shared composition files were imported, not edited by Explore.

The reproducible PostgreSql recipe uses `eng/test.ps1 -Suite PostgreSql`, a fresh runner-labelled container/volume and `DisposablePostgreSqlDatabase` inside the test; the test explicitly applies the owner migrations and creates controlled semantic CL via actual acceptance/freeze/publication APIs. All database/publication IDs are ephemeral and are removed on cleanup. No expired integration IDs or ambient connection strings are used. The approved integration case filter is `FullyQualifiedName~Vulgata.Tests.October.Integration.OctoberWikiPostgreSqlTests`; new Explore cases use `FullyQualifiedName~Vulgata.Tests.Explore.ExplorePersistedReaderTests`.

The first current-source PostgreSql attempt stopped before build/provisioning/tests because Docker Desktop's Linux engine pipe was absent; launcher exit 1, `TestResults/isolation-423c0e4b6c2845199b9733e14975c183/docker-readiness.log`. This is prerequisite failure, not behavioral RED or persistence qualification. Integrator was informed for consolidated runtime coordination. No daemon/global setting, unrelated process or database was changed.

Available current-source SelfContained baseline passed **78/78 selected, 0 failed/skipped** with filter `FullyQualifiedName~Vulgata.Tests.Explore|FullyQualifiedName~Vulgata.Tests.October.Integration|FullyQualifiedName~Vulgata.Tests.October.Knowledge.OctoberKnowledgeAuthorizationTests`; evidence `TestResults/isolation-aa9de5c6945b467dbe8e2590d87e4107/results.trx`. This separately exercises SQLite-backed publication/Identity and controlled Web composition, not PostgreSQL or real Scan.

### T015/T016 owned compatibility work

The separate test author owns `ExplorePersistedReaderTests.cs`, the single exact PostgreSql suite entry and the configured-claims cases in `ExploreCompositionTests.cs`; the production owner owns only `ExplorePresentationService.cs`/`ExploreModule.cs`. Root owns Git/runs/docs. Independent review is read-only; no shared Business or integration implementation was changed.

Four PostgreSql cases now compile and are independently accepted for execution: exact >64-KiB Unicode whole-Document and section reconstruction with repeated qualifications and full response byte caps; immutable publication membership/history plus wrong batch/publication/version/section and Business CL exclusion; denial/outage/cancellation/auth-change with explicit same-target restart; and actual Identity security-stamp revocation through the production selection authority/policy/reader/facade. The last case uses PostgreSQL Knowledge and owned SQLite Identity/management; it does not claim an all-PostgreSQL Web host. Knowledge rows originate only from actual acceptance/freeze/publication APIs with labelled controlled semantic/completion/producer inputs. Existing publication-history tests do not prove a changed Document-Version chain. No persisted BL is fabricated.

A concrete compatibility defect required a source correction: imported Identity/context components honor configured user-ID/security-stamp claim types, but Explore hardcoded default types. The facade now receives host IdentityOptions via feature DI and reads both bindings from exactly one authenticated identity. It does not fall back to conflicting legacy claims, reconstruct contributors or change the owner policy. Existing uniform optional-stamp behavior remains; real context/Identity policy requires the stamp. No fixture label relaxes authorization.

| Check | Actual result | Evidence under TestResults |
| --- | --- | --- |
| First custom-claims baseline attempt | Compilation failed due to a missing test-only `Vulgata.Web.Data` import in the new PostgreSql file; corrected by test owner. Not behavioral RED. | `isolation-6e1a7ea86edf476891cff3693df1ce13/build.log` |
| Custom-claims behavioral RED | Build succeeded; 2 selected, 0 passed, 2 failed, 0 skipped. Custom claim names incorrectly denied; mismatched configured stamp incorrectly allowed. | `isolation-ab8883b1ce694bbea375aa543d152151/results.trx` |
| Corrected focused GREEN | 80 selected, 80 passed, 0 failed/skipped; same Explore/integration/owner-authorization filter as baseline. All four PostgreSql cases compiled, not executed by this suite. | `isolation-76c0bcd4ca4646668cc1d903689d37a6/results.trx` |
| Full SelfContained regression | 3,854 discovered; **2,778 selected/2,778 passed, 0 failed/skipped**, launcher exit 0. External suites excluded, not passed. Policy covered 361 source files. | `isolation-1257f995462445d28ec85c7d718d4707/results.trx` |
| Explicit Web build | `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`: exit 0, **0 errors, 15 existing dependency warnings**. No build/analyzer settings changed. | `isolation-1257f995462445d28ec85c7d718d4707/web-build.log` |

Independent review accepted the correction conditional on GREEN/affected regressions; the focused result satisfies those conditions. It accepted the four PostgreSql cases for execution, explicitly not as runtime qualification. Their pending command is `pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~Vulgata.Tests.Explore.ExplorePersistedReaderTests|FullyQualifiedName~Vulgata.Tests.October.Integration.OctoberWikiPostgreSqlTests'` after Docker availability is restored through authorized coordination. Do not weaken suite guards or substitute an ambient database. T015–T017 remain unchecked pending their unmet verification/data obligations.

### Remaining data and graph dependencies

T017 real generated CL awaits Integrator's one coordinated Scan S2 run and retained publication: approved owned database, exact binding/Document handles, schema, ordinary user/key/log profile and host/port/start-stop boundaries. Real BL additionally needs Business B2. Reuse that publication; Explore does not generate a second dataset. Controlled persisted inputs never close either real-data milestone.

The complete accumulated-graph requirement was sent to Integrator: nodes, exact accepted relationships/direction/meaning, Uncertainties, labels/qualified text and coverage/count/incomplete signals must all receive final live all-contributor authorization under the same publication/subject/authentication/Graph-purpose/mode/scope. Business owns contributor resolution and the canonical mechanism. No guessed contributor union, competing policy or repeated-page authorization substitute is allowed. Final authorization fits within eight calls/ten seconds, 40 nodes/80 Edges and one-hop bounds. Graph has no Chat-dialogue-persistence dependency. T018–T027 resume on the agreed owner checkpoint, without another routine approval gate; no graph capability is claimed yet.

### Reviewed checkpoint and latest synchronization

The owned correction/tests/qualification record was committed as `d28ba2369ea9b60fefae57c122b7f18814705e5b`. Integrator then supplied approved preparation `0949d6337cb09a297ca81a6a992d384af6d82b2a`, adding separate Chat-purpose host context while preserving Reader semantics. It was merged into the clean assigned branch as `b56b5c4e29d8128f27923e2967d492e60628371f`; no owned edits were lost, and both exact suite entries were preserved. The same focused Explore/integration/owner-authorization command passed **91/91 selected, 0 failed/skipped** after this merge (`TestResults/isolation-c2c2045df6d944b9a948d872cf72b101/results.trx`, launcher exit 0; 362 classified sources). The 2,778-case full-suite result above belongs to the preceding owned checkpoint, not an invented full replay of the later integration preparation. No further production/test changes followed the 91-case pass.

Integrator has consolidated Docker/startup resource readiness with the user; no additional runtime startup authority was received here. The four PostgreSql cases remain unexecuted. Exact owner graph candidate review and the shared retained real publication remain pending. Next usable UI milestone: an ordinary IT user opens Integrator's retained source-generated CL through the existing exact reader; subsequent controlled graph UI follows the agreed available aggregate mechanism, independent of Chat persistence.
