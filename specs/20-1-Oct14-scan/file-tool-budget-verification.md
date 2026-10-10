# Two-level primary-file tool budget

The user accepted advisory32/hard48 per-file tool budgets, explicit configuration,
model awareness and reset semantics, with isolated account POM first and whole
scan only on success. Policy exposes advisoryToolCallsPerFile and
maximumToolCallsPerFile, default32/48; validate0<advisory<maximum<=1000.

Scan Control owns one budget per logical primary file, created outside its retry
loop and bound to run/workstream/admission. A new file creates a new budget and
Worker conversation. Source windows, supporting-file reads, tool-argument repairs,
JSON/schema correction and retries of the same file do not reset it.

Before each provider leaf, inject current bounded counts as application-owned
system metadata, counted in ordinary input reservation. The fixed system prompt
explains both levels, warning-only status at32, up to48 admitted calls, and exact
reset conditions. No stale counter is appended permanently to conversation history.
The49th eligible requested tool call is refused before execution and the existing
fail-fast Scan behavior stops the scan. Other source/byte/token/time/request and
authorization guards remain unchanged.

Track requested, admitted and actually invoked calls separately. Invalid-argument
refusals consume admitted capacity but are not counted as executed functions;
hard-limit refusals are not admitted or executed. This avoids falsely counting
rejected calls as execution while retaining a bound on repeated invalid requests.
Direct legacy Worker callers can omit the advisory setting; Generate supplies the
explicit policy and shared budget. No provider limits or shared schemas changed.

Controlled, review and real qualification evidence follow separately.

RED config `isolation-19b44f4073f54c9d85302d22b7998d08`:16 selected,12 passed,
4 failed, zero skips (invalid advisory/hard pairs lacked validation). Worker RED
`isolation-40e803292b2d469f859d9c674a0394a9`:9 selected,9 failed, zero skips.
These cover actual32/48 boundaries, warning continuation, hard refusal, shared
attempt/correction counts, new-file reset and budget-binding mismatch rejection.
The initial hard-limit test draft incorrectly expected continuation and was fixed
before Worker RED to require BudgetExceeded and no over-limit invocation.

Prepared isolated target `phone-15a59cf9791b`, batch
`becdd960-700b-43be-b68a-70e17d35d571`, database
`vulgata_oct_pilot_91924af6762c42b78ca3f225e7cb1b8b`. Account pom.xml is selected;
remaining approved77-file scope is supporting. Configuration explicitly writes
32/48 and preserves low reasoning/cap393216/CL-only and other guards. Earlier
successful and partial data are retained.

Independent read-only review accepted the production boundary with no findings:
same-file lifetime/binding, measured fresh metadata, separate counters, hard
fail-fast behavior and unchanged other finite guards. No provider qualification
was inferred from review.

Full SelfContained `isolation-882dc2e2d007459c8b4a173886e1b276`:4821 selected,
4817 passed,4 failed, zero skips. Build passed. One failure is the retained
`OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`
report-shape mismatch. Two existing reflection-based stream tests needed the new
WorkerClient budget constructor argument. The new49th-call case incorrectly
expected execution of the admitted portion of a rejected buffered round. Corrected
to assert49 requested/48 admitted/32 invoked and executed; successful48-call
case still requires48 executions. No production behavior or guard was weakened.
An affected SelfContained rerun follows; full-suite GREEN is not claimed.

GREEN affected rerun `isolation-68d33eaf367d49c0a9cd435bf1f20c3f`, standard
launcher with `FullyQualifiedName~OctoberScan`:1052 selected,1052 passed, zero
failures/skips. Includes all nine new Worker budget cases, configuration cases,
stream/replay and Generate/acceptance/publication controlled regressions. No
PostgreSQL, native-tool or provider qualification is inferred from this suite.

Generator and test output copies match: Vulgata.Agents.dll SHA256
`C4A70BFC545F02C33C3FD7A3804463651290AF897C93E000E6A4564AC5E6469C`;
Vulgata.OctoberGenerator.dll SHA256
`69225D5EDBB775DF8143AB7A856BDA824BF77E2B92018F47845B08AA97155684`.

## Real isolated POM and conditional whole scan

Reviewed implementation commit `c82adf22` ran account pom.xml alone. Report
`generate-4d0dd47f81fd4f848d0399e14561782a.stdout`: exit0 in74.82 seconds,
no timeout or settlement check, Completed/Complete/Confirmed. Two provider
requests,42824 input/17826 output tokens, all usage known. One accepted primary
file produced five CL Documents in publication
`35353a66-f512-4eb2-b22a-8994bca4aaa4`:

| Logical Document | Exact version |
| --- | --- |
| a84a1156-e63c-c9e9-114a-4fa1af29b430 | 90145d74-b58c-43e7-a264-e3e1030ba029 |
| de83017f-c7e6-13be-6cfa-63148ec62e43 | ecd860c5-82ce-4bb7-a64f-413864e5873a |
| 23dd5d69-9e86-32de-9b7e-49a55765d8ce | 68cad5a4-20c4-4108-9407-837feac8af95 |
| 099af32a-6d66-53be-c74d-8a38de4a44be | c901e9db-6f33-4330-83cf-2411bc20752e |
| e53adfe0-2934-4a82-9f45-998fce71c379 | 7409f682-ef18-4078-8b6d-62ce258559ed |

All77 prepared-source hashes still match. This proves real accepted CL publication
for this POM, not ordinary-reader qualification or that the advisory was crossed;
the terminal report does not expose executed tool counts.

The user's success condition was met. Fresh whole target `phone-ddbd64a3c12d`,
batch `44146569-84be-41a3-a72f-0e8d57b96955`, database
`vulgata_oct_pilot_92076fb8a4d742e9b6fd190f54f1718b` selects all77 approved files.
Bootstrap, source-copy verification, prepare and select passed. Same reviewed
executable and explicit32/48/low/393216 settings; isolated and historical data
remain retained. Whole-run outcome follows separately.

Whole report `generate-e0e7c71189a644a7ad7007b24258e8e1.stdout`: exit1 in431.11
seconds, no timeout, NeedsSettlementCheck=false, empty stderr. Failed/Partial;
seven accepted files, zero returned Documents, publication NotRequested.17
provider requests,316839 input/85308 output tokens, all usage known. Account POM,
EleAcctCustInfoCacheOp.java, GetMobileByAccessCodeOp.java,
GetMobileToReplaceResOp.java, QueryAllCreditCardOp.java, BaseAntiRequestOp.java
and LoginAntiRequestOp.java were accepted. Account
`src/main/java/cn/com/yitong/actions/cust/custcenter/GuestSyncOp.java` failed
InvalidOutput/ProposalParse, Syntax/Root. No tool or budget failure is reported.

Final bounded HTTP observation: MaxTokens393216, HTTP/SDK Stop, DONE seen;
HTTP/SDK/parser each2640 UTF-8 bytes and equal. Each reports MismatchedDelimiter,
two open objects/two open arrays, atEof=false.343 reasoning tokens reported by
both HTTP and SDK. This is malformed provider JSON reaching the parser unchanged,
not a local tool limit or evidence of output-cap truncation. The existing shared
single corrective-turn allowance remains unchanged; no local JSON repair,
validation waiver, further budget widening or additional paid retry was applied.

All77 prepared-source hashes still match after the whole run. All successful
isolated and partial historical/whole-run data remain retained. Controlled budget
behavior and isolated POM real publication pass; whole-slice publication and
ordinary-reader qualification remain incomplete. PostgreSQL suite was not rerun;
these real operations used only the separately approved owned databases.
