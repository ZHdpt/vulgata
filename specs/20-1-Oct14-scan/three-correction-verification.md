# Three same-position JSON corrections

## Authorization and boundary

The user requested up to three consecutive regenerations of malformed JSON at
the same position, with diagnostic logging. The independent plan assessment
accepted normal Generate = three corrections, legacy hard mode = one ordinary
plus two additional corrections (sixteen additional per original run), no stacked
allowance, and suppression of outer transient replay after correction begins.
This is bounded Scan-owned correction behavior under the accepted October plan;
it does not change Business contracts, accepted Knowledge, publication authority,
tools, source scope or provider/runtime approvals. No paid run is part of this work.

## Implementation

The initial candidate may be followed by three replacements at the same final
source window in the same Worker conversation. Each replacement receives safe
feedback from the latest rejected candidate and the remaining allowance after
that requested replacement. The existing eligible parser categories remain
Syntax, RootShape, ClosedFields, ValueShape and Qualification. Semantic/evidence,
length-finish, authorization, source, budget and tool-protocol failures do not
gain a new retry route. Corrections retain json_object and disabled tools.

Worker result diagnostics count scheduled corrections. Once a correction is
scheduled, a later transport failure cannot advertise safe transient replay, and
the runner independently checks the count before restarting a Worker. Existing
pre-correction safe transient behavior remains. All original request, cumulative
input/output, token, deadline, source, authority and settlement guards still apply.
An available correction slot is not an entitlement to another provider call.

At most eleven typed events are buffered per Worker. The Generator appends them
only after the actual Worker finishes and provider accounting confirms settlement.
Its fixed owned `worker-json-corrections.jsonl` is separate from Business `log.md`.
An explicit projection stores GUID correlations, hashed relative path/provider,
limits, phase, retry/window ordinal, elapsed Worker time, provider rounds, candidate
byte count, normalized current-leaf finish reason, bounded parser location/category
and terminal status/guard details. Finish observations reset before each leaf,
including refused admissions, so a failed correction cannot inherit an earlier stop.
No raw response, source, prompt, reasoning, credential or exception text is logged.
`RetryRequested` is scheduling, not proof of HTTP dispatch. `Completed` means a
validated Worker proposal, not accepted/published Knowledge.

The sink exclusively appends a bounded batch with cancellation-aware I/O and a
two-second cancellation budget. It refuses reparse paths, existing partial lines
and writer contention. Failure adds the safe `json-correction-log-incomplete` gap
without replaying model work or changing acceptance. Process failure before flush
can lose buffered events. No diagnostic record serves as completion authority.

## Verification

Behavioral RED: `isolation-8a09a9e6dcef4cc7beb14ee52ed5ecdc`, ten selected,
four passed, six failed, zero skipped. Failures demonstrated rejection of normal
limits two/three, lack of three-retry recovery and absent correction counters;
build and discovery succeeded.

First affected check: `isolation-31c365dab1eb495c8771eda697e79206`, 552
selected, 545 passed, seven failed. The failures exposed old one-correction call
counts/controlled response sequences and incorrect new parser/event expectations.
Tests were corrected to assert three replacements, the parser's existing
ClosedFields category and zero-retry rejection diagnostics; no production parser
or acceptance assertion was weakened.

Independent static review accepted the final implementation with no remaining
findings, conditional on controlled verification. Review led to explicit path-hash
correlation assertions, fail-fast log-contention coverage, runner-level transient
replay coverage before/after correction, and normalized per-leaf finish diagnostics.
Full SelfContained: `isolation-ee56a15636c247fa9b0d6a6c8d8ac435`, 5,997
discovered, 4,879 selected, 4,877 passed, two failed, zero skipped. One new sink
assertion expected uppercase SHA-256 text although the correct implementation
emits lowercase; the expected digest was corrected, retaining exact comparison.
The other failure is the recorded unrelated
`OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`
(`ArgumentException`: output is not an exact successful one-file CL qualification
report). That assertion and implementation remain unchanged. Both runner transient
cases, three-correction success/exhaustion and existing budget/authority guards
passed. The focused sink rerun `isolation-5ba1597d858545699a91bff9ab3df5ee`
passed one selected test, zero failed/skipped, verifying the final test-only
adjustment. Independent review then accepted the completed change. The full suite
has not been rerun after that assertion-only change; only the known report-parser
failure remains unresolved. This is controlled evidence, not a fully green suite
or real generation qualification.

The prior restricted attempt `isolation-1aaac4d402af436a8d9af459c86fb322`
stopped with an empty build failure before tests; it is infrastructure failure,
not behavioral RED. The identical launcher outside that boundary produced the
complete result above. No PostgreSQL, real provider, native-tool or paid scan
qualification was performed, and the previous real-run grant remains exhausted.
