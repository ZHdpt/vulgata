# Project state for agent resumption

Test infrastructure (2026-09-29): the standard command is now
`pwsh -NoProfile -File eng/test.ps1`, which builds current sources and selects the
classified SelfContained suite. Direct CLI/IDE execution has the same safe default
and external fixture guards. PostgreSQL qualification uses a new owned PostgreSQL 17
container; tool/provider/E2E and historical suites remain explicit and retain their
feature-specific gates. See [testing guidance](testing.md). This changes the routine
regression boundary, not historical product evidence or story lifecycle state.

Updated: 2026-10-09 (real synthetic CL-and-BL Generate-to-ordinary-Business-reader passed; retained presentation operation remains separately pending).

Newest qualification supersedes older BL-pending statements below for this exact
synthetic scenario only: attempt 5 on `ea611586` passed **1/1**, run
`11c3fce168954d7f8c0c8e62b29fef38`. One accepted AccountPolicy.java produced a
confirmed CL-and-BL publication. Real ordinary User Identity login, canonical
Business reader paging/dependencies/denials and same-session security-stamp
revocation passed. This is not interactive BL UI, Chat, graph or two-repository
qualification. All disposable resources were cleaned; no presentation DB retained.
Five-attempt series complete: nine requests/121,726 tokens, all usage known, grant
exhausted. Do not launch a sixth run. Production-source operation C needs separate
authority. Full-suite GREEN is still unconfirmed; the prior 4,188/4,189 result is
not replaced by focused or E2E passing checks.

Newer user authority (October 9): selected real login/registration source may use
the approved DeepSeek endpoint/model and protected credential; normal LLM use may
consume the available balance, with finite execution limits and no blind retry.
Preparation is delegated to the MBS sample chat. Keep actual prepared-root paths
out of tracked code/config; use CLI or ignored local configuration. Successful
presentation data is retained until explicit deletion; later runs use fresh owned
databases. Failed incomplete owned runs may be cleaned after settlement/diagnostics.
The assigned Scan lane now has 77 manifest-verified prepared files and retained
runtime bindings. Its real single-Java-file CL generation/publication passed after
the reasoning replay correction; ordinary-reader access was not requalified in
that run. The full slice then stopped at the channel POM, whose isolated trial
confirmed Length at a reduced 4,011-token output cap. The user-approved fixed-output
admission is reviewed/committed at `e5c53e00`; 423 affected checks passed. Its real
POM trial hit the unchanged 8192 streamed-update guard before terminal usage, with
one request, unknown token usage and no accepted/published CL; no full-slice retry
ran. The subsequent bounded stream-aggregation correction removes the unnecessary
update list, coalesces plain reasoning and retains finite non-progress/segment
bounds plus explicit synchronous cancellation checks. Its affected checks pass
434/434; full SelfContained remains non-green (4741/4743). The correction is
committed at `c17b5cae`. Its fresh real POM trial reached Length with known usage:
one request, 9748 input tokens and 8192 output tokens, all output reported as
reasoning. No local stream-budget failure, accepted CL or publication; no full-slice
retry ran. Current implementation and verification are in
[stream aggregation evidence](../../specs/20-1-Oct14-scan/stream-aggregation-verification.md)
and [output reservation evidence](../../specs/20-1-Oct14-scan/output-reservation-verification.md);
the user subsequently authorized an explicit low-reasoning POM experiment while
keeping the 8192 cap. Its optional command-local DeepSeek Worker setting is reviewed;
all 19 new/453 affected cases pass, full SelfContained is 4761/4762 with the same
report-parser failure. Committed at `b3192899`, the real low-effort trial also ended
with Length: one request, 9748 input/8192 output tokens, all output reported as
reasoning; no accepted CL/publication and no whole-slice retry. See
[low reasoning evidence](../../specs/20-1-Oct14-scan/low-reasoning-verification.md).
The subsequent user-requested 393216 output-cap experiment is opt-in through
`October:WorkerMaxOutputTokens`, retaining the 8192 default and other guards.
All 20 cap/replay and 434 affected-class checks pass; full SelfContained is
4773/4775 (report parser and CodeGraph cancellation failures). See
[maximum-cap evidence](../../specs/20-1-Oct14-scan/max-output-cap-verification.md)
for the separately recorded real POM retry on `7c5e82fc`: one provider request,
9748 input/1816 output tokens, followed by local BudgetExceeded/Admission. No
Length diagnostic this time, but no accepted CL/publication or whole-slice retry.
The report does not identify the exact admission limit; existing guards remain.
The user's subsequent explicit whole-slice attempt selected all 77 files but
stopped on `mbs_account/pom.xml` before any provider call with the same
BudgetExceeded/Admission classification. Zero accepted files/Documents; no
publication. The maximum-cap evidence above retains this distinct run and its
fresh owned resources. No additional retry ran.
The user subsequently authorized correcting admission and another whole scan.
Generate now uses the existing 1048576 per-request Worker ceiling, capped by run
policy, instead of 32768 input bytes plus output. Actual leaf reservations and
other finite guards remain; specific budget categories distinguish local failures.
All 436 affected tests pass; full-suite GREEN remains unconfirmed. See
[input admission evidence](../../specs/20-1-Oct14-scan/input-admission-verification.md)
for diagnosis, review, test failures and the separately recorded live attempt.
On `d0f3b691`, the post-fix whole scan passed admission and accepted the account
POM plus EleAcctCustInfoCacheOp.java. It stopped on GetMobileByAccessCodeOp.java
after 109.05 seconds: InvalidOutput/ProposalParse, incomplete JSON despite HTTP
and SDK Stop; bounded observation confirms identical HTTP/SDK/parser text.
Three requests, 21276 input/26340 output tokens, all usage known. Two accepted
files, zero returned Documents, publication not requested. No retry; owned data
retained. This does not qualify complete generation/publication or reader access.
The user then authorized a documentation-guided correction and whole retry.
Generate permits one same-session corrective turn for syntactically invalid final
JSON after Stop, preserving full reasoning replay and all admission/acceptance
guards. No local JSON repair or schema relaxation. All 447 affected cases pass;
full-suite GREEN remains unconfirmed. See
[JSON correction evidence](../../specs/20-1-Oct14-scan/json-correction-verification.md)
for official API sources, independent review and separate real qualification.
Real retry on `0b16d4c8` accepted channel pom.xml, then failed on
SdkAntiFraudHandleOp.java after the correction allowance: malformed JSON despite
Stop, identical HTTP/SDK/parser text. 76.84 seconds, three requests, 21012 input
and 16675 output tokens, all known. One accepted file, no publication/Documents;
resources retained, no additional identical paid attempt. The provider output
problem remains unresolved; bounded correction is not a guarantee of valid output.
The user next requested explicit stop/JSON checks and an isolated-first retry.
Four actual-SDK controlled wire cases pass: stop absent, json_object present on
initial/tool/correction requests. No production request change was needed.
The isolated SdkAntiFraudHandleOp.java succeeded in29.1 seconds with one confirmed
CL publication. Its conditionally authorized whole scan accepted four channel
files, then stopped on LoginAntiSendOp.java with host run_unavailable after153.48
seconds; no Worker diagnostic identifies the exception. Four requests, all usage
known, no whole-run publication. Both fresh databases and isolated publication
are retained. See [stop/JSON evidence](../../specs/20-1-Oct14-scan/stop-json-wire-verification.md).
The user authorized fixing this host failure and isolated-first qualification.
A matching CodeGraph lifecycle defect is corrected: next-file preparation now
renews an expired native session once, only after confirmed old-session cleanup
and within the existing runtime lifetime/replacement cap. Original exception was
not captured; historical causality is inferred from the deterministic path/timing.
All100 affected tests pass; SelfContained4791/4792 retains the report-parser failure.
See [session renewal evidence](../../specs/20-1-Oct14-scan/session-renewal-verification.md).
Fix committed at b164c7d2. Isolated LoginAntiSendOp.java completed/published in
33.6 seconds. The conditional whole scan passed the prior host boundary and
accepted seven account files over354.45 seconds, then failed GuestSyncOp.java
on valid JSON with ClosedFields/Units schema rejection. Ten requests, usage known;
no whole-run publication. All data retained; no additional retry or parser waiver.
The user accepted structural correction and isolated-first qualification. The
existing single allowance now covers syntax and parser-defined structural errors,
with safe missing/unexpected/duplicate-field feedback and unit indices. Evidence
and semantic failures remain non-correctable; all acceptance guards stay strict.
All463 affected cases pass; prior full-suite failure remains visible. See
[structural correction evidence](../../specs/20-1-Oct14-scan/structural-correction-verification.md)
for review, limits, diagnostics and separate runtime outcomes.
Committed at5bcdc246. Isolated GuestSyncOp.java succeeded/published in52.23 seconds.
The conditional whole run then stopped on account pom.xml at98.88 seconds with
the unchanged16-tool-call limit (BudgetFailure=ToolCalls), not a parse/schema
failure. Three requests,83947 input/20898 output tokens, all known; no whole-run
acceptance/publication. All data retained, no extra attempt or budget widening.

The user subsequently approved two-level per-primary-file tool budgets: advisory32
and hard48, explicit Scan policy settings and fresh model-visible counters before
each request. Same-file attempts/corrections retain the counter; only a new primary
file resets it. Other guards remain unchanged. Independent review accepted the
implementation, committed at `c82adf22`. All1052 affected SelfContained checks pass;
full-suite GREEN remains unconfirmed. The isolated account POM completed in74.82
seconds with five CL Documents and confirmed publication. The conditional whole
scan accepted seven files, then stopped on GuestSyncOp.java after431.11 seconds:
Syntax/Root JSON error, MismatchedDelimiter before EOF, identical HTTP/SDK/parser
text despite Stop. No tool-budget or deadline failure;17 requests, usage known,
no whole-run publication. Data retained, no further paid retry. See
[file tool budget evidence](../../specs/20-1-Oct14-scan/file-tool-budget-verification.md).
The user's subsequent isolated GuestSyncOp.java retry succeeded unchanged in22.73
seconds, one request, one accepted file and confirmed CL Document publication.
Its prior malformed JSON did not reproduce; this does not qualify the whole scan.
No subsequent whole run started; the exact retained version is in that evidence.

October11 Scan status: the tool-disabled single JSON correction and safe numeric
syntax feedback are reviewed and implemented. The initial whole attempt plus10
restarts produced no complete publication. Most failed malformed final JSON;
restart10 hit the retained cumulative input-byte limit. Isolated GuestSync,
QueryAllCreditCardOp and channel userAuth trials published successfully. Exact
results are in [tool-disabled correction evidence](../../specs/20-1-Oct14-scan/tool-disabled-correction-verification.md).

Real runs exposed and prompted correction of approval expiry and descriptor
ownership. Current Scan replaces still-current approval-bound runtimes only at
settled file boundaries, retains one original run-owned CodeGraph descriptor per
repository and the cumulative native-session counter, and caps file work by
genuine remaining authority. It does not reinitialize, delete/adopt retained state,
extend issuer policy or change shared schemas. Independent review accepted;
full SelfContained4838/4839 with sole existing report-parser failure. Controlled
two-approval contained reads pass; second-series trial5 continued accepting files
after genuine renewal in a real run. That run still failed final JSON validation.
See [renewal evidence](../../specs/20-1-Oct14-scan/source-approval-renewal-verification.md).

The user's conditional next experiment is implemented and independently reviewed:
default-false UseTextForJsonCorrection changes only the existing
eligible correction's response format, preserving strict parsing and every budget.
All31correction cases pass; full SelfContained4843/4844, sole existing report-parser
failure. Isolated POM passed with1accepted file/6published Documents on one request
(no correction exercised). All ten second-series whole trials failed final JSON
validation with the flag enabled; none published. The user's conditional bounded
hard-retry implementation and single qualification run are now authorized. See
[text correction evidence](../../specs/20-1-Oct14-scan/text-correction-verification.md).

earlier [replay evidence](../../specs/20-1-Oct14-scan/reasoning-replay-verification.md)
retains the exact real outcomes. Neither proves completion of the remaining slice.
Retained pilot resource amendment: reuse the existing PostgreSQL 17 container and
volume, creating only a new isolated database; do not stop/delete shared resources.
The user removed cumulative request/token caps for this run; current finite fields
will use nonbinding values while deadlines and physical per-call limits remain.
An elapsed/deadline console watchdog is implemented and controlled-tested; kill only
the owned Generator tree at the 30-minute boundary, then assess settlement/remaining
time. See the retained-pilot plan for exact bounds and source scope.
Retained pilot bootstrap now exists in new database
`vulgata_oct_pilot_mv0r9q4b_e19qg1` within the unchanged existing container.
Production registration/setup and Generator prepare/select passed. The Generate
launch was blocked by execution auto-review BEFORE dispatch because supporting
rules could disclose the remaining 68 files beyond nine generated files. Seek
explicit supporting-source confirmation was subsequently received: the user
confirmed relevant content from all 77 prepared files may be sent to DeepSeek,
while only nine files generate Documents and evaluation answers stay excluded.
Temporary Web stopped; protected local run `phone-mv0r8ity` and database/keys retained.

Latest qualification: corrected B E2E **1/1** passed on `e3804063`, run
`ba60d34f60a54c40b14b46b5b00ef629`. One real Worker request produced one accepted
CL Document/confirmed publication; actual ordinary Identity HTTP/SSR reading,
Business-mode CL denial and revocation checks passed. Reported usage was
2,095 input/1,888 output tokens, unknown=false. All fixture resources were cleaned.
This supersedes earlier B-failure/pending statements below for this exact synthetic
scenario only. Its older boundary did not qualify BL; the newer result above does
for the synthetic scenario. Two-Repository resolution, interactive streaming Chat,
graph and retained presentation operation C remain unqualified/unapproved as
applicable; no production-source disclosure is implied.

Active next increment (October 9, explicitly approved): sequential multi-file
processing (WorkerCap 1), bounded same-original-run operator selection of actual
accepted BL roots, and an additive BL ordinary-reader qualification path. Scan
owns CLI/runner, Business canonical contracts/schema, Explorer the new BL E2E,
Integration the shared fixture/host/resources. No broad scheduler/recovery restart
or automatic root discovery. See the activation record for the exact scope;
implementation approval does not supply missing live-spend/retained-host limits.

Sequential/operator Scan is now integrated at `b270bfba` (owner 103/103 affected
tests). Main combined SelfContained run `7b467b17aee64b0fb730cc1d3e0d99c2` is
**4,185/4,186, not GREEN**: a Chat seed failed before provider dispatch with
ExecutionFailure and zero requests. A deterministic registration-order race was
subsequently reproduced and the reviewed fix integrated at `3feca6fb`. The next
combined run `19591731af10405197165d4a0e56e7ef` passed all Chat cases but remains
**4,188/4,189**, with one Scan concurrent-execution test wait timeout under diagnosis.
The reviewed BL fixture/scenario compiled at `cafedace`
(83/83 affected checks). Its approved live run `3bde4a7267ca4d7d9ecb5c38d50f6a7b`
failed Interrupted without successful operator selection/publication after one provider request
(2,095 input/2,889 output tokens); all owned resources were removed. The one-run
grant is consumed, with no automatic retry. Scan owns interruption diagnosis;
hidden zero report counts do not establish absence of accepted CL. Safe fixture
stage diagnostics and a controlled no-response reproducer are now integrated and
compiled (75/75 affected checks, `262bd0272fed4e79b66657488b2ab783`). Another live
diagnostic attempt was explicitly approved on October 9 under the same bounded
synthetic/provider/disposable-resource limits. That attempt is now complete:
`38f6d3a06bcc4b40be260eeb932f5346` failed at the fixture's registration-root selector.
The single offered candidate had the correct source origin and covered the
registration declaration/return, but its anchors exceeded the method-only boundary.
The fixture rejected it, published no operator response, and Generate subsequently
reported Interrupted. One request used 2,095 input/2,064 output tokens; all owned
resources were cleaned. This identifies the initiating rejection for this attempt,
not a BL engine failure. The fixture-selection correction now accepts valid broader
anchors covering the registration declaration and return while retaining exact
source/corpus validation and requiring exactly one eligible offered candidate.
Independent source review accepted this test-only correction; its focused source-free
checks pass 11/11 (`06acf5e8b4aa45e893b1c8940e7cdde6`). A newly approved corrected
run on `028534d2`, `ca1f324058864d41b9d8d6684b28b0ef`, confirmed successful unique
root selection and operator-response publication, then failed later in Generate
(exit 1, source-or-execution-gap). It used two requests, 14,393 input/6,274 output
tokens; all owned resources were cleaned. Scan and Business own source-free
diagnosis of the later failure. BL publication/reading remains unqualified;
the latest one-run grant is consumed with no automatic retry.
Reviewed command-local Business diagnostics are integrated at `ec7af241`; they
retain fixed failure codes and exact-operation enum observations behind live
disclosure/replay checks. Root fixture extraction is allowlisted and independently
reviewed. This improves the next run's evidence, not the prior failure's diagnosis;
The user subsequently authorized up to five further synthetic attempts with bounded
diagnosis/correction, retaining all per-run limits and disposable boundaries.
Attempt 1 (`bbc63f6cdeb149319856274b6af025fc`, source `45ed7f65`) failed after
successful selection: finalizer and exact lookup both Failed / AlreadyCommitted,
operation mismatch false, `business_publication_unavailable`. Detailed synthesis
cause remains under Business-owned source-free diagnosis. Usage 2 requests/30,587
tokens; all cleanup verified. Four attempts remain; no retained dataset authorized.
Attempt 2 (`1d0fc8e1618a46d5b17f35656a9add1e`, source `eb907bd7`) failed earlier
with `worker_not_completed` before accepted CL/operator selection/Business. It used
one request/2,653 tokens and cleanup passed. Three attempts remain; series total
three requests/33,240 tokens. Scan owns Worker-path diagnosis. The reviewed new
Business reason and prompt clarification are integrated (`9814268d`, `e0b99bbc`),
with main controlled verification 248/248; they were not exercised by attempt 2.
Attempt 3 (`a77616cdda8844d3b1d684ff301330d5`, source `ce7340a8`) reached Business
and reported ResponseShape: an envelope role/content-kind rejection before JSON
parsing. The exact rejected type remains unknown. Worker diagnostic integration
passed main 144/144 beforehand. Series totals are five requests/58,108 tokens;
two attempts remain, cleanup verified. Business owns bounded envelope diagnosis.
The installed-SDK ancillary reasoning compatibility defect was reproduced/fixed
at `4b555cc7` (owner 403/403, main 80/80). Attempt 4
(`d38660c31adc4a49b1dd7ea0b9000233`, source `1bb6b0ba`) passed the envelope guard
but failed exact SectionReference validation. Business is diagnosing reference
supply/prompt semantics, without weakening validation. Series totals seven
requests/90,944 tokens; one live attempt remains. All disposable cleanup verified.
Presentation operation C remains unapproved.

Current activation authority and ownership are in the [October 8 activation record](../bmad/planning-artifacts/october-activation-2026-10-08.md).
The approved 62-case source-free PostgreSQL selection passed with owned cleanup;
its reviewed tests/evidence are integrated at `9271f8f0`. This supersedes older
"unrun PostgreSQL" statements for exactly those cases, not real generated content.
Operation B is now complete for the exact synthetic CL scenario above; retained
operation C still requires its separate concrete authorization. Reviewed Business
per-turn cleanup is integrated at `260c5706`, with its own 62/62 PostgreSQL rerun and
verified cleanup. Final combined SelfContained `9e0129e1d5d54b94bc5df77ed6e57ffb`
passes **3,991/3,991**, zero failures/skips, including Identity reader binding, prompt
digests, shared E2E compilation and the Business cleanup. The earlier logger and
streaming-inactivity fixture races were diagnosed and corrected without weakening
assertions; see the activation record for failed and passing runs. This is not real
Generate, live Chat, browser or retained presentation qualification. Narrow Scan
command activation is subsequently integrated at `c00db2cd` from reviewed `28902685`:
source/tests/build files match the owner's 2,170/2,170 affected controlled boundary,
and root policy checks pass (431 files). The 3,991 full-suite result remains pinned
before this Scan addition. Chat parent/application checkpoints are integrated at
`71cdd7a0` and `589d78b4`. Reviewed root Web composition now passes the combined
SelfContained suite **4,121/4,121**, zero failures/skips, run
`488bd10080be4d9cba87308df3ccf8aa`; this includes the narrow Scan activation.
The user subsequently approved A+ (seven source-free Chat/Explorer PostgreSQL
cases) and resumed B (one synthetic CL-only Generate using the protected root
`.env` provider binding, at most three requests/no retries and cumulative
100,000 input/12,000 output tokens). A+ passed **7/7**, zero failures/skips, run
`4afa67195cbc4ada8ecf1f84ef40bb9b`, with owned container/volume removal verified;
its evidence is integrated at `3316505d`. B run
`aed6ea62f99b48038de105aa787834d1` failed (0/1) at generated-source provenance
resolution before ordinary-reader assertions; owned cleanup passed. Generate report
and provider usage were not retained, so underlying cause/dispatch remain unknown.
Root's reviewed qualification diagnostics now pass 53/53 affected source-free
cases (`b35ffd84f2f74f42845d2263dc6b3605`); no automatic external retry is allowed.
The user approved one additional B and subsequent diagnosis/fix. That attempt
`ea77060cab984234a9a5de09ec6b2324` failed before inventory, reporting zero provider
requests. Diagnosis reproduced native launch failure when the Generator fixture
omitted Windows `PATHEXT`; restoring only that key passed the native probe.
Root's fixture-only correction and exact real authority/contained-list regression
pass 1/1 (`3ddd767908e04d738097eca9f131d8b5`), with owned cleanup. Production Scan
and persistent machine configuration are unchanged. The subsequent corrected full B
passed as recorded above; native and provider-free passes alone were not
generated-content evidence. C remains separately pending authorization; no production
source disclosure is approved.
Story 13.1 is complete in `specs/015-retained-source-access`.
Exact retained capture, scoped reads, publication/recovery, cleanup, trusted
workspaces and separate-process reopening have bounded controlled evidence.
Convergence 02 is clean; quality review 03 accepted with no findings; required
HTTP deletion checks using production startup and Identity cookies pass. Final
verification passed 3,135 tests with 15 existing skips. This includes 167 retained-source
passes and four child-only skips covered by parent tests. Web build: zero errors,
nine existing warnings. See the feature's `evidence/final-verification-01.json`.

The approved capacity-test polling repair preserves the launch timeout, counts and
required terminal state. An earlier failure after 48 worker launches remains
unexplained and is separate from the repaired terminal observation; controlled
passing runs are not a causal explanation. The existing exact-resolution stable-key
limitation below remains outside this story and unqualified. Story 13.2 remains
backlog; Epic 13 remains in-progress. Use the feature's `workflow-state.json` for
the bound completion checkpoint. Other as-built notes retain baseline
`2bb83c3b44fc0afc07ae253ec1df5fab4399b964` and require fresh checkout/source
verification before reuse.

## Current authority and delivery

Earlier October integration checkpoint (October 7): `d92833c1` includes the reviewed
Business trusted flow-input loader, controlled CL-only Generate runner and actual
Main/safe-report wiring, saved Chat reference footer and canonical history/display
consumer. Shared explicit Generate fixture inputs are at `417c888f`; the behavior-free
Business handoff lifetime declaration is selectively imported at `6578a2d5`.
The actual same-scope handoff lifetime, both corrected Business executor consumers,
canonical finalization helpers, synthesis prompt resource and guarded one-Generate
qualification adapter are now imported. The full SelfContained suite passes
3,727/3,727, zero failures/skips; external suites remain unqualified. Shared ordinary-user
reader qualification, BL persistence/first-dispatch composition and final Web Chat composition remain
open. Source/provider/database/host permission for the shared real run is still
outstanding. See the current delivery-wave handoff below for exact ownership and evidence.

Subsequent controlled slot integration imports Scan `ba43236b`: private original-run
one-shot finalization mechanics and cleanup lifetime, not live S3 activation.
The combined October/Explore/PreparedSource selection passes 1,868/1,868, zero
failures/skips (`e35d387415e34dc699e485edfb542b49`). Actual CL-only/fresh-host paths
cannot issue the slot. Business persistence/finalizer `a0c9676b` plus exact deferred-root
correction `bf3ca6ca` are now reviewed for controlled integration; seven PostgreSQL
facts remain authored/unrun. Final combined SelfContained run `a3c026a8b3f042ab8d6e8ca5e4be3a16`
is 3,876/3,877, zero skips, NOT green: the sole logging-test cleanup failure captures
an undisposed local logger/open handle after factory scope exit. Root owns this
unresolved teardown defect; no test waiver or runtime readiness is claimed. Production
command composition, Web Chat activation and one-dataset ordinary-user qualification
remain open. See the wave handoff for exact evidence and resource boundaries.

Business attempt consumers `9950c8b` now implement the accepted lease-aware
interface, exact profile checks, observed dispatch and terminal accounting. Root
combined October/Explore/PreparedSource tests pass 2,008/2,008, zero skips
(`1ce61f77cba84bf493b6890a5e13d5f6`). This does not replace the non-green full-suite
result above. Consumer correction `b3edb9e0` subsequently removed premature rate
reservation and preserved typed final denial. Scan `95864e55` now supplies internal
natural-completion and actual selected-client controlled conformance; root selection
passes 2,062/2,062 (`dd3b1f95502e4ab888e69e0c86ca365d`). Program/CLI remains inactive
for that composition. Its controlled reader authentication binding is not production
Identity proof: root must supply a trusted headless Reader context/security-stamp
binding before live activation. Native/provider/PostgreSQL and ordinary-user E2E
qualification remain open; see the wave handoff for exact boundaries.

- [PRD](../bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md): product scope
  and explicit Current/Deferred/History/Original unmet backlog dispositions.
- [Glossary](../UBIQUITOUS_LANGUAGE.md): canonical domain terms.
- [Architecture gateway](../bmad/planning-artifacts/architecture.md) and its spine:
  adopted generic target and project responsibilities; not as-built qualification.
- [Epics](../bmad/epics.md) define delivery; [sprint status](../bmad/implementation-artifacts/sprint-status.yaml)
  owns lifecycle. Epics 13–17 contain 41 definitions (19/7/8/3/4). The user approved
  adding October Epics 18–20 and their 17 definitions (12/3/2) to that tracker on
  September 27; [October release epics](../bmad/planning-artifacts/october-release-epics.md)
  owns their scoped requirements and definitions, routed from the canonical epic index.
  Tracker refreshes require both epic files. Story 13.1 and October Stories 18.1
  and 18.4 are done. The narrowed 18.2 and 18.3 slices are integrated; their original
  full-story requirements remain incomplete and skipped, not silently completed.
  Integration does not activate another story or resume the frozen tool workflows.
- The [October delivery plan, revised October 6](../bmad/planning-artifacts/october-delivery-plan-2026-09-30.md)
  now selects the first useful outcomes of four feature groups and reserves an
  [integration phase](../bmad/planning-artifacts/october-integration-plan-2026-10-06.md).
  The user abandoned full 18.5 execution for this delivery; OCT-BUSINESS owns the
  minimum Knowledge foundation derived from its plan. Old 18.5 evidence remains
  historical and incomplete; resuming its held workflow is not a new prerequisite.
  Follow the revised briefs rather than copying the full original story matrices
  or skipped tool tasks. Initial Knowledge K/CP-K1 is accepted at `12d92d80`
  (candidate `07c3fe44`), with Scan/Chat/Explore conformance and 89/89 combined
  controlled passes. Business B0a T007–T013 is now locally implemented and
  independently accepted: 160/160 focused SelfContained, 9/9 owned PostgreSQL 17,
  and 1/1 owned cross-process logging qualification, with zero skips and verified
  cleanup. Its source commit is imported with B0b; see the Business
  [execution/handoff record](../../specs/20-1-Oct14-business/quickstart.md).
  Local B0b T014–T018 is also independently accepted: exact immutable corpus/CL
  publication, live ordinary-reader authorization, bounded protected reads and
  feature-local composition. Final affected verification passed 373/373 SelfContained,
  20/20 owned PostgreSQL and 1/1 cross-process logging checks, with zero skips and
  verified cleanup. The writer now additionally requires trusted host freeze/inventory
  authority. The October 7 integration imports Business `b65213ad`, Explore E0
  `50be5782` and Scan S1 `50779d73`, and wires opt-in exact reader composition to
  the frozen DefaultConnection with live ordinary-user authorization. Full
  SelfContained regression passed 2,592/2,592 and owned PostgreSQL passed 21/21,
  zero skips. See the [integration handoff](../bmad/planning-artifacts/october-reader-integration-2026-10-07.md)
  for configuration, evidence and remaining resource limits. Real Scan S2 adapters,
  retained source-backed publication, Business T020 and Explorer's real generated-CL
  qualification remain pending. This is not Chat W0/live Chat completion.
  The subsequent controlled Chat checkpoint `e4689f0b` (including entry parent
  `80314207`) is integrated at `10db709b`. Combined SelfContained verification
  passed 2,775/2,775, zero skips, including 193 Chat and 15 reader-integration
  cases. October generation remains disabled; prompt embedding, raw transport and
  Web admission, full-contributor disclosure and durable dialogue remain missing.
  See the [Chat integration/next-step handoff](../bmad/planning-artifacts/october-chat-integration-2026-10-07.md).
  The subsequent user-authorized [October 7 delivery wave](../bmad/planning-artifacts/october-wave-2026-10-07.md)
  supersedes that pause: four-lane scoped development and incremental integration
  may proceed under its dependency/ownership boundaries. Shared dialogue/disclosure
  agreement precedes its consumers; real resource authority remains separate.
  The first wave integrates Chat tool progress `df7f09cc`, Explorer Identity-claim
  correction `d28ba236` and host Chat-context/prompt preparation `0949d633`.
  Combined source `84d60d8d` passed 2,798/2,798 SelfContained tests, zero skips.
  The subsequent wave accepts Business canonical dialogue/disclosure `d914fea8`
  with both consumer agreements, imports Scan configuration `8cc06035` and its Worker
  prompt `2106af16`, and adds controlled fresh-read Web provider binding. Final combined
  SelfContained verification passed 2,859/2,859, zero failures/skips. All four lanes
  are released within the wave boundaries; no all-Chat or all-lane barrier applies.
  Reviewed raw Chat guard `26a89712` is subsequently integrated: 509/509 selected
  related SelfContained cases pass (not another full-suite run). This supplies strict
  streaming validation and guard cleanup, not selected-client/admission composition.
  Scan Worker/scope `7b34147d` is also integrated with 1,064/1,064 selected combined
  October/Explore/PreparedSource passes. Actual SDK calls used controlled transports;
  Generate Main, supporting-source acceptance compatibility and real S2 remain open.
  Owner disclosure `f5e999ef`, Explorer graph `b23ed71e` and Chat canonical consumer
  `34aaf110` are now integrated with actual Web disclosure wiring and an ordinary-user
  graph navigation link. Combined affected verification passes 1,147/1,147 controlled
  cases; graph and owner crypto are exercised together over controlled published data.
  This is not a new full-suite result; Explorer's separate full-run native SQLite
  crash remains unexplained. Chat generation remains disabled; dialogue persistence,
  W1 admission/client composition and real-source retained qualification remain open.
  A subsequent selected Chat client/request-lifetime component has 20/20 controlled
  SDK/queue/cancellation/cleanup tests and independent review. It does not activate
  sending: live execution registration, commit/owner-death adapters and coordinator
  composition remain required. See the same wave handoff for its exact API and limits.
  Business bounded B1 policy/executor `407d6833` is also integrated, without its
  unfinished store/publication/S3 composition. The resulting affected combined
  selection passes 1,207/1,207 SelfContained cases (not a complete suite or real run).
  The next integrated boundary adds reviewed Scan frame/F1 `cffbe50e`, Chat admitted
  runner `6de2d394`, Business dialogue store/codec/additive migration `42cac3c4` and
  Scan-compatible input/Generate declarations `ed0f2600`. Combined affected source
  passes 1,303/1,303 controlled cases, including noncooperative readiness/deadline
  corrections. No migration or real host/provider run occurred. Generate owner helper/
  provenance implementation, Scan Main composition and live Chat execution/commit/death
  wiring remain incomplete; ordinary graph access does not wait for Chat persistence.
  Final cross-owner Chat conformance and accepted task/model wording raise the affected
  combined result to 1,306/1,306, zero skips. Real qualification remains pending.
  Dialogue lookup currently supports positive receipt reconciliation, not ConfirmedAbsent
  proof; a missing row never authorizes a retry or a second generation.
  Supporting-input `0ab10a77`, corrected Generate admission `7958a5e3` and reviewed
  Scan inventory/CL publication component `49c18845` are integrated. The admission
  callback/cancellation classification hold is lifted after independent same-gate
  exit review and 1,404/1,404 affected controlled passes at the corrected boundary.
  Scan may compose the actual owner input/digest/admission components with its exact
  live operator, F1 registration and one-dispatch lifetime. Public Generate remains
  incomplete. The six new admission PostgreSQL cases are authored, not executed;
  no real run, migration or new resource permission is implied.
  Reviewed B1 persistence/publication `cbb8051f` is now integrated with 1,437/1,437
  affected controlled passes. It preserves B0 membership and distinguishes exact
  inquiry-caller uncertainty ownership from full input provenance. The additive
  six-table October migration is unapplied; updated runtime readers require that
  schema. S3 finalization/F1 registration, the Business-owned executor-to-store
  witness handoff correction, PostgreSQL and real-source/provider qualification
  remain pending. F2 exists; controlled B2 preparation does not wait for Chat.
  No implicit B2 or Chat runtime completion is claimed.
  Reviewed Chat coordinator `d99517a6` is integrated at `9227f376`, with
  1,459/1,459 combined affected SelfContained passes and zero failures/skips.
  It coordinates the canonical admitted turn, actual generation/drain ownership,
  phase acknowledgement reconciliation and immutable terminal save. Root-owned
  live execution registration, commit/owner-death adapters and route activation
  remain missing; Chat owns the bounded presentation adapter. Controlled evidence
  does not authorize or prove a real provider/host/database run.
  The same-attempt Business resolution witness handoff `8b72833d` is integrated,
  and the unregistered root Chat commit-fence component is independently reviewed.
  Combined affected verification passes 1,486/1,486 controlled cases, zero skips.
  Business's reproduced dialogue-store submission/disposal race is corrected by
  `4ff94d02`, integrated at `be1915ab`, with 1,487/1,487 combined affected controlled
  passes. The store-specific implementation hold is lifted; no live mutation or
  PostgreSQL qualification is claimed. The owner's separate discovery CLR crash
  remains unexplained/non-success. The root commit guard alone is not execution
  registration, owner-death proof or a reachable Chat route.
  Reviewed Chat presentation `2d371f4f` is integrated at `3d40c143` with
  1,499/1,499 affected controlled passes, zero skips. Its bounded nonblocking queue
  retains subscriber lifetime, rechecks full disclosure lineage and suppresses
  denied content without inventing a saved state. Actual page attachment/host
  activation and browser qualification remain open; Chat continues leaf rendering
  and reader links while root owns parent composition.
  The encoded-text Chat renderer `bd51e764` is integrated at `97f428ec`;
  combined affected controlled checks pass 1,505/1,505, zero skips. It supports a
  bounded formatting subset with literal fallback, not full CommonMark or live
  Chat activation. Business/Explorer/Chat footer agreement is now accepted and
  controlled T026/T027 implementation is released; a URL builder alone is not title
  or disclosure authority. The exact display-only lineage and limits are in the
  wave handoff.
  Explicit-root Business planner `44aac568` is integrated at `fccde66c`;
  combined affected verification passes 1,543/1,543 controlled cases, zero skips.
  Complete corpus lineage includes coverage without CL, independently of bounded
  directed flow membership. Trusted persisted-input loading, synthesis and atomic
  BL persistence remain unfinished; no schema or live execution was added.
- [Constitution](../../.specify/memory/constitution.md), root AGENTS and the current
  story workflow govern development. The constitution's 2.0.0 local amendment aligns
  prior conflicting rules; it does not claim maintainer PR review/merge.

Story 18.4 is integrated from `7e19bf69`; its
`specs/017-october-batch-setup/workflow-state.json` records completion and closed
recovery. Independent batch setup preserves source authority, context provenance,
isolation and explicit provider binding. Ordinary Web uses startup
`DefaultConnection`, normal migrations/roles/browser/provider administration and
no October mode or generator prerequisite. Its retained evidence records 174
focused and 2,149 SelfContained passes, a successful Web build, and owned
PostgreSQL/generator/Web process qualification; this is not later Scan or paid
provider qualification. See `evidence/verify-aggregate-01.json` and
`evidence/ordinary-web-runtime-qualification-final-green-01.json` in that feature.

Story 18.2 is integrated from `9be46d6a` and Story 18.3 from `870dc49a` under the
user's narrowed direct-SpecKit targets. Their original governed workflows remain
frozen; prior approvals and skipped tasks are historical, not new execution gates.
The JDT slice has independent dedicated-root startup/navigation/lifetime evidence
in `specs/18-2-navigate-prepared-java-source-with-restricted-jdt-ls/evidence/immediate-lifetime-verification-01.json`.
The CodeGraph slice prepares in place and returns the descriptor actually used
for native MCP discovery/invocation; its bounded process-exit correction is in
`specs/18-3-use-native-codegraph-mcp-against-prepared-source/evidence/process-exit-correction-01.json`.
Neither narrow slice proves customer Repository qualification, mixed-root JDT and
CodeGraph coexistence, or the deferred ordinary-reader integration. The trusted
native profiles do not establish preventive filesystem/network isolation.

Workflow installation itself activated no story. Verify current runner ownership
and its assigned checkout before interpreting the integration tracker. Story 13.1's
`specs/015-retained-source-access` plan, state and evidence are completed history;
Story 13.2 remains backlog and requires deliberate activation. Specs 001–014 also
retain historical scope. The agent-context extension targets root AGENTS; pass the
selected feature's actual plan path. Its no-argument auto-discovery chooses by
modification time and must not select a historical plan. The optional context hook
was not run for 18.4 because its author write boundaries excluded root AGENTS.

Story definitions were accepted under explicit approval for 13.1–13.3 and a scoped
confidence delegation for the rest. That completed planning delegation is not standing
authority to bypass a later feature's plan/tool/Scan/PRD or implementation approval.
Use the current unified workflow and configured risk escalation; do not revive old
Full/Standard/expedited or overnight/deadline waivers from historical handoffs.

The installed workflow is governed-delivery 0.7.4 (updated 2026-09-28), using BMad 6.12 and SpecKit
1.0.6 through `.governed-delivery/config.toml`. Use `$run-governed-delivery`;
new story states use schema 3.1. Sol/Luna profiles use GPT-6, with existing efforts
and Astra recovery unchanged. Verification follows accepted behavior/risk, not file
type; nonbehavioral work does not require manufactured RED or new tests. This upgrade
does not activate a story or alter existing states. Root AGENTS now permits explicit
story-scoped delegation for planned changes, first implementation and finite recovery
extensions. Recovery requires a separate user-set cumulative per-episode ceiling and
a fresh bound decision at every reached limit; policy support alone is not a grant.
The story root records and validates decisions. Human approval remains default;
other reserved gates and execution permissions are unchanged. See the adoption
record for current management roles and the remaining designation requirements.
One persistent Sol/medium definition author owns
spec, plan, tasks and later document corrections; each coherent implementation
group retains separate Luna/high test and Sol/medium production owners. Root owns
lifecycle, evidence, approvals, selective project lessons and Git. See the installed
[agent catalog](../../.agents/skills/run-governed-delivery/references/agent-catalog.md)
for conditional architecture/recovery escalation, and the
[adoption record](../agent-workflow/adoption-0.7.md) for installation verification.
Consumer development rules and planned-change/implementation approval gates remain
in force; installation grants no product-story authorization.

Existing story-state files and historical activation/epic receipts were not migrated.
The 0.7.1 compatibility patch validates Story 13.1's unchanged original completion
checkpoint from its actual pre-distribution evidence; its missing-configuration
blocker is resolved without migration or rebinding. This is not Epic 13 closure or
new product verification. Aggregate validation still reports 256 other historical
findings; no waiver or history rewrite was applied. See the adoption record for the
exact boundary. New configured checkpoints use the current contract. A genuinely
active 3.0 state would need bounded root-owned adoption, not a
bulk rewrite; no such current story was found. Project lessons are maintained
selectively when warranted, not by creating an empty file or retrofitting completed
stories.

## As-built reuse boundaries

Outside the completed retained-source work, these remain earlier static observations.
Use the feature evidence for current controlled source tests; no row proves a full
runtime workflow:

| Area | Existing value and limit |
| --- | --- |
| Administration/persistence | Reusable Identity, roles, configuration and domain persistence; Identity remains in Web/Data. |
| Source preparation | Story 13.1 has verified raw-Git retained capture, canonical manifests, and scoped metadata and byte/text reads. Live denial, concurrent publication and controlled process-crash cleanup are verified; trusted workspace materialization is verified; fresh-process remote-unavailable reopen is verified; source-only boundaries and diagnostics are verified; controlled full-suite and all Story 13.1 independent gates pass; no generic Scan qualification is implied. Legacy disposable leases and prepared pair contracts remain separate. |
| CodeGraph/LSP | Integrated narrow adapters: 18.2 records 121 controlled JDT checks, real pinned JDT 1.58 navigation under application-supplied JDK 21, and 2,063 SelfContained passes. 18.3 records actual returned-descriptor native MCP use, process-exit corrections and 2,019 SelfContained passes. These are separate branch/owned-fixture results, not combined-root or customer-Repository qualification. General source-authority/reader integration remains unfinished; no preventive OS filesystem/network isolation is claimed. |
| Knowledge | OCT-BUSINESS B0a implements atomic semantic file acceptance, immutable CL history, exact replay/lookup and sanitized logging in October persistence; focused controlled, owned PostgreSQL and two-process logger evidence is recorded in its quickstart. Real Scan authority/source adapters and B0b publication/live reads remain unqualified; legacy stores are not substitutes. |
| Resolution | Finite exact matching is profile-bound; generic inquiry, late-evidence reconciliation and the ordinary production caller are not delivered. Controlled baseline qualification also exposed an unchanged consumer candidate missing ConsumerEntryStableKey, which the V2 store rejects with Conflict; see specs/015-retained-source-access/evidence/canonical-correction-affected-fixture-diagnosis-01.json. |
| Chat | Provider SSE is ingested but the current answer path buffers strict candidates and reveals accepted text in the UI. Pair-bound scope and process-local dialogue/publication are not genuine generic streaming/durable reattachment or Business BL-only behavior. |
| Reader/graph | Current pages are placeholders; existing Knowledge/citation code is not a working exact-version reader or graph. The planned diagram package is not installed at this baseline. |
| Runtime/operations | The current Compose profile is a baseline, not bundled-tool/source/key persistence or coherent backup/restore proof. Startup applies migrations/seeding. |

Consult actual `src/dotnet/`, `tests/Vulgata.Tests`, Docker/package files and the owning
story before reuse. Earlier external assessment notes may help locate code, but the
repository's current source and accepted documents remain the authorities. In
particular, a registered service is not proof of a reachable end-to-end route.

## Historical material

The [adoption ledger](../bmad/planning-artifacts/generic-adoption-2026-09-11.md) and
[archive manifest](../bmad/history/pre-generic-adoption-2026-09-11/manifest.json)
preserve exact pre-images. Epics 1–4 retain recorded completion; 11.1–11.4 and
12.1–12.4 retain their accepted history. Story 11.5 remains incomplete and 12.5
terminated incomplete. Old Epics 5–10, July plans and historical specs are not active
queue or fallback instructions. Preserve their evidence rather than rewriting it as
generic success.

Prompt Workbench, dedicated unknown-library inference and general configurable MCP
remain original unmet backlog outside the five-epic core sequence. Deferred features
remain deferred; native CodeGraph/internal wiki tools do not complete general MCP.

## Harness compatibility

Root `CLAUDE.md` is a symlink to AGENTS; Copilot and Trae share root guidance.
Only the installed Codex governed-delivery profiles currently support governed
story delivery. Obsolete Copilot sprint-loop agents and run/close prompts were
removed; remaining unrelated helpers are not alternative delivery orchestrators.
Copilot/Trae assistance remains subject to AGENTS and must not imply workflow
harness parity. Start a fresh Codex session after installation so the new skills and
profiles are discovered. Any other delivery-harness adapter requires a separate
bounded compatibility task.
Scan architecture helpers use current authorities; July work requires explicit
historical scope. Legacy test/seeding recipes grant no runtime or credential authority.

## Maintenance

Update this compact snapshot after accepted implementation or workflow changes;
replace superseded facts rather than accumulating turn logs. Recheck affected links,
source ownership and harness compatibility. Keep future requirements in PRD/stories,
stable conventions in the constitution/development guide, and observed test/runtime
pitfalls in [testing guidance](testing.md). Current source/lifecycle evidence always
outranks this dated orientation note.
