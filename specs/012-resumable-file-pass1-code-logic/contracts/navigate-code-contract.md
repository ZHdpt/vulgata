# Contract: Bounded `navigate_code`

`navigate_code` is the sole evidence-access Worker Agent function, not an HTTP endpoint. It is one of exactly two registered functions; the other, `submit_code_logic`, is non-evidence finalization and cannot invoke this facade. The server binds pair, job, current expected job `StateVersion`, current lease token/expiry, actor, System, Repository, Scan Run, Profiled Revision, Prepared Scan Manifest, approved snapshot, prompt/schema/runtime identity, source File Code Unit, and remaining budget. None is accepted from model arguments. Lease values are not serialized into provider messages, function arguments/results, receipts, API/status output, Documents, or logs.

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

- At most eight `navigate_code` invocation attempts and eight accepted navigation calls per job across all retries, lease recovery, crash, and restart. `Pass1NavigationBudget.ToolAttemptCount` is the contiguous durable reservation high-water: every ordinal through it is consumed even if no receipt exists. Submission does not alter it.
- At most 20 canonical distinct results in one accepted call.
- At most 65,536 UTF-8 evidence bytes across accepted calls for the job.
- One relationship hop per call; explicit depth at most three.
- Whole-response call-set eligibility is checked before reservation; an illegal response reserves nothing. A one-through-eight all-navigation response may be eligible beyond current remaining capacity: only its provider-order fitting prefix reaches this reservation path, and every excess sibling receives constant `navigation-budget-exhausted` with no callback/analysis/source/receipt effect. For an admitted serial call, one current-lease/version transaction reserves exactly the next ordinal before any callback/analysis/source access and returns the committed high-water/state-version cursor. Receipt commit replaces that cursor; a no-receipt outcome retains the reservation cursor. The next admitted sibling may use only the latest store-returned cursor. A failed reservation invokes no callback for that call or later siblings.
- A response is assembled and measured before receipt commit. If it exceeds results/bytes, persist one metadata-only refusal against the already-reserved ordinal; do not increment `ToolAttemptCount` again or advance accepted/evidence counters.
- Duplicate/cyclic descriptors are removed before count/byte measurement and cannot reset depth or budget.
- A depth-four/cycle/repeated-target refusal persists the same metadata-only Depth receipt before returning. It never invokes CodeGraph, JDT LS, source, or Document reads.
- `Guid.Empty`, malformed local input, exception, cancellation, lost response, lease expiry, or crash after reservation leaves that ordinal consumed and may leave no ordinary receipt. Such a gap has no evidence authority and is never reclaimed.
- After reserved ordinal eight reaches a closed callback outcome, idempotently append the existing Calls marker at receipt ordinal nine with final cumulative counters. Claim hydration/lease recovery and terminal/reconciliation paths repair a crash-window missing marker before provider or terminal visibility. No ninth callback occurs.
- The executor allows at most ten provider service turns and at most two separate `submit_code_logic` invocations. Navigation is disabled when submission begins and is never provider-visible after turn eight. The pinned SDK callback iteration is zero-based and Vulgata derives `providerTurn = Iteration + 1`; SDK iteration 8/provider turn nine forces a sole first submission when no earlier submission ended the execution, and only an invalid turn-nine candidate authorizes SDK iteration 9/provider turn ten as the sole correction. Navigation always rejects without evidence access on turns nine and ten, turn ten cannot be a first-submission fallback, and no provider request eleven occurs.

## Durable receipt and revalidation

Claim hydration loads the budget row, repairs a required Calls marker, and binds exact server-only `ToolAttemptCount`, budget state version, and `RemainingNavigationBudget = 8 - ToolAttemptCount`; defaulting to eight without a zero high-water is forbidden. Exact hydrated zero enters the normal provider conversation and permits submission but can never reach reservation. Before each callback, reservation requires the bound pair Running/closed and job Running at the exact version/current unexpired lease. A missing/expired/replaced/paused/terminal binding fails reservation and performs no callback/read.

After bounded evidence is assembled, receipt commit locks pair/job/budget, repeats lease/version checks, requires `1 <= ToolAttemptOrdinal <= ToolAttemptCount`, and rejects an unreserved or conflicting ordinal. Ordinary `ReceiptOrdinal` equals `ToolAttemptOrdinal`; commit updates accepted/evidence/depth but never attempt count. Same ordinal/fingerprint replays the existing ID. Only a committed source-bearing receipt releases evidence. Every refusal/marker authorizes only the named bound.

Final Worker result validation checks every cited receipt belongs to the same job, reserved ordinal, and exact durable budget. It tolerates uncited missing receipt ordinals as consumed no-evidence attempts, requires marker nine when high-water is eight, revalidates source-bearing descriptors, and reconstructs refusals without source lookup. Stale/mismatched/conflicting rows invalidate the result.

## Forbidden behavior

The function cannot expose or invoke raw JSON-RPC/LSP, CodeGraph command/query/storage, CodeGraphContext, shell/process, network, arbitrary filesystem, database tools, write operations, host absolute paths, another Repository/revision, or another job's state. It cannot create, claim, await, reprioritize, cancel, or mutate another File Code Unit/job. No function other than this one may expose evidence; `submit_code_logic` only performs local finalization.

Security violations return one bounded refusal to the model, terminate that Worker attempt, and route the durable Security failure policy; target existence is not disclosed.
