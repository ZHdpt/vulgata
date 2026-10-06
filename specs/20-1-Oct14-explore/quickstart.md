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
