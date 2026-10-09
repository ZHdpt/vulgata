# Tool outcome diagnosis

2026-10-09. Source-free diagnosis only; no production correction or new diagnostic
is authorized by this record. Base Scan `ab0de7fde5ec1078bbae31bb29ab9f4b1285dd4c`
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
