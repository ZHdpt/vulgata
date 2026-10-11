# One local-flow BL: implementation and execution boundary

2026-10-11. User approved the narrowed management handoff and separately authorized
Business-only nonstreaming changes in `OctoberWorkerClientFactory.cs`. Assigned
`f081` / `oct14/business` was clean at `fde78bdc`; no reset, merge or push.

## Implemented boundary

- Project complete flow-member sections/claims into an exact packet; include accepted
  relationships, frontiers and gap counts. No whole-corpus prompt, retrieval tools,
  role classifier, resolution-policy change, source execution or alternate publication.
- New proposals can cite only packet members. The existing publication store rebuilds
  the same packet during independent validation. Full dependencies, contributors,
  coverage and gap records remain owner-held and published through the canonical route.
  Historical result lookup continues checking stored request/outcome digests without
  reinterpreting historical proposals under the new member-only rule.
- Finite UTF-8 bounds: owner input 16 MiB; complete gap ledger 8 MiB; validation result
  16 MiB; model packet/messages and proposal 1 MiB each. Existing stored-read 32 MiB
  bound remains unchanged. Oversize evidence is rejected, never silently truncated.
- Synthesis reserves conservative escaped-message UTF-8 bytes plus bounded protocol
  overhead **and** output allowance. This is a reservation estimate, not tokenizer usage.
  Actual usage/settlement remains on each original leaf lease.
- Maximum output ceiling 393216; total synthesis deadline 900 seconds; initial response
  plus at most three structural replacements, no outer transport retry. Strategies:
  configured, configured, thinking disabled, compacted history with thinking disabled.
  Original packet/instructions remain; rejected proposals are not evidence.
- Required output fields cannot silently default omitted ordinal/qualification values.
  Duplicate fields and unknown fields remain invalid. Explicit Mixed/undefined or
  changed qualifications, invalid evidence references, Length, unknown usage, authority
  failure and transport failure do not obtain structural correction retries.
- Static correction instructions are inside the existing hash-bound synthesis prompt;
  follow-up feedback contains only fixed failure classification and remaining count.
  DeepSeek compatibility/reasoning replay is reused on the Business port. CL streaming
  code is unchanged. No schema or migration change.

## Exact single root and refreshed read-only preflight

Target `phone-15bfe12615eb`, batch `1a327657-6a55-4e65-ba06-6992f423d40e`,
CL publication `fff8ddfc-d8ae-480c-aadd-286f03c8f4d9`, corpus
`0ea50f38-2441-4ec0-b31c-313acd0c5f55`, database
`vulgata_oct_pilot_7e073703d96e4368a869ccace9bc827d`.

Selected repository `9172b209-ed3a-415c-8d8b-c0ef1b2f18f6`, relative path
`src/main/resources/flow/register/selfRegist.flw`, selector `selfRegistFlow`,
unit `7b983152-5326-a834-eec1-9634c4384886`, logical document
`c162bbec-92aa-ea8c-3f32-e119253f7fe0`, exact CL version
`38884f7e-d58b-436f-9a03-cb3fa769e3af`. Kind `SelectedCodeUnit`.

Authenticated owner API reads and canonical owner metadata, using PostgreSQL
read-only connections and a deny-write guard, confirmed:

- 163 CL versions / 77 accepted files, exact frozen-corpus membership.
- Zero Business Begin/Complete receipts at observation time.
- Unchanged policy yields zero inquiries, 355 NotEvaluated dispositions. Do not
  reinterpret zero inquiries as resolution completeness; refuse a changed prerequisite.
- One planned group, 29 supplied claims, packet **25717 bytes**.
- Complete owner input **2419063 bytes**, full gap ledger **547626 bytes**.
- Final prompt input reservation **47102**, output ceiling **393216**, initial
  attempt reservation **440318**. Correction history is measured again per attempt.
- Original CL content digest remains
  `7d905781b3d6aa6a46bc83fa6a6d7e3969226fb0e46c22391130e994c4fa47f7`.

Local preflight evidence `TestResults/business-readiness/minimal-verified.json`,
SHA-256 `6B67F1175721B7B80B0428C87283BE0D0E66268363734E89838E52EEA04D3004`.
This is not BL generation or ordinary non-management reader qualification.

## Review and tests

Independent focused source review accepted after fixing two findings: explicit
qualification failures being retryable, and correction instructions missing from
the prompt identity. Required-field checks were also reviewed. No larger redesign
was introduced.

Behavioral RED: outside-packet acceptance `40961eb0b0be4ce5a195b290c1b0f6ed`;
large owner input `74a7c4a2400d421ab4d1c5f8e2e45166`; four qualification/presence
regressions `9036c0cee0024349a29a553d13aba6b6`. Earlier invalid test-fixture
fingerprints, sandbox build failures and interrupted frozen-clock runs are not RED.
Initial controlled 74/74 passed at `f017c0a077f54b4aace299f26db5b447` before review
fixes. Broader tests exposed old fixture-only 100-token and transport-retry/deadline
assumptions; only synthesis fixtures were adjusted, preserving existing budget-test
defaults and resolution branches.

Final controlled qualification: **373/373 passed, zero failures/skips**, including
18 new correction/wire cases, finalizer, canonical publication and immutable replay,
existing-CL continuation, execution scope and transport. Evidence:
`TestResults/isolation-bcaef8c6e60143ecb0cedc3c299181aa/results.trx`.
Run through `eng/test.ps1` in the SelfContained inventory. This does not establish
real-provider success, retained-database BL publication or ordinary-reader qualification.
The unrelated historical qualification-report parser mismatch remains separate.

## Single execution proposal — subsequently authorized and consumed

Use the existing `cl-and-bl` continuation with the exact existing publication and
single root above. Selected profile `selected`, model `deepseek-flash`, exact
DeepSeek endpoint, configured low reasoning. No CL generation or new resolution
inquiries may be introduced to fit this milestone.

One invocation, freshly generated RunId and original capability; worker cap 1;
**maximum 4 physical provider calls**, **6000000 cumulative tokens**, output ceiling
393216 per call, synthesis deadline 900 seconds, overall deadline **1200 seconds**,
graceful drain 30 seconds and owned-process watchdog 1260 seconds. Input reservations
cannot exceed 1 MiB per call, so four maximum reservations total 5767168 tokens.
No second whole invocation is included. Unknown outcomes require reporting, not retry.

Fresh ignored local policy/settings proposals have hashes:
`DF08091D6A887BF9A2BF46A198A45641C5E7D6B02318DE61E1D359B738080B2F` and
`2B9DA4220E6470422914109833F7F3017752777D23AF5A99FCB23B5A383EB6B6`.
Credentials/runtime paths remain outside tracked configuration. Original consumed
launch markers stay untouched; a new invocation requires its own explicit grant.

Required consolidated authority: disclose this accepted root packet to the selected
provider within these limits; run one owned headless generator process and protected
logs; append canonical Business admission/finalization/publication/completion records
only in this exact retained database; create and remove one dedicated User-only
verification account through Identity APIs if no existing ordinary-reader credential
is supplied. No management role, migration, Prepare, source scan, host startup,
unrelated-process operation or manual accepted-Knowledge seed is part of the proposal.

After a successful settled invocation, verify one separate BL publication and its
full dependencies through owner APIs; authenticate the ordinary non-management User
and read all BL pages; compare original CL membership/content digest and reopen its
exact publication. Review useful conditions, branches, effects and limitations
against the accepted root, without supplying evaluation answers to generation.
Report **“Local-flow BL published; cross-repository behavior remains unconfirmed.”**
Browser/Chat/graph qualification remains separate. Stop after this first success.

## Authorized execution and qualified result

User explicitly replied **“authorize one execution”** to the complete proposal above.
Executed the reviewed implementation `616cae1f9b1995ff886e91a529e7a6c4dfc02b10`
once on 2026-10-11, 20:12:37–20:13:26 Asia/Shanghai. The fresh local marker is
consumed. No second generation, structural replacement, migration, source execution,
host startup or unmatched-receipt clearing occurred.

- RunId / Begin and Complete operation: `19114288-0afa-4a2d-a1be-7080de879f64`.
- Finalization operation: `f2e65272-5bce-4e98-bcc2-28d161b76bed`.
- Separate publication: `5a9d50ef-d738-4fec-b04f-bb58c9f61562`.
- BL logical document: `50a6b29d-57bc-92d9-f778-5fcf58b43d0c`.
- BL version: `ce649bf4-a4cd-0469-b6f5-70e8402e80f5`.
- Title: **手机银行自助注册流程业务说明**.
- One provider request; known input 12272 / output 11126 / total **23398 tokens**.
  Model wait 36.918 seconds; owned process duration 48.96 seconds. No watchdog
  timeout, unknown usage or outstanding process. Both Business receipts committed.

The canonical finalizer committed and exact lookup returned Published. The command
nonetheless exited 1 with `business_publication_read_unavailable` at ExactReader:
its catalog request used 100 documents and exceeded the existing 64 KiB response
budget. Read-only reproduction returned ReadBudgetExceeded for 100 and Success for
1. This is a **post-publication command verification defect**, not an indeterminate
publication and not permission to regenerate. The original report is preserved;
no parser helper was used to turn it into a successful command report. A later
bounded correction should use bounded catalog pages without weakening budgets or
the exact-publication check; it is not implemented after this milestone stop.

Independent post-run ordinary-reader qualification used real Identity password
authentication and a newly created account with exactly the existing User role.
Canonical Business search returned only the BL; its full content read succeeded
in one page. An exact CL handle was denied without content or metadata. The owned
account was deleted through UserManager and absence verified. This is API/Identity
qualification, not browser/UI qualification.

Content review compared all 12 generated claims with the selected root's 29 accepted
CL claims and exact source anchors: registration-type alternatives; SMS presence
and success/failure conditions; transaction/write sequence; the counterintuitive
needLoginLog 1/0 branches; post-commit channel/SMS/synchronization declarations;
and the no-password branch. All generated evidence references resolve to that exact
root and known claim IDs; none promotes advisory context to source-supported facts.
This verifies synthesis against accepted CL evidence, not fresh source execution or
deployed side effects. The SMS presence wording follows the CL description; the exact
underlying condition is smsSerialNo presence, not independently validated OTP presence.

Complete conservative lineage remains: 163 exact document dependencies, one selected
member, zero accepted relationship dependencies, 240 file-uncertainty dependencies;
850 inherited gap explanations/limitations remain owner-held in the synthetic gap
claim. Business displays the concise uncertainty projection. Stored finalization is
Published and its coverage IsPartial flag is false for completion of the selected
root; that flag **does not establish cross-repository completeness**. Resolution
still has 355 NotEvaluated dispositions. No supported cross-repository link is claimed.

Original CL publication still has 163 versions, unchanged content digest recorded
above, and its selected root was reopened and read successfully through its exact
original publication. New publication membership contains those exact 163 CL versions
plus one BL; Business mode exposes only BL. No original publication was altered.

Local evidence (ignored, no credentials):

- `TestResults/minimal-bl-run/stdout.log`, SHA-256
  `B9DA523228BE90EB9DB2CE8E022DC45F07E28403085A540E206066B6054BF39A`.
- `TestResults/business-postrun/inspection.json`, SHA-256
  `6A82781F7513B44D76CB16A32B16332DA8C38426FF00E884CC830F73CB05100E`.
- `TestResults/business-postrun/ordinary-reader-content.txt`, SHA-256
  `8C9762BAC055979FBA95F5C7FB1FD0C0B1FF1810D54401196686902B5610BA62`.
- `TestResults/business-postrun/settlement.json`, SHA-256
  `DAFBC9E9C46FAC9B3E14F72B0DFA135641ED0452D782675D193F03F11E06F0E9`.

**Local-flow BL published and ordinary-reader verified; cross-repository behavior
remains unconfirmed.** Stop reached before the four-hour checkpoint. No merge/push.

## Focused post-publication catalog correction

The user subsequently authorized fixing the response-limit defect, focused tests,
independent review and verification against the existing publication, explicitly
without regeneration or another provider call. Starting checkout `04bdda0b` was clean.

The Business continuation now halves its catalog page size only on ReadBudgetExceeded,
down to one. Because cursors bind the requested page size, each reduction restarts the
same immutable selection and discards the incomplete accumulated catalog. The existing
256 total-call ceiling is retained across restarts; exact publication identities,
duplicate/nonprogress checks, live authorization, original operation lifetimes,
deadline/cancellation and the 4096-document ceiling remain intact. Other failures and
an oversized single item fail closed. No response limit, schema, publication or
provider behavior changes.

The existing loop is extracted into one internal Business paging method so the
read-only qualification can invoke the actual production algorithm without starting
Generate or acquiring provider/commit capability. The command still supplies its
original retained-call wrapper and run-availability checks.

Focused RED: `TestResults/isolation-63543ce94cd540878be5b13d7aa69491/results.trx`,
four selected / one passed / three failed on the original implementation: first-page
overflow, later-page restart and minimum-page bounded failure. The non-budget failure
already stopped correctly.

Focused GREEN: **67/67 passed, zero failures/skips**, including five new paging cases
and all 62 existing Business-finalization/lifecycle cases, through `eng/test.ps1` in
SelfContained. Evidence: `TestResults/isolation-df5d7ee8353643569972802744a22609/results.trx`.
The additional case confirms the total 256 calls do not reset after size reduction.
Independent focused source review accepted with no actionable findings.

Real read-only verification at 2026-10-11 20:32 Asia/Shanghai invoked this exact
production paging method through the authenticated owner reader on publication
`5a9d50ef-d738-4fec-b04f-bb58c9f61562`. Size 100 and 50 returned ReadBudgetExceeded;
size 25 returned seven pages (six of 25 and one of 14). All **164 distinct exact
handles** matched the immutable publication: original 163 CL versions plus the BL.
Original CL digest and full current publication content digest were unchanged;
Knowledge operation count remained 83. Connections enforced PostgreSQL read-only;
there was no Generate invocation, provider call, database write or account creation.

Evidence: `TestResults/business-catalog-verification/result.json`, SHA-256
`2DBCB3021C2832C871AC1E0FE3B1993D72448553C8AAEA75B4909D930EF4BBD7`.
The earlier command's historical exit-1 report remains truthful and unchanged.
This correction qualifies its catalog phase against the already published result;
it does not claim a second complete Generate run or new cross-repository qualification.

## Additional authorized end-to-end execution — successful

The user requested a successful BL run with generated data and then explicitly
replied **“Authorize this one additional run”** to the same four-call / six-million-token
proposal, including canonical publication writes and one temporary ordinary-reader
account. The corrected checkpoint `341243ffdee4dbfe110bbcc9637a121437b8a8b9`
was clean on `oct14/business`. Preflight confirmed the same 163-version CL corpus,
single root and unchanged settings, with the prior Begin/Complete pair both present.
A fresh separate local launch marker was consumed; prior markers remained untouched.

Executed 2026-10-11 **20:45:40–20:47:18 Asia/Shanghai**. The command returned
**exit 0**, requested/completed stage `cl-and-bl`, execution Completed, publication
Confirmed, Business diagnostics Completed with no operation mismatch. Catalog
verification returned 164 exact handles. No timeout or unknown settlement occurred.

- RunId / Begin and Complete operation: `8b970cea-0c5f-4724-80d9-f1776f84dea4`.
- Finalization operation: `8e5079bd-5b8e-4a08-a83f-1b1fd7d1fecb`.
- New publication: `464743bc-93f3-4254-bda0-ec992a5ae7b8`.
- Same logical BL document: `50a6b29d-57bc-92d9-f778-5fcf58b43d0c`.
- New immutable BL version: `37c2b676-d9aa-5509-a4e7-d867b8161947`.
- Title: **手机银行自助注册流程业务说明（局部）**.
- One provider call, input **12272**, output **11301**, total **23573 tokens**;
  all usage known, no structural replacement or provider retry. Process 98.53 seconds.

Both new admission/completion receipts are committed. Original CL membership/content
digest and original root readability remain unchanged. The first BL publication and
its document content also remain unchanged. The new version reuses the canonical
flow group and complete lineage: 163 document dependencies, one member, zero accepted
relationship dependencies and 240 file-uncertainty dependencies. Evidence queries
follow the new document's group reference, since the group's creation operation
correctly remains the first finalization operation.

Real Identity password authentication of a new User-only account succeeded.
Canonical Business search/read returned the complete new BL in one page and denied
CL exact reads without metadata. The account was deleted and absence verified.
Nine generated claims map to known claims of the exact selected CL version, with no
foreign or advisory evidence references. The document describes registration-type
branches, SMS verification outcomes, writes/transaction sequence, optional login
logging and post-commit declarations, plus explicit uncertainty. As before, SMS
presence prose follows CL descriptions of smsSerialNo; deployed behavior and other
services' implementation are not independently established by this local-flow result.

The report's Complete/matching-completed values describe completion under the current
bounded policy and selected-root scope. They do not establish cross-repository
completeness: zero accepted cross-repository links and 355 NotEvaluated dispositions
remain. No source rescan, migration, host startup, manual Knowledge seeding, CL
mutation, merge or push occurred. This additional grant is consumed; stop reached.

Local evidence:

- `TestResults/minimal-bl-run-2/stdout.log`, SHA-256
  `22A72BE84DD0090B0EEBC5E3496DBB03B0B08BA6AE9512B04EA8ED7F8BBEB3EB`.
- `TestResults/business-postrun-2/inspection.json`, SHA-256
  `BEF862E15156CAFF6810D254F83CF66F6CF0921C53E440573A32604B73B9B953`.
- `TestResults/business-postrun-2/ordinary-reader-content.txt`, SHA-256
  `FB4BEBFCE26AF4EAC2CCDABF4E88F236DFBD8E13BC3550BC8505CC6CAF0DC8CB`.
- `TestResults/business-postrun-2/settlement.json`, SHA-256
  `BAD14E1CF888F41FBBDE98C7D06CDBCAAF9B97A074B9F15A5F4380F6E9AC8431`.
