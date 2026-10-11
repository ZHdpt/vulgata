# Real whole CL scan with three JSON corrections

## Authorization and executable boundary

On 2026-10-11 the user requested a whole scan after implementation, then explicitly
confirmed the concrete source/provider/resource/budget proposal after automatic
approval review had blocked the initial launch. That rejected attempt never
started. The confirmed grant covers one new paid whole run, not another retry
series. The earlier grants remain exhausted.

Implementation: `7fb5ce2e713c5fec240ac286774327779f6a0d04`, clean `oct14/scan`
in the assigned f8dc worktree. Generator and test-copy Agents DLL SHA256 match:
`7D8B5503DD2F6AA6E1079820EE129BDC60F91759435BF00B5159BBA02750D321`.
Generator DLL SHA256:
`D01D8387818CA6F5F24CF43123A5BC642DB79D0450262F68B09A5DBDF2AA1D63`.

The existing launcher ran `json-three-whole-01` against all 77 manifest-verified
files from `G:\AIplayground\vulgata\login_slice`, with both repository roots
explicitly selected. DeepSeek endpoint `https://api.deepseek.com`, model
`deepseek-flash`, low reasoning, max_tokens 393216. CL-only, normal mode with
three corrections, hard mode false, transient retries zero. Original limits:
1800-second run, 300-second file, one Worker, 100 model requests per file,
32 advisory/48 maximum tool calls per file, 1000000 cumulative provider requests,
1000000000000 cumulative tokens. The user explicitly confirmed these inherited
ceilings; no budget was silently enlarged for this run.

Fresh owned root: `phone-113feac396e5` under the approved OctoberRuns directory.
Database: `vulgata_oct_pilot_9a003cd0903a41da949b3d80d56d1e0e`.
Batch: `803e62e8-9b09-4fe5-a2eb-9bea83425a7a`.
Existing container retained; no unrelated database, container, source or process
was changed. Child-only environment settings were used; no persistent PATH change.

## Outcome

Failed after **600.43 seconds**: **15 accepted / 77 selected**, partial coverage,
zero published Documents, publication NotRequested. Matching and Business Logic
both remained not-requested. Exit 1, TimedOut false, NeedsSettlementCheck false.
All 77 prepared-source hashes still match. Usage is known: 24 requests,
234852 input tokens and 135476 output tokens (370328 total).

This is a final-proposal validation failure, not a run/file deadline or token
budget failure. The failing Worker itself finished after 44.857 seconds. No
accepted-file result is represented as a complete Knowledge base, and ordinary
reader qualification was not reached.

## Retry evidence

The new log contains 15 events across two Workers, four scheduled corrections,
and no `json-correction-log-incomplete` warning.

`acct/EleAcctCustInfoCacheOp.java` recovered from a Syntax error at byte 7787
after one correction. Its replacement was 7476 bytes, passed validation and was
accepted. Worker elapsed time was 36.321 seconds; the correction request-to-response
interval was 12.253 seconds. These are application intervals, not pure provider latency.

The failing account file was
`src/main/java/cn/com/yitong/actions/domain/login/LoginNoticeOp.java`:

| Candidate | UTF-8 bytes | Elapsed Worker seconds | Rejection |
| --- | ---: | ---: | --- |
| Initial | 6916 | 18.828 | ClosedFields: missing root `observations` |
| Regeneration 1 | 6860 | 25.704 | Syntax at EOF, line 0 / byte 6860 |
| Regeneration 2 | 6863 | 35.883 | Syntax at EOF, line 0 / byte 6863 |
| Regeneration 3 | 6828 | 44.363 | Syntax at EOF, line 0 / byte 6828 |

All four candidates ended with normalized Stop. Correction intervals were
6.876, 10.179 and 8.480 seconds. The Worker stopped at InvalidOutput/ProposalParse
after exactly three regenerations; no outer replay replenished the allowance.
The final bounded HTTP observation reported Stop and DONE, one unclosed object
and one unclosed array. HTTP text, SDK text and parser input all matched at 6828
bytes; reasoning count was 421. EOF was not observed, so this comparison does not
claim inspection of an unread tail after DONE. No raw candidate text was persisted.

## Retained evidence and stop

Ignored local ledger: `TestResults/replay-paid/json-three-whole-01.json`.
Protected report: `phone-113feac396e5/logs/generate-66ddcf5b3b574cfb8a0028cfb6079fc3.stdout`.
Report SHA256: `E1BFC826897F139C014315909C10B6D41CCD9A8242F868E02D9F54718D55FDA3`.
Protected retry log: `phone-113feac396e5/logs/generator/worker-json-corrections.jsonl`.
Retry log SHA256: `F4D786B82FEB5B0D5D9EBF9DADA671D16BD216FA705B95E5D892B32DB80B1DE3`.

The single newly confirmed whole run is complete and its grant consumed. Retain
the partial accepted data and diagnostics; no automatic further scan, parser
relaxation, local JSON repair or additional paid execution was performed.
