# Tool-disabled correction and safe syntax feedback

User acceptance on October11 authorizes the narrow correction, controlled tests,
isolated GuestSyncOp.java first and then whole-slice qualification. Existing
investigation and valid initial proposals remain unchanged. The same single
eligible syntax/structure correction must send tool_choice=none while retaining
tool declarations and complete evidence/reasoning history. Host enforcement
rejects unexpected tool calls. Safe zero-based parser line/byte positions provide
feedback without exception text, untrusted paths or source excerpts. No local
JSON repair, evidence/semantic correction, schema relaxation or extra allowance.

Owned production files: OctoberFileWorkerExecutor.cs and its correction prompt.
Tests stay in OctoberScanFileWorkerTests.cs with existing SelfContained inventory.
Business contracts, persistence, schemas, publication and Identity are unchanged.

User further authorizes bounded whole-run restarts until one complete success:
after initial whole failure, at most10 restarts; if exhausted, consult the existing
Deepseek JSON Truncation Fixes chat with sanitized errors for diagnosis/fix planning,
evaluate and correct concrete defects, then at most10 further whole runs. A later
configurable hard-retry mode is conditional on continued failure and requires
its own concrete design/review/tests; it cannot waive source/authorization,
spend/deadline, evidence/schema validity, atomicity or settlement. No blind retry
of denied access, exhausted credit or unsettled resources is authorized by this
reliability request. Every real attempt uses fresh owned resources and retains
prior data, with low reasoning, MaxTokens393216 and explicit32/48 tool limits.

Consultation final recommendation was read, not treated as implementation
authority. It favors this bounded experiment over an unconditional extra call or
a beta strict-submission migration. Root endpoint/provider settings remain fixed.
Controlled and real evidence will be recorded separately below.

## Controlled verification

Initial syntax-only selection `isolation-ed81985c888b471a95b87e6ce93c5074`
selected1/failed1. Full correction RED selection
`isolation-ef9347673d9e4873ab8e704d3b04c0d9`:25 selected,22 passed,3 failed,
zero skips. The wire test observed auto instead of none; unexpected correction
tool call lacked the required ToolsDisabled refusal. The syntax test initially
failed on an exact exception-type test assertion (JsonReaderException derives
from JsonException); test author corrected it to ThrowsAny. That draft failure
is not evidence of missing production syntax feedback. The test also correctly
distinguishes first-response feedback coordinates from final-response diagnostics.

Implementation preserves one eligible correction, declarations and replay. It
clones outgoing options to set ToolMode=None and rejects tools both during stream
collection and invocation. Zero-based lineNumber/bytePositionInLine are retained
only for syntax failures and only in0..262144; no exception message/path is emitted.

Independent read-only review accepted the correction path without a correctness
or authorization defect. Positions intentionally reach the Worker diagnostic and
model correction feedback only; existing runner/CLI projections do not expose
them. This preserves the public report shape for the narrow correction change.

Full SelfContained `isolation-ab8dc71c38c94714957dc7654d76b367`:4824 selected,
4803 passed,21 failed, zero skips. All25 correction tests pass including the three
new tests. Twenty older syntax snapshot assertions expected null coordinates and
require exact expected numeric-location updates; the remaining failure is the
known OctoberGeneratedClReportTests report-shape mismatch. No production guard
is weakened to satisfy snapshots. Affected rerun follows after test alignment.

Affected GREEN `isolation-2832e7d4326f4c85bf21807fbcd9d0ee`:1055 selected,
1055 passed, zero failures/skips. Exact syntax coordinates are asserted from the
controlled fixture text; structural snapshots stay unchanged. Full-suite GREEN
is not claimed. Native/provider/PostgreSQL qualification is separate.
Matching Generator/test assembly SHA256: Agents
`2CC3DE9FD99F3322FFAA98F81A68D310A5D3E2F9D78C0960E072FB486BEBD9CA`,
OctoberGenerator `D54B5170D8CDAAEA23D031C60DE83BB50D9376B880928F9CBF6B6A5018283966`.

## Real retry series on reviewed commit8dab2251

Isolated GuestSyncOp.java (`finalization-isolated-01`) passed in25.92 seconds,
exit0, no timeout/settlement check, Completed/Complete/Confirmed. One request,
3321 input/1656 output tokens, all known; one accepted file and one CL Document.
No correction was needed in this single-request success. Target
`phone-8798a45133c9`, database `vulgata_oct_pilot_22886ce6f3e04ecc99fb796c73cccf09`,
batch `1320b1ab-040d-445d-b616-a33f1a7f901b`, report
`generate-90bc723ffaad43b68a33f6559d7d3b08.stdout`. Publication
`51316844-0e1c-4898-b89b-ff511e90c29a`, logical Document
`3b541843-4725-0b35-e8c5-62eab158be49`, version
`bd40830b-8c8c-4063-86d8-9b399a06a3f8`. All77 prepared-source hashes match.
Ordinary-reader qualification is not claimed. Isolated success permits initial
whole trial `finalization-whole-00`; up to10 subsequent restarts are authorized
only while no complete success has occurred and resources remain admissible.

Whole initial attempt finalization-whole-00: target phone-0eb120c8cd07,
database vulgata_oct_pilot_ac5cbb3ca5854cdca311854f64eb58a0, batch d3e0831b-1281-44c9-b3b3-8b71068df958,
report generate-ca1e126993304d13be1c3a3dc8d8ce8a.stdout. Exit1 in131.29 seconds, settled/no timeout;
3 accepted files, zero Documents/publication. Failed GetMobileToReplaceResOp.java:
InvalidOutput/ProposalParse/Syntax/Root, MissingClosingDelimiters at EOF,
1 object/1 array open. HTTP/SDK/parser identical7742bytes, Stop/DONE,
179 reasoning tokens on final leaf. Seven requests,40647input/28987output tokens,
all known. All77 source hashes match. Authorized restart1 started; consultation
received sanitized updated evidence and a bounded error-analysis/fix-plan mission.

Restart1 (finalization-whole-01): target phone-b6d632bb8913,
database vulgata_oct_pilot_ac846b6d06d04676af4e2b9838101452, batch 8dd909ab-29fd-4b41-91ff-7774782b84ce, report generate-26cddf52a37644fe9bc2a7bd1be0dcbe.stdout.
Exit1 in70.28seconds, settled/no timeout,1accepted/0Documents/NotRequested.
SdkAntiFraudHandleOp.java failed Syntax/Root: MissingClosingDelimiters at EOF,
1object/1array open, HTTP/SDK/parser identical4079bytes, Stop/DONE,
132reasoning tokens on final leaf.3requests,17971input/15728output, all known.
All77 source hashes match; data retained. Restart2 authorized and started.

Restart2: target phone-22d9bd21a4c8, database vulgata_oct_pilot_191bed94eaf0429a8d07e9f052171bd9,
batch fafdbc2e-bc7a-4bb1-a487-6905a2ea7103, report generate-d4f6c355968c4cb8a370a8a89e0c0441.stdout.
Exit1 in496.38seconds, settled/no timeout,10accepted/0Documents/NotRequested.
Failed src/main/java/cn/com/yitong/actions/domain/login/GuestLoginAuthCheckOp.java : Syntax/Root,
MissingClosingDelimiters atEOF,1object/1array open. HTTP/SDK/parser identical6106
bytes, Stop/DONE,38reasoning tokens. Usage: {"requests":19,"inputTokens":298777,"outputTokens":94499,"hasUnknownUsage":false}.
All77 source hashes match; data retained. Restart3 follows under original grant.

Restart3: target phone-019c3f13c950, database vulgata_oct_pilot_7088b916ca1f4e0e849183f07d59378c,
batch 1efdc60c-33e9-4a51-83ce-9dcf79312b09, report generate-d92e6cdfdd0a4ef397a1ff83286a0f52.stdout.
Exit1 in379.61seconds, settled/no timeout,9accepted/0Documents/NotRequested.
Failed src/main/java/cn/com/yitong/actions/domain/login/CustChnlPwdCheckOp.java : Syntax/Root,
MissingClosingDelimiters atEOF,1object/1array open. HTTP/SDK/parser identical6994
bytes, Stop/DONE,481reasoning tokens. Usage: {"requests":13,"inputTokens":81555,"outputTokens":75595,"hasUnknownUsage":false}.
All77 source hashes match; data retained. Restart4 follows under original grant.

Restart4: target phone-c7bc3722ec83, database vulgata_oct_pilot_8c03a9c5f74e43c2a57fab65df8a2c40,
batch 61384e8a-33b6-4f0f-bbea-d288723c742e, report generate-8c8516aede31437aba139773c3bad3c1.stdout.
Exit1 in319.03seconds, settled/no timeout,10accepted/0Documents/NotRequested.
Failed src/main/resources/disgram/out/login/handPwdLogin.xml : Syntax/Root,
MissingClosingDelimiters atEOF,1object/1array open. HTTP/SDK/parser identical5167
bytes, Stop/DONE,286reasoning tokens. Usage: {"requests":16,"inputTokens":88503,"outputTokens":73308,"hasUnknownUsage":false}.
All77 source hashes match; data retained. Restart5 follows under original grant.

## Conditional next-fix assessment (not implemented during baseline)

The consulter's final output first recommended strict SubmitProposal. Code
assessment found the retained strict-schema projector belongs to a different
historical contract; current provider matching is root-only and ordinary tool
arguments pass through dictionaries, requiring a separate raw-argument path to
preserve duplicate detection. A faithful beta/schema/terminal-carrier migration
is broader than the current narrow correction. No historical schema is adopted.

After receiving these facts, the consulter's final follow-up endorsed a smaller
isolated experiment: opt-in text response format only for the existing single
tool-disabled correction, leaving initial/tool requests in JSON-object mode.
This is a hypothesis about mode-dependent output, not a proven provider cause.
It removes a provider formatting control while preserving the exact local parser,
schema, evidence, coverage and acceptance standards. Prose, fences, multiple JSON
values, malformed JSON and duplicate fields must still fail without extraction or
repair. Existing source/history/reasoning, low effort, cap, endpoint, budgets and
prompt remain unchanged. The baseline series must finish before this patch.

Restart5: target phone-0f327570b687, database vulgata_oct_pilot_b8a08e0b385a416585aee14b228f09f6,
batch 6652f66e-a960-410a-a7a9-a1e86114c68a, report generate-e53d53a7bc6d4be3a75a693e43b16760.stdout.
Exit1 in253.66seconds, settled/no timeout,3accepted/0Documents/NotRequested.
Failed src/main/java/cn/com/yitong/actions/acct/GetMobileToReplaceResOp.java : Syntax/Root,
MissingClosingDelimiters atEOF,1object/1array open. HTTP/SDK/parser identical5579
bytes, Stop/DONE,251reasoning tokens. Usage: {"requests":10,"inputTokens":204807,"outputTokens":42645,"hasUnknownUsage":false}.
All77 source hashes match; data retained. Restart6 follows under original grant.

Restart6: target phone-5ab5ec8229e4, database vulgata_oct_pilot_9cf2fef48ab842ea9d0b9f7e4163c7d1,
batch 2f1efce1-88e2-44ab-9c98-e3991372a869, report generate-2a90aa1fd4704ec689f288372173df68.stdout.
Exit1 in611.28seconds, settled/no timeout,23accepted/0Documents/NotRequested.
QueryAllCreditCardOp.java failed SourceChanged (not a formatting/tool budget error).
Usage: {"requests":32,"inputTokens":275763,"outputTokens":123837,"hasUnknownUsage":false}. All77 source hashes match.
Paid restarts7-10 held for diagnosis: ownedDB read-only expiry metadata shows both
source approvals expire at2026-10-10T17:04:29Z and are not revoked. Authority issues
10minute approvals; Scan factory pins each runtime once to that expiry even though
run deadline is30minutes. This independently explains the later source verification
refusal. No approval token or secret was output. No lifetime waiver or blind retry.
Consumer renewal boundary and native-budget/provenance implications are under
independent assessment; consultation received this distinct error for advice.
