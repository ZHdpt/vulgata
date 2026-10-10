# Retained phone-verification pilot

Status: one prior real CL remains accepted but unpublished. Parser diagnostics
identify `Syntax / Root`; paid runs held for source-free outbound JSON-mode and
final-text assessment. No malformed-output repair or acceptance relaxation approved.
An initial Generate
launch was rejected by execution auto-review before starting because supporting
reads could disclose additional slice files. The user subsequently explicitly
confirmed relevant content from ALL 77 prepared files may be sent to the approved
DeepSeek provider, while only nine selected files generate Documents. Evaluation
answers remain excluded. This resolves that specific scope hold; reverify source
and runtime bindings before launching. No provider request occurred in the rejected
invocation.

## Scope

The supplied two-repository slice contains 77 manifest-verified files / 411,180
bytes, with no extra files. Use only its two prepared roots for source access.
The handoff is advisory context; separate evaluation answers must not enter Worker
or Business inputs. Do not assume deployed RPC wiring or external gateway behavior.

The first selected generation scope is nine files / 10,516 bytes:

| Repository | Relative path |
| --- | --- |
| mbs_account | src/main/java/cn/com/yitong/actions/acct/GetMobileByAccessCodeOp.java |
| mbs_account | src/main/java/cn/com/yitong/actions/acct/GetMobileToReplaceResOp.java |
| mbs_account | src/main/java/cn/com/yitong/actions/register/RegisterCheckPhoneNumOp.java |
| mbs_account | src/main/resources/disgram/in/register/phoneNumbAuth.xml |
| mbs_account | src/main/resources/flow/acct/getMobileByAccessCode.flw |
| mbs_account | src/main/resources/flow/register/phoneNumbAuth.flw |
| mbs_channel | src/main/resources/disgram/in/register/phoneNumbAuth.xml |
| mbs_channel | src/main/resources/disgram/out/register/phoneNumbAuth.xml |
| mbs_channel | src/main/resources/flow/register/phoneNumbAuth.flw |

Other slice files may supply read-only support; mark them as supporting/unprocessed,
not generated or covered. Full-slice completion is not claimed. Choose actual
offered channel/account entrypoint roots from accepted CL, not invented IDs.

## Proposed bounded runtime

- Reuse the existing running PostgreSQL 17 container and volume; do not create,
  restart or delete a container/volume. Only create a new uniquely named owned pilot
  database. No mutation of existing databases. Read-only verification found PostgreSQL
  17.10 and the configured role has database-creation authority.
- Existing Web Identity/domain migrations and Generator October migrations only;
  no schema changes. Dedicated bootstrap Administrator and ordinary User accounts.
- Temporary Web on an unused loopback port for production registration/setup/login
  and exact reader verification. Stop host after qualification; successful data and
  required keys remain retained. No externally accessible listener.
- Existing approved provider/model and protected credential reference. The user
  removed the proposed cumulative request/token budgets. The implementation still
  requires positive finite fields: use nonbinding configuration values (1,000,000
  total requests; 1,000,000,000,000 tokens), not a claim of literal infinite limits.
  Worker per-file rounds and continuation windows must remain at the supported
  technical maximum of 100: setting them to 1,000,000 passes policy validation but
  fails Worker admission. With concurrency 1, a 30-minute deadline,
  existing rate control and per-call context/output bounds, these must not be the
  practical stopping condition. No change to semantic validation or automatic
  transient retry policy. Business existing two-attempt policy remains unchanged.
  Run deadline 1,800 seconds, file deadline 300 seconds, operator selection 120
  seconds, drain 30. Stop on provider credit exhaustion rather than buying credit.
- Pinned CodeGraph/offline JDT profile with owned tool state only. Do not install
  tools, change persistent PATH or modify business source. Recheck source hashes.
- Runtime configuration, credentials, keys, source paths, process IDs and physical
  bindings stay in protected local files outside Git; tracked material uses logical
  names only. Never print secrets. Retain sanitized timing/usage/status evidence.
- Use `eng/watch-october-batch.ps1` with explicit 1,800-second timeout. Console
  progress is elapsed/deadline, not an invented work-completion percentage. At the
  wall-clock limit terminate only the launched Generator tree and check settlement.
  A hard kill does not prove rollback; preserve ambiguous state and remaining-time
  assessment before any retry or cleanup. The user's ten-minute constraint was
  monitor implementation effort, not a ten-minute batch deadline.

## Production bootstrap prerequisites

Repository registration invokes git ls-remote. The original local repositories
were verified readable without network or writes; use those only as registration
locators if confirmed, never as source-tool roots. The prepared roots remain exact
scan inputs. Do not replace Git validation with a fixture stub or fabricated URL.

The Generator reads child-process environment, not CLI configuration arguments.
An untracked local run configuration can supply those variables. Use prepare,
select and generate with their existing command contracts and production authority.
There is no existing non-test protected-credential export utility; a small local
bootstrap adapter using the existing Data Protection/secret service is needed.
Do not reuse GeneratorPrepareFixture or disable its cleanup. Use System/Repository
advisory context through production APIs; no need to select global context for this
pilot. Preserve exact reader identity and publication binding.

## Retention and evidence

Success: retain database, source bindings and required keys until explicit deletion;
every later run gets a fresh database. Failed incomplete owned runs may be removed
after settled outcome and sanitized evidence; indeterminate state is preserved.
Measure setup/tool/Worker/Business/publication/reader times separately where available.
Claim cross-repository resolution only if actual source-backed accepted results
demonstrate it. Compare generated content with evaluation answers after generation.

## Bootstrap checkpoint

Existing container `vulgata-postgres` was reused unchanged. Created only database
`vulgata_oct_pilot_mv0r9q4b_e19qg1`. The dedicated temporary loopback Web host applied
existing migrations; production registration and System/Repository APIs succeeded.
Keys and encrypted provider/operator files are in the protected local run directory
`phone-mv0r8ity`, outside Git. Generator prepare/select passed without a provider call.
The temporary Web process was stopped after the blocked launch; keep the database,
keys and configuration for continuation. Do not delete the shared container/volume.

The generic credential packager uses stdin-only secrets and production Data
Protection purposes; its controlled synthetic roundtrip/overwrite refusal passed
review. The watchdog's completion/output/environment/timeout controls passed; timeout
remains an indeterminate-settlement signal, never proof of database rollback.

## Runtime recovery observations

- Missing owned Generator log directory originally caused exit 1 before admission;
  create it explicitly during bootstrap.
- Original batch admitted Generate but rejected invalid per-file limits before any
  provider request. One operation, zero accepted files/publications were verified.
- October setup has one selection slot per database. A different batch in an already
  selected database conflicts and is surfaced as `Denied`; do not reset that record.
- Replacement `phone-527d7e0ffe4f` prepared successfully, then ended in 56.26 seconds
  with zero requests, empty inventory and `source-authority-or-identity-lost` gaps.
  The earlier attempt's two `.codegraph` directories were still present. The adapter
  rejects pre-existing state but disposal releases handles without deleting state.
  Their creation times match the original run; all 77 source hashes still matched.
  Archived only those generated directories under the failed run's protected local
  directory, not in the source trees. This needs a repeat-run operational fix; no
  source-access check was weakened.
- Current fresh run `phone-9350f644c594`, batch
  `94363a52-69d2-465f-be62-82a2a6a5b9bf`, uses database
  `vulgata_oct_pilot_97cbb54eb7e34cf493f020c06b8dba95` in the same existing container.
  Production bootstrap and prepare passed. Generate ended after 18.70 seconds
  (reported pipeline 16.46 seconds), exit 1, no timeout or unconfirmed cleanup.
  Inventory contained all 77 files; first Worker failed `InvalidOutput`,
  `StreamTerminal`. One request used 2,438 input / 1,087 output tokens, usage known.
  Read-only settlement: one operation, zero accepted-file manifests, zero publications.
  No successful retained dataset exists; failed databases remain isolated for diagnosis.
  Raw provider content was not captured, so this broad failure stage does not prove
  which stream condition failed. Scan owns bounded diagnosis/safe diagnostics and
  controlled tests; root owns any later real retry. No unchanged retry is appropriate.

## Diagnostic checkpoint and retry

Integrated reviewed Scan delta `5aa759a9` as `0bb15df6`: one finite nullable
`workerDiagnostics.terminalFailure`, no acceptance/retry/validation changes or raw
payload logging. Main affected SelfContained verification passed **173/173**, no
skips, isolation `d16a79f3687b4792a9a716d1b5d47d1c`. This is not full-suite GREEN.
Initial restricted build had no compiler diagnostics; subsequent host build exposed
`pwsh` resolution failure under the inherited over-8,200-character PATH. A short
process-local PATH resolved it; no persistent configuration changed.

Fresh diagnostic run `phone-43e354514b75`, batch
`ea0798f0-1ad6-476e-8751-9a2e2c241e2c`, database
`vulgata_oct_pilot_e57f71e490ef4c41bc577570dee86168` in the same existing container:

- Wall **21.02 seconds**, pipeline 18.74 seconds; exit 1, no timeout or cleanup doubt.
- Inventory **77**, accepted files **0**, Documents **0**, publications **0**.
- **1 request**, 2,438 input / 1,613 output tokens; usage known.
- `InvalidOutput / StreamTerminal / ContentAfterFinish`, retry disposition `None`.
- Read-only settlement: one operation, zero accepted manifests/publications.
- Previous attempt's owned indexes archived; all 77 source hashes matched before retry.

This identifies the exact rejected branch, not the nature of the trailing content.
An SDK-assembled tool-call event is a hypothesis to inspect, not an established
cause. Scan owns SDK-sequence diagnosis/controlled correction; preserve rejection
of genuinely invalid trailing content. No further unchanged paid retry.

## SDK correction and first accepted real CL

Reviewed lane fix `12f1873f` integrated as `88790331`. Controlled actual-SDK SSE
reproduction proved that the installed adapter emits an assembled executable
tool-call update after the `ToolCalls` finish marker. The narrow exception permits
one matching aggregate only, retaining all call validation and rejecting trailing
text. Main affected tests **186/186**, no skips, isolation
`98bc7f4a235445d88bb9bde9be257fba`; not full regression/runtime qualification.

Corrected real run `phone-56378a6c60d7`, batch
`75e30e39-7ec3-48e3-8b55-d9c547bf5731`, database
`vulgata_oct_pilot_beca4c15e7414fc7b4732d527f480f18`:

- **31.49 seconds** wall / 29.24 seconds reported pipeline, exit 1, no timeout or
  unconfirmed cleanup.
- First selected `GetMobileByAccessCodeOp.java` produced one accepted CL Document:
  logical ID `68569a16-c897-5890-72ca-0a3fd53bb347`, exact version
  `770b1751-02ea-4721-8958-a421bf1f9521`.
- Second file stopped at `BudgetExceeded / StreamTerminal`, terminal code null.
  **2 requests**, input/output token totals unknown. Do not invent totals or treat
  removed cumulative caps as removal of all internal per-file safety limits.
- Database counts: **1 accepted manifest / 1 Document Version / 0 publications**.
  Accepted content is not yet an ordinarily readable retained publication.
- Scan owns precise internal-budget diagnosis before any further retry. Root
  preserves the partial database and keys; later runs must use fresh databases.

Read-only semantic spot-check of the first accepted CL: it correctly describes the
method writing `timeStamp` and returning `NEXT`, and does not invent a mobile-number
lookup based on the class name. However, some cited line ranges are misaligned:
the execute claim cites 17–21, while the actual method declaration/body runs 20–23
(return on22). Structural acceptance is not complete citation-accuracy qualification.
Record this quality gap; do not present this partial dataset as presentation-ready.

## Budget diagnostic integration and divergent retry

Integrated reviewed `b72aae4a` as `31b00465`; labels only existing output-byte,
tool-call and stream-update guards. Main affected SelfContained **192/192**, no
skips, isolation `da32e16289cd4c8dbe1b33a0ac00a7a2`; no limit/charge/acceptance changes.

Fresh run `phone-38a2b10a166c`, batch `56dcd8b2-0ef2-4100-8044-9628b527019f`,
database `vulgata_oct_pilot_fe1981e86bc8427689f6e70c77c31265`:

- **19.83 seconds** wall; exit1, no timeout or unconfirmed cleanup.
- **1 request**, 2,438 input / 648 output tokens, usage known.
- `ProviderFailed / None`, failureStage/terminalFailure/budgetFailure all null.
- Inventory77; one admitted operation; accepted0/publications0.
- This did **not** reproduce or identify the prior budget bound. The broad status
  does not establish a remote provider outage; local SDK/MAF/tool exceptions remain
  possible. Scan owns controlled diagnosis of the unlabelled path before any next
  paid retry. No arbitrary trailing output, safety-limit or accounting bypass.
- Earlier accepted CL database/keys remain untouched. All77 source hashes matched
  before this run; generated indexes were archived outside the source roots.

## Phase diagnostics and proposal parsing failure

Reviewed two-file checkpoint `36299848` integrated as `a1afb9e3`; finite phase
observations preserve status/retries/guards and identify exact escaping exceptions
without logging them. Main affected SelfContained **205/205**, no skips, isolation
`2bc0e6efb6b14f25be9eca71bc9a410d`; no full-suite claim.

Fresh run `phone-6216c2bfaa55`, batch `0bdd7c09-863e-48ed-9706-298dd88424f4`,
database `vulgata_oct_pilot_6873d244d5b84c30bb7c513f20383a2d`:

- **72.11 seconds** wall /69.84 seconds pipeline, exit1, no timeout/cleanup doubt.
- **4 requests**, 15,437 input /4,845 output tokens, usage known.
- Inventory77, accepted0; `InvalidOutput / ProposalParse / None`; terminal and
  budget reasons null. This does not resolve or reproduce earlier budget/provider
  failure branches. No publication or useful BL/Chat/graph qualification.
- Root has held further paid runs. Scan is assigned a bounded **assessment only**
  of current prompt/schema/parser, actual SDK/MAF final-text handling and tool/ledger
  contracts together. Return concrete inconsistencies, controlled evidence and the
  smallest missing observation before more implementation or live spend. Do not
  repeat another isolated diagnostic-field/paid-run cycle without that decision.
- Previous partial accepted CL and its database/keys remain retained. Only generated
  tool state from the settled prior run was archived; all77 source hashes matched.

## Consolidated correction decision

Test-only characterization `5eb0a4d` integrated as `4b466447`; lane controlled
139/139 passed and independent review accepted. Affected production matches main.
See `specs/20-1-Oct14-scan/worker-contract-assessment.md` for evidence and limits.
Integrator authorized the proposed narrow correction under existing October scope:

- Explicit prompt rules for the existing exact parser; no relaxed acceptance.
- Verified primary relative path and precise whole-file/window line metadata;
  no physical paths, extra source authority or Knowledge schema change.
- Omit unsupported caller cursors from Worker-facing schemas; retain internal page
  draining and leave the shared direct facade's cursor support unchanged.
- Sanitize malformed input before source execution, permitting correction only
  within existing round/tool/byte/time limits. Authority/provenance failures stay
  fatal; refusal must not invent evidence/contributors or bypass the ledger.
- Keep primary anchors primary and preserve supporting provenance; richer supporting
  citation semantics are outside this correction.

Scan owns scoped implementation with separate test/production owners and independent
review. No added parser diagnostic/status normalization, SDK/charging changes or
live resources. Root decides qualification only after reviewed affected GREEN.
This addresses proven contract gaps, not a proven identification of the last
malformed proposal field. Existing partial data and keys remain retained.

## Consolidated correction qualification

Reviewed Scan `ab0de7fd` integrated as `500e2f07`. Main's eleven affected controlled
classes passed **328/328**, zero skips (isolation
`ac98f025e95d44ea8a683ae81dc8a4ed`). No full regression or native/runtime success claim.

Fresh run `phone-1f2243ae6f6e`, batch `28a58dcc-16f2-4dbe-a399-1d2165d362a7`,
database `vulgata_oct_pilot_3098051296684929a4800ec6bed276b8`:

- **37.17 seconds** wall /34.91 seconds pipeline; exit1, no timeout/cleanup doubt.
- **2 requests**, 6,727 input /676 output tokens, usage known; inventory77, accepted0.
- `ProviderFailed / ToolInvocation / None`; no terminal/budget reason. This locates
  the escaping exception in the local tool invocation path, not provider transport.
  It does not identify the tool, argument binding, body, internal serialization or
  ledger refusal. Tool25.50s/model4.86s does not prove a particular tool failed.
- Paid runs held. Scan has bounded source-free diagnosis-only authority over actual
  tool/runtime/ledger handling of non-success results: distinguish required fatal
  authority/provenance refusal from ordinary unavailable-tool fallback. No new
  diagnostic/behavior change or live resource use before a reviewed proposal.
- Prior accepted partial CL/database/keys remain intact; no publication yet.

## Settled JDT limitation correction decision

Reviewed characterization `dc77ddec` integrated as `cbcfd139`; lane controlled
109/109 passed. Inert public-factory tests prove confirmed-cleanup JDT configuration
and readiness failures permanently fault the October runtime even though an
independent same-approval source grant remains valid. This violates the permitted
file-tool fallback in accepted FR-006; it does not prove the latest live failed tool.

Integrator approved one narrow correction and one bounded diagnostic together:

- Preserve typed JDT open outcome and positively confirmed settlement. Permit only
  explicit ConfigurationUnavailable/ReadinessFailed after fresh exact source/root/
  authority checks to retain file-tool fallback with honest limitation metadata.
- Keep unknown/cancelled/expired/authority/provenance/identity/unconfirmed cleanup
  failures fatal; required CodeGraph policy unchanged. No retries, lifetime/budget
  extension, profile, shared-facade API or Knowledge schema changes.
- Fixed observed operation/boundary, existing typed outcome where observed, nullable
  confirmed-settlement flag only; no raw payload/paths/exception text. Preserve first
  actual cause and all output/revocation fences. No broad logging framework.
- Scan owns scoped runtime/wrapper/projection/tests, separate test and production
  owners and independent review. Composed real-frame/ledger fallback and fatal
  counterparts must pass controlled tests before root considers live qualification.

Paid/native/PostgreSQL/source-root operations remain outside the lane dispatch.

## JDT/tool checkpoint and exact byte-bound observation

Reviewed `b5aff0ef` integrated as `449e00bf`; main15 affected controlled classes
passed **466/466**, no skips (isolation `7899441b665e4e06b3543e8b8485160f`). Native
JDT recovery is not qualified by these controlled seams or the next partial run.

Fresh `phone-5dabccf9027c`, batch `021f168d-97e4-4cbf-9f3e-201da9c862d6`, database
`vulgata_oct_pilot_df18c46b20c84f16a0d62d4f9655f102`:

- **51.13s wall /48.87s pipeline**, exit1, no timeout/cleanup doubt; inventory77,
  accepted0; **3 requests, token totals unknown**.
- `BudgetExceeded / StreamTerminal / OutputBytes`, retryNone, toolFailure null.
  This identifies the262,144-byte cumulative Worker output bound, not a cumulative
  run token/request cap and not which content categories consumed it.
- Root assigned source-free accounting assessment: quantify real-SDK reasoning
  fragment serialization versus semantic UTF8, chunking/Unicode invariance, final
  text/call arguments/tool-result contributions. No limit increase, reasoning
  exclusion, production edit or paid retry before an evidence-backed decision.
- Earlier partial acceptedCL database/keys remain retained; no successful publication.

## Byte-accounting correction decision

Reviewed test-only `d3240f85` integrated as `723f6fce`. Lane controlled164/164 GREEN
proves chunk-envelope inflation: identical7,500-byte ASCII reasoning plus413-byte
finaltext costs8,012 bytes in one delta versus750,413 in7,500 deltas, both below
the existing update-count cap. Chinese/emoji comparisons likewise reproduce it.
This proves a defect, not the last live run's exact byte contributors.

Integrator approved strictUTF8 charging for recognized text/reasoning while keeping
the same262,144-byte file-global cap. Opaque ProtectedData, payload-bearing metadata
on both text types, executable arguments and full toolresults remain charged.
Informational calls remain non-executable but their serialized payload counts;
extensible usage metadata counts while fixednumericusage retains its existing role.
Unknown/unmeasurable payloads remain bounded or failclosed. Measurement itself must
be bounded; no arbitrary large temporary serialization/newgeneralframework.

No update/input/provider-token/tool/lifetime/settlement limit, retry, acceptance or
authority change. Executor helpers/existingtests/assessment only, separate owners,
RED/GREEN and independent review. Paid/native/PG/source-root operations remain held
until root accepts a reviewed implementation and decides qualification.

## Accounting implementation and integration verification

Reviewed `27df8f7f` integrated as `9df504a6`. Only Worker executor, its existing tests
and assessment changed. Recognized text/reasoning/ProtectedData uses strictUTF8;
metadata, informational calls, executablearguments/results and extensibleusage
remain bounded. Unknown/unmeasurable types failclosed; no cap/providerpolicy change.
Measurement itself uses bounded storage and rejects unsupported custom converters.

Main same allOctoberScan SelfContained selection **727/727**, zero skips, isolation
`7d0758fe750f4e9e960b83375141758a`; build0errors. This is not whole-project GREEN.
Lane broader run `f60d5b3cbab2443b888176ebc1ae846d` was726/727, failing unchanged
BusinessTransport old-rate-reservation expected2HTTP/actual1. An isolated1/1 and
main727/727 do not erase/explain it. Scan is assigned bounded pre-change baseline
assessment; source shows virtual60s steps with10ms scheduling windows around a120s
deadline, a hypothesis not yet proved for the recordedfailure. No test weakening,
Businessfix or paidretry while that disposition is pending. PartialCL remains intact.

## Baseline disposition and CodeGraph result-admission failure

Two unchanged parent fullOctoberScan runs passed704/704 (f73f76a139a046cdb0721acf7466f960,
c5782778eda94776b1f0522eb152d555). Current727 includes23added accountingcases. Failed
Business test, ResolutionExecutor, scope and clock helpers are unchanged. Attribution
of lane726/727 remains inconclusive; preserve that intermittent concern. Root allowed
one qualification based on reviewed correction/main727/727, not a regression waiver.

Fresh `phone-c0c810df49ae`, batch `cdca79a6-12a7-4c52-978f-174e4e126e99`, database
`vulgata_oct_pilot_8217b6076edc401083bcd59d4209160b`:

- **49.12s wall /46.90s pipeline**, exit1, no timeout/cleanup doubt; inventory77,
  accepted0; **2requests,7,556input/1,528output tokens**, usageknown.
- `ProviderFailed / ToolInvocation / None`; observed tool `CodeGraphExplore`,
  boundary `ResultAdmission`, actual returned `codeGraphStatus: Complete`. Other
  typed statuses/settlement unknown(null). This does not prove payload validity,
  but distinguishes this failure from JDT startup/provider transport/tool readiness.
- Paid runs held. Scan owns source-free controlled Complete-result-to-ledger
  assessment, preserving authority/currentness/provenance. If real native envelope
  observation is necessary, propose a bounded source-local no-LLM probe for root
  before any resource action; do not perform another paid diagnostic loop.
- Prior partialCL database/keys remain preserved and unpublished. All77 hashes
  matched before this run; no sharedcontainer/database mutation or newcontainer.

## CodeGraph excerpt representation correction decision

Reviewed test-only `febbb239` accepted, lane193/193 controlled GREEN. The actual
contained adapter parser verifies/returns LF excerpts against normalized source;
ledger rawContains rejects the same excerpt on unchangedCRLF source. LF control
is allowed. This is a concrete producer/consumer mismatch, not proven attribution
of the latest live payload. Other legitimate consumer denials remain necessary.

Integrator approved only ordinal CRLF→LF comparison alignment on both values in
the Scan ledger. Raw retainedsource/hash/identity stay unchanged; all nonempty,
path/range/size/distinctfile/currentness/provenance/authorization checks remain.
No arbitrary whitespace trimming, bareCR/Unicode/case normalization or blanket
Complete admission. Existing tests/assessment plus ledger comparison only, separate
RED/GREEN and independent review. No new diagnostic/native probe or paid run in
this dispatch; root owns later qualification. PriorpartialCL remains retained.

## CRLF qualification and remaining parser observation

Reviewed `877fb8a4` integrated as `cb5008c3`; mainaffected202/202, no skips, isolation
`82e6065d6b2540bd8650f71dac5d0ef0`. Fresh `phone-7743a9fb013b`, batch
`9f9e4027-daca-49df-86b3-21874dfb1a38`, database
`vulgata_oct_pilot_cfcad84511c04afc8c1952daedef8c47`:

- **56.30s wall /54.13s pipeline**, exit1, no timeout/cleanup doubt.
- Inventory77,accepted0; **2requests,7,883input/4,037output tokens**, usageknown.
- `InvalidOutput / ProposalParse / None`, no terminal/budget/tool failure.
  Exact rejected shape remains unknown; this does not prove prior rejection'scause.

After the completed parser/SDK/tool matrix and contract correction, Integrator
approved the previously proposed minimal fixed parser rejectioncategory/schema-slot
observation. No dynamic keys/values/indices/paths/exceptiontext/responsebody. Preserve
parser acceptance/status, distinguish later validation and existing disclosurefences.
Existingtests, separateowners and independentreview; no repair/retry/schema change.
Paid/native/PG/source-root operations held pending root integration decision.

## Parser diagnostic qualification

Reviewed `268c5aea` integrated as `52fa489b`; main221/221 affected controlled tests
passed, no skips (isolation `4a2bb3b23dec470a9a58a810180290fb`). Fresh
`phone-64d5fbe0a2b9`, batch `f54f2248-f539-4b85-8002-c376dd7e9980`, database
`vulgata_oct_pilot_8317b9d466a04945afa27707a11751cb`:

- **54.94s wall /52.75s pipeline**, exit1, no timeout/cleanup doubt.
- Inventory77,accepted0; **3requests,15,195input/3,696output**, usageknown.
- `InvalidOutput / ProposalParse / None`, **parseFailure Syntax/Root**; other
  diagnostic reasons null. This identifies the JSONparse boundary, not the response
  contents: empty/fenced/malformed/depth-exceeded hypotheses remain unproved.
- Paidruns held. Scan authorized source-free actualSDK/MAF outboundJSON-mode,
  post-tool finalcall and final-text/depth assessment. Return one evidence-backed
  proposal/minimalmissingobservation; no repair/stripfences/schema/status change or
  raw response capture in current scope. Existing partialCL remains preserved.

## Wire assessment and privileged observation decision pending

Reviewed test-only `3a17cd1a` accepted, lane199/199 controlled GREEN. Actual factory/
MAF/SDK controls keep exact JSONmode on initial/posttool requests and parse only
the finalleaf. Empty/fenced/prose anddepth33 inputs all explain the broadSyntax/Root
classification in controls; none is established as the real rejected response.
No wire/assembly productionfix is justified by current evidence.

Root declined another counts-only diagnostic layer and is asking the user for
explicit approval of ONE bounded protected local capture/inspection of the exact
failed finalproposal, outsideGit; no reasoning/request/credential capture, sanitized
diagnosis only retained, rawcapture deleted after diagnosis. This is a proposal,
not runtime/capture authority. No instrumentation, paid/native/PG operation, output
repair/normalization or depth increase before scoped decision. Scan remains idle.

## User approval: one privileged final-proposal observation

The user subsequently explicitly replied **approve** to one diagnostic run capturing
only the failing finalproposal up to256KiB in protectedlocal storage outsideGit.
Exclude reasoning, requests and credentials. Root inspects privately, retains only
a sanitizeddiagnosis and deletes the rawcapture afterward. No retryseries or raw
output publication is authorized by this grant.

Scan is dispatched to implement a minimal default-off, explicitly run-bound injected
capture seam; trusted Generator composition owns the local sink, not arbitrary
filesystem access in AgentExecution. At mostone capture, strictUTF8 bound, no
misleading truncation, CreateNew/nooverwrite and protected/non-reparse destination.
Fresh source/authority checks gate capture; revocation/cancellation/changedsource
cannot disclose. Original parser/status/acceptance/limits unchanged; failures must
not mask Workerfailure or claim successfulcapture. No rawcontent in standardreports.

Syntheticcontrols, separateowners and independentreview precede rootintegration and
the one paidrun. The lane has no realprovider/native/PG/source-root/capture authority.
Existing retained partialCL and sharedcontainer remain untouched. Root config/paths
stay outsideGit; no outputrepair or modelresponse normalization is approved.

## Approved protected observation completed (October 10)

Reviewed capture checkpoint `9f1ee796` integrated as `1f4f4ecf`. Root built current
sources and passed **321/321** affected tests, zero failures/skips, isolation
`bdf45ce41cd2482a9a668eabe69b4449`. This is affected-test GREEN, not full-suite GREEN.

One explicitly opted-in Generate used fresh run `phone-b6a9aec9fb24`, batch
`577bbf61-e168-4c31-b559-8ee6e87dec9f`, owned database
`vulgata_oct_pilot_50386ea33a534ea39fe5b78a4c33b5f3` in the existing shared PG17
container. Preparation passed. Generate exited1 after **52.59s wall /50.39s pipeline**,
without timeout or cleanup doubt. Inventory77, accepted0, no publication or BL.
Usage was known: **3 requests,11,599 input and3,367 output tokens**.
Safe report: `InvalidOutput / ProposalParse / Syntax / Root`, capture `Captured`;
no terminal, budget or tool failure. Local report identity:
`generate-3833177011de44d09874896b33f6de12`.

Private bounded inspection found 5,412 strictUTF8 bytes beginning with an object,
no Markdown fence, failing JSON at EOF (line0,byte5412) at both depth32 and128.
Lexical inspection found no unfinished string or mismatched closing delimiter;
exactly one array and one object remained open. Adding their two closing delimiters
in memory made syntax valid. This was diagnosis only, not schema/semantic validation,
repair, acceptance or persistence. The exact owned raw file was deleted afterward;
absence verified. No raw content, dynamic fields, reasoning or credentials retained
in this evidence. Capture opt-in was child-process-local and an external CreateNew
invocation marker prevents accidental reuse of that launcher grant.

This establishes incomplete final JSON, not its causal origin. Existing terminal
guards require an accepted Stop before proposal parsing; token-length termination
is not proven. Scan received only sanitized facts and is authorized source-free
synthetic/SDK assembly diagnosis and a bounded remediation proposal, not production
repair or further paid/capture/native/database runs. The earlier accepted partial
CL/database and shared container/volume remain untouched. No presentation-ready
dataset, ordinary-user read, BL, Chat or graph success is claimed for this run.

## Approved complete-proposal prompt trial

The user explicitly approved the narrow final-window prompt clarification and
one fresh-database trial, without raw capture. Reviewed source-free suffix tests
`0f3cedd8` integrated as `8af27960`: validly ordered closing deltas survive the
actual SDK/Worker path; no application suffix-loss defect was reproduced. Scan
owns only the prompt clarification and controlled verification/review. Root owns
integration and the one live trial. Strict acceptance, full-file coverage, evidence,
budgets and retry behavior remain unchanged; no bracket repair or automatic series.

All77 source hashes still match. Prior diagnostic-owned CodeGraph state was moved
outside the prepared roots into its owned run archive after boundary/reparse checks.
Fresh production-API bootstrap created run `phone-ea740448c019`, batch
`dcf7f41e-be52-4439-9490-99ab7d58de38`, database
`vulgata_oct_pilot_9d483f1b39ad41b0b0f2ad40abbcfd61` in the existing PG17 container.
The temporary bootstrap Web stopped. No Generate/provider request has occurred
at this checkpoint; it awaits the reviewed prompt and current-source verification.

### Complete-proposal trial outcome

Reviewed prompt checkpoint `3fc0951b` integrated as `9213d69a`. Main rebuilt current
sources and passed **330/330**, no failures/skips, build zero errors, isolation
`c61af7651d1d4769b29998eefc914d99`. The prompt change remains a mitigation attempt,
not a demonstrated fix. No parser, transport, schema, retry or limit changed.

The approved single trial in `phone-ea740448c019` exited1 after **27.35s wall /
24.61s pipeline**, no timeout or cleanup doubt. Inventory77, accepted0, Documents0,
publication not requested; matching and BL did not start. Known usage:
**1 request,3,021 input/3,002 output tokens**. Safe Worker diagnosis remained
`InvalidOutput / ProposalParse / Syntax / Root`, retry `None`; terminal, budget
and tool failures were null. Capture status was null and no capture directory
existed. Local report: `generate-30ac88fa6a2446f08c1028c7a54a170c`.

The clarification did not produce accepted output in this trial. Without raw
capture, this does not prove the same missing-delimiter shape or its cause.
No further paid run is launched under the one-trial grant. Scan received sanitized
results and is asked only for the next discriminating experiment recommendation;
production edits, further capture and native/provider/PG activity remain held.
Earlier retained partial CL and shared resources are preserved. No retained
presentation publication, ordinary-user read, useful BL, Chat or graph qualification
is claimed for this attempt.

## Two approved synthetic provider probes (October 10)

User explicitly approved two diagnostic requests to check thinking/output limits
and premature termination. Root ran exactly one streaming and one non-streaming
request against the existing approved DeepSeek endpoint/model/credential. Public
synthetic request only: a two-field JSON object; no source, tools, Worker, database,
native tools or generation dataset. Common requested output cap8192, timeout120s,
SDK retries0, no application retry. Local one-use invocation guard consumed.

The external temporary probe referenced the installed SDK assemblies; no product
code changed. HTTP response bodies were bounded to1MiB, buffered only in memory,
then parsed by the SDK. Only fixed sanitized outcomes were printed. This tests
HTTP framing/text mapping, not live streaming latency or the full Worker/MAF path.
No raw body, reasoning text, headers or credentials were persisted. Both processes'
response memory ended with the probe process. No provider failure or timeout.

| Observation | Streaming | Non-streaming |
| --- | --- | --- |
| Elapsed seconds |0.795|0.761|
| HTTP / reported finish |200 / stop|200 / stop|
| Input / completion tokens |73 /41|73 /57|
| Reasoning tokens within completion |31|47|
| Valid expected JSON at HTTP and SDK |yes|yes|
| HTTP text equals SDK text |yes|yes|
| DONE marker |present|not applicable|
| Content events after finish / DONE |0 /0|not applicable|

Both requests omitted explicit thinking/effort and stop parameters; reasoning was
present. The observed reasoning token accounting is consistent with thinking
consuming the output budget. Neither response approached the requested cap or
showed premature termination. These two small successful samples do not explain
the previous complex Worker failures or establish general provider reliability.

Important unresolved compatibility detail: the installed SDK serialized the cap
as `max_completion_tokens:8192`, whereas current official DeepSeek Chat Completions
documentation describes `max_tokens`. The provider accepted the request, but these
small outputs cannot prove the alias is enforced. Root asked Scan for source-free
verification of the actual Worker adapter's serialized field; no cap/transport
change or additional live probe is authorized by this completed two-call grant.

References: https://api-docs.deepseek.com/api/create-chat-completion/ (completion
usage breakdown, max_tokens and finish semantics),
https://api-docs.deepseek.com/guides/thinking_mode/ (thinking enabled by default).
No retained Knowledge qualification or repair of the earlier failure is claimed.

## DeepSeek cap adapter and enforcement comparison

Root verified the direct human instruction in OCT-SCAN turn
`01a1239e-251d-7090-9fd7-7928e1c69622`: implement a simple compatibility adapter
and test whether max_tokens improves behavior. Reviewed characterization
`d4f4afa9` integrated as `833a05c1`; reviewed adapter `6734662d` as `299227de`.
The adapter changes only the cap key for the exact supported DeepSeek base URI;
the admitted number, thinking, tools, format, parser, retries and other endpoints
are unchanged. Lane controlled342/342 passed; root verification is recorded below
once complete. No new source Generate is implied by this checkpoint.

Root chose two small synthetic streaming calls before any new source run. Both
requested the same complete JSON array of integers1..60, json_object, output cap32,
120-second deadline, retries0, same approved endpoint/model/credential and default
thinking. Only the cap property name differed. The standalone installed-SDK probe
performed key-only translation for the canonical comparison; controlled tests
separately verify the production adapter. In-memory bounded HTTP observation only,
no raw persistence, source, tools, DB or new container. One-use probe guard consumed.

| Measured outcome | max_completion_tokens:32 | max_tokens:32 |
| --- | --- | --- |
| Wall seconds |2.980|0.823|
| HTTP |200|200|
| Input tokens |92|92|
| Completion tokens |502|32|
| Included reasoning tokens |378|32|
| Finish reason |stop|length|
| Expected complete JSON |yes|no|
| DONE / HTTP-to-SDK text equality |yes / yes|yes / yes|

Neither request timed out or had content after termination. The old field did not
enforce32 on the tested deployed endpoint; the documented field did enforce32,
including reasoning. Invalid JSON for the deliberately tiny canonical limit is
expected diagnostic evidence of truncation, not a generated-Knowledge regression.
This proves a cap-compatibility improvement, not that the old Syntax/Root failures
were cap failures: those reached Stop with much smaller output than8192. Correcting
the cap may expose Length for reasoning-heavy tasks; no cap increase or automatic
retry is introduced. No full Worker/source/presentation qualification is claimed.

Root current-source combined verification completed: **342/342** selected affected
SelfContained tests passed, zero failures/skips, isolation
`43279885958c4d50865985b9523ea096`. This is affected-suite GREEN, not whole-project
or full source-generation qualification. Scan received the measured live results.

## Approved corrected-cap source trial

The user explicitly approved ONE fresh-database source-generation trial with the
corrected adapter, unchanged8192 output ceiling/default thinking/strict acceptance,
capture disabled and no automatic retry. At clean `b023e8ef`, all77 prepared source
hashes matched. Previous trial-owned index state was archived outside the source
roots after exact path/non-reparse checks; no source files or prior data changed.

New run `phone-5afd5e5f8d70`, batch `626b757d-58c2-4f73-8325-1b998fbee1d0`, database
`vulgata_oct_pilot_7c5c5336818b4e04b58b519fcca5649f` uses the same approved existing
PG17 container. Bootstrap used production Identity/System/Repository APIs; owned
temporary Web stopped. Prepare passed3.14s. The current verified Generator binary
started the single Generate with existing selected9/supporting68 scope, same
provider/credential, run1800s/file300s/operator120s/drain30s. Local configuration
contains no capture opt-in. Results follow after settlement; startup alone is not
qualification.

### Corrected-cap source trial result

`phone-5afd5e5f8d70` exited1 after **67.45s wall /65.04s pipeline**, no timeout or
cleanup doubt. Known usage **3 requests,17,082 input/3,831 output tokens**. Inventory77,
accepted0, Documents0, no publication/matching/BL. Worker result remained
`InvalidOutput / ProposalParse / Syntax / Root`, retryNone; terminal/budget/tool
failure null. Capture disabled/null, no capture directory. Local safe report
`generate-f386159d217b4aa19f69a78d401102c3`.

The corrected cap enforcement did not solve this source-generation failure.
No Length termination, timeout or actual shape of malformed response was observed
for this trial. No unchanged paid retry is launched. Scan receives the sanitized
outcome and holds further changes/operations. A future discriminating observation
would compare actual failing Worker HTTP final content/framing against SDK text;
it requires a new bounded privacy/runtime decision, not an inferred extension of
the completed capture or single-trial grants. Prior partial CL remains preserved.

## Approved one in-memory Worker comparison

User explicitly approved: bounded in-memory comparison of the failing Worker's
HTTP response and SDK text, ONE diagnostic run, no retained raw responses, and
report the root cause supported by evidence. Root delegated only implementation,
controlled tests and review to Scan; root retains all runtime ownership. Observer
must be default-off/run-bound, bounded and transparent to streaming/acceptance,
retain only finite sanitized facts, and never repair output or mask failure.
No guarantee of attribution beyond the observed HTTP/SDK/Worker boundary is made.

At clean `d57d42e5`, all77 hashes matched. Prior settled tool state archived inside
its owned run after exact path/non-reparse checks. New production-API bootstrap:
run `phone-24c8f0d911f9`, batch `8efbd82b-8a6b-4e2c-8d57-4574979bde20`, database
`vulgata_oct_pilot_6729136baa7e484a94ee21b4dab449db`, same approved PG17 container.
Temporary bootstrap Web stopped; prepare passed3.07s. No Generate/provider request
at this preparation checkpoint; reviewed observation implementation is pending.
Raw-file capture stays disabled. Prior data and original sources remain intact.

### In-memory diagnostic outcome: target failure not reached

Reviewed observation checkpoint `99ce34a2` integrated as `5661bfa2`. Main rebuilt
current sources and passed **382/382** affected SelfContained tests, zero failures/
skips, build zero errors; isolation `f2f876a1e832411e890aa66a61b6073a`.

The ONE approved Generate used the exact prepared run above and process-local
observation opt-in, with a CreateNew one-use guard and raw capture absent. It exited1
after **49.26s wall /46.72s pipeline**, no timeout or cleanup doubt. Inventory77,
accepted0, Documents0, no publication/matching/BL. Known usage: **3 requests,
11,758 input/1,499 output tokens**. Local report:
`generate-aababf895db74885966f25558f766700`.

This run stopped earlier than the intended JSON boundary: `ProviderFailed /
ToolInvocation`, retryNone, toolFailure `SearchText / ResultAdmission /
SourceOutcome:Denied`; other tool details null. Parse failure, capture and HTTP
observation were null. Therefore no HTTP/SDK/parse comparison exists and no cause
of the prior malformed final JSON was established. Null observation is not a match.
No raw-response file was retained; no capture directory exists. Observation raw
references are discarded on the existing failure/disposal path.

Static interpretation: SearchText returned a typed Denied outcome that the input
ledger correctly refuses to admit. This finite outcome alone does not distinguish
invalid source-tool arguments, source authorization/containment, or runtime binding
failure. It is not evidence of a newline-excerpt mismatch, which cannot by itself
explain the reported typed Denied outcome. Root requested source-free diagnosis
from Scan, not another paid run, new capture, admission weakening or production fix.
The single diagnostic grant is consumed; prior retained partial CL remains intact.
