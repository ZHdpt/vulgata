# Bounded retained-link paid qualification

User authorization: “Start a bounded paid scan, focusing on whether links are
generated.” Executed once from clean `oct14/business` at
`30ca6f0d65fbff0f9152ffb252d1d42d223573a2`. No retry was launched.

## Exact scope

- Original CL publication: `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`.
- Corpus: `0ea50f38-2441-4ec0-b31c-313acd0c5f55`, 163 exact CL versions.
- Batch: `1a327657-6a55-4e65-ba06-6992f423d40e`.
- Target: `phone-15bfe12615eb`; existing retained pilot database.
- One synthesis root: `authMethods` in `src/main/resources/flow/cust/userLogin.flw`,
  CL version `217b7ece-ef71-4209-959f-419229bd0cd7`.
- Profile: `october-retained-evidence-v2`. The canonical continuation resolves all
  11 eligible corpus pairs before synthesizing this selected root.
- DeepSeek at `https://api.deepseek.com`, `deepseek-flash`; original output ceiling
  393,216. Run limits narrowed to 26 requests, 2,700 seconds, 20 million total
  tokens. Original context override retained. No CL rescan or source tools.

Authenticated read-only preflight verified the exact original CL digest, root,
11 candidates, zero existing v2 outcomes, 14 settled prior receipts and all seven
existing publication projections. Protected bindings stayed outside tracked files.

## Observed result

RunId `86b950d4-20ea-4125-8fa4-67a12e634082`, owned PID 41864, started
2026-10-13 03:30:53 UTC. BeginBusinessFromCl committed at 03:30:59 UTC;
CompleteBusinessFromCl committed at 03:32:41 UTC. The process exited 1 after
108.44 seconds without watchdog expiry. It is locally settled, not an abandoned
run or permission to repeat an unknown provider operation.

One physical provider request was reported, with 100.2547138 seconds model wait.
Input/output usage is unknown. Terminal code `business_resolution_failed`:
matching Failed, Business synthesis not started, publication NotRequested.
**Zero v2 outcomes or accepted edges; zero new BL publications.** This attempt
does not qualify real link generation or connected BL usefulness.

The first planned inquiry concerns retained `phoneNumbAuth` and
`acct/getMobileByAccessCode.flw`; the selected authMethods synthesis root was not
reached. No model witness or provider response body is retained for this failed
request, so malformed JSON is not an established cause.

Post-run read-only owner verification confirmed matching begin/completion receipts,
zero v2 outcomes and unchanged membership/content projections for all seven
pre-existing publications. Original CL remains readable. No ordinary-user new-BL
verification is possible because no new publication exists; no temporary reader
account was created. No migration, host restart, recovery mutation or receipt
deletion occurred. This consumes the third invocation of the five-run grant;
two remain unused, and no additional attempt is dispatched by this checkpoint.

## Diagnosis and next bounded correction

The Business HTTP client sets HttpClient.Timeout to infinite but leaves the SDK's
OpenAIClientOptions.NetworkTimeout unset. An offline inspection of the installed
System.ClientModel 1.14.0 pipeline confirmed its effective message timeout is
100 seconds. This closely matches observed 100.25-second waiting and strongly
suggests SDK transport cancellation before the resolution owner's 120-second
deadline. The underlying exception was not retained, so the timeout diagnosis
is not a captured-exception proof and says nothing definitive about server-side
completion or charging.

Source inspection also confirms resolution calls BusinessAttemptAsync with null
strategy; only nonnull synthesis strategy applies the configured low-reasoning
override. Thus the process's low setting did not establish low reasoning for this
resolution request. No claim is made about the provider's effective default.

Before another paid attempt, align the Business SDK network timeout with the owned
cancellation deadline, apply explicit Business resolution reasoning settings, and
retain a safe failure category sufficient to distinguish transport timeout from
response validation. Preserve CL streaming, strict edge validation, unknown-usage
accounting, settlement and the no-blind-retry boundary. These corrections were
identified but not implemented or paid-qualified by this run.

Ignored evidence: `TestResults/business-links-run-1/` contains authorization,
preflight, process/log/result files and `after-run-verification.json`.
`TestResults/business-links-probe/` contains the read-only owner verifier and the
offline installed-SDK default inspection. Helpers used actual terminal JSON and
owner APIs rather than the known qualification-report parser mismatch.
