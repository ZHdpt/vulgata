# Stop/JSON request checks and isolated-first retry

The user asked to check outgoing stop sequences and JSON response format, retry
the previously failing file first, and run the whole slice only if it succeeded.
No production request change was necessary. The Worker already uses
ChatResponseFormat.Json and does not supply StopSequences. Controlled actual-SDK
wire assertions now explicitly check absent `stop` and
`response_format.type=json_object` on initial/tool/corrective requests, including
DeepSeek low reasoning and cap393216. Four selected SelfContained cases passed,
zero skips, `isolation-07477a69953c4060999a08a2c1d3b2f2`. Commit `838bdfb5` adds
only these assertions. This establishes controlled serialization, not a retained
raw request capture of historical paid calls. Prior full-suite limitations remain.

## Isolated success

Fresh target `phone-d87325809382`, batch
`651704ad-b461-48c6-bff5-0823443b87df`, database
`vulgata_oct_pilot_43123983b4e24969b831337f99dff8a7` selected only
`mbs_channel/src/main/java/cn/com/yitong/actions/domain/common/SdkAntiFraudHandleOp.java`,
with the same approved supporting source, low reasoning and393216 cap.
Report `generate-053a8743e7304b9a9af759696bf58978.stdout`: exit0 after29.1 seconds,
no timeout/settlement check, empty stderr; execution Completed, coverage Complete,
publication Confirmed, one accepted file and one Document. One request used3448
input/3022 output tokens, all known; no correction was needed.

Retained publication `27fdc0cb-d3a7-4a50-a734-da9aba6f2be2`, logical Document
`b5af5853-0e12-62a4-318b-9155e395a8a9`, version
`5cf9a538-7f51-4c35-b294-fbbdc2c42526`. Ordinary-reader access was not newly tested.

## Conditional whole-slice attempt

Isolated success satisfied the user's explicit condition. Fresh target
`phone-6d3cfbd2f16b`, batch `08733cee-86ea-4fdb-a805-3f8365b42691`, database
`vulgata_oct_pilot_995613cf2ff8456aba9bd75b45b2a85a` selects all77 approved files,
with the same settings/guards. Bootstrap, prepare and select passed. Isolated
successful data and all prior data remain retained; shared container/volume is
unchanged. Whole-run outcome follows separately.

Whole report `generate-730c0df51ce64102aa5726d24abae3fb.stdout`: exit1 after
153.48 seconds, no timeout, NeedsSettlementCheck=false, empty stderr. Four
requests used26554 input/35242 output tokens, all known. Four channel files were
accepted and their operation log entries confirm committed acceptance: pom.xml,
SdkAntiFraudHandleOp.java, BaseAntiResultRequestOp.java and GuestSessionCreateOp.java.
The next file LoginAntiSendOp.java is marked Failed; the terminal gap is
`run_unavailable`, with no Worker diagnostic. The broad host exception boundary
maps exceptions to that code; the report does not expose the underlying cause.
Do not infer another JSON/stop failure or timeout from this result.

Execution Failed, coverage Partial, publication NotRequested, zero returned
Documents. The successful isolated publication is unaffected. All77 source hashes
match in both fresh runs. Both databases and prior successful/accepted data are
retained. No additional attempt ran; ordinary-reader access remains separately
unqualified. Runtime success of the isolated file does not establish full-slice
reliability or prove an internal provider cause for earlier malformed responses.
