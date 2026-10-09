# Tool outcome diagnosis

Current status: bounded correction implemented and independently accepted;
**466/466 affected controlled tests passed**. Native/provider qualification remains
separate, and the latest real `ToolInvocation` failure is not attributed to JDT.

2026-10-09. The initial sections record source-free diagnosis; a subsequent explicit
Integrator decision authorizes the bounded correction recorded at the end. Base
Scan `ab0de7fde5ec1078bbae31bb29ab9f4b1285dd4c`
matches affected production in integration `500e2f07a0b85e1fce83c2a4cb211e2c9c1d610a`.

## Real observation and limits

Integrator's latest report records `ProviderFailed`, retry `None`, stage
`ToolInvocation`, with no terminal or budget reason. Two requests used 6,727 input
and 676 output tokens, all known. Reported tool wait was 25.505 seconds and model
wait 4.864 seconds. These scalar observations do not identify the tool, prove a
transport outage or identify which invocation boundary failed. No raw provider
response or source content was acquired for this diagnosis. Prior accepted CL and
all retained runtime resources remain untouched; paid repeats stay held.

`ToolInvocation` encloses function binding/body, runtime calls, internal projection
serialization and ledger admission. The outer executor's later tool-result
serialization has its own stage, so not every serialization failure belongs here.

## Observed code paths

| Boundary | Current behavior | Interpretation |
| --- | --- | --- |
| Java typed result | Correctly bound `NoResultObserved`, `Limited`, `ConfigurationUnavailable`, `ReadinessFailed`, `ProtocolFailed` can enter the ledger as metadata. | A limitation is not automatically fatal; it is not proof of no behavior in the source. |
| Java open failure | The production October dependency throws on every non-`Opened` result. `RunAsync` catches the exception, marks the entire runtime faulted and returns `ReadinessFailed`. | Loses the distinction between a settled availability problem and an authority/cleanup failure. |
| Real primary-frame recheck | Ledger verifies primary source before and after admission. Its real runtime-backed frame cannot be reacquired from a faulted runtime. | A superficially permitted `ReadinessFailed` may therefore be withheld by the wrapper, producing `ToolInvocation`. A standalone valid-envelope test cannot prove this route works. |
| CodeGraph | Bound `NoMatch`/`Limited` and even some failure-status envelopes can pass ledger shape checks. Runtime renews expired sessions only within its existing bounds, and catches exceptions into a faulted runtime. | Ledger admission is not the required-tool availability policy. Required CodeGraph failure must not become a source-only substitute. Undefined statuses and absent/foreign identities are invalid, not an ordinary “unknown” result. |
| Source reads | Correctly bound `Missing`, `Unreadable`, `UnsupportedText`, `InvalidRange`, `Incomplete` can be metadata with no text. Genuine EOF carries empty text, exact range and file length. | Missing or unsupported content can remain an explicit gap. `CompleteEmpty` without its required read envelope is malformed and withheld. |
| Authority/provenance | Denied or unavailable authority, changed source, wrong identities, cancellation/deadline, missing verification and malformed returned provenance are refused. Runtime/session cleanup uncertainty is fenced. | Keep fatal and non-disclosing; never convert these to recoverable argument or availability refusals. |

Relevant production: `OctoberToolRuntime.cs` constructor/open dependency and
`NavigateAsync`/`RunAsync`; `OctoberWorkerInputLedger.cs` admission and
`PrepareOtherAsync`; `PreparedJavaNavigationContextFactory.cs` typed open-failure
handling. The factory already preserves `CleanupConfirmed` and the typed open
status. The October dependency currently discards that distinction. The native
adapter can report settled configuration failure; that is a static producer path,
not evidence that the latest real run took it.

## Controlled counterexamples

The existing fixtures now contain 27 additional characterization cases: 25 typed
ledger-envelope cases and two inert public-factory JDT open-failure cases. No new
fixture or dataset was created. Run `9f485a8a7017440291cb83b407b6165a` used the
standard SelfContained launcher for all `OctoberScanWorkerInputLedgerTests`,
`OctoberScanToolRuntimeTests` and `OctoberScanWorkerToolsTests`: **109/109 passed**,
zero failures/skips. This is current-behavior characterization, not RED for an
implemented fix. Existing assertions, including unconfirmed-cleanup controls, remain.

Both clean JDT failure cases (`ConfigurationUnavailable`, `ReadinessFailed`) prove:
the inert open executes once, the owned lease is settled and disposed, navigation
collapses to `ReadinessFailed`, and later runtime file acquisition is unavailable.
An independent grant for the same source approval is still issued and open, and
runtime disposal succeeds. Thus a confirmed-cleanup JDT availability failure can
unnecessarily disable the source fallback permitted by the feature spec. This does
not reproduce the latest live provider/native failure or identify its tool.

The ledger probes separately confirm that permitted, correctly bound limitation
envelopes retain metadata without acquiring extra content; bad identities and
missing success envelopes are withheld with no completion. Genuine EOF with its
exact verified empty range is admitted. These tests do not establish a complete
runtime-backed Worker fallback; a future correction must cover that composition.

Both test owners assessed the final evidence. Independent review accepted this
test/document checkpoint with no blocking findings, retaining the implementation
and paid-run hold pending Integrator's decision. Build completed with zero errors;
`git diff --check` passed. No production or prompt file changed.

## Policy and bounded proposal

The accepted feature spec, acceptance scenario 2.4 and FR-006, permits JDT
limitations with existing file-tool fallback and explicit gaps. Plan stop condition
4 requires fencing on changed/uncontained source, authority loss, unconfirmed
disposal and required CodeGraph unavailability. Therefore the proposal is narrow:

1. Preserve the existing typed JDT open status and confirmed-settlement evidence
   through the Scan-owned runtime boundary instead of collapsing every unsuccessful
   open into an unclassified exception.
2. Only a positively classified, confirmed-cleanup JDT configuration/readiness
   limitation may remain recoverable after fresh same-binding source and authority
   checks. Return an honest bound limitation through the existing ledger; do not
   invent successful navigation, source evidence or a new contributor kind.
3. All authority, source identity, cancellation/deadline, unknown-exception and
   unconfirmed-cleanup cases remain fatal. Required CodeGraph policy stays unchanged.
   No extra retry, extended lifetime, process profile or source-only CodeGraph path.

Candidate production ownership is `OctoberToolRuntime.cs` and existing runtime/tool
tests. No Business schema or shared-facade change is proposed. If preserving the
classification requires a shared contract change, stop that part for its owner.
This is a proposal, not permission to implement and not a claimed fix for the real
failure. A later correction would need RED/GREEN proving both recoverable JDT
limitations and preserved fatal controls through the composed Worker path.

## Minimum missing observation

The real branch remains unknown without a bounded discriminator. If Integrator
chooses diagnostic work, preserve the **first actually observed** fixed tool
operation and failure boundary (binding, runtime open/invoke/settlement, source
currentness, or typed-result admission). Where available, attach only the existing
typed outcome/open status and confirmed-settlement boolean. In particular, retain
the producer's typed JDT open status before the generic catch loses it.

These must be fixed enums/booleans, not exception messages, model arguments, paths,
queries, result bodies, source or hidden reasoning. Do not infer them from elapsed
time or classify every `ReadinessFailed`/`Unavailable` as recoverable. No such
diagnostic has been implemented, and no paid rerun is proposed before the root
decides the consolidated boundary.

## Subsequent approved correction

OCT-Integrator (`01a1109d-ec4c-7500-8b73-642a5e22f59b`) explicitly approved one
bounded JDT correction plus the minimal content-free discriminator after the
diagnosis checkpoint `dc77ddec0824d9b13b69044f82ac7c7738ac53bb`. Production ownership
is Scan-local runtime, tool wrapper, report, runner and CLI projection; shared
facade/API/Knowledge/Business schemas remain unchanged. Separate test and production
owners and independent review remain required; native/provider/PostgreSQL work is
not authorized in this lane.

Only positively settled `ConfigurationUnavailable` or `ReadinessFailed` may become
honest navigation limitations after fresh same-approval/root/source authority
verification under the original lifetime. Unknown exceptions, invalid results,
disposal failure, unavailable authority, changed identities, cancellation/deadline
and required CodeGraph failure remain fatal. No extra retries or lifetime changes.

The additive JSON shape is `workerDiagnostics.toolFailure`, nullable, containing
exactly `operation`, `boundary`, `javaOpenStatus`, `javaOutcome`, `sourceOutcome`,
`codeGraphStatus`, and `cleanupConfirmed`. Operations use the existing fixed
six-operation enum; boundaries are `ArgumentBinding`, `RuntimeOpen`, `RuntimeInvoke`,
`RuntimeSettlement`, `SourceCurrentness`, `ResultAdmission`. Status fields use only
defined existing typed values actually observed; unavailable evidence is null.
For example, a source-grant issue status is not invented as a source-result outcome.
No IDs, names, paths, arguments, source, result bodies or exception text are fields.

Observation is local to one invocation, closed on settlement, and copied to the
current Worker attempt only when that invocation fails. Successful recoverable
limitations do not leave a failure record. Existing report revocation/disclosure
fences cover this nested field. The original concrete cause must survive later
generic runtime/source/ledger failures without crossing calls or files.

Before RED only the diagnostic record/enum/property declarations and a null
observation stub were added to permit test compilation; production behavior was
unchanged. Tests cover the public JDT factory's settlement evidence separately
from the composed runtime-backed primary frame, limitation admission and subsequent
file-tool read. They use existing controlled fixtures and inert dependencies, not
native JDT or CodeGraph execution.

RED `181cd77c430b49ffa604d15e12ba60a8` built successfully and ran the full affected
runtime, wrapper, input-ledger, runner and entry-point classes: **198 selected,
169 passed, 29 failed, zero skipped**. Missing fallback, diagnostic capture and
projection accounted for the failures. The cleanup-unconfirmed row's expected
teardown failure can mask its earlier missing-diagnostic assertion in RED; other
rows independently establish missing capture, and GREEN must satisfy both body
and teardown. Both test owners assessed RED before production behavior edits.

Review also found that typed CodeGraph `Failed`, `Unavailable` and `TimedOut`
could leave the runtime usable after wrapper refusal. The approved no-source-only
CodeGraph stop condition requires closing that path. Focused RED
`316a65913375416d8d0b3b69f7c508b2` ran 14 fatal-control cases: 11 passed and exactly
those three failed because their runtime-backed primary frame remained current.
The correction faults the runtime on a returned required-tool failure while
preserving the actual typed status. `Complete`, `NoMatch` and `Limited` remain
distinct, and the existing bounded `Expired` renewal is unchanged. No retry or
lifetime expansion accompanies this guard.

An initial expanded run `17fb2d8efc21483c958dc39c751a9d53` passed 462/462 controlled
tests. Independent review still found an uncovered binding edge: foreign/null
CodeGraph result identities and a foreign Java result from a correctly bound
context were converted to sanitized failure/limitation results before the runtime
was faulted. The Java limitation could be ledger-permitted. These are binding gaps,
not evidence of the live failure, and are not assumed to be newly introduced by
this correction. Three additional controlled counterparts require permanent
runtime fencing and a content-free `SourceCurrentness` observation retaining only
the actually returned typed status. Java cleanup is reported true only after its
existing successful context disposal.

Binding RED `13c42bb6e8524a91b022f40d98bbae43` ran 17 fatal-control cases:
14 passed and those three failed. The Java foreign-result case did not throw;
Graph foreign/null cases reported the downstream synthesized failure rather than
the actual returned status. Production then added the narrow binding fence before
sanitizing either result. No foreign identity or returned payload is retained in
the diagnostic. These additional guards preserve the approved provenance boundary;
they do not qualify native tools or identify the latest live failing tool.

The binding correction passed 465/465 in
`40ffbe88c33a42128d1c70517b7b8569`. A final observation-only check then reproduced
the generic-catch gap: `6ef13d928d8848b7b95acf58f103fc7f` failed its single controlled
source-exception case because the report showed downstream `ResultAdmission` /
`AuthorityUnavailable`, rather than the observed runtime exception with unknown
typed status. The runtime generic catch now observes `RuntimeInvoke` before its
existing fault-and-sanitized-return behavior. First-wins capture preserves any
earlier more precise cause; cancellation and failure policy are unchanged.

## Accepted implementation evidence and handoff

Final run `ff0c0234e2354177b7c4749ab4749013` passed **466/466**, zero failures or
skips, through `eng/test.ps1 -Suite SelfContained`. The full selected classes were
Worker, WorkerTools, InputLedger, ToolRuntime, ExecutionScope, Acceptance,
GenerateRunner, ClPublication, EntryPoint, CodeGraphLifetime and PreparedSourceFrame
under OctoberScan, plus PreparedSourceToolFunction, PreparedJavaNavigationFunctions,
PreparedJavaNavigationComposition and CodeGraphFunctions. Policy/guard probes and
`git diff --check` passed. This is an affected regression selection, not a new
full-suite qualification or waiver of the previously recorded sequential timeout.

Both test owners assessed the final shape. Independent review accepted the final
production/test checkpoint with no remaining blockers after the returned-binding
and generic-observation corrections. Production changes are confined to five files
in `Vulgata.OctoberGenerator`: `OctoberToolRuntime.cs`, `OctoberWorkerTools.cs`,
`OctoberScanReport.cs`, `OctoberGenerateRunner.cs` and `Program.cs`. Four existing
test files and this record carry the controlled evidence. No shared API, facade,
Business/Knowledge schema, migration, provider configuration or runtime profile changed.

There is no new invocation/configuration requirement. Integrator retains the
original run/session/provider/budget, exact source bindings and verified Identity
reader requirements. The additive nested report field described above is the only
report-shape change. A reported status describes the observed typed boundary; it
does not convert a failed call into successful navigation or prove native tool
identity from elapsed time. Unknown values remain null.

Integrator owns merging and deciding one subsequent real qualification with its
approved resources. This lane performed no paid/provider/native/PostgreSQL or
real-source-root operation and preserved earlier accepted CL. Controlled public
factory settlement and composed runtime/frame/ledger fallback are separate tested
seams; no native JDT fallback or latest-pilot-cause claim is made.
