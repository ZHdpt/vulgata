# Worker contract characterization and proposed correction

Date: 2026-10-09. Status: controlled evidence complete; production correction awaits Integrator decision.

## Boundary and evidence

The source-free matrix uses existing fixtures in `OctoberScanFileWorkerTests.cs` and
`OctoberScanWorkerToolsTests.cs`. No production, prompt, tool contract, Knowledge
schema, diagnostic, provider, native process or database resource changed.
The lane base is `36299848e9c350fdbc4346699d54e565dda87325`; the affected production
files match the integration branch observed at `c26dd22480a22658185c8c1068a629a08a251a26`.

`eng/test.ps1 -Suite SelfContained` with filter
`FullyQualifiedName~OctoberScanFileWorkerTests|FullyQualifiedName~OctoberScanWorkerToolsTests|FullyQualifiedName~OctoberScanWorkerInputLedgerTests`
completed **139/139**, zero failures/skips, in run
`eabc537d52ec4b169f6961adee786b7b`. The build had zero errors; policy classified
441 files and discovery found 5,492 tests. Existing tests and assertions remain intact.
The earlier run `55e2de15b2fc4033b504086d47ed4b4c` failed compilation on an ambiguous
test type name; it is not test evidence. The test owner qualified that type before GREEN.
Independent review accepted the final test-only changes with no blocking findings.

This is focused controlled evidence, not PostgreSQL or real provider/native
qualification. It does not waive the previously recorded full-suite sequential-run
timeout or establish the cause of the latest real `ProposalParse` rejection.

## Findings

1. Exact proposal parsing requires present, case-sensitive keys, including explicit
   null for nullable fields. Duplicate/unknown keys, wrong root shape, qualification
   casing and invalid advisory indices are rejected. Numeric string/overflow cases
   currently report `ProviderFailed` at `ProposalParse`; these tests characterize that
   legacy classification, not a desired normalization. Generic JSON mode does not
   enforce the exact proposal schema.
2. The installed SDK controlled SSE case produced four final updates, two text
   fragments and one mapped reasoning item. Exact Unicode final text and resulting
   proposal exclude both reasoning and prior tool-leaf prose. No observed failure
   justifies further SDK assembly or charging changes.
3. Window splitting preserves exact UTF8 and byte offsets, including a CR/LF split.
   Window payloads lack primary relative path and whole-file line metadata. Scripted
   anchors prove host coordinate handling, not live-model line counting or live JSON
   mode across multiple windows.
4. Missing/null/wrong-type required paths are sanitized by the direct source facade,
   but the composed `LedgerFunction` throws while extracting the path, before source
   access. No native call is made in the tested malformed cases.
5. The direct facade supports continuation. The October composition drains pages
   internally, returns no cursor and rejects an incoming cursor **before another
   source grant/page call**. Its advertised continuation parameter is therefore a
   contract mismatch, not extra native dispatch. This corrects the earlier assessment.
6. CodeGraph maximum omission is accepted; explicit null is withheld. This probe does
   not measure the effective numeric default. Supporting source contributors retain
   provenance, but proposal source anchors bind to the primary file: this is a citation
   limitation, not missing contributor provenance.

## One proposed correction boundary (not yet implemented)

| Scan-owned file | Proposed change | Compatibility and required verification |
| --- | --- | --- |
| `src/dotnet/Vulgata.Agents/Prompts/OctoberFileWorker.md` | State existing required/null/case/type/duplicate rules explicitly; distinguish primary evidence from supporting context. | Same canonical schema and validator. Test prompt/contract consistency and valid/invalid proposals; no weakening. |
| `src/dotnet/Vulgata.Agents/WorkerExecution/OctoberFileWorkerExecutor.cs` | Supply trusted primary relative path, whole-file line count and precise per-window line/column context from verified source. | Authorized relative metadata only; no physical path or extra scope. Preserve byte limits/splitting; verify UTF8, CRLF boundaries, empty/final windows and whole-file anchors. |
| `src/dotnet/Vulgata.OctoberGenerator/OctoberWorkerTools.cs` | Narrow Worker-exposed continuation to the composition's supported automatic paging; route malformed arguments to sanitized refusal before execution. | Do not change the shared direct facade's working paging. Malformed-input refusal may allow bounded same-run continuation instead of fatal exceptions; preserve authority/provenance failures and ledger fail-closed behavior. Verify schemas, no extra dispatch, refusal and security cases. |
| Existing Worker, WorkerTools and WorkerInputLedger tests | Add desired regressions alongside approved behavior changes; update current-behavior characterizations explicitly. | Preserve existing adversarial, acceptance, settlement and accounting checks; run focused SelfContained before integration. |

Shared source/tool facades remain with their owner. If the implementation needs shared
validation changes, agree that boundary with the owner first. Knowledge schemas,
publication and authorization remain Business-owned and unchanged.

## Decisions required before correction or another paid run

- Confirm narrowing the advertised continuation contract, rather than adding external
  cursor lifetime support. The latter would expand this first-deliverable boundary.
- Confirm sanitized malformed-input refusal and bounded continuation semantics while
  keeping genuine authorization/provenance denial fatal and non-disclosing.
- Confirm trusted primary path/line metadata and explicit parser instructions as the
  combined correction. Numeric failure-status normalization is separate unless added
  explicitly; no new parser diagnostic is included.
- Keep primary anchors primary. Supporting context may inform qualified uncertainty;
  it must not be falsely cited as primary evidence. Any richer supporting citation
  model requires a separate Business-owned contract decision.

Integrator owns the implementation dispatch and any subsequent shared qualification
decision, exact resources and paid-run authorization. The current evidence supports
the above bounded correction proposal; it does not identify which exact malformed
field caused the real rejection. Existing retained accepted CL must be preserved.
