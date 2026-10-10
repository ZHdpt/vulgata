# CodeGraph preparation renewal

The user authorized fixing the host failure, retrying LoginAntiSendOp.java first,
then the whole slice only after success. Prior run returned run_unavailable on
the fifth file after about153 seconds, four provider requests and four committed
acceptances; the underlying exception was not retained in that report.

Inspection found a matching deterministic defect: native CodeGraph sessions expire
after two minutes. OctoberToolRuntime.ExploreAsync already closes and replaces an
expired session once, but PrepareAsync returned Expired directly. Every new file
creates Worker tools and performs discovery through PrepareAsync. Expired readiness
there throws before Worker dispatch and is caught as run_unavailable. This explains
the observed timing and zero fifth request, while the absent original exception
means the historical cause is inferred from this path rather than directly logged.

The scoped fix gives preparation the same one-replacement behavior for Expired,
only while the original runtime remains available. CloseSessionAsync must confirm
disposal before opening another session. Existing run lifetime, native two-minute
session lifetime, replacement cap21, source authorization, fail-closed cleanup,
serialization gate and cancellation remain. No provider retry or schema change.

Previous partial/successful data is retained for diagnosis; no cleanup is necessary
for this fix. Controlled verification and real isolated/whole outcomes follow.

RED `isolation-b1e09e30a288498b92068de2592c978a`: four selected, four failed,
zero skips. Ready replacement returned Expired; old session was not disposed;
repeated expiry retained one session instead of reaching the finite replacement
cap. The cleanup-failure test also exposed its own teardown propagation; final
test explicitly asserts runtime/descriptor cleanup failure and always disposes
its owned source fixture. No production cleanup assertion was weakened.
Independent GPT-6 Luna review accepted the one-shot renewal without findings.

Prepared isolated target `phone-bd2ba2a54c6f`, batch
`65c49c1f-1cc3-49a1-87a7-b4583b6c4a3f`, database
`vulgata_oct_pilot_4c0f6527bff94aab9dfef78f651b391e`: only channel
`src/main/java/cn/com/yitong/actions/domain/login/LoginAntiSendOp.java` selected,
same approved supporting source, low reasoning/cap393216, CL-only. Bootstrap,
prepare and select passed; no paid request during preparation.

Full SelfContained `isolation-229a2fea7d334e6e967e7bc26384c3ce` passed4791/4792,
zero skips. All100 ToolRuntime/CodeGraphLifetime/GenerateRunner cases passed,
including all four new cases. Sole failure remains the known
OctoberGeneratedClReportTests exact-report projection mismatch. No full-suite
GREEN claim. Generator executable/test copies agree at SHA-256
`A5696989EBB4174C4E5FFC5AFFEF34978F5A08CA20E0070577E6794B391573F1`.

## Isolated real success and conditional whole scan

Reviewed commit `b164c7d2` ran isolated LoginAntiSendOp.java successfully. Report
`generate-efe0b2f9b353417f9bdba6a6bed21c1d.stdout`: exit0, 33.6 seconds, no
timeout/settlement check; Completed/Complete/Confirmed. Two provider requests,
9429 input/3997 output tokens, all known. One accepted file and one Document.
Retained publication `121c9b87-a007-4f7c-ad65-88785f199038`, logical Document
`12f07cdf-8f18-7daa-6509-7798f430dc84`, version
`5c00914c-1c67-4f75-a19e-830f80be1f76`. Reader access was not newly qualified.

This satisfied the user's condition. Whole target `phone-9b2d8b0717e8`, batch
`8d80d560-aa59-4420-acb8-22d32a209e71`, database
`vulgata_oct_pilot_8eb4642d338b47d095d03df1a98fb574` selects all77 approved files
with unchanged low/393216/CL-only settings and guard limits. Bootstrap, prepare
and select passed. All prior successful/partial data is preserved.

Whole report `generate-8191cf1749624529aeeb516cb655a1d0.stdout`: exit1 after
354.45 seconds, no timeout, NeedsSettlementCheck=false, empty stderr. All77 files
selected. Ten provider requests used67026 input/82260 output tokens, all known.
Seven account files committed acceptance: pom.xml, EleAcctCustInfoCacheOp.java,
GetMobileByAccessCodeOp.java, GetMobileToReplaceResOp.java, QueryAllCreditCardOp.java,
BaseAntiRequestOp.java and LoginAntiRequestOp.java. The run progressed through
multiple two-minute session intervals without the previous run_unavailable stop.

The eighth file, account
`src/main/java/cn/com/yitong/actions/cust/custcenter/GuestSyncOp.java`, failed
InvalidOutput/ProposalParse/ClosedFields/Units. Its3998-byte text is identical
at HTTP/SDK/parser boundaries; all three bounded JSON summaries say ValidObject.
HTTP/SDK finish Stop, DONE seen, MaxTokens393216; reasoning usage agrees at760.
This is a different strict field-set/schema rejection, not missing delimiters,
token exhaustion, tool failure or the previous host failure. No raw provider
output was retained, so the precise missing/extra/duplicate unit field is not
established by these diagnostics. Syntax-only correction does not repair a valid
JSON object's schema and no parser relaxation was applied.

Execution Failed, coverage Partial, publication NotRequested, zero returned
Documents. All77 source hashes match in both fresh runs. Isolated confirmed
publication and all partial/successful data remain retained. No further paid
attempt or cleanup occurred. Complete whole-slice/ordinary-reader qualification
remains outstanding despite this runtime progress.
