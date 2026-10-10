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
