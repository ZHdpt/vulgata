# Narrow SearchText validation and controlled reasoning replay

User authorization: October 10, 2026, "Proceed with the narrow SearchText
validation fix and controlled reasoning-replay verification. Preserve the existing
budgets and authorization guards."

Baseline: assigned `f8dc/vulgata` worktree, `oct14/scan`, clean at
`99ce34a2846c83403572d605c7e2ab49c99ccc1a` before this change.

## Implemented boundary

The October Worker adapter rejects invalid SearchText directory/literal arguments
as the existing `InvalidArguments` result before calling the runtime. Canonical
directory validation mirrors `PreparedSourceTraversal`; literal validation mirrors
`PreparedSourceText`: nonempty, no CR/LF, strict UTF-8 at most 256 bytes. Root is
explicitly the empty string. Inputs are never trimmed, truncated or normalized.
Raw string literals are checked before JSON serialization can replace malformed
UTF-16. Tool schema descriptions and the Worker prompt explain these rules.

Current authority verification still precedes argument refusal. Real source
`Denied` results remain fatal, and source containment, owned-state exclusion,
lifetime and result admission remain with their existing owners. The existing
bounded correction route, provider/tool budgets, retries, acceptance and publication
are unchanged. No shared contracts or schemas changed.

## Controlled evidence

Separate test and production owners retained. Tests remain in their existing
SelfContained-classified files; no new suite entry or external dependency needed.

- RED: `isolation-ddab30db10774456aefc44711a0cdbeb`, 21 selected, 7 passed,
  14 failed against unchanged production. Invalid semantic arguments reached the
  runtime; existing page bounds already passed.
- Focused GREEN: `isolation-c4dcd8c3eddd4939ab07863af76665a0`, 289/289 passed,
  zero skips, WorkerTools and FileWorker classes. Includes source-validator parity,
  valid byte boundaries, unchanged arguments, correction, stale authority, typed
  denial and existing provider/tool budget checks.
- An additional escaped unpaired-surrogate `JsonElement` counterexample was added
  after focused GREEN at independent review's request; final gate recorded below.

`CurrentContract_ActualSdkWorkerSecondRequestOmitsToolLeafReasoningReplayAsync`
uses synthetic SSE and an injected in-memory HTTP handler with the actual selected
client, installed SDK and Worker executor. The SDK mapped **one reasoning
fragment**. The subsequent actual serialized request retained the correlated
assistant tool call and tool result but omitted `reasoning_content`. Two Worker
requests and one tool call settled once each; no retry was introduced.

This verifies a compatibility gap on the Worker route after SDK mapping. The
controlled responder permits completion; this is not DeepSeek conformance or a
reproduction of its live failure. The test intentionally characterizes the current
gap and must change when a separately authorized replay fix is implemented. No
reasoning replay production behavior was changed. This cannot explain the earlier
single-request JSON failure and does not establish the JSON-truncation cause.

Independent review found no correctness/scope defect, conditional on the added
escaped-surrogate case and required final verification passing. No real provider,
native tool, PostgreSQL or host qualification was performed. No messages were sent
to other chats.

## Final verification and remaining limitation

`pwsh -NoProfile -File eng/test.ps1` on the final source produced
`isolation-1b730784ee404e808dd69f33f9ac591d`: **4,691/4,692 passed**, one
failure, zero skips. Build completed with zero errors (existing warnings remain);
suite policy and guard checks passed. All **290/290** WorkerTools/FileWorker tests
passed, including the escaped-surrogate counterexample and reasoning replay probe.

The full suite is **not GREEN**. The sole failure is
`OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`.
Static inspection identifies a pre-existing report-contract mismatch: the real
entry-point projection includes `businessDiagnostics` and `workerDiagnostics`,
while the integration fixture's exact top-level `Shape` omits them. Both files
are unchanged from the baseline. That test uses a controlled report module and
does not invoke the changed tools or Worker prompt. No baseline replay/reset was
performed, and no unrelated integration fixture assertion was changed.

This scoped checkpoint does not waive the failing full-suite gate. The integration
report contract needs a separate correction; the controlled replay compatibility
gap remains unfixed, and real JSON/provider qualification remains separate.
Independent final review accepted the scoped change with all 290 affected checks
passing and no remaining scoped findings; it explicitly retained the full-suite
failure and did not claim release readiness.
