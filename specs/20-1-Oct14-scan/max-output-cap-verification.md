# Opt-in maximum output cap experiment

The user explicitly requested `max_tokens=393216` and one retry. DeepSeek's
[current API reference](https://api-docs.deepseek.com/api/create-chat-completion/)
allows that value. This is an output allowance, not a promise of completion.

`October:WorkerMaxOutputTokens` defaults to 8192 and accepts integers 1–393216.
Generate passes the configured cap through the existing Worker and SDK adapter.
Reservation preserves 32768 input-proxy tokens: the requested experiment reserves
up to 425984 tokens per request, subject to the existing cumulative policy.
Low reasoning remains explicitly selected for the retry. Byte limits, deadlines,
source/acceptance/publication authorization, usage settlement and zero automatic
retries remain unchanged. Other 8192 stream/non-progress bounds are unchanged.

Controlled RED selected 20 cases: 10 failed, 10 passed, zero skips, in
`isolation-942ca5ceb9a14618bad1d4eafae57762`. High-cap test setup was subsequently
corrected to supply its required total reservation. Independent GPT-6 Luna review
and test-owner assessment accepted the final scoped implementation with no
findings. Controlled verification and live result are recorded below separately.

Prepared fresh owned target `phone-0fb736db6377`, batch
`4b423969-c587-43e0-9575-3816c59e1e85`, database
`vulgata_oct_pilot_d890f00fb42445679856f395ca7f04ab`. Bootstrap, prepare and select
passed. Only `mbs_channel/pom.xml` is selected among the same 77 approved source
files. The child launcher explicitly supplies low reasoning and 393216 tokens;
no provider call occurs during preparation. Existing retained data and shared
container/volume are preserved. A failed POM stops the conditional whole-slice retry.

## Controlled verification

Full SelfContained run `isolation-5f42614090e14ceeb8e844006e5a19a0` passed
4773/4775, zero skips. All 20 cap/replay cases and all 434 cases in the three
affected classes passed. Full-suite GREEN is not claimed: failures were
`OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`
(previously recorded exact report-shape mismatch) and
`OctoberScanCodeGraphLifetimeTests.ExploreAsync_ContainedVerificationWaits_UsesSameBoundedOperationCancellationAsync`
(`Assert.True` failed). Neither assertion was weakened; the latter is not a
successful native CodeGraph qualification. No additional suites are qualified.
The executable Generator and tested copy have identical SHA-256
`F67B8654B09B25A6E08DEB028BD3F1AF6E8021920E20AB3A87EBB1A8302A0E2E`.

## Real POM retry

Executed reviewed commit `7c5e82fc41eebd9db51630120b50730522f2e460`, with low
reasoning and 393216 output cap in the owned child configuration. Report
`generate-40f41f0428e74d748645e1b9da9a2aae.stdout`: exit 1 after 29.8 seconds,
no timeout, `NeedsSettlementCheck=false`, empty stderr. One provider request used
9748 input and 1816 output tokens, all usage known. The Worker subsequently failed
`BudgetExceeded / Admission`, with no terminal/parse/tool failure diagnostics.
Execution Failed, coverage Partial, publication NotRequested, zero accepted files
and zero Documents. All 77 prepared-source hashes still match the approved manifest.

The earlier Length failure did not recur in this attempt. The remaining failure
is local admission after the first provider request; the current diagnostic does
not distinguish the exact admission refusal. The retained 32768-byte input proxy
headroom is a candidate when conversation/tool history grows, not a confirmed
measurement of this failed request. No input/byte/deadline guard was widened, no
raw provider reasoning was retained, and no extra paid attempt or whole-slice
retry ran. Resources and previous successful data remain retained. This is failed
real generation evidence, not reader qualification or completion of the slice.
