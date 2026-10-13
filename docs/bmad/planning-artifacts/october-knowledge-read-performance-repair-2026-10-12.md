# Knowledge-read latency repair — 2026-10-12

## Scope and authority

Workspace: `G:\source\repos\vulgata`, `oct14/main`, base commit `4457b38d`.
The user requested a direct repair without skills or broad planning and explicitly
authorized ordinary-user reads, sanitized timing/query diagnostics, and restart of
only the verified port-15075 Web host using its existing configuration. Startup
migration checks/role seeding were included; schema, permissions, publications,
provider execution and retained-data changes were excluded. The user confirmed
OCT-BUSINESS could be editing data concurrently but had no overlapping code edits.
Its process and work were left alone. Both pre-existing untracked planning files
were preserved.

The pinned target/publication/batch/digest are those in
[the handoff](october-knowledge-read-performance-handoff-2026-10-11.md).
Read-only SQL verified 163 membership rows and membership-version MD5
`ae618561a9d0b2e0ef787680df19781e` before and after qualification. This is an
additional comparison fingerprint, not a replacement for canonical selection or
content digest validation. The account used its existing ordinary `User` role.
The original publication has zero accepted relationship rows. No provider calls,
rescans, permission changes, new containers or data deletion occurred.

## Reproduction and diagnosis

No listener existed on port 15075 at the start of this repair. An owned process
was started with the same retained October IT publication, stable key ring and
Chat dispatch disabled. This avoids the prior report's unknown-listener problem.
Temporary `ActivitySource` probes measured the actual production read stages and
EF `CommandExecuted` events under each operation. They recorded only correlation
IDs, stage names, elapsed times and counts; no SQL parameters or Document bodies.
All probe code and activation hooks were removed from the final source.

The bottleneck was repeated live authorization, not full publication loading:

| Representative server operation | Before ms | After ms | Before SQL count | After SQL count | Before policy calls | After policy calls |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Browse | 4,990.4 | 431.5 | 6,994 | 193 | 498 | 12 |
| Exact Document | 5,533.9 | 296.2 | 6,994 | 190 | 498 | 12 |
| Graph | 10,012.2, timeout | 1,221.8, success | 12,691 | 1,118 | 903 | 74 |

These are representative individual traces, not percentiles. SQL counts are
observed completed EF commands inside the operation. Stage durations are nested
and must not be added together. The baseline graph root returned at its deadline;
a pending canceled child may finish unwinding afterward, so its root snapshot is
not a complete canceled-command count.

Before the fix, the representative list trace spent 4,808.1 ms in policy checks,
dominated by repeated selection/Identity work; full publication loading was
only 157.2 ms across two loads. The exact-Document trace spent 5,394.4 ms in policy
checks and 119.8 ms loading publication material. The graph spent about 9,694.7 ms
in policy calls and 208.0 ms loading three publications. Search and the first
related materialization completed; the first related operation's final recheck
was still running at cancellation. This reproduction does not support the earlier
inference that the deadline expired during a third related read.

Database commands themselves were predominantly small repeated queries. Total
measured DB execution in those baseline operations was 3,534.4 / 3,934.8 / 6,983.9
ms respectively. No query-plan/index or frontend-only cause was established.
Projection/validation/hashing remain inside the measured publication/read stages;
the probes did not separately isolate each CPU substage. Browser navigation also
includes transport, SSR/interactive rendering and tool observation overhead and
is not interchangeable with these server traces. Reloads can trigger separate
prerender and interactive read operations. The fix does not alter that lifecycle.

## Small repair and review

Only `OctoberKnowledgeReader.cs` changes production behavior:

- Search checks the union of all contributors per Document kind once per phase.
  A denied union falls back to individual checks so permitted members remain
  visible. Authority-unavailable and cancellation still fail closed.
- Exact reads authorize only the requested immutable version. Related reads
  authorize the requested Document and endpoints of its adjacent relationships,
  while retaining relationship/uncertainty and coverage contributor checks.
- Final checks still occur after materialization, before logging, and after
  logging. They use the existing owner dependency codec's contributor grouping.
  Grouping retains every contributor and is already the capsule authorization
  representation; it is not cached permission.

No public interface, schema, publication loader, graph deadline, graph acquisition
limit, output budget, provider binding or concurrency behavior changed. No cache
was introduced. Publication selection, stored-data budgets, hashing and exact
version validation remain intact. The graph still performs final aggregate
reauthorization. The patch was checked against mixed denial, mode filtering,
uncited contributors, final revocation, continuation and cancellation cases.
This was a direct source/test review, not an independent review-agent gate.

## Controlled evidence

`eng/test.ps1` was used for each run:

- RED: `isolation-8b6f654ede064bbabe9195152d36e039`, 4 selected, 1 passed,
  3 failed. Each large-publication search/exact/related case observed 492 policy
  calls against a maximum of 10. The mixed-denial/revocation control passed.
- Focused GREEN: `isolation-9cff0f2495614960835748275f002194`, 124/124 passed,
  zero failures/skips. Includes Knowledge disclosure, graph authorization,
  exact-version and continuation checks.
- Final-source broader regression: `isolation-7ff6856849de4d878af856393fc1d14b`, 1,387/1,387 passed, zero failures/skips. This broader selection includes shared Knowledge, Explorer, and October Chat; build completed with existing analyzer warnings and zero errors.

Tests do not mutate the retained database. No live permission-revocation mutation
was performed; revocation/denial coverage is controlled, not a new customer-data
revocation qualification. No full SelfContained-suite GREEN is claimed.

## Browser evidence

The user agreed to warm visible-load targets of list <=3 s, exact Document <=2 s,
and successful graph completion within its unchanged 10-second server budget.
Measurements used one ordinary authenticated browser and the same exact version:
logical `006e858e-2b01-9e90-979e-f18b4f1b1536`, version
`4d071ec1-95de-4027-bbf0-b93f4b78e538`.

| Surface | Before warm loads, ms | After warm loads, ms |
| --- | --- | --- |
| Document list | 14,070 / 9,506 / 8,450 | 1,094 / 576 / 569 |
| Exact Document | 4,911 / 11,941 / 10,367 | 423 / 420 / 398 |
| Graph navigation | 10,081, timeout message | 1,367, loaded |

First measured exact-Document loads were 10,973 ms before and 469 ms after. The
first list load after restarting the after-build host was 8,260 ms; only its warm
loads met the agreed target. No valid end-to-end first-list baseline was captured
in this run; an initial navigation observation ended at Loading and was excluded.
These are not cold PostgreSQL/cache measurements and do not establish removal of
first-process initialization cost.

The live graph rendered 20 Documents, zero edges, IT dark theme and an explicit
incomplete-range notice. Zero edges match the pinned publication; none were
invented. Selecting a node produced its exact batch/publication/logical/version
link, and that link opened successfully in 871 ms. Controlled tests separately
exercise graphs with edges, denied contributors, partial and empty conditions.

Final uninstrumented browser verification also passed: list warm loads
587 / 508 / 573 ms; exact Document warm loads 404 / 394 / 481 ms; graph
1,428 ms. First list load in this final process was 4,458 ms and first Document
load was 807 ms. The graph retained 20 nodes, zero edges, the incomplete notice,
matching canvas/list accessible labels and no visible Blazor error banner.

## Retained state and limits

The final uninstrumented Web host remains running on loopback port 15075 with the same IT publication and Chat dispatch disabled. Original CL membership is unchanged; concurrent Business data
work was neither stopped nor adopted as this read target. This repair does not
qualify IT Chat/provider execution or Business-mode knowledge generation.
Sanitized raw metrics are retained in ignored `.tmp/knowledge-browser-metrics.json`,
`.tmp/knowledge-baseline-stages.json` and `.tmp/knowledge-after-stages.json`.
Temporary probe helper sources are disabled under `.tmp`, outside compilation.
## Final build identity

The repair is uncommitted on base `4457b38d`. Final-source SHA-256:

- `OctoberKnowledgeReader.cs`: `16A8B7191BA445D9CD0DCDF104CFE3B8FDA65A0CADECEAD4CC996F199C97A043`.
- `Vulgata.Web.dll`: `D5411317E655A3572328A80D3BF64FDA978B7702887EF68B262B1C2F36A8864E`.
- `Vulgata.Infrastructure.dll`: `3BC885997D0CEFE894F3B5CEC73572400A55FC3369E3050A67CA8800604566A7`.

Final browser counts/timings are in ignored `.tmp/knowledge-browser-final.json`;
the graph-only viewport proof is `.tmp/knowledge-graph-final.png`. No Document
body is included in the metrics or graph viewport proof.