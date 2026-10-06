# OCT-SCAN validation and handoff guide

This is a future validation guide, not executed evidence. No Generate implementation or new tests are present from this planning request. Follow [plan.md](plan.md) stop conditions and [contracts/scan-boundaries.md](contracts/scan-boundaries.md).

Subsequent bounded preparation imports integration `3bcd815b09cfbac7418d6644a2a75816e0788ec0` and permits only T001–T004 controlled consumer probes using the imported Core/Shared types and fixture. See `conformance-c0.md` for actual results; all feature/runtime scenarios below remain future work. Final CP-K1 acceptance is integration-owned. This local probe command selects only SelfContained tests and provisions no database/provider/tool runtime:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.OctoberScan|FullyQualifiedName~Vulgata.Tests.October.Knowledge'
```

## Prerequisites

1. Explicit post-review tool/Scan planned-change approval and implementation authority. F1/F2/F5 semantics are adopted against Business contract SHA-256 `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36`; this is not a compiled checkpoint. Business first publishes an initial compiling contract/fixture commit, integration imports it, and minimal consumer compile/conformance probes validate CP-K1/C0 before dependent production. Completed consumer implementations are not probe prerequisites. S2 needs accepted checkpoint plus B0 acceptance/CL publication/exact reads; S3 adds B1/B2 and actual scope conformance. No probe/code/runtime execution is authorized by this guide.
2. Owned prepared source fixture/approved registration-login slice, exact batch/database bindings, real permitted accounts, compatible pinned native/JDK profile and separate disposable tool state. Existing Identity setup is required; October setup does not provision an empty organization.
3. Explicit authorization for owned PostgreSQL/native processes and provider spending, selected explicit provider document, protected keys and finite budgets. Keep values outside source control/evidence. Do not inspect unrelated secrets or modify PATH/global configuration.
4. Business publication/read ports, Explore destination and integration-owner Web composition for ordinary-reader proof. Ordinary Web uses frozen DefaultConnection and cannot run October migrations or require generator credentials.

## Controlled checks after implementation

From the assigned worktree, use the existing launcher (new test names are illustrative until implemented; zero discovered tests is failure):

```powershell
pwsh -NoProfile -File eng/test.ps1 -Filter 'FullyQualifiedName~OctoberScan'
pwsh -NoProfile -File eng/test.ps1 -Suite LocalTools -Filter 'FullyQualifiedName~OctoberScan'
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~OctoberScan'
pwsh -NoProfile -File eng/test.ps1 -Suite External -WithPostgreSql -Filter 'FullyQualifiedName~OctoberScan'
```

Run only the approved suites/resources. Native/provider extra consent flags remain required. Classify every new test/helper in the existing suite inventory through its owner. The runner provisions owned PostgreSQL resources; never use ambient/shared databases. Use `-Suite E2E -WithPostgreSql` only for the separately authorized combined route. SelfContained cannot be relabelled native or live-provider proof.

## Observable scenario sequence

- **S1**: With no paid provider, prepare the approved tool fixture once. Demonstrate returned-descriptor native discovery/explore over more than one call/session, JDT definition/reference, and source list/read/search over multiple pages. Exercise expiry, revoked authority, second session while prior disposal is unfinished, external/preexisting/changed `.codegraph`, cancellation and owned cleanup. Expect explicit failure/gaps rather than fallback or unbounded renewal. Stop before S2 if combined coexistence/lifetime fails.
- **S2**: Preserve environment-backed configuration: `October__Mode=prepare` or `select`, exact target/batch and protected provider/authority configuration. After implementation select `October__Mode=generate`, `October__ScanPolicyFile` and required `outputStage: cl-only` in that same policy file, child-process settings only. Configure one admitted real registration/login file, cap 1 and explicit budgets. With B1/B2 absent or guarded by zero-call sentinels, prove real Worker → atomic accepted CL → Business B0 fixed CL publication → owner-returned exact handle → ordinary permitted IT reader. Expected command result: requested/completed stage cl-only, matching/BL not-requested, exact batch/publication/Document handles, exit 0 only for complete selected scope. Verify Business exclusion, no-content for zero CL Documents, partial exit 2, acknowledgement loss/lookup (indeterminate exit 4) without new Worker calls, and rejection of missing/unknown stage. Accepted rows alone are not success; no manual SQL or invented publication ID.
- **S3**: Explicitly set `outputStage: cl-and-bl`, after B1/B2 and F2 scope conformance are available. Run the two-Repository slice sequentially then cap 2/configured cap in separately owned fresh batches. Missing matching/BL fails this requested stage rather than silently downgrading. Record equal scope/configuration, real counts/waits/duration/usage and gaps. Inspect real BL conditions/effects, supported link and unknown; verify one shared execution scope. Any later CL+BL publication is a separate identity, leaving earlier CL-only membership/handles unchanged; no automatic reader retargeting or resume feature.
- **Stop / F1 probes**: Use controlled barriers: seal→drain-expiry fence→entry must create no transaction; expiry after staging but before guarded submission rolls back with no commit submission, even with delayed timer callback. Submit commit first, then fence/hard-stop/revoke and vary delayed acknowledgement, actual commit/rollback and lookup failure: cancellation is not rollback proof; a temporarily missing row cannot release freeze while the original write might commit. Track exact settlement and recheck disclosure authority. Repeat for publication. First graceful stop is not a fence and permits eligible drain; drain expiry fences file attempts without cancelling a separately eligible finalization phase. Hard stop/authority failure bars new calls/publication, preserves committed history and skips finalization. No possible writer becomes a stopped-gap prematurely; indeterminate blocks success/replay/freeze.
- **Execution scope / F2 probes**: Cap=1 with two concurrent inquiries, then inquiry+synthesis: at most one active provider attempt, queued child progresses after settled release, no outer orchestration permit/deadlock. Hard stop with one active/one queued cancels active I/O and bars queued/new/late/retry dispatch; late proposal cannot newly publish. Test grant-to-send cancellation, host rate delay/denial (no send before admission, original deadline retained), exhausted budget, missing scope, retry reacquisition without allowance reset, exact-once actual/unknown accounting and conservative unknown reservation. Unconfirmed cancellation/disposal must stop dispatch without early permit release. These initial controlled probes need the shared contract/fixture, not completed production consumers or paid calls; real claims still require later authorized runtime checks.
- **Fresh batch**: After abrupt interruption, do not restart/adopt sessions/index automatically. Preserve accepted history and source. Use explicitly authorized fresh prepared target/root handling; preexisting `.codegraph` is a blocker for operator resolution, not an instruction for automatic delete or source copy.

## Handoff

Record exact code/C0/schema versions, supported host/JDK/tool profile, non-secret configuration paths, prepared-origin identity, source selection, batch/publication binding, start/select/Generate/stop/fresh-batch instructions, actual limits/timings/usage and known gaps. Keep required keys/data protected. Distinguish controlled/native/PostgreSQL/real provider/usefulness observations and unresolved blockers.

Integration owner combines the actual source-to-store-to-ordinary-Web route with Chat/reader/graph checks from the October integration plan. One final combined build and SelfContained run after corrections plus affected runtime checks suffice; additional repeats require changed behavior or unresolved defects. Scan alone does not certify release or generic product completion.
