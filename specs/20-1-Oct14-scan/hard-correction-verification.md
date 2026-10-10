# Bounded hard correction mode

## Authority and design

The user explicitly authorized this conditional implementation after both whole-run
series failed, followed by one hard-mode whole run. Both series are complete; see
[text correction evidence](text-correction-verification.md) and its predecessor.
No broad S3, shared schema, provider change, host expansion or budget increase.

Independent reviewer accepted the narrowed design before implementation:
all existing provider rounds, per-file admission, cumulative input/output,
run request/token/rate limits, deadlines and settlement checks remain unchanged.
Consultation final c97dfb67-3dd0-4602-8f5d-3c3ed3796002 agrees. This grants additional
opportunities for valid output, not a guaranteed successful knowledge base.

`HardRetryMode` is default false in Scan policy. Enabling it requires
`MaximumTransientRetries=0` before run admission. The existing ordinary correction
remains one attempt. The same original Worker may reserve at most two more
correction attempts per file and sixteen across its original run, only after the
ordinary correction was used and the latest settled final candidate has an
existing eligible typed syntax/structural validation failure. The same history,
current feedback, source frame, tools-disabled enforcement and configured
correction response format remain in effect. Each reservation is monotonic even
if an original guard subsequently denies the request. No additional ordinary
correction loop, new Worker, source-window renewal or run identity resets it.

Semantic/evidence/coverage failures, Length, unexpected tool calls, incomplete
transport, unknown application failures, source/authority changes, cancellation,
budget/deadline exhaustion and uncertain writes remain terminal. No local JSON
repair, parser relaxation, retry after acceptance or extra publication attempt.

Owned production: Agents Worker executor and new original-run correction allowance;
OctoberGenerator policy, runner composition and optional bounded report counters.
Owned tests: existing OctoberScan Worker, Generate runner and configuration suites.
Business contracts/persistence/migrations and shared Web composition unchanged.

Report counters distinguish reserved extra attempts from entry into the provider
client. Entry is not proof of an HTTP send. Counters follow existing disclosure
suppression; successful Knowledge still requires full strict acceptance/publication.

## Verification

Normal-mode series ended at commit `2a197275`. The second series totals
143requests,1512059input/689740output tokens, all known,3377.52seconds across ten
runs,zero source mismatches. Accepted-file instances across distinct failed runs
are not a complete corpus and were not combined or published.

Initial restricted test launch failed before discovery with an empty build error
summary; it is not behavioral RED. The same authorized elevated SelfContained
selection produced honest RED at `isolation-20f516752a8c4ad0b1ecb6e57d277ce1`:
5selected,1passed,4failed,0skipped. Extra recovery/exhaustion and mode/budget
binding tests failed against the behaviorless scaffolding as expected.

Pending final controlled GREEN, independent implementation review and one real
hard-mode whole trial. Consultation final e4a6f9a4-dd6a-4cb1-955f-846a880945f9
after all ten outcomes found no further design defect; effectiveness remains
unproven. Reviewer found an omitted CLI counter projection, to be corrected and
covered with output-binding suppression tests before qualification.

Focused run `isolation-37505134436441b2ae338bb254b0bb11`:34selected,
33passed,1expected projection failure,0skipped. Worker recovery/exhaustion,
eight-file shared allowance plus ninth refusal, no-refund admission denial,
foreign identity and duplicate workstream refusal, unexpected tools, Length,
semantic/evidence refusal, controlled final acceptance/publication, revoked
report suppression and invalid-policy pre-admission tests passed. The valid CLI
projection case failed before its production correction; projection and the
output-binding suppression branch are now corrected.

Full SelfContained after production/projection correction:
`isolation-8587be164413461fb435574913a4fe91`,4865selected,4864passed,1failed,
0skipped. Every included hard-mode test passed. The sole failure remains
`OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`
(exact successful one-file qualification report shape), previously recorded and
unchanged; no assertion was weakened. This is not a fully green suite or external
qualification. A final reviewer-requested round-cap test was added after this
build and is being verified separately.

Independent source review accepted the corrected CLI projection and all other
production/test boundaries, conditional only on that explicit two-round guard
test passing. It asserts one consumed reservation, zero extra client calls and
the original ProviderRounds failure, proving the allowance cannot override a cap.

Final guard selection `isolation-33611d73854343378c612c3235fbfc32`:
1selected,1passed,0failed,0skipped. Independent reviewer confirmed final acceptance
with no remaining findings. Controlled verification is complete; the one authorized
real whole scan with hard mode is next. No complete Knowledge base is claimed yet.
