# Serialized-input admission correction

The user authorized a fix and then a whole scan after the 17.34-second local
admission failure. Read-only measurement of the actual account POM found 16029
raw bytes, 27016 bytes as a JSON string, 7379 prompt bytes and at least 384 bytes
of message overhead. That minimum 34779 input proxy already exceeded Generate's
32768 allowance before metadata/tool schemas. With output reserved at 393216,
the minimum total 427995 exceeded the configured 425984. This was not a timeout
or a DeepSeek refusal. The input proxy counts UTF-8 bytes, not tokenizer output.

The correction uses the existing Worker per-request upper bound of 1048576,
capped by the configured run token policy. Admission still reserves actual
measured input plus the full requested output; it does not reserve the ceiling
on every call. The 1048576 cumulative input-byte guard, 262144 output-byte guard,
32768 source window, deadlines, request counts, live authorization and usage
settlement remain in place. With output 393216 the request ceiling leaves at
most 655360 input-proxy bytes; conversation growth can still fail safely.
No tokenizer/context guarantee is claimed by this conservative byte proxy.

Budget diagnostics distinguish input bytes, request-token reservation, provider
rounds and provider admission. These are bounded categories with no source,
credential or reasoning content. Existing output/tool/stream categories retain
their numeric values. No automatic retries or competing persistence changes.

Controlled verification, independent review and the separate real run follow.

## Controlled evidence

RED `isolation-ef03ff3291ef42e49e3a2c004f3881df`: 13 selected, 7 passed,
6 failed, zero skips. The runner's 48 KB reasoning/tool-history case failed with
BudgetExceeded instead of reaching the controlled terminal; five diagnostic
assertions failed on missing InputBytes/RequestTokens categories. The initial
large-input Worker case already passed with an explicitly sufficient envelope,
confirming that the problematic ceiling came from Generate composition.

Independent GPT-6 Luna review accepted the scoped implementation with no findings:
per-leaf reservation, cumulative policy, finite guards and bounded diagnostics
remain intact. Review did not claim runtime qualification or run tests.

Fresh owned target `phone-f01a53d3e840`, batch
`c65652a9-ecb1-4709-8c01-17533d89d986`, database
`vulgata_oct_pilot_5190c2799dfb4a2b903bc8995ada02ca` is prepared and selected for
all 77 approved files. CL-only, low reasoning and 393216 output cap are explicit.
Bootstrap host is stopped; previous data and shared container/volume retained.

Full SelfContained `isolation-265f183a72134d859e1a124a64ed1b64` passed 4774/4777,
zero skips. New admission regressions passed. Two old null diagnostic assertions
failed on the now-specific ProviderRounds value and were corrected only in their
exhaustion branches; status/count/settlement assertions were preserved. The third
failure is the previously recorded
`OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`.
Full-suite GREEN is not claimed. The executable Generator and tested copy match
SHA-256 `7768BE855306F0F8D4868E9E9F18D27EB5D3A356D5FA31D33B8F8C9855A02E24`.

Final affected SelfContained run `isolation-c19fd58f062547c6b558e3206bda9cc1`
passed **436/436**, zero skips. Independent final-shape review accepted the exact
diagnostic assertion updates. No production change followed the full suite;
focused GREEN does not erase its recorded failures or qualify external resources.

## Real whole-slice outcome

Reviewed production commit `d0f3b691dd22e2df97cb835a31c220b9c3dc0f05` ran one
whole-slice Generate on the fresh target above. Report
`generate-33cf4565e5204d9bb855cf95157e573e.stdout` records exit 1 after **109.05
seconds**, no timeout, `NeedsSettlementCheck=false`, empty stderr. All 77 files
were inventoried/selected. Three provider requests used 21276 input and 26340
output tokens, all usage known. No budget failure occurred.

Two files were accepted: `mbs_account/pom.xml` and
`mbs_account/src/main/java/cn/com/yitong/actions/acct/EleAcctCustInfoCacheOp.java`.
The third, `mbs_account/src/main/java/cn/com/yitong/actions/acct/GetMobileByAccessCodeOp.java`,
failed `InvalidOutput / ProposalParse / Syntax / Root`. Execution Failed,
coverage Partial, publication NotRequested, zero returned Documents. This is
accepted-file progress, not published Knowledge or ordinary-reader qualification.

The bounded observer reports MaxTokens=393216, HTTP and SDK finish Stop, DONE
seen, 3519 UTF-8 text bytes identical at HTTP, SDK and parser boundaries. Each
JSON summary reports MissingClosingDelimiters at EOF with one object and one
array still open. HTTP/SDK reasoning usage agrees at 991 for this failing leaf.
Thus this attempt did not reproduce admission rejection or Length exhaustion;
the observed response content itself was incomplete JSON despite Stop. No raw
source/response/reasoning is recorded here and no blind JSON repair was applied.

All 77 prepared-source hashes remain unchanged. The owned run database and
diagnostics, including the two accepted files, are retained; previous data and
shared container/volume are preserved. No automatic retry or further paid call.
