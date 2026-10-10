# Correction-only text response-format experiment

The user authorized a narrow evaluated fix and another bounded whole-run series
after the first series failed. Initial whole plus10restarts produced no complete
publication. Most failures were normally-ended incomplete final JSON; separate
source approval/descriptor lifecycle defects were corrected and separately tested.
Restart10 hit the existing cumulative input-byte limit. That limit stays unchanged.

Consulter final advice and independent plan assessment accept this experiment:
`UseTextForJsonCorrection` defaults false in Scan policy and Worker limits. When
true, only the existing single eligible correction changes its cloned request
ResponseFormat to Text. Initial, tool-continuation and next-file requests remain
JSON-object mode. Same endpoint, low reasoning,393216 cap, prompt, replay/history,
tool declarations and tool-choice-none; host rejects any correction tool call.
No new attempt, carrier, beta endpoint, extraction, delimiter repair or schema.
The local duplicate-aware parser, evidence/semantic checks, authorization, budgets,
deadlines, settlement, atomic acceptance and exact publication remain unchanged.

This is a hypothesis about generation mode, not evidence of the provider's internal
cause. Success without a correction does not qualify correction recovery. Neither
text mode nor a completed Stop can make malformed JSON acceptable. A budget refusal
is not an unexpected syntax error and does not receive an allowance reset.

Owned production: OctoberScanPolicy.cs, OctoberGenerateRunner.cs,
Vulgata.Agents/WorkerExecution/OctoberFileWorkerExecutor.cs. Existing controlled
wire/runner/parser test classes own verification; shared contracts/store/Identity
remain unchanged. Hardmode stays off and unimplemented during the comparison.
After controlled tests and independent implementation review, retry the latest
failed account POM alone, then up to10 fresh whole runs, stopping at the first
complete successful publication. New authority/settlement defects stop affected
runtime work for diagnosis rather than blindly consuming the remaining attempts.

Controlled RED `isolation-cd67f29a2d0b4c1a9117a704c6738b26`:31selected,
29passed,2failed,0skipped. Both failures are actual controlled SDK wire checks:
opt-in Worker and runner correction expected text but still emitted json_object.
Production correction now propagates the flag and changes only cloned correction
options. Independent implementation/test review accepted without findings.
Full SelfContained verification follows; no real text-mode run has yet occurred.

Full SelfContained `isolation-8ea4d1d9786942608f7a1c16de460362`:
4844selected,4843passed,1failed,0skipped. All31correction cases passed. The sole
failure is the existing exact generated-report parser case; no assertion was
weakened and full-suite GREEN is not claimed. Actual controlled SDK wire shows
initial/tool leaves JSON-object, correction text only when opted in, unchanged
tool declarations/none/history/cap, and strict malformed-output refusal.
Generator/test-copy Agents SHA256 both
`10187D5D3CB63937B1B799D4E5360863A1D450D9344E858467F0AA5D441D60BE`;
Generator SHA256 `D1456D490A3D00B97F01478603DED47866CAE67B82BFEF6039EA120E819020C0`.

Committed `ee9077df7fe81ea738580143a9e76d953cbd406c`.
Real isolated account POM `text-isolated-01`:95.14s,
Completed/Complete/Confirmed,1accepted file/6Documents, settled/no timeout,
all77source hashes match. One request,14113input/25561output, known usage.
Because only one request occurred, no correction was exercised: this success
does not establish effectiveness of text-mode correction. Reader access was not
requalified. Root phone-46a3969b350a, database
vulgata_oct_pilot_b76bab0e6aba45c6bba16fecef90df78,
batch b0f1adb5-3773-4550-99f8-3d66ec9b7a82,
report generate-2f50da7c2b824190b0fbb1e2faa7a9fb.stdout,
retained publication e6aae385-47f9-43b8-bcd2-81cec9b69495.

| Logical Document | Exact version |
|---|---|
|0696bc07-8690-8b05-1347-64061cbcc6d0|5730606e-5d77-4e04-9a79-6e060941161e|
|c0576127-96f0-1676-7753-bdbdd2952bd2|9e926eff-b7ab-49b5-8d6a-83b76ed5cfbb|
|90bd5f7a-79b6-9e58-2b28-2f4114439d37|30a039f4-c84a-4db7-ac2c-c06bdf50e596|
|40c73201-0a47-c12f-d30d-fd3ff7061e9a|67ab23d6-1527-46d4-a5c9-0e39c9525f62|
|0e69a09b-7d5f-90f7-0b99-d73f06ee929e|12e800a4-89fd-46c1-914d-7222dc7cc552|
|a4104a92-2af2-ac64-5667-91410ce21b15|58fd7af8-42f2-453e-8fcd-59c6fac6c3ff|

Second series whole trial1 started with the flag true and hardmode off.

Whole trial1 `text-whole-01`: phone-237a2f82de67,
database vulgata_oct_pilot_72c00085f8b84b4fae176599e37fab1f,
batch d8a7ef35-0eae-41d4-b4b2-b99fadccdfc7,
report generate-e655e3c2d3b04433bb372aebcde1d489.stdout.
Exit1 in309.82s, settled/no timeout,5accepted/0Documents/NotRequested.
BaseAntiRequestOp.java failed Syntax/Root: MissingClosingDelimiters atEOF,
1object/1array open,10452identical HTTP/SDK/parser bytes, Stop/DONE,
205reasoning tokens.14requests,229244input/55881output, known usage.
Flagtrue confirmed in run ledger; all77source hashes match. This was not a
file-deadline failure; an earlier progress count of0was stale by terminal reporting.
Trial2 follows. No full publication or real text-mode recovery is claimed.

Whole trial2: phone-ccebfd1944e0,
database vulgata_oct_pilot_57a2071674d64819aa0f2aaa002ae1d7,
batch dfc89de4-f0b6-4b13-8d2c-378605fd9922,
report generate-bd9b622abf9c41eeb078b840087bcf37.stdout.
Exit1 in298.14s, settled/no timeout,9accepted/0Documents/NotRequested.
CustChnlPwdCheckOp.java failed Syntax/Root, MissingClosingDelimiters atEOF,
1object/1array open,7221identical HTTP/SDK/parser bytes, Stop/DONE,
1499reasoning tokens.12requests,75447input/71418output, known usage;
all77source hashes match. Trial3 follows with unchanged options.

Whole trial3: phone-97489f4617f7,
database vulgata_oct_pilot_2c8c28bb67d5400b9f79c9292d9f862d,
batch c50bba53-6546-44b3-9ea2-e3f45149bcd6,
report generate-5a7d1886f0fb4758afd7b64ce69a5f01.stdout.
Exit1 in129.96s, settled/no timeout,3accepted/0Documents/NotRequested.
GuestSessionCreateOp.java failed Syntax/Root, MissingClosingDelimiters atEOF,
1object/1array open,7707identical HTTP/SDK/parser bytes, Stop/DONE,
335reasoning tokens.5requests,34350input/29913output, known usage;
all77source hashes match. Trial4 follows with unchanged options.

Whole trial4: phone-2f104b1f690a,
database vulgata_oct_pilot_c525351588104b6c997a067615499407,
batch5f4d448e-dc23-4af9-8246-74be2a5dd430,
report generate-b8cbfc9cc8b1496fb45edaad6eade8dd.stdout.
Exit1 in181.18s, settled/no timeout,2accepted/0Documents/NotRequested.
GetMobileByAccessCodeOp.java failed Syntax/Root, MissingClosingDelimiters atEOF,
1object/1array open,3705identical HTTP/SDK/parser bytes, Stop/DONE,
302reasoning tokens.7requests,111822input/35408output, known usage;
all77source hashes match. Trial5 follows with unchanged options.

Whole trial5: phone-dbd8474d23e6,
database vulgata_oct_pilot_8efadc1e145f4980a5e8d29195bc9b26,
batch e409cc6c-3db7-4747-9a6d-ddc7e5cf0600,
report generate-c75db05793474d9aab77ecf8d8edd9be.stdout.
Exit1 in639.02s, settled/no timeout,22accepted/0Documents/NotRequested.
GetMobileToReplaceResOp.java failed Syntax/Root, MissingClosingDelimiters atEOF,
1object/1array open,7071identical HTTP/SDK/parser bytes, Stop/DONE,
1241reasoning tokens.28requests,146024input/139219output, known usage;
all77source hashes match. Approval metadata showed both initial grants expiring
19:02:38 UTC and renewed grants expiring19:07:20 UTC; accepted-file count advanced
from14 to18 to22 after renewal. This is real continuation across renewal, not full
publication or ordinary-reader qualification. Trial6 follows unchanged.

Whole trial6: phone-7e000b12868e,
database vulgata_oct_pilot_011a65458c5b4ed494eb24ed144ce03c,
batch d823be5d-6c2d-4d90-a904-7d3dbb2a913e,
report generate-6243c7dfca7a428abd6a445aac2772b8.stdout.
Exit1 in285.54s, settled/no timeout,4accepted/0Documents/NotRequested.
QueryAllCreditCardOp.java failed Syntax/Root, MissingClosingDelimiters atEOF,
1object/1array open,6932identical HTTP/SDK/parser bytes, Stop/DONE,
111reasoning tokens.12requests,255314input/54336output, known usage;
all77source hashes match. Trial7 follows unchanged.

Consultation final c97dfb67-3dd0-4602-8f5d-3c3ed3796002 (turn
6e56b72f-4df8-4e90-8161-923c44d5d646) assessed first five outcomes: no evidence-based
reason to change this active series or parser; text mode has not eliminated the
dominant failure. Counts do not establish a correction success rate. Conditional
hard mode remains a bounded experiment, not guaranteed publication: latest typed
feedback, same Worker/history, no tools, no ordinary-loop reset, no semantic or
evidence retries, and every original admission/deadline ceiling still applies.
Independent reviewer agreed: no exceptions to provider rounds, per-file requests,
scope admission or attempt settlement; two extra/file and sixteen/run only permit
attempts with existing headroom. Require transient retries zero before admission.

Whole trial7: phone-11fa9c2644a8,
database vulgata_oct_pilot_11e90ad14df04c85928323b3ea329bb8,
batch4cac3d9b-0b3b-498d-b82d-f15ad103703b,
report generate-0ca9e265e4804a24826e47e1a7085f1f.stdout.
Exit1 in231.97s, settled/no timeout,3accepted/0Documents/NotRequested.
GetMobileToReplaceResOp.java failed Syntax/Root, MissingClosingDelimiters atEOF,
1object/1array open,6929identical HTTP/SDK/parser bytes, Stop/DONE,
1411reasoning tokens.10requests,192173input/35022output, known usage;
all77source hashes match. Trial8 follows unchanged.

Whole trial8: phone-f4758a075f1d,
database vulgata_oct_pilot_7df4527ee3e94b9cab29431d94664319,
batch07663672-3117-464f-9ee3-b1e1ac07d0cd,
report generate-b050f5ec607a486a923aeb771810d9f8.stdout.
Exit1 in480.01s, settled/no timeout,9accepted/0Documents/NotRequested.
CustChnlPwdCheckOp.java failed Syntax/Root, MissingClosingDelimiters atEOF,
1object/1array open,5988identical HTTP/SDK/parser bytes, Stop/DONE,
540reasoning tokens.21requests,267998input/83583output, known usage;
all77source hashes match. Trial9 follows unchanged.

Whole trial9: phone-d6e3cf9d8a5f,
database vulgata_oct_pilot_f2790c9cde4a4ca18eb9db0e8276d5d5,
batch7d81898b-dfd9-4e26-84a2-c9a1fb2b5987,
report generate-c4760ab7b8c144759e9031704875de98.stdout.
Exit1 in241.10s, settled/no timeout,6accepted/0Documents/NotRequested.
channel disgram/in/login/handPwdLogin.xml failed Syntax/Root,
MissingClosingDelimiters atEOF,1object/1array open,
5024identical HTTP/SDK/parser bytes, Stop/DONE,1630reasoning tokens.
8requests,49578input/60086output, known usage; all77source hashes match.
Trial10 follows unchanged and is the last trial in this series.

Whole trial10: phone-6569f6cc1af9,
database vulgata_oct_pilot_4f26d337bf444033af6212e77e089592,
batch54040c86-bf0d-47d6-b9d1-2675634246f8,
report generate-be67d6d967ba4aa8b4fa08bb7e239481.stdout.
Exit1 in580.78s, settled/no timeout,18accepted/0Documents/NotRequested.
LoginTypeOp.java failed Syntax/Root, MissingClosingDelimiters atEOF,
1object/1array open,6892identical HTTP/SDK/parser bytes, Stop/DONE,
1448reasoning tokens.26requests,150109input/124874output, known usage;
all77source hashes match. Second series complete: ten failures, no full publication.
The user's conditional hard-mode implementation and single qualification run now apply.
