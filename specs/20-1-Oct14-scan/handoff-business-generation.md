# CL to Business generation handoff

Date: 2026-10-11. User transfers BL generation to
`specs/20-1-Oct14-business`; Scan waits for further CL changes. This file transfers
implementation context, not a paid-run grant or authority to change another
checkout. No runtime operation or message to another chat was performed for this
handoff. Verify the receiving checkout before integrating; do not reset newer work.

## Checkpoint and preserved result

Implementation: **`5c220d55b7e356e2cc654763b80804d38aa81bbe`**, branch
`oct14/scan`, assigned checkout `C:\Users\13728\.codex\worktrees\f8dc\vulgata`.
Consume this checkpoint with its ancestors; earlier planning contract prose is
not a substitute for these compiled interfaces. In particular, the Business
`contracts/knowledge-contract.md` opening still describes proposed APIs; inspect
current code for actual names and implemented behavior.

The real CL-only run `deadline240-whole-01` completed 77/77 source files and
published **163 CL Documents** in 2765.81 seconds. Coverage Complete, execution
Completed, publication Confirmed; no source mismatches or unsettled process flag.

| Binding | Exact retained value |
| --- | --- |
| Target | `phone-15bfe12615eb` |
| Batch | `1a327657-6a55-4e65-ba06-6992f423d40e` |
| Original CL publication | `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9` |
| Selection digest | `52fde3a3354bb00ce686d6eb4f10feb9279d0f2d9daea9a6b8473f51e48b43bd` |
| Database | `vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d` |
| Owned runtime root | `C:\Users\13728\AppData\Local\Vulgata\OctoberRuns\phone-15bfe12615eb` |
| Local terminal ledger | `TestResults/replay-paid/deadline240-whole-01.json` |
| Root-relative report | `logs/generate-35afbe1fa6744344b312c3673dc7e048.stdout` |

Do not delete, migrate to a competing schema, replace or regenerate this CL result
as BL preparation. The original CL publication remains the recovery/reference
anchor even after successful BL publication. This run did not separately qualify
the ordinary reader/browser. Its single paid-run grant is consumed. See
[real result and budgets](configurable-run-deadline.md).

## Ownership and interfaces

Paths below are repository-relative; all contracts are under
`src/dotnet/Vulgata.Core/DomainServices`, persistence under
`src/dotnet/Vulgata.Infrastructure/Data/October/Knowledge`.

| Owner / seam | Implementation and obligation |
| --- | --- |
| Scan Control / CL | `Vulgata.OctoberGenerator/OctoberGenerateRunner*.cs`, `OctoberExecutionScope.cs`, `OctoberScanPolicy.cs`; `Vulgata.Agents/WorkerExecution/OctoberFileWorkerExecutor.cs`. Own deterministic execution, budgets, source verification, actual call settlement and trusted acceptance handoff. CL-only must stay independent of Business resolution/synthesis. |
| Business / accepted Knowledge | `OctoberKnowledgeContracts.cs`, `OctoberKnowledgePublicationPolicy.cs` and owner persistence. Own canonical schemas, accepted semantics, immutable versions, publication membership, contributor authorization and October migrations. No parallel store/policy. |
| Existing-CL admission | `OctoberBusinessFromClContracts.cs`, `OctoberBusinessFromClStore.cs`, `OctoberKnowledgePublicationCommitter.cs`. `BusinessFromClRequest` pins publication, subject, fresh RunId and canonical digest. `BusinessFromClSnapshot` returns corpus, accepted files, coverage, contributors and validated retained resolutions. |
| Shared live authority | `IOctoberBusinessFromClAuthority`, `IOctoberCommitSubmissionGuard`, `IOctoberBusinessFromClSettlementAuthority`; `BusinessFromClAdmission` is an issuer-verified process-local capability, not serialized execution permission. |
| Business planning/execution | `OctoberResolutionPersistenceContracts.cs`, `OctoberBusinessFinalizationContracts.cs`, `OctoberBusinessOriginalDispatchContracts.cs`; owner `OctoberFlowPlanningInputLoader.cs`, `OctoberResolutionStore.cs`; Agents `OctoberBusiness/OctoberResolutionExecutor.cs`, `OctoberBusinessSynthesisExecutor.cs`, `OctoberBusinessFinalizer.cs`. Preserve exact input Document Versions, root selections, qualifications and contributor union. |
| Command seam | `OctoberGenerateRunner.ExistingCl.cs`, `.Business.cs`, `.BusinessGuard.cs`, `.OriginalBusiness.cs`, `Program.cs`. Business may continue the transferred BL work here as needed, coordinating shared-file changes with subsequent Scan work. Keep ordinary fresh Generate and CL-only behavior intact. |
| Web / Identity | Shared Web composition remains integration-owned. Use the genuine verified reader Identity binding already composed in Program. Management authority is not a security stamp or ordinary-reader authorization. |

Core remains domain-only; Infrastructure owns EF/storage. Agent executors receive
injected capabilities, not direct EF or arbitrary source/host access. A document
handle identifies exact content; it never grants disclosure permission. Business
Mode remains BL-only. Preserve qualified source/advisory/uncertain claims and
all-contributor authorization; synthesis cannot promote advisory material to proof.

## Existing-CL route and failure boundaries

Policy fragment (retain the other explicit policy fields):

```json
{
  "outputStage": "cl-and-bl",
  "existingClPublicationId": "fff8ddfc-d8ae-480c-aadd-286f03c8f4d9"
}
```

An empty selector or a selector with `cl-only` is rejected. Absence preserves the
original fresh Generate route. The existing-CL path requires the exact database,
target, batch and selection binding and a committed complete CL-only publication
whose full membership matches the frozen corpus. It performs no BeginGenerate,
CL Worker call, source inventory/native tool execution, AcceptFile, FreezeCorpus
or replacement CL publication.

1. Begin a fresh authenticated run/provider/budget scope. Owner validation loads
   the exact frozen corpus and retained resolution outcomes under fresh permission
   checks. Explicit root selection remains required.
2. Persist `BeginBusinessFromCl` (OperationKind 6) using fresh RunId. Begin and
   `CompleteBusinessFromCl` (kind 7) serialize on the existing batch row with
   `FOR UPDATE`; an unmatched begin blocks another attempt. These are append-only
   receipts in existing KnowledgeOperations, not new tables or a migration.
3. Run Business resolution/planning/synthesis with original dispatch guards. Reuse
   exact committed Supported/Unresolved resolution outcomes. In this new route,
   null-witness provider/parse/budget failure stops before storing Failed or
   NotEvaluated, avoiding a poisoned unique inquiry fingerprint. Legacy retained
   Failed/NotEvaluated outcomes fail closed; do not silently overwrite them.
4. Successful BL finalization appends a separate publication. It never changes the
   original CL versions/content/members or automatically retargets its readers.
5. After actual calls, commits and providers settle, the original owner capability
   can write the completion receipt. This cleanup-only action can finish after
   deadline or permission revocation but cannot disclose content or reopen work.
   A subsequent attempt needs a new RunId and fresh authorization/budget.

A settled failure permits a fresh attempt from the same CL; already committed
resolution progress survives. Unknown commit/transport outcomes, unfinished calls
or a lost original process remain blocked. There is no automatic crash takeover,
time-based lease expiry, generic Scan resume or reconstruction of dispatch
authority from stored receipts. Do not clear unmatched receipts to force a retry.
A failed BL report need not return the old publication handle: preservation is
proved by unchanged storage and opening the original exact handle.

See [implementation and review evidence](business-from-existing-cl.md) and
[configuration instructions](quickstart.md#bl-from-an-existing-complete-cl-publication).

## LLM failures: findings, fixes and limits

These are different failure classes; do not classify every short/incomplete
response as token exhaustion or SDK stream loss.

| Observed problem | Applied correction / interpretation | Evidence |
| --- | --- | --- |
| DeepSeek did not honor the newer cap field as expected | Exact DeepSeek endpoint compatibility maps `max_completion_tokens` to `max_tokens`, retaining the admitted value. This fixed cap compatibility, not all malformed JSON. Adapter lives in `WorkerExecution/PreparedPass1JobExecutor.cs`; preserve its endpoint predicate. | [Wire investigation](json-wire-assessment.md), current `PreparedPass1JobExecutor.cs` compatibility adapter |
| Tool continuation lost reasoning replay | Worker preserves accounted reasoning separately from final text. Installed SDK adapter read reasoning but omitted outgoing `reasoning_content`; selected-client conversion patches exact concatenated reasoning into cloned assistant messages without mutating caller history. Scope is qualified DeepSeek Worker streaming, not blanket Business behavior. | [Reasoning replay](reasoning-replay-verification.md) |
| POM output cap consumed entirely by reasoning | Earlier byte-based input subtraction reduced output to 4011; output reservation was corrected. Low reasoning with 8192 still hit Length using all 8192 as reasoning. Explicit 393216 cap was later selected; it is not a cure for normal-Stop malformed output. | [Output reservation](output-reservation-verification.md), [low effort](low-reasoning-verification.md), [large cap](max-output-cap-verification.md) |
| BudgetExceeded after only 17.34 seconds | Local serialized-input reservation, not elapsed timeout: JSON/prompt/schema overhead exceeded the old allowance. Use the existing 1048576 per-request ceiling capped by run policy; reserve measured input plus requested output. Byte proxy is not tokenizer output. Existing cumulative/output/source bounds remain. | [Input admission](input-admission-verification.md) |
| 8192 stream-update guard stopped useful output | Removed unnecessary retention of every update. Aggregate charged text/reasoning; retain last response ID. Bound non-progress updates, bytes and protected segments and honor cancellation. Do not remove the remaining bounds. | [Stream aggregation](stream-aggregation-verification.md) |
| Invalid SearchText model arguments | Validate canonical directory and nonempty strict-UTF8 literal (at most 256 bytes, no CR/LF), after live authority check and before runtime call. Return typed InvalidArguments; no trimming or normalization; genuine Denied stays fatal. Prompt/schema explain constraints. | [Search validation](search-validation-replay-verification.md) |
| Valid JSON violated closed fields/types/qualification | Exact schema already appears in the system prompt. JSON-object mode guarantees neither our schema nor evidence correctness. Reuse strict duplicate-aware parser and safe known-field/category feedback; no competing validator or automatic key/bracket repair. | [Structural correction](structural-correction-verification.md) |
| Incomplete final JSON with normal Stop | Seen across changing files; isolated retries could succeed. Representative diagnostics show identical HTTP/SDK/parser bytes with Stop/DONE and open delimiters. No missing suffix in our aggregation was demonstrated; no outgoing `stop` sequence in controlled wire tests. These cases were not demonstrated max-token exhaustion. Timing alone does not establish transport loss. | [Wire checks](stop-json-wire-verification.md), [text experiment](text-correction-verification.md), [hard experiment](hard-correction-verification.md) |
| Correction continued tool investigation or returned bad JSON | Correction sends `tool_choice=none` and host rejects unexpected calls, while retaining declarations and protocol-required completed tool/reasoning history. Safe syntax line/byte and structural feedback only. Text-format experiment did not eliminate failures and was rolled back: requests expecting JSON use `json_object`; normal text conversations do not. | [Tool-disabled correction](tool-disabled-correction-verification.md), [current quickstart](quickstart.md) |
| Repeated malformed final output | Current normal Worker permits three regenerations at the same final source-window position, excluding initial proposal. First keeps configured thinking; second disables thinking; third prunes rejected proposals/obsolete feedback and disables thinking. Original system schema, evidence/windows/advisories and complete tool exchanges remain. Next call/file restores configured behavior because overrides are request-local. | [Three corrections](three-correction-verification.md), [staged strategy](staged-correction-verification.md) |
| Tool limit confusion across files | Explicit advisory32/hard48 per primary file, with fresh system budget metadata before each provider leaf. Warning32 does not stop; call49 is refused. Same-file windows, correction and retries share the budget; only a new primary file/conversation resets it. | [File tool budgets](file-tool-budget-verification.md) |
| Transport/Cancelled near run deadline | Means cancellation during provider transport, not malformed JSON. The 49/77 run's retained report did not identify the cancellation source; proximity to 1800 seconds was not proof. Configurable runDeadlineSeconds now supports 1..86400, default1800; later14400-second run completed. Keep file/lease/session budgets distinct and renew only through valid authority. | [Staged real failure](staged-correction-verification.md), [deadline](configurable-run-deadline.md), [session renewal](session-renewal-verification.md) |

Correction eligibility remains syntax/root/closed-field/value/qualification shape
on an eligible final Stop response. It does not repair provenance/semantic failures,
Length, authorization, source changes, unknown writes or exhausted budgets. Once
correction starts, outer transient Worker replay cannot stack a fresh allowance.
Legacy hard mode is optional, off in the successful real run, and does not provide
unbounded retries. Old repeated-whole-run grants are exhausted, not reusable BL grants.

Diagnostic log `logs/generator/worker-json-corrections.jsonl` records bounded IDs,
hashed file/provider identity, retry strategy, byte count, finish/parser category,
timings and outcome. No source, raw response/reasoning, credentials or exception
text. `RetryRequested` is not proof of HTTP dispatch; Worker Completed is not
accepted/published Knowledge. Logging gaps must remain visible.

**Important for Business:** the staged Worker correction/history/tool-budget
mechanism is not automatically applied to `OctoberResolutionExecutor` or
`OctoberBusinessSynthesisExecutor`. They already request JSON with empty tools,
but any equivalent correction policy needs Business-specific design, guards and
wire tests. Do not assume a CL-tested option changes their requests. Preserve
Business token reservations and witness/acceptance contracts when evaluating reuse.
The successful CL run used 122 requests, 1198311 input and 624861 output tokens,
all usage known. It proves one complete CL run, not universal JSON reliability or
BL provider qualification.

## Verification and start prerequisites for Business

Independent review accepted the new route. Final focused controlled checks passed
6/6 (`isolation-42b1f54871db4e0da4b993de7b897fba`); owned PostgreSQL passed10/10
(`isolation-268ce4ffac57480ebdca058728d2e58b`), including concurrent admission and
completion/next-attempt behavior. Its test container and volume were removed.
Full SelfContained passed4908/4909, with the pre-existing
`OctoberGeneratedClReportTests.Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync`
failure. No full-suite GREEN or paid BL qualification is claimed.

Tests live in `tests/Vulgata.Tests/OctoberScan/OctoberScanBusinessFinalizationTests.cs`,
`OctoberScanGenerateConfigurationTests.cs`, `OctoberScanClPublicationPostgreSqlTests.cs`
and existing Business flow-publication tests. Use `pwsh -NoProfile -File eng/test.ps1`
with the correct suite/filter; preserve inventory and owned-resource enforcement.
The tests never used the retained real CL database.

Before a paid BL run, establish together:

- Explicit roots: last inspected Business config had `roots: []`, limits64 nodes,
  128 edges/depth8 and maximumAttemptTokens32768. Select exact repository/path/
  semantic selectors or use the bounded operator-selection route; no automatic
  root discovery is implied by handing over BL.
- Provider/model, run/token/spend ceiling and current contributor permissions.
  Prior CL settings are evidence, not a recommendation to copy every budget to BL.
- A fresh authorized invocation/log/process boundary using the preserved binding
  and protected credentials. Do not bypass the old launcher's single-use marker,
  invoke Prepare or create a replacement CL batch. Verify genuine Identity reader
  binding; do not copy secrets into this file or reports.
- Exact final publication and ordinary Business-reader qualification after success,
  while proving the old CL handle/content remain usable. On failure, classify
  settled versus indeterminate before authorizing any new attempt.

No new BL run, source cleanup, schema change, broad S3 scheduling or general
recovery policy was started for this handoff. Scan is waiting for CL-only changes.
