# Story 12.3 exclusion verification

Result: PASS. No Story 12.4 or Story 12.5 production behavior was added in the pinned Story 12.3 boundary.

## Pinned boundary

- Observed: `2026-08-22T00:23:44.9520832+08:00` (Asia/Shanghai).
- Story 12.3 base: `9d763845b311e7b5e8ae23fa78c24631233f9840`.
- Candidate inspected: `cb3fb0f17b2eaf92d93afe71c710cd7b92c1e3bd`.
- Branch: `demo0831/story-12-3` (non-detached).
- `HEAD` was the candidate. The only pre-existing working-tree changes during this qualification were the root-owned checkpoint-binding edits in `docs/bmad/implementation-artifacts/workflow-activations/12-3-resumable-file-pass-1-and-code-logic-documents.json` and `specs/012-resumable-file-pass1-code-logic/workflow-state.json`; this task changed neither.

## Commands and evidence

The following commands were run from `G:\source\repos\vulgata`:

```powershell
git diff --name-status 9d763845b311e7b5e8ae23fa78c24631233f9840..cb3fb0f17b2eaf92d93afe71c710cd7b92c1e3bd
git diff --name-status --diff-filter=A 9d763845b311e7b5e8ae23fa78c24631233f9840..cb3fb0f17b2eaf92d93afe71c710cd7b92c1e3bd -- src/dotnet
git diff --unified=0 9d763845b311e7b5e8ae23fa78c24631233f9840..cb3fb0f17b2eaf92d93afe71c710cd7b92c1e3bd -- src/dotnet/Vulgata.Web | rg '^\+[^+].*(MapGet|MapPost|MapPatch|MapPut|MapDelete|pass1|pass2|chat|release|evaluation|flow)' -i
git grep -n -i -e retrieve_code_logic 9d763845b311e7b5e8ae23fa78c24631233f9840 -- src/dotnet tests/Vulgata.Tests
git grep -n -i -e retrieve_code_logic cb3fb0f17b2eaf92d93afe71c710cd7b92c1e3bd -- src/dotnet tests/Vulgata.Tests
rg -n 'name: "|CreateTable\(' src/dotnet/Vulgata.Infrastructure/Data/Migrations/20260821114734_AddResumablePass1CodeLogic.cs
git status --short
```

The changed-path inventory contains the Story 12.3 contracts, Pass 1 worker/navigation/commit/recovery/API implementation, its migration, and their tests. No added production path is named for Cross-Repository resolution, flow grouping, Pass 2, Business Logic Documents, Chat, release UI, evaluation, or release execution. The added web route/handler inspection returned only `MapPass1ExecutionApiEndpoints` and the four `/api/pass1-executions` routes.

`retrieve_code_logic` returned zero matches at both base and candidate. The new migration's `Up` operation creates only `Pass1PairExecutions`, `Pass1ConcurrencyChanges`, `Pass1NavigationBudgets`, `Pass1NavigationReceipts`, `Pass1Outcomes`, and `Pass1FailureInventoryEntries`. Its generated model snapshot repeats pre-existing Cross-Repository/flow/Business-Logic/Pass 2 model metadata; it does not create or invoke those later-story paths.

## Exclusion results

| Exclusion | Result | Evidence / scoped match |
|---|---|---|
| Story 12.4 Cross-Repository Resolution | PASS | No resolver/store/endpoint path was added. `CrossRepository` in the new file-v2 deferred-outcome validation and deferred-reason mapping records a bounded refusal only; it does not resolve or create a Cross-Repository Edge. Existing Cross-Repository entities and services are present at the base commit. |
| Endpoint-centered flow grouping | PASS | No `FlowGroup`, `FlowTrace`, or flow-grouping production path was added. Those names occur only in generated migration snapshot metadata for pre-existing model types. |
| `retrieve_code_logic` | PASS | Exact symbol search: base `0`, candidate `0`. |
| Pass 2 | PASS | No new Pass 2 worker, dispatch, transition, or endpoint was added. Existing `Pass2` phase/model members are present at the base; generated migration metadata carries the existing phase check constraints. |
| Business Logic Documents | PASS | No Business Logic Document production path was added. `BusinessLogicDocumentVersionId`/document-type strings in generated snapshot metadata are pre-existing model metadata, not new behavior. Story 12.3 persists Code Logic only. |
| Chat | PASS | No Chat implementation, route, prompt, or composition path was added. Existing Chat registrations remain pre-existing; the only new embedded resource is `pass1-code-logic-worker.v2.md`. |
| Final release UI | PASS | No release UI/component/page/route was added; the only new web surface is the bounded Pass 1 execution API. |
| Evaluation questions | PASS | No evaluation-question source, endpoint, or artifact was added. |
| Release run | PASS | No release-run automation or production execution path was added. |
| CodeGraphContext | PASS | No new runtime dependency or composition path was added. The only new production-text occurrence is the Worker prompt's explicit prohibition: “No raw LSP, raw CodeGraph storage/query, CodeGraphContext...”. Other candidate occurrences are pre-existing provider/test references. |
| Unrestricted Agent loop | PASS | The added agent surface is the bounded `PreparedNavigateCodeFunction`/Pass 1 dispatcher with authorized operations and fixed limits. No generic unrestricted tool loop, raw protocol/storage, shell, network, database, or arbitrary filesystem tool was added. |

The candidate therefore remains within Story 12.3's Pass 1 and Code Logic Document boundary. No exclusion violation was found, and no correction was routed.
