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

Implementation commit `64dbb66589391b00a6ee33efd72b9595a480ed1d`.
Generator and test-copy Agents DLL SHA256 both
`B32EE68EDA2C2D55C8956FA936DE5DE6400E874BD25809602D3E4D16256D8C66`;
Generator SHA256 `D17DF854A42A671DEB9C3CAF0D619C3B4513B520785DDEC86546D51066AB7010`.
`hard-whole-01` started with hardRetryMode and useTextForJsonCorrection true,
transient retries zero and unchanged approved source/provider/resource/budget
settings. No automatic further whole-run restart is configured.

## Real hard-mode outcome

`hard-whole-01` failed after274.24seconds:7accepted files of77selected,
Partial coverage,0Documents,publication NotRequested. Root phone-0c4a45eb3851,
database vulgata_oct_pilot_973f4f76090a4fa6907bdd6c42ae9f52,
batch de25d53d-2bdf-45a0-ad31-2d4dbefbe0fd,
report generate-b809629553d3426d8ca2fcd0860be5c7.stdout.
Hard counters:3reserved extra attempts and3provider-client entries across the run.
Actual usage:13requests,77983input/63291output tokens, all known.
Process exit1, no timeout, no unresolved settlement check; all77source hashes match.

account `src/main/java/cn/com/yitong/actions/cust/custcenter/GuestSyncOp.java`
failed InvalidOutput/ProposalParse/Syntax/Root. HTTP/SDK/parser all retained the
same3477UTF8 bytes; Stop/DONE; MissingClosingDelimiters atEOF with1object/1array
open;1652reasoning tokens. Hard mode supplied bounded extra opportunities but did
not eliminate the observed failure. No incomplete candidate was accepted and no
partial corpus was represented as successful publication. Ordinary-reader access
was not requalified. Both normal series and this single hard-mode run are exhausted;
no further paid retry or format change is implicitly authorized by this outcome.
