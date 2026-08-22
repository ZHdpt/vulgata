# Contract: Bounded `navigate_code`

`navigate_code` is an internal Worker Agent function, not an HTTP endpoint. The server binds pair, job, current expected job `StateVersion`, current lease token/expiry, actor, System, Repository, Scan Run, Profiled Revision, Prepared Scan Manifest, approved snapshot, prompt/schema/runtime identity, source File Code Unit, and remaining budget. None is accepted from model arguments. Lease values are not serialized into provider messages, function arguments/results, receipts, API/status output, Documents, or logs.

## Request

```json
{
  "operation": "Definition",
  "target": {
    "repositoryRelativePath": "src/main/java/app/OrderService.java",
    "symbol": "app.OrderService.submit",
    "line": 42,
    "utf16Character": 17,
    "lineStart": null,
    "lineEnd": null,
    "expectedSourceHash": null
  },
  "depth": 1,
  "parentObservationIdentity": null
}
```

Unknown properties and unknown operation names are rejected. Exact operation forms are:

| Operation | Required target | Backing capability |
| --- | --- | --- |
| `Definition` | Java path + one-based line + zero-based UTF-16 character | JDT LS definition |
| `References` | Java path + position | JDT LS references |
| `Implementations` | Java path + position | JDT LS implementations |
| `Callers` | exact symbol | CodeGraph callers |
| `Callees` | exact symbol | CodeGraph callees |
| `SymbolSearch` | bounded symbol query | CodeGraph symbol search |
| `RelatedFiles` | exact symbol | CodeGraph one-hop related files |
| `ExactSourceEvidence` | approved path + exact line range + expected Source Hash | revision-bound source evidence |

Unused target fields must be null. A relationship operation traverses one hop. An evidence-bearing request has `depth` equal to parent depth plus one, or one when no parent is supplied, and at most three. The closed request grammar also accepts exactly depth four only to return a metadata-only Depth `BoundExceeded` refusal before analysis; it never traverses a fourth hop. Other nonpositive, over-four, or inconsistent jumps are malformed attempts, not evidence-bearing calls. A cycle or repeated target produces the same metadata-only Depth refusal and no evidence.

## Response

```json
{
  "outcome": "DocumentEvidence",
  "receiptId": "00000000-0000-0000-0000-000000000000",
  "operation": "Definition",
  "depth": 1,
  "resultCount": 1,
  "evidenceBytes": 317,
  "results": [
    {
      "kind": "Document",
      "repositoryRelativePath": "src/main/java/app/OrderService.java",
      "documentVersionId": "00000000-0000-0000-0000-000000000000",
      "sectionIds": ["00000000-0000-0000-0000-000000000000"],
      "sourceAnchors": [
        {
          "sourceAnchorId": "00000000-0000-0000-0000-000000000000",
          "lineStart": 40,
          "lineEnd": 48,
          "byteStart": 912,
          "byteEnd": 1180,
          "sourceHash": "64-lowercase-hex"
        }
      ]
    }
  ],
  "reasonCode": null
}
```

Outcomes are `DocumentEvidence`, `SourceEvidence`, `Unavailable`, or `BoundExceeded`. A `BoundExceeded` response includes only `receiptId`, `operation`/requested `depth` when an invocation exists, and closed `boundKind` (`Results`, `EvidenceBytes`, or `Depth`); results/resultCount/evidenceBytes are empty/zero. `Unavailable` covers absent, unauthorized, stale, outside-approved-snapshot, unsupported-language, and hidden-scope targets and has the same bounded shape. The response contains no provider/host/tool diagnostic.

## Knowledge-first selection

For each exact target descriptor, the facade derives its stable target/observation identity before checking Document availability. It returns current Document evidence only when:

1. the Logical Document is in the job's Repository and authorization scope;
2. the Document Version is current and belongs to the same Scan Run/Profiled Revision;
3. every returned section reference is relevant to the requested target;
4. returned Source Anchors bind current source hash and exact contained byte/line ranges;
5. the target has not been superseded or changed since resolution.

Otherwise the facade immediately reads exact source evidence from the approved revision snapshot. It may use selected File Code Units and safely eligible Story 12.2 audit entries; filtered/skipped/unsafe paths are unavailable. A CodeGraph/JDT result that cannot be bound to an approved current path/hash is discarded, not returned.

## Bounds

- At most eight tool invocation attempts and eight accepted calls per job across all retries. A well-formed `Unavailable` consumes an accepted call; a refused BoundExceeded attempt consumes only its tool attempt.
- At most 20 canonical distinct results in one accepted call.
- At most 65,536 UTF-8 evidence bytes across accepted calls for the job.
- One relationship hop per call; explicit depth at most three.
- A response is assembled and measured before receipt commit. If the complete canonical response exceeds 20 results or the remaining byte bound, atomically persist one metadata-only Results/EvidenceBytes refusal receipt, return its `BoundExceeded` outcome with no partial evidence, and do not advance accepted-call/evidence counters.
- Duplicate/cyclic descriptors are removed before count/byte measurement and cannot reset depth or budget.
- A depth-four/cycle/repeated-target refusal persists the same metadata-only Depth receipt before returning. It never invokes CodeGraph, JDT LS, source, or Document reads.
- When either accepted-call or tool-attempt count reaches eight and disables further calls, that same budget transaction appends at most one server-created Calls exhaustion marker. Before the final result turn the dispatcher supplies this marker's bounded machine outcome; no ninth tool attempt/call occurs.
- The dispatcher allows at most ten provider turns and disables further tool invocation before the final result turn.

## Durable receipt and revalidation

Scope resolution before any Document/analysis/source read requires the bound pair to be `Running` with a closed circuit and the bound job to be `Running` at the exact expected version with the same unexpired lease. The approved Repository/Profiled Revision/source inventory and budget must still match that job. A missing, expired, replaced-owner, paused, completed, terminal, or mismatched binding returns the same empty `Unavailable` response and performs no analysis/source read.

After bounded evidence is assembled internally, receipt commit locks pair, job, and budget and repeats every lease/version/expiry/phase/circuit check using the injected UTC `TimeProvider`. Only a successfully committed source-bearing receipt releases navigation evidence to the Worker. If authorization changes during analysis, the transaction writes no receipt and the function returns empty `Unavailable`; internal evidence is discarded. Every metadata-only BoundExceeded receipt/marker authorizes only the truthful statement that the named bound was exhausted; it cannot prove a relationship, communication kind, target existence, or source claim.

Final Worker result validation checks every receipt belongs to the same job and exact durable budget. It revalidates source-bearing descriptors against the same authorized Document/anchor or revision-bound path/range/hash, and reconstructs refusal receipts from counters/request hashes without any source lookup. A stale or mismatched receipt makes the result invalid and commits nothing.

## Forbidden behavior

The function cannot expose or invoke raw JSON-RPC/LSP, CodeGraph command/query/storage, CodeGraphContext, shell/process, network, arbitrary filesystem, database tools, write operations, host absolute paths, another Repository/revision, or another job's state. It cannot create, claim, await, reprioritize, cancel, or mutate another File Code Unit/job. The model has no other function/tool registered.

Security violations return one bounded refusal to the model, terminate that Worker attempt, and route the durable Security failure policy; target existence is not disclosed.
