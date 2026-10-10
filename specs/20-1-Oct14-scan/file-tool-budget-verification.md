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
