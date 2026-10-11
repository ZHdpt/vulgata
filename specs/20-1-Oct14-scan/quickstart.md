# OCT-SCAN validation and handoff guide

S1 T005–T012 are implemented, independently reviewed and qualified. S2 is now authorized and in progress: Configuration, Worker, shared scope, primary framing/F1, inventory and internal CL publication have reviewed controlled GREEN. Complete dynamic input provenance and exact-bound tool wrappers are also reviewed at `f396e35b1fcaab83586245ad23ecbcc46616e639`. Executable Generate, fresh-run admission and the full controlled CL-only route are now independently reviewed (299/299 affected checks); broad regression and real qualification remain pending. The corrected owner admission store and provenance helpers are imported for controlled composition; this is not runtime resource permission. The combined S1 fixture passes source paging/read/search, real JDT definition/reference, renewed CodeGraph sessions and owned cleanup. See [verification-s1.md](verification-s1.md) and [verification-s2.md](verification-s2.md). Stop before S3; retain the [plan.md](plan.md) runtime/authority stop conditions and [contracts/scan-boundaries.md](contracts/scan-boundaries.md).

Historical bounded preparation imported integration `3bcd815b09cfbac7418d6644a2a75816e0788ec0` for T001–T004 controlled consumer probes. See `conformance-c0.md` for those results. S1 subsequently imported accepted common CP-K1 `12d92d80339ea622cd26c593ebdb74da59401f91` under its direct user instruction; accepted S1 evidence is in `verification-s1.md`. This local conformance command selects only SelfContained tests and provisions no database/provider/tool runtime:

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite SelfContained -Filter 'FullyQualifiedName~Vulgata.Tests.OctoberScan|FullyQualifiedName~Vulgata.Tests.October.Knowledge'
```

## Prerequisites

1. Explicit post-review tool/Scan planned-change approval and implementation authority. F1/F2/F5 semantics are adopted against Business contract SHA-256 `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36`; this is not a compiled checkpoint. Business first publishes an initial compiling contract/fixture commit, integration imports it, and minimal consumer compile/conformance probes validate CP-K1/C0 before dependent production. Completed consumer implementations are not probe prerequisites. S2 needs accepted checkpoint plus B0 acceptance/CL publication/exact reads; S3 adds B1/B2 and actual scope conformance. No probe/code/runtime execution is authorized by this guide.
2. Owned prepared source fixture/approved registration-login slice, exact batch/database bindings, real permitted accounts, compatible pinned native/JDK profile and separate disposable tool state. Existing Identity setup is required; October setup does not provision an empty organization.
3. Explicit authorization for owned PostgreSQL/native processes and provider spending, selected explicit provider document, protected keys and finite budgets. Keep values outside source control/evidence. Do not inspect unrelated secrets or modify user/machine PATH or global configuration.
4. Business publication/read ports, Explore destination and integration-owner Web composition for ordinary-reader proof. Ordinary Web uses frozen DefaultConnection and cannot run October migrations or require generator credentials.

## Controlled checks after implementation

Generate composition uses one explicit `October__RuntimeFile`
alongside the existing scan-policy/provider/authority configuration. Its trusted
host document contains `preparedRoots` (exact target/batch/Repository/root-binding
tuples and physical roots), `codeGraphCommandPath`, `javaNavigation`,
`javaHostPolicy` and `logDirectory`. These reuse the existing bounded native
profile and source-binding types. Parsing is source-free: it reads only that
configuration document, rejects duplicate/unknown fields and relative paths, and
does not start tools or create state/log directories. Exact selection matching
precedes fresh-run admission; source approval and tool composition follow a known
Begin receipt. This configuration is not a resource grant or a claim that Generate
has passed real runtime qualification. Keep actual deployment paths and protected values outside
source control and evidence.

The qualified S1 invocation uses installed CodeGraph 1.5.0, JDT-LS core `1.58.0.202605150919`, and physical Java 21.0.12. Source and dedicated JDT grants remain 30 seconds; the optional 120-second policy was **not** added. Both isolated JDT and combined S1 fixtures pass useful navigation and cleanup. The launcher settles validated retained descendants using actual Windows process signals; stderr EOF and exit-code availability are not cleanup proof. Shutdown remains bounded at 15 seconds. This qualifies the pinned launcher profile, not arbitrary process trees.

The native test process must contain only absolute PATH entries. This host's inherited PATH contains an empty entry; the verified runner preparation removes only empty entries in its child process, preserving absolute entry order and leaving user/machine settings untouched:

```powershell
$s1AbsolutePathEntries = @($env:PATH -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($s1AbsolutePathEntries | Where-Object { -not [System.IO.Path]::IsPathFullyQualified($_) }) { throw 'Unexpected non-absolute test-process search path.' }
$env:PATH = $s1AbsolutePathEntries -join ';'
$env:VULGATA_JDT_JAVA_BIN = 'C:\Program Files\Java\jdk-21.0.12\bin'
pwsh -NoProfile -File eng/test.ps1 -Suite LocalTools -Filter 'FullyQualifiedName~OctoberScanToolRuntimeQualificationTests' -TestTimeoutSeconds 300 -CommandTimeoutSeconds 900
```

Run this only inside a fresh owned shell with native-fixture authority. The recorded blocker is corrected and qualified; reruns need a changed behavior or verification purpose. Never adopt/delete retained state from an earlier failed attempt automatically.

From the assigned worktree, use the existing launcher; zero discovered tests is failure:

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

The run policy accepts `runDeadlineSeconds` from 1 through 86400 (24 hours),
default 1800. For an explicitly approved four-hour run use 14400. This changes
only the run-owner lifetime: existing file/tool/provider budgets, ten-minute
source grants and live authorization checks still apply. Generate derives its
operator-session expiry from max(one hour, configured run duration); it does not
renew an expired session. CL publication remains capped at 30 minutes within the
remaining run. When using `eng/watch-october-batch.ps1`, pass
`-RunDeadlineSeconds` from that same policy instead of `-TimeoutSeconds`; its
60-second startup/cleanup allowance does not extend the application deadline.
See [review and verification](configurable-run-deadline.md).

### BL from an existing complete CL publication

BL generation is handed to the Business spec. Start with the
[CL/BL contracts, boundaries and LLM troubleshooting handoff](handoff-business-generation.md).

Use a fresh authenticated Generate invocation with an explicit Business policy:

```json
{
  "outputStage": "cl-and-bl",
  "existingClPublicationId": "fff8ddfc-d8ae-480c-aadd-286f03c8f4d9"
}
```

This is a policy fragment: retain the explicit finite budgets and repository
scope rules in the complete policy file. Bind the exact existing target,
database, batch and selection, and supply the existing protected provider and
Identity configuration. `October__Mode=generate` and
`October__ScanPolicyFile=<absolute new policy file>` select this route.
`October__BusinessSettingsFile` must contain explicit roots (Repository ID,
relative path, semantic selector and SelectedCodeUnit kind), planning limits and
maximum attempt tokens; alternatively use the existing bounded operator root
selection mechanism. A genuinely verified Identity reader binding is mandatory.
An old paid-run wrapper's single-use marker is not permission to invoke it again;
prepare a separately authorized BL invocation/log boundary with the same protected
bindings. Do not run prepare or create another CL batch for this route.

The selector is valid only with cl-and-bl. It reuses the owner-validated exact CL
corpus and bypasses CL/source-tool execution and freezing. New BL publications
have their own identities; the original publication and all CL versions remain
unchanged. A fresh attempt can start after the previous attempt's owner confirms
settlement and writes its terminal receipt. Unconfirmed commits/transports or a
lost original process block; there is no automatic crash takeover or retry.
Reader selection is not automatically changed. See
[scope, safety and evidence](business-from-existing-cl.md).

## Handoff

### Bounded final-format recovery

Normal Generate permits three consecutive regenerations after an unparseable or
structurally invalid final JSON proposal (four candidates total), in the same file/window and conversation.
The model receives safe parser feedback and the remaining correction count. This
does not restart investigation or reset the original budgets. A transient failure
after correction begins cannot restart the Worker to replenish this allowance.

For the qualified DeepSeek endpoint, final-format retries now use these stages:

| Regeneration | Thinking | Request context |
| --- | --- | --- |
| 1 | Configured behavior (low in the pilot) | Existing history and safe feedback |
| 2 | Explicitly disabled | Existing history and newest safe feedback |
| 3 | Explicitly disabled | Preserved investigation plus newest safe feedback; rejected final proposals and obsolete correction turns removed |

Compaction retains every original source window/advisory input, system schema and
settled tool exchange, including protocol-required reasoning. It is deterministic,
uses no summarization call, and does not reset cumulative budgets. Thinking is a
per-request override; the next file on the same client uses configured behavior
again after either success or failure. Other providers and Business calls are
unchanged. See [staged correction evidence](staged-correction-verification.md).

`hardRetryMode` defaults to `false` in the Generate scan-policy JSON. To enable
the bounded experiment, set `hardRetryMode: true` and `maximumTransientRetries: 0`.
Keep the explicit `outputStage`, source selection and original budget settings.
JSON proposals and corrections request `json_object`. The former
`useTextForJsonCorrection` experiment setting is accepted for configuration
compatibility but no longer selects text, even when true. Ordinary non-JSON
conversations are unaffected.

Legacy hard mode retains one ordinary correction plus at most two additional
corrections per file, with sixteen additional corrections across the original run;
it never stacks extra corrections on top of the normal three. All original
admission, deadline, source, authorization and acceptance guards still apply, so
available correction slots do not guarantee another request. Tools remain disabled
for corrections. It does not retry semantic/evidence failures, unknown application
exceptions, uncertain writes or an entire failed run. Exhaustion is failure.

Generate writes bounded diagnostic events to `worker-json-corrections.jsonl` in
the configured owned log directory after each Worker settles. Records correlate
batch/run/workstream/admission/attempt IDs, hashed file/provider identities, retry
ordinal, source-window ordinal, elapsed Worker time, provider-round count, response
byte count, normalized finish reason, typed parser diagnostics and final Worker outcome. They contain no raw
source, model response, reasoning, prompts or secrets. `Completed` here means the
proposal passed validation, not acceptance or publication. Logging failure adds
`json-correction-log-incomplete` to the report and never causes another request.
Events buffered in memory can be lost on process failure before settlement.
`RetryRequested` means a correction was scheduled, not that an HTTP request was
sent; admission may still refuse it. The shared log uses an exclusive, fail-fast
append: concurrent writer contention produces the incomplete-log warning rather
than blocking execution or replaying provider work.
The staged strategy and removed-message count are logged as bounded diagnostics;
no compacted text or rejected proposal is written to this log.

The optional `hardRetry` report contains `reservedAttempts` and
`providerCallsStarted`. The latter counts entry into the underlying client, not
confirmed HTTP transmission; a reservation can be consumed before a guard refuses
dispatch. These counters do not establish successful Knowledge. Require complete
coverage and confirmed publication, and qualify ordinary-reader access separately.

Record exact code/C0/schema versions, supported host/JDK/tool profile, non-secret configuration paths, prepared-origin identity, source selection, batch/publication binding, start/select/Generate/stop/fresh-batch instructions, actual limits/timings/usage and known gaps. Keep required keys/data protected. Distinguish controlled/native/PostgreSQL/real provider/usefulness observations and unresolved blockers.

Integration owner combines the actual source-to-store-to-ordinary-Web route with Chat/reader/graph checks from the October integration plan. One final combined build and SelfContained run after corrections plus affected runtime checks suffice; additional repeats require changed behavior or unresolved defects. Scan alone does not certify release or generic product completion.
